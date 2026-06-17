<?php
/**
 * Plugin Name: PDF Pro Licensing Server
 * Description: API Server quản lý, kích hoạt và xác thực bản quyền ứng dụng PDF Pro thông qua chữ ký số RSA.
 * Version: 1.1.0
 * Author: HPhat
 * Text Domain: pdfpro-licensing
 */

if (!defined('ABSPATH')) {
    exit; // Thoát nếu truy cập trực tiếp
}

// Định nghĩa các hằng số đường dẫn
define('PDFPRO_LICENSING_DIR', plugin_dir_path(__FILE__));
define('PDFPRO_LICENSING_URL', plugin_dir_url(__FILE__));
define('PDFPRO_KEYS_DIR', PDFPRO_LICENSING_DIR . 'keys/');
define('PDFPRO_PRIVATE_KEY_PATH', PDFPRO_KEYS_DIR . 'private_key.pem');
define('PDFPRO_PUBLIC_KEY_PATH', PDFPRO_KEYS_DIR . 'public_key.pem');

// 1. Hook kích hoạt plugin (Tạo DB & Sinh cặp khóa bảo mật RSA)
register_activation_hook(__FILE__, 'pdfpro_licensing_activate_plugin');

function pdfpro_licensing_activate_plugin() {
    // A. Thiết lập cơ sở dữ liệu
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

    // B. Tạo thư mục keys và sinh cặp khóa RSA nếu chưa tồn tại
    if (!file_exists(PDFPRO_KEYS_DIR)) {
        @wp_mkdir_p(PDFPRO_KEYS_DIR);
        // Bảo vệ thư mục keys bằng file .htaccess
        @file_put_contents(PDFPRO_KEYS_DIR . '.htaccess', "Deny from all");
        @file_put_contents(PDFPRO_KEYS_DIR . 'index.php', "<?php // Silence is golden.\n");
    }

    pdfpro_licensing_ensure_rsa_keypair();
}

// Tự động kiểm tra và nâng cấp CSDL khi admin truy cập
add_action('admin_init', 'pdfpro_licensing_check_db_upgrade');
function pdfpro_licensing_check_db_upgrade() {
    $current_db_version = get_option('pdfpro_db_version', '1.0.0');
    if ($current_db_version !== '1.0.1') {
        pdfpro_licensing_activate_plugin();
    }
}


/**
 * Tạo cặp khóa RSA 2048-bit bằng OpenSSL.
 * Key được sinh ĐỘNG mỗi lần gọi — không hardcode trong source code.
 *
 * @return bool True nếu sinh key thành công, false nếu thất bại.
 */
function pdfpro_licensing_generate_rsa_keys() {
    if (!extension_loaded('openssl')) {
        error_log('[PDF Pro Licensing] OpenSSL extension is not loaded. Cannot generate RSA keys.');
        return false;
    }

    // Sinh cặp khóa RSA 2048-bit mới hoàn toàn
    $config = array(
        'digest_alg'       => 'sha256',
        'private_key_bits' => 2048,
        'private_key_type' => OPENSSL_KEYTYPE_RSA,
    );

    $res = openssl_pkey_new($config);
    if (!$res) {
        error_log('[PDF Pro Licensing] openssl_pkey_new() failed: ' . openssl_error_string());
        return false;
    }

    // Xuất Private Key dạng PEM
    $private_key_pem = '';
    if (!openssl_pkey_export($res, $private_key_pem)) {
        error_log('[PDF Pro Licensing] openssl_pkey_export() failed: ' . openssl_error_string());
        return false;
    }

    // Xuất Public Key dạng PEM
    $key_details = openssl_pkey_get_details($res);
    if (!$key_details || empty($key_details['key'])) {
        error_log('[PDF Pro Licensing] openssl_pkey_get_details() failed.');
        return false;
    }
    $public_key_pem = $key_details['key'];

    // Đảm bảo thư mục keys tồn tại và được bảo vệ
    if (!file_exists(PDFPRO_KEYS_DIR)) {
        @wp_mkdir_p(PDFPRO_KEYS_DIR);
        @file_put_contents(PDFPRO_KEYS_DIR . '.htaccess', "Deny from all\n");
        @file_put_contents(PDFPRO_KEYS_DIR . 'index.php', "<?php // Silence is golden.\n");
    }

    // Lưu key vào file với quyền truy cập tối thiểu
    $private_written = @file_put_contents(PDFPRO_PRIVATE_KEY_PATH, $private_key_pem);
    $public_written  = @file_put_contents(PDFPRO_PUBLIC_KEY_PATH, $public_key_pem);

    if ($private_written === false || $public_written === false) {
        error_log('[PDF Pro Licensing] Failed to write RSA key files to disk.');
        return false;
    }

    // Thiết lập quyền file tối thiểu cho private key (chỉ chủ sở hữu đọc được)
    @chmod(PDFPRO_PRIVATE_KEY_PATH, 0600);
    @chmod(PDFPRO_PUBLIC_KEY_PATH, 0644);

    // Xác thực lại cặp key vừa sinh bằng cách ký và kiểm tra
    $test_payload = 'pdfpro-keygen-verify-' . time();
    $test_signature = '';
    $priv = openssl_pkey_get_private($private_key_pem);
    $pub  = openssl_pkey_get_public($public_key_pem);

    if (!$priv || !$pub) {
        error_log('[PDF Pro Licensing] Generated keys failed to reload.');
        @unlink(PDFPRO_PRIVATE_KEY_PATH);
        @unlink(PDFPRO_PUBLIC_KEY_PATH);
        return false;
    }

    if (!openssl_sign($test_payload, $test_signature, $priv, OPENSSL_ALGO_SHA256)) {
        error_log('[PDF Pro Licensing] Self-test signing failed.');
        @unlink(PDFPRO_PRIVATE_KEY_PATH);
        @unlink(PDFPRO_PUBLIC_KEY_PATH);
        return false;
    }

    if (openssl_verify($test_payload, $test_signature, $pub, OPENSSL_ALGO_SHA256) !== 1) {
        error_log('[PDF Pro Licensing] Self-test verification failed.');
        @unlink(PDFPRO_PRIVATE_KEY_PATH);
        @unlink(PDFPRO_PUBLIC_KEY_PATH);
        return false;
    }

    // Lưu fingerprint public key vào wp_options để client có thể verify
    update_option('pdfpro_public_key_fingerprint', hash('sha256', trim($public_key_pem)));

    return true;
}

// 2. Nhúng các tệp cấu phần khác
function pdfpro_licensing_ensure_rsa_keypair() {
    if (!file_exists(PDFPRO_KEYS_DIR)) {
        @wp_mkdir_p(PDFPRO_KEYS_DIR);
        @file_put_contents(PDFPRO_KEYS_DIR . '.htaccess', "Deny from all");
    }

    if (!pdfpro_licensing_rsa_keypair_is_valid()) {
        @pdfpro_licensing_generate_rsa_keys();
    }
}

function pdfpro_licensing_rsa_keypair_is_valid() {
    if (!extension_loaded('openssl')) {
        return false;
    }

    if (!file_exists(PDFPRO_PRIVATE_KEY_PATH) || !file_exists(PDFPRO_PUBLIC_KEY_PATH)) {
        return false;
    }

    $private_key_pem = file_get_contents(PDFPRO_PRIVATE_KEY_PATH);
    $public_key_pem = file_get_contents(PDFPRO_PUBLIC_KEY_PATH);
    if ($private_key_pem === false || $public_key_pem === false) {
        return false;
    }

    $private_key = openssl_pkey_get_private($private_key_pem);
    $public_key = openssl_pkey_get_public($public_key_pem);
    if (!$private_key || !$public_key) {
        return false;
    }

    $payload = 'pdfpro-rsa-self-test';
    $signature = '';
    if (!openssl_sign($payload, $signature, $private_key, OPENSSL_ALGO_SHA256)) {
        return false;
    }

    return openssl_verify($payload, $signature, $public_key, OPENSSL_ALGO_SHA256) === 1;
}

require_once PDFPRO_LICENSING_DIR . 'api-handlers.php';
require_once PDFPRO_LICENSING_DIR . 'admin-menu.php';
require_once PDFPRO_LICENSING_DIR . 'public-key-route.php';
