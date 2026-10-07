// Contacts edit enhancements. The form and native dialog remain fully usable
// without this asset; site.js owns the shared data-dialog-open/close contract.
(function () {
    'use strict';

    var addRecipient = document.querySelector('[data-add-report-recipient]');
    var recipients = document.getElementById('additional-report-recipients');
    if (addRecipient && recipients) {
        addRecipient.addEventListener('click', function () {
            var input = document.createElement('input');
            input.name = 'AdditionalReportRecipients';
            input.type = 'email';
            input.autocomplete = 'email';
            input.setAttribute('aria-label', 'Additional report recipient');
            recipients.appendChild(input);
            input.focus();
        });
    }

    // Salvage matrix: Add band appends a blank row to its category's table and
    // Remove takes its row away. Each table's spare row is the pattern.
    var salvageEditor = document.querySelector('[data-salvage-matrix-editor]');
    if (salvageEditor) {
        var blankBands = {};
        salvageEditor.querySelectorAll('[data-salvage-bands]').forEach(function (body) {
            var spare = body.lastElementChild;
            if (spare) {
                var blank = spare.cloneNode(true);
                blank.querySelectorAll('input:not([type="hidden"])').forEach(function (input) {
                    input.value = '';
                });
                blankBands[body.getAttribute('data-salvage-bands')] = blank;
            }
        });
        salvageEditor.addEventListener('click', function (event) {
            var add = event.target.closest('[data-add-salvage-band]');
            if (add) {
                var category = add.getAttribute('data-add-salvage-band');
                var body = salvageEditor.querySelector('[data-salvage-bands="' + category + '"]');
                if (body && blankBands[category]) {
                    var row = blankBands[category].cloneNode(true);
                    body.appendChild(row);
                    row.querySelector('input:not([type="hidden"])').focus();
                }
                return;
            }
            var remove = event.target.closest('[data-remove-salvage-band]');
            if (remove) {
                var removed = remove.closest('tr');
                var bands = removed.closest('[data-salvage-bands]');
                removed.remove();
                var addFor = salvageEditor.querySelector('[data-add-salvage-band="' + bands.getAttribute('data-salvage-bands') + '"]');
                if (addFor) { addFor.focus(); }
            }
        });
    }

    var contactEditor = document.querySelector('[data-contact-editor]');
    if (contactEditor) {
        var roleInputs = contactEditor.querySelectorAll('[data-contact-role]');
        var principalFields = contactEditor.querySelector('[data-principal-fields]');
        var caseGuidance = contactEditor.querySelector('[data-case-guidance]');

        var hasRole = function (role) {
            var input = contactEditor.querySelector('[data-contact-role="' + role + '"]');
            return input && input.checked;
        };

        var toggleContactFields = function () {
            var isPrincipal = hasRole('Principal');
            var showsGuidance = isPrincipal || hasRole('ClaimSource');

            if (principalFields) {
                principalFields.hidden = !isPrincipal;
                principalFields.querySelectorAll('[data-principal-field-input]').forEach(function (input) {
                    input.disabled = !isPrincipal;
                });
            }

            if (caseGuidance) {
                caseGuidance.hidden = !showsGuidance;
            }

            contactEditor.querySelectorAll('[data-principal-associations]').forEach(function (section) {
                var isSelected = hasRole(section.getAttribute('data-principal-associations'));
                section.hidden = !isSelected;
                section.querySelectorAll('[data-principal-association-input]').forEach(function (input) {
                    input.disabled = !isSelected;
                });
            });
        };

        roleInputs.forEach(function (input) {
            input.addEventListener('change', toggleContactFields);
        });
        toggleContactFields();
    }

    var imageBased = document.getElementById('LocationIsImageBasedAssessment');
    if (!imageBased) {
        return;
    }

    var physicalFields = document.querySelectorAll('[data-location-physical]');
    var toggleLocation = function () {
        physicalFields.forEach(function (field) {
            field.hidden = imageBased.checked;
        });
    };
    imageBased.addEventListener('change', toggleLocation);
    toggleLocation();
})();
