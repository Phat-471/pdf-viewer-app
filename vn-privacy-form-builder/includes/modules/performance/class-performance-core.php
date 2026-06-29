<?php
/**
 * VN Performance Module - Core v2
 * DB cleanup, WebP, Minify, Lazy Load, Auto-Cron, Cleanup Log
 */
if ( ! defined( 'ABSPATH' ) ) exit;

class VN_Performance_Core {

	public function __construct() {
		$settings = self::get_settings();

		// Cron schedules
		add_filter( 'cron_schedules', [ $this, 'register_cron_schedules' ] );
		add_action( 'init', [ $this, 'maybe_setup_cron' ] );
		add_action( 'vn_perf_auto_cleanup', [ $this, 'run_cron_cleanup' ] );

		// WebP khi upload
		if ( ! empty( $settings['webp_enabled'] ) ) {
			add_filter( 'wp_handle_upload', [ $this, 'convert_to_webp' ] );
		}

		// Minify HTML
		if ( ! empty( $settings['minify_html'] ) && ! is_admin() ) {
			add_action( 'template_redirect', [ $this, 'start_html_minify' ] );
		}

		// Lazy Load ảnh & Iframe/Video
		if ( ! empty( $settings['lazy_load'] ) && ! is_admin() ) {
			add_filter( 'the_content',        [ $this, 'add_lazy_load' ] );
			add_filter( 'post_thumbnail_html',[ $this, 'add_lazy_load' ] );
			add_filter( 'widget_text',        [ $this, 'add_lazy_load' ] );
		}

		// DNS prefetch / preconnect
		if ( ! empty( $settings['dns_prefetch_list'] ) && ! is_admin() ) {
			add_action( 'wp_head', [ $this, 'output_dns_prefetches' ], 1 );
		}

		// AJAX handlers
		add_action( 'wp_ajax_vn_perf_clean_db',          [ $this, 'ajax_clean_db' ] );
		add_action( 'wp_ajax_vn_perf_optimize_tables',    [ $this, 'ajax_optimize_tables' ] );
		add_action( 'wp_ajax_vn_perf_get_bulk_images',   [ $this, 'ajax_get_bulk_images' ] );
		add_action( 'wp_ajax_vn_perf_convert_bulk_webp', [ $this, 'ajax_convert_bulk_webp' ] );
	}

	/* ================================================================
	   Cài đặt
	================================================================ */
	public static function get_settings() {
		$defaults = [
			'webp_enabled'      => 0,
			'webp_quality'      => 82,
			'minify_html'       => 0,
			'lazy_load'         => 1,     // Lazy load ảnh
			'lazy_load_iframe'  => 1,     // Lazy load iframe & video
			'dns_prefetch_list' => '',
			'keep_revisions'    => 3,
			'cron_schedule'     => 'disabled', // disabled | vn_perf_daily | vn_perf_weekly | vn_perf_monthly
			'cron_items'        => [ 'revisions', 'spam', 'transients' ],
		];
		return wp_parse_args( get_option( 'vn_performance_settings', [] ), $defaults );
	}

	public static function save_settings( $data ) {
		$settings = [
			'webp_enabled'      => ! empty( $data['webp_enabled'] ) ? 1 : 0,
			'webp_quality'      => max( 50, min( 100, absint( $data['webp_quality'] ?? 82 ) ) ),
			'minify_html'       => ! empty( $data['minify_html'] ) ? 1 : 0,
			'lazy_load'         => ! empty( $data['lazy_load'] ) ? 1 : 0,
			'lazy_load_iframe'  => ! empty( $data['lazy_load_iframe'] ) ? 1 : 0,
			'dns_prefetch_list' => sanitize_textarea_field( $data['dns_prefetch_list'] ?? '' ),
			'keep_revisions'    => absint( $data['keep_revisions'] ?? 3 ),
			'cron_schedule'     => sanitize_text_field( $data['cron_schedule'] ?? 'disabled' ),
			'cron_items'        => array_filter( [
				! empty( $data['cron_rev'] )   ? 'revisions'  : '',
				! empty( $data['cron_spam'] )  ? 'spam'       : '',
				! empty( $data['cron_trans'] ) ? 'transients' : '',
				! empty( $data['cron_trash'] ) ? 'trash'      : '',
			] ),
		];
		update_option( 'vn_performance_settings', $settings );
		return $settings;
	}

	/* ================================================================
	   WP-Cron: Đăng ký schedule tùy chỉnh
	================================================================ */
	public function register_cron_schedules( $schedules ) {
		$schedules['vn_perf_daily']   = [ 'interval' => DAY_IN_SECONDS,     'display' => 'Mỗi ngày (VN Perf)' ];
		$schedules['vn_perf_weekly']  = [ 'interval' => WEEK_IN_SECONDS,    'display' => 'Mỗi tuần (VN Perf)' ];
		$schedules['vn_perf_monthly'] = [ 'interval' => 30 * DAY_IN_SECONDS,'display' => 'Mỗi tháng (VN Perf)' ];
		return $schedules;
	}

	/* ================================================================
	   WP-Cron: Setup / Clear
	================================================================ */
	public function maybe_setup_cron() {
		$settings = self::get_settings();
		$schedule = $settings['cron_schedule'];

		if ( $schedule === 'disabled' ) {
			wp_clear_scheduled_hook( 'vn_perf_auto_cleanup' );
			return;
		}

		$next = wp_next_scheduled( 'vn_perf_auto_cleanup' );
		// Nếu chưa có hoặc sai schedule → reset
		if ( ! $next ) {
			wp_schedule_event( time() + 60, $schedule, 'vn_perf_auto_cleanup' );
		}
	}

	public static function get_next_cron_time() {
		$ts = wp_next_scheduled( 'vn_perf_auto_cleanup' );
		return $ts ? date_i18n( 'd/m/Y H:i', $ts ) : '—';
	}

	/* ================================================================
	   WP-Cron: Chạy dọn dẹp tự động
	================================================================ */
	public function run_cron_cleanup() {
		$settings = self::get_settings();
		$items    = (array) $settings['cron_items'];
		$options  = [
			'revisions'      => in_array( 'revisions',  $items ),
			'spam'           => in_array( 'spam',       $items ),
			'transients'     => in_array( 'transients', $items ),
			'trash'          => in_array( 'trash',      $items ),
			'keep_revisions' => $settings['keep_revisions'],
		];
		$cleaned = self::clean_database( $options );
		self::log_cleanup( $cleaned, 'auto' );
	}

	/* ================================================================
	   Cleanup Log
	================================================================ */
	public static function log_cleanup( $cleaned, $type = 'manual' ) {
		$log   = get_option( 'vn_perf_cleanup_log', [] );
		$entry = [
			'time'    => current_time( 'mysql' ),
			'type'    => $type,
			'cleaned' => $cleaned,
		];
		array_unshift( $log, $entry );
		$log = array_slice( $log, 0, 100 ); // Giữ tối đa 100 entries
		update_option( 'vn_perf_cleanup_log', $log, false );
	}

	public static function get_cleanup_log( $limit = 50 ) {
		return array_slice( (array) get_option( 'vn_perf_cleanup_log', [] ), 0, $limit );
	}

	public static function clear_cleanup_log() {
		delete_option( 'vn_perf_cleanup_log' );
	}

	/* ================================================================
	   DB Statistics
	================================================================ */
	public static function get_db_stats() {
		global $wpdb;
		return [
			'revisions'     => (int) $wpdb->get_var( "SELECT COUNT(*) FROM {$wpdb->posts} WHERE post_type = 'revision'" ),
			'spam'          => (int) $wpdb->get_var( "SELECT COUNT(*) FROM {$wpdb->comments} WHERE comment_approved = 'spam'" ),
			'trash_posts'   => (int) $wpdb->get_var( "SELECT COUNT(*) FROM {$wpdb->posts} WHERE post_status = 'trash'" ),
			'expired_trans' => (int) $wpdb->get_var( "SELECT COUNT(*) FROM {$wpdb->options} WHERE option_name LIKE '_transient_timeout_%' AND option_value < UNIX_TIMESTAMP()" ),
			'orphan_meta'   => (int) $wpdb->get_var( "SELECT COUNT(*) FROM {$wpdb->postmeta} pm LEFT JOIN {$wpdb->posts} p ON pm.post_id = p.ID WHERE p.ID IS NULL" ),
		];
	}

	/* ================================================================
	   Dọn dẹp Database
	================================================================ */
	public static function clean_database( $options = [] ) {
		global $wpdb;
		$settings = self::get_settings();
		$keep     = (int) ( $options['keep_revisions'] ?? $settings['keep_revisions'] );
		$cleaned  = [];

		if ( ! empty( $options['revisions'] ) ) {
			if ( $keep <= 0 ) {
				$count = $wpdb->query( "DELETE FROM {$wpdb->posts} WHERE post_type = 'revision'" );
				$wpdb->query( "DELETE pm FROM {$wpdb->postmeta} pm LEFT JOIN {$wpdb->posts} p ON pm.post_id = p.ID WHERE p.ID IS NULL" );
			} else {
				$parent_ids = $wpdb->get_col( "SELECT DISTINCT post_parent FROM {$wpdb->posts} WHERE post_type = 'revision' AND post_parent > 0" );
				$count      = 0;
				foreach ( $parent_ids as $pid ) {
					$keep_ids = $wpdb->get_col( $wpdb->prepare(
						"SELECT ID FROM {$wpdb->posts} WHERE post_type = 'revision' AND post_parent = %d ORDER BY post_date DESC LIMIT %d",
						$pid, $keep
					) );
					if ( ! empty( $keep_ids ) ) {
						$ph = implode( ',', array_fill( 0, count( $keep_ids ), '%d' ) );
						$count += $wpdb->query( $wpdb->prepare(
							"DELETE FROM {$wpdb->posts} WHERE post_type = 'revision' AND post_parent = %d AND ID NOT IN ($ph)",
							array_merge( [ $pid ], $keep_ids )
						) );
					}
				}
			}
			$cleaned['revisions'] = (int) $count;
		}

		if ( ! empty( $options['spam'] ) ) {
			$count = $wpdb->query( "DELETE FROM {$wpdb->comments} WHERE comment_approved = 'spam'" );
			$wpdb->query( "DELETE cm FROM {$wpdb->commentmeta} cm LEFT JOIN {$wpdb->comments} c ON cm.comment_id = c.comment_ID WHERE c.comment_ID IS NULL" );
			$cleaned['spam'] = (int) $count;
		}

		if ( ! empty( $options['transients'] ) ) {
			$count = $wpdb->query( "DELETE FROM {$wpdb->options} WHERE option_name LIKE '_transient_timeout_%' AND option_value < UNIX_TIMESTAMP()" );
			$wpdb->query( "DELETE FROM {$wpdb->options} WHERE option_name LIKE '_transient_%' AND REPLACE(option_name,'_transient_','_transient_timeout_') NOT IN (SELECT option_name FROM (SELECT option_name FROM {$wpdb->options}) AS tmp)" );
			$cleaned['transients'] = (int) $count;
		}

		if ( ! empty( $options['trash'] ) ) {
			$trash_ids = $wpdb->get_col( "SELECT ID FROM {$wpdb->posts} WHERE post_status = 'trash'" );
			$count     = 0;
			foreach ( $trash_ids as $id ) { wp_delete_post( $id, true ); $count++; }
			$cleaned['trash'] = $count;
		}

		if ( ! empty( $options['optimize'] ) ) {
			$tables = $wpdb->get_col( "SHOW TABLES LIKE '{$wpdb->prefix}%'" );
			foreach ( $tables as $t ) { $wpdb->query( "OPTIMIZE TABLE `$t`" ); }
			$cleaned['optimized'] = count( $tables );
		}

		return $cleaned;
	}

	/* ================================================================
	   AJAX: Dọn dẹp
	================================================================ */
	public function ajax_clean_db() {
		check_ajax_referer( 'vn_performance_nonce', 'nonce' );
		if ( ! current_user_can( 'manage_options' ) ) wp_send_json_error( 'Unauthorized' );

		$options = [
			'revisions'      => ! empty( $_POST['revisions'] ),
			'spam'           => ! empty( $_POST['spam'] ),
			'transients'     => ! empty( $_POST['transients'] ),
			'trash'          => ! empty( $_POST['trash'] ),
			'optimize'       => ! empty( $_POST['optimize'] ),
			'keep_revisions' => absint( $_POST['keep_revisions'] ?? 3 ),
		];
		$result = self::clean_database( $options );
		self::log_cleanup( $result, 'manual' );

		wp_send_json_success( [
			'cleaned' => $result,
			'stats'   => self::get_db_stats(),
			'message' => 'Dọn dẹp hoàn tất!',
		] );
	}

	public function ajax_optimize_tables() {
		check_ajax_referer( 'vn_performance_nonce', 'nonce' );
		if ( ! current_user_can( 'manage_options' ) ) wp_send_json_error( 'Unauthorized' );
		global $wpdb;
		$tables = $wpdb->get_col( "SHOW TABLES LIKE '{$wpdb->prefix}%'" );
		foreach ( $tables as $t ) { $wpdb->query( "OPTIMIZE TABLE `$t`" ); }
		wp_send_json_success( [ 'tables' => count( $tables ), 'message' => 'Optimize ' . count( $tables ) . ' bảng thành công!' ] );
	}

	/* ================================================================
	   Lazy Load — thêm loading="lazy" vào ảnh, iframe và video
	================================================================ */
	public function add_lazy_load( $html ) {
		if ( ! $html ) return $html;
		$settings = self::get_settings();

		// Lazy load cho thẻ <img>
		$html = preg_replace_callback( '/<img\s([^>]*)>/i', function( $m ) {
			$attrs = $m[1];
			if ( stripos( $attrs, 'loading=' ) !== false ) return $m[0];
			return '<img loading="lazy" ' . $attrs . '>';
		}, $html );

		// Lazy load cho <iframe> và <video> nếu được bật
		if ( ! empty( $settings['lazy_load_iframe'] ) ) {
			$html = preg_replace_callback( '/<(iframe|video)\s([^>]*)>/i', function( $m ) {
				$tag   = $m[1];
				$attrs = $m[2];
				if ( stripos( $attrs, 'loading=' ) !== false ) return $m[0];
				return '<' . $tag . ' loading="lazy" ' . $attrs . '>';
			}, $html );
		}

		return $html;
	}

	/* ================================================================
	   DNS Prefetch & Preconnect
	================================================================ */
	public function output_dns_prefetches() {
		$settings = self::get_settings();
		$domains  = array_filter( array_map( 'trim', explode( "\n", $settings['dns_prefetch_list'] ) ) );
		if ( empty( $domains ) ) return;

		echo "\n<!-- VN Performance DNS Prefetch & Preconnect -->\n";
		foreach ( $domains as $domain ) {
			if ( strpos( $domain, '//' ) !== 0 && strpos( $domain, 'http' ) !== 0 ) {
				$domain = '//' . $domain;
			}
			$esc_domain = esc_url( $domain );
			echo '<link rel="dns-prefetch" href="' . $esc_domain . '">' . "\n";
			echo '<link rel="preconnect" href="' . $esc_domain . '">' . "\n";
		}
		echo "<!-- End VN Performance -->\n\n";
	}

	/* ================================================================
	   Bulk WebP Conversion Helpers & AJAX
	================================================================ */
	public function ajax_get_bulk_images() {
		check_ajax_referer( 'vn_performance_nonce', 'nonce' );
		if ( ! current_user_can( 'manage_options' ) ) wp_send_json_error( 'Unauthorized' );

		$query = new WP_Query( [
			'post_type'      => 'attachment',
			'post_mime_type' => [ 'image/jpeg', 'image/png' ],
			'post_status'    => 'inherit',
			'posts_per_page' => -1,
			'fields'         => 'ids',
		] );

		wp_send_json_success( [
			'ids'   => $query->posts,
			'count' => count( $query->posts ),
		] );
	}

	public function ajax_convert_bulk_webp() {
		check_ajax_referer( 'vn_performance_nonce', 'nonce' );
		if ( ! current_user_can( 'manage_options' ) ) wp_send_json_error( 'Unauthorized' );

		$ids     = isset( $_POST['ids'] ) ? array_map( 'absint', $_POST['ids'] ) : [];
		$quality = max( 50, min( 100, absint( $_POST['quality'] ?? 82 ) ) );

		if ( empty( $ids ) ) {
			wp_send_json_error( 'Không tìm thấy ảnh cần chuyển đổi.' );
		}

		$converted_files = 0;
		$converted_attachments = 0;

		foreach ( $ids as $id ) {
			$files_count = self::convert_attachment_to_webp( $id, $quality );
			if ( $files_count > 0 ) {
				$converted_files += $files_count;
				$converted_attachments++;
			}
		}

		wp_send_json_success( [
			'converted_attachments' => $converted_attachments,
			'converted_files'       => $converted_files,
		] );
	}

	public static function convert_file_to_webp( $file, $quality = 82 ) {
		if ( ! function_exists( 'imagewebp' ) || ! file_exists( $file ) ) return false;
		
		$webp_path = preg_replace( '/\.(jpe?g|png)$/i', '.webp', $file );
		if ( file_exists( $webp_path ) ) return true;

		$image = null;
		$ext   = strtolower( pathinfo( $file, PATHINFO_EXTENSION ) );

		if ( $ext === 'jpeg' || $ext === 'jpg' ) {
			$image = @imagecreatefromjpeg( $file );
		} elseif ( $ext === 'png' ) {
			$image = @imagecreatefrompng( $file );
			if ( $image ) {
				@imagepalettetotruecolor( $image );
				@imagealphablending( $image, true );
				@imagesavealpha( $image, true );
			}
		}

		if ( $image ) {
			$saved = @imagewebp( $image, $webp_path, $quality );
			@imagedestroy( $image );
			return $saved;
		}

		return false;
	}

	public static function convert_attachment_to_webp( $id, $quality = 82 ) {
		$metadata = wp_get_attachment_metadata( $id );
		if ( ! $metadata ) return 0;

		$main_file = get_attached_file( $id );
		$converted = 0;
		
		if ( $main_file && file_exists( $main_file ) ) {
			if ( self::convert_file_to_webp( $main_file, $quality ) ) {
				$converted++;
			}
		}

		if ( ! empty( $metadata['sizes'] ) ) {
			$path_info = pathinfo( $main_file );
			$dir       = $path_info['dirname'];
			foreach ( $metadata['sizes'] as $size => $info ) {
				if ( ! empty( $info['file'] ) ) {
					$size_file = $dir . '/' . $info['file'];
					if ( file_exists( $size_file ) ) {
						if ( self::convert_file_to_webp( $size_file, $quality ) ) {
							$converted++;
						}
					}
				}
			}
		}
		return $converted;
	}

	/* ================================================================
	   WebP Conversion
	================================================================ */
	public function convert_to_webp( $upload ) {
		if ( ! function_exists( 'imagewebp' ) ) return $upload;
		$settings = self::get_settings();
		$quality  = (int) $settings['webp_quality'];
		$file     = $upload['file'];
		$type     = $upload['type'];
		$image    = null;

		if ( $type === 'image/jpeg' || $type === 'image/jpg' ) {
			$image = imagecreatefromjpeg( $file );
		} elseif ( $type === 'image/png' ) {
			$image = imagecreatefrompng( $file );
			if ( $image ) {
				imagepalettetotruecolor( $image );
				imagealphablending( $image, true );
				imagesavealpha( $image, true );
			}
		}

		if ( $image ) {
			$webp_path = preg_replace( '/\.(jpe?g|png)$/i', '.webp', $file );
			imagewebp( $image, $webp_path, $quality );
			imagedestroy( $image );
		}
		return $upload;
	}

	/* ================================================================
	   HTML Minify
	================================================================ */
	public function start_html_minify() {
		if ( is_feed() || is_embed() ) return;
		ob_start( [ $this, 'minify_html_output' ] );
	}

	public function minify_html_output( $html ) {
		if ( preg_match( '/<(pre|textarea)/i', $html ) ) return $html;
		$html = preg_replace( '/<!--(?!\[if).*?-->/s', '', $html );
		$html = preg_replace( '/>\s+</', '><', $html );
		$html = preg_replace( '/\s{2,}/', ' ', $html );
		return trim( $html );
	}

	/* ================================================================
	   Image Stats
	================================================================ */
	public static function get_image_stats() {
		$counts = wp_count_attachments( 'image' );
		$total  = isset( $counts->inherit ) ? (int) $counts->inherit : 0;

		$uploads    = wp_upload_dir();
		$base       = $uploads['basedir'];
		$webp_count = 0;
		if ( is_dir( $base ) ) {
			$iterator = new RecursiveIteratorIterator(
				new RecursiveDirectoryIterator( $base, RecursiveDirectoryIterator::SKIP_DOTS )
			);
			foreach ( $iterator as $file ) {
				if ( strtolower( $file->getExtension() ) === 'webp' ) $webp_count++;
			}
		}

		return [
			'total_images' => $total,
			'webp_files'   => $webp_count,
			'gd_available' => extension_loaded( 'gd' ) && function_exists( 'imagewebp' ),
		];
	}
}
