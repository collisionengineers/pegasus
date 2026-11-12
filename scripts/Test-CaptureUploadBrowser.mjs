// Browser evidence for the photo capture app's own script (FRD-28; issues 2095 and 2097): a file
// over the single-request bound goes in parts with the right digest, a file the phone can no longer
// read says so and frees Send, the signal sentence is kept for a server that cannot be reached, and
// the walk-around video's Record and Choose open the camera and the gallery. Runs the production
// capture.js, capture.css and shell.html against scripted local answers in an isolated headless
// Chrome, Chromium or Edge profile. On demand, not in CI; it makes no request beyond its own local
// server.
// Usage: node scripts/Test-CaptureUploadBrowser.mjs [absolute-path-to-browser]
//        or CHROME in the environment.
// Every scenario runs; the exit code is 1 when any failed. Evidence is written under
// artifacts/capture/browser/.
import { spawn } from 'node:child_process';
import { createServer } from 'node:http';
import { createHash, randomBytes } from 'node:crypto';
import { readFile, mkdir, writeFile, utimes } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { resolve, join } from 'node:path';
import assert from 'node:assert/strict';

const root = resolve(import.meta.dirname, '..');
const browser = process.argv[2] || process.env.CHROME || [
    join(process.env.ProgramFiles || '', 'Google/Chrome/Application/chrome.exe'),
    join(process.env['ProgramFiles(x86)'] || '', 'Microsoft/Edge/Application/msedge.exe'),
    '/usr/bin/google-chrome', '/usr/bin/chromium', '/usr/bin/chromium-browser'
].find(existsSync);
if (!browser) { throw new Error('Supply the Chrome, Chromium or Edge executable path.'); }
const output = join(root, 'artifacts/capture/browser', new Date().toISOString().replaceAll(/[:.]/g, '-'));
await mkdir(output, { recursive: true });
const asset = name => readFile(join(root, 'src/Pegasus.Web/wwwroot/capture', name));
const script = await asset('capture.js'), styles = await asset('capture.css');
const scriptText = script.toString('utf8');
// The two sentences, read from the script that owns them.
const offline = /offline: '([^']+)'/.exec(scriptText)[1], unreadable = /unreadable: '([^']+)'/.exec(scriptText)[1];
const partBytes = Number(/\bconst ONE=(\d+)/.exec(scriptText)[1]);

// The files a sender picks: a video over the single-request bound (an MP4 header and filler) and
// a small PDF. Each scenario picks its own copy, so one made unreadable harms no other.
const mp4 = Buffer.concat([Buffer.from([0, 0, 0, 0x18]), Buffer.from('ftypmp42'), Buffer.from([0, 0, 0, 0]), Buffer.from('isommp42'), randomBytes(12345678 - 24)]);
const pdf = Buffer.concat([Buffer.from('%PDF-1.4\n'), randomBytes(2000)]);
const fileFor = async (name, bytes) => { const path = join(output, name); await writeFile(path, bytes); return path; };

// The link context a Case link gets, and the page a link opens: shell.html as the Razor page emits it.
const context = {
    kind: 'capture', mode: 'guided', liveCameraOnly: false, originals: true, origin: 'case', quickLinkFor: null,
    registration: 'AB12CDE', registrationDisplay: 'AB12 CDE', customerName: null, incidentDate: null, expiresAtUtc: null,
    sent: false, linkKey: 'fixture', details: null, counts: { total: 0, byKind: {}, overviewPositions: [], damage: [] }, standing: null
};
const shell = (await asset('shell.html')).toString('utf8')
    .replaceAll('__LOGO_URL__', '').replaceAll('__CAPTURE_ASSETS_JSON__', '{}').replaceAll('__CAPTURE_CONTEXT_JSON__', JSON.stringify(context));
const page = `<!doctype html><html lang="en-GB"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
<title>Collision Engineers – Vehicle photo capture</title><link rel="stylesheet" href="/capture/capture.css"></head><body>${shell}<script src="/capture/capture.js" defer></script></body></html>`;

// The scripted API: what each scenario asks of it is set on `api`, and every call is kept.
const api = { calls: [], uploads: new Map(), holdPartMs: 0, partStatus: 201 };
const reset = () => { api.calls = []; api.uploads = new Map(); api.holdPartMs = 0; api.partStatus = 201; };
const json = (res, status, body) => { res.writeHead(status, { 'content-type': status >= 400 ? 'application/problem+json' : 'application/json' }); res.end(JSON.stringify(body)); };
const delay = ms => new Promise(done => setTimeout(done, ms));
const server = createServer(async (req, res) => {
    const url = new URL(req.url, 'http://local');
    if (url.pathname.startsWith('/c/')) { res.writeHead(200, { 'content-type': 'text/html; charset=utf-8' }); return res.end(page); }
    if (url.pathname === '/capture/capture.js') { res.writeHead(200, { 'content-type': 'text/javascript' }); return res.end(script); }
    if (url.pathname === '/capture/capture.css') { res.writeHead(200, { 'content-type': 'text/css' }); return res.end(styles); }
    if (!url.pathname.startsWith('/capture/api/v1/')) { res.writeHead(404); return res.end(); }
    const chunks = []; for await (const chunk of req) { chunks.push(chunk); }
    const body = Buffer.concat(chunks), route = url.pathname.slice('/capture/api/v1/'.length);
    const call = { method: req.method, route: route.replace(/[0-9a-f-]{36}/, '{id}'), bytes: body.length, type: req.headers['content-type'] || '', kind: url.searchParams.get('kind') };
    api.calls.push(call);
    const part = /^items\/([0-9a-f-]{36})\/parts\/(\d+)$/.exec(route), commit = /^items\/([0-9a-f-]{36})\/commit$/.exec(route);
    if (route === 'context' || route === 'details') { return json(res, 200, context); }
    if (route === 'complete') { return json(res, 200, { missing: [], count: 0 }); }
    if (part) {
        if (api.holdPartMs) { await delay(api.holdPartMs); }
        if (api.partStatus !== 201) { return json(res, api.partStatus, { title: '', code: 'server_error' }); }
        const parts = api.uploads.get(part[1]) || new Map(); parts.set(Number(part[2]), body); api.uploads.set(part[1], parts);
        return json(res, 201, { itemId: part[1], part: Number(part[2]), write: 'created' });
    }
    if (commit) {
        // What the API does: the staged bytes must be the declared length and digest.
        const parts = api.uploads.get(commit[1]) || new Map(), declared = JSON.parse(body.toString('utf8'));
        const whole = Buffer.concat([...parts.keys()].sort((a, b) => a - b).map(n => parts.get(n)));
        call.digestMatches = whole.length === declared.totalBytes && createHash('sha256').update(whole).digest('hex') === String(declared.sha256).toLowerCase();
        call.partLengths = [...parts.keys()].sort((a, b) => a - b).map(n => parts.get(n).length);
        return call.digestMatches ? json(res, 201, { itemId: commit[1], write: 'created', count: 1 })
            : json(res, 409, { title: 'The file is not the one its digest names.', code: 'item_conflict' });
    }
    if (/^items\/[0-9a-f-]{36}$/.test(route)) { return json(res, 201, { itemId: route.slice(6), write: 'created', count: 1 }); }
    res.writeHead(404); res.end();
});
await new Promise(done => server.listen(0, '127.0.0.1', done));
const origin = `http://127.0.0.1:${server.address().port}`;

const proc = spawn(browser, ['--headless=new', '--disable-gpu', '--no-first-run', '--no-default-browser-check', '--no-sandbox',
    `--user-data-dir=${join(output, 'profile')}`, '--remote-debugging-port=0', 'about:blank'], { stdio: 'ignore' });
const results = [], errors = [];
let failures = 0;
try {
    let port;
    for (let i = 0; i < 600 && !port; i++) {
        await delay(100);
        try { port = Number((await readFile(join(output, 'profile/DevToolsActivePort'), 'utf8')).split('\n')[0]); } catch { /* starting */ }
    }
    if (!port) { throw new Error('The browser did not start.'); }
    let target;
    for (let i = 0; i < 100 && !target; i++) {
        try { target = (await (await fetch(`http://127.0.0.1:${port}/json`)).json()).find(t => t.type === 'page'); } catch { /* starting */ }
        if (!target) { await delay(100); }
    }
    const ws = new WebSocket(target.webSocketDebuggerUrl);
    await new Promise(done => ws.onopen = done);
    let sequence = 0, chooser = null; const pending = new Map();
    ws.onmessage = event => {
        const message = JSON.parse(event.data);
        if (message.id) { pending.get(message.id)?.(message); pending.delete(message.id); return; }
        if (message.method === 'Runtime.exceptionThrown') { errors.push(message.params.exceptionDetails?.exception?.description || message.params.exceptionDetails?.text); }
        if (message.method === 'Page.fileChooserOpened') { chooser?.(message.params); }
    };
    const send = (method, params = {}) => new Promise(done => { const id = ++sequence; pending.set(id, done); ws.send(JSON.stringify({ id, method, params })); });
    const evaluate = async expression => {
        const response = await send('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true, userGesture: true });
        if (response.result?.exceptionDetails) { throw new Error(response.result.exceptionDetails.exception?.description || 'Script error'); }
        return response.result?.result?.value;
    };
    const until = async (probe, what, seconds = 20) => {
        for (let i = 0; i < seconds * 20; i++) { if (await probe()) { return; } await delay(50); }
        throw new Error(`${what} did not happen within ${seconds} s.`);
    };
    for (const domain of ['Page', 'Runtime', 'DOM']) { await send(`${domain}.enable`); }
    await send('Emulation.setDeviceMetricsOverride', { width: 360, height: 780, deviceScaleFactor: 2, mobile: true });
    // The app opens on the step this phone had reached, which it keeps under the link's key.
    const open = async step => {
        reset();
        await send('Page.navigate', { url: 'about:blank' });
        const added = await send('Page.addScriptToEvaluateOnNewDocument', { source: `try { localStorage.setItem('ce-capture-fixture', ${JSON.stringify(JSON.stringify({ stepName: step }))}); } catch (e) { }` });
        await send('Page.navigate', { url: `${origin}/c/fixture-link` });
        await until(() => evaluate("!!document.querySelector('#cta button')"), 'The app starting');
        await send('Page.removeScriptToEvaluateOnNewDocument', { identifier: added.result.identifier });
    };
    const pick = async (input, path) => {
        const document = await send('DOM.getDocument');
        const node = await send('DOM.querySelector', { nodeId: document.result.root.nodeId, selector: input });
        await send('DOM.setFileInputFiles', { files: [path], nodeId: node.result.nodeId });
    };
    const videoRow = () => evaluate(`(row => ({ classes: [...row.classList].join(' '), buttons: [...row.querySelectorAll('button')].map(b => b.textContent.trim()) }))(document.querySelector('.xrow[data-row=video]'))`);
    const screenshot = async name => { const shot = await send('Page.captureScreenshot', { format: 'png' }); await writeFile(join(output, `${name}.png`), Buffer.from(shot.result.data, 'base64')); };
    const commits = () => api.calls.filter(c => c.route.endsWith('/commit'));
    const scenario = async (name, run) => {
        try { await run(); results.push({ name, passed: true }); console.log('PASS', name); }
        catch (error) { failures++; results.push({ name, passed: false, error: error.message, calls: api.calls }); console.log('FAIL', name, '-', error.message); }
    };

    await scenario('The video row offers Record and Choose until it has a video (2095, R984, R985)', async () => {
        await open('extras');
        assert.deepEqual(await videoRow(), { classes: 'xrow two', buttons: ['Record', 'Choose'] });
        assert.equal(await evaluate("document.querySelector('.xrow').scrollWidth <= document.querySelector('.xrow').clientWidth"), true, 'the row overflows at 360 px');
        await screenshot('extras-360');
    });

    await scenario('Record opens the camera and Choose the phone\'s own picker (2095, R984)', async () => {
        await open('extras');
        await send('Page.setInterceptFileChooserDialog', { enabled: true });
        try {
            for (const [selector, expected] of [['[data-x=video][data-record]', 'environment'], ['[data-x=video]:not([data-record])', null], ['[data-x=video][data-record]', 'environment']]) {
                const opened = new Promise(done => { chooser = done; });
                await evaluate(`document.querySelector(${JSON.stringify(selector)}).click()`);
                await Promise.race([opened, delay(5000).then(() => { throw new Error(`${selector} opened no picker.`); })]);
                assert.equal(await evaluate("document.getElementById('fileVideo').getAttribute('capture')"), expected, selector);
            }
        } finally { chooser = null; await send('Page.setInterceptFileChooserDialog', { enabled: false }); }
    });

    await scenario('A video over the single-request bound goes in parts, read a part at a time, with the file\'s digest (2097, R983)', async () => {
        await open('extras');
        await pick('#fileVideo', await fileFor('sent.mp4', mp4));
        await until(async () => commits().length > 0, 'The commit');
        const commit = commits()[0];
        assert.deepEqual(commit.partLengths, [partBytes, partBytes, mp4.length - 2 * partBytes]);
        assert.equal(commit.digestMatches, true, 'the digest the app declared is not the file\'s');
        assert.ok(api.calls.filter(c => c.route.includes('/parts/')).every(c => c.type === 'video/mp4' && c.kind === 'video'));
        await until(async () => (await videoRow()).classes === 'xrow done', 'The row reading Added');
        assert.deepEqual((await videoRow()).buttons, ['Added']);
        await screenshot('extras-added-360');
    });

    await scenario('A small file goes whole in one request (2097)', async () => {
        await open('extras');
        await pick('#fileDocs', await fileFor('estimate.pdf', pdf));
        await until(async () => api.calls.some(c => c.route === 'items/{id}'), 'The upload');
        const call = api.calls.find(c => c.route === 'items/{id}');
        assert.deepEqual({ bytes: call.bytes, type: call.type, kind: call.kind }, { bytes: pdf.length, type: 'application/pdf', kind: 'paperwork' });
    });

    await scenario('A file the phone can no longer read says so, is not tried again, and leaves Send free (2097, R983)', async () => {
        await open('extras');
        api.holdPartMs = 1500;
        const path = await fileFor('unreadable.mp4', mp4);
        await pick('#fileVideo', path);
        await until(async () => api.calls.some(c => c.route.endsWith('/parts/1')), 'The first part');
        // The file changes under the page while its first part is on the way: the browser refuses every read after.
        const later = new Date(Date.now() + 60000); await utimes(path, later, later);
        await until(async () => await evaluate("document.querySelector('#note').textContent") === unreadable, 'The sentence');
        const sent = api.calls.length;
        await until(async () => (await videoRow()).classes === 'xrow two failed', 'The row outlined');
        assert.deepEqual((await videoRow()).buttons, ['Record', 'Choose']);
        await screenshot('extras-unreadable-360');
        await delay(4000);
        assert.equal(api.calls.length, sent, 'the file was tried again');
        assert.equal(commits().length, 0);
        await evaluate("document.getElementById('next').click()");
        await until(() => evaluate("/Send to Collision Engineers/.test((document.getElementById('next') || {}).textContent || '')"), 'The review page');
        assert.equal(await evaluate("document.getElementById('next').disabled"), false, 'Send is held back');
        assert.equal(await evaluate(`document.body.textContent.includes(${JSON.stringify(offline)})`), false, 'the signal sentence is shown');
    });

    await scenario('A server that cannot be reached keeps the signal sentence and the retry (R786)', async () => {
        await open('extras');
        api.partStatus = 503;
        await pick('#fileVideo', await fileFor('waiting.mp4', mp4));
        await until(() => evaluate(`(document.querySelector('[data-retry]') || {}).textContent === ${JSON.stringify(offline)}`), 'The retry line', 30);
        assert.ok(api.calls.filter(c => c.route.endsWith('/parts/1')).length >= 3);
        assert.equal(await evaluate("document.querySelector('#note').textContent"), '', 'a sentence was shown for a file that is only waiting');
        api.partStatus = 201;
        await evaluate("document.querySelector('[data-retry]').click()");
        await until(async () => commits().some(c => c.digestMatches), 'The commit after the retry');
    });

    await scenario('No browser runtime exception', async () => { assert.deepEqual(errors, []); });
} finally {
    await writeFile(join(output, 'evidence.json'), JSON.stringify({ results, errors }, null, 2));
    proc.kill(); server.close();
}
console.log(failures ? `${failures} scenario(s) failed.` : 'Every scenario passed.', 'Evidence:', join(output, 'evidence.json'));
process.exit(failures ? 1 : 0);
