<?php
if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

class VN_Privacy_Admin_Menu {

	public static function register_menu_pages() {
		// Parent: Danh sách biểu mẫu
		add_menu_page(
			'VN Privacy Forms',
			'VN Privacy Forms',
			'manage_options',
			'vn-privacy-forms',
			[ 'VN_Privacy_Admin_Forms', 'render_forms_page' ],
			'dashicons-shield',
			30
		);

		// Submenu: Danh sách biểu mẫu
		add_submenu_page(
			'vn-privacy-forms',
			'Danh sách Biểu mẫu',
			'📋 Biểu mẫu',
			'manage_options',
			'vn-privacy-forms',
			[ 'VN_Privacy_Admin_Forms', 'render_forms_page' ]
		);

		// Submenu: Tạo / Chỉnh sửa biểu mẫu (Ẩn khỏi sidebar bằng parent_slug = null)
		add_submenu_page(
			null,
			'Tạo Biểu Mẫu Mới',
			'➕ Tạo Form mới',
			'manage_options',
			'vn-privacy-create-form',
			[ 'VN_Privacy_Admin_Forms', 'render_create_form_page' ]
		);

		// Submenu: Danh sách đăng ký
		add_submenu_page(
			'vn-privacy-forms',
			'Nhật ký Đồng ý',
			'💾 Nhật ký đồng ý',
			'manage_options',
			'vn-privacy-entries',
			[ 'VN_Privacy_Admin_Entries', 'render_entries_page' ]
		);

		// Submenu: Bộ lọc Sản phẩm (luôn hiển thị, yêu cầu WooCommerce)
		add_submenu_page(
			'vn-privacy-forms',
			'Bộ lọc Sản phẩm',
			'🔍 Bộ lọc SP',
			'manage_options',
			'vn-filter-settings',
			[ 'VN_Privacy_Admin_Menu', 'render_filter_page' ]
		);

		// Submenu: Cấu hình & Tối ưu trung tâm
		add_submenu_page(
			'vn-privacy-forms',
			'Cấu hình & Tối ưu hệ thống',
			'⚙️ Cấu hình & Tối ưu',
			'manage_options',
			'vn-settings',
			[ 'VN_Privacy_Admin_Settings', 'render_page' ]
		);
	}

	/**
	 * Trang bộ lọc — delegate sang VN_Filter_Admin::render_page() nếu WooCommerce active
	 */
	public static function render_filter_page() {
		if ( class_exists( 'VN_Filter_Admin' ) ) {
			VN_Filter_Admin::render_page();
			return;
		}
		// WooCommerce chưa cài
		?>
		<div class="wrap">
			<h1>🔍 VN Product Filter</h1>
			<div class="notice notice-warning" style="padding:20px;border-radius:8px;">
				<h3 style="margin-top:0;">⚠️ Yêu cầu WooCommerce</h3>
				<p>Module <strong>Bộ lọc Sản phẩm</strong> yêu cầu plugin <strong>WooCommerce</strong> phải được cài đặt và kích hoạt.</p>
				<p>
					<a href="<?php echo admin_url( 'plugin-install.php?s=woocommerce&tab=search&type=term' ); ?>" class="button button-primary">
						🛒 Cài đặt WooCommerce
					</a>
				</p>
				<hr>
				<p style="color:#555;font-size:13px;">Sau khi kích hoạt WooCommerce, trang này sẽ hiện đầy đủ các cài đặt cho bộ lọc sản phẩm.</p>
			</div>
		</div>
		<?php
	}
}

