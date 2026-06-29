<?php
/**
 * VN Performance Module - Admin Page v2
 * Tabs: Dọn dẹp DB | WebP | Minify+LazyLoad | Tự động (Cron) | Lịch sử
 */
if ( ! defined( 'ABSPATH' ) ) exit;

class VN_Performance_Admin {

	public function __construct() {
		add_action( 'admin_enqueue_scripts', [ $this, 'enqueue' ] );
	}

	public function enqueue( $hook ) {
		$is_perf_tab = ( isset( $_GET['page'] ) && $_GET['page'] === 'vn-settings' && isset( $_GET['setting_tab'] ) && $_GET['setting_tab'] === 'performance' );
		if ( strpos( $hook, 'vn-performance' ) === false && ! $is_perf_tab ) return;
		wp_enqueue_style( 'vn-privacy-admin', VN_PRIVACY_URL . 'assets/admin.css', [], VN_PRIVACY_VERSION );
		wp_enqueue_script( 'vn-performance-admin', VN_PRIVACY_URL . 'assets/performance.js', [ 'jquery' ], VN_PRIVACY_VERSION, true );
		wp_enqueue_script( 'vn-performance-bulk', VN_PRIVACY_URL . 'assets/performance-bulk.js', [ 'jquery' ], VN_PRIVACY_VERSION, true );
		wp_localize_script( 'vn-performance-admin', 'vnPerf', [
			'ajaxUrl' => admin_url( 'admin-ajax.php' ),
			'nonce'   => wp_create_nonce( 'vn_performance_nonce' ),
		] );
	}

	public static function handle_save() {
		if ( empty( $_POST['vn_performance_nonce_field'] ) ) return;
		if ( ! wp_verify_nonce( $_POST['vn_performance_nonce_field'], 'vn_save_performance' ) ) return;
		if ( ! current_user_can( 'manage_options' ) ) return;

		// Xóa log nếu yêu cầu
		if ( ! empty( $_POST['clear_log'] ) ) {
			VN_Performance_Core::clear_cleanup_log();
		}

		VN_Performance_Core::save_settings( $_POST );
		
		$is_settings_page = ( isset( $_GET['page'] ) && $_GET['page'] === 'vn-settings' );
		$page_slug        = $is_settings_page ? 'vn-settings' : 'vn-performance';
		$args = [
			'page'  => $page_slug,
			'tab'   => sanitize_text_field( $_POST['active_tab'] ?? 'database' ),
			'saved' => '1',
		];
		if ( $is_settings_page ) {
			$args['setting_tab'] = 'performance';
		}
		wp_redirect( add_query_arg( $args, admin_url( 'admin.php' ) ) );
		exit;
	}

	public static function render_page() {
		if ( ! current_user_can( 'manage_options' ) ) return;
		$settings  = VN_Performance_Core::get_settings();
		$db_stats  = VN_Performance_Core::get_db_stats();
		$img_stats = VN_Performance_Core::get_image_stats();
		$tab       = sanitize_text_field( $_GET['tab'] ?? 'database' );
		$saved     = isset( $_GET['saved'] );

		$tabs = [
			'database' => '🗑️ Dọn dẹp DB',
			'webp'     => '🖼️ WebP',
			'speed'    => '⚡ Tốc độ',
			'cron'     => '📅 Tự động',
			'log'      => '📋 Lịch sử',
		];
		$is_settings_page = ( isset( $_GET['page'] ) && $_GET['page'] === 'vn-settings' );
		$page_slug        = $is_settings_page ? 'vn-settings' : 'vn-performance';
		$setting_tab_arg  = $is_settings_page ? [ 'setting_tab' => 'performance' ] : [];
		?>
		<?php if ( ! $is_settings_page ) : ?>
		<div class="wrap"><div id="vn-privacy-app">
		<div class="vn-page-header">
			<div class="vn-page-header-left">
				<h1>⚡ Hiệu Năng & Tối Ưu</h1>
				<p>Dọn dẹp Database · Chuyển WebP · Lazy Load · Tự động dọn theo lịch</p>
			</div>
		</div>
		<?php endif; ?>

		<?php if ( $saved ) : ?>
		<div class="vn-alert vn-alert-success" style="margin-bottom:20px;"><span class="vn-alert-icon">✅</span><div>Đã lưu cài đặt!</div></div>
		<?php endif; ?>

		<div style="display:flex;gap:4px;margin-bottom:24px;border-bottom:2px solid #e2e8f0;flex-wrap:wrap;">
			<?php foreach ( $tabs as $key => $label ) :
				$active = $tab === $key ? 'background:#7c3aed;color:#fff;' : 'background:#f1f5f9;color:#475569;';
				$link   = add_query_arg( array_merge( $setting_tab_arg, [ 'page' => $page_slug, 'tab' => $key ] ), admin_url( 'admin.php' ) );
			?>
			<a href="<?php echo esc_url( $link ); ?>"
			   style="padding:10px 18px;border-radius:8px 8px 0 0;text-decoration:none;font-weight:600;font-size:13px;<?php echo $active; ?>">
				<?php echo esc_html( $label ); ?>
			</a>
			<?php endforeach; ?>
		</div>

		<form method="POST">
		<?php wp_nonce_field( 'vn_save_performance', 'vn_performance_nonce_field' ); ?>
		<input type="hidden" name="active_tab" value="<?php echo esc_attr( $tab ); ?>">

		<?php
		switch ( $tab ) {
			case 'database': self::render_tab_database( $settings, $db_stats ); break;
			case 'webp':     self::render_tab_webp( $settings, $img_stats ); break;
			case 'speed':    self::render_tab_speed( $settings ); break;
			case 'cron':     self::render_tab_cron( $settings ); break;
			case 'log':      self::render_tab_log(); break;
		}
		?>
		</form>
		<?php if ( ! $is_settings_page ) : ?>
		</div></div>
		<?php endif; ?>
		<?php
	}

	/* ── Tab: Dọn dẹp DB ────────────────────────────────────── */
	private static function render_tab_database( $settings, $db_stats ) { ?>
	<div style="display:grid;grid-template-columns:1fr 1fr;gap:24px;">
		<div class="vn-card" style="border-radius:12px;border:1px solid #e2e8f0;padding:24px;background:#fff;">
			<h3 style="margin-top:0;">📊 Thống kê hiện tại</h3>
			<?php $items = [
				'revisions'    => ['📝 Revisions',           $db_stats['revisions'],    'warning'],
				'spam'         => ['🚫 Bình luận Spam',       $db_stats['spam'],         'danger'],
				'trash_posts'  => ['🗑️ Bài trong Trash',     $db_stats['trash_posts'],  'warning'],
				'expired_trans'=> ['⏱️ Transients hết hạn',  $db_stats['expired_trans'],'info'],
				'orphan_meta'  => ['🔗 Post Meta mồ côi',    $db_stats['orphan_meta'],  'info'],
			];
			foreach ( $items as $key => [$label,$count,$type] ) :
				$color = $count > 0 ? ($type==='danger'?'#ef4444':($type==='warning'?'#f59e0b':'#3b82f6')) : '#22c55e';
			?>
			<div style="display:flex;align-items:center;justify-content:space-between;padding:9px 12px;background:#f8fafc;border-radius:8px;border:1px solid #e2e8f0;margin-bottom:8px;">
				<span style="font-size:13px;"><?php echo esc_html($label); ?></span>
				<span id="stat-<?php echo $key; ?>" style="font-weight:700;font-size:15px;color:<?php echo $color; ?>;"><?php echo number_format($count); ?></span>
			</div>
			<?php endforeach; ?>
		</div>

		<div class="vn-card" style="border-radius:12px;border:1px solid #e2e8f0;padding:24px;background:#fff;">
			<h3 style="margin-top:0;">🧹 Dọn dẹp thủ công</h3>
			<?php foreach (['clean_revisions'=>'📝 Xóa Revisions thừa','clean_spam'=>'🚫 Xóa Spam','clean_transients'=>'⏱️ Xóa Transients hết hạn','clean_trash'=>'🗑️ Xóa bài Trash','clean_optimize'=>'⚙️ Optimize tables'] as $name=>$label) : ?>
			<label style="display:flex;align-items:center;gap:8px;cursor:pointer;padding:9px 12px;border:1px solid #e2e8f0;border-radius:8px;background:#f8fafc;margin-bottom:8px;">
				<input type="checkbox" name="<?php echo $name; ?>" value="1" checked>
				<span style="font-size:13px;"><?php echo esc_html($label); ?></span>
			</label>
			<?php endforeach; ?>
			<div style="margin:12px 0;">
				<label style="font-weight:600;font-size:13px;">Giữ lại revision:</label>
				<input type="number" id="keep-revisions" value="<?php echo esc_attr($settings['keep_revisions']); ?>" min="0" max="20"
					style="width:70px;padding:6px;border:1px solid #e2e8f0;border-radius:6px;margin-left:8px;">
				<span style="font-size:12px;color:#94a3b8;">(0 = xóa tất)</span>
			</div>
			<button type="button" id="vn-clean-db-btn" style="width:100%;padding:11px;background:#7c3aed;color:#fff;border:none;border-radius:8px;font-size:14px;font-weight:600;cursor:pointer;">
				🧹 Dọn dẹp ngay
			</button>
			<div id="vn-clean-result" style="margin-top:12px;display:none;"></div>
		</div>
	</div>
	<?php }

	/* ── Tab: WebP ──────────────────────────────────────────── */
	private static function render_tab_webp( $settings, $img_stats ) { ?>
	<div style="display:grid;grid-template-columns:1fr 1fr;gap:24px;">
		<div class="vn-card" style="border-radius:12px;border:1px solid #e2e8f0;padding:24px;background:#fff;">
			<h3 style="margin-top:0;">🖼️ Chuyển đổi WebP tự động</h3>
			<div style="background:#e0f2fe;border:1px solid #bae6fd;border-radius:8px;padding:12px;margin-bottom:16px;font-size:13px;color:#0c4a6e;">
				<strong>WebP</strong> giảm ~25-34% dung lượng so với JPEG, ~26% so với PNG — cùng chất lượng. <br>
				Ảnh gốc <strong>vẫn được giữ</strong>, file .webp tạo thêm bên cạnh.
			</div>
			<?php if ( ! $img_stats['gd_available'] ) : ?>
			<div style="background:#fef2f2;border:1px solid #fecaca;border-radius:8px;padding:12px;margin-bottom:12px;font-size:13px;color:#991b1b;">
				⚠️ PHP GD không hỗ trợ WebP. Liên hệ hosting bật <code>imagewebp()</code>.
			</div>
			<?php endif; ?>
			<label style="display:flex;align-items:center;gap:8px;cursor:pointer;padding:12px;border:1px solid #e2e8f0;border-radius:8px;background:#f8fafc;margin-bottom:14px;">
				<input type="checkbox" name="webp_enabled" value="1" <?php checked($settings['webp_enabled'],1); ?> <?php disabled(!$img_stats['gd_available']); ?>>
				<span><strong>Tự động tạo WebP</strong> khi tải ảnh lên</span>
			</label>
			<div>
				<label style="font-weight:600;display:block;margin-bottom:6px;">Chất lượng: <strong id="webp-quality-val"><?php echo $settings['webp_quality']; ?></strong>%</label>
				<input type="range" name="webp_quality" min="50" max="100" id="vn-webp-quality-slider" value="<?php echo esc_attr($settings['webp_quality']); ?>"
					oninput="document.getElementById('webp-quality-val').textContent=this.value" style="width:100%;">
				<div style="display:flex;justify-content:space-between;font-size:11px;color:#94a3b8;margin-top:3px;">
					<span>Nhỏ (50%)</span><span>Cân bằng (82%)</span><span>Tốt nhất (100%)</span>
				</div>
			</div>
			<button type="submit" style="margin-top:18px;width:100%;padding:11px;background:#7c3aed;color:#fff;border:none;border-radius:8px;font-size:14px;font-weight:600;cursor:pointer;">💾 Lưu cấu hình</button>
		</div>
		<div class="vn-card" style="border-radius:12px;border:1px solid #e2e8f0;padding:24px;background:#fff;">
			<h3 style="margin-top:0;">📊 Thống kê</h3>
			<?php foreach ([
				['Tổng ảnh trong thư viện', number_format($img_stats['total_images']), '#7c3aed'],
				['File WebP đã tạo', number_format($img_stats['webp_files']), '#22c55e'],
			] as [$lbl,$val,$color]) : ?>
			<div style="padding:14px;background:#f8fafc;border-radius:8px;border:1px solid #e2e8f0;margin-bottom:10px;">
				<div style="font-size:26px;font-weight:700;color:<?php echo $color; ?>;"><?php echo $val; ?></div>
				<div style="font-size:13px;color:#64748b;"><?php echo $lbl; ?></div>
			</div>
			<?php endforeach; ?>
			<div style="padding:12px;background:<?php echo $img_stats['gd_available']?'#f0fdf4':'#fef2f2'; ?>;border-radius:8px;border:1px solid <?php echo $img_stats['gd_available']?'#bbf7d0':'#fecaca'; ?>;">
				<strong style="color:<?php echo $img_stats['gd_available']?'#166534':'#991b1b'; ?>;">
					<?php echo $img_stats['gd_available'] ? '✅ PHP GD + WebP: Sẵn sàng' : '❌ WebP chưa khả dụng'; ?>
				</strong>
			</div>
		</div>

		<!-- Bulk WebP Converter Box -->
		<div class="vn-card" style="border-radius:12px;border:1px solid #e2e8f0;padding:24px;background:#fff;grid-column: span 2;">
			<h3 style="margin-top:0;">⚡ Chuyển đổi hàng loạt ảnh cũ sang WebP</h3>
			<p style="font-size:13px;color:#64748b;margin-bottom:16px;">
				Tính năng này sẽ quét tất cả các file hình ảnh (JPEG/PNG) cũ trong thư viện Media và tạo bản sao WebP cho chúng (bao gồm cả các ảnh kích thước thu nhỏ).
			</p>
			
			<div id="vn-bulk-webp-box" style="padding:16px;background:#f8fafc;border-radius:8px;border:1px solid #e2e8f0;">
				<div style="display:flex;gap:12px;align-items:center;margin-bottom:12px;flex-wrap:wrap;">
					<button type="button" id="vn-bulk-webp-scan" class="button button-secondary" style="font-weight:600;padding:6px 14px;">🔍 Quét ảnh trong thư viện</button>
					<button type="button" id="vn-bulk-webp-start" class="button button-primary" style="font-weight:600;display:none;background:#7c3aed;border-color:#7c3aed;color:#fff;padding:6px 14px;">⚡ Bắt đầu chuyển đổi</button>
					<span id="vn-bulk-webp-status" style="font-size:13px;color:#475569;"></span>
				</div>
				
				<!-- Progress Bar -->
				<div id="vn-bulk-webp-progress-wrap" style="display:none;margin-top:14px;">
					<div style="display:flex;justify-content:space-between;font-size:12px;font-weight:600;margin-bottom:6px;">
						<span id="vn-bulk-webp-progress-txt">Đang chuyển đổi: 0/0 ảnh</span>
						<span id="vn-bulk-webp-progress-pct">0%</span>
					</div>
					<div style="background:#e2e8f0;border-radius:9999px;height:12px;overflow:hidden;width:100%;">
						<div id="vn-bulk-webp-progress-bar" style="background:#7c3aed;width:0%;height:100%;transition:width 0.2s ease;"></div>
					</div>
				</div>
			</div>
		</div>
	</div>
	<?php }

	/* ── Tab: Tốc độ (Minify + Lazy Load) ─────────────────── */
	private static function render_tab_speed( $settings ) { ?>
	<div style="max-width:700px;display:flex;flex-direction:column;gap:20px;">
		<div class="vn-card" style="border-radius:12px;border:1px solid #e2e8f0;padding:24px;background:#fff;">
			<h3 style="margin-top:0;">🦥 Lazy Load (Tải chậm)</h3>
			<div style="background:#e0f2fe;border:1px solid #bae6fd;border-radius:8px;padding:12px;margin-bottom:14px;font-size:13px;color:#0c4a6e;">
				Trình duyệt chỉ tải tài nguyên khi người dùng cuộn đến nơi. Giảm đáng kể thời gian tải trang ban đầu.
			</div>
			<div style="display:flex;flex-direction:column;gap:10px;">
				<label style="display:flex;align-items:center;gap:10px;cursor:pointer;padding:12px;border:1px solid #e2e8f0;border-radius:8px;background:#f8fafc;">
					<input type="checkbox" name="lazy_load" value="1" <?php checked($settings['lazy_load'],1); ?>>
					<span><strong>Bật Lazy Load cho Hình ảnh</strong> (nội dung bài viết, ảnh đại diện, widget)</span>
				</label>
				<label style="display:flex;align-items:center;gap:10px;cursor:pointer;padding:12px;border:1px solid #e2e8f0;border-radius:8px;background:#f8fafc;">
					<input type="checkbox" name="lazy_load_iframe" value="1" <?php checked($settings['lazy_load_iframe'] ?? 1, 1); ?>>
					<span><strong>Bật Lazy Load cho Iframe & Video</strong> (Youtube nhúng, Google Maps nhúng)</span>
				</label>
			</div>
		</div>

		<div class="vn-card" style="border-radius:12px;border:1px solid #e2e8f0;padding:24px;background:#fff;">
			<h3 style="margin-top:0;">🚀 DNS Prefetch / Preconnect (Kết nối sớm)</h3>
			<div style="background:#f0fdf4;border:1px solid #bbf7d0;border-radius:8px;padding:12px;margin-bottom:14px;font-size:13px;color:#166534;">
				Khai báo các tên miền bên thứ ba để trình duyệt phân giải DNS và tạo kết nối HTTP trước khi nhận yêu cầu, giúp giảm độ trễ khi tải các mã script/font bên ngoài.
			</div>
			<div>
				<label style="font-weight:600;display:block;margin-bottom:8px;">Danh sách tên miền (mỗi dòng một tên miền):</label>
				<textarea name="dns_prefetch_list" rows="5" placeholder="fonts.googleapis.com&#10;connect.facebook.net&#10;www.googletagmanager.com" style="width:100%;padding:10px;border:1px solid #e2e8f0;border-radius:6px;font-family:monospace;font-size:13px;"><?php echo esc_textarea($settings['dns_prefetch_list'] ?? ''); ?></textarea>
				<p style="font-size:12px;color:#64748b;margin-top:6px;">Nhập domain không chứa http:// hoặc https:// (ví dụ: fonts.gstatic.com).</p>
			</div>
		</div>

		<div class="vn-card" style="border-radius:12px;border:1px solid #e2e8f0;padding:24px;background:#fff;">
			<h3 style="margin-top:0;">⚡ Minify HTML Output</h3>
			<div style="background:#fef9c3;border:1px solid #fde68a;border-radius:8px;padding:12px;margin-bottom:14px;font-size:13px;color:#713f12;">
				⚠️ Loại bỏ khoảng trắng và comment HTML thừa (~5-15% nhẹ hơn). Tắt nếu trang có vấn đề sau khi bật.
			</div>
			<label style="display:flex;align-items:center;gap:10px;cursor:pointer;padding:12px;border:1px solid #e2e8f0;border-radius:8px;background:#f8fafc;">
				<input type="checkbox" name="minify_html" value="1" <?php checked($settings['minify_html'],1); ?>>
				<span><strong>Bật Minify HTML</strong></span>
			</label>
		</div>

		<button type="submit" style="padding:11px 28px;background:#7c3aed;color:#fff;border:none;border-radius:8px;font-size:14px;font-weight:600;cursor:pointer;">💾 Lưu cấu hình</button>
	</div>
	<?php }

	/* ── Tab: Cron ──────────────────────────────────────────── */
	private static function render_tab_cron( $settings ) {
		$next   = VN_Performance_Core::get_next_cron_time();
		$items  = (array) $settings['cron_items'];
		?>
	<div style="max-width:700px;">
		<div class="vn-card" style="border-radius:12px;border:1px solid #e2e8f0;padding:24px;background:#fff;">
			<h3 style="margin-top:0;">📅 Dọn dẹp tự động theo lịch</h3>
			<div style="background:#e0f2fe;border:1px solid #bae6fd;border-radius:8px;padding:12px;margin-bottom:18px;font-size:13px;color:#0c4a6e;">
				WP-Cron sẽ tự động chạy dọn dẹp theo lịch bạn cài đặt — không cần làm thủ công.
			</div>

			<div style="margin-bottom:18px;">
				<label style="font-weight:600;display:block;margin-bottom:8px;">Tần suất dọn dẹp</label>
				<div style="display:flex;gap:10px;flex-wrap:wrap;">
					<?php $schedules = [
						'disabled'         => '🚫 Tắt',
						'vn_perf_daily'    => '📆 Mỗi ngày',
						'vn_perf_weekly'   => '📅 Mỗi tuần',
						'vn_perf_monthly'  => '🗓️ Mỗi tháng',
					];
					foreach ( $schedules as $val => $lbl ) :
						$sel = $settings['cron_schedule'] === $val;
					?>
					<label style="display:flex;align-items:center;gap:6px;cursor:pointer;padding:10px 16px;border:2px solid <?php echo $sel?'#7c3aed':'#e2e8f0'; ?>;border-radius:8px;background:<?php echo $sel?'#f5f3ff':'#fff'; ?>;">
						<input type="radio" name="cron_schedule" value="<?php echo $val; ?>" <?php checked($settings['cron_schedule'],$val); ?>>
						<?php echo $lbl; ?>
					</label>
					<?php endforeach; ?>
				</div>
			</div>

			<div style="margin-bottom:18px;">
				<label style="font-weight:600;display:block;margin-bottom:8px;">Tự động dọn:</label>
				<?php foreach ([
					'cron_rev'   => '📝 Revisions',
					'cron_spam'  => '🚫 Spam comments',
					'cron_trans' => '⏱️ Transients hết hạn',
					'cron_trash' => '🗑️ Bài viết Trash',
				] as $name=>$lbl) : ?>
				<label style="display:flex;align-items:center;gap:8px;cursor:pointer;padding:8px 12px;border:1px solid #e2e8f0;border-radius:7px;background:#f8fafc;margin-bottom:7px;">
					<input type="checkbox" name="<?php echo $name; ?>" value="1"
						<?php echo in_array(str_replace('cron_','',str_replace('cron_rev','revisions',str_replace('cron_spam','spam',str_replace('cron_trans','transients',str_replace('cron_trash','trash',$name))))), $items) ? 'checked' : ''; ?>>
					<?php echo $lbl; ?>
				</label>
				<?php endforeach; ?>
			</div>

			<div style="padding:14px;background:#f0fdf4;border:1px solid #bbf7d0;border-radius:8px;margin-bottom:18px;">
				<strong>🕐 Lần chạy tiếp theo:</strong>
				<span style="color:#166534;font-weight:700;margin-left:8px;"><?php echo esc_html($next); ?></span>
			</div>

			<button type="submit" style="width:100%;padding:11px;background:#7c3aed;color:#fff;border:none;border-radius:8px;font-size:14px;font-weight:600;cursor:pointer;">💾 Lưu lịch tự động</button>
		</div>
	</div>
	<?php }

	/* ── Tab: Lịch sử dọn dẹp ──────────────────────────────── */
	private static function render_tab_log() {
		$log = VN_Performance_Core::get_cleanup_log( 50 );
		?>
	<div style="max-width:900px;">
		<div class="vn-card" style="border-radius:12px;border:1px solid #e2e8f0;padding:24px;background:#fff;">
			<div style="display:flex;align-items:center;justify-content:space-between;margin-bottom:18px;">
				<h3 style="margin:0;">📋 Lịch sử dọn dẹp (50 gần nhất)</h3>
				<?php if ( ! empty( $log ) ) : ?>
				<button type="submit" name="clear_log" value="1" onclick="return confirm('Xóa toàn bộ lịch sử?')"
					style="padding:8px 16px;background:#ef4444;color:#fff;border:none;border-radius:7px;font-size:13px;font-weight:600;cursor:pointer;">
					🗑️ Xóa log
				</button>
				<?php endif; ?>
			</div>

			<?php if ( empty( $log ) ) : ?>
			<div style="padding:40px;text-align:center;color:#94a3b8;">
				<div style="font-size:2rem;margin-bottom:10px;">📭</div>
				<p>Chưa có lịch sử dọn dẹp. Thực hiện dọn dẹp để bắt đầu ghi log.</p>
			</div>
			<?php else : ?>
			<table style="width:100%;border-collapse:collapse;font-size:13px;">
				<thead>
					<tr style="background:#f8fafc;border-bottom:2px solid #e2e8f0;">
						<th style="padding:10px 12px;text-align:left;">Thời gian</th>
						<th style="padding:10px 12px;text-align:center;">Loại</th>
						<th style="padding:10px 12px;text-align:center;">Revisions</th>
						<th style="padding:10px 12px;text-align:center;">Spam</th>
						<th style="padding:10px 12px;text-align:center;">Transients</th>
						<th style="padding:10px 12px;text-align:center;">Trash</th>
					</tr>
				</thead>
				<tbody>
				<?php foreach ( $log as $i => $entry ) :
					$bg = $i % 2 === 0 ? '#fff' : '#f8fafc';
					$c  = $entry['cleaned'] ?? [];
				?>
				<tr style="background:<?php echo $bg; ?>;border-bottom:1px solid #e2e8f0;">
					<td style="padding:9px 12px;color:#475569;"><?php echo esc_html($entry['time']); ?></td>
					<td style="padding:9px 12px;text-align:center;">
						<span style="padding:3px 10px;border-radius:12px;font-size:11px;font-weight:600;<?php echo $entry['type']==='auto'?'background:#dbeafe;color:#1e40af;':'background:#e9d5ff;color:#6b21a8;'; ?>">
							<?php echo $entry['type'] === 'auto' ? '🤖 Tự động' : '👤 Thủ công'; ?>
						</span>
					</td>
					<td style="padding:9px 12px;text-align:center;color:#7c3aed;font-weight:600;"><?php echo isset($c['revisions'])  ? number_format($c['revisions'])  : '—'; ?></td>
					<td style="padding:9px 12px;text-align:center;color:#dc2626;font-weight:600;"><?php echo isset($c['spam'])       ? number_format($c['spam'])       : '—'; ?></td>
					<td style="padding:9px 12px;text-align:center;color:#0284c7;font-weight:600;"><?php echo isset($c['transients']) ? number_format($c['transients']) : '—'; ?></td>
					<td style="padding:9px 12px;text-align:center;color:#d97706;font-weight:600;"><?php echo isset($c['trash'])      ? number_format($c['trash'])      : '—'; ?></td>
				</tr>
				<?php endforeach; ?>
				</tbody>
			</table>
			<?php endif; ?>
		</div>
	</div>
	<?php }
}
