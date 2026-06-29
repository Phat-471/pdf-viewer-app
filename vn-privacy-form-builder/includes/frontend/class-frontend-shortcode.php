<?php
if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

class VN_Privacy_Frontend_Shortcode {
	public static function render_form( $atts ) {
		$args = shortcode_atts( [
			'id' => 0,
		], $atts );

		$form_id = intval( $args['id'] );
		if ( ! $form_id ) {
			return '<p style="color:#ef4444; font-weight:700;">[Lỗi: Thiếu ID của biểu mẫu]</p>';
		}

		$form = VN_Privacy_DB::get_form( $form_id );
		if ( ! $form ) {
			return '<p style="color:#ef4444; font-weight:700;">[Lỗi: Biểu mẫu không tồn tại hoặc đã bị xóa]</p>';
		}

		$decoded_data = json_decode( $form->fields, true );
		if ( empty( $decoded_data ) ) {
			return '<p style="color:#ef4444; font-weight:700;">[Lỗi: Cấu hình biểu mẫu trống]</p>';
		}

		// Support backwards compatibility for old form fields arrays
		if ( isset( $decoded_data['fields'] ) ) {
			$fields = $decoded_data['fields'];
			$settings = $decoded_data['settings'];
		} else {
			$fields = $decoded_data;
			$settings = [
				'primary_color' => '#d97706',
				'button_text_color' => '#ffffff',
				'border_radius' => '6',
				'policy_url' => home_url( '/chinh-sach-bao-mat/' ),
				'consent_text' => 'Tôi đồng ý cho phép thu thập & xử lý thông tin ({fields}) để tư vấn báo giá theo {policy_link}.',
				'email_notify_enable' => 0,
				'email_notify_address' => ''
			];
		}

		// Enqueue the registered style
		wp_enqueue_style( 'vn-privacy-frontend-css' );

		// Unique ID for JS targeting
		$unique_form_id = 'vn-privacy-form-' . $form_id;

		$primary_color = ! empty( $settings['primary_color'] ) ? sanitize_text_field( $settings['primary_color'] ) : '#d97706';
		$btn_text_color = ! empty( $settings['button_text_color'] ) ? sanitize_text_field( $settings['button_text_color'] ) : '#ffffff';
		$border_radius = isset( $settings['border_radius'] ) ? intval( $settings['border_radius'] ) : 6;
		$policy_url = ! empty( $settings['policy_url'] ) ? esc_url( $settings['policy_url'] ) : home_url( '/chinh-sach-bao-mat/' );

		ob_start();
		?>
		<style>
			#<?php echo esc_attr( $unique_form_id ); ?>-wrapper .vn-form-submit-btn {
				background-color: <?php echo $primary_color; ?> !important;
				color: <?php echo $btn_text_color; ?> !important;
				border-radius: <?php echo $border_radius; ?>px !important;
				box-shadow: 0 4px 6px <?php echo $primary_color; ?>26 !important;
			}
			#<?php echo esc_attr( $unique_form_id ); ?>-wrapper .vn-form-submit-btn:hover:not(:disabled) {
				filter: brightness(0.9) !important;
			}
			#<?php echo esc_attr( $unique_form_id ); ?>-wrapper .vn-form-input {
				border-radius: <?php echo $border_radius; ?>px !important;
			}
			#<?php echo esc_attr( $unique_form_id ); ?>-wrapper .vn-consent-label a {
				color: <?php echo $primary_color; ?> !important;
			}
		</style>

		<div class="vn-privacy-form-wrapper" id="<?php echo esc_attr( $unique_form_id ); ?>-wrapper">
			<form class="vn-privacy-custom-form" id="<?php echo esc_attr( $unique_form_id ); ?>" method="POST">
				<?php wp_nonce_field( 'vn_privacy_form_submit_nonce', 'nonce' ); ?>
				
				<!-- Honeypot -->
				<div style="display:none !important; visibility:hidden !important;">
					<input type="text" name="vn_honeypot" tabindex="-1" autocomplete="off" />
				</div>

				<input type="hidden" name="form_id" value="<?php echo intval( $form_id ); ?>" />
				<input type="hidden" name="action" value="vn_submit_privacy_form" />
				
				<?php 
				$product_info = '';
				if ( is_singular() ) {
					global $post;
					$product_info = esc_attr( get_the_title( $post->ID ) . ' (Liên kết: ' . get_permalink( $post->ID ) . ')' );
				}
				?>
				<input type="hidden" name="vn_context_product" value="<?php echo $product_info; ?>" />

				<h3 class="vn-form-heading"><?php echo esc_html( $form->title ); ?></h3>

				<?php foreach ( $fields as $f ) : 
					$req_mark = $f['required'] ? ' <span class="required-mark" style="color:#ef4444;">*</span>' : '';
					$req_attr = $f['required'] ? 'required' : '';
					$col_width_class = ( isset( $f['width'] ) && $f['width'] == '50' ) ? 'vn-col-50' : 'vn-col-100';
				?>
					<div class="vn-form-group <?php echo esc_attr( $col_width_class ); ?>">
						<label class="vn-form-label" for="<?php echo esc_attr( $unique_form_id . '-' . $f['name'] ); ?>">
							<?php echo esc_html( $f['label'] ) . $req_mark; ?>
						</label>
						<?php if ( $f['type'] === 'textarea' ) : ?>
							<textarea class="vn-form-input vn-form-textarea" 
								name="<?php echo esc_attr( $f['name'] ); ?>" 
								id="<?php echo esc_attr( $unique_form_id . '-' . $f['name'] ); ?>" 
								placeholder="<?php echo esc_attr( $f['placeholder'] ); ?>" 
								rows="4" <?php echo $req_attr; ?>></textarea>
						<?php else : ?>
							<input class="vn-form-input" 
								type="<?php echo esc_attr( $f['type'] ); ?>" 
								name="<?php echo esc_attr( $f['name'] ); ?>" 
								id="<?php echo esc_attr( $unique_form_id . '-' . $f['name'] ); ?>" 
								placeholder="<?php echo esc_attr( $f['placeholder'] ); ?>" 
								<?php echo $req_attr; ?> />
						<?php endif; ?>
					</div>
				<?php endforeach; ?>

				<!-- Mandatory Consent Checkbox -->
				<?php
				$consent_fields = [];
				foreach ( $fields as $f ) {
					$consent_fields[] = esc_html( $f['label'] );
				}
				$consent_fields_str = implode( ', ', $consent_fields );

				$raw_consent_text = ! empty( $settings['consent_text'] ) ? $settings['consent_text'] : 'Tôi đồng ý cho phép thu thập & xử lý thông tin ({fields}) để tư vấn báo giá theo {policy_link}.';
				
				// Replace template tags
				$policy_link_html = sprintf( '<a href="%s" target="_blank">%s</a>', $policy_url, esc_html__( 'Chính sách bảo mật', 'vn-privacy' ) );
				$parsed_consent_text = str_replace( 
					[ '{fields}', '{policy_link}' ], 
					[ $consent_fields_str, $policy_link_html ], 
					$raw_consent_text 
				);
				?>
				<div class="vn-form-group vn-consent-group">
					<label class="vn-consent-label">
						<input type="checkbox" name="data_consent" value="yes" required />
						<span><?php echo wp_kses_post( $parsed_consent_text ); ?> <span class="required-mark" style="color:#ef4444;">*</span></span>
					</label>
				</div>

				<div class="vn-form-message" style="display: none;"></div>

				<button type="submit" class="vn-form-submit-btn">
					<span class="btn-text">Gửi thông tin</span>
					<span class="btn-spinner" style="display:none;">Đang gửi...</span>
				</button>
			</form>
		</div>

		<script>
		document.addEventListener('DOMContentLoaded', function() {
			var formId = '<?php echo esc_js( $unique_form_id ); ?>';
			var form = document.getElementById(formId);
			if (!form) return;

			form.addEventListener('submit', function(e) {
				e.preventDefault();

				var messageBox = form.querySelector('.vn-form-message');
				var submitBtn = form.querySelector('.vn-form-submit-btn');
				var btnText = form.querySelector('.btn-text');
				var btnSpinner = form.querySelector('.btn-spinner');

				if (messageBox) {
					messageBox.style.display = 'none';
					messageBox.className = 'vn-form-message';
					messageBox.innerHTML = '';
				}

				// 1. Client-side validations
				var fullnameInput = form.querySelector('input[name="fullname"]');
				var phoneInput = form.querySelector('input[name="phone"]');
				var consentCheckbox = form.querySelector('input[name="data_consent"]');

				if (fullnameInput && fullnameInput.value.trim() === '') {
					alert('Vui lòng nhập Họ tên.');
					fullnameInput.focus();
					return;
				}

				if (phoneInput) {
					var phoneVal = phoneInput.value.trim();
					var phoneRegex = /^(03|05|07|08|09)\d{8}$/;
					if (!phoneRegex.test(phoneVal)) {
						alert('Số điện thoại chưa hợp lệ. Vui lòng nhập đúng 10 số điện thoại Việt Nam (ví dụ: 0912345678).');
						phoneInput.focus();
						return;
					}
				}

				if (consentCheckbox && !consentCheckbox.checked) {
					alert('Bạn phải tích chọn đồng ý với Chính sách bảo mật dữ liệu trước khi gửi.');
					return;
				}

				// Disable submit btn
				if (submitBtn) submitBtn.disabled = true;
				if (btnText) btnText.style.display = 'none';
				if (btnSpinner) btnSpinner.style.display = 'inline';

				var formData = new FormData(form);

				fetch('<?php echo esc_url( admin_url( 'admin-ajax.php' ) ); ?>', {
					method: 'POST',
					body: formData
				})
				.then(function(res) {
					return res.json();
				})
				.then(function(data) {
					if (submitBtn) submitBtn.disabled = false;
					if (btnText) btnText.style.display = 'inline';
					if (btnSpinner) btnSpinner.style.display = 'none';

					if (messageBox) {
						messageBox.style.display = 'block';
						if (data.success) {
							messageBox.classList.add('vn-success');
							messageBox.innerHTML = data.data.message;
							form.reset();
						} else {
							messageBox.classList.add('vn-error');
							messageBox.innerHTML = data.data.message || 'Lỗi không xác định.';
						}
					}
				})
				.catch(function() {
					if (submitBtn) submitBtn.disabled = false;
					if (btnText) btnText.style.display = 'inline';
					if (btnSpinner) btnSpinner.style.display = 'none';

					if (messageBox) {
						messageBox.style.display = 'block';
						messageBox.classList.add('vn-error');
						messageBox.innerHTML = 'Lỗi kết nối máy chủ. Vui lòng thử lại.';
					}
				});
			});
		});
		</script>
		<?php
		return ob_get_clean();
	}
}
