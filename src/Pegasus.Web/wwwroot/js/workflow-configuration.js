(() => {
    const form = document.querySelector('[data-config-editor][data-existing="true"]');
    if (!form) return;
    const timer = window.setInterval(async () => {
        try {
            const data = new FormData();
            for (const name of ['EditingId', 'ExpectedVersion', 'LeaseToken', '__RequestVerificationToken']) {
                data.set(name, form.elements.namedItem(name)?.value || '');
            }
            const result = await fetch('?handler=Heartbeat', { method: 'POST', body: data });
            if (!result.ok) throw new Error();
        } catch {
            window.clearInterval(timer);
            form.querySelector('[data-config-lease-message]').textContent = 'Editing could not be renewed. Cancel and reopen before saving.';
        }
    }, Number(form.dataset.heartbeatMs));
    window.addEventListener('pagehide', () => window.clearInterval(timer), { once: true });
})();
