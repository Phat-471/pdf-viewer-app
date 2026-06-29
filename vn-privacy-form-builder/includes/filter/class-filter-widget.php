<?php
/**
 * VN Product Filter - WordPress Widget
 * Cho phép đặt bộ lọc sản phẩm vào bất kỳ sidebar nào
 */
if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

class VN_Filter_Widget extends WP_Widget {

	public function __construct() {
		parent::__construct(
			'vn_product_filter_widget',
			'🔍 VN Product Filter',
			[
				'description' => 'Bộ lọc sản phẩm WooCommerce — đặt vào sidebar để lọc theo danh mục, giá, thuộc tính.',
				'classname'   => 'vn-filter-widget',
			]
		);
	}

	/**
	 * Render widget ở frontend
	 */
	public function widget( $args, $instance ) {
		if ( ! class_exists( 'WooCommerce' ) ) return;

		// Chỉ hiển thị trên trang shop, archive, search
		if ( ! is_shop() && ! is_product_category() && ! is_product_tag() && ! is_product_taxonomy() && ! is_search() ) {
			// Vẫn cho hiển thị nếu admin muốn ép
			if ( empty( $instance['show_everywhere'] ) ) return;
		}

		$title = apply_filters( 'widget_title', $instance['title'] ?? '' );

		echo $args['before_widget'];

		if ( $title ) {
			echo $args['before_title'] . esc_html( $title ) . $args['after_title'];
		}

		// Enqueue assets
		wp_enqueue_style( 'nouislider-css' );
		wp_enqueue_style( 'vn-filter-css' );
		wp_enqueue_script( 'nouislider' );
		wp_enqueue_script( 'vn-filter-js' );

		$settings    = VN_Filter_Core::get_settings();
		wp_localize_script( 'vn-filter-js', 'vnFilterData', [
			'ajaxUrl' => admin_url( 'admin-ajax.php' ),
			'nonce'   => wp_create_nonce( 'vn_filter_nonce' ),
			'columns' => $settings['columns'] ?? 3,
			'perPage' => $settings['per_page'] ?? 12,
			'i18n'    => [
				'loading'   => 'Đang tải...',
				'no_result' => 'Không tìm thấy sản phẩm.',
				'reset'     => 'Đặt lại',
			],
		] );

		$filter_data = VN_Filter_Core::get_filter_data();
		$shortcode   = new VN_Filter_Shortcode();

		// Gọi render_filter_panel qua reflection (hoặc make it accessible)
		$this->render_widget_filter( $filter_data, $settings );

		echo $args['after_widget'];
	}

	/**
	 * Render form filter cho widget
	 */
	private function render_widget_filter( $filter_data, $settings ) {
		$price_range = $filter_data['price_range'];
		$active      = $settings['active_filters'];
		$primary     = $settings['primary_color'] ?: '#d97706';
		?>
		<style>:root { --vn-filter-primary: <?php echo esc_attr( $primary ); ?>; }</style>
		<div class="vn-filter-panel vertical widget-mode" id="vn-filter-panel">
			<div class="vn-filter-header">
				<button type="button" class="vn-reset-btn" id="vn-reset-filters" title="Đặt lại">↺ Đặt lại</button>
			</div>
			<form id="vn-filter-form" class="vn-filter-form">
				<?php wp_nonce_field( 'vn_filter_nonce', 'vn_filter_nonce_field' ); ?>

				<?php if ( in_array( 'product_cat', $active ) && ! empty( $filter_data['categories'] ) ) : ?>
				<div class="vn-filter-group">
					<button type="button" class="vn-filter-group-toggle"><span>📁 Danh mục</span><span class="vn-toggle-arrow">▼</span></button>
					<div class="vn-filter-group-body">
						<ul class="vn-filter-list">
						<?php foreach ( $filter_data['categories'] as $cat ) : ?>
							<li>
								<label class="vn-filter-item">
									<input type="checkbox" name="categories[]" value="<?php echo esc_attr( $cat->term_id ); ?>">
									<span class="vn-checkmark"></span>
									<span class="vn-item-label"><?php echo esc_html( $cat->name ); ?></span>
									<?php if ( ! empty( $settings['show_count'] ) ) : ?>
										<span class="vn-item-count">(<?php echo esc_html( $cat->count ); ?>)</span>
									<?php endif; ?>
								</label>
							</li>
						<?php endforeach; ?>
						</ul>
					</div>
				</div>
				<?php endif; ?>

				<?php if ( in_array( '_price', $active ) && $price_range['max'] > 0 ) : ?>
				<div class="vn-filter-group">
					<button type="button" class="vn-filter-group-toggle"><span>💰 Khoảng giá</span><span class="vn-toggle-arrow">▼</span></button>
					<div class="vn-filter-group-body">
						<div class="vn-price-slider-wrap">
							<div id="vn-price-slider" data-min="<?php echo esc_attr( $price_range['min'] ); ?>" data-max="<?php echo esc_attr( $price_range['max'] ); ?>"></div>
							<div class="vn-price-inputs">
								<input type="hidden" id="vn-price-min" name="price_min" value="<?php echo esc_attr( $price_range['min'] ); ?>">
								<input type="hidden" id="vn-price-max" name="price_max" value="<?php echo esc_attr( $price_range['max'] ); ?>">
								<span class="vn-price-display">
									<span id="vn-price-min-label"><?php echo wc_price( $price_range['min'] ); ?></span> —
									<span id="vn-price-max-label"><?php echo wc_price( $price_range['max'] ); ?></span>
								</span>
							</div>
						</div>
					</div>
				</div>
				<?php endif; ?>

				<?php if ( in_array( '_stock', $active ) ) : ?>
				<div class="vn-filter-group" style="border:none;padding-top:0;">
					<label class="vn-filter-item vn-stock-toggle" style="padding:8px 0;">
						<input type="checkbox" name="in_stock" id="vn-in-stock" value="1">
						<span class="vn-toggle-switch"></span>
						<span class="vn-item-label">📦 Chỉ còn hàng</span>
					</label>
				</div>
				<?php endif; ?>

				<div class="vn-filter-actions">
					<button type="submit" class="vn-btn vn-btn-filter" id="vn-apply-filter">🔍 Lọc sản phẩm</button>
				</div>
			</form>
		</div>
		<?php
	}

	/**
	 * Form cài đặt widget trong admin
	 */
	public function form( $instance ) {
		$title           = $instance['title'] ?? 'Lọc sản phẩm';
		$show_everywhere = $instance['show_everywhere'] ?? 0;
		?>
		<p>
			<label for="<?php echo $this->get_field_id( 'title' ); ?>">Tiêu đề:</label>
			<input class="widefat" type="text"
				id="<?php echo $this->get_field_id( 'title' ); ?>"
				name="<?php echo $this->get_field_name( 'title' ); ?>"
				value="<?php echo esc_attr( $title ); ?>">
		</p>
		<p>
			<label>
				<input type="checkbox"
					name="<?php echo $this->get_field_name( 'show_everywhere' ); ?>"
					<?php checked( $show_everywhere, 1 ); ?> value="1">
				Hiển thị trên tất cả các trang
			</label>
		</p>
		<p style="color:#666;font-size:12px;">
			Mặc định widget chỉ hiện trên trang Shop, Category, Tag của WooCommerce.<br>
			Cấu hình chi tiết tại <a href="<?php echo admin_url( 'admin.php?page=vn-filter-settings' ); ?>">Bộ lọc SP Settings</a>.
		</p>
		<?php
	}

	/**
	 * Lưu cài đặt widget
	 */
	public function update( $new_instance, $old_instance ) {
		return [
			'title'           => sanitize_text_field( $new_instance['title'] ?? '' ),
			'show_everywhere' => ! empty( $new_instance['show_everywhere'] ) ? 1 : 0,
		];
	}
}
