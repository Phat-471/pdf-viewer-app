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

/**
 * Automatically create demo blog posts for SEO
 */
function sanitary_create_demo_posts() {
	if ( get_option( 'sanitary_demo_posts_created' ) ) {
		return;
	}

	$posts = [
		[
			'post_title'   => 'Kinh nghiệm chọn mua thiết bị vệ sinh chính hãng cho phòng tắm nhỏ',
			'post_content' => '<!-- wp:paragraph -->
<p>Thiết kế phòng tắm nhỏ hẹp luôn là bài toán đau đầu cho các gia đình hiện đại. Việc chọn thiết bị vệ sinh phù hợp không chỉ giúp tối ưu hóa không gian sử dụng mà còn mang lại sự thoải mái, sạch sẽ.</p>
<!-- /wp:paragraph -->

<!-- wp:heading {"level":2} -->
<h2>1. Chọn bồn cầu nhỏ gọn hoặc treo tường</h2>
<!-- /wp:heading -->

<!-- wp:paragraph -->
<p>Đối với phòng tắm có diện tích hạn chế, bồn cầu treo tường (wall-hung toilet) hoặc bồn cầu 1 khối dáng thon gọn là sự lựa chọn tối ưu. Phần két nước âm tường giúp tiết kiệm diện tích mặt sàn đáng kể.</p>
<!-- /wp:paragraph -->

<!-- wp:heading {"level":2} -->
<h2>2. Lựa chọn sen tắm đứng massage đa năng</h2>
<!-- /wp:heading -->

<!-- wp:paragraph -->
<p>Một bộ sen tắm đứng âm tường hoặc sen tắm đa năng có kệ chứa đồ tích hợp sẽ loại bỏ nhu cầu lắp đặt thêm các kệ phụ rườm rà, tạo cảm giác thông thoáng cho phòng tắm nhỏ.</p>
<!-- /wp:paragraph -->

<!-- wp:heading {"level":2} -->
<h2>3. Cam kết thiết bị vệ sinh chính hãng Hồng Miên</h2>
<!-- /wp:heading -->

<!-- wp:paragraph -->
<p>Khi mua sắm tại Showroom Hồng Miên, quý khách hoàn toàn yên tâm với các dòng sản phẩm đạt chuẩn quốc tế từ các thương hiệu hàng đầu như GIFTO, TAKAMI, TQC... Bảo hành chính hãng và hỗ trợ lắp đặt trọn gói.</p>
<!-- /wp:paragraph -->',
			'post_excerpt' => 'Kinh nghiệm lựa chọn thiết bị vệ sinh chính hãng tối ưu diện tích và tăng tính thẩm mỹ cho phòng tắm nhỏ hẹp.',
			'post_slug'    => 'kinh-nghiem-chon-mua-thiet-bi-ve-sinh-cho-phong-tam-nho',
		],
		[
			'post_title'   => 'Quy trình thiết kế và thi công lắp đặt thiết bị vệ sinh chuẩn kỹ thuật',
			'post_content' => '<!-- wp:paragraph -->
<p>Để đảm bảo thiết bị phòng tắm hoạt động bền bỉ, không bị rò rỉ nước hay hư hỏng sau một thời gian ngắn sử dụng, việc thi công lắp đặt thiết bị vệ sinh cần tuân thủ nghiêm ngặt các tiêu chuẩn kỹ thuật.</p>
<!-- /wp:paragraph -->

<!-- wp:heading {"level":2} -->
<h2>Bước 1: Khảo sát thực tế công trình</h2>
<!-- /wp:heading -->

<!-- wp:paragraph -->
<p>Đo đạc chính xác khoảng cách từ tường đến tâm xả bồn cầu (tiêu chuẩn là 300mm - 305mm), cao độ đường nước cấp lavabo và sen tắm.</p>
<!-- /wp:paragraph -->

<!-- wp:heading {"level":2} -->
<h2>Bước 2: Lắp đặt bồn cầu và lavabo</h2>
<!-- /wp:heading -->

<!-- wp:paragraph -->
<p>Sử dụng gioăng cao su chống hôi và xi măng trắng/keo silicone chuyên dụng để ngăn mùi tuyệt đối. Không siết ốc quá chặt dễ gây nứt vỡ sứ vệ sinh.</p>
<!-- /wp:paragraph -->

<!-- wp:heading {"level":2} -->
<h2>Bước 3: Dịch vụ lắp đặt chuyên nghiệp từ Hồng Miên</h2>
<!-- /wp:heading -->

<!-- wp:paragraph -->
<p>Hồng Miên cung cấp dịch vụ thiết kế 3D phòng tắm và thi công lắp đặt trọn gói chuyên nghiệp bởi đội ngũ thợ lành nghề, cam kết bảo hành lắp đặt uy tín.</p>
<!-- /wp:paragraph -->',
			'post_excerpt' => 'Hướng dẫn chi tiết quy trình khảo sát và lắp đặt các thiết bị vệ sinh bồn cầu, vòi sen, lavabo đúng tiêu chuẩn kỹ thuật xây dựng.',
			'post_slug'    => 'quy-trinh-thiet-ke-thi-cong-lap-dat-thiet-bi-ve-sinh',
		],
		[
			'post_title'   => 'Top 5 mẫu bồn cầu thông minh cao cấp được ưa chuộng nhất 2026',
			'post_content' => '<!-- wp:paragraph -->
<p>Bồn cầu thông minh (smart toilet) đang trở thành xu hướng tất yếu trong các phòng tắm hiện đại nhờ tính năng tự động hóa, sấy sưởi và tiết kiệm nước tối đa.</p>
<!-- /wp:paragraph -->

<!-- wp:heading {"level":2} -->
<h2>1. Bồn cầu thông minh GIFTO GOLD Luxury</h2>
<!-- /wp:heading -->

<!-- wp:paragraph -->
<p>Tích hợp hệ thống xả xoáy kép siêu mạnh, tự động đóng mở nắp khi có người đến gần, sưởi ấm bệ ngồi và sấy khô bằng khí ấm.</p>
<!-- /wp:paragraph -->

<!-- wp:heading {"level":2} -->
<h2>2. Bồn cầu cảm ứng TAKAMI Smart-X</h2>
<!-- /wp:heading -->

<!-- wp:paragraph -->
<p>Sở hữu công nghệ tự rửa bằng tia nước ấm massage và diệt khuẩn bằng tia cực tím UV cực kỳ an toàn cho sức khỏe gia đình.</p>
<!-- /wp:paragraph -->

<!-- wp:heading {"level":2} -->
<h2>3. Mua sắm bồn cầu thông minh tại Hồng Miên</h2>
<!-- /wp:heading -->

<!-- wp:paragraph -->
<p>Chúng tôi trưng bày đầy đủ các dòng sản phẩm bồn cầu thông minh cao cấp tại showroom Thanh An, Quảng Ngãi với giá ưu đãi tốt nhất thị trường.</p>
<!-- /wp:paragraph -->',
			'post_excerpt' => 'Đánh giá các mẫu bồn cầu thông minh cao cấp tích hợp tự động sấy sưởi, xả cảm ứng tốt nhất hiện nay cho phòng tắm thông minh.',
			'post_slug'    => 'top-5-mau-bon-cau-thong-minh-cao-cap-nhat',
		]
	];

	foreach ( $posts as $p ) {
		// Check if post already exists
		$existing = get_page_by_path( $p['post_slug'], OBJECT, 'post' );
		if ( ! $existing ) {
			wp_insert_post( [
				'post_title'   => $p['post_title'],
				'post_content' => $p['post_content'],
				'post_excerpt' => $p['post_excerpt'],
				'post_name'    => $p['post_slug'],
				'post_status'  => 'publish',
				'post_type'    => 'post',
			] );
		}
	}

	update_option( 'sanitary_demo_posts_created', 1 );
}
add_action( 'init', 'sanitary_create_demo_posts' );

/**
 * Register Custom Post Type: Dự án (Project)
 */
function sanitary_register_project_cpt() {
	$labels = [
		'name'               => _x( 'Dự án', 'post type general name', 'sanitary-catalog-theme' ),
		'singular_name'      => _x( 'Dự án', 'post type singular name', 'sanitary-catalog-theme' ),
		'menu_name'          => _x( 'Dự án', 'admin menu', 'sanitary-catalog-theme' ),
		'name_admin_bar'     => _x( 'Dự án', 'add new on admin bar', 'sanitary-catalog-theme' ),
		'add_new'            => _x( 'Thêm dự án mới', 'project', 'sanitary-catalog-theme' ),
		'add_new_item'       => __( 'Thêm dự án mới', 'sanitary-catalog-theme' ),
		'new_item'           => __( 'Dự án mới', 'sanitary-catalog-theme' ),
		'edit_item'          => __( 'Chỉnh sửa dự án', 'sanitary-catalog-theme' ),
		'view_item'          => __( 'Xem dự án', 'sanitary-catalog-theme' ),
		'all_items'          => __( 'Tất cả dự án', 'sanitary-catalog-theme' ),
		'search_items'       => __( 'Tìm kiếm dự án', 'sanitary-catalog-theme' ),
		'parent_item_colon'  => __( 'Dự án cha:', 'sanitary-catalog-theme' ),
		'not_found'          => __( 'Không tìm thấy dự án nào.', 'sanitary-catalog-theme' ),
		'not_found_in_trash' => __( 'Không tìm thấy dự án nào trong Thùng rác.', 'sanitary-catalog-theme' )
	];

	$args = [
		'labels'             => $labels,
		'public'             => true,
		'publicly_queryable' => true,
		'show_ui'            => true,
		'show_in_menu'       => true,
		'query_var'          => true,
		'rewrite'            => [ 'slug' => 'du-an' ],
		'capability_type'    => 'post',
		'has_archive'        => true,
		'hierarchical'       => false,
		'menu_position'      => 6,
		'menu_icon'          => 'dashicons-portfolio',
		'supports'           => [ 'title', 'editor', 'thumbnail', 'excerpt' ],
		'show_in_rest'       => true,
	];

	register_post_type( 'sanitary_project', $args );
}
add_action( 'init', 'sanitary_register_project_cpt' );

/**
 * Automatically create demo projects for testing
 */
function sanitary_create_demo_projects() {
	if ( get_option( 'sanitary_demo_projects_created' ) ) {
		return;
	}

	$projects = [
		[
			'post_title'   => 'Thi công phòng tắm Biệt Thự Ecopark',
			'post_content' => '<!-- wp:paragraph -->
<p>Công trình thi công lắp đặt trọn gói thiết bị vệ sinh cao cấp cho biệt thự tại phân khu Ecopark. Chủ đầu tư yêu cầu phong cách thiết kế sang trọng, hiện đại với các sản phẩm thông minh.</p>
<!-- /wp:paragraph -->

<!-- wp:heading {"level":2} -->
<h2>Hạng mục thực hiện</h2>
<!-- /wp:heading -->

<!-- wp:list -->
<ul>
<li>Khảo sát cao độ đường nước và thiết kế bố cục 3D phòng tắm.</li>
<li>Lắp đặt bồn cầu thông minh GIFTO GOLD Luxury tự động cảm ứng.</li>
<li>Thi công bồn tắm nằm massage Acrylic cao cấp.</li>
<li>Lắp đặt hệ thống sen tắm âm tường mạ vàng sang trọng.</li>
</ul>
<!-- /wp:list -->

<!-- wp:heading {"level":2} -->
<h2>Thông tin chi tiết dự án</h2>
<!-- /wp:heading -->
<p><strong>Chủ đầu tư:</strong> Anh Hoàng Anh</p>
<p><strong>Địa điểm:</strong> Biệt thự Ecopark, Hưng Yên</p>
<p><strong>Thương hiệu sử dụng:</strong> GIFTO GOLD & MANDY</p>',
			'post_excerpt' => 'Công trình thi công lắp đặt trọn gói thiết bị vệ sinh cao cấp cho biệt thự Ecopark sử dụng thương hiệu GIFTO GOLD & MANDY.',
			'post_slug'    => 'thi-cong-phong-tam-biet-thu-ecopark',
		],
		[
			'post_title'   => 'Lắp đặt thiết bị vệ sinh Căn Hộ Vinhomes',
			'post_content' => '<!-- wp:paragraph -->
<p>Dự án hoàn thiện 2 phòng tắm căn hộ 3 phòng ngủ tại chung cư Vinhomes. Tối ưu hóa không gian phòng tắm nhỏ nhưng vẫn đảm bảo đầy đủ công năng tiện ích cao cấp.</p>
<!-- /wp:paragraph -->

<!-- wp:heading {"level":2} -->
<h2>Hạng mục thực hiện</h2>
<!-- /wp:heading -->

<!-- wp:list -->
<ul>
<li>Thi công lắp đặt vách kính tắm đứng ngăn nước.</li>
<li>Lắp đặt bệt vệ sinh TAKAMI nắp rửa cơ thông minh tiện dụng.</li>
<li>Lắp đặt chậu lavabo âm bàn đá kết hợp vòi cổ cao TQC.</li>
</ul>
<!-- /wp:list -->

<!-- wp:heading {"level":2} -->
<h2>Thông tin chi tiết dự án</h2>
<!-- /wp:heading -->
<p><strong>Chủ đầu tư:</strong> Chị Mai Lan</p>
<p><strong>Địa điểm:</strong> Căn hộ Vinhomes Grand Park, Quận 9</p>
<p><strong>Thương hiệu sử dụng:</strong> TAKAMI & TQC</p>',
			'post_excerpt' => 'Dự án hoàn thiện phòng tắm căn hộ Vinhomes sử dụng thiết bị vệ sinh cao cấp chính hãng từ TAKAMI & TQC.',
			'post_slug'    => 'lap-dat-thiet-bi-ve-sinh-can-ho-vinhomes',
		],
		[
			'post_title'   => 'Thiết kế & Thi công trọn gói Nhà Phố Quận 2',
			'post_content' => '<!-- wp:paragraph -->
<p>Nhà phố 3 tầng hiện đại tại Quận 2 được thiết kế và thi công trọn gói hệ thống phòng tắm master và phòng tắm phụ, phối màu tông xám bê tông hiện đại kết hợp thiết bị vệ sinh đen mờ cá tính.</p>
<!-- /wp:paragraph -->

<!-- wp:heading {"level":2} -->
<h2>Hạng mục thực hiện</h2>
<!-- /wp:heading -->

<!-- wp:list -->
<ul>
<li>Tư vấn bản vẽ kỹ thuật đường cấp thoát nước.</li>
<li>Lắp đặt bồn cầu 1 khối liền mạch phủ men Nano kháng khuẩn GIFTO.</li>
<li>Lắp đặt sen cây phím đàn đa năng hiển thị nhiệt độ SDUY.</li>
</ul>
<!-- /wp:list -->

<!-- wp:heading {"level":2} -->
<h2>Thông tin chi tiết dự án</h2>
<!-- /wp:heading -->
<p><strong>Chủ đầu tư:</strong> Anh Minh Trí</p>
<p><strong>Địa điểm:</strong> Đường Nguyễn Cơ Thạch, Quận 2</p>
<p><strong>Thương hiệu sử dụng:</strong> GIFTO & SDUY</p>',
			'post_excerpt' => 'Dự án thiết kế và thi công trọn gói phòng tắm nhà phố hiện đại tại Quận 2 sử dụng thương hiệu GIFTO & SDUY.',
			'post_slug'    => 'thiet-ke-thi-cong-tron-goi-nha-pho-quan-2',
		]
	];

	foreach ( $projects as $p ) {
		$existing = get_page_by_path( $p['post_slug'], OBJECT, 'sanitary_project' );
		if ( ! $existing ) {
			wp_insert_post( [
				'post_title'   => $p['post_title'],
				'post_content' => $p['post_content'],
				'post_excerpt' => $p['post_excerpt'],
				'post_name'    => $p['post_slug'],
				'post_status'  => 'publish',
				'post_type'    => 'sanitary_project',
			] );
		}
	}

	update_option( 'sanitary_demo_projects_created', 1 );
}
add_action( 'init', 'sanitary_create_demo_projects' );

