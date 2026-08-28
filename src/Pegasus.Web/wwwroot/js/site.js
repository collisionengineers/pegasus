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
            // A page can still be moving while an operator has a form open
            // that a reload would wipe; any element opting in with
            // data-refresh-hold pauses the reload while it is open.
            var reload = function () {
                if (document.querySelector('[data-refresh-hold][open]')) {
                    window.setTimeout(reload, delay);
                    return;
                }
                window.location.reload();
            };
            window.setTimeout(reload, delay);
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

    // Case search on the upload confirmation surface. The input is a plain
    // required text field that the server resolves as a typed case reference,
    // so the form keeps working without script; with script it becomes a
    // combobox — a debounced fetch of the page's own suggestion handler, a
    // listbox of matching cases, and a selection that fills the hidden case
    // value. The ARIA combobox wiring is added here rather than shipped in
    // markup, because without script there is no popup for it to describe.
    document.querySelectorAll('[data-case-search]').forEach(function (form) {
        var input = form.querySelector('[data-case-search-input]');
        var list = form.querySelector('[data-case-search-list]');
        var hidden = form.querySelector('[data-case-search-value]');
        var url = form.getAttribute('data-case-search-url');
        if (!input || !list || !hidden || !url || typeof fetch !== 'function') {
            return;
        }

        input.setAttribute('role', 'combobox');
        input.setAttribute('aria-expanded', 'false');
        input.setAttribute('aria-controls', list.id);
        input.setAttribute('aria-autocomplete', 'list');
        input.setAttribute('aria-haspopup', 'listbox');
        list.setAttribute('role', 'listbox');

        var options = [];
        var active = -1;
        var timer = null;
        var requestSequence = 0;
        var inFlight = null;

        var close = function () {
            list.hidden = true;
            list.replaceChildren();
            options = [];
            active = -1;
            input.setAttribute('aria-expanded', 'false');
            input.removeAttribute('aria-activedescendant');
        };

        var setActive = function (index) {
            active = index;
            list.querySelectorAll('[role="option"]').forEach(function (option, position) {
                option.classList.toggle('is-active', position === index);
                option.setAttribute('aria-selected', position === index ? 'true' : 'false');
            });
            if (index >= 0) {
                input.setAttribute('aria-activedescendant', list.id + '-option-' + index);
            } else {
                input.removeAttribute('aria-activedescendant');
            }
        };

        var choose = function (index) {
            var chosen = options[index];
            if (!chosen) {
                return;
            }
            hidden.value = chosen.caseId;
            input.value = chosen.reference;
            close();
        };

        var render = function (items) {
            options = items;
            var rows = items.map(function (item, index) {
                var row = document.createElement('li');
                row.id = list.id + '-option-' + index;
                row.setAttribute('role', 'option');
                row.setAttribute('aria-selected', 'false');
                row.textContent = [item.reference, item.registration, item.claimant, item.stage]
                    .filter(function (part) { return Boolean(part); })
                    .join(' · ');
                // mousedown, not click: click lands after the input's blur
                // would have closed the list.
                row.addEventListener('mousedown', function (event) {
                    event.preventDefault();
                    choose(index);
                });
                return row;
            });
            if (rows.length === 0) {
                var empty = document.createElement('li');
                empty.className = 'case-search-list__empty';
                empty.textContent = 'No matching cases found';
                rows = [empty];
            }
            list.replaceChildren.apply(list, rows);
            list.hidden = false;
            input.setAttribute('aria-expanded', 'true');
            setActive(-1);
        };

        input.addEventListener('input', function () {
            // Typing again always invalidates any earlier selection: the
            // submitted case is either the one just chosen or the typed
            // reference the server resolves — never a stale hidden value.
            hidden.value = '';
            var term = input.value.trim();
            if (timer) {
                window.clearTimeout(timer);
            }
            if (term.length < 2) {
                close();
                return;
            }
            timer = window.setTimeout(function () {
                var sequence = ++requestSequence;
                // Abort the superseded request rather than merely ignoring
                // its result: the server honours the cancellation, so the
                // abandoned search stops running instead of completing.
                if (inFlight) {
                    inFlight.abort();
                }
                inFlight = typeof AbortController === 'function' ? new AbortController() : null;
                fetch(url + (url.indexOf('?') >= 0 ? '&' : '?') + 'term=' + encodeURIComponent(term), {
                    headers: { Accept: 'application/json' },
                    signal: inFlight ? inFlight.signal : undefined
                }).then(function (response) {
                    return response.ok ? response.json() : [];
                }).then(function (items) {
                    if (sequence === requestSequence) {
                        render(items);
                    }
                }).catch(function () {
                    if (sequence === requestSequence) {
                        close();
                    }
                });
            }, 250);
        });

        input.addEventListener('keydown', function (event) {
            if (list.hidden) {
                return;
            }
            if (event.key === 'ArrowDown') {
                event.preventDefault();
                setActive(Math.min(active + 1, options.length - 1));
            } else if (event.key === 'ArrowUp') {
                event.preventDefault();
                setActive(Math.max(active - 1, 0));
            } else if (event.key === 'Enter' && active >= 0) {
                event.preventDefault();
                choose(active);
            } else if (event.key === 'Escape') {
                event.preventDefault();
                close();
            }
        });

        input.addEventListener('blur', function () {
            close();
        });
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


// CASE-007: finishing edit mode with unsaved changes asks first. Dirty means
// any input inside a lease-carrying form changed since load; Save submits the
// form that changed, Discard releases the lease as posted.
(function () {
    var toggle = document.querySelector('[data-edit-toggle-off]');
    var dialog = document.getElementById('edit-finish-confirm');
    if (!toggle || !dialog) {
        return;
    }
    var dirtyForm = null;
    document.querySelectorAll('form').forEach(function (form) {
        if (form === toggle || !form.querySelector('input[name="editLeaseToken"]')) {
            return;
        }
        form.addEventListener('input', function () { dirtyForm = form; });
        form.addEventListener('submit', function () { dirtyForm = null; });
    });
    var allowed = false;
    toggle.addEventListener('submit', function (event) {
        if (allowed || !dirtyForm) {
            return;
        }
        event.preventDefault();
        dialog.hidden = false;
    });
    dialog.querySelector('[data-edit-finish-keep]').addEventListener('click', function () {
        dialog.hidden = true;
    });
    dialog.querySelector('[data-edit-finish-discard]').addEventListener('click', function () {
        dialog.hidden = true;
        allowed = true;
        toggle.requestSubmit();
    });
    dialog.querySelector('[data-edit-finish-save]').addEventListener('click', function () {
        dialog.hidden = true;
        if (dirtyForm) {
            dirtyForm.requestSubmit();
        }
    });
})();

// CASE-024: an open editor keeps its own lease alive, so a real editing session
// is never timed out mid-edit. The beat posts the rendered form, whose
// antiforgery token rides in the FormData exactly as the upload enhancement
// above does. With script the manual "Renew editing" button is redundant, so it
// is hidden here; without script it stays and is the only way to keep editing.
(function () {
    var form = document.querySelector('[data-edit-heartbeat]');
    if (!form) {
        return;
    }

    var renew = document.querySelector('[data-edit-renew]');
    if (renew) {
        renew.hidden = true;
    }

    var seconds = parseInt(form.getAttribute('data-heartbeat-seconds'), 10);
    if (!(seconds > 0)) {
        // The interval is a server value; without it, leave the Renew button
        // showing rather than beat on a guessed one.
        if (renew) {
            renew.hidden = false;
        }
        return;
    }

    // A live timer is what "still beating" means; visibilitychange checks it too,
    // because it calls beat() directly rather than through the interval.
    var timer = null;
    var stop = function () {
        window.clearInterval(timer);
        timer = null;
    };

    var beat = function () {
        if (timer === null) {
            return;
        }

        fetch(form.getAttribute('action') || window.location.href, {
            method: 'POST',
            body: new FormData(form)
        }).then(function (response) {
            // 204 is the only answer that means the lease is still ours. A
            // refusal is final - the lease was released, expired, or is now
            // someone else's - and the page the operator lands on next already
            // shows the case's real edit state, so nothing is said here.
            if (response.status !== 204) {
                stop();
            }
        }).catch(function () {
            // A single failed beat is not a lost lease: there are several more
            // before the lease could lapse, so keep beating.
        });
    };

    timer = window.setInterval(beat, seconds * 1000);
    // A hidden tab has its timers throttled, so the phase on return is
    // unknowable; one beat on becoming visible again settles it.
    document.addEventListener('visibilitychange', function () {
        if (!document.hidden) {
            beat();
        }
    });
})();

// INTK-022: a filter form marked data-auto-submit submits itself when any of
// its selects change; the noscript Apply button covers the rest.
(function () {
    document.querySelectorAll('form[data-auto-submit]').forEach(function (form) {
        form.addEventListener('change', function (event) {
            if (event.target instanceof HTMLSelectElement) {
                form.submit();
            }
        });
    });
})();

// UI-10: evidence-only mail preview. A subject remains an ordinary full-detail
// link; this enhancement selects its row on pointer/keyboard intent and reads
// the same authorized exact-message projection without moving focus or state.
(function () {
    document.querySelectorAll('[data-mail-preview-workspace]').forEach(function (workspace) {
        var panel = workspace.querySelector('[data-mail-preview]');
        var status = workspace.querySelector('[data-mail-preview-status]');
        var facts = workspace.querySelector('[data-mail-preview-facts]');
        var rows = Array.from(workspace.querySelectorAll('[data-mail-preview-row]'));
        if (!panel || !status || !facts || rows.length === 0) {
            return;
        }

        var activeRow = null;
        var request = null;
        var cache = new Map();

        var field = function (name) {
            return facts.querySelector('[data-mail-preview-' + name + ']');
        };

        var resetSelection = function () {
            if (request) {
                request.abort();
                request = null;
            }
            rows.forEach(function (row) {
                row.classList.remove('is-preview-selected');
                var trigger = row.querySelector('[data-mail-preview-trigger]');
                if (trigger) {
                    trigger.setAttribute('aria-expanded', 'false');
                }
            });
            activeRow = null;
            panel.hidden = true;
            panel.removeAttribute('aria-busy');
        };

        var render = function (data) {
            field('sender').textContent = data.sender;
            field('subject').textContent = data.subject;
            field('received').textContent = data.received;
            field('received').setAttribute('datetime', data.receivedAtUtc);
            field('excerpt').textContent = data.excerpt;
            field('classification').textContent = data.classification;
            field('association').textContent = data.association;

            var attachments = field('attachments');
            attachments.replaceChildren();
            (data.attachments.length === 0 ? ['No attachments'] : data.attachments)
                .forEach(function (name) {
                    var item = document.createElement('li');
                    item.textContent = name;
                    attachments.appendChild(item);
                });

            status.hidden = true;
            facts.hidden = false;
            panel.removeAttribute('aria-busy');
        };

        var select = function (row) {
            var trigger = row.querySelector('[data-mail-preview-trigger]');
            var url = trigger && trigger.getAttribute('data-mail-preview-url');
            if (!trigger || !url || activeRow === row) {
                return;
            }

            if (request) {
                request.abort();
            }
            rows.forEach(function (candidate) {
                candidate.classList.toggle('is-preview-selected', candidate === row);
                var candidateTrigger = candidate.querySelector('[data-mail-preview-trigger]');
                if (candidateTrigger) {
                    candidateTrigger.setAttribute(
                        'aria-expanded',
                        candidate === row ? 'true' : 'false');
                }
            });
            activeRow = row;
            panel.hidden = false;
            status.hidden = false;
            status.textContent = 'Loading quick preview…';
            facts.hidden = true;
            panel.setAttribute('aria-busy', 'true');

            if (cache.has(url)) {
                render(cache.get(url));
                return;
            }

            request = new AbortController();
            var currentRequest = request;
            fetch(url, {
                headers: { 'Accept': 'application/json' },
                signal: currentRequest.signal
            }).then(function (response) {
                if (!response.ok) {
                    throw new Error('Preview unavailable');
                }
                return response.json();
            }).then(function (data) {
                cache.set(url, data);
                if (activeRow === row) {
                    render(data);
                }
            }).catch(function (error) {
                if (error.name === 'AbortError' || activeRow !== row) {
                    return;
                }
                facts.hidden = true;
                status.hidden = false;
                status.textContent = 'Quick preview unavailable. Open the message for full detail.';
                panel.removeAttribute('aria-busy');
            }).finally(function () {
                if (request === currentRequest) {
                    request = null;
                }
            });
        };

        rows.forEach(function (row) {
            var trigger = row.querySelector('[data-mail-preview-trigger]');
            row.addEventListener('pointerenter', function () { select(row); });
            row.addEventListener('pointerleave', function () {
                if (activeRow === row && !row.contains(document.activeElement)) {
                    resetSelection();
                }
            });
            if (!trigger) {
                return;
            }
            trigger.addEventListener('focus', function () { select(row); });
            trigger.addEventListener('blur', function () {
                setTimeout(function () {
                    if (activeRow === row && !row.contains(document.activeElement)) {
                        resetSelection();
                    }
                }, 0);
            });
        });
    });

    // Reason dialogs built as div backdrops ([data-reason-dialog]): open from
    // any [data-dialog-open="<id>"] control, close on Cancel, Escape, or a
    // backdrop click, contain focus while open, and return focus to the
    // invoking control. This lives here rather than beside the markup because
    // the deployed Content-Security-Policy discards inline scripts.
    document.querySelectorAll('[data-reason-dialog]').forEach(function (dialog) {
        if (dialog.dataset.dialogBound === 'true') {
            return;
        }
        dialog.dataset.dialogBound = 'true';

        var invoker = null;

        function focusable() {
            return Array.prototype.filter.call(
                dialog.querySelectorAll('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'),
                function (element) { return !element.disabled && !element.hidden; });
        }

        function open(source) {
            invoker = source;
            dialog.hidden = false;
            document.addEventListener('keydown', onKeydown, true);
            var items = focusable();
            if (items.length > 0) {
                items[0].focus();
            }
        }

        function close() {
            dialog.hidden = true;
            document.removeEventListener('keydown', onKeydown, true);
            if (invoker) {
                invoker.focus();
            }
        }

        function onKeydown(event) {
            if (event.key === 'Escape') {
                // Safe: closing abandons an unsent reason and changes nothing.
                event.preventDefault();
                close();
                return;
            }
            if (event.key !== 'Tab') {
                return;
            }
            var items = focusable();
            if (items.length === 0) {
                return;
            }
            var first = items[0];
            var last = items[items.length - 1];
            if (event.shiftKey && document.activeElement === first) {
                event.preventDefault();
                last.focus();
            } else if (!event.shiftKey && document.activeElement === last) {
                event.preventDefault();
                first.focus();
            }
        }

        dialog.querySelectorAll('[data-dialog-dismiss]').forEach(function (control) {
            control.addEventListener('click', close);
        });

        dialog.addEventListener('click', function (event) {
            if (event.target === dialog) {
                close();
            }
        });

        document.querySelectorAll('[data-dialog-open="' + dialog.id + '"]').forEach(function (control) {
            control.addEventListener('click', function () {
                open(control);
            });
        });
    });

    // Evidence viewer ([data-evidence-viewer], DOCS-011): preview an evidence
    // image or PDF over the page instead of navigating away from the case.
    // Modelled on the reason-dialog block above and sharing its contract --
    // initial focus, focus containment, Escape, focus return -- with paging
    // added. Every trigger is a real link, so with no script a click still
    // opens the file exactly as it did before.
    (function () {
        var viewer = document.querySelector('[data-evidence-viewer]');
        if (!viewer) {
            return;
        }

        var stage = viewer.querySelector('[data-evidence-stage]');
        var image = viewer.querySelector('[data-evidence-image]');
        var frame = viewer.querySelector('[data-evidence-document]');
        var caption = viewer.querySelector('[data-evidence-name]');
        var position = viewer.querySelector('[data-evidence-position]');
        var download = viewer.querySelector('[data-evidence-download]');
        var previous = viewer.querySelector('[data-evidence-previous]');
        var following = viewer.querySelector('[data-evidence-next]');

        var items = [];
        var index = 0;
        var invoker = null;

        // Only what a browser renders without executing it. Anything else is
        // left to the link, which saves it -- the server refuses to disposition
        // it inline either way, so the two agree.
        function previewKind(value) {
            var type = String(value || '').split(';')[0].trim().toLowerCase();
            // SVG is excluded to stay in step with the server's inline rule:
            // it is an image that executes script when navigated to, and these
            // triggers are real links. Anything not listed here is left to the
            // link, which saves it.
            if (type.indexOf('image/') === 0 && type !== 'image/svg+xml') {
                return 'image';
            }
            return type === 'application/pdf' ? 'document' : '';
        }

        function focusable() {
            return Array.prototype.filter.call(
                viewer.querySelectorAll('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'),
                function (element) { return !element.disabled && !element.hidden; });
        }

        function settle() {
            stage.removeAttribute('aria-busy');
            stage.classList.remove('is-loading');
        }

        function show(at) {
            var item = items[at];
            if (!item) {
                return;
            }
            index = at;
            var kind = previewKind(item.getAttribute('data-media-type'));
            var href = item.getAttribute('href');
            var fileName = item.getAttribute('data-file-name') || '';

            // The design contract calls for an explicit loading state. It is
            // shown, not said: aria-busy plus the same class-driven treatment
            // the manual refresh feedback above uses.
            stage.setAttribute('aria-busy', 'true');
            stage.classList.add('is-loading');

            image.hidden = kind !== 'image';
            frame.hidden = kind !== 'document';
            if (kind === 'image') {
                frame.removeAttribute('src');
                image.alt = fileName;
                image.src = href;
            } else {
                image.removeAttribute('src');
                frame.title = fileName;
                frame.src = href;
            }

            caption.textContent = fileName;
            position.textContent = (index + 1) + ' / ' + items.length;
            download.href = item.getAttribute('data-download-href') || href;
            download.setAttribute('download', fileName);
            previous.disabled = index === 0;
            following.disabled = index === items.length - 1;
        }

        function open(trigger) {
            var set = trigger.closest('[data-evidence-set]');
            // Only previewable siblings join the paging set. A document table
            // carries every version, previewable or not; without this filter
            // Next could land on one and set a hidden iframe's src, which
            // downloads it unasked and leaves the loading state stuck on.
            items = (set
                ? Array.prototype.slice.call(set.querySelectorAll('[data-evidence-item]'))
                : [trigger]).filter(function (item) {
                    return previewKind(item.getAttribute('data-media-type')) !== '';
                });
            var start = items.indexOf(trigger);
            invoker = trigger;
            viewer.hidden = false;
            document.addEventListener('keydown', onKeydown, true);
            show(start < 0 ? 0 : start);
            var controls = focusable();
            if (controls.length > 0) {
                controls[0].focus();
            }
        }

        function close() {
            viewer.hidden = true;
            document.removeEventListener('keydown', onKeydown, true);
            // Drop the source so a large preview stops loading once it is off
            // screen; the next open sets it again.
            image.removeAttribute('src');
            frame.removeAttribute('src');
            settle();
            if (invoker) {
                invoker.focus();
            }
        }

        function step(offset) {
            var target = index + offset;
            if (target >= 0 && target < items.length) {
                show(target);
            }
        }

        function onKeydown(event) {
            if (event.key === 'Escape') {
                // Safe: closing a preview changes nothing.
                event.preventDefault();
                close();
                return;
            }
            if (event.key === 'ArrowLeft') {
                event.preventDefault();
                step(-1);
                return;
            }
            if (event.key === 'ArrowRight') {
                event.preventDefault();
                step(1);
                return;
            }
            if (event.key !== 'Tab') {
                return;
            }
            var controls = focusable();
            if (controls.length === 0) {
                return;
            }
            var first = controls[0];
            var last = controls[controls.length - 1];
            if (event.shiftKey && document.activeElement === first) {
                event.preventDefault();
                last.focus();
            } else if (!event.shiftKey && document.activeElement === last) {
                event.preventDefault();
                first.focus();
            }
        }

        image.addEventListener('load', settle);
        image.addEventListener('error', settle);
        frame.addEventListener('load', settle);
        frame.addEventListener('error', settle);
        previous.addEventListener('click', function () { step(-1); });
        following.addEventListener('click', function () { step(1); });
        viewer.querySelectorAll('[data-evidence-close]').forEach(function (control) {
            control.addEventListener('click', close);
        });
        viewer.addEventListener('click', function (event) {
            if (event.target === viewer) {
                close();
            }
        });

        document.querySelectorAll('[data-evidence-item]').forEach(function (trigger) {
            trigger.addEventListener('click', function (event) {
                if (!previewKind(trigger.getAttribute('data-media-type'))) {
                    return;
                }
                event.preventDefault();
                open(trigger);
            });
        });
    })();

    // The Other classification name and reasoning fields exist only while an
    // Other option is selected; the select drives their visibility.
    document.querySelectorAll('[data-other-toggle]').forEach(function (select) {
        var scope = select.closest('[data-reason-dialog]') || document;
        function sync() {
            var isOther = select.value === 'other-received' || select.value === 'other-sent';
            scope.querySelectorAll('[data-other-field]').forEach(function (field) {
                field.hidden = !isOther;
            });
        }
        select.addEventListener('change', sync);
        sync();
    });
})();
