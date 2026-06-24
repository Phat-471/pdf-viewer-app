<?php get_header(); ?>

<main class="site-main container">
	<header class="archive-header-banner">
		<div class="archive-banner-content">
			<h1 class="archive-title">Catalogue Sản Phẩm</h1>
			<p class="taxonomy-description">Tổng hợp đầy đủ các sản phẩm thiết bị vệ sinh cao cấp chính hãng từ các thương hiệu đối tác.</p>
		</div>
		<div class="archive-banner-image">
			<img src="<?php echo esc_url( get_template_directory_uri() . '/assets/images/slide_luxury_bath.webp' ); ?>" alt="Catalogue Sản Phẩm">
		</div>
	</header>

	<div class="catalog-layout">
		<?php echo do_shortcode( '[sanitary_product_filter]' ); ?>
		<div class="catalog-content">
			<?php if ( have_posts() ) : ?>
				<div class="products-grid">
					<?php while ( have_posts() ) : the_post(); ?>
						<div class="product-card">
							<a href="<?php the_permalink(); ?>" class="product-img-link">
								<?php if ( has_post_thumbnail() ) : ?>
									<?php the_post_thumbnail( 'medium_large' ); ?>
								<?php else : ?>
									<img src="<?php echo esc_url( get_template_directory_uri() . '/assets/images/placeholder.jpg' ); ?>" alt="<?php the_title_attribute(); ?>">
								<?php endif; ?>
							</a>
							<div class="product-info">
								<span class="product-brand-tag">
									<?php
									$terms = get_the_terms( get_the_ID(), 'product_brand' );
									if ( ! empty( $terms ) && ! is_wp_error( $terms ) ) {
										echo esc_html( $terms[0]->name );
									}
									?>
								</span>
								<h3 class="product-title"><a href="<?php the_permalink(); ?>"><?php the_title(); ?></a></h3>
								<p class="product-excerpt"><?php echo wp_trim_words( get_the_excerpt(), 15 ); ?></p>
								<a href="<?php the_permalink(); ?>" class="view-detail-btn">Xem chi tiết</a>
							</div>
						</div>
					<?php endwhile; ?>
				</div>
				<?php the_posts_pagination(); ?>
			<?php else : ?>
				<div class="no-products">
					<p>Không tìm thấy sản phẩm nào khớp với bộ lọc đã chọn.</p>
				</div>
			<?php endif; ?>
		</div>
	</div>
</main>

<?php get_footer(); ?>
