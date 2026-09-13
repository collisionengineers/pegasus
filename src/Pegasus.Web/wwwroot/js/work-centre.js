// Work Centre (v26): Office/Mine remembered per browser, the Assign Engineer
// dialog opened in place, and freshness (P5) - refresh when the tab regains
// focus and every five minutes. The page works without this file: every
// control is a link or a form.
(function () {
    'use strict';

    var SCOPE_KEY = 'pegasus.workCentre.scope';
    var FIVE_MINUTES = 5 * 60 * 1000;
    var FOCUS_GAP = 30 * 1000;

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

    document.addEventListener('click', function (event) {
        var scopeLink = event.target.closest('[data-wc-scope-link]');
        if (scopeLink) {
            writeScope(scopeLink.getAttribute('data-wc-scope-link'));
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

    function busy() {
        var active = document.activeElement;
        return refreshing
            || document.querySelector('[data-work-centre] [data-dialog]:not([hidden])')
            || (active && active.closest('[data-work-centre]') && active.matches('input, select, textarea'));
    }

    function refresh() {
        var current = root();
        if (!current || busy()) {
            return;
        }

        refreshing = true;
        var url = new URL(window.location.href);
        url.hash = '';
        url.searchParams.set('refresh', 'true');
        var since = current.getAttribute('data-wc-since');
        if (since) {
            url.searchParams.set('since', since);
        }

        window.fetch(url.toString(), { credentials: 'same-origin', headers: { Accept: 'text/html' } })
            .then(function (response) {
                return response.ok ? response.text() : Promise.reject(new Error('refresh failed'));
            })
            .then(function (html) {
                var next = new DOMParser().parseFromString(html, 'text/html').querySelector('[data-work-centre]');
                var live = root();
                if (!next || !live || busy()) {
                    return;
                }
                var adopted = document.importNode(next, true);
                live.replaceWith(adopted);
                (window.pegasusMountBinders || []).forEach(function (bind) { bind(adopted); });
                lastRefresh = Date.now();
            })
            .catch(function () {
                // A failed refresh keeps the last good page; the Updated time says how old it is.
            })
            .then(function () {
                refreshing = false;
            });
    }

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
