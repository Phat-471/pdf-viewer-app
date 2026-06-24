<?php get_header(); ?>

<?php
// Fetch layouts configuration
$sections = [
	'hero_banner' => ['order' => get_theme_mod('sanitary_order_hero_banner', 1), 'visible' => get_theme_mod('sanitary_visible_hero_banner', 1)],
	'commitment_strip' => ['order' => get_theme_mod('sanitary_order_commitment_strip', 2), 'visible' => get_theme_mod('sanitary_visible_commitment_strip', 1)],
	'promotions' => ['order' => get_theme_mod('sanitary_order_promotions', 3), 'visible' => get_theme_mod('sanitary_visible_promotions', 1)],
	'services' => ['order' => get_theme_mod('sanitary_order_services', 4), 'visible' => get_theme_mod('sanitary_visible_services', 1)],
	'category_products' => ['order' => get_theme_mod('sanitary_order_category_products', 5), 'visible' => get_theme_mod('sanitary_visible_category_products', 1)],
	'latest_products' => ['order' => get_theme_mod('sanitary_order_latest_products', 6), 'visible' => get_theme_mod('sanitary_visible_latest_products', 1)],
	'brands' => ['order' => get_theme_mod('sanitary_order_brands', 7), 'visible' => get_theme_mod('sanitary_visible_brands', 1)],
	'projects' => ['order' => get_theme_mod('sanitary_order_projects', 8), 'visible' => get_theme_mod('sanitary_visible_projects', 1)],
];

// Sort sections by order
uasort($sections, function ($a, $b) {
	return intval($a['order']) <=> intval($b['order']);
});

// Zalo URL helper
$zalo_url = get_theme_mod('sanitary_zalo_url', 'https://zalo.me/0901234567');
?>

<main class="site-main">
	<?php
	foreach ($sections as $section_key => $section_data) {
		if (!$section_data['visible']) {
			continue;
		}

		switch ($section_key) {
			case 'hero_banner':
				$slides_json = get_theme_mod('sanitary_slides');
				$slides = !empty($slides_json) ? json_decode($slides_json, true) : [];
				if (empty($slides) || !is_array($slides)) {
					// Fallback to defaults or legacy migration
					$slides = [];
					for ($i = 1; $i <= 3; $i++) {
						$s_title = get_theme_mod('sanitary_slide_title_' . $i);
						if (empty($s_title) && $i === 1) {
							$s_title = 'THIẾT BỊ VỆ SINH CAO CẤP & THI CÔNG TRỌN GÓI';
						}
						if (!empty($s_title)) {
							$slides[] = [
								'title' => $s_title,
								'desc' => get_theme_mod('sanitary_slide_desc_' . $i, $i === 1 ? 'Giải pháp phòng tắm hoàn hảo từ thiết kế, thi công đến lắp đặt thiết bị chính hãng từ 6 thương hiệu hàng đầu.' : ''),
								'btn1_text' => get_theme_mod('sanitary_slide_btn1_text_' . $i, $i === 1 ? 'Nhận báo giá qua Zalo' : ''),
								'btn1_url' => get_theme_mod('sanitary_slide_btn1_url_' . $i, $i === 1 ? 'https://zalo.me/0901234567' : ''),
								'btn2_text' => get_theme_mod('sanitary_slide_btn2_text_' . $i, $i === 1 ? 'Xem các hãng liên kết' : ''),
								'btn2_url' => get_theme_mod('sanitary_slide_btn2_url_' . $i, $i === 1 ? '#brands' : ''),
								'bg' => get_theme_mod('sanitary_slide_bg_' . $i),
							];
						}
					}
					if (empty($slides)) {
						$slides[] = [
							'title' => 'THIẾT BỊ VỆ SINH CAO CẤP & THI CÔNG TRỌN GÓI',
							'desc' => 'Giải pháp phòng tắm hoàn hảo từ thiết kế, thi công đến lắp đặt thiết bị chính hãng từ 6 thương hiệu hàng đầu.',
							'btn1_text' => 'Nhận báo giá qua Zalo',
							'btn1_url' => 'https://zalo.me/0901234567',
							'btn2_text' => 'Xem các hãng liên kết',
							'btn2_url' => '#brands',
							'bg' => ''
						];
					}
				}

				// Filter visible slides only
				$slides = array_filter($slides, function ($slide) {
					return !isset($slide['visible']) || $slide['visible'] == 1;
				});
				$slides = array_values($slides); // Reset indices
				?>
				<!-- HERO BANNER SLIDER -->
				<section class="hero-banner-slider-wrapper">
					<div class="hero-slider" id="homepage-hero-slider">
						<?php foreach ($slides as $index => $slide):
							$bg_style = !empty($slide['bg']) ? "background-image: linear-gradient(135deg, rgba(15, 23, 42, 0.7) 0%, rgba(15, 23, 42, 0.25) 100%), url('" . esc_url($slide['bg']) . "');" : "";
							$btn1_text = isset($slide['btn1_text']) ? $slide['btn1_text'] : (isset($slide['btn1']) ? $slide['btn1'] : '');
							$btn2_text = isset($slide['btn2_text']) ? $slide['btn2_text'] : (isset($slide['btn2']) ? $slide['btn2'] : '');
							?>
							<div class="hero-slide <?php echo $index === 0 ? 'active' : ''; ?>"
								style="<?php echo esc_attr($bg_style); ?>">
								<div class="hero-content">
									<h1><?php echo esc_html($slide['title']); ?></h1>
									<p><?php echo esc_html($slide['desc']); ?></p>
									<div class="hero-buttons">
										<?php if (!empty($btn1_text)): ?>
											<a href="<?php echo esc_url($slide['btn1_url']); ?>" target="_blank" rel="noopener noreferrer"
												class="btn btn-accent"><?php echo esc_html($btn1_text); ?></a>
										<?php endif; ?>
										<?php if (!empty($btn2_text)): ?>
											<a href="<?php echo esc_url($slide['btn2_url']); ?>"
												class="btn btn-secondary"><?php echo esc_html($btn2_text); ?></a>
										<?php endif; ?>
									</div>
								</div>
							</div>
						<?php endforeach; ?>
					</div>
					<?php if (count($slides) > 1): ?>
						<button class="slider-arrow prev" id="slider-prev">&lt;</button>
						<button class="slider-arrow next" id="slider-next">&gt;</button>
						<div class="slider-dots" id="slider-dots">
							<?php foreach ($slides as $index => $slide): ?>
								<span class="slider-dot <?php echo $index === 0 ? 'active' : ''; ?>"
									data-slide="<?php echo $index; ?>"></span>
							<?php endforeach; ?>
						</div>
					<?php endif; ?>
				</section>
				<?php
				break;

			case 'commitment_strip':
				$commitments_json = get_theme_mod( 'sanitary_commitments' );
				$commitments = ! empty( $commitments_json ) ? json_decode( $commitments_json, true ) : [];
				if ( empty( $commitments ) || ! is_array( $commitments ) ) {
					$commit_defaults = [
						1 => [ 'icon' => '🛡️', 'title' => 'Cam Kết Chính Hãng', 'desc' => 'Đền 200% nếu phát hiện hàng nhái' ],
						2 => [ 'icon' => '🚚', 'title' => 'Vận Chuyển Toàn Quốc', 'desc' => 'Giao hàng tận nơi nhanh chóng' ],
						3 => [ 'icon' => '🔧', 'title' => 'Lắp Đặt Trọn Gói', 'desc' => 'Kỹ thuật viên kinh nghiệm lắp ráp' ],
						4 => [ 'icon' => '💎', 'title' => 'Bảo Hành Dài Hạn', 'desc' => 'Bảo hành chính hãng lỗi 1 đổi 1' ]
					];
					$commitments = [];
					for ( $i = 1; $i <= 4; $i++ ) {
						$commitments[] = [
							'icon'    => get_theme_mod( 'sanitary_commit' . $i . '_icon', $commit_defaults[$i]['icon'] ),
							'title'   => get_theme_mod( 'sanitary_commit' . $i . '_title', $commit_defaults[$i]['title'] ),
							'desc'    => get_theme_mod( 'sanitary_commit' . $i . '_desc', $commit_defaults[$i]['desc'] ),
							'visible' => 1
						];
					}
				}
				$commitments = array_filter( $commitments, function( $item ) {
					return ! isset( $item['visible'] ) || $item['visible'] == 1;
				} );
				?>
				<!-- 2. COMMITMENT STRIP -->
				<section class="commitment-strip">
					<div class="container commitment-grid">
						<?php foreach ( $commitments as $item ) : ?>
							<div class="commitment-item">
								<span class="commitment-icon"><?php echo esc_html( $item['icon'] ); ?></span>
								<div>
									<h4><?php echo esc_html( $item['title'] ); ?></h4>
									<p><?php echo esc_html( $item['desc'] ); ?></p>
								</div>
							</div>
						<?php endforeach; ?>
					</div>
				</section>
				<?php
				break;

			case 'promotions':
				$promotions_json = get_theme_mod( 'sanitary_promotions' );
				$promotions = ! empty( $promotions_json ) ? json_decode( $promotions_json, true ) : [];
				if ( empty( $promotions ) || ! is_array( $promotions ) ) {
					$promotions = [
						[
							'title'    => get_theme_mod( 'sanitary_promo1_title', 'COMBO PHÒNG TẮM TRỌN GÓI' ),
							'desc'     => get_theme_mod( 'sanitary_promo1_desc', 'Tiết kiệm lên đến 30% khi đặt trọn bộ thiết bị vệ sinh & thi công lắp đặt.' ),
							'btn_text' => get_theme_mod( 'sanitary_promo1_btn_text', 'Xem chi tiết' ),
							'btn_url'  => get_theme_mod( 'sanitary_promo1_btn_url', $zalo_url ),
							'bg'       => get_theme_mod( 'sanitary_promo1_bg', '' ),
							'tag'      => 'Giá Tốt Nhất',
							'visible'  => 1
						],
						[
							'title'    => get_theme_mod( 'sanitary_promo2_title', 'THIẾT BỊ VỆ SINH NHẬP KHẨU' ),
							'desc'     => get_theme_mod( 'sanitary_promo2_desc', 'Bộ sưu tập bồn cầu thông minh, sen tắm massage cao cấp từ các hãng hàng đầu.' ),
							'btn_text' => get_theme_mod( 'sanitary_promo2_btn_text', 'Liên hệ tư vấn' ),
							'btn_url'  => get_theme_mod( 'sanitary_promo2_btn_url', $zalo_url ),
							'bg'       => get_theme_mod( 'sanitary_promo2_bg', '' ),
							'tag'      => 'Luxury Series',
							'visible'  => 1
						]
					];
				}
				$promotions = array_filter( $promotions, function( $item ) {
					return ! isset( $item['visible'] ) || $item['visible'] == 1;
				} );
				?>
				<!-- 3. PROMOTION BANNER GRID -->
				<section class="promotions-section container">
					<div class="promotions-grid">
						<?php foreach ( $promotions as $item ) : 
							$p_bg = ! empty( $item['bg'] ) ? $item['bg'] : '';
							$style = ! empty( $p_bg ) ? "background-image: linear-gradient(135deg, rgba(15, 23, 42, 0.8) 0%, rgba(15, 23, 42, 0.4) 100%), url('" . esc_url( $p_bg ) . "');" : "";
						?>
							<div class="promo-banner" style="<?php echo esc_attr( $style ); ?>">
								<div class="promo-content">
									<?php if ( ! empty( $item['tag'] ) ) : ?>
										<span class="promo-tag"><?php echo esc_html( $item['tag'] ); ?></span>
									<?php endif; ?>
									<h3><?php echo esc_html( $item['title'] ); ?></h3>
									<p><?php echo esc_html( $item['desc'] ); ?></p>
									<?php if ( ! empty( $item['btn_text'] ) ) : ?>
										<a href="<?php echo esc_url( $item['btn_url'] ); ?>" class="btn btn-accent btn-sm"><?php echo esc_html( $item['btn_text'] ); ?></a>
									<?php endif; ?>
								</div>
							</div>
						<?php endforeach; ?>
					</div>
				</section>
				<?php
				break;

			case 'services':
				$services_json = get_theme_mod( 'sanitary_services' );
				$services_data = ! empty( $services_json ) ? json_decode( $services_json, true ) : [];
				if ( empty( $services_data ) || ! is_array( $services_data ) ) {
					$service_defaults = [
						1 => [ 'icon' => '✏️', 'title' => '1. THIẾT KẾ PHÒNG TẮM', 'desc' => 'Tư vấn bố trí không gian, thiết kế bản vẽ kỹ thuật 2D/3D phù hợp với phong thủy và diện tích nhà bạn.' ],
						2 => [ 'icon' => '🏗️', 'title' => '2. THI CÔNG TRỌN GÓI', 'desc' => 'Thi công đường nước, chống thấm, ốp lát gạch nền tường chuẩn kỹ thuật trước khi lắp đặt thiết bị.' ],
						3 => [ 'icon' => '🔧', 'title' => '3. LẮP ĐẶT THIỆT BỊ', 'desc' => 'Lắp ráp bồn cầu, chậu rửa, sen vòi, bồn tắm chuyên nghiệp, đảm bảo không rò rỉ, bảo hành chính hãng.' ]
					];
					$services_data = [];
					for ( $i = 1; $i <= 3; $i++ ) {
						$services_data[] = [
							'icon'    => get_theme_mod( 'sanitary_service' . $i . '_icon', $service_defaults[$i]['icon'] ),
							'title'   => get_theme_mod( 'sanitary_service' . $i . '_title', $service_defaults[$i]['title'] ),
							'desc'    => get_theme_mod( 'sanitary_service' . $i . '_desc', $service_defaults[$i]['desc'] ),
							'visible' => 1
						];
					}
				}
				$services_data = array_filter( $services_data, function( $item ) {
					return ! isset( $item['visible'] ) || $item['visible'] == 1;
				} );
				?>
				<!-- 4. SERVICES SECTION -->
				<section class="services-section container">
					<h2 class="section-title"><?php echo esc_html( get_theme_mod( 'sanitary_title_services', 'DỊCH VỤ CHUYÊN NGHIỆP' ) ); ?></h2>
					<p class="section-subtitle"><?php echo esc_html( get_theme_mod( 'sanitary_subtitle_services', 'Quy trình dịch vụ khép kín từ tư vấn, thiết kế bản vẽ đến thi công lắp đặt tại công trình.' ) ); ?></p>

					<div class="services-grid">
						<?php foreach ( $services_data as $item ) : ?>
							<div class="service-card">
								<div class="service-icon"><?php echo esc_html( $item['icon'] ); ?></div>
								<h3><?php echo esc_html( $item['title'] ); ?></h3>
								<p><?php echo esc_html( $item['desc'] ); ?></p>
							</div>
						<?php endforeach; ?>
					</div>
				</section>
				<?php
				break;

			case 'category_products':
				$categories = get_terms([
					'taxonomy' => 'product_cat',
					'hide_empty' => false,
				]);

				if (!is_wp_error($categories) && !empty($categories)):
					// Split categories into groups of 5
					$cat_chunks = array_chunk($categories, 5);
					$chunk_idx = 0;

					foreach ($cat_chunks as $chunk):
						$chunk_idx++;
						?>
						<div class="category-tabs-block container" id="cat-tabs-block-<?php echo $chunk_idx; ?>">
							<div class="category-tabs-nav-wrapper">
								<ul class="category-tabs-nav">
									<?php foreach ($chunk as $index => $cat): ?>
										<li class="category-tab-item <?php echo $index === 0 ? 'active' : ''; ?>"
											data-tab="tab-<?php echo $chunk_idx; ?>-<?php echo esc_attr($cat->slug); ?>">
											<?php echo esc_html($cat->name); ?>
										</li>
									<?php endforeach; ?>
								</ul>
							</div>

							<div class="category-tabs-content">
								<?php foreach ($chunk as $index => $cat):
									// Query 4 products
									$prod_query = new WP_Query([
										'post_type' => 'sanitary_product',
										'posts_per_page' => 4,
										'tax_query' => [
											[
												'taxonomy' => 'product_cat',
												'field' => 'term_id',
												'terms' => $cat->term_id,
											],
										],
									]);
									?>
									<div class="category-tab-pane <?php echo $index === 0 ? 'active' : ''; ?>"
										id="tab-<?php echo $chunk_idx; ?>-<?php echo esc_attr($cat->slug); ?>">
										<?php if ($prod_query->have_posts()): ?>
											<div class="products-grid">
												<?php while ($prod_query->have_posts()):
													$prod_query->the_post(); ?>
													<div class="product-card">
														<a href="<?php the_permalink(); ?>" class="product-img-link">
															<?php if (has_post_thumbnail()): ?>
																<?php the_post_thumbnail('medium_large'); ?>
															<?php else: ?>
																<img src="<?php echo esc_url(get_template_directory_uri() . '/assets/images/placeholder.jpg'); ?>"
																	alt="<?php the_title_attribute(); ?>">
															<?php endif; ?>
														</a>
														<div class="product-info">
															<span class="product-brand-tag">
																<?php
																$brands = get_the_terms(get_the_ID(), 'product_brand');
																if (!empty($brands) && !is_wp_error($brands)) {
																	echo esc_html($brands[0]->name);
																}
																?>
															</span>
															<h3 class="product-title"><a href="<?php the_permalink(); ?>"><?php the_title(); ?></a></h3>
															<p class="product-excerpt"><?php echo wp_trim_words(get_the_excerpt(), 15); ?></p>
															<a href="<?php the_permalink(); ?>" class="view-detail-btn">Xem chi tiết</a>
														</div>
													</div>
												<?php endwhile;
												wp_reset_postdata(); ?>
											</div>
											<div class="category-pane-footer">
												<a href="<?php echo esc_url(get_term_link($cat)); ?>" class="btn btn-secondary view-all-btn-centered">Xem tất cả <?php echo esc_html($cat->name); ?></a>
											</div>
										<?php else: ?>
											<div class="no-products-alert">
												<p>Chưa có sản phẩm nào thuộc danh mục <strong><?php echo esc_html($cat->name); ?></strong>.</p>
											</div>
										<?php endif; ?>
									</div>
								<?php endforeach; ?>
							</div>
						</div>
						<?php
					endforeach;
				else:
					?>
					<div class="no-categories-alert container">
						<p>Hệ thống đang chờ bạn tạo <strong>Danh mục sản phẩm</strong> và gán sản phẩm vào để hiển thị tại đây.</p>
					</div>
				<?php endif;
				break;

			case 'latest_products':
				?>
				<!-- 6. ALL PRODUCTS GRID -->
				<section class="products-section container">
					<h2 class="section-title"><?php echo esc_html( get_theme_mod( 'sanitary_title_latest', 'SẢN PHẨM MỚI NHẤT' ) ); ?></h2>
					<p class="section-subtitle"><?php echo esc_html( get_theme_mod( 'sanitary_subtitle_latest', 'Danh mục tất cả các thiết bị vệ sinh nổi bật vừa cập nhật.' ) ); ?></p>

					<?php
					$product_args = [
						'post_type' => 'sanitary_product',
						'posts_per_page' => 8,
					];
					$products_query = new WP_Query($product_args);

					if ($products_query->have_posts()):
						?>
						<div class="products-carousel-wrapper" style="position: relative; overflow: hidden; margin-top: 40px; padding: 10px 0;">
							<div class="products-carousel-track" id="latest-products-track" style="display: flex; gap: 30px; transition: transform 0.5s cubic-bezier(0.25, 1, 0.5, 1); will-change: transform;">
								<?php while ($products_query->have_posts()):
									$products_query->the_post(); ?>
									<div class="product-card carousel-slide" style="flex: 0 0 calc((100% - 90px) / 4); max-width: calc((100% - 90px) / 4);">
										<a href="<?php the_permalink(); ?>" class="product-img-link">
											<?php if (has_post_thumbnail()): ?>
												<?php the_post_thumbnail('medium_large'); ?>
											<?php else: ?>
												<img src="<?php echo esc_url(get_template_directory_uri() . '/assets/images/placeholder.jpg'); ?>"
													alt="<?php the_title_attribute(); ?>">
											<?php endif; ?>
										</a>
										<div class="product-info">
											<span class="product-brand-tag">
												<?php
												$terms = get_the_terms(get_the_ID(), 'product_brand');
												if (!empty($terms) && !is_wp_error($terms)) {
													echo esc_html($terms[0]->name);
												}
												?>
											</span>
											<h3 class="product-title"><a href="<?php the_permalink(); ?>"><?php the_title(); ?></a></h3>
											<p class="product-excerpt"><?php echo wp_trim_words(get_the_excerpt(), 15); ?></p>
											<a href="<?php the_permalink(); ?>" class="view-detail-btn">Xem chi tiết</a>
										</div>
									</div>
								<?php endwhile;
								wp_reset_postdata(); ?>
							</div>
							<!-- Navigation Buttons -->
							<button class="carousel-nav-btn prev-btn" id="latest-prev-btn" aria-label="Previous products">&lsaquo;</button>
							<button class="carousel-nav-btn next-btn" id="latest-next-btn" aria-label="Next products">&rsaquo;</button>
						</div>

						<script>
						document.addEventListener('DOMContentLoaded', function() {
							var track = document.getElementById('latest-products-track');
							var prevBtn = document.getElementById('latest-prev-btn');
							var nextBtn = document.getElementById('latest-next-btn');
							if (!track || !prevBtn || !nextBtn) return;

							var currentIndex = 0;
							
							function getSlidesPerView() {
								if (window.innerWidth <= 576) return 1;
								if (window.innerWidth <= 991) return 2;
								return 4;
							}

							function getGap() {
								return 30; // Matches CSS gap
							}

							function updateCarousel() {
								var slides = track.querySelectorAll('.carousel-slide');
								var totalSlides = slides.length;
								if (totalSlides === 0) return;
								var slidesPerView = getSlidesPerView();
								var maxIndex = Math.max(0, totalSlides - slidesPerView);
								
								if (currentIndex > maxIndex) {
									currentIndex = maxIndex;
								}
								if (currentIndex < 0) {
									currentIndex = 0;
								}

								// Calculate offset
								var slideWidth = slides[0].getBoundingClientRect().width;
								var gap = getGap();
								var offset = currentIndex * (slideWidth + gap);
								track.style.transform = 'translateX(-' + offset + 'px)';

								// Enable/Disable buttons
								prevBtn.style.opacity = currentIndex === 0 ? '0.5' : '1';
								prevBtn.style.cursor = currentIndex === 0 ? 'default' : 'pointer';
								nextBtn.style.opacity = currentIndex === maxIndex ? '0.5' : '1';
								nextBtn.style.cursor = currentIndex === maxIndex ? 'default' : 'pointer';
							}

							// Apply slide widths dynamically for responsive transitions
							function applyResponsiveWidths() {
								var slides = track.querySelectorAll('.carousel-slide');
								var slidesPerView = getSlidesPerView();
								var gap = getGap();
								
								slides.forEach(function(slide) {
									if (slidesPerView === 1) {
										slide.style.flex = '0 0 100%';
										slide.style.maxWidth = '100%';
									} else if (slidesPerView === 2) {
										slide.style.flex = '0 0 calc((100% - ' + gap + 'px) / 2)';
										slide.style.maxWidth = 'calc((100% - ' + gap + 'px) / 2)';
									} else {
										slide.style.flex = '0 0 calc((100% - ' + (gap * 3) + 'px) / 4)';
										slide.style.maxWidth = 'calc((100% - ' + (gap * 3) + 'px) / 4)';
									}
								});
								
								updateCarousel();
							}

							prevBtn.addEventListener('click', function() {
								var slidesPerView = getSlidesPerView();
								currentIndex = Math.max(0, currentIndex - slidesPerView);
								updateCarousel();
							});

							nextBtn.addEventListener('click', function() {
								var slides = track.querySelectorAll('.carousel-slide');
								var slidesPerView = getSlidesPerView();
								var maxIndex = Math.max(0, slides.length - slidesPerView);
								currentIndex = Math.min(maxIndex, currentIndex + slidesPerView);
								updateCarousel();
							});

							window.addEventListener('resize', function() {
								applyResponsiveWidths();
							});

							// Touch Swipe Support
							var startX = 0;
							var isSwiping = false;

							track.addEventListener('touchstart', function(e) {
								startX = e.touches[0].clientX;
								isSwiping = true;
							});

							track.addEventListener('touchmove', function(e) {
								if (!isSwiping) return;
								var diffX = startX - e.touches[0].clientX;
								if (Math.abs(diffX) > 50) {
									if (diffX > 0) {
										nextBtn.click();
									} else {
										prevBtn.click();
									}
									isSwiping = false;
								}
							});

							track.addEventListener('touchend', function() {
								isSwiping = false;
							});

							// Initialize
							setTimeout(function() {
								applyResponsiveWidths();
							}, 150);
						});
						</script>

					<?php else: ?>
						<div class="no-products">
							<p>Chưa có sản phẩm nào được cập nhật.</p>
						</div>
					<?php endif; ?>
				</section>
				<?php
				break;

			case 'brands':
				?>
				<!-- 7. BRAND LIST SECTION -->
				<section id="brands" class="brands-section">
					<div class="container">
						<h2 class="section-title"><?php echo esc_html( get_theme_mod( 'sanitary_title_brands', '6 HÃNG THƯƠNG HIỆU ĐỒNG HÀNH' ) ); ?></h2>
						<p class="section-subtitle"><?php echo esc_html( get_theme_mod( 'sanitary_subtitle_brands', 'Click vào hãng để xem các dòng sản phẩm của hãng đó.' ) ); ?></p>

						<div class="brands-grid">
							<?php
							$brands = get_terms([
								'taxonomy' => 'product_brand',
								'hide_empty' => false,
							]);
							if (!is_wp_error($brands) && !empty($brands)):
								foreach ($brands as $brand):
									$brand_link = get_term_link($brand);
									if (!is_wp_error($brand_link)):
										?>
										<a href="<?php echo esc_url($brand_link); ?>" class="brand-card">
											<span class="brand-name"><?php echo esc_html($brand->name); ?></span>
										</a>
										<?php
									endif;
								endforeach;
							endif;
							?>
						</div>
					</div>
				</section>
				<?php
				break;

			case 'projects':
				$projects_json = get_theme_mod( 'sanitary_projects' );
				$projects_data = ! empty( $projects_json ) ? json_decode( $projects_json, true ) : [];
				if ( empty( $projects_data ) || ! is_array( $projects_data ) ) {
					$project_defaults = [
						1 => [ 'title' => 'Thi công phòng tắm Biệt Thự Ecopark', 'desc' => 'Thương hiệu sử dụng: GIFTO GOLD & MANDY', 'img' => '' ],
						2 => [ 'title' => 'Lắp đặt thiết bị vệ sinh Căn Hộ Vinhomes', 'desc' => 'Thương hiệu sử dụng: TAKAMI & TQC', 'img' => '' ],
						3 => [ 'title' => 'Thiết kế & Thi công trọn gói Nhà Phố Quận 2', 'desc' => 'Thương hiệu sử dụng: GIFTO & SDUY', 'img' => '' ]
					];
					$projects_data = [];
					for ( $i = 1; $i <= 3; $i++ ) {
						$projects_data[] = [
							'title'   => get_theme_mod( 'sanitary_project' . $i . '_title', $project_defaults[$i]['title'] ),
							'desc'    => get_theme_mod( 'sanitary_project' . $i . '_desc', $project_defaults[$i]['desc'] ),
							'img'     => get_theme_mod( 'sanitary_project' . $i . '_img', $project_defaults[$i]['img'] ),
							'visible' => 1
						];
					}
				}
				$projects_data = array_filter( $projects_data, function( $item ) {
					return ! isset( $item['visible'] ) || $item['visible'] == 1;
				} );
				?>
				<!-- 8. REAL CONSTRUCTIONS & PROJECTS -->
				<section class="projects-section container">
					<h2 class="section-title"><?php echo esc_html( get_theme_mod( 'sanitary_title_projects', 'DỰ ÁN THI CÔNG THỰC TẾ' ) ); ?></h2>
					<p class="section-subtitle"><?php echo esc_html( get_theme_mod( 'sanitary_subtitle_projects', 'Hình ảnh thực tế bàn giao phòng tắm hoàn thiện cho khách hàng.' ) ); ?></p>

					<div class="projects-grid">
						<?php foreach ( $projects_data as $item ) : 
							$p_img = ! empty( $item['img'] ) ? $item['img'] : get_template_directory_uri() . '/assets/images/placeholder.jpg';
						?>
							<div class="project-item">
								<div class="project-img-wrapper">
									<img src="<?php echo esc_url($p_img); ?>" alt="<?php echo esc_attr($item['title']); ?>">
								</div>
								<div class="project-meta">
									<h4><?php echo esc_html($item['title']); ?></h4>
									<p><?php echo esc_html($item['desc']); ?></p>
								</div>
							</div>
						<?php endforeach; ?>
					</div>
				</section>
				<?php
				break;
		}
	}
	?>
</main>

<script>
	document.addEventListener('DOMContentLoaded', function () {
		// 1. HERO SLIDER LOGIC
		var heroSlider = document.getElementById('homepage-hero-slider');
		if (heroSlider) {
			var slides = heroSlider.querySelectorAll('.hero-slide');
			var dots = document.querySelectorAll('#slider-dots .slider-dot');
			var prevBtn = document.getElementById('slider-prev');
			var nextBtn = document.getElementById('slider-next');
			var currentSlide = 0;
			var slideInterval = setInterval(nextSlide, 5000); // Autoplay 5s

			function goToSlide(n) {
				slides[currentSlide].classList.remove('active');
				if (dots.length > 0) dots[currentSlide].classList.remove('active');
				currentSlide = (n + slides.length) % slides.length;
				slides[currentSlide].classList.add('active');
				if (dots.length > 0) dots[currentSlide].classList.add('active');
			}

			function nextSlide() {
				goToSlide(currentSlide + 1);
			}

			function prevSlide() {
				goToSlide(currentSlide - 1);
			}

			if (nextBtn) {
				nextBtn.addEventListener('click', function () {
					nextSlide();
					resetInterval();
				});
			}
			if (prevBtn) {
				prevBtn.addEventListener('click', function () {
					prevSlide();
					resetInterval();
				});
			}

			dots.forEach(function (dot) {
				dot.addEventListener('click', function () {
					var slideIndex = parseInt(this.getAttribute('data-slide'));
					goToSlide(slideIndex);
					resetInterval();
				});
			});

			function resetInterval() {
				clearInterval(slideInterval);
				slideInterval = setInterval(nextSlide, 5000);
			}
		}

		// 2. CATEGORY TABS LOGIC
		var tabBlocks = document.querySelectorAll('.category-tabs-block');
		tabBlocks.forEach(function (block) {
			var tabItems = block.querySelectorAll('.category-tab-item');
			var tabPanes = block.querySelectorAll('.category-tab-pane');

			tabItems.forEach(function (item) {
				item.addEventListener('click', function () {
					var targetId = this.getAttribute('data-tab');

					// Remove active class from all tabs & panes in this block
					tabItems.forEach(function (t) { t.classList.remove('active'); });
					tabPanes.forEach(function (p) { p.classList.remove('active'); });

					// Add active class to clicked tab and corresponding pane
					this.classList.add('active');
					var targetPane = block.querySelector('#' + targetId);
					if (targetPane) {
						targetPane.classList.add('active');
					}
				});
			});
		});
	});
</script>

<?php get_footer(); ?>