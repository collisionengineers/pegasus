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
