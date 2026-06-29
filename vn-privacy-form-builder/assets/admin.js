/* global jQuery */
(function ($) {
    'use strict';

    /* ================================================================
       1. TAB SYSTEM
    ================================================================ */
    $(document).on('click', '.vn-tab-btn', function () {
        var target = $(this).data('tab');
        var $container = $(this).closest('.vn-tab-wrapper');

        $container.find('.vn-tab-btn').removeClass('active');
        $(this).addClass('active');

        $container.find('.vn-tab-pane').removeClass('active');
        $container.find('#vn-tab-' + target).addClass('active');

        // Persist active tab in sessionStorage
        try { sessionStorage.setItem('vn_active_tab_' + $container.attr('id'), target); } catch (e) {}
    });

    // Restore last active tab
    $(document).ready(function () {
        $('.vn-tab-wrapper').each(function () {
            var $container = $(this);
            var saved;
            try { saved = sessionStorage.getItem('vn_active_tab_' + $container.attr('id')); } catch (e) {}
            if (saved) {
                var $btn = $container.find('.vn-tab-btn[data-tab="' + saved + '"]');
                if ($btn.length) $btn.trigger('click');
            }
        });
    });

    /* ================================================================
       2. TOAST NOTIFICATIONS
    ================================================================ */
    window.vnToast = function (msg, type, duration) {
        type = type || 'success';
        duration = duration || 3500;

        var icons = { success: '✅', warning: '⚠️', error: '❌', info: 'ℹ️' };

        if (!document.getElementById('vn-toast-container')) {
            $('body').append('<div id="vn-toast-container"></div>');
        }

        var $toast = $('<div class="vn-toast ' + type + '">' +
            '<span>' + (icons[type] || '') + '</span>' +
            '<span>' + msg + '</span>' +
            '</div>');

        $('#vn-toast-container').append($toast);

        setTimeout(function () {
            $toast.addClass('out');
            setTimeout(function () { $toast.remove(); }, 300);
        }, duration);
    };

    /* ================================================================
       3. COPY SHORTCODE
    ================================================================ */
    $(document).on('click', '.vn-copy-btn', function () {
        var text = $(this).closest('.vn-shortcode-box').find('code').text();
        if (navigator.clipboard) {
            navigator.clipboard.writeText(text).then(function () {
                vnToast('Đã sao chép shortcode!', 'success', 2000);
            });
        } else {
            var $tmp = $('<textarea>').val(text).appendTo('body').select();
            document.execCommand('copy');
            $tmp.remove();
            vnToast('Đã sao chép shortcode!', 'success', 2000);
        }
    });

    /* ================================================================
       4. FULL-SITE BACKUP (Progressive AJAX)
    ================================================================ */
    $(document).on('click', '#btn-full-backup', function () {
        var $btn   = $(this);
        var nonce  = $btn.data('nonce');
        var $prog  = $('#vn-backup-progress-wrap');
        var $bar   = $('#vn-backup-bar');
        var $text  = $('#vn-backup-text');
        var mode   = $('#vn-backup-mode').val() || 'full';

        $btn.prop('disabled', true).html('⏳ Đang xử lý...');
        $prog.show();
        $bar.css({ width: '5%' });
        $text.text('Đang khởi tạo...');

        $.post(ajaxurl, { action: 'vn_privacy_backup_init', nonce: nonce, mode: mode })
            .done(function (r) {
                if (!r.success) { return _backupError($btn, $bar, $text, r.data); }
                $bar.css('width', '30%');
                $text.text('Đang sao lưu Database...');

                $.post(ajaxurl, { action: 'vn_privacy_backup_db', nonce: nonce })
                    .done(function (r2) {
                        if (!r2.success) { return _backupError($btn, $bar, $text, r2.data); }
                        
                        if (mode === 'db_only') {
                            $bar.css('width', '80%');
                            $text.text('Đang hoàn thiện bản sao lưu Database...');
                            _finishBackup($btn, $bar, $text, nonce);
                        } else {
                            $bar.css('width', '40%');
                            $text.text('Đang nén tệp tin...');
                            _loopFiles($btn, $bar, $text, nonce);
                        }
                    })
                    .fail(function () { _backupError($btn, $bar, $text, 'Lỗi kết nối khi backup DB.'); });
            })
            .fail(function () { _backupError($btn, $bar, $text, 'Lỗi kết nối khi khởi tạo.'); });
    });

    function _finishBackup($btn, $bar, $text, nonce) {
        $.post(ajaxurl, { action: 'vn_privacy_backup_finish', nonce: nonce })
            .done(function (r4) {
                if (!r4.success) { return _backupError($btn, $bar, $text, r4.data); }
                $bar.css({ width: '100%', background: 'var(--vn-success)' });
                $text.text('🎉 Hoàn tất! Đang tải file...');
                $btn.html('✅ Hoàn tất').css('background', 'var(--vn-success)');
                vnToast('Sao lưu hoàn tất!', 'success');
                setTimeout(function () { window.location.href = r4.data.download_url; }, 1000);
                setTimeout(function () { location.reload(); }, 2500);
            })
            .fail(function () { _backupError($btn, $bar, $text, 'Lỗi khi hoàn tất.'); });
    }

    function _loopFiles($btn, $bar, $text, nonce) {
        $.post(ajaxurl, { action: 'vn_privacy_backup_files', nonce: nonce })
            .done(function (r) {
                if (!r.success) { return _backupError($btn, $bar, $text, r.data); }
                var ui = 25 + parseInt(r.data.progress, 10) * 0.65;
                $bar.css('width', ui + '%');
                $text.text(r.data.message);

                if (r.data.is_done) {
                    $bar.css('width', '95%');
                    $text.text('Đang hoàn thiện...');
                    $.post(ajaxurl, { action: 'vn_privacy_backup_finish', nonce: nonce })
                        .done(function (r4) {
                            if (!r4.success) { return _backupError($btn, $bar, $text, r4.data); }
                            $bar.css({ width: '100%', background: 'var(--vn-success)' });
                            $text.text('🎉 Hoàn tất! Đang tải file...');
                            $btn.html('✅ Hoàn tất').css('background', 'var(--vn-success)');
                            vnToast('Sao lưu hoàn tất!', 'success');
                            setTimeout(function () { window.location.href = r4.data.download_url; }, 1000);
                            // Refresh backup list
                            setTimeout(function () { location.reload(); }, 2500);
                        })
                        .fail(function () { _backupError($btn, $bar, $text, 'Lỗi khi hoàn tất.'); });
                } else {
                    _loopFiles($btn, $bar, $text, nonce);
                }
            })
            .fail(function () { _backupError($btn, $bar, $text, 'Lỗi kết nối khi nén file.'); });
    }

    function _backupError($btn, $bar, $text, msg) {
        $bar.css('background', 'var(--vn-danger)');
        $text.text('❌ Lỗi: ' + msg);
        $btn.prop('disabled', false).html('🚀 Thử lại');
        vnToast('Lỗi sao lưu: ' + msg, 'error');
    }

    /* ================================================================
       5. TOOL ACTIONS (Flush transients, maintenance, etc.)
    ================================================================ */
    $(document).on('click', '.vn-tool-action-btn', function () {
        var $btn    = $(this);
        var action  = $btn.data('action');
        var nonce   = $btn.data('nonce');
        var confirm = $btn.data('confirm');

        if (confirm && !window.confirm(confirm)) return;

        $btn.prop('disabled', true).prepend('⏳ ');

        $.post(ajaxurl, { action: action, nonce: nonce })
            .done(function (r) {
                if (r.success) {
                    vnToast(r.data || 'Thao tác thành công!', 'success');
                } else {
                    vnToast('Lỗi: ' + (r.data || 'Không xác định'), 'error');
                }
            })
            .fail(function () { vnToast('Lỗi kết nối máy chủ.', 'error'); })
            .always(function () {
                $btn.prop('disabled', false);
                $btn.find('span:first').remove();
            });
    });

    /* ================================================================
       6. DELETE BACKUP FILE
    ================================================================ */
    $(document).on('click', '.vn-delete-backup-btn', function () {
        var $btn  = $(this);
        var file  = $btn.data('file');
        var nonce = $btn.data('nonce');
        if (!confirm('Xóa bản sao lưu "' + file + '"?')) return;

        $btn.prop('disabled', true).text('Đang xóa...');

        $.post(ajaxurl, { action: 'vn_privacy_delete_backup', nonce: nonce, file: file })
            .done(function (r) {
                if (r.success) {
                    $btn.closest('tr').fadeOut(300, function () { $(this).remove(); });
                    vnToast('Đã xóa bản sao lưu.', 'success');
                } else {
                    vnToast('Lỗi: ' + r.data, 'error');
                    $btn.prop('disabled', false).text('Xóa');
                }
            })
            .fail(function () { vnToast('Lỗi kết nối.', 'error'); $btn.prop('disabled', false).text('Xóa'); });
    });

    /* ================================================================
       7. FORM BUILDER — Live color preview
    ================================================================ */
    $(document).on('input', '#primary_color', function () {
        var c = $(this).val();
        var $preview = $('#vn-form-preview-btn');
        if ($preview.length) $preview.css({ background: c });
    });

    /* ================================================================
       8. ENTRY DETAIL MODAL
    ================================================================ */
    $(document).on('click', '.btn-view-entry-detail', function () {
        var $btn = $(this);
        $('#modal-detail-fullname').text($btn.data('fullname') || '');
        $('#modal-detail-phone').text($btn.data('phone') || '');
        $('#modal-detail-form').text($btn.data('form') || '');
        $('#modal-detail-message').html($btn.data('message') || '');
        $('#modal-detail-ip').text($btn.data('ip') || '');
        $('#modal-detail-time').text($btn.data('time') || '');
        $('#modal-detail-agent').text($btn.data('agent') || '');
        $('#vn-entry-detail-modal').css('display', 'flex');
    });

    $(document).on('click', '#close-detail-modal, #vn-entry-detail-modal', function (e) {
        if (e.target === this) {
            $('#vn-entry-detail-modal').css('display', 'none');
        }
    });

    /* ================================================================
       9. RESTORE DEFINITION & ENGINE (Polled, Timeout-Proof, Visual Steps)
    ================================================================ */
    var CHUNK_SIZE = 1.5 * 1024 * 1024; // 1.5 MB per chunk

    function updateRestoreStep(stepId, status, detail) {
        var $step = $('#step-' + stepId);
        if (!$step.length) return;
        
        var icons = {
            pending: '⚪',
            running: '⏳',
            success: '✅',
            error: '❌'
        };
        
        var labels = {
            upload: 'Tải lên tệp sao lưu',
            assemble: 'Ghép nối các phần tệp tin',
            extract: 'Giải nén & khôi phục mã nguồn',
            db: 'Nhập dữ liệu Database (SQL)',
            finish: 'Hoàn tất & tối ưu hóa hệ thống'
        };
        
        var icon = icons[status] || '⚪';
        var label = labels[stepId] || '';
        if (detail) {
            label += ' (' + detail + ')';
        }
        
        $step.find('.step-icon').html(icon);
        $step.find('.step-text').html(label);
        
        if (status === 'running') {
            $step.css('color', 'var(--vn-primary)').css('font-weight', '600');
        } else if (status === 'success') {
            $step.css('color', 'var(--vn-success)').css('font-weight', '600');
        } else if (status === 'error') {
            $step.css('color', 'var(--vn-danger)').css('font-weight', '600');
        } else {
            $step.css('color', 'var(--vn-muted)').css('font-weight', 'normal');
        }
    }

    function startRestoreEngine(restoreKey, nonce, isUploaded, $btn, $bar, $text, $wrap) {
        // Step 3: Extract + copy files
        updateRestoreStep('extract', 'running');
        $bar.css('width', '85%');
        
        $.ajax({
            url:     ajaxurl,
            type:    'POST',
            timeout: 600000, // 10 mins
            data:    { action: 'vn_privacy_restore_step_files', nonce: nonce, restore_key: restoreKey }
        })
        .done(function (r) {
            if (!r.success) {
                _restoreError(r.data || 'Lỗi khôi phục tệp tin.');
                return;
            }
            updateRestoreStep('extract', 'success');
            
            // Step 4: Import DB
            updateRestoreStep('db', 'running', '0%');
            _doDbRestorePolled(0, nonce, restoreKey);
        })
        .fail(function (xhr, status) {
            _restoreError('Mất kết nối khi giải nén tệp tin.');
        });

        function _doDbRestorePolled(offset, nonce, restoreKey) {
            $.ajax({
                url:     ajaxurl,
                type:    'POST',
                timeout: 120000, // 2 mins
                data: {
                    action:      'vn_privacy_restore_step_db',
                    nonce:       nonce,
                    restore_key: restoreKey,
                    db_offset:   offset
                }
            })
            .done(function(r) {
                if (!r.success) {
                    _restoreError(r.data || 'Lỗi import DB.');
                    return;
                }
                if (r.data.done) {
                    // Step 5: Finish
                    updateRestoreStep('db', 'success', '100%');
                    updateRestoreStep('finish', 'running');
                    $bar.css({ width: '100%', background: 'var(--vn-success)' });
                    
                    setTimeout(function() {
                        updateRestoreStep('finish', 'success');
                        $text.html('✅ <strong>Khôi phục thành công!</strong> Hệ thống sẽ tự đăng xuất trong 3 giây.');
                        $btn.text('✅ Hoàn tất').css('background', 'var(--vn-success)');
                        vnToast('✅ Khôi phục thành công!', 'success', 5000);
                        setTimeout(function() {
                            window.location.href = ajaxurl.replace('admin-ajax.php', '') + 'wp-login.php?loggedout=true';
                        }, 3000);
                    }, 1000);
                } else {
                    var pctDb = r.data.progress_pct || 0;
                    updateRestoreStep('db', 'running', pctDb + '%');
                    var barPct = 85 + Math.round(pctDb * 0.13); // 85-98%
                    $bar.css('width', Math.min(barPct, 98) + '%');
                    _doDbRestorePolled(r.data.db_offset, nonce, restoreKey);
                }
            })
            .fail(function() {
                _restoreError('Mất kết nối khi import DB.');
            });
        }

        function _restoreError(msg) {
            $bar.css('background', 'var(--vn-danger)');
            updateRestoreStep('extract', 'error', msg);
            updateRestoreStep('db', 'error', 'Bị gián đoạn');
            $btn.prop('disabled', false).text(isUploaded ? '📥 Tải lên & Khôi phục' : '🔄 Khôi phục');
            vnToast('Lỗi: ' + msg, 'error');
        }
    }

    // A. RESTORE FROM SERVER BACKUPS (Polled Flow)
    $(document).on('click', '.vn-restore-server-btn', function () {
        var $btn  = $(this);
        var file  = $btn.data('file');
        var nonce = $btn.data('nonce');

        if (!confirm('⚠️ CẢNH BÁO: Khôi phục sẽ XÓA và thay thế toàn bộ dữ liệu hiện tại bằng bản sao lưu "' + file + '"!\n\nBạn có chắc chắn không?')) return;

        // Scroll to and show progress wrap in the main restore section
        var $bar        = $('#vn-restore-bar');
        var $text       = $('#vn-restore-text');
        var $wrap       = $('#vn-restore-progress-wrap');

        $btn.prop('disabled', true).text('⏳ Đang xử lý...');
        $wrap.show();
        $bar.css({ width: '10%', background: 'linear-gradient(90deg, var(--vn-accent), var(--vn-accent-light))' });
        $text.text('Đang khởi tạo khôi phục...');
        
        $('html, body').animate({
            scrollTop: $wrap.offset().top - 120
        }, 500);

        updateRestoreStep('upload', 'success', 'Đã có trên server');
        updateRestoreStep('assemble', 'success', 'Không cần ghép');
        updateRestoreStep('extract', 'pending');
        updateRestoreStep('db', 'pending');
        updateRestoreStep('finish', 'pending');

        $.post(ajaxurl, {
            action: 'vn_privacy_restore_server_init',
            nonce:  nonce,
            file:   file
        })
        .done(function (r) {
            if (!r.success) {
                $bar.css('background', 'var(--vn-danger)');
                $text.text('❌ Lỗi: ' + r.data);
                $btn.prop('disabled', false).text('🔄 Khôi phục');
                vnToast('Lỗi: ' + r.data, 'error');
                return;
            }
            startRestoreEngine(r.data.restore_key, nonce, false, $btn, $bar, $text, $wrap);
        })
        .fail(function () {
            $bar.css('background', 'var(--vn-danger)');
            $text.text('❌ Lỗi kết nối khi khởi tạo.');
            $btn.prop('disabled', false).text('🔄 Khôi phục');
            vnToast('Lỗi kết nối máy chủ.', 'error');
        });
    });

    // B. UPLOAD ZIP RESTORE (Polled Flow)
    $(document).on('change', '#vn-restore-file-input', function () {
        var file = this.files[0];
        if (!file) return;
        var sizeMB = (file.size / 1048576).toFixed(2);
        var chunks = Math.ceil(file.size / CHUNK_SIZE);
        $('#vn-restore-file-info').text(
            '📦 ' + file.name + ' (' + sizeMB + ' MB) — sẽ tải lên ' + chunks + ' phần.'
        );
    });

    $(document).on('click', '#btn-chunked-restore', function () {
        var $btn    = $(this);
        var nonce   = $btn.data('nonce');
        var fileInput = document.getElementById('vn-restore-file-input');

        if (!fileInput || !fileInput.files[0]) {
            vnToast('Vui lòng chọn file sao lưu (.zip)', 'warning');
            return;
        }

        if (!confirm('⚠️ CẢNH BÁO: Khôi phục sẽ XÓA và thay thế toàn bộ dữ liệu hiện tại!\n\nBạn có chắc chắn không?')) return;

        var file        = fileInput.files[0];
        var totalChunks = Math.ceil(file.size / CHUNK_SIZE);
        var uploadId    = 'up_' + Date.now() + '_' + Math.random().toString(36).substr(2, 6);
        var $bar        = $('#vn-restore-bar');
        var $text       = $('#vn-restore-text');
        var $wrap       = $('#vn-restore-progress-wrap');

        $btn.prop('disabled', true).text('⏳ Đang tải lên...');
        $wrap.show();
        $bar.css({ width: '0%', background: 'linear-gradient(90deg, var(--vn-accent), var(--vn-accent-light))' });
        $text.text('Đang chuẩn bị...');

        updateRestoreStep('upload', 'running', '0%');
        updateRestoreStep('assemble', 'pending');
        updateRestoreStep('extract', 'pending');
        updateRestoreStep('db', 'pending');
        updateRestoreStep('finish', 'pending');

        _uploadChunk(0);

        function _uploadChunk(index) {
            var start = index * CHUNK_SIZE;
            var end   = Math.min(start + CHUNK_SIZE, file.size);
            var blob  = file.slice(start, end);

            var fd = new FormData();
            fd.append('action',       'vn_privacy_chunk_upload');
            fd.append('nonce',        nonce);
            fd.append('chunk_index',  index);
            fd.append('total_chunks', totalChunks);
            fd.append('upload_id',    uploadId);
            fd.append('chunk',        blob, 'chunk_' + index + '.bin');

            $.ajax({
                url:         ajaxurl,
                type:        'POST',
                data:        fd,
                processData: false,
                contentType: false,
                timeout:     120000,
                success: function (r) {
                    if (!r.success) {
                        _uploadError('Lỗi chunk ' + index + ': ' + r.data);
                        return;
                    }
                    var pct = Math.round(((index + 1) / totalChunks) * 100);
                    var barPct = Math.round(pct * 0.7); // 0-70% during upload
                    $bar.css('width', barPct + '%');
                    updateRestoreStep('upload', 'running', pct + '%');

                    if (index + 1 < totalChunks) {
                        _uploadChunk(index + 1);
                    } else {
                        updateRestoreStep('upload', 'success', '100%');
                        updateRestoreStep('assemble', 'running');
                        $bar.css('width', '75%');

                        $.ajax({
                            url:     ajaxurl,
                            type:    'POST',
                            timeout: 300000,
                            data:    { action: 'vn_privacy_chunk_restore_apply', nonce: nonce, upload_id: uploadId, total_chunks: totalChunks }
                        })
                        .done(function (r2) {
                            if (!r2.success) {
                                _uploadError(r2.data || 'Lỗi ghép file ZIP.');
                                return;
                            }
                            updateRestoreStep('assemble', 'success');
                            startRestoreEngine(r2.data.restore_key, nonce, true, $btn, $bar, $text, $wrap);
                        })
                        .fail(function () {
                            _uploadError('Mất kết nối khi ghép file ZIP.');
                        });
                    }
                },
                error: function (xhr, status) {
                    var msg = status === 'timeout'
                        ? 'Quá thời gian tải chunk ' + index + '. Kết nối chậm — thử lại.'
                        : 'Lỗi kết nối khi tải chunk ' + index;
                    _uploadError(msg);
                }
            });
        }
        function _uploadError(msg) {
            $bar.css('background', 'var(--vn-danger)');
            updateRestoreStep('upload', 'error', msg);
            $btn.prop('disabled', false).text('📥 Tải lên & Khôi phục');
            vnToast('Lỗi: ' + msg, 'error');
        }
    });

    /* ================================================================
       CHUNKED DOWNLOAD — dùng Fetch + Range headers
       Tránh Cloudflare/Nginx timeout khi tải file lớn (>100MB)
       Chia file thành nhiều phần 10MB, ghép Blob client-side
    ================================================================ */
    var DOWNLOAD_CHUNK = 10 * 1024 * 1024; // 10 MB mỗi phần

    $(document).on('click', '.vn-chunked-download-btn', function () {
        var $btn      = $(this);
        var url       = $btn.data('url');
        var filename  = $btn.data('filename');
        var totalSize = parseInt($btn.data('size'), 10);

        if (!url || !filename || !totalSize) {
            vnToast('Không tìm thấy thông tin file để tải.', 'error');
            return;
        }

        // Fallback: nếu trình duyệt không hỗ trợ Fetch, dùng link thông thường
        if (typeof fetch === 'undefined') {
            window.location.href = url;
            return;
        }

        $btn.prop('disabled', true).html('⏳ 0%');
        var totalChunks = Math.ceil(totalSize / DOWNLOAD_CHUNK);
        var chunks      = [];
        var chunksDone  = 0;

        function _fetchChunk(index) {
            var rangeStart = index * DOWNLOAD_CHUNK;
            var rangeEnd   = Math.min(rangeStart + DOWNLOAD_CHUNK - 1, totalSize - 1);

            fetch(url, {
                headers: { 'Range': 'bytes=' + rangeStart + '-' + rangeEnd }
            })
            .then(function (res) {
                if (!res.ok && res.status !== 206) {
                    throw new Error('HTTP ' + res.status + ' khi tải phần ' + index);
                }
                return res.arrayBuffer();
            })
            .then(function (buf) {
                chunks[index] = buf;
                chunksDone++;
                var pct = Math.round((chunksDone / totalChunks) * 100);
                $btn.html('⏳ ' + pct + '%');

                if (chunksDone < totalChunks) {
                    _fetchChunk(chunksDone); // sequential — tránh overload server
                } else {
                    // Ghép tất cả chunk thành 1 Blob rồi tạo link tải
                    var blob    = new Blob(chunks, { type: 'application/zip' });
                    var objUrl  = URL.createObjectURL(blob);
                    var tmpLink = document.createElement('a');
                    tmpLink.href     = objUrl;
                    tmpLink.download = filename;
                    document.body.appendChild(tmpLink);
                    tmpLink.click();
                    document.body.removeChild(tmpLink);
                    setTimeout(function () { URL.revokeObjectURL(objUrl); }, 10000);

                    $btn.prop('disabled', false).html('⬇️ Tải về');
                    vnToast('Tải xuống hoàn tất: ' + filename, 'success');
                }
            })
            .catch(function (err) {
                $btn.prop('disabled', false).html('⬇️ Tải về');
                vnToast('Lỗi tải chunk ' + index + ': ' + err.message, 'error');
            });
        }

        _fetchChunk(0);
    });

    /* ================================================================
       11. NEW BACKUP FEATURES (Verify, Note, Run Auto Backup Now)
    ================================================================ */

    // Verify backup
    $(document).on('click', '.vn-verify-backup-btn', function () {
        var $btn = $(this);
        var file = $btn.data('file');
        var nonce = $btn.data('nonce');

        $btn.prop('disabled', true).text('⏳');
        vnToast('Đang quét và xác minh tệp tin...', 'info');

        $.post(ajaxurl, {
            action: 'vn_privacy_verify_backup',
            nonce: nonce,
            file: file
        })
        .done(function (r) {
            if (r.success) {
                var d = r.data;
                var msg = 'Bản sao lưu hợp lệ!\n- DB: ' + (d.has_db ? 'Có' : 'Không') + '\n- Số lượng file: ' + d.file_count + '\n- Domain gốc: ' + d.site_url + '\n- Prefix: ' + d.wp_prefix;
                alert(msg);
                location.reload();
            } else {
                vnToast('Xác minh thất bại: ' + r.data, 'error');
            }
        })
        .fail(function () {
            vnToast('Lỗi kết nối máy chủ khi xác minh.', 'error');
        })
        .always(function () {
            $btn.prop('disabled', false).text('🔍');
        });
    });

    // Save backup note on input change (blur or enter)
    $(document).on('change', '.vn-backup-note-input', function () {
        var $input = $(this);
        var file = $input.data('file');
        var nonce = $input.data('nonce');
        var note = $input.val();

        $input.css('border-color', 'var(--vn-accent)');
        $.post(ajaxurl, {
            action: 'vn_privacy_save_backup_note',
            nonce: nonce,
            file: file,
            note: note
        })
        .done(function (r) {
            if (r.success) {
                vnToast('Đã lưu ghi chú!', 'success');
                $input.css('border-color', 'var(--vn-success)');
            } else {
                vnToast('Lỗi: ' + r.data, 'error');
                $input.css('border-color', 'var(--vn-danger)');
            }
        })
        .fail(function () {
            vnToast('Lỗi kết nối khi lưu ghi chú.', 'error');
            $input.css('border-color', 'var(--vn-danger)');
        });
    });

    // Run auto backup now
    $(document).on('click', '#btn-run-auto-backup-now', function (e) {
        e.preventDefault();
        var $btn = $(this);
        var nonce = $btn.data('nonce');

        $btn.prop('disabled', true).text('⏳ Đang sao lưu...');
        vnToast('Đang chạy sao lưu tự động dưới nền...', 'info');

        $.post(ajaxurl, {
            action: 'vn_privacy_run_auto_backup',
            nonce: nonce
        })
        .done(function (r) {
            if (r.success) {
                vnToast('Sao lưu tự động thành công!', 'success');
                location.reload();
            } else {
                vnToast('Lỗi: ' + r.data, 'error');
                $btn.prop('disabled', false).text('▶ Chạy ngay');
            }
        })
        .fail(function () {
            vnToast('Lỗi kết nối máy chủ.', 'error');
            $btn.prop('disabled', false).text('▶ Chạy ngay');
        });
    });

    // Start malware scan
    $(document).on('click', '#btn-start-malware-scan', function (e) {
        e.preventDefault();
        var $btn = $(this);
        var nonce = $btn.data('nonce');
        var $wrap = $('#malware-scan-progress-wrap');
        var $results = $('#malware-scan-results');

        $btn.prop('disabled', true).text('⏳ Đang quét...');
        $wrap.show();
        $results.html('');

        $.post(ajaxurl, {
            action: 'vn_privacy_scan_malware',
            nonce: nonce
        })
        .done(function (r) {
            $wrap.hide();
            $btn.prop('disabled', false).text('🔍 Bắt đầu Quét Hệ Thống');
            
            if (!r.success) {
                vnToast('Lỗi: ' + r.data, 'error');
                $results.html('<div class="vn-alert vn-alert-danger">❌ Lỗi: ' + r.data + '</div>');
                return;
            }

            var files = r.data;
            if (files.length === 0) {
                $results.html(
                    '<div class="vn-alert vn-alert-success">' +
                    '🟢 <strong>Tuyệt vời!</strong> Không phát hiện bất kỳ tập tin chứa dấu hiệu mã độc hay backdoor nào.' +
                    '</div>'
                );
                vnToast('Quét hoàn tất: Hệ thống sạch!', 'success');
            } else {
                var html = '<div class="vn-alert vn-alert-warning">' +
                    '⚠️ <strong>Cảnh báo:</strong> Phát hiện <strong>' + files.length + '</strong> tập tin có dấu hiệu đáng ngờ. Hãy kiểm tra kỹ.' +
                    '</div>';

                html += '<table style="width:100%;border-collapse:collapse;font-size:13px;background:#fff;border:1px solid #e2e8f0;border-radius:8px;overflow:hidden;">' +
                    '<thead>' +
                    '<tr style="background:#f8fafc;border-bottom:2px solid #e2e8f0;">' +
                    '<th style="padding:10px 12px;text-align:left;">Đường dẫn tệp tin</th>' +
                    '<th style="padding:10px 12px;text-align:left;width:150px;">Mã phát hiện</th>' +
                    '<th style="padding:10px 12px;text-align:left;">Mô tả mối nguy hại</th>' +
                    '<th style="padding:10px 12px;text-align:left;width:150px;">Ngày sửa đổi</th>' +
                    '</tr>' +
                    '</thead>' +
                    '<tbody>';

                $.each(files, function (i, f) {
                    var dateStr = new Date(f.mtime * 1000).toLocaleString();
                    html += '<tr style="border-bottom:1px solid #e2e8f0;background:' + (i % 2 === 0 ? '#fff' : '#f8fafc') + '">' +
                        '<td style="padding:10px 12px;font-family:monospace;font-weight:600;color:#dc2626;word-break:break-all;">' + f.file + '</td>' +
                        '<td style="padding:10px 12px;font-family:monospace;"><span style="background:#fee2e2;color:#dc2626;padding:2px 6px;border-radius:4px;font-weight:700;">' + f.signature + '</span></td>' +
                        '<td style="padding:10px 12px;color:#475569;">' + f.desc + '</td>' +
                        '<td style="padding:10px 12px;color:#64748b;font-size:12px;">' + dateStr + '</td>' +
                        '</tr>';
                });

                html += '</tbody></table>';
                $results.html(html);
                vnToast('Phát hiện tệp nguy hiểm!', 'warning');
            }
        })
        .fail(function () {
            $wrap.hide();
            $btn.prop('disabled', false).text('🔍 Bắt đầu Quét Hệ Thống');
            vnToast('Lỗi kết nối máy chủ.', 'error');
        });
    });


    /* ================================================================
       MALWARE SCANNER (Updated for new UI)
    ================================================================ */
    $(document).on('click', '#btn-start-malware-scan', function () {
        var $btn     = $(this);
        var $wrap    = $('#malware-scan-progress-wrap');
        var $results = $('#malware-scan-results');
        var nonce    = $btn.data('nonce');

        $btn.prop('disabled', true).html('⏳ Đang quét...');
        $wrap.show();
        $results.html('');

        // Animate progress bar
        var $fill = $('#malware-progress-fill');
        $fill.css('width','5%');
        var pct = 5;
        var timer = setInterval(function(){
            pct = Math.min(pct + Math.random() * 8, 88);
            $fill.css('width', pct + '%');
        }, 400);

        $.ajax({
            url: (window.vnSec && vnSec.ajaxurl) || ajaxurl,
            method: 'POST',
            timeout: 360000,
            data: { action: 'vn_scan_malware', nonce: nonce }
        })
        .done(function (r) {
            clearInterval(timer);
            $fill.css('width','100%');
            setTimeout(function(){ $wrap.hide(); }, 500);
            $btn.prop('disabled', false).html('🔍 Bắt đầu Quét Hệ Thống');

            if (!r.success) {
                $results.html('<div class="vn-alert vn-alert-error">❌ ' + (r.data || 'Lỗi không xác định') + '</div>');
                return;
            }

            var files = r.data;
            if (!files || files.length === 0) {
                $results.html('<div class="vn-alert vn-alert-success">✅ Tuyệt vời! Không phát hiện mã độc hay tệp nguy hiểm nào.</div>');
                return;
            }

            var html = '<div class="vn-card" style="margin-top:0;border-color:#fecaca;">'
                + '<h3 class="vn-card-title" style="color:#dc2626;">⚠️ Phát hiện ' + files.length + ' tệp đáng ngờ!</h3>'
                + '<div class="vn-table-wrap"><table class="vn-table">'
                + '<thead><tr><th>Tệp tin</th><th>Chữ ký phát hiện</th><th>Mô tả</th><th>Ngày sửa</th></tr></thead>'
                + '<tbody>';

            $.each(files, function (i, f) {
                var dateStr = new Date(f.mtime * 1000).toLocaleString('vi-VN');
                html += '<tr class="' + (i%2?'alt':'') + '">'
                    + '<td class="vn-mono" style="color:#dc2626;word-break:break-all;font-weight:600;">' + f.file + '</td>'
                    + '<td><span class="vn-badge vn-badge-red">' + f.signature + '</span></td>'
                    + '<td class="vn-muted">' + f.desc + '</td>'
                    + '<td class="vn-muted small">' + dateStr + '</td>'
                    + '</tr>';
            });
            html += '</tbody></table></div></div>';
            $results.html(html);
            if (window.vnToast) vnToast('Phát hiện ' + files.length + ' tệp nguy hiểm!', 'warning');
        })
        .fail(function () {
            clearInterval(timer);
            $wrap.hide();
            $btn.prop('disabled', false).html('🔍 Bắt đầu Quét Hệ Thống');
            $results.html('<div class="vn-alert vn-alert-error">❌ Lỗi kết nối máy chủ. Vui lòng thử lại.</div>');
        });
    });

})(jQuery);


