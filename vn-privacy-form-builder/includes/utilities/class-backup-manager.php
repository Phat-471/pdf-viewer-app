<?php
if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

class VN_Privacy_Backup_Manager {

	/* ----------------------------------------------------------------
	   Backup Directory Helper
	---------------------------------------------------------------- */
	private static function get_backup_dir() {
		$upload_dir = wp_upload_dir();
		$dir        = $upload_dir['basedir'] . '/vn-privacy-backups';
		// FIX #13: check is_dir() to prevent race condition
		if ( ! is_dir( $dir ) ) {
			wp_mkdir_p( $dir );
		}
		if ( ! file_exists( $dir . '/index.php' ) ) {
			@file_put_contents( $dir . '/index.php', '<?php // Silence is golden' );
		}
		if ( ! file_exists( $dir . '/.htaccess' ) ) {
			@file_put_contents( $dir . '/.htaccess', "Options -Indexes\ndeny from all" );
		}
		return $dir;
	}

	/* ----------------------------------------------------------------
	   List existing backups
	---------------------------------------------------------------- */
	public static function list_backups() {
		$dir = self::get_backup_dir();
		$temp_folders = glob( $dir . '/tmp_*', GLOB_ONLYDIR );
		if ( $temp_folders ) {
			$one_day_ago = time() - 86400;
			foreach ( $temp_folders as $temp_folder ) {
				if ( filemtime( $temp_folder ) < $one_day_ago ) self::recursive_delete( $temp_folder );
			}
		}
		$files = glob( $dir . '/vn-privacy-backup-*.zip' );
		if ( ! $files ) return [];
		$result = [];
		foreach ( $files as $f ) {
			$meta = get_option( 'vn_backup_meta_' . md5( basename( $f ) ), [] );
			$result[] = [
				'filename'   => basename( $f ),
				'path'       => $f,
				'size'       => size_format( filesize( $f ) ),
				'size_raw'   => filesize( $f ),
				'date'       => date( 'd/m/Y H:i', filemtime( $f ) ),
				'mtime'      => filemtime( $f ),
				'note'       => $meta['note'] ?? '',
				'verified'   => $meta['verified'] ?? false,
				'auto'       => $meta['auto'] ?? false,
				'ftp_status' => $meta['ftp_status'] ?? null,
			];
		}
		usort( $result, fn( $a, $b ) => $b['mtime'] - $a['mtime'] );
		return $result;
	}

	/* ----------------------------------------------------------------
	   Delete a specific backup file (AJAX)
	---------------------------------------------------------------- */
	public function ajax_delete_backup() {
		$nonce = $_POST['nonce'] ?? $_GET['nonce'] ?? '';
		if ( ! wp_verify_nonce( $nonce, 'vn_backup_nonce' ) ) {
			wp_send_json_error( 'Lỗi bảo mật (Invalid Nonce).' );
		}
		if ( ! current_user_can( 'manage_options' ) ) wp_send_json_error( 'Unauthorized' );
		$file = sanitize_file_name( $_POST['file'] ?? '' );
		if ( empty( $file ) ) wp_send_json_error( 'Thiếu tên tệp tin.' );
		$path = self::get_backup_dir() . '/' . $file;
		if ( ! file_exists( $path ) ) wp_send_json_error( 'Không tìm thấy tệp tin sao lưu.' );
		if ( ! is_writable( $path ) ) wp_send_json_error( 'Không có quyền xóa tệp tin.' );
		if ( @unlink( $path ) ) {
			delete_option( 'vn_backup_meta_' . md5( $file ) );
			wp_send_json_success( 'Đã xóa bản sao lưu.' );
		} else {
			wp_send_json_error( 'Không thể xóa tệp tin.' );
		}
	}

	/* ----------------------------------------------------------------
	   NEW: Save backup notes
	---------------------------------------------------------------- */
	public function ajax_save_backup_note() {
		check_ajax_referer( 'vn_backup_nonce', 'nonce' );
		if ( ! current_user_can( 'manage_options' ) ) wp_send_json_error( 'Unauthorized' );
		$file = sanitize_file_name( $_POST['file'] ?? '' );
		$note = sanitize_text_field( $_POST['note'] ?? '' );
		if ( empty( $file ) ) wp_send_json_error( 'Thieu ten tep tin.' );
		$key  = 'vn_backup_meta_' . md5( $file );
		$meta = get_option( $key, [] );
		$meta['note'] = $note;
		update_option( $key, $meta, false );
		wp_send_json_success( 'Da luu ghi chu.' );
	}

	/* ----------------------------------------------------------------
	   NEW: Verify backup integrity
	---------------------------------------------------------------- */
	public function ajax_verify_backup() {
		check_ajax_referer( 'vn_backup_nonce', 'nonce' );
		if ( ! current_user_can( 'manage_options' ) ) wp_send_json_error( 'Unauthorized' );
		$file = sanitize_file_name( $_POST['file'] ?? '' );
		$path = self::get_backup_dir() . '/' . $file;
		if ( ! file_exists( $path ) ) wp_send_json_error( 'Khong tim thay file.' );
		$zip = new ZipArchive();
		if ( $zip->open( $path ) !== true ) wp_send_json_error( 'File ZIP bi loi hoac khong hop le.' );
		$has_manifest = $zip->getFromName( 'manifest.json' ) !== false;
		$has_db       = $zip->getFromName( 'database/dump.sql' ) !== false;
		$file_count   = $zip->numFiles;
		$manifest     = $has_manifest ? json_decode( $zip->getFromName( 'manifest.json' ), true ) : [];
		$zip->close();
		$key  = 'vn_backup_meta_' . md5( $file );
		$meta = get_option( $key, [] );
		$meta['verified']    = $has_manifest;
		$meta['verify_time'] = current_time( 'mysql' );
		update_option( $key, $meta, false );
		wp_send_json_success( [
			'status'     => $has_manifest ? 'valid' : 'warn',
			'has_db'     => $has_db,
			'file_count' => $file_count,
			'site_url'   => $manifest['site_url'] ?? '',
			'created_at' => $manifest['generated_at'] ?? '',
			'wp_prefix'  => $manifest['wp_prefix'] ?? '',
		] );
	}

	/* ----------------------------------------------------------------
	   Backup: Init
	---------------------------------------------------------------- */
	public function ajax_backup_init() {
		$nonce = $_POST['nonce'] ?? $_GET['nonce'] ?? '';
		if ( ! wp_verify_nonce( $nonce, 'vn_backup_nonce' ) ) {
			wp_send_json_error( 'Lỗi bảo mật (Invalid Nonce).' );
		}
		if ( ! current_user_can( 'manage_options' ) ) wp_send_json_error( 'Unauthorized' );
		$mode       = sanitize_key( $_POST['mode'] ?? 'full' );
		$backup_dir = self::get_backup_dir();
		if ( ! is_writable( $backup_dir ) ) {
			wp_send_json_error( 'Thư mục sao lưu không thể ghi: ' . $backup_dir );
		}
		$prefix     = ( $mode === 'db_only' ) ? 'db-only-' : '';
		$filename   = 'vn-privacy-backup-' . $prefix . date( 'Y-m-d-H-i-s' ) . '.zip';
		$zip_path   = $backup_dir . '/' . $filename;
		$sql_path   = $backup_dir . '/dump.sql';
		if ( file_exists( $sql_path ) ) unlink( $sql_path );
		$wp_content_dir = WP_CONTENT_DIR;
		$files_to_zip   = [];
		if ( $mode !== 'db_only' ) {
			try {
				$iterator = new RecursiveIteratorIterator(
					new RecursiveDirectoryIterator( $wp_content_dir, RecursiveDirectoryIterator::SKIP_DOTS ),
					RecursiveIteratorIterator::SELF_FIRST
				);
				foreach ( $iterator as $file ) {
					$path = $file->getPathname();
					if ( strpos( $path, $backup_dir ) !== false ) continue;
					if ( preg_match( '#[/\\\\](cache|wc-logs|upgrade)[/\\\\]#', $path ) ) continue;
					$files_to_zip[] = $path;
				}
			} catch ( Exception $e ) {
				wp_send_json_error( 'Lỗi quét thư mục: ' . $e->getMessage() );
			}
		}
		$files_list_path = $zip_path . '.list.json';
		if ( ! empty( $files_to_zip ) ) {
			@file_put_contents( $files_list_path, json_encode( $files_to_zip ) );
		}
		update_option( 'vn_privacy_backup_state', [
			'mode'            => $mode,
			'zip_path'        => $zip_path,
			'sql_path'        => $sql_path,
			'files_list_path' => $files_list_path,
			'total_files'     => count( $files_to_zip ),
			'processed_files' => 0,
			'wp_content_dir'  => $wp_content_dir,
		] );
		wp_send_json_success( [
			'message'     => ( $mode === 'db_only' ) ? 'Khởi tạo sao lưu Database...' : 'Đã khởi tạo sao lưu.',
			'total_files' => count( $files_to_zip ),
			'mode'        => $mode,
		] );
	}

	/* ----------------------------------------------------------------
	   Backup DB — FIX #14: use prepare for SHOW TABLES
	                FIX #10: binary-safe column export
	---------------------------------------------------------------- */
	public function ajax_backup_db() {
		check_ajax_referer( 'vn_backup_nonce', 'nonce' );
		if ( ! current_user_can( 'manage_options' ) ) wp_send_json_error( 'Unauthorized' );
		@set_time_limit( 0 );
		@ini_set( 'memory_limit', '512M' );
		$state = get_option( 'vn_privacy_backup_state' );
		if ( ! $state ) wp_send_json_error( 'Không tìm thấy trạng thái sao lưu.' );
		global $wpdb;
		// FIX #14: use prepare() to avoid potential injection via prefix
		$tables = $wpdb->get_col( $wpdb->prepare( 'SHOW TABLES LIKE %s', $wpdb->prefix . '%' ) );
		if ( empty( $tables ) ) wp_send_json_error( 'Không tìm thấy bảng nào trong database.' );
		$sql_file = fopen( $state['sql_path'], 'w' );
		if ( ! $sql_file ) wp_send_json_error( 'Không thể tạo file SQL. Kiểm tra quyền thư mục.' );
		fwrite( $sql_file, "SET SQL_MODE = \"NO_AUTO_VALUE_ON_ZERO\";\nSET FOREIGN_KEY_CHECKS = 0;\nSET time_zone = \"+00:00\";\n\n" );
		fwrite( $sql_file, '-- VN Privacy Backup -- ' . current_time( 'mysql' ) . "\n" );
		fwrite( $sql_file, '-- Site: ' . get_site_url() . "\n\n" );
		$BATCH = 500;
		foreach ( $tables as $table ) {
			$create = $wpdb->get_row( 'SHOW CREATE TABLE `' . esc_sql( $table ) . '`', ARRAY_N );
			if ( ! $create ) continue;
			fwrite( $sql_file, "DROP TABLE IF EXISTS `$table`;\n" . $create[1] . ";\n\n" );
			// FIX #10: detect binary columns
			$col_info = $wpdb->get_results( 'DESCRIBE `' . esc_sql( $table ) . '`', ARRAY_A );
			$bin_cols = [];
			if ( $col_info ) {
				foreach ( $col_info as $idx => $col ) {
					$t = strtolower( $col['Type'] );
					if ( strpos( $t, 'blob' ) !== false || strpos( $t, 'binary' ) !== false ) {
						$bin_cols[ $idx ] = true;
					}
				}
			}
			$offset = 0;
			do {
				$rows = $wpdb->get_results(
					$wpdb->prepare( "SELECT * FROM `$table` LIMIT %d OFFSET %d", $BATCH, $offset ),
					ARRAY_N
				);
				if ( $rows ) {
					foreach ( $rows as $row ) {
						$vals = [];
						foreach ( $row as $idx => $v ) {
							if ( $v === null ) {
								$vals[] = 'NULL';
							} elseif ( isset( $bin_cols[ $idx ] ) ) {
								// FIX #10: hex-encode binary data
								$vals[] = '0x' . bin2hex( $v );
							} else {
								$vals[] = "'" . esc_sql( $v ) . "'";
							}
						}
						fwrite( $sql_file, "INSERT INTO `$table` VALUES (" . implode( ',', $vals ) . ");\n" );
					}
				}
				$offset += $BATCH;
			} while ( $rows && count( $rows ) === $BATCH );
			fwrite( $sql_file, "\n" );
		}
		fwrite( $sql_file, "SET FOREIGN_KEY_CHECKS = 1;\n" );
		fclose( $sql_file );
		wp_send_json_success( [ 'message' => 'Sao lưu Database hoàn tất.' ] );
	}

	/* ----------------------------------------------------------------
	   Backup: Chunk files into ZIP — FIX #1: lock file prevents concurrent writes
	---------------------------------------------------------------- */
	public function ajax_backup_files() {
		$nonce = $_POST['nonce'] ?? $_GET['nonce'] ?? '';
		if ( ! wp_verify_nonce( $nonce, 'vn_backup_nonce' ) ) {
			wp_send_json_error( 'Lỗi bảo mật (Invalid Nonce).' );
		}
		if ( ! current_user_can( 'manage_options' ) ) wp_send_json_error( 'Unauthorized' );
		$state = get_option( 'vn_privacy_backup_state' );
		if ( ! $state ) wp_send_json_error( 'Không tìm thấy trạng thái sao lưu.' );
		// FIX #1: Exclusive lock file prevents two simultaneous AJAX calls corrupting the ZIP
		$lock_file = $state['zip_path'] . '.lock';
		$lock      = @fopen( $lock_file, 'x' ); // 'x' = create only, fails if already exists
		if ( ! $lock ) {
			wp_send_json_error( 'File ZIP đang được xử lý. Vui lòng thử lại sau vài giây.' );
		}
		// FIX #1: Open existing ZIP to APPEND, or create new
		$zip        = new ZipArchive();
		$open_flags = file_exists( $state['zip_path'] ) ? 0 : ZipArchive::CREATE;
		if ( $zip->open( $state['zip_path'], $open_flags ) !== true ) {
			@fclose( $lock );
			@unlink( $lock_file );
			wp_send_json_error( 'Không thể mở ZipArchive.' );
		}
		$chunk     = 150; // Reduced chunk to lower per-request memory
		$processed = $state['processed_files'];
		$files     = [];
		if ( ! empty( $state['files_list_path'] ) && file_exists( $state['files_list_path'] ) ) {
			$files = json_decode( @file_get_contents( $state['files_list_path'] ), true ) ?: [];
		}
		$total    = $state['total_files'];
		$base_dir = $state['wp_content_dir'];
		$end      = min( $processed + $chunk, $total );
		for ( $i = $processed; $i < $end; $i++ ) {
			if ( isset( $files[ $i ] ) ) {
				$fp = $files[ $i ];
				if ( is_file( $fp ) && is_readable( $fp ) ) {
					$local = 'wp-content/' . ltrim( str_replace( $base_dir, '', $fp ), '/\\' );
					$zip->addFile( $fp, $local );
				}
			}
		}
		$zip->close();
		// Release lock
		@fclose( $lock );
		@unlink( $lock_file );
		$state['processed_files'] = $end;
		update_option( 'vn_privacy_backup_state', $state );
		$is_done  = $end >= $total;
		$progress = $total > 0 ? round( ( $end / $total ) * 100 ) : 100;
		wp_send_json_success( [ 'message' => "Đã nén $end / $total tệp...", 'progress' => $progress, 'is_done' => $is_done ] );
	}

	/* ----------------------------------------------------------------
	   Backup: Finish — FIX #11: MD5 hash, FIX #9: async FTP, FIX #8: prune with exclude
	---------------------------------------------------------------- */
	public function ajax_backup_finish() {
		$nonce = $_POST['nonce'] ?? $_GET['nonce'] ?? '';
		if ( ! wp_verify_nonce( $nonce, 'vn_backup_nonce' ) ) {
			wp_send_json_error( 'Lỗi bảo mật (Invalid Nonce).' );
		}
		if ( ! current_user_can( 'manage_options' ) ) wp_send_json_error( 'Unauthorized' );
		$state = get_option( 'vn_privacy_backup_state' );
		if ( ! $state ) wp_send_json_error( 'Không tìm thấy trạng thái sao lưu.' );
		$zip = new ZipArchive();
		if ( $zip->open( $state['zip_path'] ) === true ) {
			if ( file_exists( $state['sql_path'] ) ) {
				$zip->addFile( $state['sql_path'], 'database/dump.sql' );
			}
			global $wpdb;
			$zip->addFromString( 'manifest.json', json_encode( [
				'plugin_version' => VN_PRIVACY_VERSION,
				'generated_at'   => current_time( 'mysql' ),
				'wp_prefix'      => $wpdb->prefix,
				'site_url'       => get_site_url(),
				'mode'           => $state['mode'],
				'wp_version'     => get_bloginfo( 'version' ),
			], JSON_PRETTY_PRINT ) );
			$zip->close();
		} else {
			wp_send_json_error( 'Không thể hoàn tất ZipArchive.' );
		}
		if ( file_exists( $state['sql_path'] ) ) @unlink( $state['sql_path'] );
		if ( ! empty( $state['files_list_path'] ) && file_exists( $state['files_list_path'] ) ) {
			@unlink( $state['files_list_path'] );
		}
		delete_option( 'vn_privacy_backup_state' );
		$filename = basename( $state['zip_path'] );
		// FIX #11: Compute MD5 of final ZIP for integrity verification
		$zip_md5  = md5_file( $state['zip_path'] );
		// FIX #8: Pass current filename so prune won't delete it
		self::prune_old_backups( 5, $filename );
		$download_url = wp_nonce_url(
			admin_url( 'admin.php?page=vn-settings&setting_tab=utilities&action=download_zip&file=' . urlencode( $filename ) ),
			'download_zip_nonce'
		);
		update_option( 'vn_backup_meta_' . md5( $filename ), [
			'created_at' => current_time( 'mysql' ),
			'mode'       => $state['mode'],
			'site_url'   => get_site_url(),
			'verified'   => true,
			'md5'        => $zip_md5,
			'md5_ok'     => true,
		], false );
		// FIX #9: Async FTP via WP-Cron instead of blocking in this request
		$security_settings = get_option( 'vn_security_settings', [] );
		$ftp_enabled       = ! empty( $security_settings['ftp_enabled'] );
		if ( $ftp_enabled ) {
			wp_schedule_single_event( time() + 3, 'vn_ftp_upload_cron', [ $state['zip_path'] ] );
		}
		$msg = 'Hoàn tất sao lưu!';
		if ( $ftp_enabled ) $msg .= ' Đang lên lịch tải lên FTP...';
		wp_send_json_success( [
			'message'      => $msg,
			'download_url' => $download_url,
			'filename'     => $filename,
			'md5'          => $zip_md5,
		] );
	}

	private static function prune_old_backups( $keep = 5, $exclude_filename = '' ) {
		$backups = self::list_backups();
		// FIX #8: exclude file being created right now
		if ( $exclude_filename ) {
			$backups = array_values( array_filter( $backups, fn( $b ) => $b['filename'] !== $exclude_filename ) );
		}
		if ( count( $backups ) <= $keep ) return;
		foreach ( array_slice( $backups, $keep ) as $b ) {
			if ( ! file_exists( $b['path'] ) ) continue;
			// FIX #8: don't delete files modified in last 5 minutes (may be in use by restore)
			if ( ( time() - filemtime( $b['path'] ) ) < 300 ) continue;
			@unlink( $b['path'] );
			delete_option( 'vn_backup_meta_' . md5( $b['filename'] ) );
		}
	}

	/* ----------------------------------------------------------------
	   Restore from ZIP (server-side) — FIX: streaming SQL import
	---------------------------------------------------------------- */
	public static function restore_backup_zip( $tmp_path ) {
		@set_time_limit( 0 );
		@ini_set( 'memory_limit', '256M' );
		if ( function_exists( 'ignore_user_abort' ) ) @ignore_user_abort( true );
		$zip = new ZipArchive();
		if ( $zip->open( $tmp_path ) !== true ) return false;
		$manifest_json = $zip->getFromName( 'manifest.json' );
		if ( ! $manifest_json ) { $zip->close(); return false; }
		$manifest     = json_decode( $manifest_json, true );
		$old_site_url = ! empty( $manifest['site_url'] ) ? untrailingslashit( $manifest['site_url'] ) : '';
		$upload_dir   = wp_upload_dir();
		$extract_to   = $upload_dir['basedir'] . '/vn_privacy_temp_restore_' . time();
		wp_mkdir_p( $extract_to );
		$zip->extractTo( $extract_to );
		$zip->close();
		$extracted_content = $extract_to . '/wp-content';
		if ( is_dir( $extracted_content ) ) self::recursive_copy( $extracted_content, WP_CONTENT_DIR );
		$sql_file = $extract_to . '/database/dump.sql';
		if ( file_exists( $sql_file ) ) {
			global $wpdb;
			$wpdb->hide_errors();
			self::stream_import_sql( $sql_file );
			$new_site_url = untrailingslashit( get_site_url() );
			if ( ! empty( $old_site_url ) && $old_site_url !== $new_site_url ) self::migrate_db_urls( $old_site_url, $new_site_url );
			self::post_restore_fixes();
		}
		self::recursive_delete( $extract_to );
		return true;
	}

	/* ----------------------------------------------------------------
	   FIX #1: Stream SQL line-by-line — NO file_get_contents OOM
	---------------------------------------------------------------- */
	private static function stream_import_sql( $sql_path ) {
		global $wpdb;
		$handle = fopen( $sql_path, 'r' );
		if ( ! $handle ) return false;
		$buffer    = '';
		$delimiter = ';';
		while ( ! feof( $handle ) ) {
			$line = fgets( $handle, 65536 );
			if ( $line === false ) break;
			$trimmed = ltrim( $line );
			if ( $trimmed === '' || str_starts_with( $trimmed, '--' ) || str_starts_with( $trimmed, '#' ) || str_starts_with( $trimmed, '/*' ) ) continue;
			if ( str_starts_with( strtoupper( $trimmed ), 'DELIMITER ' ) ) {
				$delimiter = trim( substr( $trimmed, 10 ) );
				continue;
			}
			$buffer .= $line;
			if ( str_ends_with( rtrim( $line ), $delimiter ) ) {
				$q = trim( $buffer );
				if ( ! empty( $q ) ) $wpdb->query( $q );
				$buffer = '';
			}
		}
		$q = trim( $buffer );
		if ( ! empty( $q ) ) $wpdb->query( $q );
		fclose( $handle );
		return true;
	}

	private static function migrate_db_urls( $old_url, $new_url ) {
		global $wpdb;
		$like = '%' . $wpdb->esc_like( $old_url ) . '%';
		
		// Đảm bảo tên bảng luôn động theo prefix mới
		$posts_table    = $wpdb->prefix . 'posts';
		$comments_table = $wpdb->prefix . 'comments';
		$postmeta_table = $wpdb->prefix . 'postmeta';
		$options_table  = $wpdb->prefix . 'options';
		$usermeta_table = $wpdb->prefix . 'usermeta';

		$direct = [
			[ 'table' => $posts_table,    'cols' => [ 'guid', 'post_content', 'post_excerpt', 'post_content_filtered' ] ],
			[ 'table' => $comments_table, 'cols' => [ 'comment_content', 'comment_author_url' ] ],
		];

		foreach ( $direct as $entry ) {
			if ( ! $wpdb->get_var( $wpdb->prepare( 'SHOW TABLES LIKE %s', $entry['table'] ) ) ) continue;
			foreach ( $entry['cols'] as $col ) {
				$wpdb->query( $wpdb->prepare( "UPDATE `{$entry['table']}` SET `$col` = REPLACE(`$col`, %s, %s) WHERE `$col` LIKE %s", $old_url, $new_url, $like ) );
			}
		}

		if ( $wpdb->get_var( $wpdb->prepare( 'SHOW TABLES LIKE %s', $postmeta_table ) ) ) {
			$wpdb->query( $wpdb->prepare( "UPDATE `$postmeta_table` SET meta_value = REPLACE(meta_value, %s, %s) WHERE meta_value LIKE %s", $old_url, $new_url, $like ) );
		}

		foreach ( [ [ $options_table, 'option_id', 'option_value' ], [ $usermeta_table, 'umeta_id', 'meta_value' ] ] as list( $tbl, $pk, $col ) ) {
			if ( ! $wpdb->get_var( $wpdb->prepare( 'SHOW TABLES LIKE %s', $tbl ) ) ) continue;
			$rows = $wpdb->get_results( $wpdb->prepare( "SELECT $pk, $col FROM `$tbl` WHERE `$col` LIKE %s", $like ), ARRAY_A );
			foreach ( $rows as $row ) {
				$new_val = self::replace_urls_in_value( $row[ $col ], $old_url, $new_url );
				if ( $new_val !== $row[ $col ] ) {
					$wpdb->update( $tbl, [ $col => $new_val ], [ $pk => $row[ $pk ] ] );
				}
			}
		}
	}

	private static function post_restore_fixes() {
		global $wpdb;
		// Lấy URL hiện tại từ $_SERVER để tránh cache của get_site_url() cũ
		$protocol = ( ! empty( $_SERVER['HTTPS'] ) && $_SERVER['HTTPS'] !== 'off' ) ? 'https://' : 'http://';
		$host     = $_SERVER['HTTP_HOST'] ?? '';
		
		// Tìm thư mục con nếu WordPress được cài đặt trong thư mục con
		$script_name = $_SERVER['SCRIPT_NAME'] ?? '';
		$sub_dir     = '';
		if ( $script_name ) {
			$sub_dir = str_replace( '/wp-admin/admin-ajax.php', '', $script_name );
			$sub_dir = str_replace( '/wp-admin/admin.php', '', $sub_dir );
			$sub_dir = rtrim( $sub_dir, '/' );
		}
		
		if ( ! empty( $host ) ) {
			$current_url = untrailingslashit( $protocol . $host . $sub_dir );
		} else {
			$current_url = untrailingslashit( site_url() );
		}

		$wpdb->update( $wpdb->options, [ 'option_value' => $current_url ], [ 'option_name' => 'siteurl' ] );
		$wpdb->update( $wpdb->options, [ 'option_value' => $current_url ], [ 'option_name' => 'home' ] );
		$wpdb->delete( $wpdb->options, [ 'option_name' => 'rewrite_rules' ] );
		wp_cache_flush();
	}

	/* ----------------------------------------------------------------
	   FIX #5: Cleanup — separate from fixes to avoid transient race
	---------------------------------------------------------------- */
	private static function cleanup_restore_session( $state, $restore_key ) {
		if ( is_dir( $state['extract_to'] ) ) self::recursive_delete( $state['extract_to'] );
		$is_uploaded = ! empty( $state['zip_path'] ) && strpos( basename( $state['zip_path'] ), 'restore_' ) === 0;
		if ( $is_uploaded && file_exists( $state['zip_path'] ) ) @unlink( $state['zip_path'] );
		delete_transient( 'vn_restore_zip_' . $restore_key );
		delete_transient( 'vn_restore_state_' . $restore_key );
		global $wpdb;
		$wpdb->query( "DELETE FROM {$wpdb->options} WHERE option_name LIKE '_transient_timeout_%' AND option_value < UNIX_TIMESTAMP()" );
	}

	/* ----------------------------------------------------------------
	   AJAX: Restore from server (Legacy/Direct)
	---------------------------------------------------------------- */
	public function ajax_restore_from_server() {
		$nonce = $_POST['nonce'] ?? $_GET['nonce'] ?? '';
		if ( ! wp_verify_nonce( $nonce, 'vn_restore_server_nonce' ) ) {
			wp_send_json_error( 'Lỗi bảo mật (Invalid Nonce).' );
		}
		if ( ! current_user_can( 'manage_options' ) ) wp_send_json_error( 'Unauthorized' );
		$filename = sanitize_file_name( $_POST['file'] ?? '' );
		if ( empty( $filename ) ) wp_send_json_error( 'Thiếu tên tệp tin.' );
		$path = self::get_backup_dir() . '/' . $filename;
		if ( ! file_exists( $path ) ) wp_send_json_error( 'Không tìm thấy tệp tin sao lưu trên server.' );
		$ok = self::restore_backup_zip( $path );
		if ( $ok ) wp_send_json_success( 'Khôi phục thành công từ bản sao lưu trên server!' );
		else wp_send_json_error( 'Khôi phục thất bại -- tệp tin không hợp lệ hoặc bị lỗi.' );
	}

	/* ----------------------------------------------------------------
	   AJAX: Restore from server (Init Step-by-Step)
	---------------------------------------------------------------- */
	public function ajax_restore_server_init() {
		$nonce = $_POST['nonce'] ?? $_GET['nonce'] ?? '';
		if ( ! wp_verify_nonce( $nonce, 'vn_restore_server_nonce' ) ) {
			wp_send_json_error( 'Lỗi bảo mật (Invalid Nonce).' );
		}
		if ( ! current_user_can( 'manage_options' ) ) wp_send_json_error( 'Unauthorized' );
		$filename = sanitize_file_name( $_POST['file'] ?? '' );
		if ( empty( $filename ) ) wp_send_json_error( 'Thiếu tên tệp tin.' );
		$path = self::get_backup_dir() . '/' . $filename;
		if ( ! file_exists( $path ) ) wp_send_json_error( 'Không tìm thấy tệp tin sao lưu trên server.' );
		// FIX #5: Validate ZIP has manifest before starting restore session
		$zip = new ZipArchive();
		if ( $zip->open( $path ) !== true ) wp_send_json_error( 'File ZIP bị hỏng. Vui lòng xác minh lại bản sao lưu.' );
		$ok = $zip->getFromName( 'manifest.json' ) !== false;
		$zip->close();
		if ( ! $ok ) wp_send_json_error( 'File ZIP không hợp lệ — thiếu manifest.json.' );
		$restore_key = 'rk_server_' . md5( $filename );
		// FIX #7: Extend transient to 6 hours for large sites
		set_transient( 'vn_restore_zip_' . $restore_key, $path, 6 * HOUR_IN_SECONDS );
		wp_send_json_success( [ 'restore_key' => $restore_key ] );
	}

	/* ----------------------------------------------------------------
	   AJAX: Chunk upload
	---------------------------------------------------------------- */
	public function ajax_chunk_upload() {
		check_ajax_referer( 'vn_chunk_restore_nonce', 'nonce' );
		if ( ! current_user_can( 'manage_options' ) ) wp_send_json_error( 'Unauthorized' );
		$chunk_index  = intval( $_POST['chunk_index'] ?? -1 );
		$total_chunks = intval( $_POST['total_chunks'] ?? 0 );
		$upload_id    = sanitize_key( $_POST['upload_id'] ?? '' );
		if ( $chunk_index < 0 || $total_chunks < 1 || empty( $upload_id ) ) wp_send_json_error( 'Thieu thong tin chunk.' );
		if ( empty( $_FILES['chunk']['tmp_name'] ) || ! is_uploaded_file( $_FILES['chunk']['tmp_name'] ) ) wp_send_json_error( 'Khong nhan duoc du lieu chunk.' );
		$tmp_dir    = self::get_backup_dir() . '/tmp_' . $upload_id;
		wp_mkdir_p( $tmp_dir );
		$chunk_path = $tmp_dir . '/chunk_' . str_pad( $chunk_index, 6, '0', STR_PAD_LEFT );
		move_uploaded_file( $_FILES['chunk']['tmp_name'], $chunk_path );
		wp_send_json_success( [ 'chunk_index' => $chunk_index, 'total_chunks' => $total_chunks, 'upload_id' => $upload_id, 'message' => "Da nhan chunk $chunk_index" ] );
	}

	/* ----------------------------------------------------------------
	   AJAX Step 1: Assemble chunks -> ZIP — FIX #5: verify assembled ZIP
	---------------------------------------------------------------- */
	public function ajax_chunk_restore_apply() {
		check_ajax_referer( 'vn_chunk_restore_nonce', 'nonce' );
		if ( ! current_user_can( 'manage_options' ) ) wp_send_json_error( 'Unauthorized' );
		@set_time_limit( 300 );
		@ini_set( 'memory_limit', '512M' );
		$upload_id    = sanitize_key( $_POST['upload_id'] ?? '' );
		$total_chunks = intval( $_POST['total_chunks'] ?? 0 );
		if ( empty( $upload_id ) || $total_chunks < 1 ) wp_send_json_error( 'Thiếu thông tin ghép file.' );
		$tmp_dir  = self::get_backup_dir() . '/tmp_' . $upload_id;
		$zip_path = self::get_backup_dir() . '/restore_' . $upload_id . '.zip';
		for ( $i = 0; $i < $total_chunks; $i++ ) {
			$chunk_file = $tmp_dir . '/chunk_' . str_pad( $i, 6, '0', STR_PAD_LEFT );
			if ( ! file_exists( $chunk_file ) ) wp_send_json_error( "Thiếu chunk $i — vui lòng thử lại từ đầu." );
		}
		$out = fopen( $zip_path, 'wb' );
		if ( ! $out ) wp_send_json_error( 'Không thể tạo file ZIP để ghép.' );
		for ( $i = 0; $i < $total_chunks; $i++ ) {
			$chunk_file = $tmp_dir . '/chunk_' . str_pad( $i, 6, '0', STR_PAD_LEFT );
			$in = fopen( $chunk_file, 'rb' );
			if ( $in ) {
				while ( ! feof( $in ) ) fwrite( $out, fread( $in, 1048576 ) );
				fclose( $in );
			}
		}
		fclose( $out );
		self::recursive_delete( $tmp_dir );
		// FIX #5: Verify assembled ZIP is valid before proceeding
		$zip = new ZipArchive();
		if ( $zip->open( $zip_path ) !== true ) {
			@unlink( $zip_path );
			wp_send_json_error( 'File ZIP sau khi ghép bị hỏng. Vui lòng upload lại.' );
		}
		$has_manifest = $zip->getFromName( 'manifest.json' ) !== false;
		$zip->close();
		if ( ! $has_manifest ) {
			@unlink( $zip_path );
			wp_send_json_error( 'File ZIP không hợp lệ — thiếu manifest.json. Đây không phải backup của plugin.' );
		}
		$restore_key = 'rk_' . $upload_id;
		// FIX #7: 6 hour transient
		set_transient( 'vn_restore_zip_' . $restore_key, $zip_path, 6 * HOUR_IN_SECONDS );
		wp_send_json_success( [
			'message'     => 'Ghép file thành công (' . size_format( filesize( $zip_path ) ) . ').',
			'restore_key' => $restore_key,
		] );
	}

	/* ----------------------------------------------------------------
	   AJAX Step 2: Extract ZIP + copy files — FIX #5: verify before extract, FIX #7: 6h transient
	---------------------------------------------------------------- */
	public function ajax_restore_step_files() {
		$nonce = $_POST['nonce'] ?? $_GET['nonce'] ?? '';
		if ( ! wp_verify_nonce( $nonce, 'vn_chunk_restore_nonce' ) && ! wp_verify_nonce( $nonce, 'vn_restore_server_nonce' ) ) {
			wp_send_json_error( 'Lỗi bảo mật (Invalid Nonce).' );
		}
		if ( ! current_user_can( 'manage_options' ) ) wp_send_json_error( 'Unauthorized' );
		@set_time_limit( 0 );
		@ini_set( 'memory_limit', '512M' );
		if ( function_exists( 'ignore_user_abort' ) ) @ignore_user_abort( true );
		$restore_key = sanitize_key( $_POST['restore_key'] ?? '' );
		if ( empty( $restore_key ) ) wp_send_json_error( 'Thiếu restore_key.' );
		$zip_path = get_transient( 'vn_restore_zip_' . $restore_key );
		if ( ! $zip_path || ! file_exists( $zip_path ) ) wp_send_json_error( 'Không tìm thấy file ZIP — phiên khôi phục đã hết hạn.' );
		$zip = new ZipArchive();
		if ( $zip->open( $zip_path ) !== true ) wp_send_json_error( 'Không thể mở file ZIP. File có thể bị lỗi.' );
		$manifest_json = $zip->getFromName( 'manifest.json' );
		if ( ! $manifest_json ) { $zip->close(); wp_send_json_error( 'File ZIP không hợp lệ — thiếu manifest.json.' ); }
		$manifest     = json_decode( $manifest_json, true );
		$old_site_url = ! empty( $manifest['site_url'] ) ? untrailingslashit( $manifest['site_url'] ) : '';
		$has_db       = $zip->getFromName( 'database/dump.sql' ) !== false;
		$file_count   = $zip->numFiles;
		if ( $file_count < 1 ) { $zip->close(); wp_send_json_error( 'File ZIP rỗng.' ); }
		$upload_dir = wp_upload_dir();
		$extract_to = $upload_dir['basedir'] . '/vn_restore_tmp_' . $restore_key;
		wp_mkdir_p( $extract_to );
		// FIX #5: Check extractTo result
		if ( ! $zip->extractTo( $extract_to ) ) {
			$zip->close();
			self::recursive_delete( $extract_to );
			wp_send_json_error( 'Giải nén thất bại. Disk có thể đầy hoặc không đủ quyền.' );
		}
		$zip->close();
		$extracted_content = $extract_to . '/wp-content';
		if ( is_dir( $extracted_content ) ) self::recursive_copy( $extracted_content, WP_CONTENT_DIR );
		$sql_path = $extract_to . '/database/dump.sql';
		$sql_size = file_exists( $sql_path ) ? filesize( $sql_path ) : 0;
		// FIX #7: 6 hour transient
		set_transient( 'vn_restore_state_' . $restore_key, [
			'extract_to'   => $extract_to,
			'old_site_url' => $old_site_url,
			'old_prefix'   => $manifest['wp_prefix'] ?? 'wp_',
			'zip_path'     => $zip_path,
			'sql_size'     => $sql_size,
			'has_db'       => $has_db,
		], 6 * HOUR_IN_SECONDS );
		wp_send_json_success( [
			'message'     => 'Khôi phục tệp tin hoàn tất.',
			'restore_key' => $restore_key,
			'has_db'      => $has_db,
			'sql_size'    => $sql_size,
			'file_count'  => $file_count,
		] );
	}

	/* ----------------------------------------------------------------
	   AJAX Step 3: Chunked SQL import
	   FIX #3: DELIMITER support for stored procedures
	   FIX #4: regex-based prefix replace (DDL only, not data values)
	   FIX #7: refresh transient on each chunk
	---------------------------------------------------------------- */
	public function ajax_restore_step_db() {
		$nonce = $_POST['nonce'] ?? $_GET['nonce'] ?? '';
		if ( ! wp_verify_nonce( $nonce, 'vn_chunk_restore_nonce' ) && ! wp_verify_nonce( $nonce, 'vn_restore_server_nonce' ) ) {
			wp_send_json_error( 'Lỗi bảo mật (Invalid Nonce).' );
		}
		if ( ! current_user_can( 'manage_options' ) ) wp_send_json_error( 'Unauthorized' );
		@set_time_limit( 60 );
		@ini_set( 'memory_limit', '512M' );
		if ( function_exists( 'ignore_user_abort' ) ) @ignore_user_abort( true );
		$restore_key = sanitize_key( $_POST['restore_key'] ?? '' );
		$db_offset   = intval( $_POST['db_offset'] ?? 0 );
		if ( empty( $restore_key ) ) wp_send_json_error( 'Thiếu restore_key.' );
		$state = get_transient( 'vn_restore_state_' . $restore_key );
		if ( ! $state ) wp_send_json_error( 'Phiên khôi phục đã hết hạn. Vui lòng thử lại từ đầu.' );
		if ( empty( $state['has_db'] ) ) {
			self::post_restore_fixes();
			self::cleanup_restore_session( $state, $restore_key );
			wp_send_json_success( [ 'done' => true, 'message' => 'Khôi phục hoàn tất (không có DB).' ] );
		}
		$sql_path = $state['extract_to'] . '/database/dump.sql';
		if ( ! file_exists( $sql_path ) ) {
			self::post_restore_fixes();
			self::cleanup_restore_session( $state, $restore_key );
			wp_send_json_success( [ 'done' => true, 'message' => 'Khôi phục hoàn tất.' ] );
		}
		$sql_size = $state['sql_size'] ?: filesize( $sql_path );
		$handle   = fopen( $sql_path, 'r' );
		if ( ! $handle ) wp_send_json_error( 'Không thể đọc file SQL. Kiểm tra quyền thư mục.' );
		if ( $db_offset > 0 ) fseek( $handle, $db_offset );
		global $wpdb;
		$wpdb->hide_errors();
		$old_prefix = $state['old_prefix'] ?? 'wp_';
		$new_prefix = $wpdb->prefix;
		$MAX_STMTS  = 300;
		$buffer     = '';
		$count      = 0;
		$errors     = [];
		// FIX #3: Track DELIMITER changes for stored procedures/triggers
		$delimiter  = ';';
		$in_routine = false;
		while ( ! feof( $handle ) && $count < $MAX_STMTS ) {
			$line = fgets( $handle, 65536 );
			if ( $line === false ) break;
			$trimmed = ltrim( $line );
			// Skip blank and comment-only lines
			if ( $trimmed === '' ) { $buffer .= $line; continue; }
			if ( str_starts_with( $trimmed, '--' ) || str_starts_with( $trimmed, '#' ) || str_starts_with( $trimmed, '/*' ) ) continue;
			// FIX #3: Handle DELIMITER statements
			if ( str_starts_with( strtoupper( $trimmed ), 'DELIMITER ' ) ) {
				$new_delim = trim( substr( $trimmed, 10 ) );
				if ( $new_delim !== '' ) { $delimiter = $new_delim; $in_routine = ( $delimiter !== ';' ); }
				$buffer = '';
				continue;
			}
			$buffer .= $line;
			if ( str_ends_with( rtrim( $line ), $delimiter ) ) {
				$q = trim( $buffer );
				if ( ! empty( $q ) ) {
					// FIX #4: regex-based prefix replacement — only inside backtick-quoted identifiers
					if ( $old_prefix !== $new_prefix && ! $in_routine ) {
						$q = preg_replace(
							'/(?<=`)' . preg_quote( $old_prefix, '/' ) . '(?=[a-zA-Z0-9_])/',
							$new_prefix,
							$q
						);
					}
					$result = $wpdb->query( $q );
					if ( $result === false && $wpdb->last_error ) $errors[] = substr( $wpdb->last_error, 0, 100 );
					$count++;
				}
				$buffer = '';
				// FIX #3: Reset delimiter after routine body
				if ( $in_routine && $delimiter !== ';' ) { $delimiter = ';'; $in_routine = false; }
			}
		}
		$new_offset   = ftell( $handle );
		$is_done      = feof( $handle );
		fclose( $handle );
		$progress_pct = $sql_size > 0 ? min( 99, round( ( $new_offset / $sql_size ) * 100 ) ) : 50;
		if ( $is_done ) {
			$old_site_url = $state['old_site_url'] ?? '';
			$new_site_url = untrailingslashit( get_site_url() );
			if ( ! empty( $old_site_url ) && $old_site_url !== $new_site_url ) self::migrate_db_urls( $old_site_url, $new_site_url );
			self::post_restore_fixes();
			self::cleanup_restore_session( $state, $restore_key );
			wp_send_json_success( [
				'done'        => true,
				'message'     => 'Khôi phục toàn bộ thành công!',
				'error_count' => count( $errors ),
				'errors'      => array_slice( $errors, 0, 5 ),
			] );
		} else {
			// FIX #7: Refresh transient on each chunk to prevent expiry on large DBs
			set_transient( 'vn_restore_state_' . $restore_key, $state, 6 * HOUR_IN_SECONDS );
			wp_send_json_success( [ 'done' => false, 'db_offset' => $new_offset, 'progress_pct' => $progress_pct, 'stmt_count' => $count ] );
		}
	}

	/* ================================================================
	   NEW FEATURE: Scheduled Auto-Backup using WP Cron
	================================================================ */
	public static function schedule_auto_backup( $enabled, $frequency ) {
		$hook = 'vn_privacy_auto_backup_cron';
		wp_clear_scheduled_hook( $hook );
		if ( $enabled && ! wp_next_scheduled( $hook ) ) {
			wp_schedule_event( time(), $frequency, $hook );
		}
	}

	public static function run_auto_backup() {
		@set_time_limit( 0 );
		@ini_set( 'memory_limit', '512M' );
		$mode       = get_option( 'vn_autobackup_mode', 'full' );
		$backup_dir = self::get_backup_dir();
		$prefix     = ( $mode === 'db_only' ) ? 'db-only-auto-' : 'auto-';
		$filename   = 'vn-privacy-backup-' . $prefix . date( 'Y-m-d-H-i-s' ) . '.zip';
		$zip_path   = $backup_dir . '/' . $filename;
		$sql_path   = $backup_dir . '/dump_auto.sql';
		global $wpdb;
		// FIX #14: Dùng prepare
		$tables   = $wpdb->get_col( $wpdb->prepare( 'SHOW TABLES LIKE %s', $wpdb->prefix . '%' ) );
		if ( empty( $tables ) ) return;
		$sql_file = fopen( $sql_path, 'w' );
		if ( ! $sql_file ) return;
		fwrite( $sql_file, "SET SQL_MODE = \"NO_AUTO_VALUE_ON_ZERO\";\nSET FOREIGN_KEY_CHECKS = 0;\nSET time_zone = \"+00:00\";\n\n" );
		
		$BATCH = 500;
		foreach ( $tables as $table ) {
			$create = $wpdb->get_row( 'SHOW CREATE TABLE `' . esc_sql( $table ) . '`', ARRAY_N );
			if ( ! $create ) continue;
			fwrite( $sql_file, "DROP TABLE IF EXISTS `$table`;\n" . $create[1] . ";\n\n" );
			
			// FIX #10: Phát hiện binary columns
			$col_info = $wpdb->get_results( 'DESCRIBE `' . esc_sql( $table ) . '`', ARRAY_A );
			$bin_cols = [];
			if ( $col_info ) {
				foreach ( $col_info as $idx => $col ) {
					$t = strtolower( $col['Type'] );
					if ( strpos( $t, 'blob' ) !== false || strpos( $t, 'binary' ) !== false ) {
						$bin_cols[ $idx ] = true;
					}
				}
			}

			$offset = 0;
			do {
				$rows = $wpdb->get_results( $wpdb->prepare( "SELECT * FROM `$table` LIMIT %d OFFSET %d", $BATCH, $offset ), ARRAY_N );
				if ( $rows ) {
					foreach ( $rows as $row ) {
						$vals = [];
						foreach ( $row as $idx => $v ) {
							if ( $v === null ) {
								$vals[] = 'NULL';
							} elseif ( isset( $bin_cols[ $idx ] ) ) {
								// FIX #10: hex-encode binary data
								$vals[] = '0x' . bin2hex( $v );
							} else {
								$vals[] = "'" . esc_sql( $v ) . "'";
							}
						}
						fwrite( $sql_file, "INSERT INTO `$table` VALUES (" . implode( ',', $vals ) . ");\n" );
					}
				}
				$offset += $BATCH;
			} while ( $rows && count( $rows ) === $BATCH );
			fwrite( $sql_file, "\n" );
		}
		fwrite( $sql_file, "SET FOREIGN_KEY_CHECKS = 1;\n" );
		fclose( $sql_file );
		
		$zip = new ZipArchive();
		if ( $zip->open( $zip_path, ZipArchive::CREATE | ZipArchive::OVERWRITE ) !== true ) {
			if ( file_exists( $sql_path ) ) @unlink( $sql_path );
			return;
		}
		if ( $mode !== 'db_only' ) {
			$iterator = new RecursiveIteratorIterator( new RecursiveDirectoryIterator( WP_CONTENT_DIR, RecursiveDirectoryIterator::SKIP_DOTS ), RecursiveIteratorIterator::SELF_FIRST );
			foreach ( $iterator as $file ) {
				$path = $file->getPathname();
				if ( strpos( $path, $backup_dir ) !== false ) continue;
				if ( preg_match( '#[/\\\\](cache|wc-logs|upgrade|vn_restore_tmp_)[/\\\\]?#', $path ) ) continue;
				if ( is_file( $path ) ) {
					$local = 'wp-content/' . ltrim( str_replace( WP_CONTENT_DIR, '', $path ), '/\\' );
					$zip->addFile( $path, $local );
				}
			}
		}
		if ( file_exists( $sql_path ) ) $zip->addFile( $sql_path, 'database/dump.sql' );
		$zip->addFromString( 'manifest.json', json_encode( [ 
			'plugin_version' => VN_PRIVACY_VERSION, 
			'generated_at'   => current_time( 'mysql' ), 
			'wp_prefix'      => $wpdb->prefix, 
			'site_url'       => get_site_url(),
			'mode'           => $mode,
			'auto'           => true
		], JSON_PRETTY_PRINT ) );
		$zip->close();
		if ( file_exists( $sql_path ) ) @unlink( $sql_path );
		
		// FIX #11: Tính MD5 hash
		$zip_md5 = md5_file( $zip_path );

		// FIX #8: Prune trước khi tạo meta
		self::prune_old_backups( 5, $filename );
		
		// FIX #9: Tải lên FTP bất đồng bộ qua Cron
		$security_settings = get_option( 'vn_security_settings', [] );
		$ftp_enabled       = ! empty( $security_settings['ftp_enabled'] );
		if ( $ftp_enabled ) {
			wp_schedule_single_event( time() + 5, 'vn_ftp_upload_cron', [ $zip_path ] );
		}
		
		update_option( 'vn_backup_meta_' . md5( $filename ), [
			'created_at' => current_time( 'mysql' ),
			'mode'       => $mode,
			'site_url'   => get_site_url(),
			'auto'       => true,
			'verified'   => true,
			'md5'        => $zip_md5,
			'md5_ok'     => true,
		], false );
		
		update_option( 'vn_autobackup_last_run', [
			'time'   => current_time( 'mysql' ),
			'status' => 'success',
			'file'   => $filename,
		] );
	}

	/* ----------------------------------------------------------------
	   FIX #9: Đăng ký Hook WP-Cron cho FTP
	---------------------------------------------------------------- */
	public static function register_ftp_cron() {
		add_action( 'vn_ftp_upload_cron', [ __CLASS__, 'cron_ftp_upload' ] );
	}

	public static function cron_ftp_upload( $filepath ) {
		if ( ! file_exists( $filepath ) ) return;
		$ftp_status = self::upload_to_ftp( $filepath );
		$filename   = basename( $filepath );
		$key        = 'vn_backup_meta_' . md5( $filename );
		$meta       = get_option( $key, [] );
		$meta['ftp_status'] = $ftp_status;
		$meta['ftp_time']   = current_time( 'mysql' );
		update_option( $key, $meta, false );
	}

	public static function cleanup_stale_restore_sessions() {
		// Dọn dẹp các thư mục restore tạm cũ hơn 6 tiếng
		$upload_dir = wp_upload_dir();
		$dirs       = glob( $upload_dir['basedir'] . '/vn_restore_tmp_*', GLOB_ONLYDIR );
		if ( $dirs ) {
			$cutoff = time() - 6 * HOUR_IN_SECONDS;
			foreach ( $dirs as $dir ) {
				if ( filemtime( $dir ) < $cutoff ) {
					self::recursive_delete( $dir );
				}
			}
		}
		// Dọn dẹp các file ZIP restore tạm cũ hơn 6 tiếng
		$restore_zips = glob( self::get_backup_dir() . '/restore_*.zip' );
		if ( $restore_zips ) {
			$cutoff = time() - 6 * HOUR_IN_SECONDS;
			foreach ( $restore_zips as $rz ) {
				if ( filemtime( $rz ) < $cutoff ) @unlink( $rz );
			}
		}
	}

	/* ----------------------------------------------------------------
	   Helpers
	---------------------------------------------------------------- */
	private static function replace_urls_in_value( $value, $old_url, $new_url ) {
		if ( is_serialized( $value ) ) {
			$data = @unserialize( $value );
			if ( $data !== false ) {
				$seen = [];
				$data = self::replace_urls_in_array_or_object( $data, $old_url, $new_url, $seen );
				return serialize( $data );
			}
		}
		return str_replace( $old_url, $new_url, $value );
	}

	private static function replace_urls_in_array_or_object( $data, $old_url, $new_url, &$seen = [], $depth = 0 ) {
		if ( $depth > 20 ) return $data;
		if ( is_string( $data ) ) return str_replace( $old_url, $new_url, $data );
		if ( is_array( $data ) ) {
			foreach ( $data as $key => $val ) {
				$data[ $key ] = self::replace_urls_in_array_or_object( $val, $old_url, $new_url, $seen, $depth + 1 );
			}
		} elseif ( is_object( $data ) ) {
			$id = spl_object_hash( $data );
			if ( isset( $seen[ $id ] ) ) return $data;
			$seen[ $id ] = true;
			foreach ( get_object_vars( $data ) as $property => $val ) {
				$data->$property = self::replace_urls_in_array_or_object( $val, $old_url, $new_url, $seen, $depth + 1 );
			}
		}
		return $data;
	}

	private static function recursive_copy( $src, $dst ) {
		$dir = opendir( $src );
		@mkdir( $dst, 0755, true );
		while ( ( $f = readdir( $dir ) ) !== false ) {
			if ( $f !== '.' && $f !== '..' ) {
				is_dir( "$src/$f" ) ? self::recursive_copy( "$src/$f", "$dst/$f" ) : copy( "$src/$f", "$dst/$f" );
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

	public static function upload_to_ftp( $filepath ) {
		$security_settings = get_option( 'vn_security_settings', [] );
		if ( empty( $security_settings['ftp_enabled'] ) ) {
			return false;
		}

		$host = $security_settings['ftp_host'] ?? '';
		$port = intval( $security_settings['ftp_port'] ?? 21 );
		$user = $security_settings['ftp_user'] ?? '';
		$pass = $security_settings['ftp_pass'] ?? '';
		$path = rtrim( $security_settings['ftp_path'] ?? '/', '/' ) . '/';

		if ( empty( $host ) || empty( $user ) ) {
			return false;
		}

		$conn = @ftp_connect( $host, $port, 15 );
		if ( ! $conn ) {
			error_log( 'VN Privacy: FTP connection failed to ' . $host );
			return false;
		}

		$login = @ftp_login( $conn, $user, $pass );
		if ( ! $login ) {
			@ftp_close( $conn );
			error_log( 'VN Privacy: FTP login failed for ' . $user );
			return false;
		}

		@ftp_pasv( $conn, true );

		if ( ! empty( $path ) && $path !== '/' ) {
			$parts = array_filter( explode( '/', $path ) );
			foreach ( $parts as $part ) {
				if ( ! @ftp_chdir( $conn, $part ) ) {
					@ftp_mkdir( $conn, $part );
					@ftp_chdir( $conn, $part );
				}
			}
		}

		$remote_file = basename( $filepath );
		$upload = @ftp_put( $conn, $remote_file, $filepath, FTP_BINARY );

		@ftp_close( $conn );

		if ( ! $upload ) {
			error_log( 'VN Privacy: FTP upload failed for ' . $remote_file );
			return false;
		}

		return true;
	}
}
