// Progressive enhancement only: every behaviour here is a convenience on top of
// markup that already works without it.
//
// This file exists as a file rather than as inline <script> blocks because the
// deployed Content-Security-Policy is `default-src 'self'` with no nonce or
// hash allowance, so an inline script is silently discarded in Production. The
// enhancements below therefore ran only in Development until they moved here.
(function () {
    'use strict';

    // Bounded status pages ask to be reloaded while their state is still
    // moving; the delay is data on the element, so nothing here is inline.
    var autoRefresh = document.querySelector('[data-auto-refresh]');
    if (autoRefresh) {
        var delay = Number(autoRefresh.getAttribute('data-auto-refresh'));
        if (Number.isFinite(delay) && delay > 0) {
            window.setTimeout(function () { window.location.reload(); }, delay);
        }
    }

    // Manual refresh feedback. The label change is the signal; the spin is
    // decoration on top of it, so the feedback still reads correctly under
    // reduced motion or with no CSS at all.
    document.querySelectorAll('[data-refresh-form]').forEach(function (form) {
        form.addEventListener('submit', function () {
            var region = form.closest('[data-refresh-region]') || form.parentElement;
            if (region) {
                region.classList.add('is-refreshing');
                region.setAttribute('aria-busy', 'true');
            }
            var label = form.querySelector('[data-refresh-label]');
            if (label) {
                label.textContent = 'Refreshing';
            }
            form.querySelectorAll('button').forEach(function (button) {
                button.disabled = true;
            });
        });
    });

    // Copy a support reference. Without script the value is still selectable
    // text, which is why the button is rendered hidden and revealed here rather
    // than shipped as a control that might do nothing.
    document.querySelectorAll('[data-copy-target]').forEach(function (button) {
        var source = document.getElementById(button.getAttribute('data-copy-target'));
        if (!source || !navigator.clipboard) {
            return;
        }

        button.hidden = false;
        button.addEventListener('click', function () {
            navigator.clipboard.writeText(source.textContent.trim()).then(function () {
                var original = button.textContent;
                button.textContent = 'Copied';
                window.setTimeout(function () { button.textContent = original; }, 2000);
            });
        });
    });

    // Reason dialogs: a focus trap so a modal that asks for a required reason
    // cannot be tabbed out of while it is open.
    document.querySelectorAll('dialog[data-focus-trap]').forEach(function (dialog) {
        dialog.addEventListener('keydown', function (event) {
            if (event.key !== 'Tab') {
                return;
            }

            var focusable = dialog.querySelectorAll(
                'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])');
            if (focusable.length === 0) {
                return;
            }

            var first = focusable[0];
            var last = focusable[focusable.length - 1];
            if (event.shiftKey && document.activeElement === first) {
                event.preventDefault();
                last.focus();
            } else if (!event.shiftKey && document.activeElement === last) {
                event.preventDefault();
                first.focus();
            }
        });
    });

    // Buttons that open their own dialog, so an action can carry its fields
    // without the page shipping a permanently open form for every action.
    document.querySelectorAll('[data-dialog-open]').forEach(function (trigger) {
        var dialog = document.getElementById(trigger.getAttribute('data-dialog-open'));
        if (!dialog || typeof dialog.showModal !== 'function') {
            return;
        }

        trigger.addEventListener('click', function (event) {
            event.preventDefault();
            dialog.showModal();
            var initial = dialog.querySelector('[data-dialog-initial-focus]')
                || dialog.querySelector('input, select, textarea, button');
            if (initial) {
                initial.focus();
            }
        });
    });

    document.querySelectorAll('[data-dialog-close]').forEach(function (button) {
        button.addEventListener('click', function (event) {
            event.preventDefault();
            var dialog = button.closest('dialog');
            if (dialog) {
                dialog.close();
            }
        });
    });

    // Live character counters for reason fields whose limit is policy.
    document.querySelectorAll('[data-counter-for]').forEach(function (counter) {
        var field = document.getElementById(counter.getAttribute('data-counter-for'));
        if (!field) {
            return;
        }

        var limit = field.getAttribute('maxlength');
        var render = function () {
            counter.textContent = field.value.length + '/' + limit + ' characters';
        };

        field.addEventListener('input', render);
        render();
    });

}());
