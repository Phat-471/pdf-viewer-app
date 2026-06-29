<?php
/**
 * Unified System Settings Page - centralizes settings tabs:
 * Tiện ích chung, Hiệu năng, Bảo mật, SEO, Lượt xem
 */
if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

class VN_Privacy_Admin_Settings {

	public static function render_page() {
		if ( ! current_user_can( 'manage_options' ) ) return;
		
		$active_tab = sanitize_text_field( $_GET['setting_tab'] ?? 'utilities' );

		$tabs = [
			'utilities'   => '⚙️ Tiện ích chung',
			'performance' => '⚡ Hiệu năng',
			'security'    => '🔒 Bảo mật',
			'seo'         => '📈 SEO & Tiện ích',
			'analytics'   => '📊 Lượt xem',
		];
		?>
		<div class="wrap" style="margin-top:20px; background:var(--vn-bg); color:var(--vn-text); padding:20px; border-radius:var(--vn-radius);">
			<!-- Header -->
			<div class="vn-page-header" style="background: linear-gradient(135deg, #111827 0%, #1e1b4b 100%); border: 1px solid var(--vn-border); border-radius: var(--vn-radius); padding:24px; margin-bottom:24px; box-shadow: var(--vn-shadow-lg); display: flex; align-items: center; justify-content: space-between;">
				<div style="display:flex;align-items:center;gap:16px;">
					<div style="font-size:32px;">⚙️</div>
					<div>
						<h1 style="margin:0;font-size:22px;font-weight:800;color:#fff;">Cấu hình & Tối ưu hệ thống</h1>
						<p style="margin:4px 0 0;color:var(--vn-muted);font-size:14px;">Quản lý toàn bộ cấu hình tối ưu, bảo mật, SEO và thống kê lượt xem website.</p>
					</div>
				</div>
			</div>

			<!-- Centralized Tab Bar -->
			<div style="display:flex;flex-wrap:wrap;gap:6px;margin-bottom:24px;border-bottom:2px solid var(--vn-border);">
				<?php foreach ( $tabs as $key => $label ) :
					$active = $active_tab === $key;
					$style = $active 
						? 'background:var(--vn-accent);color:#fff;border-bottom:3px solid var(--vn-accent);box-shadow: 0 4px 12px var(--vn-accent-glow);' 
						: 'background:#1f2937;color:var(--vn-muted);border-bottom:1px solid var(--vn-border);';
				?>
				<a href="<?php echo esc_url( add_query_arg( [ 'page' => 'vn-settings', 'setting_tab' => $key ], admin_url( 'admin.php' ) ) ); ?>"
				   style="padding:12px 24px;border-radius:8px 8px 0 0;text-decoration:none;font-weight:600;font-size:14px;transition:all 0.2s;<?php echo $style; ?>">
					<?php echo esc_html( $label ); ?>
				</a>
				<?php endforeach; ?>
			</div>

			<!-- Render Inner Content -->
			<div class="vn-settings-inner-content">
				<?php
				switch ( $active_tab ) {
					case 'utilities':
						if ( class_exists( 'VN_Privacy_Admin_Utilities' ) ) {
							$util_view = new VN_Privacy_Admin_Utilities();
							$backups     = VN_Privacy_Backup_Manager::list_backups();
							$health_data = VN_Privacy_System_Health::get_system_health();
							$file_stats  = VN_Privacy_System_Health::get_file_stats();
							$util_view->render( $backups, $health_data, $file_stats );
						}
						break;
					case 'performance':
						if ( class_exists( 'VN_Performance_Admin' ) ) {
							VN_Performance_Admin::render_page();
						}
						break;
					case 'security':
						if ( class_exists( 'VN_Security_Admin' ) ) {
							VN_Security_Admin::render_page();
						}
						break;
					case 'seo':
						if ( class_exists( 'VN_SEO_Admin' ) ) {
							VN_SEO_Admin::render_page();
						}
						break;
					case 'analytics':
						if ( class_exists( 'VN_Analytics_Admin' ) ) {
							VN_Analytics_Admin::render_page();
						}
						break;
				}
				?>
			</div>
		</div>
		<?php
	}
}
