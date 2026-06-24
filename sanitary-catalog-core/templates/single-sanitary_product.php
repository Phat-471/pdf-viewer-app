<?php get_header(); ?>

<main class="site-main container" style="margin-top: 30px; margin-bottom: 60px;">
	<?php if ( have_posts() ) : while ( have_posts() ) : the_post(); ?>
		
		<!-- Breadcrumbs -->
		<div class="product-breadcrumbs" style="font-size: 0.85rem; color: #64748b; margin-bottom: 30px;">
			<a href="<?php echo esc_url( home_url( '/' ) ); ?>" style="color: #64748b; text-decoration: none;">Trang chủ</a> &raquo; 
			<a href="<?php echo esc_url( get_post_type_archive_link( 'sanitary_product' ) ); ?>" style="color: #64748b; text-decoration: none;">Sản phẩm</a> &raquo; 
			<?php
			$cats = get_the_terms( get_the_ID(), 'product_cat' );
			if ( ! empty( $cats ) && ! is_wp_error( $cats ) ) {
				echo '<a href="' . esc_url( get_term_link( $cats[0] ) ) . '" style="color: #64748b; text-decoration: none;">' . esc_html( $cats[0]->name ) . '</a> &raquo; ';
			}
			?>
			<span style="color: #0f172a; font-weight: 500;"><?php the_title(); ?></span>
		</div>

		<div class="product-detail-layout" style="display: grid; grid-template-columns: 1fr 1fr; gap: 40px; margin-bottom: 40px;">
			<div class="product-gallery">
				<?php if ( has_post_thumbnail() ) : ?>
					<?php the_post_thumbnail( 'large', [ 'class' => 'featured-product-image', 'style' => 'width: 100%; border-radius: 8px; border: 1px solid #e2e8f0; object-fit: cover;' ] ); ?>
				<?php else : ?>
					<img src='data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" width="600" height="400" viewBox="0 0 600 400"><rect width="100%" height="100%" fill="%23f1f5f9"/><text x="50%" y="50%" dominant-baseline="middle" text-anchor="middle" font-family="sans-serif" font-size="20" fill="%2394a3b8">Hồng Miên</text></svg>' class="featured-product-image" style="width: 100%; border-radius: 8px; border: 1px solid #e2e8f0; object-fit: cover;" alt="<?php the_title_attribute(); ?>">
				<?php endif; ?>
			</div>

			<div class="product-summary">
				<div class="product-meta-header" style="margin-bottom: 15px;">
					<span class="product-brand-label" style="font-size: 0.85rem; color: #64748b;">Hãng sản xuất: </span>
					<span class="product-brand-value" style="font-weight: 700; color: #d97706;">
						<?php
						$terms = get_the_terms( get_the_ID(), 'product_brand' );
						if ( ! empty( $terms ) && ! is_wp_error( $terms ) ) {
							$brand_links = [];
							foreach ( $terms as $term ) {
								$brand_links[] = '<a href="' . esc_url( get_term_link( $term ) ) . '" style="color: #d97706; text-decoration: none;">' . esc_html( $term->name ) . '</a>';
							}
							echo implode( ', ', $brand_links );
						} else {
							echo 'Chưa phân loại';
						}
						?>
					</span>
				</div>

				<h1 class="product-detail-title" style="font-size: 2rem; font-weight: 800; color: #0f172a; margin-bottom: 20px; line-height: 1.2;"><?php the_title(); ?></h1>
				
				<div class="product-detail-excerpt" style="color: #475569; font-size: 0.95rem; margin-bottom: 25px; line-height: 1.6;">
					<?php the_excerpt(); ?>
				</div>

				<!-- Commitments / Badges -->
				<div class="product-detail-badges" style="display: flex; gap: 15px; margin-bottom: 30px;">
					<div class="badge-item" style="display: flex; align-items: center; gap: 5px; font-size: 0.8rem; font-weight: 600; color: #16a34a; background: #f0fdf4; padding: 5px 10px; border-radius: 4px;">
						<span class="badge-icon">✓</span>
						<span>100% Chính hãng</span>
					</div>
					<div class="badge-item" style="display: flex; align-items: center; gap: 5px; font-size: 0.8rem; font-weight: 600; color: #16a34a; background: #f0fdf4; padding: 5px 10px; border-radius: 4px;">
						<span class="badge-icon">✓</span>
						<span>Bảo hành dài hạn</span>
					</div>
					<div class="badge-item" style="display: flex; align-items: center; gap: 5px; font-size: 0.8rem; font-weight: 600; color: #16a34a; background: #f0fdf4; padding: 5px 10px; border-radius: 4px;">
						<span class="badge-icon">✓</span>
						<span>Khảo sát tận nơi</span>
					</div>
				</div>

				<div class="product-cta-box" style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 25px; margin-top: 30px;">
					<h3 style="font-size: 1.1rem; font-weight: 700; color: #0f172a; margin-bottom: 10px;">Nhận Báo Giá & Tư Vấn Lắp Đặt</h3>
					<p style="font-size: 0.85rem; color: #475569; margin-bottom: 20px; line-height: 1.5;">Sản phẩm chính hãng, hỗ trợ khảo sát tại công trình và lắp đặt hoàn thiện trọn gói bởi kỹ thuật viên kinh nghiệm.</p>
					<?php
					$hotline = get_theme_mod( 'sanitary_hotline', '090 123 4567' );
					$hotline_tel = get_theme_mod( 'sanitary_hotline_tel', '0901234567' );
					$zalo_url = get_theme_mod( 'sanitary_zalo_url', 'https://zalo.me/0901234567' );
					?>
					<div class="cta-actions" style="display: flex; gap: 15px; flex-wrap: wrap;">
						<a href="<?php echo esc_url( $zalo_url ); ?>" target="_blank" rel="noopener noreferrer" class="btn btn-zalo" style="display: inline-flex; align-items: center; justify-content: center; padding: 12px 20px; background: #0068ff; color: #fff; font-weight: 700; border-radius: 6px; text-decoration: none; font-size: 0.9rem;">Liên hệ Zalo báo giá</a>
						<a href="tel:<?php echo esc_attr( $hotline_tel ); ?>" class="btn btn-phone" style="display: inline-flex; align-items: center; justify-content: center; padding: 12px 20px; background: #22c55e; color: #fff; font-weight: 700; border-radius: 6px; text-decoration: none; font-size: 0.9rem;">Gọi ngay: <?php echo esc_html( $hotline ); ?></a>
					</div>
				</div>
			</div>
		</div>

		<div class="product-description-tabs" style="border-top: 1px solid #e2e8f0; padding-top: 30px; margin-top: 40px;">
			<h2 class="tab-title" style="font-size: 1.3rem; font-weight: 800; color: #0f172a; margin-bottom: 20px;">Thông tin chi tiết sản phẩm</h2>
			<div class="tab-content entry-content" style="color: #334155; line-height: 1.8; font-size: 0.95rem;">
				<?php the_content(); ?>
			</div>
		</div>

		<!-- Related Products Section -->
		<?php
		$related_args = [
			'post_type'      => 'sanitary_product',
			'posts_per_page' => 4,
			'post__not_in'   => [ get_the_ID() ],
			'tax_query'      => [],
		];

		$brand_term_ids = [];
		if ( ! empty( $terms ) && ! is_wp_error( $terms ) ) {
			foreach ( $terms as $term ) {
				$brand_term_ids[] = $term->term_id;
			}
		}

		if ( ! empty( $brand_term_ids ) ) {
			$related_args['tax_query'][] = [
				'taxonomy' => 'product_brand',
				'field'    => 'term_id',
				'terms'    => $brand_term_ids,
			];
		} else if ( ! empty( $cats ) && ! is_wp_error( $cats ) ) {
			$related_args['tax_query'][] = [
				'taxonomy' => 'product_cat',
				'field'    => 'term_id',
				'terms'    => [ $cats[0]->term_id ],
			];
		}

		$related_query = new WP_Query( $related_args );

		if ( $related_query->have_posts() ) :
		?>
			<div class="related-products-section" style="margin-top: 60px; padding-top: 40px; border-top: 1px solid #e2e8f0;">
				<h2 class="related-title" style="font-size: 1.5rem; font-weight: 800; color: #0f172a; margin-bottom: 25px;">Sản Phẩm Liên Quan</h2>
				<div class="products-grid" style="display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 25px;">
					<?php while ( $related_query->have_posts() ) : $related_query->the_post(); ?>
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
									$r_terms = get_the_terms( get_the_ID(), 'product_brand' );
									if ( ! empty( $r_terms ) && ! is_wp_error( $r_terms ) ) {
										echo esc_html( $r_terms[0]->name );
									}
									?>
								</span>
								<h3 class="product-title" style="font-size: 0.95rem; font-weight: 700; line-height: 1.4; margin: 0 0 8px 0; height: 42px; overflow: hidden; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical;">
									<a href="<?php the_permalink(); ?>" style="color: #0f172a; text-decoration: none;"><?php the_title(); ?></a>
								</h3>
								<p class="product-excerpt" style="font-size: 0.8rem; color: #64748b; margin-bottom: 12px; height: 36px; overflow: hidden; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical;"><?php echo wp_trim_words( get_the_excerpt(), 10 ); ?></p>
								<a href="<?php the_permalink(); ?>" class="view-detail-btn" style="display: block; text-align: center; background: #0f172a; color: #fff; padding: 8px 0; border-radius: 4px; font-size: 0.8rem; font-weight: 600; text-decoration: none;">Xem chi tiết</a>
							</div>
						</div>
					<?php endwhile; wp_reset_postdata(); ?>
				</div>
			</div>
		<?php endif; ?>

	<?php endwhile; endif; ?>
</main>

<?php get_footer(); ?>
