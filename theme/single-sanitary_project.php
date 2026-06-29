<?php
/**
 * The template for displaying Project Detail pages
 */
get_header(); ?>

<main class="site-main container" style="padding-top: 40px; padding-bottom: 80px;">
	<div class="content-area">
		<div class="back-to-archive-wrapper" style="max-width: 850px; margin: 0 auto 20px;">
			<a href="<?php echo esc_url( home_url( '/du-an/' ) ); ?>" class="back-to-archive-link" style="display: inline-flex; align-items: center; gap: 8px; color: var(--color-secondary); font-size: 0.9rem; font-weight: 600; text-decoration: none; transition: var(--transition);">
				<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><line x1="19" y1="12" x2="5" y2="12"></line><polyline points="12 19 5 12 12 5"></polyline></svg>
				Quay lại danh sách dự án
			</a>
		</div>

		<?php if ( have_posts() ) : while ( have_posts() ) : the_post(); 
			$content = get_the_content();
			
			// Parse meta details
			$client = 'Đang cập nhật';
			$location = 'Đang cập nhật';
			$brands = 'Đang cập nhật';
			
			if (preg_match('/Chủ đầu tư:<\/strong>\s*([^<]+)/i', $content, $matches)) {
				$client = trim($matches[1]);
			}
			if (preg_match('/Địa điểm:<\/strong>\s*([^<]+)/i', $content, $matches)) {
				$location = trim($matches[1]);
			}
			if (preg_match('/Thương hiệu sử dụng:<\/strong>\s*([^<]+)/i', $content, $matches)) {
				$brands = trim($matches[1]);
			}
			
			// Clean content from meta strings to avoid duplication in display
			$clean_content = preg_replace('/<p><strong>(Chủ đầu tư|Địa điểm|Thương hiệu sử dụng):<\/strong>.*?<\/p>/i', '', $content);
			$clean_content = apply_filters('the_content', $clean_content);
		?>
			<article id="post-<?php the_ID(); ?>" <?php post_class('single-project-detail'); ?> style="max-width: 850px; margin: 0 auto; background: var(--color-card-bg); border: 1px solid var(--color-border); border-radius: 16px; padding: 50px; box-shadow: 0 4px 35px rgba(15, 23, 42, 0.015);">
				<header class="entry-header" style="margin-bottom: 35px; border-bottom: 1px solid var(--color-border); padding-bottom: 25px;">
					<span class="post-single-category" style="display: inline-block; background-color: rgba(217, 119, 6, 0.1); color: var(--color-accent); font-size: 0.78rem; font-weight: 700; text-transform: uppercase; letter-spacing: 1.5px; padding: 6px 16px; border-radius: 30px; margin-bottom: 15px;">Dự án hoàn thành</span>
					<h1 class="entry-title" style="font-size: 2.5rem; font-weight: 800; line-height: 1.25; color: var(--color-primary); margin-bottom: 15px; letter-spacing: -0.5px;"><?php the_title(); ?></h1>
					
					<div class="entry-meta" style="display: flex; align-items: center; gap: 15px; color: var(--color-secondary); font-size: 0.88rem; margin-top: 15px;">
						<span class="posted-on" style="display: inline-flex; align-items: center; gap: 6px;">
							<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="color: var(--color-accent);"><rect x="3" y="4" width="18" height="18" rx="2" ry="2"></rect><line x1="16" y1="2" x2="16" y2="6"></line><line x1="8" y1="2" x2="8" y2="6"></line><line x1="3" y1="10" x2="21" y2="10"></line></svg>
							Bàn giao: <?php echo get_the_date(); ?>
						</span>
					</div>
				</header>

				<!-- Quick Summary Box -->
				<div class="project-info-card" style="background-color: var(--color-bg); border: 1px solid var(--color-border); border-radius: 12px; padding: 25px; margin-bottom: 40px; display: grid; grid-template-columns: repeat(3, 1fr); gap: 20px;">
					<div class="info-item">
						<span style="display: block; font-size: 0.8rem; text-transform: uppercase; color: var(--color-secondary); font-weight: 700; margin-bottom: 5px; letter-spacing: 0.5px;">Chủ đầu tư</span>
						<strong style="color: var(--color-primary); font-size: 1rem;"><?php echo esc_html($client); ?></strong>
					</div>
					<div class="info-item">
						<span style="display: block; font-size: 0.8rem; text-transform: uppercase; color: var(--color-secondary); font-weight: 700; margin-bottom: 5px; letter-spacing: 0.5px;">Địa điểm</span>
						<strong style="color: var(--color-primary); font-size: 1rem;">📍 <?php echo esc_html($location); ?></strong>
					</div>
					<div class="info-item">
						<span style="display: block; font-size: 0.8rem; text-transform: uppercase; color: var(--color-secondary); font-weight: 700; margin-bottom: 5px; letter-spacing: 0.5px;">Thương hiệu</span>
						<strong style="color: var(--color-accent); font-size: 1rem;"><?php echo esc_html($brands); ?></strong>
					</div>
				</div>

				<?php if ( has_post_thumbnail() ) : ?>
					<div class="post-featured-image" style="margin-bottom: 40px; border-radius: 14px; overflow: hidden; box-shadow: 0 10px 30px rgba(0,0,0,0.06);">
						<?php the_post_thumbnail('large', ['style' => 'width:100%; height:auto; display:block;']); ?>
					</div>
				<?php endif; ?>

				<div class="entry-content" style="font-size: 1.08rem; line-height: 1.85; color: #1e293b;">
					<?php echo $clean_content; ?>
				</div>

				<!-- Premium Call to Action Banner -->
				<div class="project-cta-banner" style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 100%); color: #ffffff; border-radius: 14px; padding: 35px; margin-top: 50px; text-align: center; position: relative; overflow: hidden;">
					<div style="position: absolute; top: 0; left: 0; right: 0; bottom: 0; background: radial-gradient(circle at 80% 20%, rgba(217, 119, 6, 0.2), transparent 60%); pointer-events: none;"></div>
					<h3 style="font-size: 1.5rem; font-weight: 800; margin-bottom: 12px; color: #ffffff;">Bạn muốn sở hữu không gian tắm đẳng cấp như thế này?</h3>
					<p style="color: #94a3b8; font-size: 0.98rem; max-width: 600px; margin: 0 auto 25px; line-height: 1.6;">Hồng Miên cung cấp trọn gói tư vấn bản vẽ kỹ thuật, cung cấp thiết bị và thi công lắp đặt chuyên nghiệp cam kết chất lượng tốt nhất.</p>
					<a href="<?php echo esc_url( home_url( '/lien-he/' ) ); ?>" class="btn btn-accent" style="display: inline-flex; align-items: center; gap: 8px; text-decoration: none;">
						Tư vấn & Nhận báo giá ngay
						<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><line x1="5" y1="12" x2="19" y2="12"></line><polyline points="12 5 19 12 12 19"></polyline></svg>
					</a>
				</div>
			</article>
		<?php endwhile; endif; ?>
	</div>
</main>

<style>
/* Responsive overrides for project info box */
@media (max-width: 767px) {
	.single-project-detail {
		padding: 25px 20px !important;
	}
	.project-info-card {
		grid-template-columns: 1fr !important;
		gap: 15px !important;
		padding: 20px !important;
	}
	.project-cta-banner {
		padding: 25px 20px !important;
	}
	.project-cta-banner h3 {
		font-size: 1.25rem !important;
	}
}
</style>

<?php get_footer(); ?>
