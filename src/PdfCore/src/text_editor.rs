//! Module xử lý Sửa Chữ PDF Trực Tiếp (PdfCraft Text Editor Module)
//! Tích hợp 100% động cơ biên tập PDF chuyên sâu kế thừa từ pdfcraft:
//! - Quản lý phiên làm việc tài liệu Document Session thuần Rust
//! - Nhóm dòng thành Đoạn văn bản (TextBlock Grouping)
//! - Word wrap tự động reflow khi thay đổi độ rộng
//! - Tự động tạo Type 3 Vector Fallback Font cho tiếng Việt và ký tự thiếu
//! - Cho phép sửa liên tiếp nhiều block chữ (block 1, block 2, block N) không giới hạn
//! - Hỗ trợ Undo / Redo nhiều tầng trong bộ nhớ RAM
//! - Giữ nguyên 100% định dạng, màu sắc và cấu trúc gốc của trang

use std::collections::HashMap;
use std::ffi::CString;
use std::os::raw::c_char;
use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::{Arc, Mutex};

use serde::Serialize;
use pdfcraft_cos::{Document, SaveOptions, write_full, write_incremental};
use pdfcraft_edit::{BlockStyle, clear_block, rewrite_block, text_blocks};

// ---------------------------------------------------------------------------
// Structs cho FFI
// ---------------------------------------------------------------------------

#[repr(C)]
#[derive(Debug, Clone, Copy)]
pub struct RawTextRegion {
    pub x: f64,
    pub y: f64,
    pub width: f64,
    pub height: f64,
    pub font_size: f64,
    pub obj_type: i32, // 1: Vector, 2: Subset CID, 3: Scanned OCR
}

#[derive(Serialize)]
pub struct PdfBlockExportInfo {
    pub index: usize,
    pub text: String,
    pub editor_text: String,
    pub reflow_text: String,
    pub rect: [f64; 4], // [min_x, min_y, max_x, max_y]
    pub font_name: String,
    pub font_size: f64,
    pub bold: bool,
    pub italic: bool,
    pub color_rgb: [f64; 3], // [r, g, b] từ 0.0 đến 1.0
    pub rotation: f64,       // độ xoay góc
    pub invisible: bool,     // text layer của bản quét OCR
}

// ---------------------------------------------------------------------------
// Session State & Global Store
// ---------------------------------------------------------------------------

struct DocumentSession {
    doc: Document,
    undo_stack: Vec<Document>,
    redo_stack: Vec<Document>,
}

const MAX_UNDO_DEPTH: usize = 50;

static NEXT_SESSION_ID: AtomicU64 = AtomicU64::new(1);
static SESSIONS: Mutex<Option<HashMap<u64, DocumentSession>>> = Mutex::new(None);

fn with_sessions<F, R>(f: F) -> R
where
    F: FnOnce(&mut HashMap<u64, DocumentSession>) -> R,
{
    let mut guard = SESSIONS.lock().unwrap_or_else(|poisoned| poisoned.into_inner());
    let map = guard.get_or_insert_with(HashMap::new);
    f(map)
}

// ---------------------------------------------------------------------------
// Helpers nội bộ
// ---------------------------------------------------------------------------

fn load_cos_document(path: &str) -> Result<Document, String> {
    let bytes = std::fs::read(path).map_err(|e| format!("Không đọc được tệp PDF: {}", e))?;
    Document::open(Arc::new(bytes)).map_err(|e| format!("Lỗi phân tích tài liệu PDF: {:?}", e))
}

fn save_cos_document(doc: &Document, out_path: &str, incremental: bool) -> Result<(), String> {
    let opts = SaveOptions::default();
    let data = if incremental {
        write_incremental(doc, &opts)
            .or_else(|_| write_full(doc, &opts))
            .map_err(|e| format!("Lỗi ghi gia số tài liệu PDF: {:?}", e))?
    } else {
        write_full(doc, &opts).map_err(|e| format!("Lỗi ghi tài liệu PDF: {:?}", e))?
    };
    std::fs::write(out_path, data).map_err(|e| format!("Không ghi được tệp PDF kết quả: {}", e))?;
    Ok(())
}

fn extract_blocks_json(doc: &Document, page_index: usize) -> Option<String> {
    let blocks = text_blocks(doc, page_index).ok()?;
    let list: Vec<PdfBlockExportInfo> = blocks
        .into_iter()
        .enumerate()
        .map(|(idx, b)| PdfBlockExportInfo {
            index: idx,
            text: b.text,
            editor_text: b.editor_text,
            reflow_text: b.reflow_text,
            rect: b.rect,
            font_name: b.base_font,
            font_size: b.size,
            bold: b.bold,
            italic: b.italic,
            color_rgb: b.color,
            rotation: b.rotation,
            invisible: b.invisible,
        })
        .collect();

    serde_json::to_string(&list).ok()
}

// ---------------------------------------------------------------------------
// C FFI APIs: Quản lý Phiên Làm Việc (Document Session) thuần PdfCraft
// ---------------------------------------------------------------------------

/// Giải phóng chuỗi C được cấp phát bởi PdfCore
#[no_mangle]
pub extern "C" fn pdf_free_string(s: *mut c_char) {
    if !s.is_null() {
        unsafe {
            let _ = CString::from_raw(s);
        }
    }
}

/// Mở phiên làm việc biên tập tài liệu PDF.
/// Nạp toàn bộ cây đối tượng PDF vào bộ nhớ RAM.
/// Trả về ID phiên làm việc (u64 > 0), hoặc 0 nếu thất bại.
#[no_mangle]
pub extern "C" fn pdf_session_open(pdf_path: *const c_char) -> u64 {
    let path = match crate::to_str(pdf_path) {
        Some(p) => p,
        None => return 0,
    };

    let doc = match load_cos_document(path) {
        Ok(d) => d,
        Err(_) => return 0,
    };

    let session_id = NEXT_SESSION_ID.fetch_add(1, Ordering::SeqCst);
    with_sessions(|sessions| {
        sessions.insert(
            session_id,
            DocumentSession {
                doc,
                undo_stack: Vec::new(),
                redo_stack: Vec::new(),
            },
        );
    });

    session_id
}

/// Đọc danh sách tất cả các khối văn bản (TextBlock) của trang trong phiên hiện tại.
/// Dữ liệu luôn phản ánh trạng thái mới nhất của tài liệu sau các lần sửa trước đó.
#[no_mangle]
pub extern "C" fn pdf_session_get_blocks_json(session_id: u64, page_num: i32) -> *mut c_char {
    if session_id == 0 || page_num < 1 {
        return std::ptr::null_mut();
    }

    let page_index = (page_num - 1) as usize;
    let json_opt = with_sessions(|sessions| {
        let session = sessions.get(&session_id)?;
        extract_blocks_json(&session.doc, page_index)
    });

    match json_opt {
        Some(json_str) => match CString::new(json_str) {
            Ok(c_str) => c_str.into_raw(),
            Err(_) => std::ptr::null_mut(),
        },
        None => std::ptr::null_mut(),
    }
}

/// Áp dụng sửa nội dung một khối văn bản (commit edit) trong phiên làm việc.
/// Tự động bù glyphs Type 3 vector font và dàn trang theo new_width nếu có.
/// Lưu trạng thái cũ vào undo stack.
#[no_mangle]
pub extern "C" fn pdf_session_commit_block(
    session_id: u64,
    page_num: i32,
    block_index: i32,
    new_text: *const c_char,
    new_width: f64,
) -> bool {
    if session_id == 0 || page_num < 1 || block_index < 0 {
        return false;
    }

    let text = match crate::to_str(new_text) {
        Some(t) => t,
        None => "",
    };

    let page_index = (page_num - 1) as usize;
    let b_idx = block_index as usize;

    let mut style = BlockStyle::default();
    if new_width.is_finite() && new_width > 0.0 {
        style.width = Some(new_width);
    }

    with_sessions(|sessions| {
        let session = match sessions.get_mut(&session_id) {
            Some(s) => s,
            None => return false,
        };

        let prev_doc = session.doc.clone();
        match rewrite_block(&mut session.doc, page_index, b_idx, Some(text), &style) {
            Ok(_) => {
                if session.undo_stack.len() >= MAX_UNDO_DEPTH {
                    session.undo_stack.remove(0);
                }
                session.undo_stack.push(prev_doc);
                session.redo_stack.clear();
                true
            }
            Err(e) => {
                eprintln!("[PdfCore] rewrite_block failed for page {} block {}: {:?}", page_num, block_index, e);
                session.doc = prev_doc;
                false
            }
        }
    })
}

/// Xóa một khối văn bản trên trang trong phiên làm việc.
#[no_mangle]
pub extern "C" fn pdf_session_delete_block(
    session_id: u64,
    page_num: i32,
    block_index: i32,
) -> bool {
    if session_id == 0 || page_num < 1 || block_index < 0 {
        return false;
    }

    let page_index = (page_num - 1) as usize;
    let b_idx = block_index as usize;
    let style = BlockStyle::default();

    with_sessions(|sessions| {
        let session = match sessions.get_mut(&session_id) {
            Some(s) => s,
            None => return false,
        };

        let prev_doc = session.doc.clone();
        if rewrite_block(&mut session.doc, page_index, b_idx, Some(""), &style).is_err() {
            session.doc = prev_doc;
            return false;
        }

        if session.undo_stack.len() >= MAX_UNDO_DEPTH {
            session.undo_stack.remove(0);
        }
        session.undo_stack.push(prev_doc);
        session.redo_stack.clear();
        true
    })
}

/// Di chuyển vị trí khối văn bản trong phiên làm việc.
#[no_mangle]
pub extern "C" fn pdf_session_move_block(
    session_id: u64,
    page_num: i32,
    block_index: i32,
    dx: f64,
    dy: f64,
) -> bool {
    if session_id == 0 || page_num < 1 || block_index < 0 {
        return false;
    }

    let page_index = (page_num - 1) as usize;
    let b_idx = block_index as usize;

    let mut style = BlockStyle::default();
    if dx.is_finite() && dy.is_finite() {
        style.offset = Some([dx, dy]);
    }

    with_sessions(|sessions| {
        let session = match sessions.get_mut(&session_id) {
            Some(s) => s,
            None => return false,
        };

        let prev_doc = session.doc.clone();
        if rewrite_block(&mut session.doc, page_index, b_idx, None, &style).is_err() {
            session.doc = prev_doc;
            return false;
        }

        if session.undo_stack.len() >= MAX_UNDO_DEPTH {
            session.undo_stack.remove(0);
        }
        session.undo_stack.push(prev_doc);
        session.redo_stack.clear();
        true
    })
}

/// Xuất toàn bộ tài liệu của phiên làm việc ra tệp PDF trên đĩa.
#[no_mangle]
pub extern "C" fn pdf_session_export(
    session_id: u64,
    output_path: *const c_char,
    incremental: bool,
) -> bool {
    if session_id == 0 {
        return false;
    }

    let out = match crate::to_str(output_path) {
        Some(o) => o,
        None => return false,
    };

    with_sessions(|sessions| {
        let session = match sessions.get(&session_id) {
            Some(s) => s,
            None => return false,
        };

        save_cos_document(&session.doc, out, incremental).is_ok()
    })
}

/// Hoàn tác bước sửa chữ gần nhất trong phiên làm việc.
#[no_mangle]
pub extern "C" fn pdf_session_undo(session_id: u64) -> bool {
    if session_id == 0 {
        return false;
    }

    with_sessions(|sessions| {
        let session = match sessions.get_mut(&session_id) {
            Some(s) => s,
            None => return false,
        };

        if let Some(prev) = session.undo_stack.pop() {
            let current = std::mem::replace(&mut session.doc, prev);
            session.redo_stack.push(current);
            true
        } else {
            false
        }
    })
}

/// Làm lại bước sửa chữ đã hoàn tác trong phiên làm việc.
#[no_mangle]
pub extern "C" fn pdf_session_redo(session_id: u64) -> bool {
    if session_id == 0 {
        return false;
    }

    with_sessions(|sessions| {
        let session = match sessions.get_mut(&session_id) {
            Some(s) => s,
            None => return false,
        };

        if let Some(next) = session.redo_stack.pop() {
            let current = std::mem::replace(&mut session.doc, next);
            session.undo_stack.push(current);
            true
        } else {
            false
        }
    })
}

/// Kiểm tra xem phiên làm việc có thể hoàn tác hay không.
#[no_mangle]
pub extern "C" fn pdf_session_can_undo(session_id: u64) -> bool {
    if session_id == 0 {
        return false;
    }

    with_sessions(|sessions| {
        sessions
            .get(&session_id)
            .map(|s| !s.undo_stack.is_empty())
            .unwrap_or(false)
    })
}

/// Kiểm tra xem phiên làm việc có thể làm lại hay không.
#[no_mangle]
pub extern "C" fn pdf_session_can_redo(session_id: u64) -> bool {
    if session_id == 0 {
        return false;
    }

    with_sessions(|sessions| {
        sessions
            .get(&session_id)
            .map(|s| !s.redo_stack.is_empty())
            .unwrap_or(false)
    })
}

/// Đóng và giải phóng toàn bộ tài nguyên phiên làm việc khỏi bộ nhớ RAM.
#[no_mangle]
pub extern "C" fn pdf_session_close(session_id: u64) {
    if session_id == 0 {
        return;
    }

    with_sessions(|sessions| {
        sessions.remove(&session_id);
    });
}

// ---------------------------------------------------------------------------
// Standalone APIs (Tương thích ngược)
// ---------------------------------------------------------------------------

#[no_mangle]
pub extern "C" fn pdf_get_text_blocks_json(
    pdf_path: *const c_char,
    page_num: i32,
) -> *mut c_char {
    let path = match crate::to_str(pdf_path) {
        Some(p) => p,
        None => return std::ptr::null_mut(),
    };

    if page_num < 1 {
        return std::ptr::null_mut();
    }

    let doc = match load_cos_document(path) {
        Ok(d) => d,
        Err(_) => return std::ptr::null_mut(),
    };

    let page_index = (page_num - 1) as usize;
    match extract_blocks_json(&doc, page_index) {
        Some(json_str) => match CString::new(json_str) {
            Ok(c_str) => c_str.into_raw(),
            Err(_) => std::ptr::null_mut(),
        },
        None => std::ptr::null_mut(),
    }
}

#[no_mangle]
pub extern "C" fn pdf_generate_edit_preview(
    pdf_path: *const c_char,
    page_num: i32,
    block_index: i32,
    output_path: *const c_char,
) -> bool {
    let path = match crate::to_str(pdf_path) {
        Some(p) => p,
        None => return false,
    };
    let out = match crate::to_str(output_path) {
        Some(o) => o,
        None => return false,
    };

    if page_num < 1 || block_index < 0 {
        return false;
    }

    let mut doc = match load_cos_document(path) {
        Ok(d) => d,
        Err(_) => return false,
    };

    let page_index = (page_num - 1) as usize;
    let b_idx = block_index as usize;

    if clear_block(&mut doc, page_index, b_idx).is_err() {
        return false;
    }

    save_cos_document(&doc, out, true).is_ok()
}

#[no_mangle]
pub extern "C" fn pdf_commit_block_edit(
    pdf_path: *const c_char,
    page_num: i32,
    block_index: i32,
    new_text: *const c_char,
    new_width: f64,
    output_path: *const c_char,
) -> bool {
    let path = match crate::to_str(pdf_path) {
        Some(p) => p,
        None => return false,
    };
    let out = match crate::to_str(output_path) {
        Some(o) => o,
        None => return false,
    };
    let text = match crate::to_str(new_text) {
        Some(t) => t,
        None => "",
    };

    if page_num < 1 || block_index < 0 {
        return false;
    }

    let mut doc = match load_cos_document(path) {
        Ok(d) => d,
        Err(_) => return false,
    };

    let page_index = (page_num - 1) as usize;
    let b_idx = block_index as usize;

    let mut style = BlockStyle::default();
    if new_width.is_finite() && new_width > 0.0 {
        style.width = Some(new_width);
    }

    if rewrite_block(&mut doc, page_index, b_idx, Some(text), &style).is_err() {
        return false;
    }

    save_cos_document(&doc, out, false).is_ok()
}

#[no_mangle]
pub extern "C" fn pdf_delete_block(
    pdf_path: *const c_char,
    page_num: i32,
    block_index: i32,
    output_path: *const c_char,
) -> bool {
    let path = match crate::to_str(pdf_path) {
        Some(p) => p,
        None => return false,
    };
    let out = match crate::to_str(output_path) {
        Some(o) => o,
        None => return false,
    };

    if page_num < 1 || block_index < 0 {
        return false;
    }

    let mut doc = match load_cos_document(path) {
        Ok(d) => d,
        Err(_) => return false,
    };

    let page_index = (page_num - 1) as usize;
    let b_idx = block_index as usize;

    let style = BlockStyle::default();
    if rewrite_block(&mut doc, page_index, b_idx, Some(""), &style).is_err() {
        return false;
    }

    save_cos_document(&doc, out, false).is_ok()
}

#[no_mangle]
pub extern "C" fn pdf_move_block(
    pdf_path: *const c_char,
    page_num: i32,
    block_index: i32,
    dx: f64,
    dy: f64,
    output_path: *const c_char,
) -> bool {
    let path = match crate::to_str(pdf_path) {
        Some(p) => p,
        None => return false,
    };
    let out = match crate::to_str(output_path) {
        Some(o) => o,
        None => return false,
    };

    if page_num < 1 || block_index < 0 {
        return false;
    }

    let mut doc = match load_cos_document(path) {
        Ok(d) => d,
        Err(_) => return false,
    };

    let page_index = (page_num - 1) as usize;
    let b_idx = block_index as usize;

    let mut style = BlockStyle::default();
    if dx.is_finite() && dy.is_finite() {
        style.offset = Some([dx, dy]);
    }

    if rewrite_block(&mut doc, page_index, b_idx, None, &style).is_err() {
        return false;
    }

    save_cos_document(&doc, out, false).is_ok()
}
