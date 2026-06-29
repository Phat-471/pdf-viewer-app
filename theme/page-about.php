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
				<p style="margin-bottom: 20px; color: var(--color-secondary); font-size: 1.05rem; line-height: 1.7;"><strong>CÔNG TY TNHH TMDV HỒNG MIÊN</strong> được thành lập trên cơ sở tiền thân là cửa hàng vật liệu xây dựng Hồng Miên với 25 năm kinh nghiệm trong lĩnh vực cung cấp vật liệu xây dựng.</p>
				<p style="margin-bottom: 20px; color: var(--color-secondary); font-size: 1.05rem; line-height: 1.7;">Qua thời gian dài phát triển với những nỗ lực không ngừng vươn lên của đội ngũ lãnh đạo và nhân viên kết hợp với từng bước nghiên cứu và nâng cao chất lượng sản phẩm nhằm đáp ứng ngày càng cao của người tiêu dùng, CÔNG TY TNHH TMDV HỒNG MIÊN đã trở thành một doanh nghiệp uy tín có chỗ đứng trong lĩnh vực nhập khẩu và cung ứng thiết bị vệ sinh (GIFTO, GIFTO GOLD, SDUY, TAKAMI, TQC, MANDY).</p>
				<p style="margin-bottom: 20px; color: var(--color-secondary); font-size: 1.05rem; line-height: 1.7;">Các sản phẩm của CÔNG TY HỒNG MIÊN đều đạt tiêu chuẩn của quốc tế, tất cả các nhóm ngành của CÔNG TY HỒNG MIÊN đều được kiểm tra 100% nhằm phát hiện những lỗi nhỏ nhất trước khi đóng gói ra thị trường. CÔNG TY HỒNG MIÊN đã được đăng ký bảo hộ quyền tác giả và bản quyền nhãn hiệu tại cục sở hữu trí tuệ Việt Nam.</p>
				<p style="margin-bottom: 20px; color: var(--color-secondary); font-size: 1.05rem; line-height: 1.7;">Chúng tôi tự hào vì đã mang lại sự sang trọng và tiện nghi cho quý khách hàng.</p>
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
