// The Pegasus helper's service worker (R1170, ADR-0091). It answers only this
// extension's own content script in Glass's editor frame, reads that frame's
// address from the sender (never from the message), and posts only to a Pegasus
// origin this extension's manifest lists in host_permissions: the key hand-off
// with the staff cookie, the poll without it. It keeps nothing between messages.
'use strict';

// The protocol of each body; Pegasus's HelpModel.Protocol must equal it.
const PROTOCOL = 1;
const HELP_PATH = 'Integrations/Glass/Help/';
const CALLBACK_PATH = /^(\/(?:[^?#]*\/)?)Integrations\/Glass\/Callback\/([0-9a-f]{64})$/;

const manifest = chrome.runtime.getManifest();
const withoutWildcard = (pattern) => new URL(pattern.replace(/\*$/, ''));
const pegasusOrigins = (manifest.host_permissions || []).map((pattern) => withoutWildcard(pattern).origin);
const editorFrames = manifest.content_scripts.flatMap((script) => script.matches).map(withoutWildcard);

// The query parameter as the address writes it, not decoded.
function rawParameter(search, name) {
    for (const part of search.replace(/^\?/, '').split('&')) {
        if (part.startsWith(name + '=')) { return part.slice(name.length + 1); }
    }
    return null;
}

// The sending frame, when it is Glass's editor frame whose way home is a listed
// Pegasus origin's callback: the frame's own address, where to post, and the
// session's return token. Anything else is null and nothing is sent.
function editorFrame(sender) {
    if (sender.id !== chrome.runtime.id || typeof sender.url !== 'string') { return null; }
    let frame;
    try { frame = new URL(sender.url); } catch (_) { return null; }
    if (!editorFrames.some((known) => known.protocol === frame.protocol && known.host === frame.host && known.pathname === frame.pathname)) { return null; }
    const url0 = rawParameter(frame.search, 'url0');
    const caller = url0 === null ? null : /[?&]caller=(.*)$/.exec(unescape(url0));
    if (!caller) { return null; }
    let callback;
    try { callback = new URL(caller[1]); } catch (_) { return null; }
    const path = CALLBACK_PATH.exec(callback.pathname);
    if (callback.protocol !== 'https:' || !path || callback.search || callback.hash || !pegasusOrigins.includes(callback.origin)) { return null; }
    return { frame: sender.url, help: callback.origin + path[1] + HELP_PATH, token: path[2] };
}

async function post(address, body, credentials) {
    const response = await fetch(address, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
        credentials,
        cache: 'no-store',
        redirect: 'error'
    });
    let answer = null;
    if ((response.headers.get('Content-Type') || '').startsWith('application/json')) {
        try { answer = await response.json(); } catch (_) { answer = null; }
    }
    return { status: response.status, answer };
}

chrome.runtime.onMessage.addListener((message, sender, reply) => {
    const editor = editorFrame(sender);
    const kind = message && message.kind;
    if (!editor || (kind !== 'key' && kind !== 'poll')) {
        reply({ ignored: true });
        return false;
    }
    const sent = kind === 'key'
        ? post(editor.help + 'Key', { v: PROTOCOL, frame: editor.frame, version: manifest.version }, 'include')
        : post(editor.help + 'Poll', { v: PROTOCOL, token: editor.token, seen: Number.isSafeInteger(message.seen) ? message.seen : 0, version: manifest.version }, 'omit');
    sent.then(reply, () => reply({ status: 0 }));
    return true;
});
