// Cases list (v26): a click or Enter on a row (not on its reference link)
// selects it for the Quick detail. Without script the reference link opens
// the record and ?selected= still selects server-side.
(function () {
    'use strict';

    var rows = document.querySelectorAll('tr[data-select-url]');
    if (rows.length === 0) {
        return;
    }

    function go(row) {
        window.location.assign(row.getAttribute('data-select-url'));
    }

    Array.prototype.forEach.call(rows, function (row) {
        if (!row.hasAttribute('tabindex')) {
            row.setAttribute('tabindex', '0');
        }
        row.addEventListener('click', function (event) {
            if (event.target.closest('a, button, input, select, textarea')) {
                return;
            }
            go(row);
        });
        row.addEventListener('keydown', function (event) {
            if (event.key === 'Enter' && !event.target.closest('a, button')) {
                event.preventDefault();
                go(row);
            }
        });
    });
})();
