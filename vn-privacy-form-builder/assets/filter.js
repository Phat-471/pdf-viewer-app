/**
 * VN Product Filter - Frontend JavaScript
 * AJAX filtering, price slider, pagination, accordion
 */
(function ($) {
  'use strict';

  /* ── Globals ─────────────────────────────────────────────── */
  const VNF = window.vnFilterData || {};
  let   debounceTimer  = null;
  let   priceSlider    = null;
  let   currentPage    = 1;
  let   currentOrderby = 'date';

  /* ── Init ────────────────────────────────────────────────── */
  function init() {
    if ( typeof noUiSlider === 'undefined' ) return;

    initPriceSlider();
    initAccordion();
    bindFormSubmit();
    bindPageBtns();
    bindOrderby();
    bindResetBtn();
    bindResetInProducts();

    // Tải sản phẩm lần đầu để hiển thị số lượng
    triggerFilter();
  }

  /* ── Price Slider ────────────────────────────────────────── */
  function initPriceSlider() {
    const sliderEl = document.getElementById('vn-price-slider');
    if ( ! sliderEl ) return;

    const min = parseFloat( sliderEl.dataset.min ) || 0;
    const max = parseFloat( sliderEl.dataset.max ) || 10000000;

    priceSlider = noUiSlider.create( sliderEl, {
      start:     [ min, max ],
      connect:   true,
      step:      1000,
      range:     { min, max },
      format: {
        to:   v => Math.round(v),
        from: v => Number(v),
      },
    });

    priceSlider.on( 'update', function (values) {
      document.getElementById('vn-price-min').value = values[0];
      document.getElementById('vn-price-max').value = values[1];
      // Format VND
      const fmt = n => Number(n).toLocaleString('vi-VN') + '₫';
      const minLabel = document.getElementById('vn-price-min-label');
      const maxLabel = document.getElementById('vn-price-max-label');
      if ( minLabel ) minLabel.textContent = fmt(values[0]);
      if ( maxLabel ) maxLabel.textContent = fmt(values[1]);
    });

    priceSlider.on( 'change', function() {
      debounceFilter(600);
    });
  }

  /* ── Accordion ───────────────────────────────────────────── */
  function initAccordion() {
    document.querySelectorAll('.vn-filter-group-toggle').forEach(function (btn) {
      const group = btn.closest('.vn-filter-group');
      const body  = group ? group.querySelector('.vn-filter-group-body') : null;
      if ( ! body ) return;

      // Bắt đầu ở trạng thái mở
      group.classList.add('is-open');

      btn.addEventListener('click', function() {
        const isOpen = group.classList.toggle('is-open');
        body.style.display = isOpen ? '' : 'none';
      });
    });
  }

  /* ── Form Submit ─────────────────────────────────────────── */
  function bindFormSubmit() {
    const form = document.getElementById('vn-filter-form');
    if ( ! form ) return;

    form.addEventListener('submit', function(e) {
      e.preventDefault();
      currentPage = 1;
      triggerFilter();
    });

    // Autosubmit khi thay đổi checkbox (debounced)
    form.querySelectorAll('input[type="checkbox"]').forEach(function(inp) {
      inp.addEventListener('change', function() {
        currentPage = 1;
        debounceFilter(400);
      });
    });
  }

  /* ── Orderby select ──────────────────────────────────────── */
  function bindOrderby() {
    const sel = document.getElementById('vn-orderby');
    if ( ! sel ) return;
    sel.addEventListener('change', function() {
      currentOrderby = this.value;
      currentPage    = 1;
      triggerFilter();
    });
  }

  /* ── Pagination ──────────────────────────────────────────── */
  function bindPageBtns() {
    document.addEventListener('click', function(e) {
      const btn = e.target.closest('.vn-page-btn');
      if ( ! btn ) return;
      const page = parseInt( btn.dataset.page, 10 );
      if ( page ) {
        currentPage = page;
        triggerFilter();
        // Scroll lên đầu sản phẩm
        const wrapper = document.getElementById('vn-products-wrapper');
        if ( wrapper ) {
          wrapper.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
      }
    });
  }

  /* ── Reset Button ────────────────────────────────────────── */
  function bindResetBtn() {
    document.addEventListener('click', function(e) {
      if ( ! e.target.closest('#vn-reset-filters') ) return;
      resetAllFilters();
    });
  }

  function bindResetInProducts() {
    document.addEventListener('click', function(e) {
      if ( ! e.target.closest('#vn-reset-all-filters') ) return;
      resetAllFilters();
    });
  }

  function resetAllFilters() {
    const form = document.getElementById('vn-filter-form');
    if ( ! form ) return;

    // Uncheck tất cả checkboxes
    form.querySelectorAll('input[type="checkbox"]').forEach(cb => { cb.checked = false; });

    // Reset price slider
    if ( priceSlider ) {
      const sliderEl = document.getElementById('vn-price-slider');
      if ( sliderEl ) {
        const min = parseFloat( sliderEl.dataset.min ) || 0;
        const max = parseFloat( sliderEl.dataset.max ) || 10000000;
        priceSlider.set([ min, max ]);
      }
    }

    currentPage = 1;
    triggerFilter();
  }

  /* ── Debounce ────────────────────────────────────────────── */
  function debounceFilter(delay) {
    clearTimeout(debounceTimer);
    debounceTimer = setTimeout(triggerFilter, delay);
  }

  /* ── AJAX Filter ─────────────────────────────────────────── */
  function triggerFilter() {
    const form = document.getElementById('vn-filter-form');
    if ( ! form ) return;

    const wrapper = document.getElementById('vn-products-wrapper');
    if ( wrapper ) wrapper.classList.add('is-loading');

    const applyBtn = document.getElementById('vn-apply-filter');
    if ( applyBtn ) applyBtn.classList.add('loading');

    const params = collectParams(form);
    params.action   = 'vn_filter_products';
    params.nonce    = VNF.nonce || '';
    params.paged    = currentPage;
    params.orderby  = currentOrderby;
    params.columns  = VNF.columns || 3;
    params.per_page = VNF.perPage || 12;

    $.post( VNF.ajaxUrl, params, function(response) {
      if ( wrapper ) wrapper.classList.remove('is-loading');
      if ( applyBtn ) applyBtn.classList.remove('loading');

      if ( response.success && response.data ) {
        // Replace nội dung sản phẩm
        const existingWrapper = document.getElementById('vn-products-wrapper');
        if ( existingWrapper ) {
          existingWrapper.outerHTML = response.data.html;
        }

        // Cập nhật số lượng tổng
        const countEl = document.getElementById('vn-total-count');
        if ( countEl ) {
          countEl.textContent = response.data.found_text || '';
        }

        // Cập nhật dynamic counts cho linked filters
        if ( response.data.counts ) {
          updateDynamicCounts( response.data.counts );
        }
      }
    }).fail(function() {
      if ( wrapper ) wrapper.classList.remove('is-loading');
      if ( applyBtn ) applyBtn.classList.remove('loading');
    });
  }

  /* ── Dynamic Linked Filters — cập nhật count & ẩn/hiện ─── */
  /**
   * Nhận `counts` từ server: { taxonomy: { term_id: count, ... }, ... }
   * Với mỗi filter item trong form:
   *   - Nếu term_id KHÔNG có trong counts (count = 0) => ẩn/mờ item
   *   - Nếu term_id CÓ trong counts => hiện item + cập nhật số count
   *   - Nếu item đang được check => luôn hiện (không ẩn)
   */
  function updateDynamicCounts( counts ) {
    const form = document.getElementById('vn-filter-form');
    if ( ! form ) return;

    // Xử lý category items
    updateTaxonomyItems( form, 'categories[]', 'product_cat', counts );

    // Xử lý tag items
    updateTaxonomyItems( form, 'tags[]', 'product_tag', counts );

    // Xử lý attribute items (pa_*)
    const attrInputs = form.querySelectorAll('input[name^="attributes["]');
    const attrSlugs  = new Set();
    attrInputs.forEach(function(inp) {
      const match = inp.name.match(/attributes\[(.+?)\]\[\]/);
      if ( match ) attrSlugs.add( match[1] );
    });
    attrSlugs.forEach(function(slug) {
      updateTaxonomyItems( form, 'attributes[' + slug + '][]', slug, counts );
    });
  }

  /**
   * Cập nhật từng item của một taxonomy
   * @param {HTMLElement} form     Form element
   * @param {string}      nameAttr Tên input attribute (ví dụ "categories[]")
   * @param {string}      taxonomy Taxonomy slug (ví dụ "product_cat")
   * @param {object}      counts   { taxonomy: { term_id: count } }
   */
  function updateTaxonomyItems( form, nameAttr, taxonomy, counts ) {
    const taxCounts = counts[ taxonomy ] || {}; // { term_id: count }
    const inputs    = form.querySelectorAll( 'input[name="' + nameAttr + '"]' );

    inputs.forEach(function(inp) {
      const termId  = parseInt( inp.value, 10 );
      const item    = inp.closest('.vn-filter-item') || inp.closest('.vn-tag-item');
      if ( ! item ) return;

      const isChecked = inp.checked;
      const count     = taxCounts[ termId ] !== undefined ? taxCounts[ termId ] : 0;

      // Cập nhật badge số lượng
      const badge = item.querySelector('.vn-item-count');
      if ( badge ) {
        badge.textContent = count > 0 ? '(' + count + ')' : '(0)';
        badge.classList.toggle('is-zero', count === 0);
      }

      // Ẩn/mờ items không có sản phẩm (trừ khi đang được chọn)
      if ( count === 0 && ! isChecked ) {
        item.classList.add('vn-item-disabled');
        item.classList.remove('is-checked');
      } else {
        item.classList.remove('vn-item-disabled');
        if ( isChecked ) {
          item.classList.add('is-checked');
        } else {
          item.classList.remove('is-checked');
        }
      }
    });
  }

  /* ── Collect Form Params ─────────────────────────────────── */
  function collectParams(form) {
    const params = {};

    // Categories
    const cats = form.querySelectorAll('input[name="categories[]"]:checked');
    if ( cats.length ) {
      params['categories[]'] = Array.from(cats).map(c => c.value);
    }

    // Attributes (nested: attributes[pa_color][] = 1,2...)
    const attrInputs = form.querySelectorAll('input[name^="attributes["]:checked');
    attrInputs.forEach(function(inp) {
      const match = inp.name.match(/attributes\[(.+?)\]\[\]/);
      if ( match ) {
        const slug = match[1];
        const key  = 'attributes[' + slug + '][]';
        if ( ! params[key] ) params[key] = [];
        params[key].push(inp.value);
      }
    });

    // Tags
    const tags = form.querySelectorAll('input[name="tags[]"]:checked');
    if ( tags.length ) {
      params['tags[]'] = Array.from(tags).map(t => t.value);
    }

    // Price
    const priceMin = document.getElementById('vn-price-min');
    const priceMax = document.getElementById('vn-price-max');
    if ( priceMin ) params.price_min = priceMin.value;
    if ( priceMax ) params.price_max = priceMax.value;

    // In stock
    const inStock = document.getElementById('vn-in-stock');
    if ( inStock && inStock.checked ) params.in_stock = 1;

    // Nonce field
    const nonceField = form.querySelector('#vn_filter_nonce_field');
    if ( nonceField ) params.nonce = nonceField.value;

    return params;
  }

  /* ── Document Ready ──────────────────────────────────────── */
  $(document).ready(function() {
    init();
  });

})(jQuery);
