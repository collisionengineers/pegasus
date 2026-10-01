// Case record behaviour (v26). Everything here is a convenience on top of
// markup that already works as plain forms and links; nothing creates a
// separate mutation path and no business rule lives in the browser.
//
// Blocks, in order:
//   frame       sticky measure, Scroll/Tabs, lazy section bodies, the
//               section nav, in-place actions (fetch + swap), the edit
//               session (save as you go, heartbeat, expiry)
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
    var submitting = false;
    // Save as you go (operator, 29 September 2026): a change to the Case
    // form is committed through the one Save handler as soon as it is made,
    // at once for a simple cell and after a short idle for a composite
    // editor, and the page keeps editing. One commit is in flight at a time;
    // a change made during it commits again when it lands, and whatever
    // waits on the queue (an action, a link away) runs once it is empty.
    var COMMIT_IDLE_MS = 1000;
    var commitTimer = null;
    var commitPending = false;
    var commitInFlight = false;
    var commitWaiters = [];
    var commitStatus = null;
    // One editor: the record's Save form. The Repair Spec and the valuation
    // calculator are controls of it.
    var editorLabels = {
        'case-edit-form': 'Case'
    };
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
    // A report blocker's Edit (issue 898) lands on the control that clears
    // it, once edit mode has drawn that control.
    function focusControl(key, selector) {
        var navigation;
        function land(host) {
            if (navigation !== navigationVersion) {
                return;
            }
            var control = host.querySelector(selector);
            if (!control) {
                return;
            }
            control.scrollIntoView({ block: 'center' });
            try { control.focus({ preventScroll: true }); } catch (_) { control.focus(); }
        }
        if (layout === 'tabs') {
            selectTab(key);
        }
        // As jumpTo: a later navigation wins over a section still mounting.
        navigation = ++navigationVersion;
        var target = sectionFor(key);
        if (!target) {
            return;
        }
        if (target.hasAttribute('data-lazy')) {
            mount(target, land);
            return;
        }
        land(target);
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

    // ---- the edit session: save as you go, heartbeat, expiry ---------------
    function caseForm() { return document.getElementById('case-edit-form'); }
    function editorFor(control) {
        var form = control.form || (control.closest ? control.closest('form') : null);
        return form && editorLabels[form.getAttribute('id')] ? form : null;
    }
    // A composite editor commits when focus leaves it or after an idle: its
    // parts are typed one after another, and it announces a change by
    // dispatching input on a hidden control, never change.
    var COMPOSITE = '[data-estimate-form], [data-damage-editor], [data-valuation-form], [data-valuation-card], [data-report-wording]';
    function compositeOf(control) {
        return control.closest ? control.closest(COMPOSITE) : null;
    }
    function queueBusy() {
        return submitting || commitTimer !== null || commitPending;
    }
    function scheduleCommit() {
        if (commitTimer !== null) { window.clearTimeout(commitTimer); }
        commitTimer = window.setTimeout(function () { commitTimer = null; commitNow(); }, COMMIT_IDLE_MS);
    }
    // A commit is the Case form's own submit, so everything a submit means
    // (the scale preview putting its cells back, the browser's own checks,
    // the in-place post) holds. A value the browser cannot accept is sent
    // nowhere: the status word says so, and what waited on the commit does
    // not run. Only a commit the operator asked for (Ctrl S, an action or a
    // link that waits on it) points at the value; an idle leaves focus alone.
    function commitNow(explicit) {
        if (commitTimer !== null) { window.clearTimeout(commitTimer); commitTimer = null; }
        var form = caseForm();
        if (!form || record.getAttribute('data-case-editing') !== 'true') { settleQueue(); return; }
        if (submitting) { commitPending = true; return; }
        if (!form.checkValidity()) {
            var invalid = Array.prototype.find.call(form.elements, function (control) {
                return control.willValidate && !control.validity.valid;
            });
            if (explicit) { form.reportValidity(); }
            setCommitStatus('refused', 'The change was not saved: ' + (invalid ? invalid.validationMessage : 'a value cannot be saved.'));
            abandonWaiters();
            return;
        }
        form.requestSubmit();
    }
    // Runs once nothing is in flight or waiting to commit. What waits reads
    // the Case as the operator has it, so it runs only once the change has
    // landed: a commit that is refused, cannot be sent or cannot be checked
    // abandons it (its abandon, when given, undoes what it began), and the
    // change stays on the page.
    function afterCommit(next, abandon) {
        if (!queueBusy()) { next(); return; }
        commitWaiters.push({ run: next, abandon: abandon });
        if (commitTimer !== null) { commitNow(true); }
    }
    function settleQueue() {
        if (submitting) { return; }
        if (commitPending) { commitPending = false; commitNow(); return; }
        if (commitTimer !== null) { return; }
        var waiters = commitWaiters;
        commitWaiters = [];
        waiters.forEach(function (waiter) { waiter.run(); });
    }
    function abandonWaiters() {
        var waiters = commitWaiters;
        commitWaiters = [];
        waiters.forEach(function (waiter) { if (waiter.abandon) { waiter.abandon(); } });
    }
    document.addEventListener('change', function (event) {
        var control = event.target;
        if (!editorFor(control)) { return; }
        if (compositeOf(control)) { scheduleCommit(); return; }
        // A simple cell commits as it is left. A value the browser cannot
        // accept is said so there and then, and nothing is sent.
        if (typeof control.reportValidity === 'function' && !control.reportValidity()) { return; }
        commitNow();
    });
    document.addEventListener('input', function (event) {
        var control = event.target;
        if (!editorFor(control)) { return; }
        // Typing waits for the cell to be left; a script's fill (the damage
        // plan, a bank wording, a slider's figure) and a composite's typing
        // commit after the idle.
        if (!event.isTrusted || control.type === 'hidden' || compositeOf(control)) { scheduleCommit(); }
    });
    document.addEventListener('focusout', function (event) {
        if (commitTimer === null) { return; }
        var host = compositeOf(event.target);
        if (host && !(event.relatedTarget && host.contains(event.relatedTarget))) { commitNow(); }
    });
    // Closing the tab with a change not yet sent, or sent but not confirmed:
    // the change goes by beacon, and the lease lapses by server time
    // (FRD-14). A beacon behind a commit that did land carries the same
    // operation key, so the server answers it as a replay.
    window.addEventListener('pagehide', function () {
        if (commitTimer === null && !commitPending && !commitInFlight) { return; }
        var form = caseForm();
        if (form && typeof navigator.sendBeacon === 'function') {
            navigator.sendBeacon(form.getAttribute('action') || window.location.href, new FormData(form));
        }
    });
    // The ribbon's status word: what the last commit did.
    function setCommitStatus(kind, text) {
        commitStatus = kind ? { kind: kind, text: text } : null;
        paintCommitStatus();
    }
    function paintCommitStatus() {
        var line = record.querySelector('[data-lease-line]');
        if (!line || line.classList.contains('is-expiring')) { return; }
        line.classList.remove('is-refused');
        if (!commitStatus) { line.hidden = true; line.textContent = ''; return; }
        var words = commitStatus.kind === 'saving' ? line.getAttribute('data-lease-saving-text')
            : commitStatus.kind === 'saved' ? (line.getAttribute('data-lease-saved-text') || '') + ' ' + commitStatus.text
            : commitStatus.text;
        line.textContent = words || '';
        line.classList.toggle('is-refused', commitStatus.kind === 'refused');
        line.hidden = !words;
    }
    function clockNow() {
        var now = new Date();
        return (now.getHours() < 10 ? '0' : '') + now.getHours() + ':' + (now.getMinutes() < 10 ? '0' : '') + now.getMinutes();
    }
    // With script the ribbon's Save now is the commit's own submit, reached
    // by Ctrl S and by Enter in a cell; without script it is the save. It is
    // drawn and hidden here, as the estimate import's fallback is.
    function bindEditControls() {
        var saveNow = record.querySelector('[data-case-save-now]');
        if (saveNow) { saveNow.hidden = true; }
        paintCommitStatus();
    }

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
                // 409 and 403 are the server refusing the lease itself, and 404 is
                // a Case that no longer exists (a stale tab after a wipe). Anything
                // else - a faulted request, a replica restarting - says nothing
                // about the lease, and the next beat settles it.
                if (response.status === 409 || response.status === 403 || response.status === 404) {
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

    // The toasts the notices now on the page ask for: what was done, work not
    // yet finished, and a refusal the server rendered.
    function announceNotices() {
        var confirmation = document.querySelector('[data-case-notices] [data-confirmation]');
        if (confirmation && typeof window.pegasusToast === 'function') {
            var text = confirmation.querySelector('span');
            if (text) { window.pegasusToast(text.textContent.trim()); }
        }
        // Work the server reports as not yet finished is announced in amber.
        var warning = document.querySelector('[data-case-notices] [data-case-warning]');
        if (warning && typeof window.pegasusToast === 'function') {
            var warningText = warning.querySelector('span');
            if (warningText) { window.pegasusToast(warningText.textContent.trim(), 'warning'); }
        }
        // Only a refusal the server rendered into the swapped-in notices;
        // showActionError has already toasted its own [data-inplace-error].
        var alertNotice = document.querySelector('[data-case-notices] [role="alert"]:not([data-inplace-error])');
        if (alertNotice && typeof window.pegasusToast === 'function') {
            var alertText = alertNotice.textContent.trim();
            if (alertText) { window.pegasusToast(alertText, 'danger'); }
        }
    }

    var swapRoots = ['[data-case-notices]', '[data-case-ribbon-facts]', '[data-case-ribbon-actions]', '#case-main', '[data-case-aside]', '[data-case-dialogs]', '[data-case-viewer-host]'];
    // The inputs a commit consumes or moves on (its authority, the valuation
    // calculation the page opened on) are copied from the response's Save
    // form into the one the operator keeps typing in, so the next commit
    // carries the Case's new version, lease and operation key. An input may
    // sit in its section and belong to the form by its form attribute (the
    // valuation's opening calculation), so both sides go by ownership. The
    // form's id is read as its attribute: the form posts a control named id,
    // which form.id would return instead.
    function carryForward(parsed) {
        var form = caseForm();
        var id = form ? form.getAttribute('id') : null;
        if (!form || !parsed.getElementById(id)) { return; }
        var owned = '#' + id + ' [data-carry-forward], [data-carry-forward][form="' + id + '"]';
        Array.prototype.forEach.call(parsed.querySelectorAll(owned), function (input) {
            var current = form.elements.namedItem(input.name);
            if (current && current.hasAttribute && current.hasAttribute('data-carry-forward')) { current.value = input.value; }
        });
    }
    function swap(html, command, preferred) {
        glassRefreshGeneration += 1;
        var parsed = new DOMParser().parseFromString(html, 'text/html');
        var incoming = parsed.querySelector('[data-case-record]');
        if (!incoming) {
            return false;
        }
        var commit = null;
        try { commit = JSON.parse(incoming.getAttribute('data-editor-commit') || 'null'); } catch (_) { /* An unrecognised response cannot confirm a commit. */ }
        var confirmed = command && commit && commit.editor === command.editor
            && commit.operationKey === command.operationKey && String(commit.expectedVersion) === command.expectedVersion;
        var mayAdvance = confirmed && String(commit.version) === incoming.getAttribute('data-case-version')
            && incoming.getAttribute('data-case-editing') === 'true';
        var isCommit = !!command && command.editor === 'case-edit-form'
            && record.getAttribute('data-case-editing') === 'true';
        // A commit that landed keeps every section as the operator has it and
        // redraws what the Case's new facts change: the notices, the ribbon,
        // the aside and the dialogs. A refused commit redraws only the notices
        // and the aside, so the typed value stays for another go. Anything
        // else, a commit whose session ended included, is the whole record as
        // the server now draws it.
        var commitLanded = isCommit && mayAdvance;
        var commitRefused = isCommit && !confirmed;
        var keepSections = commitLanded || commitRefused;
        // The section a head Edit was pressed on keeps its place; any other
        // swap keeps the section at the reading line.
        var saved = preferred || anchor();
        if (commitLanded) {
            var newLease = incoming.querySelector('[name="editLeaseToken"]');
            Array.prototype.forEach.call(document.querySelectorAll('form'), function (form) {
                // A Case's own forms name the version expectedVersion; the
                // Glass's and report forms name it expectedCaseVersion.
                var version = form.querySelector('[name="expectedVersion"], [name="expectedCaseVersion"]');
                var lease = form.querySelector('[name="editLeaseToken"]');
                if (lease && newLease && lease.value === command.editLeaseToken
                    && (!version || version.value === command.expectedVersion)) {
                    lease.value = newLease.value;
                    if (version) { version.value = String(commit.version); }
                }
            });
            carryForward(parsed);
            // Staged crops and rotations are recorded now: Files is drawn
            // afresh from the response, and holds no typed control to lose.
            var form = caseForm();
            if (form && form.__pegasusPreparationStaged) {
                form.__pegasusPreparationStaged = null;
                Array.prototype.forEach.call(form.querySelectorAll('[data-preparation-hidden]'), function (input) { input.remove(); });
                var files = sectionFor('files');
                var nextFiles = parsed.getElementById('section-files');
                if (files && nextFiles) { files.replaceWith(nextFiles); bindMounted(nextFiles); }
            }
        }
        // A refused commit drew the Case again with a fresh operation key and
        // its current version. The typed value stays for another go, and that
        // go carries the new authority: not a key the Case has already
        // applied, nor a version it has moved past.
        if (commitRefused) { carryForward(parsed); }
        var openDialog = document.querySelector('[data-case-dialogs] [data-dialog]:not([hidden])');
        swapRoots.forEach(function (selector) {
            if (keepSections && (selector === '#case-main' || selector === '[data-case-viewer-host]')) { return; }
            if (commitRefused && selector !== '[data-case-notices]' && selector !== '[data-case-aside]') { return; }
            // A dialog the operator has open stays open across a commit; its
            // forms already carry the new authority.
            if (commitLanded && selector === '[data-case-dialogs]' && openDialog) { return; }
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
        if (commitLanded) {
            // The ribbon's status word says it; the notice would say it again.
            var confirmation = document.querySelector('[data-case-notices] [data-confirmation]');
            if (confirmation) { confirmation.remove(); }
        }
        (commitRefused ? [] : ['class', 'data-case-version', 'data-case-editing', 'data-section-current', 'data-case-view']).forEach(function (name) {
            var value = incoming.getAttribute(name);
            if (value === null) { record.removeAttribute(name); } else { record.setAttribute(name, value); }
        });
        record.setAttribute('data-layout', layout);
        main = document.getElementById('case-main');
        // Dialogs first so the openers in the swapped roots find them.
        ['[data-case-dialogs]', '[data-case-viewer-host]', '[data-case-notices]', '[data-case-ribbon-facts]', '[data-case-ribbon-actions]', '#case-main', '[data-case-aside]'].forEach(function (selector) {
            var root = document.querySelector(selector);
            if (root) { bindMounted(root); }
        });
        if (!keepSections) {
            if (layout === 'tabs') {
                applyTabState();
                var selected = sectionFor(activeKey);
                if (selected && selected.hasAttribute('data-lazy')) { mount(selected, applyTabState); }
            } else { applyScrollState(); }
        }
        updateSectionFields();
        measure();
        if (!keepSections) { keep(saved); }
        if (commitLanded) {
            setCommitStatus('saved', clockNow());
        } else if (commitRefused) {
            var refusal = document.querySelector('[data-case-notices] [role="alert"]');
            setCommitStatus('refused', refusal ? refusal.textContent.trim() : 'The change was not saved.');
            abandonWaiters();
        } else if (incoming.getAttribute('data-case-editing') !== 'true') {
            setCommitStatus(null);
        }
        bindHeartbeat();
        bindEditControls();
        if (!keepSections) {
            mountApproaching();
            spy();
        }
        announceNotices();
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
    // Choosing a repair spec (a spec tab, New repair spec, Compare's From and
    // To) changes only what the Repair Spec section draws: the section and
    // the dialogs drawn after it. Only those are redrawn from the page the
    // choice addresses, so nothing above them moves and the page stays where
    // it is. A choice that cannot be redrawn takes the navigation it replaced.
    function estimatePart(section) {
        var part = [section];
        for (var next = section.nextElementSibling; next && next.hasAttribute('data-dialog'); next = next.nextElementSibling) {
            part.push(next);
        }
        return part;
    }
    function showEstimate(href) {
        glassRefreshGeneration += 1;
        return fetch(href, {
            credentials: 'same-origin', headers: { 'X-Requested-With': 'fetch', 'Accept': 'text/html' }
        }).then(function (response) {
            if (!response.ok || !samePage(response.url || href)) { throw new Error('section estimate: ' + response.status); }
            return response.text();
        }).then(function (html) {
            var section = sectionFor('estimate');
            var next = new DOMParser().parseFromString(html, 'text/html').getElementById('section-estimate');
            if (!section || !next) { throw new Error('section estimate: not returned'); }
            var incoming = estimatePart(next);
            estimatePart(section).slice(1).forEach(function (dialog) {
                if (!dialog.hidden && typeof dialog.pegasusClose === 'function') { dialog.pegasusClose(); }
                dialog.remove();
            });
            section.replaceWith.apply(section, incoming);
            bindMounted(main);
            measure();
            if (layout === 'tabs') { applyTabState(); } else { spy(); }
            window.history.replaceState(null, '', href);
        }).catch(function () {
            window.location.assign(href);
        });
    }

    // The shared Refresh control (site.js) marks itself busy on submit and
    // expects the navigation to end that. An intercepted refresh never
    // navigates, so every way out of one ends it here.
    function resetRefresh(form) {
        if (form.hasAttribute('data-refresh-form') && typeof window.pegasusResetRefresh === 'function') {
            window.pegasusResetRefresh(form);
        }
    }
    function submitInPlace(form, submitter) {
        // A section-head Edit keeps its own section where it is on screen.
        var editKey = submitter ? submitter.getAttribute('data-section-edit') : null;
        var editFocus = submitter ? submitter.getAttribute('data-edit-focus') : null;
        var editHost = editKey ? sectionFor(editKey) : null;
        var preferred = editHost ? { key: editKey, top: editHost.getBoundingClientRect().top } : null;
        var body = new FormData(form, submitter && submitter.name ? submitter : undefined);
        var isImport = form.hasAttribute('data-estimate-import-form');
        var command = editorLabels[form.getAttribute('id')] || isImport ? {
            editor: form.getAttribute('id'), operationKey: body.get('operationKey'),
            expectedVersion: body.get('expectedVersion'), editLeaseToken: body.get('editLeaseToken')
        } : null;
        var isCommit = !!command && command.editor === 'case-edit-form'
            && record.getAttribute('data-case-editing') === 'true';
        if (isCommit) { commitInFlight = true; setCommitStatus('saving'); }
        var action = (submitter && submitter.getAttribute('formaction')) || form.getAttribute('action') || window.location.href;
        var method = ((submitter && submitter.getAttribute('formmethod')) || form.getAttribute('method') || 'get').toUpperCase();
        var request = { method: method, credentials: 'same-origin', redirect: 'follow', headers: { 'X-Requested-With': 'fetch', 'Accept': 'text/html' } };
        if (method === 'GET') {
            var url = new URL(action, window.location.href);
            new URLSearchParams(body).forEach(function (value, key) { url.searchParams.set(key, value); });
            action = url.toString();
            if (form.hasAttribute('data-estimate-compare-form')) {
                form.setAttribute('aria-busy', 'true');
                return showEstimate(action).finally(function () {
                    form.removeAttribute('aria-busy');
                    form.removeAttribute('data-inplace-submitting');
                    submitting = false;
                    settleQueue();
                });
            }
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
                if (commitPending || commitTimer !== null) { throw new Error('The action left the Case before its result was confirmed.'); }
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
            if (editKey && editFocus) { focusControl(editKey, editFocus); }
            if (form.hasAttribute('data-glass-close-form')) { return refreshGlassControls(); }
        }).catch(function (error) {
            var failure = isImport
                ? error.message + ' Import completion was not confirmed. Reload the Case before retrying. If the source was already stored, it will be reused.'
                : isCommit
                    ? error.message + ' Your last change was not saved; it is still on the page.'
                    : error.message + ' Your last change is still on the page.';
            if (isCommit) { setCommitStatus('refused', error.message); abandonWaiters(); }
            showActionError(failure);
        }).finally(function () {
            form.removeAttribute('aria-busy');
            form.removeAttribute('data-inplace-submitting');
            resetRefresh(form);
            if (importSection && importSection.isConnected) {
                importSection.removeAttribute('data-estimate-importing');
                importSection.classList.remove('is-import-unavailable');
            }
            if (importStatus && importStatus.isConnected) {
                importStatus.hidden = true;
                importStatus.textContent = '';
            }
            if (isCommit) { commitInFlight = false; }
            submitting = false;
            settleQueue();
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

    // A document action posts at once and the sections stay as the operator
    // has them. Only what the action changed is drawn again: the image's tile
    // (or every tag picker, for a new tag), the notices and the aside. A tag
    // moves the Case version and the edit lease, so the new pair is carried
    // into every form that held the old one, the Save form included: the
    // next commit still carries the Case's authority.
    function submitDocumentAction(form, submitter) {
        var body = new FormData(form, submitter && submitter.name ? submitter : undefined);
        var previousLease = body.get('editLeaseToken');
        var previousVersion = record.getAttribute('data-case-version');
        var changesTile = body.has('occurrenceId');
        var tile = form.closest('[data-image-tile]');
        var tileId = tile ? tile.getAttribute('data-image-tile') : null;
        var toReport = form.hasAttribute('data-image-in-report-form');
        form.setAttribute('aria-busy', 'true');
        var action = (submitter && submitter.getAttribute('formaction')) || form.getAttribute('action') || window.location.href;
        return fetch(action, {
            method: 'POST', body: body, credentials: 'same-origin', redirect: 'follow',
            headers: { 'X-Requested-With': 'fetch', 'Accept': 'text/html' }
        }).then(function (response) {
            if (!samePage(response.url || action)) {
                throw new Error('The action left the Case before its result was confirmed.');
            }
            if (response.status === 403 || response.status === 404) {
                throw new Error('The action is unavailable or no longer permitted.');
            }
            if (!response.ok) { throw new Error('The server could not confirm the action.'); }
            return response.text();
        }).then(function (html) {
            var parsed = new DOMParser().parseFromString(html, 'text/html');
            var incoming = parsed.querySelector('[data-case-record]');
            if (!incoming) { throw new Error('The server did not return the Case.'); }
            carryAuthority(incoming, previousLease, previousVersion);
            ['[data-case-notices]', '[data-case-aside]'].forEach(function (selector) {
                var current = document.querySelector(selector);
                var next = parsed.querySelector(selector);
                if (current && next) { current.replaceWith(next); bindMounted(next); }
            });
            if (tileId) {
                if (changesTile) { redrawImageTile(parsed, tileId, toReport); } else { redrawTagPickers(parsed, tileId); }
            }
            announceNotices();
        }).catch(function (error) {
            showActionError(error.message + ' Your last change is still on the page.');
        }).finally(function () {
            form.removeAttribute('aria-busy');
            form.removeAttribute('data-inplace-submitting');
            submitting = false;
            settleQueue();
        });
    }
    // Moves every form that held the lease the action consumed onto the one it
    // reclaimed, with the Case version beside it. A response that is no longer
    // editing means the session ended: the changes stay on screen unsaved.
    function carryAuthority(incoming, previousLease, previousVersion) {
        var nextLease = incoming.querySelector('[name="editLeaseToken"]');
        var nextVersion = incoming.getAttribute('data-case-version');
        if (incoming.getAttribute('data-case-editing') !== 'true' || !nextLease) {
            showActionError('The action was applied, but editing has ended. Reload the Case to carry on.');
            return;
        }
        if (nextLease.value === previousLease) { return; }
        Array.prototype.forEach.call(document.querySelectorAll('input[name="editLeaseToken"]'), function (lease) {
            if (lease.value !== previousLease) { return; }
            lease.value = nextLease.value;
            var owner = lease.form || lease.closest('form');
            if (!owner) { return; }
            Array.prototype.forEach.call(owner.elements, function (element) {
                if (element.name === 'expectedVersion' && element.value === previousVersion) { element.value = nextVersion; }
            });
        });
        if (nextVersion !== null) { record.setAttribute('data-case-version', nextVersion); }
    }
    function redrawImageTile(parsed, id, focusInReport) {
        var current = document.querySelector('[data-image-tile="' + id + '"]');
        var next = parsed.querySelector('[data-image-tile="' + id + '"]');
        if (!current || !next) { return; }
        var grid = current.parentElement;
        if (window.pegasusCasePreparation && window.pegasusCasePreparation.adopt) {
            window.pegasusCasePreparation.adopt(id, next);
        }
        current.replaceWith(next);
        bindMounted(grid);
        var count = document.querySelector('[data-image-report-count]');
        var nextCount = parsed.querySelector('[data-image-report-count]');
        if (count && nextCount) { count.textContent = nextCount.textContent; }
        var target = next.querySelector(focusInReport ? '[data-image-in-report]' : 'details.tag-picker > summary');
        if (target) { target.focus(); }
    }
    // A new tag joins the vocabulary every picker offers. The picker it was
    // made in stays open, so the tag is there to apply.
    function redrawTagPickers(parsed, id) {
        Array.prototype.forEach.call(document.querySelectorAll('[data-image-tile]'), function (tile) {
            var tileKey = tile.getAttribute('data-image-tile');
            var picker = tile.querySelector('details.tag-picker');
            var next = parsed.querySelector('[data-image-tile="' + tileKey + '"] details.tag-picker');
            if (!picker || !next) { return; }
            if (tileKey === id) { next.setAttribute('open', ''); }
            picker.replaceWith(next);
        });
        bindMounted(main);
        var name = document.querySelector('[data-image-tile="' + id + '"] .tag-picker-new input[name="name"]');
        if (name) { name.focus(); }
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
            // The frame's submit queue lets an import follow a commit; an
            // import or another action already on its way refuses it.
            function canAccept() {
                return !(submitting && !commitInFlight) && form.dataset.inplaceSubmitting !== 'true';
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
                if (!canAccept()) {
                    input.value = '';
                    showActionError(importMessage('Busy', 'Wait for the current Case action to finish before importing an estimate.'));
                    return;
                }
                form.requestSubmit();
            }

            picker.addEventListener('click', function () {
                if (!canAccept()) { return; }
                input.click();
            });
            input.addEventListener('change', submitSelectedFile);
            section.addEventListener('dragenter', function (event) {
                if (!isFileDrag(event)) { return; }
                event.preventDefault();
                depth += 1;
                showDrag(!canAccept());
            });
            section.addEventListener('dragover', function (event) {
                if (!isFileDrag(event)) { return; }
                event.preventDefault();
                event.dataTransfer.dropEffect = canAccept() ? 'copy' : 'none';
                showDrag(!canAccept());
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
        var isCommit = !!editorLabels[form.getAttribute('id')];
        if (form.dataset.inplaceSubmitting === 'true') {
            return;
        }
        var name = submitter ? submitter.name : '';
        var value = submitter ? submitter.value : '';
        var formaction = submitter ? submitter.getAttribute('formaction') : null;
        function proceed() {
            // A form that waited on a commit is found again: the ribbon's and
            // the dialogs' forms are drawn afresh with the Case's new authority.
            var next = refind(form);
            if (!next || next.dataset.inplaceSubmitting === 'true') { return; }
            // Waiters run together once the queue empties; the first to post
            // holds it, and the next follows that post.
            if (submitting) { commitWaiters.push({ run: proceed }); return; }
            var button = submitter && next !== form ? Array.prototype.find.call(next.elements, function (element) {
                return element.type === 'submit' && element.name === name && element.value === value
                    && element.getAttribute('formaction') === formaction;
            }) || null : submitter;
            submitting = true;
            next.dataset.inplaceSubmitting = 'true';
            // A document action (tag, untag, new tag, In report) while editing
            // is not an edit of the Case: it posts at once and redraws only
            // its own tile, so the sections stay as the operator has them.
            if (next.hasAttribute('data-document-action') && record.getAttribute('data-case-editing') === 'true') {
                submitDocumentAction(next, button);
                return;
            }
            submitInPlace(next, button);
        }
        if (submitting) {
            // A commit behind a post in flight follows it, and so does an
            // action (Done, an import) pressed while a commit is on its way:
            // the press that left a cell is what started that commit. A
            // Refresh during a commit, and a second press of an action while
            // an action is in flight, are dropped; a Refresh already in
            // flight (F5 bypasses the disabled button) stays busy until its
            // own response lands.
            if (isCommit) { commitPending = true; }
            else if (commitInFlight && !form.hasAttribute('data-refresh-form')) { afterCommit(proceed); }
            else { resetRefresh(form); }
            return;
        }
        // Every other post (an action, Done, a refresh) follows the change
        // not yet sent, so it reads the Case as the operator has it.
        if (isCommit) { proceed(); } else { afterCommit(proceed); }
    });
    function refind(form) {
        if (form.isConnected) { return form; }
        var id = form.getAttribute('id');
        if (id && document.getElementById(id)) { return document.getElementById(id); }
        var action = form.getAttribute('action');
        var dialogs = document.querySelector('[data-case-dialogs]');
        var candidates = Array.prototype.slice.call(record.querySelectorAll('form[action]'))
            .concat(dialogs ? Array.prototype.slice.call(dialogs.querySelectorAll('form[action]')) : []);
        return candidates.find(function (candidate) { return candidate.getAttribute('action') === action; }) || null;
    }

    // The frame owns this shortcut even inside a field; site.js handles it on
    // other pages. It commits the Case form now, a composite's typing included.
    document.addEventListener('keydown', function (event) {
        if (!(event.ctrlKey || event.metaKey) || event.key.toLowerCase() !== 's') { return; }
        event.preventDefault();
        event.stopImmediatePropagation();
        commitNow(true);
    }, true);
    // Leaving the Case by a link ends edit mode (FRD-14): a change not yet
    // sent lands first, and the lease is released as the operator goes, so
    // the Case is free rather than held until its lease lapses. A link to
    // this same Case (a section, a view, one of its own pages) keeps editing.
    function leavesCase(link) {
        var beacon = record.querySelector('[data-case-release-beacon]');
        if (!beacon) {
            return false;
        }
        var url;
        try {
            url = new URL(link.href, window.location.href);
        } catch (error) {
            return false;
        }
        var caseId = (beacon.getAttribute('data-case-id') || '').toLowerCase();
        return url.origin !== window.location.origin
            || (url.pathname.toLowerCase().indexOf(caseId) === -1
                && (url.searchParams.get('id') || '').toLowerCase() !== caseId);
    }
    function releaseOnLeaving(link) {
        var beacon = record.querySelector('[data-case-release-beacon]');
        if (beacon && typeof navigator.sendBeacon === 'function' && leavesCase(link)) {
            navigator.sendBeacon(beacon.action, new FormData(beacon));
        }
    }
    document.addEventListener('click', function (event) {
        var link = event.target.closest('a[href]');
        if (!link || event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey
            || link.hasAttribute('target') || link.hasAttribute('download') || link.hasAttribute('data-section-link')
            || link.getAttribute('data-section-jump') || link.hasAttribute('data-evidence-item')
            || link.getAttribute('href').startsWith('#')) { return; }
        if (link.matches('[data-estimate-tab], [data-estimate-new]')) {
            // A change not yet sent lands first, then the spec is redrawn.
            event.preventDefault();
            afterCommit(function () {
                submitting = true;
                showEstimate(link.href).finally(function () {
                    submitting = false;
                    settleQueue();
                });
            });
            return;
        }
        if (!queueBusy()) {
            releaseOnLeaving(link);
            return;
        }
        // A post in flight decides the lease itself, and a change not yet
        // sent lands first; then the lease goes with the operator.
        event.preventDefault();
        afterCommit(function () {
            releaseOnLeaving(link);
            window.location.assign(link.href);
        });
    });

    // Glass's controls are independent of the Case form and its authority.
    var glassRefreshGeneration = 0;
    var glassOpening = false;
    var glassWindow = null;
    var glassWindowWatch = null;
    function finishGlassOpening() {
        glassOpening = false;
        if (glassWindowWatch) { window.clearInterval(glassWindowWatch); glassWindowWatch = null; }
        record.querySelectorAll('[data-glass-window]').forEach(function (form) { form.removeAttribute('aria-busy'); });
    }
    function cancelGlassOpening() {
        if (glassWindow && !glassWindow.closed) { glassWindow.close(); }
        finishGlassOpening();
    }
    function refreshGlassControls() {
        var host = record.querySelector('[data-glass-controls="launch"]');
        if (!host) { return Promise.reject(new Error("Glass's controls are unavailable. Reload the Case.")); }
        var generation = ++glassRefreshGeneration;
        var lease = record.querySelector('#case-edit-form [name="editLeaseToken"]');
        return fetch(host.dataset.glassControlsUrl, {
            credentials: 'same-origin', cache: 'no-store',
            headers: { 'X-Pegasus-Edit-Lease': lease ? lease.value : '', 'Accept': 'text/html' }
        }).then(function (response) {
            if (!response.ok) { throw new Error("Glass's controls could not be refreshed. Reload the Case before retrying."); }
            return response.text();
        }).then(function (html) {
            if (generation !== glassRefreshGeneration) { return; }
            var parsed = new DOMParser().parseFromString(html, 'text/html');
            var nextLaunch = parsed.querySelector('[data-glass-controls="launch"]');
            var nextSession = parsed.querySelector('[data-glass-controls="session"]');
            var nextOutcome = parsed.querySelector('[data-glass-controls="outcome"]');
            if (!nextLaunch || !nextSession || !nextOutcome) { throw new Error("Sign in again to refresh Glass's controls."); }
            var current = record.querySelector('[data-glass-controls="session"]');
            if (current && current.dataset.glassId === nextSession.dataset.glassId
                && Number(current.dataset.glassVersion) > Number(nextSession.dataset.glassVersion)) { return; }
            var saved = anchor();
            var focused = document.activeElement;
            var focusedHost = focused && focused.closest('[data-glass-controls]');
            var focusSelector = focusedHost && (focused.name ? '[name="' + focused.name + '"]' : focused.tagName.toLowerCase());
            [nextLaunch, nextOutcome, nextSession].forEach(function (next) {
                var old = record.querySelector('[data-glass-controls="' + next.dataset.glassControls + '"]');
                if (old) { old.replaceWith(next); bindMounted(next); }
            });
            if (focusSelector) {
                var replacement = record.querySelector('[data-glass-controls="' + focusedHost.dataset.glassControls + '"] ' + focusSelector);
                if (replacement) { replacement.focus({ preventScroll: true }); }
            }
            keep(saved);
            return nextSession.dataset.glassState;
        });
    }
    window.pegasusGlassHandoff = function () {
        return refreshGlassControls().catch(function (error) { showActionError(error.message); }).finally(finishGlassOpening);
    };
    window.pegasusGlassReturn = function (url) {
        if (!samePage(url) || new URL(url, window.location.href).origin !== window.location.origin) {
            return Promise.reject(new Error('The Glass return does not belong to this Case.'));
        }
        // The controls first; then, once any change not yet sent has landed,
        // the Case as it now stands, the recorded spec included.
        return refreshGlassControls().then(function () {
            return new Promise(function (resolve, reject) {
                afterCommit(function () {
                    var versionBeforeRead = record.getAttribute('data-case-version');
                    var generationBeforeRead = glassRefreshGeneration;
                    fetch(url, { credentials: 'same-origin', cache: 'no-store' }).then(function (response) {
                        if (!response.ok || !samePage(response.url)) { throw new Error('The Case could not be refreshed.'); }
                        return response.text();
                    }).then(function (html) {
                        // A post or another refresh may have finished during this
                        // read. An older response cannot put the record back on
                        // its old version.
                        if (submitting || generationBeforeRead !== glassRefreshGeneration
                            || versionBeforeRead !== record.getAttribute('data-case-version')) { return; }
                        if (!swap(html)) { throw new Error('The Case could not be refreshed.'); }
                    }).then(resolve, reject);
                    // A change the Case refused stays on the page for another
                    // go, so the Case is not read over it.
                }, resolve);
            });
        }).catch(function (error) { showActionError(error.message); throw error; }).finally(finishGlassOpening);
    };
    document.addEventListener('submit', function (event) {
        var form = event.target;
        if (!(form instanceof HTMLFormElement) || !form.hasAttribute('data-glass-window')) { return; }
        event.preventDefault();
        if (glassOpening) { return; }
        var windowName = 'pegasus-glass-' + window.location.pathname;
        glassWindow = window.open('', windowName, 'popup=yes,width=1280,height=900');
        if (!glassWindow) { showActionError("Allow pop-ups for Pegasus, then open Glass's again."); return; }
        glassOpening = true;
        form.setAttribute('aria-busy', 'true');
        glassWindowWatch = window.setInterval(function () {
            if (glassWindow.closed) { finishGlassOpening(); }
        }, 500);
        var action = form.getAttribute('action');
        // A change not yet sent lands first, and the launch carries the
        // Case's current lease and session version, never a detached form's.
        afterCommit(function () {
            var next = Array.from(record.querySelectorAll('form[data-glass-window]')).find(function (candidate) {
                return candidate.getAttribute('action') === action;
            });
            if (!next || glassWindow.closed) {
                cancelGlassOpening();
                showActionError("Glass's was not opened. Check the Case and try again.");
                return;
            }
            next.target = windowName;
            next.setAttribute('aria-busy', 'true');
            HTMLFormElement.prototype.submit.call(next);
        }, cancelGlassOpening);
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
    bindEditControls();
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

    bindReportRecipients(document);
    (window.pegasusMountBinders = window.pegasusMountBinders || []).push(bindReportRecipients);
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
// The bursts on the plan, the three chips, the recorded-areas list and the
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
            // Core's one comic burst (DamageBurst), drawn over each disc.
            var burst = vocabulary.burst;
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
            // On the drawing's outline: its body, tyres and mirrors (and a
            // motorbike's frame and bars), as Core lists them.
            function onVehicle(x, y) {
                var point = svg.createSVGPoint();
                point.x = x;
                point.y = y;
                return Array.prototype.slice.call(svg.querySelectorAll('.dv-hit path')).some(function (path) {
                    return path.isPointInFill(point);
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
            // The burst over a disc, as Core draws it (DamagePlanGeometry.BurstPath).
            function burstPath(disc) {
                var vertices = [];
                for (var i = 0; i < burst.points * 2; i++) {
                    var theta = (burst.rotation - 90) * Math.PI / 180 + i * Math.PI / burst.points;
                    var reach = i % 2 ? disc.r * (1 - burst.depth) : disc.r;
                    var wave = 1 + burst.jitter * (Math.sin(theta * 3.7 + burst.points * 0.31) + Math.cos(theta * 2.1 + burst.rotation * 0.07)) * 0.35;
                    vertices.push(format(disc.x + Math.cos(theta) * reach * wave * burst.stretchX) + ' ' + format(disc.y + Math.sin(theta) * reach * wave * burst.stretchY));
                }
                return 'M' + vertices.join(' L') + ' Z';
            }
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
                    group.appendChild(circle('area', mark.disc.x, mark.disc.y, mark.disc.r));
                    var shape = document.createElementNS(SVG_NS, 'path');
                    shape.setAttribute('class', 'burst');
                    shape.setAttribute('d', burstPath(mark.disc));
                    shape.setAttribute('fill', burst.fill);
                    shape.setAttribute('fill-opacity', String(burst.opacity));
                    shape.setAttribute('stroke', burst.line);
                    shape.setAttribute('stroke-width', String(burst.lineWidth));
                    shape.setAttribute('stroke-linejoin', 'round');
                    group.appendChild(shape);
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
// ribbon Save records a changed calculation (one Save, 23 September 2026).
// Choosing a card fills the Retail and Trade boxes in place, and each
// calculation fills the Engineer's Value box; the operator may overtype any
// of them (operator, 26 September 2026).
//
// The preview shows what the Save will use (operator, 28 September 2026): it
// posts the chosen card's retail as typed and the claimant's VAT position as
// the form holds it, so an unsaved figure is calculated, not the recorded one.
// While a preview is pending the lines are dimmed, and a failed one says so;
// a calculation Core cannot work out shows its own reason.
//
// Use this value on a card is the visible decision to use that card's figure:
// it chooses the card as the basis, fills the boxes, and switches on the
// selection.Use field so the Case Save records the calculation even when it
// is the one the page opened on. A card typed in the same edit has no
// identity yet, so it is named to the Save by its source.
(function () {
    'use strict';

    var VAT_FIELD = 'assessmentFields[settlement.claimant_vat_registered]';

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
            var sourceInput = section.querySelector('[data-valuation-source-input]');
            var useInput = section.querySelector('[data-valuation-use-input]');
            var previewUrl = calc.getAttribute('data-preview-url');
            var timer = null;
            var inFlight = null;

            // A calculator control: one of this section's selection fields of
            // the Case form (the guide cards' own boxes are the Case's too, but
            // they are the cards, not the calculation).
            function belongs(control) {
                return !!control && isSelection(control.name);
            }

            // The card the calculation starts from: the checked Basis radio, or
            // the card named by source when it was typed in this edit.
            function chosenCard() {
                var checked = section.querySelector('[data-valuation-basis]:checked');
                if (checked) {
                    return checked.closest('[data-valuation-card]');
                }
                if (sourceInput && !sourceInput.disabled && sourceInput.value) {
                    return section.querySelector('[data-valuation-source-card="' + sourceInput.value + '"]');
                }
                return null;
            }

            function busy(on) {
                if (!host) { return; }
                if (on) { host.setAttribute('aria-busy', 'true'); } else { host.removeAttribute('aria-busy'); }
            }

            // The lines could not be refreshed: say so where the figure was,
            // rather than leaving the last figure standing as if it were current.
            function showFailure() {
                if (!host) { return; }
                var lines = document.createElement('div');
                lines.className = 'lines';
                lines.setAttribute('data-valuation-lines', '');
                var notice = document.createElement('div');
                notice.className = 'notice notice--danger';
                notice.setAttribute('role', 'alert');
                notice.setAttribute('data-valuation-error', '');
                notice.textContent = calc.getAttribute('data-text-preview-failed')
                    || 'The calculation could not be updated.';
                lines.appendChild(notice);
                host.textContent = '';
                host.appendChild(lines);
            }

            // The Engineer's Value box holds the last calculated figure until
            // the Engineer types over it. When a calculation cannot be worked
            // out or refreshed, that figure is out of date: the box goes back to
            // the recorded value and any Use this value decision is withdrawn,
            // so nothing stale is saved as if it were current.
            var lastProposal = null;
            function invalidate() {
                var box = section.querySelector('[data-valuation-value="engineer"]');
                if (box && lastProposal !== null && box.value === lastProposal) {
                    fill(section, '[data-valuation-value="engineer"]', box.defaultValue);
                }
                lastProposal = null;
                clearUse();
            }

            function preview() {
                if (!previewUrl || !host) {
                    return;
                }
                var body = caseFields(function (name) {
                    return name === '__RequestVerificationToken' || isSelection(name) || name === VAT_FIELD;
                });
                // The retail the Save will use: the chosen card's, as typed.
                var card = chosenCard();
                if (card) {
                    // Posted even when empty: an empty box is "no retail", which
                    // the Save reads the same way, never the recorded card.
                    body.append('basisRetail', shown(card, '[data-valuation-retail]', 'data-retail'));
                }
                if (inFlight) {
                    inFlight.abort();
                }
                inFlight = new AbortController();
                busy(true);
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
                    busy(false);
                    host.innerHTML = html;
                    var proposal = host.querySelector('[data-valuation-proposal]');
                    if (proposal) {
                        lastProposal = proposal.getAttribute('data-valuation-proposal');
                        fill(section, '[data-valuation-value="engineer"]', lastProposal);
                    } else {
                        invalidate();
                    }
                }).catch(function (error) {
                    if (error && error.name === 'AbortError') {
                        // A newer preview replaced this one and owns the state.
                        return;
                    }
                    busy(false);
                    showFailure();
                    invalidate();
                });
            }
            function schedule() {
                if (!previewUrl || !host) {
                    return;
                }
                // The lines on screen are for figures that have since changed.
                busy(true);
                window.clearTimeout(timer);
                timer = window.setTimeout(preview, 250);
            }

            // The chosen card is drawn selected and named in the calculator's head.
            function markChosen(card, name) {
                section.querySelectorAll('[data-valuation-card]').forEach(function (other) {
                    other.classList.toggle('sel', other === card);
                });
                if (basisName) {
                    basisName.textContent = 'from ' + (name || 'guide') + ' retail';
                }
            }
            function chooseBasis(radio) {
                markChosen(radio.closest('[data-valuation-card]'), radio.getAttribute('data-source-name'));
            }

            // The chosen card's figures as it shows them: an entry card's own
            // boxes, any other card as recorded.
            function shown(card, box, recorded) {
                var input = card.querySelector(box);
                return input ? input.value : (card.getAttribute(recorded) || '');
            }
            function fillFromCard(card) {
                fill(section, '[data-valuation-value="retail"]', shown(card, '[data-valuation-retail]', 'data-retail'));
                fill(section, '[data-valuation-value="trade"]', shown(card, '[data-valuation-trade]', 'data-trade'));
            }

            // Use this value is off until pressed, and pressing another card or
            // choosing a basis by clicking a card puts the decision back to
            // "not yet": only the button expresses it.
            function setUseButton(button, on) {
                button.setAttribute('aria-pressed', on ? 'true' : 'false');
                var label = button.querySelector('span');
                var text = calc.getAttribute(on ? 'data-text-using' : 'data-text-use');
                if (label && text) { label.textContent = text; }
            }
            function clearUse() {
                if (useInput) { useInput.disabled = true; }
                if (sourceInput) { sourceInput.disabled = true; sourceInput.value = ''; }
                section.querySelectorAll('[data-valuation-use]').forEach(function (button) {
                    setUseButton(button, false);
                });
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
                    clearUse();
                    chooseBasis(control);
                    fillFromCard(control.closest('[data-valuation-card]'));
                }
                if (control.matches('[data-preset-toggle]')) {
                    paintAdditions();
                }
                schedule();
            });
            section.addEventListener('input', function (event) {
                if (event.isTrusted && event.target && event.target.matches
                    && event.target.matches('[data-valuation-value="engineer"]')) {
                    // Typed over by the Engineer: that figure is their own, so
                    // the decision to use the calculated one is withdrawn.
                    lastProposal = null;
                    clearUse();
                    return;
                }
                if (belongs(event.target)) {
                    schedule();
                    return;
                }
                // A figure typed into the chosen card is the basis figure now.
                var typed = event.target;
                if (typed && typed.matches
                    && (typed.matches('[data-valuation-retail]') || typed.matches('[data-valuation-trade]'))) {
                    var card = typed.closest('[data-valuation-card]');
                    if (card && card === chosenCard()) {
                        fillFromCard(card);
                        schedule();
                    }
                }
            });
            // Get valuation refilled the card that is already the basis.
            section.addEventListener('pegasus:valuation-basis-refilled', function (event) {
                var card = event.target && event.target.closest ? event.target.closest('[data-valuation-card]') : null;
                if (card) {
                    fillFromCard(card);
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

            // Use this value: choose the card, fill the boxes from it, and
            // mark the decision for the Save. A card with no retail has no
            // figure to use, so it says so on its own card.
            section.querySelectorAll('[data-valuation-use]').forEach(function (button) {
                button.addEventListener('click', function (event) {
                    event.preventDefault();
                    var card = button.closest('[data-valuation-card]');
                    if (!card || !useInput) {
                        return;
                    }
                    var retail = parseFloat(shown(card, '[data-valuation-retail]', 'data-retail'));
                    var notice = card.querySelector('[data-valuation-notice]');
                    if (!(retail > 0)) {
                        showNotice(notice, true, calc.getAttribute('data-text-use-needs-retail'));
                        return;
                    }
                    showNotice(notice, false);
                    clearUse();
                    var radio = card.querySelector('[data-valuation-basis]');
                    if (radio) {
                        // A recorded card is chosen by its identity, as a click would.
                        radio.checked = true;
                        chooseBasis(radio);
                    } else {
                        // A card typed in this edit has no identity yet: the Save
                        // is told its source, and no recorded card is the basis.
                        section.querySelectorAll('[data-valuation-basis]').forEach(function (other) {
                            other.checked = false;
                        });
                        if (sourceInput) {
                            sourceInput.value = card.getAttribute('data-valuation-source-card') || '';
                            sourceInput.disabled = false;
                        }
                        var name = card.querySelector('h3 > span');
                        markChosen(card, name ? name.textContent : null);
                    }
                    useInput.disabled = false;
                    setUseButton(button, true);
                    fillFromCard(card);
                    // The decision is a change to the Case form: the frame
                    // commits it once the card is left.
                    useInput.dispatchEvent(new Event('input', { bubbles: true }));
                });
            });

            // The claimant's VAT position is the Claim section's control. The
            // calculation uses it as the form holds it, and a registered
            // claimant never has a commercial addition.
            var vatSelect = document.querySelector('select[name="' + VAT_FIELD + '"]');
            if (vatSelect && vatSelect.dataset.valuationVatBound !== 'true') {
                vatSelect.dataset.valuationVatBound = 'true';
                vatSelect.addEventListener('change', function () {
                    if (!section.isConnected) { return; }
                    var addVat = section.querySelector('[data-valuation-vat-wrap] input[type="checkbox"]');
                    if (addVat) {
                        var registered = vatSelect.value === 'true';
                        addVat.disabled = registered;
                        if (registered) { addVat.checked = false; }
                    }
                    schedule();
                });
            }

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
        // provider, or a refused request, shows the card's own notice. A source
        // known to have no provider offers no button at all.
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
                        // When this card is already the basis, its new figures
                        // are the basis figures: the Retail and Trade boxes take
                        // them, and the calculation follows. The Engineer's Value
                        // box is left as it stands until that calculation lands.
                        var basis = card.querySelector('[data-valuation-basis]');
                        if (basis && basis.checked) {
                            basis.dispatchEvent(new CustomEvent('pegasus:valuation-basis-refilled', { bubbles: true }));
                        }
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
    // words in its place when the server gave some. A source known to have no
    // provider keeps its notice standing, and it returns to the sentence when
    // the words go.
    function showNotice(notice, visible, message) {
        if (!notice) {
            return;
        }
        var standing = notice.hasAttribute('data-valuation-not-connected');
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
        notice.hidden = standing ? false : !visible;
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
    // chip and to put the boxes back; Core still decides on Save
    // (EstimateVatPolicy.Revised).
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
            // The frame listens for input on the Case form's controls: a
            // removed line is a change of the spec, committed after the idle.
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
    // Core owns the Engineer's Value, floors and all monetary arithmetic.
    // Moving the slider previews (issue 897): Core scales and totals the spec
    // as the editor holds it, and the changed cells show its figures in amber,
    // read-only, with the rollup and the readout following. Any submit puts
    // the cells back first, so a Save records the spec as edited; only Apply
    // records a scaled spec.
    var scalePreview = null;
    document.addEventListener('submit', function () {
        if (scalePreview) { scalePreview(); }
    }, true);
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
        var section = form.closest('[data-estimate-section]') || form;
        var url = bar.getAttribute('data-scale-preview-url');
        var read = bar.querySelector('[data-scale-read]');
        var chip = bar.querySelector('[data-scale-preview]');
        var floors = [bar.querySelector('[data-scale-floor-rate]'), bar.querySelector('[data-scale-floor-price]')];
        var timer = null;
        var inFlight = null;
        // What the preview changed, to put back: each cell's value and
        // read-only state, and each rollup figure.
        var shown = null;

        function field(name) {
            return Array.prototype.slice.call(section.querySelectorAll('input[name="' + name + '"][form="case-edit-form"]'));
        }
        function end() {
            window.clearTimeout(timer);
            if (inFlight) { inFlight.abort(); inFlight = null; }
            if (shown) {
                shown.cells.forEach(function (cell) {
                    cell.input.value = cell.value;
                    cell.input.readOnly = cell.readOnly;
                    cell.input.classList.remove('is-previewed');
                });
                shown.figures.forEach(function (figure) { figure.node.textContent = figure.text; });
                shown = null;
            }
            if (read) { read.textContent = ''; }
            if (chip) { chip.hidden = true; }
            if (scalePreview === end) { scalePreview = null; }
        }
        // The request carries the spec as edited, never as previewed.
        function body() {
            var caseForm = document.getElementById('case-edit-form');
            var previewed = shown ? shown.cells.map(function (cell) {
                var value = cell.input.value;
                cell.input.value = cell.value;
                return value;
            }) : null;
            var data = new FormData(caseForm);
            if (previewed) {
                shown.cells.forEach(function (cell, index) { cell.input.value = previewed[index]; });
            }
            data.set('targetPercent', percent.value);
            data.set('floorRate', floors[0] ? floors[0].value : '');
            data.set('floorPrice', floors[1] ? floors[1].value : '');
            return data;
        }
        function paint(result) {
            if (!shown) {
                shown = { cells: [], figures: [] };
                field('linePartPounds').concat(field('lineMaterials'), field('estimateLabourRate')).forEach(function (input) {
                    shown.cells.push({ input: input, value: input.value, readOnly: input.readOnly });
                    input.readOnly = true;
                });
                section.querySelectorAll('[data-rollup]').forEach(function (node) {
                    shown.figures.push({ node: node, text: node.textContent });
                });
                scalePreview = end;
            }
            function set(input, value) {
                if (!input) { return; }
                var original = shown.cells.filter(function (cell) { return cell.input === input; })[0];
                if (!original) {
                    // A line typed in since the preview began.
                    original = { input: input, value: input.value, readOnly: input.readOnly };
                    shown.cells.push(original);
                    input.readOnly = true;
                }
                input.value = value === null ? '' : value;
                input.classList.toggle('is-previewed', Number(input.value) !== Number(original.value));
            }
            var prices = field('linePartPounds');
            var materials = field('lineMaterials');
            result.lines.forEach(function (line) {
                set(prices[line.row], line.price);
                set(materials[line.row], line.materials);
            });
            set(field('estimateLabourRate')[0], result.labourRate);
            shown.figures.forEach(function (figure) {
                var text = result.rollup[figure.node.getAttribute('data-rollup')];
                if (typeof text === 'string') { figure.node.textContent = text; }
            });
            if (read) { read.textContent = result.readout; }
            if (chip) { chip.hidden = false; }
        }
        function preview() {
            var caseForm = document.getElementById('case-edit-form');
            if (!url || !caseForm) {
                return;
            }
            if (inFlight) { inFlight.abort(); }
            var request = new AbortController();
            inFlight = request;
            fetch(url, {
                method: 'POST',
                body: body(),
                credentials: 'same-origin',
                headers: { 'X-Requested-With': 'fetch', 'Accept': 'application/json' },
                signal: request.signal
            }).then(function (response) {
                if (!response.ok) {
                    throw new Error('scale preview: ' + response.status);
                }
                return response.json();
            }).then(function (result) {
                if (inFlight !== request) { return; }
                inFlight = null;
                if (result.status === 'ok') { paint(result); } else { end(); }
            }).catch(function () {
                // A replaced request is the newer one's to settle; any other
                // failure leaves the spec as edited, and Apply still asks Core.
                if (inFlight !== request) { return; }
                end();
            });
        }
        function schedule() {
            window.clearTimeout(timer);
            // A submit from here on cancels the preview on its way.
            scalePreview = end;
            timer = window.setTimeout(preview, 250);
        }
        range.addEventListener('input', function () {
            percent.value = range.value;
            reveal();
            schedule();
        });
        percent.addEventListener('input', function () {
            range.value = percent.value;
            reveal();
            schedule();
        });
        floors.forEach(function (input) {
            if (input) {
                input.addEventListener('input', function () { if (shown) { schedule(); } });
            }
        });
        // A header change while previewing (VAT, discounts) is previewed too.
        section.addEventListener('change', function (event) {
            if (shown && !bar.contains(event.target)) { schedule(); }
        });
        // So is a line removed or put back: the grid announces it on the
        // spec's own estimateId (renumber), which no change event carries.
        form.addEventListener('input', function (event) {
            if (shown && event.target.name === 'estimateId') { schedule(); }
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
        function defaults(value) {
            return vatDefaults[value] || [];
        }
        function overridden(value) {
            var expected = defaults(value);
            return boxes.some(function (box) {
                return box.checked !== (expected.indexOf(box.getAttribute('data-vat-category')) >= 0);
            });
        }
        function tickDefaults() {
            var expected = defaults(status.value);
            boxes.forEach(function (box) {
                box.checked = expected.indexOf(box.getAttribute('data-vat-category')) >= 0;
            });
        }
        function paint() {
            var over = overridden(status.value);
            if (chip) { chip.hidden = !over; }
            if (reset) { reset.hidden = !over; }
        }
        // Boxes the operator did not choose by hand follow the status: a new
        // status ticks its own categories (issue 898).
        var previous = status.value;
        boxes.forEach(function (box) { box.addEventListener('change', paint); });
        status.addEventListener('change', function () {
            if (status.value !== previous && !overridden(previous)) {
                tickDefaults();
            }
            previous = status.value;
            paint();
        });
        if (reset) {
            reset.addEventListener('click', function () {
                tickDefaults();
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
// Drag writes through the order controls the preparation binder already owns,
// so the tile, the viewer and the Case Save cannot disagree; a click on the
// image presses the tile's own In report button, posted at once.
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
                    var order = tile.querySelector('[data-preparation-order]');
                    if (!order || tile.getAttribute('data-preparation-in-report') !== 'true') { return; }
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
                if (!link || !tile || !tile.querySelector('[data-image-in-report]')) { return; }
                event.preventDefault();
                event.stopPropagation();
                window.pegasusCasePreparation.toggleInReport(
                    tile.getAttribute('data-preparation-occurrence'),
                    tile.getAttribute('data-preparation-in-report') !== 'true');
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
// apply to them. Every control is a plain form field of #case-edit-form, so
// without script the rows the server rendered stand and the operator picks
// the value.
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

    // The Principal's salvage matrix (29 September 2026). While the salvage
    // value follows it, a change of outcome, category or Engineer's Value
    // fills the money field with the value times the band's percentage, or
    // empties it when no band applies. A figure the Engineer sets (typed,
    // slid or snapped) is their own and the matrix leaves it; emptying the
    // field hands it back. The arithmetic is Core's SalvageMatrix, in pence.
    // The Engineer's Value box is the Valuation section's, so one listener,
    // added once, refills whichever settlement binding is current.
    var engineerValueBox = '[data-valuation-value="engineer"]';
    var salvageMatrixRefill = null;
    document.addEventListener('input', function (event) {
        if (salvageMatrixRefill && event.target && event.target.matches && event.target.matches(engineerValueBox)) {
            salvageMatrixRefill();
        }
    });

    function bindSalvageMatrix(section) {
        salvageMatrixRefill = null;
        var share = section.querySelector('[data-salvage-share]');
        var row = share ? share.closest('.dec') : null;
        var field = row ? row.querySelector('input.fi') : null;
        var bands = null;
        try { bands = share ? JSON.parse(share.getAttribute('data-salvage-matrix') || 'null') : null; } catch (_) { bands = null; }
        if (!field || !Array.isArray(bands)) {
            return;
        }
        var following = share.getAttribute('data-salvage-matrix-follows') === 'true';
        var outcome = control(section.querySelector('[data-decision="assessment.outcome"]'));
        var category = control(section.querySelector('[data-decision="assessment.category"]'));
        var settlementValue = section.querySelector('[data-decision-engineer-value]');

        function pence(text) {
            var amount = parseFloat(text);
            return isFinite(amount) ? Math.round(amount * 100) : null;
        }
        function engineerValue() {
            var box = document.querySelector(engineerValueBox);
            return pence(box ? box.value
                : settlementValue ? settlementValue.getAttribute('data-engineer-value') : null);
        }
        function figure() {
            var value = engineerValue();
            if (!outcome || outcome.value !== 'total_loss' || !category || value === null) {
                return null;
            }
            for (var index = 0; index < bands.length; index++) {
                var band = bands[index];
                if (band.category === category.value && pence(band.from) <= value && value <= pence(band.to)) {
                    var salvage = Math.floor((value * Math.round(band.percentage * 100) + 5000) / 10000);
                    return (salvage / 100).toFixed(2);
                }
            }
            return null;
        }
        function refill() {
            if (!following || !field.isConnected) {
                return;
            }
            var next = figure();
            var text = next === null ? '' : next;
            if (field.value === text) {
                return;
            }
            field.value = text;
            field.dispatchEvent(new Event('input', { bubbles: true }));
        }

        // The row holds the money field and the slider: typing is the
        // Engineer's own figure unless it empties the field, and sliding
        // always is.
        row.addEventListener('input', function (event) {
            if (event.isTrusted) {
                following = event.target === field && field.value === '';
            }
        });
        share.addEventListener('click', function (event) {
            if (event.target.closest('[data-salvage-snap]')) {
                following = false;
            }
        });
        if (outcome) { outcome.addEventListener('change', refill); }
        if (category) { category.addEventListener('change', refill); }
        salvageMatrixRefill = refill;
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
            bindSalvageMatrix(section);
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
            function sync() {
                if (outcome) {
                    show('total-loss', outcome.value === 'total_loss');
                    syncReserve(outcome.value);
                }
                if (legal) {
                    show('unroadworthy', legal.value === 'unroadworthy');
                }
            }
            if (outcome) { outcome.addEventListener('change', sync); }
            if (legal) { legal.addEventListener('change', sync); }
            sync();
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

            // The preview follows the Include fee note choice beside Generate
            // report, and opens in the page's document viewer when one is present.
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

    function currentViewer() {
        var viewer = window.pegasusCaseViewer;
        return viewer && typeof viewer.openDocument === 'function' ? viewer : null;
    }

    async function openInViewer(trigger, viewer) {
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
    }

    function bind(root) {
        root.querySelectorAll('[data-document-preview]').forEach(function (trigger) {
            if (trigger.dataset.documentPreviewBound === 'true') { return; }
            trigger.dataset.documentPreviewBound = 'true';
            trigger.addEventListener('click', function (event) {
                var viewer = currentViewer();
                if (!viewer) { return; }
                event.preventDefault();
                openInViewer(trigger, viewer);
            });
        });
    }
    bind(document);
    (window.pegasusMountBinders = window.pegasusMountBinders || []).push(bind);

    // The report Generate report has just stored opens by itself, once: the
    // page that follows the generation carries the mark on its Open report
    // link, whether it was swapped in or loaded whole. The mark is taken off
    // as it is read, so a section kept across a later swap cannot open the
    // report again, and no later page carries it.
    function openOnArrival() {
        var trigger = document.querySelector('[data-document-preview][data-open-on-arrival="true"]');
        if (!trigger) { return; }
        trigger.removeAttribute('data-open-on-arrival');
        var viewer = currentViewer();
        if (viewer) { openInViewer(trigger, viewer); }
    }
    document.addEventListener('pegasus:case-swapped', openOnArrival);
    // The viewer is bound further down this file, so a whole page is read
    // once every block has run.
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', openOnArrival);
    } else {
        window.setTimeout(openOnArrival, 0);
    }
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
            store[id] = {
                id: id,
                version: number(card.getAttribute('data-preparation-version'), 0),
                // Whether the report uses the image is the server's, posted at
                // once (operator, 26 September 2026): read here, never staged.
                inReport: card.getAttribute('data-preparation-in-report') === 'true',
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
                ['OccurrenceId', value.id], ['ExpectedPreparationVersion', value.version],
                ['Order', value.inReport && value.order !== null ? value.order : ''], ['Rotation', value.rotation],
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
        // The frame listens for input on the form's controls and commits the
        // staged edits after the idle.
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
            var order = card.querySelector('[data-preparation-order]');
            var rotationLabelElement = card.querySelector('[data-preparation-rotation-label]');
            var cropLabelElement = card.querySelector('[data-preparation-crop-label]');
            if (order) { order.value = value.order === null ? '' : value.order; order.disabled = !value.inReport; }
            // v28 P41 and P50: the tile's Full page flag and its order cell
            // are an image in the report's.
            var orderCell = card.querySelector('[data-image-order-cell]');
            if (orderCell) { orderCell.hidden = !value.inReport; }
            var fullButton = card.querySelector('[data-image-full-page]');
            if (fullButton) {
                fullButton.hidden = !value.inReport;
                fullButton.setAttribute('aria-pressed', value.fullPage ? 'true' : 'false');
            }
            var fullChip = card.querySelector('[data-image-full-chip]');
            if (fullChip) { fullChip.hidden = !value.fullPage; }
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
        if (patch.order !== undefined) { value.order = patch.order === null ? null : Math.max(1, Math.floor(number(patch.order, 1))); }
        // An image the report does not use never claims a page of its own.
        if (patch.fullPage !== undefined) { value.fullPage = !!patch.fullPage; }
        if (!value.inReport) { value.fullPage = false; }
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
    // In report is posted at once through the tile's own form (operator, 26
    // September 2026), so the tile, the viewer and a click on the image all
    // press the same button.
    function toggleInReport(id, on) {
        var value = get(id);
        var form = document.querySelector('[data-image-in-report-form="' + id + '"]');
        if (!value || !form || value.inReport === on) { return; }
        form.requestSubmit();
    }
    // A document action (a tag, In report) changed the image on the server while
    // the Case form holds unsaved changes: take the preparation version, the
    // report flag and the order the server now holds, and keep the staged
    // rotation and crop.
    function adopt(id, tile) {
        var store = staged();
        var value = store && store[id];
        if (!value) { return; }
        value.version = number(tile.getAttribute('data-preparation-version'), value.version);
        var inReport = tile.getAttribute('data-preparation-in-report') === 'true';
        if (inReport !== value.inReport) {
            value.inReport = inReport;
            value.order = tile.getAttribute('data-preparation-order') ? number(tile.getAttribute('data-preparation-order'), null) : null;
            if (!inReport) { value.fullPage = false; }
        }
        if (value.changed) { writeHidden(); }
    }
    window.pegasusCasePreparation = {
        adopt: adopt,
        get: get,
        set: set,
        openCrop: function (id) { if (viewer) { viewer.openCrop(id); } },
        toggleInReport: toggleInReport
    };

    function bindPreparationCards(root) {
        all(root, '[data-preparation-card]').forEach(function (card) {
            if (card.dataset.preparationBound === 'true') { sync(card.getAttribute('data-preparation-occurrence')); return; }
            card.dataset.preparationBound = 'true';
            var value = seed(card);
            if (!value) { return; }
            var enhanced = card.querySelector('[data-image-report]');
            if (enhanced) { enhanced.hidden = false; }
            sync(value.id);
            var order = card.querySelector('[data-preparation-order]');
            if (order) { order.addEventListener('change', function () { set(value.id, { order: order.value === '' ? null : order.value }); }); }
            all(card, '[data-preparation-rotate]').forEach(function (button) {
                button.addEventListener('click', function () {
                    var current = get(value.id);
                    set(value.id, { rotation: current.rotation + number(button.getAttribute('data-preparation-rotate'), 0) });
                });
            });
            // v28 P41: Full page is a flag on an image the report uses.
            var fullPage = card.querySelector('[data-image-full-page]');
            if (fullPage) {
                fullPage.addEventListener('click', function (event) {
                    event.preventDefault();
                    event.stopPropagation();
                    var current = get(value.id);
                    if (!current || !current.inReport) { return; }
                    set(value.id, { fullPage: !current.fullPage });
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
            if (value) { inReport.checked = value.inReport; }
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
                var excluded = value ? !value.inReport : item.excluded;
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
            bringIntoView();
        }
        // Every way in (a tile, a preview, a card's crop) shows the viewer
        // through show(), so this is the one place that brings it into view
        // when the operator opened it from further up the Case.
        function bringIntoView() {
            var box = host.getBoundingClientRect();
            if (box.top >= 0 && box.bottom <= window.innerHeight) { return; }
            var reduced = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
            host.scrollIntoView({ block: 'start', behavior: reduced ? 'auto' : 'smooth' });
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
            var value = item && preparable(item) ? get(item.occurrence) : null;
            if (!value) { return; }
            var wanted = inReport.checked;
            // The box shows what is stored. The post redraws it; Keep editing
            // on the unsaved-changes question, or a refusal, posts nothing.
            inReport.checked = value.inReport;
            toggleInReport(item.occurrence, wanted);
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
                    name: (card.querySelector('h3') || {}).textContent || '', thumb: '', tag: '', occurrence: id, excluded: !value.inReport,
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
