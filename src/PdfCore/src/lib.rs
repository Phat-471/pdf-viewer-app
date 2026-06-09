use std::collections::BTreeMap;
use std::ffi::CStr;
use std::os::raw::c_char;
use lopdf::{xobject, Dictionary, Document, Object};

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
