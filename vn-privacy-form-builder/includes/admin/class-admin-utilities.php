<?php
if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

class VN_Privacy_Admin_Utilities {

	/* ----------------------------------------------------------------
	   Render the full Utilities admin page
	---------------------------------------------------------------- */
	public function render( $backups, $health_data, $file_stats ) {
		$backup_nonce = wp_create_nonce( 'vn_backup_nonce' );
		$tools_nonce  = wp_create_nonce( 'vn_tools_nonce' );
		?>
		<div class="vn-tab-wrapper" id="vn-utilities-tabs">
			<?php $this->render_tabs_nav(); ?>
			<div class="vn-tabs-content">

			<!-- ==================== TAB: BACKUP ==================== -->
			<div id="vn-tab-backup" class="vn-tab-pane active">
				<div class="vn-grid-2">

					<!-- Create Backup -->
					<div class="vn-card">
						<div class="vn-card-header">
							<div class="vn-card-icon">💾</div>
							<div>
								<h3>Tạo bản sao lưu</h3>
								<p>Sao lưu toàn bộ website hoặc chỉ Database</p>
							</div>
						</div>
						<div class="vn-form-row">
							<label class="vn-label">Loại sao lưu</label>
							<select id="vn-backup-mode" class="vn-select">
								<option value="full">Toàn bộ (Files + Database)</option>
								<option value="db_only">Chỉ Database</option>
							</select>
						</div>
						<div id="vn-backup-progress-wrap" style="display:none;margin:12px 0;">
							<div class="vn-progress" style="height:10px;">
								<div id="vn-backup-bar" class="vn-progress-bar" style="width:0%;"></div>
							</div>
							<p id="vn-backup-text" style="font-size:.8rem;color:var(--vn-muted);margin:6px 0 0;"></p>
						</div>
						<button id="btn-full-backup" class="vn-btn vn-btn-primary"
							style="width:100%;justify-content:center;"
							data-nonce="<?php echo $backup_nonce; ?>">
							🚀 Tạo Bản Sao Lưu
						</button>
					</div>

					<!-- Upload & Restore -->
					<div class="vn-card">
						<div class="vn-card-header">
							<div class="vn-card-icon">📤</div>
							<div>
								<h3>Khôi phục từ file ZIP</h3>
								<p>Tải lên file sao lưu để khôi phục website</p>
							</div>
						</div>
						<div class="vn-alert vn-alert-warning" style="margin-bottom:16px;">
							<span class="vn-alert-icon">⚠️</span>
							<div><strong>Cảnh báo:</strong> Khôi phục sẽ ghi đè toàn bộ tệp tin và database. Hãy sao lưu trước khi thực hiện.</div>
						</div>
						<div class="vn-form-row">
							<label class="vn-label">Chọn file sao lưu (.zip)</label>
							<input type="file" id="vn-restore-file-input" accept=".zip"
								style="display:block;width:100%;padding:8px;border:1px solid var(--vn-border);border-radius:8px;font-size:.88rem;box-sizing:border-box;" />
							<p id="vn-restore-file-info" style="margin:6px 0 0;font-size:.78rem;color:var(--vn-muted);"></p>
						</div>
						<div id="vn-restore-progress-wrap" style="display:none;margin:16px 0;background:var(--vn-bg);padding:14px;border-radius:8px;border:1px solid var(--vn-border);">
							<div class="vn-progress" style="height:8px;margin-bottom:14px;border-radius:4px;overflow:hidden;background:#e2e8f0;">
								<div id="vn-restore-bar" class="vn-progress-bar" style="width:0%;height:100%;background:var(--vn-accent);transition:width .3s ease;"></div>
							</div>
							<div id="vn-restore-steps" style="display:flex;flex-direction:column;gap:8px;font-size:.84rem;">
								<div class="vn-restore-step" id="step-upload" style="display:flex;align-items:center;gap:8px;color:var(--vn-muted);">
									<span class="step-icon">⚪</span> <span class="step-text">Tải lên tệp sao lưu (0%)</span>
								</div>
								<div class="vn-restore-step" id="step-assemble" style="display:flex;align-items:center;gap:8px;color:var(--vn-muted);">
									<span class="step-icon">⚪</span> <span class="step-text">Ghép nối các phần tệp tin</span>
								</div>
								<div class="vn-restore-step" id="step-extract" style="display:flex;align-items:center;gap:8px;color:var(--vn-muted);">
									<span class="step-icon">⚪</span> <span class="step-text">Giải nén & khôi phục mã nguồn</span>
								</div>
								<div class="vn-restore-step" id="step-db" style="display:flex;align-items:center;gap:8px;color:var(--vn-muted);">
									<span class="step-icon">⚪</span> <span class="step-text">Nhập dữ liệu Database (SQL)</span>
								</div>
								<div class="vn-restore-step" id="step-finish" style="display:flex;align-items:center;gap:8px;color:var(--vn-muted);">
									<span class="step-icon">⚪</span> <span class="step-text">Hoàn tất & tối ưu hóa hệ thống</span>
								</div>
							</div>
							<p id="vn-restore-text" style="font-size:.8rem;color:var(--vn-muted);margin:12px 0 0;padding-top:8px;border-top:1px solid var(--vn-border);display:none;"></p>
						</div>
						<button id="btn-chunked-restore" class="vn-btn vn-btn-danger" style="width:100%;justify-content:center;margin-top:8px;"
							data-nonce="<?php echo wp_create_nonce('vn_chunk_restore_nonce'); ?>">
							📥 Tải lên &amp; Khôi phục
						</button>
					</div>

				</div>

				<!-- Auto-Backup Schedule Card -->
				<div class="vn-card" style="margin-top:24px;">
					<div class="vn-card-header">
						<div class="vn-card-icon">🕐</div>
						<div>
							<h3>Sao lưu tự động</h3>
							<p>Tự động tạo backup theo lịch — không cần thao tác thủ công</p>
						</div>
					</div>
					<?php
					$autobackup_enabled   = get_option( 'vn_autobackup_enabled', false );
					$autobackup_frequency = get_option( 'vn_autobackup_frequency', 'daily' );
					$autobackup_mode      = get_option( 'vn_autobackup_mode', 'full' );
					$last_auto            = get_option( 'vn_autobackup_last_run', [] );
					$next_cron            = wp_next_scheduled( 'vn_privacy_auto_backup_cron' );
					?>
					<div style="display:flex;gap:16px;flex-wrap:wrap;align-items:flex-start;">
						<form method="post" action="options.php" style="flex:1;min-width:260px;">
							<?php wp_nonce_field( 'vn_autobackup_settings', 'vn_autobackup_nonce' ); ?>
							<input type="hidden" name="action" value="vn_save_autobackup_settings">
							<div style="display:flex;gap:12px;flex-wrap:wrap;align-items:center;">
								<label style="display:flex;align-items:center;gap:6px;cursor:pointer;">
									<input type="checkbox" name="vn_autobackup_enabled" value="1" <?php checked( $autobackup_enabled ); ?> id="vn-autobackup-toggle">
									<strong>Bật tự động</strong>
								</label>
								<select name="vn_autobackup_frequency" style="padding:6px 10px;border-radius:6px;border:1px solid var(--vn-border);">
									<option value="daily"   <?php selected( $autobackup_frequency, 'daily' ); ?>>Hàng ngày</option>
									<option value="weekly"  <?php selected( $autobackup_frequency, 'weekly' ); ?>>Hàng tuần</option>
									<option value="monthly" <?php selected( $autobackup_frequency, 'monthly' ); ?>>Hàng tháng</option>
								</select>
								<select name="vn_autobackup_mode" style="padding:6px 10px;border-radius:6px;border:1px solid var(--vn-border);">
									<option value="full"    <?php selected( $autobackup_mode, 'full' ); ?>>Toàn bộ</option>
									<option value="db_only" <?php selected( $autobackup_mode, 'db_only' ); ?>>Chỉ Database</option>
								</select>
								<button type="submit" class="vn-btn vn-btn-primary" style="padding:8px 16px;">💾 Lưu lịch</button>
							</div>
						</form>
						<div style="display:flex;flex-direction:column;gap:6px;font-size:.83rem;color:var(--vn-muted);">
							<?php if ( ! empty( $last_auto ) ) : ?>
								<span>🕐 Lần cuối: <strong><?php echo esc_html( $last_auto['time'] ?? '' ); ?></strong></span>
							<?php endif; ?>
							<?php if ( $next_cron ) : ?>
								<span>⏳ Lần tiếp: <strong><?php echo date( 'd/m/Y H:i', $next_cron ); ?></strong></span>
							<?php endif; ?>
							<button class="vn-btn vn-btn-secondary" id="btn-run-auto-backup-now"
								data-nonce="<?php echo $backup_nonce; ?>"
								style="margin-top:4px;padding:6px 14px;font-size:.82rem;">▶ Chạy ngay</button>
						</div>
					</div>
				</div>

				<!-- FTP Backup Settings -->
				<div class="vn-card" style="margin-top:24px;">
					<div class="vn-card-header">
						<div class="vn-card-icon">☁️</div>
						<div>
							<h3>Cấu hình gửi bản sao lưu từ xa (FTP Server)</h3>
							<p>Tự động gửi file sao lưu lên máy chủ FTP/SFTP khác sau khi quá trình sao lưu hoàn tất</p>
						</div>
					</div>
					<?php
					$sec_settings = get_option( 'vn_security_settings', [] );
					$ftp_enabled  = ! empty( $sec_settings['ftp_enabled'] );
					$ftp_host     = $sec_settings['ftp_host'] ?? '';
					$ftp_port     = $sec_settings['ftp_port'] ?? '21';
					$ftp_user     = $sec_settings['ftp_user'] ?? '';
					$ftp_pass     = $sec_settings['ftp_pass'] ?? '';
					$ftp_path     = $sec_settings['ftp_path'] ?? '/';
					?>
					<form method="post" action="">
						<?php wp_nonce_field( 'vn_ftp_settings', 'vn_ftp_nonce' ); ?>
						<input type="hidden" name="action" value="vn_save_ftp_settings">
						
						<div style="display:grid;grid-template-columns:repeat(auto-fit, minmax(200px, 1fr));gap:16px;margin-bottom:16px;">
							<div>
								<label style="font-weight:600;display:block;margin-bottom:6px;font-size:13px;">Máy chủ FTP (Host)</label>
								<input type="text" name="ftp_host" value="<?php echo esc_attr( $ftp_host ); ?>" placeholder="Ví dụ: ftp.example.com"
									style="width:100%;padding:8px 10px;border-radius:6px;border:1px solid var(--vn-border);background:var(--vn-surface);color:var(--vn-text);">
							</div>
							<div>
								<label style="font-weight:600;display:block;margin-bottom:6px;font-size:13px;">Cổng (Port)</label>
								<input type="text" name="ftp_port" value="<?php echo esc_attr( $ftp_port ); ?>" placeholder="21"
									style="width:100%;padding:8px 10px;border-radius:6px;border:1px solid var(--vn-border);background:var(--vn-surface);color:var(--vn-text);">
							</div>
							<div>
								<label style="font-weight:600;display:block;margin-bottom:6px;font-size:13px;">Tên đăng nhập (Username)</label>
								<input type="text" name="ftp_user" value="<?php echo esc_attr( $ftp_user ); ?>"
									style="width:100%;padding:8px 10px;border-radius:6px;border:1px solid var(--vn-border);background:var(--vn-surface);color:var(--vn-text);">
							</div>
							<div>
								<label style="font-weight:600;display:block;margin-bottom:6px;font-size:13px;">Mật khẩu (Password)</label>
								<input type="password" name="ftp_pass" value="<?php echo esc_attr( $ftp_pass ); ?>"
									style="width:100%;padding:8px 10px;border-radius:6px;border:1px solid var(--vn-border);background:var(--vn-surface);color:var(--vn-text);">
							</div>
							<div>
								<label style="font-weight:600;display:block;margin-bottom:6px;font-size:13px;">Thư mục đích (Remote Path)</label>
								<input type="text" name="ftp_path" value="<?php echo esc_attr( $ftp_path ); ?>" placeholder="/"
									style="width:100%;padding:8px 10px;border-radius:6px;border:1px solid var(--vn-border);background:var(--vn-surface);color:var(--vn-text);">
							</div>
						</div>

						<div style="display:flex;align-items:center;justify-content:space-between;border-top:1px solid var(--vn-border);padding-top:16px;">
							<label style="display:flex;align-items:center;gap:6px;cursor:pointer;">
								<input type="checkbox" name="ftp_enabled" value="1" <?php checked( $ftp_enabled ); ?>>
								<strong>Kích hoạt tự động tải lên FTP sau khi sao lưu</strong>
							</label>
							<button type="submit" class="vn-btn vn-btn-primary" style="padding:8px 20px;">💾 Lưu cấu hình FTP</button>
						</div>
					</form>
				</div>

				<!-- Backup List -->
				<?php if ( ! empty( $backups ) ) : ?>
				<div class="vn-card" style="margin-top:24px;">
					<div class="vn-card-header">
						<div class="vn-card-icon">📂</div>
						<div>
							<h3>Danh sách bản sao lưu (<?php echo count( $backups ); ?>/5)</h3>
							<p>Khôi phục trực tiếp, tải về, kiểm tra toàn vẹn hoặc xóa từng bản</p>
						</div>
					</div>
					<div style="overflow-x:auto;">
					<table class="vn-backup-table" style="min-width:700px;">
						<thead>
							<tr>
								<th>Tên tệp tin</th>
								<th>Kích thước</th>
								<th>Ngày tạo</th>
								<th>Ghi chú</th>
								<th>FTP</th>
								<th style="text-align:center;">Thao tác</th>
								<th style="text-align:center;">Khôi phục</th>
							</tr>
						</thead>
						<tbody>
							<?php foreach ( $backups as $b ) :
								$download_url = wp_nonce_url(
									admin_url( 'admin.php?page=vn-settings&setting_tab=utilities&action=download_zip&file=' . urlencode( $b['filename'] ) ),
									'download_zip_nonce'
								);
							?>
							<tr>
								<td>
									<div style="font-family:monospace;font-size:.82rem;">
										<?php if ( ! empty( $b['auto'] ) ) : ?><span class="vn-badge vn-badge-info" style="font-size:.7rem;margin-right:3px;">AUTO</span><?php endif; ?>
										<?php if ( ! empty( $b['verified'] ) ) : ?><span class="vn-badge vn-badge-success" style="font-size:.7rem;margin-right:3px;" title="Đã xác minh">✓</span><?php endif; ?>
										📦 <?php echo esc_html( $b['filename'] ); ?>
									</div>
								</td>
								<td><span class="vn-badge vn-badge-info"><?php echo esc_html( $b['size'] ); ?></span></td>
								<td style="color:var(--vn-muted);font-size:.82rem;"><?php echo esc_html( $b['date'] ); ?></td>
								<td style="min-width:120px;">
									<input type="text" class="vn-backup-note-input" placeholder="Ghi chú..."
										value="<?php echo esc_attr( $b['note'] ); ?>"
										data-file="<?php echo esc_attr( $b['filename'] ); ?>"
										data-nonce="<?php echo $backup_nonce; ?>"
										style="width:100%;padding:4px 8px;border:1px solid var(--vn-border);border-radius:5px;font-size:.8rem;background:var(--vn-surface);color:var(--vn-text);">
								</td>
								<td>
									<?php if ( isset( $b['ftp_status'] ) && $b['ftp_status'] === true ) : ?>
										<span style="color:var(--vn-success);font-weight:600;font-size:11px;">🟢 Đã gửi</span>
									<?php elseif ( isset( $b['ftp_status'] ) && $b['ftp_status'] === false ) : ?>
										<span style="color:var(--vn-danger);font-weight:600;font-size:11px;">🔴 Lỗi</span>
									<?php else : ?>
										<span style="color:var(--vn-muted);font-size:11px;">—</span>
									<?php endif; ?>
								</td>
								<td style="text-align:center;white-space:nowrap;">
									<button type="button"
										class="vn-btn vn-btn-secondary vn-chunked-download-btn"
										style="padding:5px 9px;margin-right:3px;font-size:.78rem;"
										data-url="<?php echo esc_url( $download_url ); ?>"
										data-filename="<?php echo esc_attr( $b['filename'] ); ?>"
										data-size="<?php echo (int) @filesize( wp_upload_dir()['basedir'] . '/vn-privacy-backups/' . $b['filename'] ); ?>">⬇️ Tải</button>
									<button type="button" class="vn-btn vn-verify-backup-btn"
										style="padding:5px 9px;margin-right:3px;font-size:.78rem;background:var(--vn-accent);color:#fff;border-radius:6px;border:none;cursor:pointer;"
										data-file="<?php echo esc_attr( $b['filename'] ); ?>"
										data-nonce="<?php echo $backup_nonce; ?>"
										title="Kiểm tra tính toàn vẹn">🔍</button>
									<button type="button" class="vn-btn vn-btn-danger vn-delete-backup-btn"
										style="padding:5px 9px;font-size:.78rem;"
										data-file="<?php echo esc_attr( $b['filename'] ); ?>"
										data-nonce="<?php echo $backup_nonce; ?>">🗑️</button>
								</td>
								<td style="text-align:center;">
									<button type="button" class="vn-btn vn-btn-warning vn-restore-server-btn"
										style="padding:6px 10px;font-size:.78rem;"
										data-file="<?php echo esc_attr( $b['filename'] ); ?>"
										data-nonce="<?php echo wp_create_nonce('vn_restore_server_nonce'); ?>"
										title="Khôi phục từ bản sao lưu này">🔄 Khôi phục</button>
								</td>
							</tr>
							<?php endforeach; ?>
						</tbody>
					</table>
					</div>
				</div>
				<?php else : ?>
				<div class="vn-card" style="margin-top:24px;">
					<div class="vn-empty-state">
						<div class="vn-empty-icon">📂</div>
						<h3>Chưa có bản sao lưu nào</h3>
						<p>Nhấn "Tạo Bản Sao Lưu" ở trên để tạo bản sao lưu đầu tiên.</p>
					</div>
				</div>
				<?php endif; ?>

			</div><!-- #vn-tab-backup -->

			<!-- ==================== TAB: SYSTEM HEALTH ==================== -->
			<div id="vn-tab-health" class="vn-tab-pane">
				<div class="vn-grid-2">

					<div class="vn-card">
						<div class="vn-card-header">
							<div class="vn-card-icon">❤️</div>
							<div>
								<h3>Trạng thái Hệ thống</h3>
								<p>Kiểm tra PHP, WordPress, SSL, Extensions</p>
							</div>
						</div>
						<?php foreach ( $health_data as $item ) :
							$badge_class = $item['status'] === 'success' ? 'vn-badge-success' : ( $item['status'] === 'warning' ? 'vn-badge-warning' : 'vn-badge-danger' );
						?>
						<div class="vn-health-row">
							<div>
								<div class="vn-health-label"><?php echo esc_html( $item['label'] ); ?></div>
								<div class="vn-health-desc"><?php echo esc_html( $item['desc'] ); ?></div>
							</div>
							<span class="vn-badge <?php echo $badge_class; ?>"><?php echo esc_html( $item['value'] ); ?></span>
						</div>
						<?php endforeach; ?>
					</div>

					<div style="display:flex;flex-direction:column;gap:20px;">

						<!-- Upload Stats -->
						<div class="vn-card">
							<div class="vn-card-header">
								<div class="vn-card-icon">📁</div>
								<div><h3>Thống kê Thư mục Uploads</h3></div>
							</div>
							<div style="display:flex;flex-direction:column;gap:10px;font-size:.88rem;">
								<div style="display:flex;justify-content:space-between;">
									<span style="color:var(--vn-muted);">Tổng tệp tin:</span>
									<strong><?php echo number_format( $file_stats['count'] ); ?> tệp</strong>
								</div>
								<div style="display:flex;justify-content:space-between;">
									<span style="color:var(--vn-muted);">Tổng dung lượng:</span>
									<strong><?php echo $file_stats['size']; ?></strong>
								</div>
							</div>
						</div>

						<!-- Tools -->
						<div class="vn-card">
							<div class="vn-card-header">
								<div class="vn-card-icon">🔧</div>
								<div>
									<h3>Công cụ Quản trị</h3>
									<p>Tối ưu hóa và bảo trì hệ thống</p>
								</div>
							</div>
							<div style="display:flex;flex-direction:column;gap:10px;">
								<button class="vn-btn vn-btn-secondary vn-tool-action-btn" style="justify-content:flex-start;"
									data-action="vn_privacy_flush_transients"
									data-nonce="<?php echo $tools_nonce; ?>"
									data-confirm="Xóa toàn bộ transient cache?">
									🧹 Xóa Transient Cache
								</button>
								<button class="vn-btn vn-btn-secondary vn-tool-action-btn" style="justify-content:flex-start;"
									data-action="vn_privacy_cleanup_db"
									data-nonce="<?php echo $tools_nonce; ?>"
									data-confirm="Dọn dẹp và tối ưu Database?">
									🗃️ Dọn dẹp &amp; Tối ưu DB
								</button>
								<button class="vn-btn vn-btn-secondary vn-tool-action-btn" style="justify-content:flex-start;"
									data-action="vn_privacy_optimize_htaccess"
									data-nonce="<?php echo $tools_nonce; ?>">
									⚡ Tối ưu .htaccess
								</button>
								<button class="vn-btn vn-btn-secondary vn-tool-action-btn" style="justify-content:flex-start;"
									data-action="vn_privacy_check_permissions"
									data-nonce="<?php echo $tools_nonce; ?>">
									🔒 Kiểm tra Quyền thư mục
								</button>
								<button class="vn-btn vn-btn-secondary vn-tool-action-btn" style="justify-content:flex-start;"
									data-action="vn_privacy_reinstall_core"
									data-nonce="<?php echo wp_create_nonce('vn_reinstall_core_nonce'); ?>"
									data-confirm="Cài đặt lại WordPress core? Sẽ tải và cài lại toàn bộ WordPress core files.">
									🔄 Cài lại WordPress Core
								</button>
								<button class="vn-btn vn-btn-secondary vn-tool-action-btn" style="justify-content:flex-start;"
									data-action="vn_privacy_delete_debug_log"
									data-nonce="<?php echo wp_create_nonce('vn_delete_debug_log_nonce'); ?>"
									data-confirm="Xóa file debug.log?">
									📝 Xóa Debug Log
								</button>
								<button class="vn-btn vn-btn-secondary vn-tool-action-btn" style="justify-content:flex-start;"
									data-action="vn_privacy_scan_changed_files"
									data-nonce="<?php echo wp_create_nonce('vn_scan_files_nonce'); ?>">
									🔍 Quét File Bị Thay Đổi
								</button>
								<button class="vn-btn vn-btn-warning vn-tool-action-btn" style="justify-content:flex-start;"
									data-action="vn_privacy_toggle_maintenance"
									data-nonce="<?php echo $tools_nonce; ?>">
									🚧 Bật/Tắt Chế độ Bảo trì
								</button>
							</div>
						</div>

					</div>
				</div>
			</div><!-- #vn-tab-health -->

		</div><!-- .vn-tabs-content -->
		</div><!-- .vn-tab-wrapper -->
		<?php
	}

	private function render_tabs_nav() {
		$tabs = [
			'backup' => [ 'icon' => '💾', 'label' => 'Sao lưu & Khôi phục' ],
			'health' => [ 'icon' => '❤️', 'label' => 'Sức khỏe Hệ thống' ],
		];
		?>
		<div class="vn-tabs-nav" style="display:flex;gap:4px;margin-bottom:24px;border-bottom:2px solid var(--vn-border);padding-bottom:0;">
			<?php foreach ( $tabs as $id => $tab ) : ?>
			<button class="vn-tab-btn <?php echo $id === 'backup' ? 'active' : ''; ?>"
				data-tab="<?php echo $id; ?>"
				style="padding:10px 18px;border:none;background:none;cursor:pointer;font-size:.9rem;font-weight:600;color:var(--vn-muted);border-bottom:2px solid transparent;margin-bottom:-2px;transition:all .2s;">
				<?php echo $tab['icon']; ?> <?php echo $tab['label']; ?>
			</button>
			<?php endforeach; ?>
		</div>
		<?php
	}
}
