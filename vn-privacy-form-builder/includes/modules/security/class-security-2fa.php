<?php
/**
 * VN Security - Two-Factor Authentication (TOTP)
 * Supports Google Authenticator compatible TOTP (RFC 6238).
 */
if ( ! defined( 'ABSPATH' ) ) exit;

class VN_Security_2FA {

	public function __construct() {
		// Hook into login flow
		add_action( 'login_form',           [ $this, 'add_2fa_field_if_needed' ] );
		add_filter( 'authenticate',          [ $this, 'verify_2fa_on_auth' ], 50, 3 );
		add_action( 'show_user_profile',     [ $this, 'render_user_profile_section' ] );
		add_action( 'edit_user_profile',     [ $this, 'render_user_profile_section' ] );
		add_action( 'personal_options_update', [ $this, 'save_user_profile_section' ] );
		add_action( 'edit_user_profile_update', [ $this, 'save_user_profile_section' ] );
		add_action( 'wp_ajax_vn_2fa_generate_qr', [ $this, 'ajax_generate_qr' ] );
		add_action( 'wp_ajax_vn_2fa_disable',      [ $this, 'ajax_disable_2fa' ] );
	}

	/* ================================================================
	   Login Form - inject 2FA field if user has 2FA enabled
	================================================================ */
	public function add_2fa_field_if_needed() {
		// We don't know the user yet at this point, so we always show the field.
		// The actual check happens in authenticate filter.
		echo '<p>
			<label for="vn_2fa_code">' . __( 'Mã xác thực 2FA (nếu có)' ) . '</label>
			<input type="text" name="vn_2fa_code" id="vn_2fa_code" 
				   class="input" autocomplete="one-time-code" 
				   inputmode="numeric" maxlength="6" pattern="[0-9]{6}"
				   placeholder="6 chữ số từ ứng dụng Authenticator" 
				   style="padding:8px 12px;font-size:16px;letter-spacing:4px;">
		</p>';
	}

	/* ================================================================
	   Authenticate Filter - verify TOTP code
	================================================================ */
	public function verify_2fa_on_auth( $user, $username, $password ) {
		// Only verify if we have a valid user so far
		if ( is_wp_error( $user ) || ! ( $user instanceof WP_User ) ) return $user;

		$secret = get_user_meta( $user->ID, 'vn_2fa_secret', true );
		if ( empty( $secret ) ) return $user; // 2FA not enabled for this user

		$code = isset( $_POST['vn_2fa_code'] ) ? preg_replace( '/\D/', '', $_POST['vn_2fa_code'] ) : '';

		if ( empty( $code ) ) {
			return new WP_Error( 'vn_2fa_required',
				'<strong>⚠️ Xác thực 2 bước:</strong> Vui lòng nhập mã 6 chữ số từ ứng dụng Authenticator của bạn.'
			);
		}

		if ( ! self::verify_totp( $secret, $code ) ) {
			return new WP_Error( 'vn_2fa_invalid',
				'<strong>❌ Mã 2FA không đúng.</strong> Vui lòng thử lại hoặc chờ mã mới (mỗi 30 giây).'
			);
		}

		return $user;
	}

	/* ================================================================
	   User Profile Section
	================================================================ */
	public function render_user_profile_section( $user ) {
		if ( ! current_user_can( 'manage_options' ) && ! ( $user->ID === get_current_user_id() ) ) return;

		$secret  = get_user_meta( $user->ID, 'vn_2fa_secret', true );
		$enabled = ! empty( $secret );
		wp_nonce_field( 'vn_2fa_profile_' . $user->ID, 'vn_2fa_profile_nonce' );
		?>
		<h2>🔐 Xác thực hai bước (2FA)</h2>
		<table class="form-table">
			<tr>
				<th>Trạng thái 2FA</th>
				<td>
					<?php if ( $enabled ) : ?>
						<span style="color:#16a34a;font-weight:700;">✅ Đã bật</span>
						<br><br>
						<button type="button" class="button button-secondary" 
								id="vn-disable-2fa" 
								data-uid="<?php echo esc_attr( $user->ID ); ?>"
								data-nonce="<?php echo wp_create_nonce( 'vn_2fa_disable_' . $user->ID ); ?>">
							🗑️ Tắt 2FA
						</button>
						<p class="description">Tài khoản này đang được bảo vệ bởi xác thực 2 bước.</p>
					<?php else : ?>
						<span style="color:#94a3b8;font-weight:600;">⭕ Chưa bật</span>
						<br><br>
						<button type="button" class="button button-primary" 
								id="vn-enable-2fa"
								data-uid="<?php echo esc_attr( $user->ID ); ?>"
								data-nonce="<?php echo wp_create_nonce( 'vn_2fa_setup_' . $user->ID ); ?>"
								data-site="<?php echo esc_attr( get_bloginfo( 'name' ) ); ?>"
								data-email="<?php echo esc_attr( $user->user_email ); ?>">
							📲 Thiết lập 2FA ngay
						</button>
						<div id="vn-2fa-setup-box" style="display:none;margin-top:20px;padding:20px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:12px;max-width:400px;">
							<h3 style="margin-top:0;">Quét mã QR</h3>
							<p style="font-size:13px;color:#64748b;">Mở ứng dụng <strong>Google Authenticator</strong> hoặc <strong>Authy</strong>, sau đó quét mã QR bên dưới:</p>
							<div id="vn-qr-code-img" style="margin:12px 0;text-align:center;"></div>
							<div id="vn-2fa-secret-display" style="font-family:monospace;font-size:14px;background:#1e293b;color:#a5f3fc;padding:10px;border-radius:6px;text-align:center;letter-spacing:3px;margin-bottom:12px;"></div>
							<p style="font-size:13px;">Nhập mã 6 chữ số từ ứng dụng để xác nhận:</p>
							<input type="text" id="vn-2fa-verify-code" maxlength="6" pattern="[0-9]{6}" 
								   placeholder="000000" inputmode="numeric"
								   style="width:140px;padding:10px;font-size:18px;letter-spacing:4px;border:1px solid #e2e8f0;border-radius:8px;text-align:center;">
							<br><br>
							<button type="button" class="button button-primary" id="vn-confirm-2fa-setup">✅ Xác nhận & Bật 2FA</button>
							<span id="vn-2fa-setup-msg" style="margin-left:10px;font-size:13px;"></span>
						</div>
						<input type="hidden" id="vn-2fa-pending-secret" value="">
						<input type="hidden" name="vn_2fa_confirm_secret" id="vn-2fa-confirm-secret" value="">
						<input type="hidden" name="vn_2fa_confirm_code"   id="vn-2fa-confirm-code"   value="">
					<?php endif; ?>
				</td>
			</tr>
		</table>
		<script>
		(function($){
			$('#vn-enable-2fa').on('click', function(){
				var btn = $(this);
				var uid = btn.data('uid');
				$('#vn-2fa-setup-box').slideDown(200);
				// Generate secret via AJAX
				$.post(ajaxurl, {
					action: 'vn_2fa_generate_qr',
					nonce: btn.data('nonce'),
					uid: uid,
					site: btn.data('site'),
					email: btn.data('email')
				}, function(r){
					if(r.success){
						$('#vn-qr-code-img').html('<img src="'+r.data.qr_url+'" style="width:200px;height:200px;" alt="QR Code">');
						$('#vn-2fa-secret-display').text(r.data.secret.match(/.{1,4}/g).join(' '));
						$('#vn-2fa-pending-secret').val(r.data.secret);
					}
				});
			});

			$('#vn-confirm-2fa-setup').on('click', function(){
				var code   = $('#vn-2fa-verify-code').val().replace(/\D/g,'');
				var secret = $('#vn-2fa-pending-secret').val();
				if(code.length !== 6){ $('#vn-2fa-setup-msg').text('Nhập đủ 6 chữ số!').css('color','#ef4444'); return; }
				$('#vn-2fa-confirm-secret').val(secret);
				$('#vn-2fa-confirm-code').val(code);
				$('#vn-2fa-setup-msg').text('Đã lưu! Trang sẽ làm mới...').css('color','#16a34a');
				// Submit the profile form
				setTimeout(function(){ $('#your-profile').submit(); }, 800);
			});

			$('#vn-disable-2fa').on('click', function(){
				if(!confirm('Bạn có chắc muốn tắt 2FA không?')) return;
				var btn = $(this);
				$.post(ajaxurl, {
					action: 'vn_2fa_disable',
					nonce: btn.data('nonce'),
					uid: btn.data('uid')
				}, function(r){
					if(r.success) location.reload();
					else alert('Lỗi: ' + r.data);
				});
			});
		})(jQuery);
		</script>
		<?php
	}

	public function save_user_profile_section( $user_id ) {
		if ( ! isset( $_POST['vn_2fa_profile_nonce'] ) ) return;
		if ( ! wp_verify_nonce( $_POST['vn_2fa_profile_nonce'], 'vn_2fa_profile_' . $user_id ) ) return;
		if ( ! current_user_can( 'edit_user', $user_id ) ) return;

		$secret = sanitize_text_field( $_POST['vn_2fa_confirm_secret'] ?? '' );
		$code   = preg_replace( '/\D/', '', $_POST['vn_2fa_confirm_code'] ?? '' );

		if ( $secret && strlen( $code ) === 6 ) {
			if ( self::verify_totp( $secret, $code ) ) {
				update_user_meta( $user_id, 'vn_2fa_secret', $secret );
			}
		}
	}

	/* ================================================================
	   AJAX: Generate QR
	================================================================ */
	public function ajax_generate_qr() {
		$nonce = $_POST['nonce'] ?? '';
		$uid   = absint( $_POST['uid'] ?? 0 );
		if ( ! wp_verify_nonce( $nonce, 'vn_2fa_setup_' . $uid ) ) wp_send_json_error( 'Invalid nonce' );
		if ( ! current_user_can( 'edit_user', $uid ) ) wp_send_json_error( 'Unauthorized' );

		$secret = self::generate_secret();
		$label  = rawurlencode( sanitize_text_field( $_POST['email'] ?? 'user' ) );
		$issuer = rawurlencode( sanitize_text_field( $_POST['site']  ?? get_bloginfo( 'name' ) ) );
		$uri    = "otpauth://totp/{$label}?secret={$secret}&issuer={$issuer}&algorithm=SHA1&digits=6&period=30";

		// Use Google Charts QR API (no server-side dependency)
		$qr_url = 'https://api.qrserver.com/v1/create-qr-code/?size=200x200&data=' . rawurlencode( $uri );

		wp_send_json_success( [
			'secret' => $secret,
			'uri'    => $uri,
			'qr_url' => $qr_url,
		] );
	}

	/* ================================================================
	   AJAX: Disable 2FA
	================================================================ */
	public function ajax_disable_2fa() {
		$nonce = $_POST['nonce'] ?? '';
		$uid   = absint( $_POST['uid'] ?? 0 );
		if ( ! wp_verify_nonce( $nonce, 'vn_2fa_disable_' . $uid ) ) wp_send_json_error( 'Invalid nonce' );
		if ( ! current_user_can( 'edit_user', $uid ) ) wp_send_json_error( 'Unauthorized' );

		delete_user_meta( $uid, 'vn_2fa_secret' );
		wp_send_json_success();
	}

	/* ================================================================
	   TOTP Implementation (RFC 6238)
	================================================================ */
	public static function generate_secret( $length = 16 ) {
		$chars  = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
		$secret = '';
		$max    = strlen( $chars ) - 1;
		for ( $i = 0; $i < $length; $i++ ) {
			$secret .= $chars[ random_int( 0, $max ) ];
		}
		return $secret;
	}

	public static function verify_totp( $secret, $code, $discrepancy = 1 ) {
		$code  = preg_replace( '/\D/', '', $code );
		if ( strlen( $code ) !== 6 ) return false;

		$secret_bin = self::base32_decode( $secret );
		if ( ! $secret_bin ) return false;

		$timestamp = (int) floor( time() / 30 );

		for ( $i = -$discrepancy; $i <= $discrepancy; $i++ ) {
			$t    = $timestamp + $i;
			$msg  = pack( 'N*', 0, $t );
			$hash = hash_hmac( 'sha1', $msg, $secret_bin, true );
			$offset = ord( $hash[19] ) & 0x0F;
			$otp    = (
				( ( ord( $hash[ $offset ]     ) & 0x7F ) << 24 ) |
				( ( ord( $hash[ $offset + 1 ] ) & 0xFF ) << 16 ) |
				( ( ord( $hash[ $offset + 2 ] ) & 0xFF ) << 8  ) |
				(   ord( $hash[ $offset + 3 ] ) & 0xFF )
			) % 1000000;

			if ( hash_equals( str_pad( $otp, 6, '0', STR_PAD_LEFT ), $code ) ) {
				return true;
			}
		}
		return false;
	}

	public static function base32_decode( $input ) {
		$map    = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
		$input  = strtoupper( rtrim( $input, '=' ) );
		$binary = '';

		for ( $i = 0; $i < strlen( $input ); $i++ ) {
			$char = strpos( $map, $input[ $i ] );
			if ( $char === false ) continue;
			$binary .= str_pad( decbin( $char ), 5, '0', STR_PAD_LEFT );
		}

		$output = '';
		for ( $i = 0; $i + 7 < strlen( $binary ); $i += 8 ) {
			$output .= chr( bindec( substr( $binary, $i, 8 ) ) );
		}
		return $output;
	}
}
