// The local verification walk: drives a headless Chromium through the main
// Pegasus flows of a running local run over the Chrome DevTools protocol (the
// pattern of Test-CaseRefreshBrowser.mjs) and records one pass/fail/skipped
// row and one screenshot per step. On demand, not in CI; the lifecycle Smoke
// still proves process health only.
// Usage: PEGASUS_URL=https://localhost:7139 node scripts/Test-LocalVerificationBrowser.mjs
//   PEGASUS_RUN_ID        evidence folder name under artifacts/local-verification/
//   PEGASUS_USER/PASSWORD sign in first (required for a role other than Administrator)
//   PEGASUS_ROLE          Administrator | Engineer | User (default Administrator)
//   PEGASUS_FLAGS         comma list of enabled live integrations (vehicleLookup,boxCustody,glass,passwordSignIn,automationMcp,principalApi)
//   PEGASUS_UPLOAD_FILES  comma list of files for the Upload step (absent: skipped)
//   PEGASUS_PRINCIPAL_CODE Principal code for the created Case (default AX)
//   PEGASUS_REGISTRATION  registration for the created Case (default AB12CDE)
//   CHROME                Chromium/Chrome executable (default: the Playwright cache)
import { spawn } from 'node:child_process';
import { readFile, mkdir, writeFile, readdir } from 'node:fs/promises';
import { existsSync, openSync } from 'node:fs';
import { resolve, join, isAbsolute } from 'node:path';
import { homedir } from 'node:os';

const root = resolve(import.meta.dirname, '..');
const base = process.env.PEGASUS_URL;
if (!base) { console.error('PEGASUS_URL is required.'); process.exit(2); }
const origin = base.replace(/\/+$/, '');
const runId = process.env.PEGASUS_RUN_ID || 'adhoc';
const role = process.env.PEGASUS_ROLE || 'Administrator';
const flags = new Set((process.env.PEGASUS_FLAGS || '').split(',').map(f => f.trim()).filter(Boolean));
const uploadFiles = (process.env.PEGASUS_UPLOAD_FILES || '').split(',').map(f => f.trim()).filter(Boolean)
    .map(f => isAbsolute(f) ? f : resolve(f));
const principalCode = process.env.PEGASUS_PRINCIPAL_CODE || 'AX';
const registration = process.env.PEGASUS_REGISTRATION || 'AB12CDE';
if (role !== 'Administrator' && !process.env.PEGASUS_USER) {
    console.error(`PEGASUS_ROLE=${role} needs PEGASUS_USER and PEGASUS_PASSWORD.`); process.exit(2);
}

async function findChromium() {
    if (process.env.CHROME) { return process.env.CHROME; }
    const cache = join(homedir(), '.cache/ms-playwright');
    if (existsSync(cache)) {
        const versions = (await readdir(cache)).filter(n => n.startsWith('chromium-')).sort().reverse();
        for (const version of versions) {
            for (const dir of await readdir(join(cache, version)).catch(() => [])) {
                const candidate = join(cache, version, dir, 'chrome');
                if (dir.startsWith('chrome-linux') && existsSync(candidate)) { return candidate; }
            }
        }
    }
    for (const candidate of ['/usr/bin/chromium', '/usr/bin/chromium-browser', '/usr/bin/google-chrome']) {
        if (existsSync(candidate)) { return candidate; }
    }
    throw new Error('No Chromium found: set CHROME.');
}
const browser = await findChromium();
const output = join(root, 'artifacts/local-verification', runId, `${role.toLowerCase()}-${new Date().toISOString().replaceAll(/[:.]/g, '-')}`);
await mkdir(output, { recursive: true });

// The browser's own output goes beside the evidence: a Chromium that cannot
// start says why there, not nowhere.
const browserLog = openSync(join(output, 'browser.log'), 'w');
const proc = spawn(browser, ['--headless=new', '--disable-gpu', '--no-first-run', '--no-default-browser-check', '--ignore-certificate-errors',
    '--window-size=1580,1000', `--user-data-dir=${join(output, 'profile')}`, '--remote-debugging-port=0', 'about:blank'], { stdio: ['ignore', browserLog, browserLog] });
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
const LEAVING = 'window.pegasusWalkLeaving = true';
const ARRIVED = "window.pegasusWalkLeaving === undefined && document.readyState === 'complete'";
const steps = [];
let ws, send, stepIndex = 0;
let caseId = null;

try {
    let port;
    // A first start on a fresh profile can take tens of seconds on a cold
    // workstation; the port file is the only readiness signal.
    for (let i = 0; i < 600 && !port; i++) {
        await delay(100);
        try { port = Number((await readFile(join(output, 'profile/DevToolsActivePort'), 'utf8')).split('\n')[0]); } catch { /* starting */ }
    }
    if (!port) { throw new Error('The browser did not start.'); }
    const target = (await (await fetch(`http://127.0.0.1:${port}/json`)).json()).find(t => t.type === 'page');
    ws = new WebSocket(target.webSocketDebuggerUrl);
    await new Promise(resolve => ws.onopen = resolve);
    let sequence = 0; const pending = new Map();
    ws.onmessage = event => {
        const message = JSON.parse(event.data);
        if (message.id) { pending.get(message.id)?.(message); pending.delete(message.id); }
        if (message.method === 'Page.javascriptDialogOpening') { send('Page.handleJavaScriptDialog', { accept: true }); }
    };
    send = (method, params = {}) => new Promise(resolve => {
        const id = ++sequence; pending.set(id, resolve); ws.send(JSON.stringify({ id, method, params }));
    });
    await send('Page.enable'); await send('Runtime.enable'); await send('DOM.enable');
    await send('Emulation.setDeviceMetricsOverride', { width: 1580, height: 1000, deviceScaleFactor: 1, mobile: false });

    const evaluate = async (expression, tolerant = false) => {
        const response = await send('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true, userGesture: true });
        if (response.error) { if (tolerant) { return undefined; } throw new Error(response.error.message); }
        if (response.result?.exceptionDetails) { throw new Error(response.result.exceptionDetails.exception?.description || 'Script error'); }
        return response.result?.result?.value;
    };
    const waitFor = async (expression, what, seconds = 15) => {
        for (let i = 0; i < seconds * 20; i++) {
            if (await evaluate(expression, true)) { return true; }
            await delay(50);
        }
        throw new Error(`${what} did not happen within ${seconds} s at ${await evaluate('location.href', true)}`);
    };
    const navigate = async (path, seconds = 20) => {
        await evaluate(LEAVING, true);
        await send('Page.navigate', { url: origin + path });
        await waitFor(ARRIVED, `Opening ${path}`, seconds);
        return evaluate('location.pathname + location.search');
    };
    // A same-origin request from the page: the staff cookie travels, so the
    // answer is the one the signed-in operator would get.
    const probe = (path, init = {}) => evaluate(`fetch(${JSON.stringify(path)}, ${JSON.stringify({ redirect: 'manual', ...init })})
        .then(r => r.text().then(body => ({ status: r.status, type: r.type, body: body.slice(0, 2000) })))`);
    const screenshot = async name => {
        const file = `${String(stepIndex).padStart(2, '0')}-${name}.png`;
        const shot = await send('Page.captureScreenshot', { format: 'png' });
        if (shot.result?.data) { await writeFile(join(output, file), Buffer.from(shot.result.data, 'base64')); }
        return file;
    };
    const step = async (name, enabled, body) => {
        stepIndex++;
        const record = { name, status: 'skipped', detail: null, screenshot: null, url: null };
        steps.push(record);
        // Only `true` runs a step; a string is the reason it is skipped.
        if (enabled !== true) { record.detail = typeof enabled === 'string' && enabled ? enabled : 'not enabled for this run'; console.log('SKIP', name, '-', record.detail); return; }
        try {
            record.detail = (await body()) ?? 'ok';
            record.status = 'passed';
            console.log('PASS', name, '-', record.detail);
        } catch (error) {
            record.status = 'failed';
            record.detail = error.message;
            console.log('FAIL', name, '-', error.message);
        } finally {
            record.url = await evaluate('location.href', true) ?? null;
            try { record.screenshot = await screenshot(name); } catch { /* best effort */ }
        }
    };
    const expect = (condition, message) => { if (!condition) { throw new Error(message); } };
    const fill = (id, value) => evaluate(`(function (e) { e.value = ${JSON.stringify(value)}; e.dispatchEvent(new Event('input', { bubbles: true })); e.dispatchEvent(new Event('change', { bubbles: true })); return true; })(document.getElementById(${JSON.stringify(id)}))`);

    await navigate('/health/live');
    await step('health', true, async () => {
        const ready = await probe('/health/ready');
        expect(ready.status === 200, `/health/ready returned ${ready.status}`);
        return `/health/ready ${ready.status}`;
    });
    await step('version', true, async () => {
        const version = await probe('/diagnostics/version');
        expect(version.status === 200, `/diagnostics/version returned ${version.status}`);
        const body = JSON.parse(version.body);
        expect(/^[0-9a-f]{40}$/i.test(body.sourceSha || ''), 'sourceSha is not a 40-character SHA');
        return `version ${body.version} source ${body.sourceSha.slice(0, 12)}`;
    });
    await step('sign-in', process.env.PEGASUS_USER ? true : 'auto sign-in run (no PEGASUS_USER)', async () => {
        await navigate('/Account/SignIn');
        expect(await evaluate("!!document.getElementById('UserName')"), 'The sign-in form did not render');
        await fill('UserName', process.env.PEGASUS_USER);
        await fill('Password', process.env.PEGASUS_PASSWORD || '');
        await evaluate(`${LEAVING}; document.getElementById('UserName').form.requestSubmit();`);
        await waitFor(`${ARRIVED} && !location.pathname.toLowerCase().includes('/account/signin')`, 'Signing in');
        const where = await evaluate('location.pathname');
        expect(!where.toLowerCase().includes('/account/passwordchange'), 'The account must change its password first');
        return `signed in as ${process.env.PEGASUS_USER}, landed on ${where}`;
    });
    await step('work-centre', true, async () => {
        const path = await navigate('/');
        expect(!path.toLowerCase().includes('/account/'), `Landed on ${path} instead of the Work Centre`);
        const title = await evaluate('document.title');
        expect(/work centre/i.test(title), `Title was '${title}'`);
        return title;
    });
    await step('cases-list', true, async () => {
        await navigate('/Cases');
        expect(await evaluate("!!document.querySelector('[data-cases-queue]')"), 'The Cases queue did not render');
        const rows = await evaluate("document.querySelectorAll('[data-cases-row]').length");
        return `${rows} case row(s) in the queue`;
    });
    await step('create-case', true, async () => {
        await navigate('/Cases/Create');
        expect(await evaluate("!!document.getElementById('PrincipalCode')"), 'The manual Create case form did not render');
        await fill('PrincipalCode', principalCode);
        await fill('ClaimantName', `Local verification ${new Date().toISOString().slice(0, 16)}`);
        await fill('ClaimNumber', `LV-${Date.now().toString(36).toUpperCase()}`);
        await fill('VehicleRegistration', registration);
        await evaluate(`${LEAVING}; document.getElementById('PrincipalCode').form.requestSubmit();`);
        await waitFor(`${ARRIVED} && /\\/Cases\\/[0-9a-f-]{36}$/i.test(location.pathname)`, 'Creating the Case', 30);
        caseId = await evaluate('location.pathname.split("/").pop()');
        const errors = await evaluate("Array.from(document.querySelectorAll('.validation-summary-errors li, [data-error-summary] li')).map(e => e.textContent.trim()).join('; ')");
        expect(!errors, `Create case reported: ${errors}`);
        return `Case ${caseId} created for Principal ${principalCode}`;
    });
    await step('case-details', caseId ? true : 'no Case was created', async () => {
        await navigate(`/Cases/${caseId}`);
        expect(await evaluate("!!document.querySelector('[data-case-record]')"), 'The Case record did not render');
        expect(await evaluate('typeof window.pegasusGlassReturn') === 'function', 'The Case workspace script did not load');
        return `Case ${caseId} renders its record and workspace script`;
    });
    await step('upload', uploadFiles.length ? true : 'no PEGASUS_UPLOAD_FILES', async () => {
        for (const file of uploadFiles) { expect(existsSync(file), `Upload file missing: ${file}`); }
        await navigate(caseId ? `/Upload?caseId=${caseId}` : '/Upload');
        expect(await evaluate("!!document.getElementById('Upload')"), 'The Upload page did not render (Features:LocalIntake off?)');
        const { root: documentNode } = (await send('DOM.getDocument', { depth: 1 })).result;
        const { nodeId } = (await send('DOM.querySelector', { nodeId: documentNode.nodeId, selector: '#Upload' })).result;
        expect(nodeId, 'The file input was not found');
        await send('DOM.setFileInputFiles', { nodeId, files: uploadFiles });
        await evaluate(`${LEAVING}; document.getElementById('Upload').form.submit();`);
        await waitFor(`${ARRIVED} && !location.pathname.toLowerCase().startsWith('/upload') || ${ARRIVED} && location.pathname.toLowerCase().startsWith('/upload/')`, 'Uploading', 60);
        const where = await evaluate('location.pathname');
        expect(/\/Cases\/|\/Upload\/Status\/|\/Upload\/Group/i.test(where), `Upload landed on ${where}`);
        return `${uploadFiles.length} file(s) accepted; landed on ${where}`;
    });
    await step('vehicle-lookup', !caseId ? 'no Case was created' : flags.has('vehicleLookup') ? true : 'vehicleLookup flag off (replay fixtures only)', async () => {
        await navigate(`/Cases/${caseId}`);
        const before = await evaluate("document.querySelector('[data-vehicle-lookup-line]')?.textContent.trim()");
        if (!await evaluate("!!document.querySelector('[data-vehicle-lookup] button')")) {
            expect(await evaluate("(function (b) { if (!b) { return false; } b.click(); return true; })(document.querySelector('[data-case-edit]'))"), 'Edit Case is not offered');
            await waitFor("document.querySelector('[data-case-record]').getAttribute('data-case-editing') === 'true'", 'Entering edit');
            await waitFor("!!document.querySelector('[data-vehicle-lookup] button')", 'The lookup control appearing');
        }
        await evaluate(`${LEAVING}; document.querySelector('[data-vehicle-lookup] button').click();`);
        // The request answers either with a full navigation back to the Case or
        // in place with the queued notice; accept whichever the page does.
        await waitFor(`${ARRIVED} || /vehicle lookup was queued/i.test(document.body.innerText)`, 'The lookup request', 30);
        let line = null;
        for (let i = 0; i < 18; i++) {
            await navigate(`/Cases/${caseId}`);
            line = await evaluate("document.querySelector('[data-vehicle-lookup-line]')?.textContent.trim()");
            if (line && line !== 'Not yet looked up' && line !== before) { break; }
            await delay(5000);
        }
        expect(line && line !== 'Not yet looked up', `No lookup answer within 90 s (line: ${line})`);
        expect(!/^Failed/i.test(line) && !/^Lookup failed/i.test(line), `The lookup failed: ${line}`);
        return `Worker answered: ${line}`;
    });
    await step('documents', caseId ? true : 'no Case was created', async () => {
        await navigate(`/Cases/${caseId}`);
        expect(await evaluate("!!document.getElementById('section-files')"), 'The Files section did not render (document custody off?)');
        let detail = 'Files section renders';
        if (uploadFiles.length) {
            let filed = false;
            for (let i = 0; i < 18 && !filed; i++) {
                await navigate(`/Cases/${caseId}`);
                filed = await evaluate("!!document.getElementById('section-files') && !document.querySelector('#section-files [data-documents-empty]')");
                if (!filed) { await delay(5000); }
            }
            expect(filed, 'The uploaded file was not listed on the Case within 90 s');
            detail += flags.has('boxCustody') ? '; uploaded document filed through Box custody' : '; uploaded document filed through local custody';
        }
        return detail;
    });
    await step('administration', true, async () => {
        const path = await navigate('/Administration');
        if (role === 'Administrator') {
            expect(path.toLowerCase().startsWith('/administration'), `Administrator landed on ${path}`);
            return 'Administration opens for the Administrator';
        }
        expect(path.toLowerCase().includes('/account/accessdenied'), `${role} reached ${path} instead of AccessDenied`);
        return `${role} is refused Administration (AccessDenied)`;
    });
    await step('glass-control', caseId ? true : 'no Case was created', async () => {
        await navigate(`/Cases/${caseId}`);
        if (await evaluate("document.querySelector('[data-case-record]')?.getAttribute('data-case-editing') !== 'true'")) {
            if (await evaluate("(function (b) { if (!b) { return false; } b.click(); return true; })(document.querySelector('[data-case-edit]'))")) {
                await waitFor("document.querySelector('[data-case-record]').getAttribute('data-case-editing') === 'true'", 'Entering edit');
            }
        }
        const control = await evaluate(`(function (c) { return c ? { hidden: c.hidden, launch: !!c.querySelector('[data-glass-slot="launch"]') } : null; })(document.querySelector('[data-glass-controls="launch"]'))`);
        if (!flags.has('glass')) {
            expect(!control || control.hidden, 'Glass\'s is not composed but the launch control is visible');
            return 'Glass\'s not composed: launch control hidden';
        }
        return control && !control.hidden
            ? 'Glass\'s composed and this account holds a Glass\'s credential: Launch offered'
            : 'Glass\'s composed; Launch hidden for this account (no stored Glass\'s credential or not editable)';
    });
    await step('principal-api', flags.has('principalApi') ? true : 'principalApi flag off', async () => {
        const response = await probe('/api/principal/v1/submissions', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{}' });
        expect(response.status === 401, `Unauthenticated submission returned ${response.status}, expected 401`);
        return 'Principal API route answers 401 without a bearer credential';
    });
    await step('mcp', flags.has('automationMcp') ? true : 'automationMcp flag off', async () => {
        const response = await probe('/connect/token', { method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, body: 'grant_type=client_credentials' });
        expect([400, 401].includes(response.status), `/connect/token returned ${response.status}, expected 400 or 401`);
        return `MCP token endpoint answers ${response.status} to an unauthenticated grant`;
    });
    await step('sign-out', process.env.PEGASUS_USER ? true : 'auto sign-in run', async () => {
        await navigate('/Account/SignOut');
        return 'signed out';
    });
} finally {
    const summary = {
        kind: 'Pegasus.LocalVerification.Walk', runId, role, origin, browser, caseId, flags: [...flags],
        observedUtc: new Date().toISOString(),
        counts: { passed: steps.filter(s => s.status === 'passed').length, failed: steps.filter(s => s.status === 'failed').length, skipped: steps.filter(s => s.status === 'skipped').length },
        steps
    };
    await writeFile(join(output, 'result.json'), JSON.stringify(summary, null, 2) + '\n');
    console.log(`Result: ${summary.counts.passed} passed, ${summary.counts.failed} failed, ${summary.counts.skipped} skipped -> ${join(output, 'result.json')}`);
    if (send) { await Promise.race([send('Browser.close'), delay(2000)]); }
    ws?.close(); proc.kill();
    process.exitCode = summary.counts.failed > 0 ? 1 : 0;
}
