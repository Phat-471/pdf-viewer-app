<?php
/**
 * VN Security - WordPress Core Integrity Monitor
 * Compares wp-includes & wp-admin file hashes against the official WordPress checksums.
 */
if ( ! defined( 'ABSPATH' ) ) exit;

class VN_Security_Integrity {

	public function __construct() {
		add_action( 'wp_ajax_vn_integrity_scan', [ $this, 'ajax_scan' ] );
	}

	/** Static entry point registered in class-utilities.php */
	public static function ajax_scan_static() {
		( new self() )->ajax_scan();
	}

	/* ================================================================
	   AJAX: Run scan
	================================================================ */
	public function ajax_scan() {
		$nonce = $_POST['nonce'] ?? '';
		if ( ! wp_verify_nonce( $nonce, 'vn_save_security' ) ) {
			wp_send_json_error( 'Invalid nonce.' );
		}
		if ( ! current_user_can( 'manage_options' ) ) {
			wp_send_json_error( 'Unauthorized' );
		}

		@set_time_limit( 300 );

		$result = self::run_scan();
		wp_send_json_success( $result );
	}

	/* ================================================================
	   Core Scanner
	================================================================ */
	public static function run_scan() {
		global $wp_version;

		// Fetch official checksums from WordPress.org API
		$api_url  = "https://api.wordpress.org/core/checksums/1.0/?version={$wp_version}&locale=en_US";
		$response = wp_remote_get( $api_url, [ 'timeout' => 20 ] );

		if ( is_wp_error( $response ) ) {
			return [
				'status' => 'error',
				'message' => 'Không thể kết nối tới WordPress.org API: ' . $response->get_error_message(),
			];
		}

		$body = json_decode( wp_remote_retrieve_body( $response ), true );
		if ( empty( $body['checksums'] ) ) {
			return [
				'status'  => 'error',
				'message' => 'Không thể tải bảng checksum từ WordPress.org.',
			];
		}

		$checksums = $body['checksums'];
		$modified  = [];
		$missing   = [];
		$added     = [];

		foreach ( $checksums as $rel_path => $expected_md5 ) {
			// Only check wp-admin and wp-includes (not wp-content)
			if (
				strpos( $rel_path, 'wp-admin/' ) !== 0 &&
				strpos( $rel_path, 'wp-includes/' ) !== 0 &&
				! in_array( $rel_path, [ 'index.php', 'wp-login.php', 'wp-settings.php', 'wp-cron.php', 'wp-blog-header.php', 'wp-load.php', 'wp-mail.php', 'xmlrpc.php' ], true )
			) {
				continue;
			}

			$file_path = ABSPATH . $rel_path;

			if ( ! file_exists( $file_path ) ) {
				$missing[] = $rel_path;
				continue;
			}

			$actual_md5 = md5_file( $file_path );
			if ( $actual_md5 !== $expected_md5 ) {
				$modified[] = [
					'file'     => $rel_path,
					'expected' => $expected_md5,
					'actual'   => $actual_md5,
					'mtime'    => @filemtime( $file_path ),
					'size'     => @filesize( $file_path ),
				];
			}
		}

		// Check for extra files added to wp-admin/wp-includes (potential backdoors)
		$dirs_to_check = [
			ABSPATH . 'wp-admin'    => 'wp-admin/',
			ABSPATH . 'wp-includes' => 'wp-includes/',
		];
		foreach ( $dirs_to_check as $dir => $prefix ) {
			if ( ! is_dir( $dir ) ) continue;
			$it = new RecursiveIteratorIterator(
				new RecursiveDirectoryIterator( $dir, RecursiveDirectoryIterator::SKIP_DOTS )
			);
			foreach ( $it as $f ) {
				if ( ! $f->isFile() ) continue;
				$rel = $prefix . str_replace( '\\', '/', substr( $f->getPathname(), strlen( $dir ) + 1 ) );
				if ( ! isset( $checksums[ $rel ] ) ) {
					$added[] = [
						'file'  => $rel,
						'mtime' => $f->getMTime(),
						'size'  => $f->getSize(),
					];
				}
			}
		}

		return [
			'status'   => 'ok',
			'version'  => $wp_version,
			'modified' => $modified,
			'missing'  => $missing,
			'added'    => $added,
			'clean'    => empty( $modified ) && empty( $missing ) && empty( $added ),
			'scanned'  => count( $checksums ),
		];
	}
}
