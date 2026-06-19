<?php
/**
 * REST API Handlers for PDF Pro Licensing
 */

if (!defined('ABSPATH')) {
    exit;
}

/**
 * Rate Limiting: Giới hạn số lần gọi API từ cùng một IP.
 * Sử dụng WordPress transients để lưu trữ bộ đếm.
 *
 * @param string $action   Tên hành động (ví dụ: 'activate', 'check')
 * @param int    $max      Số lần tối đa cho phép trong khoảng thời gian
 * @param int    $window   Khoảng thời gian tính bằng giây
 * @return bool|WP_Error   True nếu cho phép, WP_Error nếu vượt giới hạn
 */
function pdfpro_licensing_check_rate_limit($action, $max = 10, $window = 60) {
    $ip = $_SERVER['REMOTE_ADDR'] ?? '0.0.0.0';
    $key = 'pdfpro_rl_' . md5($action . '|' . $ip);
    $current = get_transient($key);

    if ($current === false) {
        set_transient($key, 1, $window);
        return true;
    }

    if ((int) $current >= $max) {
        return new WP_Error(
            'rate_limited',
            'Quá nhiều yêu cầu. Vui lòng thử lại sau ' . $window . ' giây.',
            array('status' => 429)
        );
    }

    set_transient($key, (int) $current + 1, $window);
    return true;
}

add_action('rest_api_init', 'pdfpro_licensing_register_routes');

// Hỗ trợ bỏ qua yêu cầu đăng nhập đối với các API endpoint của PDF Pro
add_filter('rest_authentication_errors', 'pdfpro_licensing_bypass_rest_auth', 9999);

function pdfpro_licensing_bypass_rest_auth($result) {
    $is_pdfpro_api = false;
    
    // 1. Kiểm tra qua REQUEST_URI (cho url dạng đẹp /wp-json/pdfpro/v1/...)
    if (isset($_SERVER['REQUEST_URI']) && strpos($_SERVER['REQUEST_URI'], 'pdfpro/v1') !== false) {
        $is_pdfpro_api = true;
    }
    
    // 2. Kiểm tra qua tham số?rest_route=/pdfpro/v1/... (nếu web dùng url dạng cũ)
    if (isset($_GET['rest_route']) && strpos($_GET['rest_route'], 'pdfpro/v1') !== false) {
        $is_pdfpro_api = true;
    }

    if ($is_pdfpro_api) {
        return null; // Trả vịnull để xóa lỗi WP_Error từ các plugin bảo mật khác, cho phép tiếp tục truy cập
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
    // Rate limit: tối đa 5 lần kích hoạt / phút / IP
    $rate_check = pdfpro_licensing_check_rate_limit('activate', 5, 60);
    if (is_wp_error($rate_check)) {
        return $rate_check;
    }

    global $wpdb;
    
    $params = $request->get_json_params();
    $license_key = sanitize_text_field($params['license_key'] ?? '');
    $machine_id = sanitize_text_field($params['machine_id'] ?? '');
    $machine_name = sanitize_text_field($params['machine_name'] ?? '');

    $log_entry = "--- API ACTIVATE ---\n";
    $log_entry .= "Time: " . date('Y-m-d H:i:s') . "\n";
    $log_entry .= "Received Key: " . $license_key . "\n";
    $log_entry .= "Received Machine ID: " . $machine_id . "\n";
    $log_entry .= "Received Machine Name: " . $machine_name . "\n";

    if (empty($license_key) || empty($machine_id)) {
        $log_entry .= "Error: Missing params\n\n";
        @file_put_contents(PDFPRO_LICENSING_DIR . 'error_logs.txt', $log_entry, FILE_APPEND | LOCK_EX);
        return new WP_Error('missing_params', 'Yêu cầu điền đầy đủ license_key và machine_id.', array('status' => 400));
    }

    // Kiểm tra thiết bị bị chặn (Blacklist)
    $blacklisted = get_option('pdfpro_blacklisted_devices', array());
    if (is_array($blacklisted) && in_array($machine_id, $blacklisted)) {
        $log_entry .= "Error: Device is blacklisted\n\n";
        @file_put_contents(PDFPRO_LICENSING_DIR . 'error_logs.txt', $log_entry, FILE_APPEND | LOCK_EX);
        return new WP_Error('device_banned', 'Thiết bị này đã bị khóa do vi phạm bảo mật.', array('status' => 403));
    }

    $table_licenses = $wpdb->prefix . 'pdfpro_licenses';
    $table_activations = $wpdb->prefix . 'pdfpro_activations';

    // Tìm kiếm License (Chuẩn hóa key loại bịdấu gạch ngang)
    $normalized_key = preg_replace('/[^A-Za-z0-9]/', '', $license_key);
    $license = $wpdb->get_row($wpdb->prepare(
        "SELECT * FROM $table_licenses WHERE REPLACE(license_key, '-', '') = %s",
        $normalized_key
    ));

    if (!$license) {
        $log_entry .= "Error: License not found in DB\n\n";
        @file_put_contents(PDFPRO_LICENSING_DIR . 'error_logs.txt', $log_entry, FILE_APPEND | LOCK_EX);
        return new WP_Error('invalid_license', 'Mã bản quyền không tồn tại.', array('status' => 404));
    }

    $log_entry .= "DB License Found: ID=" . $license->id . ", Key=" . $license->license_key . ", Status=" . $license->status . "\n";

    if ($license->status !== 'active') {
        $log_entry .= "Error: License status is not active (" . $license->status . ")\n\n";
        @file_put_contents(PDFPRO_LICENSING_DIR . 'error_logs.txt', $log_entry, FILE_APPEND | LOCK_EX);
        return new WP_Error('license_suspended', 'Mã bản quyền này đã bị khóa hoặc tạm dừng.', array('status' => 403));
    }

    if ($license->expires_at && strtotime($license->expires_at) < time()) {
        $log_entry .= "Error: License expired\n\n";
        @file_put_contents(PDFPRO_LICENSING_DIR . 'error_logs.txt', $log_entry, FILE_APPEND | LOCK_EX);
        return new WP_Error('license_expired', 'Mã bản quyền này đã hết hạn sử dụng.', array('status' => 403));
    }

    // Lấy danh sách thiết bịđã kích hoạt
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
        // Kiểm tra xem có vượt quá giới hạn thiết bịkhông
        if (count($activations) >= (int)$license->max_devices) {
            $log_entry .= "Error: Device limit exceeded. Active count: " . count($activations) . " Max: " . $license->max_devices . "\n\n";
            @file_put_contents(PDFPRO_LICENSING_DIR . 'error_logs.txt', $log_entry, FILE_APPEND | LOCK_EX);
            return new WP_Error('limit_exceeded', 'Mã bản quyền này đã vượt quá số lượng máy cho phép kích hoạt.', array('status' => 403));
        }

        // Lưu thông tin kích hoạt mới
        $wpdb->insert($table_activations, array(
            'license_id'   => $license->id,
            'machine_id'   => $machine_id,
            'machine_name' => $machine_name,
            'activated_at' => current_time('mysql')
        ));
        $log_entry .= "Inserted new activation record for machine_id: " . $machine_id . "\n";
    } else {
        $log_entry .= "Machine already activated\n";
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

    $log_entry .= "Activation Success! Payload: " . $json_payload . "\n\n";
    @file_put_contents(PDFPRO_LICENSING_DIR . 'error_logs.txt', $log_entry, FILE_APPEND | LOCK_EX);

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
    // Rate limit: tối đa 20 lần check / phút / IP (heartbeat có thể gọi thường xuyên)
    $rate_check = pdfpro_licensing_check_rate_limit('check', 20, 60);
    if (is_wp_error($rate_check)) {
        return $rate_check;
    }

    global $wpdb;

    $params = $request->get_json_params();
    $license_key = sanitize_text_field($params['license_key'] ?? '');
    $machine_id = sanitize_text_field($params['machine_id'] ?? '');

    $log_entry = "--- API CHECK (HEARTBEAT) ---\n";
    $log_entry .= "Time: " . date('Y-m-d H:i:s') . "\n";
    $log_entry .= "Received Key: " . $license_key . "\n";
    $log_entry .= "Received Machine ID: " . $machine_id . "\n";

    if (empty($license_key) || empty($machine_id)) {
        $log_entry .= "Error: Missing params\n\n";
        @file_put_contents(PDFPRO_LICENSING_DIR . 'error_logs.txt', $log_entry, FILE_APPEND | LOCK_EX);
        return new WP_Error('missing_params', 'Yêu cầu điền đầy đủ license_key và machine_id.', array('status' => 400));
    }

    $table_licenses = $wpdb->prefix . 'pdfpro_licenses';
    $table_activations = $wpdb->prefix . 'pdfpro_activations';

    // Tìm kiếm License và bản ghi kích hoạt (Chuẩn hóa key loại bịdấu gạch ngang)
    $normalized_key = preg_replace('/[^A-Za-z0-9]/', '', $license_key);
    $log_entry .= "Normalized Key: " . $normalized_key . "\n";

    $license = $wpdb->get_row($wpdb->prepare(
        "SELECT l.*, a.id as activation_id FROM $table_licenses l 
         LEFT JOIN $table_activations a ON l.id = a.license_id AND a.machine_id = %s
         WHERE REPLACE(l.license_key, '-', '') = %s",
        $machine_id,
        $normalized_key
    ));

    if (!$license) {
        $log_entry .= "Error: License not found in DB\n\n";
        @file_put_contents(PDFPRO_LICENSING_DIR . 'error_logs.txt', $log_entry, FILE_APPEND | LOCK_EX);
        return new WP_Error('invalid_license', 'Mã bản quyền không tồn tại.', array('status' => 404));
    }

    $log_entry .= "DB License Found: ID=" . $license->id . ", Key=" . $license->license_key . ", Status=" . $license->status . "\n";
    $log_entry .= "DB Activation ID: " . ($license->activation_id ?? 'NULL') . "\n";

    $status = 'activated';
    // Kiểm tra thiết bị bị chặn (Blacklist)
    $blacklisted = get_option('pdfpro_blacklisted_devices', array());
    if (is_array($blacklisted) && in_array($machine_id, $blacklisted)) {
        $status = 'suspended';
        $log_entry .= "Status Check: Suspended (Blacklisted device)\n";
    } elseif ($license->status !== 'active') {
        $status = 'suspended';
        $log_entry .= "Status Check: Suspended (License is not active)\n";
    } elseif ($license->expires_at && strtotime($license->expires_at) < time()) {
        $status = 'expired';
        $log_entry .= "Status Check: Expired\n";
    } elseif (empty($license->activation_id)) {
        $status = 'unregistered_device';
        $log_entry .= "Status Check: Unregistered device\n";
    }

    $payload_data = array(
        'license_key' => $license->license_key,
        'machine_id'  => $machine_id,
        'expires_at'  => $license->expires_at ? date('c', strtotime($license->expires_at)) : 'never',
        'status'      => $status,
        'timestamp'   => time()
    );

    $json_payload = json_encode($payload_data);
    $signature = pdfpro_licensing_sign_payload($json_payload);

    $log_entry .= "Status Result: " . $status . " Payload: " . $json_payload . "\n\n";
    @file_put_contents(PDFPRO_LICENSING_DIR . 'error_logs.txt', $log_entry, FILE_APPEND | LOCK_EX);

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

    // Chuẩn hóa key loại bịdấu gạch ngang
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
        'message' => 'Đã hủy kích hoạt thiết bị thành công.'
    );
}

/**
 * Tạo chữ ký số RSA SHA-256 từ chuỗi Payload bằng Private Key
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
 * Xử lý báo cáo lỗi từ ứng dụng Desktop (Telemetry)
 */
function pdfpro_licensing_api_report_error(WP_REST_Request $request) {
    // Rate limit: tối đa 10 báo cáo lỗi / phút / IP
    $rate_check = pdfpro_licensing_check_rate_limit('report_error', 10, 60);
    if (is_wp_error($rate_check)) {
        return $rate_check;
    }

    global $wpdb;
    $params = $request->get_json_params();
    $app_version = sanitize_text_field($params['app_version'] ?? 'unknown');
    $machine_id = sanitize_text_field($params['machine_id'] ?? 'unknown');
    $error_message = sanitize_text_field($params['error_message'] ?? '');
    $stack_trace = sanitize_textarea_field($params['stack_trace'] ?? '');
    $os_version = sanitize_text_field($params['os_version'] ?? 'unknown');
    $timestamp = isset($params['timestamp']) ? intval($params['timestamp']) : time();

    // 1. Ghi vào file error_logs.txt làm dự phòng
    $log_time = date('Y-m-d H:i:s', $timestamp);
    $log_entry = "=========================================\n";
    $log_entry .= "THỜI GIAN: $log_time\n";
    $log_entry .= "PHIÊN BẢN APP: $app_version\n";
    $log_entry .= "HỆ ĐIỀU HÀNH: $os_version\n";
    $log_entry .= "MÃ THIẾT BỊ: $machine_id\n";
    $log_entry .= "THÔNG BÁO LỖI: $error_message\n";
    $log_entry .= "STACK TRACE:\n$stack_trace\n\n";

    $log_file = PDFPRO_LICENSING_DIR . 'error_logs.txt';
    @file_put_contents($log_file, $log_entry, FILE_APPEND | LOCK_EX);

    // 2. Ghi vào bảng wp_pdfpro_errors
    $table_errors = $wpdb->prefix . 'pdfpro_errors';
    $wpdb->insert($table_errors, array(
        'app_version'   => $app_version,
        'machine_id'    => $machine_id,
        'os_version'    => $os_version,
        'error_message' => $error_message,
        'stack_trace'   => $stack_trace,
        'reported_at'   => date('Y-m-d H:i:s', $timestamp)
    ));

    return array(
        'success' => true,
        'message' => 'Đã lưu báo cáo lỗi lên máy chủ.'
    );
}

/**
 * Xử lý yêu cầu Phát hành bản cập nhật mới
 */
function pdfpro_licensing_api_update_publish(WP_REST_Request $request) {
    global $wpdb;
    $params = $request->get_json_params();
    $token = sanitize_text_field($params['token'] ?? '');
    
    $expected_token = defined('PDFPRO_PUBLISH_TOKEN') ? PDFPRO_PUBLISH_TOKEN : get_option('pdfpro_publish_token', '');
    
    if (empty($expected_token)) {
        $expected_token = wp_generate_password(32, false);
        update_option('pdfpro_publish_token', $expected_token);
    }
    
    if (empty($token) || !hash_equals($expected_token, $token)) {
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
    update_option('pdfpro_update_sha256', strtolower(preg_replace('/[^a-fA-F0-9]/', '', $sha256)));
    update_option('pdfpro_update_file_size', $file_size);
    update_option('pdfpro_update_release_date', $release_date);
    update_option('pdfpro_update_mandatory', $mandatory);
    update_option('pdfpro_changelog', $changelog);

    // Lưu thông tin vào bảng lịch sử updates
    $table_updates = $wpdb->prefix . 'pdfpro_updates';
    $wpdb->insert($table_updates, array(
        'version'      => $latest_version,
        'download_url' => $download_url,
        'sha256'       => strtolower(preg_replace('/[^a-fA-F0-9]/', '', $sha256)),
        'file_size'    => $file_size,
        'release_date' => $release_date,
        'mandatory'    => $mandatory === '1' ? 1 : 0,
        'changelog'    => $changelog,
        'published_at' => current_time('mysql')
    ));
    
    return array(
        'success' => true,
        'message' => 'Đã phát hành bản cập nhật mới v' . $latest_version . ' thành công!'
    );
}
