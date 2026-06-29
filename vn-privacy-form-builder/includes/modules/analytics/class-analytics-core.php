<?php
/**
 * VN Analytics Module - Core
 * Page View log, top pages, today/week/month stats, automatic retention cleanup
 */
if ( ! defined( 'ABSPATH' ) ) exit;

class VN_Analytics_Core {

	public function __construct() {
		// Tạo bảng db
		self::maybe_create_tables();

		$settings = self::get_settings();

		if ( ! empty( $settings['analytics_enabled'] ) ) {
			add_action( 'template_redirect', [ $this, 'log_page_view' ] );
		}
	}

	/* ================================================================
	   Cài đặt
	================================================================ */
	public static function get_settings() {
		$defaults = [
			'analytics_enabled' => 1,
			'exclude_logged_in' => 0,
			'exclude_bots'      => 1,
			'retention_days'    => 30,
		];
		return wp_parse_args( get_option( 'vn_analytics_settings', [] ), $defaults );
	}

	public static function save_settings( $data ) {
		$settings = [
			'analytics_enabled' => ! empty( $data['analytics_enabled'] ) ? 1 : 0,
			'exclude_logged_in' => ! empty( $data['exclude_logged_in'] ) ? 1 : 0,
			'exclude_bots'      => ! empty( $data['exclude_bots'] ) ? 1 : 0,
			'retention_days'    => max( 1, min( 365, absint( $data['retention_days'] ?? 30 ) ) ),
		];
		update_option( 'vn_analytics_settings', $settings );
		return $settings;
	}

	/* ================================================================
	   Tạo bảng Database
	================================================================ */
	public static function maybe_create_tables() {
		global $wpdb;
		$table   = $wpdb->prefix . 'vn_page_views';
		$charset = $wpdb->get_charset_collate();

		if ( $wpdb->get_var( "SHOW TABLES LIKE '$table'" ) !== $table ) {
			$wpdb->query( "CREATE TABLE IF NOT EXISTS $table (
				id bigint(20) NOT NULL AUTO_INCREMENT,
				post_id bigint(20) NOT NULL DEFAULT 0,
				url text NOT NULL,
				ip varchar(45) NOT NULL DEFAULT '',
				user_agent varchar(255) NOT NULL DEFAULT '',
				referrer text NOT NULL,
				viewed_at datetime NOT NULL,
				PRIMARY KEY (id),
				KEY post_id (post_id),
				KEY ip (ip),
				KEY viewed_at (viewed_at)
			) ENGINE=InnoDB $charset" );
		}
	}

	/* ================================================================
	   Ghi nhận lượt xem
	================================================================ */
	public function log_page_view() {
		// Chỉ log ngoài frontend, không log feed, admin, ajax, cron
		if ( is_admin() || wp_doing_ajax() || wp_doing_cron() || is_feed() || is_trackback() || is_embed() ) {
			return;
		}

		$settings = self::get_settings();

		// Kiểm tra loại trừ người dùng đã đăng nhập
		if ( ! empty( $settings['exclude_logged_in'] ) && is_user_logged_in() ) {
			return;
		}

		$user_agent = isset( $_SERVER['HTTP_USER_AGENT'] ) ? sanitize_text_field( $_SERVER['HTTP_USER_AGENT'] ) : '';

		// Kiểm tra loại trừ bot
		if ( ! empty( $settings['exclude_bots'] ) && self::is_bot( $user_agent ) ) {
			return;
		}

		global $wpdb;
		$table = $wpdb->prefix . 'vn_page_views';

		$post_id  = is_singular() ? get_the_ID() : 0;
		$url      = self::get_current_url();
		$ip       = self::get_client_ip();
		$referrer = isset( $_SERVER['HTTP_REFERER'] ) ? esc_url_raw( $_SERVER['HTTP_REFERER'] ) : '';

		// Tránh user_agent quá dài
		$user_agent_short = substr( $user_agent, 0, 250 );

		$wpdb->insert(
			$table,
			[
				'post_id'    => $post_id,
				'url'        => $url,
				'ip'         => $ip,
				'user_agent' => $user_agent_short,
				'referrer'   => $referrer,
				'viewed_at'  => current_time( 'mysql' ),
			],
			[ '%d', '%s', '%s', '%s', '%s', '%s' ]
		);

		// Dọn dẹp định kỳ (xác suất 1% để tránh tải server)
		if ( wp_rand( 1, 100 ) === 50 ) {
			self::clean_old_logs();
		}
	}

	/* ================================================================
	   Xóa Log cũ theo retention_days
	================================================================ */
	public static function clean_old_logs() {
		global $wpdb;
		$table    = $wpdb->prefix . 'vn_page_views';
		$settings = self::get_settings();
		$days     = (int) $settings['retention_days'];

		if ( $days > 0 ) {
			$wpdb->query( $wpdb->prepare(
				"DELETE FROM $table WHERE viewed_at < DATE_SUB(%s, INTERVAL %d DAY)",
				current_time( 'mysql' ),
				$days
			) );
		}
	}

	/* ================================================================
	   Trợ giúp & Phân tích Thống kê
	================================================================ */
	public static function get_client_ip() {
		$keys = [ 'HTTP_CLIENT_IP', 'HTTP_X_FORWARDED_FOR', 'HTTP_X_FORWARDED', 'HTTP_FORWARDED_FOR', 'HTTP_FORWARDED', 'REMOTE_ADDR' ];
		foreach ( $keys as $key ) {
			if ( ! empty( $_SERVER[ $key ] ) ) {
				$ip_list = explode( ',', $_SERVER[ $key ] );
				$ip      = trim( reset( $ip_list ) );
				if ( filter_var( $ip, FILTER_VALIDATE_IP ) ) {
					return $ip;
				}
			}
		}
		return '0.0.0.0';
	}

	public static function get_current_url() {
		$schema = is_ssl() ? 'https://' : 'http://';
		$host   = isset( $_SERVER['HTTP_HOST'] ) ? sanitize_text_field( $_SERVER['HTTP_HOST'] ) : '';
		$uri    = isset( $_SERVER['REQUEST_URI'] ) ? sanitize_text_field( $_SERVER['REQUEST_URI'] ) : '';
		return esc_url_raw( $schema . $host . $uri );
	}

	public static function is_bot( $ua ) {
		if ( empty( $ua ) ) return true;
		$bot_keywords = [
			'bot', 'crawl', 'spider', 'slurp', 'yahoo', 'facebookexternalhit',
			'mediapartners-google', 'baiduspider', 'yandex', 'pingdom',
			'screaming', 'semrush', 'ahrefs', 'mj12bot', 'dotbot'
		];
		$ua = strtolower( $ua );
		foreach ( $bot_keywords as $keyword ) {
			if ( strpos( $ua, $keyword ) !== false ) {
				return true;
			}
		}
		return false;
	}

	public static function get_stats() {
		global $wpdb;
		$table = $wpdb->prefix . 'vn_page_views';

		// Đảm bảo bảng tồn tại
		self::maybe_create_tables();

		$now = current_time( 'mysql' );

		// Hôm nay
		$today_pv = (int) $wpdb->get_var( $wpdb->prepare(
			"SELECT COUNT(*) FROM $table WHERE DATE(viewed_at) = DATE(%s)",
			$now
		) );
		$today_uv = (int) $wpdb->get_var( $wpdb->prepare(
			"SELECT COUNT(DISTINCT ip) FROM $table WHERE DATE(viewed_at) = DATE(%s)",
			$now
		) );

		// Tuần này
		$week_pv = (int) $wpdb->get_var( $wpdb->prepare(
			"SELECT COUNT(*) FROM $table WHERE viewed_at >= DATE_SUB(%s, INTERVAL 7 DAY)",
			$now
		) );
		$week_uv = (int) $wpdb->get_var( $wpdb->prepare(
			"SELECT COUNT(DISTINCT ip) FROM $table WHERE viewed_at >= DATE_SUB(%s, INTERVAL 7 DAY)",
			$now
		) );

		// Tháng này
		$month_pv = (int) $wpdb->get_var( $wpdb->prepare(
			"SELECT COUNT(*) FROM $table WHERE viewed_at >= DATE_SUB(%s, INTERVAL 30 DAY)",
			$now
		) );
		$month_uv = (int) $wpdb->get_var( $wpdb->prepare(
			"SELECT COUNT(DISTINCT ip) FROM $table WHERE viewed_at >= DATE_SUB(%s, INTERVAL 30 DAY)",
			$now
		) );

		// Tổng lượt xem
		$total_pv = (int) $wpdb->get_var( "SELECT COUNT(*) FROM $table" );

		return [
			'today_pv' => $today_pv,
			'today_uv' => $today_uv,
			'week_pv'  => $week_pv,
			'week_uv'  => $week_uv,
			'month_pv' => $month_pv,
			'month_uv' => $month_uv,
			'total_pv' => $total_pv,
		];
	}

	public static function get_top_pages( $limit = 10 ) {
		global $wpdb;
		$table = $wpdb->prefix . 'vn_page_views';

		self::maybe_create_tables();

		return $wpdb->get_results( $wpdb->prepare(
			"SELECT post_id, url, COUNT(*) as views, COUNT(DISTINCT ip) as unique_views 
			 FROM $table 
			 GROUP BY post_id, url 
			 ORDER BY views DESC 
			 LIMIT %d",
			$limit
		) );
	}

	public static function get_recent_views( $limit = 200 ) {
		global $wpdb;
		$table = $wpdb->prefix . 'vn_page_views';

		self::maybe_create_tables();

		return $wpdb->get_results( $wpdb->prepare(
			"SELECT * FROM $table ORDER BY viewed_at DESC LIMIT %d",
			$limit
		) );
	}

	public static function truncate_logs() {
		global $wpdb;
		$table = $wpdb->prefix . 'vn_page_views';
		return $wpdb->query( "TRUNCATE TABLE $table" );
	}
}
