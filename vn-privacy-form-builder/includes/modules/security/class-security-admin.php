<?php
/**
 * VN Security Module - Admin Dashboard v3
 * Premium SaaS-style security dashboard with all modules.
 */
if ( ! defined( 'ABSPATH' ) ) exit;

class VN_Security_Admin {

	public function __construct() {
		add_action( 'admin_enqueue_scripts', [ $this, 'enqueue' ] );
	}

	public function enqueue( $hook ) {
		$is_sec = (
			( isset( $_GET['page'] ) && $_GET['page'] === 'vn-security' ) ||
			( isset( $_GET['page'] ) && $_GET['page'] === 'vn-settings' && isset( $_GET['setting_tab'] ) && $_GET['setting_tab'] === 'security' )
		);
		if ( ! $is_sec ) return;
		wp_enqueue_style(  'vn-privacy-admin', VN_PRIVACY_URL . 'assets/admin.css',    [], VN_PRIVACY_VERSION );
		wp_enqueue_script( 'vn-privacy-admin', VN_PRIVACY_URL . 'assets/admin.js', ['jquery'], VN_PRIVACY_VERSION, true );
		wp_localize_script( 'vn-privacy-admin', 'vnSec', [
			'ajaxurl' => admin_url( 'admin-ajax.php' ),
			'nonce'   => wp_create_nonce( 'vn_save_security' ),
		] );
	}

	/* ================================================================
	   Handle Save (POST)
	================================================================ */
	public static function handle_save() {
		if ( empty( $_POST['vn_security_nonce_field'] ) ) return;
		if ( ! wp_verify_nonce( $_POST['vn_security_nonce_field'], 'vn_save_security' ) ) return;
		if ( ! current_user_can( 'manage_options' ) ) return;

		// Special actions
		if ( ! empty( $_POST['clear_login_log'] ) ) VN_Security_Core::clear_login_log();
		if ( ! empty( $_POST['clear_debug_log'] ) ) VN_Security_Core::clear_debug_log();
		if ( ! empty( $_POST['clear_waf_log'] ) )   VN_Security_WAF::clear_waf_logs();

		// Save general + WAF settings
		VN_Security_Core::save_settings( $_POST );
		VN_Security_WAF::save_waf_settings( $_POST );

		$is_settings_page = ( isset( $_GET['page'] ) && $_GET['page'] === 'vn-settings' );
		$page_slug        = $is_settings_page ? 'vn-settings' : 'vn-security';
		$args = [
			'page'  => $page_slug,
			'tab'   => sanitize_text_field( $_POST['active_tab'] ?? 'dashboard' ),
			'saved' => '1',
		];
		if ( $is_settings_page ) $args['setting_tab'] = 'security';
		wp_redirect( add_query_arg( $args, admin_url( 'admin.php' ) ) );
		exit;
	}

	/* ================================================================
	   Render Full Page
	================================================================ */
	public static function render_page() {
		if ( ! current_user_can( 'manage_options' ) ) return;

		$settings     = VN_Security_Core::get_settings();
		$waf_settings = VN_Security_WAF::get_waf_settings();
		$tab          = sanitize_text_field( $_GET['tab'] ?? 'dashboard' );
		$saved        = isset( $_GET['saved'] );

		$is_settings_page = ( isset( $_GET['page'] ) && $_GET['page'] === 'vn-settings' );
		$page_slug        = $is_settings_page ? 'vn-settings' : 'vn-security';
		$setting_tab_arg  = $is_settings_page ? [ 'setting_tab' => 'security' ] : [];

		$tabs = [
			'dashboard'      => [ 'icon' => '📊', 'label' => 'Tổng quan' ],
			'waf'            => [ 'icon' => '🛡️',  'label' => 'Tường Lửa WAF' ],
			'limiter'        => [ 'icon' => '🔒',  'label' => 'Login Guard' ],
			'login_log'      => [ 'icon' => '📋',  'label' => 'Nhật Ký Login' ],
			'file_monitor'   => [ 'icon' => '📁',  'label' => 'Thay Đổi File' ],
			'malware_scanner'=> [ 'icon' => '🔍',  'label' => 'Quét Mã Độc' ],
			'integrity'      => [ 'icon' => '✅',  'label' => 'WP Toàn Vẹn' ],
			'core'           => [ 'icon' => '⚙️',  'label' => 'Bảo Mật Lõi' ],
			'login'          => [ 'icon' => '🔐',  'label' => 'URL Đăng Nhập' ],
			'antispam'       => [ 'icon' => '🚫',  'label' => 'Chống Spam' ],
			'protect'        => [ 'icon' => '🖱️',  'label' => 'Bảo Vệ ND' ],
			'web_log'        => [ 'icon' => '📝',  'label' => 'Debug Log' ],
		];
		?>
		<?php if ( ! $is_settings_page ) : ?>
		<div class="wrap"><div id="vn-privacy-app">
		<div class="vn-page-header">
			<div class="vn-page-header-left">
				<h1>🔒 Security Center</h1>
				<p>Tường lửa WAF · Quét mã độc · Login Guard · 2FA · Kiểm tra toàn vẹn WP</p>
			</div>
			<div class="vn-page-header-right">
				<?php self::render_threat_badge(); ?>
			</div>
		</div>
		<?php endif; ?>

		<?php if ( $saved ) : ?>
		<div class="vn-alert vn-alert-success" style="margin-bottom:20px;">
			<span class="vn-alert-icon">✅</span>
			<div>Đã lưu cài đặt bảo mật thành công!</div>
		</div>
		<?php endif; ?>

		<!-- Security Nav Tabs -->
		<div class="vn-sec-tabs">
			<?php foreach ( $tabs as $key => $t ) :
				$active = $tab === $key;
				$link   = add_query_arg( array_merge( $setting_tab_arg, [ 'page' => $page_slug, 'tab' => $key ] ), admin_url('admin.php') );
			?>
			<a href="<?php echo esc_url( $link ); ?>" 
			   class="vn-sec-tab<?php echo $active ? ' active' : ''; ?>">
				<span class="tab-icon"><?php echo $t['icon']; ?></span>
				<span class="tab-label"><?php echo esc_html( $t['label'] ); ?></span>
				<?php if ( $key === 'waf' && ! empty( $waf_settings['waf_enabled'] ) ) echo '<span class="vn-badge-dot green"></span>'; ?>
			</a>
			<?php endforeach; ?>
		</div>

		<form method="POST" id="vn-sec-form">
		<?php wp_nonce_field( 'vn_save_security', 'vn_security_nonce_field' ); ?>
		<input type="hidden" name="active_tab" value="<?php echo esc_attr( $tab ); ?>">

		<?php
		switch ( $tab ) {
			case 'dashboard':       self::render_tab_dashboard( $settings, $waf_settings ); break;
			case 'waf':             self::render_tab_waf( $waf_settings ); break;
			case 'limiter':         self::render_tab_limiter( $settings ); break;
			case 'login_log':       self::render_tab_login_log(); break;
			case 'file_monitor':    self::render_tab_file_monitor(); break;
			case 'malware_scanner': self::render_tab_malware_scanner(); break;
			case 'integrity':       self::render_tab_integrity(); break;
			case 'core':            self::render_tab_core( $settings ); break;
			case 'login':           self::render_tab_login( $settings ); break;
			case 'antispam':        self::render_tab_antispam( $settings ); break;
			case 'protect':         self::render_tab_protect( $settings ); break;
			case 'web_log':         self::render_tab_web_log(); break;
		}
		?>
		</form>

		<?php if ( ! $is_settings_page ) : ?>
		</div></div>
		<?php endif; ?>
		<?php
	}

	/* ================================================================
	   Helper: Threat Badge in Header
	================================================================ */
	private static function render_threat_badge() {
		$waf_stats = VN_Security_WAF::get_waf_stats();
		$login_stats = VN_Security_Core::get_login_stats();
		$total_threats = $waf_stats['total'] + $login_stats['total_failed'];
		$today = $waf_stats['today'] + $login_stats['today_failed'];
		?>
		<div style="display:flex;gap:12px;flex-wrap:wrap;">
			<div style="background:rgba(255,255,255,.1);border:1px solid rgba(255,255,255,.2);padding:12px 20px;border-radius:12px;text-align:center;min-width:110px;">
				<div style="font-size:22px;font-weight:800;color:#fbbf24;"><?php echo number_format( $today ); ?></div>
				<div style="font-size:11px;color:rgba(255,255,255,.7);margin-top:2px;">Tấn công hôm nay</div>
			</div>
			<div style="background:rgba(255,255,255,.1);border:1px solid rgba(255,255,255,.2);padding:12px 20px;border-radius:12px;text-align:center;min-width:110px;">
				<div style="font-size:22px;font-weight:800;color:#f87171;"><?php echo number_format( $total_threats ); ?></div>
				<div style="font-size:11px;color:rgba(255,255,255,.7);margin-top:2px;">Tổng bị chặn</div>
			</div>
		</div>
		<?php
	}

	/* ================================================================
	   Tab: Dashboard (Overview)
	================================================================ */
	private static function render_tab_dashboard( $settings, $waf_settings ) {
		$login_stats = VN_Security_Core::get_login_stats();
		$waf_stats   = VN_Security_WAF::get_waf_stats();
		$waf_logs    = VN_Security_WAF::get_waf_logs( 10 );
		$login_logs  = VN_Security_Core::get_login_log( 8, 'failed' );

		// Feature status map
		$features = [
			[ 'label' => 'Tường lửa WAF',           'on' => ! empty( $waf_settings['waf_enabled'] ) ],
			[ 'label' => 'Login Attempt Limiter',    'on' => ! empty( $settings['login_limiter_enabled'] ) ],
			[ 'label' => 'Chặn XML-RPC',             'on' => ! empty( $settings['disable_xmlrpc'] ) ],
			[ 'label' => 'Ẩn phiên bản WP',          'on' => ! empty( $settings['hide_wp_version'] ) ],
			[ 'label' => 'Bảo vệ REST API',          'on' => ! empty( $settings['block_rest_api'] ) ],
			[ 'label' => 'Chặn quét tác giả',        'on' => ! empty( $settings['block_author_scan'] ) ],
			[ 'label' => 'Chặn PHP / Uploads',       'on' => ! empty( $settings['block_uploads_php'] ) ],
			[ 'label' => 'Chống Spam bình luận',     'on' => ! empty( $settings['antispam_enabled'] ) ],
		];

		$active_count = count( array_filter( array_column( $features, 'on' ) ) );
		$score = round( ( $active_count / count( $features ) ) * 100 );
		$score_color = $score >= 75 ? '#10b981' : ( $score >= 40 ? '#f59e0b' : '#ef4444' );
		?>
		<!-- Security Score -->
		<div class="vn-sec-dashboard">

			<!-- Score Card -->
			<div class="vn-sec-score-card">
				<div class="vn-score-ring">
					<svg viewBox="0 0 100 100" width="140" height="140">
						<circle cx="50" cy="50" r="42" fill="none" stroke="#e2e8f0" stroke-width="8"/>
						<circle cx="50" cy="50" r="42" fill="none" stroke="<?php echo $score_color; ?>" stroke-width="8"
							stroke-dasharray="<?php echo round($score * 2.638); ?> 263.8"
							stroke-linecap="round"
							transform="rotate(-90 50 50)"/>
					</svg>
					<div class="vn-score-label">
						<div class="vn-score-number" style="color:<?php echo $score_color; ?>"><?php echo $score; ?></div>
						<div class="vn-score-text">/ 100</div>
					</div>
				</div>
				<div class="vn-score-desc">
					<h3>Điểm bảo mật</h3>
					<p><?php echo $active_count; ?>/<?php echo count( $features ); ?> tính năng đang bật</p>
					<?php if ( $score < 50 ) : ?>
					<div class="vn-score-warn">⚠️ Website đang ở mức rủi ro cao!</div>
					<?php elseif ( $score < 75 ) : ?>
					<div class="vn-score-ok">🟡 Cần bật thêm tính năng bảo mật</div>
					<?php else : ?>
					<div class="vn-score-good">✅ Website được bảo vệ tốt</div>
					<?php endif; ?>
				</div>
			</div>

			<!-- Stats Row -->
			<div class="vn-sec-stats-grid">
				<?php
				$stats = [
					[ 'icon' => '🛡️', 'val' => number_format($waf_stats['total']),       'label' => 'WAF đã chặn',          'color' => '#6366f1' ],
					[ 'icon' => '⛔', 'val' => number_format($login_stats['total_failed']),'label' => 'Login thất bại',       'color' => '#ef4444' ],
					[ 'icon' => '🤖', 'val' => number_format($waf_stats['bots']),         'label' => 'Bot bị chặn',          'color' => '#f59e0b' ],
					[ 'icon' => '💉', 'val' => number_format($waf_stats['sqli']),         'label' => 'SQLi bị chặn',         'color' => '#8b5cf6' ],
					[ 'icon' => '🔥', 'val' => number_format($waf_stats['xss']),          'label' => 'XSS bị chặn',          'color' => '#ec4899' ],
					[ 'icon' => '🔒', 'val' => number_format($login_stats['blocked_ips']), 'label' => 'IP đáng ngờ (24h)',   'color' => '#0ea5e9' ],
				];
				foreach ( $stats as $s ) : ?>
				<div class="vn-stat-card">
					<div class="vn-stat-icon" style="background:<?php echo $s['color']; ?>20;color:<?php echo $s['color']; ?>"><?php echo $s['icon']; ?></div>
					<div class="vn-stat-val" style="color:<?php echo $s['color']; ?>"><?php echo $s['val']; ?></div>
					<div class="vn-stat-label"><?php echo $s['label']; ?></div>
				</div>
				<?php endforeach; ?>
			</div>

			<!-- Feature Status -->
			<div class="vn-sec-features">
				<h3>Trạng thái tính năng</h3>
				<div class="vn-feature-grid">
					<?php foreach ( $features as $f ) : ?>
					<div class="vn-feature-item<?php echo $f['on'] ? ' on' : ' off'; ?>">
						<span class="vn-feature-dot"></span>
						<span><?php echo esc_html( $f['label'] ); ?></span>
					</div>
					<?php endforeach; ?>
				</div>
			</div>

			<!-- Recent Threats -->
			<div class="vn-sec-recent-threats">
				<h3>⚡ Mối đe dọa gần đây (WAF)</h3>
				<?php if ( empty( $waf_logs ) ) : ?>
				<div class="vn-empty-state">🛡️ Chưa phát hiện mối đe dọa nào được ghi nhận.</div>
				<?php else : ?>
				<div class="vn-threat-list">
					<?php foreach ( $waf_logs as $log ) :
						$type_colors = [
							'SQLi'    => ['#fef2f2','#dc2626','💉'],
							'XSS'     => ['#fff7ed','#ea580c','🔥'],
							'LFI/RFI' => ['#f5f3ff','#7c3aed','📁'],
							'BadBot'  => ['#fefce8','#ca8a04','🤖'],
						];
						[$bg,$col,$ic] = $type_colors[$log->type] ?? ['#f8fafc','#64748b','⚠️'];
					?>
					<div class="vn-threat-row">
						<span class="vn-threat-badge" style="background:<?php echo $bg; ?>;color:<?php echo $col; ?>"><?php echo $ic . ' ' . esc_html($log->type); ?></span>
						<span class="vn-threat-ip"><?php echo esc_html($log->ip); ?></span>
						<span class="vn-threat-uri" title="<?php echo esc_attr($log->uri); ?>"><?php echo esc_html(substr($log->uri,0,50)); ?></span>
						<span class="vn-threat-time"><?php echo esc_html(human_time_diff(strtotime($log->blocked_at),time()).' trước'); ?></span>
					</div>
					<?php endforeach; ?>
				</div>
				<?php endif; ?>
			</div>

			<!-- Recent Failed Logins -->
			<div class="vn-sec-recent-logins">
				<h3>🚨 Đăng nhập thất bại gần đây</h3>
				<?php if ( empty( $login_logs ) ) : ?>
				<div class="vn-empty-state">✅ Không có đăng nhập thất bại.</div>
				<?php else : ?>
				<div class="vn-threat-list">
					<?php foreach ( $login_logs as $log ) : ?>
					<div class="vn-threat-row">
						<span class="vn-threat-badge" style="background:#fef2f2;color:#dc2626">⛔ Login</span>
						<span class="vn-threat-ip"><?php echo esc_html($log->ip); ?></span>
						<span class="vn-threat-uri"><?php echo esc_html($log->username); ?></span>
						<span class="vn-threat-time"><?php echo esc_html(human_time_diff(strtotime($log->logged_at),time()).' trước'); ?></span>
					</div>
					<?php endforeach; ?>
				</div>
				<?php endif; ?>
			</div>

		</div><!-- .vn-sec-dashboard -->
		<?php
	}

	/* ================================================================
	   Tab: WAF Settings + Logs
	================================================================ */
	private static function render_tab_waf( $waf ) {
		$stats = VN_Security_WAF::get_waf_stats();
		$logs  = VN_Security_WAF::get_waf_logs( 100 );
		?>
		<div class="vn-sec-two-col">
			<!-- Settings -->
			<div class="vn-card">
				<h3 class="vn-card-title">🛡️ Cấu hình Tường Lửa (WAF)</h3>

				<label class="vn-toggle-row">
					<input type="checkbox" name="waf_enabled" value="1" <?php checked($waf['waf_enabled'],1); ?>>
					<div class="vn-toggle-info">
						<strong>Bật Tường Lửa WAF</strong>
						<span>Tự động chặn các tấn công nguy hiểm theo thời gian thực</span>
					</div>
					<div class="vn-switch"></div>
				</label>

				<div class="vn-divider"></div>

				<p class="vn-section-label">🔍 Loại tấn công cần chặn:</p>

				<?php
				$rules = [
					'waf_block_sqli' => [ '💉 SQL Injection (SQLi)', 'Chặn các truy vấn SQL độc hại trong URL/form' ],
					'waf_block_xss'  => [ '🔥 Cross-Site Scripting (XSS)', 'Chặn script độc hại nhúng vào trang web' ],
					'waf_block_lfi'  => [ '📁 LFI/RFI / Path Traversal', 'Ngăn đọc file nhạy cảm của server' ],
					'waf_block_bots' => [ '🤖 Scanner & Bot độc hại', 'Chặn tool quét lỗ hổng: sqlmap, nikto, ...' ],
				];
				foreach ( $rules as $k => [$lbl, $desc] ) : ?>
				<label class="vn-toggle-row small">
					<input type="checkbox" name="<?php echo $k; ?>" value="1" <?php checked($waf[$k],1); ?>>
					<div class="vn-toggle-info">
						<strong><?php echo $lbl; ?></strong>
						<span><?php echo $desc; ?></span>
					</div>
				</label>
				<?php endforeach; ?>

				<div class="vn-divider"></div>

				<label class="vn-toggle-row small">
					<input type="checkbox" name="waf_log_enabled" value="1" <?php checked($waf['waf_log_enabled'],1); ?>>
					<div class="vn-toggle-info">
						<strong>📋 Ghi nhật ký tấn công</strong>
						<span>Lưu lại tất cả các yêu cầu bị chặn vào cơ sở dữ liệu</span>
					</div>
				</label>

				<div style="margin-top:16px;">
					<label class="vn-label">IP được phép bỏ qua WAF (Whitelist - mỗi IP 1 dòng)</label>
					<textarea name="waf_whitelist_ips" rows="3" class="vn-textarea"
						placeholder="Ví dụ: 192.168.1.1 hoặc 10.0.0.*"><?php echo esc_textarea($waf['waf_whitelist_ips']); ?></textarea>
				</div>

				<div style="display:flex;gap:10px;margin-top:20px;">
					<button type="submit" class="vn-btn vn-btn-primary">💾 Lưu cấu hình WAF</button>
					<?php if ( ! empty( $logs ) ) : ?>
					<button type="submit" name="clear_waf_log" value="1" class="vn-btn vn-btn-danger"
						onclick="return confirm('Xóa toàn bộ nhật ký WAF?')">🗑️ Xóa Log</button>
					<?php endif; ?>
				</div>
			</div>

			<!-- Stats Panel -->
			<div class="vn-card">
				<h3 class="vn-card-title">📊 Thống kê WAF</h3>
				<?php
				$items = [
					['🛡️ Tổng đã chặn',     $stats['total'], '#6366f1'],
					['📅 Hôm nay',           $stats['today'], '#3b82f6'],
					['💉 SQLi',              $stats['sqli'],  '#ef4444'],
					['🔥 XSS',               $stats['xss'],   '#f97316'],
					['🤖 Bot',               $stats['bots'],  '#f59e0b'],
				];
				foreach ( $items as [$lbl,$val,$c] ) : ?>
				<div class="vn-stat-row">
					<span><?php echo $lbl; ?></span>
					<span class="vn-stat-pill" style="background:<?php echo $c; ?>20;color:<?php echo $c; ?>"><?php echo number_format($val); ?></span>
				</div>
				<?php endforeach; ?>

				<div class="vn-info-box" style="margin-top:16px;">
					💡 WAF chặn ở tầng <strong>application layer</strong> trước khi WordPress xử lý request.
					Hiệu quả nhất chống các cuộc tấn công bot tự động.
				</div>
			</div>
		</div>

		<!-- WAF Log Table -->
		<?php if ( ! empty( $logs ) ) : ?>
		<div class="vn-card" style="margin-top:24px;">
			<h3 class="vn-card-title">📋 Nhật ký tấn công bị chặn (<?php echo count($logs); ?> gần nhất)</h3>
			<div class="vn-table-wrap">
				<table class="vn-table">
					<thead>
						<tr>
							<th>Thời gian</th>
							<th>IP</th>
							<th>Loại</th>
							<th>URI</th>
							<th>Payload</th>
						</tr>
					</thead>
					<tbody>
					<?php foreach ( $logs as $i => $log ) :
						$type_map = [
							'SQLi'    => 'vn-badge-red',
							'XSS'     => 'vn-badge-orange',
							'LFI/RFI' => 'vn-badge-purple',
							'BadBot'  => 'vn-badge-yellow',
						];
						$badge_cls = $type_map[$log->type] ?? 'vn-badge-gray';
					?>
					<tr class="<?php echo $i % 2 ? 'alt' : ''; ?>">
						<td class="vn-muted"><?php echo esc_html($log->blocked_at); ?></td>
						<td><code><?php echo esc_html($log->ip); ?></code></td>
						<td><span class="vn-badge <?php echo $badge_cls; ?>"><?php echo esc_html($log->type); ?></span></td>
						<td class="vn-mono small"><?php echo esc_html(substr($log->uri,0,60)); ?></td>
						<td class="vn-mono small" title="<?php echo esc_attr($log->payload); ?>"><?php echo esc_html(substr($log->payload,0,80)); ?></td>
					</tr>
					<?php endforeach; ?>
					</tbody>
				</table>
			</div>
		</div>
		<?php endif; ?>
		<?php
	}

	/* ================================================================
	   Tab: Login Limiter
	================================================================ */
	private static function render_tab_limiter( $settings ) {
		$stats = VN_Security_Core::get_login_stats();
		?>
		<div class="vn-sec-two-col">
			<div class="vn-card">
				<h3 class="vn-card-title">🛡️ Giới hạn đăng nhập sai</h3>

				<label class="vn-toggle-row">
					<input type="checkbox" name="login_limiter_enabled" value="1" <?php checked($settings['login_limiter_enabled'],1); ?>>
					<div class="vn-toggle-info">
						<strong>Bật Login Attempt Limiter</strong>
						<span>Tự động khóa IP khi đăng nhập sai nhiều lần</span>
					</div>
					<div class="vn-switch"></div>
				</label>

				<div class="vn-field-row">
					<div>
						<label class="vn-label">Số lần sai tối đa</label>
						<input type="number" name="max_attempts" value="<?php echo esc_attr($settings['max_attempts']); ?>" min="1" max="100" class="vn-input">
					</div>
					<div>
						<label class="vn-label">Thời gian khóa (phút)</label>
						<input type="number" name="lockout_minutes" value="<?php echo esc_attr($settings['lockout_minutes']); ?>" min="1" max="1440" class="vn-input">
					</div>
				</div>

				<label class="vn-toggle-row small">
					<input type="checkbox" name="log_logins" value="1" <?php checked($settings['log_logins'],1); ?>>
					<div class="vn-toggle-info">
						<strong>Ghi nhật ký đăng nhập</strong>
						<span>Lưu lại lịch sử đăng nhập thành công và thất bại</span>
					</div>
				</label>

				<button type="submit" class="vn-btn vn-btn-primary" style="margin-top:20px;">💾 Lưu cài đặt</button>
			</div>

			<div class="vn-card">
				<h3 class="vn-card-title">📊 Thống kê bảo mật</h3>
				<?php
				$items = [
					['⛔ Tổng đăng nhập sai',      $stats['total_failed'],  '#ef4444'],
					['✅ Tổng đăng nhập thành công', $stats['total_success'], '#22c55e'],
					['🚨 Đăng nhập sai hôm nay',    $stats['today_failed'],  '#f59e0b'],
					['🔒 IP đáng ngờ (24h)',          $stats['blocked_ips'],   '#7c3aed'],
				];
				foreach ( $items as [$lbl,$val,$c] ) : ?>
				<div class="vn-stat-row">
					<span><?php echo $lbl; ?></span>
					<span class="vn-stat-pill" style="background:<?php echo $c; ?>20;color:<?php echo $c; ?>"><?php echo number_format($val); ?></span>
				</div>
				<?php endforeach; ?>
				<div class="vn-info-box" style="margin-top:16px;">
					💡 IP bị khóa sẽ tự mở sau <strong><?php echo $settings['lockout_minutes']; ?> phút</strong>.
				</div>
			</div>
		</div>
		<?php
	}

	/* ================================================================
	   Tab: Login Log
	================================================================ */
	private static function render_tab_login_log() {
		$filter = sanitize_text_field( $_GET['filter'] ?? '' );
		$logs   = VN_Security_Core::get_login_log( 100, $filter );
		$stats  = VN_Security_Core::get_login_stats();

		$is_settings_page = ( isset( $_GET['page'] ) && $_GET['page'] === 'vn-settings' );
		$page_slug        = $is_settings_page ? 'vn-settings' : 'vn-security';
		$setting_tab_arg  = $is_settings_page ? [ 'setting_tab' => 'security' ] : [];
		?>
		<div class="vn-card">
			<div class="vn-card-header-row">
				<h3 class="vn-card-title" style="margin:0;">📋 Nhật ký đăng nhập (100 gần nhất)</h3>
				<div style="display:flex;gap:8px;flex-wrap:wrap;">
					<a href="<?php echo esc_url(add_query_arg(array_merge($setting_tab_arg,['page'=>$page_slug,'tab'=>'login_log','filter'=>'failed']),admin_url('admin.php'))); ?>"
					   class="vn-btn vn-btn-sm vn-btn-danger-soft">⛔ Thất bại (<?php echo $stats['total_failed']; ?>)</a>
					<a href="<?php echo esc_url(add_query_arg(array_merge($setting_tab_arg,['page'=>$page_slug,'tab'=>'login_log']),admin_url('admin.php'))); ?>"
					   class="vn-btn vn-btn-sm">📋 Tất cả</a>
					<?php if ( ! empty($logs) ) : ?>
					<button type="submit" name="clear_login_log" value="1" class="vn-btn vn-btn-sm vn-btn-danger"
						onclick="return confirm('Xóa toàn bộ nhật ký đăng nhập?')">🗑️ Xóa log</button>
					<?php endif; ?>
				</div>
			</div>

			<?php if ( empty($logs) ) : ?>
			<div class="vn-empty-state">📭 Chưa có dữ liệu đăng nhập.</div>
			<?php else : ?>
			<div class="vn-table-wrap">
				<table class="vn-table">
					<thead><tr>
						<th>Thời gian</th>
						<th>Tên đăng nhập</th>
						<th>Địa chỉ IP</th>
						<th style="text-align:center;">Trạng thái</th>
					</tr></thead>
					<tbody>
					<?php foreach ( $logs as $i => $row ) :
						$fail = $row->status === 'failed';
					?>
					<tr class="<?php echo $i % 2 ? 'alt' : ''; ?>">
						<td class="vn-muted"><?php echo esc_html($row->logged_at); ?></td>
						<td><strong><?php echo esc_html($row->username); ?></strong></td>
						<td><code><?php echo esc_html($row->ip); ?></code></td>
						<td style="text-align:center;">
							<span class="vn-badge <?php echo $fail ? 'vn-badge-red' : 'vn-badge-green'; ?>">
								<?php echo $fail ? '⛔ Thất bại' : '✅ Thành công'; ?>
							</span>
						</td>
					</tr>
					<?php endforeach; ?>
					</tbody>
				</table>
			</div>
			<?php endif; ?>
		</div>
		<?php
	}

	/* ================================================================
	   Tab: File Monitor
	================================================================ */
	private static function render_tab_file_monitor() {
		$files = VN_Security_Core::get_recently_modified_files( 100 );
		?>
		<div class="vn-card">
			<h3 class="vn-card-title">📁 Báo cáo thay đổi tệp tin (Realtime)</h3>
			<p class="vn-card-desc">Quét và theo dõi các tệp mã nguồn (PHP, JS, CSS, .htaccess) được chỉnh sửa gần đây trong <code>wp-content</code>.</p>

			<?php if ( empty($files) ) : ?>
			<div class="vn-empty-state">🍃 Không phát hiện thay đổi tệp tin gần đây.</div>
			<?php else : ?>
			<div class="vn-table-wrap">
				<table class="vn-table">
					<thead><tr>
						<th>Tên / Đường dẫn tệp tin</th>
						<th>Kích thước</th>
						<th>Thời gian sửa đổi</th>
						<th style="text-align:center;">Trạng thái</th>
					</tr></thead>
					<tbody>
					<?php foreach ( $files as $i => $f ) :
						$age = time() - $f['mtime'];
						$recent = $age < 86400;
					?>
					<tr class="<?php echo $i % 2 ? 'alt' : ''; ?>">
						<td class="vn-mono" style="word-break:break-all;font-weight:600"><?php echo esc_html($f['path']); ?></td>
						<td class="vn-muted"><?php echo size_format($f['size']); ?></td>
						<td class="vn-muted">
							<?php echo date_i18n('d/m/Y H:i:s', $f['mtime']); ?><br>
							<span style="font-size:11px;"><?php echo human_time_diff($f['mtime'],time()); ?> trước</span>
						</td>
						<td style="text-align:center;">
							<span class="vn-badge <?php echo $recent ? 'vn-badge-yellow' : 'vn-badge-green'; ?>">
								<?php echo $recent ? '⚠️ Vừa sửa' : '🟢 Ổn định'; ?>
							</span>
						</td>
					</tr>
					<?php endforeach; ?>
					</tbody>
				</table>
			</div>
			<?php endif; ?>
		</div>
		<?php
	}

	/* ================================================================
	   Tab: Malware Scanner
	================================================================ */
	private static function render_tab_malware_scanner() { ?>
		<div class="vn-card">
			<h3 class="vn-card-title">🔍 Quét mã độc chủ động (Malware Scanner)</h3>
			<p class="vn-card-desc">Quét toàn diện plugins/themes để phát hiện mã độc, backdoor, eval base64 và các lệnh hệ thống nguy hiểm.</p>

			<div style="margin-bottom:20px;">
				<button type="button" id="btn-start-malware-scan" class="vn-btn vn-btn-primary vn-btn-lg"
					data-nonce="<?php echo wp_create_nonce('vn_save_security'); ?>">
					🔍 Bắt đầu Quét Hệ Thống
				</button>
			</div>

			<div id="malware-scan-progress-wrap" style="display:none;">
				<div class="vn-progress-bar"><div class="vn-progress-fill" id="malware-progress-fill"></div></div>
				<div id="malware-scan-status-text" class="vn-scan-status">⏳ Đang quét... Có thể mất 1-3 phút.</div>
			</div>

			<div id="malware-scan-results"></div>
		</div>
		<?php
	}

	/* ================================================================
	   Tab: WP Core Integrity
	================================================================ */
	private static function render_tab_integrity() {
		global $wp_version;
		?>
		<div class="vn-card">
			<h3 class="vn-card-title">✅ Kiểm tra toàn vẹn WordPress Core</h3>
			<p class="vn-card-desc">
				So sánh checksum (MD5) của các file <code>wp-admin/</code>, <code>wp-includes/</code> và các file gốc
				với dữ liệu chính thức từ <strong>WordPress.org API</strong>.
				Phát hiện file bị chỉnh sửa, file bị xóa, hoặc file lạ được thêm vào.
			</p>

			<div style="display:flex;align-items:center;gap:16px;margin-bottom:20px;flex-wrap:wrap;">
				<button type="button" id="btn-integrity-scan" class="vn-btn vn-btn-primary vn-btn-lg"
					data-nonce="<?php echo wp_create_nonce('vn_save_security'); ?>">
					🔬 Bắt đầu Quét Toàn Vẹn
				</button>
				<div style="padding:10px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;font-size:13px;">
					Phiên bản WP hiện tại: <strong>v<?php echo esc_html($wp_version); ?></strong>
				</div>
			</div>

			<div id="integrity-progress-wrap" style="display:none;margin-bottom:16px;">
				<div class="vn-progress-bar"><div class="vn-progress-fill vn-progress-animate"></div></div>
				<div class="vn-scan-status">⏳ Đang tải checksums từ WordPress.org và quét files...</div>
			</div>

			<div id="integrity-results"></div>
		</div>

		<script>
		(function($){
			$('#btn-integrity-scan').on('click', function(){
				var btn = $(this);
				btn.prop('disabled', true).text('⏳ Đang quét...');
				$('#integrity-progress-wrap').show();
				$('#integrity-results').html('');

				$.post(vnSec.ajaxurl, {
					action: 'vn_integrity_scan',
					nonce: $(this).data('nonce')
				}, function(r){
					btn.prop('disabled', false).text('🔬 Bắt đầu Quét Toàn Vẹn');
					$('#integrity-progress-wrap').hide();

					if( !r.success ){
						$('#integrity-results').html('<div class="vn-alert vn-alert-error">❌ ' + (r.data||'Lỗi không xác định') + '</div>');
						return;
					}
					var d = r.data;
					if( d.status === 'error' ){
						$('#integrity-results').html('<div class="vn-alert vn-alert-error">❌ ' + d.message + '</div>');
						return;
					}

					var html = '';
					if( d.clean ){
						html = '<div class="vn-alert vn-alert-success">✅ Tuyệt vời! Tất cả <strong>' + d.scanned + '</strong> file WordPress Core đều toàn vẹn. Không phát hiện chỉnh sửa nào.</div>';
					} else {
						if( d.modified.length ){
							html += '<div class="vn-card" style="margin-top:0;border-color:#fecaca;"><h4 style="color:#dc2626;margin-top:0;">⚠️ File bị chỉnh sửa ('+d.modified.length+')</h4>';
							html += '<div class="vn-table-wrap"><table class="vn-table"><thead><tr><th>File</th><th>Sửa lần cuối</th><th>Kích thước</th></tr></thead><tbody>';
							d.modified.forEach(function(f){
								html += '<tr><td class="vn-mono">'+f.file+'</td><td class="vn-muted">'+new Date(f.mtime*1000).toLocaleString('vi-VN')+'</td><td>'+f.size+' bytes</td></tr>';
							});
							html += '</tbody></table></div></div>';
						}
						if( d.added.length ){
							html += '<div class="vn-card" style="margin-top:16px;border-color:#fde68a;"><h4 style="color:#d97706;margin-top:0;">🚨 File lạ được thêm vào ('+d.added.length+') — Cảnh báo backdoor!</h4>';
							html += '<div class="vn-table-wrap"><table class="vn-table"><thead><tr><th>File</th><th>Ngày tạo</th><th>Kích thước</th></tr></thead><tbody>';
							d.added.forEach(function(f){
								html += '<tr><td class="vn-mono" style="color:#dc2626;font-weight:700;">⛔ '+f.file+'</td><td class="vn-muted">'+new Date(f.mtime*1000).toLocaleString('vi-VN')+'</td><td>'+f.size+' bytes</td></tr>';
							});
							html += '</tbody></table></div></div>';
						}
						if( d.missing.length ){
							html += '<div class="vn-card" style="margin-top:16px;"><h4 style="color:#64748b;margin-top:0;">📋 File bị thiếu ('+d.missing.length+')</h4><ul style="font-family:monospace;font-size:12px;column-count:2;">';
							d.missing.forEach(function(f){ html += '<li>'+f+'</li>'; });
							html += '</ul></div>';
						}
					}
					$('#integrity-results').html(html);
				}).fail(function(){
					btn.prop('disabled', false).text('🔬 Bắt đầu Quét Toàn Vẹn');
					$('#integrity-progress-wrap').hide();
					$('#integrity-results').html('<div class="vn-alert vn-alert-error">❌ Kết nối lỗi. Vui lòng thử lại.</div>');
				});
			});
		})(jQuery);
		</script>
		<?php
	}

	/* ================================================================
	   Tab: Core Security
	================================================================ */
	private static function render_tab_core( $settings ) {
		$uploads = wp_upload_dir();
		?>
		<div class="vn-sec-two-col">
			<div class="vn-card">
				<h3 class="vn-card-title">⚙️ Tùy chọn bảo mật lõi</h3>
				<div style="display:flex;flex-direction:column;gap:12px;">
				<?php
				$opts = [
					'disable_xmlrpc'     => ['Chặn truy cập XML-RPC', 'Ngăn tấn công Brute-force qua xmlrpc.php'],
					'hide_wp_version'    => ['Ẩn phiên bản WordPress', 'Xóa ?ver= và thẻ generator'],
					'block_uploads_php'  => ['Chặn PHP trong thư mục Uploads', 'Tạo .htaccess chặn webshell tải lên'],
					'block_rest_api'     => ['Bảo vệ REST API', 'Chặn khách ẩn danh xem /wp-json/wp/v2/users'],
					'block_author_scan'  => ['Chặn quét tác giả (?author=N)', 'Ngăn bot dò tên tài khoản admin'],
				];
				foreach ( $opts as $k => [$lbl,$desc] ) : ?>
				<label class="vn-toggle-row">
					<input type="checkbox" name="<?php echo $k; ?>" value="1" <?php checked($settings[$k]??0,1); ?>>
					<div class="vn-toggle-info">
						<strong><?php echo $lbl; ?></strong>
						<span><?php echo $desc; ?></span>
					</div>
				</label>
				<?php endforeach; ?>
				</div>
				<button type="submit" class="vn-btn vn-btn-primary" style="margin-top:20px;">💾 Lưu cấu hình</button>
			</div>

			<div class="vn-card">
				<h3 class="vn-card-title">🛡️ Quản lý truy cập IP</h3>
				<div style="margin-bottom:16px;">
					<label class="vn-label">Danh sách Đen (Blacklist) – Mỗi IP 1 dòng</label>
					<textarea name="blacklist_ips" rows="4" class="vn-textarea" placeholder="192.168.1.100&#10;10.0.0.*"><?php echo esc_textarea($settings['blacklist_ips']??''); ?></textarea>
					<p class="vn-hint">Hỗ trợ ký tự đại diện *. IP này sẽ bị chặn ngay lập tức (403).</p>
				</div>
				<div style="margin-bottom:16px;">
					<label class="vn-label">Danh sách Trắng (Whitelist) – IP của bạn: <code><?php echo esc_html(VN_Security_Core::get_client_ip()); ?></code></label>
					<textarea name="whitelist_ips" rows="4" class="vn-textarea" placeholder="IP của bạn..."><?php echo esc_textarea($settings['whitelist_ips']??''); ?></textarea>
					<p class="vn-hint">IP whitelist sẽ bỏ qua mọi giới hạn khóa.</p>
				</div>
				<div class="vn-info-box">💡 Nếu dùng WAMP/XAMPP, IP thường là <code>::1</code> hoặc <code>127.0.0.1</code>.</div>
			</div>
		</div>
		<?php
	}

	/* ================================================================
	   Tab: Custom Login URL
	================================================================ */
	private static function render_tab_login( $settings ) {
		$login_url = VN_Security_Core::get_current_login_url();
		?>
		<div style="max-width:700px;">
			<div class="vn-card">
				<h3 class="vn-card-title">🔐 Đổi đường dẫn đăng nhập</h3>
				<div class="vn-alert vn-alert-warning" style="margin-bottom:18px;">
					<strong>⚠️ Đọc trước khi lưu!</strong>
					<ul style="margin:8px 0 0;padding-left:18px;font-size:13px;line-height:1.9;">
						<li>URL đăng nhập cũ <code>/wp-admin</code> sẽ bị chặn sau khi lưu</li>
						<li><strong>Lưu URL mới lại ngay bây giờ</strong> trước khi bấm Lưu</li>
						<li>Xóa trống ô bên dưới và lưu để khôi phục về mặc định</li>
					</ul>
				</div>
				<div style="margin-bottom:14px;">
					<label class="vn-label">URL hiện tại:</label>
					<code class="vn-code-block"><?php echo esc_html($login_url); ?></code>
				</div>
				<div style="margin-bottom:16px;">
					<label class="vn-label">Slug mới</label>
					<div style="display:flex;align-items:center;gap:8px;flex-wrap:wrap;">
						<span class="vn-muted" style="font-size:13px;"><?php echo esc_html(home_url('/')); ?></span>
						<input type="text" name="custom_login_slug" value="<?php echo esc_attr($settings['custom_login_slug']); ?>"
							placeholder="my-secret-login" id="login-slug-input" class="vn-input" style="width:200px;"
							oninput="document.getElementById('login-preview').textContent='<?php echo esc_js(home_url('/')); ?>'+this.value+'/'">
						<span class="vn-muted">/</span>
					</div>
					<div style="margin-top:6px;font-size:13px;color:#64748b;">
						Mới: <strong id="login-preview" style="color:#dc2626;"><?php echo esc_html($login_url); ?></strong>
					</div>
				</div>
				<button type="submit" class="vn-btn vn-btn-primary">💾 Lưu (đã lưu URL mới chưa?)</button>
			</div>
		</div>
		<?php
	}

	/* ================================================================
	   Tab: Anti-Spam
	================================================================ */
	private static function render_tab_antispam( $settings ) { ?>
		<div style="max-width:700px;">
			<div class="vn-card">
				<h3 class="vn-card-title">🚫 Chặn bình luận Spam</h3>
				<label class="vn-toggle-row" style="margin-bottom:16px;">
					<input type="checkbox" name="antispam_enabled" value="1" <?php checked($settings['antispam_enabled'],1); ?>>
					<div class="vn-toggle-info">
						<strong>Bật chống Spam</strong>
						<span>Honeypot field + kiểm tra từ khóa blacklist trong bình luận</span>
					</div>
					<div class="vn-switch"></div>
				</label>
				<div>
					<label class="vn-label">Từ khóa Blacklist (mỗi từ 1 dòng)</label>
					<textarea name="spam_keywords" rows="8" class="vn-textarea"><?php echo esc_textarea($settings['spam_keywords']); ?></textarea>
					<p class="vn-hint">Bình luận chứa từ này sẽ tự động bị đánh dấu là spam.</p>
				</div>
				<button type="submit" class="vn-btn vn-btn-primary" style="margin-top:16px;">💾 Lưu</button>
			</div>
		</div>
		<?php
	}

	/* ================================================================
	   Tab: Content Protect
	================================================================ */
	private static function render_tab_protect( $settings ) { ?>
		<div style="max-width:700px;">
			<div class="vn-card">
				<h3 class="vn-card-title">🖱️ Bảo vệ nội dung</h3>
				<div class="vn-info-box" style="margin-bottom:16px;">
					💡 Không thể bảo vệ 100% — người dùng kỹ thuật vẫn có thể xem được. Dùng để ngăn người dùng phổ thông.
				</div>
				<?php foreach ([
					'disable_right_click' => ['🖱️ Chặn chuột phải', 'Vô hiệu hóa menu chuột phải'],
					'disable_text_select' => ['📋 Chặn chọn văn bản', 'Không cho bôi đen & copy'],
					'disable_view_source' => ['💻 Chặn Ctrl+U / F12', 'Ngăn xem source & DevTools'],
				] as $name=>[$label,$desc]) : ?>
				<label class="vn-toggle-row" style="margin-bottom:10px;">
					<input type="checkbox" name="<?php echo $name; ?>" value="1" <?php checked($settings[$name]??0,1); ?>>
					<div class="vn-toggle-info">
						<strong><?php echo $label; ?></strong>
						<span><?php echo $desc; ?></span>
					</div>
				</label>
				<?php endforeach; ?>
				<button type="submit" class="vn-btn vn-btn-primary" style="margin-top:6px;">💾 Lưu</button>
			</div>
		</div>
		<?php
	}

	/* ================================================================
	   Tab: Web Log (debug.log)
	================================================================ */
	private static function render_tab_web_log() {
		$log_file   = WP_CONTENT_DIR . '/debug.log';
		$is_enabled = defined('WP_DEBUG') && WP_DEBUG && defined('WP_DEBUG_LOG') && WP_DEBUG_LOG;
		$log_exists = file_exists( $log_file );
		$entries    = [];
		if ( $log_exists ) {
			$lines   = self::read_last_lines( $log_file, 500 );
			$entries = self::parse_debug_logs( $lines );
		}
		?>
		<div class="vn-card">
			<div class="vn-card-header-row">
				<h3 class="vn-card-title" style="margin:0;">📝 Nhật ký lỗi Website (PHP debug.log)</h3>
				<?php if ( $log_exists && ! empty($entries) ) : ?>
				<button type="submit" name="clear_debug_log" value="1" class="vn-btn vn-btn-sm vn-btn-danger"
					onclick="return confirm('Xóa file debug.log?')">🗑️ Xóa debug.log</button>
				<?php endif; ?>
			</div>

			<?php if ( ! $is_enabled ) : ?>
			<div class="vn-alert vn-alert-warning" style="margin:16px 0;">
				💡 <strong>Debug Log đang TẮT.</strong> Thêm vào <code>wp-config.php</code>:
				<pre class="vn-code-block" style="margin-top:8px;">define( 'WP_DEBUG', true );
define( 'WP_DEBUG_LOG', true );
define( 'WP_DEBUG_DISPLAY', false );</pre>
			</div>
			<?php endif; ?>

			<?php if ( empty($entries) ) : ?>
			<div class="vn-empty-state">✨ Không phát hiện lỗi PHP nào.</div>
			<?php else : ?>
			<div class="vn-table-wrap">
				<table class="vn-table">
					<thead><tr>
						<th style="width:150px;">Thời gian</th>
						<th style="width:120px;text-align:center;">Loại</th>
						<th>Thông báo lỗi</th>
						<th style="width:250px;">Nơi xảy ra</th>
					</tr></thead>
					<tbody>
					<?php foreach ( $entries as $i => $e ) :
						$type = strtolower($e['type']);
						if ( strpos($type,'fatal') !== false || strpos($type,'error') !== false ) {
							$badge_cls = 'vn-badge-red'; $label = '🔴 ERROR';
						} elseif ( strpos($type,'warning') !== false ) {
							$badge_cls = 'vn-badge-orange'; $label = '🟠 WARNING';
						} elseif ( strpos($type,'deprecated') !== false ) {
							$badge_cls = 'vn-badge-gray'; $label = '⚫ DEPRECATED';
						} else {
							$badge_cls = 'vn-badge-blue'; $label = '🔵 ' . strtoupper($e['type']);
						}
					?>
					<tr class="<?php echo $i % 2 ? 'alt' : ''; ?>" style="vertical-align:top;">
						<td class="vn-muted"><?php echo esc_html($e['time']); ?></td>
						<td style="text-align:center;"><span class="vn-badge <?php echo $badge_cls; ?>"><?php echo $label; ?></span></td>
						<td style="word-break:break-all;font-weight:500;"><?php echo esc_html($e['message']); ?></td>
						<td class="vn-mono small" style="word-break:break-all;"><?php echo esc_html($e['file']); ?></td>
					</tr>
					<?php endforeach; ?>
					</tbody>
				</table>
			</div>
			<?php endif; ?>
		</div>
		<?php
	}

	/* ================================================================
	   Helpers: Read & Parse debug.log
	================================================================ */
	private static function read_last_lines( $filepath, $num = 500 ) {
		$file = @fopen( $filepath, 'r' );
		if ( ! $file ) return [];
		$pos = -2; $lines = []; $current = '';
		while ( @fseek( $file, $pos, SEEK_END ) !== -1 ) {
			$char = fgetc( $file );
			if ( $char === "\n" ) {
				$lines[] = strrev( $current );
				$current = '';
				if ( count($lines) >= $num ) break;
			} else {
				$current .= $char;
			}
			$pos--;
		}
		if ( count($lines) < $num && $current ) $lines[] = strrev($current);
		@fclose($file);
		return array_reverse($lines);
	}

	private static function parse_debug_logs( $lines ) {
		$entries = [];
		foreach ( $lines as $line ) {
			$line = trim($line);
			if ( empty($line) ) continue;
			if ( preg_match('#^\[([^\]]+)\]\s+PHP\s+([^:]+):\s+(.+)\s+in\s+(.+)\s+on\s+line\s+(\d+)#i', $line, $m) ) {
				$entries[] = ['time'=>$m[1],'type'=>trim($m[2]),'message'=>trim($m[3]),'file'=>str_replace(ABSPATH,'',$m[4]).':'.$m[5]];
			} elseif ( preg_match('#^\[([^\]]+)\]\s+PHP\s+([^:]+):\s+(.+)#i', $line, $m) ) {
				$entries[] = ['time'=>$m[1],'type'=>trim($m[2]),'message'=>trim($m[3]),'file'=>'Unknown'];
			} else {
				$entries[] = ['time'=>'N/A','type'=>'Info','message'=>$line,'file'=>'N/A'];
			}
		}
		return array_reverse($entries);
	}
}
