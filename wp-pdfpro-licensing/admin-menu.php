<?php
/**
 * Admin Panel GUI for PDF Pro Licensing (Submenu-style dashboard)
 * Version: 1.0.2
 */

if (!defined('ABSPATH')) {
    exit;
}

// Thêm Menu và các Submenu vào trang quản trềEWordPress Admin
add_action('admin_menu', 'pdfpro_licensing_add_admin_menu');

// Hook xử lý các hành động POST trước khi render HTML
add_action('admin_init', 'pdfpro_licensing_handle_admin_actions');

// Hook chèn CSS Premium UI cho trang quản trị PDF Pro
add_action('admin_print_styles', 'pdfpro_licensing_admin_styles');

function pdfpro_licensing_admin_styles() {
    $screen = get_current_screen();
    if (!$screen || strpos($screen->id, 'pdfpro') === false) {
        return;
    }
    ?>
    <style>
        /* CSS resets & container wrapper */
        .pdfpro-admin-wrap {
            background-color: #0B0F19 !important;
            color: #F8FAFC !important;
            font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Oxygen, Ubuntu, Cantarell, "Helvetica Neue", sans-serif;
            padding: 28px !important;
            margin: 20px 20px 0 0 !important;
            border-radius: 12px !important;
            border: 1px solid #1E293B !important;
            box-shadow: 0 10px 15px -3px rgba(0, 0, 0, 0.3), 0 4px 6px -4px rgba(0, 0, 0, 0.3) !important;
        }
        /* Style headers */
        .pdfpro-admin-wrap h1 {
            color: #38BDF8 !important;
            font-size: 26px !important;
            font-weight: 800 !important;
            margin: 0 0 12px 0 !important;
            text-shadow: 0 0 10px rgba(56, 189, 248, 0.1);
        }
        .pdfpro-admin-wrap h2 {
            color: #F8FAFC !important;
            font-size: 18px !important;
            font-weight: 700 !important;
            margin: 0 0 16px 0 !important;
            border-bottom: 1px solid #1E293B !important;
            padding-bottom: 10px !important;
        }
        /* Paragraph & Text descriptions */
        .pdfpro-admin-wrap p {
            color: #94A3B8 !important;
            font-size: 13.5px !important;
            line-height: 1.6 !important;
        }
        .pdfpro-admin-wrap .description {
            color: #64748B !important;
            font-size: 12px !important;
            margin-top: 6px !important;
        }
        /* Card design */
        .pdfpro-admin-wrap .card {
            background: #111827 !important;
            border: 1px solid #1E293B !important;
            border-radius: 10px !important;
            padding: 24px !important;
            box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -2px rgba(0, 0, 0, 0.1) !important;
            max-width: 100% !important;
            margin-bottom: 24px !important;
        }
        /* Buttons */
        .pdfpro-admin-wrap .button,
        .pdfpro-admin-wrap .button-primary,
        .pdfpro-admin-wrap .button-secondary {
            border-radius: 6px !important;
            font-weight: 600 !important;
            height: auto !important;
            min-height: 32px !important;
            padding: 4px 16px !important;
            transition: all 0.2s ease !important;
            text-shadow: none !important;
            box-shadow: 0 1px 2px 0 rgba(0, 0, 0, 0.05) !important;
        }
        .pdfpro-admin-wrap .button-primary {
            background: #0F766E !important;
            border-color: #0F766E !important;
            color: #FFFFFF !important;
        }
        .pdfpro-admin-wrap .button-primary:hover {
            background: #14B8A6 !important;
            border-color: #14B8A6 !important;
            box-shadow: 0 0 12px rgba(20, 184, 166, 0.3) !important;
        }
        .pdfpro-admin-wrap .button-secondary,
        .pdfpro-admin-wrap .button {
            background: #1E293B !important;
            border-color: #334155 !important;
            color: #F8FAFC !important;
        }
        .pdfpro-admin-wrap .button-secondary:hover,
        .pdfpro-admin-wrap .button:hover {
            background: #334155 !important;
            border-color: #475569 !important;
            color: #FFFFFF !important;
        }
        .pdfpro-admin-wrap .button-link-delete {
            color: #EF4444 !important;
            font-weight: 600 !important;
            text-decoration: none !important;
        }
        .pdfpro-admin-wrap .button-link-delete:hover {
            color: #F87171 !important;
            text-shadow: 0 0 8px rgba(239, 68, 68, 0.2) !important;
        }
        .pdfpro-admin-wrap .button-link {
            color: #38BDF8 !important;
            font-weight: 600 !important;
            text-decoration: none !important;
            background: none !important;
            border: none !important;
            cursor: pointer !important;
            padding: 0 !important;
        }
        .pdfpro-admin-wrap .button-link:hover {
            color: #7DD3FC !important;
        }
        /* Tables styling */
        .pdfpro-admin-wrap table.wp-list-table {
            background: #0F172A !important;
            border: 1px solid #1E293B !important;
            border-collapse: collapse !important;
            border-radius: 8px !important;
            overflow: hidden !important;
        }
        .pdfpro-admin-wrap table.wp-list-table th {
            background: #1E293B !important;
            color: #F8FAFC !important;
            font-weight: 700 !important;
            border-bottom: 2px solid #334155 !important;
            padding: 12px 16px !important;
        }
        .pdfpro-admin-wrap table.wp-list-table td {
            color: #CBD5E1 !important;
            border-bottom: 1px solid #1E293B !important;
            padding: 14px 16px !important;
            vertical-align: middle !important;
        }
        .pdfpro-admin-wrap table.wp-list-table tr:hover {
            background: #1E293B !important;
        }
        .pdfpro-admin-wrap table.wp-list-table tr.alternate {
            background: #111827 !important;
        }
        .pdfpro-admin-wrap table.wp-list-table tr.alternate:hover {
            background: #1E293B !important;
        }
        /* Code blocks & monospace */
        .pdfpro-admin-wrap code {
            background: #1E293B !important;
            color: #38BDF8 !important;
            padding: 3px 6px !important;
            border-radius: 4px !important;
            font-family: Menlo, Monaco, Consolas, "Courier New", monospace !important;
            font-size: 12.5px !important;
            border: 1px solid #334155 !important;
        }
        .pdfpro-admin-wrap .pdfpro-key-line {
            display: flex !important;
            align-items: center !important;
            gap: 8px !important;
            flex-wrap: wrap !important;
        }
        .pdfpro-admin-wrap .pdfpro-copy-key {
            min-height: 26px !important;
            padding: 2px 10px !important;
            font-size: 11px !important;
            color: #67E8F9 !important;
            border-color: #0E7490 !important;
            background: rgba(14, 116, 144, 0.25) !important;
        }
        .pdfpro-admin-wrap .pdfpro-copy-key.is-copied {
            color: #A7F3D0 !important;
            border-color: #10B981 !important;
            background: rgba(16, 185, 129, 0.22) !important;
        }
        /* Inputs, textareas, selects */
        .pdfpro-admin-wrap input[type="text"],
        .pdfpro-admin-wrap input[type="url"],
        .pdfpro-admin-wrap input[type="number"],
        .pdfpro-admin-wrap input[type="date"],
        .pdfpro-admin-wrap select,
        .pdfpro-admin-wrap textarea {
            background: #1E293B !important;
            border: 1px solid #475569 !important;
            color: #F8FAFC !important;
            border-radius: 6px !important;
            padding: 8px 12px !important;
            font-size: 13.5px !important;
            outline: none !important;
            transition: all 0.2s ease !important;
            box-shadow: none !important;
        }
        .pdfpro-admin-wrap input[type="text"]:focus,
        .pdfpro-admin-wrap input[type="url"]:focus,
        .pdfpro-admin-wrap input[type="number"]:focus,
        .pdfpro-admin-wrap input[type="date"]:focus,
        .pdfpro-admin-wrap select:focus,
        .pdfpro-admin-wrap textarea:focus {
            border-color: #38BDF8 !important;
            box-shadow: 0 0 0 2px rgba(56, 189, 248, 0.2) !important;
            background: #0F172A !important;
        }
        /* Badges */
        .pdfpro-admin-wrap .badge {
            display: inline-block !important;
            padding: 4px 8px !important;
            border-radius: 9999px !important;
            font-size: 11px !important;
            font-weight: 700 !important;
            text-transform: uppercase !important;
            letter-spacing: 0.05em !important;
        }
        .pdfpro-admin-wrap .badge-active {
            background-color: rgba(16, 185, 129, 0.15) !important;
            color: #34D399 !important;
            border: 1px solid rgba(16, 185, 129, 0.3) !important;
        }
        .pdfpro-admin-wrap .badge-suspended {
            background-color: rgba(239, 68, 68, 0.15) !important;
            color: #F87171 !important;
            border: 1px solid rgba(239, 68, 68, 0.3) !important;
        }
        /* Form layouts */
        .pdfpro-admin-wrap .form-table th {
            color: #F8FAFC !important;
            font-weight: 600 !important;
            width: 200px !important;
            padding: 20px 10px 20px 0 !important;
        }
        .pdfpro-admin-wrap .form-table td {
            padding: 15px 10px !important;
        }
        /* Notices overrides inside wrapper */
        .pdfpro-admin-wrap .notice {
            background: #111827 !important;
            border: 1px solid #1E293B !important;
            border-left-width: 4px !important;
            color: #CBD5E1 !important;
            border-radius: 6px !important;
            padding: 12px 16px !important;
            margin: 0 0 20px 0 !important;
        }
        .pdfpro-admin-wrap .notice-success {
            border-left-color: #10B981 !important;
        }
        .pdfpro-admin-wrap .notice-warning {
            border-left-color: #F59E0B !important;
        }
        .pdfpro-admin-wrap .notice-error {
            border-left-color: #EF4444 !important;
        }
        /* Hide default WP subheader hr */
        .pdfpro-admin-wrap hr.wp-header-end {
            display: none !important;
        }
    </style>
    <?php
}


function pdfpro_licensing_add_admin_menu() {
    // Menu cha chính
    add_menu_page(
        'PDF Pro Suite',
        'PDF Pro Suite',
        'manage_options',
        'pdfpro-licensing',
        'pdfpro_licensing_render_licenses_page',
        'dashicons-awards',
        80
    );

    // Submenu 1: Quản lý Licenses (ghi đè slug của menu cha đềElàm trang mặc định)
    add_submenu_page(
        'pdfpro-licensing',
        'Quản lý Licenses',
        'Quản lý Licenses',
        'manage_options',
        'pdfpro-licensing',
        'pdfpro_licensing_render_licenses_page'
    );

    // Submenu 2: Cấu hình Cập nhật
    add_submenu_page(
        'pdfpro-licensing',
        'Cấu hình Cập nhật',
        'Cấu hình Cập nhật',
        'manage_options',
        'pdfpro-updates',
        'pdfpro_licensing_render_updates_page'
    );

    // Submenu 3: Nhật ký Lỗi
    add_submenu_page(
        'pdfpro-licensing',
        'Nhật ký Lỗi',
        'Nhật ký Lỗi',
        'manage_options',
        'pdfpro-errors',
        'pdfpro_licensing_render_errors_page'
    );

    // Submenu 4: Tạo Key Mới
    add_submenu_page(
        'pdfpro-licensing',
        'Tạo Key Mới',
        'Tạo Key Mới',
        'manage_options',
        'pdfpro-create-license',
        'pdfpro_licensing_render_create_page'
    );
}

/**
 * Xử lý dữ liệu form (POST requests) trong trang quản trềEtrước khi xuất HTML
 */
function pdfpro_licensing_handle_admin_actions() {
    if (!isset($_POST['pdfpro_license_nonce']) || !wp_verify_nonce($_POST['pdfpro_license_nonce'], 'pdfpro_license_action')) {
        return;
    }

    if (!current_user_can('manage_options')) {
        return;
    }

    global $wpdb;
    $table_licenses = $wpdb->prefix . 'pdfpro_licenses';
    $table_activations = $wpdb->prefix . 'pdfpro_activations';

    $redirect_url = admin_url('admin.php?page=pdfpro-licensing');

    // A. Xử lý tạo License mới
    if (isset($_POST['pdfpro_action']) && $_POST['pdfpro_action'] === 'create_license') {
        $license_key = sanitize_text_field($_POST['license_key'] ?? '');
        $max_devices = intval($_POST['max_devices'] ?? 1);
        $expires_at = sanitize_text_field($_POST['expires_at'] ?? '');
        $status = sanitize_text_field($_POST['status'] ?? 'active');

        // Tự tạo key ngẫu nhiên nếu bềEtrống
        if (empty($license_key)) {
            $license_key = 'PDFPRO-' . strtoupper(wp_generate_password(4, false, false)) . '-' . 
                           strtoupper(wp_generate_password(4, false, false)) . '-' . 
                           strtoupper(wp_generate_password(4, false, false)) . '-' . 
                           strtoupper(wp_generate_password(4, false, false));
        }

        $db_data = array(
            'license_key' => $license_key,
            'max_devices' => $max_devices,
            'status'      => $status,
            'expires_at'  => !empty($expires_at) ? date('Y-m-d H:i:s', strtotime($expires_at)) : null,
        );

        $wpdb->insert($table_licenses, $db_data);
        wp_safe_redirect(add_query_arg('pdfpro_msg', 'created', $redirect_url));
        exit;
    }

    // B. Xử lý cập nhật License đã có (Sửa Key)
    if (isset($_POST['pdfpro_action']) && $_POST['pdfpro_action'] === 'update_license') {
        $license_id = intval($_POST['license_id'] ?? 0);
        $license_key = sanitize_text_field($_POST['license_key'] ?? '');
        $max_devices = intval($_POST['max_devices'] ?? 1);
        $expires_at = sanitize_text_field($_POST['expires_at'] ?? '');
        $status = sanitize_text_field($_POST['status'] ?? 'active');

        if ($license_id > 0 && !empty($license_key)) {
            $db_data = array(
                'license_key' => $license_key,
                'max_devices' => $max_devices,
                'status'      => $status,
                'expires_at'  => !empty($expires_at) ? date('Y-m-d H:i:s', strtotime($expires_at)) : null,
            );

            $wpdb->update($table_licenses, $db_data, array('id' => $license_id));
            wp_safe_redirect(add_query_arg('pdfpro_msg', 'updated', $redirect_url));
            exit;
        }
    }

    // C. Xử lý xóa License Key
    if (isset($_POST['pdfpro_action']) && $_POST['pdfpro_action'] === 'delete_license') {
        $license_id = intval($_POST['license_id'] ?? 0);
        if ($license_id > 0) {
            $wpdb->delete($table_licenses, array('id' => $license_id));
            $wpdb->delete($table_activations, array('license_id' => $license_id));
            wp_safe_redirect(add_query_arg('pdfpro_msg', 'deleted', $redirect_url));
            exit;
        }
    }

    // D. Reset device activation.
    if (isset($_POST['pdfpro_action']) && $_POST['pdfpro_action'] === 'reset_device') {
        $activation_id = intval($_POST['activation_id'] ?? 0);
        if ($activation_id > 0) {
            $wpdb->delete($table_activations, array('id' => $activation_id));
            wp_safe_redirect(add_query_arg('pdfpro_msg', 'device_reset', $redirect_url));
            exit;
        }
    }

    // E. Xử lý đổi trạng thái Key (Active <=> Suspended)
    if (isset($_POST['pdfpro_action']) && $_POST['pdfpro_action'] === 'toggle_status') {
        $license_id = intval($_POST['license_id'] ?? 0);
        $new_status = sanitize_text_field($_POST['new_status'] ?? 'active');
        if ($license_id > 0) {
            $wpdb->update($table_licenses, array('status' => $new_status), array('id' => $license_id));
            wp_safe_redirect(add_query_arg('pdfpro_msg', 'status_toggled', $redirect_url));
            exit;
        }
    }

    // F. Xử lý sinh cặp khóa RSA mới
    if (isset($_POST['pdfpro_action']) && $_POST['pdfpro_action'] === 'regenerate_keys') {
        if (extension_loaded('openssl') && function_exists('pdfpro_licensing_generate_rsa_keys')) {
            pdfpro_licensing_generate_rsa_keys();
            wp_safe_redirect(add_query_arg('pdfpro_msg', 'keys_regenerated', $redirect_url));
            exit;
        }
    }

    // G. Xử lý lưu cấu hình cập nhật phần mềm
    if (!function_exists('pdfpro_licensing_decode_manifest_input')) {
        function pdfpro_licensing_decode_manifest_input($manifest_input) {
            $manifest_input = trim((string) $manifest_input);
            if ($manifest_input === '') {
                return array();
            }

            if (filter_var($manifest_input, FILTER_VALIDATE_URL)) {
                $response = wp_remote_get($manifest_input, array(
                    'timeout'     => 10,
                    'redirection' => 3,
                ));

                if (!is_wp_error($response)) {
                    $body = wp_remote_retrieve_body($response);
                    if (is_string($body) && trim($body) !== '') {
                        $manifest_input = $body;
                    }
                }
            }

            $decoded_manifest = json_decode($manifest_input, true);
            return is_array($decoded_manifest) ? $decoded_manifest : array();
        }
    }

    if (isset($_POST['pdfpro_action']) && $_POST['pdfpro_action'] === 'save_software_settings') {
        $manifest_json = isset($_POST['pdfpro_update_manifest_json']) ? wp_unslash($_POST['pdfpro_update_manifest_json']) : '';
        $manifest = pdfpro_licensing_decode_manifest_input($manifest_json);

        $posted_version = sanitize_text_field($_POST['pdfpro_latest_version'] ?? '');
        $posted_download_url = esc_url_raw($_POST['pdfpro_download_url'] ?? '');
        $posted_sha256 = sanitize_text_field($_POST['pdfpro_update_sha256'] ?? '');
        $posted_file_size = $_POST['pdfpro_update_file_size'] ?? '';
        $posted_release_date = sanitize_text_field($_POST['pdfpro_update_release_date'] ?? '');
        $posted_changelog = $_POST['pdfpro_changelog'] ?? '';

        $latest_version = $posted_version !== '' ? $posted_version : sanitize_text_field($manifest['version'] ?? '1.0.0');
        $download_url = $posted_download_url !== '' ? $posted_download_url : esc_url_raw($manifest['download_url'] ?? '');
        $sha256 = $posted_sha256 !== '' ? $posted_sha256 : sanitize_text_field($manifest['sha256'] ?? '');
        $file_size = $posted_file_size !== '' ? $posted_file_size : ($manifest['size'] ?? ($manifest['file_size'] ?? 0));
        $release_date = $posted_release_date !== '' ? $posted_release_date : sanitize_text_field($manifest['release_date'] ?? '');
        $mandatory = isset($_POST['pdfpro_update_mandatory']) || (!empty($manifest['mandatory']));
        $changelog = $posted_changelog !== '' ? $posted_changelog : ($manifest['changelog'] ?? '');

        update_option('pdfpro_latest_version', $latest_version);
        update_option('pdfpro_download_url', $download_url);
        update_option('pdfpro_update_sha256', strtolower(preg_replace('/[^a-fA-F0-9]/', '', $sha256)));
        update_option('pdfpro_update_file_size', absint($file_size));
        update_option('pdfpro_update_release_date', $release_date);
        update_option('pdfpro_update_mandatory', $mandatory ? '1' : '0');
        update_option('pdfpro_changelog', wp_kses_post($changelog));
        $updates_url = admin_url('admin.php?page=pdfpro-updates');
        wp_safe_redirect(add_query_arg('pdfpro_msg', 'settings_saved', $updates_url));
        exit;
    }

    // H. Xử lý xóa nhật ký lỗi
    if (isset($_POST['pdfpro_action']) && $_POST['pdfpro_action'] === 'clear_error_logs') {
        $log_file = PDFPRO_LICENSING_DIR . 'error_logs.txt';
        if (file_exists($log_file)) {
            unlink($log_file);
        }
        $errors_url = admin_url('admin.php?page=pdfpro-errors');
        wp_safe_redirect(add_query_arg('pdfpro_msg', 'logs_cleared', $errors_url));
        exit;
    }
}

/**
 * Hiển thềEthông báo (Notices) động dựa trên query parameter
 */
function pdfpro_licensing_render_notices() {
    if (!isset($_GET['pdfpro_msg'])) {
        return;
    }
    $msg = sanitize_text_field($_GET['pdfpro_msg']);
    switch ($msg) {
        case 'created':
            echo '<div class="notice notice-success is-dismissible"><p>Đã tạo thành công License Key mới!</p></div>';
            break;
        case 'updated':
            echo '<div class="notice notice-success is-dismissible"><p>Đã cập nhật thành công License Key!</p></div>';
            break;
        case 'deleted':
            echo '<div class="notice notice-warning is-dismissible"><p>Đã xóa thành công License Key.</p></div>';
            break;
        case 'device_reset':
            echo '<div class="notice notice-success is-dismissible"><p>Đã thu hồi kích hoạt của thiết bềEthành công.</p></div>';
            break;
        case 'status_toggled':
            echo '<div class="notice notice-success is-dismissible"><p>Đã chuyển đổi trạng thái license thành công.</p></div>';
            break;
        case 'keys_regenerated':
            echo '<div class="notice notice-success is-dismissible"><p>Đã sinh cặp khóa bảo mật RSA mới thành công!</p></div>';
            break;
        case 'settings_saved':
            echo '<div class="notice notice-success is-dismissible"><p>Đã lưu cấu hình cập nhật phần mềm thành công!</p></div>';
            break;
        case 'logs_cleared':
            echo '<div class="notice notice-success is-dismissible"><p>Đã xóa sạch nhật ký lỗi thành công!</p></div>';
            break;
    }
}

/**
 * SUBMENU 1: Hiển thềEgiao diện quản lý Licenses & Khóa RSA
 */
function pdfpro_licensing_render_licenses_page() {
    global $wpdb;
    
    // Hiển thềEthông báo kết quả hành động
    pdfpro_licensing_render_notices();

    // Hiển thềEcảnh báo hềEthống nếu có lỗi cấu hình
    if (!extension_loaded('openssl')) {
        echo '<div class="notice notice-error"><p><strong>Cảnh báo:</strong> Thư viện <strong>OpenSSL</strong> của PHP chưa được kích hoạt. Cơ chế ký bản quyền RSA sẽ không hoạt động!</p></div>';
    } elseif (!file_exists(PDFPRO_PUBLIC_KEY_PATH) || !file_exists(PDFPRO_PRIVATE_KEY_PATH)) {
        echo '<div class="notice notice-warning"><p><strong>Lưu ý:</strong> Cặp khóa bảo mật RSA chưa được khởi tạo. Vui lòng bấm nút <strong>Sinh Khóa RSA</strong> ềEcột bên phải đềEtiếp tục.</p></div>';
    }

    $table_licenses = $wpdb->prefix . 'pdfpro_licenses';
    $table_activations = $wpdb->prefix . 'pdfpro_activations';

    // Truy vấn danh sách Licenses kèm sềEmáy đã active
    $licenses = $wpdb->get_results("
        SELECT l.*, COUNT(a.id) as active_count 
        FROM $table_licenses l
        LEFT JOIN $table_activations a ON l.id = a.license_id
        GROUP BY l.id
        ORDER BY l.id DESC
    ");

    // Đọc Public Key
    $public_key = '';
    if (file_exists(PDFPRO_PUBLIC_KEY_PATH)) {
        $public_key = file_get_contents(PDFPRO_PUBLIC_KEY_PATH);
    }
    ?>
    <div class="wrap pdfpro-admin-wrap">
        <h1 class="wp-heading-inline">PDF Pro Licensing Server - Quản lý Bản Quyền</h1>
        <hr class="wp-header-end">

        <div style="margin-top: 20px;">
            
            <!-- Danh sách Licenses -->
            <div class="card" style="max-width: 100%; margin-bottom: 20px; padding: 20px;">
                <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 15px;">
                    <h2 style="margin: 0;">Danh sách các mã kích hoạt (License Keys)</h2>
                    <a href="<?php echo esc_url(admin_url('admin.php?page=pdfpro-create-license')); ?>" class="button button-primary">Tạo Key Mới</a>
                </div>
                <table class="wp-list-table widefat fixed striped table-view-list">
                    <thead>
                        <tr>
                            <th style="width: 5%;">STT</th>
                            <th style="width: 35%;">License Key / Thiết bềEkích hoạt</th>
                            <th style="width: 15%;">SềEmáy kích hoạt</th>
                            <th style="width: 10%;">Trạng thái</th>
                            <th style="width: 15%;">Hạn dùng</th>
                            <th style="width: 20%;">Hành động</th>
                        </tr>
                    </thead>
                    <tbody>
                        <?php if (empty($licenses)) : ?>
                            <tr>
                                <td colspan="6" style="text-align: center;">Chưa có mã kích hoạt nào được tạo.</td>
                            </tr>
                        <?php else : ?>
                            <?php 
                            $stt = 1;
                            foreach ($licenses as $lic) : 
                            ?>
                                <tr>
                                    <td><?php echo esc_html($stt); ?></td>
                                    <td>
                                        <div class="pdfpro-key-line">
                                            <strong><code><?php echo esc_html($lic->license_key); ?></code></strong>
                                            <button type="button" class="button button-small pdfpro-copy-key" data-copy-value="<?php echo esc_attr($lic->license_key); ?>">Copy key</button>
                                        </div>
                                        <?php
                                        $active_devices = $wpdb->get_results($wpdb->prepare(
                                            "SELECT machine_name, machine_id FROM $table_activations WHERE license_id = %d",
                                            $lic->id
                                        ));
                                        if (!empty($active_devices)) {
                                            echo '<div style="font-size: 11px; color: #555; margin-top: 4px; padding-left: 2px;">';
                                            foreach ($active_devices as $dev) {
                                                echo '<span style="color: #0F766E; font-weight: 500;">' . esc_html($dev->machine_name) . '</span> (<code>' . esc_html($dev->machine_id) . '</code>)<br/>';
                                            }
                                            echo '</div>';
                                        }
                                        ?>
                                    </td>
                                    <td>
                                        <?php echo esc_html($lic->active_count); ?> / <?php echo esc_html($lic->max_devices); ?>
                                        <a href="#details-<?php echo esc_attr($lic->id); ?>" onclick="jQuery('#devices-<?php echo esc_attr($lic->id); ?>').toggle(); return false;" style="margin-left:5px; font-size:11px;">(Chi tiết máy)</a>
                                    </td>
                                    <td>
                                        <span class="badge <?php echo $lic->status === 'active' ? 'badge-active' : 'badge-suspended'; ?>">
                                            <?php echo esc_html(strtoupper($lic->status)); ?>
                                        </span>
                                    </td>
                                    <td>
                                        <?php echo $lic->expires_at ? esc_html(date('d/m/Y H:i', strtotime($lic->expires_at))) : '<span style="color: #6c757d;">Vĩnh viễn</span>'; ?>
                                    </td>
                                    <td>
                                        <a href="<?php echo esc_url(admin_url('admin.php?page=pdfpro-create-license&action=edit&id=' . $lic->id)); ?>" class="button button-small" style="text-decoration: none;">Sửa</a>
                                        |
                                        <form method="post" style="display: inline-block;" onsubmit="return confirm('Bạn có chắc chắn muốn xóa key này? Mọi thiết bị đang kích hoạt key này sẽ bị ngắt kết nối.');">
                                            <?php wp_nonce_field('pdfpro_license_action', 'pdfpro_license_nonce'); ?>
                                            <input type="hidden" name="pdfpro_action" value="delete_license">
                                            <input type="hidden" name="license_id" value="<?php echo esc_attr($lic->id); ?>">
                                            <button type="submit" class="button button-link-delete" style="color: #a00; text-decoration: none;">Xóa</button>
                                        </form>
                                        |
                                        <form method="post" style="display: inline-block;">
                                            <?php wp_nonce_field('pdfpro_license_action', 'pdfpro_license_nonce'); ?>
                                            <input type="hidden" name="pdfpro_action" value="toggle_status">
                                            <input type="hidden" name="license_id" value="<?php echo esc_attr($lic->id); ?>">
                                            <?php if ($lic->status === 'active') : ?>
                                                <input type="hidden" name="new_status" value="suspended">
                                                <button type="submit" class="button-link">Khóa</button>
                                            <?php else : ?>
                                                <input type="hidden" name="new_status" value="active">
                                                <button type="submit" class="button-link">Mở khóa</button>
                                            <?php endif; ?>
                                        </form>
                                    </td>
                                </tr>
                                <!-- Hộp con ẩn/hiển thị chi tiết thiết bị -->
                                <tr id="devices-<?php echo esc_attr($lic->id); ?>" style="display: none; background: #fafafa;">
                                    <td colspan="6" style="padding: 10px 20px;">
                                        <strong>Danh sách thiết bị kết nối chi tiết:</strong>
                                        <?php
                                        $devices = $wpdb->get_results($wpdb->prepare(
                                            "SELECT * FROM $table_activations WHERE license_id = %d",
                                            $lic->id
                                        ));
                                        if (empty($devices)) {
                                            echo '<p style="color: #888; font-style: italic; margin: 5px 0 0 0;">Chưa có thiết bị nào kích hoạt key này.</p>';
                                        } else {
                                            echo '<table class="wp-list-table widefat fixed striped" style="margin-top: 5px; width: 100%;">';
                                            echo '<thead><tr><th>Tên thiết bị</th><th>ID vân tay thiết bị</th><th>Thời điểm kích hoạt</th><th>Hành động</th></tr></thead><tbody>';
                                            foreach ($devices as $dev) {
                                                echo '<tr>';
                                                echo '<td>' . esc_html($dev->machine_name) . '</td>';
                                                echo '<td><code>' . esc_html($dev->machine_id) . '</code></td>';
                                                echo '<td>' . esc_html(date('d/m/Y H:i', strtotime($dev->activated_at))) . '</td>';
                                                echo '<td>';
                                                echo '<form method="post" onsubmit="return confirm(\'Hủy kích hoạt máy này?\');">';
                                                wp_nonce_field('pdfpro_license_action', 'pdfpro_license_nonce');
                                                echo '<input type="hidden" name="pdfpro_action" value="reset_device">';
                                                echo '<input type="hidden" name="activation_id" value="' . esc_attr($dev->id) . '">';
                                                echo '<button type="submit" class="button button-small">Hủy máy này</button>';
                                                echo '</form>';
                                                echo '</td>';
                                                echo '</tr>';
                                            }
                                            echo '</tbody></table>';
                                        }
                                        ?>
                                    </td>
                                </tr>
                            <?php 
                            $stt++;
                            endforeach; 
                            ?>
                        <?php endif; ?>
                    </tbody>
                </table>
            </div>

            <!-- Hộp thông tin RSA -->
            <div class="card" style="padding: 20px; max-width: 800px; margin-top: 20px;">
                <h2>Thông tin mã khóa RSA</h2>
                <p style="font-size: 13px; color: #555;">
                    Sao chép mã khóa công khai (Public Key) dưới đây và nhúng vào mã nguồn ứng dụng <strong>C# WPF Client</strong> đềExác minh Token bản quyền được gửi từ server.
                </p>
                
                <?php if (empty($public_key)) : ?>
                    <div style="background: #fff3cd; border-left: 4px solid #ffc107; padding: 10px; margin-bottom: 10px;">
                        <span style="color: #856404;">Khóa công khai chưa được tạo hoặc thư viện OpenSSL PHP chưa hoạt động.</span>
                    </div>
                <?php else : ?>
                    <textarea readonly style="width: 100%; height: 180px; font-family: monospace; font-size: 11px; background: #f9f9f9; padding: 5px; margin-bottom: 10px;"><?php echo esc_textarea($public_key); ?></textarea>
                <?php endif; ?>

                <form method="post" onsubmit="return confirm('Bạn có chắc chắn muốn tạo cặp khóa RSA mới? Lưu ý: Nếu tạo lại, bạn bắt buộc phải cập nhật lại Public Key mới này vào trong mã nguồn C# Client của ứng dụng.');">
                    <?php wp_nonce_field('pdfpro_license_action', 'pdfpro_license_nonce'); ?>
                    <input type="hidden" name="pdfpro_action" value="regenerate_keys">
                    <input type="submit" class="button button-secondary" style="width: 100%; text-align: center;" value="<?php echo empty($public_key) ? 'Sinh Cặp Khóa RSA' : 'Tạo Lại Cặp Khóa RSA mới'; ?>">
                </form>
                
                <p style="font-size: 12px; color: #c00; font-style: italic; margin-top: 10px;">
                    *Lưu ý: Không chia sẻ file private_key.pem lưu trong máy chủ WordPress cho bất kỳ ai.
                </p>
            </div>
        </div>
    </div>
    <script>
        (function() {
            function fallbackCopy(text) {
                var textarea = document.createElement('textarea');
                textarea.value = text;
                textarea.setAttribute('readonly', 'readonly');
                textarea.style.position = 'fixed';
                textarea.style.left = '-9999px';
                document.body.appendChild(textarea);
                textarea.select();
                try {
                    document.execCommand('copy');
                } finally {
                    document.body.removeChild(textarea);
                }
                return Promise.resolve();
            }

            function copyText(text) {
                if (navigator.clipboard && navigator.clipboard.writeText) {
                    return navigator.clipboard.writeText(text).catch(function() {
                        return fallbackCopy(text);
                    });
                }
                return fallbackCopy(text);
            }

            document.addEventListener('click', function(event) {
                var button = event.target.closest('.pdfpro-copy-key');
                if (!button) {
                    return;
                }
                event.preventDefault();
                var text = button.getAttribute('data-copy-value') || '';
                if (!text) {
                    return;
                }
                copyText(text).then(function() {
                    var originalText = button.textContent;
                    button.textContent = 'Copied';
                    button.classList.add('is-copied');
                    window.setTimeout(function() {
                        button.textContent = originalText;
                        button.classList.remove('is-copied');
                    }, 1400);
                });
            });
        })();
    </script>
    <?php
}

/**
 * SUBMENU 2: Hiển thềEgiao diện cấu hình Cập Nhật Phần Mềm
 */
function pdfpro_licensing_render_updates_page() {
    // Hiển thềEthông báo kết quả hành động
    pdfpro_licensing_render_notices();

    $latest_version = get_option('pdfpro_latest_version', '1.0.0');
    $download_url = get_option('pdfpro_download_url', '');
    $update_sha256 = get_option('pdfpro_update_sha256', '');
    $update_file_size = get_option('pdfpro_update_file_size', 0);
    $update_release_date = get_option('pdfpro_update_release_date', '');
    $update_mandatory = get_option('pdfpro_update_mandatory', '0');
    $changelog = get_option('pdfpro_changelog', '');
    ?>
    <div class="wrap pdfpro-admin-wrap">
        <h1 class="wp-heading-inline">PDF Pro Licensing Server - Cấu Hình Cập Nhật</h1>
        <hr class="wp-header-end">

        <div style="max-width: 800px; margin-top: 20px;">
            <div class="card" style="padding: 20px;">
                <h2>Thông Tin Bản Cập Nhật Ứng Dụng</h2>
                <p style="font-size: 13px; color: #666; margin-bottom: 20px;">
                    Cấu hình phiên bản mới nhất đềEứng dụng Desktop khách hàng tự động kiểm tra và thông báo tải vềEkhi mềEapp.
                </p>

                <form method="post">
                    <?php wp_nonce_field('pdfpro_license_action', 'pdfpro_license_nonce'); ?>
                    <input type="hidden" name="pdfpro_action" value="save_software_settings">
                    
                    <table class="form-table" role="presentation">
                        <tbody>
                            <tr>
                                <th scope="row"><label for="pdfpro_update_manifest_json"><strong>Manifest JSON:</strong></label></th>
                                <td>
                                    <textarea id="pdfpro_update_manifest_json" name="pdfpro_update_manifest_json" rows="8" class="large-text code" placeholder="Paste JSON or update-manifest URL here" style="width: 100%;"></textarea>
                                    <p class="description">Optional. Paste update-manifest JSON or a direct URL to update-manifest.json to auto-fill version, SHA256, file size, release date, mandatory flag, and changelog on save. Manual fields below can override the manifest.</p>
                                </td>
                            </tr>
                            <tr>
                                <th scope="row"><label><strong>Publish Token (GitHub Secret):</strong></label></th>
                                <td>
                                    <?php 
                                    $pub_token = defined('PDFPRO_PUBLISH_TOKEN') ? PDFPRO_PUBLISH_TOKEN : get_option('pdfpro_publish_token', '');
                                    if (empty($pub_token)) {
                                        $pub_token = wp_generate_password(32, false);
                                        update_option('pdfpro_publish_token', $pub_token);
                                    }
                                    ?>
                                    <input type="text" readonly value="<?php echo esc_attr($pub_token); ?>" class="large-text code" style="width: 100%; background: #1a202c; color: #a0aec0; border: 1px solid #4a5568; padding: 8px;" onclick="this.select();">
                                    <p class="description">Sao chép mã Token này để điền vào biến <code>PDFPRO_PUBLISH_TOKEN</code> trong mục Secrets trên GitHub.</p>
                                </td>
                            </tr>
                            <tr>
                                <th scope="row"><label for="pdfpro_latest_version"><strong>Phiên bản mới nhất (Latest Version):</strong></label></th>
                                <td>
                                    <input type="text" id="pdfpro_latest_version" name="pdfpro_latest_version" value="<?php echo esc_attr($latest_version); ?>" class="regular-text" placeholder="Ví dụ: 1.0.2">
                                    <p class="description">Cần khớp với định dạng Assembly Version (ví dụ: 1.0.2).</p>
                                </td>
                            </tr>
                            <tr>
                                <th scope="row"><label for="pdfpro_download_url"><strong>Đường dẫn tải bản cập nhật (Download URL):</strong></label></th>
                                <td>
                                    <input type="url" id="pdfpro_download_url" name="pdfpro_download_url" value="<?php echo esc_url($download_url); ?>" class="large-text" placeholder="Ví dụ: https://link-tai-google-drive/file.zip" style="width: 100%;">
                                    <p class="description">Đường dẫn tệp ZIP hoặc tệp cài đặt (ví dụ: Google Drive link dạng trực tiếp hoặc chia sẻ mềE.</p>
                                </td>
                            </tr>
                            <tr>
                                <th scope="row"><label for="pdfpro_update_sha256"><strong>SHA256:</strong></label></th>
                                <td>
                                    <input type="text" id="pdfpro_update_sha256" name="pdfpro_update_sha256" value="<?php echo esc_attr($update_sha256); ?>" class="large-text code" maxlength="64" placeholder="SHA256 from update-manifest.json" style="width: 100%;">
                                    <p class="description">Used by the desktop app to verify the downloaded ZIP before installing.</p>
                                </td>
                            </tr>
                            <tr>
                                <th scope="row"><label for="pdfpro_update_file_size"><strong>File size:</strong></label></th>
                                <td>
                                    <input type="number" id="pdfpro_update_file_size" name="pdfpro_update_file_size" value="<?php echo esc_attr($update_file_size); ?>" class="regular-text" min="0" step="1" placeholder="80871736">
                                    <p class="description">ZIP size in bytes from update-manifest.json.</p>
                                </td>
                            </tr>
                            <tr>
                                <th scope="row"><label for="pdfpro_update_release_date"><strong>Release date:</strong></label></th>
                                <td>
                                    <input type="text" id="pdfpro_update_release_date" name="pdfpro_update_release_date" value="<?php echo esc_attr($update_release_date); ?>" class="regular-text" placeholder="2026-06-06T00:00:00Z">
                                    <p class="description">Release timestamp from update-manifest.json. ISO 8601 is recommended.</p>
                                </td>
                            </tr>
                            <tr>
                                <th scope="row"><label for="pdfpro_update_mandatory"><strong>Mandatory update:</strong></label></th>
                                <td>
                                    <label>
                                        <input type="checkbox" id="pdfpro_update_mandatory" name="pdfpro_update_mandatory" value="1" <?php checked($update_mandatory, '1'); ?>>
                                        Force this version as a required update
                                    </label>
                                </td>
                            </tr>
                            <tr>
                                <th scope="row"><label for="pdfpro_changelog"><strong>Thông tin cập nhật (Changelog):</strong></label></th>
                                <td>
                                    <textarea id="pdfpro_changelog" name="pdfpro_changelog" rows="8" class="large-text" placeholder="Nhập các thay đổi trong phiên bản mới..." style="width: 100%;"><?php echo esc_textarea($changelog); ?></textarea>
                                    <p class="description">Nhập các cải tiến, sửa lỗi trong phiên bản này đềEhiển thềEtrên thông báo ứng dụng.</p>
                                </td>
                            </tr>
                        </tbody>
                    </table>

                    <p class="submit">
                        <input type="submit" class="button button-primary" value="Lưu Cấu Hình Cập Nhật">
                    </p>
                </form>
            </div>
        </div>
    </div>
    <?php
}

/**
 * SUBMENU 3: Hiển thềEgiao diện Nhật Ký Lỗi Khách Hàng (Telemetry)
 */
function pdfpro_licensing_render_errors_page() {
    // Hiển thềEthông báo kết quả hành động
    pdfpro_licensing_render_notices();

    $log_file = PDFPRO_LICENSING_DIR . 'error_logs.txt';
    $error_logs = '';
    if (file_exists($log_file)) {
        $error_logs = file_get_contents($log_file);
    }
    ?>
    <div class="wrap pdfpro-admin-wrap">
        <h1 class="wp-heading-inline">PDF Pro Licensing Server - Nhật Ký Lỗi Khách Hàng</h1>
        <hr class="wp-header-end">

        <div style="margin-top: 20px; max-width: 1000px;">
            <div class="card" style="padding: 20px;">
                <h2>Telemetry Error Logs</h2>
                <p style="font-size: 13px; color: #666; margin-bottom: 20px;">
                    Nhật ký các lỗi runtime/crash được tự động báo cáo vềEtừ ứng dụng Desktop của khách hàng đềElập trình viên theo dõi và gỡ lỗi (debug).
                </p>

                <textarea readonly style="width: 100%; height: 450px; font-family: monospace; font-size: 12px; background: #fafafa; padding: 15px; border: 1px solid #ccd0d4; border-radius: 4px;" placeholder="Chưa có nhật ký lỗi nào được ghi nhận từ phía khách hàng."><?php echo esc_textarea($error_logs); ?></textarea>
                
                <?php if (!empty($error_logs)) : ?>
                    <form method="post" style="margin-top: 20px;" onsubmit="return confirm('Bạn có chắc chắn muốn xóa sạch hoàn toàn nhật ký lỗi trên máy chủ?');">
                        <?php wp_nonce_field('pdfpro_license_action', 'pdfpro_license_nonce'); ?>
                        <input type="hidden" name="pdfpro_action" value="clear_error_logs">
                        <input type="submit" class="button button-secondary" value="Xóa Sạch Toàn BềENhật Ký Lỗi">
                    </form>
                <?php endif; ?>
            </div>
        </div>
    </div>
    <?php
}

/**
 * SUBMENU 4: Tạo hoặc Sửa License Key
 */
function pdfpro_licensing_render_create_page() {
    global $wpdb;
    
    // Hiển thềEthông báo notices
    pdfpro_licensing_render_notices();

    $table_licenses = $wpdb->prefix . 'pdfpro_licenses';

    // Kiểm tra chế đềESửa (Edit)
    $edit_license = null;
    if (isset($_GET['action']) && $_GET['action'] === 'edit' && isset($_GET['id'])) {
        $edit_id = intval($_GET['id']);
        $edit_license = $wpdb->get_row($wpdb->prepare("SELECT * FROM $table_licenses WHERE id = %d", $edit_id));
    }
    ?>
    <div class="wrap pdfpro-admin-wrap">
        <h1><?php echo $edit_license ? 'Chỉnh Sửa Key Kích Hoạt' : 'Tạo Key Kích Hoạt Mới'; ?></h1>
        <hr class="wp-header-end">

        <div style="max-width: 600px; margin-top: 20px;">
            <div class="card" style="padding: 20px;">
                <?php if ($edit_license) : ?>
                    <h2>Cập nhật thông tin License</h2>
                    <form method="post">
                        <?php wp_nonce_field('pdfpro_license_action', 'pdfpro_license_nonce'); ?>
                        <input type="hidden" name="pdfpro_action" value="update_license">
                        <input type="hidden" name="license_id" value="<?php echo esc_attr($edit_license->id); ?>">
                        
                        <p>
                            <label><strong>Mã Key (Sửa):</strong></label><br>
                            <input type="text" name="license_key" value="<?php echo esc_attr($edit_license->license_key); ?>" style="width: 100%; margin-top: 5px;" required class="regular-text">
                        </p>
                        <p>
                            <label><strong>SềEmáy kích hoạt tối đa:</strong></label><br>
                            <input type="number" name="max_devices" value="<?php echo esc_attr($edit_license->max_devices); ?>" min="1" style="width: 100%; margin-top: 5px;" required class="small-text">
                        </p>
                        <p>
                            <label><strong>Ngày hết hạn (Bỏ trống nếu vĩnh viễn):</strong></label><br>
                            <input type="date" name="expires_at" value="<?php echo $edit_license->expires_at ? esc_attr(date('Y-m-d', strtotime($edit_license->expires_at))) : ''; ?>" style="width: 100%; margin-top: 5px;">
                        </p>
                        <p>
                            <label><strong>Trạng thái:</strong></label><br>
                            <select name="status" style="width: 100%; margin-top: 5px;">
                                <option value="active" <?php selected($edit_license->status, 'active'); ?>>ACTIVE (Kích hoạt)</option>
                                <option value="suspended" <?php selected($edit_license->status, 'suspended'); ?>>SUSPENDED (Tạm khóa)</option>
                            </select>
                        </p>
                        <p style="margin-top: 20px;">
                            <input type="submit" class="button button-primary" value="Cập nhật Key">
                            <a href="<?php echo esc_url(admin_url('admin.php?page=pdfpro-licensing')); ?>" class="button button-secondary" style="margin-left: 5px;">Quay lại danh sách</a>
                        </p>
                    </form>
                <?php else : ?>
                    <h2>Nhập thông tin tạo mới</h2>
                    <form method="post">
                        <?php wp_nonce_field('pdfpro_license_action', 'pdfpro_license_nonce'); ?>
                        <input type="hidden" name="pdfpro_action" value="create_license">
                        
                        <p>
                            <label><strong>Mã Key (Bỏ trống để tự động sinh):</strong></label><br>
                            <input type="text" name="license_key" placeholder="Ví dụ: PDFPRO-ABCD-EFGH..." style="width: 100%; margin-top: 5px;" class="regular-text">
                        </p>
                        <p>
                            <label><strong>Thiết bị kích hoạt tối đa:</strong></label><br>
                            <input type="number" name="max_devices" value="1" min="1" style="width: 100%; margin-top: 5px;" class="small-text">
                        </p>
                        <p>
                            <label><strong>Ngày hết hạn (Bỏ trống nếu vĩnh viễn):</strong></label><br>
                            <input type="date" name="expires_at" style="width: 100%; margin-top: 5px;">
                        </p>
                        <p>
                            <label><strong>Trạng thái ban đầu:</strong></label><br>
                            <select name="status" style="width: 100%; margin-top: 5px;">
                                <option value="active">ACTIVE (Kích hoạt ngay)</option>
                                <option value="suspended">SUSPENDED (Tạm khóa)</option>
                            </select>
                        </p>
                        <p style="margin-top: 20px;">
                            <input type="submit" class="button button-primary" value="Tạo Key Bản Quyền">
                            <a href="<?php echo esc_url(admin_url('admin.php?page=pdfpro-licensing')); ?>" class="button button-secondary" style="margin-left: 5px;">Quay lại danh sách</a>
                        </p>
                    </form>
                <?php endif; ?>
            </div>
        </div>
    </div>
    <?php
}
