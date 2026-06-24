<?php get_header(); ?>

<main class="site-main container">
	<header class="archive-header" style="margin-bottom: 30px; text-align: center; padding: 40px 0 20px;">
		<h1 class="archive-title" style="font-size: 2.2rem; font-weight: 800; color: #0f172a; margin-bottom: 10px;"><?php esc_html_e( 'Catalogue Sản Phẩm', 'sanitary-catalog-core' ); ?></h1>
		<p class="taxonomy-description" style="color: #475569; max-width: 600px; margin: 0 auto;"><?php esc_html_e( 'Tổng hợp đầy đủ các sản phẩm thiết bị vệ sinh cao cấp chính hãng từ các thương hiệu đối tác.', 'sanitary-catalog-core' ); ?></p>
	</header>

	<div class="catalog-layout" style="display: flex; gap: 40px; margin-bottom: 60px; align-items: flex-start;">
		<?php echo do_shortcode( '[sanitary_product_filter]' ); ?>
		
		<div class="catalog-content" style="flex-grow: 1;">
			<?php if ( have_posts() ) : ?>
				<div class="products-grid" style="display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 25px;">
					<?php while ( have_posts() ) : the_post(); ?>
						<div class="product-card" style="background: #fff; border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden; box-shadow: 0 4px 6px rgba(0,0,0,0.02); transition: all 0.3s ease;">
							<a href="<?php the_permalink(); ?>" class="product-img-link" style="display: block; position: relative; padding-bottom: 100%; overflow: hidden; background: #f8fafc;">
								<?php if ( has_post_thumbnail() ) : ?>
									<?php the_post_thumbnail( 'medium_large', [ 'style' => 'position: absolute; top:0; left:0; width:100%; height:100%; object-fit: cover;' ] ); ?>
								<?php else : ?>
									<img src='data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" width="600" height="400" viewBox="0 0 600 400"><rect width="100%" height="100%" fill="%23f1f5f9"/><text x="50%" y="50%" dominant-baseline="middle" text-anchor="middle" font-family="sans-serif" font-size="20" fill="%2394a3b8">Hồng Miên</text></svg>' style="position: absolute; top:0; left:0; width:100%; height:100%; object-fit: cover;" alt="<?php the_title_attribute(); ?>">
								<?php endif; ?>
							</a>
							<div class="product-info" style="padding: 15px;">
								<span class="product-brand-tag" style="display: inline-block; font-size: 0.7rem; font-weight: 700; text-transform: uppercase; color: #d97706; margin-bottom: 5px;">
									<?php
									$terms = get_the_terms( get_the_ID(), 'product_brand' );
									if ( ! empty( $terms ) && ! is_wp_error( $terms ) ) {
										echo esc_html( $terms[0]->name );
									}
									?>
								</span>
								<h3 class="product-title" style="font-size: 0.95rem; font-weight: 700; line-height: 1.4; margin: 0 0 8px 0; height: 42px; overflow: hidden; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical;">
									<a href="<?php the_permalink(); ?>" style="color: #0f172a; text-decoration: none;"><?php the_title(); ?></a>
								</h3>
								<p class="product-excerpt" style="font-size: 0.8rem; color: #64748b; margin-bottom: 12px; height: 36px; overflow: hidden; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical;"><?php echo wp_trim_words( get_the_excerpt(), 10 ); ?></p>
								<a href="<?php the_permalink(); ?>" class="view-detail-btn" style="display: block; text-align: center; background: #0f172a; color: #fff; padding: 8px 0; border-radius: 4px; font-size: 0.8rem; font-weight: 600; text-decoration: none;"><?php esc_html_e( 'Xem chi tiết', 'sanitary-catalog-core' ); ?></a>
							</div>
						</div>
					<?php endwhile; ?>
				</div>
				<div style="margin-top: 40px;">
					<?php the_posts_pagination(); ?>
				</div>
			<?php else : ?>
				<div class="no-products" style="text-align: center; padding: 60px 20px; border: 1px dashed #cbd5e1; border-radius: 8px; background: #f8fafc;">
					<p style="color: #64748b; font-size: 1rem; margin: 0;"><?php esc_html_e( 'Không tìm thấy sản phẩm nào khớp với bộ lọc đã chọn.', 'sanitary-catalog-core' ); ?></p>
				</div>
			<?php endif; ?>
		</div>
	</div>
</main>

<?php get_footer(); ?>
