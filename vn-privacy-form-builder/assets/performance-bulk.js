jQuery(document).ready(function($) {
    let imageIds = [];
    let currentIndex = 0;
    let batchSize = 5;
    let totalImages = 0;

    $('#vn-bulk-webp-scan').on('click', function(e) {
        e.preventDefault();
        const $btn = $(this);
        const $status = $('#vn-bulk-webp-status');
        const $startBtn = $('#vn-bulk-webp-start');

        $btn.prop('disabled', true).text('⏳ Đang quét...');
        $status.text('');
        $startBtn.hide();

        $.ajax({
            url: vnPerf.ajaxUrl,
            type: 'POST',
            data: {
                action: 'vn_perf_get_bulk_images',
                nonce: vnPerf.nonce
            },
            success: function(response) {
                $btn.prop('disabled', false).text('🔍 Quét lại thư viện');
                if (response.success) {
                    imageIds = response.data.ids;
                    totalImages = response.data.count;
                    $status.html(`Tìm thấy <strong>${totalImages}</strong> ảnh (JPEG/PNG) trong thư viện.`);
                    if (totalImages > 0) {
                        $startBtn.show();
                    }
                } else {
                    $status.text('Có lỗi xảy ra: ' + (response.data || 'Không rõ nguyên nhân'));
                }
            },
            error: function() {
                $btn.prop('disabled', false).text('🔍 Quét lại thư viện');
                $status.text('Lỗi kết nối máy chủ.');
            }
        });
    });

    $('#vn-bulk-webp-start').on('click', function(e) {
        e.preventDefault();
        if (imageIds.length === 0) return;

        const $btn = $(this);
        const $scanBtn = $('#vn-bulk-webp-scan');
        const $status = $('#vn-bulk-webp-status');
        const $progressWrap = $('#vn-bulk-webp-progress-wrap');
        const $progressBar = $('#vn-bulk-webp-progress-bar');
        const $progressTxt = $('#vn-bulk-webp-progress-txt');
        const $progressPct = $('#vn-bulk-webp-progress-pct');

        $btn.prop('disabled', true).text('⏳ Đang chạy...');
        $scanBtn.prop('disabled', true);
        $progressWrap.show();

        currentIndex = 0;
        runBatch();

        function runBatch() {
            if (currentIndex >= totalImages) {
                // Done
                $btn.text('⚡ Hoàn thành!').prop('disabled', false).fadeOut(2000);
                $scanBtn.prop('disabled', false);
                $status.html('🎉 <strong>Hoàn thành chuyển đổi WebP!</strong>');
                setTimeout(function() {
                    window.location.reload();
                }, 2000);
                return;
            }

            const batch = imageIds.slice(currentIndex, currentIndex + batchSize);
            const quality = $('#vn-webp-quality-slider').val() || 82;

            $progressTxt.text(`Đang xử lý: ${currentIndex}/${totalImages} ảnh...`);
            const pct = Math.round((currentIndex / totalImages) * 100);
            $progressBar.css('width', pct + '%');
            $progressPct.text(pct + '%');

            $.ajax({
                url: vnPerf.ajaxUrl,
                type: 'POST',
                data: {
                    action: 'vn_perf_convert_bulk_webp',
                    nonce: vnPerf.nonce,
                    ids: batch,
                    quality: quality
                },
                success: function(response) {
                    if (response.success) {
                        currentIndex += batch.length;
                        runBatch();
                    } else {
                        $status.html(`<span style="color:#ef4444;">Lỗi tại vị trí ${currentIndex}: ${response.data || 'Không rõ'}</span>`);
                        $btn.prop('disabled', false).text('⚡ Bắt đầu chuyển đổi');
                        $scanBtn.prop('disabled', false);
                    }
                },
                error: function() {
                    $status.html('<span style="color:#ef4444;">Lỗi kết nối khi đang chuyển đổi. Thử lại sau.</span>');
                    $btn.prop('disabled', false).text('⚡ Bắt đầu chuyển đổi');
                    $scanBtn.prop('disabled', false);
                }
            });
        }
    });
});
