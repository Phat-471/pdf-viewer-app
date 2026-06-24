<?php
/**
 * Template Name: Liên hệ
 */
get_header(); ?>

<main class="site-main">
	<!-- Hero Banner -->
	<section class="contact-hero" style="background-color: var(--color-primary); color: var(--color-white); padding: 80px 20px; text-align: center; position: relative;">
		<div class="container">
			<h1 style="font-size: 2.5rem; font-weight: 800; margin-bottom: 15px; letter-spacing: -0.5px;">LIÊN HỆ VỚI HỒNG MIÊN</h1>
			<p style="color: #cbd5e1; max-width: 600px; margin: 0 auto; font-size: 1.1rem;">Chúng tôi luôn sẵn sàng lắng nghe, khảo sát thực tế công trình và hỗ trợ tư vấn báo giá miễn phí.</p>
		</div>
	</section>

	<!-- Main Contact Section -->
	<section class="contact-content-section" style="padding: 80px 20px;">
		<div class="container contact-flex-layout">
			
			<!-- Left side: Information -->
			<div class="contact-info-block">
				<h2 style="font-size: 1.8rem; font-weight: 800; margin-bottom: 30px; color: var(--color-primary);">Thông Tin Showroom</h2>
				
				<?php
				$hotline = get_theme_mod( 'sanitary_hotline', '090 123 4567' );
				$hotline_tel = get_theme_mod( 'sanitary_hotline_tel', '0901234567' );
				$zalo_url = get_theme_mod( 'sanitary_zalo_url', 'https://zalo.me/0901234567' );
				$address = get_theme_mod( 'sanitary_address', 'Showroom Thiết Bị Vệ Sinh Hồng Miên' );
				$email = get_theme_mod( 'sanitary_email', 'contact@example.com' );
				$working_hours = get_theme_mod( 'sanitary_working_hours', '8:00 - 18:00 (Thứ 2 - Chủ Nhật)' );
				?>

				<div class="contact-detail-items">
					<div class="contact-detail-item">
						<span class="detail-icon">📍</span>
						<div>
							<strong>Địa chỉ showroom:</strong>
							<p><?php echo esc_html( $address ); ?></p>
						</div>
					</div>

					<div class="contact-detail-item">
						<span class="detail-icon">📞</span>
						<div>
							<strong>Điện thoại / Hotline:</strong>
							<p><a href="tel:<?php echo esc_attr( $hotline_tel ); ?>"><?php echo esc_html( $hotline ); ?></a></p>
						</div>
					</div>

					<div class="contact-detail-item">
						<span class="detail-icon">✉️</span>
						<div>
							<strong>Địa chỉ Email:</strong>
							<p><a href="mailto:<?php echo esc_attr( $email ); ?>"><?php echo esc_html( $email ); ?></a></p>
						</div>
					</div>

					<div class="contact-detail-item">
						<span class="detail-icon">⏰</span>
						<div>
							<strong>Giờ mở cửa showroom:</strong>
							<p><?php echo esc_html( $working_hours ); ?></p>
						</div>
					</div>
				</div>
			</div>

			<!-- Right side: Quick Contact CTA -->
			<div class="contact-cta-block">
				<div class="contact-cta-card">
					<h3>Hỗ Trợ Nhanh Qua Zalo / Hotline</h3>
					<p>Bấm liên hệ trực tuyến ngay dưới đây để gửi hình ảnh bản vẽ kỹ thuật phòng tắm hoặc sản phẩm bạn đang quan tâm để nhận tư vấn trọn gói.</p>
					
					<div class="contact-actions-vertical">
						<a href="<?php echo esc_url( $zalo_url ); ?>" target="_blank" rel="noopener noreferrer" class="btn btn-zalo btn-large-contact">Chát Tư Vấn Qua Zalo</a>
						<a href="tel:<?php echo esc_attr( $hotline_tel ); ?>" class="btn btn-phone btn-large-contact">Gọi Điện Trực Tiếp</a>
					</div>
				</div>
			</div>

		</div>
	</section>

	<!-- Google Map Section (Full Width) -->
	<section class="contact-map-section" style="width: 100%; height: 450px; background-color: #cbd5e1; border-top: 1px solid var(--color-border);">
		<iframe src="https://www.google.com/maps/embed?pb=!1m18!1m12!1m3!1d3724.8198905391307!2d105.84074211153075!3d20.99985398967926!2m3!1f0!2f0!3f0!3m2!1i1024!2i768!4f13.1!3m3!1m2!1s0x3135ac7074744747%3A0x260c88c7f394468f!2zSHVzdCAtIMSQ4bqhaSBI4buNYyBCw6FjaCBLaG9hIEjDoCBO4buZaQ!5e0!3m2!1svi!2s!4v1700000000000!5m2!1svi!2s" width="100%" height="100%" style="border:0;" allowfullscreen="" loading="lazy" referrerpolicy="no-referrer-when-downgrade"></iframe>
	</section>
</main>

<?php get_footer(); ?>
