// E-mail templates: a placeholder button inserts its placeholder at the
// cursor of its template body. The body stays a plain textarea without this
// asset; site.js owns the shared dialog contract.
(function () {
    'use strict';

    document.querySelectorAll('[data-insert-placeholder]').forEach(function (button) {
        button.addEventListener('click', function () {
            var body = document.getElementById(button.getAttribute('data-insert-target'));
            if (!body) {
                return;
            }

            var start = body.selectionStart;
            var end = body.selectionEnd;
            body.setRangeText(button.getAttribute('data-insert-placeholder'), start, end, 'end');
            body.dispatchEvent(new Event('input', { bubbles: true }));
            body.focus();
        });
    });
})();
