<?php
/**
 * Plugin Name: PDF Pro Licensing Server
 * Description: API Server quản lý, kích hoạt và xác thực bản quyền ứng dụng PDF Pro thông qua chữ ký số RSA.
 * Version: 1.0.3
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
 * Tự động tạo cặp khóa RSA 2048-bit bằng OpenSSL
 */
function pdfpro_licensing_generate_rsa_keys() {
    $private_key = "-----BEGIN PRIVATE KEY-----\n" .
        "MIIEvAIBADANBgkqhkiG9w0BAQEFAASCBKYwggSiAgEAAoIBAQCwxqsH2112mYFj\n" .
        "6ebH4v5r/lzzRAEYaQI2+FrdKymn0qkHJQ5L2JxAub+3yqIOwIHPuGTGlFHlT2hZ\n" .
        "bItzE5bqmVs5OM1SgBr6sb/jkaYgkwBI6f0NC9YBXMTznoKuAxW8RI6DcIx1AcE4\n" .
        "UT3rSVYdnqcIbV5t9Ys54f6l/TpGF/JfhxX4b8ykbC3W0Zms4ZZlp71focqmCEnp\n" .
        "uJ3iM96pSXADhLaOrvF/z4MsZh5M6kijDURKgYXFqh0E8CNhbKy2wgGksP0htF21\n" .
        "VA5RYzBkoYZDhjLYbc0NjQrf/mp3Mi/GXppSH+GZ5+ffyWELk4IYEUF7F6OE7605\n" .
        "FWAVw0v/AgMBAAECggEAF9kCpgGHccz9TMZ3Xjw2rh33WdAGRSNBa7tZvWApyJyd\n" .
        "izu0r9xjHpjluWcqMUryGDIxfNfx1mQTLovQoi/LL7nhjxjPxiicZc6IlawxQ6Vh\n" .
        "J/T9BioJxE7zY5mHhUR1mwY1TZu82boTYBlsUitSD+vhGmdwEkMfoFQ8ahXSgIsC\n" .
        "vCYXw6kSDlsubveh6xRDRh0xrkspowtd0eFg5vl2F24UErIBVaniCvDhoeybIAdn\n" .
        "UMarLAnNPqn/49jlHe5QEXkET2TH9enOBc+/7XSArir+ft945cEz2xA/XR8tpt9E\n" .
        "gUMoH4D7n6fRoDC9cRZ6FwiSEhyx74TsWY4lU7E+HQKBgQDZNa5pJ+92AdBsb1F1\n" .
        "cmsuNI6k3ZNJjK8zvygAJS1ygmiDkmjsNwlxSokrw9pFz68XWisbQwE2mepKN81E\n" .
        "OduHPkv/sPOWAaRNENZPIZzo5q8Np9PA7xkSFj88bmwqfDlnykDVcYtBE0bMrXE1\n" .
        "/995kM5MXG35/O64oh+u310t3QKBgQDQWHSGhe+n/hOfaAKbw8w+sA3ctKppgQSg\n" .
        "oQGbTPxkbv34fTC75fr55gQoI7AowS8brcFK4cNreJSSc8a41Q0A25ZmjP+ZZphr\n" .
        "CIUDNibNnRB7yZjiSmf0HI/RO2J/0ObgNIgDx0GGqieNgLxMD1bqxYGdNKqAJJRh\n" .
        "jzCti28piwKBgCVr/Td6vOPM3jbAWv1sEBEu1uCKmCSUy16T8XVM8m6HDzCT2eXQ\n" .
        "eZz+JXHX1VQvus/AJisVOTFKBTZyNLgra6n6Tqenud+/OqpYW0PY26q4i7JDltTn\n" .
        "nJ8kHBLyR0puiolaLB9Z5473njwHKbkO81aDXzeCuSPXst02eVTsgKY1AoGAP03x\n" .
        "MgK2O/wWaEQJLt0CTTXfMGVwthfumQPy4gY1VirnXj5jtWP+qzm5n5ygZPG155oW\n" .
        "9jK81wXPVuR4yCZsCguumkBTVX/35eWzzLMCfU0w+fvaST/EcEbRaAi8OAv4ar1r\n" .
        "aoJ7pXhEBlnMXOv4Q+N5K5QaDk+PCkmgx8prH1sCgYAezneUH/ht0OyltevnfPCn\n" .
        "DwuyuPqPPtG1hHBgOJrw3wAoNKqM5mY8ObkEvpEjPwF7CffDRD0lKBXzHANGIz1+\n" .
        "s+EMkEdiATksCTne+onTQKUMzNKgNr6WdvNaUXZIV67trWZeB7sYiIyO0QiKkSh7\n" .
        "sp8xhh6Keo918eLw2/z7+Q==\n" .
        "-----END PRIVATE KEY-----\n";

    $public_key = "-----BEGIN PUBLIC KEY-----\n" .
        "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAsMarB9tddpmBY+nmx+L+\n" .
        "a/5c80QBGGkCNvha3Sspp9KpByUOS9icQLm/t8qiDsCBz7hkxpRR5U9oWWyLcxOW\n" .
        "6plbOTjNUoAa+rG/45GmIJMASOn9DQvWAVzE856CrgMVvESOg3CMdQHBOFE960lW\n" .
        "HZ6nCG1ebfWLOeH+pf06RhfyX4cV+G/MpGwt1tGZrOGWZae9X6HKpghJ6bid4jPe\n" .
        "qUlwA4S2jq7xf8+DLGYeTOpIow1ESoGFxaodBPAjYWystsIBpLD9IbRdtVQOUWMw\n" .
        "ZKGGQ4Yy2G3NDY0K3/5qdzIvxl6aUh/hmefn38lhC5OCGBFBexejhO+tORVgFcNL\n" .
        "/wIDAQAB\n" .
        "-----END PUBLIC KEY-----\n";

    @file_put_contents(PDFPRO_PRIVATE_KEY_PATH, $private_key);
    @file_put_contents(PDFPRO_PUBLIC_KEY_PATH, $public_key);
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
