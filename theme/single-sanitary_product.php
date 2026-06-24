<?php get_header(); ?>

<main class="site-main container">
	<?php if ( have_posts() ) : while ( have_posts() ) : the_post(); ?>
		
		<!-- Breadcrumbs -->
		<div class="product-breadcrumbs">
			<a href="<?php echo esc_url( home_url( '/' ) ); ?>">Trang chủ</a> &raquo; 
			<a href="<?php echo esc_url( get_post_type_archive_link( 'sanitary_product' ) ); ?>">Sản phẩm</a> &raquo; 
			<?php
			$cats = get_the_terms( get_the_ID(), 'product_cat' );
			if ( ! empty( $cats ) && ! is_wp_error( $cats ) ) {
				echo '<a href="' . esc_url( get_term_link( $cats[0] ) ) . '">' . esc_html( $cats[0]->name ) . '</a> &raquo; ';
			}
			?>
			<span><?php the_title(); ?></span>
		</div>

		<?php
		$gallery_json = get_post_meta( get_the_ID(), '_sanitary_product_gallery', true );
		$gallery_images = ! empty( $gallery_json ) ? json_decode( $gallery_json, true ) : [];
		if ( ! is_array( $gallery_images ) ) {
			$gallery_images = [];
		}
		$code = get_post_meta( get_the_ID(), '_sanitary_product_code', true );
		$material = get_post_meta( get_the_ID(), '_sanitary_product_material', true );
		$size = get_post_meta( get_the_ID(), '_sanitary_product_size', true );
		$warranty = get_post_meta( get_the_ID(), '_sanitary_product_warranty', true );
		?>
		<div class="product-detail-layout">
			<div class="product-gallery">
				<div class="main-image-wrapper" style="border: 1px solid var(--color-border); border-radius: 8px; overflow: hidden; background: #fff;">
					<?php if ( has_post_thumbnail() ) : ?>
						<?php the_post_thumbnail( 'large', [ 'class' => 'featured-product-image', 'id' => 'main-product-img', 'style' => 'width:100%; height:auto; display:block; cursor:zoom-in;' ] ); ?>
					<?php else : ?>
						<img src="<?php echo esc_url( get_template_directory_uri() . '/assets/images/placeholder.jpg' ); ?>" class="featured-product-image" id="main-product-img" style="width:100%; height:auto; display:block;" alt="<?php the_title_attribute(); ?>">
					<?php endif; ?>
				</div>
				<?php if ( ! empty( $gallery_images ) ) : ?>
					<div class="product-gallery-thumbnails" style="display: flex; gap: 10px; margin-top: 15px; overflow-x: auto; padding-bottom: 5px;">
						<?php 
						$featured_img_url = get_the_post_thumbnail_url( get_the_ID(), 'large' );
						if ( $featured_img_url ) :
						?>
							<div class="thumb-item active" style="width: 70px; height: 70px; border: 2px solid var(--color-accent); border-radius: 4px; overflow: hidden; cursor: pointer; flex-shrink: 0;">
								<img src="<?php echo esc_url( $featured_img_url ); ?>" style="width: 100%; height: 100%; object-fit: cover;" class="gallery-thumb-trigger" />
							</div>
						<?php endif; ?>
						<?php foreach ( $gallery_images as $img_url ) : ?>
							<div class="thumb-item" style="width: 70px; height: 70px; border: 1px solid var(--color-border); border-radius: 4px; overflow: hidden; cursor: pointer; flex-shrink: 0; transition: border 0.2s ease;">
								<img src="<?php echo esc_url( $img_url ); ?>" style="width: 100%; height: 100%; object-fit: cover;" class="gallery-thumb-trigger" />
							</div>
						<?php endforeach; ?>
					</div>
				<?php endif; ?>
			</div>

			<div class="product-summary">
				<div class="product-meta-header">
					<span class="product-brand-label">Hãng sản xuất: </span>
					<span class="product-brand-value">
						<?php
						$terms = get_the_terms( get_the_ID(), 'product_brand' );
						if ( ! empty( $terms ) && ! is_wp_error( $terms ) ) {
							$brand_links = [];
							foreach ( $terms as $term ) {
								$brand_links[] = '<a href="' . esc_url( get_term_link( $term ) ) . '">' . esc_html( $term->name ) . '</a>';
							}
							echo implode( ', ', $brand_links );
						} else {
							echo 'Chưa phân loại';
						}
						?>
					</span>
				</div>

				<h1 class="product-detail-title"><?php the_title(); ?></h1>
				
				<div class="product-detail-excerpt">
					<?php the_excerpt(); ?>
				</div>

				<!-- Specs Sheet -->
				<?php if ( ! empty( $code ) || ! empty( $material ) || ! empty( $size ) || ! empty( $warranty ) ) : ?>
					<div class="product-specs-table-wrapper" style="margin-bottom: 25px;">
						<h3 style="font-size: 1.1rem; font-weight: 700; margin-bottom: 12px; color: var(--color-primary);">Thông Số Kỹ Thuật</h3>
						<table class="product-specs-table" style="width: 100%; border-collapse: collapse; font-size: 0.9rem;">
							<tbody>
								<?php if ( ! empty( $code ) ) : ?>
									<tr>
										<td style="padding: 10px 12px; border: 1px solid var(--color-border); font-weight: 600; background-color: var(--color-bg-alt); width: 35%; color: var(--color-text);">Mã sản phẩm (SKU)</td>
										<td style="padding: 10px 12px; border: 1px solid var(--color-border); color: var(--color-text);"><?php echo esc_html( $code ); ?></td>
									</tr>
								<?php endif; ?>
								<?php if ( ! empty( $material ) ) : ?>
									<tr>
										<td style="padding: 10px 12px; border: 1px solid var(--color-border); font-weight: 600; background-color: var(--color-bg-alt); color: var(--color-text);">Chất liệu</td>
										<td style="padding: 10px 12px; border: 1px solid var(--color-border); color: var(--color-text);"><?php echo esc_html( $material ); ?></td>
									</tr>
								<?php endif; ?>
								<?php if ( ! empty( $size ) ) : ?>
									<tr>
										<td style="padding: 10px 12px; border: 1px solid var(--color-border); font-weight: 600; background-color: var(--color-bg-alt); color: var(--color-text);">Kích thước</td>
										<td style="padding: 10px 12px; border: 1px solid var(--color-border); color: var(--color-text);"><?php echo esc_html( $size ); ?></td>
									</tr>
								<?php endif; ?>
								<?php if ( ! empty( $warranty ) ) : ?>
									<tr>
										<td style="padding: 10px 12px; border: 1px solid var(--color-border); font-weight: 600; background-color: var(--color-bg-alt); color: var(--color-text);">Bảo hành</td>
										<td style="padding: 10px 12px; border: 1px solid var(--color-border); color: var(--color-text);"><?php echo esc_html( $warranty ); ?></td>
									</tr>
								<?php endif; ?>
							</tbody>
						</table>
					</div>
				<?php endif; ?>

				<!-- Commitments / Badges inside Flatsome style product page -->
				<div class="product-detail-badges">
					<div class="badge-item">
						<span class="badge-icon">✓</span>
						<span>100% Chính hãng</span>
					</div>
					<div class="badge-item">
						<span class="badge-icon">✓</span>
						<span>Bảo hành dài hạn</span>
					</div>
					<div class="badge-item">
						<span class="badge-icon">✓</span>
						<span>Khảo sát tận nơi</span>
					</div>
				</div>

				<div class="product-cta-box">
					<h3>Nhận Báo Giá & Tư Vấn Lắp Đặt</h3>
					<p>Sản phẩm chính hãng, hỗ trợ khảo sát tại công trình và lắp đặt hoàn thiện trọn gói bởi kỹ thuật viên kinh nghiệm.</p>
					<?php
					$hotline = get_theme_mod( 'sanitary_hotline', '090 123 4567' );
					$hotline_tel = get_theme_mod( 'sanitary_hotline_tel', '0901234567' );
					$zalo_url = get_theme_mod( 'sanitary_zalo_url', 'https://zalo.me/0901234567' );
					$zalo_text = sprintf( 'Tôi muốn nhận báo giá sản phẩm %s%s', get_the_title(), ! empty( $code ) ? ' (Mã: ' . $code . ')' : '' );
					$zalo_link = add_query_arg( 'text', urlencode( $zalo_text ), $zalo_url );
					?>
					<div class="cta-actions">
						<button type="button" id="btn-open-inquiry-modal" class="btn btn-zalo">Yêu cầu báo giá</button>
						<a href="tel:<?php echo esc_attr( $hotline_tel ); ?>" class="btn btn-phone">Gọi ngay: <?php echo esc_html( $hotline ); ?></a>
					</div>
				</div>
			</div>

			<div class="product-description-tabs">
				<h2 class="tab-title">Thông tin chi tiết sản phẩm</h2>
				<div class="tab-content entry-content">
					<?php the_content(); ?>
				</div>
			</div>
		</div>

		<script>
		document.addEventListener('DOMContentLoaded', function() {
			var mainImg = document.getElementById('main-product-img');
			var thumbs = document.querySelectorAll('.gallery-thumb-trigger');
			var thumbContainers = document.querySelectorAll('.product-gallery-thumbnails .thumb-item');

			thumbs.forEach(function(thumb) {
				thumb.addEventListener('click', function() {
					if (mainImg) {
						mainImg.src = this.src;
						// Update active class
						thumbContainers.forEach(function(container) {
							container.style.border = '1px solid var(--color-border)';
						});
						this.parentElement.style.border = '2px solid var(--color-accent)';
					}
				});
			});
		});
		</script>


		<!-- Related Products Section -->
		<?php
		// Fetch related products by brand or category
		$related_args = [
			'post_type'      => 'sanitary_product',
			'posts_per_page' => 4,
			'post__not_in'   => [ get_the_ID() ],
			'tax_query'      => [],
		];

		// Try to match by Brand first, then Category
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
			<div class="related-products-section">
				<h2 class="related-title">Sản Phẩm Liên Quan</h2>
				<div class="products-grid">
					<?php while ( $related_query->have_posts() ) : $related_query->the_post(); ?>
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
									$r_terms = get_the_terms( get_the_ID(), 'product_brand' );
									if ( ! empty( $r_terms ) && ! is_wp_error( $r_terms ) ) {
										echo esc_html( $r_terms[0]->name );
									}
									?>
								</span>
								<h3 class="product-title"><a href="<?php the_permalink(); ?>"><?php the_title(); ?></a></h3>
								<p class="product-excerpt"><?php echo wp_trim_words( get_the_excerpt(), 15 ); ?></p>
								<a href="<?php the_permalink(); ?>" class="view-detail-btn">Xem chi tiết</a>
							</div>
						</div>
					<?php endwhile; wp_reset_postdata(); ?>
				</div>
			</div>
		<?php endif; ?>

	<?php endwhile; endif; ?>
</main>

<!-- Inquiry Modal Overlay -->
<div id="sanitary-inquiry-modal" class="sanitary-modal-overlay" style="display: none;">
	<div class="sanitary-modal-container">
		<button type="button" class="sanitary-modal-close">&times;</button>
		<div class="sanitary-modal-header">
			<h3>Yêu Cầu Báo Giá</h3>
			<p class="product-inquiry-name">Sản phẩm: <strong><?php the_title(); ?><?php echo ! empty( $code ) ? ' (Mã: ' . esc_html( $code ) . ')' : ''; ?></strong></p>
		</div>
		<form id="sanitary-inquiry-form" method="POST">
			<?php wp_nonce_field( 'sanitary_inquiry_nonce', 'nonce' ); ?>
			
			<!-- Honeypot -->
			<div class="sp-field-honey" style="display:none !important; visibility:hidden !important;">
				<input type="text" name="sp_honeypot" id="sp_honeypot" tabindex="-1" autocomplete="off" />
			</div>

			<!-- Product Info Hidden Fields -->
			<input type="hidden" name="product_id" value="<?php the_ID(); ?>" />
			<input type="hidden" name="product_name" value="<?php echo esc_attr( get_the_title() . ( ! empty( $code ) ? ' (' . $code . ')' : '' ) ); ?>" />
			<input type="hidden" name="action" value="sanitary_submit_inquiry" />

			<div class="form-group">
				<label for="inquiry_fullname">Họ tên của bạn <span class="required">*</span></label>
				<input type="text" name="fullname" id="inquiry_fullname" required placeholder="Ví dụ: Nguyễn Văn A" />
			</div>

			<div class="form-group">
				<label for="inquiry_phone">Số điện thoại <span class="required">*</span></label>
				<input type="tel" name="phone" id="inquiry_phone" required placeholder="Ví dụ: 0912345678" />
				<span class="error-msg" id="phone-error" style="display: none; color: #ef4444; font-size: 0.85rem; margin-top: 4px;">Số điện thoại chưa hợp lệ. Vui lòng nhập số điện thoại Việt Nam 10 chữ số.</span>
			</div>

			<div class="form-group">
				<label for="inquiry_message">Yêu cầu chi tiết (Không bắt buộc)</label>
				<textarea name="message" id="inquiry_message" rows="3" placeholder="Ví dụ: Cần tư vấn lắp đặt tại công trình quận 2..."></textarea>
			</div>

			<div class="form-message" id="inquiry-form-response" style="display: none; padding: 10px; border-radius: 4px; margin-bottom: 15px; font-size: 0.9rem;"></div>

			<button type="submit" class="btn-submit-inquiry">
				<span class="btn-text">Gửi Yêu Cầu</span>
				<span class="btn-spinner" style="display: none;">Đang gửi...</span>
			</button>
		</form>
	</div>
</div>

<script>
document.addEventListener('DOMContentLoaded', function() {
	var modal = document.getElementById('sanitary-inquiry-modal');
	var openBtn = document.getElementById('btn-open-inquiry-modal');
	var form = document.getElementById('sanitary-inquiry-form');
	var responseBox = document.getElementById('inquiry-form-response');
	var phoneInput = document.getElementById('inquiry_phone');
	var phoneError = document.getElementById('phone-error');
	
	if (!modal || !openBtn || !form) return;

	var submitBtn = form.querySelector('.btn-submit-inquiry');
	var btnText = submitBtn ? submitBtn.querySelector('.btn-text') : null;
	var btnSpinner = submitBtn ? submitBtn.querySelector('.btn-spinner') : null;

	// Open modal
	openBtn.addEventListener('click', function(e) {
		e.preventDefault();
		modal.style.display = 'flex';
		document.body.classList.add('modal-open');
	});

	// Close modal on overlay click
	modal.addEventListener('click', function(e) {
		if (e.target === modal || e.target.classList.contains('sanitary-modal-close')) {
			modal.style.display = 'none';
			document.body.classList.remove('modal-open');
		}
	});

	// Close button click
	var closeBtn = modal.querySelector('.sanitary-modal-close');
	if (closeBtn) {
		closeBtn.addEventListener('click', function(e) {
			e.preventDefault();
			modal.style.display = 'none';
			document.body.classList.remove('modal-open');
		});
	}

	// Validate phone client-side on input
	if (phoneInput) {
		phoneInput.addEventListener('input', function() {
			var phoneVal = this.value.trim();
			var phoneRegex = /^(03|05|07|08|09)\d{8}$/;
			if (phoneVal === '' || phoneRegex.test(phoneVal)) {
				phoneError.style.display = 'none';
			} else {
				phoneError.style.display = 'block';
			}
		});
	}

	// Handle form submission
	form.addEventListener('submit', function(e) {
		e.preventDefault();
		if (phoneError) phoneError.style.display = 'none';
		if (responseBox) {
			responseBox.style.display = 'none';
			responseBox.className = 'form-message';
			responseBox.style.backgroundColor = '';
			responseBox.style.color = '';
			responseBox.style.border = '';
		}

		var fullname = document.getElementById('inquiry_fullname').value.trim();
		var phone = phoneInput.value.trim();
		var phoneRegex = /^(03|05|07|08|09)\d{8}$/;

		// Validate
		if (fullname === '') {
			alert('Vui lòng nhập Họ tên.');
			return;
		}

		if (phone === '') {
			alert('Vui lòng nhập Số điện thoại.');
			return;
		}

		if (!phoneRegex.test(phone)) {
			if (phoneError) phoneError.style.display = 'block';
			phoneInput.focus();
			return;
		}

		// Disable submit & show spinner
		if (submitBtn) submitBtn.disabled = true;
		if (btnText) btnText.style.display = 'none';
		if (btnSpinner) btnSpinner.style.display = 'inline';

		// Prepare form data
		var formData = new FormData(form);

		// AJAX Request using fetch
		fetch('<?php echo esc_url( admin_url( 'admin-ajax.php' ) ); ?>', {
			method: 'POST',
			body: formData
		})
		.then(function(response) {
			return response.json();
		})
		.then(function(data) {
			if (submitBtn) submitBtn.disabled = false;
			if (btnText) btnText.style.display = 'inline';
			if (btnSpinner) btnSpinner.style.display = 'none';

			if (responseBox) {
				responseBox.style.display = 'block';
				if (data.success) {
					responseBox.classList.add('success');
					responseBox.style.backgroundColor = '#d1fae5';
					responseBox.style.color = '#065f46';
					responseBox.style.border = '1px solid #10b981';
					responseBox.innerHTML = data.data.message;
					form.reset();
					
					// Auto close after 2.5 seconds
					setTimeout(function() {
						modal.style.display = 'none';
						document.body.classList.remove('modal-open');
						responseBox.style.display = 'none';
						responseBox.classList.remove('success');
					}, 2500);
				} else {
					responseBox.classList.add('error');
					responseBox.style.backgroundColor = '#fee2e2';
					responseBox.style.color = '#991b1b';
					responseBox.style.border = '1px solid #ef4444';
					responseBox.innerHTML = data.data.message || 'Lỗi không xác định.';
				}
			}
		})
		.catch(function(err) {
			if (submitBtn) submitBtn.disabled = false;
			if (btnText) btnText.style.display = 'inline';
			if (btnSpinner) btnSpinner.style.display = 'none';
			
			if (responseBox) {
				responseBox.style.display = 'block';
				responseBox.classList.add('error');
				responseBox.style.backgroundColor = '#fee2e2';
				responseBox.style.color = '#991b1b';
				responseBox.style.border = '1px solid #ef4444';
				responseBox.innerHTML = 'Có lỗi kết nối xảy ra. Vui lòng thử lại.';
			}
		});
	});
});
</script>

<?php get_footer(); ?>

