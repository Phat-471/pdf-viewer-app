//! Module xử lý Sửa Chữ PDF Trực Tiếp (Text Editor Module)
//! Được tách biệt thành 1 tệp riêng để dễ dàng nâng cấp & bảo trì độc lập.

use std::ffi::CStr;
use std::os::raw::c_char;

#[repr(C)]
pub struct RawTextRegion {
    pub x: f64,
    pub y: f64,
    pub width: f64,
    pub height: f64,
    pub font_size: f64,
    pub obj_type: i32, // 1: Vector, 2: Subset CID, 3: Scanned OCR
}

/// Trích xuất danh sách đối tượng chữ có thể sửa trên trang
#[no_mangle]
pub extern "C" fn pdf_get_page_text_objects(
    _pdf_path: *const c_char,
    _page_num: i32,
    _out_regions: *mut RawTextRegion,
    _max_count: i32,
) -> i32 {
    // TODO: Sử dụng FPDF_PAGEOBJECT Text Objects trong Pdfium C-API để trích xuất vị trí chuẩn xác
    0
}

/// Sửa đổi nội dung chữ của một Text Object cụ thể
#[no_mangle]
pub extern "C" fn pdf_replace_text_object(
    _pdf_path: *const c_char,
    _page_num: i32,
    _x: f64,
    _y: f64,
    _width: f64,
    _height: f64,
    _new_text: *const c_char,
    _output_path: *const c_char,
) -> bool {
    // TODO: Thay thế chữ trên FPDF_PAGEOBJECT hoặc Re-embed Font cho Subset Font
    true
}
