// The Glass's window while its provider work runs in the background.
//
// Asks the owner-only State handler until nothing runs for the session. An
// open estimator first refreshes the owning Case's Glass's controls through
// its handoff; then this window continues to Go, which answers with the
// estimator or hands the outcome back to the Case. The estimator address
// never reaches this script. Without script, the Continue link does the same.
(function () {
    'use strict';

    var stateUrl = document.body.getAttribute('data-glass-opening-state');
    var goUrl = document.body.getAttribute('data-glass-opening-go');
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
