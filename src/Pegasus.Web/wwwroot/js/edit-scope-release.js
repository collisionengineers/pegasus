// Releasing an edit scope when its page goes away.
//
// A record edit scope is server-owned and outlives the page that claimed it,
// so an operator who navigates away or closes the tab would otherwise be
// refused by their own lease until it expired. This beacons the page's own
// release form as the page unloads. It is best effort by construction: the
// store's rule that a holder may replace their own unbeaten scope is the
// guarantee, and this only makes the common case immediate.
//
// The form is a real posted form so its antiforgery token rides in the
// FormData, exactly as the heartbeat forms do; there is no second antiforgery
// mechanism and no separate token anywhere in this file.
(function () {
    'use strict';

    if (typeof navigator === 'undefined' || typeof navigator.sendBeacon !== 'function') {
        return;
    }

    // A page that is unloading because the operator pressed Save, Cancel or
    // Refresh is already telling the server what to do with the scope. Beacons
    // sent alongside that post would race it, and a release that overtook a
    // save would refuse the save, so a submit in flight suppresses them. A
    // plain GET submit — a filter or search field — changes nothing about the
    // scope and must never suppress the release.
    var submitting = false;
    var submittingTimeoutId = null;
    document.addEventListener('submit', function (event) {
        var form = event.target;
        if (form && typeof form.method === 'string' && form.method.toLowerCase() === 'get') {
            return;
        }

        submitting = true;
        if (submittingTimeoutId !== null) {
            clearTimeout(submittingTimeoutId);
        }
        // A page that does not navigate away after all — a post answered in
        // place, such as a refused save — must not leave the latch stuck, so
        // it also clears on a short timer rather than relying only on
        // pageshow, which fires solely for a back/forward-cache restore.
        submittingTimeoutId = setTimeout(function () {
            submitting = false;
            submittingTimeoutId = null;
        }, 2000);
    }, true);

    // A navigation the page itself asked for on a command's behalf — the
    // Glass's window handing its outcome back to this Case record — keeps the
    // scope exactly as a posted command does.
    window.pegasusHoldEditScopeRelease = function () {
        submitting = true;
        if (submittingTimeoutId !== null) {
            clearTimeout(submittingTimeoutId);
            submittingTimeoutId = null;
        }
    };

    // Restored from the back/forward cache: the earlier submit is over and the
    // page is live again, so the next departure releases as usual.
    window.addEventListener('pageshow', function (event) {
        if (event.persisted) {
            submitting = false;
            if (submittingTimeoutId !== null) {
                clearTimeout(submittingTimeoutId);
                submittingTimeoutId = null;
            }
        }
    });

    window.addEventListener('pagehide', function () {
        if (submitting) {
            return;
        }

        document.querySelectorAll('form[data-edit-scope-release]').forEach(function (form) {
            navigator.sendBeacon(form.action, new FormData(form));
        });
    });
})();
