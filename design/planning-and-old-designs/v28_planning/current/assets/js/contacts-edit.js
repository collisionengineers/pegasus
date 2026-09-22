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
