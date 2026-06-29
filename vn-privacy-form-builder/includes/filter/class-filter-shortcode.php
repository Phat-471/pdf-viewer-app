<?php
/**
 * VN Product Filter - Shortcode Class
 * Đăng ký và render các shortcodes cho module lọc sản phẩm
 */
if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

class VN_Filter_Shortcode {

	public function __construct() {
		add_shortcode( 'vn_filter',          [ $this, 'render_filter_only' ] );
		add_shortcode( 'vn_products',        [ $this, 'render_products_only' ] );
		add_shortcode( 'vn_filter_products', [ $this, 'render_combined' ] );
		add_action( 'wp_enqueue_scripts',    [ $this, 'enqueue_assets' ] );
	}

	/**
	 * Enqueue CSS & JS cho filter frontend
	 */
	public function enqueue_assets() {
		wp_register_style(
			'vn-filter-css',
			VN_PRIVACY_URL . 'assets/filter.css',
			[],
			VN_PRIVACY_VERSION
		);
		wp_register_script(
			'nouislider',
			'https://cdnjs.cloudflare.com/ajax/libs/noUiSlider/15.7.1/nouislider.min.js',
			[],
			'15.7.1',
			true
		);
		wp_register_style(
			'nouislider-css',
			'https://cdnjs.cloudflare.com/ajax/libs/noUiSlider/15.7.1/nouislider.min.css',
			[],
			'15.7.1'
		);
		wp_register_script(
			'vn-filter-js',
			VN_PRIVACY_URL . 'assets/filter.js',
			[ 'jquery', 'nouislider' ],
			VN_PRIVACY_VERSION,
			true
		);
	}

	/**
	 * Enqueue assets thực sự khi shortcode được dùng
	 */
	private function load_assets( $settings ) {
		wp_enqueue_style( 'nouislider-css' );
		wp_enqueue_style( 'vn-filter-css' );
		wp_enqueue_script( 'nouislider' );
		wp_enqueue_script( 'vn-filter-js' );

		// Localize script với dữ liệu cần thiết
		wp_localize_script( 'vn-filter-js', 'vnFilterData', [
			'ajaxUrl'   => admin_url( 'admin-ajax.php' ),
			'nonce'     => wp_create_nonce( 'vn_filter_nonce' ),
			'columns'   => $settings['columns'] ?? 3,
			'perPage'   => $settings['per_page'] ?? 12,
			'i18n'      => [
				'loading'   => 'Đang tải...',
				'no_result' => 'Không tìm thấy sản phẩm.',
				'reset'     => 'Xóa bộ lọc',
			],
		] );
	}

	/**
	 * [vn_filter] — Chỉ hiển thị form bộ lọc
	 */
	public function render_filter_only( $atts ) {
		if ( ! class_exists( 'WooCommerce' ) ) return '';

		$atts = shortcode_atts( [
			'show' => '', // comma-separated: product_cat,pa_color,pa_size,_price,product_tag,_stock
		], $atts );

		$settings    = VN_Filter_Core::get_settings();
		$filter_data = VN_Filter_Core::get_filter_data();
		$this->load_assets( $settings );

		ob_start();
		$this->render_filter_panel( $filter_data, $settings, $atts );
		return ob_get_clean();
	}

	/**
	 * [vn_products] — Chỉ hiển thị danh sách sản phẩm
	 */
	public function render_products_only( $atts ) {
		if ( ! class_exists( 'WooCommerce' ) ) return '';

		$atts = shortcode_atts( [
			'per_page' => '',
			'columns'  => '',
			'orderby'  => '',
		], $atts );

		$settings = VN_Filter_Core::get_settings();
		$params   = [
			'per_page' => $atts['per_page'] ?: $settings['per_page'],
			'columns'  => $atts['columns']  ?: $settings['columns'],
			'orderby'  => $atts['orderby']  ?: $settings['orderby'],
		];

		$query_args = VN_Filter_Core::build_query_args( $params );
		$query      = VN_Filter_Core::get_products( $query_args );
		$this->load_assets( $settings );

		return VN_Filter_Core::render_products_html( $query, $params );
	}

	/**
	 * [vn_filter_products] — Kết hợp filter + sản phẩm
	 */
	public function render_combined( $atts ) {
		if ( ! class_exists( 'WooCommerce' ) ) return '';

		$atts = shortcode_atts( [
			'layout'   => 'sidebar-left',  // sidebar-left | sidebar-right | top-bar
			'per_page' => '',
			'columns'  => '',
			'orderby'  => '',
			'show'     => '',
		], $atts );

		$settings    = VN_Filter_Core::get_settings();
		$filter_data = VN_Filter_Core::get_filter_data();
		$this->load_assets( $settings );

		$params = [
			'per_page' => $atts['per_page'] ?: $settings['per_page'],
			'columns'  => $atts['columns']  ?: $settings['columns'],
			'orderby'  => $atts['orderby']  ?: $settings['orderby'],
		];

		$query_args   = VN_Filter_Core::build_query_args( $params );
		$query        = VN_Filter_Core::get_products( $query_args );
		$products_html = VN_Filter_Core::render_products_html( $query, $params );

		$layout = sanitize_text_field( $atts['layout'] );

		ob_start();
		?>
		<div class="vn-filter-wrap vn-layout-<?php echo esc_attr( $layout ); ?>">
			<?php if ( $layout === 'top-bar' ) : ?>
				<!-- Layout: bộ lọc phía trên ngang -->
				<div class="vn-filter-top">
					<?php $this->render_filter_panel( $filter_data, $settings, $atts, 'horizontal' ); ?>
				</div>
				<div class="vn-filter-content full-width">
					<?php $this->render_toolbar( $settings ); ?>
					<?php echo $products_html; ?>
				</div>

			<?php elseif ( $layout === 'sidebar-right' ) : ?>
				<!-- Layout: sản phẩm trái, filter phải -->
				<div class="vn-filter-content">
					<?php $this->render_toolbar( $settings ); ?>
					<?php echo $products_html; ?>
				</div>
				<div class="vn-filter-sidebar">
					<?php $this->render_filter_panel( $filter_data, $settings, $atts ); ?>
				</div>

			<?php else : ?>
				<!-- Layout mặc định: filter trái, sản phẩm phải -->
				<div class="vn-filter-sidebar">
					<?php $this->render_filter_panel( $filter_data, $settings, $atts ); ?>
				</div>
				<div class="vn-filter-content">
					<?php $this->render_toolbar( $settings ); ?>
					<?php echo $products_html; ?>
				</div>
			<?php endif; ?>
		</div>
		<?php
		return ob_get_clean();
	}

	/**
	 * Render thanh công cụ (sắp xếp + số kết quả)
	 */
	private function render_toolbar( $settings ) {
		?>
		<div class="vn-filter-toolbar">
			<div class="vn-found-count" id="vn-found-count">
				<span class="count-icon">🛍️</span>
				<span id="vn-total-count"></span>
			</div>
			<div class="vn-sort-wrap">
				<label for="vn-orderby">Sắp xếp:</label>
				<select id="vn-orderby" name="orderby" class="vn-select">
					<option value="date">Mới nhất</option>
					<option value="popularity">Phổ biến</option>
					<option value="rating">Đánh giá</option>
					<option value="price">Giá tăng dần</option>
					<option value="price-desc">Giá giảm dần</option>
					<option value="title">Tên A-Z</option>
				</select>
			</div>
		</div>
		<?php
	}

	/**
	 * Render panel bộ lọc (form)
	 */
	private function render_filter_panel( $filter_data, $settings, $atts = [], $mode = 'vertical' ) {
		$price_range  = $filter_data['price_range'];
		$active       = $settings['active_filters'];
		$show_count   = ! empty( $settings['show_count'] );
		$show_reset   = ! empty( $settings['show_reset'] );
		$primary      = $settings['primary_color'] ?: '#d97706';
		?>
		<style>
		:root {
			--vn-filter-primary: <?php echo esc_attr( $primary ); ?>;
		}
		</style>

		<div class="vn-filter-panel <?php echo esc_attr( $mode ); ?>" id="vn-filter-panel">
			<div class="vn-filter-header">
				<h3 class="vn-filter-title">
					<span>🔍</span> Bộ lọc
				</h3>
				<?php if ( $show_reset ) : ?>
					<button type="button" class="vn-reset-btn" id="vn-reset-filters" title="Xóa tất cả bộ lọc">
						↺ Đặt lại
					</button>
				<?php endif; ?>
			</div>

			<form id="vn-filter-form" class="vn-filter-form">
				<?php wp_nonce_field( 'vn_filter_nonce', 'vn_filter_nonce_field' ); ?>

				<!-- Danh mục sản phẩm -->
				<?php if ( in_array( 'product_cat', $active ) && ! empty( $filter_data['categories'] ) ) : ?>
				<div class="vn-filter-group">
					<button type="button" class="vn-filter-group-toggle">
						<span>📁 Danh mục</span>
						<span class="vn-toggle-arrow">▼</span>
					</button>
					<div class="vn-filter-group-body">
						<ul class="vn-filter-list">
							<?php foreach ( $filter_data['categories'] as $cat ) : ?>
								<li>
									<label class="vn-filter-item">
										<input type="checkbox" name="categories[]" value="<?php echo esc_attr( $cat->term_id ); ?>">
										<span class="vn-checkmark"></span>
										<span class="vn-item-label"><?php echo esc_html( $cat->name ); ?></span>
										<?php if ( $show_count ) : ?>
											<span class="vn-item-count">(<?php echo esc_html( $cat->count ); ?>)</span>
										<?php endif; ?>
									</label>
								</li>
							<?php endforeach; ?>
						</ul>
					</div>
				</div>
				<?php endif; ?>

				<!-- Thuộc tính sản phẩm -->
				<?php foreach ( $filter_data['attributes'] as $attr ) : ?>
					<?php if ( ! in_array( $attr['slug'], $active ) ) continue; ?>
					<div class="vn-filter-group">
						<button type="button" class="vn-filter-group-toggle">
							<span><?php echo esc_html( $attr['label'] ); ?></span>
							<span class="vn-toggle-arrow">▼</span>
						</button>
						<div class="vn-filter-group-body">
							<ul class="vn-filter-list <?php echo $attr['name'] === 'color' || $attr['name'] === 'mau' ? 'vn-swatch-list' : ''; ?>">
								<?php foreach ( $attr['terms'] as $term ) :
									$thumbnail_id = get_term_meta( $term->term_id, 'thumbnail_id', true );
									$color        = get_term_meta( $term->term_id, 'color', true );
									?>
									<li>
										<label class="vn-filter-item <?php echo $color ? 'has-color' : ''; ?>">
											<input type="checkbox" name="attributes[<?php echo esc_attr( $attr['slug'] ); ?>][]" value="<?php echo esc_attr( $term->term_id ); ?>">
											<?php if ( $color ) : ?>
												<span class="vn-color-swatch" style="background-color:<?php echo esc_attr( $color ); ?>;" title="<?php echo esc_attr( $term->name ); ?>"></span>
											<?php else : ?>
												<span class="vn-checkmark"></span>
											<?php endif; ?>
											<span class="vn-item-label"><?php echo esc_html( $term->name ); ?></span>
											<?php if ( $show_count ) : ?>
												<span class="vn-item-count">(<?php echo esc_html( $term->count ); ?>)</span>
											<?php endif; ?>
										</label>
									</li>
								<?php endforeach; ?>
							</ul>
						</div>
					</div>
				<?php endforeach; ?>

				<!-- Khoảng giá -->
				<?php if ( in_array( '_price', $active ) && $price_range['max'] > 0 ) : ?>
				<div class="vn-filter-group">
					<button type="button" class="vn-filter-group-toggle">
						<span>💰 Khoảng giá</span>
						<span class="vn-toggle-arrow">▼</span>
					</button>
					<div class="vn-filter-group-body">
						<div class="vn-price-slider-wrap">
							<div id="vn-price-slider"
								data-min="<?php echo esc_attr( $price_range['min'] ); ?>"
								data-max="<?php echo esc_attr( $price_range['max'] ); ?>"
							></div>
							<div class="vn-price-inputs">
								<input type="hidden" id="vn-price-min" name="price_min" value="<?php echo esc_attr( $price_range['min'] ); ?>">
								<input type="hidden" id="vn-price-max" name="price_max" value="<?php echo esc_attr( $price_range['max'] ); ?>">
								<span class="vn-price-display">
									<span id="vn-price-min-label"><?php echo wc_price( $price_range['min'] ); ?></span>
									—
									<span id="vn-price-max-label"><?php echo wc_price( $price_range['max'] ); ?></span>
								</span>
							</div>
						</div>
					</div>
				</div>
				<?php endif; ?>

				<!-- Thẻ sản phẩm -->
				<?php if ( in_array( 'product_tag', $active ) && ! empty( $filter_data['tags'] ) ) : ?>
				<div class="vn-filter-group">
					<button type="button" class="vn-filter-group-toggle">
						<span>🏷️ Thẻ sản phẩm</span>
						<span class="vn-toggle-arrow">▼</span>
					</button>
					<div class="vn-filter-group-body">
						<div class="vn-tag-cloud">
							<?php foreach ( $filter_data['tags'] as $tag ) : ?>
								<label class="vn-tag-item">
									<input type="checkbox" name="tags[]" value="<?php echo esc_attr( $tag->term_id ); ?>">
									<span><?php echo esc_html( $tag->name ); ?></span>
								</label>
							<?php endforeach; ?>
						</div>
					</div>
				</div>
				<?php endif; ?>

				<!-- Tình trạng kho -->
				<?php if ( in_array( '_stock', $active ) ) : ?>
				<div class="vn-filter-group">
					<div class="vn-filter-group-body" style="padding:0;">
						<label class="vn-filter-item vn-stock-toggle">
							<input type="checkbox" name="in_stock" id="vn-in-stock" value="1">
							<span class="vn-toggle-switch"></span>
							<span class="vn-item-label">📦 Chỉ còn hàng</span>
						</label>
					</div>
				</div>
				<?php endif; ?>

				<!-- Nút áp dụng (không autosubmit) -->
				<div class="vn-filter-actions">
					<button type="submit" class="vn-btn vn-btn-filter" id="vn-apply-filter">
						<span class="vn-spinner" style="display:none;">⏳</span>
						🔍 Lọc sản phẩm
					</button>
				</div>
			</form>
		</div>
		<?php
	}
}
