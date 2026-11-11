// Send a photo link and Resend (FRD-28; R806, R808, R919): one composer that
// shows one channel at a time, Email, Text message or Link to copy (operator,
// 11 November 2026; v51 L, M; R948). "Edited for this one only." shows as soon
// as a message (the e-mail's or the text's) differs from its standard wording,
// and "Put the standard wording back" restores it in place; "Type" shows, and
// is required, only while Quick is chosen. The form remains fully usable
// without this asset: the switch still shows its channel by CSS, the reset
// button posts and the page returns with the standard wording.
//
// A link carrying data-photo-link-composer (Send photo link, Resend) opens its
// composer as a dialog over the page instead of navigating, where the page
// holds the [data-dialog="photo-link-compose"] backdrop: the composer is
// fetched from the link's own address with ?handler=Composer each time, and
// its form posts to the page as the page's own form does. A failed fetch
// follows the link, so the page itself is always the fallback.
(function () {
    'use strict';

    function normalized(text) {
        return text.replace(/\r\n/g, '\n').trim();
    }

    function bindComposer(form) {
        if (form.dataset.photoLinkBound === 'true') {
            return;
        }
        form.dataset.photoLinkBound = 'true';

        // The channel: the checked switch shows its own boxes and button. The
        // others' boxes stay in the form, posted as they stand.
        var showChannel = function () {
            var chosen = form.querySelector('input[name="Channel"]:checked');
            if (!chosen) {
                return;
            }
            form.querySelectorAll('[data-photo-link-for]').forEach(function (part) {
                part.hidden = part.getAttribute('data-photo-link-for') !== chosen.value;
            });
        };
        form.querySelectorAll('input[name="Channel"]').forEach(function (radio) {
            radio.addEventListener('change', showChannel);
        });
        showChannel();

        // A box the browser refuses in a channel not shown would stop the
        // press without a word: show that channel so the browser can say why.
        form.addEventListener('invalid', function (event) {
            var part = event.target instanceof Element ? event.target.closest('[data-photo-link-for]') : null;
            if (!part || !part.hidden) {
                return;
            }
            var radio = form.querySelector('input[name="Channel"][value="' + part.getAttribute('data-photo-link-for') + '"]');
            if (radio) {
                radio.checked = true;
                showChannel();
            }
        }, true);

        // "Type" shows, and is required, only while Quick is chosen (R808).
        var type = form.querySelector('[data-photo-link-type]');
        var mode = form.querySelector('[data-photo-link-mode]');
        if (type && mode) {
            var showType = function () {
                var asked = mode.value === 'Quick';
                type.hidden = !asked;
                type.querySelectorAll('select, input').forEach(function (control) {
                    control.required = asked;
                });
            };
            mode.addEventListener('change', showType);
            showType();
        }

        // One group per message: the e-mail's and the text's (R919).
        form.querySelectorAll('[data-photo-link-wording]').forEach(function (group) {
            var message = group.querySelector('[data-photo-link-message]');
            var standard = group.querySelector('[data-photo-link-standard]');
            var edited = group.querySelector('[data-photo-link-edited]');
            var reset = group.querySelector('[data-photo-link-reset]');
            if (!message || !standard || !edited || !reset) {
                return;
            }

            var showEdited = function () {
                edited.hidden = normalized(message.value) === normalized(standard.value);
            };

            message.addEventListener('input', showEdited);
            reset.addEventListener('click', function (event) {
                event.preventDefault();
                message.value = standard.value;
                showEdited();
                message.focus();
            });
        });
    }

    // The page's own composer now, and the dialog's when its fragment mounts.
    function bind(root) {
        if (root instanceof Element && root.matches('form[data-photo-link-composer-form]')) {
            bindComposer(root);
        }
        root.querySelectorAll('form[data-photo-link-composer-form]').forEach(bindComposer);
    }
    bind(document);
    (window.pegasusMountBinders = window.pegasusMountBinders || []).push(bind);

    // The trigger: opened in the capture phase, before site.js marks a
    // navigating link busy. A modified click still opens the page.
    var opening = false;
    document.addEventListener('click', function (event) {
        if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
            return;
        }
        var trigger = event.target instanceof Element ? event.target.closest('a[href][data-photo-link-composer]') : null;
        var dialog = trigger ? document.querySelector('[data-dialog="photo-link-compose"]') : null;
        if (!dialog || typeof dialog.pegasusLoad !== 'function' || typeof dialog.pegasusOpen !== 'function') {
            return;
        }
        event.preventDefault();
        // The Actions menu the trigger sat in closes behind the dialog.
        var menu = trigger.closest('details[open]');
        if (menu) {
            menu.open = false;
        }
        if (opening) {
            return;
        }
        opening = true;
        var url = new URL(trigger.href, window.location.href);
        url.searchParams.set('handler', 'Composer');
        // Each trigger fetches its own composer, with a fresh operation key.
        dialog.setAttribute('data-dialog-fragment', url.pathname + url.search);
        dialog.pegasusLoad().then(function () {
            opening = false;
            if (dialog.hidden) {
                dialog.pegasusOpen(trigger);
            }
        }, function () {
            opening = false;
            window.location.assign(trigger.href);
        });
    }, true);
})();
