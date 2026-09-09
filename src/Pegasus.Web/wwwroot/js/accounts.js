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
        var active = dialog && !dialog.hidden;
        var released = false;
        var heartbeat = null;

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
            if (!antiForgery || !leaseToken || !operationKey) { return; }
            var body = new URLSearchParams();
            body.set('__RequestVerificationToken', antiForgery.value);
            body.set('staffId', form.dataset.accountStaffId || '');
            body.set('editLeaseToken', leaseToken.value);
            body.set('operationKey', operationKey.value);
            fetch('?handler=' + handler, {
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
        syncSignOffEligibility();
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
