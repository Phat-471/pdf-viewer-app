<?php

if (!defined('ABSPATH')) {
    exit;
}

add_action('rest_api_init', 'pdfpro_licensing_register_public_key_routes');

function pdfpro_licensing_register_public_key_routes() {
    $namespace = 'pdfpro/v1';

    register_rest_route($namespace, '/public-key', array(
        'methods'             => array('GET', 'POST'),
        'callback'            => 'pdfpro_licensing_api_public_key',
        'permission_callback' => '__return_true',
    ));

    register_rest_route($namespace, '/update-check', array(
        'methods'             => 'POST',
        'callback'            => 'pdfpro_licensing_api_update_check',
        'permission_callback' => '__return_true',
    ));
}

function pdfpro_licensing_api_public_key(WP_REST_Request $request) {
    $public_key = pdfpro_licensing_get_public_key_pem();
    if (empty($public_key)) {
        return new WP_Error('public_key_missing', 'Public key is not available.', array('status' => 500));
    }

    return array(
        'success'     => true,
        'public_key'  => $public_key,
        'fingerprint' => hash('sha256', $public_key),
    );
}

function pdfpro_licensing_get_public_key_pem() {
    if (function_exists('pdfpro_licensing_ensure_rsa_keypair')) {
        pdfpro_licensing_ensure_rsa_keypair();
    }

    if (!defined('PDFPRO_PUBLIC_KEY_PATH') || !file_exists(PDFPRO_PUBLIC_KEY_PATH)) {
        return '';
    }

    $public_key_pem = file_get_contents(PDFPRO_PUBLIC_KEY_PATH);
    if ($public_key_pem === false) {
        return '';
    }

    return trim($public_key_pem);
}
