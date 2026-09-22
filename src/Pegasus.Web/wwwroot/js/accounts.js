// Keep account-signoff controls eligible and protect unsaved settings when
// navigating to the account's Glass's login.
(function () {
    'use strict';

    function bindAccountSettings(form) {
        if (form.dataset.accountSettingsBound === 'true') { return; }
        form.dataset.accountSettingsBound = 'true';

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

        function resetSettings() {
            form.reset();
        }

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
    }

    document.querySelectorAll('[data-account-settings]').forEach(bindAccountSettings);
    (window.pegasusMountBinders = window.pegasusMountBinders || []).push(function (root) {
        root.querySelectorAll('[data-account-settings]').forEach(bindAccountSettings);
    });
})();
