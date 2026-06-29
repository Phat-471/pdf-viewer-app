<?php
/**
 * VN Security - Web Application Firewall (WAF)
 * Blocks SQLi, XSS, LFI/RFI, and malicious bots automatically.
 */
if ( ! defined( 'ABSPATH' ) ) exit;

class VN_Security_WAF {

	/** Cached settings */
	private static $settings = null;

	public function __construct() {
		$s = self::get_waf_settings();
		if ( empty( $s['waf_enabled'] ) ) return;

		// Run WAF before WordPress loads (priority 1 on init)
		add_action( 'init', [ $this, 'run' ], 1 );
	}

	/* ================================================================
	   Settings
	================================================================ */
	public static function get_waf_settings() {
		if ( self::$settings !== null ) return self::$settings;
		$defaults = [
			'waf_enabled'       => 0,
			'waf_block_sqli'    => 1,
			'waf_block_xss'     => 1,
			'waf_block_lfi'     => 1,
			'waf_block_bots'    => 1,
			'waf_whitelist_ips' => '',
			'waf_log_enabled'   => 1,
		];
		self::$settings = wp_parse_args( get_option( 'vn_waf_settings', [] ), $defaults );
		return self::$settings;
	}

	public static function save_waf_settings( $data ) {
		$settings = [
			'waf_enabled'       => ! empty( $data['waf_enabled'] )    ? 1 : 0,
			'waf_block_sqli'    => ! empty( $data['waf_block_sqli'] ) ? 1 : 0,
			'waf_block_xss'     => ! empty( $data['waf_block_xss'] )  ? 1 : 0,
			'waf_block_lfi'     => ! empty( $data['waf_block_lfi'] )  ? 1 : 0,
			'waf_block_bots'    => ! empty( $data['waf_block_bots'] ) ? 1 : 0,
			'waf_whitelist_ips' => sanitize_textarea_field( $data['waf_whitelist_ips'] ?? '' ),
			'waf_log_enabled'   => ! empty( $data['waf_log_enabled'] ) ? 1 : 0,
		];
		update_option( 'vn_waf_settings', $settings );
		self::$settings = $settings;
		return $settings;
	}

	/* ================================================================
	   DB Table for WAF Logs
	================================================================ */
	public static function maybe_create_table() {
		global $wpdb;
		$table   = $wpdb->prefix . 'vn_waf_log';
		$charset = $wpdb->get_charset_collate();

		if ( $wpdb->get_var( "SHOW TABLES LIKE '$table'" ) !== $table ) {
			$wpdb->query( "CREATE TABLE IF NOT EXISTS $table (
				id bigint(20) NOT NULL AUTO_INCREMENT,
				ip varchar(45) NOT NULL DEFAULT '',
				type varchar(50) NOT NULL DEFAULT '',
				uri text NOT NULL,
				payload text NOT NULL,
				blocked_at datetime NOT NULL,
				PRIMARY KEY (id),
				KEY ip (ip),
				KEY type (type),
				KEY blocked_at (blocked_at)
			) ENGINE=InnoDB $charset" );
		}
	}

	/* ================================================================
	   Main WAF Runner
	================================================================ */
	public function run() {
		// Skip AJAX and admin (only protect frontend & REST)
		if ( defined( 'DOING_CRON' ) && DOING_CRON ) return;
		if ( is_admin() && ! wp_doing_ajax() ) return;

		$s  = self::get_waf_settings();
		$ip = VN_Security_Core::get_client_ip();

		// Whitelist check
		$whitelist = array_filter( array_map( 'trim', explode( "\n", $s['waf_whitelist_ips'] ) ) );
		foreach ( $whitelist as $w ) {
			if ( self::ip_matches( $ip, $w ) ) return;
		}

		// Collect all input to inspect
		$uri     = $_SERVER['REQUEST_URI']       ?? '';
		$ua      = $_SERVER['HTTP_USER_AGENT']   ?? '';
		$referer = $_SERVER['HTTP_REFERER']       ?? '';
		$inputs  = array_merge(
			array_values( $_GET  ),
			array_values( $_POST ),
			[ $uri, $referer ]
		);

		$threat = null;

		// 1. Block Bad Bots
		if ( ! empty( $s['waf_block_bots'] ) ) {
			$threat = $this->detect_bad_bot( $ua );
		}

		// 2. SQLi Detection
		if ( ! $threat && ! empty( $s['waf_block_sqli'] ) ) {
			foreach ( $inputs as $val ) {
				$threat = $this->detect_sqli( $val );
				if ( $threat ) break;
			}
		}

		// 3. XSS Detection
		if ( ! $threat && ! empty( $s['waf_block_xss'] ) ) {
			foreach ( $inputs as $val ) {
				$threat = $this->detect_xss( $val );
				if ( $threat ) break;
			}
		}

		// 4. LFI/RFI Detection
		if ( ! $threat && ! empty( $s['waf_block_lfi'] ) ) {
			foreach ( $inputs as $val ) {
				$threat = $this->detect_lfi( $val );
				if ( $threat ) break;
			}
		}

		if ( $threat ) {
			// Log it
			if ( ! empty( $s['waf_log_enabled'] ) ) {
				self::log_threat( $ip, $threat['type'], $uri, $threat['payload'] );
			}
			// Block with 403
			status_header( 403 );
			nocache_headers();
			wp_die(
				'<h1>403 – Forbidden</h1><p>Yêu cầu của bạn đã bị chặn bởi tường lửa bảo mật (WAF).<br><small>Mã: ' . esc_html( $threat['type'] ) . '</small></p>',
				'403 Forbidden',
				[ 'response' => 403 ]
			);
		}
	}

	/* ================================================================
	   Threat Detection Methods
	================================================================ */
	private function detect_sqli( $val ) {
		if ( empty( $val ) || ! is_string( $val ) ) return false;
		$val = urldecode( $val );

		$patterns = [
			// Classic union-based
			'/(\bunion\b.{0,20}\bselect\b)/i',
			// Error-based / stacked queries
			'/(\bselect\b.{0,40}\bfrom\b)/i',
			// Boolean-based blind
			'/\b(and|or)\b\s+\d+\s*[=<>]\s*\d+/i',
			// Comment sequences used in injection
			'/(--|#|\/\*|;\s*drop\s+table|;\s*insert\s+into)/i',
			// Hex encoding attempt
			'/0x[0-9a-f]{4,}/i',
			// sleep/benchmark DOS
			'/\b(sleep|benchmark|waitfor\s+delay)\s*\(/i',
		];

		foreach ( $patterns as $p ) {
			if ( preg_match( $p, $val ) ) {
				return [ 'type' => 'SQLi', 'payload' => substr( $val, 0, 200 ) ];
			}
		}
		return false;
	}

	private function detect_xss( $val ) {
		if ( empty( $val ) || ! is_string( $val ) ) return false;
		$val = urldecode( html_entity_decode( $val ) );

		$patterns = [
			'/<\s*script[^>]*>/i',
			'/javascript\s*:/i',
			'/on(load|error|click|mouseover|focus|blur|change|submit|keyup|keydown)\s*=/i',
			'/<\s*(img|iframe|object|embed|svg)[^>]+src\s*=/i',
			'/expression\s*\(/i',
			'/vbscript\s*:/i',
			'/<\s*\/?\s*(script|applet|object|embed|frame|iframe|meta|link)/i',
		];

		foreach ( $patterns as $p ) {
			if ( preg_match( $p, $val ) ) {
				return [ 'type' => 'XSS', 'payload' => substr( $val, 0, 200 ) ];
			}
		}
		return false;
	}

	private function detect_lfi( $val ) {
		if ( empty( $val ) || ! is_string( $val ) ) return false;
		$val = urldecode( $val );

		$patterns = [
			'/\.\.\/\.\.\/|\.\.\\\\\.\.\\\\/',      // path traversal
			'/etc\/(passwd|shadow|hosts|group)/i',  // sensitive Linux files
			'/(php|expect|zip|phar|data|glob)\:\/\//i', // PHP wrappers
			'/\/(proc|sys|dev)\//i',                // system dirs
		];

		foreach ( $patterns as $p ) {
			if ( preg_match( $p, $val ) ) {
				return [ 'type' => 'LFI/RFI', 'payload' => substr( $val, 0, 200 ) ];
			}
		}
		return false;
	}

	private function detect_bad_bot( $ua ) {
		if ( empty( $ua ) ) return false;

		// Known malicious/scanner bots signatures
		$bad_bots = [
			'sqlmap', 'nikto', 'nmap', 'masscan', 'zgrab', 'dirbuster', 'gobuster',
			'wfuzz', 'w3af', 'burpsuite', 'acunetix', 'openvas', 'metasploit',
			'python-requests/2', 'go-http-client', 'curl/7', 'libwww-perl',
			'scrapy', 'mechanize', 'wget/', 'lwp-trivial', 'java/1.',
			'masscan', 'zmap', 'xsser', 'havij', 'sqlninja',
		];

		$ua_lower = strtolower( $ua );
		foreach ( $bad_bots as $bot ) {
			if ( strpos( $ua_lower, $bot ) !== false ) {
				return [ 'type' => 'BadBot', 'payload' => substr( $ua, 0, 200 ) ];
			}
		}

		// Extremely short or empty UA (often scanners)
		if ( strlen( $ua ) < 10 ) {
			return [ 'type' => 'BadBot', 'payload' => '(short UA): ' . $ua ];
		}

		return false;
	}

	/* ================================================================
	   Logging
	================================================================ */
	public static function log_threat( $ip, $type, $uri, $payload ) {
		global $wpdb;
		$table = $wpdb->prefix . 'vn_waf_log';

		// Silently fail if table doesn't exist yet
		if ( $wpdb->get_var( "SHOW TABLES LIKE '$table'" ) !== $table ) return;

		$wpdb->insert( $table, [
			'ip'         => sanitize_text_field( $ip ),
			'type'       => sanitize_text_field( $type ),
			'uri'        => esc_url_raw( $uri ),
			'payload'    => sanitize_textarea_field( substr( $payload, 0, 1000 ) ),
			'blocked_at' => current_time( 'mysql' ),
		], [ '%s', '%s', '%s', '%s', '%s' ] );

		// Keep latest 2000 records
		$count = (int) $wpdb->get_var( "SELECT COUNT(*) FROM $table" );
		if ( $count > 2000 ) {
			$wpdb->query( "DELETE FROM $table ORDER BY blocked_at ASC LIMIT " . ( $count - 2000 ) );
		}
	}

	public static function get_waf_logs( $limit = 100, $type = '' ) {
		global $wpdb;
		$table = $wpdb->prefix . 'vn_waf_log';
		if ( $wpdb->get_var( "SHOW TABLES LIKE '$table'" ) !== $table ) return [];

		$where = $type ? $wpdb->prepare( 'WHERE type = %s', $type ) : '';
		return $wpdb->get_results(
			"SELECT * FROM $table $where ORDER BY blocked_at DESC LIMIT " . absint( $limit )
		);
	}

	public static function get_waf_stats() {
		global $wpdb;
		$table = $wpdb->prefix . 'vn_waf_log';
		if ( $wpdb->get_var( "SHOW TABLES LIKE '$table'" ) !== $table ) {
			return [ 'total' => 0, 'today' => 0, 'sqli' => 0, 'xss' => 0, 'bots' => 0 ];
		}
		$today = current_time( 'Y-m-d' );
		return [
			'total' => (int) $wpdb->get_var( "SELECT COUNT(*) FROM $table" ),
			'today' => (int) $wpdb->get_var( $wpdb->prepare( "SELECT COUNT(*) FROM $table WHERE DATE(blocked_at)=%s", $today ) ),
			'sqli'  => (int) $wpdb->get_var( "SELECT COUNT(*) FROM $table WHERE type='SQLi'" ),
			'xss'   => (int) $wpdb->get_var( "SELECT COUNT(*) FROM $table WHERE type='XSS'" ),
			'bots'  => (int) $wpdb->get_var( "SELECT COUNT(*) FROM $table WHERE type='BadBot'" ),
		];
	}

	public static function clear_waf_logs() {
		global $wpdb;
		$wpdb->query( "TRUNCATE {$wpdb->prefix}vn_waf_log" );
	}

	/* ================================================================
	   Helper
	================================================================ */
	private static function ip_matches( $ip, $pattern ) {
		if ( $ip === $pattern ) return true;
		if ( strpos( $pattern, '*' ) !== false ) {
			$regex = '/^' . str_replace( [ '.', '*' ], [ '\\.', '\\d+' ], $pattern ) . '$/';
			return (bool) preg_match( $regex, $ip );
		}
		return false;
	}
}
