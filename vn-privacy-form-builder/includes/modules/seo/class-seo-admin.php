<?php
/**
 * VN SEO & Utilities Module - Admin Page
 */
if ( ! defined( 'ABSPATH' ) ) exit;

class VN_SEO_Admin {

	public function __construct() {
		add_action( 'admin_enqueue_scripts', [ $this, 'enqueue' ] );
	}

	public function enqueue( $hook ) {
		$is_seo_tab = ( isset( $_GET['page'] ) && $_GET['page'] === 'vn-settings' && isset( $_GET['setting_tab'] ) && $_GET['setting_tab'] === 'seo' );
		if ( strpos( $hook, 'vn-seo' ) === false && ! $is_seo_tab ) return;
		wp_enqueue_style( 'vn-privacy-admin', VN_PRIVACY_URL . 'assets/admin.css', [], VN_PRIVACY_VERSION );
	}

	public static function handle_save() {
		if ( empty( $_POST['vn_seo_nonce_field'] ) ) return;
		if ( ! wp_verify_nonce( $_POST['vn_seo_nonce_field'], 'vn_save_seo' ) ) return;
		if ( ! current_user_can( 'manage_options' ) ) return;
		VN_SEO_Core::save_settings( $_POST );
		
		$is_settings_page = ( isset( $_GET['page'] ) && $_GET['page'] === 'vn-settings' );
		$page_slug        = $is_settings_page ? 'vn-settings' : 'vn-seo';
		$args = [
			'page'  => $page_slug,
			'tab'   => sanitize_text_field( $_POST['active_tab'] ?? 'sitemap' ),
			'saved' => '1',
		];
		if ( $is_settings_page ) {
			$args['setting_tab'] = 'seo';
		}
		wp_redirect( add_query_arg( $args, admin_url( 'admin.php' ) ) );
		exit;
	}

	public static function render_page() {
		if ( ! current_user_can( 'manage_options' ) ) return;
		$settings     = VN_SEO_Core::get_settings();
		$tab          = sanitize_text_field( $_GET['tab'] ?? 'sitemap' );
		$saved        = isset( $_GET['saved'] );
		$sitemap_url  = home_url( '/sitemap.xml' );
		$sitemap_stat = $settings['sitemap_enabled'] ? VN_SEO_Core::get_sitemap_stats() : 0;
		$is_settings_page = ( isset( $_GET['page'] ) && $_GET['page'] === 'vn-settings' );
		$page_slug        = $is_settings_page ? 'vn-settings' : 'vn-seo';
		$setting_tab_arg  = $is_settings_page ? [ 'setting_tab' => 'seo' ] : [];
		?>
		<?php if ( ! $is_settings_page ) : ?>
		<div class="wrap"><div id="vn-privacy-app">
		<div class="vn-page-header">
			<div class="vn-page-header-left">
				<h1>📈 SEO & Tiện Ích</h1>
				<p>XML Sitemap, Chèn script Analytics/Pixel, Nút liên hệ nhanh</p>
			</div>
		</div>
		<?php endif; ?>

		<?php if ( $saved ) : ?>
		<div class="vn-alert vn-alert-success" style="margin-bottom:20px;"><span class="vn-alert-icon">✅</span><div>Đã lưu cài đặt!</div></div>
		<?php endif; ?>

		<!-- Tabs -->
		<div style="display:flex;flex-wrap:wrap;gap:4px;margin-bottom:24px;border-bottom:2px solid #e2e8f0;">
			<?php foreach ( [ 
				'sitemap' => '🗺️ XML Sitemap', 
				'scripts' => '📜 Chèn Script', 
				'contact' => '📞 Nút liên hệ',
				'meta'    => '📝 Thẻ Meta',
				'og'      => '🖼️ Open Graph',
				'breadcrumb' => '🍞 Breadcrumb'
			] as $key => $label ) :
				$active = $tab === $key ? 'background:#059669;color:#fff;' : 'background:#f1f5f9;color:#475569;';
				$link   = add_query_arg( array_merge( $setting_tab_arg, [ 'page' => $page_slug, 'tab' => $key ] ), admin_url( 'admin.php' ) );
			?>
			<a href="<?php echo esc_url( $link ); ?>"
			   style="padding:10px 20px;border-radius:8px 8px 0 0;text-decoration:none;font-weight:600;font-size:13px;<?php echo $active; ?>">
				<?php echo esc_html( $label ); ?>
			</a>
			<?php endforeach; ?>
		</div>

		<form method="POST">
		<?php wp_nonce_field( 'vn_save_seo', 'vn_seo_nonce_field' ); ?>
		<input type="hidden" name="active_tab" value="<?php echo esc_attr( $tab ); ?>">

		<?php if ( $tab === 'sitemap' ) : ?>
		<!-- ═══════ TAB: SITEMAP ═══════ -->
		<div style="display:grid;grid-template-columns:1fr 1fr;gap:24px;">
			<div class="vn-card" style="border-radius:12px;border:1px solid #e2e8f0;padding:24px;background:#fff;">
				<h3 style="margin-top:0;">🗺️ XML Sitemap</h3>
				<label style="display:flex;align-items:center;gap:10px;cursor:pointer;padding:12px;border:1px solid #e2e8f0;border-radius:8px;background:#f8fafc;margin-bottom:16px;">
					<input type="checkbox" name="sitemap_enabled" value="1" <?php checked( $settings['sitemap_enabled'], 1 ); ?>>
					<div><strong>Bật XML Sitemap tự động</strong><br>
					<span style="font-size:12px;color:#94a3b8;">Tạo sitemap.xml giúp Google index nhanh hơn</span></div>
				</label>
				<div style="margin-bottom:16px;">
					<p style="font-weight:600;margin-bottom:10px;">Bao gồm trong sitemap:</p>
					<?php foreach ( ['sitemap_posts' => '📝 Bài viết', 'sitemap_pages' => '📄 Trang tĩnh', 'sitemap_cats' => '📁 Danh mục'] as $name => $label ) : ?>
					<label style="display:flex;align-items:center;gap:8px;cursor:pointer;margin-bottom:8px;padding:8px 12px;background:#f8fafc;border-radius:6px;border:1px solid #e2e8f0;">
						<input type="checkbox" name="<?php echo $name; ?>" value="1" <?php checked( $settings[ $name ], 1 ); ?>>
						<span><?php echo $label; ?></span>
					</label>
					<?php endforeach; ?>
				</div>
				<button type="submit" style="width:100%;padding:11px;background:#059669;color:#fff;border:none;border-radius:8px;font-size:14px;font-weight:600;cursor:pointer;">
					💾 Lưu & Tạo lại Sitemap
				</button>
			</div>

			<div class="vn-card" style="border-radius:12px;border:1px solid #e2e8f0;padding:24px;background:#fff;">
				<h3 style="margin-top:0;">📊 Thông tin Sitemap</h3>
				<?php if ( $settings['sitemap_enabled'] ) : ?>
				<div style="display:flex;flex-direction:column;gap:12px;">
					<div style="padding:16px;background:#f0fdf4;border-radius:8px;border:1px solid #bbf7d0;">
						<div style="font-size:24px;font-weight:700;color:#059669;"><?php echo number_format( $sitemap_stat ); ?> URLs</div>
						<div style="font-size:13px;color:#166534;">đã được đưa vào sitemap</div>
					</div>
					<div>
						<label style="font-weight:600;display:block;margin-bottom:6px;">URL Sitemap:</label>
						<div style="display:flex;gap:8px;align-items:center;">
							<code style="background:#1e293b;color:#a5f3fc;padding:8px 12px;border-radius:6px;font-size:13px;flex:1;word-break:break-all;">
								<?php echo esc_html( $sitemap_url ); ?>
							</code>
							<a href="<?php echo esc_url( $sitemap_url ); ?>" target="_blank" class="button">Xem</a>
						</div>
					</div>
					<div style="background:#fef9c3;border:1px solid #fde68a;border-radius:8px;padding:12px;font-size:13px;color:#713f12;">
						💡 Thêm URL sitemap vào <strong>Google Search Console</strong> để Google index nhanh hơn.
					</div>
				</div>
				<?php else : ?>
				<div style="padding:40px 20px;text-align:center;color:#94a3b8;">
					<div style="font-size:2rem;margin-bottom:12px;">🗺️</div>
					<p>Bật Sitemap ở bên trái để bắt đầu.</p>
				</div>
				<?php endif; ?>
			</div>
		</div>

		<?php elseif ( $tab === 'scripts' ) : ?>
		<!-- ═══════ TAB: SCRIPTS ═══════ -->
		<div style="display:grid;grid-template-columns:1fr 1fr;gap:24px;">
			<?php foreach ( [
				['head_scripts',   '📄 Chèn vào <head>',   'Dùng cho Google Analytics (GA4), Google Tag Manager, Facebook Pixel...'],
				['footer_scripts', '📄 Chèn vào <footer>', 'Dùng cho live chat widget, remarketing scripts, Hotjar...'],
			] as [$name, $label, $desc] ) : ?>
			<div class="vn-card" style="border-radius:12px;border:1px solid #e2e8f0;padding:24px;background:#fff;">
				<h3 style="margin-top:0;"><?php echo $label; ?></h3>
				<p style="color:#64748b;font-size:13px;margin-bottom:12px;"><?php echo esc_html( $desc ); ?></p>
				<textarea name="<?php echo $name; ?>" rows="12"
					placeholder="<!-- Dán mã Google Analytics, Facebook Pixel... vào đây -->"
					style="width:100%;padding:10px;border:1px solid #e2e8f0;border-radius:6px;font-family:monospace;font-size:12px;resize:vertical;background:#1e293b;color:#e2e8f0;"
					><?php echo esc_textarea( $settings[ $name ] ); ?></textarea>
			</div>
			<?php endforeach; ?>
		</div>
		<div style="margin-top:20px;">
			<button type="submit" style="padding:11px 28px;background:#059669;color:#fff;border:none;border-radius:8px;font-size:14px;font-weight:600;cursor:pointer;">
				💾 Lưu Scripts
			</button>
		</div>

		<?php elseif ( $tab === 'contact' ) : ?>
		<!-- ═══════ TAB: CONTACT BUTTONS ═══════ -->
		<div style="display:grid;grid-template-columns:1fr 1fr;gap:24px;">
			<div class="vn-card" style="border-radius:12px;border:1px solid #e2e8f0;padding:24px;background:#fff;">
				<h3 style="margin-top:0;">📞 Nút liên hệ nhanh</h3>
				<div style="display:flex;flex-direction:column;gap:14px;">
					<label style="display:flex;align-items:center;gap:10px;cursor:pointer;padding:12px;border:1px solid #e2e8f0;border-radius:8px;background:#f8fafc;">
						<input type="checkbox" name="contact_enabled" value="1" <?php checked( $settings['contact_enabled'], 1 ); ?>>
						<strong>Bật nút liên hệ nhanh trên website</strong>
					</label>

					<div>
						<label style="font-weight:600;display:block;margin-bottom:6px;">📞 Số điện thoại</label>
						<input type="text" name="contact_phone" value="<?php echo esc_attr( $settings['contact_phone'] ); ?>"
							placeholder="0901234567" style="width:100%;padding:8px 12px;border:1px solid #e2e8f0;border-radius:6px;">
					</div>
					<div>
						<label style="font-weight:600;display:block;margin-bottom:6px;">💬 Số Zalo</label>
						<input type="text" name="contact_zalo" value="<?php echo esc_attr( $settings['contact_zalo'] ); ?>"
							placeholder="0901234567 hoặc https://zalo.me/..." style="width:100%;padding:8px 12px;border:1px solid #e2e8f0;border-radius:6px;">
					</div>
					<div>
						<label style="font-weight:600;display:block;margin-bottom:6px;">✉️ Link Messenger</label>
						<input type="url" name="contact_messenger" value="<?php echo esc_attr( $settings['contact_messenger'] ); ?>"
							placeholder="https://m.me/your-page" style="width:100%;padding:8px 12px;border:1px solid #e2e8f0;border-radius:6px;">
					</div>

					<div>
						<label style="font-weight:600;display:block;margin-bottom:8px;">📍 Vị trí (trên PC)</label>
						<div style="display:flex;gap:12px;">
							<?php foreach ( ['right' => '▶ Góc phải', 'left' => '◀ Góc trái'] as $val => $lbl ) : ?>
							<label style="display:flex;align-items:center;gap:6px;cursor:pointer;padding:10px 16px;border:2px solid <?php echo $settings['contact_position']===$val?'#059669':'#e2e8f0'; ?>;border-radius:8px;flex:1;justify-content:center;background:<?php echo $settings['contact_position']===$val?'#f0fdf4':'#fff'; ?>">
								<input type="radio" name="contact_position" value="<?php echo $val; ?>" <?php checked( $settings['contact_position'], $val ); ?> style="display:none;">
								<?php echo $lbl; ?>
							</label>
							<?php endforeach; ?>
						</div>
					</div>
				</div>
				<button type="submit" style="margin-top:20px;width:100%;padding:11px;background:#059669;color:#fff;border:none;border-radius:8px;font-size:14px;font-weight:600;cursor:pointer;">
					💾 Lưu cài đặt
				</button>
			</div>

			<div class="vn-card" style="border-radius:12px;border:1px solid #e2e8f0;padding:24px;background:#fff;">
				<h3 style="margin-top:0;">⚙️ Tùy chọn hiển thị</h3>
				<div style="display:flex;flex-direction:column;gap:14px;">
					<label style="display:flex;align-items:center;gap:10px;cursor:pointer;padding:12px;border:1px solid #e2e8f0;border-radius:8px;background:#f8fafc;">
						<input type="checkbox" name="contact_show_label" value="1" <?php checked( $settings['contact_show_label'], 1 ); ?>>
						<div><strong>Hiện nhãn tên</strong> bên cạnh icon<br>
						<span style="font-size:12px;color:#94a3b8;">Tắt để chỉ hiện icon trên PC</span></div>
					</label>

					<div style="background:#e0f2fe;border:1px solid #bae6fd;border-radius:8px;padding:14px;">
						<p style="font-weight:600;margin:0 0 10px;font-size:13px;color:#0c4a6e;">📱 Hiển thị theo thiết bị:</p>
						<label style="display:flex;align-items:center;gap:8px;cursor:pointer;margin-bottom:8px;">
							<input type="checkbox" name="contact_hide_desktop" value="1" <?php checked( $settings['contact_hide_desktop'] ?? 0, 1 ); ?>>
							<span style="font-size:13px;">🖥️ Ẩn trên PC / Desktop</span>
						</label>
						<label style="display:flex;align-items:center;gap:8px;cursor:pointer;">
							<input type="checkbox" name="contact_hide_mobile" value="1" <?php checked( $settings['contact_hide_mobile'] ?? 0, 1 ); ?>>
							<span style="font-size:13px;">📱 Ẩn trên Mobile / Tablet</span>
						</label>
					</div>

					<div style="background:#f8fafc;border-radius:8px;padding:14px;border:1px solid #e2e8f0;">
						<p style="font-weight:600;margin:0 0 8px;font-size:13px;">🖼️ Xem trước giao diện:</p>
						<div style="font-size:12px;color:#64748b;line-height:1.8;">
							<strong>PC:</strong> Nút dọc ở góc màn hình, hover hiện nhãn + hiệu ứng mở rộng<br>
							<strong>Mobile:</strong> Thanh ngang cố định ở đáy màn hình (như bottom nav của app)
						</div>
					</div>
				</div>
			</div>
		</div>
		<?php elseif ( $tab === 'meta' ) : ?>
		<!-- ═══════ TAB: META ═══════ -->
		<div style="display:grid;grid-template-columns:1fr 1fr;gap:24px;">
			<div class="vn-card" style="border-radius:12px;border:1px solid #e2e8f0;padding:24px;background:#fff;">
				<h3 style="margin-top:0;">📝 Tự động tối ưu thẻ Meta Description</h3>
				<div style="display:flex;flex-direction:column;gap:14px;">
					<label style="display:flex;align-items:center;gap:10px;cursor:pointer;padding:12px;border:1px solid #e2e8f0;border-radius:8px;background:#f8fafc;">
						<input type="checkbox" name="meta_desc_enabled" value="1" <?php checked( $settings['meta_desc_enabled'] ?? 1, 1 ); ?>>
						<strong>Bật Meta Description tự động</strong>
					</label>
					<div>
						<label style="font-weight:600;display:block;margin-bottom:6px;">Độ dài mô tả tối đa (kí tự):</label>
						<input type="number" name="meta_desc_length" value="<?php echo esc_attr( $settings['meta_desc_length'] ?? 160 ); ?>" min="50" max="320" style="width:100%;padding:8px 12px;border:1px solid #e2e8f0;border-radius:6px;">
						<p style="font-size:12px;color:#64748b;margin-top:6px;">Khuyên dùng từ 120 đến 160 kí tự.</p>
					</div>
				</div>
				<button type="submit" style="margin-top:20px;width:100%;padding:11px;background:#059669;color:#fff;border:none;border-radius:8px;font-size:14px;font-weight:600;cursor:pointer;">
					💾 Lưu cài đặt
				</button>
			</div>
			<div class="vn-card" style="border-radius:12px;border:1px solid #e2e8f0;padding:24px;background:#fff;">
				<h3 style="margin-top:0;">💡 Cơ chế hoạt động</h3>
				<p>Hệ thống tự động phân tích và tạo thẻ meta description tối ưu cho công cụ tìm kiếm:</p>
				<ul style="padding-left:20px;line-height:1.6;color:#475569;">
					<li><strong>Bài viết / Trang tĩnh:</strong> Sử dụng Tóm tắt (Excerpt), nếu không có sẽ tự động lấy đoạn đầu tiên của nội dung bài viết và rút gọn theo độ dài đã chọn.</li>
					<li><strong>Trang danh mục / thẻ:</strong> Lấy Mô tả (Description) của danh mục/thẻ đó.</li>
					<li><strong>Trang chủ:</strong> Lấy khẩu hiệu (Tagline/Description) của website trong Cấu hình chung.</li>
				</ul>
			</div>
		</div>

		<?php elseif ( $tab === 'og' ) : ?>
		<!-- ═══════ TAB: OPEN GRAPH ═══════ -->
		<div style="display:grid;grid-template-columns:1fr 1fr;gap:24px;">
			<div class="vn-card" style="border-radius:12px;border:1px solid #e2e8f0;padding:24px;background:#fff;">
				<h3 style="margin-top:0;">🖼️ Open Graph & Twitter Cards</h3>
				<div style="display:flex;flex-direction:column;gap:14px;">
					<label style="display:flex;align-items:center;gap:10px;cursor:pointer;padding:12px;border:1px solid #e2e8f0;border-radius:8px;background:#f8fafc;">
						<input type="checkbox" name="og_enabled" value="1" <?php checked( $settings['og_enabled'] ?? 1, 1 ); ?>>
						<strong>Bật Open Graph & Twitter Card</strong>
					</label>
					<div>
						<label style="font-weight:600;display:block;margin-bottom:6px;">Tên Website (og:site_name):</label>
						<input type="text" name="og_site_name" value="<?php echo esc_attr( $settings['og_site_name'] ?? get_bloginfo('name') ); ?>" style="width:100%;padding:8px 12px;border:1px solid #e2e8f0;border-radius:6px;">
					</div>
					<div>
						<label style="font-weight:600;display:block;margin-bottom:6px;">Ảnh đại diện mặc định (og:image):</label>
						<input type="url" name="og_default_image" value="<?php echo esc_attr( $settings['og_default_image'] ?? '' ); ?>" placeholder="https://yoursite.com/wp-content/uploads/...jpg" style="width:100%;padding:8px 12px;border:1px solid #e2e8f0;border-radius:6px;">
						<p style="font-size:12px;color:#64748b;margin-top:6px;">Dùng khi chia sẻ các trang không có ảnh đại diện (ảnh nổi bật).</p>
					</div>
					<div>
						<label style="font-weight:600;display:block;margin-bottom:6px;">Tài khoản Twitter (X):</label>
						<div style="position:relative;">
							<span style="position:absolute;left:12px;top:50%;transform:translateY(-50%);color:#94a3b8;font-weight:600;">@</span>
							<input type="text" name="og_twitter_handle" value="<?php echo esc_attr( $settings['og_twitter_handle'] ?? '' ); ?>" placeholder="username" style="width:100%;padding:8px 12px 8px 28px;border:1px solid #e2e8f0;border-radius:6px;">
						</div>
					</div>
				</div>
				<button type="submit" style="margin-top:20px;width:100%;padding:11px;background:#059669;color:#fff;border:none;border-radius:8px;font-size:14px;font-weight:600;cursor:pointer;">
					💾 Lưu cài đặt
				</button>
			</div>
			<div class="vn-card" style="border-radius:12px;border:1px solid #e2e8f0;padding:24px;background:#fff;">
				<h3 style="margin-top:0;">💡 Lợi ích của Open Graph</h3>
				<p>Các thẻ meta Open Graph giúp hiển thị đẹp mắt, đầy đủ tiêu đề, mô tả và hình ảnh lớn khi chia sẻ liên kết lên các mạng xã hội như Facebook, Zalo, Twitter, LinkedIn...</p>
				<p>Nếu không bật tính năng này, mạng xã hội sẽ tự quét và có thể hiển thị sai ảnh hoặc không hiện ảnh thu nhỏ của liên kết.</p>
			</div>
		</div>

		<?php elseif ( $tab === 'breadcrumb' ) : ?>
		<!-- ═══════ TAB: BREADCRUMB ═══════ -->
		<div style="display:grid;grid-template-columns:1fr 1fr;gap:24px;">
			<div class="vn-card" style="border-radius:12px;border:1px solid #e2e8f0;padding:24px;background:#fff;">
				<h3 style="margin-top:0;">🍞 Breadcrumb Schema Markup</h3>
				<div style="display:flex;flex-direction:column;gap:14px;">
					<label style="display:flex;align-items:center;gap:10px;cursor:pointer;padding:12px;border:1px solid #e2e8f0;border-radius:8px;background:#f8fafc;">
						<input type="checkbox" name="breadcrumb_schema" value="1" <?php checked( $settings['breadcrumb_schema'] ?? 1, 1 ); ?>>
						<strong>Bật Breadcrumb JSON-LD</strong>
					</label>
				</div>
				<button type="submit" style="margin-top:20px;width:100%;padding:11px;background:#059669;color:#fff;border:none;border-radius:8px;font-size:14px;font-weight:600;cursor:pointer;">
					💾 Lưu cài đặt
				</button>
			</div>
			<div class="vn-card" style="border-radius:12px;border:1px solid #e2e8f0;padding:24px;background:#fff;">
				<h3 style="margin-top:0;">💡 Breadcrumb JSON-LD là gì?</h3>
				<p>Tính năng này chèn mã cấu trúc JSON-LD ẩn vào trang web của bạn để khai báo vị trí phân cấp của trang (ví dụ: Trang chủ > Tin tức > Bài viết).</p>
				<p>Nó giúp các công cụ tìm kiếm như Google hiểu rõ cấu trúc liên kết trang web của bạn và hiển thị thanh điều hướng Breadcrumbs trực tiếp trên kết quả tìm kiếm, tăng tỉ lệ click chuột (CTR).</p>
			</div>
		</div>
		<?php endif; ?>

		</form>
		<?php if ( ! $is_settings_page ) : ?>
		</div></div>
		<?php endif; ?>
		<?php
	}
}
