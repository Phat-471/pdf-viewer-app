<?php
if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

class VN_Privacy_Frontend_Ajax {
	public static function handle_form_submission() {
		// 1. Verify CSRF Nonce
		if ( ! isset( $_POST['nonce'] ) || ! wp_verify_nonce( $_POST['nonce'], 'vn_privacy_form_submit_nonce' ) ) {
			wp_send_json_error( [ 'message' => 'Lỗi bảo mật (Session expired). Vui lòng tải lại trang.' ] );
		}

		// 2. Honeypot Check
		if ( ! empty( $_POST['vn_honeypot'] ) ) {
			wp_send_json_success( [ 'message' => 'Gửi yêu cầu thành công!' ] ); // Silent success for spam bots
			exit;
		}

		// 3. Extract Form ID
		$form_id = isset( $_POST['form_id'] ) ? intval( $_POST['form_id'] ) : 0;
		if ( ! $form_id ) {
			wp_send_json_error( [ 'message' => 'Không tìm thấy ID biểu mẫu.' ] );
		}

		$form = VN_Privacy_DB::get_form( $form_id );
		if ( ! $form ) {
			wp_send_json_error( [ 'message' => 'Biểu mẫu không tồn tại hoặc đã bị xóa.' ] );
		}

		$decoded_data = json_decode( $form->fields, true );
		if ( empty( $decoded_data ) || ! is_array( $decoded_data ) ) {
			wp_send_json_error( [ 'message' => 'Cấu hình biểu mẫu bị lỗi.' ] );
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
				'email_notify_enable' => 0,
				'email_notify_address' => ''
			];
		}

		$fullname = '';
		$phone    = '';
		$message_parts = [];

		// 4. Strict Dynamic Validation & Sanitization
		foreach ( $fields as $f ) {
			$name = $f['name'];
			$label = $f['label'];
			$required = ! empty( $f['required'] );
			$type = $f['type'];

			$val = isset( $_POST[ $name ] ) ? trim( $_POST[ $name ] ) : '';

			// Required check
			if ( $required && $val === '' ) {
				wp_send_json_error( [ 'message' => sprintf( 'Trường "%s" là bắt buộc.', esc_html( $label ) ) ] );
			}

			// Clean/Sanitize input
			if ( $type === 'textarea' ) {
				$val = sanitize_textarea_field( $val );
			} else {
				$val = sanitize_text_field( $val );
			}

			// Phone number validation (Vietnam format)
			if ( $name === 'phone' || $type === 'tel' ) {
				if ( ! empty( $val ) && ! preg_match( '/^(03|05|07|08|09)\d{8}$/', $val ) ) {
					wp_send_json_error( [ 'message' => sprintf( 'Số điện thoại trong trường "%s" không hợp lệ. Vui lòng nhập đúng 10 số di động Việt Nam.', esc_html( $label ) ) ] );
				}
			}

			// Email validation
			if ( $type === 'email' && ! empty( $val ) ) {
				if ( ! is_email( $val ) ) {
					wp_send_json_error( [ 'message' => sprintf( 'Địa chỉ hòm thư trong trường "%s" không hợp lệ.', esc_html( $label ) ) ] );
				}
			}

			// Assign values
			if ( $name === 'fullname' ) {
				$fullname = $val;
			} elseif ( $name === 'phone' ) {
				$phone = $val;
			} else {
				// Format date nicely if type is date
				if ( $type === 'date' && ! empty( $val ) ) {
					$val = date( 'd/m/Y', strtotime( $val ) );
				}
				$message_parts[] = sprintf( '<strong>%s:</strong> %s', esc_html( $label ), esc_html( $val ) );
			}
		}

		// Consent Checkbox check
		if ( ! isset( $_POST['data_consent'] ) || $_POST['data_consent'] !== 'yes' ) {
			wp_send_json_error( [ 'message' => 'Bạn phải đồng ý với Chính sách bảo mật dữ liệu trước khi gửi.' ] );
		}

		// Append WooCommerce/Product Context details to message if present
		if ( ! empty( $_POST['vn_context_product'] ) ) {
			$message_parts[] = sprintf( '<strong>Sản phẩm quan tâm:</strong> %s', sanitize_text_field( $_POST['vn_context_product'] ) );
		}

		// Build final message column content
		$message = implode( "<br />\n", $message_parts );

		// 5. Gather Device Evidence (Consent Audit Trail)
		$ip_address = '';
		if ( ! empty( $_SERVER['HTTP_CLIENT_IP'] ) ) {
			$ip_address = $_SERVER['HTTP_CLIENT_IP'];
		} elseif ( ! empty( $_SERVER['HTTP_X_FORWARDED_FOR'] ) ) {
			$ip_address = $_SERVER['HTTP_X_FORWARDED_FOR'];
		} else {
			$ip_address = $_SERVER['REMOTE_ADDR'];
		}
		$ip_address = sanitize_text_field( $ip_address );
		$user_agent = isset( $_SERVER['HTTP_USER_AGENT'] ) ? sanitize_text_field( $_SERVER['HTTP_USER_AGENT'] ) : '';

		// 6. Save securely using database helper
		$saved = VN_Privacy_DB::save_entry( [
			'form_id'    => $form_id,
			'fullname'   => $fullname,
			'phone'      => $phone,
			'message'    => $message,
			'ip_address' => $ip_address,
			'user_agent' => $user_agent
		] );

		if ( $saved ) {
			// 7. Send Email Notification if enabled
			if ( ! empty( $settings['email_notify_enable'] ) ) {
				$to = ! empty( $settings['email_notify_address'] ) ? $settings['email_notify_address'] : get_option( 'admin_email' );
				$subject = sprintf( '[VN Privacy Form] Đăng ký mới từ form: %s', $form->title );
				
				// Build clean HTML table message
				$email_body = '<h2>Thông tin đăng ký mới</h2>';
				$email_body .= '<table style="border-collapse: collapse; width: 100%; max-width: 600px; font-family: sans-serif;">';
				$email_body .= '<tr><td style="border: 1px solid #cbd5e1; padding: 8px; font-weight: bold; background: #f1f5f9; width: 30%;">Form:</td><td style="border: 1px solid #cbd5e1; padding: 8px;">' . esc_html( $form->title ) . '</td></tr>';
				$email_body .= '<tr><td style="border: 1px solid #cbd5e1; padding: 8px; font-weight: bold; background: #f1f5f9;">Họ và Tên:</td><td style="border: 1px solid #cbd5e1; padding: 8px;">' . esc_html( $fullname ) . '</td></tr>';
				$email_body .= '<tr><td style="border: 1px solid #cbd5e1; padding: 8px; font-weight: bold; background: #f1f5f9;">Số điện thoại:</td><td style="border: 1px solid #cbd5e1; padding: 8px;">' . esc_html( $phone ) . '</td></tr>';
				
				// Custom fields
				foreach ( $fields as $f ) {
					if ( $f['name'] !== 'fullname' && $f['name'] !== 'phone' ) {
						$val = isset( $_POST[ $f['name'] ] ) ? trim( $_POST[ $f['name'] ] ) : '';
						if ( $f['type'] === 'date' && ! empty( $val ) ) {
							$val = date( 'd/m/Y', strtotime( $val ) );
						}
						$email_body .= '<tr><td style="border: 1px solid #cbd5e1; padding: 8px; font-weight: bold; background: #f1f5f9;">' . esc_html( $f['label'] ) . ':</td><td style="border: 1px solid #cbd5e1; padding: 8px;">' . esc_html( $val ) . '</td></tr>';
					}
				}
				
				$email_body .= '<tr><td style="border: 1px solid #cbd5e1; padding: 8px; font-weight: bold; background: #f1f5f9;">Địa chỉ IP:</td><td style="border: 1px solid #cbd5e1; padding: 8px;"><code>' . esc_html( $ip_address ) . '</code></td></tr>';
				$email_body .= '<tr><td style="border: 1px solid #cbd5e1; padding: 8px; font-weight: bold; background: #f1f5f9;">Thời gian:</td><td style="border: 1px solid #cbd5e1; padding: 8px;">' . current_time( 'mysql' ) . '</td></tr>';
				$email_body .= '</table>';
				$email_body .= '<p style="color: #64748b; font-size: 0.85rem; margin-top: 15px;">Đã ghi nhận bằng chứng đồng ý (Consent Log) theo Nghị định 13/2023/NĐ-CP.</p>';

				$headers = [ 'Content-Type: text/html; charset=UTF-8' ];
				wp_mail( $to, $subject, $email_body, $headers );
			}

			wp_send_json_success( [ 'message' => 'Thông tin của bạn đã được gửi và ghi nhận an toàn. Chúng tôi sẽ liên hệ sớm nhất.' ] );
		} else {
			wp_send_json_error( [ 'message' => 'Có lỗi cơ sở dữ liệu xảy ra khi lưu thông tin. Vui lòng thử lại.' ] );
		}
	}
}
