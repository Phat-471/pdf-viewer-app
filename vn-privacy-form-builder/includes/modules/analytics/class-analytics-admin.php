<?php
/**
 * VN Analytics Module - Admin View
 */
if ( ! defined( 'ABSPATH' ) ) exit;

class VN_Analytics_Admin {

	public function __construct() {
		add_action( 'admin_enqueue_scripts', [ $this, 'enqueue' ] );
	}

	public function enqueue( $hook ) {
		$is_analytics_tab = ( isset( $_GET['page'] ) && $_GET['page'] === 'vn-settings' && isset( $_GET['setting_tab'] ) && $_GET['setting_tab'] === 'analytics' );
		if ( strpos( $hook, 'vn-analytics' ) === false && ! $is_analytics_tab ) return;
		wp_enqueue_style( 'vn-privacy-admin', VN_PRIVACY_URL . 'assets/admin.css', [], VN_PRIVACY_VERSION );
	}

	public static function handle_save() {
		if ( empty( $_POST['vn_analytics_nonce_field'] ) ) return;
		if ( ! wp_verify_nonce( $_POST['vn_analytics_nonce_field'], 'vn_save_analytics' ) ) return;
		if ( ! current_user_can( 'manage_options' ) ) return;

		$is_settings_page = ( isset( $_GET['page'] ) && $_GET['page'] === 'vn-settings' );
		$page_slug        = $is_settings_page ? 'vn-settings' : 'vn-analytics';

		if ( isset( $_POST['action'] ) && $_POST['action'] === 'clear_logs' ) {
			VN_Analytics_Core::truncate_logs();
			$args = [
				'page'    => $page_slug,
				'tab'     => 'logs',
				'cleared' => '1',
			];
			if ( $is_settings_page ) {
				$args['setting_tab'] = 'analytics';
			}
			wp_redirect( add_query_arg( $args, admin_url( 'admin.php' ) ) );
			exit;
		}

		VN_Analytics_Core::save_settings( $_POST );
		$args = [
			'page'  => $page_slug,
			'tab'   => sanitize_text_field( $_POST['active_tab'] ?? 'dashboard' ),
			'saved' => '1',
		];
		if ( $is_settings_page ) {
			$args['setting_tab'] = 'analytics';
		}
		wp_redirect( add_query_arg( $args, admin_url( 'admin.php' ) ) );
		exit;
	}

	public static function render_page() {
		if ( ! current_user_can( 'manage_options' ) ) return;
		$settings = VN_Analytics_Core::get_settings();
		$tab      = sanitize_text_field( $_GET['tab'] ?? 'dashboard' );
		$saved    = isset( $_GET['saved'] );
		$cleared  = isset( $_GET['cleared'] );
		$stats    = VN_Analytics_Core::get_stats();

		$is_settings_page = ( isset( $_GET['page'] ) && $_GET['page'] === 'vn-settings' );
		$page_slug        = $is_settings_page ? 'vn-settings' : 'vn-analytics';
		$setting_tab_arg  = $is_settings_page ? [ 'setting_tab' => 'analytics' ] : [];
		?>
		<?php if ( ! $is_settings_page ) : ?>
		<div class="wrap"><div id="vn-privacy-app">
		<div class="vn-page-header">
			<div class="vn-page-header-left">
				<h1>📊 Báo Cáo Lượt Xem</h1>
				<p>Xem số liệu truy cập chi tiết, các trang được xem nhiều nhất và cấu hình lưu trữ log.</p>
			</div>
		</div>
		<?php endif; ?>

		<?php if ( $saved ) : ?>
		<div class="vn-alert vn-alert-success" style="margin-bottom:20px;"><span class="vn-alert-icon">✅</span><div>Đã lưu cài đặt!</div></div>
		<?php endif; ?>

		<?php if ( $cleared ) : ?>
		<div class="vn-alert vn-alert-success" style="margin-bottom:20px;"><span class="vn-alert-icon">🧹</span><div>Đã xóa sạch tất cả nhật ký lượt xem!</div></div>
		<?php endif; ?>

		<!-- Tabs -->
		<div style="display:flex;flex-wrap:wrap;gap:4px;margin-bottom:24px;border-bottom:2px solid #e2e8f0;">
			<?php foreach ( [ 
				'dashboard' => '📊 Bảng điều khiển', 
				'logs'      => '📋 Nhật ký chi tiết', 
				'settings'  => '⚙️ Cài đặt'
			] as $key => $label ) :
				$active = $tab === $key ? 'background:#2563eb;color:#fff;' : 'background:#f1f5f9;color:#475569;';
				$link   = add_query_arg( array_merge( $setting_tab_arg, [ 'page' => $page_slug, 'tab' => $key ] ), admin_url( 'admin.php' ) );
			?>
			<a href="<?php echo esc_url( $link ); ?>"
			   style="padding:10px 20px;border-radius:8px 8px 0 0;text-decoration:none;font-weight:600;font-size:13px;<?php echo $active; ?>">
				<?php echo esc_html( $label ); ?>
			</a>
			<?php endforeach; ?>
		</div>

		<form method="POST">
		<?php wp_nonce_field( 'vn_save_analytics', 'vn_save_analytics' ); // use consistent nonce naming ?>
		<input type="hidden" name="active_tab" value="<?php echo esc_attr( $tab ); ?>">

		<?php if ( $tab === 'dashboard' ) : ?>
		<!-- ═══════ TAB: DASHBOARD ═══════ -->
		<!-- Stats Cards -->
		<div style="display:grid;grid-template-columns:repeat(auto-fit, minmax(220px, 1fr));gap:20px;margin-bottom:28px;">
			<div class="vn-card" style="border-radius:12px;border:1px solid #e2e8f0;padding:20px;background:#fff;box-shadow: 0 1px 3px rgba(0,0,0,0.05);">
				<div style="font-size:13px;font-weight:600;color:#64748b;margin-bottom:8px;">Hôm Nay</div>
				<div style="font-size:28px;font-weight:700;color:#1e293b;"><?php echo number_format($stats['today_pv']); ?> <span style="font-size:13px;font-weight:normal;color:#64748b;">PV</span></div>
				<div style="font-size:13px;color:#2563eb;margin-top:6px;font-weight:600;"><?php echo number_format($stats['today_uv']); ?> khách truy cập (UV)</div>
			</div>
			<div class="vn-card" style="border-radius:12px;border:1px solid #e2e8f0;padding:20px;background:#fff;box-shadow: 0 1px 3px rgba(0,0,0,0.05);">
				<div style="font-size:13px;font-weight:600;color:#64748b;margin-bottom:8px;">7 Ngày Qua</div>
				<div style="font-size:28px;font-weight:700;color:#1e293b;"><?php echo number_format($stats['week_pv']); ?> <span style="font-size:13px;font-weight:normal;color:#64748b;">PV</span></div>
				<div style="font-size:13px;color:#2563eb;margin-top:6px;font-weight:600;"><?php echo number_format($stats['week_uv']); ?> khách truy cập (UV)</div>
			</div>
			<div class="vn-card" style="border-radius:12px;border:1px solid #e2e8f0;padding:20px;background:#fff;box-shadow: 0 1px 3px rgba(0,0,0,0.05);">
				<div style="font-size:13px;font-weight:600;color:#64748b;margin-bottom:8px;">30 Ngày Qua</div>
				<div style="font-size:28px;font-weight:700;color:#1e293b;"><?php echo number_format($stats['month_pv']); ?> <span style="font-size:13px;font-weight:normal;color:#64748b;">PV</span></div>
				<div style="font-size:13px;color:#2563eb;margin-top:6px;font-weight:600;"><?php echo number_format($stats['month_uv']); ?> khách truy cập (UV)</div>
			</div>
			<div class="vn-card" style="border-radius:12px;border:1px solid #e2e8f0;padding:20px;background:#fff;box-shadow: 0 1px 3px rgba(0,0,0,0.05);">
				<div style="font-size:13px;font-weight:600;color:#64748b;margin-bottom:8px;">Tổng Số Lượt Xem (Log)</div>
				<div style="font-size:28px;font-weight:700;color:#1e293b;"><?php echo number_format($stats['total_pv']); ?> <span style="font-size:13px;font-weight:normal;color:#64748b;">PV</span></div>
				<div style="font-size:13px;color:#64748b;margin-top:6px;">Giới hạn lưu trữ: <?php echo (int) $settings['retention_days']; ?> ngày</div>
			</div>
		</div>

		<!-- Top Pages -->
		<div class="vn-card" style="border-radius:12px;border:1px solid #e2e8f0;padding:24px;background:#fff;box-shadow: 0 1px 3px rgba(0,0,0,0.05);">
			<h3 style="margin-top:0;margin-bottom:18px;">🔥 Trang được xem nhiều nhất</h3>
			<table class="wp-list-table widefat fixed striped" style="border:none;box-shadow:none;">
				<thead>
					<tr>
						<th style="padding:12px;font-weight:600;width:50%;">Liên kết</th>
						<th style="padding:12px;font-weight:600;width:25%;text-align:center;">Lượt xem (PV)</th>
						<th style="padding:12px;font-weight:600;width:25%;text-align:center;">Lượt xem duy nhất (UV)</th>
					</tr>
				</thead>
				<tbody>
					<?php
					$top_pages = VN_Analytics_Core::get_top_pages(15);
					if ( empty( $top_pages ) ) : ?>
						<tr>
							<td colspan="3" style="padding:40px;text-align:center;color:#94a3b8;">Chưa có dữ liệu lượt xem nào được ghi nhận. Hãy truy cập website ở ngoài frontend để tạo lượt xem!</td>
						</tr>
					<?php else :
						foreach ( $top_pages as $page ) :
							$title = '';
							if ( $page->post_id > 0 ) {
								$title = get_the_title( $page->post_id );
							}
							?>
							<tr>
								<td style="padding:12px;vertical-align:middle;word-break:break-all;">
									<?php if ( $title ) : ?>
										<strong style="display:block;margin-bottom:2px;color:#1e293b;"><?php echo esc_html($title); ?></strong>
									<?php endif; ?>
									<a href="<?php echo esc_url($page->url); ?>" target="_blank" style="color:#2563eb;text-decoration:none;font-size:12px;"><?php echo esc_html($page->url); ?></a>
								</td>
								<td style="padding:12px;text-align:center;vertical-align:middle;font-weight:700;color:#1e293b;"><?php echo number_format($page->views); ?></td>
								<td style="padding:12px;text-align:center;vertical-align:middle;color:#475569;"><?php echo number_format($page->unique_views); ?></td>
							</tr>
						<?php endforeach;
					endif; ?>
				</tbody>
			</table>
		</div>

		<?php elseif ( $tab === 'logs' ) : ?>
		<!-- ═══════ TAB: RECENT LOGS ═══════ -->
		<div class="vn-card" style="border-radius:12px;border:1px solid #e2e8f0;padding:24px;background:#fff;box-shadow: 0 1px 3px rgba(0,0,0,0.05);">
			<div style="display:flex;justify-content:between;align-items:center;margin-bottom:18px;flex-wrap:wrap;gap:12px;">
				<h3 style="margin:0;flex:1;">📋 Nhật ký lượt xem gần đây (tối đa 200)</h3>
				<button type="submit" name="action" value="clear_logs" 
					onclick="return confirm('Bạn có chắc chắn muốn xóa toàn bộ nhật ký lượt xem không? Hành động này không thể hoàn tác!')"
					style="padding:8px 16px;background:#dc2626;color:#fff;border:none;border-radius:6px;font-size:12px;font-weight:600;cursor:pointer;">
					🗑️ Xóa sạch Log lượt xem
				</button>
			</div>

			<table class="wp-list-table widefat fixed striped" style="border:none;box-shadow:none;font-size:13px;">
				<thead>
					<tr>
						<th style="padding:12px;font-weight:600;width:18%;">Thời gian</th>
						<th style="padding:12px;font-weight:600;width:40%;">Trang truy cập</th>
						<th style="padding:12px;font-weight:600;width:15%;">Địa chỉ IP</th>
						<th style="padding:12px;font-weight:600;width:27%;">User Agent / Trình duyệt</th>
					</tr>
				</thead>
				<tbody>
					<?php
					$logs = VN_Analytics_Core::get_recent_views(200);
					if ( empty( $logs ) ) : ?>
						<tr>
							<td colspan="4" style="padding:40px;text-align:center;color:#94a3b8;">Chưa có lượt truy cập nào.</td>
						</tr>
					<?php else :
						foreach ( $logs as $log ) :
							$title = $log->post_id > 0 ? get_the_title($log->post_id) : '';
							?>
							<tr>
								<td style="padding:12px;vertical-align:middle;color:#64748b;"><?php echo esc_html( date('d/m/Y H:i:s', strtotime($log->viewed_at)) ); ?></td>
								<td style="padding:12px;vertical-align:middle;word-break:break-all;">
									<?php if ( $title ) : ?>
										<strong style="display:block;margin-bottom:2px;color:#1e293b;"><?php echo esc_html($title); ?></strong>
									<?php endif; ?>
									<a href="<?php echo esc_url($log->url); ?>" target="_blank" style="color:#2563eb;text-decoration:none;font-size:12px;"><?php echo esc_html($log->url); ?></a>
									<?php if ( $log->referrer ) : ?>
										<div style="font-size:11px;color:#94a3b8;margin-top:4px;word-break:break-all;">Nguồn: <?php echo esc_html($log->referrer); ?></div>
									<?php endif; ?>
								</td>
								<td style="padding:12px;vertical-align:middle;font-family:monospace;"><?php echo esc_html($log->ip); ?></td>
								<td style="padding:12px;vertical-align:middle;color:#475569;font-size:11px;word-break:break-word;"><?php echo esc_html($log->user_agent); ?></td>
							</tr>
						<?php endforeach;
					endif; ?>
				</tbody>
			</table>
		</div>

		<?php elseif ( $tab === 'settings' ) : ?>
		<!-- ═══════ TAB: SETTINGS ═══════ -->
		<div style="display:grid;grid-template-columns:1fr 1fr;gap:24px;">
			<div class="vn-card" style="border-radius:12px;border:1px solid #e2e8f0;padding:24px;background:#fff;box-shadow: 0 1px 3px rgba(0,0,0,0.05);">
				<h3 style="margin-top:0;margin-bottom:18px;">⚙️ Cấu hình lượt xem</h3>
				<div style="display:flex;flex-direction:column;gap:16px;">
					<label style="display:flex;align-items:center;gap:10px;cursor:pointer;padding:12px;border:1px solid #e2e8f0;border-radius:8px;background:#f8fafc;">
						<input type="checkbox" name="analytics_enabled" value="1" <?php checked( $settings['analytics_enabled'], 1 ); ?>>
						<div>
							<strong>Kích hoạt theo dõi lượt xem</strong><br>
							<span style="font-size:12px;color:#64748b;">Ghi nhận mọi truy cập frontend vào cơ sở dữ liệu.</span>
						</div>
					</label>

					<label style="display:flex;align-items:center;gap:10px;cursor:pointer;padding:12px;border:1px solid #e2e8f0;border-radius:8px;background:#f8fafc;">
						<input type="checkbox" name="exclude_logged_in" value="1" <?php checked( $settings['exclude_logged_in'], 1 ); ?>>
						<div>
							<strong>Không theo dõi quản trị viên/thành viên</strong><br>
							<span style="font-size:12px;color:#64748b;">Không ghi nhận lượt xem của những ai đã đăng nhập vào website.</span>
						</div>
					</label>

					<label style="display:flex;align-items:center;gap:10px;cursor:pointer;padding:12px;border:1px solid #e2e8f0;border-radius:8px;background:#f8fafc;">
						<input type="checkbox" name="exclude_bots" value="1" <?php checked( $settings['exclude_bots'], 1 ); ?>>
						<div>
							<strong>Loại trừ bot tìm kiếm (Google, Bing...)</strong><br>
							<span style="font-size:12px;color:#64748b;">Bật tính năng này giúp số liệu chính xác hơn, tránh bị làm nhiễu bởi crawler.</span>
						</div>
					</label>

					<div>
						<label style="font-weight:600;display:block;margin-bottom:6px;">Thời gian lưu trữ nhật ký lượt xem:</label>
						<div style="display:flex;align-items:center;gap:10px;">
							<input type="number" name="retention_days" value="<?php echo esc_attr( $settings['retention_days'] ); ?>" min="1" max="365" style="width:100px;padding:8px 12px;border:1px solid #e2e8f0;border-radius:6px;">
							<span>ngày</span>
						</div>
						<p style="font-size:12px;color:#64748b;margin-top:6px;">Dữ liệu vượt quá số ngày này sẽ được tự động xóa để giải phóng cơ sở dữ liệu.</p>
					</div>
				</div>

				<button type="submit" style="margin-top:24px;width:100%;padding:11px;background:#2563eb;color:#fff;border:none;border-radius:8px;font-size:14px;font-weight:600;cursor:pointer;">
					💾 Lưu cấu hình
				</button>
			</div>

			<div class="vn-card" style="border-radius:12px;border:1px solid #e2e8f0;padding:24px;background:#fff;box-shadow: 0 1px 3px rgba(0,0,0,0.05);">
				<h3 style="margin-top:0;">💡 Khuyến nghị tối ưu cơ sở dữ liệu</h3>
				<p>Tính năng theo dõi lượt xem trực tiếp trong WordPress lưu trữ dữ liệu vào bảng cơ sở dữ liệu tự tạo. Điều này rất tiện lợi cho các website vừa và nhỏ vì không cần tích hợp bên thứ ba (như Google Analytics).</p>
				<p><strong>Tuy nhiên:</strong></p>
				<ul style="padding-left:20px;line-height:1.6;color:#475569;">
					<li>Nếu website có lượng truy cập cực kỳ lớn (hàng triệu lượt xem/tháng), cơ sở dữ liệu có thể phình to nhanh chóng.</li>
					<li>Nên giữ thời gian lưu trữ ở mức <strong>30 ngày</strong> đến <strong>60 ngày</strong> để tối ưu hiệu năng database.</li>
					<li>Sử dụng nút <strong>"Xóa sạch Log lượt xem"</strong> trong tab Nhật ký chi tiết để giải phóng dung lượng bất cứ lúc nào.</li>
				</ul>
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
