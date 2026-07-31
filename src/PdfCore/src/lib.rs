use std::collections::BTreeMap;
use std::ffi::CStr;
use std::os::raw::c_char;
use std::io::Write;

pub mod text_editor;

use lopdf::{xobject, Dictionary, Document, Object};
use image::ImageEncoder;
use flate2::read::ZlibDecoder;
use std::io::Read;

// Convert C string to Rust string slice safely
fn to_str<'a>(s: *const c_char) -> Option<&'a str> {
    if s.is_null() {
        return None;
    }
    unsafe {
        CStr::from_ptr(s).to_str().ok()
    }
}

pub fn load_pdf_document<P: AsRef<std::path::Path>>(path: P) -> lopdf::Result<Document> {
    let mut bytes = std::fs::read(path).map_err(|e| lopdf::Error::IO(e))?;
    fix_pdf_offsets(&mut bytes);
    Document::load_mem(&bytes)
}


// Helper to get integer value from Object enum safely without method version dependency
fn get_integer(obj: &Object) -> Option<i64> {
    match obj {
        Object::Integer(i) => Some(*i),
        _ => None,
    }
}

fn resolve_object<'a>(doc: &'a Document, obj: &'a Object) -> &'a Object {
    match obj {
        Object::Reference(id) => {
            if let Ok(resolved) = doc.get_object(*id) {
                resolve_object(doc, resolved)
            } else {
                obj
            }
        }
        _ => obj,
    }
}

// Manually decompress FlateDecode (zlib) data
// lopdf's decompressed_content() fails when Filter is an Array ([/FlateDecode])
// This function handles both cases by attempting raw inflate
fn manual_decompress_flate(compressed: &[u8]) -> Option<Vec<u8>> {
    // Try standard zlib (with zlib header 0x78..)
    let mut decoder = ZlibDecoder::new(compressed);
    let mut out = Vec::new();
    if decoder.read_to_end(&mut out).is_ok() && !out.is_empty() {
        return Some(out);
    }
    // Try raw deflate (without zlib header, used by some PDFs)
    let mut decoder2 = flate2::read::DeflateDecoder::new(compressed);
    let mut out2 = Vec::new();
    if decoder2.read_to_end(&mut out2).is_ok() && !out2.is_empty() {
        return Some(out2);
    }
    None
}

fn collect_pages_from_tree(
    doc: &Document,
    pages_id: lopdf::ObjectId,
    pages: &mut Vec<lopdf::ObjectId>,
    visited: &mut std::collections::HashSet<lopdf::ObjectId>,
) {
    if visited.contains(&pages_id) {
        return;
    }
    visited.insert(pages_id);

    if let Ok(dict) = doc.get_object(pages_id).and_then(|obj| obj.as_dict()) {
        let type_name = dict.type_name().unwrap_or("");
        if type_name == "Page" {
            pages.push(pages_id);
        } else if type_name == "Pages" {
            if let Ok(kids_val) = dict.get(b"Kids") {
                if let Ok(kids_arr) = kids_val.as_array() {
                    for kid in kids_arr {
                        if let Ok(kid_ref) = kid.as_reference() {
                            collect_pages_from_tree(doc, kid_ref, pages, visited);
                        }
                    }
                }
            }
        } else if dict.get(b"Kids").is_ok() {
            if let Ok(kids_val) = dict.get(b"Kids") {
                if let Ok(kids_arr) = kids_val.as_array() {
                    for kid in kids_arr {
                        if let Ok(kid_ref) = kid.as_reference() {
                            collect_pages_from_tree(doc, kid_ref, pages, visited);
                        }
                    }
                }
            }
        } else if dict.get(b"Parent").is_ok() {
            pages.push(pages_id);
        }
    }
}

fn log_debug(output_path: Option<&str>, message: &str) {
    let temp_log = std::env::temp_dir().join("pdfpro_merge_debug.log");
    if let Ok(mut file) = std::fs::OpenOptions::new().create(true).write(true).append(true).open(&temp_log) {
        let _ = writeln!(file, "{}", message);
    }
    if let Some(out_p) = output_path {
        let path = std::path::Path::new(out_p);
        if let Some(parent) = path.parent() {
            let log_path = parent.join("pdfpro_merge_debug.log");
            if let Ok(mut file) = std::fs::OpenOptions::new().create(true).write(true).append(true).open(&log_path) {
                let _ = writeln!(file, "{}", message);
            }
        }
    }
}

#[no_mangle]
pub extern "C" fn merge_pdfs(paths_semicolon: *const c_char, output_path: *const c_char) -> bool {
    merge_pdfs_with_progress(paths_semicolon, output_path, None)
}

#[no_mangle]
pub extern "C" fn merge_pdfs_with_progress(
    paths_semicolon: *const c_char,
    output_path: *const c_char,
    progress_callback: Option<extern "C" fn(u32, u32)>,
) -> bool {
    let paths_str = match to_str(paths_semicolon) {
        Some(s) => s,
        None => return false,
    };
    let output_str = match to_str(output_path) {
        Some(s) => s,
        None => return false,
    };

    // Clear old log files
    let temp_log = std::env::temp_dir().join("pdfpro_merge_debug.log");
    let _ = std::fs::remove_file(&temp_log);
    if let Some(parent) = std::path::Path::new(output_str).parent() {
        let _ = std::fs::remove_file(parent.join("pdfpro_merge_debug.log"));
    }

    log_debug(Some(output_str), "=== MERGE START ===");
    log_debug(Some(output_str), &format!("Output path: {}", output_str));
    log_debug(Some(output_str), &format!("Input paths raw string: {}", paths_str));

    let paths: Vec<&str> = paths_str.split(';').filter(|s| !s.is_empty()).collect();
    log_debug(Some(output_str), &format!("Parsed {} input files", paths.len()));
    if paths.is_empty() {
        log_debug(Some(output_str), "Error: No input paths parsed.");
        return false;
    }

    let mut target_doc = Document::with_version("1.5");
    let mut documents = Vec::new();

    // Load documents
    for (idx, path) in paths.iter().enumerate() {
        log_debug(Some(output_str), &format!("Loading file {}: {}", idx + 1, path));
        match load_pdf_document(path) {
            Ok(doc) => {
                log_debug(Some(output_str), &format!("Loaded file {} successfully. Version: {}, Object count: {}", idx + 1, doc.version, doc.objects.len()));
                documents.push(doc);
            }
            Err(e) => {
                log_debug(Some(output_str), &format!("Error loading file {}: {:?}", idx + 1, e));
                return false;
            }
        }
    }

    let total_files = documents.len() as u32;
    if let Some(cb) = progress_callback {
        cb(0, total_files);
    }

    let mut max_id = 1;
    let mut pages_kids = Vec::new();
    let mut target_objects = BTreeMap::new();

    for (i, mut doc) in documents.into_iter().enumerate() {
        log_debug(Some(output_str), &format!("--- Processing document {} ---", i + 1));
        // Resolve Catalog and Pages root IDs using trailer first (before renumbering)
        let mut catalog_id = doc.trailer.get(b"Root").and_then(|obj| obj.as_reference()).ok();
        let mut pages_id = None;
        if let Some(cat_id) = catalog_id {
            if let Ok(cat_dict) = doc.get_object(cat_id).and_then(|obj| obj.as_dict()) {
                pages_id = cat_dict.get(b"Pages").and_then(|obj| obj.as_reference()).ok();
            }
        }

        log_debug(Some(output_str), &format!("Catalog ID (from trailer): {:?}", catalog_id));
        log_debug(Some(output_str), &format!("Pages ID (from trailer Catalog): {:?}", pages_id));

        // Fallback search if trailer is missing Root or Pages
        if catalog_id.is_none() || pages_id.is_none() {
            log_debug(Some(output_str), "Catalog ID or Pages ID not found from trailer. Starting fallback scan...");
            for (id, object) in doc.objects.iter() {
                if let Ok(dict) = object.as_dict() {
                    let type_name = dict.type_name().unwrap_or("");
                    if type_name == "Catalog" && catalog_id.is_none() {
                        catalog_id = Some(*id);
                        log_debug(Some(output_str), &format!("Found Catalog ID via fallback scan: {:?}", catalog_id));
                    } else if type_name == "Pages" && pages_id.is_none() {
                        pages_id = Some(*id);
                        log_debug(Some(output_str), &format!("Found Pages ID via fallback scan: {:?}", pages_id));
                    }
                }
            }
        }

        // Collect all leaf Page objects BEFORE renumbering using multiple robust strategies
        let mut pages = Vec::new();
        let mut visited = std::collections::HashSet::new();
        if let Some(p_id) = pages_id {
            log_debug(Some(output_str), "Running collect_pages_from_tree...");
            collect_pages_from_tree(&doc, p_id, &mut pages, &mut visited);
        }

        log_debug(Some(output_str), &format!("collect_pages_from_tree found {} pages", pages.len()));

        if pages.is_empty() {
            log_debug(Some(output_str), "collect_pages_from_tree returned 0 pages. Trying doc.get_pages() fallback...");
            for (_num, id) in doc.get_pages() {
                pages.push(id);
            }
            log_debug(Some(output_str), &format!("doc.get_pages() fallback found {} pages", pages.len()));
        }

        if pages.is_empty() {
            log_debug(Some(output_str), "doc.get_pages() fallback returned 0 pages. Trying direct scan fallback...");
            for (id, object) in doc.objects.iter() {
                if let Ok(dict) = object.as_dict() {
                    if dict.type_name().unwrap_or("") == "Page" {
                        pages.push(*id);
                    }
                }
            }
            log_debug(Some(output_str), &format!("Direct scan fallback found {} pages", pages.len()));
        }

        log_debug(Some(output_str), &format!("Collected pages final list: {:?}", pages));

        // Get sorted keys of doc.objects before renumbering
        let keys: Vec<lopdf::ObjectId> = doc.objects.keys().cloned().collect();

        // Renumber objects
        let start_renumber_id = max_id;
        doc.renumber_objects_with(max_id);
        log_debug(Some(output_str), &format!("Renumbered objects starting from {}. Document max_id is now {}.", start_renumber_id, doc.max_id));

        // Map original page IDs to new renumbered page IDs
        let mut mapped_count = 0;
        for page_id in pages {
            if let Ok(idx) = keys.binary_search(&page_id) {
                let new_page_id = (start_renumber_id + idx as u32, 0);
                pages_kids.push(Object::Reference(new_page_id));
                mapped_count += 1;
            } else {
                log_debug(Some(output_str), &format!("Warning: Could not find page ID {:?} in original objects keys!", page_id));
            }
        }
        log_debug(Some(output_str), &format!("Mapped {} page IDs successfully. Total kids so far: {}", mapped_count, pages_kids.len()));

        // Map catalog and pages root to their new renumbered IDs
        let new_catalog_id = catalog_id.and_then(|orig_id| {
            keys.binary_search(&orig_id).ok().map(|idx| (start_renumber_id + idx as u32, 0))
        });
        let new_pages_id = pages_id.and_then(|orig_id| {
            keys.binary_search(&orig_id).ok().map(|idx| (start_renumber_id + idx as u32, 0))
        });
        log_debug(Some(output_str), &format!("New Catalog ID: {:?}", new_catalog_id));
        log_debug(Some(output_str), &format!("New Pages ID: {:?}", new_pages_id));

        // Add all objects to the target dictionary, except catalog and root pages
        let mut copied_objects = 0;
        for (id, object) in doc.objects {
            if Some(id) != new_catalog_id && Some(id) != new_pages_id {
                target_objects.insert(id, object);
                copied_objects += 1;
            }
        }
        log_debug(Some(output_str), &format!("Copied {} objects to target", copied_objects));

        max_id = doc.max_id + 1;

        if let Some(cb) = progress_callback {
            cb((i + 1) as u32, total_files);
        }
    }

    log_debug(Some(output_str), "--- Finalizing merged document ---");
    log_debug(Some(output_str), &format!("Total pages collected in kids: {}", pages_kids.len()));

    // Create the Pages dictionary
    let pages_id = (max_id, 0);
    max_id += 1;

    let mut pages_dict = Dictionary::new();
    pages_dict.set("Type", Object::Name("Pages".as_bytes().to_vec()));
    pages_dict.set("Count", Object::Integer(pages_kids.len() as i64));
    pages_dict.set("Kids", Object::Array(pages_kids.clone()));
    target_objects.insert(pages_id, Object::Dictionary(pages_dict));

    // Create Catalog dictionary
    let catalog_id = (max_id, 0);
    let mut catalog_dict = Dictionary::new();
    catalog_dict.set("Type", Object::Name("Catalog".as_bytes().to_vec()));
    catalog_dict.set("Pages", Object::Reference(pages_id));
    target_objects.insert(catalog_id, Object::Dictionary(catalog_dict));

    target_doc.objects = target_objects;
    target_doc.trailer.set("Root", Object::Reference(catalog_id));
    target_doc.max_id = max_id;

    // Adjust parent reference of all kids pages
    let mut adjusted_parents = 0;
    for kid in &pages_kids {
        if let Ok(ref_id) = kid.as_reference() {
            if let Ok(Object::Dictionary(ref mut kid_dict)) = target_doc.get_object_mut(ref_id) {
                kid_dict.set("Parent", Object::Reference(pages_id));
                adjusted_parents += 1;
            }
        }
    }
    log_debug(Some(output_str), &format!("Adjusted parent reference for {}/{} page objects", adjusted_parents, pages_kids.len()));

    log_debug(Some(output_str), &format!("Saving merged PDF to {}...", output_str));
    let save_success = target_doc.save(output_str).is_ok();
    log_debug(Some(output_str), &format!("Merge finished. Save status: {}", save_success));
    save_success
}


#[no_mangle]
pub extern "C" fn rotate_pdf_page(
    pdf_path: *const c_char,
    page_number: i32,
    rotation_delta: i32,
    output_path: *const c_char,
) -> bool {
    let pdf_str = match to_str(pdf_path) {
        Some(s) => s,
        None => return false,
    };
    let output_str = match to_str(output_path) {
        Some(s) => s,
        None => return false,
    };

    let mut doc = match load_pdf_document(pdf_str) {
        Ok(d) => d,
        Err(_) => return false,
    };

    let pages = doc.get_pages();
    let page_id = match pages.get(&(page_number as u32)) {
        Some(&id) => id,
        None => return false,
    };

    if let Ok(Object::Dictionary(ref mut dict)) = doc.get_object_mut(page_id) {
        let current_rotation = dict
            .get(b"Rotate")
            .ok()
            .and_then(|obj| get_integer(obj))
            .unwrap_or(0);
        let new_rotation = (current_rotation + rotation_delta as i64).rem_euclid(360);
        dict.set("Rotate", Object::Integer(new_rotation));
    } else {
        return false;
    }

    doc.save(output_str).is_ok()
}

#[no_mangle]
pub extern "C" fn replace_pdf_text(
    pdf_path: *const c_char,
    page_number: i32,
    original_text: *const c_char,
    replacement_text: *const c_char,
    output_path: *const c_char,
) -> bool {
    let pdf_str = match to_str(pdf_path) {
        Some(s) => s,
        None => return false,
    };
    let original_str = match to_str(original_text) {
        Some(s) => s,
        None => return false,
    };
    let replacement_str = match to_str(replacement_text) {
        Some(s) => s,
        None => return false,
    };
    let output_str = match to_str(output_path) {
        Some(s) => s,
        None => return false,
    };

    if page_number <= 0 {
        return false;
    }

    let mut doc = match load_pdf_document(pdf_str) {
        Ok(d) => d,
        Err(_) => return false,
    };

    if doc
        .replace_text(page_number as u32, original_str, replacement_str)
        .is_err()
    {
        return false;
    }

    doc.save(output_str).is_ok()
}

/// Lấy danh sách các content stream (đã decompress) của một trang.
/// Hỗ trợ cả Contents là Reference (1 stream) và Array (nhiều stream).
fn get_page_content_streams(
    doc: &Document,
    page_id: lopdf::ObjectId,
) -> Option<Vec<Vec<u8>>> {
    let page = doc.get_object(page_id).ok()?;
    let page_dict = page.as_dict().ok()?;
    let contents = page_dict.get(b"Contents").ok()?;

    let mut streams: Vec<Vec<u8>> = Vec::new();
    match contents {
        Object::Reference(id) => {
            if let Ok(Object::Stream(stream)) = doc.get_object(*id) {
                streams.push(decompress_stream(doc, stream));
            }
        }
        Object::Array(arr) => {
            for item in arr {
                if let Ok(id) = item.as_reference() {
                    if let Ok(Object::Stream(stream)) = doc.get_object(id) {
                        streams.push(decompress_stream(doc, stream));
                    }
                }
            }
        }
        Object::Stream(stream) => {
            streams.push(decompress_stream(doc, stream));
        }
        _ => {}
    }
    if streams.is_empty() {
        None
    } else {
        Some(streams)
    }
}

/// Decompress một stream (FlateDecode / raw deflate), fallback giữ raw bytes.
fn decompress_stream(_doc: &Document, stream: &lopdf::Stream) -> Vec<u8> {
    if let Ok(dec) = stream.decompressed_content() {
        if !dec.is_empty() {
            return dec;
        }
    }
    manual_decompress_flate(&stream.content).unwrap_or_else(|| stream.content.clone())
}

/// Ghi lại các content stream đã sửa vào trang (compress lại).
/// Hỗ trợ cả Contents là Reference, Array, hay Stream inline.
fn set_page_content_streams(
    doc: &mut Document,
    page_id: lopdf::ObjectId,
    streams: Vec<Vec<u8>>,
) -> bool {
    // Luôn tạo stream object MỚI (đã nén) và gán vào page dict.
    let mut new_refs: Vec<Object> = Vec::with_capacity(streams.len());
    for data in streams {
        let mut sd = Dictionary::new();
        sd.set("Filter", Object::Name(b"FlateDecode".to_vec()));
        let mut s = lopdf::Stream::new(sd, data);
        let _ = s.compress();
        let id = doc.add_object(Object::Stream(s));
        new_refs.push(Object::Reference(id));
    }

    if let Ok(Object::Dictionary(ref mut pd)) = doc.get_object_mut(page_id) {
        if new_refs.len() == 1 {
            pd.set("Contents", new_refs.into_iter().next().unwrap());
        } else {
            pd.set("Contents", Object::Array(new_refs));
        }
        true
    } else {
        false
    }
}

/// Escape chuỗi hiển thị PDF: \(( \) \\.
fn escape_pdf_string(s: &str) -> String {
    let mut out = String::with_capacity(s.len() + 2);
    for c in s.chars() {
        match c {
            '(' => out.push_str("\\("),
            ')' => out.push_str("\\)"),
            '\\' => out.push_str("\\\\"),
            _ => out.push(c),
        }
    }
    out
}

/// Encode một chuỗi Unicode thành chuỗi hex CID (mỗi codepoint 4 chữ số hex).
/// Dùng cho font CID (Identity-H) khi CID == Unicode codepoint.
fn encode_cid_hex(s: &str) -> String {
    let mut out = String::with_capacity(s.len() * 4 + 2);
    out.push('<');
    for c in s.chars() {
        out.push_str(&format!("{:04X}", c as u32));
    }
    out.push('>');
    out
}

/// Decode một chuỗi hex CID thành Unicode (mỗi 4 hex = 1 codepoint).
fn decode_cid_hex(hex: &str) -> String {
    let h: String = hex.chars().filter(|c| !c.is_whitespace()).collect();
    let h = h.trim_start_matches('<').trim_end_matches('>');
    let mut out = String::new();
    let mut i = 0;
    let chars: Vec<char> = h.chars().collect();
    while i + 4 <= chars.len() {
        let s: String = chars[i..i + 4].iter().collect();
        if let Ok(cp) = u32::from_str_radix(&s, 16) {
            if let Some(ch) = char::from_u32(cp) {
                out.push(ch);
            }
        }
        i += 4;
    }
    out
}

/// Tạo payload text thay thế GIỮ NGUYÊN kiểu encoding của gốc.
/// - is_hex = true  -> font CID (Identity-H): mã hóa thành chuỗi hex CID <...>
///   (mỗi codepoint Unicode == CID, đúng với Identity-H).
/// - is_hex = false -> literal string (...): escape (, ), \ theo PDF.
/// TUYỆT ĐỐI không đổi kiểu: hex không bao giờ thành literal và ngược lại,
/// vì đổi kiểu = đổi font/size/encoding -> người dùng phát hiện ra.
fn build_payload(replacement: &str, is_hex: bool) -> String {
    if is_hex {
        encode_cid_hex(replacement)
    } else {
        // Literal string: KEEP WinAnsi encoding of the original font.
        // Encode each Unicode char back to its WinAnsi byte, then escape PDF specials.
        // This is critical for Vietnamese: writing UTF-8 bytes would corrupt the font.
        let mut body = String::new();
        for c in replacement.chars() {
            match unicode_to_winansi_byte(c) {
                Some(b) => {
                    let ch = b as char;
                    match ch {
                        '(' => body.push_str("\\("),
                        ')' => body.push_str("\\)"),
                        '\\' => body.push_str("\\\\"),
                        _ => body.push(ch),
                    }
                }
                None => {
                    // Ký tự không có trong WinAnsi: giữ nguyên escaped (fallback an toàn)
                    body.push_str(&escape_pdf_string(&c.to_string()));
                }
            }
        }
        let mut s = String::from("(");
        s.push_str(&body);
        s.push(')');
        s
    }
}

/// Decode 1 byte WinAnsi (PDF WinAnsiEncoding, tương đương CP1252 mở rộng)
/// thành 1 codepoint Unicode. Dùng để so khớp text tiếng Việt với chuỗi
/// literal (...) trong content stream (mà Pdfium trả về dạng Unicode).
fn winansi_byte_to_unicode(b: u8) -> char {
    // CP1252: 0x80-0x9F là các ký tự điều khiển đặc biệt, 0xA0-0xFF map trực tiếp.
    const CP1252: &[u16] = &[
        0x20AC, 0x0081, 0x201A, 0x0192, 0x201E, 0x2026, 0x2020, 0x2021,
        0x02C6, 0x2030, 0x0160, 0x2039, 0x0152, 0x008D, 0x017D, 0x008F,
        0x0090, 0x2018, 0x2019, 0x201C, 0x201D, 0x2022, 0x2013, 0x2014,
        0x02DC, 0x2122, 0x0161, 0x203A, 0x0153, 0x009D, 0x017E, 0x0178,
    ];
    if b < 0x80 {
        b as char
    } else if b >= 0x80 && b <= 0x9F {
        let cp = CP1252[(b - 0x80) as usize];
        if cp == 0x0081 || cp == 0x008D || cp == 0x008F || cp == 0x0090 || cp == 0x009D {
            // undefined control -> fallback giữ nguyên byte dưới dạng char an toàn
            b as char
        } else {
            char::from_u32(cp as u32).unwrap_or(b as char)
        }
    } else {
        b as char
    }
}

/// Encode 1 codepoint Unicode thành byte WinAnsi (nếu có thể).
/// Trả về None nếu ký tự không nằm trong WinAnsi (phải báo lỗi / không thay).
fn unicode_to_winansi_byte(c: char) -> Option<u8> {
    let cp = c as u32;
    if cp < 0x80 {
        return Some(cp as u8);
    }
    if cp >= 0xA0 && cp <= 0xFF {
        return Some(cp as u8);
    }
    const CP1252: &[u16] = &[
        0x20AC, 0x0081, 0x201A, 0x0192, 0x201E, 0x2026, 0x2020, 0x2021,
        0x02C6, 0x2030, 0x0160, 0x2039, 0x0152, 0x008D, 0x017D, 0x008F,
        0x0090, 0x2018, 0x2019, 0x201C, 0x201D, 0x2022, 0x2013, 0x2014,
        0x02DC, 0x2122, 0x0161, 0x203A, 0x0153, 0x009D, 0x017E, 0x0178,
    ];
    for (i, &v) in CP1252.iter().enumerate() {
        if v == cp as u16 {
            return Some((0x80 + i) as u8);
        }
    }
    None
}

/// Thay thế chuỗi văn bản trong các toán tử show-text (Tj / TJ) của một content stream,
/// GIỮ NGUYÊN mọi toán tử định dạng (Tf, Tm, Td, T*, màu sắc...).
///
/// Hỗ trợ:
/// - Literal string `(...)` (WinAnsi/Latin1) trong Tj / TJ.
/// - Hex string `<...>` (CID, thường == Unicode với Identity-H) trong Tj / TJ.
/// - TJ array `[ (a) off (b) ]` / `[ <aa> off <bb> ]`: gom các phần tử string thành
///   một chuỗi Unicode để so khớp, khi thay sẽ gộp thành một payload đơn giữ nguyên font.
///
/// Font và kích thước KHÔNG BAO GIỜ bị động vào.
fn replace_text_in_content(content: &[u8], original: &str, replacement: &str) -> Option<Vec<u8>> {
    let text = String::from_utf8_lossy(content);
    let mut result = String::new();
    let mut changed = false;
    let mut i = 0;
    let chars: Vec<char> = text.chars().collect();
    let len = chars.len();

    // Đọc một literal string (...) bắt đầu tại index `start` (start ở '(').
    // Trả về (decoded_unicode, raw_buf_bao_gồm_cả_dấu_ngoặc, end_index_sau_')').
    fn read_literal(chars: &[char], start: usize, len: usize) -> (String, String, usize) {
        let mut depth = 1usize;
        let mut j = start + 1;
        let mut raw = String::from("(");
        let mut buf = String::new();
        while j < len {
            let cc = chars[j];
            if cc == '\\' && j + 1 < len {
                // Giữ nguyên escaped trong raw; decode sang unicode cho so khớp
                raw.push('\\');
                raw.push(chars[j + 1]);
                match chars[j + 1] {
                    '(' => buf.push('('),
                    ')' => buf.push(')'),
                    '\\' => buf.push('\\'),
                    'n' => buf.push('\n'),
                    'r' => buf.push('\r'),
                    't' => buf.push('\t'),
                    o => buf.push(o),
                }
                j += 2;
                continue;
            }
            if cc == '(' {
                depth += 1;
                raw.push(cc);
                buf.push(winansi_byte_to_unicode(cc as u8));
            } else if cc == ')' {
                depth -= 1;
                raw.push(cc);
                if depth == 0 {
                    break;
                }
            } else {
                raw.push(cc);
                buf.push(winansi_byte_to_unicode(cc as u8));
            }
            j += 1;
        }
        (buf, raw, j + 1)
    }

    // Đọc một hex string <...> bắt đầu tại index `start` (start ở '<').
    fn read_hex(chars: &[char], start: usize, len: usize) -> (String, String, usize) {
        let mut j = start + 1;
        let mut raw = String::from("<");
        while j < len {
            let cc = chars[j];
            if cc == '>' {
                raw.push('>');
                break;
            }
            raw.push(cc);
            j += 1;
        }
        let decoded = decode_cid_hex(&raw);
        (decoded, raw, j + 1)
    }

    while i < len {
        let c = chars[i];
        if c == '(' {
            let (decoded, raw, next) = read_literal(&chars, i, len);
            let token_after = peek_token(&chars, next);
            if token_after == "Tj" {
                // Giữ nguyên kiểu encoding của gốc: literal -> literal, hex CID -> hex CID.
                if decoded == original {
                    result.push_str(&build_payload(replacement, false));
                    changed = true;
                } else {
                    result.push_str(&raw);
                }
            } else {
                result.push_str(&raw);
            }
            i = next;
        } else if c == '<' {
            // Có thể là hex string <...> (text CID) hoặc dict << >>.
            // Nếu ký tự tiếp theo cũng '<' -> dict, bỏ qua.
            if i + 1 < len && chars[i + 1] == '<' {
                result.push(c);
                i += 1;
                continue;
            }
            let (decoded, raw, next) = read_hex(&chars, i, len);
            let token_after = peek_token(&chars, next);
            if token_after == "Tj" {
                if decoded == original {
                    // Giữ nguyên kiểu encoding: gốc là hex CID -> thay bằng hex CID.
                    result.push_str(&build_payload(replacement, true));
                    changed = true;
                } else {
                    result.push_str(&raw);
                }
            } else {
                result.push_str(&raw);
            }
            i = next;
        } else if c == '[' {
            // Có thể là TJ array: quét nội dung, gom các string thành 1 chuỗi unicode.
            let mut depth = 1;
            let mut j = i + 1;
            let mut buf = String::new();          // raw array content (giữ nguyên)
            let mut collected: Vec<(String, usize, usize)> = Vec::new(); // (decoded, start_in_buf, end_in_buf)
            while j < len {
                let cc = chars[j];
                if cc == '\\' && j + 1 < len {
                    buf.push('\\');
                    buf.push(chars[j + 1]);
                    j += 2;
                    continue;
                }
                if cc == '(' {
                    let (decoded, raw, next) = read_literal(&chars, j, len);
                    let start = buf.len();
                    buf.push_str(&raw);
                    collected.push((decoded, start, buf.len()));
                    j = next;
                } else if cc == '<' && !(j + 1 < len && chars[j + 1] == '<') {
                    let (decoded, raw, next) = read_hex(&chars, j, len);
                    let start = buf.len();
                    buf.push_str(&raw);
                    collected.push((decoded, start, buf.len()));
                    j = next;
                } else if cc == '[' {
                    depth += 1;
                    buf.push(cc);
                    j += 1;
                } else if cc == ']' {
                    depth -= 1;
                    buf.push(cc);
                    if depth == 0 {
                        break;
                    }
                    j += 1;
                } else {
                    buf.push(cc);
                    j += 1;
                }
            }
            let is_tj = peek_token(&chars, j + 1) == "TJ";
            if is_tj {
                // QUY TẮC GIỮ NGUYÊN CẤU TRÚC:
                // Không bao giờ gộp các phần tử thành 1 payload, không động vào
                // các offset (số nguyên) giữa các phần tử, không đổi kiểu encoding.
                // Chỉ thay nội dung text tại chỗ, giữ nguyên font/size/vị trí gốc.

                // Xác định phần tử (literal hay hex) khớp với original.
                // Ưu tiên: khớp toàn bộ mảng, nếu không thì khớp 1 phần tử đơn lẻ.
                let mut merged = String::new();
                for (decoded, _, _) in &collected {
                    merged.push_str(decoded);
                }

                if merged == original && !collected.is_empty() {
                    // Ghi text mới vào phần tử ĐẦU, xóa nội dung các phần tử SAU
                    // (để rỗng) để giữ nguyên số phần tử & offset → vị trí đầu không đổi.
                    let first_is_hex = collected[0].1 < buf.len()
                        && buf[collected[0].1..].starts_with('<')
                        && !buf[collected[0].1..].starts_with("<<");
                    let repl_payload = build_payload(replacement, first_is_hex);
                    // Thay phần tử đầu
                    let (_, s0, e0) = collected[0];
                    buf.replace_range(s0..e0, &repl_payload);
                    // Xóa nội dung các phần tử còn lại (giữ lại offset số nguyên)
                    for k in 1..collected.len() {
                        let (_, sk, ek) = &collected[k];
                        buf.replace_range(*sk..*ek, "");
                    }
                    changed = true;
                    result.push('[');
                    result.push_str(&buf);
                } else {
                    // Không khớp toàn bộ: thay từng phần tử đơn lẻ == original,
                    // giữ nguyên offset và kiểu encoding của chính phần tử đó.
                    let mut replaced_any = false;
                    for (decoded, start, end) in collected.iter().rev() {
                        if *decoded == original {
                            let is_hex = *start < buf.len()
                                && buf[*start..].starts_with('<')
                                && !buf[*start..].starts_with("<<");
                            let repl = build_payload(replacement, is_hex);
                            buf.replace_range(*start..*end, &repl);
                            changed = true;
                            replaced_any = true;
                            break;
                        }
                    }
                    let _ = replaced_any;
                    result.push('[');
                    result.push_str(&buf);
                }
            } else {
                result.push('[');
                result.push_str(&buf);
            }
            i = j + 1;
        } else {
            result.push(c);
            i += 1;
        }
    }

    if changed {
        Some(result.into_bytes())
    } else {
        None
    }
}

fn peek_token(chars: &[char], start: usize) -> String {
    let mut k = start;
    while k < chars.len() && chars[k].is_whitespace() {
        k += 1;
    }
    let mut tok = String::new();
    while k < chars.len() && !chars[k].is_whitespace() {
        tok.push(chars[k]);
        k += 1;
    }
    tok
}

/// Hàm FFI: thay thế văn bản trên một trang, GIỮ NGUYÊN font và kích thước.
///
/// Hoạt động bằng cách parse content stream, chỉ thay chuỗi literal trong
/// toán tử Tj / TJ, không động đến Tf (font+size), Tm/Td (vị trí) hay màu.
#[no_mangle]
pub extern "C" fn replace_text_in_page(
    pdf_path: *const c_char,
    page_number: i32,
    original_text: *const c_char,
    replacement_text: *const c_char,
    output_path: *const c_char,
) -> bool {
    let pdf_str = match to_str(pdf_path) {
        Some(s) => s,
        None => return false,
    };
    let original_str = match to_str(original_text) {
        Some(s) => s,
        None => return false,
    };
    let replacement_str = match to_str(replacement_text) {
        Some(s) => s,
        None => return false,
    };
    let output_str = match to_str(output_path) {
        Some(s) => s,
        None => return false,
    };

    if page_number <= 0 {
        return false;
    }

    let mut doc = match load_pdf_document(pdf_str) {
        Ok(d) => d,
        Err(_) => return false,
    };

    // Decompress mọi stream object (bao gồm object streams của PDF 1.5+).
    // Bắt buộc để các thay đổi lên content stream được ghi đúng khi save
    // (ngược lại lopdf có thể giữ nguyên object stream cũ và bỏ qua sửa đổi).
    let _ = doc.decompress();

    let pages = doc.get_pages();
    let page_id = match pages.get(&(page_number as u32)) {
        Some(&id) => id,
        None => return false,
    };

    let streams = match get_page_content_streams(&doc, page_id) {
        Some(s) => s,
        None => return false,
    };

    let mut any_changed = false;
    let mut new_streams: Vec<Vec<u8>> = Vec::with_capacity(streams.len());
    for stream in &streams {
        match replace_text_in_content(stream, original_str, replacement_str) {
            Some(modified) => {
                any_changed = true;
                new_streams.push(modified);
            }
            None => new_streams.push(stream.clone()),
        }
    }

    eprintln!("[REPLACE DEBUG] streams.len={} any_changed={}", streams.len(), any_changed);
    if any_changed {
        let concat: Vec<u8> = new_streams.iter().flatten().copied().collect();
        let s = String::from_utf8_lossy(&concat);
        eprintln!("[REPLACE DEBUG] KILOMET in new_streams={} TAKAMI in new_streams={}", s.contains("KILOMET"), s.contains("TAKAMI"));
    }

    if !any_changed {
        // Không tìm thấy chuỗi cần thay -> coi như thất bại (để UI báo)
        return false;
    }

    if !set_page_content_streams(&mut doc, page_id, new_streams) {
        return false;
    }

    doc.save(output_str).is_ok()
}

#[no_mangle]
pub extern "C" fn overlay_pdf_image(
    pdf_path: *const c_char,
    page_number: i32,
    image_path: *const c_char,
    x: f64,
    y: f64,
    width: f64,
    height: f64,
    output_path: *const c_char,
) -> bool {
    let pdf_str = match to_str(pdf_path) {
        Some(s) => s,
        None => return false,
    };
    let image_str = match to_str(image_path) {
        Some(s) => s,
        None => return false,
    };
    let output_str = match to_str(output_path) {
        Some(s) => s,
        None => return false,
    };

    if page_number <= 0 || width <= 0.0 || height <= 0.0 {
        return false;
    }

    let mut doc = match load_pdf_document(pdf_str) {
        Ok(d) => d,
        Err(_) => return false,
    };

    let pages = doc.get_pages();
    let page_id = match pages.get(&(page_number as u32)) {
        Some(&id) => id,
        None => return false,
    };

    let image_object = match xobject::image(image_str) {
        Ok(img) => img,
        Err(_) => return false,
    };

    if doc
        .insert_image(page_id, image_object, (x as f32, y as f32), (width as f32, height as f32))
        .is_err()
    {
        return false;
    }

    doc.save(output_str).is_ok()
}

#[no_mangle]
pub extern "C" fn delete_pdf_page(
    pdf_path: *const c_char,
    page_number: i32,
    output_path: *const c_char,
) -> bool {
    let pdf_str = match to_str(pdf_path) {
        Some(s) => s,
        None => return false,
    };
    let output_str = match to_str(output_path) {
        Some(s) => s,
        None => return false,
    };

    let mut doc = match load_pdf_document(pdf_str) {
        Ok(d) => d,
        Err(_) => return false,
    };

    let pages = doc.get_pages();
    if !pages.contains_key(&(page_number as u32)) {
        return false;
    }

    // Remove page from documents
    let _ = doc.delete_pages(&[page_number as u32]);
    doc.save(output_str).is_ok()
}

#[no_mangle]
pub extern "C" fn insert_blank_page(
    pdf_path: *const c_char,
    target_page: i32,
    insert_before: bool,
    output_path: *const c_char,
) -> bool {
    let pdf_str = match to_str(pdf_path) {
        Some(s) => s,
        None => return false,
    };
    let output_str = match to_str(output_path) {
        Some(s) => s,
        None => return false,
    };

    let mut doc = match load_pdf_document(pdf_str) {
        Ok(d) => d,
        Err(_) => return false,
    };

    let page_index = if insert_before {
        (target_page - 1).max(0) as usize
    } else {
        target_page as usize
    };

    // Create a new blank page object
    let new_page_id = doc.add_object(Object::Dictionary(Dictionary::new()));
    let mut blank_page_dict = Dictionary::new();
    blank_page_dict.set("Type", Object::Name("Page".as_bytes().to_vec()));
    // Standard A4 dimensions in points: 595 x 842
    blank_page_dict.set(
        "MediaBox",
        Object::Array(vec![
            Object::Integer(0),
            Object::Integer(0),
            Object::Integer(595),
            Object::Integer(842),
        ]),
    );
    blank_page_dict.set("Resources", Object::Dictionary(Dictionary::new()));

    // Insert into Pages catalog
    let mut pages_id = None;
    for (id, object) in doc.objects.iter() {
        if let Ok(dict) = object.as_dict() {
            if dict.type_name().unwrap_or("") == "Pages" {
                pages_id = Some(*id);
                break;
            }
        }
    }

    if let Some(pages_id) = pages_id {
        blank_page_dict.set("Parent", Object::Reference(pages_id));
        doc.set_object(new_page_id, Object::Dictionary(blank_page_dict));

        if let Ok(Object::Dictionary(ref mut dict)) = doc.get_object_mut(pages_id) {
            let mut kids = match dict.get(b"Kids") {
                Ok(Object::Array(k)) => k.clone(),
                _ => Vec::new(),
            };
            if page_index >= kids.len() {
                kids.push(Object::Reference(new_page_id));
            } else {
                kids.insert(page_index, Object::Reference(new_page_id));
            }
            dict.set("Kids", Object::Array(kids));
            let count = dict.get(b"Count").ok().and_then(|o| get_integer(o)).unwrap_or(0);
            dict.set("Count", Object::Integer(count + 1));
        } else {
            return false;
        }
    } else {
        return false;
    }

    doc.save(output_str).is_ok()
}

#[no_mangle]
pub extern "C" fn reorder_pdf_pages(
    pdf_path: *const c_char,
    order_semicolon: *const c_char,
    output_path: *const c_char,
) -> bool {
    let pdf_str = match to_str(pdf_path) {
        Some(s) => s,
        None => return false,
    };
    let order_str = match to_str(order_semicolon) {
        Some(s) => s,
        None => return false,
    };
    let output_str = match to_str(output_path) {
        Some(s) => s,
        None => return false,
    };

    let mut doc = match load_pdf_document(pdf_str) {
        Ok(d) => d,
        Err(_) => return false,
    };

    let order_indices: Vec<u32> = order_str
        .split(';')
        .filter_map(|s| s.parse::<u32>().ok())
        .collect();

    if order_indices.is_empty() {
        return false;
    }

    let pages = doc.get_pages();
    let mut new_kids = Vec::new();

    for index in order_indices {
        if let Some(&page_id) = pages.get(&index) {
            new_kids.push(Object::Reference(page_id));
        } else {
            return false; // Invalid index
        }
    }

    let mut pages_id = None;
    for (id, object) in doc.objects.iter() {
        if let Ok(dict) = object.as_dict() {
            if dict.type_name().unwrap_or("") == "Pages" {
                pages_id = Some(*id);
                break;
            }
        }
    }

    if let Some(pages_id) = pages_id {
        if let Ok(Object::Dictionary(ref mut dict)) = doc.get_object_mut(pages_id) {
            dict.set("Kids", Object::Array(new_kids));
            dict.set("Count", Object::Integer(pages.len() as i64));
        } else {
            return false;
        }
    } else {
        return false;
    }

    doc.save(output_str).is_ok()
}

#[no_mangle]
pub extern "C" fn extract_pdf_pages(
    pdf_path: *const c_char,
    pages_semicolon: *const c_char,
    output_path: *const c_char,
) -> bool {
    let pdf_str = match to_str(pdf_path) {
        Some(s) => s,
        None => return false,
    };
    let pages_str = match to_str(pages_semicolon) {
        Some(s) => s,
        None => return false,
    };
    let output_str = match to_str(output_path) {
        Some(s) => s,
        None => return false,
    };

    let mut doc = match load_pdf_document(pdf_str) {
        Ok(d) => d,
        Err(_) => return false,
    };

    let pages = doc.get_pages();
    
    // Parse pages sequence (e.g. "1;3;5-8")
    let mut target_pages = Vec::new();
    for part in pages_str.split(';') {
        if part.contains('-') {
            let range_parts: Vec<&str> = part.split('-').collect();
            if range_parts.len() == 2 {
                if let (Ok(start), Ok(end)) = (range_parts[0].parse::<u32>(), range_parts[1].parse::<u32>()) {
                    for page in start..=end {
                        target_pages.push(page);
                    }
                }
            }
        } else if let Ok(page) = part.parse::<u32>() {
            target_pages.push(page);
        }
    }

    if target_pages.is_empty() {
        return false;
    }

    let mut new_kids = Vec::new();
    for page in &target_pages {
        if let Some(&page_id) = pages.get(page) {
            new_kids.push(Object::Reference(page_id));
        } else {
            return false;
        }
    }

    let mut pages_id = None;
    for (id, object) in doc.objects.iter() {
        if let Ok(dict) = object.as_dict() {
            if dict.type_name().unwrap_or("") == "Pages" {
                pages_id = Some(*id);
                break;
            }
        }
    }

    if let Some(pages_id) = pages_id {
        if let Ok(Object::Dictionary(ref mut dict)) = doc.get_object_mut(pages_id) {
            dict.set("Kids", Object::Array(new_kids));
            dict.set("Count", Object::Integer(target_pages.len() as i64));
        } else {
            return false;
        }
    } else {
        return false;
    }

    // Explicitly delete unselected page objects to ensure their resources (streams, fonts, images)
    // are unreferenced and can be successfully pruned to reduce file size.
    for (&p, &page_id) in &pages {
        if !target_pages.contains(&p) {
            doc.objects.remove(&page_id);
        }
    }

    // Prune unused objects to minimize file size
    let _ = doc.prune_objects();
    doc.save(output_str).is_ok()
}

#[no_mangle]
pub extern "C" fn make_pdf_searchable(
    pdf_path: *const c_char,
    ocr_data_raw: *const c_char,
    output_path: *const c_char,
) -> bool {
    let pdf_str = match to_str(pdf_path) {
        Some(s) => s,
        None => return false,
    };
    let ocr_str = match to_str(ocr_data_raw) {
        Some(s) => s,
        None => return false,
    };
    let output_str = match to_str(output_path) {
        Some(s) => s,
        None => return false,
    };

    let mut doc = match load_pdf_document(pdf_str) {
        Ok(d) => d,
        Err(_) => return false,
    };

    // Group OCR words by page
    // Format: "page_number|x|y|w|h|text"
    let mut page_words: BTreeMap<u32, Vec<(f64, f64, f64, f64, String)>> = BTreeMap::new();
    for line in ocr_str.split('\n') {
        if line.is_empty() {
            continue;
        }
        let parts: Vec<&str> = line.split('|').collect();
        if parts.len() < 6 {
            continue;
        }
        let page_num = match parts[0].parse::<u32>() {
            Ok(n) => n,
            Err(_) => continue,
        };
        let x = parts[1].parse::<f64>().unwrap_or(0.0);
        let y = parts[2].parse::<f64>().unwrap_or(0.0);
        let w = parts[3].parse::<f64>().unwrap_or(0.0);
        let h = parts[4].parse::<f64>().unwrap_or(12.0);
        let text = parts[5..].join("|"); // in case the text itself contains '|'

        page_words.entry(page_num).or_default().push((x, y, w, h, text));
    }

    let pages = doc.get_pages();

    for (page_num, words) in page_words {
        let page_id = match pages.get(&page_num) {
            Some(&id) => id,
            None => continue,
        };

        // Create the Font resource first
        let mut font_dict = Dictionary::new();
        font_dict.set("Type", Object::Name("Font".as_bytes().to_vec()));
        font_dict.set("Subtype", Object::Name("Type1".as_bytes().to_vec()));
        font_dict.set("BaseFont", Object::Name("Helvetica".as_bytes().to_vec()));
        let font_id = doc.add_object(Object::Dictionary(font_dict));

        // Create the stream first (so we don't borrow doc while building it)
        let mut stream_content = Vec::new();
        stream_content.extend_from_slice(b"BT\n/F_OcrHelper 10 Tf\n3 Tr\n");
        for (x, y, w, h, text) in words {
            let escaped_text = text.replace('(', "\\(").replace(')', "\\)");
            let len_f = escaped_text.len().max(1) as f64;
            let h_scaled = h;
            let tz = ((w / (len_f * 0.6 * h_scaled)) * 100.0).clamp(20.0, 300.0);
            
            let word_stream = format!(
                "1 0 0 1 {:.2} {:.2} Tm\n{:.1} Tf\n{:.1} Tz\n({}) Tj\n",
                x, y, h_scaled, tz, escaped_text
            );
            stream_content.extend_from_slice(word_stream.as_bytes());
        }
        stream_content.extend_from_slice(b"ET\n");
        let stream_obj = lopdf::Stream::new(Dictionary::new(), stream_content);
        let stream_id = doc.add_object(Object::Stream(stream_obj));

        // Now update the page object
        if let Ok(Object::Dictionary(ref mut page_dict)) = doc.get_object_mut(page_id) {
            // Ensure Resources dictionary exists
            if !page_dict.has(b"Resources") {
                page_dict.set("Resources", Object::Dictionary(Dictionary::new()));
            }
        }

        // Check if Resources is a reference
        let mut resources_ref_id = None;
        if let Ok(Object::Dictionary(page_dict)) = doc.get_object(page_id) {
            if let Ok(Object::Reference(ref_id)) = page_dict.get(b"Resources") {
                resources_ref_id = Some(*ref_id);
            }
        }

        let resources_target_id = resources_ref_id.unwrap_or(page_id);

        if let Ok(Object::Dictionary(ref mut res_dict)) = doc.get_object_mut(resources_target_id) {
            let dict = if resources_ref_id.is_some() {
                res_dict
            } else {
                res_dict.get_mut(b"Resources").unwrap().as_dict_mut().unwrap()
            };

            // Ensure Font dictionary exists in Resources
            if !dict.has(b"Font") {
                dict.set("Font", Object::Dictionary(Dictionary::new()));
            }
        }

        // Check if Font is a reference
        let mut font_ref_id = None;
        if let Ok(Object::Dictionary(res_dict)) = doc.get_object(resources_target_id) {
            let dict = if resources_ref_id.is_some() {
                res_dict
            } else {
                res_dict.get(b"Resources").unwrap().as_dict().unwrap()
            };
            if let Ok(Object::Reference(ref_id)) = dict.get(b"Font") {
                font_ref_id = Some(*ref_id);
            }
        }

        let font_target_id = font_ref_id.or(resources_ref_id);

        if let Some(target_id) = font_target_id {
            if let Ok(Object::Dictionary(ref mut target_dict)) = doc.get_object_mut(target_id) {
                let dict = if font_ref_id.is_some() {
                    target_dict
                } else {
                    target_dict.get_mut(b"Font").unwrap().as_dict_mut().unwrap()
                };
                dict.set("F_OcrHelper", Object::Reference(font_id));
            }
        } else {
            // Both Resources and Font are inline dictionaries on the page object
            if let Ok(Object::Dictionary(ref mut page_dict)) = doc.get_object_mut(page_id) {
                if let Ok(ref mut res_dict) = page_dict.get_mut(b"Resources").and_then(|o| o.as_dict_mut()) {
                    if let Ok(ref mut font_dict) = res_dict.get_mut(b"Font").and_then(|o| o.as_dict_mut()) {
                        font_dict.set("F_OcrHelper", Object::Reference(font_id));
                    }
                }
            }
        }

        // Update Contents of the page
        let mut contents_ref_id = None;
        if let Ok(Object::Dictionary(page_dict)) = doc.get_object(page_id) {
            if let Ok(Object::Reference(ref_id)) = page_dict.get(b"Contents") {
                contents_ref_id = Some(*ref_id);
            }
        }

        if let Ok(Object::Dictionary(ref mut page_dict)) = doc.get_object_mut(page_id) {
            match page_dict.get(b"Contents") {
                Ok(Object::Array(arr)) => {
                    let mut new_arr = arr.clone();
                    new_arr.push(Object::Reference(stream_id));
                    page_dict.set("Contents", Object::Array(new_arr));
                }
                Ok(Object::Reference(_)) => {
                    if let Some(ref_id) = contents_ref_id {
                        page_dict.set("Contents", Object::Array(vec![
                            Object::Reference(ref_id),
                            Object::Reference(stream_id),
                        ]));
                    }
                }
                _ => {
                    page_dict.set("Contents", Object::Reference(stream_id));
                }
            }
        }
    }

    doc.save(output_str).is_ok()
}

#[no_mangle]
pub extern "C" fn compress_pdf(
    pdf_path: *const c_char,
    image_quality: u8,
    output_path: *const c_char,
) -> bool {
    // Wrap in catch_unwind to prevent Rust panics from crossing FFI and crashing the app
    let result = std::panic::catch_unwind(|| {
        compress_pdf_inner(pdf_path, image_quality, output_path)
    });
    match result {
        Ok(val) => val,
        Err(_) => {
            // Panic occurred - try to log it
            let desktop = std::env::var("USERPROFILE").unwrap_or_else(|_| "C:\\Users\\Public".to_string());
            let log_path = format!("{}\\Desktop\\compress_debug.log", desktop);
            if let Ok(mut f) = std::fs::OpenOptions::new().create(true).append(true).write(true).open(&log_path) {
                let _ = writeln!(f, "[RUST] !!! PANIC/CRASH xay ra trong compress_pdf !!!");
                let _ = writeln!(f, "[RUST] Co the do het RAM khi xu ly anh lon.");
            }
            false
        }
    }
}

fn compress_pdf_inner(
    pdf_path: *const c_char,
    image_quality: u8,
    output_path: *const c_char,
) -> bool {
    let pdf_str = match to_str(pdf_path) {
        Some(s) => s,
        None => return false,
    };
    let output_str = match to_str(output_path) {
        Some(s) => s,
        None => return false,
    };

    // Open log file on Desktop for debug output
    let log_path = {
        let desktop = std::env::var("USERPROFILE").unwrap_or_else(|_| "C:\\Users\\Public".to_string());
        format!("{}\\Desktop\\compress_debug.log", desktop)
    };
    let mut log_file = std::fs::OpenOptions::new()
        .create(true).append(false).write(true)
        .open(&log_path)
        .ok();

    macro_rules! log {
        ($($arg:tt)*) => {{
            let msg = format!($($arg)*);
            println!("{}", msg);
            if let Some(ref mut f) = log_file {
                let _ = writeln!(f, "{}", msg);
                let _ = f.flush();
            }
        }};
    }

    log!("[RUST] --- Bat dau compress_pdf ---");
    log!("[RUST] File goc: {}", pdf_str);
    log!("[RUST] File ra: {}", output_str);
    log!("[RUST] Chat luong anh nen: {}", image_quality);
    log!("[RUST] Log file: {}", log_path);

    let mut doc = match load_pdf_document(pdf_str) {
        Ok(d) => {
            log!("[RUST] Load Document thanh cong.");
            d
        }
        Err(e) => {
            log!("[RUST] ERROR: Khong the load document. Loi: {:?}", e);
            return false;
        }
    };

    // Tìm tất cả các stream của Image XObject
    let mut image_ids = Vec::new();
    for (id, object) in doc.objects.iter() {
        if let Ok(stream) = object.as_stream() {
            let subtype = stream.dict.get(b"Subtype").ok()
                .map(|o| resolve_object(&doc, o))
                .and_then(|o| o.as_name().ok().map(|n| String::from_utf8_lossy(n).into_owned()));
            let filter = stream.dict.get(b"Filter").ok()
                .map(|o| resolve_object(&doc, o))
                .map(|o| format!("{:?}", o));
            let w = stream.dict.get(b"Width").ok()
                .map(|o| resolve_object(&doc, o))
                .and_then(|o| get_integer(o)).unwrap_or(0);
            let h = stream.dict.get(b"Height").ok()
                .map(|o| resolve_object(&doc, o))
                .and_then(|o| get_integer(o)).unwrap_or(0);
            log!("[RUST] Object {:?}: Subtype={:?} Filter={:?} W={} H={} raw_len={}",
                 id, subtype, filter, w, h, stream.content.len());
            if subtype.as_deref() == Some("Image") {
                image_ids.push(*id);
            }
        }
    }

    log!("[RUST] Tim thay {} doi tuong Image trong file PDF.", image_ids.len());

    if image_ids.is_empty() {
        log!("[RUST] Khong co anh nao can nen. Luu lai file goc.");
        return doc.save(output_str).is_ok();
    }

    let mut success_count = 0;
    let mut skip_count = 0;

    for id in image_ids {
        // --- Step 1: Collect metadata (immutable borrow) ---
        let mut width: u32 = 0;
        let mut height: u32 = 0;
        let mut is_dct = false;
        let mut is_cmyk = false;  // DeviceCMYK has 4 channels, different from RGBA
        let mut raw_stream_bytes: Vec<u8> = Vec::new();
        let mut has_stream = false;

        if let Ok(Object::Stream(ref stream)) = doc.get_object(id) {
            has_stream = true;
            width = stream.dict.get(b"Width").ok()
                .map(|o| resolve_object(&doc, o))
                .and_then(|o| get_integer(o))
                .unwrap_or(0) as u32;

            height = stream.dict.get(b"Height").ok()
                .map(|o| resolve_object(&doc, o))
                .and_then(|o| get_integer(o))
                .unwrap_or(0) as u32;

            // Detect ColorSpace (CRITICAL for CMYK vs RGBA distinction)
            if let Ok(cs_obj) = stream.dict.get(b"ColorSpace") {
                let cs_resolved = resolve_object(&doc, cs_obj);
                let cs_name = match cs_resolved {
                    Object::Name(ref n) => String::from_utf8_lossy(n).to_string(),
                    Object::Array(ref arr) => {
                        // e.g. [/ICCBased 3 0 R] - check first element
                        if let Some(Object::Name(ref n)) = arr.first() {
                            String::from_utf8_lossy(n).to_string()
                        } else { String::new() }
                    }
                    _ => String::new(),
                };
                if cs_name.contains("CMYK") || cs_name.contains("Cmyk") {
                    is_cmyk = true;
                }
                log!("[RUST] -> ColorSpace: {} is_cmyk:{}", cs_name, is_cmyk);
            }

            // Detect filter type
            if let Ok(filter_obj) = stream.dict.get(b"Filter") {
                let resolved = resolve_object(&doc, filter_obj);
                let check_dct = |name: &[u8]| name == b"DCTDecode";
                match resolved {
                    Object::Name(ref n) => { if check_dct(n) { is_dct = true; } }
                    Object::Array(ref arr) => {
                        for item in arr {
                            if let Object::Name(ref n) = resolve_object(&doc, item) {
                                if check_dct(n) { is_dct = true; break; }
                            }
                        }
                    }
                    _ => {}
                }
            }

            // Store raw compressed content for DCT, otherwise try decompressing
            if is_dct {
                raw_stream_bytes = stream.content.clone();
            } else {
                // Try lopdf's built-in first
                match stream.decompressed_content() {
                    Ok(dec) if !dec.is_empty() => {
                        raw_stream_bytes = dec;
                    }
                    _ => {
                        // lopdf fails for [/FlateDecode] array filter — decompress manually
                        log!("[RUST] -> lopdf decode failed cho ID {:?}, thu manual zlib inflate...", id);
                        match manual_decompress_flate(&stream.content) {
                            Some(dec) => {
                                log!("[RUST] -> Manual zlib OK: {} -> {} bytes", stream.content.len(), dec.len());
                                raw_stream_bytes = dec;
                            }
                            None => {
                                log!("[RUST] -> Manual zlib THAT BAI cho ID {:?}, dung raw bytes", id);
                                raw_stream_bytes = stream.content.clone();
                            }
                        }
                    }
                }
            }
        }

        log!("[RUST] === Image ID {:?} === W:{} H:{} is_dct:{} raw_bytes:{} has_stream:{}",
                 id, width, height, is_dct, raw_stream_bytes.len(), has_stream);

        if !has_stream || raw_stream_bytes.is_empty() {
            log!("[RUST] -> SKIP: stream rong hoac khong co stream.");
            skip_count += 1;
            continue;
        }

        // --- Step 2: Decode to DynamicImage ---
        let dynamic_img: Option<image::DynamicImage> = if is_dct {
            // DCT stream.content IS the raw JPEG bytes
            match image::load_from_memory_with_format(&raw_stream_bytes, image::ImageFormat::Jpeg) {
                Ok(img) => {
                    log!("[RUST] -> OK: Decode JPEG thanh cong. Kich thuoc thuc: {}x{}", img.width(), img.height());
                    Some(img)
                }
                Err(e) => {
                    log!("[RUST] -> WARN: load JPEG that bai ({:?}), thu voi load_from_memory...", e);
                    // Sometimes DCT stream might have extra wrapper, try generic
                    image::load_from_memory(&raw_stream_bytes).ok()
                }
            }
        } else {
            // Non-DCT: raw_stream_bytes is decompressed pixel data
            // Try by expected size first (RGB, Gray, RGBA/CMYK)
            let try_rgb   = width > 0 && height > 0 && raw_stream_bytes.len() == (width * height * 3) as usize;
            let try_gray  = width > 0 && height > 0 && raw_stream_bytes.len() == (width * height) as usize;
            let try_cmyk_or_rgba = width > 0 && height > 0 && raw_stream_bytes.len() == (width * height * 4) as usize;

            log!("[RUST] -> Non-DCT decode attempt: len={} expected_rgb={} expected_gray={} expected_4ch={} is_cmyk:{}",
                     raw_stream_bytes.len(), width * height * 3, width * height, width * height * 4, is_cmyk);

            if try_rgb {
                image::ImageBuffer::<image::Rgb<u8>, _>::from_raw(width, height, raw_stream_bytes.clone())
                    .map(|b| { log!("[RUST] -> OK: Parsed as RGB8"); image::DynamicImage::ImageRgb8(b) })
            } else if try_gray {
                image::ImageBuffer::<image::Luma<u8>, _>::from_raw(width, height, raw_stream_bytes.clone())
                    .map(|b| { log!("[RUST] -> OK: Parsed as Gray8"); image::DynamicImage::ImageLuma8(b) })
            } else if try_cmyk_or_rgba {
                if is_cmyk {
                    // CMYK -> RGB8 manual conversion
                    log!("[RUST] -> Converting CMYK -> RGB8...");
                    let cmyk = &raw_stream_bytes;
                    let mut rgb_data = Vec::with_capacity((cmyk.len() / 4) * 3);
                    for chunk in cmyk.chunks(4) {
                        if chunk.len() == 4 {
                            let c = chunk[0] as f32 / 255.0;
                            let m = chunk[1] as f32 / 255.0;
                            let y = chunk[2] as f32 / 255.0;
                            let k = chunk[3] as f32 / 255.0;
                            let r = ((1.0 - c) * (1.0 - k) * 255.0) as u8;
                            let g = ((1.0 - m) * (1.0 - k) * 255.0) as u8;
                            let b = ((1.0 - y) * (1.0 - k) * 255.0) as u8;
                            rgb_data.push(r); rgb_data.push(g); rgb_data.push(b);
                        }
                    }
                    image::ImageBuffer::<image::Rgb<u8>, _>::from_raw(width, height, rgb_data)
                        .map(|b| { log!("[RUST] -> OK: CMYK->RGB8 done"); image::DynamicImage::ImageRgb8(b) })
                } else {
                    // RGBA8
                    image::ImageBuffer::<image::Rgba<u8>, _>::from_raw(width, height, raw_stream_bytes.clone())
                        .map(|b| { log!("[RUST] -> OK: Parsed as RGBA8"); image::DynamicImage::ImageRgba8(b) })
                }
            } else {
                log!("[RUST] -> Thu load_from_memory (generic)...");
                image::load_from_memory(&raw_stream_bytes).map(|img| {
                    log!("[RUST] -> OK: Generic decode thanh cong. Size: {}x{}", img.width(), img.height());
                    img
                }).ok()
            }
        };

        let dynamic_img = match dynamic_img {
            Some(img) => img,
            None => {
                log!("[RUST] -> SKIP: Khong the decode anh cho ID {:?}", id);
                skip_count += 1;
                continue;
            }
        };

        // --- Step 3: Convert to RGB8 ImageBuffer (manual, safe for all color types) ---
        let (orig_w, orig_h) = (dynamic_img.width(), dynamic_img.height());
        let raw_bytes_len = raw_stream_bytes.len();
        let uncompressed_mb = raw_bytes_len as f64 / (1024.0 * 1024.0);

        // Safety guard: skip images that are extremely large (> 150MB uncompressed) to prevent OOM
        if uncompressed_mb > 150.0 {
            log!("[RUST] -> SKIP: Anh qua lon ({:.1}MB unpacked), bo qua de tranh OOM cho ID {:?}", uncompressed_mb, id);
            drop(raw_stream_bytes);
            drop(dynamic_img);
            skip_count += 1;
            continue;
        }
        drop(raw_stream_bytes);

        log!("[RUST] -> [3a] Converting to RGB8 {}x{}...", orig_w, orig_h);
        // Manual conversion: avoids DynamicImage::to_rgb8() which can panic on Gray8/RGBA8
        let rgb8_buf: image::ImageBuffer<image::Rgb<u8>, Vec<u8>> = match &dynamic_img {
            image::DynamicImage::ImageLuma8(luma) => {
                // Gray8 -> RGB8: replicate each gray byte 3 times
                let gray = luma.as_raw();
                let mut rgb = Vec::with_capacity(gray.len() * 3);
                for &g in gray.iter() {
                    rgb.push(g); rgb.push(g); rgb.push(g);
                }
                image::ImageBuffer::from_raw(orig_w, orig_h, rgb)
                    .unwrap_or_else(|| image::ImageBuffer::new(orig_w, orig_h))
            }
            image::DynamicImage::ImageRgba8(rgba) => {
                // RGBA8 -> RGB8: drop alpha channel
                let raw = rgba.as_raw();
                let mut rgb = Vec::with_capacity((raw.len() / 4) * 3);
                for chunk in raw.chunks(4) {
                    rgb.push(chunk[0]); rgb.push(chunk[1]); rgb.push(chunk[2]);
                }
                image::ImageBuffer::from_raw(orig_w, orig_h, rgb)
                    .unwrap_or_else(|| image::ImageBuffer::new(orig_w, orig_h))
            }
            image::DynamicImage::ImageRgb8(rgb) => {
                rgb.clone()
            }
            _ => {
                // Fallback for other types
                dynamic_img.to_rgb8()
            }
        };
        drop(dynamic_img);
        log!("[RUST] -> [3b] RGB8 conversion done.");

        // --- Step 3b: Downsample if needed using imageops::resize directly ---
        let max_dimension: u32 = if image_quality >= 80 {
            3000
        } else if image_quality >= 60 {
            2000
        } else {
            1200
        };
        let (final_buf, final_w, final_h) = if orig_w > max_dimension || orig_h > max_dimension {
            let scale = max_dimension as f32 / orig_w.max(orig_h) as f32;
            let new_w = ((orig_w as f32 * scale) as u32).max(1);
            let new_h = ((orig_h as f32 * scale) as u32).max(1);
            log!("[RUST] -> [3c] Resize {}x{} -> {}x{} ({:.1}MB)...", orig_w, orig_h, new_w, new_h, uncompressed_mb);
            // Use high-quality CatmullRom (Bicubic) filter instead of Nearest to prevent pixelation/jagged lines
            let resized = image::imageops::resize(&rgb8_buf, new_w, new_h, image::imageops::FilterType::CatmullRom);
            drop(rgb8_buf);
            log!("[RUST] -> [3d] Resize done.");
            (resized, new_w, new_h)
        } else {
            (rgb8_buf, orig_w, orig_h)
        };

        // --- Step 4: JPEG encode directly from raw RGB bytes ---
        log!("[RUST] -> [4] JPEG encode {}x{} quality={}...", final_w, final_h, image_quality);
        let mut jpeg_bytes = Vec::new();
        let encoder = image::codecs::jpeg::JpegEncoder::new_with_quality(&mut jpeg_bytes, image_quality);
        match encoder.write_image(final_buf.as_raw(), final_w, final_h, image::ColorType::Rgb8) {
            Ok(_) => {
                let old_size = raw_bytes_len;
                let new_size = jpeg_bytes.len();
                let reduction = if old_size > 0 {
                    ((old_size as f64 - new_size as f64) / old_size as f64 * 100.0) as i64
                } else { 0 };
                log!("[RUST] -> JPEG encode OK: {} bytes -> {} bytes (giam {}%)", old_size, new_size, reduction);

                // --- Step 5: Write back (mutable borrow) ---
                if let Ok(Object::Stream(ref mut stream)) = doc.get_object_mut(id) {
                    stream.set_content(jpeg_bytes);
                    stream.dict.set("Filter", Object::Name(b"DCTDecode".to_vec()));
                    stream.dict.set("Width", Object::Integer(final_w as i64));
                    stream.dict.set("Height", Object::Integer(final_h as i64));
                    stream.dict.set("ColorSpace", Object::Name(b"DeviceRGB".to_vec()));
                    stream.dict.set("BitsPerComponent", Object::Integer(8));
                    stream.dict.remove(b"DecodeParms");
                    success_count += 1;
                    log!("[RUST] -> Ghi lai stream thanh cong.");
                } else {
                    log!("[RUST] -> WARN: Khong the get_object_mut de ghi lai.");
                    skip_count += 1;
                }
            }
            Err(e) => {
                log!("[RUST] -> SKIP: JPEG encode THAT BAI cho ID {:?}: {:?}", id, e);
                skip_count += 1;
            }
        }
    }

    log!("[RUST] ========================================");
    log!("[RUST] Ket qua nen: {} thanh cong, {} bo qua.", success_count, skip_count);
    log!("[RUST] ========================================");

    // Prune unused/orphaned objects for additional space savings
    log!("[RUST] Pruning unused objects...");
    doc.prune_objects();

    log!("[RUST] Saving to: {}", output_str);
    let save_res = doc.save(output_str);
    let save_ok = save_res.is_ok();
    if let Err(ref e) = save_res {
        log!("[RUST] Save Error: {:?}", e);
    }
    log!("[RUST] Save Status: {}", save_ok);
    save_ok
}

#[no_mangle]
pub extern "C" fn optimize_pdf_lossless(
    pdf_path: *const c_char,
    remove_metadata: bool,
    output_path: *const c_char,
) -> bool {
    let pdf_str = match to_str(pdf_path) {
        Some(s) => s,
        None => return false,
    };
    let output_str = match to_str(output_path) {
        Some(s) => s,
        None => return false,
    };

    let mut doc = match load_pdf_document(pdf_str) {
        Ok(d) => d,
        Err(_) => return false,
    };

    // 1. Clean up unused objects
    doc.prune_objects();

    // 2. Remove metadata if requested
    if remove_metadata {
        if let Ok(root_ref) = doc.trailer.get(b"Root").and_then(|o| o.as_reference()) {
            if let Ok(Object::Dictionary(ref mut catalog_dict)) = doc.get_object_mut(root_ref) {
                catalog_dict.remove(b"Metadata");
                catalog_dict.remove(b"PieceInfo");
            }
        }
        doc.trailer.remove(b"Info");
    }

    // 3. Compress uncompressed streams using FlateDecode
    for (_id, object) in doc.objects.iter_mut() {
        if let Object::Stream(ref mut stream) = *object {
            let has_filter = stream.dict.get(b"Filter").is_ok();
            if !has_filter {
                let _ = stream.compress();
            }
        }
    }

    doc.save(output_str).is_ok()
}


#[no_mangle]
pub extern "C" fn add_pdf_watermark(
    pdf_path: *const c_char,
    text: *const c_char,
    angle: f64,
    opacity: f64,
    font_size: f64,
    r: f64,
    g: f64,
    b: f64,
    output_path: *const c_char,
) -> bool {
    let pdf_str = match to_str(pdf_path) {
        Some(s) => s,
        None => return false,
    };
    let text_str = match to_str(text) {
        Some(s) => s,
        None => return false,
    };
    let output_str = match to_str(output_path) {
        Some(s) => s,
        None => return false,
    };

    let mut doc = match load_pdf_document(pdf_str) {
        Ok(d) => d,
        Err(_) => return false,
    };

    // Create Font resource
    let mut font_dict = Dictionary::new();
    font_dict.set("Type", Object::Name("Font".as_bytes().to_vec()));
    font_dict.set("Subtype", Object::Name("Type1".as_bytes().to_vec()));
    font_dict.set("BaseFont", Object::Name("Helvetica-Bold".as_bytes().to_vec()));
    let font_id = doc.add_object(Object::Dictionary(font_dict));

    // Create ExtGState resource for opacity
    let mut gs_dict = Dictionary::new();
    gs_dict.set("Type", Object::Name("ExtGState".as_bytes().to_vec()));
    gs_dict.set("ca", Object::Real(opacity as f32));
    gs_dict.set("CA", Object::Real(opacity as f32));
    let gs_id = doc.add_object(Object::Dictionary(gs_dict));

    let pages = doc.get_pages();
    for (_page_num, page_id) in pages {
        let mut width = 595.0f64;
        let mut height = 842.0f64;
        if let Ok(Object::Dictionary(ref page_dict)) = doc.get_object(page_id) {
            if let Ok(Object::Array(ref mb)) = page_dict.get(b"MediaBox") {
                if mb.len() >= 4 {
                    let x1 = mb[0].as_float().unwrap_or(0.0) as f64;
                    let y1 = mb[1].as_float().unwrap_or(0.0) as f64;
                    let x2 = mb[2].as_float().unwrap_or(595.0) as f64;
                    let y2 = mb[3].as_float().unwrap_or(842.0) as f64;
                    width = (x2 - x1).abs();
                    height = (y2 - y1).abs();
                }
            }
        }

        let angle_rad = angle.to_radians();
        let cos_a = angle_rad.cos();
        let sin_a = angle_rad.sin();

        let text_width_approx = text_str.len() as f64 * font_size * 0.28;
        let tx = (width / 2.0) - (text_width_approx * cos_a / 2.0) + (font_size * sin_a / 4.0);
        let ty = (height / 2.0) - (text_width_approx * sin_a / 2.0) - (font_size * cos_a / 4.0);

        let escaped_text = text_str.replace('(', "\\(").replace(')', "\\)");
        let watermark_content = format!(
            "q\n/GS_Watermark gs\nBT\n/F_Watermark {:.1} Tf\n{:.2} {:.2} {:.2} rg\n{:.4} {:.4} {:.4} {:.4} {:.2} {:.2} Tm\n({}) Tj\nET\nQ\n",
            font_size, r, g, b, cos_a, sin_a, -sin_a, cos_a, tx, ty, escaped_text
        );

        let stream = lopdf::Stream::new(Dictionary::new(), watermark_content.into_bytes());
        let stream_id = doc.add_object(Object::Stream(stream));

        if let Ok(Object::Dictionary(ref mut page_dict)) = doc.get_object_mut(page_id) {
            if !page_dict.has(b"Resources") {
                page_dict.set("Resources", Object::Dictionary(Dictionary::new()));
            }
        }

        let mut resources_ref_id = None;
        if let Ok(Object::Dictionary(page_dict)) = doc.get_object(page_id) {
            if let Ok(Object::Reference(ref_id)) = page_dict.get(b"Resources") {
                resources_ref_id = Some(*ref_id);
            }
        }

        let resources_target_id = resources_ref_id.unwrap_or(page_id);

        if let Ok(Object::Dictionary(ref mut res_dict)) = doc.get_object_mut(resources_target_id) {
            let dict = if resources_ref_id.is_some() {
                res_dict
            } else {
                res_dict.get_mut(b"Resources").unwrap().as_dict_mut().unwrap()
            };

            if !dict.has(b"Font") {
                dict.set("Font", Object::Dictionary(Dictionary::new()));
            }
            if let Ok(ref mut font_res_dict) = dict.get_mut(b"Font").and_then(|o| o.as_dict_mut()) {
                font_res_dict.set("F_Watermark", Object::Reference(font_id));
            }

            if !dict.has(b"ExtGState") {
                dict.set("ExtGState", Object::Dictionary(Dictionary::new()));
            }
            if let Ok(ref mut gs_res_dict) = dict.get_mut(b"ExtGState").and_then(|o| o.as_dict_mut()) {
                gs_res_dict.set("GS_Watermark", Object::Reference(gs_id));
            }
        }

        let mut contents_ref_id = None;
        if let Ok(Object::Dictionary(page_dict)) = doc.get_object(page_id) {
            if let Ok(Object::Reference(ref_id)) = page_dict.get(b"Contents") {
                contents_ref_id = Some(*ref_id);
            }
        }

        if let Ok(Object::Dictionary(ref mut page_dict)) = doc.get_object_mut(page_id) {
            match page_dict.get(b"Contents") {
                Ok(Object::Array(arr)) => {
                    let mut new_arr = arr.clone();
                    new_arr.push(Object::Reference(stream_id));
                    page_dict.set("Contents", Object::Array(new_arr));
                }
                Ok(Object::Reference(_)) => {
                    if let Some(ref_id) = contents_ref_id {
                        page_dict.set("Contents", Object::Array(vec![
                            Object::Reference(ref_id),
                            Object::Reference(stream_id),
                        ]));
                    }
                }
                _ => {
                    page_dict.set("Contents", Object::Reference(stream_id));
                }
            }
        }
    }

    doc.save(output_str).is_ok()
}

#[no_mangle]
pub extern "C" fn add_pdf_page_numbers(
    pdf_path: *const c_char,
    format_str: *const c_char,
    position: i32,
    font_size: f64,
    r: f64,
    g: f64,
    b: f64,
    output_path: *const c_char,
) -> bool {
    let pdf_str = match to_str(pdf_path) {
        Some(s) => s,
        None => return false,
    };
    let fmt_str = match to_str(format_str) {
        Some(s) => s,
        None => return false,
    };
    let output_str = match to_str(output_path) {
        Some(s) => s,
        None => return false,
    };

    let mut doc = match load_pdf_document(pdf_str) {
        Ok(d) => d,
        Err(_) => return false,
    };

    let mut font_dict = Dictionary::new();
    font_dict.set("Type", Object::Name("Font".as_bytes().to_vec()));
    font_dict.set("Subtype", Object::Name("Type1".as_bytes().to_vec()));
    font_dict.set("BaseFont", Object::Name("Helvetica".as_bytes().to_vec()));
    let font_id = doc.add_object(Object::Dictionary(font_dict));

    let pages = doc.get_pages();
    let total_pages = pages.len();

    for (page_idx, page_id) in pages.iter().map(|(&num, &id)| (num as usize, id)) {
        let mut width = 595.0f64;
        let mut height = 842.0f64;
        if let Ok(Object::Dictionary(ref page_dict)) = doc.get_object(page_id) {
            if let Ok(Object::Array(ref mb)) = page_dict.get(b"MediaBox") {
                if mb.len() >= 4 {
                    let x1 = mb[0].as_float().unwrap_or(0.0) as f64;
                    let y1 = mb[1].as_float().unwrap_or(0.0) as f64;
                    let x2 = mb[2].as_float().unwrap_or(595.0) as f64;
                    let y2 = mb[3].as_float().unwrap_or(842.0) as f64;
                    width = (x2 - x1).abs();
                    height = (y2 - y1).abs();
                }
            }
        }

        let label = fmt_str
            .replace("{n}", &page_idx.to_string())
            .replace("{total}", &total_pages.to_string());

        let text_width_approx = label.len() as f64 * font_size * 0.28;

        let (tx, ty) = match position {
            1 => (50.0, 30.0), // Bottom Left
            2 => (width - 50.0 - text_width_approx, 30.0), // Bottom Right
            3 => (width / 2.0 - text_width_approx / 2.0, height - 40.0), // Top Center
            4 => (50.0, height - 40.0), // Top Left
            5 => (width - 50.0 - text_width_approx, height - 40.0), // Top Right
            _ => (width / 2.0 - text_width_approx / 2.0, 30.0), // Bottom Center
        };

        let escaped_text = label.replace('(', "\\(").replace(')', "\\)");
        let numbering_content = format!(
            "q\nBT\n/F_Num {:.1} Tf\n{:.2} {:.2} {:.2} rg\n1 0 0 1 {:.2} {:.2} Tm\n({}) Tj\nET\nQ\n",
            font_size, r, g, b, tx, ty, escaped_text
        );

        let stream = lopdf::Stream::new(Dictionary::new(), numbering_content.into_bytes());
        let stream_id = doc.add_object(Object::Stream(stream));

        if let Ok(Object::Dictionary(ref mut page_dict)) = doc.get_object_mut(page_id) {
            if !page_dict.has(b"Resources") {
                page_dict.set("Resources", Object::Dictionary(Dictionary::new()));
            }
        }

        let mut resources_ref_id = None;
        if let Ok(Object::Dictionary(page_dict)) = doc.get_object(page_id) {
            if let Ok(Object::Reference(ref_id)) = page_dict.get(b"Resources") {
                resources_ref_id = Some(*ref_id);
            }
        }

        let resources_target_id = resources_ref_id.unwrap_or(page_id);

        if let Ok(Object::Dictionary(ref mut res_dict)) = doc.get_object_mut(resources_target_id) {
            let dict = if resources_ref_id.is_some() {
                res_dict
            } else {
                res_dict.get_mut(b"Resources").unwrap().as_dict_mut().unwrap()
            };

            if !dict.has(b"Font") {
                dict.set("Font", Object::Dictionary(Dictionary::new()));
            }
            if let Ok(ref mut font_res_dict) = dict.get_mut(b"Font").and_then(|o| o.as_dict_mut()) {
                font_res_dict.set("F_Num", Object::Reference(font_id));
            }
        }

        let mut contents_ref_id = None;
        if let Ok(Object::Dictionary(page_dict)) = doc.get_object(page_id) {
            if let Ok(Object::Reference(ref_id)) = page_dict.get(b"Contents") {
                contents_ref_id = Some(*ref_id);
            }
        }

        if let Ok(Object::Dictionary(ref mut page_dict)) = doc.get_object_mut(page_id) {
            match page_dict.get(b"Contents") {
                Ok(Object::Array(arr)) => {
                    let mut new_arr = arr.clone();
                    new_arr.push(Object::Reference(stream_id));
                    page_dict.set("Contents", Object::Array(new_arr));
                }
                Ok(Object::Reference(_)) => {
                    if let Some(ref_id) = contents_ref_id {
                        page_dict.set("Contents", Object::Array(vec![
                            Object::Reference(ref_id),
                            Object::Reference(stream_id),
                        ]));
                    }
                }
                _ => {
                    page_dict.set("Contents", Object::Reference(stream_id));
                }
            }
        }
    }

    doc.save(output_str).is_ok()
}

#[no_mangle]
pub extern "C" fn extract_pdf_images(
    pdf_path: *const c_char,
    output_dir: *const c_char,
) -> i32 {
    let pdf_str = match to_str(pdf_path) {
        Some(s) => s,
        None => return -1,
    };
    let out_dir_str = match to_str(output_dir) {
        Some(s) => s,
        None => return -1,
    };

    let doc = match load_pdf_document(pdf_str) {
        Ok(d) => d,
        Err(_) => return -2,
    };

    let mut image_count = 0;
    for (_id, object) in doc.objects.iter() {
        if let Ok(stream) = object.as_stream() {
            let is_image = stream.dict.get(b"Subtype").ok()
                .and_then(|o| o.as_name().ok())
                .map(|n| n == b"Image")
                .unwrap_or(false);
            if !is_image {
                continue;
            }

            let width = stream.dict.get(b"Width").ok().and_then(|o| get_integer(o)).unwrap_or(0) as u32;
            let height = stream.dict.get(b"Height").ok().and_then(|o| get_integer(o)).unwrap_or(0) as u32;
            if width == 0 || height == 0 {
                continue;
            }

            image_count += 1;
            let filter = stream.dict.get(b"Filter").ok().and_then(|o| o.as_name().ok()).unwrap_or(&[]);
            
            if filter == b"DCTDecode" {
                let raw_bytes = &stream.content;
                let file_name = format!("img_{:03}.jpg", image_count);
                let out_path = std::path::Path::new(out_dir_str).join(file_name);
                if std::fs::write(out_path, raw_bytes).is_ok() {
                    continue;
                }
            }

            if let Ok(decompressed) = stream.decompressed_content() {
                let file_name = format!("img_{:03}.png", image_count);
                let out_path = std::path::Path::new(out_dir_str).join(file_name);

                let color_space = stream.dict.get(b"ColorSpace").ok().and_then(|o| o.as_name().ok()).unwrap_or(&[]);
                if color_space == b"DeviceRGB" && decompressed.len() == (width * height * 3) as usize {
                    let _ = image::save_buffer(&out_path, &decompressed, width, height, image::ColorType::Rgb8);
                } else if color_space == b"DeviceGray" && decompressed.len() == (width * height) as usize {
                    let _ = image::save_buffer(&out_path, &decompressed, width, height, image::ColorType::L8);
                }
            }
        }
    }

    image_count
}

#[no_mangle]
pub extern "C" fn repair_pdf(
    pdf_path: *const c_char,
    output_path: *const c_char,
) -> bool {
    let pdf_str = match to_str(pdf_path) {
        Some(s) => s,
        None => return false,
    };
    let output_str = match to_str(output_path) {
        Some(s) => s,
        None => return false,
    };

    let mut bytes = match std::fs::read(pdf_str) {
        Ok(b) => b,
        Err(_) => return false,
    };

    // 1. Clean up header: Find first %PDF-
    let pdf_magic = b"%PDF-";
    if let Some(start_idx) = bytes.windows(pdf_magic.len()).position(|w| w == pdf_magic) {
        if start_idx > 0 {
            bytes = bytes[start_idx..].to_vec();
        }
    } else {
        return false; // Not a PDF file
    }

    // 2. Clean up trailer: Find last %%EOF
    let eof_magic = b"%%EOF";
    if let Some(end_idx) = bytes.windows(eof_magic.len()).rposition(|w| w == eof_magic) {
        let truncate_len = end_idx + eof_magic.len();
        if truncate_len < bytes.len() {
            bytes.truncate(truncate_len);
        }
    }

    // 3. Load via lopdf
    let mut doc = match Document::load_mem(&bytes) {
        Ok(d) => d,
        Err(_) => return false,
    };

    // 4. Save to output path - this regenerates the xref table and trailer
    doc.save(output_str).is_ok()
}

#[cfg(test)]
mod tests {
    use super::*;
    use lopdf::{Document, Dictionary, Object};

    fn generate_fake_document() -> Document {
        let mut doc = Document::with_version("1.5");
        let pages_id = doc.new_object_id();
        
        let mut font_dict = Dictionary::new();
        font_dict.set("Type", "Font");
        font_dict.set("Subtype", "Type1");
        font_dict.set("BaseFont", "Courier");
        let font_id = doc.add_object(Object::Dictionary(font_dict));

        let mut font_list = Dictionary::new();
        font_list.set("F1", font_id);
        let mut res_dict = Dictionary::new();
        res_dict.set("Font", Object::Dictionary(font_list));
        let resources_id = doc.add_object(Object::Dictionary(res_dict));

        let mut page_dict = Dictionary::new();
        page_dict.set("Type", "Page");
        page_dict.set("Parent", pages_id);
        page_dict.set("Resources", resources_id);
        page_dict.set("MediaBox", vec![0.into(), 0.into(), 595.into(), 842.into()]);
        let page_id = doc.add_object(Object::Dictionary(page_dict));

        let mut pages_dict = Dictionary::new();
        pages_dict.set("Type", "Pages");
        pages_dict.set("Kids", vec![Object::Reference(page_id)]);
        pages_dict.set("Count", 1);
        doc.objects.insert(pages_id, Object::Dictionary(pages_dict));

        let mut catalog_dict = Dictionary::new();
        catalog_dict.set("Type", "Catalog");
        catalog_dict.set("Pages", Object::Reference(pages_id));
        let catalog_id = doc.add_object(Object::Dictionary(catalog_dict));
        doc.trailer.set("Root", Object::Reference(catalog_id));

        doc
    }

    #[test]
    fn test_merge() {
        let mut doc1 = generate_fake_document();
        let mut doc2 = generate_fake_document();
        
        doc1.save("doc1.pdf").unwrap();
        doc2.save("doc2.pdf").unwrap();

        let paths = std::ffi::CString::new("doc1.pdf;doc2.pdf").unwrap();
        let out = std::ffi::CString::new("merged.pdf").unwrap();
        let success = merge_pdfs(paths.as_ptr(), out.as_ptr());
        assert!(success);

        let merged = Document::load("merged.pdf").unwrap();
        println!("Merged objects count: {}", merged.objects.len());
        let pages = merged.get_pages();
        println!("Merged pages count: {}", pages.len());
        
        // Clean up
        let _ = std::fs::remove_file("doc1.pdf");
        let _ = std::fs::remove_file("doc2.pdf");
        let _ = std::fs::remove_file("merged.pdf");

        assert_eq!(pages.len(), 2);
    }

    #[test]
    fn test_merge_real_files() {
        let doc1 = Document::load("../../_smoke/one_page.pdf").unwrap();
        let doc2 = Document::load("../../_smoke/page_2.pdf").unwrap();
        println!("doc1 pages: {}", doc1.get_pages().len());
        println!("doc2 pages: {}", doc2.get_pages().len());

        let paths = std::ffi::CString::new("../../_smoke/one_page.pdf;../../_smoke/page_2.pdf").unwrap();
        let out = std::ffi::CString::new("merged_real.pdf").unwrap();
        let success = merge_pdfs(paths.as_ptr(), out.as_ptr());
        assert!(success);

        let merged = Document::load("merged_real.pdf").unwrap();
        println!("Merged real objects count: {}", merged.objects.len());
        let pages = merged.get_pages();
        println!("Merged real pages count: {}", pages.len());
        let _ = std::fs::remove_file("merged_real.pdf");
    }

    struct SimpleLogger;
    impl log::Log for SimpleLogger {
        fn enabled(&self, _metadata: &log::Metadata) -> bool { true }
        fn log(&self, record: &log::Record) {
            println!("[LOG] {}: {}", record.level(), record.args());
        }
        fn flush(&self) {}
    }
    static LOGGER: SimpleLogger = SimpleLogger;

    #[test]
    #[ignore = "requires a hardcoded user desktop path; not run in CI"]
    fn test_inspect_user_pdf() {
        let _ = log::set_logger(&LOGGER);
        log::set_max_level(log::LevelFilter::Debug);

        let path = "C:\\Users\\IT\\Desktop\\SB5943-24-06-26\\1.pdf";
        println!("Inspecting user PDF: {}", path);
        
        let mut data = std::fs::read(path).unwrap();
        
        // Print original bytes around some offsets first
        println!("Original bytes at 55140: {:?}", &data[55140..55150]);
        
        super::fix_pdf_offsets(&mut data);
        
        println!("Fixed bytes at 55140: {:?}", &data[55140..55150]);

        match Document::load_mem(&data) {
            Ok(doc) => {
                println!("Load success!");
                println!("Version: {}", doc.version);
                println!("Object count: {}", doc.objects.len());
                println!("Trailer keys: {:?}", doc.trailer.iter().map(|(k, _)| String::from_utf8_lossy(k)).collect::<Vec<_>>());
                println!("Reference table entries: {}", doc.reference_table.entries.len());
            }
            Err(e) => {
                println!("Load failed: {:?}", e);
            }
        }
    }

    #[test]
    fn replace_text_keeps_font_and_size() {
        let original = b"BT\n/F1 14 Tf\n1 0 0 1 50 700 Tm\n(Hello World) Tj\nET\n";
        let out = replace_text_in_content(original, "Hello World", "Xin chao Viet Nam")
            .expect("should find and replace");
        let s = String::from_utf8_lossy(&out);
        assert!(s.contains("/F1 14 Tf"), "font và size phải giữ nguyên: {}", s);
        assert!(s.contains("1 0 0 1 50 700 Tm"), "ma trận vị trí phải giữ nguyên");
        assert!(s.contains("Xin chao Viet Nam"), "chuỗi mới phải xuất hiện");
        assert!(!s.contains("Hello World"), "chuỗi cũ phải biến mất");
    }

    #[test]
    fn replace_text_no_match_returns_none() {
        let original = b"BT\n/F1 14 Tf\n(Old text) Tj\nET\n";
        let out = replace_text_in_content(original, "Not present", "New");
        assert!(out.is_none(), "không tìm thấy thì trả None");
    }

    #[test]
    fn replace_text_in_tj_array() {
        // Parser mới gộp các phần tử thành 1 payload đơn, giữ nguyên font/size.
        let original = b"BT\n/F2 10 Tf\n[ (Hello) -10 (World) ] TJ\nET\n";
        let out = replace_text_in_content(original, "HelloWorld", "XinChao").expect("replace");
        let s = String::from_utf8_lossy(&out);
        assert!(s.contains("/F2 10 Tf"), "font/size trong TJ array phải giữ nguyên");
        assert!(s.contains("XinChao"), "toàn bộ TJ phải đổi thành chuỗi mới");
        assert!(!s.contains("(Hello)"), "chuỗi cũ trong TJ phải biến mất");
    }

    #[test]
    fn replace_text_hex_cid_keeps_font() {
        // Trường hợp font subset CID (Identity-H): text lưu dạng <hex>.
        let original = b"BT\n/C2_1 1 Tf\n95 0 0 95 50 684 Tm\n<004B0041> Tj\nET\n";
        // 004B='K', 0041='A' -> "KA"
        let out = replace_text_in_content(original, "KA", "OK").expect("replace hex");
        let s = String::from_utf8_lossy(&out);
        assert!(s.contains("/C2_1 1 Tf"), "font subset phải giữ nguyên");
        assert!(s.contains("95 0 0 95 50 684 Tm"), "ma trận vị trí phải giữ nguyên");
        // OK = 004F 004B
        assert!(s.contains("<004F004B>"), "phải sinh hex CID của chữ mới: {}", s);
        assert!(!s.contains("<004B0041>"), "hex cũ phải biến mất");
    }

    #[test]
    fn replace_text_tj_split_hex_like_real_file() {
        // Giống TAKAMI CATALOG: [(T)74.3 (AKAMI)]TJ -> "TAKAMI"
        let original = b"BT\n/TT0 1 Tf\n[(T)74.3 (AKAMI)]TJ\nET\n";
        let out = replace_text_in_content(original, "TAKAMI", "KILOMET").expect("replace split");
        let s = String::from_utf8_lossy(&out);
        assert!(s.contains("/TT0 1 Tf"), "font phải giữ nguyên");
        assert!(s.contains("KILOMET"), "phải thay thành chữ mới: {}", s);
        assert!(!s.contains("TAKAMI"), "chữ cũ phải biến mất");
    }

}




fn find_last_startxref(data: &[u8]) -> Option<usize> {
    let pattern = b"startxref";
    if data.len() < pattern.len() {
        return None;
    }
    let start = data.len() - pattern.len();
    for i in (0..=start).rev() {
        if &data[i..i + pattern.len()] == pattern {
            let rest = &data[i + pattern.len()..];
            let mut num_start = 0;
            while num_start < rest.len() && rest[num_start].is_ascii_whitespace() {
                num_start += 1;
            }
            let mut num_end = num_start;
            while num_end < rest.len() && rest[num_end].is_ascii_digit() {
                num_end += 1;
            }
            if num_end > num_start {
                if let Ok(num_str) = std::str::from_utf8(&rest[num_start..num_end]) {
                    if let Ok(offset) = num_str.parse::<usize>() {
                        return Some(offset);
                    }
                }
            }
        }
    }
    None
}

fn parse_prev_from_trailer(trailer_data: &[u8]) -> Option<usize> {
    let pattern = b"/Prev";
    for i in 0..trailer_data.len() {
        if i + pattern.len() <= trailer_data.len() && &trailer_data[i..i+pattern.len()] == pattern {
            let rest = &trailer_data[i + pattern.len()..];
            let mut num_start = 0;
            while num_start < rest.len() && rest[num_start].is_ascii_whitespace() {
                num_start += 1;
            }
            let mut num_end = num_start;
            while num_end < rest.len() && rest[num_end].is_ascii_digit() {
                num_end += 1;
            }
            if num_end > num_start {
                if let Ok(num_str) = std::str::from_utf8(&rest[num_start..num_end]) {
                    if let Ok(prev_offset) = num_str.parse::<usize>() {
                        return Some(prev_offset);
                    }
                }
            }
        }
    }
    None
}

fn fix_xref_section_at(data: &mut [u8], xref_offset: usize) -> Option<usize> {
    let mut idx = xref_offset;
    if idx + 4 <= data.len() && &data[idx..idx+4] == b"xref" {
        idx += 4;
    }
    loop {
        while idx < data.len() && data[idx].is_ascii_whitespace() {
            idx += 1;
        }
        if idx >= data.len() {
            break;
        }
        if idx + 7 <= data.len() && &data[idx..idx+7] == b"trailer" {
            idx += 7;
            return parse_prev_from_trailer(&data[idx..]);
        }
        
        let start_num = idx;
        while idx < data.len() && data[idx].is_ascii_digit() {
            idx += 1;
        }
        if idx == start_num {
            break;
        }
        
        while idx < data.len() && data[idx] == b' ' {
            idx += 1;
        }
        
        let count_start = idx;
        while idx < data.len() && data[idx].is_ascii_digit() {
            idx += 1;
        }
        if idx == count_start {
            break;
        }
        
        let count_str = std::str::from_utf8(&data[count_start..idx]).ok()?;
        let count = count_str.parse::<usize>().ok()?;
        
        while idx < data.len() && (data[idx] == b'\r' || data[idx] == b'\n') {
            idx += 1;
        }
        
        for _ in 0..count {
            if idx + 20 > data.len() {
                break;
            }
            if data[idx + 17] == b'n' {
                if let Ok(offset_str) = std::str::from_utf8(&data[idx..idx+10]) {
                    if let Ok(orig_offset) = offset_str.parse::<usize>() {
                        let data_len = data.len();
                        if orig_offset < data_len {
                            let mut new_offset = orig_offset;
                            while new_offset < data_len && data[new_offset].is_ascii_whitespace() {
                                new_offset += 1;
                            }
                            if new_offset > orig_offset && new_offset < data_len {
                                if data[new_offset].is_ascii_digit() {
                                    let new_offset_str = format!("{:010}", new_offset);
                                    data[idx..idx+10].copy_from_slice(new_offset_str.as_bytes());
                                }
                            }
                        }
                    }
                }
            }
            idx += 20;
        }
    }
    None
}

pub fn fix_pdf_offsets(data: &mut [u8]) {
    let mut next_xref_offset = find_last_startxref(data);
    let mut visited = std::collections::HashSet::new();
    while let Some(offset) = next_xref_offset {
        if visited.contains(&offset) || offset >= data.len() {
            break;
        }
        visited.insert(offset);
        let prev = fix_xref_section_at(data, offset);
        next_xref_offset = prev;
    }
}

// ===========================================================================
// EDIT TEXT WITH REFLOW (giữ font/size/màu, dãn cách trên 1 dòng)
// ===========================================================================

/// Trích font dictionary của một trang (từ Resources/Font).
fn get_page_font_dict<'a>(doc: &'a Document, page_id: lopdf::ObjectId, font_res_name: &[u8]) -> Option<&'a Dictionary> {
    let page = doc.get_object(page_id).ok()?;
    let page_dict = page.as_dict().ok()?;
    let resources = page_dict.get(b"Resources").ok()?;
    let resources = resolve_object(doc, resources).as_dict().ok()?;
    let fonts = resources.get(b"Font").ok()?;
    let fonts = resolve_object(doc, fonts).as_dict().ok()?;
    let font_obj = fonts.get(font_res_name).ok()?;
    let font_obj = resolve_object(doc, font_obj);
    font_obj.as_dict().ok()
}

/// Đọc mảng Widths (per-code width) của font. Trả về (first_char, widths[]).
/// Nếu không có Widths, trả về None để caller dùng fallback.
fn read_font_widths(font_dict: &Dictionary) -> Option<(i64, Vec<f64>)> {
    let first_char = font_dict.get(b"FirstChar").ok().and_then(get_integer)?;
    let widths_obj = font_dict.get(b"Widths").ok()?;
    let widths_arr = widths_obj.as_array().ok()?;
    let mut widths = Vec::with_capacity(widths_arr.len());
    for w in widths_arr {
        match w {
            Object::Integer(i) => widths.push(*i as f64),
            Object::Real(r) => widths.push(*r as f64),
            _ => widths.push(0.0),
        }
    }
    if widths.is_empty() {
        None
    } else {
        Some((first_char, widths))
    }
}

/// Tính độ rộng (đơn vị text space, chưa nhân font size) của một chuỗi.
/// `is_cid`: nếu true, mỗi 2 hex char = 1 code; ngược lại WinAnsi 1 byte = 1 code.
/// `font_size`: kích thước font hiện tại (từ Tf) để quy ra user space.
/// `metrics`: (first_char, widths) nếu có, None = fallback 0.5/char.
fn text_width(
    text: &str,
    font_size: f64,
    metrics: &Option<(i64, Vec<f64>)>,
) -> f64 {
    let avg: f64 = 0.5; // fallback width cho font không có Widths
    let mut total = 0.0;
    for ch in text.chars() {
        let w = match metrics {
            Some((first, widths)) => {
                let code = ch as i64;
                let idx = (code - *first) as usize;
                if idx < widths.len() {
                    widths[idx]
                } else {
                    avg * 1000.0
                }
            }
            None => avg * 1000.0,
        };
        total += w;
    }
    // Widths trong font thường theo đơn vị 1/1000 em -> nhân font_size/1000.
    total * font_size / 1000.0
}

/// Tính toán khoảng trễ (TJ adjustment) cần phân bổ để dãn/thu đoạn text
/// trên cùng 1 dòng. Trả về danh sách các khoảng trễ (negative = dãn ra,
/// positive = thu lại) chèn giữa các glyph của `new_text`.
/// `delta`: chênh lệch độ rộng (new - old) tính theo user space.
fn build_reflow_gaps(new_text: &str, delta: f64) -> Vec<f64> {
    let chars: Vec<char> = new_text.chars().collect();
    let n_gaps = chars.len().saturating_sub(1);
    if n_gaps == 0 {
        return Vec::new();
    }
    // Chia đều delta cho các khoảng giữa glyph.
    // Trong PDF, TJ negative = tiến tới (dãn ra), positive = lùi (thu lại).
    let per = delta / n_gaps as f64;
    let mut gaps = Vec::with_capacity(n_gaps);
    for _ in 0..n_gaps {
        gaps.push(per);
    }
    gaps
}

/// Thay thế text trong 1 content stream, GIỮ NGUYÊN font/size/màu,
/// và TỰ ĐỘNG DÃN CÁCH trên cùng 1 dòng (reflow) khi chữ mới dài/ngắn hơn.
///
/// `font_res_name`: tên resource font đang dùng (vd b"F1"). Nếu None, đoán F1.
/// `font_size`: kích thước font hiện tại (lấy từ Tf gần nhất).
/// `metrics`: font Widths nếu có.
fn replace_text_reflow(
    content: &[u8],
    original: &str,
    replacement: &str,
    font_size: f64,
    metrics: &Option<(i64, Vec<f64>)>,
) -> Option<Vec<u8>> {
    let text = String::from_utf8_lossy(content);
    let chars: Vec<char> = text.chars().collect();
    let len = chars.len();
    let mut result = String::new();
    let mut changed = false;
    let mut i = 0;
    // reused readers
    fn read_literal(chars: &[char], start: usize, len: usize) -> (String, String, usize) {
        let mut depth = 1usize;
        let mut j = start + 1;
        let mut raw = String::from("(");
        let mut buf = String::new();
        while j < len {
            let cc = chars[j];
            if cc == '\\' && j + 1 < len {
                raw.push('\\');
                raw.push(chars[j + 1]);
                match chars[j + 1] {
                    '(' => buf.push('('),
                    ')' => buf.push(')'),
                    '\\' => buf.push('\\'),
                    'n' => buf.push('\n'),
                    'r' => buf.push('\r'),
                    't' => buf.push('\t'),
                    o => buf.push(o),
                }
                j += 2;
                continue;
            }
            if cc == '(' {
                depth += 1; raw.push(cc); buf.push(cc);
            } else if cc == ')' {
                depth -= 1; raw.push(cc);
                if depth == 0 { break; }
            } else {
                raw.push(cc); buf.push(cc);
            }
            j += 1;
        }
        (buf, raw, j + 1)
    }
    fn read_hex(chars: &[char], start: usize, len: usize) -> (String, String, usize) {
        let mut j = start + 1;
        let mut raw = String::from("<");
        while j < len {
            let cc = chars[j];
            if cc == '>' { raw.push('>'); break; }
            raw.push(cc); j += 1;
        }
        let decoded = decode_cid_hex(&raw);
        (decoded, raw, j + 1)
    }
    fn peek_token(chars: &[char], start: usize) -> String {
        let mut k = start;
        while k < chars.len() && chars[k].is_whitespace() { k += 1; }
        let mut tok = String::new();
        while k < chars.len() && !chars[k].is_whitespace() { tok.push(chars[k]); k += 1; }
        tok
    }
    fn skip_token(chars: &[char], start: usize) -> usize {
        let mut k = start;
        while k < chars.len() && chars[k].is_whitespace() { k += 1; }
        while k < chars.len() && !chars[k].is_whitespace() { k += 1; }
        k
    }

    while i < len {
        let c = chars[i];
        if c == '(' {
            let (decoded, raw, next) = read_literal(&chars, i, len);
            let token_after = peek_token(&chars, next);
            if token_after == "Tj" {
                if decoded == original {
                    let w_old = text_width(&decoded, font_size, metrics);
                    let w_new = text_width(replacement, font_size, metrics);
                    let delta = w_new - w_old;
                    let gaps = build_reflow_gaps(replacement, delta);
                    // Build TJ array: [gap0, (seg0), gap1, (seg1), ...]
                    let mut tj = String::from("[");
                    let mut gi = 0;
                    for (idx, rc) in replacement.chars().enumerate() {
                        if idx > 0 && gi < gaps.len() {
                            tj.push_str(&format!(" {:.3} ", gaps[gi]));
                            gi += 1;
                        }
                        tj.push('(');
                        tj.push_str(&escape_pdf_string(&rc.to_string()));
                        tj.push(')');
                    }
                    if replacement.is_empty() && !gaps.is_empty() {
                        tj.push_str(&format!(" {:.3} ", gaps[0]));
                    }
                    tj.push(']');
                    result.push_str(&tj);
                    result.push_str(" TJ");
                    changed = true;
                    i = skip_token(&chars, next);
                } else {
                    result.push_str(&raw);
                    i = next;
                }
            } else {
                result.push_str(&raw);
                i = next;
            }
        } else if c == '<' {
            if i + 1 < len && chars[i + 1] == '<' {
                result.push(c); i += 1; continue;
            }
            let (decoded, raw, next) = read_hex(&chars, i, len);
            let token_after = peek_token(&chars, next);
            if token_after == "Tj" {
                if decoded == original {
                    let w_old = text_width(&decoded, font_size, metrics);
                    let w_new = text_width(replacement, font_size, metrics);
                    let delta = w_new - w_old;
                    let gaps = build_reflow_gaps(replacement, delta);
                    let mut tj = String::from("[");
                    let mut gi = 0;
                    for (idx, rc) in replacement.chars().enumerate() {
                        if idx > 0 && gi < gaps.len() {
                            tj.push_str(&format!(" {:.3} ", gaps[gi]));
                            gi += 1;
                        }
                        // mỗi glyph riêng lẻ thành hex 4 hexits
                        tj.push_str(&format!("<{:04X}>", rc as u32));
                    }
                    if replacement.is_empty() && !gaps.is_empty() {
                        tj.push_str(&format!(" {:.3} ", gaps[0]));
                    }
                    tj.push(']');
                    result.push_str(&tj);
                    result.push_str(" TJ");
                    changed = true;
                    i = skip_token(&chars, next);
                } else {
                    result.push_str(&raw);
                    i = next;
                }
            } else {
                result.push_str(&raw);
                i = next;
            }
        } else if c == '[' {
            let mut depth = 1;
            let mut j = i + 1;
            let mut buf = String::new();
            let mut collected: Vec<(String, usize, usize)> = Vec::new();
            while j < len {
                let cc = chars[j];
                if cc == '\\' && j + 1 < len {
                    buf.push('\\'); buf.push(chars[j + 1]); j += 2; continue;
                }
                if cc == '(' {
                    let (decoded, raw, next) = read_literal(&chars, j, len);
                    let start = buf.len();
                    buf.push_str(&raw);
                    collected.push((decoded, start, buf.len()));
                    j = next;
                } else if cc == '<' && !(j + 1 < len && chars[j + 1] == '<') {
                    let (decoded, raw, next) = read_hex(&chars, j, len);
                    let start = buf.len();
                    buf.push_str(&raw);
                    collected.push((decoded, start, buf.len()));
                    j = next;
                } else if cc == '[' { depth += 1; buf.push(cc); j += 1; }
                else if cc == ']' {
                    depth -= 1; buf.push(cc);
                    if depth == 0 { break; }
                    j += 1;
                } else { buf.push(cc); j += 1; }
            }
            let is_tj = peek_token(&chars, j + 1) == "TJ";
            if is_tj {
                let mut merged = String::new();
                for (decoded, _, _) in &collected { merged.push_str(decoded); }
                if merged == original {
                    let w_old = text_width(&merged, font_size, metrics);
                    let w_new = text_width(replacement, font_size, metrics);
                    let delta = w_new - w_old;
                    let gaps = build_reflow_gaps(replacement, delta);
                    let mut tj = String::from("[");
                    let mut gi = 0;
                    for (idx, rc) in replacement.chars().enumerate() {
                        if idx > 0 && gi < gaps.len() {
                            tj.push_str(&format!(" {:.3} ", gaps[gi]));
                            gi += 1;
                        }
                        tj.push('(');
                        tj.push_str(&escape_pdf_string(&rc.to_string()));
                        tj.push(')');
                    }
                    if replacement.is_empty() && !gaps.is_empty() {
                        tj.push_str(&format!(" {:.3} ", gaps[0]));
                    }
                    tj.push(']');
                    result.push_str(&tj);
                    result.push_str(" TJ");
                    changed = true;
                    i = skip_token(&chars, j + 1);
                } else {
                    let mut replaced = false;
                    for (decoded, start, end) in collected.iter().rev() {
                        if *decoded == original {
                            let repl = format!("({})", escape_pdf_string(replacement));
                            buf.replace_range(*start..*end, &repl);
                            changed = true; replaced = true; break;
                        }
                    }
                    let _ = replaced;
                    result.push('[');
                    result.push_str(&buf);
                }
            } else {
                result.push('[');
                result.push_str(&buf);
            }
            i = j + 1;
        } else {
            result.push(c);
            i += 1;
        }
    }

    if changed { Some(result.into_bytes()) } else { None }
}

/// FFI: thay thế text TRÊN TOÀN BỘ FILE, giữ font/size/màu, tự động dãn cách.
///
/// Quét mọi trang, với mỗi trang lấy font resource (F1) và kích thước font
/// gần nhất trước đoạn text để tính reflow.
#[no_mangle]
pub extern "C" fn replace_text_full(
    pdf_path: *const c_char,
    original_text: *const c_char,
    replacement_text: *const c_char,
    output_path: *const c_char,
) -> bool {
    let pdf_str = match to_str(pdf_path) { Some(s) => s, None => return false };
    let original_str = match to_str(original_text) { Some(s) => s, None => return false };
    let replacement_str = match to_str(replacement_text) { Some(s) => s, None => return false };
    let output_str = match to_str(output_path) { Some(s) => s, None => return false };

    let mut doc = match load_pdf_document(pdf_str) { Ok(d) => d, Err(_) => return false };
    let _ = doc.decompress();

    let pages = doc.get_pages();
    if pages.is_empty() { return false; }

    let mut any_changed = false;

    for (_pageno, &page_id) in &pages {
        // Lấy font metrics (thử F1..F9)
        let mut metrics: Option<(i64, Vec<f64>)> = None;
        for fidx in 1..=9u8 {
            let fname = format!("F{}", fidx);
            if let Some(fd) = get_page_font_dict(&doc, page_id, fname.as_bytes()) {
                if let Some(m) = read_font_widths(fd) {
                    metrics = Some(m);
                    break;
                }
            }
        }

        let streams = match get_page_content_streams(&doc, page_id) {
            Some(s) => s,
            None => continue,
        };

        let mut page_changed = false;
        let mut new_streams: Vec<Vec<u8>> = Vec::with_capacity(streams.len());
        for stream in &streams {
            // Quét content stream để tìm font size gần nhất (Tf) cho mỗi đoạn.
            // Đơn giản: lấy font_size đầu tiên gặp, nếu không có dùng 12.
            let font_size = extract_last_font_size(stream).unwrap_or(12.0);
            match replace_text_reflow(stream, original_str, replacement_str, font_size, &metrics) {
                Some(modified) => {
                    page_changed = true;
                    new_streams.push(modified);
                }
                None => new_streams.push(stream.clone()),
            }
        }

        if page_changed {
            if set_page_content_streams(&mut doc, page_id, new_streams) {
                any_changed = true;
            }
        }
    }

    if !any_changed { return false; }
    doc.save(output_str).is_ok()
}

/// Tìm kích thước font (Tf) cuối cùng/đầu tiên trong content stream.
/// Toán tử: `<size> <font> Tf`. Trả về size đầu tiên gặp.
fn extract_last_font_size(content: &[u8]) -> Option<f64> {
    let text = String::from_utf8_lossy(content);
    let chars: Vec<char> = text.chars().collect();
    let mut found: Option<f64> = None;
    let mut i = 0;
    while i < chars.len() {
        // Tìm token "Tf"
        if chars[i] == 'T' && i + 1 < chars.len() && chars[i + 1] == 'f' {
            // lùi lại tìm số size (token trước Tf)
            let mut k = i;
            while k > 0 && chars[k].is_whitespace() { k -= 1; }
            // k ở cuối token size
            let end = k + 1;
            let mut start = k;
            while start > 0 && !chars[start - 1].is_whitespace() { start -= 1; }
            let tok: String = chars[start..end].iter().collect();
            if let Ok(v) = tok.parse::<f64>() {
                found = Some(v);
            }
        }
        i += 1;
    }
    found
}

#[cfg(test)]
mod edit_text_tests {
    use super::*;

    #[test]
    fn reflow_keeps_font_size_and_color() {
        // Content: set font F1 size 12, fill color rg, show "Hello"
        let original = "BT /F1 12 Tf 0.2 0.4 0.8 rg (Hello) Tj ET";
        let out = replace_text_reflow(
            original.as_bytes(),
            "Hello",
            "Hello World",
            12.0,
            &None,
        ).expect("should replace");
        let s = String::from_utf8_lossy(&out);
        // Font + size + color must be untouched
        assert!(s.contains("/F1 12 Tf"), "font/size must be kept: {}", s);
        assert!(s.contains("0.2 0.4 0.8 rg"), "color must be kept: {}", s);
        // Replacement glyphs must appear inside a TJ array (reflow uses TJ)
        assert!(s.contains("(H)") && s.contains("(W)") && s.contains("(d)"), "replacement glyphs must appear: {}", s);
        assert!(s.contains("TJ"), "reflow should use TJ array: {}", s);
        // No leftover old Tj operator
        assert!(!s.contains("Tj ET"), "old Tj must be converted: {}", s);
        assert!(!s.contains("(Hello)"), "old literal removed: {}", s);
    }

    #[test]
    fn reflow_shorter_text_still_reflows() {
        let original = "BT /F2 10 Tf (LongTextHere) Tj ET";
        let out = replace_text_reflow(
            original.as_bytes(),
            "LongTextHere",
            "Hi",
            10.0,
            &None,
        ).expect("should replace");
        let s = String::from_utf8_lossy(&out);
        assert!(s.contains("/F2 10 Tf"), "font/size kept: {}", s);
        assert!(s.contains("(H)") && s.contains("(i)"), "new text present: {}", s);
        assert!(s.contains("TJ"), "uses TJ: {}", s);
        assert!(!s.contains("Tj ET"), "old Tj converted: {}", s);
    }

    #[test]
    fn reflow_no_match_returns_none() {
        let original = "BT /F1 12 Tf (Nothing) Tj ET";
        let out = replace_text_reflow(original.as_bytes(), "Missing", "X", 12.0, &None);
        assert!(out.is_none(), "no match -> None");
    }

    #[test]
    fn reflow_empty_replacement_deletes_text() {
        let original = "BT /F1 12 Tf (DeleteMe) Tj ET";
        let out = replace_text_reflow(original.as_bytes(), "DeleteMe", "", 12.0, &None)
            .expect("should replace");
        let s = String::from_utf8_lossy(&out);
        assert!(!s.contains("DeleteMe"), "text removed: {}", s);
        assert!(s.contains("/F1 12 Tf"), "font kept: {}", s);
    }
}

