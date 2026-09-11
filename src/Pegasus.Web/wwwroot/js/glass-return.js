// Handing a Glass's outcome back to the Case window (CASE-047 B04).
//
// The estimator runs in a window the Case record opened, so when the provider
// returns, or a launch or resume stops short of the estimator, this window
// holds nothing the operator needs: the outcome is reported on the Case
// record. The Case window is sent to its Estimate section and this one
// closes. With no opener — script-less, or a tab the browser opened without
// one — this window goes there itself; the document's meta refresh does the
// same for a browser without script at all.
(function () {
    'use strict';

    var url = document.body.getAttribute('data-glass-return');
    if (!url) { return; }

    var opener = window.opener;
    if (opener && !opener.closed) {
        try {
            if (typeof opener.pegasusGlassReturn === 'function') {
                opener.pegasusGlassReturn(url);
            } else {
                opener.location.assign(url);
            }
            window.close();
            return;
        } catch (error) {
            // A cross-origin or departed opener: fall through and go there ourselves.
        }
    }

    window.location.replace(url);
})();
