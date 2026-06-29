<?php
/**
 * VN Security Module - Core v3
 * Bootstraps all security sub-modules.
 */
if ( ! defined( 'ABSPATH' ) ) exit;

// Load sub-modules
$_vn_sec_dir = __DIR__;
require_once $_vn_sec_dir . '/class-security-waf.php';
require_once $_vn_sec_dir . '/class-security-2fa.php';
require_once $_vn_sec_dir . '/class-security-integrity.php';

class VN_Security_Core {

	public function __construct() {
		$settings = self::get_settings();

		// Tạo bảng log nếu chưa có
		self::maybe_create_tables();

		// Boot sub-modules
		new VN_Security_WAF();
		new VN_Security_2FA();
		new VN_Security_Integrity();

		// Content protection
		if ( ! empty( $settings['disable_right_click'] ) || ! empty( $settings['disable_text_select'] ) || ! empty( $settings['disable_view_source'] ) ) {
			add_action( 'wp_footer', [ $this, 'inject_protection_script' ], 99 );
		}

		// Anti-spam
		if ( ! empty( $settings['antispam_enabled'] ) ) {
			add_action( 'comment_form',        [ $this, 'inject_honeypot_field' ] );
			add_action( 'pre_comment_on_post', [ $this, 'check_honeypot' ] );
			add_filter( 'pre_comment_approved',[ $this, 'check_keyword_blacklist' ], 10, 2 );
		}

		// Custom login URL
		if ( ! empty( $settings['custom_login_slug'] ) ) {
			add_filter( 'login_url',  [ $this, 'filter_login_url' ], 10, 3 );
			add_filter( 'logout_url', [ $this, 'filter_logout_url' ], 10, 2 );
			add_action( 'login_init', [ $this, 'block_default_login' ] );
		}

		// Login Limiter
		if ( ! empty( $settings['login_limiter_enabled'] ) ) {
			add_filter( 'authenticate',      [ $this, 'check_ip_lockout' ], 1, 3 );
			add_action( 'wp_login_failed',   [ $this, 'on_login_failed' ] );
		}

		// Login Log (luôn log nếu bật)
		if ( ! empty( $settings['log_logins'] ) ) {
			add_action( 'wp_login',         [ $this, 'on_login_success' ], 10, 2 );
			add_action( 'wp_login_failed',  [ $this, 'on_login_failed_log_only' ] );
		}

		// Chặn XML-RPC
		if ( ! empty( $settings['disable_xmlrpc'] ) ) {
			add_filter( 'xmlrpc_enabled', '__return_false' );
		}

		// Ẩn WP version
		if ( ! empty( $settings['hide_wp_version'] ) ) {
			remove_action( 'wp_head', 'wp_generator' );
			add_filter( 'script_loader_src', [ $this, 'remove_wp_version_qs' ], 9999 );
			add_filter( 'style_loader_src',  [ $this, 'remove_wp_version_qs' ], 9999 );
		}

		// IP Access (Blacklist/Whitelist)
		add_action( 'init', [ $this, 'check_ip_access' ] );

		// REST API Protection
		if ( ! empty( $settings['block_rest_api'] ) ) {
			add_filter( 'rest_authentication_errors', [ $this, 'restrict_rest_api_access' ] );
		}

		// Author Query Protection
		if ( ! empty( $settings['block_author_scan'] ) ) {
			add_action( 'template_redirect', [ $this, 'block_author_scanning' ] );
		}
	}

	/* ================================================================
	   Cài đặt
	================================================================ */
	public static function get_settings() {
		$defaults = [
			'custom_login_slug'     => '',
			'antispam_enabled'      => 0,
			'spam_keywords'         => "casino\nviagra\nsex\npoker\nloan",
			'disable_right_click'   => 0,
			'disable_text_select'   => 0,
			'disable_view_source'   => 0,
			'login_limiter_enabled' => 0,
			'max_attempts'          => 5,
			'lockout_minutes'       => 30,
			'log_logins'            => 1,
			'disable_xmlrpc'        => 0,
			'hide_wp_version'       => 0,
			'block_uploads_php'     => 0,
			'blacklist_ips'         => '',
			'whitelist_ips'         => '',
			'block_rest_api'        => 0,
			'block_author_scan'     => 0,
			'ftp_host'              => '',
			'ftp_port'              => '21',
			'ftp_user'              => '',
			'ftp_pass'              => '',
			'ftp_path'              => '/',
			'ftp_enabled'           => 0,
		];
		return wp_parse_args( get_option( 'vn_security_settings', [] ), $defaults );
	}

	public static function save_settings( $data ) {
		$slug     = sanitize_title( $data['custom_login_slug'] ?? '' );
		$reserved = [ 'wp-login', 'wp-admin', 'login', 'admin', 'dashboard', 'wp-signup' ];
		if ( in_array( $slug, $reserved, true ) ) $slug = '';

		$settings = [
			'custom_login_slug'     => $slug,
			'antispam_enabled'      => ! empty( $data['antispam_enabled'] ) ? 1 : 0,
			'spam_keywords'         => sanitize_textarea_field( $data['spam_keywords'] ?? '' ),
			'disable_right_click'   => ! empty( $data['disable_right_click'] ) ? 1 : 0,
			'disable_text_select'   => ! empty( $data['disable_text_select'] ) ? 1 : 0,
			'disable_view_source'   => ! empty( $data['disable_view_source'] ) ? 1 : 0,
			'login_limiter_enabled' => ! empty( $data['login_limiter_enabled'] ) ? 1 : 0,
			'max_attempts'          => max( 1, absint( $data['max_attempts'] ?? 5 ) ),
			'lockout_minutes'       => max( 1, absint( $data['lockout_minutes'] ?? 30 ) ),
			'log_logins'            => ! empty( $data['log_logins'] ) ? 1 : 0,
			'disable_xmlrpc'        => ! empty( $data['disable_xmlrpc'] ) ? 1 : 0,
			'hide_wp_version'       => ! empty( $data['hide_wp_version'] ) ? 1 : 0,
			'block_uploads_php'     => ! empty( $data['block_uploads_php'] ) ? 1 : 0,
			'blacklist_ips'         => sanitize_textarea_field( $data['blacklist_ips'] ?? '' ),
			'whitelist_ips'         => sanitize_textarea_field( $data['whitelist_ips'] ?? '' ),
			'block_rest_api'        => ! empty( $data['block_rest_api'] ) ? 1 : 0,
			'block_author_scan'     => ! empty( $data['block_author_scan'] ) ? 1 : 0,
			'ftp_host'              => sanitize_text_field( $data['ftp_host'] ?? '' ),
			'ftp_port'              => sanitize_text_field( $data['ftp_port'] ?? '21' ),
			'ftp_user'              => sanitize_text_field( $data['ftp_user'] ?? '' ),
			'ftp_pass'              => sanitize_text_field( $data['ftp_pass'] ?? '' ),
			'ftp_path'              => sanitize_text_field( $data['ftp_path'] ?? '/' ),
			'ftp_enabled'           => ! empty( $data['ftp_enabled'] ) ? 1 : 0,
		];
		update_option( 'vn_security_settings', $settings );
		self::manage_uploads_htaccess( $settings['block_uploads_php'] );
		return $settings;
	}

	/* ================================================================
	   Database Table
	================================================================ */
	public static function maybe_create_tables() {
		global $wpdb;
		$table   = $wpdb->prefix . 'vn_login_log';
		$charset = $wpdb->get_charset_collate();

		if ( $wpdb->get_var( "SHOW TABLES LIKE '$table'" ) !== $table ) {
			$wpdb->query( "CREATE TABLE IF NOT EXISTS $table (
				id bigint(20) NOT NULL AUTO_INCREMENT,
				username varchar(100) NOT NULL DEFAULT '',
				ip varchar(45) NOT NULL DEFAULT '',
				status varchar(20) NOT NULL DEFAULT 'failed',
				logged_at datetime NOT NULL,
				PRIMARY KEY (id),
				KEY ip (ip),
				KEY logged_at (logged_at),
				KEY status (status)
			) ENGINE=InnoDB $charset" );
		}

		// Create WAF log table
		VN_Security_WAF::maybe_create_table();
	}

	/* ================================================================
	   Login Limiter
	================================================================ */
	public function check_ip_lockout( $user, $username, $password ) {
		if ( ! $username ) return $user;
		$settings = self::get_settings();
		$max      = (int) $settings['max_attempts'];
		if ( $max <= 0 ) return $user;

		$ip       = self::get_client_ip();
		$attempts = (int) get_transient( 'vn_sec_fail_' . md5( $ip ) );

		if ( $attempts >= $max ) {
			return new WP_Error( 'vn_ip_blocked',
				sprintf( '⛔ IP của bạn đã bị tạm khóa do đăng nhập sai <strong>%d lần</strong>. Vui lòng thử lại sau <strong>%d phút</strong>.', $max, $settings['lockout_minutes'] )
			);
		}
		return $user;
	}

	public function on_login_failed( $username ) {
		$settings     = self::get_settings();
		$ip           = self::get_client_ip();
		$key          = 'vn_sec_fail_' . md5( $ip );
		$attempts     = (int) get_transient( $key ) + 1;
		$lockout_secs = (int) $settings['lockout_minutes'] * 60;
		set_transient( $key, $attempts, $lockout_secs );

		// Log nếu cũng bật log_logins
		if ( ! empty( $settings['log_logins'] ) ) {
			self::log_login( $username, $ip, 'failed' );
		}
	}

	// Chỉ log (khi login_limiter tắt nhưng log bật)
	public function on_login_failed_log_only( $username ) {
		$settings = self::get_settings();
		if ( ! empty( $settings['login_limiter_enabled'] ) ) return; // Tránh log 2 lần
		self::log_login( $username, self::get_client_ip(), 'failed' );
	}

	public function on_login_success( $username, $user ) {
		$ip = self::get_client_ip();
		// Xóa counter failed
		delete_transient( 'vn_sec_fail_' . md5( $ip ) );
		self::log_login( $username, $ip, 'success' );
	}

	public static function log_login( $username, $ip, $status ) {
		global $wpdb;
		$wpdb->insert( $wpdb->prefix . 'vn_login_log', [
			'username'  => sanitize_user( $username ),
			'ip'        => sanitize_text_field( $ip ),
			'status'    => $status,
			'logged_at' => current_time( 'mysql' ),
		], [ '%s', '%s', '%s', '%s' ] );

		// Giữ tối đa 1000 records
		$count = $wpdb->get_var( "SELECT COUNT(*) FROM {$wpdb->prefix}vn_login_log" );
		if ( $count > 1000 ) {
			$wpdb->query( "DELETE FROM {$wpdb->prefix}vn_login_log ORDER BY logged_at ASC LIMIT " . ( $count - 1000 ) );
		}
	}

	public static function get_login_log( $limit = 100, $status = '' ) {
		global $wpdb;
		$where = $status ? $wpdb->prepare( 'WHERE status = %s', $status ) : '';
		return $wpdb->get_results(
			"SELECT * FROM {$wpdb->prefix}vn_login_log $where ORDER BY logged_at DESC LIMIT " . absint( $limit )
		);
	}

	public static function get_login_stats() {
		global $wpdb;
		$table  = $wpdb->prefix . 'vn_login_log';
		$today  = current_time( 'Y-m-d' );
		return [
			'total_failed'   => (int) $wpdb->get_var( "SELECT COUNT(*) FROM $table WHERE status = 'failed'" ),
			'total_success'  => (int) $wpdb->get_var( "SELECT COUNT(*) FROM $table WHERE status = 'success'" ),
			'today_failed'   => (int) $wpdb->get_var( $wpdb->prepare( "SELECT COUNT(*) FROM $table WHERE status='failed' AND DATE(logged_at)=%s", $today ) ),
			'blocked_ips'    => (int) $wpdb->get_var( "SELECT COUNT(DISTINCT ip) FROM $table WHERE status = 'failed' AND logged_at > DATE_SUB(NOW(), INTERVAL 24 HOUR)" ),
		];
	}

	public static function clear_login_log() {
		global $wpdb;
		$wpdb->query( "TRUNCATE {$wpdb->prefix}vn_login_log" );
	}

	public static function get_client_ip() {
		$keys = [ 'HTTP_CF_CONNECTING_IP', 'HTTP_X_FORWARDED_FOR', 'HTTP_X_REAL_IP', 'REMOTE_ADDR' ];
		foreach ( $keys as $key ) {
			if ( ! empty( $_SERVER[ $key ] ) ) {
				$ip = trim( explode( ',', $_SERVER[ $key ] )[0] );
				if ( filter_var( $ip, FILTER_VALIDATE_IP ) ) return $ip;
			}
		}
		return '0.0.0.0';
	}

	/* ================================================================
	   Custom Login URL
	================================================================ */
	public function filter_login_url( $login_url, $redirect, $force_reauth ) {
		$settings = self::get_settings();
		$slug     = $settings['custom_login_slug'];
		if ( ! $slug ) return $login_url;
		$new_url  = home_url( '/' . $slug . '/' );
		return $redirect ? add_query_arg( 'redirect_to', urlencode( $redirect ), $new_url ) : $new_url;
	}

	public function filter_logout_url( $logout_url, $redirect ) {
		$settings = self::get_settings();
		$slug     = $settings['custom_login_slug'];
		if ( ! $slug ) return $logout_url;
		return str_replace( site_url( 'wp-login.php', 'login' ), home_url( '/' . $slug . '/' ), $logout_url );
	}

	public function block_default_login() {
		$settings = self::get_settings();
		$slug     = $settings['custom_login_slug'];
		if ( ! $slug ) return;
		$request  = trim( parse_url( $_SERVER['REQUEST_URI'] ?? '', PHP_URL_PATH ), '/' );
		$allowed  = [ 'logout', 'lostpassword', 'rp', 'resetpass', 'postpass' ];
		if ( in_array( $_REQUEST['action'] ?? '', $allowed, true ) ) return;
		if ( strpos( $request, 'wp-login' ) !== false && $request !== $slug ) {
			wp_redirect( home_url( '/?p=404' ) ); exit;
		}
	}

	public static function get_current_login_url() {
		$settings = self::get_settings();
		$slug     = $settings['custom_login_slug'];
		return $slug ? home_url( '/' . $slug . '/' ) : wp_login_url();
	}

	/* ================================================================
	   Anti-Spam
	================================================================ */
	public function inject_honeypot_field() {
		echo '<p style="display:none!important;visibility:hidden;position:absolute;left:-9999px;">
			<input type="text" name="vn_hp_email" value="" autocomplete="off" tabindex="-1">
		</p>';
	}

	public function check_honeypot( $comment_id ) {
		if ( ! empty( $_POST['vn_hp_email'] ) ) {
			wp_die( 'Bình luận bị từ chối (spam detected).', 'Spam', [ 'response' => 403 ] );
		}
	}

	public function check_keyword_blacklist( $approved, $commentdata ) {
		$settings = self::get_settings();
		if ( empty( $settings['spam_keywords'] ) ) return $approved;
		$keywords = array_filter( array_map( 'trim', explode( "\n", strtolower( $settings['spam_keywords'] ) ) ) );
		$text     = strtolower( $commentdata['comment_content'] . ' ' . $commentdata['comment_author_url'] );
		foreach ( $keywords as $kw ) {
			if ( $kw && strpos( $text, $kw ) !== false ) return 'spam';
		}
		return $approved;
	}

	/* ================================================================
	   Content Protection
	================================================================ */
	public function inject_protection_script() {
		$s  = self::get_settings();
		$rc = ! empty( $s['disable_right_click'] );
		$ts = ! empty( $s['disable_text_select'] );
		$vs = ! empty( $s['disable_view_source'] );
		?>
		<script>
		(function(){
		<?php if($rc): ?>document.addEventListener('contextmenu',function(e){e.preventDefault();return false;});<?php endif; ?>
		<?php if($ts): ?>document.onselectstart=function(){return false;};document.ondragstart=function(){return false;};<?php endif; ?>
		<?php if($vs): ?>document.addEventListener('keydown',function(e){if((e.ctrlKey||e.metaKey)&&(e.key==='u'||e.key==='U'||e.key==='s'||e.key==='S')){e.preventDefault();return false;}if(e.key==='F12'){e.preventDefault();return false;}});<?php endif; ?>
		})();
		</script>
		<?php
	}

	/* ================================================================
	   Ẩn Phiên Bản WordPress
	================================================================ */
	public function remove_wp_version_qs( $src ) {
		if ( strpos( $src, 'ver=' ) ) {
			$src = remove_query_arg( 'ver', $src );
		}
		return $src;
	}

	/* ================================================================
	   Bảo mật thư mục Uploads
	================================================================ */
	public static function manage_uploads_htaccess( $enable ) {
		$uploads = wp_upload_dir();
		$htaccess_file = $uploads['basedir'] . '/.htaccess';

		if ( $enable ) {
			$content = "# VN Security - Block PHP execution\n";
			$content .= "<FilesMatch \"\\.(php|php\\d*|phtml)$\">\n";
			$content .= "    Order Deny,Allow\n";
			$content .= "    Deny from all\n";
			$content .= "</FilesMatch>\n";
			
			if ( is_dir( $uploads['basedir'] ) && is_writable( $uploads['basedir'] ) ) {
				@file_put_contents( $htaccess_file, $content );
			}
		} else {
			if ( file_exists( $htaccess_file ) ) {
				$content = @file_get_contents( $htaccess_file );
				if ( strpos( $content, 'VN Security - Block PHP' ) !== false ) {
					@unlink( $htaccess_file );
				}
			}
		}
	}

	/* ================================================================
	   PHP Debug Log Clearing
	================================================================ */
	public static function clear_debug_log() {
		$log_file = WP_CONTENT_DIR . '/debug.log';
		if ( file_exists( $log_file ) && is_writable( $log_file ) ) {
			@file_put_contents( $log_file, '' );
			return true;
		}
		return false;
	}

	/* ================================================================
	   Get Recently Modified Files (PHP, JS, CSS, htaccess in wp-content)
	================================================================ */
	public static function get_recently_modified_files( $limit = 100 ) {
		$dir = WP_CONTENT_DIR;
		$files = [];
		if ( ! is_dir( $dir ) ) return [];

		try {
			$iterator = new RecursiveIteratorIterator(
				new RecursiveDirectoryIterator( $dir, RecursiveDirectoryIterator::SKIP_DOTS ),
				RecursiveIteratorIterator::SELF_FIRST
			);
			
			foreach ( $iterator as $fileInfo ) {
				if ( $fileInfo->isFile() ) {
					$pathname = $fileInfo->getPathname();
					// Skip cache, logs, and backups directory to avoid clutter
					if ( preg_match( '#[/\\\\](cache|et-cache|minify|wp-cf7-files|vn-privacy-backups|upgrade)[/\\\\]#', $pathname ) ) {
						continue;
					}
					// Only track PHP, JS, CSS, HTACCESS files which are security sensitive
					$ext = strtolower( $fileInfo->getExtension() );
					if ( ! in_array( $ext, [ 'php', 'js', 'css', 'htaccess' ], true ) ) {
						continue;
					}
					
					$mtime = $fileInfo->getMTime();
					$files[] = [
						'path'  => str_replace( ABSPATH, '', $pathname ),
						'size'  => $fileInfo->getSize(),
						'mtime' => $mtime,
					];
				}
			}
		} catch ( Exception $e ) {
			return [];
		}

		// Sort by mtime descending
		usort( $files, function ( $a, $b ) {
			return $b['mtime'] - $a['mtime'];
		} );

		return array_slice( $files, 0, $limit );
	}

	/* ================================================================
	   IP Access Control (Blacklist/Whitelist)
	================================================================ */
	public function check_ip_access() {
		$settings = self::get_settings();
		$ip       = self::get_client_ip();

		// 1. Check Whitelist first
		$whitelist = array_filter( array_map( 'trim', explode( "\n", $settings['whitelist_ips'] ) ) );
		foreach ( $whitelist as $w_ip ) {
			if ( self::ip_matches( $ip, $w_ip ) ) {
				return;
			}
		}

		// 2. Check Blacklist
		$blacklist = array_filter( array_map( 'trim', explode( "\n", $settings['blacklist_ips'] ) ) );
		foreach ( $blacklist as $b_ip ) {
			if ( self::ip_matches( $ip, $b_ip ) ) {
				wp_die( '<h1>403 Forbidden</h1><p>IP của bạn đã bị quản trị viên chặn truy cập hệ thống.</p>', 'Forbidden', [ 'response' => 403 ] );
			}
		}
	}

	private static function ip_matches( $ip, $pattern ) {
		if ( $ip === $pattern ) return true;
		if ( strpos( $pattern, '*' ) !== false ) {
			$pattern_regex = '/^' . str_replace( [ '.', '*' ], [ '\\.', '\\d+' ], $pattern ) . '$/';
			return (bool) preg_match( $pattern_regex, $ip );
		}
		return false;
	}

	/* ================================================================
	   REST API Access Protection (Block anonymous users from listing users)
	================================================================ */
	public function restrict_rest_api_access( $errors ) {
		if ( ! empty( $errors ) ) return $errors;

		$route = $GLOBALS['wp']->query_vars['rest_route'] ?? '';
		if ( empty( $route ) && isset( $_SERVER['REQUEST_URI'] ) ) {
			$route = parse_url( $_SERVER['REQUEST_URI'], PHP_URL_PATH );
		}

		if ( strpos( $route, '/wp/v2/users' ) !== false ) {
			if ( ! is_user_logged_in() ) {
				return new WP_Error(
					'rest_forbidden_anonymous',
					'Bạn không có quyền truy cập thông tin thành viên (REST API).',
					[ 'status' => 401 ]
				);
			}
		}
		return $errors;
	}

	/* ================================================================
	   Block Author Scans (?author=1)
	================================================================ */
	public function block_author_scanning() {
		if ( ! is_admin() && isset( $_GET['author'] ) ) {
			wp_die( '<h1>403 Forbidden</h1><p>Dò tìm thông tin tác giả đã bị vô hiệu hóa vì lý do bảo mật.</p>', 'Forbidden', [ 'response' => 403 ] );
		}
	}

	/* ================================================================
	   AJAX Malware Scanner (Plugins & Themes)
	================================================================ */
	public static function ajax_scan_malware() {
		$nonce = $_POST['nonce'] ?? $_GET['nonce'] ?? '';
		if ( ! wp_verify_nonce( $nonce, 'vn_save_security' ) ) {
			wp_send_json_error( 'Lỗi bảo mật (Invalid Nonce).' );
		}
		if ( ! current_user_can( 'manage_options' ) ) wp_send_json_error( 'Unauthorized' );

		@set_time_limit( 300 );
		@ini_set( 'memory_limit', '512M' );

		$dir = WP_CONTENT_DIR;
		$results = [];
		
		$signatures = [
			'eval(base64_decode' => 'Mã hóa eval(base64_decode) - Hacker thường dùng để giấu mã độc.',
			'eval(gzinflate'     => 'Mã hóa eval(gzinflate) - Thường là backdoor nén.',
			'eval(str_rot13'     => 'Mã hóa eval(str_rot13) - Che giấu mã độc cơ bản.',
			'shell_exec('        => 'Sử dụng lệnh hệ thống shell_exec() - Cho phép chạy lệnh server.',
			'system('            => 'Sử dụng lệnh hệ thống system() - Cho phép chạy lệnh server.',
			'passthru('          => 'Sử dụng lệnh hệ thống passthru() - Cho phép chạy lệnh server.',
			'exec('              => 'Sử dụng lệnh hệ thống exec() - Cho phép chạy lệnh server.',
			'popen('             => 'Sử dụng lệnh hệ thống popen() - Cho phép mở tiến trình hệ thống.',
			'proc_open('         => 'Sử dụng lệnh hệ thống proc_open() - Mở tiến trình nâng cao.',
			'$_POST['            => 'Nhận lệnh POST trực tiếp - Có thể là Webshell nhận lệnh.',
			'$_GET['             => 'Nhận lệnh GET trực tiếp - Có thể là Webshell nhận lệnh.',
			'base64_decode($_POST' => 'Giải mã dữ liệu POST - Che giấu payload gửi lên.',
			'base64_decode($_GET'  => 'Giải mã dữ liệu GET - Che giấu payload gửi lên.',
		];

		try {
			$iterator = new RecursiveIteratorIterator(
				new RecursiveDirectoryIterator( $dir, RecursiveDirectoryIterator::SKIP_DOTS ),
				RecursiveIteratorIterator::SELF_FIRST
			);

			foreach ( $iterator as $fileInfo ) {
				if ( $fileInfo->isFile() ) {
					$pathname = $fileInfo->getPathname();
					
					if ( ! preg_match( '#[/\\\\](plugins|themes)[/\\\\]#', $pathname ) ) {
						continue;
					}
					
					if ( strtolower( $fileInfo->getExtension() ) !== 'php' ) {
						continue;
					}

					if ( $fileInfo->getSize() > 1500000 ) {
						continue;
					}

					$content = @file_get_contents( $pathname );
					if ( empty( $content ) ) {
						continue;
					}

					foreach ( $signatures as $sig => $desc ) {
						if ( strpos( $content, $sig ) !== false ) {
							if ( in_array( $sig, [ 'shell_exec(', 'system(', 'passthru(', 'exec(', 'popen(', 'proc_open(' ], true ) ) {
								if ( ! preg_match( '#(shell_exec|system|passthru|exec|popen|proc_open)\s*\(\s*\$(_(POST|GET|REQUEST|COOKIE)|[a-zA-Z0-9_]+)#i', $content ) ) {
									continue;
								}
							}
							
							if ( in_array( $sig, [ '$_POST[', '$_GET[' ], true ) ) {
								if ( ! preg_match( '#(eval|assert|include|require|include_once|require_once)\s*\(\s*\$(POST|GET)#i', $content ) ) {
									continue;
								}
							}

							$results[] = [
								'file'      => str_replace( ABSPATH, '', $pathname ),
								'signature' => $sig,
								'desc'      => $desc,
								'mtime'     => $fileInfo->getMTime(),
								'size'      => $fileInfo->getSize(),
							];
							break;
						}
					}
				}
			}
		} catch ( Exception $e ) {
			wp_send_json_error( $e->getMessage() );
		}

		wp_send_json_success( $results );
	}
}
