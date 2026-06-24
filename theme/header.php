<!DOCTYPE html>
<html <?php language_attributes(); ?>>
<head>
	<meta charset="<?php bloginfo( 'charset' ); ?>">
	<meta name="viewport" content="width=device-width, initial-scale=1.0">
	<?php wp_head(); ?>
</head>
<body <?php body_class(); ?>>
<?php wp_body_open(); ?>

<header class="site-header">
	<div class="header-top-bar">
		<div class="header-top-container container">
			<div class="top-bar-left">
				<span>📍 <?php echo esc_html( get_theme_mod( 'sanitary_address', 'Showroom Thiết Bị Vệ Sinh Hồng Miên' ) ); ?></span>
			</div>
			<div class="top-bar-right">
				<span>⏰ <?php echo esc_html( get_theme_mod( 'sanitary_working_hours', '8:00 - 18:00 (Thứ 2 - Chủ Nhật)' ) ); ?></span>
			</div>
		</div>
	</div>
	<div class="header-container">
		<div class="site-branding">
			<?php 
			$custom_logo_url = get_theme_mod( 'sanitary_logo_url' );
			if ( ! empty( $custom_logo_url ) ) : ?>
				<a href="<?php echo esc_url( home_url( '/' ) ); ?>" rel="home">
					<img src="<?php echo esc_url( $custom_logo_url ); ?>" class="custom-logo" alt="<?php bloginfo( 'name' ); ?>" />
				</a>
			<?php elseif ( has_custom_logo() ) : ?>
				<?php the_custom_logo(); ?>
			<?php else : ?>
				<h1 class="site-title"><a href="<?php echo esc_url( home_url( '/' ) ); ?>" rel="home"><?php bloginfo( 'name' ); ?></a></h1>
				<p class="site-description"><?php bloginfo( 'description' ); ?></p>
			<?php endif; ?>
		</div>

		<!-- AJAX Live Search Bar -->
		<div class="header-search-bar">
			<form role="search" method="get" class="search-form-ajax" action="<?php echo esc_url( home_url( '/' ) ); ?>">
				<div class="search-input-wrapper">
					<input type="search" class="search-field ajax-search-input" placeholder="Tìm sản phẩm..." value="" name="s" autocomplete="off" />
					<button type="submit" class="search-submit">🔍</button>
				</div>
				<div class="ajax-search-results" style="display: none;"></div>
			</form>
		</div>

		<!-- Header Actions Wrapper (Always visible) -->
		<div class="header-actions-wrapper" style="display: flex; align-items: center; gap: 15px; order: 3;">
			<!-- Dark Mode Toggle Switch -->
			<button id="dark-mode-toggle" class="dark-mode-toggle-btn" aria-label="Toggle Dark Mode" style="background: none; border: none; font-size: 1.3rem; cursor: pointer; padding: 5px; line-height: 1; transition: transform 0.2s ease;">
				🌙
			</button>

			<!-- Hamburger Menu Toggle Button -->
			<button class="menu-toggle" aria-controls="primary-menu" aria-expanded="false" id="mobile-menu-trigger">
				<span></span>
				<span></span>
				<span></span>
			</button>
		</div>

		<nav id="site-navigation" class="main-navigation">
			<?php
			wp_nav_menu( [
				'theme_location' => 'primary-menu',
				'menu_id'        => 'primary-menu',
				'container'      => false,
				'fallback_cb'    => 'wp_page_menu',
			] );
			?>
			
			<!-- Mobile CTA inside Menu Drawer -->
			<div class="mobile-only-menu-cta" style="margin-top: 20px; text-align: center; display: none;">
				<?php
				$hotline = get_theme_mod( 'sanitary_hotline', '090 123 4567' );
				$hotline_tel = get_theme_mod( 'sanitary_hotline_tel', '0901234567' );
				?>
				<a href="tel:<?php echo esc_attr( $hotline_tel ); ?>" class="cta-button" style="display: block; padding: 12px; background: var(--color-accent); color: #fff; font-weight: 700; border-radius: 6px;">Hotline: <?php echo esc_html( $hotline ); ?></a>
			</div>
		</nav>

		<div class="header-cta" style="display: flex; align-items: center; gap: 15px; order: 4;">
			<?php
			$hotline = get_theme_mod( 'sanitary_hotline', '090 123 4567' );
			$hotline_tel = get_theme_mod( 'sanitary_hotline_tel', '0901234567' );
			?>
			<a href="tel:<?php echo esc_attr( $hotline_tel ); ?>" class="cta-button">Hotline: <?php echo esc_html( $hotline ); ?></a>
		</div>
	</div>
</header>

<script>
document.addEventListener('DOMContentLoaded', function() {
	var menuTrigger = document.getElementById('mobile-menu-trigger');
	var navMenu = document.getElementById('site-navigation');
	var mobileCta = document.querySelector('.mobile-only-menu-cta');

	if (menuTrigger && navMenu) {
		menuTrigger.addEventListener('click', function() {
			var active = menuTrigger.classList.toggle('active');
			navMenu.classList.toggle('active');
			menuTrigger.setAttribute('aria-expanded', active);
			
			if (mobileCta) {
				mobileCta.style.display = active ? 'block' : 'none';
			}
		});

		// Toggle dropdowns on mobile click instead of hover
		var parentItems = navMenu.querySelectorAll('ul > li.menu-item-has-children > a');
		parentItems.forEach(function(item) {
			item.addEventListener('click', function(e) {
				if (window.innerWidth <= 991) {
					e.preventDefault();
					var parentLi = this.parentElement;
					parentLi.classList.toggle('hover-active');
				}
			});
		});
	}
});
</script>
