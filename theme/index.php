<?php get_header(); ?>

<main class="site-main container">
	<div class="content-area">
		<?php if ( have_posts() ) : ?>
			<header class="page-header">
				<h1 class="page-title"><?php the_archive_title(); ?></h1>
			</header>

			<div class="posts-grid">
				<?php while ( have_posts() ) : the_post(); ?>
					<article id="post-<?php the_ID(); ?>" <?php post_class('post-card'); ?>>
						<?php if ( has_post_thumbnail() ) : ?>
							<div class="post-thumbnail">
								<a href="<?php the_permalink(); ?>">
									<?php the_post_thumbnail('medium'); ?>
								</a>
							</div>
						<?php endif; ?>

						<div class="post-content">
							<h2 class="post-title"><a href="<?php the_permalink(); ?>"><?php the_title(); ?></a></h2>
							<div class="post-excerpt">
								<?php the_excerpt(); ?>
							</div>
						</div>
					</article>
				<?php endwhile; ?>
			</div>

			<?php the_posts_navigation(); ?>
		<?php else : ?>
			<p>Không tìm thấy bài viết nào.</p>
		<?php endif; ?>
	</div>
</main>

<?php get_footer(); ?>
