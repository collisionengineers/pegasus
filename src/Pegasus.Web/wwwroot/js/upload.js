// Upload (v30 E). Progressive only: the native file input, the GET search
// form, the radio cards and the POST forms all work without this file; it
// keeps the accumulated selection, validates it against the declared limits,
// switches the inspected file and fills the review dialogs in place.
(function () {
    'use strict';

    var ICON_PREFIX = '#icon-';

    function glyph(id) {
        var svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        svg.setAttribute('class', 'icon');
        svg.setAttribute('aria-hidden', 'true');
        var use = document.createElementNS('http://www.w3.org/2000/svg', 'use');
        use.setAttribute('href', ICON_PREFIX + id);
        svg.appendChild(use);
        return svg;
    }

    function fileIcon(kind) {
        var wrap = document.createElement('span');
        wrap.className = 'up-file-icon';
        wrap.appendChild(glyph(/document/i.test(kind || '') ? 'file-text' : 'file'));
        return wrap;
    }

    function mib(bytes) {
        return (bytes / 1048576).toFixed(2) + ' MiB';
    }

    function files(count) {
        return count === 1 ? '1 file' : count + ' files';
    }

    function kindOf(file) {
        var type = (file.type || '').toLowerCase();
        if (type === 'image/jpeg') { return 'JPEG image'; }
        if (type === 'image/png') { return 'PNG image'; }
        if (type.indexOf('image/') === 0) { return 'Image'; }
        if (type === 'application/pdf') { return 'PDF document'; }
        if (type === 'application/msword' || type.indexOf('wordprocessingml') >= 0) { return 'Word document'; }
        if (type === 'message/rfc822' || type === 'application/vnd.ms-outlook') { return 'Email'; }
        if (type === 'video/mp4' || type === 'video/quicktime') { return 'Video'; }
        var extension = (file.name.split('.').pop() || '').toUpperCase();
        return extension && extension !== file.name.toUpperCase() ? extension + ' file' : 'File';
    }

    function openDialog(id, opener) {
        var dialog = document.getElementById(id);
        if (dialog && typeof dialog.pegasusOpen === 'function') {
            dialog.pegasusOpen(opener);
            return true;
        }
        return false;
    }

    // ------------------------------------------------------------------
    // Choosing files.
    // ------------------------------------------------------------------
    var select = document.querySelector('[data-upload-select]');
    if (select && typeof DataTransfer === 'function') {
        var form = select.querySelector('[data-upload-form]');
        var input = form.querySelector('input[type="file"]');
        var drop = form.querySelector('[data-drop]');
        var heading = form.querySelector('[data-select-heading]');
        var dropHeading = form.querySelector('[data-drop-heading]');
        var choose = form.querySelector('[data-select-choose]');
        var actions = form.querySelector('[data-select-actions]');
        var summary = form.querySelector('[data-select-summary]');
        var submit = form.querySelector('[data-select-submit]');
        var submitLabel = form.querySelector('[data-select-submit-label]');
        var errors = form.querySelector('[data-select-errors]');
        var errorList = form.querySelector('[data-select-error-list]');
        var panel = select.querySelector('[data-select-files]');
        var count = select.querySelector('[data-select-count]');
        var list = select.querySelector('[data-select-list]');
        var maxFiles = Number(select.getAttribute('data-max-files')) || 20;
        var maxBytes = Number(select.getAttribute('data-max-bytes')) || 104857600;
        var maxTotal = Number(select.getAttribute('data-max-total')) || 209715200;
        var accepted = /\.(jpe?g|png|pdf|docx?|eml|msg|mp4|mov)$/i;
        var store = new DataTransfer();
        var previews = [];
        var uploading = false;

        select.classList.add('is-enhanced');

        var revoke = function () {
            previews.forEach(function (url) { URL.revokeObjectURL(url); });
            previews = [];
        };

        var render = function () {
            var chosen = Array.from(store.files);
            var total = chosen.reduce(function (sum, file) { return sum + file.size; }, 0);
            var any = chosen.length > 0;
            heading.textContent = any ? 'Ready to upload' : 'Choose the files for this upload';
            dropHeading.textContent = any ? 'Add more files' : 'Drop files here';
            choose.classList.toggle('btn--primary', !any);
            actions.hidden = !any;
            summary.textContent = any ? files(chosen.length) + ' · ' + mib(total) : '';
            submitLabel.textContent = 'Upload ' + files(chosen.length);
            panel.hidden = !any;
            count.textContent = any ? files(chosen.length) + ' · ' + mib(total) : '';
            revoke();
            list.replaceChildren.apply(list, chosen.map(function (file, index) {
                var row = document.createElement('li');
                row.className = 'up-file';
                var thumb = document.createElement('span');
                thumb.className = 'up-thumb';
                if ((file.type || '').indexOf('image/') === 0) {
                    var url = URL.createObjectURL(file);
                    previews.push(url);
                    var img = document.createElement('img');
                    img.src = url;
                    img.alt = '';
                    thumb.appendChild(img);
                } else {
                    thumb.appendChild(fileIcon(kindOf(file)));
                }
                var name = document.createElement('div');
                name.className = 'up-file-name';
                var title = document.createElement('span');
                title.textContent = file.name;
                title.title = file.name;
                var meta = document.createElement('small');
                meta.textContent = mib(file.size) + ' · ' + kindOf(file);
                name.append(title, meta);
                var tail;
                if (uploading) {
                    tail = document.createElement('span');
                    tail.className = 'up-file-state is-running';
                    tail.append(glyph('upload'), document.createTextNode('Uploading'));
                } else {
                    tail = document.createElement('button');
                    tail.type = 'button';
                    tail.className = 'up-remove';
                    tail.setAttribute('data-remove', String(index));
                    tail.setAttribute('aria-label', 'Remove ' + file.name);
                    tail.appendChild(glyph('x'));
                }
                row.append(thumb, name, tail);
                return row;
            }));
        };

        var showErrors = function (messages) {
            errorList.replaceChildren.apply(errorList, messages.map(function (message) {
                var line = document.createElement('p');
                line.textContent = message;
                return line;
            }));
            errors.hidden = messages.length === 0;
            if (messages.length) {
                errors.focus();
            }
        };

        var addFiles = function (additions) {
            var current = Array.from(store.files);
            var all = current.concat(additions.filter(function (file) {
                return !current.some(function (existing) {
                    return existing.name === file.name && existing.size === file.size && existing.lastModified === file.lastModified;
                });
            }));
            var messages = [];
            if (all.length > maxFiles) {
                messages.push('Choose no more than ' + maxFiles + ' files.');
            }
            if (all.reduce(function (sum, file) { return sum + file.size; }, 0) > maxTotal) {
                messages.push('The upload must be ' + mib(maxTotal) + ' or less.');
            }
            all.forEach(function (file) {
                if (!accepted.test(file.name)) { messages.push(file.name + ': this file type is not supported.'); }
                if (file.size > maxBytes) { messages.push(file.name + ': this file exceeds ' + mib(maxBytes) + '.'); }
                if (file.size === 0) { messages.push(file.name + ': this file is empty.'); }
            });
            if (messages.length) {
                input.files = store.files;
                showErrors(messages);
                return;
            }
            var next = new DataTransfer();
            all.forEach(function (file) { next.items.add(file); });
            store = next;
            input.files = store.files;
            showErrors([]);
            render();
        };

        input.addEventListener('change', function () {
            addFiles(Array.from(input.files || []));
        });

        select.addEventListener('click', function (event) {
            var remove = event.target.closest('[data-remove]');
            if (!remove || uploading) {
                return;
            }
            var index = Number(remove.getAttribute('data-remove'));
            var next = new DataTransfer();
            Array.from(store.files).forEach(function (file, position) {
                if (position !== index) { next.items.add(file); }
            });
            store = next;
            input.files = store.files;
            showErrors([]);
            render();
            (list.querySelector('[data-remove]') || input).focus();
        });

        form.addEventListener('reset', function () {
            store = new DataTransfer();
            showErrors([]);
            window.setTimeout(function () {
                input.files = store.files;
                render();
            }, 0);
        });

        // The whole surface is the drop target: a small dashed rectangle is
        // too easy to miss on a real drag. dragenter/dragleave fire once per
        // element crossed, so a depth counter decides when the drag has left.
        var depth = 0;
        var isFileDrag = function (event) {
            return Boolean(event.dataTransfer) && Array.from(event.dataTransfer.types || []).indexOf('Files') >= 0;
        };
        select.addEventListener('dragenter', function (event) {
            if (!isFileDrag(event)) { return; }
            depth += 1;
            drop.classList.add('is-over');
        });
        select.addEventListener('dragover', function (event) {
            if (isFileDrag(event)) { event.preventDefault(); }
        });
        select.addEventListener('dragleave', function () {
            depth = Math.max(0, depth - 1);
            if (depth === 0) { drop.classList.remove('is-over'); }
        });
        select.addEventListener('dragend', function () {
            depth = 0;
            drop.classList.remove('is-over');
        });
        select.addEventListener('drop', function (event) {
            depth = 0;
            drop.classList.remove('is-over');
            if (!isFileDrag(event)) { return; }
            event.preventDefault();
            if (event.dataTransfer && event.dataTransfer.files && event.dataTransfer.files.length) {
                addFiles(Array.from(event.dataTransfer.files));
            }
        });

        // One POST stores the whole batch, so every row enters Uploading
        // together and no row is ticked before the response proves it. A
        // redirect is that proof; anything else is re-submitted natively so
        // the server's own validation page is what the operator reads.
        if (typeof fetch === 'function' && typeof FormData === 'function') {
            form.addEventListener('submit', function (event) {
                if (uploading || store.files.length === 0) {
                    return;
                }
                event.preventDefault();
                uploading = true;
                render();
                submit.disabled = true;
                submitLabel.textContent = 'Uploading';
                form.querySelectorAll('button[type="reset"]').forEach(function (button) { button.disabled = true; });
                fetch(form.getAttribute('action') || window.location.href, {
                    method: 'POST',
                    body: new FormData(form)
                }).then(function (response) {
                    if (response.redirected) {
                        window.location.assign(response.url);
                        return;
                    }
                    uploading = false;
                    form.submit();
                }).catch(function () {
                    uploading = false;
                    form.submit();
                });
            });
        }

        render();
    }

    // ------------------------------------------------------------------
    // Reviewing a stored upload.
    // ------------------------------------------------------------------
    var review = document.querySelector('[data-upload-review]');
    if (review) {
        var inspector = review.querySelector('[data-upload-inspector]');
        var roster = [];
        try { roster = JSON.parse(inspector.getAttribute('data-files') || '[]'); } catch (error) { roster = []; }
        var inspected = Math.max(0, Array.prototype.findIndex.call(
            inspector.querySelectorAll('[data-upload-inspect]'),
            function (film) { return film.getAttribute('aria-pressed') === 'true'; }));
        var previewIndex = inspected;

        var media = function (file, target) {
            target.replaceChildren();
            if (file.image) {
                var img = document.createElement('img');
                img.src = file.image;
                img.alt = '';
                target.appendChild(img);
            } else {
                target.appendChild(fileIcon(file.kind));
            }
        };

        var inspect = function (index) {
            var file = roster[index];
            if (!file) { return; }
            inspected = index;
            media(file, inspector.querySelector('[data-inspect-photo]'));
            var name = inspector.querySelector('[data-inspect-name]');
            name.textContent = file.name;
            name.title = file.name;
            inspector.querySelector('[data-inspect-count]').textContent = (index + 1) + ' of ' + roster.length;
            var open = inspector.querySelector('[data-inspect-open]');
            if (open) {
                open.href = file.image || file.original || '#';
                open.hidden = !(file.image || file.original);
            }
            inspector.querySelectorAll('[data-upload-inspect]').forEach(function (film) {
                film.setAttribute('aria-pressed', Number(film.getAttribute('data-upload-inspect')) === index ? 'true' : 'false');
            });
        };

        var preview = function (index, opener) {
            var file = roster[index];
            if (!file) { return; }
            previewIndex = index;
            var dialog = document.getElementById('upload-preview');
            dialog.querySelector('[data-preview-title]').textContent = 'File ' + (index + 1) + ' of ' + roster.length;
            media(file, dialog.querySelector('[data-preview-image]'));
            dialog.querySelector('[data-preview-name]').textContent = file.name;
            dialog.querySelector('[data-preview-meta]').textContent = [file.size, file.kind, file.unreadable ? 'Could not be read' : null]
                .filter(Boolean).join(' · ');
            var original = dialog.querySelector('[data-preview-original]');
            original.hidden = !file.original;
            original.href = file.original || '#';
            if (dialog.hidden) {
                openDialog('upload-preview', opener);
            }
        };

        review.addEventListener('click', function (event) {
            var film = event.target.closest('[data-upload-inspect]');
            if (film) {
                event.preventDefault();
                inspect(Number(film.getAttribute('data-upload-inspect')));
                return;
            }
            var open = event.target.closest('[data-upload-preview]');
            if (open) {
                var index = open.hasAttribute('data-inspect-open') ? inspected : Number(open.getAttribute('data-upload-preview'));
                if (document.getElementById('upload-preview')) {
                    event.preventDefault();
                    preview(index, open);
                }
            }
        });

        document.addEventListener('click', function (event) {
            var control = event.target.closest('button, a');
            if (!control) { return; }
            if (control.hasAttribute('data-preview-previous')) {
                preview((previewIndex + roster.length - 1) % roster.length, control);
            } else if (control.hasAttribute('data-preview-next')) {
                preview((previewIndex + 1) % roster.length, control);
            } else if (control.hasAttribute('data-upload-dialog')) {
                if (openDialog(control.getAttribute('data-upload-dialog'), control)) {
                    event.preventDefault();
                }
            } else if (control.matches('.dialog-backdrop a[data-dialog-close]')) {
                // site.js closes the dialog; the address is the no-script way back.
                event.preventDefault();
            }
        });

        // Review and add to Case: repeat the exact chosen target in the dialog
        // before anything is posted. Without script the same button posts the
        // choice and the server renders this dialog from its own re-read.
        var decision = review.querySelector('[data-upload-decision]');
        var reviewButton = review.querySelector('[data-upload-review-button]');
        var confirm = document.getElementById('upload-confirm');
        if (decision && reviewButton && confirm) {
            reviewButton.type = 'button';
            reviewButton.addEventListener('click', function () {
                var chosen = review.querySelector('input[name="caseId"]:checked');
                if (!chosen) {
                    var first = review.querySelector('input[name="caseId"]');
                    if (first) { first.focus(); }
                    return;
                }
                var facts = chosen.dataset;
                confirm.querySelector('[data-confirm-case]').value = chosen.value;
                confirm.querySelector('[data-confirm-version]').value = facts.version || '';
                confirm.querySelector('[data-confirm-reference]').value = facts.reference || '';
                confirm.querySelector('[data-confirm-ref]').textContent = facts.reference || '';
                confirm.querySelector('[data-confirm-registration]').textContent = facts.registration || '';
                confirm.querySelector('[data-confirm-claimant]').textContent = facts.claimant || '';
                confirm.querySelector('[data-confirm-principal]').textContent = facts.principal || '';
                confirm.querySelector('[data-confirm-stage]').textContent = facts.stage || '';
                confirm.querySelector('[data-confirm-submit]').textContent = 'Confirm and add to ' + (facts.reference || '');
                openDialog('upload-confirm', reviewButton);
            });
        }

        // Discard needs the acknowledgement; the native required attribute
        // already blocks the post, this only says why in the dialog itself.
        var discard = document.querySelector('[data-upload-discard]');
        if (discard) {
            discard.addEventListener('submit', function (event) {
                var check = discard.querySelector('input[type="checkbox"]');
                var note = discard.querySelector('[data-discard-error]');
                if (check && !check.checked) {
                    event.preventDefault();
                    note.hidden = false;
                    check.focus();
                } else if (note) {
                    note.hidden = true;
                }
            });
        }

        var error = review.querySelector('[data-upload-error]');
        if (error) {
            error.focus({ preventScroll: false });
        }
    }
})();
