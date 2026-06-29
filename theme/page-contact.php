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
				$hotline = get_theme_mod( 'sanitary_hotline', '0848.276.276' );
				$hotline_tel = get_theme_mod( 'sanitary_hotline_tel', '0848276276' );
				$zalo_url = get_theme_mod( 'sanitary_zalo_url', 'https://zalo.me/0848276276' );
				$address = get_theme_mod( 'sanitary_address', 'Thôn Thanh An, xã Nghĩa Phú, thành phố Quảng Ngãi, tỉnh Quảng Ngãi' );
				$email = get_theme_mod( 'sanitary_email', 'hongmien.vn@gmail.com' );
				$working_hours = get_theme_mod( 'sanitary_working_hours', '08:00 - 17:00 (Thứ 2 - Chủ Nhật)' );
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
						<span class="detail-icon">🏢</span>
						<div>
							<strong>Kho hàng 1:</strong>
							<p>141 Đinh Tiên Hoàng, phường Nghĩa Chánh, TP Quảng Ngãi, tỉnh Quảng Ngãi</p>
						</div>
					</div>

					<div class="contact-detail-item">
						<span class="detail-icon">🏭</span>
						<div>
							<strong>Kho hàng 2:</strong>
							<p>Thôn Sung Túc, xã Nghĩa Hà, TP Quảng Ngãi, tỉnh Quảng Ngãi</p>
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

	<!-- Google Map Section (Full Width, Tabbed) -->
	<section class="contact-map-section" style="width: 100%; border-top: 1px solid var(--color-border); padding: 50px 20px; background-color: var(--color-bg);">
		<div class="container">
			<h3 style="text-align: center; font-size: 1.6rem; font-weight: 800; margin-bottom: 30px; color: var(--color-primary);">VỊ TRÍ BẢN ĐỒ KHO & CỬA HÀNG</h3>
			
			<div class="map-tabs-container" style="max-width: 800px; margin: 0 auto 30px;">
				<div class="map-tab-headers" style="display: flex; justify-content: center; gap: 15px; margin-bottom: 25px; flex-wrap: wrap;">
					<button class="map-tab-btn active" data-map-target="map-showroom" style="padding: 12px 24px; font-weight: 700; border: 1px solid var(--color-accent); background: var(--color-card-bg); color: var(--color-accent); cursor: pointer; border-radius: 8px; font-size: 0.95rem; transition: var(--transition);">Cửa Hàng (Showroom)</button>
					<button class="map-tab-btn" data-map-target="map-kho1" style="padding: 12px 24px; font-weight: 700; border: 1px solid var(--color-border); background: var(--color-card-bg); color: var(--color-primary); cursor: pointer; border-radius: 8px; font-size: 0.95rem; transition: var(--transition);">Kho Hàng 1</button>
					<button class="map-tab-btn" data-map-target="map-kho2" style="padding: 12px 24px; font-weight: 700; border: 1px solid var(--color-border); background: var(--color-card-bg); color: var(--color-primary); cursor: pointer; border-radius: 8px; font-size: 0.95rem; transition: var(--transition);">Kho Hàng 2</button>
				</div>
				
				<div class="map-tab-contents" style="height: 400px; border-radius: 12px; overflow: hidden; box-shadow: 0 10px 30px rgba(0,0,0,0.06); border: 1px solid var(--color-border);">
					<div id="map-showroom" class="map-pane active" style="width: 100%; height: 100%;">
						<iframe src="https://www.google.com/maps/embed?pb=!1m18!1m12!1m3!1d3851.4570975401293!2d108.88277377385296!3d15.133221263817388!2m3!1f0!2f0!3f0!3m2!1i1024!2i768!4f13.1!3m3!1m2!1s0x31685216a5c37d49%3A0x66a2b233f125d724!2zQ8O0bmcgVHkgVE5ISCBUaMawxqFuZyBN4bqhaSBE4buLY2ggVuG7pSBI4buTbmcgTWnDqm4!5e0!3m2!1svi!2s!4v1703949000282!5m2!1svi!2s" width="100%" height="100%" style="border:0;" allowfullscreen="" loading="lazy" referrerpolicy="no-referrer-when-downgrade"></iframe>
					</div>
					<div id="map-kho1" class="map-pane" style="width: 100%; height: 100%; display: none;">
						<iframe src="https://www.google.com/maps/embed?pb=!1m18!1m12!1m3!1d3851.78359069116!2d108.81421837385265!3d15.115251264272022!2m3!1f0!2f0!3f0!3m2!1i1024!2i768!4f13.1!3m3!1m2!1s0x316852d70309ffc9%3A0x821881a34d4ae9c1!2zMTQxIMSQaW5oIFRpw6puIEhvw6BuZywgTmdoxKlhIENow6FuaCBOYW0sIFF14bqjbmcgTmfDo2ksIFZp4buHdCBOYW0!5e0!3m2!1svi!2s!4v1703948937753!5m2!1svi!2s" width="100%" height="100%" style="border:0;" allowfullscreen="" loading="lazy" referrerpolicy="no-referrer-when-downgrade"></iframe>
					</div>
					<div id="map-kho2" class="map-pane" style="width: 100%; height: 100%; display: none;">
						<iframe src="https://www.google.com/maps/embed?pb=!1m17!1m12!1m3!1d3851.529062721307!2d108.86977056915222!3d15.129262131723253!2m3!1f0!2f0!3f0!3m2!1i1024!2i768!4f13.1!3m2!1m1!2zMTXCsDA3JzQ1LjMiTiAxMDjCsDUyJzI3LjgiRQ!5e0!3m2!1svi!2s!4v1703948875114!5m2!1svi!2s" width="100%" height="100%" style="border:0;" allowfullscreen="" loading="lazy" referrerpolicy="no-referrer-when-downgrade"></iframe>
					</div>
				</div>
			</div>
		</div>
	</section>

	<script>
	document.addEventListener('DOMContentLoaded', function() {
		var buttons = document.querySelectorAll('.map-tab-btn');
		var panes = document.querySelectorAll('.map-pane');
		
		buttons.forEach(function(btn) {
			btn.addEventListener('click', function() {
				buttons.forEach(function(b) {
					b.classList.remove('active');
					b.style.borderColor = 'var(--color-border)';
					b.style.color = 'var(--color-primary)';
					b.style.background = 'var(--color-card-bg)';
				});
				panes.forEach(function(p) { p.style.display = 'none'; });
				
				this.classList.add('active');
				this.style.borderColor = 'var(--color-accent)';
				this.style.color = 'var(--color-accent)';
				
				var targetId = this.getAttribute('data-map-target');
				var targetPane = document.getElementById(targetId);
				if (targetPane) {
					targetPane.style.display = 'block';
				}
			});
		});
	});
	</script>
</main>

<?php get_footer(); ?>
