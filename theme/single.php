<?php get_header(); ?>

<main class="site-main container" style="padding-top: 40px; padding-bottom: 80px;">
	<div class="content-area">
		<div class="back-to-archive-wrapper" style="max-width: 850px; margin: 0 auto 20px;">
			<a href="<?php echo esc_url( home_url( '/tin-tuc/' ) ); ?>" class="back-to-archive-link" style="display: inline-flex; align-items: center; gap: 8px; color: var(--color-secondary); font-size: 0.9rem; font-weight: 600; text-decoration: none; transition: var(--transition);">
				<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><line x1="19" y1="12" x2="5" y2="12"></line><polyline points="12 19 5 12 12 5"></polyline></svg>
				Quay lại Tin tức
			</a>
		</div>

		<?php if ( have_posts() ) : while ( have_posts() ) : the_post(); 
			// Calculate reading time
			$content = get_the_content();
			$word_count = count( preg_split( '/\s+/', trim( strip_tags( $content ) ) ) );
			$reading_time = ceil( $word_count / 200 );
			if ( $reading_time < 1 ) $reading_time = 1;
			
			// Categories
			$categories = get_the_category();
			$category_name = ! empty( $categories ) ? esc_html( $categories[0]->name ) : 'Tin tức';
		?>
			<article id="post-<?php the_ID(); ?>" <?php post_class('single-post-detail'); ?>>
				<header class="entry-header">
					<span class="post-single-category" style="display: inline-block; background-color: rgba(217, 119, 6, 0.1); color: var(--color-accent); font-size: 0.78rem; font-weight: 700; text-transform: uppercase; letter-spacing: 1.5px; padding: 6px 16px; border-radius: 30px; margin-bottom: 15px;"><?php echo $category_name; ?></span>
					<h1 class="entry-title"><?php the_title(); ?></h1>
					
					<div class="entry-meta" style="display: flex; align-items: center; flex-wrap: wrap; gap: 20px; color: var(--color-secondary); font-size: 0.88rem; margin-top: 20px; border-top: 1px solid var(--color-border); border-bottom: 1px solid var(--color-border); padding: 15px 0;">
						<span class="posted-on" style="display: inline-flex; align-items: center; gap: 6px;">
							<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="color: var(--color-accent);"><rect x="3" y="4" width="18" height="18" rx="2" ry="2"></rect><line x1="16" y1="2" x2="16" y2="6"></line><line x1="8" y1="2" x2="8" y2="6"></line><line x1="3" y1="10" x2="21" y2="10"></line></svg>
							Đăng ngày: <?php echo get_the_date(); ?>
						</span>
						<span class="author-meta" style="display: inline-flex; align-items: center; gap: 6px;">
							<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="color: var(--color-accent);"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path><circle cx="12" cy="7" r="4"></circle></svg>
							Người viết: <?php the_author(); ?>
						</span>
						<span class="reading-time-meta" style="display: inline-flex; align-items: center; gap: 6px;">
							<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="color: var(--color-accent);"><circle cx="12" cy="12" r="10"></circle><polyline points="12 6 12 12 16 14"></polyline></svg>
							<?php echo $reading_time; ?> phút đọc
						</span>
					</div>
				</header>

				<?php if ( has_post_thumbnail() ) : ?>
					<div class="post-featured-image">
						<?php the_post_thumbnail('large'); ?>
					</div>
				<?php endif; ?>

				<div class="entry-content">
					<?php the_content(); ?>
				</div>

				<!-- Share and Social section -->
				<div class="post-share-section" style="margin-top: 40px; padding-top: 25px; border-top: 1px dashed var(--color-border); display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 15px;">
					<span style="font-weight: 700; color: var(--color-primary); font-size: 0.95rem;">Chia sẻ bài viết này:</span>
					<div class="social-share-buttons" style="display: flex; gap: 10px;">
						<a href="https://www.facebook.com/sharer/sharer.php?u=<?php echo urlencode(get_permalink()); ?>" target="_blank" rel="noopener" style="display: inline-flex; align-items: center; gap: 8px; background-color: #1877f2; color: #ffffff !important; padding: 8px 16px; border-radius: 6px; font-size: 0.85rem; font-weight: 700; transition: var(--transition);">
							<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="currentColor" viewBox="0 0 24 24"><path d="M9 8H7v3h2v9h4v-9h3.6l.4-3H13V6c0-.5.5-1 1-1h3V1H13c-3 0-4 2-4 4v3z"/></svg>
							Facebook
						</a>
						<a href="https://sp.zalo.me/share_to_zalo?url=<?php echo urlencode(get_permalink()); ?>" target="_blank" rel="noopener" style="display: inline-flex; align-items: center; gap: 8px; background-color: #0068ff; color: #ffffff !important; padding: 8px 16px; border-radius: 6px; font-size: 0.85rem; font-weight: 700; transition: var(--transition);">
							<span style="font-weight: 900; font-family: sans-serif; font-size: 1rem; line-height: 1;">Z</span>
							Chia sẻ Zalo
						</a>
					</div>
				</div>
			</article>

			<!-- Related Posts Section -->
			<?php
			$category_ids = array();
			if ( $categories ) {
				foreach ( $categories as $cat ) {
					$category_ids[] = $cat->term_id;
				}
			}

			$related_args = array(
				'category__in'        => $category_ids,
				'post__not_in'        => array( get_the_ID() ),
				'posts_per_page'      => 3,
				'ignore_sticky_posts' => 1,
			);

			$related_query = new WP_Query( $related_args );
			if ( $related_query->have_posts() ) :
			?>
				<div class="related-posts-section" style="max-width: 850px; margin: 50px auto 0; padding-top: 40px; border-top: 1px solid var(--color-border);">
					<h3 class="related-title" style="font-size: 1.5rem; font-weight: 800; color: var(--color-primary); margin-bottom: 25px; letter-spacing: -0.5px;">Bài viết liên quan</h3>
					<div class="posts-grid related-posts-grid" style="grid-template-columns: repeat(3, 1fr); gap: 20px;">
						<?php while ( $related_query->have_posts() ) : $related_query->the_post(); 
							// Get categories of related post
							$rel_categories = get_the_category();
							$rel_category_name = ! empty( $rel_categories ) ? esc_html( $rel_categories[0]->name ) : 'Tin tức';
						?>
							<article class="post-card" style="position: relative;">
								<div class="post-thumbnail-wrapper" style="height: 140px;">
									<?php if ( has_post_thumbnail() ) : ?>
										<div class="post-thumbnail">
											<a href="<?php the_permalink(); ?>">
												<?php the_post_thumbnail('medium'); ?>
											</a>
										</div>
									<?php else : ?>
										<div class="post-thumbnail no-thumb">
											<span class="thumb-placeholder" style="font-size: 2.2rem;">📰</span>
										</div>
									<?php endif; ?>
									<span class="post-card-category" style="top: 10px; left: 10px; padding: 3px 8px; font-size: 0.65rem;"><?php echo $rel_category_name; ?></span>
								</div>

								<div class="post-content" style="padding: 15px;">
									<h4 class="post-title" style="font-size: 0.95rem; margin-bottom: 8px;">
										<a href="<?php the_permalink(); ?>"><?php the_title(); ?></a>
									</h4>
									<span class="post-date" style="font-size: 0.75rem; color: var(--color-secondary);">📅 <?php echo get_the_date(); ?></span>
								</div>
							</article>
						<?php endwhile; ?>
					</div>
				</div>
			<?php 
			endif;
			wp_reset_postdata(); 
			?>

		<?php endwhile; endif; ?>
	</div>
</main>

<?php get_footer(); ?>
