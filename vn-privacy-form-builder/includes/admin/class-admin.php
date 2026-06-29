<?php
if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

require_once plugin_dir_path( __FILE__ ) . 'class-admin-menu.php';
require_once plugin_dir_path( __FILE__ ) . 'class-admin-forms.php';
require_once plugin_dir_path( __FILE__ ) . 'class-admin-entries.php';
require_once plugin_dir_path( __FILE__ ) . 'class-admin-utilities.php';
require_once plugin_dir_path( __FILE__ ) . 'class-admin-actions.php';

class VN_Privacy_Admin {

	public function __construct() {
		add_action( 'admin_menu',  [ 'VN_Privacy_Admin_Menu',    'register_menu_pages' ] );
		add_action( 'admin_init',  [ 'VN_Privacy_Admin_Actions', 'handle_export_action' ] );
		add_action( 'admin_init',  [ 'VN_Privacy_Admin_Actions', 'handle_actions' ] );
		add_action( 'admin_enqueue_scripts', [ $this, 'enqueue_admin_assets' ] );
	}

	public function enqueue_admin_assets( $hook ) {
		// Only load on plugin's own pages
		$vn_pages = [
			'toplevel_page_vn-privacy-forms',
			'vn-privacy-forms_page_vn-privacy-entries',
			'vn-privacy-forms_page_vn-privacy-create-form',
			'vn-privacy-forms_page_vn-privacy-utilities',
			'vn-privacy-forms_page_vn-settings',
		];

		if ( ! in_array( $hook, $vn_pages, true ) ) {
			return;
		}

		$css_ver = file_exists( VN_PRIVACY_PATH . 'assets/admin.css' ) ? filemtime( VN_PRIVACY_PATH . 'assets/admin.css' ) : VN_PRIVACY_VERSION;
		$js_ver  = file_exists( VN_PRIVACY_PATH . 'assets/admin.js' ) ? filemtime( VN_PRIVACY_PATH . 'assets/admin.js' ) : VN_PRIVACY_VERSION;

		wp_enqueue_style(
			'vn-privacy-admin-css',
			VN_PRIVACY_URL . 'assets/admin.css',
			[],
			$css_ver
		);

		wp_enqueue_script(
			'vn-privacy-admin-js',
			VN_PRIVACY_URL . 'assets/admin.js',
			[ 'jquery' ],
			$js_ver,
			true
		);
	}
}
