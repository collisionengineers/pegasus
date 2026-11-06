// A placeholder button inserts its placeholder at the cursor of the field it
// names: an e-mail template's body, or a Principal's report name (R740). A
// button that names several fields inserts into the one last used, else the
// first. The fields stay plain text without this asset; site.js owns the
// shared dialog contract.
(function () {
    'use strict';

    var lastUsed = null;
    document.addEventListener('focusin', function (event) {
        if (event.target.matches('input, textarea')) {
            lastUsed = event.target;
        }
    });

    document.querySelectorAll('[data-insert-placeholder]').forEach(function (button) {
        button.addEventListener('click', function () {
            var fields = button.getAttribute('data-insert-target').split(' ')
                .map(function (id) { return document.getElementById(id); })
                .filter(function (field) { return field; });
            if (fields.length === 0) {
                return;
            }

            var body = fields.indexOf(lastUsed) >= 0 ? lastUsed : fields[0];
            var start = body.selectionStart;
            var end = body.selectionEnd;
            body.setRangeText(button.getAttribute('data-insert-placeholder'), start, end, 'end');
            body.dispatchEvent(new Event('input', { bubbles: true }));
            body.focus();
        });
    });
})();
