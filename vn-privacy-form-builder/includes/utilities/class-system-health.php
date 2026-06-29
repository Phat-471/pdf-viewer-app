<?php
if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

class VN_Privacy_System_Health {

	public static function get_system_health() {
		$cached = get_transient( 'vn_privacy_system_health_data' );
		if ( $cached !== false ) {
			return $cached;
		}

		$data = [];
		global $wpdb, $wp_version;

		// 1. PHP Version
		$php_ok = version_compare( PHP_VERSION, '7.4', '>=' );
		$data[] = [
			'label'  => 'PHP Version',
			'value'  => PHP_VERSION,
			'status' => $php_ok ? 'success' : 'warning',
			'desc'   => $php_ok ? 'Phiên bản PHP đạt chuẩn (>= 7.4).' : 'Khuyến nghị nâng cấp lên PHP 7.4+.',
		];

		// 2. WordPress Version
		$data[] = [
			'label'  => 'WordPress Version',
			'value'  => $wp_version,
			'status' => 'success',
			'desc'   => 'Phiên bản WordPress đang chạy.',
		];

		// 3. Memory Limit
		$mem_raw   = ini_get( 'memory_limit' );
		$mem_bytes = self::let_to_num( $mem_raw );
		$mem_ok    = $mem_bytes >= 134217728;
		$data[] = [
			'label'  => 'Memory Limit',
			'value'  => $mem_raw,
			'status' => $mem_ok ? 'success' : 'warning',
			'desc'   => $mem_ok ? 'Đủ bộ nhớ (>= 128M).' : 'Nên tăng lên tối thiểu 128M.',
		];

		// 4. Max Execution Time
		$max_time    = (int) ini_get( 'max_execution_time' );
		$max_time_ok = $max_time === 0 || $max_time >= 30;
		$data[] = [
			'label'  => 'Max Execution Time',
			'value'  => $max_time === 0 ? 'Không giới hạn' : $max_time . 's',
			'status' => $max_time_ok ? 'success' : 'warning',
			'desc'   => $max_time_ok ? 'Đủ thời gian thực thi.' : 'Nên tăng lên ít nhất 30 giây.',
		];

		// 5. SSL / HTTPS
		$ssl = is_ssl();
		$data[] = [
			'label'  => 'HTTPS / SSL',
			'value'  => $ssl ? 'Đang bật' : 'Chưa bật',
			'status' => $ssl ? 'success' : 'danger',
			'desc'   => $ssl ? 'Dữ liệu được mã hóa TLS.' : 'Cảnh báo: cần cài SSL để bảo vệ dữ liệu khách hàng.',
		];

		// 6. ZipArchive (required for backup)
		$zip_ok = class_exists( 'ZipArchive' );
		$data[] = [
			'label'  => 'ZipArchive (PHP Extension)',
			'value'  => $zip_ok ? 'Có sẵn' : 'Không có',
			'status' => $zip_ok ? 'success' : 'danger',
			'desc'   => $zip_ok ? 'Sao lưu ZIP hoạt động bình thường.' : 'Cần bật extension php_zip để dùng tính năng Sao lưu.',
		];

		// 7. cURL
		$curl_ok = function_exists( 'curl_version' );
		$data[] = [
			'label'  => 'cURL (PHP Extension)',
			'value'  => $curl_ok ? 'Có sẵn' : 'Không có',
			'status' => $curl_ok ? 'success' : 'warning',
			'desc'   => $curl_ok ? 'cURL khả dụng, hỗ trợ gửi HTTP requests.' : 'Một số tính năng có thể bị ảnh hưởng.',
		];

		// 8. Database size
		$db_size  = 0;
		$db_rows  = $wpdb->get_results( "SHOW TABLE STATUS LIKE '{$wpdb->prefix}%'", ARRAY_A );
		foreach ( $db_rows as $row ) {
			$db_size += ( $row['Data_length'] + $row['Index_length'] );
		}
		$data[] = [
			'label'  => 'Dung lượng Database',
			'value'  => size_format( $db_size ),
			'status' => 'success',
			'desc'   => 'Tổng kích thước các bảng dữ liệu WordPress.',
		];

		// 9. WP_DEBUG
		$debug_on = defined( 'WP_DEBUG' ) && WP_DEBUG;
		$data[] = [
			'label'  => 'WP_DEBUG Mode',
			'value'  => $debug_on ? 'Đang bật' : 'Đã tắt',
			'status' => $debug_on ? 'warning' : 'success',
			'desc'   => $debug_on ? 'Nên tắt WP_DEBUG trên môi trường production để ẩn thông báo lỗi.' : 'Tốt — chế độ debug đã tắt.',
		];

		// 10. File Upload Max Size
		$upload_max = ini_get( 'upload_max_filesize' );
		$data[] = [
			'label'  => 'Upload Max Filesize',
			'value'  => $upload_max,
			'status' => 'success',
			'desc'   => 'Giới hạn kích thước tệp tin tải lên.',
		];

		// 11. Default "admin" username check
		$admin_exists = username_exists( 'admin' );
		$data[] = [
			'label'  => 'Tài khoản admin mặc định',
			'value'  => $admin_exists ? '⚠️ Tồn tại' : '✅ An toàn',
			'status' => $admin_exists ? 'warning' : 'success',
			'desc'   => $admin_exists ? 'Cảnh báo: Không nên sử dụng username "admin" vì dễ bị tấn công dò mật khẩu.' : 'Tốt — không dùng username "admin".',
		];

		// 12. wp-config.php security check
		$config_path = ABSPATH . 'wp-config.php';
		$config_safe = true;
		$config_val  = 'Đã cấu hình';
		if ( file_exists( $config_path ) ) {
			$perms = fileperms( $config_path ) & 0777;
			// Safe permissions for config are usually 400, 440, or 600/640. If group/world writeable, it is dangerous.
			if ( ( $perms & 0002 ) || ( $perms & 0020 ) ) {
				$config_safe = false;
				$config_val  = '⚠️ Quyền ghi mở';
			}
		}
		$data[] = [
			'label'  => 'Bảo mật wp-config.php',
			'value'  => $config_val,
			'status' => $config_safe ? 'success' : 'danger',
			'desc'   => $config_safe ? 'Tệp cấu hình hệ thống được bảo vệ an toàn.' : 'Cảnh báo: Tệp wp-config.php đang cho phép ghi tự do, hãy đặt lại chmod 600 hoặc 640.',
		];

		// 13. debug.log check
		$debug_log_path = WP_CONTENT_DIR . '/debug.log';
		$log_exists     = file_exists( $debug_log_path );
		$log_size       = $log_exists ? size_format( filesize( $debug_log_path ) ) : '0 KB';
		$log_status     = 'success';
		$log_desc       = 'Không phát hiện tệp tin ghi lỗi debug.log.';
		if ( $log_exists ) {
			$log_status = ( filesize( $debug_log_path ) > 10 * 1024 * 1024 ) ? 'danger' : 'warning';
			$log_desc   = "Phát hiện tệp debug.log ({$log_size}). Bạn có thể xóa tệp này để giải phóng dung lượng.";
		}
		$data[] = [
			'label'  => 'Tệp tin Nhật ký lỗi (debug.log)',
			'value'  => $log_exists ? "⚠️ Có sẵn ({$log_size})" : '✅ Không có',
			'status' => $log_status,
			'desc'   => $log_desc,
		];

		set_transient( 'vn_privacy_system_health_data', $data, 2 * HOUR_IN_SECONDS );
		return $data;
	}

	public static function get_file_stats() {
		$cached = get_transient( 'vn_privacy_file_stats_data' );
		if ( $cached !== false ) {
			return $cached;
		}

		$upload_dir = wp_upload_dir();
		$base_path  = $upload_dir['basedir'];
		$total_size = 0;
		$file_count = 0;

		if ( is_dir( $base_path ) ) {
			try {
				$io = new RecursiveIteratorIterator( new RecursiveDirectoryIterator( $base_path, RecursiveDirectoryIterator::SKIP_DOTS ) );
				foreach ( $io as $file ) {
					if ( $file->isFile() ) {
						$total_size += $file->getSize();
						$file_count++;
					}
				}
			} catch ( Exception $e ) {
				// Skip unreadable dirs
			}
		}

		$stats = [
			'path'  => $base_path,
			'size'  => size_format( $total_size ),
			'count' => $file_count,
		];

		set_transient( 'vn_privacy_file_stats_data', $stats, 2 * HOUR_IN_SECONDS );
		return $stats;
	}

	public static function get_db_summary() {
		global $wpdb;
		$total_forms   = (int) $wpdb->get_var( "SELECT COUNT(*) FROM {$wpdb->prefix}vn_privacy_forms" );
		$total_entries = (int) $wpdb->get_var( "SELECT COUNT(*) FROM {$wpdb->prefix}vn_privacy_entries" );
		return compact( 'total_forms', 'total_entries' );
	}

	public static function cleanup_database() {
		global $wpdb;
		$wpdb->query( "DELETE FROM {$wpdb->posts} WHERE post_type = 'revision'" );
		$wpdb->query( "DELETE FROM {$wpdb->options} WHERE option_name LIKE '_transient_timeout_%'" );
		$wpdb->query( "DELETE FROM {$wpdb->options} WHERE option_name LIKE '_transient_%'" );
		$tables = $wpdb->get_col( "SHOW TABLES LIKE '{$wpdb->prefix}%'" );
		foreach ( $tables as $table ) {
			$wpdb->query( "OPTIMIZE TABLE $table" );
		}
	}

	/* ----------------------------------------------------------------
	   Delete the system debug.log to save hosting space
	   ---------------------------------------------------------------- */
	public static function ajax_delete_debug_log() {
		check_ajax_referer( 'vn_tools_nonce', 'nonce' );
		if ( ! current_user_can( 'manage_options' ) ) wp_send_json_error( 'Unauthorized' );

		$debug_log_path = WP_CONTENT_DIR . '/debug.log';
		if ( file_exists( $debug_log_path ) ) {
			if ( @unlink( $debug_log_path ) ) {
				wp_send_json_success( 'Đã xóa file nhật ký lỗi debug.log thành công!' );
			} else {
				wp_send_json_error( 'Không thể xóa file. Vui lòng kiểm tra lại quyền ghi tệp.' );
			}
		} else {
			wp_send_json_error( 'Tệp tin debug.log không tồn tại hoặc đã được xóa trước đó.' );
		}
	}

	// BC alias
	public static function dọn_dẹp_database() {
		self::cleanup_database();
	}

	private static function let_to_num( $size ) {
		$l   = strtoupper( substr( $size, -1 ) );
		$ret = (int) substr( $size, 0, -1 );
		switch ( $l ) {
			case 'P': $ret *= 1024;
			case 'T': $ret *= 1024;
			case 'G': $ret *= 1024;
			case 'M': $ret *= 1024;
			case 'K': $ret *= 1024;
		}
		return $ret;
	}

	public static function export_report() {
		$health  = self::get_system_health();
		$stats   = self::get_file_stats();
		$db_sum  = self::get_db_summary();
		$lines   = [];
		$lines[] = '=== VN Privacy Plugin — System Health Report ===';
		$lines[] = 'Generated: ' . current_time( 'Y-m-d H:i:s' );
		$lines[] = 'Site URL : ' . get_site_url();
		$lines[] = '';
		foreach ( $health as $item ) {
			$icon = $item['status'] === 'success' ? '[OK]' : ( $item['status'] === 'warning' ? '[WN]' : '[ERR]' );
			$lines[] = sprintf( '%s  %-35s %s', $icon, $item['label'] . ':', $item['value'] );
		}
		$lines[] = '';
		$lines[] = '--- Uploads ---';
		$lines[] = 'Files : ' . number_format( $stats['count'] );
		$lines[] = 'Size  : ' . $stats['size'];
		$lines[] = '';
		$lines[] = '--- Plugin Data ---';
		$lines[] = 'Forms   : ' . $db_sum['total_forms'];
		$lines[] = 'Entries : ' . $db_sum['total_entries'];

		$filename = 'vn-system-report-' . date( 'Y-m-d' ) . '.txt';
		header( 'Content-Type: text/plain; charset=utf-8' );
		header( 'Content-Disposition: attachment; filename="' . $filename . '"' );
		echo implode( "\n", $lines );
		exit;
	}

	/* ----------------------------------------------------------------
	   Reinstall WordPress Core files from official API without losing data.
	   Leaves wp-content, wp-config.php, and .htaccess untouched.
	   ---------------------------------------------------------------- */
	public static function ajax_reinstall_wordpress_core() {
		check_ajax_referer( 'vn_tools_nonce', 'nonce' );
		if ( ! current_user_can( 'install_languages' ) || ! current_user_can( 'update_core' ) ) {
			wp_send_json_error( 'Bạn không có quyền cài đặt lại hệ thống.' );
		}

		global $wp_version;
		
		// 1. Get download URL from WordPress core API
		$locale = get_locale();
		$api_url = "https://api.wordpress.org/core/version-1.1/?locale=" . urlencode( $locale );
		$response = wp_remote_get( $api_url );

		if ( is_wp_error( $response ) ) {
			$response = wp_remote_get( "https://api.wordpress.org/core/version-1.1/" );
		}

		$download_url = '';
		if ( ! is_wp_error( $response ) ) {
			$body = wp_remote_retrieve_body( $response );
			$data = json_decode( $body, true );
			if ( isset( $data['offers'][0]['download'] ) ) {
				$download_url = $data['offers'][0]['download'];
			}
		}

		if ( empty( $download_url ) ) {
			$download_url = "https://wordpress.org/wordpress-{$wp_version}.zip";
		}

		// 2. Set timeout and memory limit
		@set_time_limit( 300 );
		@ini_set( 'memory_limit', '256M' );

		// 3. Download the ZIP package using WordPress HTTP API
		require_once ABSPATH . 'wp-admin/includes/file.php';
		$tmp_zip = download_url( $download_url );

		if ( is_wp_error( $tmp_zip ) ) {
			wp_send_json_error( 'Không thể tải xuống gói cài đặt WordPress Core: ' . $tmp_zip->get_error_message() );
		}

		// 4. Extract package to temp folder
		$upload_dir = wp_upload_dir();
		$extract_to = $upload_dir['basedir'] . '/vn_wp_core_temp_' . time();
		wp_mkdir_p( $extract_to );

		$zip = new ZipArchive();
		if ( $zip->open( $tmp_zip ) !== true ) {
			unlink( $tmp_zip );
			wp_send_json_error( 'Không thể mở gói cài đặt ZIP.' );
		}
		$zip->extractTo( $extract_to );
		$zip->close();
		unlink( $tmp_zip );

		// The files are unpacked into $extract_to/wordpress
		$wp_source_dir = $extract_to . '/wordpress';
		if ( ! is_dir( $wp_source_dir ) ) {
			self::recursive_delete( $extract_to );
			wp_send_json_error( 'Thư mục nguồn WordPress không tìm thấy.' );
		}

		// 5. Overwrite wp-admin and wp-includes and root files
		// WE MUST DO THIS CAREFULLY. NEVER OVERWRITE wp-content or wp-config.php.
		$core_success = true;
		try {
			self::copy_core_files( $wp_source_dir, ABSPATH );
		} catch ( Exception $e ) {
			$core_success = false;
		}

		// 6. Cleanup temp folder
		self::recursive_delete( $extract_to );

		if ( $core_success ) {
			wp_send_json_success( 'Cài đặt lại WordPress Core thành công! Toàn bộ file nhân đã được thay thế sạch sẽ.' );
		} else {
			wp_send_json_error( 'Có lỗi xảy ra trong quá trình ghi đè các tệp tin hệ thống.' );
		}
	}

	private static function copy_core_files( $src, $dst ) {
		$dir = opendir( $src );
		while ( ( $f = readdir( $dir ) ) !== false ) {
			if ( $f === '.' || $f === '..' ) continue;
			
			// Strictly ignore content, custom configuration files
			if ( in_array( $f, [ 'wp-content', 'wp-config.php', 'wp-config-sample.php', '.htaccess' ], true ) ) {
				continue;
			}

			$src_path = "$src/$f";
			$dst_path = "$dst/$f";

			if ( is_dir( $src_path ) ) {
				@mkdir( $dst_path, 0755, true );
				self::copy_core_files( $src_path, $dst_path );
			} else {
				@copy( $src_path, $dst_path );
			}
		}
		closedir( $dir );
	}

	private static function recursive_delete( $dir ) {
		if ( ! is_dir( $dir ) ) return;
		foreach ( scandir( $dir ) as $f ) {
			if ( $f === '.' || $f === '..' ) continue;
			$path = $dir . DIRECTORY_SEPARATOR . $f;
			is_dir( $path ) && ! is_link( $path ) ? self::recursive_delete( $path ) : unlink( $path );
		}
		rmdir( $dir );
	}

	/* ----------------------------------------------------------------
	   Scan WordPress core files for changes/corruption
	   Checks local file MD5s against official WordPress checksums API
	   ---------------------------------------------------------------- */
	public static function ajax_scan_changed_files() {
		check_ajax_referer( 'vn_tools_nonce', 'nonce' );
		if ( ! current_user_can( 'manage_options' ) ) wp_send_json_error( 'Unauthorized' );

		global $wp_version;
		$locale = get_locale();

		// Fetch official checksums
		$api_url = "https://api.wordpress.org/core/checksums/1.0/?version={$wp_version}&locale={$locale}";
		$response = wp_remote_get( $api_url );
		if ( is_wp_error( $response ) ) {
			// Fallback without locale
			$api_url = "https://api.wordpress.org/core/checksums/1.0/?version={$wp_version}";
			$response = wp_remote_get( $api_url );
		}

		if ( is_wp_error( $response ) ) {
			wp_send_json_error( 'Không thể kết nối tới máy chủ WordPress.org để lấy mã băm kiểm tra.' );
		}

		$body = wp_remote_retrieve_body( $response );
		$data = json_decode( $body, true );
		if ( empty( $data['checksums'] ) || ! is_array( $data['checksums'] ) ) {
			wp_send_json_error( 'Dữ liệu mã băm kiểm tra từ WordPress.org không hợp lệ.' );
		}

		$checksums = $data['checksums'];
		$modified  = [];
		$missing   = [];

		foreach ( $checksums as $file => $expected_md5 ) {
			// Skip wp-content and custom config templates
			if ( strpos( $file, 'wp-content/' ) === 0 || in_array( $file, [ 'wp-config-sample.php', 'wp-config.php' ], true ) ) {
				continue;
			}

			$local_path = ABSPATH . $file;
			if ( ! file_exists( $local_path ) ) {
				$missing[] = $file;
				continue;
			}

			$local_md5 = md5_file( $local_path );
			if ( $local_md5 !== $expected_md5 ) {
				$modified[] = $file;
			}
		}

		$output = [];
		if ( empty( $modified ) && empty( $missing ) ) {
			$output[] = '✅ Tuyệt vời! Toàn bộ file cốt lõi (WordPress Core) trùng khớp 100% với file gốc từ WordPress.org (Không bị sửa đổi hay nhiễm mã độc).';
		} else {
			if ( ! empty( $modified ) ) {
				$output[] = '⚠️ CẢNH BÁO: Phát hiện ' . count( $modified ) . ' tệp tin WordPress Core bị THAY ĐỔI so với gốc:';
				foreach ( array_slice( $modified, 0, 15 ) as $m ) {
					$output[] = "  - [Bị sửa đổi] $m";
				}
				if ( count( $modified ) > 15 ) {
					$output[] = '  - ... và ' . ( count( $modified ) - 15 ) . ' tệp tin khác.';
				}
			}
			if ( ! empty( $missing ) ) {
				$output[] = "\n❌ CẢNH BÁO: Phát hiện " . count( $missing ) . ' tệp tin WordPress Core bị THIẾU/BỊ XÓA:';
				foreach ( array_slice( $missing, 0, 15 ) as $ms ) {
					$output[] = "  - [Bị thiếu] $ms";
				}
				if ( count( $missing ) > 15 ) {
					$output[] = '  - ... và ' . ( count( $missing ) - 15 ) . ' tệp tin khác.';
				}
			}
			$output[] = "\n💡 Gợi ý: Bạn có thể sử dụng công cụ \"Cài đặt lại WordPress Core\" để tự động khôi phục lại các file này về trạng thái sạch ban đầu.";
		}

		wp_send_json_success( implode( "\n", $output ) );
	}
}
