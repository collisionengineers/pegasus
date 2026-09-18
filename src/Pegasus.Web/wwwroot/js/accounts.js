// Keep account-signoff controls eligible and protect unsaved settings when
// navigating to the account's Glass's login.
(function () {
    'use strict';

    function bindAccountSettings(form) {
        if (form.dataset.accountSettingsBound === 'true') { return; }
        form.dataset.accountSettingsBound = 'true';

        var role = form.querySelector('[data-account-role]');
        var signOff = form.querySelector('[data-account-signoff]');
        var defaultSignOff = form.querySelector('[data-account-default]');
        var dialog = form.closest('[data-dialog]');
        var glassLogin = form.querySelector('[data-account-glass-login]');

        function formState() {
            return Array.prototype.map.call(
                form.querySelectorAll('input:not([type="hidden"]), select, textarea'),
                function (input) {
                    if (input.type === 'checkbox' || input.type === 'radio') {
                        return input.name + ':' + input.value + ':' + input.checked;
                    }
                    if (input.type === 'file') {
                        return input.name + ':' + Array.prototype.map.call(input.files, function (file) {
                            return file.name + ':' + file.size;
                        }).join(',');
                    }
                    return input.name + ':' + input.value;
                }).join('|');
        }

        function syncSignOffEligibility() {
            if (!role || !signOff || !defaultSignOff) { return; }
            var allowed = role.value === 'Administrator' || role.value === 'Engineer';
            signOff.disabled = !allowed;
            defaultSignOff.disabled = !allowed;
            form.querySelectorAll('[data-account-signoff-field]').forEach(function (field) {
                field.classList.toggle('is-disabled', !allowed);
                field.querySelectorAll('input').forEach(function (input) { input.disabled = !allowed; });
            });
            if (!allowed) { signOff.value = 'false'; defaultSignOff.checked = false; }
        }

        function resetSettings() {
            form.reset();
            syncSignOffEligibility();
        }

        if (role) { role.addEventListener('change', syncSignOffEligibility); }
        if (dialog) {
            dialog.querySelectorAll('[data-account-settings-cancel]').forEach(function (button) {
                button.addEventListener('click', resetSettings);
            });
        }
        if (glassLogin) {
            var initialFormState = formState();
            glassLogin.addEventListener('click', function (event) {
                if (formState() !== initialFormState
                    && !window.confirm('Discard unsaved account settings and manage the Glass login?')) {
                    event.preventDefault();
                }
            });
        }

        syncSignOffEligibility();
    }

    document.querySelectorAll('[data-account-settings]').forEach(bindAccountSettings);
    (window.pegasusMountBinders = window.pegasusMountBinders || []).push(function (root) {
        root.querySelectorAll('[data-account-settings]').forEach(bindAccountSettings);
    });
})();
