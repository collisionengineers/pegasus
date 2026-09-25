// Work Centre: Office/Mine is remembered per browser; the section tabs switch
// in place. Refresh is progressive: every control remains a normal link or GET
// form when script is unavailable.
(function () {
    'use strict';

    var SCOPE_KEY = 'pegasus.workCentre.scope';
    var FIVE_MINUTES = 5 * 60 * 1000;
    var FOCUS_GAP = 30 * 1000;
    var SECTION_NAMES = ['metrics', 'attention', 'new-cases', 'ai-jobs'];

    function root() {
        return document.querySelector('[data-work-centre]');
    }

    if (!root()) {
        return;
    }

    function readScope() {
        try {
            return window.localStorage.getItem(SCOPE_KEY);
        } catch (error) {
            return null;
        }
    }

    function writeScope(value) {
        try {
            window.localStorage.setItem(SCOPE_KEY, value);
        } catch (error) {
            // Storage refused: the choice simply is not remembered.
        }
    }

    // An open that does not name the scope follows the person's last choice.
    var params = new URLSearchParams(window.location.search);
    var stored = readScope();
    if (!params.has('scope') && (stored === 'office' || stored === 'mine')
        && stored !== root().getAttribute('data-wc-scope')) {
        params.set('scope', stored);
        window.location.replace(window.location.pathname + '?' + params.toString() + window.location.hash);
        return;
    }

    // The section tabs (v30 WD) are links to the same page with ?tab=; with
    // script they switch the rendered panels in place and write the tab into
    // the address, so a background refresh and F5 keep the section.
    function activateTab(name, focus) {
        var current = root();
        var target = current && current.querySelector('[data-wc-tab-link="' + name + '"]');
        if (!target) {
            return false;
        }
        current.querySelectorAll('[data-wc-tab-link]').forEach(function (tab) {
            var on = tab === target;
            tab.setAttribute('aria-selected', on ? 'true' : 'false');
            tab.setAttribute('tabindex', on ? '0' : '-1');
        });
        current.querySelectorAll('[role="tabpanel"][data-wc-refresh-section]').forEach(function (panel) {
            panel.hidden = panel.getAttribute('data-wc-refresh-section') !== name;
        });
        current.setAttribute('data-wc-tab', name);
        var url = new URL(window.location.href);
        if (name === 'attention') {
            url.searchParams.delete('tab');
        } else {
            url.searchParams.set('tab', name);
        }
        window.history.replaceState(null, '', url.toString());
        if (focus) {
            target.focus();
        }
        return true;
    }

    document.addEventListener('click', function (event) {
        var scopeLink = event.target.closest('[data-wc-scope-link]');
        if (scopeLink) {
            writeScope(scopeLink.getAttribute('data-wc-scope-link'));
            return;
        }

        var tab = event.target.closest('[data-wc-tab-link]');
        if (tab && activateTab(tab.getAttribute('data-wc-tab-link'), false)) {
            event.preventDefault();
            return;
        }

        var opener = event.target.closest('[data-wc-dialog]');
        if (opener) {
            var dialog = document.getElementById(opener.getAttribute('data-wc-dialog'));
            if (dialog && typeof dialog.pegasusOpen === 'function') {
                event.preventDefault();
                dialog.pegasusOpen(opener);
            }
        }
    });

    var lastRefresh = Date.now();
    var refreshing = false;

    function openDialog() {
        return document.querySelector('[data-dialog]:not([hidden]), [data-reason-dialog]:not([hidden]), dialog[open]');
    }

    function operatorIsWorking(current) {
        var active = document.activeElement;
        if (openDialog()) {
            return true;
        }
        if (active && active.matches('input, select, textarea, [contenteditable="true"]')) {
            return true;
        }
        return Boolean(active && current.contains(active)
            && active.matches('a, button, input, select, textarea, summary, [tabindex], [contenteditable="true"]'));
    }

    function section(rootElement, name) {
        return rootElement.querySelector('[data-wc-refresh-section="' + name + '"]');
    }

    function captureScroll(current) {
        return {
            top: window.scrollY,
            left: window.scrollX,
            regions: Array.prototype.map.call(
                current.querySelectorAll('[data-row-list], .pane-scroll'),
                function (element) { return { top: element.scrollTop, left: element.scrollLeft }; })
        };
    }

    function restoreScroll(next, saved) {
        window.scrollTo(saved.left, saved.top);
        Array.prototype.forEach.call(next.querySelectorAll('[data-row-list], .pane-scroll'), function (element, index) {
            if (saved.regions[index]) {
                element.scrollTop = saved.regions[index].top;
                element.scrollLeft = saved.regions[index].left;
            }
        });
    }

    function markStale(element) {
        element.setAttribute('data-wc-refresh-state', 'stale');
        var freshness = element.querySelector('[data-wc-freshness]');
        if (freshness && freshness.getAttribute('data-wc-freshness-state') !== 'stale') {
            freshness.setAttribute('data-wc-freshness-state', 'stale');
            freshness.textContent = 'Stale · ' + freshness.textContent;
        }
    }

    function setOutcome(next, outcome) {
        next.setAttribute('data-wc-refresh-outcome', outcome);
        var label = next.querySelector('[data-wc-refresh-outcome-label]');
        if (label) {
            label.textContent = outcome === 'failed' ? 'Refresh unavailable'
                : outcome === 'partial' ? 'Partially refreshed'
                    : outcome === 'deferred' ? 'Refresh deferred' : 'Current';
        }
    }

    function markRefreshFailed(current) {
        if (!current) {
            return;
        }
        SECTION_NAMES.forEach(function (name) {
            var currentSection = section(current, name);
            if (currentSection && currentSection.getAttribute('data-wc-refresh-state') !== 'unavailable') {
                markStale(currentSection);
            }
        });
        setOutcome(current, 'failed');
    }

    function applyRefresh(html) {
        if (/<html[\s>]/i.test(html)) {
            return 'failed';
        }
        var parsed = new DOMParser().parseFromString(html, 'text/html');
        var next = parsed.querySelector('[data-work-centre]');
        var live = root();
        if (!next || !live) {
            return 'failed';
        }
        if (operatorIsWorking(live)) {
            return 'deferred';
        }

        var savedScroll = captureScroll(live);
        var retained = false;
        var pairs = SECTION_NAMES.map(function (name) {
            return { next: section(next, name), live: section(live, name) };
        });
        // Validate the entire response before moving any last-good live nodes.
        if (pairs.some(function (pair) { return !pair.next || !pair.live; })) {
            return 'failed';
        }
        var successfulSection = pairs.some(function (pair) {
            return pair.next.getAttribute('data-wc-refresh-state') === 'current';
        });
        if (!successfulSection) {
            return 'failed';
        }
        for (var index = 0; index < SECTION_NAMES.length; index += 1) {
            var nextSection = pairs[index].next;
            var liveSection = pairs[index].live;

            if (nextSection.getAttribute('data-wc-refresh-state') === 'current') {
                continue;
            }

            // Retain an independently failed section only after it has had a
            // successful render. Moving the existing node preserves its bound
            // controls and its last truthful update time.
            if (liveSection.getAttribute('data-wc-refresh-state') !== 'unavailable') {
                markStale(liveSection);
                nextSection.replaceWith(liveSection);
                retained = true;
            }
        }

        // Adopt rather than clone: a retained stale section keeps its existing
        // event listeners and controls while the successful sections are new.
        var adopted = document.adoptNode(next);
        live.replaceWith(adopted);
        restoreScroll(adopted, savedScroll);
        if (retained) {
            setOutcome(adopted, 'partial');
        }
        (window.pegasusMountBinders || []).forEach(function (bind) { bind(adopted); });
        lastRefresh = Date.now();
        return 'applied';
    }

    function refresh() {
        var current = root();
        if (refreshing || !current) {
            return;
        }
        if (operatorIsWorking(current)) {
            // A live form or dialog is protected from replacement. This is a
            // deferred refresh, not a failed one, so its last-good sections
            // remain current and no fresh timestamp is claimed.
            setOutcome(current, 'deferred');
            return;
        }

        refreshing = true;
        var url = new URL(window.location.href);
        url.hash = '';
        url.searchParams.delete('assign');
        url.searchParams.set('handler', 'Refresh');
        url.searchParams.set('refresh', 'true');
        if (!url.searchParams.has('selected')) {
            var selected = current.querySelector('[data-wc-row][aria-current="true"]');
            if (selected) {
                url.searchParams.set('selected', selected.getAttribute('data-wc-row'));
            }
        }
        var since = current.getAttribute('data-wc-since');
        if (since) {
            url.searchParams.set('since', since);
        }

        window.fetch(url.toString(), { credentials: 'same-origin', headers: { Accept: 'text/html' } })
            .then(function (response) {
                if (!response.ok || response.redirected) {
                    return Promise.reject(new Error('refresh failed'));
                }
                return response.text();
            })
            .then(applyRefresh)
            .then(function (outcome) {
                if (outcome === 'failed') {
                    markRefreshFailed(root());
                }
                else if (outcome === 'deferred') {
                    setOutcome(root(), 'deferred');
                }
            })
            .catch(function () {
                // Transport, sign-in redirects and malformed fragments retain
                // the last good display but make its freshness explicit.
                markRefreshFailed(root());
            })
            .then(function () {
                refreshing = false;
            });
    }

    document.addEventListener('keydown', function (event) {
        var tab = event.target.closest('[data-wc-tab-link]');
        if (!tab || ['ArrowLeft', 'ArrowRight', 'Home', 'End'].indexOf(event.key) < 0) {
            return;
        }
        var current = root();
        var tabs = current ? Array.prototype.map.call(current.querySelectorAll('[data-wc-tab-link]'), function (element) {
            return element.getAttribute('data-wc-tab-link');
        }) : [];
        var index = tabs.indexOf(tab.getAttribute('data-wc-tab-link'));
        if (index < 0) {
            return;
        }
        event.preventDefault();
        var next = event.key === 'Home' ? 0
            : event.key === 'End' ? tabs.length - 1
                : (index + (event.key === 'ArrowRight' ? 1 : tabs.length - 1)) % tabs.length;
        activateTab(tabs[next], true);
    });

    window.setInterval(refresh, FIVE_MINUTES);
    document.addEventListener('visibilitychange', function () {
        if (document.visibilityState === 'visible' && Date.now() - lastRefresh > FOCUS_GAP) {
            refresh();
        }
    });
    window.addEventListener('focus', function () {
        if (Date.now() - lastRefresh > FOCUS_GAP) {
            refresh();
        }
    });
})();
