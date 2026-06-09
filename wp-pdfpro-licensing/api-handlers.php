<?php
/**
 * REST API Handlers for PDF Pro Licensing
 */

if (!defined('ABSPATH')) {
    exit;
}

add_action('rest_api_init', 'pdfpro_licensing_register_routes');

// HềEtrợ bềEqua yêu cầu đăng nhập đối với các API endpoint của PDF Pro
add_filter('rest_authentication_errors', 'pdfpro_licensing_bypass_rest_auth', 9999);

function pdfpro_licensing_bypass_rest_auth($result) {
    $is_pdfpro_api = false;
    
    // 1. Kiểm tra qua REQUEST_URI (cho url dạng đẹp /wp-json/pdfpro/v1/...)
    if (isset($_SERVER['REQUEST_URI']) && strpos($_SERVER['REQUEST_URI'], 'pdfpro/v1') !== false) {
        $is_pdfpro_api = true;
    }
    
    // 2. Kiểm tra qua tham sềE?rest_route=/pdfpro/v1/... (nếu web dùng url dạng cũ)
    if (isset($_GET['rest_route']) && strpos($_GET['rest_route'], 'pdfpro/v1') !== false) {
        $is_pdfpro_api = true;
    }

    if ($is_pdfpro_api) {
        return null; // Trả vềEnull đềExóa lỗi WP_Error từ các plugin bảo mật khác, cho phép tiếp tục truy cập
    }
    
    return $result;
}

function pdfpro_licensing_register_routes() {
    $namespace = 'pdfpro/v1';

    // API Kích hoạt
    register_rest_route($namespace, '/activate', array(
        'methods'             => 'POST',
        'callback'            => 'pdfpro_licensing_api_activate',
        'permission_callback' => '__return_true', // Công khai cho ứng dụng desktop gọi
    ));

    // API Kiểm tra định kỳ (Heartbeat check)
    register_rest_route($namespace, '/check', array(
        'methods'             => 'POST',
        'callback'            => 'pdfpro_licensing_api_check',
        'permission_callback' => '__return_true',
    ));

    // API Hủy kích hoạt (Deactivate)
    register_rest_route($namespace, '/deactivate', array(
        'methods'             => 'POST',
        'callback'            => 'pdfpro_licensing_api_deactivate',
        'permission_callback' => '__return_true',
    ));

    // API Kiểm tra cập nhật (Update check)
    register_rest_route($namespace, '/update-check', array(
        'methods'             => 'GET',
        'callback'            => 'pdfpro_licensing_api_update_check',
        'permission_callback' => '__return_true',
    ));

    // API Báo cáo lỗi (Error Telemetry)
    register_rest_route($namespace, '/report-error', array(
        'methods'             => 'POST',
        'callback'            => 'pdfpro_licensing_api_report_error',
        'permission_callback' => '__return_true',
    ));

    // API Phát hành bản cập nhật mới (Update publish)
    register_rest_route($namespace, '/update-publish', array(
        'methods'             => 'POST',
        'callback'            => 'pdfpro_licensing_api_update_publish',
        'permission_callback' => '__return_true',
    ));
}

/**
 * Xử lý yêu cầu Kích hoạt bản quyền
 */
function pdfpro_licensing_api_activate(WP_REST_Request $request) {
    global $wpdb;
    
    $params = $request->get_json_params();
    $license_key = sanitize_text_field($params['license_key'] ?? '');
    $machine_id = sanitize_text_field($params['machine_id'] ?? '');
    $machine_name = sanitize_text_field($params['machine_name'] ?? '');

    if (empty($license_key) || empty($machine_id)) {
        return new WP_Error('missing_params', 'Yêu cầu điền đầy đủ license_key và machine_id.', array('status' => 400));
    }

    $table_licenses = $wpdb->prefix . 'pdfpro_licenses';
    $table_activations = $wpdb->prefix . 'pdfpro_activations';

    // Tìm kiếm License (Chuẩn hóa key loại bềEdấu gạch ngang)
    $normalized_key = preg_replace('/[^A-Za-z0-9]/', '', $license_key);
    $license = $wpdb->get_row($wpdb->prepare(
        "SELECT * FROM $table_licenses WHERE REPLACE(license_key, '-', '') = %s",
        $normalized_key
    ));

    if (!$license) {
        return new WP_Error('invalid_license', 'Mã bản quyền không tồn tại.', array('status' => 404));
    }

    if ($license->status !== 'active') {
        return new WP_Error('license_suspended', 'Mã bản quyền này đã bềEkhóa hoặc tạm dừng.', array('status' => 403));
    }

    if ($license->expires_at && strtotime($license->expires_at) < time()) {
        return new WP_Error('license_expired', 'Mã bản quyền này đã hết hạn sử dụng.', array('status' => 403));
    }

    // Lấy danh sách thiết bềEđã kích hoạt
    $activations = $wpdb->get_results($wpdb->prepare(
        "SELECT * FROM $table_activations WHERE license_id = %d",
        $license->id
    ));

    $is_activated_on_this_machine = false;
    foreach ($activations as $act) {
        if ($act->machine_id === $machine_id) {
            $is_activated_on_this_machine = true;
            break;
        }
    }

    if (!$is_activated_on_this_machine) {
        // Kiểm tra xem có vượt quá giới hạn thiết bềEkhông
        if (count($activations) >= (int)$license->max_devices) {
            return new WP_Error('limit_exceeded', 'Mã bản quyền này đã vượt quá sềElượng máy cho phép kích hoạt.', array('status' => 403));
        }

        // Lưu thông tin kích hoạt mới
        $wpdb->insert($table_activations, array(
            'license_id'   => $license->id,
            'machine_id'   => $machine_id,
            'machine_name' => $machine_name,
            'activated_at' => current_time('mysql')
        ));
    }

    // Sinh payload thong tin ban quyen duoc ky so.
    $payload_data = array(
        'license_key' => $license->license_key,
        'machine_id'  => $machine_id,
        'expires_at'  => $license->expires_at ? date('c', strtotime($license->expires_at)) : 'never',
        'status'      => 'activated',
        'timestamp'   => time()
    );

    $json_payload = json_encode($payload_data);
    $signature = pdfpro_licensing_sign_payload($json_payload);

    return array(
        'success'   => true,
        'payload'   => $json_payload,
        'signature' => $signature,
        'message'   => 'Kích hoạt thành công!'
    );
}

/**
 * Xử lý yêu cầu Kiểm tra trạng thái bản quyền (Heartbeat)
 */
function pdfpro_licensing_api_check(WP_REST_Request $request) {
    global $wpdb;

    $params = $request->get_json_params();
    $license_key = sanitize_text_field($params['license_key'] ?? '');
    $machine_id = sanitize_text_field($params['machine_id'] ?? '');

    if (empty($license_key) || empty($machine_id)) {
        return new WP_Error('missing_params', 'Yêu cầu điền đầy đủ license_key và machine_id.', array('status' => 400));
    }

    $table_licenses = $wpdb->prefix . 'pdfpro_licenses';
    $table_activations = $wpdb->prefix . 'pdfpro_activations';

    // Tìm kiếm License và bản ghi kích hoạt (Chuẩn hóa key loại bềEdấu gạch ngang)
    $normalized_key = preg_replace('/[^A-Za-z0-9]/', '', $license_key);
    $license = $wpdb->get_row($wpdb->prepare(
        "SELECT l.*, a.id as activation_id FROM $table_licenses l 
         LEFT JOIN $table_activations a ON l.id = a.license_id AND a.machine_id = %s
         WHERE REPLACE(l.license_key, '-', '') = %s",
        $machine_id,
        $normalized_key
    ));

    if (!$license) {
        return new WP_Error('invalid_license', 'Mã bản quyền không tồn tại.', array('status' => 404));
    }

    $status = 'activated';
    if ($license->status !== 'active') {
        $status = 'suspended';
    } elseif ($license->expires_at && strtotime($license->expires_at) < time()) {
        $status = 'expired';
    } elseif (empty($license->activation_id)) {
        $status = 'unregistered_device';
    }

    $payload_data = array(
        'license_key' => $license_key,
        'machine_id'  => $machine_id,
        'expires_at'  => $license->expires_at ? date('c', strtotime($license->expires_at)) : 'never',
        'status'      => $status,
        'timestamp'   => time()
    );

    $json_payload = json_encode($payload_data);
    $signature = pdfpro_licensing_sign_payload($json_payload);

    return array(
        'success'   => ($status === 'activated'),
        'payload'   => $json_payload,
        'signature' => $signature,
        'status'    => $status
    );
}

/**
 * Xử lý yêu cầu Hủy kích hoạt bản quyền từ phía máy trạm
 */
function pdfpro_licensing_api_deactivate(WP_REST_Request $request) {
    global $wpdb;

    $params = $request->get_json_params();
    $license_key = sanitize_text_field($params['license_key'] ?? '');
    $machine_id = sanitize_text_field($params['machine_id'] ?? '');

    if (empty($license_key) || empty($machine_id)) {
        return new WP_Error('missing_params', 'Yêu cầu điền đầy đủ license_key và machine_id.', array('status' => 400));
    }

    $table_licenses = $wpdb->prefix . 'pdfpro_licenses';
    $table_activations = $wpdb->prefix . 'pdfpro_activations';

    // Chuẩn hóa key loại bềEdấu gạch ngang
    $normalized_key = preg_replace('/[^A-Za-z0-9]/', '', $license_key);
    $license = $wpdb->get_row($wpdb->prepare(
        "SELECT id FROM $table_licenses WHERE REPLACE(license_key, '-', '') = %s",
        $normalized_key
    ));

    if ($license) {
        $wpdb->delete($table_activations, array(
            'license_id' => $license->id,
            'machine_id' => $machine_id
        ));
    }

    return array(
        'success' => true,
        'message' => 'Đã hủy kích hoạt thiết bềEthành công.'
    );
}

/**
 * Tạo chữ ký sềERSA SHA-256 từ chuỗi Payload bằng Private Key
 */
function pdfpro_licensing_sign_payload($payload) {
    if (function_exists('pdfpro_licensing_ensure_rsa_keypair')) {
        pdfpro_licensing_ensure_rsa_keypair();
    }

    if (!file_exists(PDFPRO_PRIVATE_KEY_PATH) || filesize(PDFPRO_PRIVATE_KEY_PATH) === 0) {
        return '';
    }

    $private_key_pem = file_get_contents(PDFPRO_PRIVATE_KEY_PATH);
    $private_key = openssl_pkey_get_private($private_key_pem);
    
    if (!$private_key) {
        return '';
    }

    $signature = '';
    openssl_sign($payload, $signature, $private_key, OPENSSL_ALGO_SHA256);
    
    if (function_exists('openssl_free_key')) {
        openssl_free_key($private_key);
    }

    return base64_encode($signature);
}

/**
 * Xử lý yêu cầu Kiểm tra bản cập nhật
 */
function pdfpro_licensing_api_update_check(WP_REST_Request $request) {
    $latest_version = get_option('pdfpro_latest_version', '1.0.0');
    $download_url = get_option('pdfpro_download_url', '');
    $sha256 = get_option('pdfpro_update_sha256', '');
    $file_size = absint(get_option('pdfpro_update_file_size', 0));
    $release_date = get_option('pdfpro_update_release_date', '');
    $mandatory = get_option('pdfpro_update_mandatory', '0') === '1';
    $changelog = get_option('pdfpro_changelog', 'No changelog provided.');

    return array(
        'success'        => true,
        'latest_version' => $latest_version,
        'download_url'   => $download_url,
        'sha256'         => $sha256,
        'file_size'      => $file_size,
        'release_date'   => $release_date,
        'mandatory'      => $mandatory,
        'changelog'      => $changelog
    );
}

/**
 * Xử lý báo cáo lỗi từ ứng dụng Desktop
 */
function pdfpro_licensing_api_report_error(WP_REST_Request $request) {
    $params = $request->get_json_params();
    $app_version = sanitize_text_field($params['app_version'] ?? 'unknown');
    $machine_id = sanitize_text_field($params['machine_id'] ?? 'unknown');
    $error_message = sanitize_text_field($params['error_message'] ?? '');
    $stack_trace = sanitize_textarea_field($params['stack_trace'] ?? '');
    $os_version = sanitize_text_field($params['os_version'] ?? 'unknown');
    $timestamp = isset($params['timestamp']) ? intval($params['timestamp']) : time();

    $log_time = date('Y-m-d H:i:s', $timestamp);
    $log_entry = "=========================================\n";
    $log_entry .= "THỜI GIAN: $log_time\n";
    $log_entry .= "PHIÊN BẢN APP: $app_version\n";
    $log_entry .= "HềEĐIỀU HÀNH: $os_version\n";
    $log_entry .= "MÁETHIẾT BềE $machine_id\n";
    $log_entry .= "THÔNG BÁO LỖI: $error_message\n";
    $log_entry .= "STACK TRACE:\n$stack_trace\n\n";

    $log_file = PDFPRO_LICENSING_DIR . 'error_logs.txt';
    file_put_contents($log_file, $log_entry, FILE_APPEND | LOCK_EX);

    return array(
        'success' => true,
        'message' => 'Đã lưu báo cáo lỗi lên máy chủ.'
    );
}

/**
 * Xử lý yêu cầu Phát hành bản cập nhật mới
 */
function pdfpro_licensing_api_update_publish(WP_REST_Request $request) {
    $params = $request->get_json_params();
    $token = sanitize_text_field($params['token'] ?? '');
    
    $expected_token = defined('PDFPRO_PUBLISH_TOKEN') ? PDFPRO_PUBLISH_TOKEN : get_option('pdfpro_publish_token', '');
    
    if (empty($expected_token)) {
        $expected_token = wp_generate_password(32, false);
        update_option('pdfpro_publish_token', $expected_token);
    }
    
    if (empty($token) || $token !== $expected_token) {
        return new WP_Error('unauthorized', 'Token bảo mật không hợp lệ hoặc thiếu.', array('status' => 401));
    }
    
    $latest_version = sanitize_text_field($params['latest_version'] ?? $params['version'] ?? '');
    $download_url = esc_url_raw($params['download_url'] ?? '');
    $sha256 = sanitize_text_field($params['sha256'] ?? '');
    $file_size = absint($params['file_size'] ?? $params['size'] ?? 0);
    $release_date = sanitize_text_field($params['release_date'] ?? '');
    $mandatory = !empty($params['mandatory']) ? '1' : '0';
    $changelog = sanitize_textarea_field($params['changelog'] ?? '');
    
    if (empty($latest_version)) {
        return new WP_Error('missing_version', 'Thiếu thông tin phiên bản (latest_version).', array('status' => 400));
    }
    
    update_option('pdfpro_latest_version', $latest_version);
    update_option('pdfpro_download_url', $download_url);
    update_option('pdfpro_update_sha256', $sha256);
    update_option('pdfpro_update_file_size', $file_size);
    update_option('pdfpro_update_release_date', $release_date);
    update_option('pdfpro_update_mandatory', $mandatory);
    update_option('pdfpro_changelog', $changelog);
    
    return array(
        'success' => true,
        'message' => 'Đã phát hành bản cập nhật mới v' . $latest_version . ' thành công!'
    );
}
