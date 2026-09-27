// Browser evidence for the Case page Refresh (plan 06, 26 September 2026): a
// press that the workspace intercepts ends with the control idle again, so
// the second, third and tenth press work. Runs the real pages of a live
// Pegasus (a local run or a deployed instance) in an isolated headless Chrome
// or Edge profile. On demand, not in CI.
// Usage: node scripts/Test-CaseRefreshBrowser.mjs <base-url> <case-id> <triage-case-id> [absolute-path-to-browser.exe]
//        or PEGASUS_URL, PEGASUS_CASE_ID, PEGASUS_TRIAGE_CASE_ID and CHROME in the environment.
//        PEGASUS_USER and PEGASUS_PASSWORD sign in first; a DevelopmentOffline run needs neither.
// The Case is put into edit and saved once with its own values, so the run
// leaves one save on it. Evidence is written under artifacts/case-refresh/browser/.
import { spawn } from 'node:child_process';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { resolve, join } from 'node:path';
import assert from 'node:assert/strict';

const root = resolve(import.meta.dirname, '..');
const base = process.argv[2] || process.env.PEGASUS_URL;
const caseId = process.argv[3] || process.env.PEGASUS_CASE_ID;
const triageCaseId = process.argv[4] || process.env.PEGASUS_TRIAGE_CASE_ID;
if (!base || !caseId || !triageCaseId) {
    console.error('Usage: node scripts/Test-CaseRefreshBrowser.mjs <base-url> <case-id> <triage-case-id> [browser.exe]');
    process.exit(2);
}
const origin = base.replace(/\/+$/, '');
const browser = process.argv[5] || process.env.CHROME || [
    join(process.env.ProgramFiles || '', 'Google/Chrome/Application/chrome.exe'),
    join(process.env['ProgramFiles(x86)'] || '', 'Microsoft/Edge/Application/msedge.exe')
].find(existsSync);
if (!browser) { throw new Error('Supply the Chrome or Edge executable path.'); }
const output = join(root, 'artifacts/case-refresh/browser', new Date().toISOString().replaceAll(/[:.]/g, '-'));
await mkdir(output, { recursive: true });

// The control as the operator sees it, read fresh from whichever document is
// current, plus what the Case record says about the edit session.
const STATE = `(function () {
    var form = document.querySelector('[data-refresh-form]');
    if (!form) { return null; }
    var region = form.closest('[data-refresh-region]');
    var button = form.querySelector('button');
    var label = form.querySelector('[data-refresh-label]');
    var record = document.querySelector('[data-case-record]');
    var error = document.querySelector('[data-inplace-error]');
    var dialog = document.getElementById('edit-finish-confirm');
    return {
        label: label ? label.textContent.trim() : null,
        disabled: !!(button && button.disabled),
        busy: region ? region.getAttribute('aria-busy') : null,
        refreshing: !!(region && region.classList.contains('is-refreshing')),
        editing: record ? record.getAttribute('data-case-editing') : null,
        error: error ? error.textContent : null,
        dialog: !!(dialog && !dialog.hidden)
    };
})()`;
const PRESS = "document.querySelector('[data-refresh-form] button').click()";
// A document about to navigate carries this flag; the next document does not.
const LEAVING = 'window.pegasusRefreshCheckLeaving = true';
const ARRIVED = "window.pegasusRefreshCheckLeaving === undefined && document.readyState === 'complete'";

const proc = spawn(browser, ['--headless=new', '--disable-gpu', '--no-first-run', '--no-default-browser-check', '--ignore-certificate-errors',
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
    const target = (await (await fetch(`http://127.0.0.1:${port}/json`)).json()).find(t => t.type === 'page');
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
    // A document that is navigating away answers with a protocol error; only
    // a wait tolerates that, everything else reports it.
    const evaluate = async (expression, tolerant = false) => {
        const response = await send('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true, userGesture: true });
        if (response.error) {
            if (tolerant) { return undefined; }
            throw new Error(response.error.message);
        }
        assert.ok(!response.result?.exceptionDetails, JSON.stringify(response.result?.exceptionDetails));
        return response.result?.result?.value;
    };
    const waitFor = async (expression, what) => {
        for (let i = 0; i < 400; i++) {
            if (await evaluate(expression, true)) { return; }
            await delay(25);
        }
        assert.fail(`${what} did not happen within 10 s at ${await evaluate('location.href', true)}: ${expression}`);
    };
    const navigate = async path => {
        await evaluate(LEAVING, true);
        await send('Page.navigate', { url: origin + path });
        await waitFor(`${ARRIVED} && location.pathname.toLowerCase().startsWith(${JSON.stringify(path.toLowerCase())})`, `Opening ${path}`);
    };
    const state = () => evaluate(STATE);
    const assertIdle = (control, name) => {
        assert.ok(control, `${name}: the refresh control is on the page`);
        assert.equal(control.label, 'Refresh', `${name}: label`);
        assert.equal(control.disabled, false, `${name}: button enabled`);
        assert.equal(control.busy, null, `${name}: aria-busy removed`);
        assert.equal(control.refreshing, false, `${name}: is-refreshing removed`);
        assert.equal(control.error, null, `${name}: no in-place error`);
    };
    const waitIdle = async name => {
        await waitFor(`(function (s) { return !!s && s.label === 'Refresh' && !s.disabled; })(${STATE})`, `${name}: Refresh returning to idle`);
        const control = await state();
        assertIdle(control, name);
        return control;
    };
    const record = (name, value) => { evidence.push({ name, value }); console.log('PASS', name); };

    if (process.env.PEGASUS_USER) {
        await navigate('/Account/SignIn');
        await evaluate(`document.getElementById('UserName').value = ${JSON.stringify(process.env.PEGASUS_USER)};
            document.getElementById('Password').value = ${JSON.stringify(process.env.PEGASUS_PASSWORD || '')};
            ${LEAVING}; document.getElementById('UserName').form.requestSubmit();`);
        await waitFor(`${ARRIVED} && !location.pathname.toLowerCase().includes('/account/signin')`, 'Signing in');
        record('Signed in as ' + process.env.PEGASUS_USER, true);
    }

    // The Case: its Refresh is intercepted and swapped in place.
    await navigate(`/Cases/${caseId}`);
    assert.equal(await evaluate('typeof window.pegasusDirtyEditForm'), 'function', 'The Case page runs the workspace script');
    assertIdle(await state(), 'Case opened');
    for (const press of [1, 2]) {
        // A marker on the window survives only an in-place swap, never a
        // navigation, so the step proves the Case re-queried where it stood.
        await evaluate(`window.__pegasusRefreshStayed = ${press}`);
        const pressed = await evaluate(`${PRESS}; ${STATE}`);
        assert.equal(pressed.label, 'Refreshing', `Case press ${press}: the press was taken`);
        assert.equal(pressed.disabled, true, `Case press ${press}: the button is busy while the Case re-queries`);
        const settled = await waitIdle(`Case press ${press}`);
        assert.equal(await evaluate('window.__pegasusRefreshStayed'), press, `Case press ${press}: the Case refreshed in place, not by navigating`);
        record(`Case press ${press} returns to idle after the in-place swap`, settled);
    }

    // Edit the Case, mark it dirty with its own values, and press Refresh over
    // the unsaved changes (Keep editing), during the save, and after it.
    assert.ok(await evaluate("(function (b) { if (!b) { return false; } b.click(); return true; })(document.querySelector('[data-case-edit]'))"), 'Edit Case is offered');
    await waitFor("document.querySelector('[data-case-record]').getAttribute('data-case-editing') === 'true' && !!document.querySelector('[data-case-save]')", 'Entering edit');
    assert.ok(await evaluate(`(function (form) {
        var control = Array.prototype.find.call(form.elements, function (c) { return c.type === 'text' && !c.disabled && !c.readOnly && c.offsetParent !== null; });
        if (!control) { return false; }
        control.dispatchEvent(new Event('input', { bubbles: true }));
        return true;
    })(document.getElementById('case-edit-form'))`), 'A text control of the Case form is editable');
    assert.equal(await evaluate('!!window.pegasusDirtyEditForm()'), true, 'The Case is dirty');
    let pressed = await evaluate(`${PRESS}; ${STATE}`);
    assert.equal(pressed.dialog, true, 'Refresh over unsaved changes asks first');
    assert.equal(pressed.label, 'Refreshing', 'Keep editing: the press was taken');
    await evaluate("document.querySelector('[data-edit-finish-keep]').click()");
    const kept = await waitIdle('Keep editing');
    assert.equal(kept.dialog, false, 'Keep editing: the question closed');
    assert.equal(kept.editing, 'true', 'Keep editing: still editing');
    assert.equal(await evaluate('!!window.pegasusDirtyEditForm()'), true, 'Keep editing: the changes are still there');
    record('Keep editing returns Refresh to idle without leaving edit', kept);

    assert.equal(await evaluate("document.getElementById('case-edit-form').checkValidity()"), true, 'The Case form is valid, so Save can start');
    const during = await evaluate(`document.querySelector('[data-case-save]').click(); ${PRESS}; ${STATE}`);
    assert.equal(during.dialog, false, 'Refresh during a save: declined, not asked');
    assert.equal(during.editing, 'true', 'Refresh during a save: the save is still in flight');
    assertIdle(during, 'Refresh during a save');
    record('Refresh pressed while a save is in flight is declined and idle at once', during);
    await waitFor("document.querySelector('[data-case-record]').getAttribute('data-case-editing') === 'false'", 'The save landing');
    const landed = await state();
    assertIdle(landed, 'After the save');
    pressed = await evaluate(`${PRESS}; ${STATE}`);
    assert.equal(pressed.label, 'Refreshing', 'After the save: the press was taken');
    record('Refresh works again once the save has landed', await waitIdle('After the save'));

    // The Triage Case: no workspace script, so its Refresh navigates and the
    // new document arrives idle.
    await navigate(`/Cases/${triageCaseId}`);
    assert.ok(await evaluate("!!document.querySelector('[data-triage-record]')"), 'The Triage Case renders its Triage record');
    assert.equal(await evaluate('typeof window.pegasusDirtyEditForm'), 'undefined', 'The Triage page runs no workspace script');
    assertIdle(await state(), 'Triage opened');
    for (const press of [1, 2]) {
        const pressedTriage = await evaluate(`${LEAVING}; ${PRESS}; ${STATE}`);
        assert.equal(pressedTriage.label, 'Refreshing', `Triage press ${press}: the press was taken`);
        await waitFor(`${ARRIVED} && !!document.querySelector('[data-triage-record]')`, `Triage press ${press}: the page reloading`);
        record(`Triage press ${press} reloads the page and arrives idle`, await waitIdle(`Triage press ${press}`));
    }

    assert.deepEqual(errors, [], 'No browser runtime exceptions');
    await writeFile(join(output, 'result.json'), JSON.stringify({ browser, origin, caseId, triageCaseId, evidence, errors }, null, 2));
    console.log('Evidence:', join(output, 'result.json'));
} finally {
    if (send) { await Promise.race([send('Browser.close'), delay(2000)]); }
    ws?.close(); proc.kill();
}
