// Same-origin return: report Save & Exit to the owning Case without
// navigating away from unsaved Case changes, then close this window.
(function () {
    'use strict';
    var url = document.body.getAttribute('data-glass-return');
    if (!url) { return; }
    function continueHere() { window.location.replace(url); }
    try {
        var opener = window.opener;
        var handler = opener && !opener.closed && opener.pegasusGlassReturn;
        if (typeof handler === 'function') {
            // Do not hold this window on an unavailable Case read.
            // The anchor remains usable throughout and without script/opener.
            Promise.race([
                Promise.resolve(handler.call(opener, url)),
                new Promise(function (_, reject) { window.setTimeout(function () { reject(new Error('Case refresh timed out.')); }, 10000); })
            ]).then(function () { window.close(); }).catch(continueHere);
            return;
        }
    } catch (_) { /* Departed or cross-origin opener: use this window. */ }
    continueHere();
})();
