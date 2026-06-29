<?php
if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

class VN_Privacy_Admin_Forms {

	public static function render_forms_page() {
		$forms = VN_Privacy_DB::get_forms();
		// Stats
		global $wpdb;
		$total_entries = $wpdb->get_var( "SELECT COUNT(*) FROM {$wpdb->prefix}vn_privacy_entries" );
		?>
		<div class="wrap">
		<div id="vn-privacy-app">

			<!-- Page Header -->
			<div class="vn-page-header">
				<div class="vn-page-header-left">
					<h1>📋 VN Privacy Forms</h1>
					<p>Quản lý biểu mẫu thu thập dữ liệu tuân thủ Nghị định 13/2023/NĐ-CP</p>
				</div>
				<div class="vn-page-header-right">
					<a href="<?php echo esc_url( admin_url( 'admin.php?page=vn-privacy-create-form' ) ); ?>" class="vn-btn vn-btn-primary">
						＋ Tạo Form mới
					</a>
				</div>
			</div>

			<!-- Stats Bar -->
			<div class="vn-stats-bar">
				<div class="vn-stat-card">
					<div class="vn-stat-icon purple">📋</div>
					<div class="vn-stat-body">
						<div class="vn-stat-value"><?php echo count( $forms ); ?></div>
						<div class="vn-stat-label">Biểu mẫu</div>
					</div>
				</div>
				<div class="vn-stat-card">
					<div class="vn-stat-icon green">✉️</div>
					<div class="vn-stat-body">
						<div class="vn-stat-value"><?php echo number_format( $total_entries ); ?></div>
						<div class="vn-stat-label">Lượt đăng ký</div>
					</div>
				</div>
				<div class="vn-stat-card">
					<div class="vn-stat-icon amber">🔒</div>
					<div class="vn-stat-body">
						<div class="vn-stat-value">NĐ13</div>
						<div class="vn-stat-label">Chuẩn bảo mật</div>
					</div>
				</div>
				<div class="vn-stat-card">
					<div class="vn-stat-icon blue">📤</div>
					<div class="vn-stat-body">
						<div class="vn-stat-value">
							<a href="<?php echo esc_url( admin_url( 'admin.php?page=vn-privacy-entries' ) ); ?>" style="color:inherit;text-decoration:none;">Xem →</a>
						</div>
						<div class="vn-stat-label">Nhật ký đồng ý</div>
					</div>
				</div>
			</div>

			<!-- Form Cards Grid -->
			<?php if ( ! empty( $forms ) ) : ?>
				<div class="vn-forms-grid">
					<?php foreach ( $forms as $f ) :
						$entry_count = $wpdb->get_var( $wpdb->prepare(
							"SELECT COUNT(*) FROM {$wpdb->prefix}vn_privacy_entries WHERE form_id = %d",
							$f->id
						) );
					?>
					<div class="vn-form-card">
						<div style="display:flex;justify-content:space-between;align-items:flex-start;gap:8px;">
							<h3 class="vn-form-card-title"><?php echo esc_html( $f->title ); ?></h3>
							<span class="vn-badge vn-badge-success">Đang hoạt động</span>
						</div>

						<div class="vn-shortcode-box">
							<code>[vn_privacy_form id="<?php echo intval( $f->id ); ?>"]</code>
							<button type="button" class="vn-copy-btn" title="Sao chép shortcode">📋</button>
						</div>

						<div style="display:flex;gap:12px;font-size:.8rem;color:var(--vn-muted);">
							<span>🆔 ID: <strong style="color:var(--vn-text);"><?php echo intval( $f->id ); ?></strong></span>
							<span>✉️ <strong style="color:var(--vn-text);"><?php echo number_format( $entry_count ); ?></strong> đăng ký</span>
						</div>
						<div style="font-size:.76rem;color:var(--vn-muted);">📅 <?php echo esc_html( $f->created_at ); ?></div>

						<div class="vn-form-card-footer">
							<a href="<?php echo esc_url( admin_url( 'admin.php?page=vn-privacy-create-form&id=' . intval( $f->id ) ) ); ?>" class="vn-btn vn-btn-secondary" style="flex:1;justify-content:center;">✏️ Chỉnh sửa</a>
							<a href="<?php echo esc_url( admin_url( 'admin.php?page=vn-privacy-entries&filter_form_id=' . intval( $f->id ) ) ); ?>" class="vn-btn vn-btn-secondary" style="flex:1;justify-content:center;">📊 Xem đăng ký</a>
							<a href="<?php echo esc_url( admin_url( 'admin.php?page=vn-privacy-forms&action=delete_form&id=' . intval( $f->id ) . '&_wpnonce=' . wp_create_nonce( 'delete_form_' . $f->id ) ) ); ?>"
							   class="vn-btn vn-btn-danger"
							   onclick="return confirm('Xóa biểu mẫu này và toàn bộ dữ liệu liên quan?')"
							   style="padding:9px 12px;">🗑️</a>
						</div>
					</div>
					<?php endforeach; ?>
				</div>

			<?php else : ?>
				<div class="vn-card">
					<div class="vn-empty-state">
						<div class="vn-empty-icon">📋</div>
						<h3>Chưa có biểu mẫu nào</h3>
						<p>Tạo biểu mẫu đầu tiên để bắt đầu thu thập dữ liệu tuân thủ Nghị định 13.</p>
						<a href="<?php echo esc_url( admin_url( 'admin.php?page=vn-privacy-create-form' ) ); ?>" class="vn-btn vn-btn-primary">＋ Tạo Form đầu tiên</a>
					</div>
				</div>
			<?php endif; ?>

		</div><!-- #vn-privacy-app -->
		</div><!-- .wrap -->
		<?php
	}

	public static function render_create_form_page() {
		$form_id = isset( $_GET['id'] ) ? intval( $_GET['id'] ) : 0;
		$form    = null;
		if ( $form_id ) {
			$form = VN_Privacy_DB::get_form( $form_id );
		}

		$title_val    = $form ? esc_attr( $form->title ) : '';
		$page_heading = $form ? 'Chỉnh Sửa Biểu Mẫu' : 'Tạo Biểu Mẫu Mới';
		$action_val   = $form ? 'update_form' : 'save_new_form';
		$submit_text  = $form ? 'Cập nhật biểu mẫu' : 'Lưu biểu mẫu';

		// Defaults
		$primary_color        = '#6366f1';
		$button_text_color    = '#ffffff';
		$border_radius        = 8;
		$policy_url           = home_url( '/chinh-sach-bao-mat/' );
		$consent_text         = 'Tôi đồng ý cho phép thu thập & xử lý thông tin ({fields}) để tư vấn báo giá theo {policy_link}.';
		$email_notify_enable  = 0;
		$email_notify_address = '';
		$fields_json_data     = '';

		if ( $form ) {
			$decoded = json_decode( $form->fields, true );
			$settings = isset( $decoded['settings'] ) ? $decoded['settings'] : [];

			$primary_color        = ! empty( $settings['primary_color'] )       ? $settings['primary_color']       : $primary_color;
			$button_text_color    = ! empty( $settings['button_text_color'] )   ? $settings['button_text_color']   : $button_text_color;
			$border_radius        = isset( $settings['border_radius'] )         ? intval( $settings['border_radius'] ) : $border_radius;
			$policy_url           = ! empty( $settings['policy_url'] )          ? $settings['policy_url']          : $policy_url;
			$consent_text         = ! empty( $settings['consent_text'] )        ? $settings['consent_text']        : $consent_text;
			$email_notify_enable  = isset( $settings['email_notify_enable'] )   ? intval( $settings['email_notify_enable'] ) : 0;
			$email_notify_address = ! empty( $settings['email_notify_address'] ) ? $settings['email_notify_address'] : '';

			$raw_fields      = isset( $decoded['fields'] ) ? $decoded['fields'] : $decoded;
			$fields_json_data = json_encode( $raw_fields, JSON_UNESCAPED_UNICODE | JSON_HEX_TAG | JSON_HEX_AMP | JSON_HEX_APOS | JSON_HEX_QUOT );
		}
		?>
		<div class="wrap">
		<div id="vn-privacy-app">

			<!-- Page Header -->
			<div class="vn-page-header">
				<div class="vn-page-header-left">
					<h1>✏️ <?php echo esc_html( $page_heading ); ?></h1>
					<p>Kéo thả hoặc thêm nhanh các trường dữ liệu vào biểu mẫu</p>
				</div>
				<div class="vn-page-header-right">
					<a href="<?php echo esc_url( admin_url( 'admin.php?page=vn-privacy-forms' ) ); ?>" class="vn-btn vn-btn-secondary">← Quay lại</a>
				</div>
			</div>

			<div style="display:flex;gap:24px;align-items:flex-start;">

				<!-- Left: Builder -->
				<form id="vn-privacy-builder-form" method="POST" style="flex:2;min-width:0;">
					<?php wp_nonce_field( 'create_privacy_form', 'form_nonce' ); ?>
					<input type="hidden" name="vn_privacy_action" value="<?php echo esc_attr( $action_val ); ?>" />
					<input type="hidden" name="form_id" value="<?php echo intval( $form_id ); ?>" />
					<input type="hidden" name="form_fields_json" id="form_fields_json" value="" />

					<!-- Form Title -->
					<div class="vn-card">
						<div class="vn-form-row">
							<label class="vn-label">Tiêu đề biểu mẫu</label>
							<input type="text" name="form_title" class="vn-input" required
								placeholder="Ví dụ: Nhận tư vấn phong thủy phòng tắm"
								value="<?php echo $title_val; ?>" />
						</div>
					</div>

					<!-- Field Builder -->
					<div class="vn-card">
						<div class="vn-card-header">
							<div class="vn-card-icon">🔧</div>
							<div>
								<h3>Cấu trúc các trường dữ liệu</h3>
								<p>Kéo thả để sắp xếp thứ tự, nhấn ✕ để xóa trường</p>
							</div>
						</div>

						<div id="active-fields-list" style="display:flex;flex-direction:column;gap:12px;background:var(--vn-bg);padding:16px;border-radius:10px;border:2px dashed var(--vn-border);min-height:100px;"></div>
					</div>

					<!-- Style & Settings -->
					<div class="vn-card">
						<div class="vn-card-header">
							<div class="vn-card-icon">🎨</div>
							<div>
								<h3>Giao diện & Cài đặt Form</h3>
								<p>Tùy chỉnh màu sắc, bo góc và chính sách bảo mật</p>
							</div>
						</div>

						<div class="vn-grid-2" style="gap:16px;margin-bottom:16px;">
							<div class="vn-form-row" style="margin:0;">
								<label class="vn-label">Màu nút chủ đạo</label>
								<input type="color" id="primary_color" name="primary_color" value="<?php echo esc_attr( $primary_color ); ?>"
									style="width:100%;height:42px;border:1px solid var(--vn-border);border-radius:8px;cursor:pointer;padding:2px;" />
							</div>
							<div class="vn-form-row" style="margin:0;">
								<label class="vn-label">Màu chữ nút</label>
								<input type="color" id="button_text_color" name="button_text_color" value="<?php echo esc_attr( $button_text_color ); ?>"
									style="width:100%;height:42px;border:1px solid var(--vn-border);border-radius:8px;cursor:pointer;padding:2px;" />
							</div>
						</div>

						<div class="vn-grid-2" style="gap:16px;margin-bottom:16px;">
							<div class="vn-form-row" style="margin:0;">
								<label class="vn-label">Độ bo góc (px)</label>
								<input type="number" id="border_radius" name="border_radius" value="<?php echo esc_attr( $border_radius ); ?>"
									min="0" max="30" class="vn-input" />
							</div>
							<div class="vn-form-row" style="margin:0;">
								<label class="vn-label">Link Chính sách bảo mật</label>
								<input type="url" id="policy_url" name="policy_url" value="<?php echo esc_url( $policy_url ); ?>"
									class="vn-input" required />
							</div>
						</div>

						<div class="vn-form-row">
							<label class="vn-label">Nội dung đồng ý bảo mật</label>
							<textarea id="consent_text" name="consent_text" rows="2" class="vn-textarea"
								placeholder="Tôi đồng ý cho phép..."><?php echo esc_textarea( $consent_text ); ?></textarea>
							<p style="font-size:.75rem;color:var(--vn-muted);margin:4px 0 0;">Dùng <code>{fields}</code> và <code>{policy_link}</code> làm từ khóa động.</p>
						</div>

						<hr class="vn-divider" />

						<div class="vn-toggle-row">
							<div class="vn-toggle-info">
								<h4>📧 Nhận Email thông báo</h4>
								<p>Gửi email khi có lượt đăng ký mới</p>
							</div>
							<label class="vn-switch">
								<input type="checkbox" id="email_notify_enable" name="email_notify_enable" value="1"
									<?php checked( $email_notify_enable, 1 ); ?>
									onchange="document.getElementById('email_notify_wrapper').style.display=this.checked?'block':'none'" />
								<span class="vn-switch-slider"></span>
							</label>
						</div>

						<div id="email_notify_wrapper" style="display:<?php echo $email_notify_enable ? 'block' : 'none'; ?>;margin-top:12px;">
							<label class="vn-label">Email nhận thông báo</label>
							<input type="text" id="email_notify_address" name="email_notify_address"
								class="vn-input" placeholder="admin@example.com, sales@example.com"
								value="<?php echo esc_attr( $email_notify_address ); ?>" />
						</div>
					</div>

					<!-- NĐ13 Notice -->
					<div class="vn-alert vn-alert-info" style="margin-bottom:20px;">
						<span class="vn-alert-icon">🔒</span>
						<div><strong>Nghị định 13:</strong> Một hộp kiểm đồng ý chính sách bảo mật sẽ tự động hiển thị ở chân biểu mẫu để thu thập bằng chứng pháp lý trước khi khách hàng bấm gửi.</div>
					</div>

					<button type="submit" class="vn-btn vn-btn-primary" style="width:100%;justify-content:center;padding:13px 20px;">
						💾 <?php echo esc_attr( $submit_text ); ?>
					</button>
				</form>

				<!-- Right: Quick Add -->
				<div style="flex:1;min-width:220px;max-width:280px;">
					<div class="vn-card" style="position:sticky;top:40px;">
						<div class="vn-card-header">
							<div class="vn-card-icon">⚡</div>
							<div><h3>Thêm trường nhanh</h3></div>
						</div>
						<div style="display:flex;flex-direction:column;gap:8px;">
							<button type="button" class="vn-btn vn-btn-secondary" style="justify-content:flex-start;" onclick="vnAddBuilderField('text','Họ và tên','Ví dụ: Nguyễn Văn A','fullname','100',true)">👤 Họ và tên</button>
							<button type="button" class="vn-btn vn-btn-secondary" style="justify-content:flex-start;" onclick="vnAddBuilderField('tel','Số điện thoại','Ví dụ: 0912345678','phone','100',true)">📞 Số điện thoại</button>
							<button type="button" class="vn-btn vn-btn-secondary" style="justify-content:flex-start;" onclick="vnAddBuilderField('email','Email / Hòm thư','contact@example.com','email_'+Date.now(),'100',false)">📧 Địa chỉ Email</button>
							<button type="button" class="vn-btn vn-btn-secondary" style="justify-content:flex-start;" onclick="vnAddBuilderField('date','Ngày sinh nhật','','birthdate_'+Date.now(),'100',false)">🎂 Ngày sinh nhật</button>
							<button type="button" class="vn-btn vn-btn-secondary" style="justify-content:flex-start;" onclick="vnAddBuilderField('date','Ngày hẹn tư vấn','','appt_'+Date.now(),'100',false)">📅 Ngày hẹn tư vấn</button>
							<button type="button" class="vn-btn vn-btn-secondary" style="justify-content:flex-start;" onclick="vnAddBuilderField('textarea','Nội dung yêu cầu','Nhập chi tiết...','message_'+Date.now(),'100',false)">💬 Nội dung lời nhắn</button>
							<button type="button" class="vn-btn vn-btn-secondary" style="justify-content:flex-start;" onclick="vnAddBuilderField('text','Trường Tùy Biến','','custom_'+Date.now(),'100',false)">✏️ Trường Tùy Biến</button>
						</div>

						<!-- Live Preview -->
						<hr class="vn-divider" />
						<p style="font-size:.78rem;color:var(--vn-muted);margin-bottom:10px;font-weight:600;">XEM TRƯỚC NÚT</p>
						<button id="vn-form-preview-btn" style="width:100%;padding:11px;border:none;border-radius:8px;font-weight:700;font-size:.9rem;cursor:default;background:<?php echo esc_attr($primary_color); ?>;color:<?php echo esc_attr($button_text_color); ?>;">
							Gửi thông tin
						</button>
					</div>
				</div>
			</div>

		</div><!-- #vn-privacy-app -->
		</div><!-- .wrap -->

		<script>
		function vnAddBuilderField(type, defaultLabel, defaultPlaceholder, fieldName, width, required) {
			var list = document.getElementById('active-fields-list');
			if (!list) return;
			var isDefault = (fieldName === 'fullname' || fieldName === 'phone');
			var item = document.createElement('div');
			item.className = 'field-item-box';
			item.setAttribute('data-type', type);
			item.setAttribute('data-name', fieldName);
			item.style.cssText = 'background:#fff;border:1px solid var(--vn-border);border-radius:10px;padding:14px;display:flex;flex-direction:column;gap:8px;position:relative;border-left:4px solid ' + (isDefault ? 'var(--vn-primary)' : 'var(--vn-accent)') + ';';

			var html = '<div style="display:flex;align-items:center;justify-content:space-between;gap:8px;">'
				+ '<span style="font-size:.75rem;font-weight:700;text-transform:uppercase;color:var(--vn-muted);">Trường ' + type.toUpperCase() + '</span>'
				+ '<div style="display:flex;gap:4px;">'
				+ '<button type="button" onclick="this.closest(\'.field-item-box\').previousElementSibling&&this.closest(\'.field-item-box\').parentNode.insertBefore(this.closest(\'.field-item-box\'),this.closest(\'.field-item-box\').previousElementSibling)" style="background:var(--vn-bg);border:1px solid var(--vn-border);border-radius:4px;padding:2px 7px;cursor:pointer;font-size:.8rem;">▲</button>'
				+ '<button type="button" onclick="var n=this.closest(\'.field-item-box\').nextElementSibling;n&&n.parentNode.insertBefore(n,this.closest(\'.field-item-box\'))" style="background:var(--vn-bg);border:1px solid var(--vn-border);border-radius:4px;padding:2px 7px;cursor:pointer;font-size:.8rem;">▼</button>'
				+ (!isDefault ? '<button type="button" onclick="this.closest(\'.field-item-box\').remove()" style="background:transparent;border:none;color:var(--vn-danger);cursor:pointer;font-size:1.2rem;font-weight:700;padding:0 4px;">✕</button>' : '')
				+ '</div></div>'
				+ '<div style="display:grid;grid-template-columns:2fr 2fr 1fr;gap:8px;">'
				+ '<input type="text" class="field-label vn-input" value="' + defaultLabel + '" placeholder="Label..." required style="padding:7px 10px;" />'
				+ '<input type="text" class="field-placeholder vn-input" value="' + defaultPlaceholder + '" placeholder="Placeholder..." style="padding:7px 10px;" />'
				+ '<select class="field-width vn-select" style="padding:7px 10px;">'
				+ '<option value="100"' + (width == '100' ? ' selected' : '') + '>100%</option>'
				+ '<option value="50"' + (width == '50' ? ' selected' : '') + '>50%</option>'
				+ '</select></div>'
				+ '<label style="font-size:.82rem;display:flex;align-items:center;gap:6px;color:var(--vn-muted);">'
				+ '<input type="checkbox" class="field-required"' + (isDefault || required ? ' checked' : '') + (isDefault ? ' disabled' : '') + '> Bắt buộc nhập (Required)'
				+ '</label>';

			item.innerHTML = html;
			list.appendChild(item);
		}

		// Serialize on submit
		document.getElementById('vn-privacy-builder-form').addEventListener('submit', function() {
			var boxes = document.querySelectorAll('#active-fields-list .field-item-box');
			var fields = [];
			boxes.forEach(function(b) {
				fields.push({
					type: b.getAttribute('data-type'),
					name: b.getAttribute('data-name'),
					label: b.querySelector('.field-label').value.trim(),
					placeholder: b.querySelector('.field-placeholder').value.trim(),
					required: b.querySelector('.field-required').checked,
					width: b.querySelector('.field-width').value
				});
			});
			var settings = {
				primary_color: document.getElementById('primary_color').value,
				button_text_color: document.getElementById('button_text_color').value,
				border_radius: document.getElementById('border_radius').value,
				policy_url: document.getElementById('policy_url').value,
				consent_text: document.getElementById('consent_text').value.trim(),
				email_notify_enable: document.getElementById('email_notify_enable').checked ? 1 : 0,
				email_notify_address: document.getElementById('email_notify_address').value.trim()
			};
			document.getElementById('form_fields_json').value = JSON.stringify({ fields: fields, settings: settings });
		});

		// Init existing fields
		(function() {
			var existing = <?php echo $form && ! empty( $fields_json_data ) ? $fields_json_data : 'null'; ?>;
			if (existing && Array.isArray(existing) && existing.length > 0) {
				existing.forEach(function(f) { vnAddBuilderField(f.type, f.label, f.placeholder||'', f.name, f.width||'100', f.required); });
			} else {
				vnAddBuilderField('text','Họ và tên','Ví dụ: Nguyễn Văn A','fullname','100',true);
				vnAddBuilderField('tel','Số điện thoại','Ví dụ: 0912345678','phone','100',true);
			}
		})();

		// Live color preview
		document.getElementById('primary_color').addEventListener('input', function() {
			document.getElementById('vn-form-preview-btn').style.background = this.value;
		});
		document.getElementById('button_text_color').addEventListener('input', function() {
			document.getElementById('vn-form-preview-btn').style.color = this.value;
		});
		</script>
		<?php
	}
}
