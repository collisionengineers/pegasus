// Bouncing a Glass's return through Pegasus's own origin (CASE-047 B04).
//
// The provider navigates this window to the return address from its own
// site, and the staff cookie is SameSite=Strict, so that first arrival
// carries no session. Asking for the same address from this document makes
// the navigation same-site, and the cookie travels with it. The document's
// meta refresh does the same for a browser without script.
(function () {
    'use strict';

    var url = document.body.getAttribute('data-glass-bounce');
    if (!url) { return; }

    window.location.replace(url);
})();
