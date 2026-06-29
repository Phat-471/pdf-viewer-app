<?php
/**
 * VN Product Filter - AJAX Handler Class
 * Xử lý AJAX requests từ frontend filter
 */
if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

class VN_Filter_Ajax {

	public function __construct() {
		// AJAX cho cả logged-in và khách (public shop)
		add_action( 'wp_ajax_vn_filter_products',        [ $this, 'handle_filter' ] );
		add_action( 'wp_ajax_nopriv_vn_filter_products', [ $this, 'handle_filter' ] );
	}

	/**
	 * Xử lý AJAX lọc sản phẩm
	 */
	public function handle_filter() {
		// Verify nonce
		if ( ! check_ajax_referer( 'vn_filter_nonce', 'nonce', false ) ) {
			wp_send_json_error( [ 'message' => 'Phiên làm việc hết hạn, vui lòng tải lại trang.' ], 403 );
		}

		// Lấy và sanitize params
		$params = $this->sanitize_params( $_POST );

		// Build query
		$query_args = VN_Filter_Core::build_query_args( $params );
		$query      = VN_Filter_Core::get_products( $query_args );

		// Render HTML sản phẩm
		$atts = [
			'columns' => isset( $params['columns'] ) ? absint( $params['columns'] ) : 3,
		];
		$html = VN_Filter_Core::render_products_html( $query, $atts );

		// Tính dynamic counts cho linked filters (faceted search)
		$counts = VN_Filter_Core::get_dynamic_counts( $params );

		wp_send_json_success( [
			'html'       => $html,
			'total'      => $query->found_posts,
			'max_pages'  => $query->max_num_pages,
			'paged'      => max( 1, absint( $params['paged'] ?? 1 ) ),
			'found_text' => sprintf(
				_n( 'Tìm thấy %d sản phẩm', 'Tìm thấy %d sản phẩm', $query->found_posts, 'vn-privacy-form-builder' ),
				$query->found_posts
			),
			'counts'     => $counts, // dynamic recount cho từng taxonomy => term_id => count
		] );
	}

	/**
	 * Sanitize toàn bộ params đầu vào
	 */
	private function sanitize_params( $post ) {
		$params = [];

		// Danh mục
		if ( ! empty( $post['categories'] ) ) {
			$params['categories'] = array_map( 'absint', (array) $post['categories'] );
		}

		// Thuộc tính (array of arrays: pa_color => [1,2,3], pa_size => [4])
		if ( ! empty( $post['attributes'] ) && is_array( $post['attributes'] ) ) {
			$params['attributes'] = [];
			foreach ( $post['attributes'] as $attr_slug => $term_ids ) {
				$clean_slug = sanitize_key( $attr_slug );
				if ( strpos( $clean_slug, 'pa_' ) === 0 ) { // phải bắt đầu bằng pa_
					$params['attributes'][ $clean_slug ] = array_map( 'absint', (array) $term_ids );
				}
			}
		}

		// Thẻ sản phẩm
		if ( ! empty( $post['tags'] ) ) {
			$params['tags'] = array_map( 'absint', (array) $post['tags'] );
		}

		// Khoảng giá
		if ( isset( $post['price_min'] ) && $post['price_min'] !== '' ) {
			$params['price_min'] = floatval( $post['price_min'] );
		}
		if ( isset( $post['price_max'] ) && $post['price_max'] !== '' ) {
			$params['price_max'] = floatval( $post['price_max'] );
		}

		// Tình trạng kho
		$params['in_stock'] = ! empty( $post['in_stock'] ) ? 1 : 0;

		// Tìm kiếm text
		if ( ! empty( $post['search'] ) ) {
			$params['search'] = sanitize_text_field( $post['search'] );
		}

		// Phân trang
		$params['paged'] = isset( $post['paged'] ) ? absint( $post['paged'] ) : 1;

		// Sắp xếp
		$allowed_orderby = [ 'date', 'price', 'price-desc', 'popularity', 'rating', 'title' ];
		$orderby = sanitize_text_field( $post['orderby'] ?? 'date' );
		$params['orderby'] = in_array( $orderby, $allowed_orderby, true ) ? $orderby : 'date';

		// Số cột
		$params['columns'] = isset( $post['columns'] ) ? absint( $post['columns'] ) : 3;

		return $params;
	}
}
