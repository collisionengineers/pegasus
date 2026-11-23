// Same-origin return: report Save & Exit to the owning Case without
// navigating away from unsaved Case changes, then close this window.
(function () {
    'use strict';
    var holder = document.querySelector('[data-glass-return]');
    var url = holder && holder.getAttribute('data-glass-return');
    if (!url) { return; }
    // The window's status is busy until it closes; site.js draws the loader.
    if (window.pegasusBusy && holder.hasAttribute('data-glass-wait')) {
        window.pegasusBusy.start(holder, null, holder.textContent.trim());
    }
    function continueHere() { window.location.replace(url); }
    try {
        var opener = window.opener;
        var handler = opener && !opener.closed && opener.pegasusGlassReturn;
        if (typeof handler === 'function') {
            // Do not hold this window on an unavailable Case read.
            Promise.race([
                Promise.resolve(handler.call(opener, url)),
                new Promise(function (_, reject) { window.setTimeout(function () { reject(new Error('Case refresh timed out.')); }, 10000); })
            ]).then(function () { window.close(); }).catch(continueHere);
            return;
        }
    } catch (_) { /* Departed or cross-origin opener: use this window. */ }
    continueHere();
})();
