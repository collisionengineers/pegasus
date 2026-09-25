// Same-origin handoff: refresh the owning Case before visiting the provider,
// or report Save & Exit without navigating away from unsaved Case changes.
(function () {
    'use strict';
    var launch = document.body.getAttribute('data-glass-launch');
    var returned = document.body.getAttribute('data-glass-return');
    var url = launch || returned;
    if (!url) { return; }
    function continueHere() { window.location.replace(url); }
    try {
        var opener = window.opener;
        var handler = opener && !opener.closed && (launch ? opener.pegasusGlassHandoff : opener.pegasusGlassReturn);
        if (typeof handler === 'function') {
            // Do not hold the provider link hostage to an unavailable Case read.
            // The anchor remains usable throughout and without script/opener.
            Promise.race([
                Promise.resolve(handler.call(opener, returned)),
                new Promise(function (_, reject) { window.setTimeout(function () { reject(new Error('Case refresh timed out.')); }, 10000); })
            ]).then(function () {
                if (launch) { continueHere(); } else { window.close(); }
            }).catch(continueHere);
            return;
        }
    } catch (_) { /* Departed or cross-origin opener: use this window. */ }
    continueHere();
})();
