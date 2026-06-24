<?php
/**
 * Template Name: Giới thiệu
 */
get_header(); ?>

<main class="site-main">
	<!-- Hero Banner -->
	<section class="about-hero" style="background-color: var(--color-primary); color: var(--color-white); padding: 80px 20px; text-align: center; position: relative;">
		<div class="container">
			<h1 style="font-size: 2.5rem; font-weight: 800; margin-bottom: 15px; letter-spacing: -0.5px;">GIỚI THIỆU HỒNG MIÊN</h1>
			<p style="color: #cbd5e1; max-width: 600px; margin: 0 auto; font-size: 1.1rem;">Giải pháp thiết bị vệ sinh cao cấp chính hãng và dịch vụ thi công trọn gói uy tín hàng đầu.</p>
		</div>
	</section>

	<!-- Main Content Section -->
	<section class="about-content-section" style="padding: 80px 20px;">
		<div class="container about-flex-layout">
			<div class="about-text">
				<h2 style="font-size: 1.8rem; font-weight: 800; margin-bottom: 20px; color: var(--color-primary);">Về Chúng Tôi</h2>
				<p style="margin-bottom: 20px; color: var(--color-secondary); font-size: 1.05rem; line-height: 1.7;">Chào mừng Quý khách đến với <strong>Showroom Thiết Bị Vệ Sinh Hồng Miên</strong>. Chúng tôi tự hào là đơn vị cung cấp các dòng sản phẩm thiết bị vệ sinh chính hãng như bồn cầu thông minh, sen tắm đứng massage, chậu rửa lavabo chất lượng cao từ các thương hiệu đối tác lớn.</p>
				<p style="margin-bottom: 20px; color: var(--color-secondary); font-size: 1.05rem; line-height: 1.7;">Không chỉ đơn thuần cung cấp sản phẩm catalogue, Hồng Miên mang tới cho khách hàng giải pháp thiết kế bố trí phòng tắm 2D/3D tối ưu diện tích và quy trình lắp đặt thi công trọn gói từ A-Z bởi các kỹ thuật viên lành nghề, đảm bảo công trình đạt tính thẩm mỹ và độ bền vững lâu dài.</p>
				
				<h3 style="font-size: 1.3rem; font-weight: 700; margin-top: 30px; margin-bottom: 15px; color: var(--color-primary);">Sứ Mệnh Của Hồng Miên</h3>
				<p style="color: var(--color-secondary); font-size: 1.05rem; line-height: 1.7; border-left: 3px solid var(--color-accent); padding-left: 15px; font-style: italic;">"Mang lại không gian sống tiện nghi, sạch sẽ và an toàn cho mỗi gia đình Việt Nam bằng những sản phẩm thiết bị phòng tắm hiện đại, thông minh cùng dịch vụ lắp đặt tận tâm chuyên nghiệp nhất."</p>
			</div>
			<div class="about-image-wrapper">
				<img src="<?php echo esc_url( get_template_directory_uri() . '/screenshot.png' ); ?>" alt="Showroom Hồng Miên" class="about-main-img">
			</div>
		</div>
	</section>

	<!-- Core Values Section -->
	<section class="about-values-section" style="background-color: #f1f5f9; padding: 80px 20px;">
		<div class="container">
			<h2 style="text-align: center; font-size: 1.8rem; font-weight: 800; margin-bottom: 50px; color: var(--color-primary);">GIÁ TRỊ CỐT LÕI</h2>
			<div class="values-grid">
				
				<div class="value-card">
					<div class="value-icon">🛡️</div>
					<h3>100% Chính Hãng</h3>
					<p>Chúng tôi cam kết tuyệt đối các sản phẩm bồn cầu, vòi sen, lavabo cung cấp đều chính hãng. Đền bù gấp đôi nếu phát hiện hàng giả, hàng nhái kém chất lượng.</p>
				</div>

				<div class="value-card">
					<div class="value-icon">🔧</div>
					<h3>Lắp Đặt Tận Tâm</h3>
					<p>Quy trình lắp đặt chuẩn kỹ thuật, không rò rỉ nước, thi công sạch sẽ. Các kỹ thuật viên giàu kinh nghiệm đồng hành cùng ngôi nhà bạn trong suốt vòng đời sản phẩm.</p>
				</div>

				<div class="value-card">
					<div class="value-icon">🤝</div>
					<h3>Đồng Hành Uy Tín</h3>
					<p>Bảo hành dài hạn và hỗ trợ tư vấn bảo dưỡng định kỳ. Mọi phản hồi của khách hàng đều được chúng tôi xử lý nhanh chóng trong vòng 24 giờ làm việc.</p>
				</div>

			</div>
		</div>
	</section>
</main>

<?php get_footer(); ?>
