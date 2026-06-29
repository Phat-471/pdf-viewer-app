<?php
if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

class VN_Privacy_Admin_Entries {

	public static function render_entries_page() {
		$filter_form_id = isset( $_GET['filter_form_id'] ) ? intval( $_GET['filter_form_id'] ) : 0;
		$filter_month   = isset( $_GET['filter_month'] ) ? sanitize_text_field( $_GET['filter_month'] ) : '';

		$entries = VN_Privacy_DB::get_entries( $filter_form_id, $filter_month );
		$forms   = VN_Privacy_DB::get_forms();

		global $wpdb;
		$table_entries = $wpdb->prefix . 'vn_privacy_entries';

		// Stats
		$total_all     = (int) $wpdb->get_var( "SELECT COUNT(*) FROM $table_entries" );
		$today_count   = (int) $wpdb->get_var( "SELECT COUNT(*) FROM $table_entries WHERE DATE(consent_time) = CURDATE()" );
		$month_count   = (int) $wpdb->get_var( "SELECT COUNT(*) FROM $table_entries WHERE DATE_FORMAT(consent_time,'%Y-%m') = DATE_FORMAT(NOW(),'%Y-%m')" );
		$months        = $wpdb->get_col( "SELECT DISTINCT DATE_FORMAT(consent_time,'%Y-%m') FROM $table_entries ORDER BY consent_time DESC" );

		// Export URL
		$export_url = admin_url( 'admin.php?page=vn-privacy-entries&action=vn_privacy_export_csv&_wpnonce=' . wp_create_nonce( 'vn_privacy_export_nonce' ) );
		if ( $filter_form_id ) $export_url = add_query_arg( 'filter_form_id', $filter_form_id, $export_url );
		if ( $filter_month )   $export_url = add_query_arg( 'filter_month',   $filter_month,   $export_url );
		?>
		<div class="wrap">
		<div id="vn-privacy-app">

			<!-- Page Header -->
			<div class="vn-page-header">
				<div class="vn-page-header-left">
					<h1>📊 Nhật ký đồng ý & Bằng chứng</h1>
					<p>Consent Log tuân thủ Nghị định 13/2023/NĐ-CP — Lưu trữ IP, thiết bị, thời gian đồng ý</p>
				</div>
				<div class="vn-page-header-right">
					<a href="<?php echo esc_url( $export_url ); ?>" class="vn-btn vn-btn-success">📥 Xuất Excel (CSV)</a>
				</div>
			</div>

			<!-- Stats Bar -->
			<div class="vn-stats-bar">
				<div class="vn-stat-card">
					<div class="vn-stat-icon purple">📊</div>
					<div class="vn-stat-body">
						<div class="vn-stat-value"><?php echo number_format( $total_all ); ?></div>
						<div class="vn-stat-label">Tổng đăng ký</div>
					</div>
				</div>
				<div class="vn-stat-card">
					<div class="vn-stat-icon green">📅</div>
					<div class="vn-stat-body">
						<div class="vn-stat-value"><?php echo number_format( $today_count ); ?></div>
						<div class="vn-stat-label">Hôm nay</div>
					</div>
				</div>
				<div class="vn-stat-card">
					<div class="vn-stat-icon amber">🗓️</div>
					<div class="vn-stat-body">
						<div class="vn-stat-value"><?php echo number_format( $month_count ); ?></div>
						<div class="vn-stat-label">Tháng này</div>
					</div>
				</div>
				<div class="vn-stat-card">
					<div class="vn-stat-icon blue">📋</div>
					<div class="vn-stat-body">
						<div class="vn-stat-value"><?php echo count( $forms ); ?></div>
						<div class="vn-stat-label">Biểu mẫu</div>
					</div>
				</div>
			</div>

			<!-- Filter Bar -->
			<div class="vn-card" style="margin-bottom:20px;">
				<form method="GET" action="<?php echo esc_url( admin_url( 'admin.php' ) ); ?>"
					  style="display:flex;flex-wrap:wrap;gap:12px;align-items:flex-end;">
					<input type="hidden" name="page" value="vn-privacy-entries" />

					<div style="flex:1;min-width:160px;">
						<label class="vn-label">📋 Biểu mẫu</label>
						<select name="filter_form_id" class="vn-select">
							<option value="">Tất cả biểu mẫu</option>
							<?php foreach ( $forms as $f ) : ?>
								<option value="<?php echo intval( $f->id ); ?>" <?php selected( $filter_form_id, $f->id ); ?>>
									<?php echo esc_html( $f->title ); ?>
								</option>
							<?php endforeach; ?>
						</select>
					</div>

					<div style="flex:1;min-width:140px;">
						<label class="vn-label">🗓️ Tháng</label>
						<select name="filter_month" class="vn-select">
							<option value="">Tất cả thời gian</option>
							<?php foreach ( $months as $m ) :
								if ( empty( $m ) ) continue;
							?>
								<option value="<?php echo esc_attr( $m ); ?>" <?php selected( $filter_month, $m ); ?>>
									<?php echo esc_html( date( 'm/Y', strtotime( $m . '-01' ) ) ); ?>
								</option>
							<?php endforeach; ?>
						</select>
					</div>

					<div style="display:flex;gap:8px;padding-bottom:2px;">
						<button type="submit" class="vn-btn vn-btn-primary">🔍 Lọc</button>
						<?php if ( $filter_form_id || $filter_month ) : ?>
							<a href="<?php echo esc_url( admin_url( 'admin.php?page=vn-privacy-entries' ) ); ?>" class="vn-btn vn-btn-secondary">✕ Xóa lọc</a>
						<?php endif; ?>
					</div>
				</form>
			</div>

			<!-- Entries Table -->
			<?php if ( ! empty( $entries ) ) : ?>
				<div class="vn-card" style="padding:0;overflow:hidden;">
					<div class="vn-entries-table-wrap">
						<table class="vn-entries-table">
							<thead>
								<tr>
									<th>Khách hàng</th>
									<th>Số điện thoại</th>
									<th>Nội dung</th>
									<th>Biểu mẫu</th>
									<th>IP / Thiết bị</th>
									<th>Thời gian đồng ý</th>
									<th style="text-align:center;">Thao tác</th>
								</tr>
							</thead>
							<tbody>
								<?php foreach ( $entries as $e ) :
									$initials = mb_strtoupper( mb_substr( $e->fullname, 0, 1, 'UTF-8' ), 'UTF-8' );
								?>
								<tr>
									<td>
										<div style="display:flex;align-items:center;gap:10px;">
											<div class="vn-avatar"><?php echo esc_html( $initials ); ?></div>
											<div>
												<div style="font-weight:700;color:var(--vn-primary);"><?php echo esc_html( $e->fullname ); ?></div>
											</div>
										</div>
									</td>
									<td>
										<a href="tel:<?php echo esc_attr( $e->phone ); ?>" style="color:var(--vn-accent);font-weight:600;text-decoration:none;">
											<?php echo esc_html( $e->phone ); ?>
										</a>
									</td>
									<td style="max-width:200px;">
										<span style="display:block;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;color:var(--vn-muted);font-size:.82rem;">
											<?php echo esc_html( wp_trim_words( strip_tags( $e->message ), 6, '...' ) ); ?>
										</span>
									</td>
									<td>
										<span class="vn-badge vn-badge-purple"><?php echo esc_html( $e->form_title ?: 'N/A' ); ?></span>
									</td>
									<td>
										<code style="font-size:.75rem;background:var(--vn-bg);padding:2px 6px;border-radius:4px;"><?php echo esc_html( $e->ip_address ); ?></code>
									</td>
									<td style="font-size:.82rem;color:var(--vn-muted);white-space:nowrap;">
										<?php echo esc_html( $e->consent_time ); ?>
									</td>
									<td style="text-align:center;white-space:nowrap;">
										<button type="button" class="vn-btn vn-btn-secondary btn-view-entry-detail"
											style="padding:6px 10px;margin-right:4px;"
											data-fullname="<?php echo esc_attr( $e->fullname ); ?>"
											data-phone="<?php echo esc_attr( $e->phone ); ?>"
											data-form="<?php echo esc_attr( $e->form_title ?: 'N/A' ); ?>"
											data-ip="<?php echo esc_attr( $e->ip_address ); ?>"
											data-agent="<?php echo esc_attr( $e->user_agent ); ?>"
											data-time="<?php echo esc_attr( $e->consent_time ); ?>"
											data-message="<?php echo esc_attr( nl2br( $e->message ) ); ?>">
											🔍 Xem
										</button>
										<a href="<?php echo esc_url( admin_url( 'admin.php?page=vn-privacy-entries&action=delete_entry&id=' . intval( $e->id ) . '&_wpnonce=' . wp_create_nonce( 'delete_entry_' . $e->id ) ) ); ?>"
										   class="vn-btn vn-btn-danger"
										   style="padding:6px 10px;"
										   onclick="return confirm('Xóa bằng chứng đồng ý của khách hàng này?')">🗑️</a>
									</td>
								</tr>
								<?php endforeach; ?>
							</tbody>
						</table>
					</div>
				</div>

			<?php else : ?>
				<div class="vn-card">
					<div class="vn-empty-state">
						<div class="vn-empty-icon">📭</div>
						<h3>Chưa có lượt đăng ký nào</h3>
						<p>Khi khách hàng điền biểu mẫu và nhấn gửi, thông tin và bằng chứng đồng ý sẽ hiển thị tại đây.</p>
					</div>
				</div>
			<?php endif; ?>

		</div><!-- #vn-privacy-app -->
		</div><!-- .wrap -->

		<!-- Entry Detail Modal -->
		<div id="vn-entry-detail-modal" style="display:none;position:fixed;inset:0;background:rgba(15,23,42,.65);z-index:99999;justify-content:center;align-items:center;backdrop-filter:blur(6px);">
			<div style="background:#fff;border-radius:16px;width:92%;max-width:580px;padding:28px;box-shadow:0 24px 60px rgba(0,0,0,.2);position:relative;box-sizing:border-box;max-height:90vh;overflow-y:auto;">
				<button type="button" id="close-detail-modal" style="position:absolute;top:16px;right:20px;background:transparent;border:none;font-size:1.8rem;color:var(--vn-muted);cursor:pointer;line-height:1;">&times;</button>

				<h2 style="font-weight:800;color:var(--vn-primary);margin:0 0 20px;font-size:1.3rem;border-bottom:1px solid var(--vn-border);padding-bottom:14px;">
					🔍 Chi tiết yêu cầu đăng ký
				</h2>

				<div style="display:flex;flex-direction:column;gap:14px;">
					<div>
						<span class="vn-label">Họ và Tên</span>
						<p id="modal-detail-fullname" style="margin:4px 0 0;font-weight:700;color:var(--vn-primary);font-size:1rem;"></p>
					</div>
					<div>
						<span class="vn-label">Số điện thoại</span>
						<p id="modal-detail-phone" style="margin:4px 0 0;font-weight:700;color:var(--vn-primary);font-size:1rem;"></p>
					</div>
					<div>
						<span class="vn-label">Biểu mẫu</span>
						<p id="modal-detail-form" style="margin:4px 0 0;color:var(--vn-text);"></p>
					</div>
					<div>
						<span class="vn-label">Chi tiết / Các trường dữ liệu</span>
						<div id="modal-detail-message" style="margin:6px 0 0;background:var(--vn-bg);padding:12px 16px;border-radius:8px;border:1px solid var(--vn-border);font-size:.88rem;line-height:1.7;white-space:pre-line;"></div>
					</div>
					<div style="display:grid;grid-template-columns:1fr 1fr;gap:12px;border-top:1px dashed var(--vn-border);padding-top:14px;">
						<div>
							<span class="vn-label">Địa chỉ IP (Bằng chứng)</span>
							<code id="modal-detail-ip" style="display:block;margin-top:4px;font-size:.85rem;background:var(--vn-bg);padding:4px 8px;border-radius:4px;"></code>
						</div>
						<div>
							<span class="vn-label">Thời gian đồng ý</span>
							<p id="modal-detail-time" style="margin:4px 0 0;font-size:.85rem;color:var(--vn-text);"></p>
						</div>
					</div>
					<div>
						<span class="vn-label">Thiết bị người dùng (User Agent)</span>
						<p id="modal-detail-agent" style="margin:4px 0 0;font-size:.75rem;color:var(--vn-muted);background:var(--vn-bg);padding:8px 12px;border-radius:6px;border:1px solid var(--vn-border);line-height:1.4;word-break:break-all;"></p>
					</div>
				</div>
			</div>
		</div>
		<?php
	}
}
