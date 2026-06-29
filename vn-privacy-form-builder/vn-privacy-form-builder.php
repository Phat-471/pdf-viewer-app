<?php
/**
 * Plugin Name: VN Personal Data Protection & Consent Forms
 * Description: Quản lý Biểu mẫu báo giá, Tích hợp Hộp kiểm đồng ý dữ liệu theo Nghị định 13/2023/NĐ-CP, lưu nhật ký bằng chứng đồng ý và hỗ trợ xuất dữ liệu ra file Excel/CSV.
 * Version: 1.3.0
 * Author: H-Phat
 * Text Domain: H-Phat
 * Domain Path: /languages
 */

if (!defined('ABSPATH')) {
	exit; // Exit if accessed directly.
}

// PHP 8.0 Polyfills for backward compatibility (PHP 7.4)
if (!function_exists('str_starts_with')) {
	function str_starts_with($haystack, $needle)
	{
		return 0 === strncmp($haystack, $needle, strlen($needle));
	}
}
if (!function_exists('str_ends_with')) {
	function str_ends_with($haystack, $needle)
	{
		return $needle !== '' && substr($haystack, -strlen($needle)) === $needle;
	}
}


// Define Constants
define('VN_PRIVACY_VERSION', '1.1.0');
define('VN_PRIVACY_PATH', plugin_dir_path(__FILE__));
define('VN_PRIVACY_URL', plugin_dir_url(__FILE__));

// Load classes
require_once VN_PRIVACY_PATH . 'includes/db/class-db.php';
require_once VN_PRIVACY_PATH . 'includes/frontend/class-frontend.php';
require_once VN_PRIVACY_PATH . 'includes/utilities/class-utilities.php';

// Module: Performance (Hiệu năng)
require_once VN_PRIVACY_PATH . 'includes/modules/performance/class-performance-core.php';
require_once VN_PRIVACY_PATH . 'includes/modules/performance/class-performance-admin.php';

// Module: Security (Bảo mật)
require_once VN_PRIVACY_PATH . 'includes/modules/security/class-security-core.php';
require_once VN_PRIVACY_PATH . 'includes/modules/security/class-security-admin.php';

// Module: SEO & Utilities
require_once VN_PRIVACY_PATH . 'includes/modules/seo/class-seo-core.php';
require_once VN_PRIVACY_PATH . 'includes/modules/seo/class-seo-admin.php';

// Module: Analytics (Nhật ký lượt xem)
require_once VN_PRIVACY_PATH . 'includes/modules/analytics/class-analytics-core.php';
require_once VN_PRIVACY_PATH . 'includes/modules/analytics/class-analytics-admin.php';

if (is_admin()) {
	require_once VN_PRIVACY_PATH . 'includes/admin/class-admin.php';
	require_once VN_PRIVACY_PATH . 'includes/admin/class-admin-settings.php';

	// Xử lý lưu cài đặt từ các module mới
	add_action('admin_init', ['VN_Performance_Admin', 'handle_save']);
	add_action('admin_init', ['VN_Security_Admin', 'handle_save']);
	add_action('admin_init', ['VN_SEO_Admin', 'handle_save']);
	add_action('admin_init', ['VN_Analytics_Admin', 'handle_save']);
}

// Activation Hook - Setup Tables
register_activation_hook(__FILE__, 'vn_privacy_activate_plugin');
function vn_privacy_activate_plugin()
{
	VN_Privacy_DB::create_tables();
	VN_Privacy_DB::insert_default_forms();
	flush_rewrite_rules();

	// Setup default autobackup on activation (daily)
	if (!wp_next_scheduled('vn_privacy_auto_backup_cron')) {
		wp_schedule_event(time(), 'daily', 'vn_privacy_auto_backup_cron');
	}
}

// Add custom cron intervals
add_filter('cron_schedules', ['VN_Privacy_Utilities', 'add_cron_intervals']);

// Initialize Plugin Components
function vn_privacy_init_plugin()
{
	new VN_Privacy_Frontend();
	new VN_Privacy_Utilities();

	// Module: Performance
	new VN_Performance_Core();

	// Module: Security
	new VN_Security_Core();

	// Module: SEO & Utilities
	new VN_SEO_Core();

	// Module: Analytics
	new VN_Analytics_Core();

	// Module: WooCommerce Product Filter
	if (class_exists('WooCommerce')) {
		require_once VN_PRIVACY_PATH . 'includes/filter/class-filter-loader.php';
		new VN_Product_Filter();
	}

	if (is_admin()) {
		new VN_Privacy_Admin();
		new VN_Performance_Admin();
		new VN_Security_Admin();
		new VN_SEO_Admin();
		new VN_Analytics_Admin();
		new VN_Privacy_Admin_Settings();
	}
}
add_action('plugins_loaded', 'vn_privacy_init_plugin');

