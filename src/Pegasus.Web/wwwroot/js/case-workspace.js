// Case record behaviour (v26). Everything here is a convenience on top of
// markup that already works as plain forms and links; nothing creates a
// separate mutation path and no business rule lives in the browser.
//
// Blocks, in order:
//   frame       sticky measure, Scroll/Tabs, lazy section bodies, the
//               section nav, in-place actions (fetch + swap), the edit
//               session (dirty guard, heartbeat, expiry)
//   sections    the section-owned enhancements (damage clicker, valuation
//               calculator, estimate grid, report images, files, viewer)
//
// Every binder is root-scoped and idempotent so a body mounted later (a lazy
// section, an in-place swap) joins the same behaviour.

// --- frame ------------------------------------------------------------------
(function () {
    'use strict';

    var record = document.querySelector('[data-case-record]');
    if (!record) {
        return;
    }

    var block = record.querySelector('[data-sticky-block]');
    var nav = record.querySelector('[data-section-nav]');
    var main = document.getElementById('case-main');
    var layoutSwitch = record.querySelector('[data-case-layout-switch]');
    var layoutButtons = layoutSwitch
        ? Array.prototype.slice.call(layoutSwitch.querySelectorAll('[data-case-layout]'))
        : [];
    var layout = 'scroll';
    var activeKey = record.getAttribute('data-section-current') || 'overview';
    var fragmentPath = window.location.pathname.replace(/\/+$/, '') + '/Section';
    var dirty = false;
    var dirtyEditors = new Map();
    var activeEditor = null;
    var submitting = false;
    // One editor: the record's Save form (one Save, 23 September 2026). The
    // Repair Spec and the valuation calculator are controls of it.
    var editorLabels = {
        'case-edit-form': 'Case'
    };
    // Whether the unsaved Case changes include the Repair Spec's, which an
    // import must not overwrite. Cleared with the Case's draft.
    var estimateTouched = false;
    var pendingAnchor = null;
    var navigationVersion = 0;

    function links() {
        return Array.prototype.slice.call(nav.querySelectorAll('[data-section-link]'));
    }
    function sections() {
        return Array.prototype.slice.call(main.querySelectorAll('.record-section'));
    }
    function sectionFor(key) {
        return document.getElementById('section-' + key);
    }
    function linkFor(key) {
        return links().find(function (link) { return link.getAttribute('data-section-link') === key; });
    }
    // A host nested under another (Damage and Valuation inside Vehicle, v28
    // P26) has no link of its own: its parent's link speaks for it.
    function ownerKey(key) {
        var host = sectionFor(key);
        return host && host.getAttribute('data-section-parent') ? host.getAttribute('data-section-parent') : key;
    }

    // Keep anchors aligned when the ribbon changes height, including swaps.
    function measure() {
        record.style.setProperty('--sticky-h', block.offsetHeight + 'px');
    }
    function readingLine() {
        return block.getBoundingClientRect().bottom;
    }

    var layoutCookie = 'pegasus-case-layout';
    function readLayout() {
        return record.getAttribute('data-layout') === 'tabs' ? 'tabs' : 'scroll';
    }
    function saveLayout() {
        if (window.pegasusPreferences) {
            window.pegasusPreferences.write(layoutCookie, layout);
        }
    }

    function bindMounted(root) {
        (window.pegasusMountBinders || []).forEach(function (bind) { bind(root); });
    }

    // ---- Scroll / Tabs ----------------------------------------------------
    function applyScrollState() {
        nav.removeAttribute('role');
        links().forEach(function (link) {
            link.removeAttribute('role');
            link.removeAttribute('aria-selected');
            link.removeAttribute('aria-controls');
            link.removeAttribute('tabindex');
        });
        sections().forEach(function (host) {
            host.classList.remove('is-active');
            host.removeAttribute('role');
            host.removeAttribute('aria-labelledby');
        });
    }
    function applyTabState() {
        nav.setAttribute('role', 'tablist');
        links().forEach(function (link) {
            var key = link.getAttribute('data-section-link');
            var selected = key === activeKey;
            var ownedPanelIds = sections()
                .filter(function (host) { return ownerKey(host.getAttribute('data-section')) === key; })
                .map(function (host) { return host.id; });
            link.id = 'case-section-tab-' + key;
            link.setAttribute('role', 'tab');
            link.setAttribute('aria-controls', ownedPanelIds.join(' '));
            link.setAttribute('aria-selected', selected ? 'true' : 'false');
            link.setAttribute('tabindex', selected ? '0' : '-1');
            link.setAttribute('aria-current', selected ? 'true' : 'false');
        });
        sections().forEach(function (host) {
            var key = host.getAttribute('data-section');
            host.setAttribute('role', 'tabpanel');
            host.setAttribute('aria-labelledby', 'case-section-tab-' + ownerKey(key));
            host.classList.toggle('is-active', ownerKey(key) === activeKey);
        });
    }
    function selectTab(key) {
        navigationVersion += 1;
        pendingAnchor = null;
        key = ownerKey(key);
        if (!linkFor(key)) {
            return;
        }
        activeKey = key;
        updateSectionFields();
        applyTabState();
        main.querySelectorAll('[data-lazy]').forEach(function (placeholder) {
            if (ownerKey(placeholder.getAttribute('data-lazy')) === activeKey) {
                mount(placeholder, function () { applyTabState(); });
            }
        });
        window.scrollTo({ top: 0, behavior: 'auto' });
    }
    function setLayout(value, persist) {
        navigationVersion += 1;
        pendingAnchor = null;
        layout = value === 'tabs' ? 'tabs' : 'scroll';
        record.setAttribute('data-layout', layout);
        if (layoutSwitch) {
            layoutSwitch.hidden = false;
        }
        layoutButtons.forEach(function (button) {
            button.setAttribute('aria-pressed', button.getAttribute('data-case-layout') === layout ? 'true' : 'false');
        });
        if (layout === 'tabs') {
            applyTabState();
            selectTab(activeKey);
        } else {
            applyScrollState();
            mountApproaching();
            spy();
        }
        measure();
        if (persist) {
            saveLayout();
        }
    }

    // ---- lazy bodies --------------------------------------------------------
    function editLeaseToken() {
        var input = document.querySelector('input[name="editLeaseToken"]');
        return input ? input.value : '';
    }
    function mount(placeholder, then) {
        var key = placeholder.getAttribute('data-lazy');
        if (!key) {
            return;
        }
        var waiting = placeholder.pegasusLazyWaiting || (placeholder.pegasusLazyWaiting = []);
        if (then) {
            waiting.push(then);
        }
        var attempted = Number(placeholder.dataset.lazyAttemptedAt || 0);
        if (placeholder.dataset.lazyState === 'loading') {
            return;
        }
        if (placeholder.dataset.lazyState === 'failed' && Date.now() - attempted < 5000) {
            return;
        }
        placeholder.dataset.lazyState = 'loading';
        placeholder.dataset.lazyAttemptedAt = String(Date.now());
        var headers = { 'Accept': 'text/html' };
        var lease = editLeaseToken();
        var requestedMain = main;
        var requestedVersion = record.getAttribute('data-case-version');
        function stillCurrent() {
            return placeholder.isConnected && requestedMain === main && main.contains(placeholder)
                && record.getAttribute('data-case-version') === requestedVersion && editLeaseToken() === lease;
        }
        if (lease) {
            headers['X-Pegasus-Edit-Lease'] = lease;
        }
        // The Inspection view's bodies read the Inspection's own work (v29).
        var view = record.getAttribute('data-case-view');
        var fragmentUrl = fragmentPath + '?section=' + encodeURIComponent(key)
            + (view ? '&view=' + encodeURIComponent(view) : '');
        fetch(fragmentUrl, { credentials: 'same-origin', headers: headers })
            .then(function (response) {
                if (!response.ok || response.redirected || !(response.headers.get('Content-Type') || '').includes('text/html')) {
                    throw new Error('section ' + key + ': ' + response.status);
                }
                return response.text();
            })
            .then(function (html) {
                if (!stillCurrent()) {
                    waiting.length = 0;
                    delete placeholder.dataset.lazyState;
                    return;
                }
                var parsed = document.createElement('div');
                parsed.innerHTML = html;
                var host = parsed.querySelector('.record-section');
                if (!host || host.id !== 'section-' + key || host.getAttribute('data-section') !== key) {
                    throw new Error('section ' + key + ': invalid response');
                }
                placeholder.replaceWith(host);
                // Binders scan below their root, so the mounted section is
                // bound through its parent (every binder is idempotent).
                bindMounted(host.parentElement || host);
                measure();
                if (layout === 'tabs') {
                    applyTabState();
                } else {
                    spy();
                }
                waiting.splice(0, waiting.length).forEach(function (callback) { callback(host); });
                settlePendingAnchor();
            })
            .catch(function (error) {
                if (!stillCurrent()) {
                    waiting.length = 0;
                    delete placeholder.dataset.lazyState;
                    return;
                }
                placeholder.dataset.lazyState = 'failed';
                placeholder.textContent = 'This section could not be loaded.';
                waiting.length = 0;
                settlePendingAnchor();
                if (window.console && window.console.error) {
                    window.console.error('Case section failed to load: ' + key, error);
                }
            });
    }
    function mountApproaching() {
        if (layout !== 'scroll') {
            return;
        }
        var limit = window.innerHeight * 2.5;
        main.querySelectorAll('[data-lazy]').forEach(function (placeholder) {
            if (placeholder.getBoundingClientRect().top < limit) {
                mount(placeholder);
            }
        });
    }

    // ---- jumping and the scroll spy ----------------------------------------
    function lazyBefore(target) {
        return Array.prototype.filter.call(main.querySelectorAll('[data-lazy]'), function (placeholder) {
            return (placeholder.compareDocumentPosition(target) & Node.DOCUMENT_POSITION_FOLLOWING) !== 0
                && placeholder.dataset.lazyState !== 'failed';
        });
    }
    function settlePendingAnchor() {
        if (!pendingAnchor || layout !== 'scroll') {
            return;
        }
        var target = sectionFor(pendingAnchor.key);
        if (!target || target.dataset.lazyState === 'failed') {
            pendingAnchor = null;
            return;
        }
        if (pendingAnchor.predecessors.some(function (placeholder) {
            return placeholder.isConnected && placeholder.dataset.lazyState !== 'failed';
        })) {
            return;
        }
        scrollSectionIntoView(target);
        pendingAnchor = null;
    }
    function scrollSectionIntoView(host) {
        measure();
        var top = window.scrollY + host.getBoundingClientRect().top - readingLine() - 8;
        window.scrollTo({ top: Math.max(0, top), behavior: 'auto' });
    }
    function focusSection(host) {
        var heading = host.querySelector('h2');
        if (!heading) {
            return;
        }
        heading.setAttribute('tabindex', '-1');
        try { heading.focus({ preventScroll: true }); } catch (_) { heading.focus(); }
    }
    function jumpTo(key, focus) {
        var navigation = ++navigationVersion;
        if (layout === 'tabs') {
            selectTab(key);
            return;
        }
        var target = sectionFor(key);
        if (!target) {
            return;
        }
        var predecessors = lazyBefore(target);
        pendingAnchor = predecessors.length ? { key: key, predecessors: predecessors } : null;
        if (target.hasAttribute('data-lazy')) {
            mount(target, function (host) {
                if (navigation !== navigationVersion || layout !== 'scroll') { return; }
                scrollSectionIntoView(host);
                mountApproaching();
                if (focus) { focusSection(host); }
            });
            return;
        }
        scrollSectionIntoView(target);
        mountApproaching();
        if (focus) { focusSection(target); }
    }
    function spy() {
        if (layout !== 'scroll') {
            return;
        }
        var hosts = sections();
        if (!hosts.length) {
            return;
        }
        var line = readingLine() + 16;
        var current = ownerKey(hosts[0].getAttribute('data-section'));
        hosts.forEach(function (host) {
            if (host.getBoundingClientRect().top <= line) {
                current = ownerKey(host.getAttribute('data-section'));
            }
        });
        if (window.innerHeight + window.scrollY >= document.documentElement.scrollHeight - 40) {
            current = ownerKey(hosts[hosts.length - 1].getAttribute('data-section'));
        }
        activeKey = current;
        links().forEach(function (link) {
            link.setAttribute('aria-current', link.getAttribute('data-section-link') === current ? 'true' : 'false');
        });
        updateSectionFields();
    }
    function updateSectionFields() {
        // The refresh partial's replay input carries the generic hook; the
        // Case's own forms carry the Case-named one.
        record.querySelectorAll('[data-case-section-field], [data-refresh-field="section"]').forEach(function (field) { field.value = activeKey; });
    }

    nav.addEventListener('click', function (event) {
        var link = event.target.closest('[data-section-link]');
        if (!link) {
            return;
        }
        event.preventDefault();
        jumpTo(link.getAttribute('data-section-link'), true);
    });
    nav.addEventListener('keydown', function (event) {
        if (layout !== 'tabs') {
            return;
        }
        var link = event.target.closest('[data-section-link]');
        if (!link) {
            return;
        }
        var all = links();
        var index = all.indexOf(link);
        var nextIndex;
        if (event.key === 'ArrowRight') { nextIndex = index + 1; }
        else if (event.key === 'ArrowLeft') { nextIndex = index - 1; }
        else if (event.key === 'Home') { nextIndex = 0; }
        else if (event.key === 'End') { nextIndex = all.length - 1; }
        else { return; }
        event.preventDefault();
        if (nextIndex < 0) { nextIndex = all.length - 1; }
        if (nextIndex >= all.length) { nextIndex = 0; }
        all[nextIndex].focus();
        selectTab(all[nextIndex].getAttribute('data-section-link'));
    });
    document.addEventListener('click', function (event) {
        // An empty jump (the Inspection view's Next action) is an ordinary link.
        var jump = event.target.closest('[data-section-jump]:not([data-section-jump=""])');
        if (!jump || !record.contains(jump)) {
            return;
        }
        event.preventDefault();
        jumpTo(jump.getAttribute('data-section-jump'), true);
    });
    layoutButtons.forEach(function (button) {
        button.addEventListener('click', function () { setLayout(button.getAttribute('data-case-layout'), true); });
    });

    var ticking = false;
    window.addEventListener('scroll', function () {
        if (ticking) {
            return;
        }
        ticking = true;
        window.setTimeout(function () {
            ticking = false;
            if (layout !== 'scroll') {
                return;
            }
            mountApproaching();
            spy();
        }, 80);
    }, { passive: true });
    ['wheel', 'touchstart', 'pointerdown', 'keydown'].forEach(function (name) {
        window.addEventListener(name, function () { pendingAnchor = null; navigationVersion += 1; }, { passive: true });
    });
    window.addEventListener('resize', function () {
        pendingAnchor = null;
        measure();
        if (layout === 'scroll') {
            spy();
        }
    });
    if ('ResizeObserver' in window) {
        new ResizeObserver(measure).observe(block);
    }

    // ---- the edit session: dirty guard, heartbeat, expiry ------------------
    function setDirty(isDirty) {
        dirty = isDirty;
        if (!isDirty) { estimateTouched = false; }
    }
    function estimateIsDirty() { return estimateTouched && dirtyEditors.has('case-edit-form'); }
    function editorFor(control) {
        var form = control.form || (control.closest ? control.closest('form') : null);
        return form && editorLabels[form.getAttribute('id')] ? form : null;
    }
    function markDirty(form) {
        var id = form.getAttribute('id');
        dirtyEditors.set(id, (dirtyEditors.get(id) || 0) + 1);
        activeEditor = id;
        setDirty(true);
    }
    ['input', 'change'].forEach(function (name) {
        document.addEventListener(name, function (event) {
            var form = editorFor(event.target);
            if (!form) { return; }
            markDirty(form);
            if (event.target.closest && event.target.closest('[data-estimate-form]')) { estimateTouched = true; }
        });
    });
    document.addEventListener('focusin', function (event) {
        var form = editorFor(event.target);
        if (form) { activeEditor = form.getAttribute('id'); }
    });
    function activeDirtyForm() {
        var id = dirtyEditors.has(activeEditor) ? activeEditor : dirtyEditors.keys().next().value;
        return id ? document.getElementById(id) : null;
    }
    window.pegasusDirtyEditForm = activeDirtyForm;
    window.addEventListener('beforeunload', function (event) {
        if (!dirty) { return; }
        event.preventDefault();
        event.returnValue = '';
    });

    var heartbeat = null;
    var heartbeatGeneration = 0;
    var heartbeatOnVisible = null;
    function stopHeartbeat() {
        heartbeatGeneration += 1;
        if (heartbeat) {
            window.clearInterval(heartbeat);
            heartbeat = null;
        }
        if (heartbeatOnVisible) {
            document.removeEventListener('visibilitychange', heartbeatOnVisible);
            heartbeatOnVisible = null;
        }
    }
    function bindHeartbeat() {
        stopHeartbeat();
        var form = record.querySelector('[data-case-heartbeat]');
        var renew = record.querySelector('[data-case-renew]');
        var line = record.querySelector('[data-lease-line]');
        if (!form) {
            return;
        }
        var seconds = parseInt(form.getAttribute('data-heartbeat-seconds'), 10);
        if (!(seconds > 0)) {
            return;
        }
        if (renew) {
            renew.hidden = true;
        }
        var generation = heartbeatGeneration;
        function expired() {
            stopHeartbeat();
            if (line) {
                line.textContent = line.getAttribute('data-lease-expired-text') || '';
                line.classList.add('is-expiring');
                line.hidden = false;
            }
            if (renew) {
                renew.hidden = false;
            }
        }
        function beat() {
            if (!heartbeat || generation !== heartbeatGeneration) {
                return;
            }
            fetch(form.getAttribute('action') || window.location.href, {
                method: 'POST',
                body: new FormData(form),
                credentials: 'same-origin'
            }).then(function (response) {
                if (generation !== heartbeatGeneration || response.status === 204) {
                    return;
                }
                // 409 and 403 are the server refusing the lease itself. Anything
                // else - a faulted request, a replica restarting - says nothing
                // about the lease, and the next beat settles it.
                if (response.status === 409 || response.status === 403) {
                    expired();
                }
            }).catch(function () {
                // One failed beat is not a lost lease; the next beat settles it.
            });
        }
        heartbeat = window.setInterval(beat, seconds * 1000);
        heartbeatOnVisible = function () {
            if (!document.hidden) {
                beat();
            }
        };
        document.addEventListener('visibilitychange', heartbeatOnVisible);
    }

    // ---- in-place actions: every form in the record posts by fetch and the
    //      record's parts are swapped for the response's (v25 decision 3) ----
    var confirmDialog = document.getElementById('edit-finish-confirm');
    var confirmResolve = null;
    var confirmInvoker = null;
    function askUnsaved() {
        if (!confirmDialog) {
            return Promise.resolve('keep');
        }
        return new Promise(function (resolve) {
            confirmResolve = resolve;
            confirmInvoker = document.activeElement;
            var form = activeDirtyForm();
            var label = form ? editorLabels[form.getAttribute('id')] : 'Case';
            confirmDialog.querySelector('h2').textContent = 'Unsaved ' + Array.from(dirtyEditors.keys()).map(function (id) { return editorLabels[id]; }).join(', ') + ' changes';
            confirmDialog.querySelector('[data-edit-finish-save]').textContent = 'Save ' + label;
            confirmDialog.hidden = false;
            var keep = confirmDialog.querySelector('[data-edit-finish-keep]');
            if (keep) { keep.focus(); }
        });
    }
    function settleUnsaved(answer) {
        if (!confirmDialog || !confirmResolve) {
            return;
        }
        confirmDialog.hidden = true;
        var resolve = confirmResolve;
        confirmResolve = null;
        if (confirmInvoker && confirmInvoker.isConnected) { confirmInvoker.focus(); }
        resolve(answer);
    }
    if (confirmDialog) {
        confirmDialog.querySelector('[data-edit-finish-keep]').addEventListener('click', function () { settleUnsaved('keep'); });
        confirmDialog.querySelector('[data-edit-finish-discard]').addEventListener('click', function () { settleUnsaved('discard'); });
        confirmDialog.querySelector('[data-edit-finish-save]').addEventListener('click', function () { settleUnsaved('save'); });
        confirmDialog.addEventListener('keydown', function (event) {
            if (event.key === 'Escape') { event.preventDefault(); settleUnsaved('keep'); }
            if (event.key === 'Tab') {
                var buttons = confirmDialog.querySelectorAll('button');
                if (event.shiftKey && document.activeElement === buttons[0]) { event.preventDefault(); buttons[buttons.length - 1].focus(); }
                else if (!event.shiftKey && document.activeElement === buttons[buttons.length - 1]) { event.preventDefault(); buttons[0].focus(); }
            }
        });
    }

    function anchor() {
        var line = readingLine() + 8;
        var best = null;
        sections().forEach(function (host) {
            var rect = host.getBoundingClientRect();
            if (rect.top <= line && rect.bottom > line) {
                best = { key: host.getAttribute('data-section'), top: rect.top };
            }
        });
        return best;
    }
    function keep(saved) {
        if (!saved) {
            return;
        }
        var host = sectionFor(saved.key);
        if (!host) {
            return;
        }
        // Instant, never 'auto': the page's smooth scroll-behavior would paint
        // the swapped page at the old offset first and then glide back — the
        // jump to the top and back on Edit. This lands before the first paint.
        window.scrollBy({ top: host.getBoundingClientRect().top - saved.top, behavior: 'instant' });
    }

    var swapRoots = ['[data-case-notices]', '[data-case-ribbon-facts]', '[data-case-ribbon-actions]', '[data-case-stale]', '#case-main', '[data-case-aside]', '[data-case-dialogs]', '[data-case-viewer-host]'];
    function swap(html, command, preferred) {
        var parsed = new DOMParser().parseFromString(html, 'text/html');
        var incoming = parsed.querySelector('[data-case-record]');
        if (!incoming) {
            return false;
        }
        var commit = null;
        try { commit = JSON.parse(incoming.getAttribute('data-editor-commit') || 'null'); } catch (_) { /* An unrecognised response cannot clear a draft. */ }
        var confirmed = command && commit && commit.editor === command.editor
            && commit.operationKey === command.operationKey && String(commit.expectedVersion) === command.expectedVersion;
        var mayAdvance = confirmed && String(commit.version) === incoming.getAttribute('data-case-version')
            && incoming.getAttribute('data-case-editing') === 'true';
        if (confirmed && dirtyEditors.get(command.editor) === command.revision) {
            dirtyEditors.delete(command.editor);
        }
        // Keep the live controls (including grid rows, form-associated fields and
        // preparation state) rather than reconstructing drafts from fresh HTML.
        var retainedSections = new Set();
        dirtyEditors.forEach(function (_, id) {
            var form = document.getElementById(id);
            if (!form) { return; }
            var nextForm = parsed.getElementById(id);
            var oldVersion = form.querySelector('[name="expectedVersion"]');
            if (mayAdvance && nextForm && oldVersion && oldVersion.value === command.expectedVersion) {
                var authorityFields = ['expectedVersion', 'editLeaseToken'];
                if (id === command.editor) { authorityFields.push('operationKey'); }
                authorityFields.forEach(function (name) {
                    var current = form.querySelector('[name="' + name + '"]');
                    var next = nextForm.querySelector('[name="' + name + '"]');
                    if (current && next) { current.value = next.value; }
                });
            }
            [form].concat(Array.from(form.elements)).forEach(function (control) {
                var host = control.closest('.record-section');
                if (host) { retainedSections.add(host); }
            });
            if (form.__pegasusPreparationStaged) {
                main.querySelectorAll('[data-preparation-card]').forEach(function (card) {
                    var host = card.closest('.record-section');
                    if (host) { retainedSections.add(host); }
                });
            }
        });
        // A refusal or unknown outcome must not replace any draft or its original
        // authority. Server notices can still explain the failed command.
        var noticesOnly = dirtyEditors.size > 0 && !confirmed;
        // The section a head Edit was pressed on keeps its place; any other
        // swap keeps the section at the reading line.
        var saved = preferred || anchor();
        var collapsed = {};
        sections().forEach(function (host) { collapsed[host.getAttribute('data-section')] = host.classList.contains('is-collapsed'); });
        if (!noticesOnly) {
            retainedSections.forEach(function (host) {
                var next = parsed.getElementById(host.id);
                if (mayAdvance) {
                    var newLease = incoming.querySelector('[name="editLeaseToken"]');
                    host.querySelectorAll('form').forEach(function (form) {
                        var version = form.querySelector('[name="expectedVersion"]');
                        var lease = form.querySelector('[name="editLeaseToken"]');
                        if (lease && newLease && lease.value === command.editLeaseToken
                            && (!version || version.value === command.expectedVersion)) {
                            lease.value = newLease.value;
                            if (version) { version.value = String(commit.version); }
                        }
                    });
                }
                if (next) { next.replaceWith(host); }
            });
        }
        swapRoots.forEach(function (selector) {
            if (noticesOnly && selector !== '[data-case-notices]' && selector !== '[data-case-stale]') { return; }
            if (selector === '[data-case-ribbon-actions]' && dirtyEditors.size > 0 && !mayAdvance) { return; }
            var current = document.querySelector(selector);
            var next = parsed.querySelector(selector);
            if (!current || !next) {
                return;
            }
            current.querySelectorAll('[data-dialog]:not([hidden])').forEach(function (dialog) {
                if (typeof dialog.pegasusClose === 'function') {
                    dialog.pegasusClose();
                }
            });
            current.replaceWith(next);
        });
        (noticesOnly ? [] : ['class', 'data-case-version', 'data-case-editing', 'data-section-current', 'data-case-view']).forEach(function (name) {
            var value = incoming.getAttribute(name);
            if (value === null) { record.removeAttribute(name); } else { record.setAttribute(name, value); }
        });
        record.setAttribute('data-layout', layout);
        main = document.getElementById('case-main');
        // Dialogs first so the openers in the swapped roots find them.
        ['[data-case-dialogs]', '[data-case-viewer-host]', '[data-case-notices]', '[data-case-ribbon-facts]', '[data-case-ribbon-actions]', '[data-case-stale]', '#case-main', '[data-case-aside]'].forEach(function (selector) {
            var root = document.querySelector(selector);
            if (root) { bindMounted(root); }
        });
        if (dirtyEditors.size > 0) {
            record.classList.add('is-editing');
            record.setAttribute('data-case-editing', 'true');
        }
        if (layout === 'tabs') {
            applyTabState();
            var selected = sectionFor(activeKey);
            if (selected && selected.hasAttribute('data-lazy')) { mount(selected, applyTabState); }
        } else { applyScrollState(); }
        updateSectionFields();
        measure();
        keep(saved);
        setDirty(dirtyEditors.size > 0);
        if (confirmed && dirtyEditors.size > 0 && !mayAdvance) {
            showActionError('The save completed, but the Case changed again or editing expired. Your other unsaved changes still use their original version.');
        }
        bindHeartbeat();
        mountApproaching();
        spy();
        var confirmation = document.querySelector('[data-case-notices] [data-confirmation]');
        if (confirmation && typeof window.pegasusToast === 'function') {
            var text = confirmation.querySelector('span');
            if (text) { window.pegasusToast(text.textContent.trim()); }
        }
        // Only a refusal the server rendered into the swapped-in notices;
        // showActionError has already toasted its own [data-inplace-error].
        var alertNotice = document.querySelector('[data-case-notices] [role="alert"]:not([data-inplace-error])');
        if (alertNotice && typeof window.pegasusToast === 'function') {
            var alertText = alertNotice.textContent.trim();
            if (alertText) { window.pegasusToast(alertText, 'danger'); }
        }
        document.dispatchEvent(new CustomEvent('pegasus:case-swapped'));
        return true;
    }

    function samePage(url) {
        try {
            var target = new URL(url, window.location.href);
            return target.pathname.replace(/\/+$/, '') === window.location.pathname.replace(/\/+$/, '');
        } catch (_) {
            return false;
        }
    }
    function submitInPlace(form, submitter) {
        // A section-head Edit keeps its own section where it is on screen.
        var editKey = submitter ? submitter.getAttribute('data-section-edit') : null;
        var editHost = editKey ? sectionFor(editKey) : null;
        var preferred = editHost ? { key: editKey, top: editHost.getBoundingClientRect().top } : null;
        var body = new FormData(form, submitter && submitter.name ? submitter : undefined);
        var isImport = form.hasAttribute('data-estimate-import-form');
        var command = editorLabels[form.getAttribute('id')] || isImport ? {
            editor: form.getAttribute('id'), operationKey: body.get('operationKey'),
            expectedVersion: body.get('expectedVersion'), editLeaseToken: body.get('editLeaseToken'),
            revision: dirtyEditors.get(form.getAttribute('id'))
        } : null;
        var action = (submitter && submitter.getAttribute('formaction')) || form.getAttribute('action') || window.location.href;
        var method = ((submitter && submitter.getAttribute('formmethod')) || form.getAttribute('method') || 'get').toUpperCase();
        var request = { method: method, credentials: 'same-origin', redirect: 'follow', headers: { 'X-Requested-With': 'fetch', 'Accept': 'text/html' } };
        if (method === 'GET') {
            var url = new URL(action, window.location.href);
            new URLSearchParams(body).forEach(function (value, key) { url.searchParams.set(key, value); });
            action = url.toString();
        } else {
            request.body = body;
        }
        form.setAttribute('aria-busy', 'true');
        var importSection = isImport ? form.closest('[data-estimate-drop-target]') : null;
        var importStatus = isImport ? form.querySelector('[data-estimate-import-status]') : null;
        if (isImport && importSection) {
            importSection.setAttribute('data-estimate-importing', 'true');
            importSection.classList.add('is-import-unavailable');
            if (importStatus) {
                importStatus.hidden = false;
                importStatus.textContent = importStatus.dataset.importingText || 'Importing estimate…';
            }
        }
        return fetch(action, request).then(function (response) {
            var landed = response.url || action;
            if (!samePage(landed)) {
                if (dirty) { throw new Error('The action left the Case before its result was confirmed.'); }
                window.location.assign(landed);
                return null;
            }
            if (response.status === 403 || response.status === 404) {
                throw new Error('The action is unavailable or no longer permitted.');
            }
            if (!response.ok) { throw new Error('The server could not confirm the action.'); }
            return response.text();
        }).then(function (html) {
            if (html === null) {
                return;
            }
            if (!swap(html, command, preferred)) {
                throw new Error('The server did not return the Case.');
            }
        }).catch(function (error) {
            // A failed save runs nothing after it.
            afterSave = null;
            var failure = isImport
                ? error.message + ' Import completion was not confirmed. Your unsaved changes are still here; reload the Case before retrying. If the source was already stored, it will be reused.'
                : error.message + ' Your unsaved changes are still here.';
            showActionError(failure);
        }).finally(function () {
            form.removeAttribute('aria-busy');
            form.removeAttribute('data-inplace-submitting');
            if (importSection && importSection.isConnected) {
                importSection.removeAttribute('data-estimate-importing');
                importSection.classList.remove('is-import-unavailable');
            }
            if (importStatus && importStatus.isConnected) {
                importStatus.hidden = true;
                importStatus.textContent = '';
            }
            submitting = false;
        });
    }
    function showActionError(message) {
        var notices = document.querySelector('[data-case-notices]');
        if (!notices) { return; }
        var error = notices.querySelector('[data-inplace-error]');
        if (!error) {
            error = document.createElement('p');
            error.setAttribute('data-inplace-error', '');
            error.setAttribute('role', 'alert');
            notices.appendChild(error);
        }
        error.textContent = message;
        if (typeof window.pegasusToast === 'function') {
            window.pegasusToast(message, 'danger');
        }
    }

    var estimateDragResetters = new WeakMap();
    function bindEstimateImport(root) {
        if (!window.pegasusEstimateImportGlobalBound) {
            window.pegasusEstimateImportGlobalBound = true;
            var clearEstimateDragStates = function () {
                document.querySelectorAll('[data-estimate-drop-target][data-estimate-dragging="true"]').forEach(function (section) {
                    var clearDrag = estimateDragResetters.get(section);
                    if (clearDrag) { clearDrag(); }
                });
            };
            var preventOutsideFileNavigation = function (event) {
                var transfer = event.dataTransfer;
                if (!transfer || Array.prototype.slice.call(transfer.types || []).indexOf('Files') < 0) { return; }
                var target = event.target;
                if (target && target.nodeType !== 1) { target = target.parentElement; }
                if (target && target.closest && target.closest('[data-estimate-drop-target]')) { return; }
                event.preventDefault();
            };
            window.addEventListener('dragend', clearEstimateDragStates, true);
            window.addEventListener('blur', clearEstimateDragStates);
            window.addEventListener('dragover', preventOutsideFileNavigation, true);
            window.addEventListener('drop', preventOutsideFileNavigation, true);
        }
        var sections = [];
        if (root.matches && root.matches('[data-estimate-drop-target]')) { sections.push(root); }
        sections = sections.concat(Array.prototype.slice.call(root.querySelectorAll('[data-estimate-drop-target]')));
        sections.forEach(function (section) {
            if (section.dataset.estimateImportBound === 'true') { return; }
            var form = section.querySelector('form[data-estimate-import-form]');
            var input = form && form.querySelector('input[type="file"][name="estimateFile"]');
            var fallback = form && form.querySelector('[data-estimate-import-fallback]');
            var picker = form && form.querySelector('[data-estimate-import-picker]');
            var overlay = section.querySelector('[data-estimate-import-overlay]');
            if (!form || !input || !fallback || !picker || !overlay) { return; }

            section.dataset.estimateImportBound = 'true';
            fallback.hidden = true;
            picker.hidden = false;
            var depth = 0;
            var maxBytes = Number(form.dataset.estimateImportMaxBytes);
            var validExtensions = String(form.dataset.estimateImportExtensions || '')
                .toLowerCase().split(',').filter(Boolean);
            var importMessage = function (name, fallback) {
                return form.dataset['estimateImport' + name] || fallback;
            };
            var isFileDrag = function (event) {
                return Boolean(event.dataTransfer)
                    && Array.prototype.slice.call(event.dataTransfer.types || []).indexOf('Files') >= 0;
            };
            function canAccept() {
                return !submitting && !confirmResolve && form.dataset.inplaceSubmitting !== 'true';
            }
            function clearDrag() {
                depth = 0;
                section.classList.remove('is-dragover', 'is-import-unavailable');
                section.removeAttribute('data-estimate-dragging');
                overlay.hidden = true;
                overlay.setAttribute('aria-hidden', 'true');
            }
            estimateDragResetters.set(section, clearDrag);
            function showDrag(unavailable) {
                section.classList.add('is-dragover');
                section.classList.toggle('is-import-unavailable', unavailable);
                section.setAttribute('data-estimate-dragging', 'true');
                overlay.hidden = false;
                overlay.setAttribute('aria-hidden', 'false');
            }
            function validate(files) {
                if (!files || files.length !== 1) {
                    return importMessage('OneFile', 'Choose exactly one estimate file.');
                }
                var file = files[0];
                if (!file || file.size <= 0) { return importMessage('NonEmpty', 'Choose a non-empty estimate file.'); }
                if (file.size > maxBytes) { return importMessage('TooLarge', 'Choose an estimate file within the size limit.'); }
                var name = String(file.name || '').toLowerCase();
                if (!validExtensions.some(function (extension) { return name.endsWith(extension); })) {
                    return importMessage('Unsupported', 'Choose a PDF, XML or JSON estimate file.');
                }
                return null;
            }
            function submitSelectedFile() {
                var files = input.files ? Array.prototype.slice.call(input.files) : [];
                var error = validate(files);
                if (error) {
                    input.value = '';
                    showActionError(error);
                    return;
                }
                if (estimateIsDirty()) {
                    input.value = '';
                    showActionError(importMessage('Dirty', 'Save or cancel the estimate changes before importing another estimate.'));
                    return;
                }
                if (!canAccept()) {
                    input.value = '';
                    showActionError(importMessage('Busy', 'Wait for the current Case action to finish before importing an estimate.'));
                    return;
                }
                form.requestSubmit();
            }

            picker.addEventListener('click', function () {
                if (!canAccept()) { return; }
                if (estimateIsDirty()) {
                    showActionError(importMessage('Dirty', 'Save or cancel the estimate changes before importing another estimate.'));
                    return;
                }
                input.click();
            });
            input.addEventListener('change', submitSelectedFile);
            section.addEventListener('dragenter', function (event) {
                if (!isFileDrag(event)) { return; }
                event.preventDefault();
                depth += 1;
                showDrag(!canAccept() || estimateIsDirty());
            });
            section.addEventListener('dragover', function (event) {
                if (!isFileDrag(event)) { return; }
                event.preventDefault();
                event.dataTransfer.dropEffect = canAccept() && !estimateIsDirty() ? 'copy' : 'none';
                showDrag(!canAccept() || estimateIsDirty());
            });
            section.addEventListener('dragleave', function (event) {
                depth = Math.max(0, depth - 1);
                if (depth === 0) { clearDrag(); }
            });
            section.addEventListener('dragend', clearDrag);
            section.addEventListener('drop', function (event) {
                var fileDrag = isFileDrag(event);
                clearDrag();
                if (!fileDrag) { return; }
                event.preventDefault();
                event.stopPropagation();
                var files = event.dataTransfer && event.dataTransfer.files
                    ? Array.prototype.slice.call(event.dataTransfer.files) : [];
                var error = validate(files);
                if (error) { showActionError(error); return; }
                if (estimateIsDirty()) {
                    showActionError(importMessage('Dirty', 'Save or cancel the estimate changes before importing another estimate.'));
                    return;
                }
                if (!canAccept()) {
                    showActionError(importMessage('Busy', 'Wait for the current Case action to finish before importing an estimate.'));
                    return;
                }
                input.files = event.dataTransfer.files;
                input.dispatchEvent(new Event('change', { bubbles: true }));
            });
        });
    }
    (window.pegasusMountBinders = window.pegasusMountBinders || []).push(bindEstimateImport);

    function inPlace(form) {
        if (form.hasAttribute('target') || form.hasAttribute('data-no-inplace') || form.hasAttribute('data-case-heartbeat')) {
            return false;
        }
        if (form.hasAttribute('data-glass-window')) {
            return false;
        }
        // A write lands on the default view (v29), so one posted from the
        // Inspection view navigates rather than swapping that view in place.
        // Razor renders the attribute empty on every other Case, so its value
        // decides, not its presence.
        if (record.getAttribute('data-case-view') && (form.getAttribute('method') || 'get').toLowerCase() === 'post') {
            return false;
        }
        var dialogs = document.querySelector('[data-case-dialogs]');
        return record.contains(form) || (dialogs && dialogs.contains(form));
    }

    document.addEventListener('submit', function (event) {
        var form = event.target;
        if (!(form instanceof HTMLFormElement) || !inPlace(form)) {
            return;
        }
        var submitter = event.submitter;
        if (submitter && (submitter.hasAttribute('formtarget') || submitter.hasAttribute('data-no-inplace'))) {
            return;
        }
        event.preventDefault();
        var isImport = form.hasAttribute('data-estimate-import-form');
        if (submitting || confirmResolve || form.dataset.inplaceSubmitting === 'true') { return; }
        if (isImport && estimateIsDirty()) {
            showActionError(form.dataset.estimateImportDirty
                || 'Save or cancel the estimate changes before importing another estimate.');
            return;
        }
        var isSave = !!editorLabels[form.getAttribute('id')];
        var proceed = function () {
            submitting = true;
            if (isSave && !dirtyEditors.has(form.getAttribute('id'))) { markDirty(form); }
            form.dataset.inplaceSubmitting = 'true';
            submitInPlace(form, submitter);
        };
        // A command that reads what the Case records (Apply and Remove scaling
        // work on the saved repair spec) saves the Case's own unsaved changes
        // first and follows in the same press. Its section renders afresh
        // after the save, so it reads the spec as saved; its choices ride
        // across (again()).
        var caseForm = document.getElementById('case-edit-form');
        if (form.hasAttribute('data-case-save-first') && caseForm && dirtyEditors.has('case-edit-form')) {
            saveThen(caseForm, again(form, submitter));
            return;
        }
        // Cancel is the operator discarding: it needs no second question.
        var isCancel = form.hasAttribute('data-case-cancel-form');
        if (!isSave && dirty && !isCancel) {
            askUnsaved().then(function (answer) {
                if (answer === 'keep') {
                    return;
                }
                if (answer === 'save') {
                    // Saving carries on into what was asked for.
                    var save = activeDirtyForm();
                    if (save) { saveThen(save, again(form, submitter)); }
                    return;
                }
                dirtyEditors.clear();
                setDirty(false);
                proceed();
            });
            return;
        }
        if (isCancel) {
            dirtyEditors.clear();
            setDirty(false);
        }
        proceed();
    });

    // The frame owns this shortcut even inside a field; site.js handles it on
    // other pages. It saves the Case and keeps editing (no finishEditing).
    document.addEventListener('keydown', function (event) {
        if (!(event.ctrlKey || event.metaKey) || event.key.toLowerCase() !== 's') { return; }
        event.preventDefault();
        event.stopImmediatePropagation();
        if (submitting || confirmResolve) { return; }
        var form = activeDirtyForm();
        if (form) { form.requestSubmit(); }
    }, true);
    document.addEventListener('click', function (event) {
        var link = event.target.closest('a[href]');
        if (!dirty || !link || event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey
            || link.hasAttribute('target') || link.hasAttribute('download') || link.hasAttribute('data-section-link')
            || link.getAttribute('data-section-jump') || link.hasAttribute('data-evidence-item')
            || link.getAttribute('href').startsWith('#')) { return; }
        event.preventDefault();
        if (submitting || confirmResolve) { return; }
        askUnsaved().then(function (answer) {
            if (answer === 'save') {
                var form = activeDirtyForm();
                if (form) {
                    saveThen(form, function () { window.location.assign(link.href); });
                }
            } else if (answer === 'discard') {
                dirtyEditors.clear();
                setDirty(false);
                window.location.assign(link.href);
            }
        });
    });

    // ---- save, then carry on: the unsaved-changes question's Save (and Apply
    //      over unsaved Case changes) runs what was asked for once the save
    //      has landed, rather than dropping it ------------------------------
    var afterSave = null;
    function saveThen(save, next) {
        afterSave = { editor: save.getAttribute('id'), next: next };
        save.requestSubmit();
    }
    // The action's form is found again after the save's swap, which renders
    // it afresh with the Case's new version and a new operation key, and the
    // operator's choices in it are put back before it is sent.
    function again(form, submitter) {
        var id = form.getAttribute('id');
        var action = form.getAttribute('action');
        var choices = visibleChoices(form);
        var name = submitter ? submitter.name : '';
        var value = submitter ? submitter.value : '';
        var formaction = submitter ? submitter.getAttribute('formaction') : null;
        return function () {
            var next = id ? document.getElementById(id) : Array.prototype.find.call(
                record.querySelectorAll('form[action]'),
                function (candidate) { return candidate.getAttribute('action') === action; });
            if (!next) { return; }
            restoreChoices(next, choices);
            var button = submitter ? Array.prototype.find.call(next.elements, function (element) {
                return element.type === 'submit' && element.name === name && element.value === value
                    && element.getAttribute('formaction') === formaction;
            }) : null;
            next.requestSubmit(button || null);
        };
    }
    // The operator's own choices in a form (never its hidden authority
    // fields), and putting them back into its freshly rendered copy.
    function visibleChoices(form) {
        return Array.prototype.filter.call(form.elements, function (element) {
            return element.name && element.type !== 'hidden' && element.type !== 'submit' && element.type !== 'button';
        }).map(function (element) {
            return { name: element.name, type: element.type, value: element.value, checked: element.checked };
        });
    }
    function restoreChoices(form, choices) {
        var seen = {};
        Array.prototype.forEach.call(form.elements, function (element) {
            if (!element.name || element.type === 'hidden' || element.type === 'submit' || element.type === 'button') { return; }
            var same = choices.filter(function (choice) { return choice.name === element.name; });
            if (element.type === 'radio' || element.type === 'checkbox') {
                element.checked = same.some(function (choice) { return choice.value === element.value && choice.checked; });
                if (element.checked && element.type === 'radio') {
                    element.dispatchEvent(new Event('change', { bubbles: true }));
                }
                return;
            }
            var at = seen[element.name] || 0;
            seen[element.name] = at + 1;
            if (same[at]) { element.value = same[at].value; }
        });
    }
    document.addEventListener('pegasus:case-swapped', function () {
        var pending = afterSave;
        afterSave = null;
        // A save the server refused leaves its editor unsaved: nothing follows.
        if (!pending || dirtyEditors.has(pending.editor)) { return; }
        // After the save's own submission has finished.
        window.setTimeout(pending.next, 0);
    });

    // ---- the section-head Edit posts the ribbon's claim and remembers the
    //      section so the swapped page keeps the reader where they were ------
    document.addEventListener('click', function (event) {
        var edit = event.target.closest('[data-section-edit]');
        if (!edit || !record.contains(edit)) {
            return;
        }
        var key = edit.getAttribute('data-section-edit');
        var form = edit.closest('form');
        var field = form && form.querySelector('input[name="section"]');
        if (field) {
            field.value = key;
        }
    });

    // ---- init ------------------------------------------------------------------
    layout = readLayout();
    measure();
    bindEstimateImport(record);
    setLayout(layout, false);
    bindHeartbeat();
    var addressed = new URLSearchParams(window.location.search).get('section');
    if (addressed && layout === 'scroll' && addressed.trim().toLowerCase() !== 'overview') {
        jumpTo(addressed.trim().toLowerCase(), false);
    }
    var editingNow = record.getAttribute('data-case-editing') === 'true';
    if (editingNow && window.location.hash === '') {
        // Landing in an edit session after a full-POST fallback: nothing to move.
    }
})();


// --- retained helpers: report recipients, the Glass's window and its return ---
(function () {
    'use strict';
    function bindReportRecipients(root) {
        root.querySelectorAll('[data-add-report-recipient]').forEach(function (button) {
            if (button.dataset.recipientBound) { return; }
            button.dataset.recipientBound = 'true';
            button.addEventListener('click', function () {
                var target = document.getElementById(button.dataset.addReportRecipient);
                if (!target) { return; }
                var input = document.createElement('input');
                input.name = button.dataset.reportRecipientName;
                input.type = 'email';
                input.autocomplete = 'email';
                input.setAttribute('aria-label', input.name === 'ccRecipients' ? 'Report copy recipient' : 'Report recipient');
                target.appendChild(input);
                input.focus();
            });
        });
    }

    // Glass's runs in its own window, opened here inside the
    // submit so no popup rule refuses it, and the Case record stays open with
    // its edit lease alive. The form's own target="_blank" stands when the
    // window is refused, and is the no-script path.
    function bindGlassWindow(root) {
        Array.prototype.slice.call((root || document).querySelectorAll('form[data-glass-window]')).forEach(function (form) {
            if (form.dataset.glassWindowBound) { return; }
            form.dataset.glassWindowBound = 'true';
            form.addEventListener('submit', function () {
                var opened = window.open('', 'pegasus-glass', 'popup=yes,width=1280,height=900');
                if (opened) { form.target = 'pegasus-glass'; }
            });
        });
    }

    // The Glass's window hands its outcome back here: the Estimate section is
    // reloaded without beaconing the edit scope away, exactly as a posted
    // command keeps it.
    window.pegasusGlassReturn = function (url) {
        if (typeof window.pegasusHoldEditScopeRelease === 'function') { window.pegasusHoldEditScopeRelease(); }
        window.location.assign(url);
    };

    bindReportRecipients(document); bindGlassWindow(document);
    (window.pegasusMountBinders = window.pegasusMountBinders || []).push(function (root) { bindReportRecipients(root); bindGlassWindow(root); });
})();


// --- vehicle -----------------------------------------------------------------
// --- Inspection details and Vehicle (lane A) -------------------------------
// The Inspect-at choice filling the address input, and the hidden
// `inspectionMode` it drives, are site.js's Inspect-at block, bound per root
// through window.pegasusMountBinders, so an in-place swap re-binds them. The
// lookup and the Save are plain forms the frame posts in place. Nothing here
// adds behaviour: the Vehicle and Inspection sections read and edit with the
// shared one-geometry cells alone.


// --- damage: damage by area (v28 P5) ---------------------------------------
// The discs on the plan, the three chips, the recorded-areas list and the
// derived cells are one view of the impacts JSON the Save reads from the
// hidden input. The page renders all of it server-side; this keeps them in
// step while the section edits: pressing and dragging on the vehicle sizes a
// disc (no smaller or wider than Core allows), dragging a disc moves it, and
// the areas a disc touches follow Core's bands. A drawn disc is kept as drawn
// and saved with its areas; Core reads the areas off it again. Reset returns
// to the values held when the edit opened. Outside a session nothing is live
// (D21: absent, not disabled).
(function () {
    'use strict';

    var SVG_NS = 'http://www.w3.org/2000/svg';

    function bind(root) {
        Array.prototype.slice.call(root.querySelectorAll('[data-damage-editor]')).forEach(function (editor) {
            if (editor.dataset.damageBound === 'true') {
                return;
            }
            editor.dataset.damageBound = 'true';

            var editable = editor.getAttribute('data-damage-editable') === 'true';
            var input = editor.querySelector('[data-damage-input]');
            var svg = editor.querySelector('svg.damage-diagram');
            var layer = editor.querySelector('[data-damage-marks]');
            var readout = editor.querySelector('[data-damage-readout]');
            var list = editor.querySelector('[data-damage-impact-list]');
            var reset = editor.querySelector('[data-damage-reset]');
            var locationCell = editor.querySelector('[data-damage-location]');
            var severityCell = editor.querySelector('[data-damage-severity]');
            var countCell = editor.querySelector('[data-damage-count]');
            var words = {
                multiple: editor.getAttribute('data-damage-multiple') || 'Multiple',
                absent: editor.getAttribute('data-damage-absent') || 'Not recorded',
                none: editor.getAttribute('data-damage-none') || 'No damage recorded.',
                noNote: editor.getAttribute('data-damage-no-note') || 'No note',
                note: editor.getAttribute('data-damage-note-label') || 'Note',
                remove: editor.getAttribute('data-damage-remove-label') || 'Remove',
                severity: editor.getAttribute('data-damage-grade-label') || 'Severity'
            };
            var vocabulary, severities, impacts;
            try { vocabulary = JSON.parse(editor.getAttribute('data-damage-areas') || '{}'); } catch (_) { vocabulary = {}; }
            try { severities = JSON.parse(editor.getAttribute('data-damage-severities') || '[]'); } catch (_) { severities = []; }
            try {
                impacts = JSON.parse(input ? input.value : (editor.getAttribute('data-damage-impacts') || '[]'));
                if (!Array.isArray(impacts)) { impacts = []; }
            } catch (_) { impacts = []; }
            var planAreas = vocabulary.plan || [];
            var otherAreas = vocabulary.other || [];
            var names = vocabulary.names || {};
            var centres = vocabulary.centres || {};
            var box = vocabulary.box || { x: 0, y: 0, w: 1, h: 1 };
            var bands = vocabulary.bands || { front: 0.34, rear: 0.72, left: 0.372, right: 0.628 };
            var baseRadius = vocabulary.radius || 0.12;
            // Core's disc limits, served in the area table: no second copy here.
            var margin = vocabulary.margin;
            var minRadius = vocabulary.minRadius * box.w;
            var maxRadius = vocabulary.maxRadius * box.w;
            var order = planAreas.concat(otherAreas);

            // Each recorded damage keeps its disc beside its areas. The disc the
            // page drew is read back, so the opening view is the recorded one
            // and Reset can return to it. `unit` is the disc the operator drew,
            // in the plan's own terms, saved with the damage exactly as it was
            // recorded until the disc is drawn or moved again; a damage recorded
            // by area alone has none.
            var marks = impacts.map(function (item, index) {
                var shown = layer ? layer.querySelector('[data-mark="' + index + '"] circle.area') : null;
                var unit = item.disc && typeof item.disc === 'object'
                    ? { x: +item.disc.x, y: +item.disc.y, r: +item.disc.r }
                    : null;
                return {
                    areas: Array.isArray(item.areas) ? item.areas.slice() : [],
                    severity: item.severity || 'moderate',
                    note: item.note || '',
                    disc: shown ? { x: +shown.getAttribute('cx'), y: +shown.getAttribute('cy'), r: +shown.getAttribute('r') } : null,
                    unit: unit
                };
            });
            var opening = JSON.parse(JSON.stringify(marks));

            function areaName(code) {
                return names[code] || String(code || '').replace(/_/g, ' ');
            }
            function areaNames(areas) {
                return areas.map(areaName).join(', ');
            }
            function sortAreas(areas) {
                return areas.slice().sort(function (a, b) { return order.indexOf(a) - order.indexOf(b); });
            }
            function severityName(code) {
                var found = severities.filter(function (item) { return item.k === code; })[0];
                return found ? found.n : code;
            }
            function severityRank(code) {
                return severities.map(function (item) { return item.k; }).indexOf(code);
            }
            function otherMark(code) {
                return marks.filter(function (mark) { return mark.areas.length === 1 && mark.areas[0] === code; })[0];
            }
            function format(value) {
                return String(Math.round(value * 10) / 10);
            }

            function persist() {
                if (input) {
                    input.value = JSON.stringify(marks.map(function (mark) {
                        return mark.unit
                            ? { areas: mark.areas, disc: mark.unit, severity: mark.severity, note: mark.note }
                            : { areas: mark.areas, severity: mark.severity, note: mark.note };
                    }));
                    input.dispatchEvent(new Event('input', { bubbles: true }));
                }
            }
            // A drawn disc in the plan's own terms, as Core stores it: the centre
            // as fractions of the body box, the radius as a fraction of its width.
            function round4(value) {
                return Math.round(value * 10000) / 10000;
            }
            function toUnit(disc) {
                return { x: round4((disc.x - box.x) / box.w), y: round4((disc.y - box.y) / box.h), r: round4(disc.r / box.w) };
            }
            function fromUnit(unit) {
                return { x: box.x + unit.x * box.w, y: box.y + unit.y * box.h, r: unit.r * box.w };
            }
            // A disc's centre stays on the body box and its radius between the
            // smallest and the largest disc Core accepts.
            function bound(disc) {
                disc.x = Math.max(box.x, Math.min(box.x + box.w, disc.x));
                disc.y = Math.max(box.y, Math.min(box.y + box.h, disc.y));
                disc.r = Math.max(minRadius, Math.min(maxRadius, disc.r));
                return disc;
            }

            // --- the plan: points, the vehicle and Core's bands
            function svgPoint(event) {
                var point = svg.createSVGPoint();
                point.x = event.clientX;
                point.y = event.clientY;
                var mapped = point.matrixTransform(svg.getScreenCTM().inverse());
                return { x: mapped.x, y: mapped.y };
            }
            function onVehicle(x, y) {
                var point = svg.createSVGPoint();
                point.x = x;
                point.y = y;
                var body = svg.querySelector('.dv-body');
                if (body && body.isPointInFill(point)) {
                    return true;
                }
                return Array.prototype.slice.call(svg.querySelectorAll('.dv-wheel, .dv-mirror')).some(function (rect) {
                    var rx = +rect.getAttribute('x');
                    var ry = +rect.getAttribute('y');
                    return x >= rx && x <= rx + +rect.getAttribute('width') && y >= ry && y <= ry + +rect.getAttribute('height');
                });
            }
            function areaAt(x, y) {
                if (!onVehicle(x, y)) {
                    return null;
                }
                var ux = (x - box.x) / box.w;
                var uy = (y - box.y) / box.h;
                var band = uy < bands.front ? 'front' : uy > bands.rear ? 'rear' : 'side';
                var lateral = ux < bands.left ? 'left' : ux > bands.right ? 'right' : 'centre';
                if (band === 'side') {
                    return (lateral === 'centre' ? (ux < 0.5 ? 'left' : 'right') : lateral) + '_side';
                }
                return lateral === 'centre' ? band : lateral + '_' + band;
            }
            function circleIntersectsArea(disc, left, top, right, bottom) {
                var closestX = Math.max(left, Math.min(disc.x, right));
                var closestY = Math.max(top, Math.min(disc.y, bottom));
                var dx = disc.x - closestX;
                var dy = disc.y - closestY;
                // Strictly positive area: tangency at a band boundary does not
                // record an area, matching DamageAreaGeometry in Core.
                return dx * dx + dy * dy < disc.r * disc.r;
            }
            function areaBounds(area) {
                var plan = box;
                var left = plan.x;
                var top = plan.y;
                var right = plan.x + plan.w;
                var bottom = plan.y + plan.h;
                var front = top + bands.front * plan.h;
                var rear = top + bands.rear * plan.h;
                var leftBand = left + bands.left * plan.w;
                var rightBand = left + bands.right * plan.w;
                var middle = left + plan.w / 2;
                switch (area) {
                    case 'front': return [leftBand, top, rightBand, front];
                    case 'left_front': return [left, top, leftBand, front];
                    case 'right_front': return [rightBand, top, right, front];
                    case 'left_side': return [left, front, middle, rear];
                    case 'right_side': return [middle, front, right, rear];
                    case 'rear': return [leftBand, rear, rightBand, bottom];
                    case 'left_rear': return [left, rear, leftBand, bottom];
                    case 'right_rear': return [rightBand, rear, right, bottom];
                    default: return null;
                }
            }
            // The plan areas a disc touches, judged as Core judges them: on the
            // disc as it is stored, so the page and the save agree at the edges.
            function areasUnder(disc) {
                var stored = fromUnit(toUnit(disc));
                return sortAreas(planAreas.filter(function (area) {
                    var bounds = areaBounds(area);
                    return bounds && circleIntersectsArea(stored, bounds[0], bounds[1], bounds[2], bounds[3]);
                }));
            }
            // The disc a damage recorded by area alone is drawn with, as Core
            // draws it (DamageAreaGeometry.Disc): between its areas, reaching
            // them, never wider than the vehicle.
            function renderedDisc(areas) {
                var points = areas.map(function (area) {
                    var centre = centres[area];
                    return centre ? { x: box.x + centre.x * box.w, y: box.y + centre.y * box.h } : null;
                }).filter(function (point) { return point !== null; });
                if (!points.length) {
                    return null;
                }
                var x = points.reduce(function (sum, point) { return sum + point.x; }, 0) / points.length;
                var y = points.reduce(function (sum, point) { return sum + point.y; }, 0) / points.length;
                var spread = points.reduce(function (max, point) {
                    return Math.max(max, Math.hypot(point.x - x, point.y - y));
                }, 0);
                return { x: x, y: y, r: Math.min(maxRadius, Math.max(baseRadius * box.w, spread + margin * box.w)) };
            }

            // --- painting
            function circle(className, x, y, r) {
                var element = document.createElementNS(SVG_NS, 'circle');
                element.setAttribute('class', className);
                element.setAttribute('cx', format(x));
                element.setAttribute('cy', format(y));
                element.setAttribute('r', format(r));
                return element;
            }
            function paintMarks() {
                if (!layer) {
                    return;
                }
                while (layer.firstChild) { layer.removeChild(layer.firstChild); }
                marks.forEach(function (mark, index) {
                    if (!mark.disc) {
                        return;
                    }
                    var group = document.createElementNS(SVG_NS, 'g');
                    group.setAttribute('class', 'dm');
                    group.setAttribute('data-mark', String(index));
                    group.setAttribute('data-sev', mark.severity);
                    var area = circle('area', mark.disc.x, mark.disc.y, mark.disc.r);
                    area.setAttribute('clip-path', 'url(#damage-plan-clip)');
                    group.appendChild(area);
                    group.appendChild(circle('n', mark.disc.x, mark.disc.y, 8));
                    var text = document.createElementNS(SVG_NS, 'text');
                    text.setAttribute('x', format(mark.disc.x));
                    text.setAttribute('y', format(mark.disc.y + 3.2));
                    text.setAttribute('text-anchor', 'middle');
                    text.textContent = String(index + 1);
                    group.appendChild(text);
                    layer.appendChild(group);
                });
            }
            function paintChips() {
                editor.querySelectorAll('[data-damage-area]').forEach(function (chip) {
                    var recorded = !!otherMark(chip.getAttribute('data-damage-area'));
                    chip.classList.toggle('is-damaged', recorded);
                    chip.setAttribute('aria-pressed', recorded ? 'true' : 'false');
                });
            }
            function paintDerived() {
                if (countCell) {
                    countCell.textContent = String(marks.length);
                }
                if (locationCell) {
                    var all = [];
                    marks.forEach(function (mark) {
                        mark.areas.forEach(function (area) { if (all.indexOf(area) < 0) { all.push(area); } });
                    });
                    var recorded = sortAreas(all).map(areaName);
                    locationCell.textContent = recorded.length === 0
                        ? words.absent
                        : recorded.length === 1
                            ? recorded[0]
                            : words.multiple + ' · ' + recorded.join(', ');
                }
                if (severityCell) {
                    var best = null;
                    marks.forEach(function (mark) {
                        if (best === null || severityRank(mark.severity) > severityRank(best)) {
                            best = mark.severity;
                        }
                    });
                    severityCell.textContent = best === null ? words.absent : severityName(best);
                }
            }
            function cell(className, contents) {
                var span = document.createElement('span');
                span.className = editable ? 'fc' : 'fc ro';
                var value = document.createElement('div');
                value.className = className;
                value.textContent = contents;
                span.appendChild(value);
                return span;
            }
            function renderList() {
                if (!list) {
                    return;
                }
                list.innerHTML = '';
                if (marks.length === 0) {
                    var empty = document.createElement('li');
                    empty.className = 'muted';
                    empty.setAttribute('data-damage-empty', '');
                    empty.textContent = words.none;
                    list.appendChild(empty);
                    return;
                }
                marks.forEach(function (mark, index) {
                    var row = document.createElement('li');
                    row.className = 'impact-row';
                    row.setAttribute('data-damage-row', String(index));

                    var name = document.createElement('span');
                    name.className = 'zc';
                    var badge = document.createElement('i');
                    badge.className = 'zn';
                    badge.textContent = String(index + 1);
                    name.appendChild(badge);
                    name.appendChild(document.createTextNode(areaNames(mark.areas)));
                    row.appendChild(name);

                    var severityCellRow = cell('fv', severityName(mark.severity));
                    if (editable) {
                        var select = document.createElement('select');
                        select.className = 'fi';
                        select.setAttribute('data-damage-row-severity', '');
                        select.setAttribute('aria-label', areaNames(mark.areas) + ' ' + words.severity.toLowerCase());
                        severities.forEach(function (severity) {
                            var option = document.createElement('option');
                            option.value = severity.k;
                            option.textContent = severity.n;
                            option.selected = severity.k === mark.severity;
                            select.appendChild(option);
                        });
                        severityCellRow.appendChild(select);
                    }
                    row.appendChild(severityCellRow);

                    var noteCellRow = cell('fv' + (mark.note ? '' : ' empty'), mark.note || words.noNote);
                    if (editable) {
                        var note = document.createElement('input');
                        note.className = 'fi';
                        note.maxLength = 200;
                        note.value = mark.note || '';
                        note.placeholder = words.note;
                        note.setAttribute('data-damage-row-note', '');
                        note.setAttribute('aria-label', areaNames(mark.areas) + ' ' + words.note.toLowerCase());
                        noteCellRow.appendChild(note);
                    }
                    row.appendChild(noteCellRow);

                    if (editable) {
                        var remove = document.createElement('button');
                        remove.type = 'button';
                        remove.className = 'del';
                        remove.setAttribute('data-damage-row-remove', '');
                        remove.setAttribute('aria-label', words.remove + ' ' + areaNames(mark.areas));
                        remove.textContent = '×';
                        row.appendChild(remove);
                    }
                    list.appendChild(row);
                });
            }
            function render() {
                paintMarks();
                paintChips();
                paintDerived();
                renderList();
            }
            function hover(index) {
                if (layer) {
                    layer.querySelectorAll('[data-mark]').forEach(function (group) {
                        group.classList.toggle('is-hover', index !== null && group.getAttribute('data-mark') === String(index));
                    });
                }
                if (list) {
                    list.querySelectorAll('[data-damage-row]').forEach(function (row) {
                        row.classList.toggle('is-hover', index !== null && row.getAttribute('data-damage-row') === String(index));
                    });
                }
            }

            function toggleOther(code) {
                if (!editable) {
                    return;
                }
                var existing = otherMark(code);
                if (existing) {
                    marks.splice(marks.indexOf(existing), 1);
                } else {
                    marks.push({ areas: [code], severity: 'moderate', note: '', disc: null, unit: null });
                }
                render();
                persist();
            }
            // The keyboard's way onto the plan: the focused area is recorded by
            // area alone, drawn with the disc its area gives.
            function addPlanArea(code) {
                if (!editable || planAreas.indexOf(code) < 0) {
                    return;
                }
                marks.push({ areas: [code], severity: 'moderate', note: '', disc: renderedDisc([code]), unit: null });
                render();
                persist();
            }
            editor.querySelectorAll('[data-damage-area]').forEach(function (chip) {
                chip.addEventListener('click', function () { toggleOther(chip.getAttribute('data-damage-area')); });
            });
            editor.querySelectorAll('[data-damage-plan-area]').forEach(function (control) {
                control.addEventListener('keydown', function (event) {
                    if (event.key !== 'Enter' && event.key !== ' ' && event.key !== 'Spacebar') {
                        return;
                    }
                    event.preventDefault();
                    addPlanArea(control.getAttribute('data-damage-plan-area'));
                });
            });

            // The plan: pressing and dragging sizes a new disc, dragging a
            // disc moves it, and the readout names the area under the pointer.
            // The disc stays as drawn; it names the areas it touches.
            if (editable && svg && layer) {
                var drawing = null;
                svg.addEventListener('pointerdown', function (event) {
                    var point = svgPoint(event);
                    var hit = event.target.closest ? event.target.closest('[data-mark]') : null;
                    if (hit) {
                        var moved = marks[+hit.getAttribute('data-mark')];
                        if (!moved || !moved.disc) {
                            return;
                        }
                        drawing = {
                            mark: moved,
                            move: true,
                            changed: false,
                            dx: moved.disc.x - point.x,
                            dy: moved.disc.y - point.y,
                            was: { disc: JSON.parse(JSON.stringify(moved.disc)), areas: moved.areas.slice(), unit: moved.unit }
                        };
                    } else {
                        var area = areaAt(point.x, point.y);
                        if (!area) {
                            return;
                        }
                        var mark = { areas: [area], severity: 'moderate', note: '', disc: bound({ x: point.x, y: point.y, r: 12 }), unit: null };
                        marks.push(mark);
                        drawing = { mark: mark, move: false, changed: true };
                    }
                    event.preventDefault();
                    svg.setPointerCapture(event.pointerId);
                    render();
                });
                svg.addEventListener('pointermove', function (event) {
                    var point = svgPoint(event);
                    if (!drawing) {
                        if (readout) {
                            var under = areaAt(point.x, point.y);
                            readout.textContent = under ? areaName(under) : '';
                        }
                        return;
                    }
                    drawing.changed = true;
                    if (drawing.move) {
                        drawing.mark.disc.x = point.x + drawing.dx;
                        drawing.mark.disc.y = point.y + drawing.dy;
                    } else {
                        drawing.mark.disc.r = Math.hypot(point.x - drawing.mark.disc.x, point.y - drawing.mark.disc.y);
                    }
                    bound(drawing.mark.disc);
                    var areas = areasUnder(drawing.mark.disc);
                    if (areas.length) {
                        drawing.mark.areas = areas;
                    }
                    render();
                });
                var finish = function () {
                    if (!drawing) {
                        return;
                    }
                    // A press on a disc that did not move it changes nothing.
                    if (!drawing.changed) {
                        drawing = null;
                        return;
                    }
                    // A disc dragged off the vehicle names nothing: a moved one
                    // returns, a new one is not recorded. Otherwise the disc is
                    // kept as drawn, at the precision it is saved at.
                    var areas = areasUnder(drawing.mark.disc);
                    if (!areas.length) {
                        if (drawing.move) {
                            drawing.mark.disc = drawing.was.disc;
                            drawing.mark.areas = drawing.was.areas;
                            drawing.mark.unit = drawing.was.unit;
                        } else {
                            marks.splice(marks.indexOf(drawing.mark), 1);
                        }
                    } else {
                        drawing.mark.unit = toUnit(drawing.mark.disc);
                        drawing.mark.disc = fromUnit(drawing.mark.unit);
                        drawing.mark.areas = areas;
                    }
                    drawing = null;
                    render();
                    persist();
                };
                svg.addEventListener('pointerup', finish);
                svg.addEventListener('pointercancel', finish);
                svg.addEventListener('pointerleave', function () {
                    if (readout && !drawing) { readout.textContent = ''; }
                });
                layer.addEventListener('mouseover', function (event) {
                    var group = event.target.closest ? event.target.closest('[data-mark]') : null;
                    hover(group ? +group.getAttribute('data-mark') : null);
                });
                layer.addEventListener('mouseleave', function () { hover(null); });
            }
            if (reset) {
                reset.addEventListener('click', function () {
                    marks = JSON.parse(JSON.stringify(opening));
                    render();
                    persist();
                });
            }
            // The list: severity and note write back; remove takes the damage
            // away; hover lights its disc.
            if (list) {
                list.addEventListener('change', function (event) {
                    var select = event.target.closest('[data-damage-row-severity]');
                    if (!select) {
                        return;
                    }
                    var row = select.closest('[data-damage-row]');
                    var recorded = row && marks[+row.getAttribute('data-damage-row')];
                    if (recorded) {
                        recorded.severity = select.value;
                        paintMarks();
                        paintDerived();
                        persist();
                    }
                });
                list.addEventListener('input', function (event) {
                    var note = event.target.closest('[data-damage-row-note]');
                    if (!note) {
                        return;
                    }
                    var row = note.closest('[data-damage-row]');
                    var recorded = row && marks[+row.getAttribute('data-damage-row')];
                    if (recorded) {
                        recorded.note = note.value;
                        persist();
                    }
                });
                list.addEventListener('click', function (event) {
                    var remove = event.target.closest('[data-damage-row-remove]');
                    if (!remove || !editable) {
                        return;
                    }
                    var row = remove.closest('[data-damage-row]');
                    if (row) {
                        marks.splice(+row.getAttribute('data-damage-row'), 1);
                        render();
                        persist();
                    }
                });
                list.addEventListener('mouseover', function (event) {
                    var row = event.target.closest('[data-damage-row]');
                    hover(row ? +row.getAttribute('data-damage-row') : null);
                });
                list.addEventListener('mouseleave', function () { hover(null); });
            }
            // The server rendered the discs, the chips, the list and the cells;
            // nothing is painted on load.
        });
    }

    bind(document);
    (window.pegasusMountBinders = window.pegasusMountBinders || []).push(bind);
})();


// --- valuation -----------------------------------------------------------------
// --- Valuation: the calculator's preview, basis cards and additions -----------
// The lines are Core's arithmetic: every change posts the selection to the
// PreviewValuation handler and the returned partial replaces the lines. The
// calculator's controls and the Basis radios belong to the Case form, so the
// ribbon Save adopts a changed calculation (one Save, 23 September 2026).
(function () {
    'use strict';

    // The Case form's own fields that a request carries, chosen by name.
    function caseFields(keep) {
        var body = new FormData();
        var caseForm = document.getElementById('case-edit-form');
        if (caseForm) {
            new FormData(caseForm).forEach(function (value, name) {
                if (keep(name)) { body.append(name, value); }
            });
        }
        return body;
    }
    function isSelection(name) { return typeof name === 'string' && name.indexOf('selection.') === 0; }

    function bind(root) {
        root.querySelectorAll('[data-valuation-form]').forEach(function (calc) {
            if (calc.dataset.valuationBound === 'true') {
                return;
            }
            calc.dataset.valuationBound = 'true';
            var section = calc.closest('.record-section') || document;
            var host = section.querySelector('[data-valuation-lines-host]');
            var basisName = section.querySelector('[data-valuation-basis-name]');
            var previewUrl = calc.getAttribute('data-preview-url');
            var timer = null;
            var inFlight = null;

            // A calculator control: one of this section's selection fields of
            // the Case form (the guide cards' own boxes are the Case's too, but
            // they are the cards, not the calculation).
            function belongs(control) {
                return !!control && isSelection(control.name);
            }

            function preview() {
                if (!previewUrl || !host) {
                    return;
                }
                var body = caseFields(function (name) {
                    return name === '__RequestVerificationToken' || isSelection(name);
                });
                if (inFlight) {
                    inFlight.abort();
                }
                inFlight = new AbortController();
                fetch(previewUrl, {
                    method: 'POST',
                    body: body,
                    credentials: 'same-origin',
                    headers: { 'X-Requested-With': 'fetch', 'Accept': 'text/html' },
                    signal: inFlight.signal
                }).then(function (response) {
                    if (!response.ok) {
                        throw new Error('valuation preview: ' + response.status);
                    }
                    return response.text();
                }).then(function (html) {
                    host.innerHTML = html;
                }).catch(function () {
                    // The lines keep their last state; a refused calculation
                    // shows on Save, which Core answers.
                });
            }
            function schedule() {
                window.clearTimeout(timer);
                timer = window.setTimeout(preview, 250);
            }

            function chooseBasis(radio) {
                section.querySelectorAll('[data-valuation-card]').forEach(function (card) {
                    var own = card.querySelector('[data-valuation-basis]');
                    card.classList.toggle('sel', own === radio);
                });
                if (basisName) {
                    basisName.textContent = 'from ' + (radio.getAttribute('data-source-name') || 'guide') + ' retail';
                }
            }

            function paintAdditions() {
                section.querySelectorAll('[data-valuation-add]').forEach(function (row) {
                    var toggle = row.querySelector('[data-preset-toggle]');
                    row.classList.toggle('on', !!(toggle && toggle.checked));
                });
            }

            section.addEventListener('change', function (event) {
                var control = event.target;
                if (!belongs(control)) {
                    return;
                }
                if (control.matches('[data-valuation-basis]') && control.checked) {
                    chooseBasis(control);
                }
                if (control.matches('[data-preset-toggle]')) {
                    paintAdditions();
                }
                schedule();
            });
            section.addEventListener('input', function (event) {
                if (belongs(event.target)) {
                    schedule();
                }
            });
            // A click anywhere on a card picks it as the basis; a click on one
            // of an entry card's own controls is the operator typing, not choosing.
            function selectCard(card) {
                var radio = card.querySelector('[data-valuation-basis]');
                if (!radio) {
                    return;
                }
                radio.checked = true;
                radio.dispatchEvent(new Event('change', { bubbles: true }));
            }
            section.querySelectorAll('[data-valuation-card]').forEach(function (card) {
                card.addEventListener('click', function (event) {
                    var radio = card.querySelector('[data-valuation-basis]');
                    if (!radio || event.target === radio || radio.checked
                        || (event.target.closest && event.target.closest('input,button,select,label,a'))) {
                        return;
                    }
                    selectCard(card);
                });
                card.addEventListener('keydown', function (event) {
                    if (event.target !== card || (event.key !== 'Enter' && event.key !== ' ' && event.key !== 'Spacebar')) {
                        return;
                    }
                    event.preventDefault();
                    selectCard(card);
                });
            });
            paintAdditions();
            var checked = section.querySelector('[data-valuation-basis]:checked');
            if (checked) {
                chooseBasis(checked);
            }
        });

        // Get valuation (23 September 2026): the source's figures come back as
        // JSON and fill the card's boxes, which belong to the Case form, so no
        // form is submitted, the page is not redrawn and nothing unsaved is put
        // at risk; the ribbon Save records the card. A source with no working
        // provider, or a refused request, shows the card's own notice.
        root.querySelectorAll('[data-valuation-get]').forEach(function (button) {
            if (button.dataset.valuationGetBound === 'true') {
                return;
            }
            button.dataset.valuationGetBound = 'true';
            button.addEventListener('click', function (event) {
                event.preventDefault();
                var card = button.closest('[data-valuation-entry]');
                var caseForm = document.getElementById('case-edit-form');
                var url = button.getAttribute('data-valuation-url');
                if (!card || !caseForm || !url || button.disabled) {
                    return;
                }
                var notice = card.querySelector('[data-valuation-notice]');
                var authority = ['__RequestVerificationToken', 'id', 'expectedVersion', 'operationKey', 'editLeaseToken'];
                var body = caseFields(function (name) { return authority.indexOf(name) >= 0; });
                var month = card.querySelector('[data-valuation-entry-month]');
                if (month) {
                    body.append('guideMonth', month.value);
                }
                showNotice(notice, false);
                button.disabled = true;
                fetch(url, {
                    method: 'POST',
                    body: body,
                    credentials: 'same-origin',
                    headers: { 'X-Requested-With': 'fetch', 'Accept': 'application/json' }
                }).then(function (response) {
                    return response.json().catch(function () { return { status: 'refused' }; });
                }).then(function (answer) {
                    if (answer && answer.status === 'ok') {
                        fill(card, '[data-valuation-retail]', answer.retail);
                        fill(card, '[data-valuation-trade]', answer.trade);
                        fill(card, '[data-valuation-entry-month]', answer.guideMonth);
                        return;
                    }
                    showNotice(notice, true, answer && answer.status === 'refused' ? answer.message : null);
                }).catch(function () {
                    showNotice(notice, true, null);
                }).then(function () {
                    button.disabled = false;
                });
            });
        });
    }

    // A fetched figure is typed into its box as if by hand: the input event
    // marks the Case form as changed, so the ribbon Save records it.
    function fill(card, selector, value) {
        var box = card.querySelector(selector);
        if (!box || value === undefined || value === null) {
            return;
        }
        box.value = String(value);
        box.dispatchEvent(new Event('input', { bubbles: true }));
    }

    // The card's notice: the approved unavailable sentence, or a refusal's own
    // words in its place when the server gave some.
    function showNotice(notice, visible, message) {
        if (!notice) {
            return;
        }
        var unavailable = notice.querySelector('[data-valuation-unavailable]');
        var refused = notice.querySelector('[data-valuation-refused]');
        if (refused) {
            refused.textContent = message || '';
            refused.hidden = !message;
        }
        if (unavailable) {
            unavailable.hidden = !!message;
        }
        if (visible) {
            notice.setAttribute('role', 'alert');
        } else {
            notice.removeAttribute('role');
        }
        notice.hidden = !visible;
    }

    bind(document);
    (window.pegasusMountBinders = window.pegasusMountBinders || []).push(bind);
})();


// --- estimate -----------------------------------------------------------------
// --- Estimate (v26 § Estimate) --------------------------------------------------
// The grid's trailing blank line, the full-screen presentation toggle, the VAT
// override reset and the Send to AI target output. Every control is a real
// form field; the phantom row posts under the same names and the save drops
// it while it stays blank. Root-scoped binders so a lazily mounted or swapped
// section joins.
(function () {
    'use strict';

    // Which VAT categories a repairer status charges by default — the same
    // table as EstimateVatPolicy.DefaultFor, read only to show the Overridden
    // chip and to put the boxes back; Core still decides on Save.
    var vatDefaults = {
        Registered: ['Labour', 'Parts', 'Materials', 'Specialist'],
        NotRegistered: ['Parts', 'Materials'],
        Unknown: []
    };

    function bindGrid(form) {
        var body = form.querySelector('[data-estimate-grid-body]');
        var template = form.querySelector('[data-estimate-line-template]');
        if (!body || !template) {
            return;
        }

        function rowCount() {
            return body.querySelectorAll('tr[data-estimate-line]').length;
        }

        function appendPhantom() {
            if (body.querySelector('tr[data-estimate-phantom]')) {
                return;
            }
            var fragment = template.content.cloneNode(true);
            var row = fragment.querySelector('tr');
            if (!row) {
                return;
            }
            body.appendChild(row);
        }

        // Typing into the blank line makes it a real line and a fresh blank
        // line appears beneath it. The remove button's value is the row's
        // index, which is what the EditLine handler reads.
        body.addEventListener('input', function (event) {
            var row = event.target.closest('tr[data-estimate-phantom]');
            if (!row || !body.contains(row)) {
                return;
            }
            row.classList.remove('phantom');
            row.removeAttribute('data-estimate-phantom');
            row.setAttribute('data-estimate-line', '');
            var remove = row.querySelector('button[name="removeLine"]');
            if (remove) {
                remove.value = String(rowCount() - 1);
            }
            var quantity = row.querySelector('input[name="lineQuantity"]');
            var operation = row.querySelector('select[name="lineOperation"]');
            if (quantity && !quantity.value && operation && operation.value === 'Replace') {
                quantity.value = '1';
            }
            appendPhantom();
        });

        // The no-script Add line posts a redraw; with script a line is one
        // keystroke away already, so Add line just focuses the blank line.
        var add = form.querySelector('[data-estimate-add-line]');
        if (add) {
            add.addEventListener('click', function (event) {
                var phantom = body.querySelector('tr[data-estimate-phantom]');
                if (!phantom) {
                    return;
                }
                event.preventDefault();
                var description = phantom.querySelector('input[name="lineDescription"]');
                if (description) {
                    description.focus();
                }
            });
        }

        // The editor's controls belong to the Case form; the save drops a
        // blank line, so the phantom row posts as nothing.
        function renumber() {
            body.querySelectorAll('tr[data-estimate-line] button[name="removeLine"]').forEach(function (button, index) {
                button.value = String(index);
            });
            // The frame's dirty guard listens for input on the Case form's
            // controls: a removed line is an unsaved change of the spec.
            form.querySelector('input[name="estimateId"]').dispatchEvent(new Event('input', { bubbles: true }));
            appendPhantom();
        }

        // A removed line, or all of them, can be put back for eight seconds
        // (v28 P16): the toast carries the one Undo.
        var undoLabel = (form.querySelector('[data-estimate-delete-all]') || {}).getAttribute
            ? form.querySelector('[data-estimate-delete-all]').getAttribute('data-undo-label') || 'Undo'
            : 'Undo';
        function undoToast(text, restore) {
            var region = document.querySelector('[data-toast-region]');
            if (!region) {
                return;
            }
            var note = document.createElement('div');
            note.className = 'toast toast--undo';
            note.setAttribute('role', 'status');
            var strong = document.createElement('strong');
            strong.textContent = text;
            var undo = document.createElement('button');
            undo.type = 'button';
            undo.className = 'btn btn--small';
            undo.textContent = undoLabel;
            undo.addEventListener('click', function () { restore(); note.remove(); });
            note.appendChild(strong);
            note.appendChild(undo);
            region.appendChild(note);
            window.setTimeout(function () { note.remove(); }, 8000);
        }

        body.addEventListener('click', function (event) {
            var remove = event.target.closest('button[name="removeLine"]');
            if (!remove) { return; }
            event.preventDefault();
            var row = remove.closest('tr');
            var next = row.nextSibling;
            row.remove();
            renumber();
            undoToast(form.getAttribute('data-line-removed-label') || 'Line removed', function () {
                if (next && next.parentNode === body) { body.insertBefore(row, next); } else { body.appendChild(row); }
                renumber();
            });
        });

        var deleteAll = form.querySelector('[data-estimate-delete-all]');
        var confirmDialog = document.querySelector('[data-dialog="delete-lines-dialog"]');
        if (deleteAll && confirmDialog) {
            var pendingDeleteRows = null;
            var yes = confirmDialog.querySelector('[data-delete-lines-confirm]');
            if (yes) {
                yes.addEventListener('click', function () {
                    var rows = pendingDeleteRows;
                    pendingDeleteRows = null;
                    if (!rows) { return; }
                    rows.forEach(function (row) { row.remove(); });
                    renumber();
                    undoToast(rows.length + ' ' + (form.getAttribute('data-lines-removed-label') || 'lines removed'), function () {
                        rows.forEach(function (row) { body.insertBefore(row, body.querySelector('tr[data-estimate-phantom]')); });
                        renumber();
                    });
                });
            }
            deleteAll.addEventListener('click', function (event) {
                event.preventDefault();
                var rows = Array.prototype.slice.call(body.querySelectorAll('tr[data-estimate-line]'));
                if (!rows.length) {
                    return;
                }
                var count = confirmDialog.querySelector('[data-delete-lines-count]');
                if (count) { count.textContent = String(rows.length); }
                pendingDeleteRows = rows;
                confirmDialog.pegasusOpen(deleteAll);
            });
        }

        appendPhantom();
    }

    // Target % of value (v28 P34): the browser carries only scaling intent.
    // Core owns the Engineer's Value,
    // floors and all monetary arithmetic when Apply is posted.
    function bindScale(form) {
        var bar = form.querySelector('[data-estimate-scale]');
        if (!bar) {
            return;
        }
        var range = bar.querySelector('[data-scale-range]');
        var percent = bar.querySelector('[data-scale-percent]');
        var apply = bar.querySelector('[data-scale-apply]');
        if (!range || !percent) {
            return;
        }
        var initial = percent.value || '100';
        percent.value = initial;
        range.value = initial;
        function reveal() {
            if (apply) { apply.hidden = false; }
        }
        range.addEventListener('input', function () {
            percent.value = range.value;
            reveal();
        });
        percent.addEventListener('input', function () {
            range.value = percent.value;
            reveal();
        });
    }

    // Contract repair (v28 P35): the tick is the outcome in Decisions; ticking
    // seeds the agreed sum from the specification, a different sum sets the
    // scaling target.
    function bindContract(form) {
        var bar = form.querySelector('[data-estimate-contract]');
        if (!bar) {
            return;
        }
        var tick = bar.querySelector('[data-contract-agreed]');
        var sum = document.getElementById(bar.getAttribute('data-estimate-contract-sum-control') || '');
        var read = bar.querySelector('[data-contract-sum-read]');
        var outcome = document.getElementById(bar.getAttribute('data-estimate-outcome-control') || '');
        var grossCell = form.querySelector('[data-estimate-gross]');
        var previous = outcome && outcome.value !== 'contract_repair' ? outcome.value : 'repairable';
        function gross() { return grossCell ? (parseFloat((grossCell.textContent || '').replace(/[^0-9.]/g, '')) || 0) : 0; }
        function setOutcome(contract) {
            if (!outcome) { return; }
            var wanted = contract ? 'contract_repair' : previous;
            if (outcome.value !== wanted) {
                outcome.value = wanted;
                outcome.dispatchEvent(new Event('input', { bubbles: true }));
                outcome.dispatchEvent(new Event('change', { bubbles: true }));
            }
        }
        if (tick) {
            tick.addEventListener('change', function () {
                if (tick.checked) {
                    if (outcome && outcome.value !== 'contract_repair') { previous = outcome.value; }
                    if (sum && !sum.value && gross() > 0) { sum.value = gross().toFixed(2); sum.dispatchEvent(new Event('input', { bubbles: true })); }
                    setOutcome(true);
                } else {
                    if (sum) { sum.value = ''; sum.dispatchEvent(new Event('input', { bubbles: true })); }
                    setOutcome(false);
                }
                paintSum();
            });
        }
        function paintSum() {
            if (!read) { return; }
            var agreed = sum ? parseFloat(sum.value) : NaN;
            read.textContent = isNaN(agreed) || !sum.value ? '\u2014' : '\u00a3' + agreed.toLocaleString('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
        }
        if (sum) {
            sum.addEventListener('input', function () {
                if (outcome && Number(sum.value) > 0 && outcome.value !== 'contract_repair') {
                    previous = outcome.value;
                    setOutcome(true);
                }
                paintSum();
            });
        }
        if (outcome) {
            outcome.addEventListener('change', function () {
                var is = outcome.value === 'contract_repair';
                if (tick) { tick.checked = is; }
                if (is) {
                    if (sum && !sum.value && gross() > 0) { sum.value = gross().toFixed(2); }
                } else {
                    previous = outcome.value;
                    if (sum) { sum.value = ''; }
                }
                paintSum();
            });
        }
    }

    // Compare (v28 P19): choosing From and To reloads the dialog with both.
    function bindCompare(section) {
        var form = section.querySelector('[data-estimate-compare-form]');
        if (!form) {
            return;
        }
        form.querySelectorAll('select').forEach(function (select) {
            select.addEventListener('change', function () {
                var from = form.querySelector('[name="from"]').value, to = form.querySelector('[name="to"]').value;
                if (from && to && from !== to) { form.requestSubmit(); }
            });
        });
        var go = form.querySelector('[data-estimate-compare-go]');
        if (go) { go.hidden = true; }
    }

    // The one labour rate control (v28 P33): choosing a card fills the
    // figure; typing a figure keeps the entered rate.
    function bindRate(form) {
        var pair = form.querySelector('[data-estimate-rate-pair]');
        if (!pair) {
            return;
        }
        var card = pair.querySelector('select');
        var rate = pair.querySelector('input');
        if (!card || !rate) {
            return;
        }
        // Filling the figure from a card still raises input, so the rest of the
        // form sees the change, but that is the card speaking rather than the
        // operator typing: only typing drops the card.
        var filling = false;
        card.addEventListener('change', function () {
            var option = card.options[card.selectedIndex];
            var figure = option && option.getAttribute('data-rate');
            if (figure) {
                filling = true;
                rate.value = figure;
                rate.dispatchEvent(new Event('input', { bubbles: true }));
                filling = false;
            }
        });
        rate.addEventListener('input', function () {
            if (!filling && card.value) { card.value = ''; }
        });
    }

    function bindVat(form) {
        var status = form.querySelector('[data-vat-status]');
        var boxes = Array.prototype.slice.call(form.querySelectorAll('input[data-vat-category]'));
        var chip = form.querySelector('[data-vat-overridden]');
        var reset = form.querySelector('[data-vat-reset]');
        if (!status || !boxes.length) {
            return;
        }
        function defaults() {
            return vatDefaults[status.value] || [];
        }
        function overridden() {
            var expected = defaults();
            return boxes.some(function (box) {
                return box.checked !== (expected.indexOf(box.getAttribute('data-vat-category')) >= 0);
            });
        }
        function paint() {
            var over = overridden();
            if (chip) { chip.hidden = !over; }
            if (reset) { reset.hidden = !over; }
        }
        boxes.forEach(function (box) { box.addEventListener('change', paint); });
        status.addEventListener('change', paint);
        if (reset) {
            reset.addEventListener('click', function () {
                var expected = defaults();
                boxes.forEach(function (box) {
                    box.checked = expected.indexOf(box.getAttribute('data-vat-category')) >= 0;
                });
                paint();
                status.dispatchEvent(new Event('change', { bubbles: true }));
            });
        }
        paint();
    }

    function bindExpand(section) {
        var button = section.querySelector('[data-estimate-expand]');
        if (!button) {
            return;
        }
        var use = button.querySelector('use');
        function apply(on) {
            section.classList.toggle('is-expanded', on);
            document.body.classList.toggle('has-expanded', on);
            var label = on ? button.getAttribute('data-label-close') : button.getAttribute('data-label-expand');
            button.title = label || '';
            button.setAttribute('aria-label', label || '');
            button.setAttribute('aria-pressed', on ? 'true' : 'false');
            if (use) {
                use.setAttribute('href', on ? '#icon-x' : '#icon-external-link');
            }
            if (on) {
                section.scrollTop = 0;
            }
        }
        button.addEventListener('click', function () {
            apply(!section.classList.contains('is-expanded'));
        });
        function onKeydown(event) {
            if (event.key === 'Escape' && section.isConnected && section.classList.contains('is-expanded')) {
                event.preventDefault();
                apply(false);
            }
        }
        document.addEventListener('keydown', onKeydown);
        // A section swapped out of the page must not leave the body expanded.
        var observer = new MutationObserver(function () {
            if (!section.isConnected) {
                document.body.classList.remove('has-expanded');
                document.removeEventListener('keydown', onKeydown);
                observer.disconnect();
            }
        });
        observer.observe(document.body, { childList: true, subtree: true });
    }

    function bindRange(root) {
        root.querySelectorAll('input[data-estimate-range]').forEach(function (range) {
            if (range.dataset.estimateRangeBound === 'true') {
                return;
            }
            range.dataset.estimateRangeBound = 'true';
            var output = document.getElementById(range.getAttribute('data-estimate-range-output'));
            var amount = document.getElementById(range.getAttribute('data-estimate-range-amount'));
            var base = Number(range.getAttribute('data-estimate-range-base'));
            function render() {
                if (range.value === '') {
                    if (output) output.textContent = '—';
                    if (amount) amount.textContent = '—';
                    return;
                }
                var percent = Number(range.value);
                if (output) {
                    output.textContent = percent + '%';
                }
                if (amount && Number.isFinite(base) && base > 0) {
                    amount.textContent = Math.round(base * percent / 100).toLocaleString('en-GB', {
                        style: 'currency', currency: 'GBP', minimumFractionDigits: 2, maximumFractionDigits: 2
                    });
                }
            }
            range.addEventListener('input', render);
            render();
        });
    }

    function bind(root) {
        root.querySelectorAll('[data-estimate-section]').forEach(function (section) {
            if (section.dataset.estimateBound === 'true') {
                return;
            }
            section.dataset.estimateBound = 'true';
            bindExpand(section);
            var form = section.querySelector('[data-estimate-form]');
            if (form) {
                bindGrid(form);
                bindVat(form);
                bindRate(form);
                bindScale(form);
                bindContract(form);
            }
            bindCompare(document);
        });
        bindRange(root);
    }

    bind(document);
    (window.pegasusMountBinders = window.pegasusMountBinders || []).push(bind);
})();


// --- report -----------------------------------------------------------------
// --- overview: the claim source select ----------------------------------------
// Choosing another claim source while editing fills the three contact cells
// and its "Notes on every Case" at once, from the option's own data attributes
// (no request); Save records the choice and the server renders the same.
(function () {
    'use strict';

    function bind(root) {
        root.querySelectorAll('[data-claim-source-select]').forEach(function (select) {
            if (select.dataset.claimSourceBound === 'true') {
                return;
            }
            select.dataset.claimSourceBound = 'true';
            var section = select.closest('[data-section="overview"]') || document;
            select.addEventListener('change', function () {
                var option = select.options[select.selectedIndex];
                var notes = option ? option.getAttribute('data-notes') || '' : '';
                section.querySelectorAll('[data-claim-source-contact]').forEach(function (cell) {
                    var contact = option ? option.getAttribute('data-contact-' + cell.getAttribute('data-claim-source-contact')) || '' : '';
                    var input = cell.querySelector('.fi');
                    if (input) {
                        input.value = contact;
                    }
                });
                var cell = section.querySelector('[data-record-notes="claim-source"], [data-record-notes-slot="claim-source"]');
                if (cell) {
                    var text = cell.querySelector('[data-record-notes-text]');
                    if (text) {
                        text.textContent = notes;
                    }
                    cell.hidden = !notes;
                }
            });
        });
    }
    bind(document);
    (window.pegasusMountBinders = window.pegasusMountBinders || []).push(bind);
})();

// --- images: the grid's own acts (v28 P41 drag, P27 click to include) --------
// Both write through the role and order controls the preparation binder
// already owns, so the tile, the viewer and the Case Save cannot disagree.
(function () {
    'use strict';

    function bind(root) {
        root.querySelectorAll('[data-image-grid]').forEach(function (grid) {
            if (grid.dataset.imageGridBound === 'true') {
                return;
            }
            grid.dataset.imageGridBound = 'true';
            var dragging = null;

            function tileOf(element) {
                return element ? element.closest('[data-image-tile][data-preparation-card]') : null;
            }
            function renumber() {
                var at = 0;
                grid.querySelectorAll('[data-image-tile][data-preparation-card]').forEach(function (tile) {
                    var role = tile.querySelector('[data-preparation-role-select]');
                    var order = tile.querySelector('[data-preparation-order]');
                    if (!role || !order || role.value !== 'Supporting') { return; }
                    at += 1;
                    if (order.value !== String(at)) {
                        order.value = String(at);
                        order.dispatchEvent(new Event('change', { bubbles: true }));
                    }
                });
            }

            grid.addEventListener('dragstart', function (event) {
                var grip = event.target.closest('.grip');
                dragging = grip ? tileOf(grip) : null;
                if (!dragging) { return; }
                dragging.classList.add('is-dragging');
                event.dataTransfer.effectAllowed = 'move';
                event.dataTransfer.setData('text/plain', dragging.getAttribute('data-preparation-occurrence') || '');
            });
            grid.addEventListener('dragover', function (event) {
                var over = tileOf(event.target);
                if (!dragging || !over || over === dragging) { return; }
                event.preventDefault();
                over.classList.add('is-drop');
            });
            grid.addEventListener('dragleave', function (event) {
                var over = tileOf(event.target);
                if (over) { over.classList.remove('is-drop'); }
            });
            grid.addEventListener('drop', function (event) {
                var over = tileOf(event.target);
                if (!dragging || !over || over === dragging) { return; }
                event.preventDefault();
                over.classList.remove('is-drop');
                grid.insertBefore(dragging, over);
                renumber();
            });
            grid.addEventListener('dragend', function () {
                if (dragging) { dragging.classList.remove('is-dragging'); }
                dragging = null;
                grid.querySelectorAll('.is-drop').forEach(function (tile) { tile.classList.remove('is-drop'); });
            });

            // P27: while the tile carries its report controls, clicking the
            // image itself toggles whether the report uses it.
            grid.addEventListener('click', function (event) {
                if (event.target.closest('[data-image-report], .image-tile-actions')) { return; }
                var link = event.target.closest('a[data-evidence-item]');
                var tile = tileOf(link);
                var role = tile ? tile.querySelector('[data-preparation-role-select]') : null;
                if (!link || !role) { return; }
                event.preventDefault();
                event.stopPropagation();
                window.pegasusCasePreparation.toggleInReport(
                    tile.getAttribute('data-preparation-occurrence'),
                    role.value === 'NotUsed');
            }, true);
        });
    }

    bind(document);
    (window.pegasusMountBinders = window.pegasusMountBinders || []).push(bind);
})();



// --- report: the wording blocks (v28 P30) ------------------------------------
// Every block posts through the record's one Save form. The tools here only
// move a row, put the composed sentence back, or add a paragraph, each by
// writing a control the form already carries.
(function () {
    'use strict';

    function bind(root) {
        root.querySelectorAll('[data-report-wording]').forEach(function (panel) {
            if (panel.dataset.wordingBound === 'true') {
                return;
            }
            panel.dataset.wordingBound = 'true';
            var list = panel.querySelector('[data-wording-list]');
            var template = panel.querySelector('[data-wording-template]');
            if (!list) {
                return;
            }
            var added = 0;

            function rows() {
                return Array.prototype.slice.call(list.querySelectorAll('[data-wording-row]'));
            }
            // The order a block carries is its place in the list, so the print
            // order and the window order cannot disagree.
            function renumber() {
                rows().forEach(function (row, at) {
                    var order = row.querySelector('[data-wording-order]');
                    if (order && order.value !== String(at)) {
                        order.value = String(at);
                        order.dispatchEvent(new Event('change', { bubbles: true }));
                    }
                });
            }
            function move(row, delta) {
                var all = rows(), at = all.indexOf(row), swap = all[at + delta];
                if (!swap) { return; }
                if (delta < 0) { list.insertBefore(row, swap); } else { list.insertBefore(swap, row); }
                renumber();
            }

            panel.addEventListener('click', function (event) {
                var row = event.target.closest('[data-wording-row]');
                if (row && event.target.closest('[data-wording-up]')) {
                    event.preventDefault(); move(row, -1); return;
                }
                if (row && event.target.closest('[data-wording-down]')) {
                    event.preventDefault(); move(row, 1); return;
                }
                if (row && event.target.closest('[data-wording-recompose]')) {
                    event.preventDefault();
                    var text = row.querySelector('[data-wording-text]');
                    if (text) {
                        text.value = row.getAttribute('data-wording-composed') || '';
                        text.dispatchEvent(new Event('input', { bubbles: true }));
                    }
                    return;
                }
                if (!event.target.closest('[data-wording-new]') || !template) { return; }
                event.preventDefault();
                added += 1;
                var slot = rows().length;
                var key = 'manual:' + Date.now().toString(36) + '-' + added;
                var holder = document.createElement('div');
                holder.innerHTML = template.innerHTML.split('__i__').join(String(slot)).split('__key__').join(key);
                var fresh = holder.querySelector('[data-wording-row]');
                if (!fresh) { return; }
                list.appendChild(fresh);
                renumber();
                var title = fresh.querySelector('.wbt');
                if (title) { title.focus(); title.select(); }
            });

            panel.addEventListener('change', function (event) {
                var box = event.target.closest('[data-wording-included]');
                var row = box ? box.closest('[data-wording-row]') : null;
                if (row) { row.classList.toggle('is-off', !box.checked); }
            });
        });
    }

    bind(document);
    (window.pegasusMountBinders = window.pegasusMountBinders || []).push(bind);
})();

// --- report: the Report and Fee tabs (v28 P24) -------------------------------
// Two panes of one section. The server renders both, so a browser without
// script reads the fee note under the report; this shows the tabs and keeps
// one pane at a time.
(function () {
    'use strict';

    function bind(root) {
        root.querySelectorAll('[data-report-tabs]').forEach(function (tabs) {
            if (tabs.dataset.reportTabsBound === 'true') {
                return;
            }
            tabs.dataset.reportTabsBound = 'true';
            var section = tabs.closest('[data-report]') || document;
            var panes = Array.prototype.slice.call(section.querySelectorAll('[data-report-pane]'));
            var buttons = Array.prototype.slice.call(tabs.querySelectorAll('[data-report-tab]'));
            if (!panes.length || !buttons.length) {
                return;
            }
            tabs.hidden = false;
            function show(name) {
                panes.forEach(function (pane) {
                    pane.hidden = pane.getAttribute('data-report-pane') !== name;
                });
                buttons.forEach(function (button) {
                    var on = button.getAttribute('data-report-tab') === name;
                    button.setAttribute('aria-selected', on ? 'true' : 'false');
                    button.setAttribute('tabindex', on ? '0' : '-1');
                });
            }
            tabs.addEventListener('click', function (event) {
                var button = event.target.closest('[data-report-tab]');
                if (button) { show(button.getAttribute('data-report-tab')); }
            });
            tabs.addEventListener('keydown', function (event) {
                var at = buttons.indexOf(document.activeElement);
                if (at < 0 || (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight')) { return; }
                event.preventDefault();
                var to = buttons[(at + (event.key === 'ArrowRight' ? 1 : buttons.length - 1)) % buttons.length];
                show(to.getAttribute('data-report-tab'));
                to.focus();
            });
            show('report');
        });
    }

    bind(document);
    (window.pegasusMountBinders = window.pegasusMountBinders || []).push(bind);
})();


// --- settlement: the Decisions strip -----------------------------------------
// The outcome and roadworthiness selects show and hide the rows that only
// apply to them; Accept copies an AI proposal into the row's own control.
// Every control is a plain form field of #case-edit-form, so without script
// the rows the server rendered stand and the operator picks the value.
(function () {
    'use strict';

    function control(row) {
        return row ? row.querySelector('select.fi, input.fi, textarea.fi') : null;
    }

    // P29: the codes as a radio group. The select stays the posted control —
    // the show-and-hide rules read it — so the group drives it and the select
    // is hidden only once this runs.
    function bindRadios(section) {
        section.querySelectorAll('[data-decision-radios]').forEach(function (group) {
            var row = group.closest('.dec');
            var select = control(row);
            if (!select) {
                return;
            }
            group.hidden = false;
            select.classList.add('is-driven');
            var buttons = Array.prototype.slice.call(group.querySelectorAll('[data-radio-value]'));
            function paint() {
                var at = 0;
                buttons.forEach(function (button, index) {
                    var on = button.getAttribute('data-radio-value') === select.value;
                    button.setAttribute('aria-checked', on ? 'true' : 'false');
                    if (on) { at = index; }
                });
                buttons.forEach(function (button, index) {
                    button.setAttribute('tabindex', index === at ? '0' : '-1');
                });
            }
            function choose(button, focus) {
                var choice = button.getAttribute('data-radio-value');
                select.value = choice;
                if (choice === '') {
                    select.dataset.decisionExplicitUnset = 'true';
                } else {
                    delete select.dataset.decisionExplicitUnset;
                }
                select.dispatchEvent(new Event('input', { bubbles: true }));
                select.dispatchEvent(new Event('change', { bubbles: true }));
                paint();
                if (focus) { button.focus(); }
            }
            group.addEventListener('click', function (event) {
                var button = event.target.closest('[data-radio-value]');
                if (button) { choose(button, false); }
            });
            group.addEventListener('keydown', function (event) {
                var at = buttons.indexOf(document.activeElement);
                if (at < 0) { return; }
                var to = null;
                if (event.key === 'ArrowRight' || event.key === 'ArrowDown') { to = (at + 1) % buttons.length; }
                else if (event.key === 'ArrowLeft' || event.key === 'ArrowUp') { to = (at - 1 + buttons.length) % buttons.length; }
                else if (event.key === 'Home') { to = 0; }
                else if (event.key === 'End') { to = buttons.length - 1; }
                else if (event.key === ' ' || event.key === 'Enter') { event.preventDefault(); choose(buttons[at], true); return; }
                if (to === null) { return; }
                event.preventDefault();
                choose(buttons[to], true);
            });
            select.addEventListener('change', paint);
            paint();
        });
    }

    // P14: the salvage value as a share of the Engineer's Value. The money
    // field stays the posted control; the slider and the snaps write into it.
    function bindSalvageShare(section) {
        var share = section.querySelector('[data-salvage-share]');
        var row = share ? share.closest('.dec') : null;
        var field = row ? row.querySelector('input.fi') : null;
        var range = share ? share.querySelector('[data-salvage-range]') : null;
        if (!share || !field || !range) {
            return;
        }
        var valueControl = section.querySelector('[data-decision-engineer-value]');
        var read = share.querySelector('[data-salvage-read]');
        var snaps = Array.prototype.slice.call(share.querySelectorAll('[data-salvage-snap]'));
        function value() {
            return valueControl ? parseFloat(valueControl.getAttribute('data-engineer-value')) || 0 : 0;
        }
        function money(amount) {
            return '£' + amount.toLocaleString('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
        }
        function paint(percent) {
            if (read) { read.textContent = '= ' + (Math.round(percent * 10) / 10) + '% of ' + money(value()); }
            snaps.forEach(function (snap) {
                snap.classList.toggle('on', Math.abs(percent - parseFloat(snap.getAttribute('data-salvage-snap'))) < 0.5);
            });
        }
        function fromField() {
            var amount = parseFloat(field.value) || 0;
            var current = value();
            var percent = current > 0 ? amount / current * 100 : 0;
            range.value = String(Math.max(0, Math.min(100, Math.round(percent))));
            paint(percent);
        }
        function fromRange() {
            var percent = parseFloat(range.value) || 0;
            field.value = (value() * percent / 100).toFixed(2);
            field.dispatchEvent(new Event('input', { bubbles: true }));
            paint(percent);
        }
        range.addEventListener('input', fromRange);
        field.addEventListener('input', fromField);
        if (valueControl) {
            valueControl.addEventListener('input', fromField);
            valueControl.addEventListener('change', fromField);
            if (typeof MutationObserver === 'function') {
                new MutationObserver(fromField).observe(valueControl, {
                    attributes: true, childList: true, characterData: true, subtree: true
                });
            }
        }
        share.addEventListener('click', function (event) {
            var snap = event.target.closest('[data-salvage-snap]');
            if (!snap) { return; }
            range.value = snap.getAttribute('data-salvage-snap');
            fromRange();
        });
        fromField();
    }

    // P15: a bank wording joins the typed reason.
    function bindReasonBank(section) {
        var bank = section.querySelector('[data-reason-bank]');
        var row = section.querySelector('[data-decision="assessment.unroadworthy_reason"]');
        var area = row ? row.querySelector('textarea.fi') : null;
        if (!bank || !area) {
            return;
        }
        bank.addEventListener('click', function (event) {
            var pick = event.target.closest('[data-bank-wording]');
            if (pick) {
                var wording = pick.getAttribute('data-bank-wording');
                var text = (area.value || '').trim();
                if (text.toLowerCase().indexOf(wording.toLowerCase()) < 0) {
                    area.value = text.length === 0
                        ? wording.charAt(0).toUpperCase() + wording.slice(1)
                        : text.replace(/\.$/, '') + ' and ' + wording;
                    area.dispatchEvent(new Event('input', { bubbles: true }));
                }
                return;
            }
        });
    }

    function bind(root) {
        root.querySelectorAll('[data-settlement]').forEach(function (section) {
            if (section.dataset.settlementBound === 'true') {
                return;
            }
            section.dataset.settlementBound = 'true';

            var outcome = control(section.querySelector('[data-decision="assessment.outcome"]'));
            var legal = control(section.querySelector('[data-decision="assessment.legal_status"]'));
            var reserveRead = section.querySelector('[data-settlement-computed-reserve-value]');
            var repairCost = parseFloat(section.getAttribute('data-settlement-repair-cost')) || 0;
            bindRadios(section);
            bindSalvageShare(section);
            bindReasonBank(section);

            function show(when, on) {
                section.querySelectorAll('[data-shown-when="' + when + '"]').forEach(function (element) {
                    element.hidden = !on;
                });
                // P29: the line that says the salvage rows do not apply is the
                // other half of the same rule.
                section.querySelectorAll('[data-hidden-when="' + when + '"]').forEach(function (element) {
                    element.hidden = on;
                });
            }
            function syncReserve(outcomeValue) {
                if (!reserveRead) {
                    return;
                }
                var reserve = outcomeValue === 'repairable' && repairCost > 0
                    ? Math.ceil(repairCost / 50) * 50 : null;
                reserveRead.textContent = reserve === null
                    ? reserveRead.getAttribute('data-not-applicable')
                    : '£' + reserve.toLocaleString('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
                        + ' (' + reserveRead.getAttribute('data-rounded-up') + ')';
                reserveRead.classList.toggle('empty', reserve === null);
            }
            // An awaiting AI proposal leaves its control empty until accepted,
            // so the rows it implies follow the proposal until a person decides.
            function decided(path, control) {
                if (control && (control.value || control.dataset.decisionExplicitUnset === 'true')) {
                    return control.value;
                }
                var awaiting = section.querySelector('[data-proposal="' + path + '"][data-proposal-status="Awaiting"] [data-proposal-value]');
                return awaiting ? awaiting.getAttribute('data-proposal-value') : '';
            }
            function sync() {
                if (outcome) {
                    var outcomeValue = decided('assessment.outcome', outcome);
                    show('total-loss', outcomeValue === 'total_loss');
                    syncReserve(outcomeValue);
                }
                if (legal) {
                    show('unroadworthy', decided('assessment.legal_status', legal) === 'unroadworthy');
                }
            }
            if (outcome) { outcome.addEventListener('change', sync); }
            if (legal) { legal.addEventListener('change', sync); }
            sync();

            function accept(button) {
                var path = button.getAttribute('data-accept-proposal');
                var row = section.querySelector('[data-decision="' + path + '"]');
                var proposal = row && row.querySelector('[data-proposal-value]');
                var target = control(row);
                if (!proposal || !target) {
                    return;
                }
                target.value = proposal.getAttribute('data-proposal-value');
                target.dispatchEvent(new Event('input', { bubbles: true }));
                target.dispatchEvent(new Event('change', { bubbles: true }));
                button.hidden = true;
            }
            section.querySelectorAll('[data-accept-proposal]').forEach(function (button) {
                button.addEventListener('click', function () { accept(button); });
            });
            var all = section.querySelector('[data-accept-all-proposals]');
            if (all) {
                all.addEventListener('click', function () {
                    section.querySelectorAll('[data-accept-proposal]').forEach(function (button) {
                        if (!button.hidden) { accept(button); }
                    });
                    all.hidden = true;
                });
            }
        });
    }
    bind(document);
    (window.pegasusMountBinders = window.pegasusMountBinders || []).push(bind);
})();

// --- report: content switches, the fee-note choice and the draft preview ------
(function () {
    'use strict';

    var words = {
        'report.disclose_guide_source': ['Guide source disclosed', 'Guide source not disclosed'],
        'report.valuation_commentary': ['valuation commentary', ''],
        'report.include_unrelated_damage': ['unrelated damage', '']
    };

    function bindContent(root) {
        root.querySelectorAll('[data-report-content]').forEach(function (summary) {
            var group = summary.closest('[data-field="report-content"]');
            if (!group || group.dataset.reportContentBound === 'true') {
                return;
            }
            group.dataset.reportContentBound = 'true';
            var switches = Array.prototype.slice.call(group.querySelectorAll('[data-report-switch]'));
            function read() {
                if (!switches.length) {
                    return;
                }
                var parts = [];
                switches.forEach(function (box) {
                    var pair = words[box.getAttribute('data-report-switch')] || ['', ''];
                    var text = box.checked ? pair[0] : pair[1];
                    if (text) { parts.push(text); }
                });
                summary.textContent = parts.join(' · ');
            }
            switches.forEach(function (box) { box.addEventListener('change', read); });
        });
    }

    function bindReport(root) {
        root.querySelectorAll('[data-report]').forEach(function (section) {
            if (section.dataset.reportPreviewBound === 'true') {
                return;
            }
            section.dataset.reportPreviewBound = 'true';

            // The preview follows the Include fee note choice, and opens in
            // the page's document viewer when one is present.
            var preview = section.querySelector('[data-report-preview]');
            var feeNote = section.querySelector('[data-include-fee-note]');
            function previewHref() {
                if (!preview) {
                    return '';
                }
                var url = new URL(preview.getAttribute('href'), window.location.href);
                url.searchParams.set('includeFeeNote', feeNote && feeNote.checked ? 'True' : 'False');
                return url.toString();
            }
            if (preview && feeNote) {
                feeNote.addEventListener('change', function () { preview.setAttribute('href', previewHref()); });
            }
        });
    }

    function bind(root) {
        bindContent(root);
        bindReport(root);
    }
    bind(document);
    (window.pegasusMountBinders = window.pegasusMountBinders || []).push(bind);
})();

// --- saved document previews -------------------------------------------------
// Fetch once, then give the viewer and its Download action the same Blob URL.
// Plain links remain the no-script browser-PDF path.
(function () {
    'use strict';

    function fileName(response, fallback) {
        var disposition = response.headers.get('content-disposition') || '';
        var match = /filename="?([^";]+)"?/i.exec(disposition);
        return match ? match[1] : fallback;
    }

    function mediaType(response) {
        return (response.headers.get('content-type') || '').split(';')[0].trim().toLowerCase();
    }

    async function failureMessage(response) {
        if (response.status === 422 && mediaType(response) === 'application/problem+json') {
            try {
                var problem = await response.json();
                if (problem && typeof problem.detail === 'string' && problem.detail.trim()) {
                    return problem.detail.trim().slice(0, 500);
                }
            } catch (_) {
                // A malformed refusal is an unexpected response below.
            }
        }
        return 'Preview unavailable';
    }

    function bind(root) {
        root.querySelectorAll('[data-document-preview]').forEach(function (trigger) {
            if (trigger.dataset.documentPreviewBound === 'true') { return; }
            trigger.dataset.documentPreviewBound = 'true';
            trigger.addEventListener('click', async function (event) {
                var viewer = window.pegasusCaseViewer;
                if (!viewer || typeof viewer.openDocument !== 'function') { return; }
                event.preventDefault();
                var menu = trigger.closest('details[data-menu]');
                if (menu) { menu.open = false; }
                var response;
                var message = 'Preview unavailable';
                try {
                    response = await fetch(trigger.href, {
                        credentials: 'same-origin',
                        headers: { 'X-Pegasus-Document-Preview': '1' }
                    });
                    if (!response.ok || mediaType(response) !== 'application/pdf') {
                        message = await failureMessage(response);
                        throw new Error(message);
                    }
                    var url = URL.createObjectURL(await response.blob());
                    viewer.openDocument({
                        href: url,
                        name: fileName(response, trigger.getAttribute('data-file-name') || 'Estimate PDF'),
                        download: url,
                        downloadLabel: trigger.hasAttribute('data-report-preview') ? 'Download draft' : 'Download',
                        revoke: url,
                        invoker: trigger
                    });
                } catch (error) {
                    if (typeof window.pegasusToast === 'function') { window.pegasusToast(message); }
                    else { window.alert(message); }
                }
            });
        });
    }
    bind(document);
    (window.pegasusMountBinders = window.pegasusMountBinders || []).push(bind);
})();


// --- files -----------------------------------------------------------------
// --- files: tabs, the Correspondence message dialog, image preparation staging,
//     tiles, the viewer and crop ------------------------------------------------
// v26 § Files, § Image viewer, § Crop and tag. The crop is a stored rectangle
// over the rotated source (Core's CaseAssetCrop), staged into the one Case
// form as preparationEdits[i].* and posted by Save; nothing here writes a
// file or a mutation of its own. Every binder is root-scoped and idempotent.
(function () {
    'use strict';

    // ---- helpers -------------------------------------------------------------
    function number(value, fallback) {
        var parsed = Number(value);
        return Number.isFinite(parsed) ? parsed : fallback;
    }
    function clamp(value, low, high) { return Math.max(low, Math.min(high, value)); }
    function round7(value) { return Math.round(value * 10000000) / 10000000; }
    function all(root, selector) { return Array.prototype.slice.call((root || document).querySelectorAll(selector)); }
    function normalRotation(value) { return ((Math.round(value / 90) * 90) % 360 + 360) % 360; }
    function isFull(c) { return c.left === 0 && c.top === 0 && c.width === 1 && c.height === 1; }
    function sameCrop(a, b) {
        return Math.abs(a.left - b.left) < .0000001 && Math.abs(a.top - b.top) < .0000001
            && Math.abs(a.width - b.width) < .0000001 && Math.abs(a.height - b.height) < .0000001;
    }
    // A rectangle in the rotated source's fractions, re-expressed after a
    // further quarter turn clockwise (so a selection follows the image while
    // the operator rotates in the crop editor).
    function turnRect(r, degrees) {
        var d = normalRotation(degrees);
        if (d === 90) { return { left: 1 - (r.top + r.height), top: r.left, width: r.height, height: r.width }; }
        if (d === 180) { return { left: 1 - r.left - r.width, top: 1 - r.top - r.height, width: r.width, height: r.height }; }
        if (d === 270) { return { left: r.top, top: 1 - (r.left + r.width), width: r.height, height: r.width }; }
        return { left: r.left, top: r.top, width: r.width, height: r.height };
    }
    // The stored crop is over the rotated source; the <img> element's own
    // coordinate space is the unrotated one, which clip-path and translate use.
    function unrotate(c, rotation) { return turnRect(c, 360 - normalRotation(rotation)); }
    function cropLabel(c) {
        return isFull(c)
            ? 'Full frame'
            : 'Left ' + fmt(c.left) + ' · Top ' + fmt(c.top) + ' · Width ' + fmt(c.width) + ' · Height ' + fmt(c.height);
        function fmt(v) { return String(Math.round(v * 100) / 100); }
    }
    function rotationLabel(r) { return r ? r + '°' : 'None'; }
    function roleLabel(role) {
        return { NotUsed: 'Not used', CloseUp: 'Close-up', Overview: 'Overview', Supporting: 'Supporting' }[role] || role;
    }

    // Lays an <img> out so the crop region (in the rotated source's
    // fractions) fills the box; the image is expected to sit centred at its
    // natural aspect (max-width/max-height 100%). Returns nothing; writes the
    // element's transform and clip-path.
    function fitImage(img, crop, rotation, boxWidth, boxHeight, zoom) {
        var rot = normalRotation(rotation);
        var rotated = rot % 180 !== 0;
        var W = img.clientWidth, H = img.clientHeight;
        img.style.clipPath = '';
        if (!W || !H) {
            img.style.transform = 'rotate(' + rot + 'deg)';
            return;
        }
        var bw = rotated ? boxHeight : boxWidth;
        var bh = rotated ? boxWidth : boxHeight;
        var c = unrotate(crop || { left: 0, top: 0, width: 1, height: 1 }, rot);
        var k = Math.min(bw / (c.width * W), bh / (c.height * H));
        if (!Number.isFinite(k) || k <= 0) { k = 1; }
        var tx = -k * (c.left + c.width / 2 - .5) * W;
        var ty = -k * (c.top + c.height / 2 - .5) * H;
        if (!isFull(c)) {
            img.style.clipPath = 'inset(' + (c.top * 100) + '% ' + ((1 - c.left - c.width) * 100) + '% ' + ((1 - c.top - c.height) * 100) + '% ' + (c.left * 100) + '%)';
        }
        img.style.transform = 'rotate(' + rot + 'deg) ' + (zoom ? 'scale(2)' : 'translate(' + tx.toFixed(1) + 'px,' + ty.toFixed(1) + 'px) scale(' + k.toFixed(4) + ')');
    }

    // ---- Files tabs --------------------------------------------------------------
    // Both panels are rendered, so with no script the section is the lists one
    // after the other; script turns the strip on and shows one at a time. The
    // chosen tab rides in the URL hash (`#case-files-<tab>`), which the Custody
    // redirects name too, so a tag/untag/create lands back on Images.
    function bindFileTabs(root) {
        all(root, '[data-file-tabs-wrap]').forEach(function (wrap) {
            if (wrap.dataset.fileTabsBound === 'true') { return; }
            var strip = wrap.querySelector('[data-file-tabs]');
            var panels = all(wrap, '[data-file-tab-panel]');
            var buttons = strip ? all(strip, '[data-file-tab]') : [];
            if (!strip || !panels.length || !buttons.length) { return; }
            wrap.dataset.fileTabsBound = 'true';
            function show(name, remember) {
                panels.forEach(function (panel) { panel.hidden = panel.getAttribute('data-file-tab-panel') !== name; });
                buttons.forEach(function (button) { button.setAttribute('aria-selected', button.getAttribute('data-file-tab') === name ? 'true' : 'false'); });
                wrap.setAttribute('data-file-tabs-active', name);
                if (remember && window.history && window.history.replaceState) {
                    window.history.replaceState(null, '', '#case-files-' + name);
                }
            }
            buttons.forEach(function (button) {
                button.addEventListener('click', function () { show(button.getAttribute('data-file-tab'), true); });
            });
            strip.hidden = false;
            wrap.classList.add('is-tabbed');
            var fromHash = (window.location.hash || '').replace('#case-files-', '');
            show(buttons.some(function (button) { return button.getAttribute('data-file-tab') === fromHash; })
                ? fromHash
                : buttons[0].getAttribute('data-file-tab'), false);
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

    // ---- preparation staging ------------------------------------------------------
    // One state per image occurrence, seeded from the [data-preparation-card]
    // attributes and kept on the Case form element, so an in-place swap (a
    // new form) starts clean from what the server now holds.
    function saveForm() { return document.getElementById('case-edit-form'); }
    function staged() {
        var form = saveForm();
        if (!form) { return null; }
        return form.__pegasusPreparationStaged || (form.__pegasusPreparationStaged = {});
    }
    function cardsFor(id) {
        return all(document, '[data-preparation-card][data-preparation-occurrence="' + id + '"]');
    }
    function seed(card) {
        var store = staged();
        if (!store) { return null; }
        var id = card.getAttribute('data-preparation-occurrence');
        if (!store[id]) {
            var crop = {
                left: number(card.getAttribute('data-preparation-crop-left'), 0),
                top: number(card.getAttribute('data-preparation-crop-top'), 0),
                width: number(card.getAttribute('data-preparation-crop-width'), 1),
                height: number(card.getAttribute('data-preparation-crop-height'), 1)
            };
            var role = card.getAttribute('data-preparation-role') || 'NotUsed';
            store[id] = {
                id: id,
                version: number(card.getAttribute('data-preparation-version'), 0),
                role: role,
                previousRole: role === 'NotUsed' ? 'Supporting' : role,
                order: card.getAttribute('data-preparation-order') ? number(card.getAttribute('data-preparation-order'), null) : null,
                rotation: normalRotation(number(card.getAttribute('data-preparation-rotation'), 0)),
                fullPage: card.getAttribute('data-preparation-full-page') === 'true',
                crop: crop,
                stored: { rotation: normalRotation(number(card.getAttribute('data-preparation-rotation'), 0)), crop: { left: crop.left, top: crop.top, width: crop.width, height: crop.height } },
                preview: card.getAttribute('data-preparation-preview') || '',
                changed: false
            };
        }
        return store[id];
    }
    function get(id) {
        var store = staged();
        if (!store) { return null; }
        if (store[id]) { return store[id]; }
        var card = cardsFor(id)[0];
        return card ? seed(card) : null;
    }
    function writeHidden() {
        var form = saveForm();
        var store = staged();
        if (!form || !store) { return; }
        all(form, '[data-preparation-hidden]').forEach(function (element) { element.remove(); });
        var index = 0;
        Object.keys(store).forEach(function (id) {
            var value = store[id];
            if (!value.changed) { return; }
            [
                ['OccurrenceId', value.id], ['ExpectedPreparationVersion', value.version], ['Role', value.role],
                ['Order', value.role === 'Supporting' && value.order !== null ? value.order : ''], ['Rotation', value.rotation],
                ['CropLeft', round7(value.crop.left)], ['CropTop', round7(value.crop.top)],
                ['CropWidth', round7(value.crop.width)], ['CropHeight', round7(value.crop.height)],
                ['FullPage', value.fullPage ? 'true' : 'false']
            ].forEach(function (field) {
                var input = document.createElement('input');
                input.type = 'hidden';
                input.name = 'preparationEdits[' + index + '].' + field[0];
                input.value = field[1];
                input.setAttribute('data-preparation-hidden', '');
                form.appendChild(input);
            });
            index += 1;
        });
        // The frame's dirty guard listens for input on the form's controls.
        var marker = form.querySelector('input[name="editLeaseToken"]') || form;
        marker.dispatchEvent(new Event('input', { bubbles: true }));
    }
    function isStagedDifferent(value) {
        return value.rotation !== value.stored.rotation || !sameCrop(value.crop, value.stored.crop);
    }
    // Every tile, strip button and card that shows this occurrence follows the
    // staged state: the thumbnail already shows the saved region; a staged
    // change draws the full image with the new region over it.
    function sync(id) {
        var value = get(id);
        if (!value) { return; }
        cardsFor(id).forEach(function (card) {
            var role = card.querySelector('[data-preparation-role-select]');
            var order = card.querySelector('[data-preparation-order]');
            var roleLabelElement = card.querySelector('[data-preparation-role-label]');
            var rotationLabelElement = card.querySelector('[data-preparation-rotation-label]');
            var cropLabelElement = card.querySelector('[data-preparation-crop-label]');
            if (role) { role.value = value.role; }
            if (order) { order.value = value.order === null ? '' : value.order; order.disabled = value.role !== 'Supporting'; }
            // v28 P41 and P50: the tile's Full page flag and its order cell
            // follow the same staged state as the role.
            var orderCell = card.querySelector('[data-image-order-cell]');
            if (orderCell) { orderCell.hidden = value.role !== 'Supporting'; }
            var reportActionsAvailable = value.role !== 'NotUsed';
            var fullButton = card.querySelector('[data-image-full-page]');
            if (fullButton) {
                fullButton.hidden = !reportActionsAvailable;
                fullButton.setAttribute('aria-pressed', value.fullPage ? 'true' : 'false');
            }
            var removeButton = card.querySelector('[data-image-remove]');
            if (removeButton) { removeButton.hidden = !reportActionsAvailable; }
            var fullChip = card.querySelector('[data-image-full-chip]');
            if (fullChip) { fullChip.hidden = !value.fullPage; }
            if (roleLabelElement) { roleLabelElement.textContent = roleLabel(value.role); }
            if (rotationLabelElement) { rotationLabelElement.textContent = rotationLabel(value.rotation); }
            if (cropLabelElement) { cropLabelElement.textContent = cropLabel(value.crop); }
            var box = card.querySelector('[data-preparation-preview-box]');
            var image = card.querySelector('[data-preparation-preview-image]');
            if (box && image) { paintTile(box, image, value); }
        });
        all(document, '[data-evidence-preparation-occurrence="' + id + '"]').forEach(function (anchor) {
            var image = anchor.querySelector('img');
            if (image) { paintTile(anchor, image, value); }
            var badge = anchor.querySelector('.rot');
            if (badge) {
                badge.textContent = value.rotation ? value.rotation + '°' : '';
                badge.hidden = !value.rotation;
            }
        });
        all(document, '[data-report-image-toggle="' + id + '"]').forEach(function (tile) {
            tile.classList.toggle('off', value.role === 'NotUsed');
            var mark = tile.querySelector('.inc');
            if (mark) { mark.textContent = value.role === 'NotUsed' ? '–' : '✓'; }
        });
        if (viewer && viewer.open && viewer.current() && viewer.current().occurrence === id) {
            viewer.render();
        }
    }
    function paintTile(box, image, value) {
        if (!isStagedDifferent(value)) {
            box.classList.remove('is-staged');
            image.style.transform = '';
            image.style.clipPath = '';
            if (image.dataset.plainSrc && image.getAttribute('src') !== image.dataset.plainSrc) {
                image.setAttribute('src', image.dataset.plainSrc);
            }
            return;
        }
        if (!image.dataset.plainSrc) { image.dataset.plainSrc = image.getAttribute('src') || ''; }
        var full = value.preview || box.getAttribute('href') || image.dataset.plainSrc;
        box.classList.add('is-staged');
        var draw = function () { fitImage(image, value.crop, value.rotation, box.clientWidth, box.clientHeight, false); };
        if (image.getAttribute('src') !== full) {
            image.addEventListener('load', draw, { once: true });
            image.setAttribute('src', full);
        } else if (image.complete) {
            draw();
        } else {
            image.addEventListener('load', draw, { once: true });
        }
    }
    function set(id, patch) {
        var value = get(id);
        if (!value) { return null; }
        if (patch.role !== undefined) {
            if (value.role !== 'NotUsed') { value.previousRole = value.role; }
            value.role = patch.role;
            if (value.role !== 'Supporting') { value.order = null; }
        }
        if (patch.order !== undefined) { value.order = patch.order === null ? null : Math.max(1, Math.floor(number(patch.order, 1))); }
        // An image the report does not use never claims a page of its own.
        if (patch.fullPage !== undefined) { value.fullPage = !!patch.fullPage; }
        if (value.role === 'NotUsed') { value.fullPage = false; }
        if (patch.rotation !== undefined) { value.rotation = normalRotation(patch.rotation); }
        if (patch.crop !== undefined) {
            value.crop = {
                left: clamp(round7(patch.crop.left), 0, 1), top: clamp(round7(patch.crop.top), 0, 1),
                width: clamp(round7(patch.crop.width), 0, 1), height: clamp(round7(patch.crop.height), 0, 1)
            };
        }
        value.changed = true;
        sync(id);
        writeHidden();
        return value;
    }
    function toggleInReport(id, on) {
        var value = get(id);
        if (!value) { return; }
        set(id, { role: on ? (value.previousRole || 'Supporting') : 'NotUsed' });
        countImagesInReport();
    }
    window.pegasusCasePreparation = {
        get: get,
        set: set,
        openCrop: function (id) { if (viewer) { viewer.openCrop(id); } },
        toggleInReport: toggleInReport
    };

    // v28 P50: how many of the Case's images the report uses, under the grid.
    function countImagesInReport() {
        var line = document.querySelector('[data-image-report-count]');
        var grid = document.querySelector('[data-image-grid]');
        if (!line || !grid) { return; }
        var tiles = all(grid, '[data-image-tile]');
        var included = tiles.filter(function (tile) {
            if (!tile.hasAttribute('data-preparation-card')) { return false; }
            var staged = get(tile.getAttribute('data-preparation-occurrence'));
            return staged ? staged.role !== 'NotUsed' : tile.getAttribute('data-preparation-role') !== 'NotUsed';
        }).length;
        line.textContent = included + ' of ' + tiles.length + ' in report';
    }

    function bindPreparationCards(root) {
        all(root, '[data-preparation-card]').forEach(function (card) {
            if (card.dataset.preparationBound === 'true') { sync(card.getAttribute('data-preparation-occurrence')); return; }
            card.dataset.preparationBound = 'true';
            var value = seed(card);
            if (!value) { return; }
            var enhanced = card.querySelector('[data-image-report]');
            if (enhanced) { enhanced.hidden = false; }
            sync(value.id);
            var role = card.querySelector('[data-preparation-role-select]');
            var order = card.querySelector('[data-preparation-order]');
            if (role) { role.addEventListener('change', function () { set(value.id, { role: role.value }); countImagesInReport(); }); }
            if (order) { order.addEventListener('change', function () { set(value.id, { order: order.value === '' ? null : order.value }); }); }
            all(card, '[data-preparation-rotate]').forEach(function (button) {
                button.addEventListener('click', function () {
                    var current = get(value.id);
                    set(value.id, { rotation: current.rotation + number(button.getAttribute('data-preparation-rotate'), 0) });
                });
            });
            // v28 P41: Full page is a flag on an image the report uses;
            // Remove sets the role to Not used and the file stays on the Case,
            // so Undo simply puts the role back.
            var fullPage = card.querySelector('[data-image-full-page]');
            if (fullPage) {
                fullPage.addEventListener('click', function (event) {
                    event.preventDefault();
                    event.stopPropagation();
                    var current = get(value.id);
                    if (!current || current.role === 'NotUsed') { return; }
                    set(value.id, { fullPage: !current.fullPage });
                    countImagesInReport();
                });
            }
            var removeImage = card.querySelector('[data-image-remove]');
            if (removeImage) {
                removeImage.addEventListener('click', function (event) {
                    event.preventDefault();
                    event.stopPropagation();
                    var current = get(value.id);
                    if (!current || current.role === 'NotUsed') { return; }
                    var was = current.role;
                    var wasOrder = current.order;
                    var wasFullPage = current.fullPage;
                    set(value.id, { role: 'NotUsed', fullPage: false });
                    countImagesInReport();
                    if (window.pegasusUndoToast) {
                        window.pegasusUndoToast(
                            removeImage.getAttribute('data-undo-title') || 'Image removed',
                            function () {
                                set(value.id, { role: was, order: wasOrder, fullPage: wasFullPage });
                                countImagesInReport();
                            },
                            removeImage.getAttribute('data-undo-label'));
                    }
                });
            }
        });
        all(root, '[data-preparation-crop]').forEach(function (button) {
            if (button.dataset.cropBound === 'true') { return; }
            button.dataset.cropBound = 'true';
            button.addEventListener('click', function () {
                var owner = button.closest('[data-preparation-occurrence]');
                var id = button.getAttribute('data-preparation-crop-occurrence')
                    || (owner ? owner.getAttribute('data-preparation-occurrence') : null);
                if (id) { window.pegasusCasePreparation.openCrop(id); }
            });
        });
        // A report-strip tile toggles inclusion while editing (v26 § Image
        // viewer); the small view glyph opens the viewer instead.
        all(root, '[data-report-image-toggle]').forEach(function (tile) {
            if (tile.dataset.toggleBound === 'true') { return; }
            tile.dataset.toggleBound = 'true';
            var id = tile.getAttribute('data-report-image-toggle');
            tile.addEventListener('click', function (event) {
                if (event.target.closest('[data-tile-view], .th-view')) { return; }
                if (!get(id)) { return; }
                event.preventDefault();
                event.stopPropagation();
                toggleInReport(id, tile.classList.contains('off'));
            }, true);
        });
        all(root, '[data-tile-view], .th-view').forEach(function (button) {
            if (button.dataset.viewBound === 'true') { return; }
            button.dataset.viewBound = 'true';
            button.addEventListener('click', function (event) {
                event.preventDefault();
                event.stopPropagation();
                var item = button.closest('[data-evidence-item]');
                if (item && viewer) { viewer.open(item); }
            });
        });
        // Tiles for occurrences whose state was seeded elsewhere (the Damage
        // strip, the Report strip) follow the staged state too.
        all(root, '[data-evidence-preparation-occurrence]').forEach(function (anchor) {
            var id = anchor.getAttribute('data-evidence-preparation-occurrence');
            if (id && get(id)) { sync(id); }
        });
    }
    window.addEventListener('resize', function () {
        var store = staged();
        if (!store) { return; }
        Object.keys(store).forEach(function (id) { if (isStagedDifferent(store[id])) { sync(id); } });
    });

    // ---- the viewer -------------------------------------------------------------
    var viewer = null;
    function previewKind(value) {
        var type = String(value || '').split(';')[0].trim().toLowerCase();
        if (type.indexOf('image/') === 0 && type !== 'image/svg+xml') { return 'image'; }
        if (type === 'application/pdf') { return 'document'; }
        return type === 'video/mp4' || type === 'video/quicktime' ? 'video' : '';
    }
    function buildViewer(host) {
        var image = host.querySelector('[data-viewer-image]');
        var frame = host.querySelector('[data-viewer-document]');
        var video = host.querySelector('[data-viewer-video]');
        var stage = host.querySelector('[data-viewer-stage]');
        var name = host.querySelector('[data-viewer-name]');
        var tag = host.querySelector('[data-viewer-tag]');
        var position = host.querySelector('[data-viewer-position]');
        var viewTools = host.querySelector('[data-viewer-view-tools]');
        var cropTools = host.querySelector('[data-viewer-crop-tools]');
        var rotateButton = host.querySelector('[data-viewer-rotate]');
        var zoomButton = host.querySelector('[data-viewer-zoom]');
        var zoomLabel = host.querySelector('[data-viewer-zoom-label]');
        var cropButton = host.querySelector('[data-viewer-crop]');
        var download = host.querySelector('[data-viewer-download]');
        var downloadLabel = host.querySelector('[data-viewer-download-label]');
        var inReportWrap = host.querySelector('[data-viewer-in-report-wrap]');
        var inReport = host.querySelector('[data-viewer-in-report]');
        var cropStatus = host.querySelector('[data-viewer-crop-status]');
        var aspect = host.querySelector('[data-viewer-aspect]');
        var strip = host.querySelector('[data-viewer-strip]');
        var downloadDefault = downloadLabel ? downloadLabel.textContent : 'Download';

        var state = { open: false, items: [], index: 0, rotation: 0, zoom: false, invoker: null, crop: null, kind: '' };
        // The crop editor's working copy: rotation and the selection in the
        // rotated source's fractions, or null for the whole frame.
        var crop = { id: null, rotation: 0, selection: null, aspect: 'free', drag: null, layer: null, box: null };

        function current() { return state.items[state.index] || null; }
        function itemFrom(trigger, extra) {
            var id = trigger.getAttribute('data-evidence-preparation-occurrence') || '';
            var img = trigger.querySelector('img');
            return {
                href: trigger.getAttribute('href') || '',
                downloadHref: trigger.getAttribute('data-download-href') || trigger.getAttribute('href') || '',
                mediaType: trigger.getAttribute('data-media-type') || '',
                name: trigger.getAttribute('data-file-name') || '',
                thumb: trigger.getAttribute('data-thumb') || (img ? img.getAttribute('src') : '') || '',
                tag: trigger.getAttribute('data-tag') || '',
                occurrence: id,
                excluded: trigger.classList.contains('off'),
                downloadLabel: extra && extra.downloadLabel ? extra.downloadLabel : downloadDefault,
                element: trigger
            };
        }
        function preparable(item) { return !!(item && item.occurrence && get(item.occurrence)); }

        function focusable() {
            return all(host, 'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])').filter(function (element) {
                return !element.disabled && !element.hidden && element.getClientRects().length > 0 && !element.closest('[hidden]');
            });
        }
        function stageBox() {
            return { width: stage.clientWidth - 28, height: stage.clientHeight - 28 };
        }
        function fit() {
            var item = current();
            if (!item || state.kind !== 'image') { return; }
            var box = stageBox();
            var rotated = state.rotation % 180 !== 0;
            image.style.maxWidth = (rotated ? stage.clientHeight : stage.clientWidth) - 28 + 'px';
            image.style.maxHeight = (rotated ? stage.clientWidth : stage.clientHeight) - 28 + 'px';
            if (state.crop) {
                image.style.clipPath = '';
                image.style.transform = 'rotate(' + state.rotation + 'deg)';
                placeLayer();
                return;
            }
            var value = preparable(item) ? get(item.occurrence) : null;
            fitImage(image, value ? value.crop : null, state.rotation, box.width, box.height, state.zoom);
        }
        function render() {
            var item = current();
            if (!item) { return; }
            var kind = item.kind || previewKind(item.mediaType);
            state.kind = kind;
            name.textContent = item.name;
            var value = preparable(item) ? get(item.occurrence) : null;
            tag.textContent = item.tag || '';
            tag.hidden = !item.tag;
            position.textContent = state.items.length > 1 ? (state.index + 1) + ' of ' + state.items.length : '';
            image.hidden = kind !== 'image';
            frame.hidden = kind !== 'document';
            video.hidden = kind !== 'video';
            stage.classList.toggle('is-zoomed', state.zoom && kind === 'image' && !state.crop);
            stage.classList.toggle('is-cropping', !!state.crop);
            stage.setAttribute('aria-busy', 'true');
            if (kind === 'image') {
                frame.removeAttribute('src'); video.removeAttribute('src');
                image.alt = item.name;
                if (image.getAttribute('src') !== item.href) {
                    image.addEventListener('load', function () { stage.removeAttribute('aria-busy'); fit(); }, { once: true });
                    image.setAttribute('src', item.href);
                } else { stage.removeAttribute('aria-busy'); }
                image.style.clipPath = '';
                image.style.transform = '';
                fit();
                window.requestAnimationFrame(fit);
            } else if (kind === 'document') {
                image.removeAttribute('src'); video.removeAttribute('src');
                frame.title = item.name;
                frame.addEventListener('load', function () { stage.removeAttribute('aria-busy'); }, { once: true });
                frame.src = item.href;
            } else if (kind === 'video') {
                image.removeAttribute('src'); frame.removeAttribute('src');
                video.addEventListener('loadedmetadata', function () { stage.removeAttribute('aria-busy'); }, { once: true });
                video.src = item.href;
                video.load();
            } else {
                stage.removeAttribute('aria-busy');
            }
            download.href = item.downloadHref;
            download.setAttribute('download', item.name);
            if (downloadLabel) { downloadLabel.textContent = item.downloadLabel || downloadDefault; }
            rotateButton.hidden = kind !== 'image';
            zoomButton.hidden = kind !== 'image';
            if (zoomLabel) { zoomLabel.textContent = state.zoom ? 'Fit' : 'Zoom'; }
            cropButton.hidden = kind !== 'image' || !value;
            inReportWrap.hidden = kind !== 'image' || !value;
            if (value) { inReport.checked = value.role !== 'NotUsed'; }
            viewTools.hidden = !!state.crop;
            cropTools.hidden = !state.crop;
            host.querySelector('[data-viewer-prev]').disabled = state.items.length < 2;
            host.querySelector('[data-viewer-next]').disabled = state.items.length < 2;
            renderStrip();
            if (state.crop) { renderCropTools(); }
        }
        function renderStrip() {
            strip.innerHTML = '';
            if (state.items.length < 2) { strip.hidden = true; return; }
            strip.hidden = false;
            state.items.forEach(function (item, at) {
                var button = document.createElement('button');
                button.type = 'button';
                var value = preparable(item) ? get(item.occurrence) : null;
                var excluded = value ? value.role === 'NotUsed' : item.excluded;
                button.className = (at === state.index ? 'on' : '') + (excluded ? ' off' : '');
                button.setAttribute('aria-label', item.name);
                button.title = item.name;
                var kind = item.kind || previewKind(item.mediaType);
                if (kind === 'image' && item.thumb) {
                    var thumb = document.createElement('img');
                    thumb.src = item.thumb;
                    thumb.alt = '';
                    button.appendChild(thumb);
                } else {
                    button.classList.add('doc');
                    button.textContent = item.name;
                }
                button.addEventListener('click', function () { go(at); });
                strip.appendChild(button);
            });
            var on = strip.querySelector('.on');
            if (on) { strip.scrollLeft = Math.max(0, on.offsetLeft - strip.clientWidth / 2 + 42); }
        }
        function go(at) {
            if (state.crop) { cancelCrop(); }
            state.index = (at + state.items.length) % state.items.length;
            state.zoom = false;
            var item = current();
            var value = item && preparable(item) ? get(item.occurrence) : null;
            state.rotation = value ? value.rotation : 0;
            render();
        }
        function step(offset) { if (state.items.length > 1) { go(state.index + offset); } }

        function open(trigger, extra) {
            var set = trigger.closest('[data-evidence-set]');
            state.items = (set ? all(set, '[data-evidence-item]') : [trigger])
                .filter(function (candidate) { return previewKind(candidate.getAttribute('data-media-type')) !== ''; })
                .map(function (candidate) { return itemFrom(candidate, extra); });
            var start = state.items.findIndex(function (item) { return item.element === trigger; });
            state.invoker = trigger;
            show(start < 0 ? 0 : start);
        }
        function openDocument(options) {
            state.items.forEach(function (item) { if (item.revoke) { URL.revokeObjectURL(item.revoke); } });
            state.items = [{
                href: options.href, downloadHref: options.download || options.href, mediaType: 'application/pdf', kind: 'document',
                name: options.name || '', thumb: '', tag: '', occurrence: '', excluded: false,
                downloadLabel: options.downloadLabel || downloadDefault, element: null, revoke: options.revoke || ''
            }];
            state.invoker = options.invoker || document.activeElement;
            show(0);
        }
        function show(at) {
            state.open = true;
            state.crop = null;
            host.hidden = false;
            document.body.classList.add('has-viewer');
            document.addEventListener('keydown', onKeydown, true);
            go(at);
            try { host.focus({ preventScroll: true }); } catch (_) { host.focus(); }
        }
        function close() {
            if (state.crop) { cancelCrop(); }
            state.open = false;
            host.hidden = true;
            document.body.classList.remove('has-viewer');
            document.removeEventListener('keydown', onKeydown, true);
            image.removeAttribute('src'); frame.removeAttribute('src'); video.removeAttribute('src'); video.load();
            state.items.forEach(function (item) { if (item.revoke) { URL.revokeObjectURL(item.revoke); item.revoke = ''; } });
            image.style.transform = ''; image.style.clipPath = '';
            stage.removeAttribute('aria-busy');
            if (state.invoker && typeof state.invoker.focus === 'function') { state.invoker.focus(); }
        }
        function rotate() {
            if (state.kind !== 'image' || state.crop) { return; }
            state.rotation = normalRotation(state.rotation + 90);
            render();
        }
        function zoom() {
            if (state.kind !== 'image' || state.crop) { return; }
            state.zoom = !state.zoom;
            render();
        }

        // ---- crop on the stage ----
        function beginCrop() {
            var item = current();
            if (!item || !preparable(item) || state.kind !== 'image') { return; }
            var value = get(item.occurrence);
            crop.id = item.occurrence;
            crop.rotation = value.rotation;
            crop.selection = isFull(value.crop) ? null : { left: value.crop.left, top: value.crop.top, width: value.crop.width, height: value.crop.height };
            crop.aspect = 'free';
            crop.drag = null;
            state.rotation = crop.rotation;
            state.zoom = false;
            state.crop = crop;
            render();
        }
        function cancelCrop() {
            if (!state.crop) { return; }
            removeLayer();
            state.crop = null;
            var value = get(crop.id);
            state.rotation = value ? value.rotation : 0;
            render();
        }
        function saveCrop() {
            if (!state.crop) { return; }
            var selection = crop.selection && crop.selection.width > .02 && crop.selection.height > .02
                ? crop.selection
                : { left: 0, top: 0, width: 1, height: 1 };
            var id = crop.id;
            removeLayer();
            state.crop = null;
            set(id, { rotation: crop.rotation, crop: selection });
            state.rotation = crop.rotation;
            render();
            if (typeof window.pegasusToast === 'function') { window.pegasusToast('The crop was staged. Save the Case to keep it.'); }
        }
        function cropRotate(degrees) {
            if (!state.crop) { return; }
            if (crop.selection) { crop.selection = turnRect(crop.selection, degrees); }
            crop.rotation = normalRotation(crop.rotation + degrees);
            state.rotation = crop.rotation;
            render();
        }
        function cropFull() { if (state.crop) { crop.selection = null; drawSelection(); renderCropTools(); } }
        function cropReset() {
            if (!state.crop) { return; }
            crop.selection = null;
            crop.rotation = 0;
            crop.aspect = 'free';
            if (aspect) { aspect.value = 'free'; }
            state.rotation = 0;
            render();
        }
        function cropAspect(value) {
            crop.aspect = value;
            if (value !== 'free' && crop.selection && crop.layer) {
                var lw = crop.layer.clientWidth || 4, lh = crop.layer.clientHeight || 3;
                var d = crop.selection;
                d.height = d.width * lw / (parseFloat(value) * lh);
                if (d.top + d.height > 1) { d.height = 1 - d.top; d.width = d.height * parseFloat(value) * lh / lw; }
                crop.selection = d;
            }
            drawSelection();
            renderCropTools();
        }
        function renderCropTools() {
            if (cropStatus) {
                cropStatus.textContent = 'Rotation ' + rotationLabel(crop.rotation) + ' · Crop ' + cropLabel(crop.selection || { left: 0, top: 0, width: 1, height: 1 });
            }
            if (aspect && aspect.value !== crop.aspect) { aspect.value = crop.aspect; }
        }
        function placeLayer() {
            if (!state.crop) { return; }
            if (!crop.layer) {
                var layer = document.createElement('div');
                layer.className = 'crop-layer';
                layer.setAttribute('data-viewer-crop-layer', '');
                var selection = document.createElement('div');
                selection.className = 'crop-sel';
                selection.setAttribute('data-viewer-crop-selection', '');
                ['nw', 'ne', 'sw', 'se'].forEach(function (handle) {
                    var element = document.createElement('span');
                    element.className = 'crop-handle crop-handle--' + handle;
                    element.setAttribute('data-crop-handle', handle);
                    selection.appendChild(element);
                });
                layer.appendChild(selection);
                stage.appendChild(layer);
                bindLayer(layer);
                crop.layer = layer;
                crop.box = selection;
            }
            var r = image.getBoundingClientRect();
            var s = stage.getBoundingClientRect();
            crop.layer.style.left = (r.left - s.left) + 'px';
            crop.layer.style.top = (r.top - s.top) + 'px';
            crop.layer.style.width = r.width + 'px';
            crop.layer.style.height = r.height + 'px';
            drawSelection();
        }
        function removeLayer() {
            if (crop.layer) { crop.layer.remove(); }
            crop.layer = null;
            crop.box = null;
            crop.drag = null;
        }
        function drawSelection() {
            if (!crop.box) { return; }
            var d = crop.selection || { left: 0, top: 0, width: 1, height: 1 };
            crop.box.hidden = false;
            crop.box.style.left = (d.left * 100) + '%';
            crop.box.style.top = (d.top * 100) + '%';
            crop.box.style.width = (d.width * 100) + '%';
            crop.box.style.height = (d.height * 100) + '%';
            crop.box.classList.toggle('is-full', !crop.selection);
            renderCropTools();
        }
        function bindLayer(layer) {
            function point(event) {
                var r = layer.getBoundingClientRect();
                return [clamp((event.clientX - r.left) / r.width, 0, 1), clamp((event.clientY - r.top) / r.height, 0, 1)];
            }
            function clampRect(d) {
                d.width = Math.min(d.width, 1); d.height = Math.min(d.height, 1);
                d.left = clamp(d.left, 0, 1 - d.width); d.top = clamp(d.top, 0, 1 - d.height);
                return d;
            }
            layer.addEventListener('pointerdown', function (event) {
                var p = point(event);
                var sel = crop.selection;
                var handle = event.target.getAttribute && event.target.getAttribute('data-crop-handle');
                layer.setPointerCapture(event.pointerId);
                if (handle && sel) {
                    crop.drag = { mode: 'resize', ax: handle.indexOf('w') >= 0 ? sel.left + sel.width : sel.left, ay: handle.indexOf('n') >= 0 ? sel.top + sel.height : sel.top };
                } else if (sel && p[0] >= sel.left && p[0] <= sel.left + sel.width && p[1] >= sel.top && p[1] <= sel.top + sel.height) {
                    crop.drag = { mode: 'move', ox: p[0] - sel.left, oy: p[1] - sel.top, width: sel.width, height: sel.height };
                } else {
                    crop.drag = { mode: 'draw', sx: p[0], sy: p[1] };
                    crop.selection = null;
                }
                event.preventDefault();
                event.stopPropagation();
            });
            layer.addEventListener('pointermove', function (event) {
                var g = crop.drag;
                if (!g) { return; }
                var p = point(event);
                var d;
                if (g.mode === 'move') {
                    d = { left: p[0] - g.ox, top: p[1] - g.oy, width: g.width, height: g.height };
                } else {
                    var ax = g.mode === 'draw' ? g.sx : g.ax, ay = g.mode === 'draw' ? g.sy : g.ay;
                    d = { left: Math.min(ax, p[0]), top: Math.min(ay, p[1]), width: Math.abs(p[0] - ax), height: Math.abs(p[1] - ay) };
                    if (crop.aspect !== 'free') {
                        var A = parseFloat(crop.aspect), lw = layer.clientWidth, lh = layer.clientHeight;
                        d.height = d.width * lw / (A * lh);
                        if (p[1] < ay) { d.top = ay - d.height; }
                    }
                }
                crop.selection = clampRect(d);
                drawSelection();
            });
            var end = function () {
                if (!crop.drag) { return; }
                crop.drag = null;
                if (crop.selection && (crop.selection.width < .02 || crop.selection.height < .02)) { crop.selection = null; }
                drawSelection();
            };
            layer.addEventListener('pointerup', end);
            layer.addEventListener('pointercancel', end);
            layer.addEventListener('click', function (event) { event.stopPropagation(); });
        }

        function onKeydown(event) {
            if (!state.open) { return; }
            if (event.key === 'Escape') {
                event.preventDefault();
                event.stopImmediatePropagation();
                if (state.crop) { cancelCrop(); } else { close(); }
                return;
            }
            if (event.key === 'Tab') {
                var controls = focusable();
                if (!controls.length) { return; }
                var first = controls[0], last = controls[controls.length - 1];
                if (event.shiftKey && (document.activeElement === first || document.activeElement === host)) {
                    event.preventDefault(); last.focus();
                } else if (!event.shiftKey && document.activeElement === last) {
                    event.preventDefault(); first.focus();
                }
                return;
            }
            var inField = event.target && event.target.closest && event.target.closest('input, select, textarea');
            if (event.key === 'Enter' && state.crop && crop.selection) { event.preventDefault(); saveCrop(); return; }
            if (state.crop || inField) { return; }
            if (event.key === 'ArrowRight') { event.preventDefault(); step(1); }
            else if (event.key === 'ArrowLeft') { event.preventDefault(); step(-1); }
            else if (event.key === 'r' || event.key === 'R') { rotate(); }
            else if (event.key === 'z' || event.key === 'Z') { zoom(); }
            else if ((event.key === 'c' || event.key === 'C') && !cropButton.hidden) { beginCrop(); }
        }

        host.querySelector('[data-viewer-close]').addEventListener('click', close);
        host.querySelector('[data-viewer-prev]').addEventListener('click', function () { step(-1); });
        host.querySelector('[data-viewer-next]').addEventListener('click', function () { step(1); });
        rotateButton.addEventListener('click', rotate);
        zoomButton.addEventListener('click', zoom);
        cropButton.addEventListener('click', beginCrop);
        stage.addEventListener('click', function (event) {
            if (event.target === stage || event.target === image) { if (!state.crop) { zoom(); } }
        });
        inReport.addEventListener('change', function () {
            var item = current();
            if (item && preparable(item)) { toggleInReport(item.occurrence, inReport.checked); }
        });
        if (aspect) { aspect.addEventListener('change', function () { cropAspect(aspect.value); }); }
        all(host, '[data-viewer-crop-rotate]').forEach(function (button) {
            button.addEventListener('click', function () { cropRotate(number(button.getAttribute('data-viewer-crop-rotate'), 90)); });
        });
        host.querySelector('[data-viewer-crop-full]').addEventListener('click', cropFull);
        host.querySelector('[data-viewer-crop-reset]').addEventListener('click', cropReset);
        host.querySelector('[data-viewer-crop-save]').addEventListener('click', saveCrop);
        host.querySelector('[data-viewer-crop-cancel]').addEventListener('click', cancelCrop);
        host.addEventListener('click', function (event) { if (event.target === host) { close(); } });
        window.addEventListener('resize', function () { if (state.open) { fit(); } });

        return {
            get open() { return state.open; },
            current: current,
            render: render,
            open: open,
            openDocument: openDocument,
            close: close,
            openCrop: function (id) {
                var trigger = document.querySelector('[data-evidence-item][data-evidence-preparation-occurrence="' + id + '"]')
                    || document.querySelector('[data-preparation-card][data-preparation-occurrence="' + id + '"] [data-evidence-item]');
                if (trigger) {
                    if (!state.open || !current() || current().occurrence !== id) { open(trigger); }
                    beginCrop();
                    return;
                }
                // No tile on the page for it (a card only): open the card's own preview.
                var card = cardsFor(id)[0];
                var value = get(id);
                if (!card || !value) { return; }
                state.items = [{
                    href: value.preview, downloadHref: value.preview, mediaType: 'image/jpeg', kind: 'image',
                    name: (card.querySelector('h3') || {}).textContent || '', thumb: '', tag: '', occurrence: id, excluded: value.role === 'NotUsed',
                    downloadLabel: downloadDefault, element: null
                }];
                state.invoker = document.activeElement;
                show(0);
                beginCrop();
            }
        };
    }
    function bindViewer(root) {
        var host = root.matches && root.matches('[data-case-viewer]')
            ? root
            : root.querySelector('[data-case-viewer]');
        if (host && host.dataset.viewerBound !== 'true') {
            host.dataset.viewerBound = 'true';
            viewer = buildViewer(host);
            window.pegasusCaseViewer = {
                open: function (trigger) { viewer.open(trigger); },
                openDocument: function (options) { viewer.openDocument(options); },
                close: function () { viewer.close(); }
            };
        }
        all(root, '[data-evidence-item]').forEach(function (trigger) {
            if (trigger.dataset.caseViewerBound === 'true') { return; }
            trigger.dataset.caseViewerBound = 'true';
            trigger.addEventListener('click', function (event) {
                if (!viewer || !previewKind(trigger.getAttribute('data-media-type'))) { return; }
                if (event.target.closest('[data-tile-view], .th-view')) { return; }
                event.preventDefault();
                viewer.open(trigger);
            });
        });
    }

    function bind(root) {
        bindViewer(root);
        bindFileTabs(root);
        bindPreparationCards(root);
    }
    bind(document);
    (window.pegasusMountBinders = window.pegasusMountBinders || []).push(bind);
})();
