use std::collections::BTreeMap;
use std::ffi::CStr;
use std::os::raw::c_char;
use lopdf::{xobject, Dictionary, Document, Object};
use image::ImageEncoder;

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
        doc.renumber_objects_with(max_id);
        max_id = doc.max_id + 1;

        // Extract catalog and pages
        let mut catalog_id = None;
        let mut pages_id = None;

        for (id, object) in doc.objects.iter() {
            if let Ok(dict) = object.as_dict() {
                let type_name = dict.type_name().unwrap_or("");
                if type_name == "Catalog" {
                    catalog_id = Some(*id);
                } else if type_name == "Pages" {
                    pages_id = Some(*id);
                }
            }
        }

        if let Some(pages_id) = pages_id {
            if let Ok(kids_val) = doc.get_object(pages_id).and_then(|obj| obj.as_dict()).and_then(|dict| dict.get(b"Kids")) {
                if let Ok(kids_arr) = kids_val.as_array() {
                    for kid in kids_arr {
                        if let Ok(ref_id) = kid.as_reference() {
                            pages_kids.push(Object::Reference(ref_id));
                        }
                    }
                }
            }
        }

        // Add all objects to the target dictionary, except catalog and root pages
        for (id, object) in doc.objects {
            if Some(id) != catalog_id && Some(id) != pages_id {
                target_objects.insert(id, object);
            }
        }

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

    // Tìm tất cả các stream của Image XObject
    let mut image_ids = Vec::new();
    for (id, object) in doc.objects.iter() {
        if let Ok(stream) = object.as_stream() {
            let is_image = stream.dict.get(b"Subtype").ok()
                .and_then(|o| o.as_name().ok())
                .map(|n| n == b"Image")
                .unwrap_or(false);
            if is_image {
                image_ids.push(*id);
            }
        }
    }

    if image_ids.is_empty() {
        return doc.save(output_str).is_ok();
    }

    for id in image_ids {
        if let Ok(Object::Stream(ref mut stream)) = doc.get_object_mut(id) {
            let width = stream.dict.get(b"Width").ok().and_then(|o| get_integer(o)).unwrap_or(0) as u32;
            let height = stream.dict.get(b"Height").ok().and_then(|o| get_integer(o)).unwrap_or(0) as u32;
            let bits = stream.dict.get(b"BitsPerComponent").ok().and_then(|o| get_integer(o)).unwrap_or(8) as u32;

            if width == 0 || height == 0 || bits != 8 {
                continue;
            }

            let color_space: &[u8] = stream.dict.get(b"ColorSpace").ok()
                .and_then(|o| o.as_name().ok())
                .unwrap_or(&[]);

            if let Ok(decompressed) = stream.decompressed_content() {
                let mut compressed_bytes = Vec::new();
                let mut success = false;

                if color_space == b"DeviceRGB" && decompressed.len() == (width * height * 3) as usize {
                    let mut jpeg_data = Vec::new();
                    let encoder = image::codecs::jpeg::JpegEncoder::new_with_quality(&mut jpeg_data, image_quality);
                    if encoder.write_image(&decompressed, width, height, image::ColorType::Rgb8).is_ok() {
                        compressed_bytes = jpeg_data;
                        success = true;
                    }
                } else if color_space == b"DeviceGray" && decompressed.len() == (width * height) as usize {
                    let mut jpeg_data = Vec::new();
                    let encoder = image::codecs::jpeg::JpegEncoder::new_with_quality(&mut jpeg_data, image_quality);
                    if encoder.write_image(&decompressed, width, height, image::ColorType::L8).is_ok() {
                        compressed_bytes = jpeg_data;
                        success = true;
                    }
                } else {
                    let filter: &[u8] = stream.dict.get(b"Filter").ok()
                        .and_then(|o| o.as_name().ok())
                        .unwrap_or(&[]);
                    if filter == b"DCTDecode" {
                        if let Ok(img) = image::load_from_memory_with_format(&decompressed, image::ImageFormat::Jpeg) {
                            let mut jpeg_data = Vec::new();
                            let encoder = image::codecs::jpeg::JpegEncoder::new_with_quality(&mut jpeg_data, image_quality);
                            let rgb = img.to_rgb8();
                            if encoder.write_image(&rgb, rgb.width(), rgb.height(), image::ColorType::Rgb8).is_ok() {
                                compressed_bytes = jpeg_data;
                                success = true;
                            }
                        }
                    }
                }

                if success && !compressed_bytes.is_empty() {
                    stream.set_content(compressed_bytes);
                    stream.dict.set("Filter", Object::Name(b"DCTDecode".to_vec()));
                }
            }
        }
    }

    doc.save(output_str).is_ok()
}
