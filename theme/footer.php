<footer class="site-footer">
	<div class="footer-container">
		<div class="footer-column branding">
			<h3 class="footer-logo"><?php bloginfo( 'name' ); ?></h3>
			<p>Chuyên cung cấp Thiết bị vệ sinh cao cấp chính hãng & Dịch vụ thiết kế, thi công, lắp đặt hoàn thiện chuyên nghiệp.</p>
			<?php
			$fb_url = get_theme_mod( 'sanitary_facebook_url', 'https://facebook.com' );
			if ( ! empty( $fb_url ) ) :
			?>
				<p style="margin-top: 15px;">
					<a href="<?php echo esc_url( $fb_url ); ?>" target="_blank" rel="noopener noreferrer" style="color: #ffffff; text-decoration: underline; font-size: 0.9rem;">
						Theo dõi chúng tôi trên Facebook
					</a>
				</p>
			<?php endif; ?>
		</div>

		<div class="footer-column services">
			<h4>Dịch vụ chính</h4>
			<ul>
				<li>Tư vấn & Thiết kế phòng tắm</li>
				<li>Thi công lắp đặt thiết bị</li>
				<li>Bảo hành & Bảo dưỡng định kỳ</li>
				<?php
				$hours = get_theme_mod( 'sanitary_working_hours', '8:00 - 18:00 (Thứ 2 - Chủ Nhật)' );
				if ( ! empty( $hours ) ) :
				?>
					<li style="margin-top: 15px; color: #94a3b8; font-size: 0.85rem;">
						<strong>Giờ làm việc:</strong><br><?php echo esc_html( $hours ); ?>
					</li>
				<?php endif; ?>
			</ul>
		</div>

		<div class="footer-column contact">
			<h4>Liên hệ tư vấn</h4>
			<?php
			$address = get_theme_mod( 'sanitary_address', 'Showroom Thiết Bị Vệ Sinh Hồng Miên' );
			$hotline = get_theme_mod( 'sanitary_hotline', '090 123 4567' );
			$zalo_url = get_theme_mod( 'sanitary_zalo_url', 'https://zalo.me/0901234567' );
			$email = get_theme_mod( 'sanitary_email', 'contact@example.com' );
			?>
			<p><strong>Địa chỉ:</strong> <?php echo esc_html( $address ); ?></p>
			<p><strong>Điện thoại:</strong> <?php echo esc_html( $hotline ); ?></p>
			<p><strong>Email:</strong> <?php echo esc_html( $email ); ?></p>
			<p><strong>Zalo báo giá:</strong> <a href="<?php echo esc_url( $zalo_url ); ?>" target="_blank" rel="noopener noreferrer" style="color:#d97706; font-weight:700;">Liên hệ Zalo</a></p>
		</div>
	</div>

	<div class="footer-bottom">
		<?php
		$copyright = get_theme_mod( 'sanitary_copyright', '© ' . date('Y') . ' Hồng Miên. Tất cả quyền được bảo lưu.' );
		?>
		<p><?php echo esc_html( $copyright ); ?></p>
	</div>
</footer>

	<!-- Floating Contact Widget -->
	<?php
	$hotline_tel = get_theme_mod( 'sanitary_hotline_tel', '0901234567' );
	$zalo_url = get_theme_mod( 'sanitary_zalo_url', 'https://zalo.me/0901234567' );
	?>
	<div class="floating-contact-widget">
		<!-- Phone Button -->
		<a href="tel:<?php echo esc_attr( $hotline_tel ); ?>" class="floating-btn phone-btn" title="Gọi điện thoại Hotline">
			<span class="phone-ripple-wave"></span>
			<span class="widget-icon-svg">
				<svg viewBox="0 0 24 24" width="24" height="24" style="fill: #fff; display: block;">
					<path d="M6.62 10.79c1.44 2.83 3.76 5.14 6.59 6.59l2.2-2.2c.27-.27.67-.36 1.02-.24 1.12.37 2.33.57 3.57.57.55 0 1 .45 1 1V20c0 .55-.45 1-1 1-9.39 0-17-7.61-17-17 0-.55.45-1 1-1h3.5c.55 0 1 .45 1 1 0 1.25.2 2.45.57 3.57.11.35.03.74-.25 1.02l-2.2 2.2z"/>
				</svg>
			</span>
		</a>
		<!-- Zalo Button -->
		<a href="<?php echo esc_url( $zalo_url ); ?>" target="_blank" rel="noopener noreferrer" class="floating-btn zalo-btn" title="Chat Zalo ngay">
			<span class="zalo-ripple-wave"></span>
			<span class="widget-icon-svg">
				<svg viewBox="0 0 40 40" width="30" height="30" style="display: block;">
					<circle cx="20" cy="20" r="20" fill="#0068ff" />
					<text x="20" y="26" font-family="Arial, sans-serif" font-size="19" font-weight="900" text-anchor="middle" fill="#fff">Z</text>
				</svg>
			</span>
		</a>
	</div>

	<!-- Privacy Consent Notification Banner (Decree 13) -->
	<div id="privacy-consent-banner" style="position: fixed; bottom: 20px; left: 20px; right: 20px; max-width: 600px; background: rgba(15, 23, 42, 0.95); backdrop-filter: blur(10px); color: #ffffff; border: 1px solid rgba(255, 255, 255, 0.1); border-radius: 12px; padding: 18px 24px; box-shadow: 0 10px 30px rgba(0,0,0,0.3); z-index: 99999; display: none; align-items: center; justify-content: space-between; gap: 20px; transition: all 0.5s ease;">
		<div style="font-size: 0.88rem; line-height: 1.5; text-align: left;">
			Chúng tôi sử dụng cookie và xử lý dữ liệu cá nhân (Họ tên, SĐT) để tối ưu trải nghiệm và cung cấp dịch vụ tốt nhất. Bằng cách nhấn "Đồng ý", bạn cho phép chúng tôi xử lý thông tin theo <a href="<?php echo esc_url( home_url( '/chinh-sach-bao-mat/' ) ); ?>" target="_blank" style="color: var(--color-accent); text-decoration: underline; font-weight: 700;">Chính sách bảo mật</a>.
		</div>
		<button id="accept-privacy-btn" class="btn btn-accent" style="padding: 8px 20px; font-size: 0.85rem; white-space: nowrap;">Đồng ý</button>
	</div>

	<script>
	document.addEventListener('DOMContentLoaded', function() {
		var banner = document.getElementById('privacy-consent-banner');
		var acceptBtn = document.getElementById('accept-privacy-btn');
		if (banner && acceptBtn) {
			// Check if already accepted
			if (!localStorage.getItem('privacy-consent-accepted')) {
				setTimeout(function() {
					banner.style.display = 'flex';
				}, 1500); // Show after 1.5s
			}
			
			acceptBtn.addEventListener('click', function() {
				localStorage.setItem('privacy-consent-accepted', 'true');
				banner.style.opacity = '0';
				setTimeout(function() {
					banner.style.display = 'none';
				}, 500);
			});
		}
	});
	</script>

<?php wp_footer(); ?>
<script>
document.addEventListener('DOMContentLoaded', function() {
    // 1. Dark Mode Toggle
    var darkToggle = document.getElementById('dark-mode-toggle');
    if (darkToggle) {
        // Check local storage or system preference
        var savedMode = localStorage.getItem('theme-mode');
        if (savedMode === 'dark' || (!savedMode && window.matchMedia('(prefers-color-scheme: dark)').matches)) {
            document.body.classList.add('dark-mode');
            darkToggle.textContent = '☀️';
        } else {
            darkToggle.textContent = '🌙';
        }

        darkToggle.addEventListener('click', function() {
            var isDark = document.body.classList.toggle('dark-mode');
            localStorage.setItem('theme-mode', isDark ? 'dark' : 'light');
            darkToggle.textContent = isDark ? '☀️' : '🌙';
        });
    }

    // 2. AJAX Live Search
    var searchInput = document.querySelector('.ajax-search-input');
    var searchResults = document.querySelector('.ajax-search-results');
    var ajaxSearchForm = document.querySelector('.search-form-ajax');
    var searchTimeout = null;

    if (searchInput && searchResults) {
        searchInput.addEventListener('input', function() {
            var query = this.value.trim();
            clearTimeout(searchTimeout);

            if (query.length < 2) {
                searchResults.innerHTML = '';
                searchResults.style.display = 'none';
                return;
            }

            searchTimeout = setTimeout(function() {
                searchResults.innerHTML = '<div class="search-loading">Đang tìm kiếm...</div>';
                searchResults.style.display = 'block';

                fetch('<?php echo admin_url('admin-ajax.php'); ?>?action=sanitary_ajax_search&q=' + encodeURIComponent(query))
                    .then(function(res) { return res.json(); })
                    .then(function(response) {
                        if (response.success && response.data.length > 0) {
                            var html = '';
                            response.data.forEach(function(item) {
                                html += '<a href="' + item.permalink + '" class="search-result-item">';
                                if (item.thumbnail) {
                                    html += '<img src="' + item.thumbnail + '" class="result-thumb" />';
                                } else {
                                    html += '<div class="result-thumb-placeholder">🔍</div>';
                                }
                                html += '<div class="result-meta">';
                                html += '<span class="result-title">' + item.title + '</span>';
                                html += '<span class="result-excerpt">' + item.excerpt + '</span>';
                                html += '</div>';
                                html += '</a>';
                            });
                            searchResults.innerHTML = html;
                        } else {
                            searchResults.innerHTML = '<div class="search-no-results">Không tìm thấy sản phẩm nào.</div>';
                        }
                    })
                    .catch(function(err) {
                        console.error(err);
                        searchResults.innerHTML = '<div class="search-no-results">Đã xảy ra lỗi.</div>';
                    });
            }, 300);
        });

        // Hide search results on click outside
        document.addEventListener('click', function(e) {
            if (ajaxSearchForm && !ajaxSearchForm.contains(e.target)) {
                searchResults.style.display = 'none';
            }
        });

        searchInput.addEventListener('focus', function() {
            if (this.value.trim().length >= 2) {
                searchResults.style.display = 'block';
            }
        });
    }

    // 3. Image Lightbox Zoom
    var galleryImgs = document.querySelectorAll('.product-gallery img, .featured-product-image');
    if (galleryImgs.length > 0) {
        // Create lightbox elements dynamically
        var lightbox = document.createElement('div');
        lightbox.id = 'sanitary-lightbox';
        lightbox.className = 'sanitary-lightbox-modal';
        lightbox.style.display = 'none';
        lightbox.innerHTML = '<span class="lightbox-close">&times;</span><img class="lightbox-content" id="lightbox-img"><div id="lightbox-caption"></div>';
        document.body.appendChild(lightbox);

        var lightboxImg = document.getElementById('lightbox-img');
        var captionText = document.getElementById('lightbox-caption');

        galleryImgs.forEach(function(img) {
            img.style.cursor = 'zoom-in';
            img.addEventListener('click', function() {
                lightbox.style.display = 'flex';
                lightboxImg.src = this.src;
                captionText.textContent = this.alt || document.title;
            });
        });

        var closeBtn = lightbox.querySelector('.lightbox-close');
        closeBtn.addEventListener('click', function() {
            lightbox.style.display = 'none';
        });

        lightbox.addEventListener('click', function(e) {
            if (e.target !== lightboxImg && e.target !== captionText) {
                lightbox.style.display = 'none';
            }
        });
    }
});
</script>
</body>
</html>
