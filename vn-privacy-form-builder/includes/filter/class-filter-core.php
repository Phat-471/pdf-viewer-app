<?php
/**
 * VN Product Filter - Core Class
 * Lõi truy vấn và xử lý dữ liệu WooCommerce cho module lọc sản phẩm
 */
if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

class VN_Filter_Core {

	/**
	 * Lấy toàn bộ dữ liệu cần thiết để render form filter
	 * (categories, attributes, tags, price range)
	 */
	public static function get_filter_data() {
		$data = [];

		// 1. Danh mục sản phẩm
		$data['categories'] = get_terms( [
			'taxonomy'   => 'product_cat',
			'hide_empty' => true,
			'orderby'    => 'name',
		] );

		// 2. Thuộc tính sản phẩm (WooCommerce attributes)
		$data['attributes'] = [];
		$attribute_taxonomies = wc_get_attribute_taxonomies();
		if ( $attribute_taxonomies ) {
			foreach ( $attribute_taxonomies as $taxonomy ) {
				$tax_name = wc_attribute_taxonomy_name( $taxonomy->attribute_name );
				$terms    = get_terms( [
					'taxonomy'   => $tax_name,
					'hide_empty' => true,
				] );
				if ( ! is_wp_error( $terms ) && ! empty( $terms ) ) {
					$data['attributes'][] = [
						'name'  => $taxonomy->attribute_name,
						'label' => $taxonomy->attribute_label,
						'slug'  => $tax_name,
						'terms' => $terms,
					];
				}
			}
		}

		// 3. Thẻ sản phẩm
		$data['tags'] = get_terms( [
			'taxonomy'   => 'product_tag',
			'hide_empty' => true,
			'orderby'    => 'name',
			'number'     => 50,
		] );

		// 4. Khoảng giá min/max
		$data['price_range'] = self::get_price_range();

		// 5. Cài đặt đã lưu
		$data['settings'] = self::get_settings();

		return $data;
	}

	/**
	 * Lấy khoảng giá min/max từ toàn bộ sản phẩm
	 */
	public static function get_price_range() {
		global $wpdb;

		$min = (float) $wpdb->get_var( "
			SELECT MIN( CAST( meta_value AS DECIMAL(10,2) ) )
			FROM {$wpdb->postmeta}
			INNER JOIN {$wpdb->posts} ON {$wpdb->posts}.ID = {$wpdb->postmeta}.post_id
			WHERE meta_key = '_price'
			AND {$wpdb->posts}.post_status = 'publish'
			AND {$wpdb->posts}.post_type = 'product'
			AND meta_value != ''
		" );

		$max = (float) $wpdb->get_var( "
			SELECT MAX( CAST( meta_value AS DECIMAL(10,2) ) )
			FROM {$wpdb->postmeta}
			INNER JOIN {$wpdb->posts} ON {$wpdb->posts}.ID = {$wpdb->postmeta}.post_id
			WHERE meta_key = '_price'
			AND {$wpdb->posts}.post_status = 'publish'
			AND {$wpdb->posts}.post_type = 'product'
			AND meta_value != ''
		" );

		return [
			'min' => max( 0, $min ),
			'max' => max( 0, $max ),
		];
	}

	/**
	 * Build WP_Query args từ params filter đầu vào
	 */
	public static function build_query_args( $params ) {
		$settings = self::get_settings();
		$per_page = isset( $params['per_page'] ) ? absint( $params['per_page'] ) : absint( $settings['per_page'] );
		$paged    = isset( $params['paged'] ) ? absint( $params['paged'] ) : 1;
		$orderby  = isset( $params['orderby'] ) ? sanitize_text_field( $params['orderby'] ) : $settings['orderby'];

		$args = [
			'post_type'      => 'product',
			'post_status'    => 'publish',
			'posts_per_page' => $per_page,
			'paged'          => $paged,
		];

		// Xử lý orderby
		switch ( $orderby ) {
			case 'price':
				$args['meta_key'] = '_price';
				$args['orderby']  = 'meta_value_num';
				$args['order']    = 'ASC';
				break;
			case 'price-desc':
				$args['meta_key'] = '_price';
				$args['orderby']  = 'meta_value_num';
				$args['order']    = 'DESC';
				break;
			case 'popularity':
				$args['meta_key'] = 'total_sales';
				$args['orderby']  = 'meta_value_num';
				$args['order']    = 'DESC';
				break;
			case 'rating':
				$args['meta_key'] = '_wc_average_rating';
				$args['orderby']  = 'meta_value_num';
				$args['order']    = 'DESC';
				break;
			case 'title':
				$args['orderby'] = 'title';
				$args['order']   = 'ASC';
				break;
			default: // 'date'
				$args['orderby'] = 'date';
				$args['order']   = 'DESC';
		}

		// Tax query builder
		$tax_query = [];

		// Filter danh mục
		if ( ! empty( $params['categories'] ) ) {
			$cat_ids = array_map( 'absint', (array) $params['categories'] );
			$cat_ids = array_filter( $cat_ids );
			if ( ! empty( $cat_ids ) ) {
				$tax_query[] = [
					'taxonomy'         => 'product_cat',
					'field'            => 'term_id',
					'terms'            => $cat_ids,
					'operator'         => 'IN',
					'include_children' => true,
				];
			}
		}

		// Filter thuộc tính
		if ( ! empty( $params['attributes'] ) && is_array( $params['attributes'] ) ) {
			foreach ( $params['attributes'] as $attr_slug => $term_ids ) {
				$term_ids = array_map( 'absint', (array) $term_ids );
				$term_ids = array_filter( $term_ids );
				if ( empty( $term_ids ) ) continue;
				$tax_name    = sanitize_text_field( $attr_slug );
				$tax_query[] = [
					'taxonomy' => $tax_name,
					'field'    => 'term_id',
					'terms'    => $term_ids,
					'operator' => 'IN',
				];
			}
		}

		// Filter thẻ sản phẩm
		if ( ! empty( $params['tags'] ) ) {
			$tag_ids = array_map( 'absint', (array) $params['tags'] );
			$tag_ids = array_filter( $tag_ids );
			if ( ! empty( $tag_ids ) ) {
				$tax_query[] = [
					'taxonomy' => 'product_tag',
					'field'    => 'term_id',
					'terms'    => $tag_ids,
					'operator' => 'IN',
				];
			}
		}

		if ( count( $tax_query ) > 1 ) {
			$tax_query['relation'] = 'AND';
		}
		if ( ! empty( $tax_query ) ) {
			$args['tax_query'] = $tax_query;
		}

		// Filter khoảng giá
		$meta_query = [];
		$price_min  = isset( $params['price_min'] ) && $params['price_min'] !== '' ? floatval( $params['price_min'] ) : null;
		$price_max  = isset( $params['price_max'] ) && $params['price_max'] !== '' ? floatval( $params['price_max'] ) : null;

		if ( $price_min !== null || $price_max !== null ) {
			$price_meta = [
				'key'     => '_price',
				'type'    => 'NUMERIC',
			];
			if ( $price_min !== null && $price_max !== null ) {
				$price_meta['value']   = [ $price_min, $price_max ];
				$price_meta['compare'] = 'BETWEEN';
			} elseif ( $price_min !== null ) {
				$price_meta['value']   = $price_min;
				$price_meta['compare'] = '>=';
			} else {
				$price_meta['value']   = $price_max;
				$price_meta['compare'] = '<=';
			}
			$meta_query[] = $price_meta;
		}

		// Filter tình trạng kho
		if ( ! empty( $params['in_stock'] ) ) {
			$meta_query[] = [
				'key'     => '_stock_status',
				'value'   => 'instock',
				'compare' => '=',
			];
		}

		if ( ! empty( $meta_query ) ) {
			if ( count( $meta_query ) > 1 ) {
				$meta_query['relation'] = 'AND';
			}
			$args['meta_query'] = $meta_query;
		}

		// Filter tìm kiếm text
		if ( ! empty( $params['search'] ) ) {
			$args['s'] = sanitize_text_field( $params['search'] );
		}

		return apply_filters( 'vn_filter_query_args', $args, $params );
	}

	/**
	 * Thực hiện WP_Query và trả về kết quả
	 */
	public static function get_products( $query_args ) {
		return new WP_Query( $query_args );
	}

	/**
	 * Render HTML danh sách sản phẩm
	 */
	public static function render_products_html( $query, $atts = [] ) {
		$settings = self::get_settings();
		$columns  = isset( $atts['columns'] ) ? absint( $atts['columns'] ) : absint( $settings['columns'] );
		$columns  = max( 1, min( 6, $columns ) );

		ob_start();

		if ( $query->have_posts() ) {
			echo '<div class="vn-products-wrapper" id="vn-products-wrapper">';
			echo '<div class="vn-products-grid vn-cols-' . esc_attr( $columns ) . '">';

			while ( $query->have_posts() ) {
				$query->the_post();
				global $product;
				$product = wc_get_product( get_the_ID() );
				if ( ! $product ) continue;

				self::render_product_card( $product );
			}

			echo '</div>';

			// Phân trang
			echo self::render_pagination( $query );
			echo '</div>';

			wp_reset_postdata();
		} else {
			echo '<div class="vn-products-wrapper vn-no-products" id="vn-products-wrapper">';
			echo '<div class="vn-empty-state">';
			echo '<div class="vn-empty-icon">🛍️</div>';
			echo '<p>' . esc_html__( 'Không tìm thấy sản phẩm phù hợp.', 'vn-privacy-form-builder' ) . '</p>';
			echo '<button class="vn-btn vn-btn-outline" id="vn-reset-all-filters">Xóa bộ lọc</button>';
			echo '</div></div>';
		}

		return ob_get_clean();
	}

	/**
	 * Render card sản phẩm đơn
	 */
	private static function render_product_card( $product ) {
		$settings   = self::get_settings();
		$link       = get_permalink( $product->get_id() );
		$title      = get_the_title();
		$image      = get_the_post_thumbnail( $product->get_id(), 'woocommerce_thumbnail' );
		if ( ! $image ) {
			$image = '<img src="' . esc_url( wc_placeholder_img_src() ) . '" alt="placeholder">';
		}
		$price      = $product->get_price_html();
		$is_sale    = $product->is_on_sale();
		$in_stock   = $product->is_in_stock();
		$rating     = $product->get_average_rating();
		$add_to_cart_text = $product->add_to_cart_text();
		$add_to_cart_url  = $product->add_to_cart_url();
		// Tuỳ chọn nút Đọc tiếp
		$show_read_more  = ! empty( $settings['show_read_more'] );
		$read_more_label = ! empty( $settings['read_more_label'] ) ? $settings['read_more_label'] : 'Đọc tiếp';
		?>
		<div class="vn-product-card <?php echo $is_sale ? 'is-sale' : ''; echo ! $in_stock ? ' out-of-stock' : ''; ?>">
			<a href="<?php echo esc_url( $link ); ?>" class="vn-product-image">
				<?php echo $image; ?>
				<?php if ( $is_sale ) : ?>
					<span class="vn-badge-sale">Sale</span>
				<?php endif; ?>
				<?php if ( ! $in_stock ) : ?>
					<span class="vn-badge-outofstock">Hết hàng</span>
				<?php endif; ?>
			</a>
			<div class="vn-product-info">
				<h3 class="vn-product-title">
					<a href="<?php echo esc_url( $link ); ?>"><?php echo esc_html( $title ); ?></a>
				</h3>
				<?php if ( $rating > 0 ) : ?>
					<div class="vn-product-rating">
						<?php for ( $i = 1; $i <= 5; $i++ ) : ?>
							<span class="<?php echo $i <= $rating ? 'star filled' : 'star'; ?>">★</span>
						<?php endfor; ?>
					</div>
				<?php endif; ?>
				<div class="vn-product-price"><?php echo $price; ?></div>
				<?php if ( $in_stock ) : ?>
					<a href="<?php echo esc_url( $add_to_cart_url ); ?>"
					   class="vn-add-to-cart <?php echo $product->is_type('simple') ? 'ajax_add_to_cart' : ''; ?>"
					   data-product_id="<?php echo esc_attr( $product->get_id() ); ?>">
						<?php echo esc_html( $add_to_cart_text ); ?>
					</a>
				<?php elseif ( $show_read_more ) : ?>
					<!-- Nút Đọc tiếp khi hết hàng — màu đồng bộ --vn-filter-primary -->
					<a href="<?php echo esc_url( $link ); ?>" class="vn-read-more-btn">
						<?php echo esc_html( $read_more_label ); ?>
					</a>
				<?php endif; ?>
			</div>
		</div>
		<?php
	}

	/**
	 * Render phân trang AJAX
	 */
	public static function render_pagination( $query ) {
		$total_pages = $query->max_num_pages;
		$paged       = max( 1, $query->get( 'paged' ) );

		if ( $total_pages <= 1 ) return '';

		$html  = '<div class="vn-pagination">';
		$html .= '<span class="vn-page-info">Trang ' . $paged . ' / ' . $total_pages . '</span>';
		$html .= '<div class="vn-page-btns">';

		if ( $paged > 1 ) {
			$html .= '<button class="vn-page-btn" data-page="' . ( $paged - 1 ) . '">« Trước</button>';
		}

		$start = max( 1, $paged - 2 );
		$end   = min( $total_pages, $paged + 2 );

		for ( $i = $start; $i <= $end; $i++ ) {
			$active = $i === $paged ? ' active' : '';
			$html  .= '<button class="vn-page-btn' . $active . '" data-page="' . $i . '">' . $i . '</button>';
		}

		if ( $paged < $total_pages ) {
			$html .= '<button class="vn-page-btn" data-page="' . ( $paged + 1 ) . '">Sau »</button>';
		}

		$html .= '</div></div>';
		return $html;
	}

	/**
	 * Lấy cài đặt module filter
	 */
	public static function get_settings() {
		$defaults = [
			'per_page'        => 12,
			'columns'         => 3,
			'orderby'         => 'date',
			'show_count'      => true,
			'show_reset'      => true,
			'primary_color'   => '#d97706',
			'active_filters'  => [ 'product_cat', '_price', '_stock' ],
			'show_read_more'  => true,   // Hiển thị nút Đọc tiếp khi hết hàng
			'read_more_label' => 'Đọc tiếp', // Nhãn nút
		];
		$saved = get_option( 'vn_filter_settings', [] );
		return wp_parse_args( $saved, $defaults );
	}

	/**
	 * Lưu cài đặt module filter
	 */
	public static function save_settings( $data ) {
		$settings = [
			'per_page'        => absint( $data['per_page'] ?? 12 ),
			'columns'         => absint( $data['columns'] ?? 3 ),
			'orderby'         => sanitize_text_field( $data['orderby'] ?? 'date' ),
			'show_count'      => ! empty( $data['show_count'] ) ? 1 : 0,
			'show_reset'      => ! empty( $data['show_reset'] ) ? 1 : 0,
			'primary_color'   => sanitize_hex_color( $data['primary_color'] ?? '#d97706' ) ?: '#d97706',
			'active_filters'  => isset( $data['active_filters'] ) ? (array) $data['active_filters'] : [],
			'show_read_more'  => ! empty( $data['show_read_more'] ) ? 1 : 0,
			'read_more_label' => sanitize_text_field( $data['read_more_label'] ?? 'Đọc tiếp' ),
		];
		update_option( 'vn_filter_settings', $settings );
		return $settings;
	}

	/**
	 * Tính toán dynamic counts cho các bộ lọc dựa trên params hiện tại
	 * (Linked Filters / Faceted Search)
	 *
	 * Logic: Với mỗi taxonomy, loại bỏ constraint của chính nó ra khỏi query
	 * để người dùng vẫn thấy các lựa chọn khác trong cùng nhóm đó.
	 * Các taxonomy còn lại vẫn được giữ nguyên (giao nhau).
	 *
	 * @param array $params  Params filter đã sanitize (từ AJAX)
	 * @return array  ['taxonomy_slug' => ['term_id' => count, ...], ...]
	 */
	public static function get_dynamic_counts( $params ) {
		if ( ! class_exists( 'WooCommerce' ) ) return [];

		// Lưu cache bằng Transient để tăng tốc độ truy vấn
		$cache_key = 'vn_flt_cnt_' . md5( serialize( $params ) );
		$cached    = get_transient( $cache_key );
		if ( $cached !== false ) {
			return $cached;
		}

		$counts = [];

		// Danh sách tất cả taxonomies cần đếm
		$all_taxonomies = [ 'product_cat' => 'categories', 'product_tag' => 'tags' ];

		// Thêm thuộc tính WooCommerce (pa_*)
		$attribute_taxonomies = wc_get_attribute_taxonomies();
		if ( $attribute_taxonomies ) {
			foreach ( $attribute_taxonomies as $attr ) {
				$slug = wc_attribute_taxonomy_name( $attr->attribute_name );
				$all_taxonomies[ $slug ] = 'attributes:' . $slug;
			}
		}

		foreach ( $all_taxonomies as $taxonomy => $param_key ) {
			// Build params loại bỏ constraint của taxonomy hiện tại
			$sub_params = $params;

			if ( $taxonomy === 'product_cat' ) {
				unset( $sub_params['categories'] );
			} elseif ( $taxonomy === 'product_tag' ) {
				unset( $sub_params['tags'] );
			} else {
				// pa_* attribute taxonomy
				if ( ! empty( $sub_params['attributes'] ) ) {
					unset( $sub_params['attributes'][ $taxonomy ] );
					if ( empty( $sub_params['attributes'] ) ) {
						unset( $sub_params['attributes'] );
					}
				}
			}

			// Query sản phẩm với params đã loại bỏ constraint của taxonomy này
			$sub_params['posts_per_page'] = -1;
			$sub_params['paged']          = 1;
			$query_args  = self::build_query_args( $sub_params );
			$query_args['posts_per_page'] = -1;
			$query_args['fields']         = 'ids'; // chỉ lấy IDs cho nhanh
			unset( $query_args['paged'] );

			$product_ids = get_posts( $query_args );

			if ( empty( $product_ids ) ) {
				$counts[ $taxonomy ] = [];
				continue;
			}

			// Đếm số sản phẩm có trong mỗi term của taxonomy này
			$terms = get_terms( [
				'taxonomy'               => $taxonomy,
				'hide_empty'             => false,
				'object_ids'             => $product_ids, // chỉ đếm trong tập sản phẩm hiện tại
			] );

			$taxonomy_counts = [];
			if ( ! is_wp_error( $terms ) ) {
				foreach ( $terms as $term ) {
					// Đếm số products trong $product_ids thuộc term này
					$term_products = get_objects_in_term( $term->term_id, $taxonomy );
					if ( is_wp_error( $term_products ) ) continue;
					$count = count( array_intersect( $product_ids, array_map( 'intval', $term_products ) ) );
					if ( $count > 0 ) {
						$taxonomy_counts[ $term->term_id ] = $count;
					}
				}
			}
			$counts[ $taxonomy ] = $taxonomy_counts;
		}

		// Cache kết quả trong 15 phút (900s)
		set_transient( $cache_key, $counts, 900 );
		return $counts;
	}

	public static function clear_filter_cache() {
		global $wpdb;
		$wpdb->query( "DELETE FROM {$wpdb->options} WHERE option_name LIKE '_transient_vn_flt_cnt_%' OR option_name LIKE '_transient_timeout_vn_flt_cnt_%'" );
	}
}
