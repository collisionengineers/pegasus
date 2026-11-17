// Browser evidence for the Case page's own script: section jumps across lazy sections (issues 1496 and
// 1497), report wording the Case refused (issues 1814 and 2043), wording blocks left as drawn (issue
// 2042) and what the redraw after a save keeps (issue 2110). Runs the production case-workspace.js against
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
// Valuation while reading; Files and Notes always), the ribbon's Actions menu, the aside's blocker
// links and its scrolling Report not ready list and, while editing, the Save form, a guide card with
// Get valuation and its notices, the valuation calculator's two Other rows and its preview, the Repair
// Spec's More menu, tab strip and line grid with its Delete all dialog, the Report section's tabs and wording panel with two blocks, and an image tile in Files with
// its rotate button and tag picker. The page scrolls smoothly, as site.css has it. ?editing=1 edits,
// ?section= addresses a section, ?short=a,b gives those sections a short body.
function fixture() {
    return `<!doctype html><html><head><meta charset="utf-8"><title>Case page fixture</title>
    <style>
    html{scroll-behavior:smooth}body{margin:0;padding-top:19px;font:13px sans-serif}
    [data-sticky-block]{position:sticky;top:0;height:96px;background:#fff;z-index:1}
    .record-section{box-sizing:border-box;height:1200px;overflow:hidden;border-top:1px solid #999}
    .record-section.is-short{height:300px}.record-section.section-placeholder{height:120px}
    h2{margin:0;height:30px}.cell{height:40px}.cell--top{margin-top:30px}.cell--deep{margin-top:800px}
    [data-case-aside]{position:fixed;right:0;top:120px;width:180px;max-height:300px;overflow-y:auto}[hidden]{display:none}
    .report-not-ready>.panel-body{max-height:100px;overflow-y:auto}.tall{height:400px}
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
    window.state = { version: 1, lease: editing ? 'lease-1' : '', reference: 'CL-1', wording: 'Composed.', heading: 'Nature of Incident',
        // The wording blocks in the order the Case holds them; the Repair Spec's lines, which every save
        // writes afresh with new ids; the image's preparation; the Other rows the calculation records.
        order: ['nature', 'settlement'], lines: [{ id: 'L1', text: 'Bumper' }, { id: 'L2', text: 'Wing' }, { id: 'L3', text: 'Door' }],
        lineSeq: 3, identities: '', prep: { version: 1, rotation: 0 }, others: [],
        // The Cazana card's figures and the calculation the Case records (selection.Opening).
        cazana: { retail: '', trade: '' }, opening: 'calc-1' };
    // What Get valuation and the calculation's preview answer: Cazana cannot value the vehicle or gives
    // figures; the preview cannot be reached, or Core works the calculation out unless it cannot
    // (calculable false), when it refuses and the Save records no calculation.
    window.getAnswer = 'not_valued'; window.previewFails = false; window.calculable = true;
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
        if (key === 'valuation') { inside = '<div class="cell cell--top" data-field="retail-trade">Retail/Trade</div>' + (editing ? calculator() : ''); }
        if (key === 'settlement') { inside = '<a id="to-valuation" href="/case?section=valuation" data-section-jump="valuation">Valuation</a>'; }
        if (key === 'estimate' && editing) { inside = estimate(); }
        if (key === 'files' && editing) { inside = imageTile(); }
        if (key === 'report' && editing) {
            var blocks = {
                nature: function (slot) { return wordingRow(slot, 'nature', 'Nature of Incident', state.heading, 'Composed.', state.wording); },
                settlement: function (slot) { return wordingRow(slot, 'settlement', 'Repair settlement', 'Repair settlement', 'Composed settlement.', 'Composed settlement.'); }
            };
            inside = '<div data-report><div role="tablist" data-report-tabs hidden>'
                + '<button type="button" role="tab" id="tab-report" data-report-tab="report" aria-selected="true">Report</button>'
                + '<button type="button" role="tab" id="tab-fee" data-report-tab="fee" aria-selected="false">Fee</button></div>'
                + '<div data-report-pane="report"></div><div data-report-pane="fee"></div></div>'
                + '<div class="sub-panel wording" data-report-wording data-wording-refusal="' + wordingRefusal + '"><div data-wording-settings></div>'
                + (reportReady || pageDrawsBlocks
                    ? '<div data-wording-list>' + state.order.map(function (block, slot) { return blocks[block](slot); }).join('') + '</div>'
                    : '')
                + '</div>';
        }
        return '<section class="record-section panel' + (shortOnes.indexOf(key) >= 0 ? ' is-short' : '') + '"' + attributes(key)
            + (key === 'estimate' ? ' data-estimate-section' : '') + '>'
            + '<h2>' + key + '</h2>' + inside + '</section>'
            + (key === 'estimate' && editing ? deleteLinesDialog() : '');
    }
    // One wording block as _CaseReport draws it: its controls named and ided by the slot the Case's order
    // gives it, the heading and sentence it was composed with on the row, the heading and the wording as
    // the Case holds them in its controls, and the buttons that move it.
    function wordingRow(slot, key, heading, title, composed, text) {
        var name = 'wordingEdits[' + slot + ']', id = 'f-wording-' + slot;
        return '<div class="wb" data-wording-row data-wording-block="' + key + '" data-wording-composed="' + composed + '" data-wording-heading="' + heading + '">'
            + '<input type="hidden" name="' + name + '.Key" value="' + key + '" form="case-edit-form" data-wording-key>'
            + '<input type="hidden" name="' + name + '.Order" value="' + slot + '" form="case-edit-form" data-wording-order>'
            + '<input id="' + id + '-title" class="fi wbt" name="' + name + '.Title" value="' + title + '" form="case-edit-form">'
            + '<button type="button" data-wording-up>Up</button><button type="button" data-wording-down>Down</button>'
            + '<textarea id="' + id + '-text" name="' + name + '.Text" form="case-edit-form" data-wording-text>' + text + '</textarea></div>';
    }
    // The chosen guide card (_CaseValuation) and the valuation calculator's two Other rows as
    // _CaseValuationCalculation draws them: an Other row the Case records is drawn first, and one still
    // short of its name or amount is drawn blank.
    function calculator() {
        return '<div class="valuation-card entry" data-valuation-entry="cazana" data-valuation-source-card="Cazana" data-valuation-card="cazana"'
            + ' data-retail="' + state.cazana.retail + '" data-trade="' + state.cazana.trade + '">'
            + '<input id="f-valuation-cazana-retail" name="guideEntries[1].RetailValue" form="case-edit-form" value="' + state.cazana.retail + '" data-valuation-retail>'
            + '<input id="f-valuation-cazana-trade" name="guideEntries[1].TradeValue" form="case-edit-form" value="' + state.cazana.trade + '" data-valuation-trade>'
            + '<button type="button" id="get-cazana" data-valuation-get data-valuation-url="/case?handler=GetValuation">Get valuation</button>'
            + '<div class="notice notice--danger" data-valuation-notice hidden><span data-valuation-unavailable>Cazana valuation is unavailable.</span><span data-valuation-refused hidden></span></div>'
            + '<div class="notice notice--info" data-valuation-needs-retail hidden>Enter the retail value on this card to use it.</div></div>'
            + '<input type="hidden" name="selection.Opening" value="' + state.opening + '" form="case-edit-form" data-carry-forward>'
            + '<input type="hidden" name="selection.Use" value="true" form="case-edit-form" disabled data-valuation-use-input>'
            + '<div class="valuation-card entry sel" data-valuation-card="v1" data-valuation-source-card="Glass" data-retail="10000">'
            + '<input type="radio" name="selection.GuideValuationId" value="v1" form="case-edit-form" checked data-valuation-basis data-source-name="Glass\\'s"></div>'
            + '<div class="valuation-calc" data-valuation-calc data-valuation-form="true" data-preview-url="/case?handler=PreviewValuation"'
            + ' data-text-preview-failed="The calculation could not be updated."><div data-valuation-adds>'
            + [0, 1].map(function (at) {
                var recorded = state.others[at];
                return '<div class="add add--custom" data-valuation-add="' + at + '" data-valuation-other>'
                    + '<input type="checkbox" name="selection.AdditionSelected" value="' + at + '" form="case-edit-form" id="f-valuation-add-' + at + '"'
                    + (recorded ? ' checked' : '') + ' data-preset-toggle data-valuation-input>'
                    + '<input type="text" name="selection.AdditionLabel" form="case-edit-form" value="' + (recorded ? recorded.label : '') + '" data-valuation-input>'
                    + '<input class="amt" type="number" name="selection.AdditionAmount" form="case-edit-form" value="' + (recorded ? recorded.amount : '') + '" data-valuation-input></div>';
            }).join('') + '</div><div data-valuation-preview-host></div></div>';
    }
    // The Repair Spec as _CaseEstimate draws it while editing: the identities a commit carries forward,
    // the More menu, the spec tabs, and the grid of lines with the blank line's template and Delete all.
    function estimate() {
        return '<input type="hidden" name="estimateLineIds" value="" form="case-edit-form" disabled data-carry-forward data-estimate-line-ids>'
            + '<details class="menu" data-menu data-estimate-more><summary>More</summary><div class="menu-body"><a href="/case?section=estimate&estimate=new">New repair spec</a></div></details>'
            + '<div class="estimate-tabs" role="tablist" data-estimate-tabs>'
            + '<a class="estimate-tab" role="tab" id="spec-e1" href="/case?section=estimate&estimate=E1" aria-selected="true" data-estimate-tab="E1">Repairer</a>'
            + '<a class="estimate-tab" role="tab" id="spec-e2" href="/case?section=estimate&estimate=E2" aria-selected="false" data-estimate-tab="E2">Engineer</a></div>'
            + '<div class="stack" data-estimate-form data-line-removed-label="Line removed" data-lines-removed-label="lines removed">'
            + '<input type="hidden" name="estimateId" value="E1" form="case-edit-form">'
            + '<template data-estimate-line-template>' + lineRow(null, 0) + '</template>'
            + '<table><tbody data-estimate-grid-body>' + state.lines.map(lineRow).join('') + '</tbody></table>'
            + '<button type="submit" name="clearLines" value="true" form="case-edit-form" data-estimate-delete-all data-undo-label="Undo">Delete all lines</button></div>';
    }
    function lineRow(line, index) {
        return '<tr' + (line ? ' data-estimate-line' : ' class="phantom" data-estimate-phantom') + '><td>'
            + '<input type="hidden" name="lineId" value="' + (line ? line.id : '') + '" form="case-edit-form">'
            + '<select name="lineOperation" form="case-edit-form"><option value="Replace"' + (line ? ' selected' : '') + '>Replace</option><option value="Repair">Repair</option></select></td>'
            + '<td><input name="lineDescription" form="case-edit-form" value="' + (line ? line.text : '') + '"></td>'
            + '<td><button type="submit" class="del" name="removeLine" value="' + (line ? index : '') + '" form="case-edit-form" aria-label="Remove this line">×</button></td></tr>';
    }
    function deleteLinesDialog() {
        return '<div id="delete-lines-dialog" class="dialog-backdrop" data-dialog="delete-lines-dialog" hidden><section class="dialog" role="alertdialog" aria-modal="true">'
            + '<p><span data-delete-lines-count></span> lines</p><button type="button" data-dialog-close>Cancel</button>'
            + '<button type="button" data-delete-lines-confirm data-dialog-close>Delete all lines</button></section></div>';
    }
    // One image tile in Files as _CaseImages draws it: the preparation the Case holds, the rotate button
    // and the tag picker (_CaseImageTagPicker) with its New tag box.
    function imageTile() {
        return '<div data-image-tile="occ1" data-preparation-card data-preparation-occurrence="occ1" data-preparation-version="' + state.prep.version + '"'
            + ' data-preparation-rotation="' + state.prep.rotation + '" data-preparation-in-report="false">'
            + '<button type="button" data-preparation-rotate="90">Rotate</button>'
            + '<details class="tag-picker" data-tag-picker data-menu><summary>Tag</summary><div class="tag-picker-body">'
            + '<form method="post" class="tag-picker-new" action="/case?handler=CreateTag" data-document-action>'
            + '<input type="hidden" name="editLeaseToken" value="' + state.lease + '">'
            + '<label for="tag-name-occ1">Name</label><input id="tag-name-occ1" name="name" placeholder="New tag"><button type="submit">Create</button></form>'
            + '</div></details></div>';
    }
    function placeholder(key) {
        return '<section class="record-section panel section-placeholder"' + attributes(key) + ' data-lazy="' + key + '"></section>';
    }
    // The ribbon's two halves: the Actions menu holds an action that posts in place.
    function ribbon() {
        return '<div data-case-ribbon-facts></div>'
            + '<div data-case-ribbon-actions><span data-lease-line hidden data-lease-saving-text="Saving" data-lease-saved-text="Saved"></span>'
            + '<details class="menu" data-menu data-case-actions><summary>Actions</summary><div class="menu-body">'
            + '<form method="post" action="/case?handler=Hold"><button type="submit" data-hold>Place on Hold</button></form></div></details></div>';
    }
    function aside() {
        return '<div data-case-aside>'
            + '<a id="blocker-claim" href="/case?section=claim" data-section-jump="claim" data-section-field="claim-number">Claim number</a> '
            + '<a id="blocker-incident" href="/case?section=claim" data-section-jump="claim" data-section-field="incident-date">Incident date</a> '
            + '<a id="blocker-retail" href="/case?section=valuation" data-section-jump="valuation" data-section-field="retail-trade">Retail/Trade</a>'
            + '<section class="panel report-not-ready" data-report-not-ready><div class="panel-body"><div class="tall">Outstanding</div></div></section>'
            + '<div class="tall"></div></div>';
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
            + aside() + '</article><div data-case-viewer-host></div><div data-case-dialogs></div><div class="toast-region" data-toast-region></div>';
    }
    // What the server answers a commit with (FRD-16, _CaseCommitResult): the wrapper that confirms it,
    // the swap roots, the Save form and the Repair Spec's line identities the next commit carries
    // forward, and Files only when the commit recorded a rotation.
    function answer(commit, notice, files) {
        return '<div data-case-notices>' + (notice || '') + '</div>' + record(commit) + ribbon() + saveForm()
            + (editing ? '<input type="hidden" name="estimateLineIds" value="' + state.identities + '" form="case-edit-form" disabled data-carry-forward data-estimate-line-ids>' : '')
            + aside() + '</article>' + (files ? body('files') : '') + '<div data-case-dialogs></div>';
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
            // What the commit sent for each block's heading and wording, as the Save reads it, by the
            // key posted beside them.
            var posted = options.body, slots = {};
            entry.blocks = {};
            Array.from(posted.keys()).forEach(function (name) {
                var field = /^wordingEdits\\[(\\d)\\]\\.(Title|Text)$/.exec(name);
                if (field) {
                    var block = posted.get('wordingEdits[' + field[1] + '].Key');
                    slots[block] = field[1];
                    entry.blocks[block + field[2]] = posted.get(name);
                }
            });
            // The Repair Spec's lines as posted, a rotation staged on the image, the Other rows.
            var lineIds = posted.getAll('lineId'), lineTexts = posted.getAll('lineDescription');
            var known = state.lines.map(function (line) { return line.id; });
            var rotation = posted.get('preparationEdits[0].Rotation');
            if (rotation !== null) { entry.rotation = Number(rotation); }
            var refusal = entry.wording && !reportReady
                // R572: nothing in a save carrying wording lands while the report is not ready.
                ? wordingRefusal
                // Estimates.ApplyEditorEvidence: a line the save names that the Case no longer has.
                : lineIds.some(function (id) { return id && known.indexOf(id) < 0; }) ? 'An estimate line changed before this edit was saved.'
                : rotation !== null && Number(posted.get('preparationEdits[0].ExpectedPreparationVersion')) !== state.prep.version ? 'The image changed before this edit was saved.'
                : null;
            entry.refused = !!refusal;
            if (entry.refused) {
                text = answer(null, '<div class="notice" role="alert"><span>' + refusal + '</span></div>');
            } else {
                var commit = { editor: 'case-edit-form', operationKey: posted.get('operationKey'),
                    expectedVersion: Number(posted.get('expectedVersion')), version: ++state.version };
                state.lease = 'lease-' + state.version; state.reference = posted.get('reference');
                // Blank is no change: the block keeps its composed sentence and standard heading. The
                // blocks take the order the commit gives them.
                if (entry.wording) {
                    state.wording = posted.get('wordingEdits[' + slots.nature + '].Text') || 'Composed.';
                    state.heading = posted.get('wordingEdits[' + slots.nature + '].Title') || 'Nature of Incident';
                    state.order.sort(function (a, b) {
                        return Number(posted.get('wordingEdits[' + slots[a] + '].Order')) - Number(posted.get('wordingEdits[' + slots[b] + '].Order'));
                    });
                }
                // Every save writes the lines afresh with new ids, and answers each line's id by the row
                // it was posted from. A blank line is dropped.
                if (posted.has('estimateId')) {
                    var identities = [];
                    state.lines = [];
                    lineTexts.forEach(function (lineText, row) {
                        if (!lineText.trim()) { return; }
                        var id = 'L' + ++state.lineSeq;
                        state.lines.push({ id: id, text: lineText });
                        identities.push(row + ':' + id);
                    });
                    state.identities = identities.join(' ');
                }
                if (rotation !== null) { state.prep.rotation = Number(rotation); state.prep.version++; }
                // An Other row is recorded once it is ticked, named and above zero (R934); a calculation
                // that changed is recorded when Core can work it out.
                if (posted.has('selection.AdditionLabel')) {
                    var ticked = posted.getAll('selection.AdditionSelected'), amounts = posted.getAll('selection.AdditionAmount');
                    var others = posted.getAll('selection.AdditionLabel').map(function (label, at) {
                        return ticked.indexOf(String(at)) >= 0 && label.trim() && Number(amounts[at]) > 0 ? { label: label, amount: amounts[at] } : null;
                    }).filter(Boolean);
                    if (JSON.stringify(others) !== JSON.stringify(state.others) && calculable) { state.opening = 'calc-' + state.version; }
                    state.others = others;
                }
                // A guide card records what was entered on it; a blank card records nothing.
                if (posted.get('guideEntries[1].RetailValue') || posted.get('guideEntries[1].TradeValue')) {
                    state.cazana = { retail: posted.get('guideEntries[1].RetailValue'), trade: posted.get('guideEntries[1].TradeValue') };
                }
                text = answer(commit, null, rotation !== null);
            }
            answered = location.origin + '/case?handler=Save';
        } else if (url.indexOf('handler=GetValuation') >= 0) {
            key = 'get';
            text = JSON.stringify(getAnswer === 'ok' ? { status: 'ok', retail: 9000, trade: 8000 }
                : { status: 'not_valued', message: 'Cazana cannot value this vehicle: it holds no data for its registration.' });
        } else if (url.indexOf('handler=PreviewValuation') >= 0) {
            key = 'preview';
            if (previewFails) { status = 500; text = ''; }
            else if (!calculable) { text = '<div class="notice notice--danger" role="alert" data-valuation-error>The deductions exceed the value.</div>'; }
            else { text = '<span hidden data-valuation-proposal="9100.00" data-valuation-vat-amount="" data-valuation-ptl-amount=""></span>'; }
        } else { text = page(); }
        var response = { ok: status === 200, status: status, redirected: false, url: answered,
            headers: { get: function () { return 'text/html'; } }, text: function () { return Promise.resolve(text); },
            json: function () { return Promise.resolve(JSON.parse(text)); } };
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
    // A wording block's heading or wording, found by its block: its ids follow the slot it is drawn in.
    function wordingControl(key, part) {
        return document.querySelector('[data-wording-block="' + key + '"] ' + (part === 'title' ? 'input.wbt' : '[data-wording-text]'));
    }
    // The Engineer types in the wording block and leaves the panel, which commits it.
    function type(value, key, part) {
        var text = wordingControl(key || 'nature', part); text.value = value;
        text.dispatchEvent(new Event('input', { bubbles: true }));
        text.dispatchEvent(new FocusEvent('focusout', { bubbles: true, relatedTarget: null }));
    }
    // A part of the page is marked as drawn; drawn() is true once a redraw has replaced it.
    function mark(selector) { document.querySelector(selector).setAttribute('data-drawn-before', ''); }
    function drawn(selector) { var part = document.querySelector(selector); return !!part && !part.hasAttribute('data-drawn-before'); }
    // A key pressed on the control that has focus.
    function pressKey(name) { document.activeElement.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true })); }
    function lines() {
        return Array.prototype.map.call(document.querySelectorAll('[data-estimate-grid-body] tr[data-estimate-line]'), function (row) {
            return row.querySelector('input[name="lineDescription"]').value;
        });
    }
    function saved() { return trace.filter(function (entry) { return entry.save; }); }
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
            wording: (wordingControl('nature') || {}).value, reference: state.reference,
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
    // A scenario may note what it saw as it goes (seen), which the evidence keeps whether it passed or not.
    const scenario = async (name, run) => {
        const entry = { name }, seen = {};
        try { entry.value = await run(seen); entry.passed = true; console.log('PASS', name); }
        catch (error) {
            failures++; entry.passed = false; entry.error = error.message;
            try { entry.value = await evaluate('at()'); } catch { /* The page is gone. */ }
            console.log('FAIL', name, '\n     ', error.message.split('\n')[0]);
        }
        if (Object.keys(seen).length) { entry.seen = seen; }
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
        assert.equal(await evaluate("wordingControl('settlement').value"), 'Composed settlement.');
        await evaluate("type('Outcome', 'settlement', 'title')");
        await saved(2); await delay(600);
        sent = (await evaluate('at()')).sent;
        assert.deepEqual(sent[1], { natureTitle: '', natureText: 'My own words.', settlementTitle: 'Outcome', settlementText: '' });
        await evaluate("type('Composed.')");
        await saved(3); await delay(600);
        sent = (await evaluate('at()')).sent;
        assert.equal(sent[2].natureText, '', 'Wording put back to the composed sentence is no change again');
        return sent;
    });

    // Issue 2110: what the redraw after a save keeps. Each scenario notes what it saw and asserts at the
    // end, so a run against the scripts before the fix records every part of it.
    const stored = "state.lines.map(function (line) { return line.text; })";
    const removeWing = "press('[data-estimate-grid-body] tr[data-estimate-line]:nth-child(2) button[name=\"removeLine\"]')";

    await scenario('Undo puts back an Estimate line, or every line, whose removal has saved (2110 item 1)', async seen => {
        // The removal saved and the catch up drew the Repair Spec afresh.
        await open('editing=1');
        await evaluate(`mark('#section-estimate'); ${removeWing}`);
        await waitFor("saved().length === 1 && drawn('#section-estimate')");
        seen.removed = await evaluate(stored);
        await evaluate("press('.toast--undo button')");
        await delay(2500);
        seen.afterRedraw = await evaluate(`({ page: lines(), stored: ${stored}, saves: at().saves })`);
        // The removal saved, and the catch up is still on its way: the rows on the page took new ids.
        await open('editing=1');
        await evaluate(`holding.page = true; ${removeWing}`);
        await waitFor("saved().length === 1 && (held.page || []).length === 1");
        await evaluate("press('.toast--undo button')");
        await delay(300);
        await evaluate("release('page')");
        await delay(2500);
        seen.beforeRedraw = await evaluate(`({ page: lines(), stored: ${stored}, saves: at().saves })`);
        // Delete all saved and drawn afresh.
        await open('editing=1');
        await evaluate("mark('#section-estimate'); press('[data-estimate-delete-all]')");
        await waitFor("!document.getElementById('delete-lines-dialog').hidden");
        await evaluate("press('[data-delete-lines-confirm]')");
        await waitFor("saved().length === 1 && drawn('#section-estimate')");
        seen.deleted = await evaluate(stored);
        await evaluate("press('.toast--undo button')");
        await delay(2500);
        seen.afterDeleteAll = await evaluate(`({ page: lines(), stored: ${stored}, saves: at().saves })`);

        const every = ['Bumper', 'Wing', 'Door'], twice = ['without wording, landed', 'without wording, landed'];
        assert.deepEqual(seen.removed, ['Bumper', 'Door']);
        assert.deepEqual(seen.afterRedraw.page, every, 'The line is back in its place in the Repair Spec on the page');
        assert.deepEqual(seen.afterRedraw.saves, twice, 'Putting it back is saved');
        assert.deepEqual(seen.afterRedraw.stored, every);
        assert.deepEqual(seen.beforeRedraw.saves, twice, 'The line put back after its removal saved is not refused as a line the Case no longer has');
        assert.deepEqual(seen.beforeRedraw.stored, every);
        assert.deepEqual(seen.beforeRedraw.page, every);
        assert.deepEqual(seen.deleted, []);
        assert.deepEqual(seen.afterDeleteAll.page, every, 'Every line is back after Delete all saved');
        assert.deepEqual(seen.afterDeleteAll.saves, twice);
        assert.deepEqual(seen.afterDeleteAll.stored, every);
        return seen;
    });

    await scenario('A rotation staged across a commit is saved, and Files drawn afresh keeps the open tag picker and its name (2110 item 2)', async seen => {
        const rotations = "({ stored: state.prep.rotation, sent: saved().map(function (entry) { return entry.rotation === undefined ? null : entry.rotation; }), saves: at().saves })";
        // Staged while a commit that carried no rotation is in flight.
        await open('editing=1&section=files');
        await evaluate("holding.save = true; edit('CL-2')");
        await waitFor('(held.save || []).length === 1');
        await evaluate("press('[data-preparation-rotate]'); release('save')");
        await delay(2500);
        seen.besideOther = await evaluate(rotations);
        // Turned again while the commit carrying the first turn is in flight.
        await open('editing=1&section=files');
        await evaluate("holding.save = true; press('[data-preparation-rotate]')");
        await waitFor('(held.save || []).length === 1');
        await evaluate("press('[data-preparation-rotate]'); release('save')");
        await delay(2500);
        seen.turnedAgain = await evaluate(rotations);
        // The tag picker open, a new tag's name typed in it, as the commit carrying a rotation lands.
        await open('editing=1&section=files');
        await evaluate("holding.save = true; press('[data-preparation-rotate]')");
        await waitFor('(held.save || []).length === 1');
        await evaluate("mark('#section-files'); document.querySelector('[data-tag-picker]').open = true;"
            + " var box = document.getElementById('tag-name-occ1'); box.focus(); box.value = 'Rear'; box.dispatchEvent(new Event('input', { bubbles: true })); box.setSelectionRange(2, 2)");
        await evaluate("release('save')");
        await waitFor("drawn('#section-files')");
        await delay(800);
        seen.picker = await evaluate("({ open: document.querySelector('[data-tag-picker]').open, name: document.getElementById('tag-name-occ1').value,"
            + " focused: document.activeElement.id, caret: document.activeElement.selectionStart, stored: state.prep.rotation })");

        assert.equal(seen.besideOther.stored, 90, 'The turn staged while another commit was in flight is saved');
        assert.deepEqual(seen.besideOther.saves, ['without wording, landed', 'without wording, landed']);
        assert.equal(seen.turnedAgain.stored, 180, 'The second turn, staged while the first was in flight, is saved');
        assert.deepEqual(seen.turnedAgain.sent, [90, 180]);
        assert.deepEqual(seen.turnedAgain.saves, ['without wording, landed', 'without wording, landed']);
        assert.equal(seen.picker.stored, 90);
        assert.equal(seen.picker.open, true, 'The tag picker stands open in the Files drawn afresh');
        assert.equal(seen.picker.name, 'Rear', 'The name typed in it stays');
        assert.equal(seen.picker.focused, 'tag-name-occ1', 'The cursor stays in it');
        assert.equal(seen.picker.caret, 2);
        return seen;
    });

    await scenario('A wording block moved and then clicked into keeps the cursor after the redraw (2110 item 3)', async seen => {
        await open('editing=1');
        await evaluate("mark('#section-report'); press('[data-wording-block=\"settlement\"] [data-wording-up]');"
            + " var text = wordingControl('settlement'); text.focus(); text.setSelectionRange(4, 4)");
        await waitFor("saved().length === 1 && drawn('#section-report')");
        await delay(300);
        Object.assign(seen, await evaluate("(function () { var row = document.activeElement.closest('[data-wording-block]'); return {"
            + " drawn: Array.prototype.map.call(document.querySelectorAll('[data-wording-block]'), function (block) { return block.getAttribute('data-wording-block'); }),"
            + " stored: state.order.slice(), block: row ? row.getAttribute('data-wording-block') : null, id: document.activeElement.id,"
            + " wording: document.activeElement.hasAttribute('data-wording-text'), caret: document.activeElement.selectionStart }; })()"));
        assert.deepEqual(seen.stored, ['settlement', 'nature']);
        assert.deepEqual(seen.drawn, ['settlement', 'nature'], 'The Report is drawn in the order the move saved');
        assert.equal(seen.block, 'settlement', 'The cursor is in the block the Engineer clicked into');
        assert.equal(seen.wording, true);
        assert.equal(seen.caret, 4);
        return seen;
    });

    await scenario("Text typed into the second Other row returns to the second Other row after the redraw (2110 item 3)", async seen => {
        await open('editing=1');
        await evaluate("mark('#section-valuation'); press('#f-valuation-add-1');"
            + " var box = document.querySelector('[data-valuation-add=\"1\"] input[type=\"text\"]'); box.value = 'Tow bar'; box.dispatchEvent(new Event('input', { bubbles: true }))");
        await waitFor("saved().length === 1 && drawn('#section-valuation')");
        await delay(300);
        seen.rows = await evaluate("Array.prototype.map.call(document.querySelectorAll('[data-valuation-other]'), function (row) {"
            + " return { ticked: row.querySelector('[data-preset-toggle]').checked, name: row.querySelector('input[type=\"text\"]').value }; })");
        seen.stored = await evaluate('state.others.length');
        assert.equal(seen.stored, 0, 'A row short of its amount is recorded nowhere');
        assert.deepEqual(seen.rows, [{ ticked: false, name: '' }, { ticked: true, name: 'Tow bar' }], 'Other 1 stays blank and Other 2 holds what was typed in it');
        return seen;
    });

    await scenario("A guide card's notices stand across a save's redraw until the card is acted on again or the Case supersedes them (2110 item 4, R1125)", async seen => {
        const card = '[data-valuation-entry="cazana"]';
        const says = "({ notice: (function (n) { return n.hidden ? null : (n.querySelector('[data-valuation-refused]').hidden ? '' : n.querySelector('[data-valuation-refused]').textContent); })(document.querySelector('" + card + " [data-valuation-notice]')),"
            + " needs: !document.querySelector('" + card + " [data-valuation-needs-retail]').hidden,"
            + " preview: (function (e) { return e ? e.textContent.trim() : null; })(document.querySelector('[data-valuation-preview-host] [data-valuation-error]')) })";
        const redrawnAfter = async (action, count) => {
            await evaluate(`mark('#section-valuation'); ${action}`);
            await waitFor(`saved().length === ${count} && drawn('#section-valuation') && !document.querySelector('[data-valuation-get][data-busy]')`);
            await delay(400);
            return evaluate(says);
        };
        // Cazana cannot value the vehicle, and a click on its card with no retail: both stand across an
        // unrelated save, and go once system work fills the card.
        await open('editing=1');
        await evaluate(`press('#get-cazana')`);
        await waitFor("!document.querySelector('[data-valuation-get][data-busy]')");
        await evaluate(`press('${card}')`);
        seen.shown = await evaluate(says);
        seen.unrelated = await redrawnAfter("edit('CL-2')", 1);
        seen.filled = await redrawnAfter("state.cazana = { retail: '9500', trade: '8500' }; edit('CL-3')", 2);
        // Get valuation pressed again: the notice goes, and the card's figures keep it gone.
        await open('editing=1');
        await evaluate(`press('#get-cazana')`);
        await waitFor("!document.querySelector('[data-valuation-get][data-busy]')");
        await evaluate(`getAnswer = 'ok'; press('#get-cazana')`);
        await waitFor("saved().length === 1 && !document.querySelector('[data-valuation-get][data-busy]')");
        await delay(600);
        seen.actedOn = await evaluate(says);
        // Core refuses the calculation and the Save records none: the refusal stands across the save that
        // carried the change and an unrelated one; worked out at last, it goes.
        await open('editing=1');
        seen.refused = await redrawnAfter("calculable = false; press('#f-valuation-add-0'); var row = document.querySelector('[data-valuation-add=\"0\"]');"
            + " var label = row.querySelector('input[type=\"text\"]'); label.value = 'Tow bar'; label.dispatchEvent(new Event('input', { bubbles: true }));"
            + " var amount = row.querySelector('input[type=\"number\"]'); amount.value = '100'; amount.dispatchEvent(new Event('input', { bubbles: true }));", 1);
        seen.refusedUnrelated = await redrawnAfter("edit('CL-2')", 2);
        seen.workedOut = await redrawnAfter("calculable = true; var again = document.querySelector('[data-valuation-add=\"0\"] input[type=\"number\"]');"
            + " again.value = '200'; again.dispatchEvent(new Event('input', { bubbles: true }));", 3);
        // The calculation could not be refreshed: the failure stands while the Case records nothing, and goes
        // once a save records the calculation.
        await open('editing=1');
        seen.failed = await redrawnAfter("previewFails = true; calculable = false; press('#f-valuation-add-0'); var row = document.querySelector('[data-valuation-add=\"0\"]');"
            + " var label = row.querySelector('input[type=\"text\"]'); label.value = 'Tow bar'; label.dispatchEvent(new Event('input', { bubbles: true }));"
            + " var amount = row.querySelector('input[type=\"number\"]'); amount.value = '100'; amount.dispatchEvent(new Event('input', { bubbles: true }));", 1);
        seen.recorded = await redrawnAfter("calculable = true; var again = document.querySelector('[data-valuation-add=\"0\"] input[type=\"number\"]');"
            + " again.value = '200'; again.dispatchEvent(new Event('input', { bubbles: true }));", 2);
        seen.opening = await evaluate('state.opening');

        const cannot = 'Cazana cannot value this vehicle: it holds no data for its registration.';
        assert.deepEqual(seen.shown, { notice: cannot, needs: true, preview: null });
        assert.deepEqual(seen.unrelated, { notice: cannot, needs: true, preview: null }, 'Both stand across an unrelated save');
        assert.deepEqual(seen.filled, { notice: null, needs: false, preview: null }, 'Both go once the card drawn afresh holds figures');
        assert.deepEqual(seen.actedOn, { notice: null, needs: false, preview: null }, 'Get valuation pressed again takes the notice away');
        const refusal = 'The deductions exceed the value.', failure = 'The calculation could not be updated.';
        assert.equal(seen.refused.preview, refusal, 'The refusal stands across the save that recorded no calculation');
        assert.equal(seen.refusedUnrelated.preview, refusal, 'and across an unrelated save');
        assert.equal(seen.workedOut.preview, null, 'A calculation worked out takes it away');
        assert.equal(seen.failed.preview, failure, 'The failure stands while no calculation is recorded');
        assert.equal(seen.recorded.preview, null, 'A save that records the calculation takes it away');
        assert.notEqual(seen.opening, 'calc-1');
        return seen;
    });

    await scenario('Open menus stay open and the aside keeps its scroll across a save; an action closes its own menu (2110 item 5)', async seen => {
        await open('editing=1');
        // The ribbon's Actions menu and the aside, drawn afresh by the save's answer and its catch up.
        await evaluate("mark('[data-case-ribbon-actions]'); mark('[data-case-aside]'); document.querySelector('[data-case-actions]').open = true;"
            + " document.querySelector('[data-case-aside]').scrollTop = 150; document.querySelector('[data-report-not-ready] > .panel-body').scrollTop = 60; edit('CL-2')");
        await waitFor("saved().length === 1 && drawn('[data-case-ribbon-actions]') && drawn('[data-case-aside]')");
        await delay(800);
        seen.ribbon = await evaluate("({ actions: document.querySelector('[data-case-actions]').open, aside: document.querySelector('[data-case-aside]').scrollTop,"
            + " list: document.querySelector('[data-report-not-ready] > .panel-body').scrollTop })");
        // The Repair Spec's More menu, in a section the catch up draws afresh.
        await evaluate("mark('#section-estimate'); document.querySelector('[data-estimate-more]').open = true");
        await delay(100);
        await evaluate("edit('CL-3')");
        await waitFor("saved().length === 2 && drawn('#section-estimate')");
        await delay(300);
        seen.section = await evaluate("({ more: document.querySelector('[data-estimate-more]').open, actions: document.querySelector('[data-case-actions]').open })");
        // An action posted from the menu: the whole record drawn afresh, the menu closed.
        await evaluate("document.querySelector('[data-case-actions]').open = true");
        await delay(100);
        await evaluate("mark('[data-case-ribbon-actions]'); press('[data-hold]')");
        await waitFor("drawn('[data-case-ribbon-actions]')");
        await delay(300);
        seen.action = await evaluate("({ actions: document.querySelector('[data-case-actions]').open })");

        assert.equal(seen.ribbon.actions, true, 'The Actions menu stands open after the save');
        assert.equal(seen.ribbon.aside, 150, 'The aside keeps its scroll');
        assert.equal(seen.ribbon.list, 60, 'The Report not ready list keeps its scroll');
        assert.equal(seen.section.more, true, "The Repair Spec's More menu stands open after its section is drawn afresh");
        assert.equal(seen.section.actions, false, 'One menu stands open at a time');
        assert.equal(seen.action.actions, false, 'The menu whose action ran is closed');
        return seen;
    });

    await scenario('Arrow keys move along a tab strip drawn again by a redraw, and one tab along the Report tabs (2110 item 6)', async seen => {
        await open('editing=1');
        await evaluate("document.getElementById('tab-report').focus(); pressKey('ArrowRight')");
        seen.report = await evaluate("({ focused: document.activeElement.id, fee: !document.querySelector('[data-report-pane=\"fee\"]').hidden })");
        await evaluate("document.getElementById('spec-e1').focus(); pressKey('ArrowRight')");
        seen.asDrawn = await evaluate('document.activeElement.id');
        await evaluate("mark('#section-estimate'); edit('CL-2')");
        await waitFor("saved().length === 1 && drawn('#section-estimate')");
        await delay(300);
        await evaluate("document.getElementById('spec-e1').focus(); pressKey('ArrowRight')");
        seen.redrawn = await evaluate("({ focused: document.activeElement.id, tabindex: [document.getElementById('spec-e1').getAttribute('tabindex'), document.getElementById('spec-e2').getAttribute('tabindex')] })");

        assert.equal(seen.report.focused, 'tab-fee', 'ArrowRight on Report moves to Fee, once');
        assert.equal(seen.report.fee, true);
        assert.equal(seen.asDrawn, 'spec-e2', "The Repair Spec's tabs move as the page drew them");
        assert.equal(seen.redrawn.focused, 'spec-e2', "The Repair Spec's tabs drawn afresh move on the arrow keys");
        assert.deepEqual(seen.redrawn.tabindex, ['-1', '0'], 'Only the focused tab is in the Tab order');
        return seen;
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
