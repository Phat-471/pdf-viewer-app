/**
 * VN Performance Module — Admin JavaScript
 * Xử lý AJAX dọn dẹp Database
 */
(function($){
  'use strict';

  $(document).ready(function(){

    // ── Dọn dẹp Database ─────────────────────────────────────
    $('#vn-clean-db-btn').on('click', function(){
      const btn = $(this);
      const result = $('#vn-clean-result');

      // Thu thập options từ checkbox
      const options = {
        action:          'vn_perf_clean_db',
        nonce:           vnPerf.nonce,
        revisions:       $('input[name="clean_revisions"]').is(':checked') ? 1 : 0,
        spam:            $('input[name="clean_spam"]').is(':checked') ? 1 : 0,
        transients:      $('input[name="clean_transients"]').is(':checked') ? 1 : 0,
        trash:           $('input[name="clean_trash"]').is(':checked') ? 1 : 0,
        optimize:        $('input[name="clean_optimize"]').is(':checked') ? 1 : 0,
        keep_revisions:  $('#keep-revisions').val() || 3,
      };

      // Xác nhận trước khi xóa
      if ( ! confirm('Bạn có chắc muốn dọn dẹp database? Thao tác này không thể hoàn tác.') ) return;

      btn.prop('disabled', true).html('⏳ Đang dọn dẹp...');
      result.hide();

      $.post( vnPerf.ajaxUrl, options, function(response){
        btn.prop('disabled', false).html('🧹 Dọn dẹp ngay');

        if ( response.success && response.data ) {
          const d = response.data;
          const c = d.cleaned || {};
          const s = d.stats || {};

          let html = '<div style="background:#f0fdf4;border:1px solid #bbf7d0;border-radius:8px;padding:14px;">';
          html += '<strong style="color:#166534;">✅ ' + ( d.message || 'Hoàn tất!' ) + '</strong>';
          html += '<ul style="margin:10px 0 0;padding-left:18px;font-size:13px;color:#166534;">';

          if ( c.revisions !== undefined ) html += '<li>Đã xóa <strong>' + c.revisions + '</strong> revision</li>';
          if ( c.spam      !== undefined ) html += '<li>Đã xóa <strong>' + c.spam + '</strong> bình luận spam</li>';
          if ( c.transients!== undefined ) html += '<li>Đã xóa <strong>' + c.transients + '</strong> transients</li>';
          if ( c.trash     !== undefined ) html += '<li>Đã xóa <strong>' + c.trash + '</strong> bài trash</li>';
          if ( c.optimized !== undefined ) html += '<li>Đã optimize <strong>' + c.optimized + '</strong> bảng</li>';

          html += '</ul></div>';

          // Cập nhật stats hiển thị
          if ( s ) {
            if ( s.revisions   !== undefined ) $('#stat-revisions').text(    Number(s.revisions).toLocaleString() );
            if ( s.spam        !== undefined ) $('#stat-spam').text(         Number(s.spam).toLocaleString() );
            if ( s.trash_posts !== undefined ) $('#stat-trash_posts').text(  Number(s.trash_posts).toLocaleString() );
            if ( s.expired_trans!== undefined) $('#stat-expired_trans').text(Number(s.expired_trans).toLocaleString() );
            if ( s.orphan_meta !== undefined ) $('#stat-orphan_meta').text(  Number(s.orphan_meta).toLocaleString() );
          }

          result.html(html).fadeIn();
        } else {
          result.html('<div style="background:#fef2f2;border:1px solid #fecaca;border-radius:8px;padding:12px;color:#991b1b;">❌ Có lỗi: ' + ( response.data?.message || 'Không rõ lỗi' ) + '</div>').fadeIn();
        }
      }).fail(function(){
        btn.prop('disabled', false).html('🧹 Dọn dẹp ngay');
        result.html('<div style="background:#fef2f2;border:1px solid #fecaca;border-radius:8px;padding:12px;color:#991b1b;">❌ Lỗi kết nối. Vui lòng thử lại.</div>').fadeIn();
      });
    });

    // ── Radio buttons nút liên hệ (highlight selected) ────────
    $('input[name="contact_position"]').on('change', function(){
      $('input[name="contact_position"]').each(function(){
        const lbl = $(this).closest('label');
        if ( $(this).is(':checked') ) {
          lbl.css({ 'border-color': '#059669', 'background': '#f0fdf4' });
        } else {
          lbl.css({ 'border-color': '#e2e8f0', 'background': '#fff' });
        }
      });
    });

  });

})(jQuery);
