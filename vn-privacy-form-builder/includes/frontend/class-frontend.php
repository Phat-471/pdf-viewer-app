<?php
if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

require_once plugin_dir_path( __FILE__ ) . 'class-frontend-shortcode.php';
require_once plugin_dir_path( __FILE__ ) . 'class-frontend-ajax.php';

class VN_Privacy_Frontend {

	public function __construct() {
		add_shortcode( 'vn_privacy_form', [ 'VN_Privacy_Frontend_Shortcode', 'render_form' ] );
		
		// AJAX Actions
		add_action( 'wp_ajax_vn_submit_privacy_form', [ 'VN_Privacy_Frontend_Ajax', 'handle_form_submission' ] );
		add_action( 'wp_ajax_nopriv_vn_submit_privacy_form', [ 'VN_Privacy_Frontend_Ajax', 'handle_form_submission' ] );
		
		// Enqueue frontend assets
		add_action( 'wp_enqueue_scripts', [ $this, 'enqueue_assets' ] );
	}

	public function enqueue_assets() {
		wp_register_style( 'vn-privacy-frontend-css', VN_PRIVACY_URL . 'assets/style.css', [], VN_PRIVACY_VERSION );
	}
}
