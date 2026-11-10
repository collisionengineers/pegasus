// Browser evidence for the Case page's own script: section jumps across lazy sections (issues 1496 and
// 1497), report wording the Case refused (issues 1814 and 2043) and wording blocks left as drawn (issue
// 2042). Runs the production case-workspace.js against
// a fixture Case page and scripted local answers in an isolated headless Chrome, Chromium or Edge
// profile. On demand, not in CI; it makes no request beyond its own local server.
// Usage: node scripts/Test-CasePageBrowser.mjs [absolute-path-to-browser]
//        or CHROME in the environment.
// Every scenario runs; the exit code is 1 when any failed. Evidence is written under
// artifacts/case-page/browser/.
import { spawn } from 'node:child_process';
import { createServer } from 'node:http';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
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
const output = join(root, 'artifacts/case-page/browser', new Date().toISOString().replaceAll(/[:.]/g, '-'));
await mkdir(output, { recursive: true });
// The shared layout script first, as the layout loads it: the Case page's script reads its helpers.
const siteScript = await readFile(join(root, 'src/Pegasus.Web/wwwroot/js/site.js'));
const workspaceScript = await readFile(join(root, 'src/Pegasus.Web/wwwroot/js/case-workspace.js'));
// The sentence a wording change is refused with on the page, read from its one owner.
const wordingOwner = await readFile(join(root, 'src/Pegasus.Web/Presentation/ReportWordingEdits.cs'), 'utf8');
const wordingRefusal = /const string Unavailable =\s*"([^"]+)";/.exec(wordingOwner)[1]
    + /const string UnavailableOnPage = Unavailable \+ "([^"]+)";/.exec(wordingOwner)[1];

// The page as the Case page draws it, as far as the frame's script reads it: the sticky block and its
// section row (standing 19px lower at the top of the page than once it has stuck, as under the real
// page's padding), the sections in their order with the deferred ones as placeholders (Vehicle and
// Valuation while reading; Files and Notes always), the aside's blocker links and, while editing, the
// Save form and the Report section's wording panel with two blocks. The page scrolls smoothly, as
// site.css has it. ?editing=1 edits, ?section= addresses a section, ?short=a,b gives those sections a
// short body.
function fixture() {
    return `<!doctype html><html><head><meta charset="utf-8"><title>Case page fixture</title>
    <style>
    html{scroll-behavior:smooth}body{margin:0;padding-top:19px;font:13px sans-serif}
    [data-sticky-block]{position:sticky;top:0;height:96px;background:#fff;z-index:1}
    .record-section{box-sizing:border-box;height:1200px;overflow:hidden;border-top:1px solid #999}
    .record-section.is-short{height:300px}.record-section.section-placeholder{height:120px}
    h2{margin:0;height:30px}.cell{height:40px}.cell--top{margin-top:30px}.cell--deep{margin-top:800px}
    [data-case-aside]{position:fixed;right:0;top:120px;width:180px}[hidden]{display:none}
    </style></head><body>
    <script>
    var params = new URLSearchParams(location.search);
    var editing = params.get('editing') === '1';
    var addressed = (params.get('section') || '').toLowerCase();
    var shortOnes = (params.get('short') || '').split(',');
    var order = ['overview', 'claim', 'vehicle', 'valuation', 'estimate', 'settlement', 'report', 'files', 'notes'];
    var parentOf = { valuation: 'vehicle' };
    var lazy = editing ? { files: 1, notes: 1 } : { vehicle: 1, valuation: 1, files: 1, notes: 1 };
    window.trace = []; window.holding = {}; window.held = {}; window.failing = {};
    window.state = { version: 1, lease: editing ? 'lease-1' : '', reference: 'CL-1', wording: 'Composed.', heading: 'Nature of Incident' };
    // While false the Case's report cannot be put together: a save carrying wording is refused, and the
    // page as the Case then stands draws the wording panel without its blocks. pageDrawsBlocks makes the
    // page draw them all the same, for a page and a Save that disagree.
    window.reportReady = true; window.pageDrawsBlocks = false;
    var wordingRefusal = ${JSON.stringify(wordingRefusal)};
    // The addressed section and its parent draw with the page, as SectionIsDeferred has it.
    function deferred(key) {
        return !!lazy[key] && key !== addressed && key !== parentOf[addressed]
            && !(addressed === 'vehicle' && parentOf[key] === 'vehicle');
    }
    function attributes(key) {
        return ' id="section-' + key + '" data-section="' + key + '"'
            + (parentOf[key] ? ' data-section-parent="' + parentOf[key] + '"' : '');
    }
    function saveForm() {
        return '<form id="case-edit-form" method="post" action="/case?handler=Save"><input name="id" type="hidden" value="case">'
            + '<input name="expectedVersion" type="hidden" value="' + state.version + '" data-carry-forward>'
            + '<input name="editLeaseToken" type="hidden" value="' + state.lease + '" data-carry-forward>'
            + '<input name="operationKey" type="hidden" value="save-' + state.version + '" data-carry-forward>'
            + '<input id="reference" name="reference" value="' + state.reference + '"></form>';
    }
    function body(key) {
        var inside = '';
        if (key === 'overview' && editing) { inside = saveForm(); }
        if (key === 'claim') {
            inside = '<div class="cell cell--top" data-field="claim-number">Claim number</div>'
                + '<div class="cell cell--deep" data-field="incident-date">Incident date</div>';
        }
        if (key === 'valuation') { inside = '<div class="cell cell--top" data-field="retail-trade">Retail/Trade</div>'; }
        if (key === 'settlement') { inside = '<a id="to-valuation" href="/case?section=valuation" data-section-jump="valuation">Valuation</a>'; }
        if (key === 'report' && editing) {
            inside = '<div class="sub-panel wording" data-report-wording data-wording-refusal="' + wordingRefusal + '"><div data-wording-settings></div>'
                + (reportReady || pageDrawsBlocks
                    ? '<div data-wording-list>' + wordingRow(0, 'nature', 'Nature of Incident', state.heading, 'Composed.', state.wording)
                        + wordingRow(1, 'settlement', 'Repair settlement', 'Repair settlement', 'Composed settlement.', 'Composed settlement.')
                        + '</div>'
                    : '')
                + '</div>';
        }
        return '<section class="record-section panel' + (shortOnes.indexOf(key) >= 0 ? ' is-short' : '') + '"' + attributes(key) + '>'
            + '<h2>' + key + '</h2>' + inside + '</section>';
    }
    // One wording block as _CaseReport draws it: the heading and sentence it was composed with on the
    // row, the heading and the wording as the Case holds them in its controls.
    function wordingRow(slot, key, heading, title, composed, text) {
        var name = 'wordingEdits[' + slot + ']';
        return '<div class="wb" data-wording-row data-wording-block="' + key + '" data-wording-composed="' + composed + '" data-wording-heading="' + heading + '">'
            + '<input type="hidden" name="' + name + '.Key" value="' + key + '" form="case-edit-form" data-wording-key>'
            + '<input type="hidden" name="' + name + '.Order" value="' + slot + '" form="case-edit-form" data-wording-order>'
            + '<input id="heading-' + key + '" class="fi wbt" name="' + name + '.Title" value="' + title + '" form="case-edit-form">'
            + '<textarea id="wording-' + key + '" name="' + name + '.Text" form="case-edit-form" data-wording-text>' + text + '</textarea></div>';
    }
    function placeholder(key) {
        return '<section class="record-section panel section-placeholder"' + attributes(key) + ' data-lazy="' + key + '"></section>';
    }
    function ribbon() {
        return '<div data-case-ribbon-facts></div>'
            + '<div data-case-ribbon-actions><span data-lease-line hidden data-lease-saving-text="Saving" data-lease-saved-text="Saved"></span></div>';
    }
    function aside() {
        return '<div data-case-aside>'
            + '<a id="blocker-claim" href="/case?section=claim" data-section-jump="claim" data-section-field="claim-number">Claim number</a> '
            + '<a id="blocker-incident" href="/case?section=claim" data-section-jump="claim" data-section-field="incident-date">Incident date</a> '
            + '<a id="blocker-retail" href="/case?section=valuation" data-section-jump="valuation" data-section-field="retail-trade">Retail/Trade</a></div>';
    }
    function record(commit) {
        return '<article data-case-record data-case-version="' + state.version + '" data-case-editing="' + (editing ? 'true' : 'false') + '"'
            + ' data-section-current="' + (addressed || 'overview') + '"' + (commit ? " data-editor-commit='" + JSON.stringify(commit) + "'" : '') + '>';
    }
    function page() {
        var current = parentOf[addressed] || addressed || 'overview';
        return '<div data-case-notices></div>' + record()
            + '<div data-sticky-block>' + ribbon() + '<nav data-section-nav>'
            + order.filter(function (key) { return !parentOf[key]; }).map(function (key) {
                return '<a href="/case?section=' + key + '" data-section-link="' + key + '" aria-current="' + (key === current ? 'true' : 'false') + '">' + key + '</a>';
            }).join(' ') + '</nav></div>'
            + '<div id="case-main">' + order.map(function (key) { return deferred(key) ? placeholder(key) : body(key); }).join('') + '</div>'
            + aside() + '</article><div data-case-viewer-host></div><div data-case-dialogs></div>';
    }
    // What the server answers a commit with (FRD-16): the wrapper that confirms it, the swap roots and
    // the Save form the next commit carries forward, with no section.
    function answer(commit, notice) {
        return '<div data-case-notices>' + (notice || '') + '</div>' + record(commit) + ribbon() + saveForm() + aside() + '</article><div data-case-dialogs></div>';
    }
    document.write(page());
    // Every answer is local. One held (holding[key]) waits for release(key): a section by its key, the
    // commit as 'save', the catch up as 'page'.
    window.fetch = function (url, options) {
        url = String(url); options = options || {};
        var entry = { type: 'fetch', url: url }; trace.push(entry);
        var key = 'page', status = 200, text, answered = location.href;
        var section = /\\/Section\\?section=([a-z-]+)/.exec(url);
        if (section) {
            key = section[1]; entry.section = key; entry.lease = (options.headers || {})['X-Pegasus-Edit-Lease'] || '';
            if (failing[key]) { status = 500; text = ''; } else { text = body(key); }
        } else if (url.indexOf('handler=Save') >= 0) {
            key = 'save'; entry.save = true;
            entry.wording = Array.from(options.body.keys()).some(function (name) { return name.indexOf('wordingEdits[') === 0; });
            // What the commit sent for each block's heading and wording, as the Save reads it.
            entry.blocks = {};
            Array.from(options.body.keys()).forEach(function (name) {
                var field = /^wordingEdits\\[(\\d)\\]\\.(Title|Text)$/.exec(name);
                if (field) { entry.blocks[(field[1] === '0' ? 'nature' : 'settlement') + field[2]] = options.body.get(name); }
            });
            entry.refused = entry.wording && !reportReady;
            if (entry.refused) {
                // R572: nothing in a save carrying wording lands while the report is not ready.
                text = answer(null, '<div class="notice" role="alert"><span>' + wordingRefusal + '</span></div>');
            } else {
                var commit = { editor: 'case-edit-form', operationKey: options.body.get('operationKey'),
                    expectedVersion: Number(options.body.get('expectedVersion')), version: ++state.version };
                state.lease = 'lease-' + state.version; state.reference = options.body.get('reference');
                // Blank is no change: the block keeps its composed sentence and standard heading.
                if (entry.wording) {
                    state.wording = options.body.get('wordingEdits[0].Text') || 'Composed.';
                    state.heading = options.body.get('wordingEdits[0].Title') || 'Nature of Incident';
                }
                text = answer(commit);
            }
            answered = location.origin + '/case?handler=Save';
        } else { text = page(); }
        var response = { ok: status === 200, status: status, redirected: false, url: answered,
            headers: { get: function () { return 'text/html'; } }, text: function () { return Promise.resolve(text); } };
        if (holding[key]) {
            return new Promise(function (resolve) { (held[key] = held[key] || []).push(function () { resolve(response); }); });
        }
        return Promise.resolve(response);
    };
    function release(key) {
        var waiting = held[key] || []; held[key] = []; holding[key] = false;
        waiting.forEach(function (go) { go(); });
        return waiting.length;
    }
    // Every scroll the page's script asks for is counted. drag() moves the page as a scrollbar drag
    // does: at once, with no wheel, pointer or key event, and uncounted.
    var realScrollTo = window.scrollTo;
    ['scrollTo', 'scrollBy'].forEach(function (name) {
        var real = window[name];
        window[name] = function () { trace.push({ type: 'scroll', by: name }); return real.apply(window, arguments); };
    });
    var realIntoView = Element.prototype.scrollIntoView;
    Element.prototype.scrollIntoView = function () { trace.push({ type: 'scroll', by: 'scrollIntoView' }); return realIntoView.apply(this, arguments); };
    function drag(top) { realScrollTo.call(window, { top: top, behavior: 'instant' }); }
    function press(selector) { document.querySelector(selector).click(); }
    function edit(value) { var input = document.getElementById('reference'); input.value = value; input.dispatchEvent(new Event('change', { bubbles: true })); }
    // The Engineer types in the wording block and leaves the panel, which commits it.
    function type(value, id) {
        var text = document.getElementById(id || 'wording-nature'); text.value = value;
        text.dispatchEvent(new Event('input', { bubbles: true }));
        text.dispatchEvent(new FocusEvent('focusout', { bubbles: true, relatedTarget: null }));
    }
    function readingLine() { return document.querySelector('[data-sticky-block]').getBoundingClientRect().bottom; }
    // The section link the reading line stands in, worked out apart from the script under test.
    function under() {
        var line = readingLine() + 16, key = null;
        Array.prototype.forEach.call(document.querySelectorAll('#case-main .record-section'), function (host) {
            if (key === null || host.getBoundingClientRect().top <= line) { key = host.getAttribute('data-section-parent') || host.getAttribute('data-section'); }
        });
        return key;
    }
    function at() {
        var line = readingLine();
        var current = document.querySelector('[data-section-link][aria-current="true"]');
        var marked = document.querySelector('.is-jump-target');
        var box = marked ? marked.getBoundingClientRect() : null;
        var tops = {};
        order.forEach(function (key) { tops[key] = Math.round(document.getElementById('section-' + key).getBoundingClientRect().top - line); });
        return {
            scrollY: Math.round(window.scrollY), max: document.documentElement.scrollHeight - window.innerHeight,
            current: current ? current.getAttribute('data-section-link') : null, under: under(), tops: tops,
            lazy: Array.prototype.map.call(document.querySelectorAll('[data-lazy]'), function (host) { return host.getAttribute('data-lazy'); }),
            loading: document.querySelectorAll('[data-lazy-state="loading"]').length,
            failed: Array.prototype.map.call(document.querySelectorAll('[data-lazy-state="failed"]'), function (host) { return host.getAttribute('data-lazy') + ': ' + host.textContent; }),
            fetched: trace.filter(function (entry) { return entry.section; }).map(function (entry) { return entry.section + (entry.lease ? '@' + entry.lease : ''); }),
            scrolls: trace.filter(function (entry) { return entry.type === 'scroll'; }).map(function (entry) { return entry.by; }),
            marked: marked ? marked.getAttribute('data-field') : null,
            markedCentre: box ? Math.round(box.top + box.height / 2) : null, middle: Math.round((line + window.innerHeight) / 2),
            markedInView: box ? box.top >= line && box.bottom <= window.innerHeight : null,
            version: document.querySelector('[data-case-record]').getAttribute('data-case-version'),
            saves: trace.filter(function (entry) { return entry.save; }).map(function (entry) { return (entry.wording ? 'with wording' : 'without wording') + (entry.refused ? ', refused' : ', landed'); }),
            wording: (document.getElementById('wording-nature') || {}).value, reference: state.reference,
            sent: trace.filter(function (entry) { return entry.save; }).map(function (entry) { return entry.blocks; }),
            status: (function (line) { return line && !line.hidden ? line.textContent.trim() : null; })(document.querySelector('[data-lease-line]'))
        };
    }
    </script><script src="/site.js"></script><script src="/workspace.js"></script><script>window.fixtureReady=true;</script></body></html>`;
}

const server = createServer((req, res) => {
    res.setHeader('Content-Type', req.url.endsWith('.js') ? 'text/javascript' : 'text/html');
    res.end(req.url === '/workspace.js' ? workspaceScript : req.url === '/site.js' ? siteScript : fixture());
});
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
const origin = `http://127.0.0.1:${server.address().port}`;
const proc = spawn(browser, ['--headless=new', '--disable-gpu', '--no-first-run', '--no-default-browser-check', '--no-sandbox',
    `--user-data-dir=${join(output, 'profile')}`, '--remote-debugging-port=0', 'about:blank'], { windowsHide: true, stdio: 'ignore' });
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
let ws, send;
const evidence = [], errors = [];
let failures = 0;
try {
    let port;
    for (let i = 0; i < 300 && !port; i++) {
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
    };
    send = (method, params = {}) => new Promise(resolve => {
        const id = ++sequence; pending.set(id, resolve); ws.send(JSON.stringify({ id, method, params }));
    });
    await send('Page.enable'); await send('Runtime.enable');
    await send('Emulation.setDeviceMetricsOverride', { width: 1280, height: 800, deviceScaleFactor: 1, mobile: false });
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
    const open = async (query = '') => {
        const marker = 'fixture=' + ++navigation;
        await send('Page.navigate', { url: origin + '/case?' + marker + (query ? '&' + query : '') });
        await waitFor(`location.search.indexOf('${marker}') >= 0 && window.fixtureReady === true`);
    };
    // The page at rest: where it stands unchanged across a quarter of a second (a smooth scroll has
    // ended, the scroll spy's own timer has run) and nothing loading beyond the answers a scenario holds.
    const settled = async (holding = 0) => {
        let last = null;
        for (let i = 0; i < 40; i++) {
            await delay(250);
            const now = JSON.stringify(await evaluate('(function () { var a = at(); return [a.scrollY, a.current, a.loading, a.lazy.length]; })()'));
            if (now === last && JSON.parse(now)[2] === holding) { return evaluate('at()'); }
            last = now;
        }
        assert.fail('The page did not come to rest.');
    };
    const mounted = key => waitFor(`!document.getElementById('section-${key}').hasAttribute('data-lazy')`);
    const sorted = list => [...list].sort();
    const landedOn = (state, key) => assert.ok(Math.abs(state.tops[key] - 8) <= 2,
        `${key} stands under the section row (it is ${state.tops[key]}px from it, 8 expected)`);
    const scenario = async (name, run) => {
        const entry = { name };
        try { entry.value = await run(); entry.passed = true; console.log('PASS', name); }
        catch (error) {
            failures++; entry.passed = false; entry.error = error.message;
            try { entry.value = await evaluate('at()'); } catch { /* The page is gone. */ }
            console.log('FAIL', name, '\n     ', error.message.split('\n')[0]);
        }
        evidence.push(entry);
    };

    await scenario('Files from the top lands on Files, stays current, and the mark follows a drag afterwards (1496 T1, T2)', async () => {
        await open();
        await evaluate("press('[data-section-link=\"files\"]')");
        const landed = await settled();
        landedOn(landed, 'files');
        assert.equal(landed.current, 'files');
        assert.deepEqual(sorted(landed.fetched), ['files', 'notes', 'valuation', 'vehicle'], 'Notes is within reach of where Files lands');
        await evaluate('drag(3000)');
        const dragged = await settled();
        assert.equal(dragged.scrollY, 3000, 'The page stays where the drag left it');
        assert.equal(dragged.current, dragged.under);
        assert.notEqual(dragged.current, 'files');
        return { landed, dragged };
    });

    await scenario('A jump reads the sections before its target and none beyond its reach (1496 E1, T3)', async () => {
        await open();
        await evaluate("press('[data-section-link=\"estimate\"]')");
        const landed = await settled();
        landedOn(landed, 'estimate');
        assert.equal(landed.current, 'estimate');
        assert.deepEqual(sorted(landed.fetched), ['valuation', 'vehicle']);
        return landed;
    });

    await scenario('A field near the top of a section behind a slower earlier section (1496 D1, E1)', async () => {
        await open();
        await evaluate("holding.vehicle = true; press('#blocker-retail')");
        await mounted('valuation');
        await delay(800);
        await evaluate("release('vehicle')");
        const landed = await settled();
        assert.deepEqual(sorted(landed.fetched), ['valuation', 'vehicle'], 'Files and Notes are far below and are not read');
        landedOn(landed, 'valuation');
        assert.equal(landed.current, 'vehicle', "Valuation's link is Vehicle's");
        assert.equal(landed.marked, 'retail-trade');
        assert.equal(landed.markedInView, true);
        return landed;
    });

    await scenario('A field jump with nothing to wait for scrolls once and marks the section clicked (1496 E2)', async () => {
        await open();
        await evaluate("press('#blocker-claim')");
        const landed = await settled();
        assert.deepEqual(landed.scrolls, ['scrollTo']);
        landedOn(landed, 'claim');
        assert.equal(landed.current, 'claim');
        assert.equal(landed.marked, 'claim-number');
        assert.equal(landed.markedInView, true);
        assert.deepEqual(sorted(landed.fetched), ['valuation', 'vehicle'], 'Only what is within reach of Claim is read');
        return landed;
    });

    await scenario('A field deep in its section is centred beneath the section row (R888)', async () => {
        await open();
        await evaluate("press('#blocker-incident')");
        const landed = await settled();
        assert.deepEqual(landed.scrolls, ['scrollTo']);
        assert.ok(Math.abs(landed.markedCentre - landed.middle) <= 2, `The field's centre ${landed.markedCentre} is at the middle ${landed.middle}`);
        assert.equal(landed.current, 'claim');
        return landed;
    });

    await scenario('A document too short to land reads what follows and lands once it has grown (1496 clamp)', async () => {
        await open('short=files');
        await evaluate("holding.notes = true; press('[data-section-link=\"files\"]')");
        await mounted('files'); await mounted('vehicle'); await mounted('valuation');
        const clamped = await settled(1);
        assert.ok(clamped.scrollY >= clamped.max - 1, 'The page stands at its bottom');
        assert.ok(clamped.tops.files > 16, 'Files has not reached the section row yet');
        assert.equal(clamped.current, 'files');
        assert.equal(await evaluate('(held.notes || []).length'), 1, 'Notes is being read');
        await evaluate("release('notes')");
        const landed = await settled();
        landedOn(landed, 'files');
        assert.equal(landed.current, 'files');
        return { clamped, landed };
    });

    await scenario('?section= lands on its section (1496 S2)', async () => {
        await open('section=files');
        const landed = await settled();
        landedOn(landed, 'files');
        assert.equal(landed.current, 'files');
        return landed;
    });

    await scenario("Settlement's link to Valuation lands on Valuation under Vehicle's link (1496 S2)", async () => {
        await open();
        await evaluate("press('#to-valuation')");
        const landed = await settled();
        landedOn(landed, 'valuation');
        assert.equal(landed.current, 'vehicle');
        assert.deepEqual(sorted(landed.fetched), ['valuation', 'vehicle']);
        return landed;
    });

    await scenario('A commit that lands before the section is read does not lose the jump (1497 D2)', async () => {
        await open('editing=1');
        await evaluate("holding.save = true; holding.files = true; edit('CL-2'); press('[data-section-link=\"files\"]')");
        await waitFor('(held.save || []).length === 1 && (held.files || []).length === 1');
        await evaluate("release('save')");
        await waitFor("at().version === '2'");
        await delay(400);
        await evaluate("release('files')");
        const landed = await settled();
        assert.deepEqual(landed.lazy.indexOf('files'), -1, 'Files is drawn');
        landedOn(landed, 'files');
        assert.equal(landed.current, 'files');
        assert.equal(landed.current, landed.under, 'The mark and the page agree');
        assert.deepEqual(landed.fetched.filter(entry => entry.startsWith('files')), ['files@lease-1', 'files@lease-2'],
            'Files is read again with the lease the commit left');
        await evaluate('drag(2000)');
        const dragged = await settled();
        assert.equal(dragged.scrollY, 2000, 'Nothing scrolls the page back');
        assert.equal(dragged.current, dragged.under);
        return { landed, dragged };
    });

    await scenario('A resize does not lose a jump still waiting, and lets go a link held at the bottom (1497 T4)', async () => {
        await open();
        await evaluate("holding.files = true; press('[data-section-link=\"files\"]')");
        await mounted('vehicle'); await mounted('valuation');
        await evaluate("window.dispatchEvent(new Event('resize'))");
        await delay(200);
        const waiting = await evaluate('at()');
        assert.equal(waiting.current, 'files', 'The clicked link is held while its section is read');
        await evaluate("release('files')");
        const landed = await settled();
        landedOn(landed, 'files');
        assert.equal(landed.current, 'files');
        // Notes is the last section and short, so the page stands at its bottom with Files under the row.
        await open('short=notes');
        await evaluate("press('[data-section-link=\"notes\"]')");
        const bottom = await settled();
        assert.equal(bottom.current, 'notes', 'The clicked link is held at the bottom of the page');
        assert.notEqual(bottom.under, 'notes');
        await evaluate('drag(at().scrollY - 200)');
        await evaluate("window.dispatchEvent(new Event('resize'))");
        await delay(200);
        const released = await evaluate('at()');
        assert.equal(released.current, released.under);
        return { waiting, landed, bottom, released };
    });

    await scenario('A section that cannot be read ends the jump to it (1497 T4)', async () => {
        await open();
        await evaluate("failing.files = true; press('[data-section-link=\"files\"]')");
        const after = await settled();
        assert.deepEqual(after.failed, ['files: This section could not be loaded.']);
        assert.equal(after.current, after.under);
        return after;
    });

    await scenario('An earlier section that cannot be read does not hold the jump (1497 T4)', async () => {
        await open();
        await evaluate("failing.vehicle = true; press('[data-section-link=\"estimate\"]')");
        const landed = await settled();
        assert.deepEqual(landed.failed, ['vehicle: This section could not be loaded.']);
        landedOn(landed, 'estimate');
        assert.equal(landed.current, 'estimate');
        return landed;
    });

    await scenario('Wording the Case refused is left out of the saves that follow, and stays on screen (1814, R891)', async () => {
        const saved = count => waitFor(`trace.filter(function (entry) { return entry.save; }).length === ${count} && (held.save || []).length === 0`);
        await open('editing=1');
        await evaluate("reportReady = false; type('My own words.')");
        await saved(1); await delay(600);
        const refused = await evaluate('at()');
        assert.deepEqual(refused.saves, ['with wording, refused']);
        assert.equal(refused.status, wordingRefusal, 'The ribbon says why in the sentence its owner holds');
        assert.equal(refused.wording, 'My own words.');
        await evaluate("edit('CL-2')");
        await saved(2); await delay(600);
        const landed = await evaluate('at()');
        assert.deepEqual(landed.saves, ['with wording, refused', 'without wording, landed'], 'The save that follows leaves the refused wording out');
        assert.equal(landed.version, '2');
        assert.equal(landed.reference, 'CL-2');
        assert.equal(landed.wording, 'My own words.', 'The typed wording is still on the page after the catch up');
        await evaluate("type('My own words, changed.')");
        await saved(3); await delay(600);
        const again = await evaluate('at()');
        assert.equal(again.saves[2], 'with wording, refused', 'Wording changed further is offered again');
        await evaluate("reportReady = true; type('My final words.')");
        await saved(4); await delay(600);
        const stored = await evaluate('at()');
        assert.equal(stored.saves[3], 'with wording, landed');
        assert.equal(stored.wording, 'My final words.');
        return { refused, landed, again, stored };
    });

    await scenario('Refused wording is offered again by the page once the report is ready, and once only (2043, R894)', async () => {
        const saved = count => waitFor(`trace.filter(function (entry) { return entry.save; }).length === ${count}`);
        await open('editing=1');
        await evaluate("reportReady = false; type('My own words.')");
        await saved(1); await delay(600);
        // The save that makes the report ready: the catch up after it draws the wording blocks again.
        await evaluate("reportReady = true; edit('CL-2')");
        await saved(3); await delay(800);
        const stored = await evaluate('at()');
        assert.deepEqual(stored.saves, ['with wording, refused', 'without wording, landed', 'with wording, landed']);
        assert.deepEqual(stored.sent[2], { natureTitle: '', natureText: 'My own words.', settlementTitle: '', settlementText: '' },
            'It carries what the Engineer wrote and nothing the page merely still showed');
        assert.equal(stored.wording, 'My own words.');
        assert.match(stored.status, /^Saved/);
        // A page that draws the blocks while the Save still refuses: offered once, then left to the Engineer.
        await open('editing=1');
        await evaluate("reportReady = false; pageDrawsBlocks = true; type('My own words.')");
        await saved(2); await delay(1500);
        const waiting = await evaluate('at()');
        assert.deepEqual(waiting.saves, ['with wording, refused', 'with wording, refused'], 'Refused again, it is not offered a third time');
        assert.equal(waiting.wording, 'My own words.');
        return { stored, waiting };
    });

    await scenario('A block left as the page drew it is sent as no change; what the Engineer wrote is sent (2042)', async () => {
        const saved = count => waitFor(`trace.filter(function (entry) { return entry.save; }).length === ${count}`);
        await open('editing=1');
        await evaluate("type('My own words.')");
        await saved(1); await delay(600);
        let sent = (await evaluate('at()')).sent;
        assert.deepEqual(sent[0], { natureTitle: '', natureText: 'My own words.', settlementTitle: '', settlementText: '' },
            'The untouched heading and the untouched block travel blank; the page still shows them');
        assert.equal(await evaluate("document.getElementById('wording-settlement').value"), 'Composed settlement.');
        await evaluate("type('Outcome', 'heading-settlement')");
        await saved(2); await delay(600);
        sent = (await evaluate('at()')).sent;
        assert.deepEqual(sent[1], { natureTitle: '', natureText: 'My own words.', settlementTitle: 'Outcome', settlementText: '' });
        await evaluate("type('Composed.')");
        await saved(3); await delay(600);
        sent = (await evaluate('at()')).sent;
        assert.equal(sent[2].natureText, '', 'Wording put back to the composed sentence is no change again');
        return sent;
    });

    await scenario('No browser runtime exception', async () => { assert.deepEqual(errors, []); });
} finally {
    await writeFile(join(output, 'evidence.json'), JSON.stringify({ failures, evidence, errors }, null, 2));
    try { ws?.close(); } catch { /* Closed. */ }
    proc.kill();
    server.close();
}
console.log(failures ? `${failures} scenario(s) failed.` : 'Every scenario passed.', 'Evidence:', join(output, 'evidence.json'));
process.exit(failures ? 1 : 0);
