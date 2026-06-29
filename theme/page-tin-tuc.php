<?php
/**
 * Template Name: Tin Tức Page Template
 */
get_header(); ?>

<main class="site-main news-archive-page">
	<div class="news-hero">
		<div class="container">
			<span class="news-hero-subtitle">Bản tin & Xu hướng</span>
			<h1 class="news-hero-title">Tin Tức & Cẩm Nang</h1>
			<p class="news-hero-description">Cập nhật tin tức mới nhất, tư vấn chọn mua thiết bị vệ sinh cao cấp và xu hướng thiết kế phòng tắm hiện đại.</p>
		</div>
	</div>

	<div class="container page-content-container">
		<div class="content-area">
			<?php
			$paged = ( get_query_var( 'paged' ) ) ? get_query_var( 'paged' ) : 1;
			$args = [
				'post_type'      => 'post',
				'posts_per_page' => 6,
				'paged'          => $paged,
			];
			$news_query = new WP_Query( $args );

			if ( $news_query->have_posts() ) :
			?>
				<div class="posts-grid">
					<?php while ( $news_query->have_posts() ) : $news_query->the_post(); 
						// Calculate reading time
						$content = get_the_content();
						$word_count = count( preg_split( '/\s+/', trim( strip_tags( $content ) ) ) );
						$reading_time = ceil( $word_count / 200 );
						if ( $reading_time < 1 ) $reading_time = 1;
						
						// Get categories
						$categories = get_the_category();
						$category_name = ! empty( $categories ) ? esc_html( $categories[0]->name ) : 'Tin tức';
					?>
						<article id="post-<?php the_ID(); ?>" <?php post_class('post-card'); ?>>
							<div class="post-thumbnail-wrapper">
								<?php if ( has_post_thumbnail() ) : ?>
									<div class="post-thumbnail">
										<a href="<?php the_permalink(); ?>">
											<?php the_post_thumbnail('medium_large'); ?>
										</a>
									</div>
								<?php else : ?>
									<div class="post-thumbnail no-thumb">
										<span class="thumb-placeholder">📰</span>
									</div>
								<?php endif; ?>
								<span class="post-card-category"><?php echo $category_name; ?></span>
							</div>

							<div class="post-content">
								<div class="post-meta">
									<span class="post-meta-item post-date">
										<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="4" width="18" height="18" rx="2" ry="2"></rect><line x1="16" y1="2" x2="16" y2="6"></line><line x1="8" y1="2" x2="8" y2="6"></line><line x1="3" y1="10" x2="21" y2="10"></line></svg>
										<?php echo get_the_date(); ?>
									</span>
									<span class="post-meta-item post-reading-time">
										<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"></circle><polyline points="12 6 12 12 16 14"></polyline></svg>
										<?php echo $reading_time; ?> phút đọc
									</span>
								</div>
								<h2 class="post-title">
									<a href="<?php the_permalink(); ?>"><?php the_title(); ?></a>
								</h2>
								<div class="post-excerpt">
									<?php echo wp_trim_words( get_the_excerpt(), 22 ); ?>
								</div>
								<a href="<?php the_permalink(); ?>" class="read-more-link">
									<span>Đọc tiếp</span>
									<svg class="arrow-icon" xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="5" y1="12" x2="19" y2="12"></line><polyline points="12 5 19 12 12 19"></polyline></svg>
								</a>
							</div>
						</article>
					<?php endwhile; ?>
				</div>

				<!-- Pagination -->
				<div class="pagination-wrapper">
					<?php
					echo paginate_links( [
						'total'   => $news_query->max_num_pages,
						'current' => $paged,
						'format'  => '?paged=%#%',
						'type'    => 'plain',
						'prev_text' => '&larr;',
						'next_text' => '&rarr;',
					] );
					?>
				</div>
			<?php else : ?>
				<div class="no-posts-alert">
					<p>Không tìm thấy bài viết nào.</p>
				</div>
			<?php endif; wp_reset_postdata(); ?>
		</div>
	</div>
</main>

<?php get_footer(); ?>
