<?php
/**
 * The template for displaying Project Archive pages
 */
get_header(); ?>

<main class="site-main project-archive-page">
	<div class="project-hero" style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 100%); color: var(--color-white); padding: 80px 0; text-align: center; position: relative; overflow: hidden; margin-bottom: 50px;">
		<div class="project-hero-overlay" style="content: ''; position: absolute; top: 0; left: 0; right: 0; bottom: 0; background: radial-gradient(circle at 50% 120%, rgba(217, 119, 6, 0.15), transparent 70%); pointer-events: none;"></div>
		<div class="container" style="position: relative; z-index: 2;">
			<span class="project-hero-subtitle" style="display: inline-block; color: var(--color-accent); font-size: 0.85rem; font-weight: 700; text-transform: uppercase; letter-spacing: 2px; margin-bottom: 12px;">Thực Tế Công Trình</span>
			<h1 class="project-hero-title" style="font-size: 3rem; font-weight: 800; letter-spacing: -0.5px; margin-bottom: 16px; background: linear-gradient(to right, #ffffff, #e2e8f0); -webkit-background-clip: text; -webkit-text-fill-color: transparent;">Dự Án Thi Công</h1>
			<p class="project-hero-description" style="max-width: 600px; margin: 0 auto; color: #94a3b8; font-size: 1.1rem; line-height: 1.6;">Hình ảnh bàn giao thực tế và quy trình thi công lắp đặt thiết bị vệ sinh cao cấp trọn gói.</p>
		</div>
	</div>

	<div class="container project-content-container">
		<div class="content-area">
			<?php if ( have_posts() ) : ?>
				<div class="projects-archive-grid" style="display: grid; grid-template-columns: repeat(3, 1fr); gap: 30px; margin-bottom: 60px;">
					<?php while ( have_posts() ) : the_post(); 
						// Try to extract location or brands from the content to display as a badge
						$content = get_the_content();
						$location = '';
						$brands_used = '';
						
						if (preg_match('/Địa điểm:<\/strong>\s*([^<]+)/i', $content, $matches)) {
							$location = trim($matches[1]);
						}
						if (preg_match('/Thương hiệu sử dụng:<\/strong>\s*([^<]+)/i', $content, $matches)) {
							$brands_used = trim($matches[1]);
						}
					?>
						<article id="post-<?php the_ID(); ?>" <?php post_class('project-card'); ?> style="background-color: var(--color-card-bg); border: 1px solid var(--color-border); border-radius: 16px; overflow: hidden; display: flex; flex-direction: column; box-shadow: 0 4px 20px rgba(15, 23, 42, 0.02); transition: all 0.4s cubic-bezier(0.16, 1, 0.3, 1); height: 100%; position: relative;">
							<div class="project-thumbnail-wrapper" style="position: relative; width: 100%; height: 240px; overflow: hidden;">
								<?php if ( has_post_thumbnail() ) : ?>
									<div class="project-thumbnail" style="width: 100%; height: 100%;">
										<a href="<?php the_permalink(); ?>">
											<?php the_post_thumbnail('medium_large', ['style' => 'width: 100%; height: 100%; object-fit: cover; transition: transform 0.8s cubic-bezier(0.16, 1, 0.3, 1);']); ?>
										</a>
									</div>
								<?php else : ?>
									<div class="project-thumbnail no-thumb" style="display: flex; align-items: center; justify-content: center; background: linear-gradient(135deg, #f1f5f9 0%, #e2e8f0 100%); height: 100%;">
										<span class="thumb-placeholder" style="font-size: 3.5rem; opacity: 0.4;">🏗️</span>
									</div>
								<?php endif; ?>
								
								<?php if ($location): ?>
									<span class="project-card-badge" style="position: absolute; top: 15px; left: 15px; background: rgba(15, 23, 42, 0.85); backdrop-filter: blur(4px); color: var(--color-white); padding: 5px 12px; font-size: 0.75rem; font-weight: 600; border-radius: 6px; z-index: 2;">📍 <?php echo esc_html($location); ?></span>
								<?php endif; ?>
							</div>

							<div class="project-content" style="padding: 24px; display: flex; flex-direction: column; flex-grow: 1;">
								<h2 class="project-title" style="font-size: 1.25rem; font-weight: 700; margin-bottom: 12px; line-height: 1.45; margin-top: 0;">
									<a href="<?php the_permalink(); ?>" style="color: var(--color-primary); transition: color 0.2s ease;">
										<?php the_title(); ?>
									</a>
								</h2>
								
								<div class="project-excerpt" style="font-size: 0.92rem; color: var(--color-secondary); line-height: 1.6; margin-bottom: 20px; display: -webkit-box; -webkit-line-clamp: 3; -webkit-box-orient: vertical; overflow: hidden;">
									<?php echo wp_trim_words( get_the_excerpt(), 20 ); ?>
								</div>

								<?php if ($brands_used): ?>
									<div class="project-brands-used" style="margin-top: auto; padding-top: 15px; border-top: 1px dashed var(--color-border); font-size: 0.82rem; color: var(--color-secondary);">
										<span style="font-weight: 700; color: var(--color-primary);">Thương hiệu:</span> <?php echo esc_html($brands_used); ?>
									</div>
								<?php endif; ?>

								<!-- Click helper to make entire card clickable -->
								<a href="<?php the_permalink(); ?>" class="project-card-link-overlay" style="position: absolute; top: 0; left: 0; right: 0; bottom: 0; z-index: 5; text-indent: -9999px;">Xem chi tiết</a>
							</div>
						</article>
					<?php endwhile; ?>
				</div>

				<!-- Pagination -->
				<div class="pagination-wrapper" style="display: flex; justify-content: center; gap: 8px; margin-bottom: 50px;">
					<?php
					echo paginate_links( [
						'prev_text' => '&larr;',
						'next_text' => '&rarr;',
					] );
					?>
				</div>
			<?php else : ?>
				<div class="no-posts-alert" style="padding: 50px; text-align: center; background-color: var(--color-card-bg); border: 1px dashed var(--color-border); border-radius: 12px;">
					<p style="margin: 0; color: var(--color-secondary);">Chưa có dự án nào được đăng tải.</p>
				</div>
			<?php endif; ?>
		</div>
	</div>
</main>

<script>
document.addEventListener('DOMContentLoaded', function() {
	// Add hover effect animations via script-injected hover classes or keep simple clean CSS
	var cards = document.querySelectorAll('.project-card');
	cards.forEach(function(card) {
		card.addEventListener('mouseenter', function() {
			var img = card.querySelector('.project-thumbnail img');
			if (img) img.style.transform = 'scale(1.05)';
			card.style.transform = 'translateY(-6px)';
			card.style.boxShadow = '0 20px 35px rgba(15, 23, 42, 0.08)';
			card.style.borderColor = 'rgba(217, 119, 6, 0.3)';
		});
		card.addEventListener('mouseleave', function() {
			var img = card.querySelector('.project-thumbnail img');
			if (img) img.style.transform = 'scale(1)';
			card.style.transform = 'translateY(0)';
			card.style.boxShadow = '0 4px 20px rgba(15, 23, 42, 0.02)';
			card.style.borderColor = 'var(--color-border)';
		});
	});
});
</script>

<?php get_footer(); ?>
