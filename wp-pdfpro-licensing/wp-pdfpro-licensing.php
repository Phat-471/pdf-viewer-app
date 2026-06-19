<?php
/**
 * Plugin Name: PDF Pro Licensing Server
 * Description: API Server quản lý, kích hoạt và xác thực bản quyền ứng dụng PDF Pro.
 * Version: 1.2.0
 * Author: HPhat
 * Text Domain: pdfpro-licensing
 */

if (!defined('ABSPATH')) {
    exit; // Thoát nếu truy cập trực tiếp
}

// Định nghĩa các hằng số đường dẫn
define('PDFPRO_LICENSING_DIR', plugin_dir_path(__FILE__));
define('PDFPRO_LICENSING_URL', plugin_dir_url(__FILE__));

// 1. Hook kích hoạt plugin (Tạo DB)
register_activation_hook(__FILE__, 'pdfpro_licensing_activate_plugin');

function pdfpro_licensing_activate_plugin() {
    global $wpdb;
    $charset_collate = $wpdb->get_charset_collate();

    $table_licenses = $wpdb->prefix . 'pdfpro_licenses';
    $table_activations = $wpdb->prefix . 'pdfpro_activations';
    $table_updates = $wpdb->prefix . 'pdfpro_updates';
    $table_errors = $wpdb->prefix . 'pdfpro_errors';

    $sql1 = "CREATE TABLE $table_licenses (
        id bigint(20) NOT NULL AUTO_INCREMENT,
        license_key varchar(100) NOT NULL,
        status varchar(20) DEFAULT 'active' NOT NULL,
        max_devices int(11) DEFAULT 1 NOT NULL,
        expires_at datetime DEFAULT NULL,
        created_at datetime DEFAULT CURRENT_TIMESTAMP NOT NULL,
        PRIMARY KEY  (id),
        UNIQUE KEY license_key (license_key)
    ) $charset_collate;";

    $sql2 = "CREATE TABLE $table_activations (
        id bigint(20) NOT NULL AUTO_INCREMENT,
        license_id bigint(20) NOT NULL,
        machine_id varchar(100) NOT NULL,
        machine_name varchar(255) DEFAULT '',
        activated_at datetime DEFAULT CURRENT_TIMESTAMP NOT NULL,
        PRIMARY KEY  (id),
        KEY license_id (license_id)
    ) $charset_collate;";

    $sql3 = "CREATE TABLE $table_updates (
        id bigint(20) NOT NULL AUTO_INCREMENT,
        version varchar(50) NOT NULL,
        download_url varchar(255) NOT NULL,
        sha256 varchar(64) NOT NULL,
        file_size bigint(20) NOT NULL,
        release_date varchar(50) DEFAULT '' NOT NULL,
        mandatory tinyint(1) DEFAULT 0 NOT NULL,
        changelog text DEFAULT '',
        published_at datetime DEFAULT CURRENT_TIMESTAMP NOT NULL,
        PRIMARY KEY  (id)
    ) $charset_collate;";

    $sql4 = "CREATE TABLE $table_errors (
        id bigint(20) NOT NULL AUTO_INCREMENT,
        app_version varchar(50) NOT NULL,
        machine_id varchar(100) NOT NULL,
        os_version varchar(255) DEFAULT '' NOT NULL,
        error_message text NOT NULL,
        stack_trace text,
        reported_at datetime DEFAULT CURRENT_TIMESTAMP NOT NULL,
        PRIMARY KEY  (id)
    ) $charset_collate;";

    require_once(ABSPATH . 'wp-admin/includes/upgrade.php');
    dbDelta($sql1);
    dbDelta($sql2);
    dbDelta($sql3);
    dbDelta($sql4);

    // Lưu phiên bản database mới
    update_option('pdfpro_db_version', '1.0.1');
}

// Tự động kiểm tra và nâng cấp CSDL khi admin truy cập
add_action('admin_init', 'pdfpro_licensing_check_db_upgrade');
function pdfpro_licensing_check_db_upgrade() {
    $current_db_version = get_option('pdfpro_db_version', '1.0.0');
    if ($current_db_version !== '1.0.1') {
        pdfpro_licensing_activate_plugin();
    }
}

require_once PDFPRO_LICENSING_DIR . 'api-handlers.php';
require_once PDFPRO_LICENSING_DIR . 'admin-menu.php';
