/* ================================================= v27 proposals ---- */
/* The reference file's "nothing in the way" differences (v27-notes § 14),
   each a switch on the strip (`p=` / `proposals=all|none`). Every one is a
   proposal: none of this is live behaviour. */
var PROPOSALS = [
  ['composed', 'Composed sentences'], ['cap', 'CAP guide'], ['salvage', 'Salvage slider'], ['bank', 'Reason bank'],
  ['estdel', 'Delete all + Undo'], ['offpattern', 'Off-pattern cells'], ['uplift', 'Regional uplift'], ['prov', 'Provenance chips'],
  ['compare', 'Compare diff + print'], ['supp', 'Supplementary'], ['abook', 'Address book'], ['attach', 'Attachments'],
  ['resend', 'Re-send naming'], ['feetab', 'Fee tab'], ['badges', 'Ribbon badges'], ['nine', 'Nine sections'],
  ['place', 'Reference placements'], ['include', 'Click to include'], ['queries', 'Queries panel'],
  ['ticks', 'Decision tick rows'], ['signoff', 'Sign-off follows Engineer'], ['reportdate', 'Report date on generate']
];
function allProposals(on) { var o = {}; PROPOSALS.forEach(function (p) { o[p[0]] = on; }); return o; }
S.p = allProposals(true);
var INITIAL_P = null;
S.repairer = 'liverpool';
OPTIONS.repairer = [['liverpool', 'Bootle repairer'], ['london', 'Croydon repairer']];
DEFAULTS.repairer = 'liverpool';

/* ---- the fixture lines the estimate proposals read ---- */
var LINES_E1 = [
  ['Replace', 'Bumper rear (primed)', 1, 286.40, 0, 0], ['Replace', 'Tailgate', 1, 512.30, 0, 0], ['Replace', 'Lamp assembly rear O/S', 1, 148.75, 0, 0], ['Replace', 'Impact absorber rear', 1, 96.20, 0, 0],
  ['Repair', 'Rear panel — repair', 0, 0, 3.5, 0], ['Repair', 'Floor pan rear — repair', 0, 0, 1.2, 0], ['R&I', 'Rear bumper — R&I', 0, 0, 0.8, 0], ['R&I', 'Tailgate trim — R&I', 0, 0, 0.5, 0],
  ['Paint', 'Paint — tailgate (new)', 0, 0, 0, 2.1], ['Paint', 'Paint — bumper rear (new)', 0, 0, 0, 1.5], ['Paint', 'Paint — rear panel (repair)', 0, 0, 0, 1.8], ['Blend', 'Blend — quarter panel O/S', 0, 0, 0, 0.9],
  ['Specialist', 'Four-wheel alignment check', 1, 65, 0, 0], ['Specialist', 'Diagnostic scan & fault report', 1, 85, 0, 0]
];
var LINES_E2 = [['Replace', 'Bumper rear (primed)', 1, 286.40, 0, 0], ['Repair', 'Rear panel — repair', 0, 95, 3.0, 0], ['Paint', 'Paint — bumper rear (new)', 0, 0, 0, 1.5]];
var RATE = 83.28;
function lineValue(l) { return (l[2] || 1) * l[3] + (l[4] + l[5]) * RATE; }
function diffLines(A, B) {
  var key = function (l) { return l[1].toLowerCase().replace(/[^a-z0-9/]+/g, ' ').trim(); };
  var ma = {}, mb = {}; A.forEach(function (l) { ma[key(l)] = l; }); B.forEach(function (l) { mb[key(l)] = l; });
  var added = [], removed = [], changed = [], same = [];
  B.forEach(function (b) { var a = ma[key(b)]; if (!a) { added.push(b); return; } var f = [2, 3, 4, 5].filter(function (k) { return (a[k] || 0) !== (b[k] || 0); }); if (f.length) changed.push([a, b, f]); else same.push([a, b]); });
  A.forEach(function (a) { if (!mb[key(a)]) removed.push(a); });
  return { added: added, removed: removed, changed: changed, same: same };
}
var SUPP_REASONS = [['estimate', 'Supplementary estimate received', 'Following receipt of a supplementary estimate'], ['dismantle', 'Further damage found on dismantling', 'Following dismantling of the vehicle, further damage was identified and'], ['inspect', 'Further inspection', 'Following a further inspection of the vehicle'], ['images', 'Further images received', 'Following receipt of further images']];
UI.suppPrint = false; UI.suppReason = 'estimate'; UI.suppCompare = ''; UI.sentCount = 0; UI.cc = ['rhs-claims@outlook.com']; UI.attach = { report: true, fee: true, breakdown: false, images: false }; UI.reportPane = 'report'; UI.uplift = false;
function suppSentence() {
  var d = diffLines(LINES_E1, LINES_E2), reason = SUPP_REASONS.filter(function (r) { return r[0] === UI.suppReason; })[0][2];
  var t = reason;
  if (d.added.length) t += ' the following additional items are now required: ' + d.added.map(function (l) { return l[1]; }).join('; ') + '.'; else t += ' the repair specification has been revised.';
  var hrs = d.changed.filter(function (c) { return c[2].indexOf(4) >= 0 || c[2].indexOf(5) >= 0; });
  if (hrs.length) t += ' The repair time for ' + hrs.map(function (c) { return c[1][1] + ' (' + (c[0][4] + c[0][5]) + ' h → ' + (c[1][4] + c[1][5]) + ' h)'; }).join('; ') + ' has been revised.';
  if (d.removed.length) t += ' ' + d.removed.map(function (l) { return l[1]; }).join('; ') + (d.removed.length > 1 ? ' are' : ' is') + ' no longer required.';
  t += ' The estimated repair cost has ' + (1047.02 >= 3004.79 ? 'increased' : 'reduced') + ' from £3,004.79 to £1,047.02.';
  return t;
}

/* ---- composed sentences: every one reads the recorded fields ---- */
var MILEAGE_SENTENCE = { lookup: 'The mileage has been calculated from online data.', owner: 'The mileage was advised by the owner.', repairer: 'The mileage was advised by the repairer/storage yard.', principal: 'The mileage was advised by our instructing principal.' };
function composeAll() {
  var setText = function (key, text) { $$('[data-composed="' + key + '"]').forEach(function (el) { el.textContent = text; }); };
  var claimant = F.editing ? $('f-claimant').value : $('f-claimant').parentElement.querySelector('.fv').textContent;
  var when = $('f-incident-date').value; var d = when ? new Date(when + 'T00:00:00') : null;
  setText('matter', 'Road Traffic Accident: ' + claimant.trim() + (d ? ': ' + d.toLocaleDateString('en-GB', { day: 'numeric', month: 'long', year: 'numeric' }) : ''));
  var choice = $('inspection-address-choice'); var addr = document.querySelector('[data-inspection-address-input]').value;
  setText('located', 'Vehicle located at: ' + (choice.value === 'ImageBasedAssessment' ? 'Image Based Assessment' : addr || '—') + '.');
  var rate = parseFloat($('edit-storage-per-day').value) || 0, rec = parseFloat($('edit-recovery-charge').value) || 0;
  setText('charges', rate && rec ? 'We understand recovery and storage charges are accruing at ' + money(rec) + ' and ' + money(rate) + 'pd, plus VAT.' : rec ? 'We understand recovery charges of ' + money(rec) + ' have been incurred, plus VAT.' : rate ? 'We understand storage charges are accruing at ' + money(rate) + 'pd, plus VAT.' : 'No charges recorded — no line appears on the report.');
  var src = UI.mileageTouched ? $('edit-mileage-source').value : 'lookup';
  setText('mileage', MILEAGE_SENTENCE[src] || MILEAGE_SENTENCE.lookup);
  var cond = $('edit-vehicle-condition'); var condWord = (cond.selectedOptions[0] ? cond.selectedOptions[0].textContent : 'Average').toLowerCase();
  setText('condition', 'The vehicle is considered to be in ' + condWord + ' condition for its age and type.');
  var vat = $('estimate-vat-status').value;
  setText('vat', vat === 'Registered' ? 'Repairer VAT registered — VAT at 20 % applies to every category.' : vat === 'NotRegistered' ? 'Repairer not VAT registered — VAT applies to parts and materials only.' : 'No repairer VAT status recorded — Use as Current is blocked until one is.');
  var disclose = document.querySelector('[data-report-switch="report.disclose_guide_source"]');
  var basis = document.querySelector('[data-valuation-basis]:checked');
  var name = basis ? basis.getAttribute('data-source-name') : "Glass's", retail = basis ? parseFloat(basis.getAttribute('data-retail')) : 2950, trade = basis ? parseFloat(basis.getAttribute('data-trade') || '0') : 2180;
  setText('carries', (disclose && disclose.checked ? name : 'Source not disclosed') + ' — Retail ' + money(retail) + (trade ? ' · Trade ' + money(trade) : '') + " · Engineer's Value " + (F.empty ? '—' : money(EV)) + '.');
  setText('supp', suppSentence());
}

/* ---- decision tick rows: the select stays the control, the buttons drive it ---- */
function renderTickRows() {
  $$('[data-tickrow]').forEach(function (row) {
    var sel = $(row.getAttribute('data-tickrow'));
    row.innerHTML = Array.prototype.filter.call(sel.options, function (o) { return o.value; }).map(function (o) { return '<button type="button" class="tick" aria-pressed="' + (sel.value === o.value) + '" data-tick="' + sel.id + '" data-value="' + o.value + '"><span class="bx"></span>' + o.textContent + '</button>'; }).join('');
  });
}
/* ---- salvage slider, reason bank ---- */
var SAL_SNAPS = [5, 10, 15, 20, 25];
function salvageRefresh(fromSlider) {
  var input = $('f-assessment-salvage-value'), range = $('salvRange'), snaps = $('salvSnaps'), read = $('salvRead');
  if (!range) return;
  var val = fromSlider ? Math.round(EV * range.value / 100) : (parseFloat(input.value) || 0);
  if (fromSlider) input.value = val.toFixed(2);
  var pct = EV > 0 ? val / EV * 100 : 0;
  range.value = Math.max(0, Math.min(100, Math.round(pct)));
  read.textContent = '= ' + (+pct.toFixed(1)) + '% of ' + money(EV);
  snaps.innerHTML = SAL_SNAPS.map(function (p) { return '<button type="button" class="' + (Math.abs(pct - p) < 0.5 ? 'on' : '') + '" data-salv-snap="' + p + '">' + p + '%</button>'; }).join('');
}
var UR_BANK = ['the steering and suspension geometry has been compromised', 'the rear lamp assemblies are inoperative', 'the headlamp assemblies are inoperative', 'the vehicle presents sharp edges likely to cause injury', 'the supplementary restraint systems have deployed', 'structural distortion is evident to the body shell', 'there is a loss of essential fluids'];
function renderBank() { var b = $('urBank'); if (b) b.innerHTML = UR_BANK.map(function (r, i) { return '<button type="button" data-bank="' + i + '">' + r + '</button>'; }).join(''); }

/* ---- estimate: off-pattern cells, provenance, uplift, delete-all, undo ---- */
function markOffPattern() {
  $$('#estEditBody tr[data-estimate-line]').forEach(function (tr) {
    var op = tr.querySelector('select').value, unit = tr.querySelector('[name="linePartPounds"]'), hours = tr.querySelector('[name="lineLabourHours"]'), paint = tr.querySelector('[name="linePaintHours"]');
    var flag = function (input, on, why) { input.classList.toggle('viol', !!(S.p.offpattern && on)); input.title = S.p.offpattern && on ? why : ''; };
    flag(unit, /Repair|R&I|Paint|Blend/.test(op) && parseFloat(unit.value) > 0, 'Outside the usual pattern for ' + op + ' — imported as received. Zero it or retype the row to normalise.');
    flag(hours, /Paint|Blend/.test(op) && parseFloat(hours.value) > 0, 'Paint and blend lines carry paint hours, not panel hours.');
    flag(paint, /Replace|Repair|R&I|Specialist/.test(op) && parseFloat(paint.value) > 0, 'Paint hours on a ' + op + ' line — imported as received.');
  });
}
var HC_FULL = 'SS RM DA BR CR SM KT TW SL UB HA WD AL EN IG LU HP GU RH BN TN ME CT E EC N NW SE SW W WC'.split(' ');
var HC_DIST = { SG: [1, 2, 3, 4, 5, 9, 10, 11, 12, 13, 14], OX: [1, 3, 4, 5, 9, 10, 11, 14, 25, 39, 44, 49], RG: [1, 2, 4, 5, 6, 7, 8, 9, 10, 12, 18, 21, 22, 23, 24, 25, 27, 29, 30, 31, 40, 41, 42, 45], CM: [0, 9] };
function outward(text) { var m = (text || '').toUpperCase().match(/\b([A-Z]{1,2})(\d{1,2})[A-Z]?\s*\d[A-Z]{2}\b/); return m ? { area: m[1], dist: parseInt(m[2], 10), raw: m[1] + m[2] } : null; }
function inHomeCounties(o) { return !!o && (HC_FULL.indexOf(o.area) >= 0 || (HC_DIST[o.area] && HC_DIST[o.area].indexOf(o.dist) >= 0)); }
function upliftRefresh() {
  var chip = $('upliftChip'), box = $('est-uplift'), rate = $('estimate-labour-rate'); if (!chip) return;
  var srcs = [['Repairer', $('repairer-address').value], ['Claimant', $('f-claimant-address').value], ['Storage', $('storage-location').value]];
  var hits = srcs.map(function (s) { return [s[0], outward(s[1])]; }).filter(function (s) { return inHomeCounties(s[1]); });
  chip.className = 'src-tag' + (hits.length ? ' src-tag--warn' : ''); chip.textContent = hits.length ? 'Suggested · ' + hits.map(function (h) { return h[0] + ' (' + h[1].raw + ')'; }).join(', ') : 'London & Home Counties';
  box.checked = UI.uplift; rate.value = (UI.uplift ? RATE * 1.15 : RATE).toFixed(2);
  $$('[data-estimate-body="e2"] .est-head-read span')[1].innerHTML = 'Labour <b>' + money(UI.uplift ? RATE * 1.15 : RATE) + '/h</b> · ABP 2026 standard' + (UI.uplift ? ' + 15 % regional uplift' : '');
}
function applyRepairerFixture() {
  var addr = S.repairer === 'london' ? 'Unit 2, Purley Way, Croydon CR0 4XE' : 'Unit 4, Brasenose Rd, Bootle L20 8HL';
  var cell = $('repairer-address'); cell.value = addr; cell.parentElement.querySelector('.fv').textContent = addr;
  var opt = document.querySelector('#inspection-address-choice option[value="RepairerLocation"]'); if (opt) opt.setAttribute('data-address', addr);
}
var undoRow = null;

/* ---- compare diff, address book, attachments, re-send naming ---- */
var ABOOK = [['QDOS Claims — claims inbox', 'claims@qdosclaims.co.uk', 'Principal', false], ['J Simmonds — QDOS handler', 'j.simmonds@qdosclaims.co.uk', 'Principal', true], ['QDOS accounts', 'accounts@qdosclaims.co.uk', 'Principal', false], ['Rapid Hire Solutions — claim source', 'claims@rapidhire.co.uk', 'This case', true], ['A Patterson', 'ap@collisionengineers.co.uk', 'CE', false], ['CE admin', 'admin@collisionengineers.co.uk', 'CE', false]];
function renderCompareDiff() {
  var host = $('cmpDiff'); if (!host) return;
  var from = $('cmpFrom').value, to = $('cmpTo').value;
  var chosen = from && to && from !== to;
  document.querySelector('[data-cmp-print]').hidden = !chosen;
  if (!chosen) { $('cmpSum').textContent = ''; host.innerHTML = ''; return; }
  var L = { e1: LINES_E1, e2: LINES_E2 }, names = { e1: 'Audatex 1', e2: 'Manual 2' }, gross = { e1: 3004.79, e2: 1047.02 };
  var A = L[from], B = L[to], d = diffLines(A, B), delta = gross[to] - gross[from];
  $('cmpSum').innerHTML = names[from] + ' <b>' + money(gross[from]) + '</b> → ' + names[to] + ' <b>' + money(gross[to]) + '</b> · <b class="' + (delta > 0 ? 'red' : '') + '">' + (delta >= 0 ? '+' : '−') + money(Math.abs(delta)) + '</b> · ' + d.added.length + ' added · ' + d.changed.length + ' changed · ' + d.removed.length + ' removed';
  var cell = function (l, k, other) { if (!l) return '<td class="num mono">—</td>'; var v = l[k]; var show = v ? (k === 3 ? v.toFixed(2) : String(v)) : '—'; var dif = other && (other[k] || 0) !== (v || 0); return '<td class="num mono' + (dif ? ' diff' : '') + '">' + show + '</td>'; };
  var row = function (cls, tag, a, b) { var l = b || a; return '<tr class="' + cls + '"><td>' + l[0] + (a && b && a[0] !== b[0] ? ' <small class="muted">(was ' + a[0] + ')</small>' : '') + '</td><td>' + l[1] + (tag ? ' <span class="src-tag src-tag--' + tag[0] + '">' + tag[1] + '</span>' : '') + '</td>' + cell(a, 2) + cell(a, 3) + cell(a, 4) + cell(a, 5) + '<td class="num mono">' + (a ? money(lineValue(a)) : '—') + '</td>' + cell(b, 2, a) + cell(b, 3, a) + cell(b, 4, a) + cell(b, 5, a) + '<td class="num mono">' + (b ? money(lineValue(b)) : '—') + '</td></tr>'; };
  host.innerHTML = '<table class="table table--compact cmp-diff"><thead><tr><th>Type</th><th>Description</th><th class="num" colspan="5">' + names[from] + '</th><th class="num" colspan="5">' + names[to] + '</th></tr><tr><th></th><th></th><th class="num">Qty</th><th class="num">Unit £</th><th class="num">Hours</th><th class="num">Paint h</th><th class="num">Line £</th><th class="num">Qty</th><th class="num">Unit £</th><th class="num">Hours</th><th class="num">Paint h</th><th class="num">Line £</th></tr></thead><tbody>'
    + d.added.map(function (l) { return row('added', ['ai', 'added'], null, l); }).join('') + d.changed.map(function (c) { return row('changed', ['warn', 'changed'], c[0], c[1]); }).join('') + d.removed.map(function (l) { return row('removed', ['lookup', 'removed'], l, null); }).join('') + d.same.map(function (c) { return row('', null, c[0], c[1]); }).join('') + '</tbody></table>';
}
function renderSupp() {
  var host = $('suppPanel'); if (!host) return;
  var d = diffLines(LINES_E1, LINES_E2);
  var li = function (l, tag, extra) { return '<li><span class="src-tag src-tag--' + tag[0] + '">' + tag[1] + '</span>' + l[1] + (extra || '') + '<span class="num mono">' + money(lineValue(l)) + '</span></li>'; };
  $('suppList').innerHTML = d.added.map(function (l) { return li(l, ['ai', 'added']); }).join('') + d.changed.map(function (c) { return li(c[1], ['warn', 'changed'], ' <span class="muted">— ' + c[2].map(function (k) { return ({ 2: 'qty', 3: 'unit', 4: 'hours', 5: 'paint h' })[k] + ' ' + (c[0][k] || 0) + ' → ' + (c[1][k] || 0); }).join(', ') + '</span>'); }).join('') + d.removed.map(function (l) { return li(l, ['lookup', 'removed']); }).join('');
  $('suppDelta').innerHTML = money(3004.79) + ' → <b>' + money(1047.02) + '</b> (<b class="red">−' + money(3004.79 - 1047.02) + '</b>)';
  $('suppPrint').checked = UI.suppPrint; $('suppReason').value = UI.suppReason; $('suppCompare').value = UI.suppCompare;
  var chosen = !!UI.suppCompare;
  $$('[data-supp-when-chosen]').forEach(function (el) { el.hidden = !chosen; });
  $('suppDelta').hidden = !chosen;
  $$('[data-supp-when-print]').forEach(function (el) { el.hidden = !(chosen && UI.suppPrint); });
}
function abookRender(which) {
  var input = $(which === 'to' ? 'abTo' : 'abCc'), box = $(which === 'to' ? 'abToList' : 'abCcList');
  var q = (input.value || '').toLowerCase();
  var rows = ABOOK.filter(function (a) { return !q || a[0].toLowerCase().indexOf(q) >= 0 || a[1].toLowerCase().indexOf(q) >= 0; });
  box.innerHTML = rows.length ? rows.map(function (a) { return '<div class="abr" data-ab-pick="' + which + '" data-address="' + a[1] + '"><span class="an">' + a[0] + '</span><span class="ae">' + a[1] + '</span><span class="at">' + a[2] + '</span></div>'; }).join('') : '<div class="abempty">No matches — press Enter to add the typed address.</div>';
  box.classList.add('open');
}
function renderCc() {
  var line = $('ccLine'), sugg = $('ccSugg'); if (!line) return;
  line.innerHTML = UI.cc.map(function (e) { return '<span class="ccchip">' + e + '<button type="button" data-cc-drop="' + e + '" aria-label="Remove ' + e + '">×</button></span>'; }).join('');
  sugg.innerHTML = ABOOK.filter(function (a) { return a[3] && UI.cc.indexOf(a[1]) < 0; }).map(function (a) { return '<button type="button" data-cc-add="' + a[1] + '">+ ' + a[0] + '</button>'; }).join('');
}
function reportFileName() { return 'QDOS26214 MA59BDY Total loss report' + '.'.repeat(UI.sentCount) + '.pdf'; }
function renderResend() {
  $$('[data-composed="fname"]').forEach(function (el) { el.textContent = reportFileName(); });
  $$('[data-composed="body"]').forEach(function (el) { el.textContent = UI.sentCount === 0 ? 'Please find attached our Total loss report in respect of MA59BDY.' : 'Please find attached our updated Total loss report in respect of MA59BDY, which supersedes our report dated ' + UI.lastSentDate + '.'; });
  $$('[data-attach]').forEach(function (cb) { cb.checked = !!UI.attach[cb.getAttribute('data-attach')]; });
}

/* ---- the nine-section map and the reference placements: DOM moves ---- */
var moved = [];
function moveNode(node, target, before) { moved.push({ node: node, parent: node.parentNode, next: node.nextSibling }); if (before) target.insertBefore(node, before); else target.appendChild(node); }
function restoreMoves() { while (moved.length) { var m = moved.pop(); if (m.next && m.next.parentNode === m.parent) m.parent.insertBefore(m.node, m.next); else m.parent.appendChild(m.node); } var claim = $('section-claim'); if (claim) claim.remove(); }
var mapApplied = { nine: false, place: false };
function applyMap() {
  if (mapApplied.nine === S.p.nine && mapApplied.place === S.p.place) return;
  restoreMoves(); mapApplied = { nine: S.p.nine, place: S.p.place };
  if (S.p.nine) {
    var overview = $('section-overview'), main = $('case-main');
    var claim = document.createElement('section'); claim.className = 'record-section panel'; claim.id = 'section-claim'; claim.setAttribute('data-section', 'claim'); claim.setAttribute('data-collapse', 'case.claim');
    claim.innerHTML = '<div class="panel-head"><h2 id="section-claim-title">Claim</h2><div class="panel-actions"><span data-head-tools="claim" style="display:contents"></span></div></div><div class="panel-body stack"></div>';
    main.insertBefore(claim, overview.nextSibling); if (observer) observer.observe(claim);
    var body = claim.querySelector('.panel-body');
    moveNode(overview.querySelector('.overview-grid > .sub-panel:nth-child(3)'), body);
    moveNode(overview.querySelector('[data-notes-band]').previousElementSibling, body); // Case contact
    moveNode(overview.querySelector('[data-accident-band]'), body);
    $('section-overview-title').textContent = 'Case details';
    var vbody = $('section-vehicle').querySelector('.panel-body');
    moveNode($('section-damage'), vbody); moveNode($('section-valuation'), vbody);
    $('section-settlement-title').textContent = 'Decisions';
    $('section-files-title').textContent = 'Images';
    var tabs = $('section-files').querySelector('[data-file-tabs]'); tabs.insertBefore(tabs.querySelector('[data-file-tab="images"]'), tabs.firstElementChild);
    UI.fileTab = 'images';
  } else { $('section-overview-title').textContent = 'Overview'; $('section-settlement-title').textContent = 'Settlement'; $('section-files-title').textContent = 'Files'; var t = $('section-files').querySelector('[data-file-tabs]'); t.insertBefore(t.querySelector('[data-file-tab="documents"]'), t.firstElementChild); }
  if (S.p.place) {
    var caseGrid = $('section-overview').querySelector('.overview-grid > .sub-panel:first-child .fg');
    moveNode(document.querySelector('[data-field="signOffEngineerId"]'), caseGrid);
    var vbody2 = $('section-vehicle').querySelector('.panel-body');
    var unrel = document.querySelector('[data-unrelated-band]'); moveNode(unrel, vbody2);
    var valBody = $('section-valuation').querySelector('.panel-body');
    moveNode(document.querySelector('[data-field="report-content"]'), valBody);
  }
  renderSectionNav();
}
function activeSections() {
  if (!S.p.nine) return SECTIONS;
  return ['overview', 'claim', 'inspection', 'vehicle', 'estimate', 'settlement', 'report', 'files', 'notes'];
}
var NAV_ICONS = { overview: 'icon-layout-dashboard', claim: 'icon-file-text', inspection: 'icon-map-pin', vehicle: 'icon-car', damage: 'icon-alert-triangle', valuation: 'icon-file-text', estimate: 'icon-list', settlement: 'icon-check-circle', report: 'icon-file', files: 'icon-folder', notes: 'icon-history' };
function sectionLabel(k) { if (S.p.nine) { if (k === 'overview') return 'Case details'; if (k === 'claim') return 'Claim'; if (k === 'settlement') return 'Decisions'; if (k === 'files') return 'Images'; } return SECTION_LABELS[k]; }
function renderSectionNav() {
  var nav = document.querySelector('[data-section-nav]');
  nav.innerHTML = activeSections().map(function (k) { return '<a class="section-link" href="#section-' + k + '" data-section-link="' + k + '" aria-current="' + (k === UI.section ? 'true' : 'false') + '"><svg class="icon" aria-hidden="true"><use href="#' + NAV_ICONS[k] + '" /></svg><span>' + sectionLabel(k) + '</span></a>'; }).join('');
  var sel = $('mockSection'); if (sel) { sel.innerHTML = ''; activeSections().forEach(function (k) { var o = document.createElement('option'); o.value = k; o.textContent = sectionLabel(k); sel.appendChild(o); }); sel.value = UI.section; }
  $$('.record-section').forEach(function (s) { if (s.parentNode === $('case-main')) s.classList.toggle('is-active', s.getAttribute('data-section') === UI.section); });
  if (activeSections().indexOf(UI.section) < 0) UI.section = 'overview';
}

/* ---- ribbon badges, fee tab, include, queries ---- */
function applyProposals() {
  applyMap();
  document.body.setAttribute('data-proposals', PROPOSALS.filter(function (p) { return S.p[p[0]]; }).map(function (p) { return p[0]; }).join(' '));
  var claim = $('section-claim'); if (claim) { claim.classList.toggle('is-locked', F.editing && !F.editCase); claim.classList.toggle('is-active', UI.section === 'claim'); var host = claim.querySelector('[data-head-tools="claim"]'); if (host) host.innerHTML = $('section-overview').querySelector('[data-head-tools="overview"]').innerHTML.replace(/overview/g, 'claim'); }
  /* ribbon badges */
  var ratio = $('badgeRatio'); if (ratio) { var pct = Math.round(REPAIR / EV * 100); ratio.className = 'status status--plain ' + (pct >= 80 ? 'status--red' : pct >= 66 ? 'status--amber' : 'status--green'); ratio.textContent = F.empty ? 'Repairs —' : 'Repairs ' + pct + '% of value · ' + money(REPAIR) + ' / ' + money(EV); }
  /* provenance chips */
  $$('[data-estimate-body="e1"] .src-tag[data-base]').forEach(function (t) { t.textContent = S.p.prov ? t.getAttribute('data-base') + ' · AX' : t.getAttribute('data-base'); });
  /* fee tab */
  var rb = $('section-report').querySelector('.panel-body');
  Array.prototype.forEach.call(rb.children, function (child) { if (child.hasAttribute('data-report-tabs')) return; if (child.hasAttribute('data-report-pane')) { child.hidden = !(S.p.feetab && UI.reportPane === 'fee'); return; } child.classList.toggle('pane-hidden', S.p.feetab && UI.reportPane === 'fee'); });
  $$('[data-report-tab]').forEach(function (b) { b.setAttribute('aria-selected', b.getAttribute('data-report-tab') === UI.reportPane ? 'true' : 'false'); });
  applyRepairerFixture();
  renderTickRows();
  if (S.p.composed) composeAll();
  if (S.p.salvage) salvageRefresh(false);
  if (S.p.bank) renderBank();
  markOffPattern(); upliftRefresh(); renderSupp(); renderCc(); renderResend(); renderCompareDiff(); renderWording();
}
function renderProposalStrip() {
  var host = document.querySelector('[data-strip-proposals]'); if (!host) return;
  host.innerHTML = PROPOSALS.map(function (p) { return '<button type="button" data-p="' + p[0] + '" class="' + (S.p[p[0]] ? 'on' : '') + '">' + p[1] + '</button>'; }).join('') + '<span class="sep"></span><button type="button" data-p-all="1">All on</button><button type="button" data-p-all="0">All off</button>';
}
document.addEventListener('click', function (e) {
  var t = e.target, b;
  if ((b = t.closest('[data-p]'))) { S.p[b.getAttribute('data-p')] = !S.p[b.getAttribute('data-p')]; apply(); return; }
  if ((b = t.closest('[data-tick]'))) { var sel = $(b.getAttribute('data-tick')); sel.value = b.getAttribute('data-value'); sel.dispatchEvent(new Event('change', { bubbles: true })); renderTickRows(); UI.dirty = true; return; }
  if ((b = t.closest('[data-p-all]'))) { S.p = allProposals(b.getAttribute('data-p-all') === '1'); apply(); return; }
  if ((b = t.closest('[data-salv-snap]'))) { $('salvRange').value = b.getAttribute('data-salv-snap'); salvageRefresh(true); UI.dirty = true; return; }
  if ((b = t.closest('[data-bank]'))) { var ta = $('f-assessment-unroadworthy-reason'); var cur = ta.value.trim().replace(/\.+$/, ''); var phrase = UR_BANK[parseInt(b.getAttribute('data-bank'), 10)]; ta.value = cur ? cur + ' and ' + phrase + '.' : phrase.charAt(0).toUpperCase() + phrase.slice(1) + '.'; UI.dirty = true; return; }
  if ((b = t.closest('[data-bank-save]'))) { var text = $('f-assessment-unroadworthy-reason').value.trim().replace(/\.+$/, ''); if (text && UR_BANK.indexOf(text) < 0) { UR_BANK.push(text); renderBank(); toast('Saved to the bank — everyone sees it.'); } return; }
  if ((b = t.closest('[data-delete-lines]'))) { $$('#estEditBody tr[data-estimate-line]').forEach(function (tr) { tr.remove(); }); closeDialog(); UI.dirty = true; toast('All lines deleted — the outgoing grid is kept under Compare.'); return; }
  if ((b = t.closest('[data-undo-row]'))) { if (undoRow) { undoRow.parent.insertBefore(undoRow.node, undoRow.next); undoRow = null; } b.closest('.toast').remove(); return; }
  if ((b = t.closest('[data-ab-pick]'))) { var which = b.getAttribute('data-ab-pick'); if (which === 'to') $('abTo').value = b.getAttribute('data-address'); else if (UI.cc.indexOf(b.getAttribute('data-address')) < 0) { UI.cc.push(b.getAttribute('data-address')); $('abCc').value = ''; } $$('.ab.open').forEach(function (x) { x.classList.remove('open'); }); renderCc(); return; }
  if ((b = t.closest('[data-cc-add]'))) { UI.cc.push(b.getAttribute('data-cc-add')); renderCc(); return; }
  if ((b = t.closest('[data-cc-drop]'))) { UI.cc = UI.cc.filter(function (x) { return x !== b.getAttribute('data-cc-drop'); }); renderCc(); return; }
  if ((b = t.closest('[data-report-tab]'))) { UI.reportPane = b.getAttribute('data-report-tab'); apply(); return; }
  if ((b = t.closest('[data-cmp-print]'))) { window.print(); return; }
  if (!t.closest('.addrwrap')) $$('.ab.open').forEach(function (x) { x.classList.remove('open'); });
}, true);
document.addEventListener('input', function (e) {
  var t = e.target;
  if (t.id === 'salvRange') { salvageRefresh(true); UI.dirty = true; }
  if (t.id === 'f-assessment-salvage-value') salvageRefresh(false);
  if (t.id === 'abTo') abookRender('to'); if (t.id === 'abCc') abookRender('cc');
  if (S.p.composed && t.closest('#case-record')) composeAll();
  if (t.closest('#estEditBody')) markOffPattern();
  if (['repairer-address', 'f-claimant-address', 'storage-location'].indexOf(t.id) >= 0) upliftRefresh();
});
document.addEventListener('focusin', function (e) { if (e.target.id === 'abTo') abookRender('to'); if (e.target.id === 'abCc') abookRender('cc'); });
document.addEventListener('keydown', function (e) { if (e.key === 'Enter' && e.target.id === 'abCc') { e.preventDefault(); var v = e.target.value.trim(); if (v && UI.cc.indexOf(v) < 0) UI.cc.push(v); e.target.value = ''; $$('.ab.open').forEach(function (x) { x.classList.remove('open'); }); renderCc(); } });
document.addEventListener('change', function (e) {
  var t = e.target;
  if (t.id === 'edit-mileage-source') UI.mileageTouched = true;
  if (t.id === 'est-uplift') { UI.uplift = t.checked; upliftRefresh(); UI.dirty = true; }
  if (t.id === 'suppCompare') { UI.suppCompare = t.value; if (!t.value) UI.suppPrint = false; renderSupp(); if (S.p.composed) composeAll(); if (S.p.wording) renderWording(); }
  if (t.id === 'suppPrint') { UI.suppPrint = t.checked; renderSupp(); if (S.p.composed) composeAll(); }
  if (t.id === 'suppReason') { UI.suppReason = t.value; renderSupp(); if (S.p.composed) composeAll(); }
  if (t.matches('[data-attach]')) { UI.attach[t.getAttribute('data-attach')] = t.checked; }
  if (t.id === 'cmpFrom' || t.id === 'cmpTo') renderCompareDiff();
  if (t.closest('#estEditBody')) markOffPattern();
  if (S.p.composed && t.closest('#case-record')) composeAll();
});
