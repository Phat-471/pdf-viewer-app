<?php
if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

class VN_Privacy_Admin_Actions {

	/* ================================================================
	   Handle standard page actions (POST / GET from admin pages)
	================================================================ */
	public static function handle_actions() {
		if ( ! is_admin() || ! current_user_can( 'manage_options' ) ) return;

		$action = $_POST['vn_privacy_action'] ?? $_GET['action'] ?? '';

		switch ( $action ) {

			/* ---- Save New Form ---- */
			case 'save_new_form':
				self::require_nonce( 'form_nonce', 'create_privacy_form' );
				$fields_json = self::parse_fields_json();
				global $wpdb;
				$wpdb->insert( $wpdb->prefix . 'vn_privacy_forms', [
					'title'  => sanitize_text_field( $_POST['form_title'] ?? '' ),
					'fields' => $fields_json,
				] );
				wp_redirect( admin_url( 'admin.php?page=vn-privacy-forms' ) );
				exit;

			/* ---- Update Existing Form ---- */
			case 'update_form':
				self::require_nonce( 'form_nonce', 'create_privacy_form' );
				$form_id     = intval( $_POST['form_id'] ?? 0 );
				$fields_json = self::parse_fields_json();
				if ( ! $form_id ) wp_die( 'Không tìm thấy Form ID.' );
				global $wpdb;
				$wpdb->update(
					$wpdb->prefix . 'vn_privacy_forms',
					[
						'title'  => sanitize_text_field( $_POST['form_title'] ?? '' ),
						'fields' => $fields_json,
					],
					[ 'id' => $form_id ],
					[ '%s', '%s' ],
					[ '%d' ]
				);
				wp_redirect( admin_url( 'admin.php?page=vn-privacy-forms' ) );
				exit;

			/* ---- Delete Form ---- */
			case 'delete_form':
				$id = intval( $_GET['id'] ?? 0 );
				self::require_nonce( '_wpnonce', 'delete_form_' . $id );
				global $wpdb;
				$wpdb->delete( $wpdb->prefix . 'vn_privacy_forms', [ 'id' => $id ], [ '%d' ] );
				wp_redirect( admin_url( 'admin.php?page=vn-privacy-forms' ) );
				exit;

			/* ---- Delete Entry ---- */
			case 'delete_entry':
				$id = intval( $_GET['id'] ?? 0 );
				self::require_nonce( '_wpnonce', 'delete_entry_' . $id );
				VN_Privacy_DB::delete_entry( $id );
				wp_redirect( admin_url( 'admin.php?page=vn-privacy-entries' ) );
				exit;

			/* ---- Save General Utilities Settings ---- */
			case 'save_utilities':
				self::require_nonce( 'utilities_nonce', 'save_privacy_utilities' );
				update_option( 'vn_privacy_classic_editor_enabled', isset( $_POST['classic_editor_enabled'] ) ? 1 : 0 );
				wp_redirect( admin_url( 'admin.php?page=vn-settings&setting_tab=utilities&status=saved' ) );
				exit;

			/* ---- Save Maintenance Message ---- */
			case 'save_maintenance_msg':
				self::require_nonce( 'utilities_nonce', 'save_privacy_utilities' );
				update_option( 'vn_privacy_maintenance_msg', sanitize_textarea_field( $_POST['maintenance_msg'] ?? '' ) );
				wp_redirect( admin_url( 'admin.php?page=vn-settings&setting_tab=utilities&status=saved' ) );
				exit;

			/* ---- Save Auto-Backup Schedule ---- */
			case 'vn_save_autobackup_settings':
				self::require_nonce( 'vn_autobackup_nonce', 'vn_autobackup_settings' );
				$enabled   = isset( $_POST['vn_autobackup_enabled'] ) ? 1 : 0;
				$frequency = sanitize_key( $_POST['vn_autobackup_frequency'] ?? 'daily' );
				$mode      = sanitize_key( $_POST['vn_autobackup_mode'] ?? 'full' );

				update_option( 'vn_autobackup_enabled', $enabled );
				update_option( 'vn_autobackup_frequency', $frequency );
				update_option( 'vn_autobackup_mode', $mode );

				// Schedule/unschedule the WP Cron job
				VN_Privacy_Backup_Manager::schedule_auto_backup( $enabled, $frequency );

				wp_redirect( admin_url( 'admin.php?page=vn-settings&setting_tab=utilities&status=saved' ) );
				exit;

			/* ---- Save FTP Backup Settings ---- */
			case 'vn_save_ftp_settings':
				self::require_nonce( 'vn_ftp_nonce', 'vn_ftp_settings' );
				$security_settings = get_option( 'vn_security_settings', [] );

				$security_settings['ftp_host']    = sanitize_text_field( $_POST['ftp_host'] ?? '' );
				$security_settings['ftp_port']    = sanitize_text_field( $_POST['ftp_port'] ?? '21' );
				$security_settings['ftp_user']    = sanitize_text_field( $_POST['ftp_user'] ?? '' );
				$security_settings['ftp_pass']    = sanitize_text_field( $_POST['ftp_pass'] ?? '' );
				$security_settings['ftp_path']    = sanitize_text_field( $_POST['ftp_path'] ?? '/' );
				$security_settings['ftp_enabled'] = isset( $_POST['ftp_enabled'] ) ? 1 : 0;

				update_option( 'vn_security_settings', $security_settings );

				wp_redirect( admin_url( 'admin.php?page=vn-settings&setting_tab=utilities&status=saved' ) );
				exit;

			/* ---- Database Optimize (legacy GET link) ---- */
			case 'optimize_db':
				self::require_nonce( '_wpnonce', 'vn_optimize_db_nonce' );
				VN_Privacy_System_Health::cleanup_database();
				wp_redirect( admin_url( 'admin.php?page=vn-settings&setting_tab=utilities&status=optimized' ) );
				exit;

			/* ---- Restore Backup ---- */
			case 'restore_backup':
				self::require_nonce( 'restore_nonce', 'restore_privacy_backup' );
				if ( ! empty( $_FILES['backup_file']['tmp_name'] ) ) {
					$ok = VN_Privacy_Backup_Manager::restore_backup_zip( $_FILES['backup_file']['tmp_name'] );
					wp_redirect( admin_url( 'admin.php?page=vn-settings&setting_tab=utilities&status=' . ( $ok ? 'restored' : 'restore_failed' ) ) );
					exit;
				}
				wp_die( 'Vui lòng chọn tệp tin sao lưu.' );

		}// end switch
	}

	/* ================================================================
	   Handle file downloads & CSV export (runs on admin_init)
	================================================================ */
	public static function handle_export_action() {
		if ( ! is_admin() || ! current_user_can( 'manage_options' ) ) return;

		$action = $_GET['action'] ?? '';

		/* ---- Export CSV ---- */
		if ( $action === 'vn_privacy_export_csv' ) {
			self::require_nonce( '_wpnonce', 'vn_privacy_export_nonce' );
			$form_id = intval( $_GET['filter_form_id'] ?? 0 );
			$month   = sanitize_text_field( $_GET['filter_month'] ?? '' );
			$entries = VN_Privacy_DB::get_entries( $form_id, $month );

			header( 'Content-Type: text/csv; charset=utf-8' );
			header( 'Content-Disposition: attachment; filename="consent-log-' . date( 'Y-m-d' ) . '.csv"' );
			header( 'Pragma: no-cache' );
			$out = fopen( 'php://output', 'w' );
			fwrite( $out, "\xEF\xBB\xBF" ); // UTF-8 BOM for Excel

			fputcsv( $out, [ 'ID', 'Họ tên', 'Số điện thoại', 'Nội dung', 'Biểu mẫu', 'IP', 'User Agent', 'Thời gian đồng ý' ] );

			$escape = function ( $v ) {
				$v = (string) $v;
				return in_array( substr( $v, 0, 1 ), [ '=', '+', '-', '@' ], true ) ? "'" . $v : $v;
			};

			foreach ( $entries as $e ) {
				fputcsv( $out, [
					$e->id,
					$escape( $e->fullname ),
					$escape( $e->phone ),
					$escape( strip_tags( str_replace( '<br />', ' | ', $e->message ) ) ),
					$escape( $e->form_title ?: 'N/A' ),
					$escape( $e->ip_address ),
					$escape( $e->user_agent ),
					$escape( $e->consent_time ),
				] );
			}
			fclose( $out );
			exit;
		}

		/* ---- Download Backup ZIP ---- */
		if ( $action === 'download_zip' ) {
			self::require_nonce( '_wpnonce', 'download_zip_nonce' );
			$filename = sanitize_file_name( $_GET['file'] ?? '' );
			$upload   = wp_upload_dir();
			$path     = $upload['basedir'] . '/vn-privacy-backups/' . $filename;

			if ( ! file_exists( $path ) ) wp_die( 'Không tìm thấy file sao lưu.' );

			// Prevent script execution timeout
			@set_time_limit( 0 );

			// Clean output buffering to prevent memory errors
			while ( ob_get_level() ) {
				ob_end_clean();
			}

			// Disable compression which interferes with streaming
			if ( function_exists( 'apache_setenv' ) ) {
				@apache_setenv( 'no-gzip', '1' );
			}
			@ini_set( 'zlib.output_compression', '0' );

			$size   = filesize( $path );
			$start  = 0;
			$end    = $size - 1;
			$length = $size;

			// Handle range requests (IDM / Resume)
			if ( isset( $_SERVER['HTTP_RANGE'] ) ) {
				$c_start = $start;
				$c_end   = $end;

				list( , $range ) = explode( '=', $_SERVER['HTTP_RANGE'], 2 );
				if ( strpos( $range, ',' ) !== false ) {
					header( 'HTTP/1.1 416 Requested Range Not Satisfiable' );
					header( "Content-Range: bytes $start-$end/$size" );
					exit;
				}
				if ( $range == '-' ) {
					$c_start = $size - substr( $range, 1 );
				} else {
					$range   = explode( '-', $range );
					$c_start = $range[0];
					$c_end   = ( isset( $range[1] ) && is_numeric( $range[1] ) ) ? $range[1] : $size - 1;
				}
				$c_end = ( $c_end > $end ) ? $end : $c_end;
				if ( $c_start > $c_end || $c_start > $size - 1 || $c_end >= $size ) {
					header( 'HTTP/1.1 416 Requested Range Not Satisfiable' );
					header( "Content-Range: bytes $start-$end/$size" );
					exit;
				}
				$start  = $c_start;
				$end    = $c_end;
				$length = $end - $start + 1;
				header( 'HTTP/1.1 206 Partial Content' );
				header( "Content-Range: bytes $start-$end/$size" );
			}

			$file = fopen( $path, 'rb' );
			if ( $file ) {
				header( 'Content-Type: application/zip' );
				header( 'Content-Disposition: attachment; filename="' . basename( $path ) . '"' );
				header( "Content-Length: $length" );
				header( 'Accept-Ranges: bytes' );
				header( 'Pragma: no-cache' );
				header( 'Cache-Control: must-revalidate, post-check=0, pre-check=0' );
				header( 'X-Accel-Buffering: no' ); // Tells Nginx & Cloudflare not to buffer this response
				header( 'Connection: keep-alive' );

				if ( $start > 0 ) {
					fseek( $file, $start );
				}

				$bytes_sent = 0;
				$chunk_size = 1024 * 64; // 64KB chunks for optimal streaming
				while ( ! feof( $file ) && $bytes_sent < $length ) {
					$buffer = fread( $file, min( $chunk_size, $length - $bytes_sent ) );
					echo $buffer;
					flush();
					$bytes_sent += strlen( $buffer );
				}
				fclose( $file );
				exit;
			}
			wp_die( 'Không thể đọc tệp tin sao lưu.' );
		}

		/* ---- Export System Health Report .txt ---- */
		if ( $action === 'export_system_report' ) {
			self::require_nonce( '_wpnonce', 'vn_export_report_nonce' );
			VN_Privacy_System_Health::export_report(); // Outputs file & exits
		}
	}

	/* ================================================================
	   Private helpers
	================================================================ */
	private static function require_nonce( $field, $action ) {
		$value = $_POST[ $field ] ?? $_GET[ $field ] ?? '';
		if ( ! wp_verify_nonce( $value, $action ) ) {
			wp_die( 'Lỗi bảo mật (Invalid nonce). Vui lòng tải lại trang và thử lại.' );
		}
	}

	private static function parse_fields_json() {
		$raw = trim( $_POST['form_fields_json'] ?? '' );
		if ( empty( $raw ) ) wp_die( 'Thiếu cấu hình các trường biểu mẫu.' );
		$decoded = json_decode( stripslashes( $raw ), true );
		if ( empty( $decoded ) || ! is_array( $decoded ) ) wp_die( 'Dữ liệu cấu hình không hợp lệ.' );
		return json_encode( $decoded );
	}
}
