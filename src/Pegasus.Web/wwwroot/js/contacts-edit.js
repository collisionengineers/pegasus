// Contacts edit enhancements. The form and native dialog remain fully usable
// without this asset; site.js owns the shared data-dialog-open/close contract.
(function () {
    'use strict';

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

    // Report sending: Add address / Add reminder append an input to its list;
    // Add rule clones the spare blank rule under the next number, and each
    // condition shows only the value field its kind reads.
    var sendingEditor = document.querySelector('[data-report-sending-editor]');
    if (sendingEditor) {
        var rulesHost = sendingEditor.querySelector('[data-rs-rules]');
        var textKinds = ['ImagesFrom', 'Mentions', 'BodyshopMentions', 'SenderNot'];
        var syncCondition = function (condition) {
            var kind = condition.querySelector('[data-rs-kind]').value;
            condition.querySelectorAll('[data-rs-value]').forEach(function (field) {
                var reads = field.getAttribute('data-rs-value');
                field.hidden = kind === '' || (reads === 'Text' ? textKinds.indexOf(kind) < 0 : reads !== kind);
            });
        };
        var syncRules = function () {
            sendingEditor.querySelectorAll('[data-rs-condition]').forEach(syncCondition);
        };
        syncRules();

        var blankRule = null;
        var nextRule = 0;
        if (rulesHost) {
            var spareRule = rulesHost.querySelector('[data-rs-rule]:last-child');
            if (spareRule) { blankRule = spareRule.cloneNode(true); }
            rulesHost.querySelectorAll('input[name="RuleIndex"]').forEach(function (input) {
                nextRule = Math.max(nextRule, parseInt(input.value, 10) + 1);
            });
        }

        sendingEditor.addEventListener('change', function (event) {
            var condition = event.target.closest('[data-rs-condition]');
            if (condition && event.target.matches('[data-rs-kind]')) { syncCondition(condition); }
        });
        sendingEditor.addEventListener('click', function (event) {
            var add = event.target.closest('[data-rs-add]');
            if (add) {
                var name = add.getAttribute('data-rs-add');
                var list = sendingEditor.querySelector('[data-rs-items="' + name + '"]');
                var input = document.createElement('input');
                input.name = name;
                input.type = add.getAttribute('data-rs-type') || 'text';
                input.autocomplete = 'off';
                input.setAttribute('aria-label', add.getAttribute('data-rs-label') || name);
                list.appendChild(input);
                input.focus();
                return;
            }
            if (event.target.closest('[data-add-rs-rule]')) {
                if (rulesHost && blankRule) {
                    var number = nextRule++;
                    var row = blankRule.cloneNode(true);
                    row.querySelectorAll('[name],[id],[for]').forEach(function (element) {
                        ['name', 'id', 'for'].forEach(function (attribute) {
                            var value = element.getAttribute(attribute);
                            if (value) { element.setAttribute(attribute, value.replace(/^Rule[0-9]+/, 'Rule' + number)); }
                        });
                    });
                    row.querySelector('input[name="RuleIndex"]').value = String(number);
                    rulesHost.appendChild(row);
                    syncRules();
                    row.querySelector('select, input:not([type="hidden"])').focus();
                }
                return;
            }
            var remove = event.target.closest('[data-remove-rs-rule]');
            if (remove) {
                remove.closest('[data-rs-rule]').remove();
                var addRule = sendingEditor.querySelector('[data-add-rs-rule]');
                if (addRule) { addRule.focus(); }
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

    var reportRoute = document.querySelector('[data-report-generation-route]');
    var reportEvaMode = document.querySelector('[data-report-generation-eva-mode]');
    var reportEvaField = document.querySelector('[data-report-generation-eva-delivery]');
    var reportValue = document.querySelector('[data-report-generation-value]');
    if (reportRoute && reportEvaMode && reportEvaField && reportValue) {
        var syncReportGeneration = function () {
            var isEva = reportRoute.value === 'Eva';
            reportEvaField.hidden = !isEva;
            reportEvaMode.disabled = !isEva;
            reportValue.value = isEva ? reportEvaMode.value : 'Pegasus';
        };
        reportRoute.addEventListener('change', syncReportGeneration);
        reportEvaMode.addEventListener('change', syncReportGeneration);
        syncReportGeneration();
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
