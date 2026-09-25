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
            // Exactly one pending timer at a time: a page can be sent to the
            // background and brought back any number of times, and every one
            // of those returns re-arms the same timer rather than adding
            // another reload to the pile.
            var timer = 0;
            var schedule = function () {
                window.clearTimeout(timer);
                timer = window.setTimeout(reload, delay);
            };
            // A page can still be moving while an operator has a form open
            // that a reload would wipe; any element opting in with
            // data-refresh-hold pauses the reload while it is open.
            var reload = function () {
                timer = 0;
                if (document.querySelector('[data-refresh-hold][open]')) {
                    schedule();
                    return;
                }
                window.location.reload();
            };
            // A hidden tab does not poll. Returning to it reloads immediately
            // instead of showing content that can be a full delay out of date.
            var trackVisibility = function () {
                if (document.hidden) {
                    window.clearTimeout(timer);
                    timer = 0;
                } else {
                    reload();
                }
            };
            document.addEventListener('visibilitychange', trackVisibility);
            if (!document.hidden) {
                schedule();
            }
        }
    }

    // Manual refresh feedback. The label change is the signal; the spin is
    // decoration on top of it, so the feedback still reads correctly under
    // reduced motion or with no CSS at all. Bound per region so a Work
    // Centre fragment adopted by its background refresh keeps the feedback.
    function bindRefreshFeedback(root) {
        root.querySelectorAll('[data-refresh-form]').forEach(function (form) {
            if (form.dataset.refreshBound === 'true') {
                return;
            }
            form.dataset.refreshBound = 'true';
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
    }
    bindRefreshFeedback(document);
    (window.pegasusMountBinders = window.pegasusMountBinders || []).push(bindRefreshFeedback);

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

    // Show / Hide a password (sign in, v30 item J). Without script the field
    // is an ordinary password field, which is why the control ships hidden.
    function bindPasswordReveal(root) {
        root.querySelectorAll('[data-password-reveal]').forEach(function (button) {
            var input = document.getElementById(button.getAttribute('aria-controls'));
            if (!input || button.dataset.revealBound === 'true') {
                return;
            }
            button.dataset.revealBound = 'true';
            button.hidden = false;
            button.addEventListener('click', function () {
                var show = input.type === 'password';
                input.type = show ? 'text' : 'password';
                button.textContent = show ? 'Hide' : 'Show';
                button.setAttribute('aria-label', show ? 'Hide password' : 'Show password');
                button.setAttribute('aria-pressed', String(show));
            });
        });
    }
    bindPasswordReveal(document);
    (window.pegasusMountBinders = window.pegasusMountBinders || []).push(bindPasswordReveal);

    // Native <dialog> openers ([data-dialog-open]) and closers
    // ([data-dialog-close]); showModal supplies the focus containment.
    function bindNativeDialogs(root) {
        // Buttons that open their own dialog, so an action can carry its fields
        // without the page shipping a permanently open form for every action.
        root.querySelectorAll('[data-dialog-open]').forEach(function (trigger) {
            var dialog = document.getElementById(trigger.getAttribute('data-dialog-open'));
            if (!dialog || typeof dialog.showModal !== 'function' || trigger.dataset.nativeDialogBound === 'true') {
                return;
            }
            trigger.dataset.nativeDialogBound = 'true';

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

        root.querySelectorAll('dialog [data-dialog-close]').forEach(function (button) {
            if (button.dataset.nativeDialogBound === 'true') {
                return;
            }
            button.dataset.nativeDialogBound = 'true';
            button.addEventListener('click', function (event) {
                event.preventDefault();
                var dialog = button.closest('dialog');
                if (dialog) {
                    dialog.close();
                }
            });
        });
    }
    bindNativeDialogs(document);
    (window.pegasusMountBinders = window.pegasusMountBinders || []).push(bindNativeDialogs);

    // Scoped edits expire unless the operator is actively keeping the edit
    // session open. Each marked form carries only its existing antiforgery
    // value and opaque lease token; the server owns the record and actor
    // resolution. A failed renewal stops rather than silently continuing with
    // an edit the operator can no longer save. A hidden tab keeps beating:
    // the operator's unsaved edit is still open, and a scope that stops being
    // renewed is exactly what the server reads as an abandoned window. The
    // browser throttles a hidden tab's timers, so the beat on becoming
    // visible again settles whatever phase the throttling left.
    function bindEditScopeHeartbeats(root) {
        root.querySelectorAll('form[data-edit-heartbeat][data-edit-heartbeat-status]').forEach(function (form) {
            if (form.dataset.editHeartbeatBound === 'true') {
                return;
            }
            form.dataset.editHeartbeatBound = 'true';

            var status = document.getElementById(form.getAttribute('data-edit-heartbeat-status'));
            var stopped = false;
            var timer = 0;
            var stop = function (message) {
                stopped = true;
                window.clearInterval(timer);
                if (status) {
                    status.textContent = message;
                    status.hidden = false;
                }
            };
            var heartbeat = function () {
                if (stopped || typeof window.fetch !== 'function') {
                    return;
                }

                window.fetch(form.action, {
                    method: 'POST',
                    body: new FormData(form),
                    credentials: 'same-origin'
                }).then(function (response) {
                    if (response.ok) {
                        return;
                    }
                    response.text().then(function (message) {
                        stop(message.replace(/^"|"$/g, '').replace(/\\"/g, '"')
                            || 'Editing this record has ended. Reload it before making further changes.');
                    });
                }).catch(function () {
                    // A transient network failure does not prove the lease has
                    // ended; retry on the next scheduled renewal.
                });
            };

            timer = window.setInterval(heartbeat, 60000);
            form.addEventListener('submit', function () {
                stopped = true;
                window.clearInterval(timer);
            });
            document.addEventListener('visibilitychange', function () {
                if (!document.hidden) {
                    heartbeat();
                }
            });
        });
    }
    bindEditScopeHeartbeats(document);
    (window.pegasusMountBinders = window.pegasusMountBinders || []).push(bindEditScopeHeartbeats);

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


}());


// Inspect-at choices fill the ordinary form-associated address
// input. The input remains the no-script editing path. The cells stay where
// they are: reading and editing show the same fields (23 September 2026).
(function () {
    function bind(root) {
        root.querySelectorAll('[data-inspection-address-choice]').forEach(function (select) {
            if (select.dataset.inspectionAddressBound === 'true') return;
            var input = document.querySelector('[data-inspection-address-input]');
            var mode = document.querySelector('input[name="inspectionMode"]');
            if (!input || !mode) return;

            select.dataset.inspectionAddressBound = 'true';
            function choose() {
                var option = select.options[select.selectedIndex];
                if (!option) return;
                if (option.value === 'ManualEntry') {
                    if (input.value.toLowerCase() === 'image based assessment') input.value = '';
                    mode.value = input.value.trim() ? 'PhysicalAddress' : '';
                    return;
                }
                input.value = option.dataset.address || '';
                mode.value = option.value === 'ImageBasedAssessment' ? 'ImageBasedAssessment' : 'PhysicalAddress';
            }
            select.addEventListener('change', choose);
            input.addEventListener('input', function () {
                mode.value = input.value.trim() ? 'PhysicalAddress' : '';
            });
        });
    }
    bind(document);
    (window.pegasusMountBinders = window.pegasusMountBinders || []).push(bind);
})();

// A filter form marked data-auto-submit submits itself when any of
// its selects change; the noscript Apply button covers the rest. A search
// input in the same form submits itself too, 300ms after the last keystroke,
// skipping a submit when the reload's own value has not actually changed.
(function () {
    document.querySelectorAll('form[data-auto-submit]').forEach(function (form) {
        form.addEventListener('change', function (event) {
            if (event.target instanceof HTMLSelectElement) {
                form.submit();
            }
        });

        var search = form.querySelector('input[type="search"]');
        if (!search) {
            return;
        }
        var lastSubmittedValue = search.value;
        var debounce = null;
        search.addEventListener('input', function () {
            if (debounce) {
                window.clearTimeout(debounce);
            }
            debounce = window.setTimeout(function () {
                debounce = null;
                if (search.value !== lastSubmittedValue) {
                    lastSubmittedValue = search.value;
                    form.submit();
                }
            }, 300);
        });
    });
})();

// UI-10: evidence-only mail preview. A subject remains an ordinary full-detail
// link; this enhancement selects its row on pointer/keyboard intent and reads
// the same authorized exact-message projection without moving focus or state.
// When that intent moves on, the pane restores the server-selected message
// instead of hiding: the pane is a fixture of the page, not a tooltip.
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

        // The pane renders only beside a list that has a server-selected row
        // (the page model resolves one whenever it renders the pane at all),
        // and that row's trigger is the pane's fallback wherever intent goes.
        var selectedRow = rows.filter(function (row) {
            var trigger = row.querySelector('[data-mail-preview-trigger]');
            return trigger && trigger.getAttribute('aria-current') === 'true';
        })[0] || null;
        if (!selectedRow) {
            return;
        }
        var actions = facts.querySelector('[data-mail-preview-actions]');
        activeRow = selectedRow;

        var field = function (name) {
            return facts.querySelector('[data-mail-preview-' + name + ']');
        };

        // The pane already shows the selected message; seeding the cache from
        // its rendered fields means restoring that message never waits on the
        // network and never leaves its actions hidden behind a failed fetch.
        cache.set(
            selectedRow
                .querySelector('[data-mail-preview-trigger]')
                .getAttribute('data-mail-preview-url'),
            {
                sender: field('sender').textContent,
                subject: field('subject').textContent,
                received: field('received').textContent,
                receivedAtUtc: field('received').getAttribute('datetime'),
                excerpt: field('excerpt').textContent,
                classification: field('classification').textContent,
                association: field('association').textContent,
                attachments: Array.prototype.map.call(
                    field('attachments').querySelectorAll('li'),
                    function (item) { return item.textContent; })
            });

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
                var candidateTrigger = candidate.querySelector('[data-mail-preview-trigger]');
                if (candidateTrigger) {
                    candidateTrigger.setAttribute(
                        'aria-expanded',
                        candidate === row ? 'true' : 'false');
                }
            });
            activeRow = row;
            if (actions) {
                // The pane's actions belong to the selected message; while a
                // transient preview shows a different row, they are not its.
                actions.hidden = row !== selectedRow;
            }
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

        // Leaving the rows ends the transient preview, not the pane: it falls
        // back to the server-selected message, whose actions must stay
        // reachable. select() no-ops when that row is already active, so
        // leaving the selected row itself leaves the pane untouched.
        var restoreSelection = function () {
            select(selectedRow);
        };

        rows.forEach(function (row) {
            var trigger = row.querySelector('[data-mail-preview-trigger]');
            row.addEventListener('pointerenter', function () { select(row); });
            row.addEventListener('pointerleave', function (event) {
                if (activeRow !== row || row.contains(document.activeElement)) {
                    return;
                }
                // Moving between rows is not leaving them: the next row's
                // pointerenter supersedes this event, and restoring between
                // every pair of rows would repaint the pane down the list.
                var entered = event.relatedTarget;
                if (!entered || !entered.closest('[data-mail-preview-row]')) {
                    restoreSelection();
                }
            });
            if (!trigger) {
                return;
            }
            trigger.addEventListener('focus', function () { select(row); });
            trigger.addEventListener('blur', function () {
                setTimeout(function () {
                    if (activeRow === row && !row.contains(document.activeElement)) {
                        restoreSelection();
                    }
                }, 0);
            });
        });
    });

    // Dialogs built as div backdrops ([data-dialog="<id>"]): open from any
    // [data-dialog-open="<id>"] control, close on [data-dialog-close], Escape,
    // or a backdrop click, contain focus while open, set `inert` on the
    // application shell so nothing behind the dialog is reachable, and return
    // focus to the invoking control. This lives here rather than beside the
    // markup because the deployed Content-Security-Policy discards inline
    // scripts.
    // While a dialog is open everything outside it is inert. A dialog may be
    // rendered anywhere in the page (a Case page's reason dialogs live inside
    // the shell), so inert is set on the siblings of each of its ancestors up
    // to body - never on an ancestor - and exactly those elements are
    // released on close. Other [data-dialog] elements
    // and native <dialog> elements are never inerted by this: a dialog that
    // auto-opens on load (a settings dialog) is commonly a sibling of further
    // action dialogs it triggers (the Accounts Delete confirmation is a
    // native dialog), and marking a closed sibling inert would leave it
    // unusable the moment it opens on top; `hidden` already keeps a closed
    // backdrop dialog out of the tab order and off screen, a closed native
    // dialog is not rendered, and showModal() makes the rest of the page
    // inert itself. A
    // module-level stack of currently-open dialogs tracks which one is
    // topmost, so a stacked open (a confirm dialog nested inside settings,
    // or an action dialog opened from a sibling settings dialog) leaves the
    // dialog beneath it open but unresponsive to Escape/Tab until the one on
    // top closes.
    function inertOutside(dialog) {
        var made = [];
        for (var node = dialog; node && node !== document.body; node = node.parentElement) {
            Array.prototype.forEach.call(node.parentElement.children, function (sibling) {
                if (sibling !== node && !sibling.hasAttribute('inert') && sibling.tagName !== 'SCRIPT'
                    && !sibling.matches('[data-dialog], dialog')) {
                    sibling.setAttribute('inert', '');
                    made.push(sibling);
                }
            });
        }
        return function release() {
            made.forEach(function (element) { element.removeAttribute('inert'); });
        };
    }

    var dialogOpeners = {};
    var openDialogStack = [];

    function bindBackdropDialogs(root) {
        root.querySelectorAll('[data-dialog]').forEach(function (dialog) {
            if (dialog.dataset.dialogBound === 'true') {
                return;
            }
            dialog.dataset.dialogBound = 'true';

            var dialogId = dialog.getAttribute('data-dialog') || dialog.id;
            var release = null;
            var invoker = null;
            var wasInertOnOpen = false;

            // A hidden input (the antiforgery token) matches the selector but
            // cannot take focus; focusing it leaves focus on the invoking control,
            // which is about to become inert and lose it to body. Links are
            // a[href]: a bare [href] also matched an icon's SVG <use>, which
            // then stood as the last control, so Tab from a last icon button
            // left the dialog instead of wrapping.
            function focusable() {
                return Array.prototype.filter.call(
                    dialog.querySelectorAll('button, a[href], input, select, textarea, [tabindex]:not([tabindex="-1"])'),
                    function (element) {
                        return !element.disabled && !element.hidden && element.type !== 'hidden' && element.getClientRects().length > 0;
                    });
            }

            function open(source) {
                invoker = source;
                // A dialog opened as a sibling of an already-open dialog (the
                // Accounts settings dialog auto-opens, and Disable/Delete are
                // its siblings) may still carry `inert` from before this
                // fix, or from markup outside this module's control; clear
                // it so the dialog being opened is always reachable, and
                // remember whether to restore it on close.
                wasInertOnOpen = dialog.hasAttribute('inert');
                if (wasInertOnOpen) {
                    dialog.removeAttribute('inert');
                }
                dialog.hidden = false;
                release = inertOutside(dialog);
                openDialogStack.push(dialog);
                document.addEventListener('keydown', onKeydown, true);
                var items = focusable();
                var initial = dialog.querySelector('[data-dialog-initial-focus]')
                    || items.find(function (element) { return element.matches('input, select, textarea'); })
                    || items[0];
                if (initial) {
                    initial.focus();
                }
                dialog.dispatchEvent(new CustomEvent('pegasus:dialog-open', { bubbles: true }));
            }

            function close() {
                dialog.hidden = true;
                if (release) {
                    release();
                    release = null;
                }
                if (wasInertOnOpen) {
                    dialog.setAttribute('inert', '');
                    wasInertOnOpen = false;
                }
                var stackIndex = openDialogStack.indexOf(dialog);
                if (stackIndex !== -1) {
                    openDialogStack.splice(stackIndex, 1);
                }
                document.removeEventListener('keydown', onKeydown, true);
                if (invoker) {
                    invoker.focus();
                }
            }

            function cancel() {
                var cancelFormId = dialog.getAttribute('data-dialog-cancel-form');
                var cancelForm = cancelFormId && document.getElementById(cancelFormId);
                if (!cancelForm) {
                    return false;
                }
                if (typeof cancelForm.requestSubmit === 'function') {
                    cancelForm.requestSubmit();
                } else {
                    cancelForm.submit();
                }
                return true;
            }

            dialog.pegasusClose = close;
            dialog.pegasusOpen = open;

            function onKeydown(event) {
                // Every open dialog keeps its own document-level listener
                // (registered in open(), above), so with two dialogs open at
                // once both would otherwise react to the same keystroke.
                // Only the topmost dialog in the stack may handle Escape or
                // trap Tab; a dialog further down waits until it is on top
                // again.
                if (openDialogStack[openDialogStack.length - 1] !== dialog) {
                    return;
                }
                // A native modal dialog opened from this one (the Accounts
                // Delete confirmation) is not on the stack; its keys are its
                // own, so Escape closes it rather than this dialog behind it.
                if (event.target instanceof Element && event.target.closest('dialog[open]')) {
                    return;
                }
                if (event.key === 'Escape') {
                    event.preventDefault();
                    if (cancel()) {
                        return;
                    }
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

            dialog.querySelectorAll('[data-dialog-close]').forEach(function (control) {
                // A nested dialog's own controls close only that dialog; the
                // parent must keep its unsaved values when a confirmation is cancelled.
                if (control.closest('[data-dialog]') !== dialog) {
                    return;
                }
                control.addEventListener('click', close);
            });

            dialog.addEventListener('click', function (event) {
                if (event.target === dialog) {
                    if (cancel()) {
                        return;
                    }
                    close();
                }
            });

            dialogOpeners[dialogId] = open;
        });
        bindDialogOpeners(root);
        root.querySelectorAll('[data-dialog-open-on-load="true"]').forEach(function (dialog) {
            if (dialog.pegasusOpen && dialog.dataset.dialogAutoOpened !== 'true') {
                dialog.dataset.dialogAutoOpened = 'true';
                dialog.pegasusOpen();
            }
        });
    }

    // The openers are bound by root rather than once over the document, so a
    // lazily mounted Case section's controls open their dialog too.
    function bindDialogOpeners(root) {
        root.querySelectorAll('[data-dialog-open]').forEach(function (control) {
            if (control.dataset.dialogOpenBound === 'true') {
                return;
            }
            var open = dialogOpeners[control.getAttribute('data-dialog-open')];
            if (!open) {
                return;
            }
            control.dataset.dialogOpenBound = 'true';
            control.addEventListener('click', function (event) {
                if (control.matches('a[href]')) {
                    event.preventDefault();
                }
                open(control);
            });
        });
    }
    bindBackdropDialogs(document);
    (window.pegasusMountBinders = window.pegasusMountBinders || []).push(bindBackdropDialogs);

    // Evidence viewer ([data-evidence-viewer]): preview an evidence
    // image, PDF or admitted video over the page instead of navigating away
    // from the case.
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
        var video = viewer.querySelector('[data-evidence-video]');
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
            if (type === 'application/pdf') {
                return 'document';
            }
            return type === 'video/mp4' || type === 'video/quicktime' ? 'video' : '';
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
            video.hidden = kind !== 'video';
            if (kind === 'image') {
                frame.removeAttribute('src');
                video.removeAttribute('src');
                image.alt = fileName;
                image.src = href;
            } else if (kind === 'document') {
                image.removeAttribute('src');
                video.removeAttribute('src');
                frame.title = fileName;
                frame.src = href;
            } else {
                image.removeAttribute('src');
                frame.removeAttribute('src');
                video.src = href;
                video.load();
            }

            caption.textContent = fileName;
            position.textContent = (index + 1) + ' / ' + items.length;
            download.href = item.getAttribute('data-download-href') || href;
            download.setAttribute('download', fileName);
            previous.disabled = index === 0;
            following.disabled = index === items.length - 1;
            showPreCase(item, kind);
        }

        // Pre-Case crop and tag (v26): an image on an image record, a Triage or
        // an Unidentified item carries data-precase-asset. The recorded rotation
        // is shown on the stage; Crop draws a frame over the image (fractions of
        // the rotated image, as a Case crop is); Apply and Clear post to the one
        // owner; Cancel leaves crop mode; the Tag select and chip × post a tag.
        var preCaseTools = viewer.querySelector('[data-precase-tools]');
        var preCaseCropForm = viewer.querySelector('[data-precase-crop-form]');
        var preCaseTagForm = viewer.querySelector('[data-precase-tag-form]');
        var preCaseCropping = null;

        function preCaseRotation() {
            if (stage.classList.contains('rot-90')) { return 90; }
            if (stage.classList.contains('rot-180')) { return 180; }
            return stage.classList.contains('rot-270') ? 270 : 0;
        }

        function endPreCaseCrop() {
            if (preCaseCropping) {
                preCaseCropping.layer.remove();
                preCaseCropping = null;
            }
            if (preCaseTools) {
                preCaseTools.querySelector('[data-precase-view]').hidden = false;
                preCaseTools.querySelector('[data-precase-cropping]').hidden = true;
            }
            drawPreCaseView(preCaseItem());
        }

        // The recorded crop, drawn over the image as it is shown (after the
        // recorded rotation) and shading what the crop leaves out: the same
        // region the tile renders. It is a picture, not a control; Crop replaces
        // it with the editable frame.
        var preCaseView = null;

        function removePreCaseView() {
            if (preCaseView) {
                preCaseView.remove();
                preCaseView = null;
            }
        }

        function drawPreCaseView(item) {
            removePreCaseView();
            if (!item || image.hidden || preCaseCropping) {
                return;
            }
            var stored = (item.getAttribute('data-precase-crop') || '').split(',').map(Number);
            if (stored.length !== 4 || stored.some(function (value) { return isNaN(value); })) {
                return;
            }
            var host = stage.parentElement;
            var layer = document.createElement('div');
            layer.className = 'precase-crop-layer precase-crop-layer--view';
            layer.setAttribute('data-precase-crop-view', '');
            layer.setAttribute('aria-hidden', 'true');
            var selection = document.createElement('div');
            selection.className = 'precase-crop-selection';
            selection.style.left = (stored[0] * 100) + '%';
            selection.style.top = (stored[1] * 100) + '%';
            selection.style.width = (stored[2] * 100) + '%';
            selection.style.height = (stored[3] * 100) + '%';
            layer.appendChild(selection);
            host.appendChild(layer);
            preCaseView = layer;
            function place() {
                if (preCaseView !== layer) {
                    return;
                }
                var bounds = image.getBoundingClientRect();
                var hostBounds = host.getBoundingClientRect();
                layer.style.left = (bounds.left - hostBounds.left + host.scrollLeft) + 'px';
                layer.style.top = (bounds.top - hostBounds.top + host.scrollTop) + 'px';
                layer.style.width = bounds.width + 'px';
                layer.style.height = bounds.height + 'px';
            }
            if (image.complete && image.naturalWidth) {
                place();
            } else {
                image.addEventListener('load', place, { once: true });
            }
            window.requestAnimationFrame(place);
        }

        function showPreCase(item, kind) {
            if (!preCaseTools) {
                return;
            }
            endPreCaseCrop();
            var asset = kind === 'image' ? item.getAttribute('data-precase-asset') : null;
            preCaseTools.hidden = !asset;
            if (!asset) {
                return;
            }
            stage.classList.remove('rot-90', 'rot-180', 'rot-270');
            var rotation = item.getAttribute('data-precase-rotation');
            if (rotation && rotation !== '0') {
                stage.classList.add('rot-' + rotation);
            }
            var cropState = preCaseTools.querySelector('[data-precase-crop-state]');
            cropState.textContent = item.getAttribute('data-precase-crop') || (rotation && rotation !== '0') ? 'Cropped' : '';
            var tagList = preCaseTools.querySelector('[data-precase-tag-list]');
            var select = preCaseTools.querySelector('[data-precase-tag-select]');
            var applied = (item.getAttribute('data-precase-tags') || '').split(',').filter(Boolean);
            tagList.textContent = '';
            Array.prototype.forEach.call(select.options, function (option) {
                if (!option.value) {
                    return;
                }
                var on = applied.indexOf(option.value) >= 0;
                option.hidden = on;
                if (on) {
                    var chip = document.createElement('span');
                    chip.className = 'tag-chip tag-chip--' + (option.getAttribute('data-colour') || 'grey');
                    chip.textContent = option.textContent;
                    var remove = document.createElement('button');
                    remove.type = 'button';
                    remove.className = 'precase-tag-remove';
                    remove.setAttribute('aria-label', 'Remove tag ' + option.textContent);
                    remove.setAttribute('data-precase-tag-remove', option.value);
                    remove.textContent = '×';
                    chip.appendChild(remove);
                    tagList.appendChild(chip);
                }
            });
            select.value = '';
            drawPreCaseView(item);
        }

        function preCaseItem() {
            var item = items[index];
            return item && item.getAttribute('data-precase-asset') ? item : null;
        }

        function fraction(value) {
            return Math.round(Math.min(1, Math.max(0, value)) * 1e7) / 1e7;
        }

        function beginPreCaseCrop() {
            var item = preCaseItem();
            if (!item || image.hidden) {
                return;
            }
            endPreCaseCrop();
            removePreCaseView();
            // The frame is drawn over the image as it is shown (after the view's
            // rotation), outside the rotated stage, so its fractions are of the
            // rotated image exactly as a Case crop's are.
            var host = stage.parentElement;
            var bounds = image.getBoundingClientRect();
            var hostBounds = host.getBoundingClientRect();
            var layer = document.createElement('div');
            layer.className = 'precase-crop-layer';
            layer.setAttribute('data-precase-crop-layer', '');
            layer.style.left = (bounds.left - hostBounds.left + host.scrollLeft) + 'px';
            layer.style.top = (bounds.top - hostBounds.top + host.scrollTop) + 'px';
            layer.style.width = bounds.width + 'px';
            layer.style.height = bounds.height + 'px';
            var selection = document.createElement('div');
            selection.className = 'precase-crop-selection';
            selection.hidden = true;
            layer.appendChild(selection);
            host.appendChild(layer);
            preCaseCropping = { layer: layer, selection: selection, frame: null };
            var stored = (item.getAttribute('data-precase-crop') || '').split(',').map(Number);
            if (stored.length === 4 && stored.every(function (value) { return !isNaN(value); })) {
                drawPreCaseFrame({ left: stored[0], top: stored[1], width: stored[2], height: stored[3] });
            }
            var start = null;
            layer.addEventListener('pointerdown', function (event) {
                var rect = layer.getBoundingClientRect();
                start = { x: (event.clientX - rect.left) / rect.width, y: (event.clientY - rect.top) / rect.height };
                layer.setPointerCapture(event.pointerId);
                event.preventDefault();
            });
            layer.addEventListener('pointermove', function (event) {
                if (!start) {
                    return;
                }
                var rect = layer.getBoundingClientRect();
                var x = fraction((event.clientX - rect.left) / rect.width);
                var y = fraction((event.clientY - rect.top) / rect.height);
                drawPreCaseFrame({
                    left: fraction(Math.min(start.x, x)),
                    top: fraction(Math.min(start.y, y)),
                    width: fraction(Math.abs(x - start.x)),
                    height: fraction(Math.abs(y - start.y))
                });
            });
            layer.addEventListener('pointerup', function () { start = null; });
            preCaseTools.querySelector('[data-precase-view]').hidden = true;
            preCaseTools.querySelector('[data-precase-cropping]').hidden = false;
        }

        function drawPreCaseFrame(frame) {
            if (!preCaseCropping) {
                return;
            }
            preCaseCropping.frame = frame;
            var selection = preCaseCropping.selection;
            selection.hidden = !(frame.width > 0 && frame.height > 0);
            selection.style.left = (frame.left * 100) + '%';
            selection.style.top = (frame.top * 100) + '%';
            selection.style.width = (frame.width * 100) + '%';
            selection.style.height = (frame.height * 100) + '%';
        }

        function postPreCaseCrop(clear) {
            var item = preCaseItem();
            if (!item || !preCaseCropForm) {
                return;
            }
            var frame = preCaseCropping && preCaseCropping.frame;
            if (!clear && !(frame && frame.width > 0 && frame.height > 0)) {
                frame = { left: 0, top: 0, width: 1, height: 1 };
            }
            if (!clear) {
                frame.width = fraction(Math.min(frame.width, 1 - frame.left));
                frame.height = fraction(Math.min(frame.height, 1 - frame.top));
            }
            preCaseCropForm.elements.intakeAssetId.value = item.getAttribute('data-precase-asset');
            preCaseCropForm.elements.expectedVersion.value = item.getAttribute('data-precase-version') || '0';
            preCaseCropForm.elements.rotation.value = clear ? '0' : String(preCaseRotation());
            preCaseCropForm.elements.clear.value = clear ? 'true' : 'false';
            preCaseCropForm.elements.cropLeft.value = clear ? '' : String(frame.left);
            preCaseCropForm.elements.cropTop.value = clear ? '' : String(frame.top);
            preCaseCropForm.elements.cropWidth.value = clear ? '' : String(frame.width);
            preCaseCropForm.elements.cropHeight.value = clear ? '' : String(frame.height);
            preCaseCropForm.submit();
        }

        function postPreCaseTag(tagId, applied) {
            var item = preCaseItem();
            if (!item || !preCaseTagForm || !tagId) {
                return;
            }
            preCaseTagForm.elements.intakeAssetId.value = item.getAttribute('data-precase-asset');
            preCaseTagForm.elements.tagId.value = tagId;
            preCaseTagForm.elements.applied.value = applied ? 'true' : 'false';
            preCaseTagForm.submit();
        }

        if (preCaseTools) {
            // Turning the view moves the image under the recorded frame, so the
            // frame leaves rather than point at the wrong region.
            var rotateView = viewer.querySelector('[data-rotate]');
            if (rotateView) {
                rotateView.addEventListener('click', removePreCaseView);
            }
            preCaseTools.querySelector('[data-precase-crop-start]').addEventListener('click', beginPreCaseCrop);
            preCaseTools.querySelector('[data-precase-crop-apply]').addEventListener('click', function () { postPreCaseCrop(false); });
            preCaseTools.querySelector('[data-precase-crop-clear]').addEventListener('click', function () { postPreCaseCrop(true); });
            preCaseTools.querySelector('[data-precase-crop-cancel]').addEventListener('click', endPreCaseCrop);
            preCaseTools.querySelector('[data-precase-tag-select]').addEventListener('change', function (event) {
                postPreCaseTag(event.target.value, true);
            });
            preCaseTools.addEventListener('click', function (event) {
                var remove = event.target.closest('[data-precase-tag-remove]');
                if (remove) {
                    postPreCaseTag(remove.getAttribute('data-precase-tag-remove'), false);
                }
            });
        }

        var release = null;

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
            release = inertOutside(viewer);
            document.addEventListener('keydown', onKeydown, true);
            show(start < 0 ? 0 : start);
            var controls = focusable();
            if (controls.length > 0) {
                controls[0].focus();
            }
        }

        function close() {
            viewer.hidden = true;
            if (release) {
                release();
                release = null;
            }
            document.removeEventListener('keydown', onKeydown, true);
            // Drop the source so a large preview stops loading once it is off
            // screen; the next open sets it again.
            image.removeAttribute('src');
            frame.removeAttribute('src');
            video.removeAttribute('src');
            video.load();
            stage.classList.remove('rot-90', 'rot-180', 'rot-270');
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
        video.addEventListener('loadedmetadata', settle);
        video.addEventListener('error', settle);
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

        // Root-scoped for the same reason as the dialog openers: a Files body
        // mounted as the reader reaches it must open its own viewer.
        function bindEvidenceItems(root) {
            root.querySelectorAll('[data-evidence-item]').forEach(function (trigger) {
                if (trigger.dataset.evidenceItemBound === 'true') {
                    return;
                }
                trigger.dataset.evidenceItemBound = 'true';
                trigger.addEventListener('click', function (event) {
                    if (!previewKind(trigger.getAttribute('data-media-type'))) {
                        return;
                    }
                    event.preventDefault();
                    open(trigger);
                });
            });
        }
        bindEvidenceItems(document);
        (window.pegasusMountBinders = window.pegasusMountBinders || []).push(bindEvidenceItems);

        // A gallery tile that fails to load. The route answers a
        // throttled or in-flight read with 503 and Retry-After: 5, so two retries
        // paced to that advice are usually the whole fix; a successful response is the only
        // cacheable one. Re-setting the same src is not reliably a re-request —
        // some browsers skip the network entirely when the URL is unchanged —
        // so the attribute is removed first to force a fresh fetch, and no
        // cache-busting query is needed. When the retry fails too the tile
        // becomes the same placeholder a not-yet-stored file draws, naming the
        // file rather than showing the browser's broken-image mark.
        function bindGalleryImages(root) {
            root.querySelectorAll('img[data-gallery-image]').forEach(function (image) {
                if (image.dataset.galleryImageBound === 'true') {
                    return;
                }
                image.dataset.galleryImageBound = 'true';
                function handleFailure() {
                    var source = image.getAttribute('src');
                    if (!source) {
                        return;
                    }
                    var retries = Number(image.dataset.galleryRetries || '0');
                    if (retries < 2) {
                        image.dataset.galleryRetries = String(retries + 1);
                        window.setTimeout(function () {
                            image.removeAttribute('src');
                            image.setAttribute('src', source);
                        }, 5000 * (retries + 1));
                        return;
                    }
                    var tile = image.closest('.gallery-item');
                    if (!tile) {
                        return;
                    }
                    image.remove();
                    tile.setAttribute('data-gallery-placeholder', '');
                    var caption = tile.querySelector('.gallery-caption');
                    if (caption && !caption.querySelector('small')) {
                        var note = document.createElement('small');
                        note.textContent = 'Storing…';
                        caption.appendChild(note);
                    }
                }
                image.addEventListener('error', handleFailure);
                // A tile whose image already failed before this end-of-body
                // script ran (a 503 during the initial page load) fired its
                // error event before this listener existed to catch it, and
                // the browser never re-fires a load failure on its own. Catch
                // that already-failed state at bind time and start the same
                // retry pacing immediately instead of leaving the tile stuck.
                if (image.complete && image.naturalWidth === 0 && image.getAttribute('src')) {
                    handleFailure();
                }
            });
        }
        bindGalleryImages(document);
        window.pegasusMountBinders.push(bindGalleryImages);
    })();

    // The Other classification name and reasoning fields exist only while an
    // Other option is selected; the select drives their visibility.
    document.querySelectorAll('[data-other-toggle]').forEach(function (select) {
        var scope = select.closest('[data-dialog]') || document;
        function sync() {
            var isOther = select.value === 'other-received' || select.value === 'other-sent';
            scope.querySelectorAll('[data-other-field]').forEach(function (field) {
                field.hidden = !isOther;
            });
        }
        select.addEventListener('change', sync);
        sync();
    });

    // Create case: a Triage Case asks only for the Principal and the
    // registration, so choosing it hides every other field and stops it being
    // required; choosing another type restores both. Without script every
    // field shows and the server takes only what a Triage Case needs.
    document.querySelectorAll('[data-manual-case-type]').forEach(function (select) {
        var form = select.closest('form');
        if (!form) {
            return;
        }
        var triageValue = select.getAttribute('data-manual-case-type');
        function sync() {
            var triage = select.value === triageValue;
            form.querySelectorAll('[data-triage-hidden]').forEach(function (field) {
                field.hidden = triage;
                field.querySelectorAll('input, select, textarea').forEach(function (control) {
                    if (triage && control.required) {
                        control.setAttribute('data-triage-required', '');
                        control.required = false;
                    } else if (!triage && control.hasAttribute('data-triage-required')) {
                        control.removeAttribute('data-triage-required');
                        control.required = true;
                    }
                });
            });
        }
        select.addEventListener('change', sync);
        sync();
    });
})();

// ===========================================================================
// Integrated Operations Workspace shell modules. Each section is
// self-contained and progressive: without script the markup it enhances is a
// working link, form or list. New sections go below; nothing above is
// reordered.
// ===========================================================================

// --- Toasts ----------------------------------------------------------------
// A transient status line in the fixed [data-toast-region]. A page-rendered
// confirmation ([data-confirmation]) is also announced this way so an action
// taken elsewhere is noticed without hunting for the notice.
(function () {
    'use strict';
    var region = document.querySelector('[data-toast-region]');
    if (!region) {
        return;
    }

    // Every toast can be put away before it goes: the shared dismiss
    // control, removed by the [data-dismiss] handler.
    function dismissable(element) {
        element.setAttribute('data-dismissable', '');
        var close = document.createElement('button');
        close.type = 'button';
        close.className = 'dismiss';
        close.setAttribute('data-dismiss', '');
        close.setAttribute('aria-label', 'Dismiss');
        var svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        svg.setAttribute('class', 'icon');
        svg.setAttribute('aria-hidden', 'true');
        var use = document.createElementNS('http://www.w3.org/2000/svg', 'use');
        use.setAttribute('href', '#icon-x');
        svg.appendChild(use);
        close.appendChild(svg);
        element.appendChild(close);
    }

    function toast(title, tone) {
        var element = document.createElement('div');
        element.className = 'toast' + (tone ? ' toast--' + tone : '');
        element.setAttribute('role', 'status');
        var strong = document.createElement('strong');
        strong.textContent = title;
        element.appendChild(strong);
        dismissable(element);
        region.appendChild(element);
        window.setTimeout(function () { element.remove(); }, 4200);
    }

    window.pegasusToast = toast;

    // An act that can be put back for eight seconds: the toast carries the
    // one Undo (v28 P16, P41).
    window.pegasusUndoToast = function (title, restore, label) {
        var element = document.createElement('div');
        element.className = 'toast toast--undo';
        element.setAttribute('role', 'status');
        var strong = document.createElement('strong');
        strong.textContent = title;
        var undo = document.createElement('button');
        undo.type = 'button';
        undo.className = 'btn btn--small';
        undo.textContent = label || 'Undo';
        undo.addEventListener('click', function () { restore(); element.remove(); });
        element.appendChild(strong);
        element.appendChild(undo);
        dismissable(element);
        region.appendChild(element);
        window.setTimeout(function () { element.remove(); }, 8000);
    };

    var confirmation = document.querySelector('[data-confirmation]');
    if (confirmation) {
        toast(confirmation.textContent.trim());
    }
})();

// --- Command palette --------------------------------------------------------
// The command dialog is pre-rendered with one [data-route] result per route
// the operator may reach plus a "Search Cases for ..." fallback. Typing
// filters by text; ArrowUp/Down move the selection; Enter follows it. Ctrl K
// anywhere, or Enter in the utility bar's [data-command-input], opens it.
(function () {
    'use strict';
    var dialog = document.querySelector('[data-dialog="command-dialog"]');
    if (!dialog) {
        return;
    }
    var input = dialog.querySelector('[data-command-palette-input]');
    var results = Array.prototype.slice.call(dialog.querySelectorAll('.command-result'));
    var fallback = dialog.querySelector('[data-command-fallback]');
    var fallbackTerm = dialog.querySelector('[data-command-fallback-term]');
    var globalInput = document.querySelector('[data-command-input]');
    if (!input || results.length === 0) {
        return;
    }

    var index = 0;

    function visible() {
        return results.filter(function (result) { return !result.hidden; });
    }

    function select(at) {
        var items = visible();
        if (items.length === 0) {
            return;
        }
        index = (at + items.length) % items.length;
        items.forEach(function (item, position) {
            item.setAttribute('aria-selected', position === index ? 'true' : 'false');
        });
        if (!dialog.hidden) {
            items[index].scrollIntoView({ block: 'nearest' });
        }
    }

    function filter() {
        var term = input.value.trim().toLowerCase();
        results.forEach(function (result) {
            if (result === fallback) {
                return;
            }
            result.hidden = term !== '' && result.textContent.toLowerCase().indexOf(term) < 0;
        });
        if (fallback) {
            fallback.hidden = term === '';
            if (fallbackTerm) {
                fallbackTerm.textContent = input.value.trim();
            }
        }
        select(0);
    }

    function searchUrl() {
        return '/Search?query=' + encodeURIComponent(input.value.trim());
    }

    function go(result) {
        window.location.assign(result === fallback ? searchUrl() : (result.getAttribute('data-route') || '/'));
    }

    function open(seed, opener) {
        // Open through the shell's own dialog binding so focus, inert and
        // Escape behave exactly as for every other dialog. The invoker the
        // dialog records for focus-return is the element that actually asked
        // for the palette (the search box on Enter, whatever had focus on
        // Ctrl+K).
        var source = opener || document.activeElement;
        if (!dialog.hidden) {
            input.value = seed || '';
            filter();
            input.focus();
            return;
        }
        if (dialog.pegasusOpen) {
            dialog.pegasusOpen(source);
        }
        input.value = seed || '';
        filter();
        input.focus();
    }

    window.pegasusOpenCommandPalette = open;

    input.addEventListener('input', filter);
    input.addEventListener('keydown', function (event) {
        if (event.key === 'ArrowDown') {
            event.preventDefault();
            select(index + 1);
        } else if (event.key === 'ArrowUp') {
            event.preventDefault();
            select(index - 1);
        } else if (event.key === 'Enter') {
            event.preventDefault();
            var items = visible();
            if (items[index]) {
                go(items[index]);
            } else {
                window.location.assign(searchUrl());
            }
        }
    });
    results.forEach(function (result) {
        result.addEventListener('click', function () { go(result); });
    });
    if (globalInput) {
        globalInput.addEventListener('keydown', function (event) {
            if (event.key === 'Enter') {
                event.preventDefault();
                open(globalInput.value, globalInput);
            }
        });
    }
    filter();
})();

// --- Layout preference cookies (Phase 5b) ------------------------------------
// The server paints the rail width, the Case record's layout and folded panels
// from first-party cookies, so nothing flashes open before this script runs
// (the CSP allows no inline script). This is the one writer of those cookies:
// Path=/, SameSite=Lax, Secure over HTTPS, readable by script by design; the
// server allow-lists every value it reads (ShellPreferences).
window.pegasusPreferences = (function () {
    'use strict';
    function read(name) {
        var prefix = name + '=';
        var parts = document.cookie ? document.cookie.split('; ') : [];
        for (var i = 0; i < parts.length; i++) {
            if (parts[i].indexOf(prefix) === 0) {
                return parts[i].substring(prefix.length);
            }
        }
        return null;
    }
    // A missing max-age writes a session cookie.
    function write(name, value, maxAgeSeconds) {
        var cookie = name + '=' + value + '; Path=/; SameSite=Lax';
        if (maxAgeSeconds) {
            cookie += '; Max-Age=' + maxAgeSeconds;
        }
        if (window.location.protocol === 'https:') {
            cookie += '; Secure';
        }
        document.cookie = cookie;
    }
    return { read: read, write: write, year: 31536000 };
})();

// --- Rail collapse (v25 C3) ----------------------------------------------------
// The Collapse control at the rail's foot narrows it to icons and counts;
// the choice is per browser in the "pegasus-rail" cookie, which the server
// reads for its first paint. A collapsed link carries its label as a title
// so the name is still readable.
(function () {
    'use strict';
    var shell = document.querySelector('[data-app-shell]');
    var toggle = document.querySelector('[data-rail-toggle]');
    if (!shell || !toggle) {
        return;
    }
    var prefs = window.pegasusPreferences;
    var COOKIE = 'pegasus-rail';
    var links = Array.prototype.slice.call(shell.querySelectorAll('.primary-nav .nav-link'));
    var collapseLabel = toggle.getAttribute('data-label-collapse') || 'Collapse navigation';
    var expandLabel = toggle.getAttribute('data-label-expand') || 'Expand navigation';

    function stored() {
        var value = prefs.read(COOKIE);
        if (value === 'collapsed' || value === 'expanded') {
            return value === 'collapsed';
        }
        prefs.write(COOKIE, 'expanded', prefs.year);
        return false;
    }

    function apply(collapsed) {
        shell.classList.toggle('rail-collapsed', collapsed);
        toggle.setAttribute('aria-expanded', String(!collapsed));
        toggle.setAttribute('aria-label', collapsed ? expandLabel : collapseLabel);
        toggle.title = collapsed ? expandLabel : '';
        links.forEach(function (link) {
            var name = link.querySelector('span:not(.nav-count)');
            if (collapsed && name) {
                link.title = name.textContent.trim();
            } else {
                link.removeAttribute('title');
            }
        });
    }

    var collapsed = stored();
    apply(collapsed);
    toggle.addEventListener('click', function () {
        collapsed = !collapsed;
        apply(collapsed);
        prefs.write(COOKIE, collapsed ? 'collapsed' : 'expanded', prefs.year);
    });
})();

// --- Menus, dismissable notices, collapsible panels ----------------------------
// Frame helpers every page composes (v26 frame rules). Each works on data
// attributes so the markup stays a plain <details>, <button> or <section>:
//   details[data-menu]       one open at a time; Escape or an outside click closes
//   [data-dismiss]           removes the enclosing .notice (or [data-dismissable])
//   [data-collapse="key"]    a panel whose [data-collapse-toggle] folds its body,
//                            remembered in the "pegasus-collapsed" cookie (the
//                            folded keys joined by "|", served in the first paint)
(function () {
    'use strict';

    function openMenus() {
        return Array.prototype.slice.call(document.querySelectorAll('details[data-menu][open]'));
    }

    document.addEventListener('toggle', function (event) {
        var menu = event.target;
        if (!menu || !menu.matches || !menu.matches('details[data-menu]') || !menu.open) {
            return;
        }
        openMenus().forEach(function (other) {
            if (other !== menu && !other.contains(menu)) {
                other.open = false;
            }
        });
    }, true);

    document.addEventListener('click', function (event) {
        openMenus().forEach(function (menu) {
            if (!menu.contains(event.target)) {
                menu.open = false;
            }
        });
    });

    document.addEventListener('keydown', function (event) {
        if (event.key !== 'Escape') {
            return;
        }
        var open = openMenus();
        if (open.length === 0) {
            return;
        }
        open.forEach(function (menu) {
            menu.open = false;
            var summary = menu.querySelector('summary');
            if (summary && menu.contains(document.activeElement)) {
                summary.focus();
            }
        });
    });

    document.addEventListener('click', function (event) {
        var control = event.target.closest('[data-dismiss]');
        if (!control) {
            return;
        }
        var host = control.closest('[data-dismissable], .notice');
        if (host) {
            host.remove();
        }
    });

    var prefs = window.pegasusPreferences;
    var COLLAPSED_COOKIE = 'pegasus-collapsed';
    var KEY_PATTERN = /^[a-z0-9.-]{1,40}$/;
    var MAX_KEYS = 40;

    function saveCollapsedKeys(keys) {
        prefs.write(COLLAPSED_COOKIE, keys.slice(-MAX_KEYS).join('|'), prefs.year);
    }

    function collapsedKeys() {
        var value = prefs.read(COLLAPSED_COOKIE);
        if (value !== null) {
            return value.split('|').filter(function (key) { return KEY_PATTERN.test(key); }).slice(0, MAX_KEYS);
        }
        saveCollapsedKeys([]);
        return [];
    }

    function bindCollapsible(root) {
        root.querySelectorAll('[data-collapse]').forEach(function (panel) {
            if (panel.dataset.collapseBound === 'true') {
                return;
            }
            var toggle = panel.querySelector('[data-collapse-toggle]');
            if (!toggle) {
                return;
            }
            panel.dataset.collapseBound = 'true';
            var key = panel.getAttribute('data-collapse');
            var collapseLabel = toggle.getAttribute('data-label-collapse') || toggle.getAttribute('aria-label') || 'Collapse section';
            var expandLabel = toggle.getAttribute('data-label-expand') || 'Expand section';

            function apply(collapsed) {
                panel.classList.toggle('is-collapsed', collapsed);
                toggle.setAttribute('aria-expanded', String(!collapsed));
                toggle.setAttribute('aria-label', collapsed ? expandLabel : collapseLabel);
            }

            // The server already painted a folded panel from the cookie; the
            // cookie is read again for a body mounted after load.
            var collapsed = panel.classList.contains('is-collapsed') || collapsedKeys().indexOf(key) !== -1;
            apply(collapsed);
            toggle.addEventListener('click', function () {
                collapsed = !collapsed;
                apply(collapsed);
                var keys = collapsedKeys().filter(function (other) { return other !== key; });
                if (collapsed && KEY_PATTERN.test(key)) {
                    keys.push(key);
                }
                saveCollapsedKeys(keys);
            });
        });
    }
    bindCollapsible(document);
    (window.pegasusMountBinders = window.pegasusMountBinders || []).push(bindCollapsible);
})();

// --- Keyboard shortcuts ----------------------------------------------------
// Ctrl K palette, Ctrl U upload, Ctrl N new case, Ctrl S submits the page's
// [data-edit-save] form when one exists, F5 submits [data-refresh-form] so a
// refresh re-queries rather than reloads. Inside an input only Ctrl K acts.
(function () {
    'use strict';
    document.addEventListener('keydown', function (event) {
        var target = event.target;
        var inField = target && target.closest && target.closest('input, select, textarea, [contenteditable="true"]');
        var control = event.ctrlKey || event.metaKey;
        var key = typeof event.key === 'string' ? event.key.toLowerCase() : '';

        if (control && key === 'k') {
            event.preventDefault();
            if (window.pegasusOpenCommandPalette) {
                window.pegasusOpenCommandPalette('', target);
            }
            return;
        }
        if (inField) {
            return;
        }
        if (control && key === 'u' && document.querySelector('[data-route="/Upload"]')) {
            event.preventDefault();
            window.location.assign('/Upload');
        } else if (control && key === 'n') {
            event.preventDefault();
            window.location.assign('/Cases/Create');
        } else if (control && key === 's') {
            var dirty = window.pegasusDirtyEditForm && window.pegasusDirtyEditForm();
            var save = dirty || document.querySelector('[data-edit-save]');
            if (save) {
                event.preventDefault();
                var saveForm = save.tagName === 'FORM' ? save : save.closest('form');
                saveForm.requestSubmit();
            }
        } else if (event.key === 'F5' && !control) {
            var refresh = document.querySelector('[data-refresh-form]');
            if (refresh) {
                event.preventDefault();
                refresh.requestSubmit();
            }
        }
    });
})();

// --- Row lists: ArrowUp/Down roving focus -------------------------------------
(function () {
    'use strict';
    var ROW = '.row-button, tr[data-select-href]';
    document.querySelectorAll('[data-row-list]').forEach(function (list) {
        list.addEventListener('keydown', function (event) {
            if (event.key !== 'ArrowDown' && event.key !== 'ArrowUp') {
                return;
            }
            var rows = Array.prototype.filter.call(
                list.querySelectorAll(ROW),
                function (row) { return !row.hidden; });
            if (rows.length === 0) {
                return;
            }
            var at = rows.indexOf(document.activeElement.closest(ROW));
            event.preventDefault();
            var next = event.key === 'ArrowDown' ? Math.min(at + 1, rows.length - 1) : Math.max(at - 1, 0);
            var row = rows[next];
            if (row.tagName === 'TR' && !row.hasAttribute('tabindex')) {
                row.setAttribute('tabindex', '-1');
            }
            row.focus();
        });
    });
})();

// --- Sort toggles ------------------------------------------------------------
// The server sorts; the toggle is a link or a form button whose arrow glyph
// swaps on activation so the direction reads before the page returns.
(function () {
    'use strict';
    document.querySelectorAll('[data-sort-toggle]').forEach(function (toggle) {
        toggle.addEventListener('click', function () {
            var label = toggle.querySelector('[data-sort-arrow]') || toggle;
            label.textContent = label.textContent.indexOf('↓') >= 0
                ? label.textContent.replace('↓', '↑')
                : label.textContent.replace('↑', '↓');
        });
    });
})();

// --- Row-selection preview -----------------------------------------------------
// A row carrying [data-select-href] is a link to its full record; with script
// a click, Enter or focus swaps the sibling <template> content into the
// page's [data-preview-target] and rewrites the address, so the operator
// reads the record beside the list. Without script the link navigates.
(function () {
    'use strict';
    var target = document.querySelector('[data-preview-target]');
    if (!target) {
        return;
    }
    var rows = Array.prototype.slice.call(document.querySelectorAll('[data-select-href]'));
    if (rows.length === 0) {
        return;
    }

    function select(row, moveFocus) {
        var template = row.querySelector('template');
        if (!template || !('content' in template)) {
            return;
        }
        rows.forEach(function (candidate) {
            candidate.setAttribute('aria-selected', candidate === row ? 'true' : 'false');
        });
        target.replaceChildren(template.content.cloneNode(true));
        var url = new URL(window.location.href);
        url.searchParams.set('selected', row.getAttribute('data-select-id') || row.getAttribute('data-select-href'));
        // Two rows of one record (an Inspection + Audit Case's two Search
        // entries) are told apart by data-select-view.
        var view = row.getAttribute('data-select-view');
        if (view) {
            url.searchParams.set('selectedView', view);
        } else {
            url.searchParams.delete('selectedView');
        }
        window.history.replaceState(null, '', url.toString());
        if (moveFocus) {
            row.focus();
        }
    }

    rows.forEach(function (row) {
        if (!row.hasAttribute('tabindex')) {
            row.setAttribute('tabindex', '0');
        }
        row.addEventListener('click', function (event) {
            if (event.target.closest('a, button')) {
                return;
            }
            event.preventDefault();
            select(row, false);
        });
        row.addEventListener('keydown', function (event) {
            if (event.key === 'Enter' && !event.target.closest('a, button')) {
                event.preventDefault();
                select(row, true);
            }
        });
        row.addEventListener('focus', function () { select(row, false); });
    });

    var initial = rows.find(function (row) { return row.getAttribute('aria-selected') === 'true'; });
    if (initial) {
        select(initial, false);
    }
})();

// --- Estimate tabs: roving tabindex ---------------------------------------------
(function () {
    'use strict';
    document.querySelectorAll('[role="tablist"]').forEach(function (list) {
        var tabs = Array.prototype.slice.call(list.querySelectorAll('[role="tab"]'));
        if (tabs.length === 0) {
            return;
        }
        function sync(active) {
            tabs.forEach(function (tab) {
                tab.setAttribute('tabindex', tab === active ? '0' : '-1');
            });
        }
        sync(tabs.find(function (tab) { return tab.getAttribute('aria-selected') === 'true'; }) || tabs[0]);
        list.addEventListener('keydown', function (event) {
            var at = tabs.indexOf(document.activeElement);
            if (at < 0) {
                return;
            }
            var next = null;
            if (event.key === 'ArrowRight') { next = tabs[(at + 1) % tabs.length]; }
            else if (event.key === 'ArrowLeft') { next = tabs[(at - 1 + tabs.length) % tabs.length]; }
            else if (event.key === 'Home') { next = tabs[0]; }
            else if (event.key === 'End') { next = tabs[tabs.length - 1]; }
            if (next) {
                event.preventDefault();
                sync(next);
                next.focus();
            }
        });
    });
})();

// --- Image rotate ----------------------------------------------------------------
// [data-rotate] cycles the .rot-* classes on the nearest [data-rotate-target]
// (the viewer stage, a gallery item), a pure view transform the CSP-safe
// classes carry.
(function () {
    'use strict';
    var steps = ['', 'rot-90', 'rot-180', 'rot-270'];
    document.querySelectorAll('[data-rotate]').forEach(function (button) {
        button.addEventListener('click', function () {
            var scope = button.closest('.dialog, .panel, .gallery-item') || document;
            var target = scope.querySelector('[data-rotate-target]');
            if (!target) {
                return;
            }
            // An unrotated target carries no class, which is step 0; findIndex
            // skips the empty step and answers -1 there, and -1 + 1 would pick
            // the empty step again, so the first turn never happened.
            var current = Math.max(0, steps.findIndex(function (step) { return step && target.classList.contains(step); }));
            steps.forEach(function (step) { if (step) { target.classList.remove(step); } });
            var next = steps[(current + 1) % steps.length];
            if (next) {
                target.classList.add(next);
            }
        });
    });
})();

// --- Report a problem ----------------------------------------------------------
// The last ten script errors are kept for the session so a report can carry
// them; on Send the form's hidden facts are filled from the page: the window,
// whether a record is being edited, and the Case reference on screen.
(function () {
    'use strict';
    var key = 'pegasus.problem.errors';
    function read() {
        try { return JSON.parse(window.sessionStorage.getItem(key) || '[]'); } catch (error) { return []; }
    }
    function remember(text) {
        try {
            var list = read();
            list.unshift(new Date().toISOString() + ' ' + String(text).slice(0, 300));
            window.sessionStorage.setItem(key, JSON.stringify(list.slice(0, 10)));
        } catch (error) { /* storage unavailable: the report goes without them */ }
    }
    function reportRoute(form) {
        var location = new URL(window.location.href);
        var section = (location.searchParams.get('section') || '').trim().toLowerCase();
        var sections = (form.getAttribute('data-problem-case-sections') || '').split(',');
        return sections.indexOf(section) >= 0
            ? location.pathname + '?section=' + encodeURIComponent(section)
            : location.pathname;
    }
    window.addEventListener('error', function (event) {
        remember((event.message || 'error') + (event.filename ? ' @ ' + event.filename + ':' + event.lineno : ''));
    });
    window.addEventListener('unhandledrejection', function (event) {
        var reason = event.reason && event.reason.message ? event.reason.message : String(event.reason);
        remember('unhandled rejection: ' + reason);
    });
    document.querySelectorAll('[data-problem-form]').forEach(function (form) {
        form.addEventListener('submit', function () {
            var set = function (selector, value) {
                var input = form.querySelector(selector);
                if (input) { input.value = value; }
            };
            set('[data-problem-route]', reportRoute(form));
            set('[data-problem-return]', window.location.pathname + window.location.search);
            set('[data-problem-viewport]', window.innerWidth + 'x' + window.innerHeight);
            set('[data-problem-editing]', document.querySelector('.case-record.is-editing') ? 'true' : 'false');
            set('[data-problem-errors]', JSON.stringify(read()));
            var reference = document.querySelector('.case-record .ribbon-value');
            set('[data-problem-case]', reference ? reference.textContent.trim() : '');
        });
    });
})();
