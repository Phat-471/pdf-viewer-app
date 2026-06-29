<?php
/**
 * VN SEO Module - Core v2
 * Sitemap, Script Injection, Contact Buttons, Meta Description, Open Graph, Breadcrumb
 */
if ( ! defined( 'ABSPATH' ) ) exit;

class VN_SEO_Core {

	public function __construct() {
		$settings = self::get_settings();

		// XML Sitemap
		if ( ! empty( $settings['sitemap_enabled'] ) ) {
			add_action( 'init',                [ $this, 'register_sitemap_rewrite' ] );
			add_action( 'template_redirect',   [ $this, 'output_sitemap' ] );
		}

		// Script injection
		if ( ! empty( $settings['head_scripts'] ) ) {
			add_action( 'wp_head',   [ $this, 'inject_head_scripts' ], 99 );
		}
		if ( ! empty( $settings['footer_scripts'] ) ) {
			add_action( 'wp_footer', [ $this, 'inject_footer_scripts' ], 99 );
		}

		// Contact Buttons
		if ( ! empty( $settings['contact_enabled'] ) ) {
			add_action( 'wp_footer',         [ $this, 'output_contact_buttons' ], 100 );
			add_action( 'wp_enqueue_scripts', [ $this, 'enqueue_contact_styles' ] );
		}

		// Meta Description
		if ( ! empty( $settings['meta_desc_enabled'] ) ) {
			remove_action( 'wp_head', 'rel_canonical' );
			add_action( 'wp_head', [ $this, 'output_meta_description' ], 2 );
		}

		// Open Graph
		if ( ! empty( $settings['og_enabled'] ) ) {
			add_action( 'wp_head', [ $this, 'output_og_tags' ], 3 );
		}

		// Breadcrumb Schema
		if ( ! empty( $settings['breadcrumb_schema'] ) ) {
			add_action( 'wp_head', [ $this, 'output_breadcrumb_schema' ], 5 );
		}
	}

	/* ================================================================
	   Cài đặt
	================================================================ */
	public static function get_settings() {
		$defaults = [
			'sitemap_enabled'    => 0,
			'sitemap_posts'      => 1,
			'sitemap_pages'      => 1,
			'sitemap_cats'       => 1,
			'head_scripts'       => '',
			'footer_scripts'     => '',
			'contact_enabled'    => 0,
			'contact_phone'      => '',
			'contact_zalo'       => '',
			'contact_messenger'  => '',
			'contact_position'   => 'right',
			'contact_show_label' => 1,
			'contact_hide_desktop' => 0,
			'contact_hide_mobile'  => 0,
			// Mới
			'meta_desc_enabled'  => 1,
			'meta_desc_length'   => 160,
			'og_enabled'         => 1,
			'og_site_name'       => get_bloginfo('name'),
			'og_default_image'   => '',
			'og_twitter_handle'  => '',
			'breadcrumb_schema'  => 1,
		];
		return wp_parse_args( get_option( 'vn_seo_settings', [] ), $defaults );
	}

	public static function save_settings( $data ) {
		$settings = [
			'sitemap_enabled'    => ! empty( $data['sitemap_enabled'] ) ? 1 : 0,
			'sitemap_posts'      => ! empty( $data['sitemap_posts'] ) ? 1 : 0,
			'sitemap_pages'      => ! empty( $data['sitemap_pages'] ) ? 1 : 0,
			'sitemap_cats'       => ! empty( $data['sitemap_cats'] ) ? 1 : 0,
			'head_scripts'       => wp_kses_post( $data['head_scripts'] ?? '' ),
			'footer_scripts'     => wp_kses_post( $data['footer_scripts'] ?? '' ),
			'contact_enabled'    => ! empty( $data['contact_enabled'] ) ? 1 : 0,
			'contact_phone'      => sanitize_text_field( $data['contact_phone'] ?? '' ),
			'contact_zalo'       => sanitize_text_field( $data['contact_zalo'] ?? '' ),
			'contact_messenger'  => esc_url_raw( $data['contact_messenger'] ?? '' ),
			'contact_position'   => sanitize_text_field( $data['contact_position'] ?? 'right' ),
			'contact_show_label' => ! empty( $data['contact_show_label'] ) ? 1 : 0,
			'contact_hide_desktop' => ! empty( $data['contact_hide_desktop'] ) ? 1 : 0,
			'contact_hide_mobile'  => ! empty( $data['contact_hide_mobile'] ) ? 1 : 0,
			'meta_desc_enabled'  => ! empty( $data['meta_desc_enabled'] ) ? 1 : 0,
			'meta_desc_length'   => max( 50, min( 320, absint( $data['meta_desc_length'] ?? 160 ) ) ),
			'og_enabled'         => ! empty( $data['og_enabled'] ) ? 1 : 0,
			'og_site_name'       => sanitize_text_field( $data['og_site_name'] ?? get_bloginfo('name') ),
			'og_default_image'   => esc_url_raw( $data['og_default_image'] ?? '' ),
			'og_twitter_handle'  => sanitize_text_field( ltrim( $data['og_twitter_handle'] ?? '', '@' ) ),
			'breadcrumb_schema'  => ! empty( $data['breadcrumb_schema'] ) ? 1 : 0,
		];
		update_option( 'vn_seo_settings', $settings );
		if ( $settings['sitemap_enabled'] ) {
			flush_rewrite_rules();
		}
		return $settings;
	}

	/* ================================================================
	   Meta Description
	================================================================ */
	public function output_meta_description() {
		$settings = self::get_settings();
		$length   = (int) $settings['meta_desc_length'];
		$desc     = '';

		if ( is_singular() ) {
			global $post;
			if ( has_excerpt( $post ) ) {
				$desc = get_the_excerpt( $post );
			} else {
				$desc = wp_strip_all_tags( get_the_content( null, false, $post ) );
			}
		} elseif ( is_category() || is_tag() || is_tax() ) {
			$desc = term_description();
		} elseif ( is_home() || is_front_page() ) {
			$desc = get_bloginfo( 'description' );
		}

		$desc = wp_trim_words( strip_tags( $desc ), 30, '' );
		if ( strlen( $desc ) > $length ) {
			$desc = substr( $desc, 0, $length - 3 ) . '...';
		}

		if ( $desc ) {
			echo '<meta name="description" content="' . esc_attr( $desc ) . '">' . "\n";
		}
	}

	/* ================================================================
	   Open Graph Tags
	================================================================ */
	public function output_og_tags() {
		$settings   = self::get_settings();
		$title      = '';
		$desc       = '';
		$image      = $settings['og_default_image'];
		$url        = get_permalink() ?: home_url( '/' );
		$type       = 'website';
		$site_name  = $settings['og_site_name'] ?: get_bloginfo( 'name' );

		if ( is_singular() ) {
			global $post;
			$title  = get_the_title( $post );
			$desc   = has_excerpt( $post ) ? get_the_excerpt( $post ) : wp_trim_words( strip_tags( get_the_content( null, false, $post ) ), 25 );
			$type   = 'article';
			if ( has_post_thumbnail( $post ) ) {
				$img_arr = wp_get_attachment_image_src( get_post_thumbnail_id( $post ), 'large' );
				$image   = $img_arr ? $img_arr[0] : $image;
			}
		} else {
			$title = get_bloginfo( 'name' ) . ( wp_title( ' | ', false ) ? wp_title( ' | ', false ) : '' );
			$desc  = get_bloginfo( 'description' );
		}

		$tags = [
			'og:type'        => $type,
			'og:url'         => $url,
			'og:title'       => $title,
			'og:description' => wp_trim_words( $desc, 25 ),
			'og:site_name'   => $site_name,
		];
		if ( $image ) $tags['og:image'] = $image;

		foreach ( $tags as $prop => $content ) :
			if ( $content ) :
				echo '<meta property="' . esc_attr( $prop ) . '" content="' . esc_attr( $content ) . '">' . "\n";
			endif;
		endforeach;

		// Twitter Card
		echo '<meta name="twitter:card" content="summary_large_image">' . "\n";
		if ( $settings['og_twitter_handle'] ) {
			echo '<meta name="twitter:site" content="@' . esc_attr( $settings['og_twitter_handle'] ) . '">' . "\n";
		}
		if ( $title ) echo '<meta name="twitter:title" content="' . esc_attr( $title ) . '">' . "\n";
		if ( $desc )  echo '<meta name="twitter:description" content="' . esc_attr( wp_trim_words($desc,25) ) . '">' . "\n";
		if ( $image ) echo '<meta name="twitter:image" content="' . esc_attr( $image ) . '">' . "\n";
	}

	/* ================================================================
	   Breadcrumb Schema (JSON-LD)
	================================================================ */
	public function output_breadcrumb_schema() {
		if ( is_front_page() || is_home() ) return;

		$items     = [];
		$position  = 1;

		$items[] = [
			'@type'    => 'ListItem',
			'position' => $position++,
			'name'     => get_bloginfo( 'name' ),
			'item'     => home_url( '/' ),
		];

		if ( is_singular() ) {
			$cats = get_the_category();
			if ( ! empty( $cats ) ) {
				$items[] = [
					'@type'    => 'ListItem',
					'position' => $position++,
					'name'     => esc_html( $cats[0]->name ),
					'item'     => get_category_link( $cats[0]->term_id ),
				];
			}
			$items[] = [
				'@type'    => 'ListItem',
				'position' => $position++,
				'name'     => get_the_title(),
				'item'     => get_permalink(),
			];
		} elseif ( is_category() ) {
			$items[] = [
				'@type'    => 'ListItem',
				'position' => $position++,
				'name'     => single_cat_title( '', false ),
				'item'     => get_category_link( get_queried_object_id() ),
			];
		} elseif ( is_tag() ) {
			$items[] = [
				'@type'    => 'ListItem',
				'position' => $position++,
				'name'     => single_tag_title( '', false ),
				'item'     => get_tag_link( get_queried_object_id() ),
			];
		} elseif ( is_page() ) {
			$items[] = [
				'@type'    => 'ListItem',
				'position' => $position++,
				'name'     => get_the_title(),
				'item'     => get_permalink(),
			];
		}

		$schema = [
			'@context'        => 'https://schema.org',
			'@type'           => 'BreadcrumbList',
			'itemListElement' => $items,
		];

		echo '<script type="application/ld+json">' . wp_json_encode( $schema, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES ) . '</script>' . "\n";
	}

	/* ================================================================
	   Script Injection
	================================================================ */
	public function inject_head_scripts() {
		$s = self::get_settings();
		if ( ! empty( $s['head_scripts'] ) ) echo "\n" . $s['head_scripts'] . "\n";
	}

	public function inject_footer_scripts() {
		$s = self::get_settings();
		if ( ! empty( $s['footer_scripts'] ) ) echo "\n" . $s['footer_scripts'] . "\n";
	}

	/* ================================================================
	   XML Sitemap
	================================================================ */
	public function register_sitemap_rewrite() {
		add_rewrite_rule( '^sitemap\.xml$', 'index.php?vn_sitemap=1', 'top' );
		add_filter( 'query_vars', function( $vars ) { $vars[] = 'vn_sitemap'; return $vars; } );
	}

	public function output_sitemap() {
		if ( ! get_query_var( 'vn_sitemap' ) ) return;
		$settings = self::get_settings();
		$urls     = [];

		if ( ! empty( $settings['sitemap_posts'] ) ) {
			$posts = get_posts( [ 'numberposts' => 1000, 'post_status' => 'publish', 'post_type' => 'post' ] );
			foreach ( $posts as $p ) $urls[] = [ 'loc' => get_permalink( $p ), 'lastmod' => mysql2date( 'c', $p->post_modified_gmt ) ];
		}
		if ( ! empty( $settings['sitemap_pages'] ) ) {
			$pages = get_pages( [ 'post_status' => 'publish' ] );
			foreach ( $pages as $p ) $urls[] = [ 'loc' => get_page_link( $p ), 'lastmod' => mysql2date( 'c', $p->post_modified_gmt ) ];
		}
		if ( ! empty( $settings['sitemap_cats'] ) ) {
			$cats = get_categories( [ 'hide_empty' => true ] );
			foreach ( $cats as $c ) $urls[] = [ 'loc' => get_category_link( $c->term_id ), 'lastmod' => '' ];
		}

		header( 'Content-Type: application/xml; charset=UTF-8' );
		echo '<?xml version="1.0" encoding="UTF-8"?>' . "\n";
		echo '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">' . "\n";
		foreach ( $urls as $u ) {
			echo '<url><loc>' . esc_url( $u['loc'] ) . '</loc>';
			if ( $u['lastmod'] ) echo '<lastmod>' . esc_html( $u['lastmod'] ) . '</lastmod>';
			echo '<changefreq>weekly</changefreq><priority>0.8</priority></url>' . "\n";
		}
		echo '</urlset>';
		exit;
	}

	public static function get_sitemap_stats() {
		$settings = self::get_settings();
		$count    = 0;
		if ( ! empty( $settings['sitemap_posts'] ) )  $count += (int) wp_count_posts()->publish;
		if ( ! empty( $settings['sitemap_pages'] ) )  $count += count( get_pages( ['post_status'=>'publish'] ) );
		if ( ! empty( $settings['sitemap_cats'] ) )   $count += (int) wp_count_terms( 'category', ['hide_empty'=>true] );
		return $count;
	}

	/* ================================================================
	   Contact Buttons
	================================================================ */
	public function enqueue_contact_styles() {
		wp_enqueue_style( 'vn-contact-buttons', VN_PRIVACY_URL . 'assets/contact-buttons.css', [], VN_PRIVACY_VERSION );
	}

	public function output_contact_buttons() {
		$s    = self::get_settings();
		$pos  = $s['contact_position'] === 'left' ? 'left' : 'right';
		$cls  = 'vn-contact-wrap vn-contact-' . $pos;
		if ( $s['contact_hide_desktop'] ) $cls .= ' vn-contact-hide-desktop';
		if ( $s['contact_hide_mobile']  ) $cls .= ' vn-contact-hide-mobile';

		$buttons = [];
		if ( $s['contact_phone'] )     $buttons[] = ['phone',     'tel:' . preg_replace('/[^+\d]/','',$s['contact_phone']), '📞', 'Gọi điện', 'vn-btn-phone',     'vn-mbtn-phone'];
		if ( $s['contact_zalo'] )      $buttons[] = ['zalo',      'https://zalo.me/' . ltrim($s['contact_zalo'],'https://zalo.me/'),'💬','Zalo','vn-btn-zalo','vn-mbtn-zalo'];
		if ( $s['contact_messenger'] ) $buttons[] = ['messenger', $s['contact_messenger'], '✉️', 'Nhắn tin', 'vn-btn-messenger', 'vn-mbtn-messenger'];

		if ( empty( $buttons ) ) return;
		?>
		<div class="<?php echo esc_attr($cls); ?>">
			<!-- Sidebar PC -->
			<div class="vn-contact-sidebar">
				<?php foreach ( $buttons as [$id,$href,$icon,$label,$cls_btn] ) : ?>
				<a href="<?php echo esc_url($href); ?>" class="vn-contact-btn <?php echo $cls_btn; ?>" target="_blank" rel="noopener" aria-label="<?php echo esc_attr($label); ?>">
					<span class="vn-contact-icon"><?php echo $icon; ?></span>
					<?php if ( $s['contact_show_label'] ) : ?>
					<span class="vn-contact-label"><?php echo esc_html($label); ?></span>
					<?php endif; ?>
				</a>
				<?php endforeach; ?>
			</div>

			<!-- Mobile Bottom Bar -->
			<nav class="vn-contact-mobile-bar" aria-label="Liên hệ nhanh">
				<?php foreach ( $buttons as [$id,$href,$icon,$label,,$cls_m] ) : ?>
				<a href="<?php echo esc_url($href); ?>" class="vn-mobile-btn <?php echo $cls_m; ?>" target="_blank" rel="noopener" aria-label="<?php echo esc_attr($label); ?>">
					<span class="vn-mobile-icon"><?php echo $icon; ?></span>
					<span class="vn-mobile-label"><?php echo esc_html($label); ?></span>
				</a>
				<?php endforeach; ?>
			</nav>
		</div>
		<?php
	}
}
