// Account-settings interaction remains local to the page: the server owns
// validation and every mutation, while this script maintains the edit scope.
(function () {
    'use strict';

    function bindAccountSettings(form) {
        if (form.dataset.accountSettingsBound === 'true') { return; }
        form.dataset.accountSettingsBound = 'true';

        var role = form.querySelector('[data-account-role]');
        var signOff = form.querySelector('[data-account-signoff]');
        var defaultSignOff = form.querySelector('[data-account-default]');
        var dialog = form.closest('[data-dialog]');
        var antiForgery = form.querySelector('input[name="__RequestVerificationToken"]');
        var leaseToken = form.querySelector('input[name="editLeaseToken"]');
        var operationKey = form.querySelector('input[name="operationKey"]');
        var glassLogin = form.querySelector('[data-account-glass-login]');
        var glassLoginError = form.querySelector('[data-account-glass-login-error]');
        var active = dialog && !dialog.hidden;
        var released = false;
        var heartbeat = null;
        var initialFormState;

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

        function showGlassLoginError(message) {
            if (!glassLoginError) { return; }
            glassLoginError.textContent = message;
            glassLoginError.hidden = false;
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

        function post(handler) {
            if (!antiForgery || !leaseToken || !operationKey) { return Promise.resolve(); }
            var body = new URLSearchParams();
            body.set('__RequestVerificationToken', antiForgery.value);
            body.set('staffId', form.dataset.accountStaffId || '');
            body.set('editLeaseToken', leaseToken.value);
            body.set('operationKey', operationKey.value);
            return fetch('?handler=' + handler, {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                body: body.toString(),
                credentials: 'same-origin'
            });
        }

        function stopHeartbeat() {
            if (heartbeat !== null) { window.clearInterval(heartbeat); heartbeat = null; }
        }

        function release() {
            if (released || !active) { return; }
            released = true;
            active = false;
            stopHeartbeat();
            post('CancelSettings');
        }

        function startHeartbeat() {
            stopHeartbeat();
            if (!active || released || !leaseToken) { return; }
            heartbeat = window.setInterval(function () { post('HeartbeatSettings'); }, 120000);
        }

        if (role) { role.addEventListener('change', syncSignOffEligibility); }
        if (glassLogin) {
            glassLogin.addEventListener('click', function (event) {
                if (!active || released) { return; }
                event.preventDefault();
                if (formState() !== initialFormState
                    && !window.confirm('Discard unsaved account settings and manage the Glass login?')) {
                    return;
                }
                released = true;
                active = false;
                stopHeartbeat();
                if (glassLoginError) { glassLoginError.hidden = true; }
                post('CancelSettings').then(function (response) {
                    if (!response || !response.ok) {
                        throw new Error('The account edit could not be released.');
                    }
                    window.location.assign(glassLogin.href);
                }).catch(function () {
                    released = false;
                    active = true;
                    startHeartbeat();
                    showGlassLoginError('The account edit could not be released. Keep editing or try again.');
                });
            });
        }
        syncSignOffEligibility();
        initialFormState = formState();
        startHeartbeat();

        if (!dialog) { return; }
        dialog.addEventListener('pegasus:dialog-open', function () {
            active = true;
            released = false;
            startHeartbeat();
        });
        new MutationObserver(function () {
            if (dialog.hidden) { release(); }
        }).observe(dialog, { attributes: true, attributeFilter: ['hidden'] });
    }

    document.querySelectorAll('[data-account-settings]').forEach(bindAccountSettings);
    (window.pegasusMountBinders = window.pegasusMountBinders || []).push(function (root) {
        root.querySelectorAll('[data-account-settings]').forEach(bindAccountSettings);
    });
})();
