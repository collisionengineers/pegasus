// The Glass's window while its provider work runs in the background.
//
// Asks the owner-only State handler until nothing runs for the session. An
// open estimator first refreshes the owning Case's Glass's controls through
// its handoff; then this window continues to Go, which answers with the
// estimator or hands the outcome back to the Case. The estimator address
// never reaches this script. Without script, the page's timed reload goes to
// Go instead. The window the Case page opens at the press names no addresses:
// it only waits for the launch to answer into it.
(function () {
    'use strict';

    // The status is busy from the start: site.js draws the one loader beside
    // its words, says "Still …" after five seconds and announces both.
    var status = document.querySelector('[data-glass-wait]');
    if (status && window.pegasusBusy) { window.pegasusBusy.start(status, null, status.textContent.trim()); }

    var holder = document.querySelector('[data-glass-opening-state]');
    var stateUrl = holder && holder.getAttribute('data-glass-opening-state');
    var goUrl = holder && holder.getAttribute('data-glass-opening-go');
    if (!stateUrl || !goUrl) { return; }

    function go() { window.location.replace(goUrl); }

    function handOff() {
        try {
            var opener = window.opener;
            var handoff = opener && !opener.closed && opener.pegasusGlassHandoff;
            if (typeof handoff === 'function') {
                // Do not hold the estimator hostage to an unavailable Case read.
                Promise.race([
                    Promise.resolve(handoff.call(opener)),
                    new Promise(function (_, reject) { window.setTimeout(function () { reject(new Error('Case refresh timed out.')); }, 10000); })
                ]).then(go, go);
                return;
            }
        } catch (_) { /* Departed or cross-origin opener: continue here. */ }
        go();
    }

    function poll() {
        fetch(stateUrl, { credentials: 'same-origin', cache: 'no-store', headers: { 'Accept': 'application/json' } })
            .then(function (response) {
                if (!response.ok) { throw new Error("Glass's state is unavailable."); }
                return response.json();
            })
            .then(function (state) {
                if (state.pending) { window.setTimeout(poll, 1000); return; }
                if (state.open) { handOff(); } else { go(); }
            })
            // Go itself sends the window back here while work still runs.
            .catch(go);
    }

    poll();
})();
