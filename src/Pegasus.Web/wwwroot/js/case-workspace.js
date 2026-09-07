// Case workspace enhancements Stream B owns (CASE-047/B08). site.css and
// site.js are Stream C's; every behaviour here is Case-record-only.
//
// Progressive enhancement only. This file exists as a file rather than as an
// inline <script> because the deployed Content-Security-Policy is
// `default-src 'self'` with no nonce or hash allowance, so an inline script is
// silently discarded in Production.
(function () {
    'use strict';

    // B08: dragging a Supporting report image onto another one reorders the
    // Supporting sequence. It is a convenience on top of the Move up and Move
    // down forms, which stay the keyboard and script-off route, and it submits
    // the same SaveAssetPreparation command they submit: the fields below are
    // the ones the server already rendered on the card, never a second
    // endpoint and never a JSON API.
    var ENVELOPE = [
        '__RequestVerificationToken',
        'id',
        'expectedVersion',
        'operationKey',
        'editLeaseToken'
    ];

    // A card only carries the shape of its own edit while it is a draggable
    // Supporting image in edit mode, so this selector is also the test for
    // "may this be reordered at all".
    var SUPPORTING = 'article.report-image[draggable="true"]';

    document.querySelectorAll('[data-report-images]').forEach(function (panel) {
        var dragged = null;

        var sequence = function () {
            // The server renders the order on each card; reading it back is
            // what makes the sequence the operator sees the one reordered,
            // whatever order the cards happen to sit in.
            return Array.prototype.slice.call(panel.querySelectorAll(SUPPORTING))
                .filter(function (card) {
                    return Number.isFinite(order(card));
                })
                .sort(function (left, right) {
                    return order(left) - order(right);
                });
        };

        var order = function (card) {
            var raw = card.getAttribute('data-report-order');
            return raw === null || raw === '' ? NaN : Number(raw);
        };

        panel.addEventListener('dragstart', function (event) {
            var card = event.target.closest ? event.target.closest(SUPPORTING) : null;
            if (!card || !panel.contains(card)) {
                return;
            }
            dragged = card;
            card.setAttribute('data-dragging', 'true');
            if (event.dataTransfer) {
                event.dataTransfer.effectAllowed = 'move';
                // Firefox starts no drag at all without payload; the card is
                // identified by the element, so the text is only the name the
                // operator can already read.
                event.dataTransfer.setData('text/plain', card.getAttribute('data-report-image') || '');
            }
        });

        panel.addEventListener('dragend', function () {
            clear();
        });

        panel.addEventListener('dragover', function (event) {
            var target = targetOf(event);
            if (!target) {
                return;
            }
            // Without this the browser refuses the drop outright.
            event.preventDefault();
            if (event.dataTransfer) {
                event.dataTransfer.dropEffect = 'move';
            }
            target.setAttribute('data-drop-target', 'true');
        });

        panel.addEventListener('dragleave', function (event) {
            var target = event.target.closest ? event.target.closest(SUPPORTING) : null;
            if (target) {
                target.removeAttribute('data-drop-target');
            }
        });

        panel.addEventListener('drop', function (event) {
            var target = targetOf(event);
            if (!target) {
                return;
            }
            event.preventDefault();
            var moved = dragged;
            clear();
            submitReorder(moved, target);
        });

        var targetOf = function (event) {
            if (!dragged || !event.target.closest) {
                return null;
            }
            var target = event.target.closest(SUPPORTING);
            if (!target || target === dragged || !panel.contains(target)) {
                return null;
            }
            return target;
        };

        var clear = function () {
            if (dragged) {
                dragged.removeAttribute('data-dragging');
            }
            dragged = null;
            panel.querySelectorAll('[data-drop-target]').forEach(function (card) {
                card.removeAttribute('data-drop-target');
            });
        };

        // The dragged card takes the target's place and the rest close up
        // behind it. The Supporting orders the server rendered are then dealt
        // back out along the new sequence, so an adjacent drop exchanges
        // exactly the two neighbours — the same command Move up and Move down
        // post — and a longer drop carries every image the move re-sequenced
        // in that one command.
        var submitReorder = function (moved, target) {
            var cards = sequence();
            var from = cards.indexOf(moved);
            var to = cards.indexOf(target);
            if (from < 0 || to < 0 || from === to) {
                return;
            }
            var orders = cards.map(order);
            var next = cards.slice();
            next.splice(from, 1);
            next.splice(to, 0, moved);

            var changed = [];
            next.forEach(function (card, index) {
                if (order(card) !== orders[index]) {
                    changed.push({ card: card, order: orders[index] });
                }
            });
            if (changed.length === 0) {
                return;
            }

            var form = commandForm();
            if (!form) {
                return;
            }
            changed.forEach(function (edit, index) {
                append(form, 'edits[' + index + '].occurrenceId', edit.card.getAttribute('data-report-image'));
                append(form, 'edits[' + index + '].expectedPreparationVersion', edit.card.getAttribute('data-preparation-version'));
                append(form, 'edits[' + index + '].role', edit.card.getAttribute('data-report-role'));
                append(form, 'edits[' + index + '].order', String(edit.order));
                append(form, 'edits[' + index + '].rotation', edit.card.getAttribute('data-report-rotation'));
                append(form, 'edits[' + index + '].cropLeft', edit.card.getAttribute('data-crop-left'));
                append(form, 'edits[' + index + '].cropTop', edit.card.getAttribute('data-crop-top'));
                append(form, 'edits[' + index + '].cropWidth', edit.card.getAttribute('data-crop-width'));
                append(form, 'edits[' + index + '].cropHeight', edit.card.getAttribute('data-crop-height'));
            });
            document.body.appendChild(form);
            form.submit();
        };

        // The action, the method, the anti-forgery token and the case
        // envelope are taken from a save form the server already rendered in
        // this section, so the drag posts to the same handler, with the same
        // guard and the same section, as the buttons beside it.
        var commandForm = function () {
            var template = panel.querySelector('form[data-preparation-command="save"]');
            if (!template) {
                return null;
            }
            var form = template.cloneNode(true);
            form.removeAttribute('data-preparation-command');
            form.hidden = true;
            form.querySelectorAll('input, select, textarea, button').forEach(function (control) {
                if (ENVELOPE.indexOf(control.getAttribute('name')) < 0) {
                    control.remove();
                }
            });
            return form;
        };

        var append = function (form, name, value) {
            var field = document.createElement('input');
            field.type = 'hidden';
            field.name = name;
            field.value = value === null ? '' : value;
            form.appendChild(field);
        };
    });
})();
