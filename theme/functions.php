<?php
/**
 * Sanitary Catalog Theme functions and definitions
 * Developed with strict compatibility for PHP 7.4 - PHP 8.3+
 *
 * @package Sanitary_Catalog_Theme
 */

if ( ! defined( 'ABSPATH' ) ) {
	exit; // Exit if accessed directly.
}

/**
 * Basic Theme Setup
 */
function sanitary_theme_setup() {
	// Support dynamic title tags
	add_theme_support( 'title-tag' );

	// Enable featured images (thumbnails)
	add_theme_support( 'post-thumbnails' );

	// Add block editor alignment support
	add_theme_support( 'align-wide' );

	// Editor styles support
	add_theme_support( 'editor-styles' );
	add_editor_style( 'style.css' );

	// Support responsive embeds
	add_theme_support( 'responsive-embeds' );

	// Support custom logo
	add_theme_support( 'custom-logo' );

	// Register Navigation Menus
	register_nav_menus( [
		'primary-menu' => __( 'Menu chính (Primary Menu)', 'sanitary-catalog-theme' ),
	] );
}
add_action( 'after_setup_theme', 'sanitary_theme_setup' );

/**
 * Enqueue Styles and Scripts
 */
function sanitary_theme_assets() {
	// Main theme stylesheet with cache busting
	wp_enqueue_style( 'sanitary-main-style', get_stylesheet_uri(), [], filemtime( get_stylesheet_directory() . '/style.css' ) );
}
add_action( 'wp_enqueue_scripts', 'sanitary_theme_assets' );

/**
 * Add Theme Customizer options for phone numbers, address, and Zalo link
 */
function sanitary_customize_register( $wp_customize ) {
	// Section: Contact Settings
	$wp_customize->add_section( 'sanitary_contact_section', [
		'title'    => __( 'Thông tin liên hệ & Mạng xã hội', 'sanitary-catalog-theme' ),
		'priority' => 30,
	] );

	// Setting: Hotline / Số điện thoại hiển thị
	$wp_customize->add_setting( 'sanitary_hotline', [
		'default'           => '090 123 4567',
		'sanitize_callback' => 'sanitize_text_field',
	] );
	$wp_customize->add_control( 'sanitary_hotline', [
		'label'    => __( 'Số Hotline', 'sanitary-catalog-theme' ),
		'section'  => 'sanitary_contact_section',
		'type'     => 'text',
	] );

	// Setting: Số điện thoại gọi điện (dùng cho link tel:)
	$wp_customize->add_setting( 'sanitary_hotline_tel', [
		'default'           => '0901234567',
		'sanitize_callback' => 'sanitize_text_field',
	] );
	$wp_customize->add_control( 'sanitary_hotline_tel', [
		'label'    => __( 'Số Điện Thoại Gọi Đi (Không khoảng trắng)', 'sanitary-catalog-theme' ),
		'section'  => 'sanitary_contact_section',
		'type'     => 'text',
	] );

	// Setting: Zalo Link
	$wp_customize->add_setting( 'sanitary_zalo_url', [
		'default'           => 'https://zalo.me/0901234567',
		'sanitize_callback' => 'esc_url_raw',
	] );
	$wp_customize->add_control( 'sanitary_zalo_url', [
		'label'    => __( 'Đường dẫn Zalo Chat', 'sanitary-catalog-theme' ),
		'section'  => 'sanitary_contact_section',
		'type'     => 'url',
	] );

	// Setting: Địa chỉ showroom
	$wp_customize->add_setting( 'sanitary_address', [
		'default'           => 'Showroom Thiết Bị Vệ Sinh Hồng Miên',
		'sanitize_callback' => 'sanitize_text_field',
	] );
	$wp_customize->add_control( 'sanitary_address', [
		'label'    => __( 'Địa chỉ Showroom', 'sanitary-catalog-theme' ),
		'section'  => 'sanitary_contact_section',
		'type'     => 'text',
	] );

	// Setting: Email
	$wp_customize->add_setting( 'sanitary_email', [
		'default'           => 'contact@example.com',
		'sanitize_callback' => 'sanitize_email',
	] );
	$wp_customize->add_control( 'sanitary_email', [
		'label'    => __( 'Địa chỉ Email', 'sanitary-catalog-theme' ),
		'section'  => 'sanitary_contact_section',
		'type'     => 'email',
	] );

	// Setting: Giờ làm việc
	$wp_customize->add_setting( 'sanitary_working_hours', [
		'default'           => '8:00 - 18:00 (Thứ 2 - Chủ Nhật)',
		'sanitize_callback' => 'sanitize_text_field',
	] );
	$wp_customize->add_control( 'sanitary_working_hours', [
		'label'    => __( 'Giờ làm việc', 'sanitary-catalog-theme' ),
		'section'  => 'sanitary_contact_section',
		'type'     => 'text',
	] );

	// Setting: Facebook Page URL
	$wp_customize->add_setting( 'sanitary_facebook_url', [
		'default'           => 'https://facebook.com',
		'sanitize_callback' => 'esc_url_raw',
	] );
	$wp_customize->add_control( 'sanitary_facebook_url', [
		'label'    => __( 'Đường dẫn Trang Facebook', 'sanitary-catalog-theme' ),
		'section'  => 'sanitary_contact_section',
		'type'     => 'url',
	] );

	// Setting: Copyright
	$wp_customize->add_setting( 'sanitary_copyright', [
		'default'           => '© ' . date('Y') . ' Hồng Miên. Tất cả quyền được bảo lưu.',
		'sanitize_callback' => 'sanitize_text_field',
	] );
	$wp_customize->add_control( 'sanitary_copyright', [
		'label'    => __( 'Thông tin bản quyền chân trang', 'sanitary-catalog-theme' ),
		'section'  => 'sanitary_contact_section',
		'type'     => 'text',
	] );
}
add_action( 'customize_register', 'sanitary_customize_register' );
