<?php get_header(); ?>

<main class="site-main container">
	<div class="content-area">
		<?php if ( have_posts() ) : while ( have_posts() ) : the_post(); ?>
			<article id="post-<?php the_ID(); ?>" <?php post_class('single-post-detail'); ?>>
				<header class="entry-header">
					<h1 class="entry-title"><?php the_title(); ?></h1>
					<div class="entry-meta">
						<span class="posted-on">Đăng ngày: <?php echo get_the_date(); ?></span>
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
			</article>
		<?php endwhile; endif; ?>
	</div>
</main>

<?php get_footer(); ?>
