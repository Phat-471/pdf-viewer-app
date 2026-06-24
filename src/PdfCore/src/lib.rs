use std::collections::BTreeMap;
use std::ffi::CStr;
use std::os::raw::c_char;
use std::io::Write;
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

    let paths: Vec<&str> = paths_str.split(';').filter(|s| !s.is_empty()).collect();
    if paths.is_empty() {
        return false;
    }

    let mut target_doc = Document::with_version("1.5");
    let mut documents = Vec::new();

    // Load documents
    for path in &paths {
        match Document::load(path) {
            Ok(doc) => documents.push(doc),
            Err(_) => return false,
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
        // Resolve Catalog and Pages root IDs using trailer first (before renumbering)
        let mut catalog_id = doc.trailer.get(b"Root").and_then(|obj| obj.as_reference()).ok();
        let mut pages_id = None;
        if let Some(cat_id) = catalog_id {
            if let Ok(cat_dict) = doc.get_object(cat_id).and_then(|obj| obj.as_dict()) {
                pages_id = cat_dict.get(b"Pages").and_then(|obj| obj.as_reference()).ok();
            }
        }

        // Fallback search if trailer is missing Root or Pages
        if catalog_id.is_none() || pages_id.is_none() {
            for (id, object) in doc.objects.iter() {
                if let Ok(dict) = object.as_dict() {
                    let type_name = dict.type_name().unwrap_or("");
                    if type_name == "Catalog" && catalog_id.is_none() {
                        catalog_id = Some(*id);
                    } else if type_name == "Pages" && pages_id.is_none() {
                        pages_id = Some(*id);
                    }
                }
            }
        }

        // Collect all leaf Page objects using get_pages() BEFORE renumbering
        let pages = doc.get_pages();

        // Get sorted keys of doc.objects before renumbering
        let keys: Vec<lopdf::ObjectId> = doc.objects.keys().cloned().collect();

        // Renumber objects
        doc.renumber_objects_with(max_id);

        // Map original page IDs to new renumbered page IDs
        for (_page_num, page_id) in pages {
            if let Ok(idx) = keys.binary_search(&page_id) {
                let new_page_id = (max_id + idx as u32, 0);
                pages_kids.push(Object::Reference(new_page_id));
            }
        }

        // Map catalog and pages root to their new renumbered IDs
        let new_catalog_id = catalog_id.and_then(|orig_id| {
            keys.binary_search(&orig_id).ok().map(|idx| (max_id + idx as u32, 0))
        });
        let new_pages_id = pages_id.and_then(|orig_id| {
            keys.binary_search(&orig_id).ok().map(|idx| (max_id + idx as u32, 0))
        });

        // Add all objects to the target dictionary, except catalog and root pages
        for (id, object) in doc.objects {
            if Some(id) != new_catalog_id && Some(id) != new_pages_id {
                target_objects.insert(id, object);
            }
        }

        max_id = doc.max_id + 1;

        if let Some(cb) = progress_callback {
            cb((i + 1) as u32, total_files);
        }
    }

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
    for kid in &pages_kids {
        if let Ok(ref_id) = kid.as_reference() {
            if let Ok(Object::Dictionary(ref mut kid_dict)) = target_doc.get_object_mut(ref_id) {
                kid_dict.set("Parent", Object::Reference(pages_id));
            }
        }
    }

    target_doc.save(output_str).is_ok()
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

    let mut doc = match Document::load(pdf_str) {
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

    let mut doc = match Document::load(pdf_str) {
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

    let mut doc = match Document::load(pdf_str) {
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

    let mut doc = match Document::load(pdf_str) {
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

    let mut doc = match Document::load(pdf_str) {
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

    let mut doc = match Document::load(pdf_str) {
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

    let mut doc = match Document::load(pdf_str) {
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

    let mut doc = match Document::load(pdf_str) {
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

    let mut doc = match Document::load(pdf_str) {
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

    let mut doc = match Document::load(pdf_str) {
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

    let mut doc = match Document::load(pdf_str) {
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

    let mut doc = match Document::load(pdf_str) {
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

    let doc = match Document::load(pdf_str) {
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
}


