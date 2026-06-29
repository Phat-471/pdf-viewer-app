<?php
if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

require_once plugin_dir_path( __FILE__ ) . 'class-system-health.php';
require_once plugin_dir_path( __FILE__ ) . 'class-backup-manager.php';

class VN_Privacy_Utilities {

	public function __construct() {
		// Block editor hooks
		add_filter( 'use_block_editor_for_post', [ $this, 'maybe_disable_block_editor' ], 10, 2 );
		add_filter( 'use_widgets_block_editor',  [ $this, 'maybe_disable_widgets_block_editor' ] );

		// Maintenance mode
		add_action( 'template_redirect', [ $this, 'maybe_maintenance_mode' ] );

		// ================================================================
		// UNLIMITED UPLOAD — bypass WordPress & server size limits
		// Mirrors the technique used by All-in-One WP Migration Unlimited
		// ================================================================
		add_filter( 'upload_size_limit',       [ $this, 'unlimited_upload_size' ], PHP_INT_MAX );
		add_action( 'admin_init',              [ $this, 'boost_php_limits' ] );
		add_filter( 'upload_mimes',            [ $this, 'allow_zip_mime' ] );

		// Write .htaccess to backup dir to override Apache limits
		add_action( 'init', [ $this, 'write_backup_htaccess' ] );

		// Backup AJAX
		$bm = new VN_Privacy_Backup_Manager();
		add_action( 'wp_ajax_vn_privacy_backup_init',          [ $bm, 'ajax_backup_init' ] );
		add_action( 'wp_ajax_vn_privacy_backup_db',            [ $bm, 'ajax_backup_db' ] );
		add_action( 'wp_ajax_vn_privacy_backup_files',         [ $bm, 'ajax_backup_files' ] );
		add_action( 'wp_ajax_vn_privacy_backup_finish',        [ $bm, 'ajax_backup_finish' ] );
		add_action( 'wp_ajax_vn_privacy_delete_backup',        [ $bm, 'ajax_delete_backup' ] );
		add_action( 'wp_ajax_vn_privacy_restore_from_server',  [ $bm, 'ajax_restore_from_server' ] );
		add_action( 'wp_ajax_vn_privacy_restore_server_init',   [ $bm, 'ajax_restore_server_init' ] );
		add_action( 'wp_ajax_vn_privacy_chunk_upload',          [ $bm, 'ajax_chunk_upload' ] );
		add_action( 'wp_ajax_vn_privacy_chunk_restore_apply',   [ $bm, 'ajax_chunk_restore_apply' ] );
		add_action( 'wp_ajax_vn_privacy_restore_step_files',    [ $bm, 'ajax_restore_step_files' ] );
		add_action( 'wp_ajax_vn_privacy_restore_step_db',       [ $bm, 'ajax_restore_step_db' ] );
		// New features
		add_action( 'wp_ajax_vn_privacy_verify_backup',         [ $bm, 'ajax_verify_backup' ] );
		add_action( 'wp_ajax_vn_privacy_save_backup_note',      [ $bm, 'ajax_save_backup_note' ] );
		add_action( 'wp_ajax_vn_privacy_run_auto_backup',       [ $this, 'ajax_run_auto_backup_now' ] );
		// WP Cron for auto-backup
		add_action( 'vn_privacy_auto_backup_cron',              [ 'VN_Privacy_Backup_Manager', 'run_auto_backup' ] );

		// Register FTP Cron & Stale Cleanups (FIX #9 & #12)
		VN_Privacy_Backup_Manager::register_ftp_cron();
		if ( ! wp_next_scheduled( 'vn_privacy_cleanup_stale_restores' ) ) {
			wp_schedule_event( time(), 'twicedaily', 'vn_privacy_cleanup_stale_restores' );
		}
		add_action( 'vn_privacy_cleanup_stale_restores', [ 'VN_Privacy_Backup_Manager', 'cleanup_stale_restore_sessions' ] );

		// Tool AJAX actions
		add_action( 'wp_ajax_vn_privacy_flush_transients',   [ $this, 'ajax_flush_transients' ] );
		add_action( 'wp_ajax_vn_privacy_toggle_maintenance', [ $this, 'ajax_toggle_maintenance' ] );
		add_action( 'wp_ajax_vn_privacy_check_permissions',  [ $this, 'ajax_check_permissions' ] );
		add_action( 'wp_ajax_vn_privacy_optimize_htaccess',  [ $this, 'ajax_optimize_htaccess' ] );
		add_action( 'wp_ajax_vn_privacy_cleanup_db',         [ $this, 'ajax_cleanup_db' ] );
		add_action( 'wp_ajax_vn_privacy_reinstall_core',     [ 'VN_Privacy_System_Health', 'ajax_reinstall_wordpress_core' ] );
		add_action( 'wp_ajax_vn_privacy_delete_debug_log',   [ 'VN_Privacy_System_Health', 'ajax_delete_debug_log' ] );
		add_action( 'wp_ajax_vn_privacy_scan_changed_files', [ 'VN_Privacy_System_Health', 'ajax_scan_changed_files' ] );
		add_action( 'wp_ajax_vn_privacy_scan_malware',       [ 'VN_Security_Core', 'ajax_scan_malware' ] );
		add_action( 'wp_ajax_vn_scan_malware',               [ 'VN_Security_Core', 'ajax_scan_malware' ] ); // alias
		add_action( 'wp_ajax_vn_integrity_scan',             [ 'VN_Security_Integrity', 'ajax_scan_static' ] );
	}

	/* ----------------------------------------------------------------
	   UNLIMITED UPLOAD — WordPress filter
	   Returns PHP_INT_MAX so WP never rejects based on size
	---------------------------------------------------------------- */
	public function unlimited_upload_size( $size ) {
		return PHP_INT_MAX;
	}

	/* ----------------------------------------------------------------
	   UNLIMITED UPLOAD — Boost PHP limits at runtime
	   Works on most shared hosts where ini_set() is not disabled
	---------------------------------------------------------------- */
	public function boost_php_limits() {
		if ( ! current_user_can( 'manage_options' ) ) return;

		// Override at runtime — all suppressed in case host forbids it
		@ini_set( 'upload_max_filesize', '0' );
		@ini_set( 'post_max_size',       '0' );
		@ini_set( 'memory_limit',        '-1' );
		@ini_set( 'max_execution_time',  '0' );
		@ini_set( 'max_input_time',      '-1' );
	}

	/* ----------------------------------------------------------------
	   Allow .zip MIME for upload
	---------------------------------------------------------------- */
	public function allow_zip_mime( $mimes ) {
		$mimes['zip'] = 'application/zip';
		$mimes['gz']  = 'application/x-gzip';
		return $mimes;
	}

	/* ----------------------------------------------------------------
	   Write .htaccess to backup directory to override Apache limits.
	   This is exactly how All-in-One WP Migration Unlimited works.
	---------------------------------------------------------------- */
	public function write_backup_htaccess() {
		$upload_dir = wp_upload_dir();
		$backup_dir = $upload_dir['basedir'] . '/vn-privacy-backups';

		if ( ! file_exists( $backup_dir ) ) return;

		$htaccess = $backup_dir . '/.htaccess';
		$content  = "<IfModule mod_php7.c>\n"
			. "  php_value upload_max_filesize 0\n"
			. "  php_value post_max_size 0\n"
			. "  php_value memory_limit -1\n"
			. "  php_value max_execution_time 0\n"
			. "  php_value max_input_time -1\n"
			. "</IfModule>\n"
			. "<IfModule mod_php8.c>\n"
			. "  php_value upload_max_filesize 0\n"
			. "  php_value post_max_size 0\n"
			. "  php_value memory_limit -1\n"
			. "  php_value max_execution_time 0\n"
			. "  php_value max_input_time -1\n"
			. "</IfModule>\n"
			. "<IfModule mod_php.c>\n"
			. "  php_value upload_max_filesize 0\n"
			. "  php_value post_max_size 0\n"
			. "  php_value memory_limit -1\n"
			. "  php_value max_execution_time 0\n"
			. "  php_value max_input_time -1\n"
			. "</IfModule>\n"
			. "Options -Indexes\n"
			. "<FilesMatch \"\\.(zip|sql|gz)$\">\n"
			. "  Order Deny,Allow\n"
			. "  Deny from all\n"
			. "</FilesMatch>\n";

		// Write/update if content changed
		if ( ! file_exists( $htaccess ) || file_get_contents( $htaccess ) !== $content ) {
			@file_put_contents( $htaccess, $content );
		}

		// Also write a php.ini override in the directory
		$phpini = $backup_dir . '/php.ini';
		$ini    = "upload_max_filesize = 0\n"
			. "post_max_size = 0\n"
			. "memory_limit = -1\n"
			. "max_execution_time = 0\n"
			. "max_input_time = -1\n";

		if ( ! file_exists( $phpini ) || file_get_contents( $phpini ) !== $ini ) {
			@file_put_contents( $phpini, $ini );
		}
	}

	/* ----------------------------------------------------------------
	   Classic Editor
	---------------------------------------------------------------- */
	public function maybe_disable_block_editor( $use, $post_type ) {
		return get_option( 'vn_privacy_classic_editor_enabled', 0 ) ? false : $use;
	}

	public function maybe_disable_widgets_block_editor( $use ) {
		return get_option( 'vn_privacy_classic_editor_enabled', 0 ) ? false : $use;
	}

	/* ----------------------------------------------------------------
	   Maintenance Mode
	---------------------------------------------------------------- */
	public function maybe_maintenance_mode() {
		if ( ! get_option( 'vn_privacy_maintenance_mode', 0 ) ) return;
		if ( is_admin() || current_user_can( 'manage_options' ) ) return;

		$msg = get_option( 'vn_privacy_maintenance_msg', 'Website đang được bảo trì. Vui lòng quay lại sau.' );
		status_header( 503 );
		nocache_headers();
		echo '<!DOCTYPE html><html><head><meta charset="utf-8"><title>Bảo trì</title>'
			. '<style>body{font-family:Inter,sans-serif;display:flex;justify-content:center;align-items:center;min-height:100vh;margin:0;background:#0f172a;color:#fff;}'
			. '.box{text-align:center;max-width:460px;padding:40px;}.icon{font-size:4rem;margin-bottom:20px;}'
			. 'h1{font-size:1.5rem;font-weight:800;margin-bottom:12px;}p{color:rgba(255,255,255,.65);line-height:1.6;}</style>'
			. '</head><body><div class="box"><div class="icon">🔧</div>'
			. '<h1>Đang bảo trì</h1><p>' . esc_html( $msg ) . '</p></div></body></html>';
		exit;
	}

	/* ----------------------------------------------------------------
	   AJAX: Flush Transients
	---------------------------------------------------------------- */
	public function ajax_flush_transients() {
		check_ajax_referer( 'vn_tools_nonce', 'nonce' );
		if ( ! current_user_can( 'manage_options' ) ) wp_send_json_error( 'Unauthorized' );

		global $wpdb;
		$count  = $wpdb->query( "DELETE FROM {$wpdb->options} WHERE option_name LIKE '_transient_%'" );
		$count += $wpdb->query( "DELETE FROM {$wpdb->options} WHERE option_name LIKE '_site_transient_%'" );

		wp_send_json_success( "Đã xóa cache tạm thời ({$count} mục)." );
	}

	/* ----------------------------------------------------------------
	   AJAX: Toggle Maintenance Mode
	---------------------------------------------------------------- */
	public function ajax_toggle_maintenance() {
		check_ajax_referer( 'vn_tools_nonce', 'nonce' );
		if ( ! current_user_can( 'manage_options' ) ) wp_send_json_error( 'Unauthorized' );

		$current = get_option( 'vn_privacy_maintenance_mode', 0 );
		$new     = $current ? 0 : 1;
		update_option( 'vn_privacy_maintenance_mode', $new );
		wp_send_json_success( $new ? '🔧 Đã bật chế độ Bảo trì.' : '✅ Đã tắt chế độ Bảo trì.' );
	}

	/* ----------------------------------------------------------------
	   AJAX: Check File Permissions
	---------------------------------------------------------------- */
	public function ajax_check_permissions() {
		check_ajax_referer( 'vn_tools_nonce', 'nonce' );
		if ( ! current_user_can( 'manage_options' ) ) wp_send_json_error( 'Unauthorized' );

		$paths = [
			ABSPATH                    => '755',
			ABSPATH . 'wp-includes'    => '755',
			ABSPATH . 'wp-admin'       => '755',
			WP_CONTENT_DIR             => '755',
			WP_CONTENT_DIR . '/themes' => '755',
			WP_CONTENT_DIR . '/plugins'=> '755',
			wp_upload_dir()['basedir'] => '755',
		];

		$lines = [];
		foreach ( $paths as $path => $recommended ) {
			if ( ! file_exists( $path ) ) continue;
			$perms   = decoct( fileperms( $path ) & 0777 );
			$ok      = $perms === $recommended;
			$lines[] = ( $ok ? '✅' : '⚠️' ) . " $perms (khuyến nghị: $recommended) — " . basename( $path );
		}

		wp_send_json_success( implode( "\n", $lines ) );
	}

	/* ----------------------------------------------------------------
	   AJAX: Generate optimized .htaccess
	---------------------------------------------------------------- */
	public function ajax_optimize_htaccess() {
		check_ajax_referer( 'vn_tools_nonce', 'nonce' );
		if ( ! current_user_can( 'manage_options' ) ) wp_send_json_error( 'Unauthorized' );

		if ( ! function_exists( 'save_mod_rewrite_rules' ) ) {
			require_once ABSPATH . 'wp-admin/includes/misc.php';
		}
		save_mod_rewrite_rules();
		wp_send_json_success( 'Đã tạo lại .htaccess với cấu hình tối ưu cho WordPress.' );
	}

	/* ----------------------------------------------------------------
	   AJAX: Database Cleanup
	---------------------------------------------------------------- */
	public function ajax_cleanup_db() {
		check_ajax_referer( 'vn_tools_nonce', 'nonce' );
		if ( ! current_user_can( 'manage_options' ) ) wp_send_json_error( 'Unauthorized' );

		VN_Privacy_System_Health::cleanup_database();
		wp_send_json_success( 'Đã dọn dẹp & tối ưu hóa Cơ sở dữ liệu thành công.' );
	}

	/* ----------------------------------------------------------------
	   NEW: Run auto-backup manually (AJAX) + Save auto-backup settings
	---------------------------------------------------------------- */
	public function ajax_run_auto_backup_now() {
		check_ajax_referer( 'vn_backup_nonce', 'nonce' );
		if ( ! current_user_can( 'manage_options' ) ) wp_send_json_error( 'Unauthorized' );

		@set_time_limit( 0 );
		VN_Privacy_Backup_Manager::run_auto_backup();
		$last = get_option( 'vn_autobackup_last_run', [] );
		wp_send_json_success( [
			'message' => 'Sao lưu tự động hoàn tất!',
			'file'    => $last['file'] ?? '',
			'time'    => $last['time'] ?? '',
		] );
	}

	/* ----------------------------------------------------------------
	   Register custom cron intervals (weekly, monthly)
	---------------------------------------------------------------- */
	public static function add_cron_intervals( $schedules ) {
		$schedules['weekly'] = [
			'interval' => WEEK_IN_SECONDS,
			'display'  => 'Hàng tuần',
		];
		$schedules['monthly'] = [
			'interval' => 30 * DAY_IN_SECONDS,
			'display'  => 'Hàng tháng',
		];
		return $schedules;
	}
}
