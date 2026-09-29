// Focused browser evidence for issue 861. Runs the real workspace, opening and return scripts
// against deterministic HTTP/form responses in an isolated Chrome or Edge profile.
// Usage: node scripts/Test-GlassBrowser.mjs [absolute-path-to-browser.exe]
import { spawn } from 'node:child_process';
import { createServer } from 'node:http';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { resolve, join } from 'node:path';
import assert from 'node:assert/strict';

const root = resolve(import.meta.dirname, '..');
const browser = process.argv[2] || process.env.CHROME || [
    join(process.env.ProgramFiles || '', 'Google/Chrome/Application/chrome.exe'),
    join(process.env['ProgramFiles(x86)'] || '', 'Microsoft/Edge/Application/msedge.exe')
].find(existsSync);
if (!browser) { throw new Error('Supply the Chrome or Edge executable path.'); }
const output = join(root, 'artifacts/issue-861/browser', new Date().toISOString().replaceAll(/[:.]/g, '-'));
await mkdir(output, { recursive: true });
const workspaceScript = await readFile(join(root, 'src/Pegasus.Web/wwwroot/js/case-workspace.js'));
const returnScript = await readFile(join(root, 'src/Pegasus.Web/wwwroot/js/glass-return.js'));
const openingScript = await readFile(join(root, 'src/Pegasus.Web/wwwroot/js/glass-opening.js'));
// The Glass's window's state answer: still running on the first ask after each load, then done and open.
let statePolls = 0;

function fixture() {
    return `<!doctype html><html><head><meta charset="utf-8"><title>Glass browser fixture</title>
    <style>body{margin:0}#case-main{padding-top:500px}.record-section{min-height:600px}[hidden]{display:none}</style></head><body>
    <script>
    window.trace = []; window.state = { caseVersion: 1, sessionVersion: 1, slot: 'launch', status: 'Active', registration: 'AB12CDE' };
    window.mode = ''; window.deferred = []; window.nativeOpen = window.open;
    function controls() {
        return '<div data-glass-controls="launch" data-glass-controls-url="/case?handler=GlassSession" data-glass-id="session" data-glass-version="' + state.sessionVersion + '">'
            + '<form data-glass-window target="_blank" method="post" action="/case?handler=' + (state.slot === 'launch' ? 'LaunchGlass' : 'ResumeGlass') + '">'
            + '<input name="expectedCaseVersion" type="hidden" value="' + state.caseVersion + '"><input name="editLeaseToken" type="hidden" value="lease">'
            + '<input name="operationKey" type="hidden" value="launch-key"><button>Glass</button></form></div>'
            + '<div data-glass-controls="outcome" hidden></div>'
            + '<div data-glass-controls="session" data-glass-id="session" data-glass-version="' + state.sessionVersion + '" data-glass-state="' + state.status + '">'
            + '<form data-glass-close-form method="post" action="/case?handler=CloseGlass"><input type="hidden" name="expectedSessionVersion" value="' + state.sessionVersion + '">'
            + '<input name="reason" required><input name="externalSessionClosed" type="checkbox" required><button>Close</button></form></div>';
    }
    function page(commit, notice) {
        return '<div data-case-record data-case-version="' + state.caseVersion + '" data-case-editing="true" data-section-current="estimate"'
            + (commit ? " data-editor-commit='" + JSON.stringify(commit) + "'" : '') + '>'
            + '<div data-sticky-block><nav data-section-nav><a href="#section-estimate" data-section-link="estimate">Estimate</a></nav></div>'
            + '<div data-case-notices>' + (notice || '') + '</div><div data-case-ribbon-facts></div>'
            + '<div data-case-ribbon-actions><span data-lease-line hidden data-lease-saving-text="Saving" data-lease-saved-text="Saved"></span></div>'
            + '<main id="case-main"><section class="record-section" id="section-estimate" data-section="estimate">'
            + '<form id="case-edit-form" method="post" action="/case?handler=Save"><input name="expectedVersion" type="hidden" value="' + state.caseVersion + '" data-carry-forward>'
            + '<input name="editLeaseToken" type="hidden" value="lease" data-carry-forward><input name="operationKey" type="hidden" value="save-' + state.caseVersion + '" data-carry-forward>'
            + '<input id="registration" name="registration" value="' + state.registration + '" required><button>Save</button></form>' + controls()
            + '<input type="hidden" name="selection.Opening" value="opening-' + state.caseVersion + '" form="case-edit-form" data-carry-forward>'
            + '</section></main><div data-case-aside></div><div data-case-dialogs></div><div data-case-viewer-host></div></div>';
    }
    document.write(page());
    window.fetch = function(url, options) {
        options = options || {}; trace.push({ type: 'fetch', url: String(url) });
        var text;
        if (String(url).includes('GlassSession')) { text = mode === 'expired-login' ? '<h1>Sign in</h1>' : controls(); }
        else if (String(url).includes('handler=Save')) {
            if (mode === 'network-failure') { return Promise.reject(new Error('Save disconnected')); }
            var body = options.body;
            if (mode === 'refused-save') { text = page(null, '<p role="alert">Save refused</p>'); }
            else {
                var commit = { editor: 'case-edit-form', operationKey: body.get('operationKey'), expectedVersion: Number(body.get('expectedVersion')), version: ++state.caseVersion };
                state.registration = body.get('registration'); text = page(commit);
            }
        } else if (String(url).includes('CloseGlass')) {
            state.sessionVersion++; text = page(null, '<p role="alert">Confirm external closure again</p>');
        } else { text = page(); }
        var response = { ok: true, status: 200, url: location.origin + '/case', text: function() { return Promise.resolve(text); } };
        if (mode === 'defer') { return new Promise(function(resolve) { deferred.push(function() { resolve(response); }); }); }
        return Promise.resolve(response);
    };
    window.open = function() {
        trace.push({ type: 'open' });
        if (mode === 'blocked-popup') { return null; }
        window.fakePopup = { closed: false, close: function() { this.closed = true; } };
        return fakePopup;
    };
    HTMLFormElement.prototype.submit = function() {
        trace.push({ type: 'provider', action: this.action, caseVersion: this.elements.expectedCaseVersion.value, lease: this.elements.editLeaseToken.value });
    };
    function edit(value) { var input = document.getElementById('registration'); input.value = value; input.dispatchEvent(new Event('input', { bubbles: true })); }
    function leave() { document.getElementById('registration').dispatchEvent(new Event('change', { bubbles: true })); }
    function launch() { document.querySelector('[data-glass-window]').requestSubmit(); }
    function result() { return { trace: trace, value: document.getElementById('registration').value, version: document.querySelector('#case-edit-form [name="expectedVersion"]').value,
        sessionVersion: document.querySelector('[data-glass-controls="session"]').dataset.glassVersion, notice: document.querySelector('[data-case-notices]').textContent + document.querySelector('[data-glass-controls="outcome"]').textContent,
        closed: window.fakePopup && fakePopup.closed, status: (function (line) { return line && !line.hidden ? line.textContent.trim() : null; })(document.querySelector('[data-lease-line]')),
        opening: document.querySelector('[name="selection.Opening"]').value, scroll: window.scrollY }; }
    </script><script src="/workspace.js"></script><script>window.fixtureReady=true;</script></body></html>`;
}

const server = createServer((req, res) => {
    res.setHeader('Content-Type', req.url.endsWith('.js') ? 'text/javascript' : req.url === '/opening/state' ? 'application/json' : 'text/html');
    if (req.url === '/workspace.js') { res.end(workspaceScript); }
    else if (req.url === '/return.js') { res.end(returnScript); }
    else if (req.url === '/opening.js') { res.end(openingScript); }
    else if (req.url === '/opening') { statePolls = 0; res.end('<body data-glass-opening-state="/opening/state" data-glass-opening-go="/provider"><p>Preparing <a href="/provider">Continue</a></p><script src="/opening.js"></script>'); }
    else if (req.url === '/opening/state') { res.end(JSON.stringify({ pending: statePolls++ === 0, open: true })); }
    else if (req.url === '/return') { res.end('<body data-glass-return="/case"><script src="/return.js"></script>'); }
    else if (req.url === '/provider') { res.end('<title>Provider fixture</title>Editor URL issued'); }
    else { res.end(fixture()); }
});
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
const origin = `http://127.0.0.1:${server.address().port}`;
const proc = spawn(browser, ['--headless=new', '--disable-gpu', '--no-first-run', '--no-default-browser-check',
    `--user-data-dir=${join(output, 'profile')}`, '--remote-debugging-port=0', 'about:blank'], { windowsHide: true, stdio: 'ignore' });
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
let ws, send;
const evidence = [], errors = [];
try {
    let port;
    for (let i = 0; i < 100 && !port; i++) {
        await delay(100);
        try { port = Number((await readFile(join(output, 'profile/DevToolsActivePort'), 'utf8')).split('\n')[0]); } catch { /* Starting. */ }
    }
    assert.ok(port, 'Browser starts');
    const targets = async () => (await fetch(`http://127.0.0.1:${port}/json`)).json();
    const target = (await targets()).find(t => t.type === 'page');
    ws = new WebSocket(target.webSocketDebuggerUrl);
    await new Promise(resolve => ws.onopen = resolve);
    let sequence = 0; const pending = new Map();
    ws.onmessage = event => {
        const message = JSON.parse(event.data);
        if (message.id) { pending.get(message.id)?.(message); pending.delete(message.id); }
        if (message.method === 'Runtime.exceptionThrown') { errors.push(message.params.exceptionDetails); }
        if (message.method === 'Page.javascriptDialogOpening') { send('Page.handleJavaScriptDialog', { accept: true }); }
    };
    send = (method, params = {}) => new Promise(resolve => {
        const id = ++sequence; pending.set(id, resolve); ws.send(JSON.stringify({ id, method, params }));
    });
    await send('Page.enable'); await send('Runtime.enable');
    const evaluate = async expression => {
        const response = await send('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true, userGesture: true });
        assert.ok(!response.result?.exceptionDetails, JSON.stringify(response.result?.exceptionDetails));
        return response.result?.result?.value;
    };
    const waitFor = async expression => {
        for (let i = 0; i < 200; i++) {
            if (await evaluate(expression)) { return; }
            await delay(25);
        }
        assert.fail('Browser condition did not become true: ' + expression);
    };
    let navigation = 0;
    const reset = async () => {
        const query = '?fixture=' + ++navigation;
        await send('Page.navigate', { url: origin + '/case' + query });
        await waitFor(`location.search === '${query}' && window.fixtureReady === true && typeof window.pegasusGlassHandoff === 'function'`);
    };
    const record = (name, value) => { evidence.push({ name, value }); console.log('PASS', name); };
    const posts = result => result.trace.filter(x => x.type === 'provider');

    // Save as you go: a script's fill (edit) commits after the idle, leaving a cell (leave) at once;
    // a launch, a return or an action waits for a change not yet sent and runs only once it has landed.
    await reset();
    await evaluate("edit('AB12 CDE'); launch(); launch();"); await delay(100);
    let result = await evaluate('result()');
    assert.equal(posts(result).length, 1); assert.equal(posts(result)[0].caseVersion, '2'); assert.equal(result.version, '2'); assert.match(result.status, /^Saved/);
    assert.deepEqual(result.trace.slice(0, 3).map(x => x.type), ['open', 'fetch', 'provider']);
    assert.equal(result.opening, 'opening-2', 'A carried input joined to the form from its section moves on with the commit');
    record('A change not yet sent lands once before the provider post, which carries the new authority', result);

    for (const [mode, status] of [['refused-save', 'Save refused'], ['network-failure', 'Save disconnected']]) {
        await reset(); await evaluate(`mode = '${mode}'; edit('AB12 CDE'); launch();`); await delay(100);
        result = await evaluate('result()');
        assert.equal(posts(result).length, 0); assert.equal(result.value, 'AB12 CDE'); assert.equal(result.version, '1');
        assert.equal(result.closed, true); assert.equal(result.status, status);
        record(mode + ' keeps the change, closes the reserved window and makes no provider post', result);
    }
    await reset(); await evaluate("mode = 'blocked-popup'; edit('AB12 CDE'); launch();"); await delay(100);
    result = await evaluate('result()');
    assert.equal(posts(result).length, 0); assert.equal(result.value, 'AB12 CDE'); assert.equal(result.version, '1');
    await delay(1200); result = await evaluate('result()');
    assert.equal(posts(result).length, 0); assert.equal(result.version, '2'); assert.match(result.status, /^Saved/);
    record('blocked-popup makes no provider post and the change still saves after the idle', result);

    await reset(); await evaluate("edit(''); launch();"); await delay(50);
    result = await evaluate('result()'); assert.equal(posts(result).length, 0); assert.equal(result.closed, true); assert.equal(result.version, '1');
    assert.match(result.status, /not saved/);
    assert.equal(result.trace.filter(x => x.type === 'fetch').length, 0);
    record('A value the browser refuses is not sent and closes the reserved popup without posting', result);

    await reset(); await evaluate("mode='defer'; edit('AB12 CDE'); launch(); edit('XY99ZZZ'); deferred.shift()();"); await delay(100);
    result = await evaluate('result()'); assert.equal(posts(result).length, 0); assert.equal(result.value, 'XY99ZZZ'); assert.equal(result.version, '2');
    await delay(1200); await evaluate('deferred.shift()();'); await delay(100);
    result = await evaluate('result()'); assert.equal(posts(result).length, 1); assert.equal(posts(result)[0].caseVersion, '3'); assert.equal(result.value, 'XY99ZZZ');
    record('Typing during a save holds the launch until the newer change has landed too', result);

    await reset(); await evaluate("document.querySelector('[data-glass-controls=outcome]').innerHTML='<p>Old recorded outcome</p>'; edit('XY99ZZZ'); window.scrollTo(0, 450); state.slot='resume'; state.sessionVersion=7; window.pegasusGlassHandoff();"); await delay(100);
    result = await evaluate('result()'); assert.equal(result.sessionVersion, '7'); assert.equal(result.value, 'XY99ZZZ'); assert.equal(result.version, '1'); assert.equal(result.scroll, 450); assert.equal(result.notice, '');
    record('Handoff refreshes controls while a change not yet sent and the scroll stay', result);
    await evaluate("state.status='Completed'; window.pegasusGlassReturn('/case');"); await delay(100);
    result = await evaluate('result()'); assert.equal(result.version, '2'); assert.equal(result.value, 'XY99ZZZ');
    assert.deepEqual(result.trace.filter(x => x.type === 'fetch').slice(-2).map(x => x.url.includes('handler=Save') ? 'save' : x.url), ['save', '/case']);
    record('Callback lands the change not yet sent, then reads the Case as it now stands', result);

    await reset(); await evaluate("state.status='Completed'; state.caseVersion=2; window.pegasusGlassReturn('/case');"); await delay(100);
    result = await evaluate('result()'); assert.equal(result.version, '2'); assert.equal(result.status, null);
    record('Callback with nothing to save refreshes workspace in place', result);

    await reset(); await evaluate("mode='defer'; state.status='Unknown'; window.returnInFlight=window.pegasusGlassReturn('/case'); deferred[0](); true;"); await delay(50);
    await evaluate("mode=''; edit('AB12 CDE'); leave();"); await delay(100);
    assert.equal((await evaluate('result()')).version, '2');
    await evaluate('deferred[1]();'); await delay(100);
    result = await evaluate('result()'); assert.equal(result.version, '2'); assert.equal(result.value, 'AB12 CDE'); assert.match(result.status, /^Saved/);
    record('Delayed callback read cannot undo a newer successful Case save', result);

    await reset(); await evaluate("mode='expired-login'; edit('XY99ZZZ'); window.pegasusGlassReturn('/case').catch(function() {});"); await delay(50);
    result = await evaluate('result()'); assert.equal(result.value, 'XY99ZZZ'); assert.equal(result.version, '1');
    assert.match(result.notice, /Sign in again/);
    record('Expired login cannot replace Case controls holding a change not yet sent', result);

    await reset(); await evaluate("mode='defer'; state.sessionVersion=5; window.pegasusGlassHandoff(); state.sessionVersion=6; window.pegasusGlassHandoff(); deferred[1]();"); await delay(50);
    await evaluate('deferred[0]();'); await delay(50);
    result = await evaluate('result()'); assert.equal(result.sessionVersion, '6');
    record('Out-of-order controls cannot replace newer session', result);

    await reset(); await evaluate("edit('XY99ZZZ'); var close=document.querySelector('[data-glass-close-form]'); close.elements.reason.value='Closed externally'; close.elements.externalSessionClosed.checked=true; close.requestSubmit();"); await delay(100);
    result = await evaluate('result()'); assert.equal(result.version, '2'); assert.equal(result.value, 'XY99ZZZ'); assert.equal(result.sessionVersion, '2');
    assert.equal(await evaluate("document.querySelector('[name=externalSessionClosed]').checked"), false);
    record('Stale Close lands the change first, then refreshes the confirmation', result);

    // Real popup and opener, with the production opening and return scripts on both legs.
    await reset(); await evaluate("window.open=window.nativeOpen; edit('XY99ZZZ'); state.slot='resume'; state.sessionVersion=8; window.realPopup=window.open('/opening','glass-real');"); await delay(2500);
    assert.ok((await targets()).some(t => t.url === origin + '/provider'));
    assert.equal((await evaluate('result()')).sessionVersion, '8');
    await evaluate("state.status='Completed'; realPopup.location='/return';"); await waitFor('realPopup.closed');
    assert.equal(await evaluate('realPopup.closed'), true); assert.equal((await evaluate('result()')).value, 'XY99ZZZ');
    record('Real popup waits, refreshes the Case, opens provider then returns without losing edits', await evaluate('result()'));
    await send('Page.navigate', { url: origin + '/opening' }); await waitFor("location.pathname === '/provider'");
    assert.equal(await evaluate('location.pathname'), '/provider');
    record('No-opener window continues in its own window', true);
    await send('Emulation.setScriptExecutionDisabled', { value: true });
    await send('Page.navigate', { url: origin + '/opening' }); await delay(200);
    assert.equal(await evaluate('location.pathname'), '/opening');
    assert.equal(await evaluate("document.querySelector('a').getAttribute('href')"), '/provider');
    await send('Emulation.setScriptExecutionDisabled', { value: false });
    record('No-script window retains the server-rendered Continue link', true);
    assert.deepEqual(errors, [], 'No browser runtime exceptions');
    await writeFile(join(output, 'result.json'), JSON.stringify({ browser, evidence, errors }, null, 2));
    console.log('Evidence:', join(output, 'result.json'));
} finally {
    if (send) { await Promise.race([send('Browser.close'), delay(2000)]); }
    ws?.close(); proc.kill(); server.close();
}
