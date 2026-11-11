// A photo link's Photos filters (FRD-28; operator, 11 November 2026; v51 B;
// R948): All, Overview, Damage and Vehicle ID show the tiles of one kind in
// place. The filters are drawn hidden and shown here, so without this asset
// every photo shows and nothing offers a filter that cannot work. The
// evidence viewer still pages through every photo of the link.
(function () {
    'use strict';

    document.querySelectorAll('[data-photo-filters]').forEach(function (group) {
        var panel = group.closest('[data-photo-link-photos]');
        if (!panel) {
            return;
        }

        var buttons = Array.prototype.slice.call(group.querySelectorAll('[data-photo-filter]'));
        var tiles = Array.prototype.slice.call(panel.querySelectorAll('[data-evidence-set] > li'));

        function choose(key) {
            buttons.forEach(function (button) {
                button.setAttribute('aria-pressed', String(button.getAttribute('data-photo-filter') === key));
            });
            tiles.forEach(function (tile) {
                tile.hidden = key !== 'all' && tile.getAttribute('data-photo-category') !== key;
            });
        }

        buttons.forEach(function (button) {
            button.addEventListener('click', function () {
                choose(button.getAttribute('data-photo-filter'));
            });
        });
        group.hidden = false;
    });
})();
