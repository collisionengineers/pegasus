// Progressive enhancement only: every behaviour here is a convenience on top of
// markup that already works without it.
//
// This file exists as a file rather than as inline <script> blocks because the
// deployed Content-Security-Policy is `default-src 'self'` with no nonce or
// hash allowance, so an inline script is silently discarded in Production. The
// enhancements below therefore ran only in Development until they moved here.
(function () {
    'use strict';

    // Bounded status pages ask to be reloaded while their state is still
    // moving; the delay is data on the element, so nothing here is inline.
    var autoRefresh = document.querySelector('[data-auto-refresh]');
    if (autoRefresh) {
        var delay = Number(autoRefresh.getAttribute('data-auto-refresh'));
        if (Number.isFinite(delay) && delay > 0) {
            window.setTimeout(function () { window.location.reload(); }, delay);
        }
    }

    // Manual refresh feedback. The label change is the signal; the spin is
    // decoration on top of it, so the feedback still reads correctly under
    // reduced motion or with no CSS at all.
    document.querySelectorAll('[data-refresh-form]').forEach(function (form) {
        form.addEventListener('submit', function () {
            var region = form.closest('[data-refresh-region]') || form.parentElement;
            if (region) {
                region.classList.add('is-refreshing');
                region.setAttribute('aria-busy', 'true');
            }
            var label = form.querySelector('[data-refresh-label]');
            if (label) {
                label.textContent = 'Refreshing';
            }
            form.querySelectorAll('button').forEach(function (button) {
                button.disabled = true;
            });
        });
    });

    // Copy a support reference. Without script the value is still selectable
    // text, which is why the button is rendered hidden and revealed here rather
    // than shipped as a control that might do nothing.
    document.querySelectorAll('[data-copy-target]').forEach(function (button) {
        var source = document.getElementById(button.getAttribute('data-copy-target'));
        if (!source || !navigator.clipboard) {
            return;
        }

        button.hidden = false;
        button.addEventListener('click', function () {
            navigator.clipboard.writeText(source.textContent.trim()).then(function () {
                var original = button.textContent;
                button.textContent = 'Copied';
                window.setTimeout(function () { button.textContent = original; }, 2000);
            });
        });
    });

    // Reason dialogs: a focus trap so a modal that asks for a required reason
    // cannot be tabbed out of while it is open.
    document.querySelectorAll('dialog[data-focus-trap]').forEach(function (dialog) {
        dialog.addEventListener('keydown', function (event) {
            if (event.key !== 'Tab') {
                return;
            }

            var focusable = dialog.querySelectorAll(
                'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])');
            if (focusable.length === 0) {
                return;
            }

            var first = focusable[0];
            var last = focusable[focusable.length - 1];
            if (event.shiftKey && document.activeElement === first) {
                event.preventDefault();
                last.focus();
            } else if (!event.shiftKey && document.activeElement === last) {
                event.preventDefault();
                first.focus();
            }
        });
    });

    // Buttons that open their own dialog, so an action can carry its fields
    // without the page shipping a permanently open form for every action.
    document.querySelectorAll('[data-dialog-open]').forEach(function (trigger) {
        var dialog = document.getElementById(trigger.getAttribute('data-dialog-open'));
        if (!dialog || typeof dialog.showModal !== 'function') {
            return;
        }

        trigger.addEventListener('click', function (event) {
            event.preventDefault();
            dialog.showModal();
            var initial = dialog.querySelector('[data-dialog-initial-focus]')
                || dialog.querySelector('input, select, textarea, button');
            if (initial) {
                initial.focus();
            }
        });
    });

    document.querySelectorAll('[data-dialog-close]').forEach(function (button) {
        button.addEventListener('click', function (event) {
            event.preventDefault();
            var dialog = button.closest('dialog');
            if (dialog) {
                dialog.close();
            }
        });
    });

    // Global drop safety net. Without this, a file dropped anywhere off a
    // dropzone's own listeners below — the heading, a panel border, released
    // a beat early while still moving — is unhandled, and the browser's
    // default action navigates the whole tab to the dropped file, losing the
    // page. A dropzone's own listener runs first (event bubbling) and calls
    // preventDefault() itself, so this only ever catches a drop nothing more
    // specific already handled.
    document.addEventListener('dragover', function (event) {
        if (!event.defaultPrevented) {
            event.preventDefault();
        }
    });
    document.addEventListener('drop', function (event) {
        if (!event.defaultPrevented) {
            event.preventDefault();
        }
    });

    // Upload dropzones. The native file input is the control and keeps working
    // on its own; with script the effective drop target is the whole panel
    // the dashed area sits in, not the dashed area itself — a small rectangle
    // is too easy to miss on a real drag — and a real button opens the same
    // input. Nothing here is required: without script the input is simply
    // visible. A genuine OS drag's dataTransfer.files is empty until the drop
    // itself; only .types is readable during dragenter/dragover, so both the
    // affordance and the drop check key off "Files" in .types rather than
    // .files.
    document.querySelectorAll('[data-dropzone]').forEach(function (zone) {
        var input = zone.querySelector('input[type="file"]');
        var browse = zone.querySelector('[data-dropzone-browse]');
        var readout = zone.querySelector('[data-dropzone-file]');
        if (!input || !browse || !readout) {
            return;
        }

        var formatSize = function (bytes) {
            return bytes >= 1048576
                ? (bytes / 1048576).toFixed(1) + ' MB'
                : Math.max(1, Math.round(bytes / 1024)) + ' KB';
        };

        // One row per file — not the crammed single line this replaced. Each
        // row carries its own state placeholder, populated only once a
        // submission is under way (see the upload-progress block below);
        // until then it stays empty, so a page whose form has no progress
        // enhancement (Uploads/Request) renders identically to before.
        var describe = function () {
            var files = input.files ? Array.from(input.files) : [];
            zone.classList.toggle('has-file', files.length > 0);
            if (files.length === 0) {
                readout.hidden = true;
                readout.replaceChildren();
                browse.textContent = 'Choose files';
                return;
            }

            var rows = files.map(function (file) {
                var row = document.createElement('span');
                row.className = 'dropzone__file-row';
                var name = document.createElement('span');
                name.className = 'dropzone__file-row__name';
                name.textContent = file.name;
                var size = document.createElement('span');
                size.className = 'dropzone__file-row__size';
                size.textContent = formatSize(file.size);
                var status = document.createElement('span');
                status.className = 'dropzone__file-row__status';
                status.setAttribute('data-file-row-status', '');
                row.append(name, size, status);
                return row;
            });
            readout.replaceChildren.apply(readout, rows);
            readout.hidden = false;
            browse.textContent = 'Choose different files';
        };

        zone.classList.add('is-enhanced');
        input.classList.add('sr-only');
        browse.hidden = false;
        browse.addEventListener('click', function () { input.click(); });
        input.addEventListener('change', describe);

        // dragenter/dragleave fire once per element the pointer crosses
        // inside the target, so a depth counter (not a toggle) decides when
        // the drag has genuinely left it, rather than flickering as it moves
        // across the panel's own children.
        var target = zone.closest('.panel') || zone;
        var depth = 0;
        var isFileDrag = function (event) {
            return Boolean(event.dataTransfer)
                && Array.from(event.dataTransfer.types || []).includes('Files');
        };

        target.addEventListener('dragenter', function (event) {
            if (!isFileDrag(event)) {
                return;
            }
            depth += 1;
            zone.classList.add('is-dragover');
        });
        target.addEventListener('dragover', function (event) {
            if (isFileDrag(event)) {
                event.preventDefault();
            }
        });
        target.addEventListener('dragleave', function () {
            depth = Math.max(0, depth - 1);
            if (depth === 0) {
                zone.classList.remove('is-dragover');
            }
        });
        target.addEventListener('dragend', function () {
            depth = 0;
            zone.classList.remove('is-dragover');
        });
        target.addEventListener('drop', function (event) {
            depth = 0;
            zone.classList.remove('is-dragover');
            if (!isFileDrag(event)) {
                return;
            }
            event.preventDefault();
            var dropped = event.dataTransfer ? event.dataTransfer.files : null;
            if (!dropped || dropped.length === 0) {
                return;
            }
            input.files = dropped;
            input.dispatchEvent(new Event('change', { bubbles: true }));
        });

        describe();

        // Per-file upload progress: opt-in via data-upload-progress on the
        // form, so this only changes behaviour on the one form that owns the
        // contract below (Upload.cshtml) and never touches the document
        // request form, which keeps its plain native submit.
        var form = zone.closest('form');
        if (form
            && form.hasAttribute('data-upload-progress')
            && typeof fetch === 'function'
            && typeof FormData === 'function') {
            var setRowStatus = function (state, text) {
                readout.classList.toggle('is-refreshing', state === 'uploading');
                readout.querySelectorAll('[data-file-row-status]').forEach(function (status) {
                    var iconId = state === 'stored' ? '#icon-check-circle' : '#icon-refresh-cw';
                    var glyph = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
                    glyph.setAttribute('class', state === 'uploading' ? 'icon icon--spin' : 'icon');
                    glyph.setAttribute('aria-hidden', 'true');
                    var use = document.createElementNS('http://www.w3.org/2000/svg', 'use');
                    use.setAttribute('href', iconId);
                    glyph.appendChild(use);
                    var label = document.createElement('span');
                    label.textContent = text;
                    status.setAttribute('data-state', state);
                    status.replaceChildren(glyph, label);
                });
            };

            form.addEventListener('submit', function (event) {
                var files = input.files ? Array.from(input.files) : [];
                if (files.length === 0) {
                    // Nothing chosen: let native "choose a file" validation
                    // say so, exactly as it always has.
                    return;
                }

                event.preventDefault();
                // Every row enters the same state together here: a single
                // POST stores the whole batch, so there is no per-file signal
                // to show yet, and showing one anyway would be a state this
                // page cannot actually know (see research.md).
                setRowStatus('uploading', 'Uploading');

                fetch(form.getAttribute('action') || window.location.href, {
                    method: 'POST',
                    body: new FormData(form)
                }).then(function (response) {
                    if (response.redirected) {
                        // The response proves the whole batch is durably
                        // stored — a real fact from the actual response, not
                        // a guess — so every row ticks together.
                        setRowStatus('stored', 'Stored');
                        window.location.assign(response.url);
                        return;
                    }

                    // Validation failed. Upload.cshtml.cs stores nothing
                    // until every file passes validation, so nothing was
                    // written on this path and a native re-submit is safe —
                    // it shows the exact, already-correct error page rather
                    // than this script guessing which row to blame.
                    form.submit();
                }).catch(function () {
                    form.submit();
                });
            });
        }
    });

    // Live character counters for reason fields whose limit is policy.
    document.querySelectorAll('[data-counter-for]').forEach(function (counter) {
        var field = document.getElementById(counter.getAttribute('data-counter-for'));
        if (!field) {
            return;
        }

        var limit = field.getAttribute('maxlength');
        var render = function () {
            counter.textContent = field.value.length + '/' + limit + ' characters';
        };

        field.addEventListener('input', render);
        render();
    });

}());
