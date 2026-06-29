<?php
/**
 * VN Product Filter - Main Loader
 * Nạp toàn bộ module và khởi tạo các class
 */
if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

// Chỉ load nếu WooCommerce đang active
if ( ! class_exists( 'WooCommerce' ) ) {
	return;
}

require_once __DIR__ . '/class-filter-core.php';
require_once __DIR__ . '/class-filter-ajax.php';
require_once __DIR__ . '/class-filter-shortcode.php';
require_once __DIR__ . '/class-filter-widget.php';
require_once __DIR__ . '/class-filter-admin.php';

/**
 * Class chính — khởi tạo toàn bộ module filter
 */
class VN_Product_Filter {

	public function __construct() {
		// AJAX handlers (cả frontend lẫn admin-ajax)
		new VN_Filter_Ajax();

		// Shortcodes frontend
		new VN_Filter_Shortcode();

		// Đăng ký Widget
		add_action( 'widgets_init', [ $this, 'register_widget' ] );

		// Xóa cache bộ lọc khi cập nhật sản phẩm
		add_action( 'save_post_product', [ 'VN_Filter_Core', 'clear_filter_cache' ] );
		add_action( 'woocommerce_product_import_complete', [ 'VN_Filter_Core', 'clear_filter_cache' ] );
		add_action( 'clean_post_cache', [ 'VN_Filter_Core', 'clear_filter_cache' ] );

		// Admin: script cho trang filter (menu được đăng ký bởi class-admin-menu.php)
		if ( is_admin() ) {
			add_action( 'admin_init',             [ 'VN_Filter_Admin', 'save_settings_static' ] );
			add_action( 'admin_enqueue_scripts',  [ $this, 'enqueue_admin_scripts' ] );
		}
	}

	/**
	 * Đăng ký widget
	 */
	public function register_widget() {
		register_widget( 'VN_Filter_Widget' );
	}


	/**
	 * Enqueue admin scripts cho trang filter settings
	 */
	public function enqueue_admin_scripts( $hook ) {
		if ( strpos( $hook, 'vn-filter-settings' ) === false ) return;
		wp_enqueue_style( 'vn-privacy-admin', VN_PRIVACY_URL . 'assets/admin.css', [], VN_PRIVACY_VERSION );
	}
}
