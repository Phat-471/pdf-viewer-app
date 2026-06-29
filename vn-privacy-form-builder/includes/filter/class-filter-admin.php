<?php
/**
 * VN Product Filter - Admin Settings Page
 * Trang quản lý cài đặt bộ lọc sản phẩm
 */
if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

class VN_Filter_Admin {

	public function __construct() {
		add_action( 'admin_menu',            [ $this, 'add_menu' ] );
		add_action( 'admin_init',            [ $this, 'save_settings' ] );
		add_action( 'admin_enqueue_scripts', [ $this, 'enqueue_scripts' ] );
	}

	public function add_menu() {
		// Được gọi từ class-admin-menu.php
	}

	public function enqueue_scripts( $hook ) {
		if ( strpos( $hook, 'vn-filter-settings' ) === false ) return;
		wp_enqueue_style( 'vn-privacy-admin', VN_PRIVACY_URL . 'assets/admin.css', [], VN_PRIVACY_VERSION );
	}

	/**
	 * Lưu cài đặt khi submit form
	 */
	public function save_settings() {
		self::save_settings_static();
	}

	/**
	 * Static wrapper — được gọi từ add_action('admin_init', [..., 'save_settings_static'])
	 */
	public static function save_settings_static() {
		if ( ! isset( $_POST['vn_filter_save_nonce'] ) ) return;
		if ( ! wp_verify_nonce( $_POST['vn_filter_save_nonce'], 'vn_filter_save_settings' ) ) return;
		if ( ! current_user_can( 'manage_options' ) ) return;

		VN_Filter_Core::save_settings( $_POST );

		// Redirect về trang settings với thông báo thành công
		$tab = sanitize_text_field( $_POST['active_tab'] ?? 'structure' );
		wp_redirect( add_query_arg( [ 'page' => 'vn-filter-settings', 'tab' => $tab, 'saved' => '1' ], admin_url( 'admin.php' ) ) );
		exit;
	}

	/**
	 * Render trang settings
	 */
	public static function render_page() {
		if ( ! current_user_can( 'manage_options' ) ) return;
		if ( ! class_exists( 'WooCommerce' ) ) {
			echo '<div class="wrap"><div class="notice notice-error"><p>⚠️ Module Lọc Sản Phẩm yêu cầu <strong>WooCommerce</strong> phải được cài đặt và kích hoạt.</p></div></div>';
			return;
		}

		$settings    = VN_Filter_Core::get_settings();
		$filter_data = VN_Filter_Core::get_filter_data();
		$tab         = sanitize_text_field( $_GET['tab'] ?? 'structure' );
		$saved       = isset( $_GET['saved'] ) && $_GET['saved'] === '1';
		?>
		<div class="wrap">
			<div class="vn-page-header" style="display:flex;align-items:center;gap:16px;margin-bottom:24px;">
				<div style="width:48px;height:48px;background:linear-gradient(135deg,#f59e0b,#d97706);border-radius:12px;display:flex;align-items:center;justify-content:center;font-size:24px;flex-shrink:0;">🔍</div>
				<div>
					<h1 style="margin:0;font-size:1.5rem;color:#1e293b;">VN Product Filter</h1>
					<p style="margin:0;color:#64748b;font-size:13px;">Bộ lọc sản phẩm WooCommerce – Cấu hình và tùy chỉnh</p>
				</div>
			</div>

			<?php if ( $saved ) : ?>
				<div class="notice notice-success is-dismissible"><p>✅ Đã lưu cài đặt thành công!</p></div>
			<?php endif; ?>

			<form method="POST">
				<?php wp_nonce_field( 'vn_filter_save_settings', 'vn_filter_save_nonce' ); ?>
				<input type="hidden" name="active_tab" id="active_tab_input" value="<?php echo esc_attr( $tab ); ?>">

				<!-- Tab navigation -->
				<div style="display:flex;gap:4px;margin-bottom:20px;border-bottom:2px solid #e2e8f0;">
					<?php
					$tabs = [
						'structure' => '📋 Cấu trúc Filter',
						'display'   => '🎨 Hiển thị',
						'shortcode' => '⚙️ Shortcodes',
					];
					foreach ( $tabs as $key => $label ) :
						$active_class = $tab === $key
							? 'background:' . esc_attr( $settings['primary_color'] ) . ';color:#fff;'
							: 'background:#f1f5f9;color:#475569;';
					?>
					<a href="<?php echo esc_url( add_query_arg( [ 'page' => 'vn-filter-settings', 'tab' => $key ], admin_url( 'admin.php' ) ) ); ?>"
					   style="padding:10px 20px;border-radius:8px 8px 0 0;text-decoration:none;font-weight:600;font-size:13px;<?php echo $active_class; ?>">
						<?php echo esc_html( $label ); ?>
					</a>
					<?php endforeach; ?>
				</div>

				<!-- TAB: Structure -->
				<?php if ( $tab === 'structure' ) : ?>
				<div style="display:grid;grid-template-columns:1fr 1fr;gap:24px;">
					<div class="vn-card" style="border-radius:12px;border:1px solid #e2e8f0;padding:24px;background:#fff;">
						<h3 style="margin-top:0;color:#1e293b;">Kích hoạt bộ lọc</h3>
						<p style="color:#64748b;font-size:13px;">Chọn những loại filter nào sẽ hiển thị trong form lọc</p>

						<div style="display:flex;flex-direction:column;gap:12px;margin-top:16px;">
							<?php
							$filter_options = [
								'product_cat' => '📁 Danh mục sản phẩm',
								'_price'      => '💰 Khoảng giá',
								'_stock'      => '📦 Tình trạng kho',
								'product_tag' => '🏷️ Thẻ sản phẩm',
							];
							foreach ( $filter_options as $key => $label ) :
								$checked = in_array( $key, $settings['active_filters'] ) ? 'checked' : '';
							?>
							<label style="display:flex;align-items:center;gap:10px;cursor:pointer;padding:10px;border:1px solid #e2e8f0;border-radius:8px;background:#f8fafc;">
								<input type="checkbox" name="active_filters[]" value="<?php echo esc_attr( $key ); ?>" <?php echo $checked; ?>>
								<span><?php echo esc_html( $label ); ?></span>
							</label>
							<?php endforeach; ?>

							<!-- Thuộc tính WooCommerce -->
							<?php if ( ! empty( $filter_data['attributes'] ) ) : ?>
								<div style="border-top:1px solid #e2e8f0;padding-top:12px;margin-top:4px;">
									<p style="font-size:12px;color:#94a3b8;margin:0 0 8px;">Thuộc tính sản phẩm:</p>
									<?php foreach ( $filter_data['attributes'] as $attr ) :
										$checked = in_array( $attr['slug'], $settings['active_filters'] ) ? 'checked' : '';
									?>
									<label style="display:flex;align-items:center;gap:10px;cursor:pointer;padding:8px;border:1px solid #e2e8f0;border-radius:8px;background:#f8fafc;margin-bottom:6px;">
										<input type="checkbox" name="active_filters[]" value="<?php echo esc_attr( $attr['slug'] ); ?>" <?php echo $checked; ?>>
										<span>🔖 <?php echo esc_html( $attr['label'] ); ?></span>
									</label>
									<?php endforeach; ?>
								</div>
							<?php endif; ?>
						</div>
					</div>

					<div class="vn-card" style="border-radius:12px;border:1px solid #e2e8f0;padding:24px;background:#fff;">
						<h3 style="margin-top:0;color:#1e293b;">Tùy chọn hiển thị</h3>

						<div style="display:flex;flex-direction:column;gap:16px;">
							<div>
								<label style="font-weight:600;display:block;margin-bottom:6px;">Sản phẩm/trang</label>
								<input type="number" name="per_page" value="<?php echo esc_attr( $settings['per_page'] ); ?>"
									min="1" max="100" style="width:80px;padding:8px;border:1px solid #e2e8f0;border-radius:6px;">
							</div>
							<div>
								<label style="font-weight:600;display:block;margin-bottom:6px;">Số cột hiển thị</label>
								<select name="columns" style="padding:8px;border:1px solid #e2e8f0;border-radius:6px;">
									<?php for ( $c = 1; $c <= 6; $c++ ) : ?>
										<option value="<?php echo $c; ?>" <?php selected( $settings['columns'], $c ); ?>><?php echo $c; ?> cột</option>
									<?php endfor; ?>
								</select>
							</div>
							<div>
								<label style="font-weight:600;display:block;margin-bottom:6px;">Sắp xếp mặc định</label>
								<select name="orderby" style="padding:8px;border:1px solid #e2e8f0;border-radius:6px;">
									<option value="date" <?php selected( $settings['orderby'], 'date' ); ?>>Mới nhất</option>
									<option value="popularity" <?php selected( $settings['orderby'], 'popularity' ); ?>>Phổ biến</option>
									<option value="rating" <?php selected( $settings['orderby'], 'rating' ); ?>>Đánh giá</option>
									<option value="price" <?php selected( $settings['orderby'], 'price' ); ?>>Giá tăng dần</option>
									<option value="price-desc" <?php selected( $settings['orderby'], 'price-desc' ); ?>>Giá giảm dần</option>
									<option value="title" <?php selected( $settings['orderby'], 'title' ); ?>>Tên A-Z</option>
								</select>
							</div>
							<div>
								<label style="font-weight:600;display:block;margin-bottom:6px;">Màu chủ đạo</label>
								<input type="color" name="primary_color" value="<?php echo esc_attr( $settings['primary_color'] ); ?>"
									style="height:40px;width:100px;border:1px solid #e2e8f0;border-radius:6px;cursor:pointer;">
							</div>
							<div style="display:flex;flex-direction:column;gap:8px;">
								<label style="display:flex;align-items:center;gap:8px;cursor:pointer;">
									<input type="checkbox" name="show_count" value="1" <?php checked( $settings['show_count'], 1 ); ?>>
									<span>Hiển thị số lượng sản phẩm cho mỗi tùy chọn</span>
								</label>
								<label style="display:flex;align-items:center;gap:8px;cursor:pointer;">
									<input type="checkbox" name="show_reset" value="1" <?php checked( $settings['show_reset'], 1 ); ?>>
									<span>Hiển thị nút "Đặt lại bộ lọc"</span>
								</label>
							</div>
						</div>
					</div>
				</div>

				<!-- TAB: Display -->
				<?php elseif ( $tab === 'display' ) : ?>
				<div style="display:grid;grid-template-columns:1fr 1fr;gap:24px;">
					<!-- Card: Nút Đọc tiếp -->
					<div class="vn-card" style="border-radius:12px;border:1px solid #e2e8f0;padding:24px;background:#fff;">
						<h3 style="margin-top:0;color:#1e293b;">🔗 Nút Đọc tiếp (khi hết hàng)</h3>
						<p style="color:#64748b;font-size:13px;margin-bottom:16px;">
							Khi sản phẩm hết hàng, thay vì ẩn nút thêm giỏ hàng có thể hiển thị nút
							<strong>Đọc tiếp</strong> đưa khách vào trang sản phẩm.
							<br><em style="font-size:12px;color:#94a3b8;">Màu nút tự động đồng bộ với Màu chủ đạo đã chọn.</em>
						</p>
						<div style="display:flex;flex-direction:column;gap:16px;">
							<label style="display:flex;align-items:center;gap:10px;cursor:pointer;padding:12px;border:1px solid #e2e8f0;border-radius:8px;background:#f8fafc;">
								<input type="checkbox" name="show_read_more" value="1"
									<?php checked( $settings['show_read_more'] ?? 1, 1 ); ?>>
								<span><strong>Hiển thị nút Đọc tiếp</strong> khi sản phẩm hết hàng</span>
							</label>
							<div>
								<label style="font-weight:600;display:block;margin-bottom:6px;">Nhãn nút</label>
								<input type="text" name="read_more_label"
									value="<?php echo esc_attr( $settings['read_more_label'] ?? 'Đọc tiếp' ); ?>"
									placeholder="Đọc tiếp"
									style="padding:8px 12px;border:1px solid #e2e8f0;border-radius:6px;width:200px;">
								<p style="color:#94a3b8;font-size:12px;margin-top:4px;">Ví dụ: Đọc tiếp, Xem chi tiết, Xem thêm...</p>
							</div>
							<!-- Preview nút -->
							<div style="padding:12px;border:1px dashed #e2e8f0;border-radius:8px;background:#fafafa;">
								<p style="margin:0 0 8px;font-size:12px;color:#94a3b8;">Xem trước:</p>
								<a style="display:inline-block;padding:9px 18px;background:<?php echo esc_attr( $settings['primary_color'] ?? '#d97706' ); ?>;color:#fff;border-radius:7px;font-size:13px;font-weight:600;text-decoration:none;cursor:default;">
									<?php echo esc_html( $settings['read_more_label'] ?? 'Đọc tiếp' ); ?>
								</a>
							</div>
						</div>
					</div>

					<!-- Card: Tùy chọn hiển thị khác -->
					<div class="vn-card" style="border-radius:12px;border:1px solid #e2e8f0;padding:24px;background:#fff;">
						<h3 style="margin-top:0;color:#1e293b;">🎨 Tùy chọn hiển thị</h3>
						<div style="display:flex;flex-direction:column;gap:16px;">
							<div>
								<label style="font-weight:600;display:block;margin-bottom:6px;">Sản phẩm/trang</label>
								<input type="number" name="per_page" value="<?php echo esc_attr( $settings['per_page'] ); ?>"
									min="1" max="100" style="width:80px;padding:8px;border:1px solid #e2e8f0;border-radius:6px;">
							</div>
							<div>
								<label style="font-weight:600;display:block;margin-bottom:6px;">Số cột hiển thị</label>
								<select name="columns" style="padding:8px;border:1px solid #e2e8f0;border-radius:6px;">
									<?php for ( $c = 1; $c <= 6; $c++ ) : ?>
										<option value="<?php echo $c; ?>" <?php selected( $settings['columns'], $c ); ?>><?php echo $c; ?> cột</option>
									<?php endfor; ?>
								</select>
							</div>
							<div>
								<label style="font-weight:600;display:block;margin-bottom:6px;">Sắp xếp mặc định</label>
								<select name="orderby" style="padding:8px;border:1px solid #e2e8f0;border-radius:6px;">
									<option value="date" <?php selected( $settings['orderby'], 'date' ); ?>>Mới nhất</option>
									<option value="popularity" <?php selected( $settings['orderby'], 'popularity' ); ?>>Phổ biến</option>
									<option value="rating" <?php selected( $settings['orderby'], 'rating' ); ?>>Đánh giá</option>
									<option value="price" <?php selected( $settings['orderby'], 'price' ); ?>>Giá tăng dần</option>
									<option value="price-desc" <?php selected( $settings['orderby'], 'price-desc' ); ?>>Giá giảm dần</option>
									<option value="title" <?php selected( $settings['orderby'], 'title' ); ?>>Tên A-Z</option>
								</select>
							</div>
							<div>
								<label style="font-weight:600;display:block;margin-bottom:6px;">Màu chủ đạo</label>
								<input type="color" name="primary_color" value="<?php echo esc_attr( $settings['primary_color'] ); ?>"
									style="height:40px;width:100px;border:1px solid #e2e8f0;border-radius:6px;cursor:pointer;">
							</div>
							<div style="display:flex;flex-direction:column;gap:8px;">
								<label style="display:flex;align-items:center;gap:8px;cursor:pointer;">
									<input type="checkbox" name="show_count" value="1" <?php checked( $settings['show_count'], 1 ); ?>>
									<span>Hiển thị số lượng sản phẩm cho mỗi tùy chọn</span>
								</label>
								<label style="display:flex;align-items:center;gap:8px;cursor:pointer;">
									<input type="checkbox" name="show_reset" value="1" <?php checked( $settings['show_reset'], 1 ); ?>>
									<span>Hiển thị nút "Đặt lại bộ lọc"</span>
								</label>
							</div>
						</div>
					</div>
				</div>

				<?php elseif ( $tab === 'shortcode' ) : ?>
				<!-- TAB: Shortcodes -->
				<div class="vn-card" style="border-radius:12px;border:1px solid #e2e8f0;padding:24px;background:#fff;max-width:800px;">
					<h3 style="margin-top:0;color:#1e293b;">📋 Hướng dẫn sử dụng Shortcodes</h3>

					<?php
					$examples = [
						[
							'code'  => '[vn_filter_products]',
							'desc'  => 'Bộ lọc + sản phẩm layout sidebar trái (mặc định)',
						],
						[
							'code'  => '[vn_filter_products layout="sidebar-right"]',
							'desc'  => 'Bộ lọc + sản phẩm layout sidebar phải',
						],
						[
							'code'  => '[vn_filter_products layout="top-bar"]',
							'desc'  => 'Bộ lọc ngang phía trên + sản phẩm bên dưới',
						],
						[
							'code'  => '[vn_filter_products per_page="12" columns="4"]',
							'desc'  => 'Tùy chỉnh số sản phẩm/trang và số cột',
						],
						[
							'code'  => '[vn_filter]',
							'desc'  => 'Chỉ hiển thị form bộ lọc (dùng kết hợp thủ công)',
						],
						[
							'code'  => '[vn_products per_page="12" columns="3" orderby="price"]',
							'desc'  => 'Chỉ hiển thị danh sách sản phẩm (không có filter)',
						],
					];
					foreach ( $examples as $ex ) : ?>
					<div style="background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:16px;margin-bottom:16px;">
						<code style="display:block;background:#1e293b;color:#a5f3fc;padding:12px 16px;border-radius:6px;font-size:14px;margin-bottom:8px;word-break:break-all;">
							<?php echo esc_html( $ex['code'] ); ?>
						</code>
						<p style="margin:0;color:#475569;font-size:13px;">📌 <?php echo esc_html( $ex['desc'] ); ?></p>
					</div>
					<?php endforeach; ?>

					<div style="background:#fef9c3;border:1px solid #fde68a;border-radius:8px;padding:16px;margin-top:20px;">
						<strong>💡 Hướng dẫn nhanh:</strong>
						<ol style="margin:8px 0 0;padding-left:20px;color:#713f12;font-size:13px;">
							<li>Vào <strong>Pages → Add New</strong> hoặc trang Shop của WooCommerce</li>
							<li>Dán shortcode vào nội dung trang</li>
							<li>Hoặc thêm Widget <strong>"🔍 VN Product Filter"</strong> vào Appearance → Widgets</li>
						</ol>
					</div>
				</div>
				<?php endif; ?>

				<?php if ( in_array( $tab, [ 'structure', 'display' ] ) ) : ?>
				<div style="margin-top:20px;">
					<button type="submit" class="button button-primary" style="height:40px;padding:0 24px;font-size:14px;">
						💾 Lưu cài đặt
					</button>
				</div>
				<?php endif; ?>
			</form>
		</div>
		<?php
	}
}
