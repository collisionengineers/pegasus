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
    // The feedback ends with the navigation it announces; a page that
    // refreshes in place instead (the Case record) ends it itself with
    // pegasusResetRefresh, kept beside the busy step so the two cannot drift.
    function refreshRegion(form) {
        return form.closest('[data-refresh-region]') || form.parentElement;
    }
    function bindRefreshFeedback(root) {
        root.querySelectorAll('[data-refresh-form]').forEach(function (form) {
            if (form.dataset.refreshBound === 'true') {
                return;
            }
            form.dataset.refreshBound = 'true';
            form.addEventListener('submit', function () {
                var region = refreshRegion(form);
                if (region) {
                    region.classList.add('is-refreshing');
                    region.setAttribute('aria-busy', 'true');
                }
                var label = form.querySelector('[data-refresh-label]');
                if (label) {
                    // The idle wording is the partial's; the reset puts it back.
                    if (!label.dataset.idleLabel) {
                        label.dataset.idleLabel = label.textContent;
                    }
                    label.textContent = 'Refreshing';
                }
                form.querySelectorAll('button').forEach(function (button) {
                    button.disabled = true;
                });
            });
        });
    }
    function resetRefresh(form) {
        var region = refreshRegion(form);
        if (region) {
            region.classList.remove('is-refreshing');
            region.removeAttribute('aria-busy');
        }
        var label = form.querySelector('[data-refresh-label]');
        if (label && label.dataset.idleLabel) {
            label.textContent = label.dataset.idleLabel;
        }
        form.querySelectorAll('button').forEach(function (button) {
            button.disabled = false;
        });
    }
    bindRefreshFeedback(document);
    (window.pegasusMountBinders = window.pegasusMountBinders || []).push(bindRefreshFeedback);
    window.pegasusResetRefresh = resetRefresh;

    // Action feedback: the Refresh button's twin for every action. From the
    // press until the result arrives, the pressed control says what it is
    // doing (its data-busy-label, or the page's default from the layout), the
    // loader glyph turns in place of its icon with the Refresh icon's own
    // spin, and the form's other submit buttons stand aside. The words are
    // the signal and the glyph decorates them, so reduced motion and forced
    // colours lose nothing. A press still waiting after five seconds says
    // "Still …" (the layout's data-busy-still word in front of its own).
    //
    // The pressed button is never disabled: a disabled submitter drops its
    // name and value from the post. A busy form refuses another submit
    // instead, which is the double-submit guard every POST form has.
    //
    // A full-page post ends with its navigation. A script that answers a
    // press in place (case-workspace.js, mail-compose.js, upload.js) prevents
    // the default, so this listener leaves it alone, and the script starts
    // and ends the state itself through window.pegasusBusy. Such a script
    // ends a success with { done: true }: the button holds a tick and its
    // data-busy-done word (or its own label) for a moment. A page that
    // reloads shows its usual notice instead, so no full-page post is done.
    var busyContents = new WeakMap();
    var busyForms = new WeakMap();
    var busyHolds = new WeakMap();
    var busyStatus = null;
    var STILL_AFTER_MS = 5000;
    var DONE_FOR_MS = 1400;
    function busyWords(control) {
        return control.getAttribute('data-busy-label')
            || document.documentElement.getAttribute('data-busy-label') || '';
    }
    // "Saving…" becomes "Still saving…"; every busy word opens with its verb.
    function stillWords(words) {
        var still = document.documentElement.getAttribute('data-busy-still') || '';
        return still && words ? still + ' ' + words.charAt(0).toLowerCase() + words.slice(1) : '';
    }
    function glyph(name, className) {
        var svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        svg.setAttribute('class', className);
        svg.setAttribute('aria-hidden', 'true');
        var use = document.createElementNS('http://www.w3.org/2000/svg', 'use');
        use.setAttribute('href', '#' + name);
        svg.appendChild(use);
        return svg;
    }
    function announceBusy(words) {
        if (!busyStatus) {
            busyStatus = document.createElement('span');
            busyStatus.className = 'sr-only';
            busyStatus.setAttribute('role', 'status');
            busyStatus.setAttribute('aria-live', 'polite');
            document.body.appendChild(busyStatus);
        }
        busyStatus.textContent = words;
    }
    function isSubmitControl(element) {
        return (element instanceof HTMLButtonElement || element instanceof HTMLInputElement)
            && element.type === 'submit';
    }
    function defaultSubmitter(form) {
        return Array.prototype.find.call(form.elements, isSubmitControl) || null;
    }
    // Takes the control's own content away and shows a glyph and words in its
    // place. What comes back restores it.
    function takeOver(control, iconName, iconClass, words) {
        var saved = { ariaLabel: control.getAttribute('aria-label'), value: null, nodes: null, label: null, timer: null, form: control.form || null };
        if (control instanceof HTMLSelectElement) {
            // A choice that posts keeps its options; it only stands still.
        } else if (control instanceof HTMLInputElement) {
            saved.value = control.value;
            control.value = words;
        } else {
            var hasText = control.textContent.trim() !== '';
            saved.nodes = document.createDocumentFragment();
            while (control.firstChild) {
                saved.nodes.appendChild(control.firstChild);
            }
            control.appendChild(glyph(iconName, iconClass));
            if (hasText) {
                saved.label = document.createElement('span');
                saved.label.textContent = words;
                control.appendChild(saved.label);
            } else {
                control.setAttribute('aria-label', words);
            }
        }
        return saved;
    }
    function reword(control, saved, words) {
        if (saved.label) {
            saved.label.textContent = words;
        } else if (saved.value !== null) {
            control.value = words;
        } else if (!(control instanceof HTMLSelectElement)) {
            control.setAttribute('aria-label', words);
        }
        announceBusy(words);
    }
    function giveBack(control, saved) {
        if (saved.timer !== null) {
            window.clearTimeout(saved.timer);
        }
        if (saved.nodes) {
            control.textContent = '';
            control.appendChild(saved.nodes);
        } else if (saved.value !== null) {
            control.value = saved.value;
        }
        if (saved.ariaLabel === null) {
            control.removeAttribute('aria-label');
        } else {
            control.setAttribute('aria-label', saved.ariaLabel);
        }
    }
    function startControl(control, words) {
        if (!control) {
            return;
        }
        // A press during the tick is a new action: the tick gives way.
        if (busyHolds.has(control)) {
            finishHold(control);
        }
        if (busyContents.has(control)) {
            return;
        }
        words = words || busyWords(control);
        var saved = takeOver(control, 'icon-loader', 'icon busy-spin', words);
        var still = stillWords(words);
        if (still && !(control instanceof HTMLSelectElement)) {
            saved.timer = window.setTimeout(function () {
                saved.timer = null;
                reword(control, saved, still);
            }, STILL_AFTER_MS);
        }
        busyContents.set(control, saved);
        control.setAttribute('data-busy', '');
        control.setAttribute('aria-busy', 'true');
        if (control instanceof HTMLAnchorElement) {
            control.setAttribute('aria-disabled', 'true');
        }
        announceBusy(words);
    }
    function endControl(control, options) {
        var saved = control ? busyContents.get(control) : null;
        if (!saved) {
            return;
        }
        busyContents.delete(control);
        giveBack(control, saved);
        control.removeAttribute('data-busy');
        control.removeAttribute('aria-busy');
        if (control instanceof HTMLAnchorElement) {
            control.removeAttribute('aria-disabled');
        }
        if (options && options.done) {
            showDone(control.isConnected ? control : replacementFor(control, saved.form), control);
        }
    }
    // The pressed control drawn afresh: a Case action redraws the Case, so
    // the result shows on the button that took the pressed one's place. Most
    // Case forms have no id, so a form is also found by its action.
    function replacementFor(control, form) {
        if (!form) {
            return null;
        }
        var next = (form.id && document.getElementById(form.id)) || null;
        var action = form.getAttribute('action');
        if (!next && action) {
            next = Array.prototype.find.call(document.forms, function (candidate) {
                return candidate.getAttribute('action') === action;
            }) || null;
        }
        if (!(next instanceof HTMLFormElement) || next === form) {
            return null;
        }
        var formaction = control.getAttribute('formaction');
        return Array.prototype.find.call(next.elements, function (element) {
            return isSubmitControl(element) && element.name === control.name && element.value === control.value
                && element.getAttribute('formaction') === formaction;
        }) || null;
    }
    // The tick: the control says its done word (data-busy-done), or keeps its
    // own label, beside the check glyph for a moment, then is itself again.
    function showDone(target, source) {
        if (!target || !target.isConnected || busyContents.has(target) || busyHolds.has(target)
            || target instanceof HTMLSelectElement) {
            return;
        }
        // An icon-only control's own words are its accessible name.
        var own = target instanceof HTMLInputElement
            ? target.value
            : target.textContent.trim() || target.getAttribute('aria-label') || '';
        var words = target.getAttribute('data-busy-done') || source.getAttribute('data-busy-done') || own;
        var saved = takeOver(target, 'icon-check', 'icon busy-done', words);
        saved.timer = window.setTimeout(function () {
            saved.timer = null;
            finishHold(target);
        }, DONE_FOR_MS);
        busyHolds.set(target, saved);
        target.setAttribute('data-busy-complete', '');
        announceBusy(words);
    }
    function finishHold(control) {
        var saved = control ? busyHolds.get(control) : null;
        if (!saved) {
            return;
        }
        busyHolds.delete(control);
        giveBack(control, saved);
        control.removeAttribute('data-busy-complete');
    }
    // words overrides the control's own, for a control that stands in for
    // another: a menu's button while the item pressed inside it loads.
    function startBusy(control, form, words) {
        form = form || (control && control.form) || null;
        if (form && !control) {
            control = defaultSubmitter(form);
        }
        startControl(control, words);
        if (!form) {
            return;
        }
        busyForms.set(form, control);
        form.setAttribute('data-busy-form', '');
        // form.elements includes the buttons tied to the form by form="…",
        // such as a dialog's footer, so they stand aside too.
        Array.prototype.forEach.call(form.elements, function (element) {
            if (element !== control && isSubmitControl(element) && !element.hasAttribute('aria-disabled')) {
                element.setAttribute('aria-disabled', 'true');
                element.setAttribute('data-busy-aside', '');
            }
        });
    }
    // options.done: the action succeeded and the page stayed put, so the
    // control shows its tick. Without it the control is simply itself again.
    function endBusy(target, options) {
        if (!target) {
            return;
        }
        if (target instanceof HTMLFormElement) {
            var control = busyForms.get(target);
            busyForms.delete(target);
            target.removeAttribute('data-busy-form');
            Array.prototype.forEach.call(target.elements, function (element) {
                if (element.hasAttribute('data-busy-aside')) {
                    element.removeAttribute('aria-disabled');
                    element.removeAttribute('data-busy-aside');
                }
            });
            endControl(control, options);
            return;
        }
        if (busyHolds.has(target)) {
            finishHold(target);
            return;
        }
        endControl(target, options);
    }
    window.pegasusBusy = { start: startBusy, end: endBusy, isBusy: function (form) { return busyForms.has(form); } };
    // A post made with form.submit() fires no submit event: the pressed
    // control starts the state, and a second press while it runs is refused.
    // True when the form was already busy.
    function startBusyOnce(control, form) {
        if (busyForms.has(form)) {
            return true;
        }
        busyForms.set(form, null);
        startControl(control);
        busyForms.set(form, control);
        form.setAttribute('data-busy-form', '');
        return false;
    }
    window.pegasusBusy.startOnce = startBusyOnce;

    document.addEventListener('submit', function (event) {
        var form = event.target;
        if (form instanceof HTMLFormElement && busyForms.has(form)) {
            event.preventDefault();
            event.stopImmediatePropagation();
        }
    }, true);
    // On window, so it runs after every page script has had its say: a
    // submit a script prevented is either refused or answered in place.
    window.addEventListener('submit', function (event) {
        var form = event.target;
        var submitter = event.submitter || null;
        if (event.defaultPrevented || !(form instanceof HTMLFormElement)) {
            return;
        }
        var method = (submitter && submitter.getAttribute('formmethod')) || form.getAttribute('method') || 'get';
        if (method.toLowerCase() !== 'post' || form.hasAttribute('target')
            || (submitter && submitter.hasAttribute('formtarget'))) {
            return;
        }
        startBusy(submitter || defaultSubmitter(form), form);
    });
    // A page restored from the back/forward cache comes back as it was left,
    // mid-post; it is idle again.
    window.addEventListener('pageshow', function (event) {
        if (!event.persisted) {
            return;
        }
        document.querySelectorAll('[data-busy-form]').forEach(endBusy);
        document.querySelectorAll('[data-busy]').forEach(endBusy);
        document.querySelectorAll('[data-busy-complete]').forEach(endBusy);
        document.querySelectorAll('[data-refresh-form]').forEach(resetRefresh);
    });

    // A link button that navigates shows the busy state a submit does until
    // the next page arrives (v36 item R, 9 October 2026): the Work Centre
    // rows' arrows, the Cases list's Open full Case, a Case's Add evidence and
    // the Next action's control. In-page jumps, downloads, dialog openers and
    // new-window links are not navigations of this kind.
    document.addEventListener('click', function (event) {
        var link = event.target.closest ? event.target.closest('a.btn[href]') : null;
        if (!link || event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
            return;
        }
        var href = link.getAttribute('href') || '';
        if (!href || href.charAt(0) === '#' || link.target || link.hasAttribute('download')
            || link.hasAttribute('data-busy-download') || link.hasAttribute('data-no-busy')
            || link.hasAttribute('data-dialog-open') || link.hasAttribute('data-section-jump')
            || link.getAttribute('aria-disabled') === 'true') {
            return;
        }
        startBusy(link, null);
    });

    // A download answers with a file, not a page, so no navigation ends its
    // busy state. A [data-busy-download] link or form fetches the file
    // instead and hands it to the browser: the state ends when the file
    // arrives, and a refusal shows as a toast rather than a blank page. A
    // refusal's plain-text body is its reason.
    function fileName(response) {
        var disposition = response.headers.get('Content-Disposition') || '';
        var encoded = /filename\*=UTF-8''([^;]+)/i.exec(disposition);
        if (encoded) {
            try { return decodeURIComponent(encoded[1]); } catch (error) { /* fall through */ }
        }
        var plain = /filename="?([^";]+)"?/i.exec(disposition);
        return plain ? plain[1] : '';
    }
    function busyDownload(control, form, url, init) {
        startBusy(control, form);
        var done = false;
        var failed = (control && control.getAttribute('data-busy-download-failed'))
            || (form && form.getAttribute('data-busy-download-failed')) || '';
        init.credentials = 'same-origin';
        init.headers = { 'X-Requested-With': 'fetch' };
        return fetch(url, init).then(function (response) {
            var type = response.headers.get('Content-Type') || '';
            if (!response.ok || type.indexOf('text/') === 0 && !/attachment/i.test(response.headers.get('Content-Disposition') || '')) {
                return response.text().then(function (text) {
                    var reason = type.indexOf('text/plain') === 0 ? text.trim() : '';
                    throw { refusal: reason || failed };
                });
            }
            return response.blob().then(function (blob) {
                var href = URL.createObjectURL(blob);
                var save = document.createElement('a');
                save.href = href;
                save.download = fileName(response);
                document.body.appendChild(save);
                save.click();
                save.remove();
                done = true;
                window.setTimeout(function () { URL.revokeObjectURL(href); }, 60000);
            });
        }).catch(function (error) {
            if (typeof window.pegasusToast === 'function') {
                // Only the server's own reason is shown; a network failure's
                // browser text is not operator wording.
                window.pegasusToast(error && error.refusal || failed, 'danger');
            }
        }).finally(function () {
            endBusy(form || control, { done: done });
        });
    }
    document.addEventListener('click', function (event) {
        var link = event.target.closest && event.target.closest('a[data-busy-download]');
        if (!link || event.defaultPrevented || event.button !== 0
            || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) {
            return;
        }
        event.preventDefault();
        if (busyContents.has(link)) {
            return;
        }
        busyDownload(link, null, link.href, { method: 'GET' });
    });
    // A form whose other buttons save (the Case list's presets) marks only its
    // download buttons.
    document.addEventListener('submit', function (event) {
        var form = event.target;
        var submitter = event.submitter || null;
        if (!(form instanceof HTMLFormElement) || event.defaultPrevented
            || !(form.hasAttribute('data-busy-download')
                || submitter && submitter.hasAttribute('data-busy-download'))) {
            return;
        }
        event.preventDefault();
        var url = (submitter && submitter.getAttribute('formaction')) || form.action;
        busyDownload(submitter || defaultSubmitter(form), form, url, {
            method: 'POST',
            body: new FormData(form, submitter && submitter.name ? submitter : undefined)
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

// UI-10: evidence-only mail preview. A subject is the message's own link;
// this enhancement previews its row on pointer/keyboard intent and reads
// the same authorized exact-message projection without moving focus or state.
// When that intent moves on, the pane restores the selected message instead
// of hiding: the pane is a fixture of the page, not a tooltip. Clicking a row
// anywhere but a link or a button pins it as the selected message.
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
        // (the page model resolves one whenever it renders the pane at all).
        // The selected row, which a click may move, is the pane's fallback
        // wherever intent goes.
        var selectedRow = rows.filter(function (row) {
            return row.getAttribute('aria-current') === 'true';
        })[0] || null;
        if (!selectedRow) {
            return;
        }
        var actions = facts.querySelector('[data-mail-preview-actions]');
        var matched = facts.querySelector('[data-mail-preview-matched]');
        // Where the search term matched is drawn for the row the page was
        // drawn with; no other row's preview carries it.
        var matchedRow = selectedRow;
        activeRow = selectedRow;

        var field = function (name) {
            return facts.querySelector('[data-mail-preview-' + name + ']');
        };
        var caseLink = function () {
            return actions && actions.querySelector('[data-mail-preview-case]');
        };
        // The state chip is the _StatusChip partial's span; its tone class is
        // the one the server's tone table gave it, and the JSON carries the
        // same table's answer for a hovered row.
        // A Sent item carries no processing chip: the span is empty and the
        // JSON's state is null, so the chip is absent rather than blank.
        var chip = function () {
            return field('state').querySelector('.status');
        };
        var toneOf = function (element) {
            var tone = /status--([a-z]+)/.exec(element.className);
            return tone ? tone[1] : 'neutral';
        };
        var paintChip = function (state, tone) {
            var existing = chip();
            if (!state) {
                if (existing) {
                    existing.remove();
                }
                return;
            }
            if (!existing) {
                existing = document.createElement('span');
                field('state').appendChild(existing);
            }
            existing.textContent = state;
            existing.className = 'status status--' + (tone || 'neutral');
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
                mailbox: field('mailbox').textContent,
                state: chip() ? chip().textContent : null,
                stateTone: chip() ? toneOf(chip()) : null,
                excerpt: field('excerpt').textContent,
                attachments: field('attachments').textContent,
                classification: field('classification').textContent,
                association: field('association').textContent,
                folder: field('folder').textContent,
                caseUrl: caseLink() ? caseLink().getAttribute('href') : null,
                caseAction: caseLink() ? caseLink().querySelector('span').textContent : null
            });

        // The actions are the shown row's own: Open full message is its
        // subject's link, which carries the list's values, and Open Case or
        // Open Triage comes with its preview.
        var renderActions = function (row, data) {
            if (!actions) {
                return;
            }
            actions.querySelector('[data-mail-preview-open]').setAttribute(
                'href',
                row.querySelector('[data-mail-preview-trigger]').getAttribute('href'));
            var link = caseLink();
            if (!data.caseUrl) {
                if (link) {
                    link.hidden = true;
                }
                return;
            }
            if (!link) {
                link = document.createElement('a');
                link.className = 'btn';
                link.setAttribute('data-mail-preview-case', '');
                link.innerHTML = '<svg class="icon" aria-hidden="true"><use href="#icon-folder" /></svg><span></span>';
                actions.appendChild(link);
            }
            link.setAttribute('href', data.caseUrl);
            link.querySelector('span').textContent = data.caseAction;
            link.hidden = false;
        };

        // The actions belong to the selected message; while a transient
        // preview shows a different row, they are not its.
        var showOwnFacts = function (row) {
            if (actions) {
                actions.hidden = row !== selectedRow;
            }
            if (matched) {
                matched.hidden = row !== matchedRow;
            }
        };

        var render = function (row, data) {
            field('sender').textContent = data.sender;
            field('subject').textContent = data.subject;
            field('received').textContent = data.received;
            field('received').setAttribute('datetime', data.receivedAtUtc);
            field('mailbox').textContent = data.mailbox;
            paintChip(data.state, data.stateTone);
            field('excerpt').textContent = data.excerpt;
            field('attachments').textContent = data.attachments;
            field('classification').textContent = data.classification;
            field('association').textContent = data.association;
            field('folder').textContent = data.folder;
            renderActions(row, data);

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
            showOwnFacts(row);
            panel.hidden = false;
            status.hidden = false;
            status.textContent = 'Loading quick preview…';
            facts.hidden = true;
            panel.setAttribute('aria-busy', 'true');

            if (cache.has(url)) {
                render(row, cache.get(url));
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
                    render(row, data);
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
        // back to the selected message, whose actions must stay reachable. select() no-ops when that row is already active, so
        // leaving the selected row itself leaves the pane untouched.
        var restoreSelection = function () {
            select(selectedRow);
        };

        // Pinning makes a row the selected message: it carries the selection
        // mark, its preview stays when intent moves on, the pane's actions
        // are its own, and the address names it, so a reload or a return
        // from the message lands on it. Without script the row still links.
        var pin = function (row) {
            if (row === selectedRow) {
                return;
            }
            selectedRow.removeAttribute('aria-current');
            row.setAttribute('aria-current', 'true');
            selectedRow = row;
            if (activeRow === row) {
                showOwnFacts(row);
            } else {
                select(row);
            }
            var address = new URL(window.location.href);
            address.searchParams.set('selected', row.getAttribute('data-mail-row'));
            window.history.replaceState(window.history.state, '', address.toString());
        };

        rows.forEach(function (row) {
            var trigger = row.querySelector('[data-mail-preview-trigger]');
            row.addEventListener('pointerenter', function () { select(row); });
            row.addEventListener('click', function (event) {
                if (!event.target.closest('a, button, form')) {
                    pin(row);
                }
            });
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
                    remove.setAttribute('data-busy-label', (preCaseTools && preCaseTools.getAttribute('data-busy-tagging')) || '');
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

        // Both post with submit(), which fires no submit event, so the
        // pressed control starts its own busy state; the page load ends it.
        function postPreCaseCrop(clear, control) {
            var item = preCaseItem();
            if (!item || !preCaseCropForm || (window.pegasusBusy && window.pegasusBusy.startOnce(control, preCaseCropForm))) {
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

        function postPreCaseTag(tagId, applied, control) {
            var item = preCaseItem();
            if (!item || !preCaseTagForm || !tagId || (window.pegasusBusy && window.pegasusBusy.startOnce(control, preCaseTagForm))) {
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
            preCaseTools.querySelector('[data-precase-crop-apply]').addEventListener('click', function (event) { postPreCaseCrop(false, event.currentTarget); });
            preCaseTools.querySelector('[data-precase-crop-clear]').addEventListener('click', function (event) { postPreCaseCrop(true, event.currentTarget); });
            preCaseTools.querySelector('[data-precase-crop-cancel]').addEventListener('click', endPreCaseCrop);
            preCaseTools.querySelector('[data-precase-tag-select]').addEventListener('change', function (event) {
                postPreCaseTag(event.target.value, true, event.target);
            });
            preCaseTools.addEventListener('click', function (event) {
                var remove = event.target.closest('[data-precase-tag-remove]');
                if (remove) {
                    postPreCaseTag(remove.getAttribute('data-precase-tag-remove'), false, remove);
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
    })();

    (function () {
        'use strict';

        // A gallery tile that fails to load, on every page that draws
        // img[data-gallery-image], the Case page included. The route answers a
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
        (window.pegasusMountBinders = window.pegasusMountBinders || []).push(bindGalleryImages);
    })();

    // The Other classification name and reasoning fields exist only while an
    // Other option is selected, and the case type only while a New instruction
    // is; the select drives their visibility.
    document.querySelectorAll('[data-other-toggle]').forEach(function (select) {
        var scope = select.closest('[data-dialog]') || document;
        function sync() {
            var isOther = select.value === 'other-received' || select.value === 'other-sent';
            scope.querySelectorAll('[data-other-field]').forEach(function (field) {
                field.hidden = !isOther;
            });
            var isInstruction = select.value.indexOf('received:NewInstructionReceived') === 0;
            scope.querySelectorAll('[data-work-type-field]').forEach(function (field) {
                field.hidden = !isInstruction;
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
//                            folded keys joined by "|", served in the first paint);
//                            with [data-collapse-folded] the panel starts folded
//                            and the cookie names it once opened
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
        // An item that opens a dialog closes its own menu too, so the menu never
        // stands open behind the dialog's backdrop (v36 item V, 9 October 2026).
        var opensDialog = event.target.closest && event.target.closest('[data-dialog-open]');
        openMenus().forEach(function (menu) {
            if (!menu.contains(event.target) || opensDialog) {
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
            // cookie is read again for a body mounted after load. The cookie
            // names a panel that is not as it starts: folded, or opened when
            // it starts folded.
            var foldedFirst = panel.hasAttribute('data-collapse-folded');
            var named = collapsedKeys().indexOf(key) !== -1;
            var collapsed = foldedFirst ? !named : panel.classList.contains('is-collapsed') || named;
            apply(collapsed);
            toggle.addEventListener('click', function () {
                collapsed = !collapsed;
                apply(collapsed);
                var keys = collapsedKeys().filter(function (other) { return other !== key; });
                if (collapsed !== foldedFirst && KEY_PATTERN.test(key)) {
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
            var save = document.querySelector('[data-edit-save]');
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
    var ROW = '.row-button, .scope-button, tr[data-select-href], tr[data-cases-row]';
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
            // A row that is a container rather than a control (the Inbox's
            // rows) hands focus to its first link, which is what Enter opens.
            if (row.tagName === 'DIV') {
                row = row.querySelector('a[href]') || row;
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

// --- Files tabs and the Correspondence message dialog ----------------------------
// Shared by the Case record and the Triage Case (moved from case-workspace.js).
(function () {
    'use strict';
    // ---- Files tabs --------------------------------------------------------------
    // Both panels are rendered, so with no script the section is the lists one
    // after the other; script turns the strip on and shows one at a time. The
    // chosen tab rides in the URL hash (`#case-files-<tab>`), which the Custody
    // redirects name too, so a tag/untag/create lands back on Images. A tab
    // set may hold another (the Triage Case's Files inside its page tabs):
    // each strip and panel belongs to its nearest wrap, and a wrap may name
    // its own hash prefix. An inner wrap's prefix extends its outer tab's
    // (`#triage-files-<tab>`), so a reload opens the outer tab as well.
    function bindFileTabs(root) {
        Array.prototype.slice.call((root || document).querySelectorAll('[data-file-tabs-wrap]')).forEach(function (wrap) {
            if (wrap.dataset.fileTabsBound === 'true') { return; }
            function own(selector) {
                return Array.prototype.slice.call(wrap.querySelectorAll(selector)).filter(function (element) {
                    return element.closest('[data-file-tabs-wrap]') === wrap;
                });
            }
            var strip = own('[data-file-tabs]')[0];
            var panels = own('[data-file-tab-panel]');
            var buttons = strip ? Array.prototype.slice.call(strip.querySelectorAll('[data-file-tab]')) : [];
            if (!strip || !panels.length || !buttons.length) { return; }
            wrap.dataset.fileTabsBound = 'true';
            var prefix = '#' + (wrap.getAttribute('data-file-tabs-hash') || 'case-files-');
            function show(name, remember) {
                panels.forEach(function (panel) { panel.hidden = panel.getAttribute('data-file-tab-panel') !== name; });
                buttons.forEach(function (button) { button.setAttribute('aria-selected', button.getAttribute('data-file-tab') === name ? 'true' : 'false'); });
                wrap.setAttribute('data-file-tabs-active', name);
                if (remember && window.history && window.history.replaceState) {
                    window.history.replaceState(null, '', prefix + name);
                }
            }
            buttons.forEach(function (button) {
                button.addEventListener('click', function () { show(button.getAttribute('data-file-tab'), true); });
            });
            strip.hidden = false;
            wrap.classList.add('is-tabbed');
            var hash = window.location.hash || '';
            var fromHash = hash.indexOf(prefix) === 0 ? hash.slice(prefix.length) : '';
            var named = buttons.map(function (button) { return button.getAttribute('data-file-tab'); })
                .filter(function (name) { return fromHash === name || fromHash.indexOf(name + '-') === 0; })[0];
            show(named || buttons[0].getAttribute('data-file-tab'), false);
        });
    }

    // ---- Correspondence: a message in a dialog -----------------------------------
    // Open message opens its row's dialog through the shell's dialog binding;
    // the first open fetches the message from the Inbox record's Content
    // handler, and the record's Reply, Reply all and Forward, where it offers
    // them, join the dialog's foot. A failure says so and the next open tries
    // again. Both listeners are on the document, so a lazily mounted or
    // swapped Files section needs no binding of its own.
    function messageSpinner() {
        var spinner = document.createElement('div');
        spinner.className = 'spinner';
        spinner.setAttribute('aria-hidden', 'true');
        return spinner;
    }
    document.addEventListener('pegasus:dialog-open', function (event) {
        var dialog = event.target;
        if (!(dialog instanceof Element) || !dialog.matches('[data-case-message-dialog]')) { return; }
        var state = dialog.dataset.caseMessageState;
        var body = dialog.querySelector('[data-case-message-body]');
        var url = dialog.getAttribute('data-case-message-url');
        if (state === 'loading' || state === 'loaded' || !body || !url) { return; }
        dialog.dataset.caseMessageState = 'loading';
        body.setAttribute('aria-busy', 'true');
        body.replaceChildren(messageSpinner());
        fetch(url, { credentials: 'same-origin', headers: { 'Accept': 'text/html' } })
            .then(function (response) {
                if (!response.ok || response.redirected || !(response.headers.get('Content-Type') || '').includes('text/html')) {
                    throw new Error('message: ' + response.status);
                }
                return response.text();
            })
            .then(function (html) {
                var fragment = new DOMParser().parseFromString(html, 'text/html');
                var content = fragment.querySelector('[data-message-content]');
                if (!content) { throw new Error('message: no content'); }
                body.replaceChildren(document.importNode(content, true));
                var actions = fragment.querySelector('[data-message-actions]');
                var foot = dialog.querySelector('[data-case-message-foot]');
                if (actions && foot) { foot.prepend(document.importNode(actions, true)); }
                body.removeAttribute('aria-busy');
                dialog.dataset.caseMessageState = 'loaded';
            })
            .catch(function () {
                var status = document.createElement('p');
                status.className = 'muted';
                status.setAttribute('role', 'status');
                status.textContent = 'Preview unavailable';
                body.replaceChildren(status);
                body.removeAttribute('aria-busy');
                delete dialog.dataset.caseMessageState;
            });
    });
    // Capture, ahead of the shell's dialog opener and the edit session's
    // unsaved-changes guard. A modified click on Open message stays a link
    // click (a new tab or window) rather than opening the dialog. A link to
    // the record (Open full message, Reply, Reply all, Forward) closes the
    // dialog first, so that question is not left behind this dialog's inert
    // backdrop.
    document.addEventListener('click', function (event) {
        var target = event.target instanceof Element ? event.target : null;
        if (!target) { return; }
        var modified = event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey;
        if (modified && target.closest('[data-correspondence] a[data-dialog-open]')) {
            event.stopPropagation();
            return;
        }
        var link = target.closest('[data-case-message-record]');
        if (!link || modified) { return; }
        var dialog = link.closest('[data-case-message-dialog]');
        if (dialog && typeof dialog.pegasusClose === 'function') { dialog.pegasusClose(); }
    }, true);
    bindFileTabs(document);
    (window.pegasusMountBinders = window.pegasusMountBinders || []).push(bindFileTabs);
})();

// --- Triage Case: Record finding's tickboxes --------------------------------------
// Reply with finding answers a completed Triage, so ticking it ticks Complete
// Triage and unticking Complete Triage clears it. The handler completes before
// it opens the composer either way.
(function () {
    'use strict';
    document.addEventListener('change', function (event) {
        var box = event.target instanceof Element ? event.target : null;
        var form = box ? box.closest('form') : null;
        if (!form) { return; }
        if (box.matches('[data-triage-tick-reply]') && box.checked) {
            var complete = form.querySelector('[data-triage-tick-complete]');
            if (complete) { complete.checked = true; }
        }
        if (box.matches('[data-triage-tick-complete]') && !box.checked) {
            var reply = form.querySelector('[data-triage-tick-reply]');
            if (reply) { reply.checked = false; }
        }
    });
})();
