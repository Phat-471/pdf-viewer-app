<?php
/**
 * Plugin Name: Sanitary Catalog Core
 * Plugin URI: https://example.com
 * Description: Logic lõi và Bộ lọc cho Website Catalogue Thiết bị vệ sinh. Tương thích PHP 7.4 - 8.3+.
 * Version: 1.0.0
 * Author: Antigravity
 * Author URI: https://example.com
 * License: GPL2
 * Text Domain: sanitary-catalog-core
 */

if ( ! defined( 'ABSPATH' ) ) {
	exit; // Exit if accessed directly.
}

/**
 * Register Custom Post Type: Product (Sản phẩm)
 */
function sanitary_register_product_cpt() {
	$labels = [
		'name'               => _x( 'Sản phẩm', 'post type general name', 'sanitary-catalog-core' ),
		'singular_name'      => _x( 'Sản phẩm', 'post type singular name', 'sanitary-catalog-core' ),
		'menu_name'          => _x( 'Sản phẩm', 'admin menu', 'sanitary-catalog-core' ),
		'name_admin_bar'     => _x( 'Sản phẩm', 'add new on admin bar', 'sanitary-catalog-core' ),
		'add_new'            => _x( 'Thêm sản phẩm mới', 'product', 'sanitary-catalog-core' ),
		'add_new_item'       => __( 'Thêm sản phẩm mới', 'sanitary-catalog-core' ),
		'new_item'           => __( 'Sản phẩm mới', 'sanitary-catalog-core' ),
		'edit_item'          => __( 'Chỉnh sửa sản phẩm', 'sanitary-catalog-core' ),
		'view_item'          => __( 'Xem sản phẩm', 'sanitary-catalog-core' ),
		'all_items'          => __( 'Tất cả sản phẩm', 'sanitary-catalog-core' ),
		'search_items'       => __( 'Tìm kiếm sản phẩm', 'sanitary-catalog-core' ),
		'parent_item_colon'  => __( 'Sản phẩm cha:', 'sanitary-catalog-core' ),
		'not_found'          => __( 'Không tìm thấy sản phẩm nào.', 'sanitary-catalog-core' ),
		'not_found_in_trash' => __( 'Không tìm thấy sản phẩm nào trong thùng rác.', 'sanitary-catalog-core' ),
	];

	$args = [
		'labels'             => $labels,
		'public'             => true,
		'publicly_queryable' => true,
		'show_ui'            => true,
		'show_in_menu'       => true,
		'query_var'          => true,
		'rewrite'            => [ 'slug' => 'san-pham', 'with_front' => false ],
		'capability_type'    => 'post',
		'has_archive'        => 'san-pham',
		'hierarchical'        => false,
		'menu_position'      => 5,
		'menu_icon'          => 'dashicons-cart',
		'show_in_rest'       => true,
		'supports'           => [ 'title', 'editor', 'thumbnail', 'excerpt', 'custom-fields' ],
	];

	register_post_type( 'sanitary_product', $args );
}
add_action( 'init', 'sanitary_register_product_cpt' );

/**
 * Register Custom Taxonomies: Brand (Thương hiệu) & Catalog Category (Danh mục sản phẩm)
 */
function sanitary_register_taxonomies() {
	// 1. Taxonomy: Thương hiệu (product_brand)
	$brand_labels = [
		'name'              => _x( 'Thương hiệu / Hãng', 'taxonomy general name', 'sanitary-catalog-core' ),
		'singular_name'     => _x( 'Thương hiệu', 'taxonomy singular name', 'sanitary-catalog-core' ),
		'search_items'      => __( 'Tìm thương hiệu', 'sanitary-catalog-core' ),
		'all_items'         => __( 'Tất cả thương hiệu', 'sanitary-catalog-core' ),
		'parent_item'       => __( 'Thương hiệu cha', 'sanitary-catalog-core' ),
		'parent_item_colon' => __( 'Thương hiệu cha:', 'sanitary-catalog-core' ),
		'edit_item'         => __( 'Chỉnh sửa thương hiệu', 'sanitary-catalog-core' ),
		'update_item'       => __( 'Cập nhật thương hiệu', 'sanitary-catalog-core' ),
		'add_new_item'      => __( 'Thêm thương hiệu mới', 'sanitary-catalog-core' ),
		'new_item_name'     => __( 'Tên thương hiệu mới', 'sanitary-catalog-core' ),
		'menu_name'         => __( 'Thương hiệu / Hãng', 'sanitary-catalog-core' ),
	];

	$brand_args = [
		'hierarchical'      => true,
		'labels'            => $brand_labels,
		'show_ui'           => true,
		'show_admin_column' => true,
		'query_var'         => true,
		'rewrite'           => [ 'slug' => 'thuong-hieu' ],
		'show_in_rest'      => true,
	];

	register_taxonomy( 'product_brand', [ 'sanitary_product' ], $brand_args );

	// 2. Taxonomy: Danh mục sản phẩm (product_cat)
	$cat_labels = [
		'name'              => _x( 'Danh mục sản phẩm', 'taxonomy general name', 'sanitary-catalog-core' ),
		'singular_name'     => _x( 'Danh mục', 'taxonomy singular name', 'sanitary-catalog-core' ),
		'search_items'      => __( 'Tìm danh mục', 'sanitary-catalog-core' ),
		'all_items'         => __( 'Tất cả danh mục', 'sanitary-catalog-core' ),
		'parent_item'       => __( 'Danh mục cha', 'sanitary-catalog-core' ),
		'parent_item_colon' => __( 'Danh mục cha:', 'sanitary-catalog-core' ),
		'edit_item'         => __( 'Chỉnh sửa danh mục', 'sanitary-catalog-core' ),
		'update_item'       => __( 'Cập nhật danh mục', 'sanitary-catalog-core' ),
		'add_new_item'      => __( 'Thêm danh mục mới', 'sanitary-catalog-core' ),
		'new_item_name'     => __( 'Tên danh mục mới', 'sanitary-catalog-core' ),
		'menu_name'         => __( 'Danh mục sản phẩm', 'sanitary-catalog-core' ),
	];

	$cat_args = [
		'hierarchical'      => true,
		'labels'            => $cat_labels,
		'show_ui'           => true,
		'show_admin_column' => true,
		'query_var'         => true,
		'rewrite'           => [ 'slug' => 'danh-muc-san-pham' ],
		'show_in_rest'      => true,
	];

	register_taxonomy( 'product_cat', [ 'sanitary_product' ], $cat_args );
}
add_action( 'init', 'sanitary_register_taxonomies' );

/**
 * Auto-populate default brands on activation
 */
function sanitary_populate_default_brands() {
	$brands = [ 'GIFTO', 'GIFTO GOLD', 'MANDY', 'SDUY', 'TAKAMI', 'TQC' ];
	foreach ( $brands as $brand_name ) {
		$name = (string) $brand_name;
		if ( ! term_exists( $name, 'product_brand' ) ) {
			wp_insert_term( $name, 'product_brand' );
		}
	}
}
register_activation_hook( __FILE__, 'sanitary_populate_default_brands' );
add_action( 'init', 'sanitary_populate_default_brands', 20 );

/**
 * Seed Demo Data
 */
function sanitary_seed_demo_data() {
	$products_count = wp_count_posts( 'sanitary_product' );
	if ( isset( $products_count->publish ) && (int) $products_count->publish > 0 ) {
		return;
	}

	$categories = [ 'Bồn Cầu Thông Minh', 'Sen Vòi Cao Cấp', 'Chậu Rửa Lavabo' ];
	$cat_ids = [];
	foreach ( $categories as $cat_name ) {
		$term = term_exists( $cat_name, 'product_cat' );
		if ( ! $term ) {
			$inserted = wp_insert_term( $cat_name, 'product_cat' );
			if ( ! is_wp_error( $inserted ) ) {
				$cat_ids[$cat_name] = $inserted['term_id'];
			}
		} else {
			$cat_ids[$cat_name] = is_array( $term ) ? $term['term_id'] : $term;
		}
	}

	$dummy_products = [
		[
			'title'    => 'Bồn Cầu Thông Minh GIFTO G-8800',
			'brand'    => 'GIFTO',
			'category' => 'Bồn Cầu Thông Minh',
			'excerpt'  => 'Dòng bồn cầu sấy ấm thông minh với hệ thống xả xoáy siphon siêu mạnh, tiết kiệm nước tối đa.',
			'desc'     => 'Bồn cầu thông minh cao cấp GIFTO G-8800 mang lại trải nghiệm đỉnh cao cho phòng tắm của bạn.'
		],
		[
			'title'    => 'Bồn Cầu Trứng Đen GIFTO GOLD G-99',
			'brand'    => 'GIFTO GOLD',
			'category' => 'Bồn Cầu Thông Minh',
			'excerpt'  => 'Kiểu dáng hình quả trứng độc đáo, lớp men đen mờ nano sang trọng chống trầy xước.',
			'desc'     => 'Bản giới hạn từ thương hiệu GIFTO GOLD.'
		],
		[
			'title'    => 'Sen Tắm Đứng Nóng Lạnh MANDY M-202',
			'brand'    => 'MANDY',
			'category' => 'Sen Vòi Cao Cấp',
			'excerpt'  => 'Củ sen bằng đồng thau nguyên chất, mạ chrome 5 lớp sáng bóng vĩnh viễn.',
			'desc'     => 'Sen tắm cây đứng nóng lạnh MANDY M-202.'
		],
		[
			'title'    => 'Sen Tắm Thuyền Massage SDUY S-500',
			'brand'    => 'SDUY',
			'category' => 'Sen Vòi Cao Cấp',
			'excerpt'  => 'Hệ thống sen thuyền với 5 chế độ phun massage nước mạnh mẽ, thư giãn cơ thể.',
			'desc'     => 'Sen thuyền massage cao cấp SDUY S-500.'
		],
		[
			'title'    => 'Chậu Lavabo Đặt Bàn TAKAMI T-45',
			'brand'    => 'TAKAMI',
			'category' => 'Chậu Rửa Lavabo',
			'excerpt'  => 'Chậu sứ đặt bàn viền mỏng nghệ thuật, công nghệ men chống bám bẩn Aquaceramic.',
			'desc'     => 'Chậu rửa mặt đặt bàn TAKAMI T-45.'
		],
		[
			'title'    => 'Vòi Lavabo Nóng Lạnh TQC T-12',
			'brand'    => 'TQC',
			'category' => 'Chậu Rửa Lavabo',
			'excerpt'  => 'Vòi nóng lạnh kiểu dáng thiên nga sang trọng, tiết kiệm nước tối đa.',
			'desc'     => 'Vòi lavabo nóng lạnh TQC T-12.'
		]
	];

	foreach ( $dummy_products as $prod ) {
		$post_id = wp_insert_post([
			'post_title'   => $prod['title'],
			'post_content' => $prod['desc'],
			'post_excerpt' => $prod['excerpt'],
			'post_status'  => 'publish',
			'post_type'    => 'sanitary_product',
		]);

		if ( $post_id && ! is_wp_error( $post_id ) ) {
			$brand_term = term_exists( $prod['brand'], 'product_brand' );
			if ( $brand_term ) {
				wp_set_post_terms( $post_id, [ (int)$brand_term['term_id'] ], 'product_brand' );
			}
			if ( isset( $cat_ids[$prod['category']] ) ) {
				wp_set_post_terms( $post_id, [ (int)$cat_ids[$prod['category']] ], 'product_cat' );
			}
		}
	}
}
add_action( 'init', 'sanitary_seed_demo_data', 30 );

/**
 * Flush rewrite rules on activation and deactivation
 */
function sanitary_catalog_core_activate() {
	sanitary_register_product_cpt();
	sanitary_register_taxonomies();
	sanitary_populate_default_brands();
	flush_rewrite_rules();
}
register_activation_hook( __FILE__, 'sanitary_catalog_core_activate' );

function sanitary_catalog_core_deactivate() {
	flush_rewrite_rules();
}
register_deactivation_hook( __FILE__, 'sanitary_catalog_core_deactivate' );

/**
 * Query Filter Hook: Combine Category and Brand filters in CPT Archive
 */
function sanitary_filter_products( $query ) {
	if ( is_admin() || ! $query->is_main_query() ) {
		return;
	}

	if ( is_post_type_archive( 'sanitary_product' ) || is_tax( 'product_brand' ) || is_tax( 'product_cat' ) ) {
		$tax_query = [];

		if ( ! empty( $_GET['filter_cat'] ) ) {
			$tax_query[] = [
				'taxonomy' => 'product_cat',
				'field'    => 'slug',
				'terms'    => sanitize_text_field( $_GET['filter_cat'] ),
			];
		}

		if ( ! empty( $_GET['filter_brand'] ) ) {
			$tax_query[] = [
				'taxonomy' => 'product_brand',
				'field'    => 'slug',
				'terms'    => sanitize_text_field( $_GET['filter_brand'] ),
			];
		}

		if ( count( $tax_query ) > 1 ) {
			$tax_query['relation'] = 'AND';
		}

		if ( ! empty( $tax_query ) ) {
			$query->set( 'tax_query', $tax_query );
		}
	}
}
add_action( 'pre_get_posts', 'sanitary_filter_products' );

/**
 * Render the sidebar filter options
 */
if ( ! function_exists( 'sanitary_get_products_filter_sidebar' ) ) {
	function sanitary_get_products_filter_sidebar() {
		$active_cat = ! empty( $_GET['filter_cat'] ) ? sanitize_text_field( $_GET['filter_cat'] ) : '';
		$active_brand = ! empty( $_GET['filter_brand'] ) ? sanitize_text_field( $_GET['filter_brand'] ) : '';

		if ( is_tax( 'product_cat' ) ) {
			$active_cat = get_queried_object()->slug;
		}
		if ( is_tax( 'product_brand' ) ) {
			$active_brand = get_queried_object()->slug;
		}

		$categories = get_terms( [ 'taxonomy' => 'product_cat', 'hide_empty' => false ] );
		if ( ! is_wp_error( $categories ) && ! empty( $categories ) ) {
			foreach ( $categories as $cat ) {
				$count_args = [
					'post_type'      => 'sanitary_product',
					'posts_per_page' => -1,
					'fields'         => 'ids',
					'tax_query'      => [
						'relation' => 'AND',
						[
							'taxonomy' => 'product_cat',
							'field'    => 'slug',
							'terms'    => $cat->slug,
						]
					]
				];
				if ( ! empty( $active_brand ) ) {
					$count_args['tax_query'][] = [
						'taxonomy' => 'product_brand',
						'field'    => 'slug',
						'terms'    => $active_brand,
					];
				}
				$count_query = new WP_Query( $count_args );
				$cat->dynamic_count = $count_query->post_count;
			}
		}

		$brands = get_terms( [ 'taxonomy' => 'product_brand', 'hide_empty' => false ] );
		if ( ! is_wp_error( $brands ) && ! empty( $brands ) ) {
			foreach ( $brands as $brand ) {
				$count_args = [
					'post_type'      => 'sanitary_product',
					'posts_per_page' => -1,
					'fields'         => 'ids',
					'tax_query'      => [
						'relation' => 'AND',
						[
							'taxonomy' => 'product_brand',
							'field'    => 'slug',
							'terms'    => $brand->slug,
						]
					]
				];
				if ( ! empty( $active_cat ) ) {
					$count_args['tax_query'][] = [
						'taxonomy' => 'product_cat',
						'field'    => 'slug',
						'terms'    => $active_cat,
					];
				}
				$count_query = new WP_Query( $count_args );
				$brand->dynamic_count = $count_query->post_count;
			}
		}

		$base_url = get_post_type_archive_link( 'sanitary_product' );
		?>
		<!-- Mobile Filter Trigger Button -->
		<button class="mobile-filter-toggle" id="mobile-filter-trigger">
			<span class="filter-icon">🔍</span> Lọc sản phẩm
		</button>

		<aside class="catalog-sidebar" id="catalog-sidebar-drawer">
			<div class="sidebar-filter-block">
				<h3>Danh Mục Sản Phẩm</h3>
				<ul class="filter-list">
					<li class="<?php echo empty( $active_cat ) ? 'active' : ''; ?>">
						<a href="<?php echo esc_url( add_query_arg( 'filter_cat', '', $base_url . ( ! empty( $active_brand ) ? '?filter_brand=' . $active_brand : '' ) ) ); ?>">
							Tất cả danh mục
						</a>
					</li>
					<?php if ( ! is_wp_error( $categories ) && ! empty( $categories ) ) : ?>
						<?php foreach ( $categories as $cat ) : ?>
							<li class="<?php echo ( $active_cat === $cat->slug ) ? 'active' : ''; ?>">
								<a href="<?php echo esc_url( add_query_arg( 'filter_cat', $cat->slug, $base_url . ( ! empty( $active_brand ) ? '?filter_brand=' . $active_brand : '' ) ) ); ?>">
									<?php echo esc_html( $cat->name ); ?> <span class="filter-count" style="font-size: 0.8rem; opacity: 0.7; font-weight: normal; margin-left: 4px;">(<?php echo esc_html( $cat->dynamic_count ); ?>)</span>
								</a>
							</li>
						<?php endforeach; ?>
					<?php endif; ?>
				</ul>
			</div>

			<div class="sidebar-filter-block">
				<h3>Thương Hiệu / Hãng</h3>
				<ul class="filter-list">
					<li class="<?php echo empty( $active_brand ) ? 'active' : ''; ?>">
						<a href="<?php echo esc_url( add_query_arg( 'filter_brand', '', $base_url . ( ! empty( $active_cat ) ? '?filter_cat=' . $active_cat : '' ) ) ); ?>">
							Tất cả thương hiệu
						</a>
					</li>
					<?php if ( ! is_wp_error( $brands ) && ! empty( $brands ) ) : ?>
						<?php foreach ( $brands as $brand ) : ?>
							<li class="<?php echo ( $active_brand === $brand->slug ) ? 'active' : ''; ?>">
								<a href="<?php echo esc_url( add_query_arg( 'filter_brand', $brand->slug, $base_url . ( ! empty( $active_cat ) ? '?filter_cat=' . $active_cat : '' ) ) ); ?>">
									<?php echo esc_html($brand->name); ?> <span class="filter-count" style="font-size: 0.8rem; opacity: 0.7; font-weight: normal; margin-left: 4px;">(<?php echo esc_html( $brand->dynamic_count ); ?>)</span>
								</a>
							</li>
						<?php endforeach; ?>
					<?php endif; ?>
				</ul>
			</div>

			<?php if ( ! empty( $active_cat ) || ! empty( $active_brand ) ) : ?>
				<div class="sidebar-clear-block">
					<a href="<?php echo esc_url( $base_url ); ?>" class="btn btn-secondary btn-sm btn-clear-filters">Xóa Bộ Lọc</a>
				</div>
			<?php endif; ?>
		</aside>

		<script>
		document.addEventListener('DOMContentLoaded', function() {
			var filterTrigger = document.getElementById('mobile-filter-trigger');
			var sidebarDrawer = document.getElementById('catalog-sidebar-drawer');
			
			function initMobileToggle() {
				filterTrigger = document.getElementById('mobile-filter-trigger');
				sidebarDrawer = document.getElementById('catalog-sidebar-drawer');
				if (filterTrigger && sidebarDrawer) {
					// Remove old listeners by recreating the node or clone
					var newTrigger = filterTrigger.cloneNode(true);
					filterTrigger.parentNode.replaceChild(newTrigger, filterTrigger);
					filterTrigger = newTrigger;
					
					filterTrigger.addEventListener('click', function() {
						sidebarDrawer.classList.toggle('active');
						filterTrigger.classList.toggle('active');
					});
				}
			}
			
			initMobileToggle();

			// Ajax filtering logic
			var catalogLayout = document.querySelector('.catalog-layout');
			if (!catalogLayout) return;

			function initAjaxFilters() {
				// Select all links inside the filter sidebar and pagination/navigation links
				var selectors = '.catalog-sidebar a, .catalog-content .pagination a, .catalog-content .navigation a, .btn-clear-filters';
				var links = document.querySelectorAll(selectors);
				
				links.forEach(function(link) {
					link.addEventListener('click', function(e) {
						var url = this.getAttribute('href');
						if (!url || url === '#' || url.startsWith('javascript:')) return;
						e.preventDefault();
						fetchPage(url);
					});
				});
			}

			function fetchPage(url) {
				var contentContainer = document.querySelector('.catalog-content');
				var sidebarContainer = document.getElementById('catalog-sidebar-drawer');
				
				if (contentContainer) {
					contentContainer.style.opacity = '0.5';
					contentContainer.classList.add('loading-fade');
				}
				if (sidebarContainer) {
					sidebarContainer.style.opacity = '0.5';
				}

				fetch(url)
					.then(function(response) {
						if (!response.ok) throw new Error('Network response was not ok');
						return response.text();
					})
					.then(function(html) {
						var parser = new DOMParser();
						var doc = parser.parseFromString(html, 'text/html');

						var newContent = doc.querySelector('.catalog-content');
						var newSidebar = doc.querySelector('.catalog-sidebar');

						if (newContent && contentContainer) {
							contentContainer.innerHTML = newContent.innerHTML;
							contentContainer.style.opacity = '1';
							contentContainer.classList.remove('loading-fade');
						}

						if (newSidebar && sidebarContainer) {
							sidebarContainer.innerHTML = newSidebar.innerHTML;
							sidebarContainer.style.opacity = '1';
						}

						// Update browser URL
						history.pushState(null, '', url);

						// Re-bind click events
						initAjaxFilters();
						initMobileToggle();

						// Smooth scroll back to top of catalog layout
						var catalogTop = document.querySelector('.catalog-layout');
						if (catalogTop) {
							catalogTop.scrollIntoView({ behavior: 'smooth' });
						}
					})
					.catch(function(err) {
						console.error('Ajax fetch failed. Redirecting...', err);
						window.location.href = url; // Fallback to standard HTTP request
					});
			}

			// Handle browser back/forward buttons
			window.addEventListener('popstate', function() {
				window.location.reload();
			});

			initAjaxFilters();
		});
		</script>
		<?php
	}
}

/**
 * Register Shortcode for Filter Sidebar
 */
function sanitary_product_filter_shortcode() {
	ob_start();
	sanitary_get_products_filter_sidebar();
	return ob_get_clean();
}
add_shortcode( 'sanitary_product_filter', 'sanitary_product_filter_shortcode' );

/**
 * Enqueue plugin styles
 */
function sanitary_catalog_core_styles() {
	wp_enqueue_style( 'sanitary-catalog-filter-css', plugins_url( 'assets/css/filter-style.css', __FILE__ ), [], '1.0.0' );
}
add_action( 'wp_enqueue_scripts', 'sanitary_catalog_core_styles' );

/**
 * Inject dynamic customizer styles, Google Fonts and Favicon to head
 */
function sanitary_inject_customizer_styles() {
	$primary = get_theme_mod( 'sanitary_color_primary', '#0f172a' );
	$secondary = get_theme_mod( 'sanitary_color_secondary', '#475569' );
	$accent = get_theme_mod( 'sanitary_color_accent', '#d97706' );
	$accent_hover = get_theme_mod( 'sanitary_color_accent_hover', '#b45309' );
	$font_family = get_theme_mod( 'sanitary_font_family', 'Inter' );
	
	// Import selected Google Font
	$font_slug = str_replace( ' ', '+', $font_family );
	echo "<link rel='preconnect' href='https://fonts.googleapis.com'>\n";
	echo "<link rel='preconnect' href='https://fonts.gstatic.com' crossorigin>\n";
	echo "<link href='https://fonts.googleapis.com/css2?family=" . esc_attr( $font_slug ) . ":wght@300;400;500;600;700;800&display=swap' rel='stylesheet'>\n";
	
	?>
	<style id="sanitary-custom-styles">
		:root {
			--color-primary: <?php echo esc_html( $primary ); ?>;
			--color-secondary: <?php echo esc_html( $secondary ); ?>;
			--color-accent: <?php echo esc_html( $accent ); ?>;
			--color-accent-hover: <?php echo esc_html( $accent_hover ); ?>;
			--font-sans: '<?php echo esc_html( $font_family ); ?>', -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
		}
	</style>
	<?php
	
	// Inject Favicon if set
	$favicon = get_theme_mod( 'sanitary_favicon_url' );
	if ( ! empty( $favicon ) ) {
		echo '<link rel="shortcut icon" href="' . esc_url( $favicon ) . '" type="image/x-icon" />' . "\n";
	}
}
add_action( 'wp_head', 'sanitary_inject_customizer_styles', 99 );

/**
 * Load Fallback Templates from Plugin if Theme doesn't provide them
 */
function sanitary_product_templates_fallback( $template ) {
	if ( is_post_type_archive( 'sanitary_product' ) || is_tax( 'product_cat' ) || is_tax( 'product_brand' ) ) {
		$theme_file = locate_template( [ 'archive-sanitary_product.php' ] );
		if ( ! $theme_file ) {
			$fallback = plugin_dir_path( __FILE__ ) . 'templates/archive-sanitary_product.php';
			if ( file_exists( $fallback ) ) {
				return $fallback;
			}
		}
	}
	return $template;
}
add_filter( 'archive_template', 'sanitary_product_templates_fallback' );
add_filter( 'taxonomy_template', 'sanitary_product_templates_fallback' );

function sanitary_product_single_template_fallback( $template ) {
	if ( is_singular( 'sanitary_product' ) ) {
		$theme_file = locate_template( [ 'single-sanitary_product.php' ] );
		if ( ! $theme_file ) {
			$fallback = plugin_dir_path( __FILE__ ) . 'templates/single-sanitary_product.php';
			if ( file_exists( $fallback ) ) {
				return $fallback;
			}
		}
	}
	return $template;
}
add_filter( 'single_template', 'sanitary_product_single_template_fallback' );

/**
 * Enqueue Admin Scripts for Media Uploader & Tabs
 */
function sanitary_admin_enqueue_assets( $hook ) {
	// Only load on our settings page
	if ( 'sanitary_product_page_sanitary-settings' !== $hook ) {
		return;
	}
	wp_enqueue_media();
	wp_enqueue_style( 'wp-color-picker' );
	wp_enqueue_script( 'wp-color-picker' );
}
add_action( 'admin_enqueue_scripts', 'sanitary_admin_enqueue_assets' );

/**
 * Register Admin Settings Page for Website Info
 */
function sanitary_register_settings_page() {
	add_submenu_page(
		'edit.php?post_type=sanitary_product',
		__( 'Cấu hình Thông tin Website', 'sanitary-catalog-core' ),
		__( 'Thông tin Website', 'sanitary-catalog-core' ),
		'manage_options',
		'sanitary-settings',
		'sanitary_render_settings_page'
	);
}
add_action( 'admin_menu', 'sanitary_register_settings_page' );

/**
 * Render Admin Settings Page
 */
function sanitary_render_settings_page() {
	if ( ! current_user_can( 'manage_options' ) ) {
		return;
	}

	// Save settings if form is submitted
	if ( isset( $_POST['sanitary_settings_nonce_field'] ) && wp_verify_nonce( $_POST['sanitary_settings_nonce_field'], 'sanitary_save_settings' ) ) {
		// Save Tab 1: General Info
		set_theme_mod( 'sanitary_hotline', sanitize_text_field( $_POST['sanitary_hotline'] ) );
		set_theme_mod( 'sanitary_hotline_tel', sanitize_text_field( $_POST['sanitary_hotline_tel'] ) );
		set_theme_mod( 'sanitary_zalo_url', esc_url_raw( $_POST['sanitary_zalo_url'] ) );
		set_theme_mod( 'sanitary_address', sanitize_text_field( $_POST['sanitary_address'] ) );
		set_theme_mod( 'sanitary_email', sanitize_email( $_POST['sanitary_email'] ) );
		set_theme_mod( 'sanitary_working_hours', sanitize_text_field( $_POST['sanitary_working_hours'] ) );
		set_theme_mod( 'sanitary_facebook_url', esc_url_raw( $_POST['sanitary_facebook_url'] ) );
		set_theme_mod( 'sanitary_copyright', sanitize_text_field( $_POST['sanitary_copyright'] ) );

		// Save Tab 2: Slides JSON Configuration
		if ( isset( $_POST['sanitary_slides'] ) ) {
			set_theme_mod( 'sanitary_slides', wp_unslash( $_POST['sanitary_slides'] ) );
		}

		// Save Section Layouts
		$layout_keys = [ 'hero_banner', 'commitment_strip', 'promotions', 'services', 'category_products', 'latest_products', 'brands', 'projects' ];
		foreach ( $layout_keys as $key ) {
			$visible = isset( $_POST['sanitary_visible_' . $key] ) ? 1 : 0;
			$order = isset( $_POST['sanitary_order_' . $key] ) ? intval( $_POST['sanitary_order_' . $key] ) : 0;
			set_theme_mod( 'sanitary_visible_' . $key, $visible );
			set_theme_mod( 'sanitary_order_' . $key, $order );
		}

		// Save Tab 3: Customization
		if ( isset( $_POST['sanitary_color_primary'] ) ) {
			set_theme_mod( 'sanitary_color_primary', sanitize_hex_color( $_POST['sanitary_color_primary'] ) );
		}
		if ( isset( $_POST['sanitary_color_secondary'] ) ) {
			set_theme_mod( 'sanitary_color_secondary', sanitize_hex_color( $_POST['sanitary_color_secondary'] ) );
		}
		if ( isset( $_POST['sanitary_color_accent'] ) ) {
			set_theme_mod( 'sanitary_color_accent', sanitize_hex_color( $_POST['sanitary_color_accent'] ) );
		}
		if ( isset( $_POST['sanitary_color_accent_hover'] ) ) {
			set_theme_mod( 'sanitary_color_accent_hover', sanitize_hex_color( $_POST['sanitary_color_accent_hover'] ) );
		}
		if ( isset( $_POST['sanitary_font_family'] ) ) {
			set_theme_mod( 'sanitary_font_family', sanitize_text_field( $_POST['sanitary_font_family'] ) );
		}
		if ( isset( $_POST['sanitary_logo_url'] ) ) {
			set_theme_mod( 'sanitary_logo_url', esc_url_raw( $_POST['sanitary_logo_url'] ) );
		}
		if ( isset( $_POST['sanitary_favicon_url'] ) ) {
			set_theme_mod( 'sanitary_favicon_url', esc_url_raw( $_POST['sanitary_favicon_url'] ) );
		}

		// Save Tab 4: Titles
		if ( isset( $_POST['sanitary_title_services'] ) ) {
			set_theme_mod( 'sanitary_title_services', sanitize_text_field( $_POST['sanitary_title_services'] ) );
		}
		if ( isset( $_POST['sanitary_subtitle_services'] ) ) {
			set_theme_mod( 'sanitary_subtitle_services', sanitize_text_field( $_POST['sanitary_subtitle_services'] ) );
		}
		if ( isset( $_POST['sanitary_title_latest'] ) ) {
			set_theme_mod( 'sanitary_title_latest', sanitize_text_field( $_POST['sanitary_title_latest'] ) );
		}
		if ( isset( $_POST['sanitary_subtitle_latest'] ) ) {
			set_theme_mod( 'sanitary_subtitle_latest', sanitize_text_field( $_POST['sanitary_subtitle_latest'] ) );
		}
		if ( isset( $_POST['sanitary_title_brands'] ) ) {
			set_theme_mod( 'sanitary_title_brands', sanitize_text_field( $_POST['sanitary_title_brands'] ) );
		}
		if ( isset( $_POST['sanitary_subtitle_brands'] ) ) {
			set_theme_mod( 'sanitary_subtitle_brands', sanitize_text_field( $_POST['sanitary_subtitle_brands'] ) );
		}
		if ( isset( $_POST['sanitary_title_projects'] ) ) {
			set_theme_mod( 'sanitary_title_projects', sanitize_text_field( $_POST['sanitary_title_projects'] ) );
		}
		if ( isset( $_POST['sanitary_subtitle_projects'] ) ) {
			set_theme_mod( 'sanitary_subtitle_projects', sanitize_text_field( $_POST['sanitary_subtitle_projects'] ) );
		}

		// Save Tab 5: Homepage Contents
		// Promotions JSON
		if ( isset( $_POST['sanitary_promotions'] ) ) {
			set_theme_mod( 'sanitary_promotions', wp_unslash( $_POST['sanitary_promotions'] ) );
		}

		// Dynamic lists JSON
		if ( isset( $_POST['sanitary_commitments'] ) ) {
			set_theme_mod( 'sanitary_commitments', wp_unslash( $_POST['sanitary_commitments'] ) );
		}
		if ( isset( $_POST['sanitary_services'] ) ) {
			set_theme_mod( 'sanitary_services', wp_unslash( $_POST['sanitary_services'] ) );
		}
		if ( isset( $_POST['sanitary_projects'] ) ) {
			set_theme_mod( 'sanitary_projects', wp_unslash( $_POST['sanitary_projects'] ) );
		}

		echo '<div class="notice notice-success is-dismissible"><p><strong>' . esc_html__( 'Cập nhật cấu hình website thành công!', 'sanitary-catalog-core' ) . '</strong></p></div>';
	}

	// Retrieve current values - Tab 1
	$hotline = get_theme_mod( 'sanitary_hotline', '090 123 4567' );
	$hotline_tel = get_theme_mod( 'sanitary_hotline_tel', '0901234567' );
	$zalo_url = get_theme_mod( 'sanitary_zalo_url', 'https://zalo.me/0901234567' );
	$address = get_theme_mod( 'sanitary_address', 'Showroom Thiết Bị Vệ Sinh Hồng Miên' );
	$email = get_theme_mod( 'sanitary_email', 'contact@example.com' );
	$working_hours = get_theme_mod( 'sanitary_working_hours', '8:00 - 18:00 (Thứ 2 - Chủ Nhật)' );
	$facebook_url = get_theme_mod( 'sanitary_facebook_url', 'https://facebook.com' );
	$copyright = get_theme_mod( 'sanitary_copyright', '© ' . date('Y') . ' Hồng Miên. Tất cả quyền được bảo lưu.' );

	// Retrieve current values - Tab 3: Customization
	$color_primary = get_theme_mod( 'sanitary_color_primary', '#0f172a' );
	$color_secondary = get_theme_mod( 'sanitary_color_secondary', '#475569' );
	$color_accent = get_theme_mod( 'sanitary_color_accent', '#d97706' );
	$color_accent_hover = get_theme_mod( 'sanitary_color_accent_hover', '#b45309' );
	$font_family = get_theme_mod( 'sanitary_font_family', 'Inter' );
	$logo_url = get_theme_mod( 'sanitary_logo_url', '' );
	$favicon_url = get_theme_mod( 'sanitary_favicon_url', '' );

	// Retrieve current values - Tab 4: Titles
	$title_services = get_theme_mod( 'sanitary_title_services', 'DỊCH VỤ CHUYÊN NGHIỆP' );
	$subtitle_services = get_theme_mod( 'sanitary_subtitle_services', 'Quy trình dịch vụ khép kín từ tư vấn, thiết kế bản vẽ đến thi công lắp đặt tại công trình.' );
	$title_latest = get_theme_mod( 'sanitary_title_latest', 'SẢN PHẨM MỚI NHẤT' );
	$subtitle_latest = get_theme_mod( 'sanitary_subtitle_latest', 'Danh mục tất cả các thiết bị vệ sinh nổi bật vừa cập nhật.' );
	$title_brands = get_theme_mod( 'sanitary_title_brands', '6 HÃNG THƯƠNG HIỆU ĐỒNG HÀNH' );
	$subtitle_brands = get_theme_mod( 'sanitary_subtitle_brands', 'Click vào hãng để xem các dòng sản phẩm của hãng đó.' );
	$title_projects = get_theme_mod( 'sanitary_title_projects', 'DỰ ÁN THI CÔNG THỰC TẾ' );
	$subtitle_projects = get_theme_mod( 'sanitary_subtitle_projects', 'Hình ảnh thực tế bàn giao phòng tắm hoàn thiện cho khách hàng.' );

	// Retrieve current values - Tab 5: Homepage Contents
	// Promotions JSON
	$promotions_json = get_theme_mod( 'sanitary_promotions' );
	$promotions = ! empty( $promotions_json ) ? json_decode( $promotions_json, true ) : [];
	if ( empty( $promotions ) || ! is_array( $promotions ) ) {
		$promotions = [
			[
				'title'    => get_theme_mod( 'sanitary_promo1_title', 'COMBO PHÒNG TẮM TRỌN GÓI' ),
				'desc'     => get_theme_mod( 'sanitary_promo1_desc', 'Tiết kiệm lên đến 30% khi đặt trọn bộ thiết bị vệ sinh & thi công lắp đặt.' ),
				'btn_text' => get_theme_mod( 'sanitary_promo1_btn_text', 'Xem chi tiết' ),
				'btn_url'  => get_theme_mod( 'sanitary_promo1_btn_url', $zalo_url ),
				'bg'       => get_theme_mod( 'sanitary_promo1_bg', '' ),
				'tag'      => 'Giá Tốt Nhất',
				'visible'  => 1
			],
			[
				'title'    => get_theme_mod( 'sanitary_promo2_title', 'THIẾT BỊ VỆ SINH NHẬP KHẨU' ),
				'desc'     => get_theme_mod( 'sanitary_promo2_desc', 'Bộ sưu tập bồn cầu thông minh, sen tắm massage cao cấp từ các hãng hàng đầu.' ),
				'btn_text' => get_theme_mod( 'sanitary_promo2_btn_text', 'Liên hệ tư vấn' ),
				'btn_url'  => get_theme_mod( 'sanitary_promo2_btn_url', $zalo_url ),
				'bg'       => get_theme_mod( 'sanitary_promo2_bg', '' ),
				'tag'      => 'Luxury Series',
				'visible'  => 1
			]
		];
	}

	// Commitments
	$commitments_json = get_theme_mod( 'sanitary_commitments' );
	$commitments = ! empty( $commitments_json ) ? json_decode( $commitments_json, true ) : [];
	if ( empty( $commitments ) || ! is_array( $commitments ) ) {
		$commit_defaults = [
			1 => [ 'icon' => '🛡️', 'title' => 'Cam Kết Chính Hãng', 'desc' => 'Đền 200% nếu phát hiện hàng nhái' ],
			2 => [ 'icon' => '🚚', 'title' => 'Vận Chuyển Toàn Quốc', 'desc' => 'Giao hàng tận nơi nhanh chóng' ],
			3 => [ 'icon' => '🔧', 'title' => 'Lắp Đặt Trọn Gói', 'desc' => 'Kỹ thuật viên kinh nghiệm lắp ráp' ],
			4 => [ 'icon' => '💎', 'title' => 'Bảo Hành Dài Hạn', 'desc' => 'Bảo hành chính hãng lỗi 1 đổi 1' ]
		];
		$commitments = [];
		for ( $i = 1; $i <= 4; $i++ ) {
			$commitments[] = [
				'icon'    => get_theme_mod( 'sanitary_commit' . $i . '_icon', $commit_defaults[$i]['icon'] ),
				'title'   => get_theme_mod( 'sanitary_commit' . $i . '_title', $commit_defaults[$i]['title'] ),
				'desc'    => get_theme_mod( 'sanitary_commit' . $i . '_desc', $commit_defaults[$i]['desc'] ),
				'visible' => 1
			];
		}
	}

	// Services
	$services_json = get_theme_mod( 'sanitary_services' );
	$services_data = ! empty( $services_json ) ? json_decode( $services_json, true ) : [];
	if ( empty( $services_data ) || ! is_array( $services_data ) ) {
		$service_defaults = [
			1 => [ 'icon' => '✏️', 'title' => '1. THIẾT KẾ PHÒNG TẮM', 'desc' => 'Tư vấn bố trí không gian, thiết kế bản vẽ kỹ thuật 2D/3D phù hợp với phong thủy và diện tích nhà bạn.' ],
			2 => [ 'icon' => '🏗️', 'title' => '2. THI CÔNG TRỌN GÓI', 'desc' => 'Thi công đường nước, chống thấm, ốp lát gạch nền tường chuẩn kỹ thuật trước khi lắp đặt thiết bị.' ],
			3 => [ 'icon' => '🔧', 'title' => '3. LẮP ĐẶT THIỆT BỊ', 'desc' => 'Lắp ráp bồn cầu, chậu rửa, sen vòi, bồn tắm chuyên nghiệp, đảm bảo không rò rỉ, bảo hành chính hãng.' ]
		];
		$services_data = [];
		for ( $i = 1; $i <= 3; $i++ ) {
			$services_data[] = [
				'icon'    => get_theme_mod( 'sanitary_service' . $i . '_icon', $service_defaults[$i]['icon'] ),
				'title'   => get_theme_mod( 'sanitary_service' . $i . '_title', $service_defaults[$i]['title'] ),
				'desc'    => get_theme_mod( 'sanitary_service' . $i . '_desc', $service_defaults[$i]['desc'] ),
				'visible' => 1
			];
		}
	}

	// Projects
	$projects_json = get_theme_mod( 'sanitary_projects' );
	$projects_data = ! empty( $projects_json ) ? json_decode( $projects_json, true ) : [];
	if ( empty( $projects_data ) || ! is_array( $projects_data ) ) {
		$project_defaults = [
			1 => [ 'title' => 'Thi công phòng tắm Biệt Thự Ecopark', 'desc' => 'Thương hiệu sử dụng: GIFTO GOLD & MANDY', 'img' => '' ],
			2 => [ 'title' => 'Lắp đặt thiết bị vệ sinh Căn Hộ Vinhomes', 'desc' => 'Thương hiệu sử dụng: TAKAMI & TQC', 'img' => '' ],
			3 => [ 'title' => 'Thiết kế & Thi công trọn gói Nhà Phố Quận 2', 'desc' => 'Thương hiệu sử dụng: GIFTO & SDUY', 'img' => '' ]
		];
		$projects_data = [];
		for ( $i = 1; $i <= 3; $i++ ) {
			$projects_data[] = [
				'title'   => get_theme_mod( 'sanitary_project' . $i . '_title', $project_defaults[$i]['title'] ),
				'desc'    => get_theme_mod( 'sanitary_project' . $i . '_desc', $project_defaults[$i]['desc'] ),
				'img'     => get_theme_mod( 'sanitary_project' . $i . '_img', $project_defaults[$i]['img'] ),
				'visible' => 1
			];
		}
	}

	// Retrieve current values - Tab 2
	$slides_json = get_theme_mod( 'sanitary_slides' );
	$slides = ! empty( $slides_json ) ? json_decode( $slides_json, true ) : [];
	if ( empty( $slides ) || ! is_array( $slides ) ) {
		// Migration or Fallback
		$slides = [];
		for ( $i = 1; $i <= 3; $i++ ) {
			$s_title = get_theme_mod( 'sanitary_slide_title_' . $i );
			if ( empty( $s_title ) && $i === 1 ) {
				$s_title = 'THIẾT BỊ VỆ SINH CAO CẤP & THI CÔNG TRỌN GÓI';
			}
			if ( ! empty( $s_title ) ) {
				$slides[] = [
					'title'     => $s_title,
					'desc'      => get_theme_mod( 'sanitary_slide_desc_' . $i, $i === 1 ? 'Giải pháp phòng tắm hoàn hảo từ thiết kế, thi công đến lắp đặt thiết bị chính hãng từ 6 thương hiệu hàng đầu.' : '' ),
					'btn1_text' => get_theme_mod( 'sanitary_slide_btn1_text_' . $i, $i === 1 ? 'Nhận báo giá qua Zalo' : '' ),
					'btn1_url'  => get_theme_mod( 'sanitary_slide_btn1_url_' . $i, $i === 1 ? 'https://zalo.me/0901234567' : '' ),
					'btn2_text' => get_theme_mod( 'sanitary_slide_btn2_text_' . $i, $i === 1 ? 'Xem các hãng liên kết' : '' ),
					'btn2_url'  => get_theme_mod( 'sanitary_slide_btn2_url_' . $i, $i === 1 ? '#brands' : '' ),
					'bg'        => get_theme_mod( 'sanitary_slide_bg_' . $i ),
				];
			}
		}
		if ( empty( $slides ) ) {
			$slides[] = [
				'title'     => 'THIẾT BỊ VỆ SINH CAO CẤP & THI CÔNG TRỌN GÓI',
				'desc'      => 'Giải pháp phòng tắm hoàn hảo từ thiết kế, thi công đến lắp đặt thiết bị chính hãng từ 6 thương hiệu hàng đầu.',
				'btn1_text' => 'Nhận báo giá qua Zalo',
				'btn1_url'  => 'https://zalo.me/0901234567',
				'btn2_text' => 'Xem các hãng liên kết',
				'btn2_url'  => '#brands',
				'bg'        => ''
			];
		}
	}

	$layout_configs = [
		'hero_banner'       => [ 'label' => '1. Banner chính (Hero Banner Slider)', 'default_order' => 1 ],
		'commitment_strip'  => [ 'label' => '2. Thanh cam kết dịch vụ (Chính hãng, Vận chuyển...)', 'default_order' => 2 ],
		'promotions'        => [ 'label' => '3. Khuyến mãi đặc biệt (2 cột banner lớn)', 'default_order' => 3 ],
		'services'          => [ 'label' => '4. Dịch vụ chuyên nghiệp (Thiết kế, Thi công...)', 'default_order' => 4 ],
		'category_products' => [ 'label' => '5. Sản phẩm theo danh mục (Dạng Tab gọn gàng)', 'default_order' => 5 ],
		'latest_products'   => [ 'label' => '6. Sản phẩm mới nhất', 'default_order' => 6 ],
		'brands'            => [ 'label' => '7. Danh sách hãng thương hiệu liên kết', 'default_order' => 7 ],
		'projects'          => [ 'label' => '8. Dự án thi công thực tế', 'default_order' => 8 ],
	];
	?>
	<div class="wrap">
		<h1 style="font-weight: 800; font-size: 1.8rem; margin-bottom: 20px; color: #0f172a;">
			<span class="dashicons dashicons-admin-generic" style="font-size: 1.8rem; width: 1.8rem; height: 1.8rem; margin-right: 8px;"></span>
			<?php echo esc_html__( 'Cấu hình Website Catalogue', 'sanitary-catalog-core' ); ?>
		</h1>

		<!-- Tab navigation bar -->
		<h2 class="nav-tab-wrapper" style="margin-bottom: 25px;">
			<a href="#tab-general" class="nav-tab nav-tab-active sanitary-tab-link"><?php echo esc_html__( 'Thông tin chung', 'sanitary-catalog-core' ); ?></a>
			<a href="#tab-homepage" class="nav-tab sanitary-tab-link"><?php echo esc_html__( 'Cấu hình Slider & Bố Cục', 'sanitary-catalog-core' ); ?></a>
			<a href="#tab-customization" class="nav-tab sanitary-tab-link"><?php echo esc_html__( 'Tùy biến Giao Diện', 'sanitary-catalog-core' ); ?></a>
			<a href="#tab-titles" class="nav-tab sanitary-tab-link"><?php echo esc_html__( 'Tiêu đề các phần', 'sanitary-catalog-core' ); ?></a>
			<a href="#tab-homecontent" class="nav-tab sanitary-tab-link"><?php echo esc_html__( 'Nội dung Trang Chủ', 'sanitary-catalog-core' ); ?></a>
		</h2>

		<form method="post" action="" style="max-width: 850px; background: #fff; padding: 30px; border-radius: 12px; box-shadow: 0 4px 20px rgba(0,0,0,0.05); border: 1px solid #e2e8f0;">
			<?php wp_nonce_field( 'sanitary_save_settings', 'sanitary_settings_nonce_field' ); ?>

			<!-- Tab 1: General Info -->
			<div id="tab-general" class="sanitary-tab-content">
				<h3 style="font-size: 1.2rem; border-bottom: 1px solid #e2e8f0; padding-bottom: 10px; margin-bottom: 20px; color: #1e293b;"><?php echo esc_html__( 'Thông tin liên hệ chân trang & Header', 'sanitary-catalog-core' ); ?></h3>
				
				<div style="display: grid; grid-template-columns: 1fr 1fr; gap: 20px; margin-bottom: 25px;">
					<div>
						<label for="sanitary_hotline" style="display: block; font-weight: 700; margin-bottom: 8px; color: #0f172a;"><?php echo esc_html__( 'Số Hotline hiển thị:', 'sanitary-catalog-core' ); ?></label>
						<input type="text" name="sanitary_hotline" id="sanitary_hotline" value="<?php echo esc_attr( $hotline ); ?>" class="regular-text" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" />
						<p class="description" style="margin-top: 5px; color: #64748b;"><?php echo esc_html__( 'Ví dụ: 090 123 4567', 'sanitary-catalog-core' ); ?></p>
					</div>

					<div>
						<label for="sanitary_hotline_tel" style="display: block; font-weight: 700; margin-bottom: 8px; color: #0f172a;"><?php echo esc_html__( 'Số Hotline cuộc gọi (tel:):', 'sanitary-catalog-core' ); ?></label>
						<input type="text" name="sanitary_hotline_tel" id="sanitary_hotline_tel" value="<?php echo esc_attr( $hotline_tel ); ?>" class="regular-text" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" />
						<p class="description" style="margin-top: 5px; color: #64748b;"><?php echo esc_html__( 'Không chứa khoảng trắng để người dùng bấm là gọi. Ví dụ: 0901234567', 'sanitary-catalog-core' ); ?></p>
					</div>
				</div>

				<div style="margin-bottom: 25px;">
					<label for="sanitary_zalo_url" style="display: block; font-weight: 700; margin-bottom: 8px; color: #0f172a;"><?php echo esc_html__( 'Đường dẫn Zalo Chat:', 'sanitary-catalog-core' ); ?></label>
					<input type="url" name="sanitary_zalo_url" id="sanitary_zalo_url" value="<?php echo esc_url( $zalo_url ); ?>" class="regular-text" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" />
					<p class="description" style="margin-top: 5px; color: #64748b;"><?php echo esc_html__( 'Ví dụ: https://zalo.me/0901234567', 'sanitary-catalog-core' ); ?></p>
				</div>

				<div style="margin-bottom: 25px;">
					<label for="sanitary_address" style="display: block; font-weight: 700; margin-bottom: 8px; color: #0f172a;"><?php echo esc_html__( 'Địa chỉ Showroom:', 'sanitary-catalog-core' ); ?></label>
					<input type="text" name="sanitary_address" id="sanitary_address" value="<?php echo esc_attr( $address ); ?>" class="regular-text" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" />
				</div>

				<div style="display: grid; grid-template-columns: 1fr 1fr; gap: 20px; margin-bottom: 25px;">
					<div>
						<label for="sanitary_email" style="display: block; font-weight: 700; margin-bottom: 8px; color: #0f172a;"><?php echo esc_html__( 'Địa chỉ Email:', 'sanitary-catalog-core' ); ?></label>
						<input type="email" name="sanitary_email" id="sanitary_email" value="<?php echo esc_attr( $email ); ?>" class="regular-text" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" />
					</div>

					<div>
						<label for="sanitary_working_hours" style="display: block; font-weight: 700; margin-bottom: 8px; color: #0f172a;"><?php echo esc_html__( 'Giờ làm việc:', 'sanitary-catalog-core' ); ?></label>
						<input type="text" name="sanitary_working_hours" id="sanitary_working_hours" value="<?php echo esc_attr( $working_hours ); ?>" class="regular-text" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" />
					</div>
				</div>

				<div style="margin-bottom: 25px;">
					<label for="sanitary_facebook_url" style="display: block; font-weight: 700; margin-bottom: 8px; color: #0f172a;"><?php echo esc_html__( 'Đường dẫn Trang Facebook:', 'sanitary-catalog-core' ); ?></label>
					<input type="url" name="sanitary_facebook_url" id="sanitary_facebook_url" value="<?php echo esc_url( $facebook_url ); ?>" class="regular-text" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" />
				</div>

				<div style="margin-bottom: 30px;">
					<label for="sanitary_copyright" style="display: block; font-weight: 700; margin-bottom: 8px; color: #0f172a;"><?php echo esc_html__( 'Thông tin bản quyền chân trang (Copyright):', 'sanitary-catalog-core' ); ?></label>
					<input type="text" name="sanitary_copyright" id="sanitary_copyright" value="<?php echo esc_attr( $copyright ); ?>" class="regular-text" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" />
				</div>
			</div>

			<!-- Tab 2: Homepage Config -->
			<div id="tab-homepage" class="sanitary-tab-content" style="display: none;">
				<h3 style="font-size: 1.2rem; border-bottom: 1px solid #e2e8f0; padding-bottom: 10px; margin-bottom: 20px; color: #1e293b;"><?php echo esc_html__( 'Cấu hình Slider Banner chính', 'sanitary-catalog-core' ); ?></h3>

				<!-- Hidden input to store slides JSON data -->
				<textarea name="sanitary_slides" id="sanitary_slides_data" style="display: none;"><?php echo esc_textarea( json_encode( $slides ) ); ?></textarea>

				<!-- Container for dynamic slide cards -->
				<div id="sanitary-slides-container"></div>

				<div style="margin-bottom: 30px; display: flex; gap: 15px; align-items: center;">
					<button type="button" class="button" id="sanitary-add-slide-btn" style="background: #22c55e; color: #fff; border-color: #16a34a; font-weight: 700; padding: 8px 20px; height: auto; font-size: 0.9rem; border-radius: 6px; box-shadow: 0 4px 6px rgba(34,197,94,0.15);">+ Thêm Slide Mới</button>
					<button type="submit" class="button button-primary" style="background: #d97706; border-color: #b45309; font-weight: 700; padding: 8px 25px; height: auto; font-size: 0.9rem; border-radius: 6px; box-shadow: 0 4px 6px rgba(217, 119, 6, 0.15);"><?php echo esc_html__( 'Lưu Cấu Hình Slide', 'sanitary-catalog-core' ); ?></button>
				</div>

				<h3 style="font-size: 1.2rem; border-bottom: 1px solid #e2e8f0; padding-bottom: 10px; margin-bottom: 15px; color: #1e293b;"><?php echo esc_html__( 'Sắp xếp & Hiển thị bố cục Trang Chủ', 'sanitary-catalog-core' ); ?></h3>
				<p style="color: #64748b; font-size: 0.88rem; margin-bottom: 20px;">
					<?php echo esc_html__( 'Kéo thả không cần thiết. Hãy điền số thứ tự từ nhỏ đến lớn (ví dụ: 1, 2, 3...) để sắp xếp vị trí hiển thị và tích/bỏ tích để ẩn/hiện phần tương ứng.', 'sanitary-catalog-core' ); ?>
				</p>

				<table class="wp-list-table widefat fixed striped" style="margin-bottom: 30px; border-radius: 8px; overflow: hidden;">
					<thead>
						<tr>
							<th style="font-weight: 700; padding: 12px;"><?php echo esc_html__( 'Tên phần bố cục', 'sanitary-catalog-core' ); ?></th>
							<th style="font-weight: 700; padding: 12px; width: 100px; text-align: center;"><?php echo esc_html__( 'Hiển thị', 'sanitary-catalog-core' ); ?></th>
							<th style="font-weight: 700; padding: 12px; width: 120px; text-align: center;"><?php echo esc_html__( 'Thứ tự vị trí', 'sanitary-catalog-core' ); ?></th>
						</tr>
					</thead>
					<tbody>
						<?php foreach ( $layout_configs as $key => $cfg ) : 
							$visible = get_theme_mod( 'sanitary_visible_' . $key, 1 );
							$order = get_theme_mod( 'sanitary_order_' . $key, $cfg['default_order'] );
						?>
							<tr>
								<td style="padding: 12px; font-weight: 600; color: #334155;"><?php echo esc_html( $cfg['label'] ); ?></td>
								<td style="padding: 12px; text-align: center;">
									<input type="checkbox" name="sanitary_visible_<?php echo esc_attr( $key ); ?>" value="1" <?php checked( $visible, 1 ); ?> style="transform: scale(1.2);" />
								</td>
								<td style="padding: 12px; text-align: center;">
									<input type="number" name="sanitary_order_<?php echo esc_attr( $key ); ?>" value="<?php echo esc_attr( $order ); ?>" style="width: 70px; text-align: center; padding: 4px; border-radius: 4px; border: 1px solid #cbd5e1;" min="1" max="50" />
								</td>
							</tr>
						<?php endforeach; ?>
					</tbody>
				</table>
			</div>

			<!-- Tab 3: Customization -->
			<div id="tab-customization" class="sanitary-tab-content" style="display: none;">
				<h3 style="font-size: 1.2rem; border-bottom: 1px solid #e2e8f0; padding-bottom: 10px; margin-bottom: 20px; color: #1e293b;"><?php echo esc_html__( 'Tùy biến Màu sắc, Font chữ & Logo', 'sanitary-catalog-core' ); ?></h3>

				<div style="display: grid; grid-template-columns: 1fr 1fr; gap: 20px; margin-bottom: 25px;">
					<div>
						<label style="display: block; font-weight: 700; margin-bottom: 8px; color: #0f172a;"><?php echo esc_html__( 'Màu chủ đạo (Primary Color):', 'sanitary-catalog-core' ); ?></label>
						<input type="text" name="sanitary_color_primary" value="<?php echo esc_attr( $color_primary ); ?>" class="sanitary-color-field" data-default-color="#0f172a" />
					</div>
					<div>
						<label style="display: block; font-weight: 700; margin-bottom: 8px; color: #0f172a;"><?php echo esc_html__( 'Màu phụ (Secondary Color):', 'sanitary-catalog-core' ); ?></label>
						<input type="text" name="sanitary_color_secondary" value="<?php echo esc_attr( $color_secondary ); ?>" class="sanitary-color-field" data-default-color="#475569" />
					</div>
				</div>

				<div style="display: grid; grid-template-columns: 1fr 1fr; gap: 20px; margin-bottom: 25px;">
					<div>
						<label style="display: block; font-weight: 700; margin-bottom: 8px; color: #0f172a;"><?php echo esc_html__( 'Màu nhấn/nút bấm (Accent Color):', 'sanitary-catalog-core' ); ?></label>
						<input type="text" name="sanitary_color_accent" value="<?php echo esc_attr( $color_accent ); ?>" class="sanitary-color-field" data-default-color="#d97706" />
					</div>
					<div>
						<label style="display: block; font-weight: 700; margin-bottom: 8px; color: #0f172a;"><?php echo esc_html__( 'Màu nhấn khi di chuột (Accent Hover):', 'sanitary-catalog-core' ); ?></label>
						<input type="text" name="sanitary_color_accent_hover" value="<?php echo esc_attr( $color_accent_hover ); ?>" class="sanitary-color-field" data-default-color="#b45309" />
					</div>
				</div>

				<div style="margin-bottom: 25px;">
					<label for="sanitary_font_family" style="display: block; font-weight: 700; margin-bottom: 8px; color: #0f172a;"><?php echo esc_html__( 'Font chữ trang web (Google Fonts):', 'sanitary-catalog-core' ); ?></label>
					<select name="sanitary_font_family" id="sanitary_font_family" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1; font-size: 0.95rem;">
						<?php
						$fonts = [ 'Inter', 'Roboto', 'Montserrat', 'Outfit', 'Playfair Display', 'Open Sans', 'Poppins' ];
						foreach ( $fonts as $font ) {
							echo '<option value="' . esc_attr( $font ) . '" ' . selected( $font_family, $font, false ) . '>' . esc_html( $font ) . '</option>';
						}
						?>
					</select>
				</div>

				<div style="display: grid; grid-template-columns: 1fr 1fr; gap: 20px; margin-bottom: 25px;">
					<div>
						<label style="display: block; font-weight: 700; margin-bottom: 8px; color: #0f172a;">Logo Website:</label>
						<div style="display: flex; gap: 10px; margin-bottom: 8px;">
							<input type="text" name="sanitary_logo_url" id="sanitary_logo_url_input" value="<?php echo esc_url( $logo_url ); ?>" style="flex-grow: 1; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" placeholder="http://..." />
							<button type="button" class="button" id="sanitary_select_logo_btn">Chọn Logo</button>
						</div>
						<div style="background: #f8fafc; padding: 10px; border-radius: 6px; border: 1px dashed #cbd5e1; text-align: center;">
							<img id="sanitary_logo_preview" src="<?php echo esc_url( $logo_url ); ?>" style="max-height: 80px; max-width: 100%; display: <?php echo ! empty( $logo_url ) ? 'inline-block' : 'none'; ?>;" />
						</div>
					</div>
					<div>
						<label style="display: block; font-weight: 700; margin-bottom: 8px; color: #0f172a;">Favicon Website:</label>
						<div style="display: flex; gap: 10px; margin-bottom: 8px;">
							<input type="text" name="sanitary_favicon_url" id="sanitary_favicon_url_input" value="<?php echo esc_url( $favicon_url ); ?>" style="flex-grow: 1; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" placeholder="http://..." />
							<button type="button" class="button" id="sanitary_select_favicon_btn">Chọn Favicon</button>
						</div>
						<div style="background: #f8fafc; padding: 10px; border-radius: 6px; border: 1px dashed #cbd5e1; text-align: center;">
							<img id="sanitary_favicon_preview" src="<?php echo esc_url( $favicon_url ); ?>" style="max-height: 32px; max-width: 100%; display: <?php echo ! empty( $favicon_url ) ? 'inline-block' : 'none'; ?>;" />
						</div>
					</div>
				</div>
			</div>

			<!-- Tab 4: Titles Customization -->
			<div id="tab-titles" class="sanitary-tab-content" style="display: none;">
				<h3 style="font-size: 1.2rem; border-bottom: 1px solid #e2e8f0; padding-bottom: 10px; margin-bottom: 20px; color: #1e293b;"><?php echo esc_html__( 'Tùy biến Tiêu đề các phần trang chủ', 'sanitary-catalog-core' ); ?></h3>

				<!-- Section: Services -->
				<div style="background: #f8fafc; border: 1px solid #e2e8f0; padding: 20px; border-radius: 8px; margin-bottom: 20px;">
					<h4 style="margin: 0 0 15px 0; font-weight: 700; color: #0f172a;">Khối 4: Dịch vụ chuyên nghiệp</h4>
					<div style="display: grid; grid-template-columns: 1fr; gap: 15px;">
						<div>
							<label style="display: block; font-weight: 600; margin-bottom: 5px; color: #475569;">Tiêu đề chính:</label>
							<input type="text" name="sanitary_title_services" value="<?php echo esc_attr( $title_services ); ?>" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" />
						</div>
						<div>
							<label style="display: block; font-weight: 600; margin-bottom: 5px; color: #475569;">Tiêu đề phụ/Mô tả:</label>
							<input type="text" name="sanitary_subtitle_services" value="<?php echo esc_attr( $subtitle_services ); ?>" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" />
						</div>
					</div>
				</div>

				<!-- Section: Latest Products -->
				<div style="background: #f8fafc; border: 1px solid #e2e8f0; padding: 20px; border-radius: 8px; margin-bottom: 20px;">
					<h4 style="margin: 0 0 15px 0; font-weight: 700; color: #0f172a;">Khối 6: Sản phẩm mới nhất</h4>
					<div style="display: grid; grid-template-columns: 1fr; gap: 15px;">
						<div>
							<label style="display: block; font-weight: 600; margin-bottom: 5px; color: #475569;">Tiêu đề chính:</label>
							<input type="text" name="sanitary_title_latest" value="<?php echo esc_attr( $title_latest ); ?>" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" />
						</div>
						<div>
							<label style="display: block; font-weight: 600; margin-bottom: 5px; color: #475569;">Tiêu đề phụ/Mô tả:</label>
							<input type="text" name="sanitary_subtitle_latest" value="<?php echo esc_attr( $subtitle_latest ); ?>" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" />
						</div>
					</div>
				</div>

				<!-- Section: Brands -->
				<div style="background: #f8fafc; border: 1px solid #e2e8f0; padding: 20px; border-radius: 8px; margin-bottom: 20px;">
					<h4 style="margin: 0 0 15px 0; font-weight: 700; color: #0f172a;">Khối 7: Đối tác thương hiệu</h4>
					<div style="display: grid; grid-template-columns: 1fr; gap: 15px;">
						<div>
							<label style="display: block; font-weight: 600; margin-bottom: 5px; color: #475569;">Tiêu đề chính:</label>
							<input type="text" name="sanitary_title_brands" value="<?php echo esc_attr( $title_brands ); ?>" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" />
						</div>
						<div>
							<label style="display: block; font-weight: 600; margin-bottom: 5px; color: #475569;">Tiêu đề phụ/Mô tả:</label>
							<input type="text" name="sanitary_subtitle_brands" value="<?php echo esc_attr( $subtitle_brands ); ?>" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" />
						</div>
					</div>
				</div>

				<!-- Section: Projects -->
				<div style="background: #f8fafc; border: 1px solid #e2e8f0; padding: 20px; border-radius: 8px; margin-bottom: 10px;">
					<h4 style="margin: 0 0 15px 0; font-weight: 700; color: #0f172a;">Khối 8: Dự án thực tế</h4>
					<div style="display: grid; grid-template-columns: 1fr; gap: 15px;">
						<div>
							<label style="display: block; font-weight: 600; margin-bottom: 5px; color: #475569;">Tiêu đề chính:</label>
							<input type="text" name="sanitary_title_projects" value="<?php echo esc_attr( $title_projects ); ?>" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" />
						</div>
						<div>
							<label style="display: block; font-weight: 600; margin-bottom: 5px; color: #475569;">Tiêu đề phụ/Mô tả:</label>
							<input type="text" name="sanitary_subtitle_projects" value="<?php echo esc_attr( $subtitle_projects ); ?>" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" />
						</div>
					</div>
				</div>
			</div>

			<!-- Tab 5: Homepage Contents -->
			<div id="tab-homecontent" class="sanitary-tab-content" style="display: none;">
				<h3 style="font-size: 1.2rem; border-bottom: 1px solid #e2e8f0; padding-bottom: 10px; margin-bottom: 20px; color: #1e293b;"><?php echo esc_html__( 'Cấu hình nội dung các khối Trang Chủ', 'sanitary-catalog-core' ); ?></h3>

				<!-- Khối 3: Khuyến Mãi (JSON dynamic) -->
				<div style="background: #f8fafc; border: 1px solid #e2e8f0; padding: 20px; border-radius: 8px; margin-bottom: 25px;">
					<h4 style="margin: 0 0 15px 0; font-weight: 700; color: #0f172a; border-bottom: 1px solid #cbd5e1; padding-bottom: 5px;">Khối 3: Khuyến mãi đặc biệt (2 cột banner lớn hoặc nhiều hơn)</h4>
					
					<!-- Hidden input to store JSON data -->
					<textarea name="sanitary_promotions" id="sanitary_promotions_data" style="display: none;"><?php echo esc_textarea( json_encode( $promotions ) ); ?></textarea>
					
					<!-- Container for dynamic cards -->
					<div id="sanitary-promotions-container"></div>
					
					<button type="button" class="button" id="sanitary-add-promo-btn" style="background: #22c55e; color: #fff; border-color: #16a34a; font-weight: 700; padding: 6px 15px; border-radius: 6px; margin-top: 10px;">+ Thêm Khuyến Mãi Mới</button>
				</div>

				<!-- Khối 2: Cam kết dịch vụ (JSON dynamic) -->
				<div style="background: #f8fafc; border: 1px solid #e2e8f0; padding: 20px; border-radius: 8px; margin-bottom: 25px;">
					<h4 style="margin: 0 0 15px 0; font-weight: 700; color: #0f172a; border-bottom: 1px solid #cbd5e1; padding-bottom: 5px;">Khối 2: Thanh cam kết dịch vụ</h4>
					
					<!-- Hidden input to store JSON data -->
					<textarea name="sanitary_commitments" id="sanitary_commitments_data" style="display: none;"><?php echo esc_textarea( json_encode( $commitments ) ); ?></textarea>
					
					<!-- Container for dynamic cards -->
					<div id="sanitary-commitments-container"></div>
					
					<button type="button" class="button" id="sanitary-add-commit-btn" style="background: #22c55e; color: #fff; border-color: #16a34a; font-weight: 700; padding: 6px 15px; border-radius: 6px; margin-top: 10px;">+ Thêm Cam kết Mới</button>
				</div>

				<!-- Khối 4: Dịch vụ chuyên nghiệp (JSON dynamic) -->
				<div style="background: #f8fafc; border: 1px solid #e2e8f0; padding: 20px; border-radius: 8px; margin-bottom: 25px;">
					<h4 style="margin: 0 0 15px 0; font-weight: 700; color: #0f172a; border-bottom: 1px solid #cbd5e1; padding-bottom: 5px;">Khối 4: Dịch vụ chuyên nghiệp</h4>
					
					<!-- Hidden input to store JSON data -->
					<textarea name="sanitary_services" id="sanitary_services_data" style="display: none;"><?php echo esc_textarea( json_encode( $services_data ) ); ?></textarea>
					
					<!-- Container for dynamic cards -->
					<div id="sanitary-services-container"></div>
					
					<button type="button" class="button" id="sanitary-add-service-btn" style="background: #22c55e; color: #fff; border-color: #16a34a; font-weight: 700; padding: 6px 15px; border-radius: 6px; margin-top: 10px;">+ Thêm Dịch vụ Mới</button>
				</div>

				<!-- Khối 8: Dự án thi công thực tế (JSON dynamic) -->
				<div style="background: #f8fafc; border: 1px solid #e2e8f0; padding: 20px; border-radius: 8px; margin-bottom: 10px;">
					<h4 style="margin: 0 0 15px 0; font-weight: 700; color: #0f172a; border-bottom: 1px solid #cbd5e1; padding-bottom: 5px;">Khối 8: Dự án thi công thực tế</h4>
					
					<!-- Hidden input to store JSON data -->
					<textarea name="sanitary_projects" id="sanitary_projects_data" style="display: none;"><?php echo esc_textarea( json_encode( $projects_data ) ); ?></textarea>
					
					<!-- Container for dynamic cards -->
					<div id="sanitary-projects-container"></div>
					
					<button type="button" class="button" id="sanitary-add-project-btn" style="background: #22c55e; color: #fff; border-color: #16a34a; font-weight: 700; padding: 6px 15px; border-radius: 6px; margin-top: 10px;">+ Thêm Dự án Mới</button>
				</div>
			</div>

			<div style="margin-top: 20px; padding-top: 20px; border-top: 1px solid #e2e8f0; display: flex; justify-content: flex-end;">
				<button type="submit" class="button button-primary button-large" style="background: #d97706; border-color: #b45309; font-weight: 700; padding: 8px 30px; height: auto; font-size: 0.95rem; border-radius: 6px; box-shadow: 0 4px 6px rgba(217, 119, 6, 0.15);"><?php echo esc_html__( 'Lưu Cấu Hình', 'sanitary-catalog-core' ); ?></button>
			</div>
		</form>
	</div>

	<script>
	jQuery(document).ready(function($){
		// Tab Switcher
		$('.sanitary-tab-link').click(function(e){
			e.preventDefault();
			$('.sanitary-tab-link').removeClass('nav-tab-active');
			$(this).addClass('nav-tab-active');
			$('.sanitary-tab-content').hide();
			$($(this).attr('href')).show();
		});

		var container = $('#sanitary-slides-container');
		var rawInput = $('#sanitary_slides_data');
		var slidesData = [];
		try {
			slidesData = JSON.parse(rawInput.val() || '[]');
		} catch(e) {
			slidesData = [];
		}

		// Ensure active slides have visible field
		slidesData.forEach(function(slide) {
			if (slide.visible === undefined) {
				slide.visible = 1;
			}
		});

		function updateRawInput() {
			rawInput.val(JSON.stringify(slidesData));
		}

		function renderSlides(openIdx) {
			container.empty();
			if (slidesData.length === 0) {
				container.append('<p style="color: #64748b; font-style: italic; margin-bottom: 20px;">Chưa có slide nào. Vui lòng bấm nút thêm mới ở dưới.</p>');
				return;
			}
			slidesData.forEach(function(slide, idx) {
				var isVisible = (slide.visible === undefined || slide.visible == 1);
				var statusText = isVisible ? '<span style="background: #e0f2fe; color: #0369a1; padding: 3px 10px; border-radius: 4px; font-size: 0.75rem; font-weight: 700;">Đang hiện</span>' : '<span style="background: #fef2f2; color: #991b1b; padding: 3px 10px; border-radius: 4px; font-size: 0.75rem; font-weight: 700;">Đang ẩn</span>';
				var titleText = slide.title ? slide.title : '(Không có tiêu đề)';
				
				var card = $('<div class="slide-config-card" style="border: 1px solid #cbd5e1; border-radius: 8px; margin-bottom: 15px; background: #fff; box-shadow: 0 2px 5px rgba(0,0,0,0.02); overflow:hidden; opacity: ' + (isVisible ? '1' : '0.7') + ';">' +
					// Header
					'<div class="slide-header" style="padding: 12px 20px; background: #f8fafc; border-bottom: 1px solid #e2e8f0; display: flex; justify-content: space-between; align-items: center; cursor: pointer; user-select: none;">' +
						'<div style="display: flex; align-items: center; gap: 15px;">' +
							(slide.bg ? '<img src="' + slide.bg + '" style="width: 45px; height: 28px; object-fit: cover; border-radius: 4px; border: 1px solid #cbd5e1;" />' : '<div style="width: 45px; height: 28px; background: #e2e8f0; border-radius: 4px; display:flex; align-items:center; justify-content:center; font-size: 0.55rem; color: #94a3b8; font-weight:700;">NO IMG</div>') +
							'<strong style="color: #0f172a; font-size: 0.95rem;">Slide #' + (idx + 1) + ': <span style="font-weight: 500; color: #475569;">' + titleText + '</span></strong>' +
							statusText +
						'</div>' +
						'<div style="display: flex; gap: 8px; align-items: center;">' +
							'<button type="button" class="button toggle-edit-btn" style="font-weight: 600;">Sửa</button>' +
							'<button type="button" class="button toggle-visibility-btn" data-idx="' + idx + '" style="font-weight: 600;">' + (isVisible ? 'Ẩn' : 'Hiện') + '</button>' +
							'<button type="button" class="button remove-slide-btn" data-idx="' + idx + '" style="background: #ef4444; color: #fff; border: none; font-weight: 600; height: auto; padding: 4px 10px; font-size: 0.8rem; cursor: pointer; border-radius: 4px;">Xóa</button>' +
						'</div>' +
					'</div>' +
					// Body Form
					'<div class="slide-body-form" style="padding: 20px; border-top: 1px solid #e2e8f0; background: #fff; display: ' + (openIdx === idx ? 'block' : 'none') + ';">' +
						'<div style="margin-bottom: 15px;">' +
							'<label style="display: block; font-weight: 600; margin-bottom: 5px; color: #1e293b;">Tiêu đề Slide:</label>' +
							'<input type="text" class="slide-title" data-idx="' + idx + '" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" />' +
						'</div>' +
						'<div style="margin-bottom: 15px;">' +
							'<label style="display: block; font-weight: 600; margin-bottom: 5px; color: #1e293b;">Mô tả Slide:</label>' +
							'<textarea class="slide-desc" data-idx="' + idx + '" rows="2" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;"></textarea>' +
						'</div>' +
						'<div style="display: grid; grid-template-columns: 1fr 1fr; gap: 15px; margin-bottom: 15px;">' +
							'<div>' +
								'<label style="display: block; font-weight: 600; margin-bottom: 5px; color: #1e293b;">Nút 1 - Chữ hiển thị:</label>' +
								'<input type="text" class="slide-btn1-text" data-idx="' + idx + '" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" />' +
							'</div>' +
							'<div>' +
								'<label style="display: block; font-weight: 600; margin-bottom: 5px; color: #1e293b;">Nút 1 - Link liên kết:</label>' +
								'<input type="text" class="slide-btn1-url" data-idx="' + idx + '" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" />' +
							'</div>' +
						'</div>' +
						'<div style="display: grid; grid-template-columns: 1fr 1fr; gap: 15px; margin-bottom: 15px;">' +
							'<div>' +
								'<label style="display: block; font-weight: 600; margin-bottom: 5px; color: #1e293b;">Nút 2 - Chữ hiển thị:</label>' +
								'<input type="text" class="slide-btn2-text" data-idx="' + idx + '" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" />' +
							'</div>' +
							'<div>' +
								'<label style="display: block; font-weight: 600; margin-bottom: 5px; color: #1e293b;">Nút 2 - Link liên kết:</label>' +
								'<input type="text" class="slide-btn2-url" data-idx="' + idx + '" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" />' +
							'</div>' +
						'</div>' +
						'<div>' +
							'<label style="display: block; font-weight: 600; margin-bottom: 5px; color: #1e293b;">Hình nền Slide:</label>' +
							'<div style="display: flex; gap: 10px; margin-bottom: 8px;">' +
								'<input type="text" class="slide-bg" id="slide-bg-input-' + idx + '" data-idx="' + idx + '" style="flex-grow: 1; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" placeholder="http://..." />' +
								'<button type="button" class="button select-bg-btn" data-idx="' + idx + '">Chọn Ảnh</button>' +
							'</div>' +
							'<img id="slide-bg-preview-' + idx + '" src="" style="max-width: 200px; max-height: 100px; border-radius: 6px; border: 1px solid #cbd5e1; display: none;" />' +
						'</div>' +
					'</div>' +
				'</div>');

				// Populate values
				card.find('.slide-title').val(slide.title || '');
				card.find('.slide-desc').val(slide.desc || '');
				card.find('.slide-btn1-text').val(slide.btn1_text || '');
				card.find('.slide-btn1-url').val(slide.btn1_url || '');
				card.find('.slide-btn2-text').val(slide.btn2_text || '');
				card.find('.slide-btn2-url').val(slide.btn2_url || '');
				card.find('.slide-bg').val(slide.bg || '');
				if (slide.bg) {
					card.find('#slide-bg-preview-' + idx).attr('src', slide.bg).show();
				}
				container.append(card);
			});
		}

		renderSlides();

		// Toggle edit form on header click
		container.on('click', '.slide-header', function(e) {
			if ($(e.target).closest('button').length) {
				return; // Do not toggle form when clicking header buttons
			}
			e.preventDefault();
			$(this).next('.slide-body-form').slideToggle(200);
		});

		// Toggle edit form on button click
		container.on('click', '.toggle-edit-btn', function(e) {
			e.preventDefault();
			$(this).closest('.slide-config-card').find('.slide-body-form').slideToggle(200);
		});

		// Toggle visibility status
		container.on('click', '.toggle-visibility-btn', function(e) {
			e.preventDefault();
			var idx = $(this).data('idx');
			slidesData[idx].visible = (slidesData[idx].visible === undefined || slidesData[idx].visible == 1) ? 0 : 1;
			renderSlides(idx); // Re-render keeping this slide open
			updateRawInput();
		});

		// Add new Slide
		$('#sanitary-add-slide-btn').click(function(e) {
			e.preventDefault();
			slidesData.push({
				title: '',
				desc: '',
				btn1_text: '',
				btn1_url: '',
				btn2_text: '',
				btn2_url: '',
				bg: '',
				visible: 1
			});
			var newIdx = slidesData.length - 1;
			renderSlides(newIdx); // Re-render and open the newly added slide
			updateRawInput();
		});

		// Remove Slide
		container.on('click', '.remove-slide-btn', function(e) {
			e.preventDefault();
			if (!confirm('Bạn có chắc chắn muốn xóa slide này không?')) {
				return;
			}
			var idx = $(this).data('idx');
			slidesData.splice(idx, 1);
			renderSlides();
			updateRawInput();
		});

		// Change fields
		container.on('input change', '.slide-title, .slide-desc, .slide-btn1-text, .slide-btn1-url, .slide-btn2-text, .slide-btn2-url, .slide-bg', function() {
			var idx = $(this).data('idx');
			var field = '';
			if ($(this).hasClass('slide-title')) {
				field = 'title';
				$(this).closest('.slide-config-card').find('.slide-header strong span').text($(this).val() || '(Không có tiêu đề)');
			}
			else if ($(this).hasClass('slide-desc')) field = 'desc';
			else if ($(this).hasClass('slide-btn1-text')) field = 'btn1_text';
			else if ($(this).hasClass('slide-btn1-url')) field = 'btn1_url';
			else if ($(this).hasClass('slide-btn2-text')) field = 'btn2_text';
			else if ($(this).hasClass('slide-btn2-url')) field = 'btn2_url';
			else if ($(this).hasClass('slide-bg')) {
				field = 'bg';
				var val = $(this).val();
				if (val) {
					$('#slide-bg-preview-' + idx).attr('src', val).show();
					$(this).closest('.slide-config-card').find('.slide-header img').attr('src', val);
				} else {
					$('#slide-bg-preview-' + idx).hide();
				}
			}
			slidesData[idx][field] = $(this).val();
			updateRawInput();
		});

		// Upload BG
		container.on('click', '.select-bg-btn', function(e) {
			e.preventDefault();
			var idx = $(this).data('idx');
			var uploader = wp.media({
				title: 'Chọn hình nền Slide ' + (idx + 1),
				button: { text: 'Sử dụng hình này' },
				multiple: false
			})
			.on('select', function() {
				var attachment = uploader.state().get('selection').first().toJSON();
				$('#slide-bg-input-' + idx).val(attachment.url).trigger('change');
			})
			.open();
		});

		// Initialize WP Color Picker on fields
		if (typeof $.fn.wpColorPicker === 'function') {
			$('.sanitary-color-field').wpColorPicker();
		}

		// Upload Logo
		$('#sanitary_select_logo_btn').click(function(e) {
			e.preventDefault();
			var uploader = wp.media({
				title: 'Chọn Logo Website',
				button: { text: 'Sử dụng Logo này' },
				multiple: false
			})
			.on('select', function() {
				var attachment = uploader.state().get('selection').first().toJSON();
				$('#sanitary_logo_url_input').val(attachment.url);
				$('#sanitary_logo_preview').attr('src', attachment.url).show();
			})
			.open();
		});

		// Upload Favicon
		$('#sanitary_select_favicon_btn').click(function(e) {
			e.preventDefault();
			var uploader = wp.media({
				title: 'Chọn Favicon Website',
				button: { text: 'Sử dụng Favicon này' },
				multiple: false
			})
			.on('select', function() {
				var attachment = uploader.state().get('selection').first().toJSON();
				$('#sanitary_favicon_url_input').val(attachment.url);
				$('#sanitary_favicon_preview').attr('src', attachment.url).show();
			})
			.open();
		});

		// Upload Promo 1 Background
		$('#sanitary_select_promo1_bg_btn').click(function(e) {
			e.preventDefault();
			var uploader = wp.media({
				title: 'Chọn Hình nền Banner 1',
				button: { text: 'Sử dụng hình này' },
				multiple: false
			})
			.on('select', function() {
				var attachment = uploader.state().get('selection').first().toJSON();
				$('#sanitary_promo1_bg_input').val(attachment.url);
				$('#sanitary_promo1_bg_preview').attr('src', attachment.url).show();
			})
			.open();
		});

		// Upload Promo 2 Background
		$('#sanitary_select_promo2_bg_btn').click(function(e) {
			e.preventDefault();
			var uploader = wp.media({
				title: 'Chọn Hình nền Banner 2',
				button: { text: 'Sử dụng hình này' },
				multiple: false
			})
			.on('select', function() {
				var attachment = uploader.state().get('selection').first().toJSON();
				$('#sanitary_promo2_bg_input').val(attachment.url);
				$('#sanitary_promo2_bg_preview').attr('src', attachment.url).show();
			})
			.open();
		});

		// Promotions Dynamic List
		var promotionsContainer = $('#sanitary-promotions-container');
		var promotionsRawInput = $('#sanitary_promotions_data');
		var promotionsData = [];
		try {
			promotionsData = JSON.parse(promotionsRawInput.val() || '[]');
		} catch(e) {
			promotionsData = [];
		}

		function updatePromotionsRawInput() {
			promotionsRawInput.val(JSON.stringify(promotionsData));
		}

		function renderPromotions(openIdx) {
			promotionsContainer.empty();
			if (promotionsData.length === 0) {
				promotionsContainer.append('<p style="color: #64748b; font-style: italic; margin-bottom: 20px;">Chưa có khuyến mãi nào. Vui lòng thêm mới.</p>');
				return;
			}
			promotionsData.forEach(function(item, idx) {
				var isVisible = (item.visible === undefined || item.visible == 1);
				var statusText = isVisible ? '<span style="background: #e0f2fe; color: #0369a1; padding: 3px 10px; border-radius: 4px; font-size: 0.75rem; font-weight: 700;">Đang hiện</span>' : '<span style="background: #fef2f2; color: #991b1b; padding: 3px 10px; border-radius: 4px; font-size: 0.75rem; font-weight: 700;">Đang ẩn</span>';
				var titleText = item.title ? item.title : '(Không có tiêu đề)';
				
				var card = $('<div class="promo-config-card" style="border: 1px solid #cbd5e1; border-radius: 8px; margin-bottom: 15px; background: #fff; box-shadow: 0 2px 5px rgba(0,0,0,0.02); overflow:hidden; opacity: ' + (isVisible ? '1' : '0.7') + ';">' +
					'<div class="promo-header" style="padding: 12px 20px; background: #f8fafc; border-bottom: 1px solid #e2e8f0; display: flex; justify-content: space-between; align-items: center; cursor: pointer; user-select: none;">' +
						'<div style="display: flex; align-items: center; gap: 15px;">' +
							(item.bg ? '<img src="' + item.bg + '" style="width: 45px; height: 28px; object-fit: cover; border-radius: 4px; border: 1px solid #cbd5e1;" />' : '<div style="width: 45px; height: 28px; background: #e2e8f0; border-radius: 4px; display:flex; align-items:center; justify-content:center; font-size: 0.55rem; color: #94a3b8; font-weight:700;">NO IMG</div>') +
							'<strong style="color: #0f172a; font-size: 0.95rem;">Khuyến mãi #' + (idx + 1) + ': <span style="font-weight: 500; color: #475569;">' + titleText + '</span></strong>' +
							statusText +
						'</div>' +
						'<div style="display: flex; gap: 8px; align-items: center;">' +
							'<button type="button" class="button toggle-promo-edit-btn" style="font-weight: 600;">Sửa</button>' +
							'<button type="button" class="button toggle-promo-visibility-btn" data-idx="' + idx + '" style="font-weight: 600;">' + (isVisible ? 'Ẩn' : 'Hiện') + '</button>' +
							'<button type="button" class="button remove-promo-btn" data-idx="' + idx + '" style="background: #ef4444; color: #fff; border: none; font-weight: 600; height: auto; padding: 4px 10px; font-size: 0.8rem; cursor: pointer; border-radius: 4px;">Xóa</button>' +
						'</div>' +
					'</div>' +
					'<div class="promo-body-form" style="padding: 20px; border-top: 1px solid #e2e8f0; background: #fff; display: ' + (openIdx === idx ? 'block' : 'none') + ';">' +
						'<div style="display: grid; grid-template-columns: 1fr 150px; gap: 15px; margin-bottom: 15px;">' +
							'<div>' +
								'<label style="display: block; font-weight: 600; margin-bottom: 5px; color: #1e293b;">Tiêu đề Khuyến mãi:</label>' +
								'<input type="text" class="promo-title" data-idx="' + idx + '" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" />' +
							'</div>' +
							'<div>' +
								'<label style="display: block; font-weight: 600; margin-bottom: 5px; color: #1e293b;">Nhãn (Tag e.g. Hot):</label>' +
								'<input type="text" class="promo-tag-input" data-idx="' + idx + '" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" />' +
							'</div>' +
						'</div>' +
						'<div style="margin-bottom: 15px;">' +
							'<label style="display: block; font-weight: 600; margin-bottom: 5px; color: #1e293b;">Mô tả:</label>' +
							'<input type="text" class="promo-desc" data-idx="' + idx + '" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" />' +
						'</div>' +
						'<div style="display: grid; grid-template-columns: 1fr 1fr; gap: 15px; margin-bottom: 15px;">' +
							'<div>' +
								'<label style="display: block; font-weight: 600; margin-bottom: 5px; color: #1e293b;">Chữ hiển thị trên nút:</label>' +
								'<input type="text" class="promo-btn-text" data-idx="' + idx + '" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" />' +
							'</div>' +
							'<div>' +
								'<label style="display: block; font-weight: 600; margin-bottom: 5px; color: #1e293b;">Liên kết nút bấm:</label>' +
								'<input type="text" class="promo-btn-url" data-idx="' + idx + '" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" />' +
							'</div>' +
						'</div>' +
						'<div>' +
							'<label style="display: block; font-weight: 600; margin-bottom: 5px; color: #1e293b;">Hình nền Banner:</label>' +
							'<div style="display: flex; gap: 10px; margin-bottom: 8px;">' +
								'<input type="text" class="promo-bg" id="promo-bg-input-' + idx + '" data-idx="' + idx + '" style="flex-grow: 1; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" placeholder="http://..." />' +
								'<button type="button" class="button select-promo-bg-btn-dyn" data-idx="' + idx + '">Chọn Ảnh</button>' +
							'</div>' +
							'<img id="promo-bg-preview-' + idx + '" src="" style="max-width: 200px; max-height: 100px; border-radius: 6px; border: 1px solid #cbd5e1; display: none;" />' +
						'</div>' +
					'</div>' +
				'</div>');

				card.find('.promo-title').val(item.title || '');
				card.find('.promo-tag-input').val(item.tag || '');
				card.find('.promo-desc').val(item.desc || '');
				card.find('.promo-btn-text').val(item.btn_text || '');
				card.find('.promo-btn-url').val(item.btn_url || '');
				card.find('.promo-bg').val(item.bg || '');
				if (item.bg) {
					card.find('#promo-bg-preview-' + idx).attr('src', item.bg).show();
				}
				promotionsContainer.append(card);
			});
		}

		// Commitments Dynamic List
		var commitmentsContainer = $('#sanitary-commitments-container');
		var commitmentsRawInput = $('#sanitary_commitments_data');
		var commitmentsData = [];
		try {
			commitmentsData = JSON.parse(commitmentsRawInput.val() || '[]');
		} catch(e) {
			commitmentsData = [];
		}

		function updateCommitmentsRawInput() {
			commitmentsRawInput.val(JSON.stringify(commitmentsData));
		}

		function renderCommitments(openIdx) {
			commitmentsContainer.empty();
			if (commitmentsData.length === 0) {
				commitmentsContainer.append('<p style="color: #64748b; font-style: italic; margin-bottom: 20px;">Chưa có cam kết nào. Vui lòng thêm mới.</p>');
				return;
			}
			commitmentsData.forEach(function(item, idx) {
				var isVisible = (item.visible === undefined || item.visible == 1);
				var statusText = isVisible ? '<span style="background: #e0f2fe; color: #0369a1; padding: 3px 10px; border-radius: 4px; font-size: 0.75rem; font-weight: 700;">Đang hiện</span>' : '<span style="background: #fef2f2; color: #991b1b; padding: 3px 10px; border-radius: 4px; font-size: 0.75rem; font-weight: 700;">Đang ẩn</span>';
				var titleText = item.title ? item.title : '(Không có tiêu đề)';
				var iconText = item.icon ? item.icon : '✨';

				var card = $('<div class="commit-config-card" style="border: 1px solid #cbd5e1; border-radius: 8px; margin-bottom: 15px; background: #fff; box-shadow: 0 2px 5px rgba(0,0,0,0.02); overflow:hidden; opacity: ' + (isVisible ? '1' : '0.7') + ';">' +
					'<div class="commit-header" style="padding: 12px 20px; background: #f8fafc; border-bottom: 1px solid #e2e8f0; display: flex; justify-content: space-between; align-items: center; cursor: pointer; user-select: none;">' +
						'<div style="display: flex; align-items: center; gap: 15px;">' +
							'<span class="commit-icon-display" style="font-size: 1.2rem;">' + iconText + '</span>' +
							'<strong style="color: #0f172a; font-size: 0.95rem;">Cam kết #' + (idx + 1) + ': <span style="font-weight: 500; color: #475569;">' + titleText + '</span></strong>' +
							statusText +
						'</div>' +
						'<div style="display: flex; gap: 8px; align-items: center;">' +
							'<button type="button" class="button toggle-commit-edit-btn" style="font-weight: 600;">Sửa</button>' +
							'<button type="button" class="button toggle-commit-visibility-btn" data-idx="' + idx + '" style="font-weight: 600;">' + (isVisible ? 'Ẩn' : 'Hiện') + '</button>' +
							'<button type="button" class="button remove-commit-btn" data-idx="' + idx + '" style="background: #ef4444; color: #fff; border: none; font-weight: 600; height: auto; padding: 4px 10px; font-size: 0.8rem; cursor: pointer; border-radius: 4px;">Xóa</button>' +
						'</div>' +
					'</div>' +
					'<div class="commit-body-form" style="padding: 20px; border-top: 1px solid #e2e8f0; background: #fff; display: ' + (openIdx === idx ? 'block' : 'none') + ';">' +
						'<div style="display: grid; grid-template-columns: 80px 1fr; gap: 15px; margin-bottom: 15px;">' +
							'<div>' +
								'<label style="display: block; font-weight: 600; margin-bottom: 5px; color: #1e293b;">Icon/Emoji:</label>' +
								'<input type="text" class="commit-icon" data-idx="' + idx + '" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1; text-align: center;" />' +
							'</div>' +
							'<div>' +
								'<label style="display: block; font-weight: 600; margin-bottom: 5px; color: #1e293b;">Tiêu đề cam kết:</label>' +
								'<input type="text" class="commit-title" data-idx="' + idx + '" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" />' +
							'</div>' +
						'</div>' +
						'<div>' +
							'<label style="display: block; font-weight: 600; margin-bottom: 5px; color: #1e293b;">Mô tả:</label>' +
							'<input type="text" class="commit-desc" data-idx="' + idx + '" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" />' +
						'</div>' +
					'</div>' +
				'</div>');

				card.find('.commit-icon').val(item.icon || '');
				card.find('.commit-title').val(item.title || '');
				card.find('.commit-desc').val(item.desc || '');
				commitmentsContainer.append(card);
			});
		}

		// Services Dynamic List
		var servicesContainer = $('#sanitary-services-container');
		var servicesRawInput = $('#sanitary_services_data');
		var servicesData = [];
		try {
			servicesData = JSON.parse(servicesRawInput.val() || '[]');
		} catch(e) {
			servicesData = [];
		}

		function updateServicesRawInput() {
			servicesRawInput.val(JSON.stringify(servicesData));
		}

		function renderServices(openIdx) {
			servicesContainer.empty();
			if (servicesData.length === 0) {
				servicesContainer.append('<p style="color: #64748b; font-style: italic; margin-bottom: 20px;">Chưa có dịch vụ nào. Vui lòng thêm mới.</p>');
				return;
			}
			servicesData.forEach(function(item, idx) {
				var isVisible = (item.visible === undefined || item.visible == 1);
				var statusText = isVisible ? '<span style="background: #e0f2fe; color: #0369a1; padding: 3px 10px; border-radius: 4px; font-size: 0.75rem; font-weight: 700;">Đang hiện</span>' : '<span style="background: #fef2f2; color: #991b1b; padding: 3px 10px; border-radius: 4px; font-size: 0.75rem; font-weight: 700;">Đang ẩn</span>';
				var titleText = item.title ? item.title : '(Không có tiêu đề)';
				var iconText = item.icon ? item.icon : '⚡';

				var card = $('<div class="service-config-card" style="border: 1px solid #cbd5e1; border-radius: 8px; margin-bottom: 15px; background: #fff; box-shadow: 0 2px 5px rgba(0,0,0,0.02); overflow:hidden; opacity: ' + (isVisible ? '1' : '0.7') + ';">' +
					'<div class="service-header" style="padding: 12px 20px; background: #f8fafc; border-bottom: 1px solid #e2e8f0; display: flex; justify-content: space-between; align-items: center; cursor: pointer; user-select: none;">' +
						'<div style="display: flex; align-items: center; gap: 15px;">' +
							'<span class="service-icon-display" style="font-size: 1.2rem;">' + iconText + '</span>' +
							'<strong style="color: #0f172a; font-size: 0.95rem;">Dịch vụ #' + (idx + 1) + ': <span style="font-weight: 500; color: #475569;">' + titleText + '</span></strong>' +
							statusText +
						'</div>' +
						'<div style="display: flex; gap: 8px; align-items: center;">' +
							'<button type="button" class="button toggle-service-edit-btn" style="font-weight: 600;">Sửa</button>' +
							'<button type="button" class="button toggle-service-visibility-btn" data-idx="' + idx + '" style="font-weight: 600;">' + (isVisible ? 'Ẩn' : 'Hiện') + '</button>' +
							'<button type="button" class="button remove-service-btn" data-idx="' + idx + '" style="background: #ef4444; color: #fff; border: none; font-weight: 600; height: auto; padding: 4px 10px; font-size: 0.8rem; cursor: pointer; border-radius: 4px;">Xóa</button>' +
						'</div>' +
					'</div>' +
					'<div class="service-body-form" style="padding: 20px; border-top: 1px solid #e2e8f0; background: #fff; display: ' + (openIdx === idx ? 'block' : 'none') + ';">' +
						'<div style="display: grid; grid-template-columns: 80px 1fr; gap: 15px; margin-bottom: 15px;">' +
							'<div>' +
								'<label style="display: block; font-weight: 600; margin-bottom: 5px; color: #1e293b;">Icon/Emoji:</label>' +
								'<input type="text" class="service-icon" data-idx="' + idx + '" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1; text-align: center;" />' +
							'</div>' +
							'<div>' +
								'<label style="display: block; font-weight: 600; margin-bottom: 5px; color: #1e293b;">Tiêu đề dịch vụ:</label>' +
								'<input type="text" class="service-title" data-idx="' + idx + '" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" />' +
							'</div>' +
						'</div>' +
						'<div>' +
							'<label style="display: block; font-weight: 600; margin-bottom: 5px; color: #1e293b;">Mô tả chi tiết:</label>' +
							'<textarea class="service-desc" data-idx="' + idx + '" rows="3" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;"></textarea>' +
						'</div>' +
					'</div>' +
				'</div>');

				card.find('.service-icon').val(item.icon || '');
				card.find('.service-title').val(item.title || '');
				card.find('.service-desc').val(item.desc || '');
				servicesContainer.append(card);
			});
		}

		// Projects Dynamic List
		var projectsContainer = $('#sanitary-projects-container');
		var projectsRawInput = $('#sanitary_projects_data');
		var projectsData = [];
		try {
			projectsData = JSON.parse(projectsRawInput.val() || '[]');
		} catch(e) {
			projectsData = [];
		}

		function updateProjectsRawInput() {
			projectsRawInput.val(JSON.stringify(projectsData));
		}

		function renderProjects(openIdx) {
			projectsContainer.empty();
			if (projectsData.length === 0) {
				projectsContainer.append('<p style="color: #64748b; font-style: italic; margin-bottom: 20px;">Chưa có dự án nào. Vui lòng thêm mới.</p>');
				return;
			}
			projectsData.forEach(function(item, idx) {
				var isVisible = (item.visible === undefined || item.visible == 1);
				var statusText = isVisible ? '<span style="background: #e0f2fe; color: #0369a1; padding: 3px 10px; border-radius: 4px; font-size: 0.75rem; font-weight: 700;">Đang hiện</span>' : '<span style="background: #fef2f2; color: #991b1b; padding: 3px 10px; border-radius: 4px; font-size: 0.75rem; font-weight: 700;">Đang ẩn</span>';
				var titleText = item.title ? item.title : '(Không có tiêu đề)';
				
				var card = $('<div class="project-config-card" style="border: 1px solid #cbd5e1; border-radius: 8px; margin-bottom: 15px; background: #fff; box-shadow: 0 2px 5px rgba(0,0,0,0.02); overflow:hidden; opacity: ' + (isVisible ? '1' : '0.7') + ';">' +
					'<div class="project-header" style="padding: 12px 20px; background: #f8fafc; border-bottom: 1px solid #e2e8f0; display: flex; justify-content: space-between; align-items: center; cursor: pointer; user-select: none;">' +
						'<div style="display: flex; align-items: center; gap: 15px;">' +
							(item.img ? '<img src="' + item.img + '" style="width: 45px; height: 28px; object-fit: cover; border-radius: 4px; border: 1px solid #cbd5e1;" />' : '<div style="width: 45px; height: 28px; background: #e2e8f0; border-radius: 4px; display:flex; align-items:center; justify-content:center; font-size: 0.55rem; color: #94a3b8; font-weight:700;">NO IMG</div>') +
							'<strong style="color: #0f172a; font-size: 0.95rem;">Dự án #' + (idx + 1) + ': <span style="font-weight: 500; color: #475569;">' + titleText + '</span></strong>' +
							statusText +
						'</div>' +
						'<div style="display: flex; gap: 8px; align-items: center;">' +
							'<button type="button" class="button toggle-project-edit-btn" style="font-weight: 600;">Sửa</button>' +
							'<button type="button" class="button toggle-project-visibility-btn" data-idx="' + idx + '" style="font-weight: 600;">' + (isVisible ? 'Ẩn' : 'Hiện') + '</button>' +
							'<button type="button" class="button remove-project-btn" data-idx="' + idx + '" style="background: #ef4444; color: #fff; border: none; font-weight: 600; height: auto; padding: 4px 10px; font-size: 0.8rem; cursor: pointer; border-radius: 4px;">Xóa</button>' +
						'</div>' +
					'</div>' +
					'<div class="project-body-form" style="padding: 20px; border-top: 1px solid #e2e8f0; background: #fff; display: ' + (openIdx === idx ? 'block' : 'none') + ';">' +
						'<div style="margin-bottom: 15px;">' +
							'<label style="display: block; font-weight: 600; margin-bottom: 5px; color: #1e293b;">Tiêu đề dự án thực tế:</label>' +
							'<input type="text" class="project-title" data-idx="' + idx + '" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" />' +
						'</div>' +
						'<div style="margin-bottom: 15px;">' +
							'<label style="display: block; font-weight: 600; margin-bottom: 5px; color: #1e293b;">Mô tả / Thương hiệu sử dụng:</label>' +
							'<input type="text" class="project-desc" data-idx="' + idx + '" style="width: 100%; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" />' +
						'</div>' +
						'<div>' +
							'<label style="display: block; font-weight: 600; margin-bottom: 5px; color: #1e293b;">Hình ảnh thực tế bàn giao:</label>' +
							'<div style="display: flex; gap: 10px; margin-bottom: 8px;">' +
								'<input type="text" class="project-img" id="project-img-input-' + idx + '" data-idx="' + idx + '" style="flex-grow: 1; padding: 8px 12px; border-radius: 6px; border: 1px solid #cbd5e1;" placeholder="http://..." />' +
								'<button type="button" class="button select-project-img-btn-dyn" data-idx="' + idx + '">Chọn Ảnh</button>' +
							'</div>' +
							'<img id="project-img-preview-' + idx + '" src="" style="max-width: 200px; max-height: 120px; border-radius: 6px; border: 1px solid #cbd5e1; display: none;" />' +
						'</div>' +
					'</div>' +
				'</div>');

				card.find('.project-title').val(item.title || '');
				card.find('.project-desc').val(item.desc || '');
				card.find('.project-img').val(item.img || '');
				if (item.img) {
					card.find('#project-img-preview-' + idx).attr('src', item.img).show();
				}
				projectsContainer.append(card);
			});
		}

		// Initial render calls
		renderPromotions();
		renderCommitments();
		renderServices();
		renderProjects();

		// Toggle custom collapsible cards (Globally using $(document) delegation to prevent breaking on tab changes)
		$(document).on('click', '.slide-header, .commit-header, .service-header, .project-header, .sanitary-card-header, .promo-header', function(e) {
			if ($(e.target).closest('button').length) {
				return;
			}
			e.preventDefault();
			$(this).next('.slide-body-form, .commit-body-form, .service-body-form, .project-body-form, .sanitary-card-body, .promo-body-form').slideToggle(200);
		});

		$(document).on('click', '.toggle-edit-btn, .toggle-commit-edit-btn, .toggle-service-edit-btn, .toggle-project-edit-btn, .toggle-card-btn, .toggle-promo-edit-btn', function(e) {
			e.preventDefault();
			$(this).closest('.slide-config-card, .commit-config-card, .service-config-card, .project-config-card, .sanitary-collapsible-card, .promo-config-card').find('.slide-body-form, .commit-body-form, .service-body-form, .project-body-form, .sanitary-card-body, .promo-body-form').slideToggle(200);
		});

		// Dynamic Header updates for Promo banners (from dynamic JSON list)
		$(document).on('input', '.promo-title', function() {
			$(this).closest('.promo-config-card').find('.promo-header strong span').text($(this).val() || '(Không có tiêu đề)');
		});

		// Promotions Action Buttons
		$(document).on('click', '.toggle-promo-visibility-btn', function(e) {
			e.preventDefault();
			var idx = $(this).data('idx');
			promotionsData[idx].visible = (promotionsData[idx].visible === undefined || promotionsData[idx].visible == 1) ? 0 : 1;
			renderPromotions(idx);
			updatePromotionsRawInput();
		});

		$('#sanitary-add-promo-btn').click(function(e) {
			e.preventDefault();
			promotionsData.push({
				title: '',
				desc: '',
				btn_text: '',
				btn_url: '',
				bg: '',
				tag: '',
				visible: 1
			});
			var newIdx = promotionsData.length - 1;
			renderPromotions(newIdx);
			updatePromotionsRawInput();
		});

		$(document).on('click', '.remove-promo-btn', function(e) {
			e.preventDefault();
			if (!confirm('Bạn có chắc chắn muốn xóa khuyến mãi này không?')) {
				return;
			}
			var idx = $(this).data('idx');
			promotionsData.splice(idx, 1);
			renderPromotions();
			updatePromotionsRawInput();
		});

		$(document).on('input change', '.promo-title, .promo-tag-input, .promo-desc, .promo-btn-text, .promo-btn-url, .promo-bg', function() {
			var idx = $(this).data('idx');
			var field = '';
			if ($(this).hasClass('promo-title')) {
				field = 'title';
				$(this).closest('.promo-config-card').find('.promo-header strong span').text($(this).val() || '(Không có tiêu đề)');
			}
			else if ($(this).hasClass('promo-tag-input')) {
				field = 'tag';
			}
			else if ($(this).hasClass('promo-desc')) {
				field = 'desc';
			}
			else if ($(this).hasClass('promo-btn-text')) {
				field = 'btn_text';
			}
			else if ($(this).hasClass('promo-btn-url')) {
				field = 'btn_url';
			}
			else if ($(this).hasClass('promo-bg')) {
				field = 'bg';
				var val = $(this).val();
				if (val) {
					$('#promo-bg-preview-' + idx).attr('src', val).show();
					$(this).closest('.promo-config-card').find('.promo-header img').attr('src', val);
				} else {
					$('#promo-bg-preview-' + idx).hide();
				}
			}
			promotionsData[idx][field] = $(this).val();
			updatePromotionsRawInput();
		});

		$(document).on('click', '.select-promo-bg-btn-dyn', function(e) {
			e.preventDefault();
			var idx = $(this).data('idx');
			var uploader = wp.media({
				title: 'Chọn hình nền Khuyến mãi ' + (idx + 1),
				button: { text: 'Sử dụng hình này' },
				multiple: false
			})
			.on('select', function() {
				var attachment = uploader.state().get('selection').first().toJSON();
				$('#promo-bg-input-' + idx).val(attachment.url).trigger('change');
			})
			.open();
		});

		// Commitments Action Buttons
		$(document).on('click', '.toggle-commit-visibility-btn', function(e) {
			e.preventDefault();
			var idx = $(this).data('idx');
			commitmentsData[idx].visible = (commitmentsData[idx].visible === undefined || commitmentsData[idx].visible == 1) ? 0 : 1;
			renderCommitments(idx);
			updateCommitmentsRawInput();
		});

		$('#sanitary-add-commit-btn').click(function(e) {
			e.preventDefault();
			commitmentsData.push({
				icon: '✨',
				title: '',
				desc: '',
				visible: 1
			});
			var newIdx = commitmentsData.length - 1;
			renderCommitments(newIdx);
			updateCommitmentsRawInput();
		});

		$(document).on('click', '.remove-commit-btn', function(e) {
			e.preventDefault();
			if (!confirm('Bạn có chắc chắn muốn xóa mục cam kết này không?')) {
				return;
			}
			var idx = $(this).data('idx');
			commitmentsData.splice(idx, 1);
			renderCommitments();
			updateCommitmentsRawInput();
		});

		$(document).on('input change', '.commit-icon, .commit-title, .commit-desc', function() {
			var idx = $(this).data('idx');
			var field = '';
			if ($(this).hasClass('commit-icon')) {
				field = 'icon';
				$(this).closest('.commit-config-card').find('.commit-header .commit-icon-display').text($(this).val() || '');
			}
			else if ($(this).hasClass('commit-title')) {
				field = 'title';
				$(this).closest('.commit-config-card').find('.commit-header strong span').text($(this).val() || '(Không có tiêu đề)');
			}
			else if ($(this).hasClass('commit-desc')) {
				field = 'desc';
			}
			commitmentsData[idx][field] = $(this).val();
			updateCommitmentsRawInput();
		});

		// Services Action Buttons
		$(document).on('click', '.toggle-service-visibility-btn', function(e) {
			e.preventDefault();
			var idx = $(this).data('idx');
			servicesData[idx].visible = (servicesData[idx].visible === undefined || servicesData[idx].visible == 1) ? 0 : 1;
			renderServices(idx);
			updateServicesRawInput();
		});

		$('#sanitary-add-service-btn').click(function(e) {
			e.preventDefault();
			servicesData.push({
				icon: '⚡',
				title: '',
				desc: '',
				visible: 1
			});
			var newIdx = servicesData.length - 1;
			renderServices(newIdx);
			updateServicesRawInput();
		});

		$(document).on('click', '.remove-service-btn', function(e) {
			e.preventDefault();
			if (!confirm('Bạn có chắc chắn muốn xóa dịch vụ này không?')) {
				return;
			}
			var idx = $(this).data('idx');
			servicesData.splice(idx, 1);
			renderServices();
			updateServicesRawInput();
		});

		$(document).on('input change', '.service-icon, .service-title, .service-desc', function() {
			var idx = $(this).data('idx');
			var field = '';
			if ($(this).hasClass('service-icon')) {
				field = 'icon';
				$(this).closest('.service-config-card').find('.service-header .service-icon-display').text($(this).val() || '');
			}
			else if ($(this).hasClass('service-title')) {
				field = 'title';
				$(this).closest('.service-config-card').find('.service-header strong span').text($(this).val() || '(Không có tiêu đề)');
			}
			else if ($(this).hasClass('service-desc')) {
				field = 'desc';
			}
			servicesData[idx][field] = $(this).val();
			updateServicesRawInput();
		});

		// Projects Action Buttons
		$(document).on('click', '.toggle-project-visibility-btn', function(e) {
			e.preventDefault();
			var idx = $(this).data('idx');
			projectsData[idx].visible = (projectsData[idx].visible === undefined || projectsData[idx].visible == 1) ? 0 : 1;
			renderProjects(idx);
			updateProjectsRawInput();
		});

		$('#sanitary-add-project-btn').click(function(e) {
			e.preventDefault();
			projectsData.push({
				title: '',
				desc: '',
				img: '',
				visible: 1
			});
			var newIdx = projectsData.length - 1;
			renderProjects(newIdx);
			updateProjectsRawInput();
		});

		$(document).on('click', '.remove-project-btn', function(e) {
			e.preventDefault();
			if (!confirm('Bạn có chắc chắn muốn xóa dự án thực tế này không?')) {
				return;
			}
			var idx = $(this).data('idx');
			projectsData.splice(idx, 1);
			renderProjects();
			updateProjectsRawInput();
		});

		$(document).on('input change', '.project-title, .project-desc, .project-img', function() {
			var idx = $(this).data('idx');
			var field = '';
			if ($(this).hasClass('project-title')) {
				field = 'title';
				$(this).closest('.project-config-card').find('.project-header strong span').text($(this).val() || '(Không có tiêu đề)');
			}
			else if ($(this).hasClass('project-desc')) {
				field = 'desc';
			}
			else if ($(this).hasClass('project-img')) {
				field = 'img';
				var val = $(this).val();
				if (val) {
					$('#project-img-preview-' + idx).attr('src', val).show();
					$(this).closest('.project-config-card').find('.project-header img').attr('src', val);
				} else {
					$('#project-img-preview-' + idx).hide();
				}
			}
			projectsData[idx][field] = $(this).val();
			updateProjectsRawInput();
		});

		$(document).on('click', '.select-project-img-btn-dyn', function(e) {
			e.preventDefault();
			var idx = $(this).data('idx');
			var uploader = wp.media({
				title: 'Chọn hình ảnh Dự án ' + (idx + 1),
				button: { text: 'Sử dụng hình này' },
				multiple: false
			})
			.on('select', function() {
				var attachment = uploader.state().get('selection').first().toJSON();
				$('#project-img-input-' + idx).val(attachment.url).trigger('change');
			})
			.open();
		});
	});
	</script>
	<?php
}

/**
 * AJAX Live Search Handler
 */
function sanitary_ajax_live_search() {
	$search_term = isset( $_GET['q'] ) ? sanitize_text_field( $_GET['q'] ) : '';
	$results = [];

	if ( ! empty( $search_term ) ) {
		$query = new WP_Query( [
			'post_type'      => 'sanitary_product',
			'post_status'    => 'publish',
			'posts_per_page' => 10,
			's'              => $search_term,
		] );

		if ( $query->have_posts() ) {
			while ( $query->have_posts() ) {
				$query->the_post();
				$thumbnail_url = get_the_post_thumbnail_url( get_the_ID(), 'thumbnail' );
				$results[] = [
					'id'        => get_the_ID(),
					'title'     => get_the_title(),
					'permalink' => get_permalink(),
					'thumbnail' => $thumbnail_url ? $thumbnail_url : '',
					'excerpt'   => wp_trim_words( get_the_excerpt(), 10 )
				];
			}
			wp_reset_postdata();
		}
	}

	wp_send_json_success( $results );
}
add_action( 'wp_ajax_sanitary_ajax_search', 'sanitary_ajax_live_search' );
add_action( 'wp_ajax_nopriv_sanitary_ajax_search', 'sanitary_ajax_live_search' );

/**
 * Register Product Specs Metabox
 */
function sanitary_add_product_specs_metabox() {
	add_meta_box(
		'sanitary_product_specs',
		__( 'Thông số kỹ thuật & Thư viện ảnh phụ', 'sanitary-catalog-core' ),
		'sanitary_render_product_specs_metabox',
		'sanitary_product',
		'normal',
		'high'
	);
}
add_action( 'add_meta_boxes', 'sanitary_add_product_specs_metabox' );

/**
 * Render Product Specs Metabox
 */
function sanitary_render_product_specs_metabox( $post ) {
	// Add nonce for security
	wp_nonce_field( 'sanitary_save_product_specs', 'sanitary_product_specs_nonce' );

	// Retrieve existing values
	$code = get_post_meta( $post->ID, '_sanitary_product_code', true );
	$material = get_post_meta( $post->ID, '_sanitary_product_material', true );
	$size = get_post_meta( $post->ID, '_sanitary_product_size', true );
	$warranty = get_post_meta( $post->ID, '_sanitary_product_warranty', true );
	$gallery = get_post_meta( $post->ID, '_sanitary_product_gallery', true );
	
	// Ensure gallery is an array
	$gallery_images = ! empty( $gallery ) ? json_decode( $gallery, true ) : [];
	if ( ! is_array( $gallery_images ) ) {
		$gallery_images = [];
	}
	?>
	<div class="sanitary-meta-wrapper" style="padding: 10px 0;">
		<table class="form-table" style="width: 100%;">
			<tr>
				<th style="width: 20%;"><label for="sanitary_product_code"><strong>Mã sản phẩm (SKU):</strong></label></th>
				<td>
					<input type="text" name="sanitary_product_code" id="sanitary_product_code" value="<?php echo esc_attr( $code ); ?>" class="regular-text" style="width: 100%; max-width: 400px;" placeholder="Ví dụ: G-8800, M-202..." />
				</td>
			</tr>
			<tr>
				<th><label for="sanitary_product_material"><strong>Chất liệu:</strong></label></th>
				<td>
					<input type="text" name="sanitary_product_material" id="sanitary_product_material" value="<?php echo esc_attr( $material ); ?>" class="regular-text" style="width: 100%; max-width: 400px;" placeholder="Ví dụ: Men sứ Nano nung, Đồng thau mạ Chrome..." />
				</td>
			</tr>
			<tr>
				<th><label for="sanitary_product_size"><strong>Kích thước:</strong></label></th>
				<td>
					<input type="text" name="sanitary_product_size" id="sanitary_product_size" value="<?php echo esc_attr( $size ); ?>" class="regular-text" style="width: 100%; max-width: 400px;" placeholder="Ví dụ: 680 x 380 x 470 mm, Cao 1.2m..." />
				</td>
			</tr>
			<tr>
				<th><label for="sanitary_product_warranty"><strong>Thời gian bảo hành:</strong></label></th>
				<td>
					<input type="text" name="sanitary_product_warranty" id="sanitary_product_warranty" value="<?php echo esc_attr( $warranty ); ?>" class="regular-text" style="width: 100%; max-width: 400px;" placeholder="Ví dụ: Men sứ 10 năm, phụ kiện 2 năm..." />
				</td>
			</tr>
			<tr>
				<th><label><strong>Thư viện hình ảnh phụ:</strong></label></th>
				<td>
					<input type="hidden" name="sanitary_product_gallery" id="sanitary_product_gallery_input" value="<?php echo esc_attr( json_encode( $gallery_images ) ); ?>" />
					<div id="sanitary-gallery-preview-container" style="display: flex; flex-wrap: wrap; gap: 10px; margin-bottom: 12px;">
						<?php foreach ( $gallery_images as $img_url ) : ?>
							<div class="gallery-preview-item" data-url="<?php echo esc_url( $img_url ); ?>" style="position: relative; width: 80px; height: 80px; border: 1px solid #cbd5e1; border-radius: 4px; overflow: hidden; background: #f8fafc; display: inline-block;">
								<img src="<?php echo esc_url( $img_url ); ?>" style="width: 100%; height: 100%; object-fit: cover;" />
								<button type="button" class="remove-gallery-img-btn" style="position: absolute; top: 2px; right: 2px; background: rgba(239, 68, 68, 0.9); color: white; border: none; border-radius: 50%; width: 18px; height: 18px; line-height: 16px; text-align: center; font-size: 11px; cursor: pointer; font-weight: bold; padding: 0;">&times;</button>
							</div>
						<?php endforeach; ?>
					</div>
					<button type="button" class="button" id="sanitary_select_gallery_btn" style="background: #22c55e; color: #fff; border-color: #16a34a; font-weight: 600;">+ Thêm ảnh thư viện</button>
					<p class="description" style="margin-top: 5px;">Chọn nhiều ảnh phụ để hiển thị slide nhỏ dạng thumbnail bên dưới ảnh đại diện ở trang chi tiết sản phẩm.</p>
				</td>
			</tr>
		</table>
	</div>
	<script>
	jQuery(document).ready(function($){
		var previewContainer = $('#sanitary-gallery-preview-container');
		var galleryInput = $('#sanitary_product_gallery_input');
		var galleryImages = [];
		try {
			galleryImages = JSON.parse(galleryInput.val() || '[]');
		} catch(e) {
			galleryImages = [];
		}

		function updateGalleryInput() {
			galleryInput.val(JSON.stringify(galleryImages));
		}

		$('#sanitary_select_gallery_btn').click(function(e) {
			e.preventDefault();
			var uploader = wp.media({
				title: 'Chọn hình ảnh thư viện sản phẩm',
				button: { text: 'Thêm vào thư viện' },
				multiple: true
			})
			.on('select', function() {
				var selection = uploader.state().get('selection');
				selection.map(function(attachment) {
					attachment = attachment.toJSON();
					if (attachment.url && galleryImages.indexOf(attachment.url) === -1) {
						galleryImages.push(attachment.url);
						previewContainer.append(
							'<div class="gallery-preview-item" data-url="' + attachment.url + '" style="position: relative; width: 80px; height: 80px; border: 1px solid #cbd5e1; border-radius: 4px; overflow: hidden; background: #f8fafc; display: inline-block; margin-right: 10px; margin-bottom: 10px;">' +
								'<img src="' + attachment.url + '" style="width: 100%; height: 100%; object-fit: cover;" />' +
								'<button type="button" class="remove-gallery-img-btn" style="position: absolute; top: 2px; right: 2px; background: rgba(239, 68, 68, 0.9); color: white; border: none; border-radius: 50%; width: 18px; height: 18px; line-height: 16px; text-align: center; font-size: 11px; cursor: pointer; font-weight: bold; padding: 0;">&times;</button>' +
							'</div>'
						);
					}
				});
				updateGalleryInput();
			})
			.open();
		});

		previewContainer.on('click', '.remove-gallery-img-btn', function(e) {
			e.preventDefault();
			var parent = $(this).parent('.gallery-preview-item');
			var url = parent.data('url');
			var index = galleryImages.indexOf(url);
			if (index > -1) {
				galleryImages.splice(index, 1);
			}
			parent.remove();
			updateGalleryInput();
		});
	});
	</script>
	<?php
}

/**
 * Save Product Specs Metabox
 */
function sanitary_save_product_specs_data( $post_id ) {
	// Security checks
	if ( ! isset( $_POST['sanitary_product_specs_nonce'] ) ) {
		return;
	}
	if ( ! wp_verify_nonce( $_POST['sanitary_product_specs_nonce'], 'sanitary_save_product_specs' ) ) {
		return;
	}
	if ( defined( 'DOING_AUTOSAVE' ) && DOING_AUTOSAVE ) {
		return;
	}
	if ( isset( $_POST['post_type'] ) && 'sanitary_product' !== $_POST['post_type'] ) {
		return;
	}
	if ( ! current_user_can( 'edit_post', $post_id ) ) {
		return;
	}

	// Sanitize and save fields
	if ( isset( $_POST['sanitary_product_code'] ) ) {
		update_post_meta( $post_id, '_sanitary_product_code', sanitize_text_field( $_POST['sanitary_product_code'] ) );
	}
	if ( isset( $_POST['sanitary_product_material'] ) ) {
		update_post_meta( $post_id, '_sanitary_product_material', sanitize_text_field( $_POST['sanitary_product_material'] ) );
	}
	if ( isset( $_POST['sanitary_product_size'] ) ) {
		update_post_meta( $post_id, '_sanitary_product_size', sanitize_text_field( $_POST['sanitary_product_size'] ) );
	}
	if ( isset( $_POST['sanitary_product_warranty'] ) ) {
		update_post_meta( $post_id, '_sanitary_product_warranty', sanitize_text_field( $_POST['sanitary_product_warranty'] ) );
	}
	if ( isset( $_POST['sanitary_product_gallery'] ) ) {
		update_post_meta( $post_id, '_sanitary_product_gallery', wp_unslash( $_POST['sanitary_product_gallery'] ) );
	}
}
add_action( 'save_post', 'sanitary_save_product_specs_data' );

/**
 * Register Custom Post Type for Inquiry
 */
function sanitary_register_inquiry_cpt() {
	$labels = [
		'name'               => __( 'Yêu cầu báo giá', 'sanitary-catalog-core' ),
		'singular_name'      => __( 'Yêu cầu báo giá', 'sanitary-catalog-core' ),
		'menu_name'          => __( 'Yêu cầu báo giá', 'sanitary-catalog-core' ),
		'name_admin_bar'     => __( 'Yêu cầu báo giá', 'sanitary-catalog-core' ),
		'add_new'            => __( 'Thêm mới', 'sanitary-catalog-core' ),
		'add_new_item'       => __( 'Thêm yêu cầu mới', 'sanitary-catalog-core' ),
		'new_item'           => __( 'Yêu cầu mới', 'sanitary-catalog-core' ),
		'edit_item'          => __( 'Chi tiết yêu cầu', 'sanitary-catalog-core' ),
		'view_item'          => __( 'Xem yêu cầu', 'sanitary-catalog-core' ),
		'all_items'          => __( 'Tất cả yêu cầu', 'sanitary-catalog-core' ),
		'search_items'       => __( 'Tìm kiếm yêu cầu', 'sanitary-catalog-core' ),
		'parent_item_colon'  => __( 'Yêu cầu cha:', 'sanitary-catalog-core' ),
		'not_found'          => __( 'Không tìm thấy yêu cầu nào.', 'sanitary-catalog-core' ),
		'not_found_in_trash' => __( 'Không có yêu cầu nào trong thùng rác.', 'sanitary-catalog-core' ),
	];

	$args = [
		'labels'             => $labels,
		'public'             => false,
		'show_ui'            => true,
		'show_in_menu'       => 'edit.php?post_type=sanitary_product',
		'query_var'          => true,
		'rewrite'            => false,
		'capability_type'    => 'post',
		'has_archive'        => false,
		'hierarchical'       => false,
		'menu_position'      => null,
		'supports'           => [ 'title' ],
		'map_meta_cap'       => true,
	];

	register_post_type( 'sanitary_inquiry', $args );
}
add_action( 'init', 'sanitary_register_inquiry_cpt' );

/**
 * Configure columns for Inquiry Admin list table
 */
function sanitary_set_inquiry_columns($columns) {
	$columns = [
		'cb'            => '<input type="checkbox" />',
		'title'         => __( 'Họ tên', 'sanitary-catalog-core' ),
		'inquiry_phone' => __( 'Số điện thoại', 'sanitary-catalog-core' ),
		'inquiry_prod'  => __( 'Sản phẩm quan tâm', 'sanitary-catalog-core' ),
		'inquiry_msg'   => __( 'Lời nhắn', 'sanitary-catalog-core' ),
		'date'          => __( 'Thời gian', 'sanitary-catalog-core' ),
	];
	return $columns;
}
add_filter( 'manage_sanitary_inquiry_posts_columns', 'sanitary_set_inquiry_columns' );

/**
 * Populate values in columns of Inquiry list table
 */
function sanitary_custom_inquiry_column( $column, $post_id ) {
	switch ( $column ) {
		case 'inquiry_phone' :
			echo esc_html( get_post_meta( $post_id, '_inquiry_phone', true ) );
			break;
		case 'inquiry_prod' :
			$prod_id = get_post_meta( $post_id, '_inquiry_product_id', true );
			$prod_name = get_post_meta( $post_id, '_inquiry_product_name', true );
			if ( $prod_id ) {
				echo '<a href="' . esc_url( get_edit_post_link( $prod_id ) ) . '">' . esc_html( $prod_name ) . '</a>';
			} else {
				echo esc_html( $prod_name );
			}
			break;
		case 'inquiry_msg' :
			echo esc_html( get_post_meta( $post_id, '_inquiry_message', true ) );
			break;
	}
}
add_action( 'manage_sanitary_inquiry_posts_custom_column' , 'sanitary_custom_inquiry_column', 10, 2 );

/**
 * AJAX Handler for submitting Inquiry Form
 */
function sanitary_submit_inquiry_handler() {
	// Verify nonce
	if ( ! isset( $_POST['nonce'] ) || ! wp_verify_nonce( $_POST['nonce'], 'sanitary_inquiry_nonce' ) ) {
		wp_send_json_error( [ 'message' => 'Lỗi bảo mật (Invalid session). Vui lòng tải lại trang.' ] );
	}

	// Honeypot check
	if ( ! empty( $_POST['sp_honeypot'] ) ) {
		// Silent success for spam bots
		wp_send_json_success( [ 'message' => 'Gửi yêu cầu thành công!' ] );
		exit;
	}

	// Get inputs
	$fullname = isset( $_POST['fullname'] ) ? sanitize_text_field( trim( $_POST['fullname'] ) ) : '';
	$phone    = isset( $_POST['phone'] ) ? sanitize_text_field( trim( $_POST['phone'] ) ) : '';
	$message  = isset( $_POST['message'] ) ? sanitize_textarea_field( trim( $_POST['message'] ) ) : '';
	$prod_id  = isset( $_POST['product_id'] ) ? intval( $_POST['product_id'] ) : 0;
	$prod_name= isset( $_POST['product_name'] ) ? sanitize_text_field( trim( $_POST['product_name'] ) ) : '';

	// Backend Validation
	if ( empty( $fullname ) ) {
		wp_send_json_error( [ 'message' => 'Vui lòng nhập Họ tên.' ] );
	}

	if ( empty( $phone ) ) {
		wp_send_json_error( [ 'message' => 'Vui lòng nhập Số điện thoại.' ] );
	}

	// Vietnamese phone number validation regex
	if ( ! preg_match( '/^(03|05|07|08|09)\d{8}$/', $phone ) ) {
		wp_send_json_error( [ 'message' => 'Số điện thoại không đúng định dạng Việt Nam (ví dụ: 0912345678, gồm 10 chữ số).' ] );
	}

	// GDPR / Decree 13 Consent verification
	if ( ! isset( $_POST['data_consent'] ) || $_POST['data_consent'] !== 'yes' ) {
		wp_send_json_error( [ 'message' => 'Bạn phải đồng ý với Điều khoản và Chính sách bảo mật thông tin cá nhân của chúng tôi.' ] );
	}

	// Insert Inquiry post
	$post_data = [
		'post_title'   => $fullname,
		'post_type'    => 'sanitary_inquiry',
		'post_status'  => 'publish',
	];

	$new_post_id = wp_insert_post( $post_data );

	if ( is_wp_error( $new_post_id ) ) {
		wp_send_json_error( [ 'message' => 'Có lỗi xảy ra khi lưu thông tin. Vui lòng liên hệ hotline.' ] );
	}

	// Save metadata
	update_post_meta( $new_post_id, '_inquiry_phone', $phone );
	update_post_meta( $new_post_id, '_inquiry_message', $message );
	update_post_meta( $new_post_id, '_inquiry_product_id', $prod_id );
	update_post_meta( $new_post_id, '_inquiry_product_name', $prod_name );

	wp_send_json_success( [ 'message' => 'Cảm ơn bạn! Yêu cầu báo giá đã được gửi thành công. Chúng tôi sẽ liên hệ lại sớm nhất.' ] );
}
add_action( 'wp_ajax_sanitary_submit_inquiry', 'sanitary_submit_inquiry_handler' );
add_action( 'wp_ajax_nopriv_sanitary_submit_inquiry', 'sanitary_submit_inquiry_handler' );

