// The Pegasus helper in Glass's editor frame (acofr.php), in the extension's own
// world (R1170, ADR-0091). It asks the service worker to hand this frame's
// address to Pegasus once, then to poll on the cadence each answer names. When
// Pegasus has asked the window to show its selected list again (the Automation
// added or removed a position), it reloads that one frame and says so a second
// later, so Pegasus can tell the Automation the window shows it. It reads
// nothing of Glass's pages but the paths of the editor's frames; the service
// worker reads this frame's address.
'use strict';

(() => {
    // Answers after which there is nothing more to do in this window.
    const STOP = new Set([400, 401, 403, 404, 409, 426]);
    // The refresh count last acted on; null until the first poll answers.
    let acted = null;

    const ask = (message) => new Promise((resolve) => {
        try {
            chrome.runtime.sendMessage(message, (answer) => resolve(chrome.runtime.lastError ? null : answer));
        } catch (_) {
            // The extension was reloaded or removed under this page.
            resolve(null);
        }
    });

    const finished = (answer) => !answer || answer.ignored || STOP.has(answer.status);

    const later = (milliseconds) =>
        setTimeout(poll, Math.min(Math.max(Number(milliseconds) || 10000, 1000), 60000));

    // The editor's selected list (selected.php), in this window's frames.
    function selectedFrame() {
        const walk = (frame, depth) => {
            if (depth > 12) { return null; }
            try {
                if (frame.location.pathname.endsWith('/selected.php')) { return frame; }
            } catch (_) {
                return null;
            }
            for (let index = 0; index < frame.frames.length; index++) {
                const found = walk(frame.frames[index], depth + 1);
                if (found) { return found; }
            }
            return null;
        };
        return walk(window.top, 0);
    }

    function showSelectedAgain() {
        const selected = selectedFrame();
        if (!selected) { return; }
        const address = new URL(selected.location.href);
        address.searchParams.set('dummy', String(Math.floor(Date.now() / 1000)));
        selected.location.replace(address.href);
    }

    async function handOver() {
        const sent = await ask({ kind: 'key' });
        return finished(sent) ? null : sent;
    }

    async function poll() {
        const polled = await ask({ kind: 'poll', seen: acted === null ? 0 : acted });
        if (finished(polled)) { return; }
        const answer = polled.status === 200 ? polled.answer : null;
        if (answer) {
            if (answer.key === 'needed' && !(await handOver())) { return; }
            const refresh = Number.isSafeInteger(answer.refresh) ? answer.refresh : 0;
            const shown = acted !== null && refresh > acted;
            if (shown) { showSelectedAgain(); }
            acted = refresh;
            // A reload is reported on the next poll, a second from now.
            if (shown) { later(1000); return; }
        }
        later(answer && answer.next);
    }

    handOver().then((sent) => {
        if (sent) { later(sent.answer && sent.answer.next); }
    });
})();
