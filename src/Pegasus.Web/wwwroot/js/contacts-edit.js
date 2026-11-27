// Contacts edit enhancements. Every form remains fully usable without this
// asset; site.js owns the shared data-dialog-open/close contract.
(function () {
    'use strict';

    // The Sending rules dialog (v55). Without script its And, Or, Add action
    // and remove buttons post the draft and the server draws it again with a
    // row more or fewer, and every row shows all its value controls. With
    // script the same rows are added and removed in place, from the dialog's
    // own templates, and each row shows only the value its fact or action
    // reads. Rows are posted as Cond.{group}.{row}.* and Action.{row}.*; the
    // server reads them in the order of their numbers, so a new row takes a
    // number above the rest.
    document.querySelectorAll('[data-sending-rule-form]').forEach(function (form) {
        var numberAfter = function (selector, part) {
            var highest = -1;
            form.querySelectorAll(selector).forEach(function (control) {
                var value = parseInt((control.getAttribute('name') || '').split('.')[part], 10);
                if (!isNaN(value) && value > highest) { highest = value; }
            });
            return String(highest + 1);
        };
        var fit = function (row) {
            var choice = row.querySelector('[data-rule-choice]');
            if (!choice) { return; }
            var value = choice.value;
            row.querySelectorAll('[data-rule-value]').forEach(function (holder) {
                holder.hidden = holder.getAttribute('data-rule-value').split(' ').indexOf(value) < 0;
            });
            var option = choice.options[choice.selectedIndex];
            row.querySelectorAll('[data-rule-named]').forEach(function (box) {
                if (option) { box.setAttribute('aria-label', option.text); }
            });
            row.querySelectorAll('[data-rule-task-list]').forEach(function (box) {
                if (value === 'Task') { box.setAttribute('list', box.getAttribute('data-rule-task-list')); } else { box.removeAttribute('list'); }
            });
        };
        // The structural buttons act in place, so they no longer submit.
        var prepare = function (root) {
            root.querySelectorAll('[data-rule-change]').forEach(function (button) { button.type = 'button'; });
            root.querySelectorAll('[data-rule-condition], [data-rule-action]').forEach(fit);
        };
        var instantiate = function (selector, numbers) {
            var template = form.querySelector(selector);
            if (!template) { return null; }
            var fragment = template.content.cloneNode(true);
            fragment.querySelectorAll('[name], [value], [data-rule-group]').forEach(function (element) {
                ['name', 'value', 'data-rule-group'].forEach(function (attribute) {
                    var text = element.getAttribute(attribute);
                    if (text === null) { return; }
                    Object.keys(numbers).forEach(function (key) { text = text.split(key).join(numbers[key]); });
                    element.setAttribute(attribute, text);
                });
            });
            prepare(fragment);
            return fragment;
        };
        var focusFirst = function (element) {
            var control = element && element.querySelector('select, input:not([type="hidden"])');
            if (control) { control.focus(); }
        };

        prepare(form);
        form.addEventListener('change', function (event) {
            var row = event.target.closest('[data-rule-condition], [data-rule-action]');
            if (row && event.target.matches('[data-rule-choice]')) { fit(row); }
        });
        form.addEventListener('click', function (event) {
            var button = event.target.closest('[data-rule-change]');
            if (!button) { return; }
            event.preventDefault();
            var buttonRow = button.closest('.button-row');
            var row;
            var card = button.closest('[data-rule-group]');
            if (button.hasAttribute('data-rule-add-group')) {
                var ifBlock = button.closest('[data-rule-if]');
                var group = numberAfter('[data-rule-if] [name$=".Fact"]', 1);
                var groupFragment = instantiate('[data-rule-group-template]', { '__G__': group });
                if (!groupFragment) { return; }
                // The first group has no "or" before it.
                if (!ifBlock.querySelector('[data-rule-group]')) {
                    var divider = groupFragment.querySelector('[data-rule-or]');
                    if (divider) { divider.remove(); }
                }
                var newCard = groupFragment.querySelector('[data-rule-group]');
                ifBlock.insertBefore(groupFragment, buttonRow);
                focusFirst(newCard);
                return;
            }
            if (card && buttonRow && buttonRow.parentElement === card) {
                var groupNumber = card.getAttribute('data-rule-group');
                var conditionFragment = instantiate('[data-rule-condition-template]', {
                    '__G__': groupNumber,
                    '__C__': (function () {
                        var highest = -1;
                        card.querySelectorAll('[name$=".Fact"]').forEach(function (select) {
                            var value = parseInt(select.getAttribute('name').split('.')[2], 10);
                            if (!isNaN(value) && value > highest) { highest = value; }
                        });
                        return String(highest + 1);
                    })()
                });
                if (!conditionFragment) { return; }
                row = conditionFragment.querySelector('[data-rule-condition]');
                card.insertBefore(conditionFragment, buttonRow);
                focusFirst(row);
                return;
            }
            if (button.hasAttribute('data-rule-add-action')) {
                var actionFragment = instantiate('[data-rule-action-template]', { '__A__': numberAfter('[data-rule-then] [name$=".Kind"]', 1) });
                if (!actionFragment) { return; }
                row = actionFragment.querySelector('[data-rule-action]');
                button.closest('[data-rule-then]').insertBefore(actionFragment, buttonRow);
                focusFirst(row);
                return;
            }
            row = button.closest('[data-rule-condition], [data-rule-action]');
            if (!row) { return; }
            var block = row.closest('[data-rule-if], [data-rule-then]');
            row.remove();
            // A group left with no condition goes, with the "or" that joined it.
            if (card && !card.querySelector('[data-rule-condition]')) {
                var before = card.previousElementSibling;
                var after = card.nextElementSibling;
                if (before && before.hasAttribute('data-rule-or')) { before.remove(); }
                else if (after && after.hasAttribute('data-rule-or')) { after.remove(); }
                card.remove();
            }
            var next = block && block.querySelector(':scope > .button-row [data-rule-change]');
            if (next) { next.focus(); }
        });
    });

    // Notes on every Case grows to its text, so every line shows without
    // scrolling inside the box; without script it keeps its four rows.
    document.querySelectorAll('[data-notes-on-every-case] textarea').forEach(function (area) {
        area.setAttribute('data-notes-fit', '');
        var fit = function () {
            area.style.height = 'auto';
            area.style.height = (area.scrollHeight + 2) + 'px';
        };
        area.addEventListener('input', fit);
        window.addEventListener('resize', fit);
        fit();
        if (document.fonts) { document.fonts.ready.then(fit); }
    });

    // Inspection mode: the default inspection location belongs to a physical
    // address, so it shows only for that mode. Without script it always shows,
    // and Image Based Assessment clears it when the contact is saved.
    var mode = document.querySelector('[data-inspection-mode]');
    if (!mode) {
        return;
    }

    var physicalFields = document.querySelectorAll('[data-location-physical]');
    var toggleLocation = function () {
        physicalFields.forEach(function (field) {
            field.hidden = mode.value === 'ImageBasedAssessment';
        });
    };
    mode.addEventListener('change', toggleLocation);
    toggleLocation();
})();
