/* v27 baseline mockup of the live Case record. Mock behaviour only: every
   rule below restates a condition in Details.cshtml, Details.Frame.cs or a
   _Case*.cshtml partial on origin/dev 5765a527a; nothing here is product
   code. Posted forms never leave the page — a toast names the live handler
   the form would have reached. */
(function () {
'use strict';
var $ = function (id) { return document.getElementById(id); };
var $$ = function (sel, root) { return Array.prototype.slice.call((root || document).querySelectorAll(sel)); };

/* ------------------------------------------------------------ state ---- */
var OPTIONS = {
  state: [['not-ready', 'Not ready'], ['review', 'Review'], ['with-engineer', 'With Engineer'], ['post-report', 'With Engineer · post report'], ['held', 'Held'], ['completed', 'Completed'], ['query', 'Query'], ['closed', 'Closed · Provider cancelled'], ['created-in-error', 'Closed · Created in error']],
  edit: [['off', 'Reading'], ['on', 'Editing'], ['colleague', 'Colleague editing']],
  role: [['engineer', 'Engineer'], ['administrator', 'Administrator'], ['user', 'User']],
  layout: [['scroll', 'Scroll'], ['tabs', 'Tabs']],
  rail: [['expanded', 'Expanded'], ['collapsed', 'Collapsed']],
  data: [['present', 'Present'], ['empty', 'Empty'], ['conflict', 'Stale save refused'], ['unavailable', 'Case unavailable']],
  kind: [['inspection', 'Inspection'], ['inspection-audit', 'Inspection + Audit'], ['audit', 'Audit']],
  archived: [['0', 'Open'], ['1', 'Archived']],
  glass: [['none', 'None'], ['open', 'Open'], ['waiting', 'Waiting'], ['failed', 'Failed'], ['elsewhere', 'Held elsewhere']],
  proposal: [['0', 'None'], ['1', 'AI proposal']],
  report: [['none', 'Not generated'], ['confirmed', 'Generated'], ['stale', 'Stale'], ['prepared', 'Delivery prepared']],
  ai: [['none', 'None'], ['estimate', 'Estimate draft ready']],
  custody: [['confirmed', 'Confirmed'], ['pending', 'Preparing'], ['failed', 'Unavailable']],
  lookup: [['current', 'Looked up'], ['never', 'Not yet looked up'], ['failed', 'Lookup failed']],
  eva: [['zip', 'EVA ZIP'], ['api', 'Manual API'], ['api-off', 'API not enabled'], ['api-failed', 'Automatic failed']],
  clicker: [['zones', 'Live · panels'], ['pins', 'A · Pins'], ['brush', 'B · Brush'], ['area', 'C · Areas'], ['arrow', 'D · Impact arrows']],
  logo: [['refined', 'Refined mark'], ['live', 'Live lockup']]
};
var DEFAULTS = { state: 'with-engineer', edit: 'off', role: 'engineer', layout: 'scroll', rail: 'expanded', data: 'present', kind: 'inspection', archived: '0', glass: 'none', proposal: '0', report: 'confirmed', ai: 'none', custody: 'confirmed', lookup: 'current', eva: 'zip', clicker: 'zones', logo: 'refined' };
var S = Object.assign({}, DEFAULTS);
var F = {};
var T = {};
var UI = { estimateTab: 'e1', fileTab: 'documents', section: 'overview', expanded: false, auditCreated: false, dirty: false };

var ENGINEER_KEYS = { damage: 1, valuation: 1, estimate: 1, settlement: 1, report: 1 };
var SECTIONS = ['overview', 'inspection', 'vehicle', 'damage', 'valuation', 'estimate', 'settlement', 'report', 'files', 'notes'];
var SECTION_LABELS = { overview: 'Overview', inspection: 'Inspection details', vehicle: 'Vehicle', damage: 'Damage', valuation: 'Valuation', estimate: 'Estimate', settlement: 'Settlement', report: 'Report', files: 'Files', notes: 'Notes' };

/* ---- the frame's rules, one line per Razor/C# condition ---- */
function computeFlags() {
  var st = S.state;
  var f = {};
  f.state = st;
  f.role = S.role; f.admin = S.role === 'administrator'; f.eng = S.role !== 'user';
  f.kind = S.kind; f.glass = S.glass; f.report = S.report; f.custody = S.custody; f.lookup = S.lookup; f.ai = S.ai;
  f.proposal = S.proposal === '1'; f.empty = S.data === 'empty'; f.conflict = S.data === 'conflict'; f.unavailable = S.data === 'unavailable'; f.archived = S.archived === '1'; f.eva = S.eva; f.clicker = S.clicker; f.p = S.p;
  var cv = document.getElementById('f-settlement-claimant-vat-registered'); f.claimantVat = !!cv && cv.value === 'true';
  f.auditExists = UI.auditCreated;
  f.isClosed = st === 'closed' || st === 'created-in-error';
  f.isHeld = st === 'held'; f.isReview = st === 'review'; f.isComplete = st === 'completed'; f.isQuery = st === 'query';
  f.notReady = st === 'not-ready'; f.withEng = st === 'with-engineer' || st === 'post-report'; f.postReport = st === 'post-report';
  f.postReadOnly = f.isComplete || f.isQuery;                                  // IsPostReportReadOnly
  f.editing = S.edit === 'on' && !f.archived;                                   // IsEditing
  f.colleague = S.edit === 'colleague';                                         // ColleagueIsEditing
  f.editCase = f.editing && !f.postReadOnly;                                    // CanEditCaseData
  f.editEng = f.editCase && f.withEng && f.eng;                                 // CanEditEngineering
  f.canHand = f.editCase && f.isReview;
  f.canEva = f.isReview || f.withEng;                                           // EvaZip policy for the fixture principal
  f.hasGen = S.report !== 'none'; f.stale = S.report === 'stale'; f.prepared = S.report === 'prepared';
  f.sentEvidence = f.hasGen && (f.postReport || f.postReadOnly);
  f.canSent = f.editing && st === 'with-engineer' && f.hasGen;                  // detected Sent evidence exists once generated
  f.canComplete = f.editing && f.postReport;
  f.canRetReview = f.editCase && f.withEng;
  f.canRetEng = f.editing && (f.isComplete || f.isQuery);
  f.canArchive = f.editing && f.isClosed && !f.archived;
  f.offersClose = f.editing && !f.isClosed;                                     // Core's closure outcomes for a non-terminal Case
  f.offersHold = f.editCase && !f.isClosed;
  f.offersAudit = S.kind === 'inspection-audit' && !UI.auditCreated && f.hasGen && f.editing && !f.archived;
  f.offersMenu = f.canEva || f.canHand || f.canSent || f.canComplete || f.canRetReview || f.canRetEng || f.canArchive || f.offersHold || f.offersAudit || f.offersClose;
  f.showEdit = !f.editing && !f.colleague && !f.archived;                       // a colleague's live lease renders no control
  f.offersRetReviewPanel = f.editCase && f.withEng;
  f.offersUnlink = f.editCase && f.sentEvidence && !f.isHeld && !f.isClosed;
  f.lifecyclePanel = f.offersRetReviewPanel || f.offersUnlink || f.canArchive || f.sentEvidence;
  f.reportReady = !f.empty;
  f.offersHead = !f.postReadOnly;                                               // AssessmentIsReadOnly is Completed/Query
  f.canGenerate = f.offersHead && f.editEng && f.reportReady;
  f.canPrepare = f.editing && f.hasGen && !f.stale && !f.prepared;
  f.canImport = f.editEng;
  f.importFromRead = !f.editing && !f.colleague && !f.postReadOnly && !f.archived && f.withEng && f.eng;
  f.offersLookup = f.editCase;
  f.offersNote = !f.postReadOnly && !f.archived;
  f.offersChase = f.editCase && (f.notReady || f.isHeld);
  f.anyLease = f.editing || f.colleague;
  return f;
}
function sectionEditable(key) { return ENGINEER_KEYS[key] ? F.editEng : F.editCase; }
function sectionAvailability(key) {
  if (F.colleague) return 'E Mawdsley is editing';
  if (!F.editing || sectionEditable(key)) return null;
  if (F.postReadOnly) return 'Return the Case to the Engineer to edit';
  if (ENGINEER_KEYS[key]) return 'Available With Engineer';
  return null;
}
function sectionOffersEdit(key) { return !F.editing && !F.colleague && !F.postReadOnly && !F.archived && key !== 'files' && key !== 'notes'; }

function money(n) { return '£' + n.toLocaleString('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 }); }
var EV = 2900, SALV = 725, REPAIR = 3004.79;

function computeText() {
  var t = {};
  var stage = { 'not-ready': 'Not ready', review: 'Review', 'with-engineer': 'With Engineer', 'post-report': 'With Engineer', held: 'Held', completed: 'Completed', query: 'Query', closed: 'Closed · Provider cancelled', 'created-in-error': 'Closed · Created in error' }[S.state];
  t.stateChip = S.state === 'held' ? 'Held · review on 24 Sep' : stage;
  t.reference = S.kind === 'audit' ? 'ap.QDOS26214' : 'QDOS26214';
  t.claimant = F.empty ? 'Not recorded' : 'Ms L Carter';
  t.engineer = (F.notReady || F.isReview || F.isHeld) ? 'Not recorded' : (UI.engineer || 'A Patterson');
  t.caseTypeChip = S.kind === 'audit' ? 'Audit' : 'Inspection + Audit';
  t.caseTypeName = { inspection: 'Inspection', 'inspection-audit': 'Inspection and audit', audit: 'Audit' }[S.kind];
  t.editLabel = F.postReadOnly ? 'Enable return' : 'Edit Case';
  t.lookupLine = { current: 'Looked up 28 Aug 2026 09:02 · Current', never: 'Not yet looked up', failed: 'Lookup failed · The lookup service did not answer (28 Aug 2026 09:02)' }[S.lookup];
  t.engineerValue = F.empty ? '—' : money(EV);
  t.salvageValue = F.empty ? '—' : money(SALV);
  t.equity = F.empty ? '—' : money(EV - SALV);
  t.repairCost = F.empty ? '—' : money(REPAIR);
  t.repairCostMeta = F.empty ? 'From current estimate' : (REPAIR > EV ? "Exceeds Engineer's Value" : 'From current estimate');
  t.repairOfValue = F.empty ? '—' : Math.round(REPAIR / EV * 100) + '%';
  t.outcomeChip = 'Total loss · Cat N';
  t.legalChip = 'Unroadworthy';
  t.genState = S.report === 'stale' ? 'Stale' : 'Confirmed';
  t.reportTitle = 'Total loss report · ' + t.reference;
  t.awaitingCount = '1';
  t.basisName = F.empty ? '' : "from Glass's retail";
  t.noBasisText = F.editEng ? 'Choose a basis card to calculate.' : 'None yet';
  t.estimateMeta = UI.estimateTab === 'e2' ? 'Manual · 3 Sep 2026 11:05 · 3 lines' : 'Audatex PDF · 28 Aug 2026 14:22 · 13 lines';
  t.docCount = F.empty ? '0' : (F.hasGen ? '3' : '2');
  t.imgCount = F.empty ? '0' : '6';
  t.mailCount = F.empty ? '0' : '1';
  t.userName = S.role === 'administrator' ? 'Alex Mercer' : S.role === 'user' ? 'Lisa Mckenzie' : 'A Patterson';
  t.roleName = { engineer: 'Engineer', administrator: 'Administrator', user: 'User' }[S.role];
  t.initials = S.role === 'administrator' ? 'AM' : S.role === 'user' ? 'LM' : 'AP';
  /* Next action (Details.Frame.cs NextAction) */
  var next;
  if (F.archived || F.isClosed) next = ['None', 'notes'];
  else if (F.isReview) next = ['Hand to Engineer', 'overview'];
  else if (F.notReady || F.isHeld) next = [F.notReady ? 'Vehicle images' : 'Held', 'overview'];
  else if (!F.reportReady) next = ["Engineer's Value · 2 more", 'valuation'];
  else if (!F.hasGen || F.stale) next = ['Generate report', 'report'];
  else if (F.sentEvidence) next = ['Mark completed', 'overview'];
  else if (F.prepared) next = ['Send prepared report', 'report'];
  else next = ['Prepare delivery', 'report'];
  t.nextLabel = next[0]; t.nextSection = SECTION_LABELS[next[1]]; t.nextKey = next[1];
  return t;
}

/* ------------------------------------------------------------ apply ---- */
function evalWhen(expr) {
  try { return !!(new Function('f', 'with (f) { return (' + expr + '); }'))(F); }
  catch (e) { console.error('data-when', expr, e); return false; }
}
function renderHeadTools() {
  SECTIONS.forEach(function (key) {
    var host = document.querySelector('[data-head-tools="' + key + '"]');
    if (!host) return;
    var section = $('section-' + key);
    var collapsed = section.classList.contains('is-collapsed');
    var html = '';
    if (sectionOffersEdit(key)) {
      html += '<form method="post" data-case-edit-form data-mock-action="claim-lease" data-section="' + key + '"><button type="submit" class="btn btn--small" data-section-edit="' + key + '"><svg class="icon" aria-hidden="true"><use href="#icon-pencil" /></svg><span>Edit</span></button></form>';
    }
    var avail = sectionAvailability(key);
    if (avail) html += '<span class="gated avail" data-section-availability="' + key + '"><svg class="icon" aria-hidden="true"><use href="#icon-lock" /></svg><span>' + avail + '</span></span>';
    html += '<button type="button" class="icon-button panel-collapse" data-collapse-toggle aria-expanded="' + (collapsed ? 'false' : 'true') + '" aria-label="' + (collapsed ? 'Expand section' : 'Collapse section') + '" data-label-collapse="Collapse section" data-label-expand="Expand section" title="Collapse section"><svg class="icon" aria-hidden="true"><use href="#icon-chevron-down" /></svg></button>';
    host.innerHTML = html;
  });
}
function apply() {
  F = computeFlags();
  T = computeText();
  var record = $('case-record');
  document.body.classList.toggle('mock-empty', F.empty);
  $('appShell').classList.toggle('rail-collapsed', S.rail === 'collapsed');
  var brand = document.querySelector('[data-brand]'); if (brand) brand.src = brand.getAttribute(S.logo === 'live' ? 'data-brand-live' : 'data-brand-refined');
  var railToggle = document.querySelector('[data-rail-toggle]');
  railToggle.setAttribute('aria-expanded', S.rail === 'collapsed' ? 'false' : 'true');
  record.classList.toggle('is-editing', F.editing);
  record.setAttribute('data-case-editing', F.editing ? 'true' : 'false');
  record.setAttribute('data-layout', S.layout);
  $$('[data-when]').forEach(function (el) { el.hidden = !evalWhen(el.getAttribute('data-when')); });
  $$('[data-text]').forEach(function (el) { var k = el.getAttribute('data-text'); if (k in T) el.textContent = T[k]; });
  /* state chip tone: _StatusChip rules */
  var chip = $('stateChip');
  var tone = { 'not-ready': 'amber', review: 'navy', 'with-engineer': 'navy', 'post-report': 'navy', held: 'amber', completed: 'green', query: 'navy', closed: 'neutral', 'created-in-error': 'neutral' }[S.state];
  chip.className = 'status status--' + tone;
  /* sections */
  SECTIONS.forEach(function (key) {
    var section = $('section-' + key);
    section.classList.toggle('is-locked', F.editing && !sectionEditable(key));
    section.classList.toggle('is-active', key === UI.section || (S.p.nine && (key === 'damage' || key === 'valuation') && UI.section === 'vehicle'));
  });
  $$('[data-eng-cell]').forEach(function (cell) { cell.classList.toggle('ro', !F.editEng); });
  var vatBox = $('f-valuation-vat'); if (vatBox) { vatBox.disabled = F.claimantVat; if (F.claimantVat) vatBox.checked = false; }
  $$('[data-eng-cell] > label').forEach(function (l) { l.hidden = false; });
  /* stepper */
  var stage = { review: 1, 'with-engineer': 2, 'post-report': 2, completed: 3, query: 3 }[S.state] || 0;
  $$('#stepper li').forEach(function (li, i) {
    li.className = i < stage ? 'done' : i === stage ? 'now' : '';
    li.querySelector('use').setAttribute('href', i < stage ? '#icon-check' : ['#icon-alert-circle', '#icon-eye', '#icon-user', '#icon-check'][i]);
  });
  /* damage */
  var damage = $('section-damage');
  damage.setAttribute('data-damage-editable', F.editEng ? 'true' : 'false');
  damage.setAttribute('data-clicker', S.clicker);
  renderDamage();
  /* settlement */
  var settlement = $('section-settlement');
  $('decisions').classList.toggle('has-proposal', F.proposal);
  settlement.setAttribute('data-settlement-editable', F.editEng ? 'true' : 'false');
  applySettlementVisibility();
  /* estimate bodies */
  $$('[data-estimate-body]').forEach(function (b) { b.hidden = b.getAttribute('data-estimate-body') !== UI.estimateTab || F.empty; });
  $$('[data-estimate-tab]').forEach(function (a) { a.setAttribute('aria-selected', a.getAttribute('data-estimate-tab') === UI.estimateTab ? 'true' : 'false'); });
  /* section nav */
  $$('[data-section-link]').forEach(function (a) { a.setAttribute('aria-current', a.getAttribute('data-section-link') === UI.section ? 'true' : 'false'); });
  $$('[data-case-layout]').forEach(function (b) { b.setAttribute('aria-pressed', b.getAttribute('data-case-layout') === S.layout ? 'true' : 'false'); });
  record.setAttribute('data-section-current', UI.section);
  var jump = $('nextJump'); if (jump) { jump.setAttribute('href', '#section-' + T.nextKey); jump.setAttribute('data-section-jump', T.nextKey); }
  /* file tabs */
  $$('[data-file-tab]').forEach(function (b) { b.setAttribute('aria-selected', b.getAttribute('data-file-tab') === UI.fileTab ? 'true' : 'false'); });
  $$('[data-file-tab-panel]').forEach(function (p) { p.hidden = p.getAttribute('data-file-tab-panel') !== UI.fileTab; });
  renderImages();
  renderIntakeImages();
  renderReportImages();
  renderHeadTools();
  applyProposals();
  renderStrip();
  renderProposalStrip();
  measureSticky();
}
function applySettlementVisibility() {
  var outcome = $('f-assessment-outcome').value;
  var legal = $('f-assessment-legal-status').value;
  var tl = outcome === 'total_loss';
  $('section-settlement').setAttribute('data-settlement-outcome', outcome);
  $$('[data-shown-when="total-loss"]').forEach(function (el) { el.hidden = !tl; });
  $$('[data-shown-when="unroadworthy"]').forEach(function (el) { el.hidden = legal !== 'unroadworthy'; });
}

/* ------------------------------------------------------------ strip ---- */
function renderStrip() {
  Object.keys(OPTIONS).forEach(function (key) {
    var host = document.querySelector('[data-strip="' + key + '"]');
    if (!host) return;
    host.innerHTML = OPTIONS[key].map(function (o) { return '<button type="button" data-set="' + key + '" data-value="' + o[0] + '" class="' + (S[key] === o[0] ? 'on' : '') + '">' + o[1] + '</button>'; }).join('');
  });
  var sel = $('mockSection'); if (sel && !sel.options.length) { SECTIONS.forEach(function (k) { var o = document.createElement('option'); o.value = k; o.textContent = SECTION_LABELS[k]; sel.appendChild(o); }); }
  if (sel) sel.value = UI.section;
  var dsel = $('mockDialog');
  if (dsel && dsel.options.length === 1) {
    $$('[data-dialog]').forEach(function (d) { var o = document.createElement('option'); o.value = d.getAttribute('data-dialog'); o.textContent = d.getAttribute('data-dialog'); dsel.appendChild(o); });
  }
}
function set(key, value) { S[key] = value; if (key === 'clicker') resetMarks(); apply(); }

/* --------------------------------------------------------- sticky ---- */
function measureSticky() {
  var block = document.querySelector('[data-sticky-block]');
  if (block) block.parentElement.style.setProperty('--sticky-h', block.offsetHeight + 'px');
}

/* --------------------------------------------------------- dialogs ---- */
var openDialogEl = null, dialogOpener = null;
function openDialog(name, opener) {
  var d = document.querySelector('[data-dialog="' + name + '"]');
  if (!d || d.hidden === false) return false;
  /* mirror site.js: an item that is hidden by state cannot open */
  closeDialog();
  d.hidden = false; openDialogEl = d; dialogOpener = opener || null;
  var focus = d.querySelector('[data-dialog-initial-focus]') || d.querySelector('h2[tabindex]');
  if (focus) focus.focus();
  return true;
}
function closeDialog() {
  if (!openDialogEl) return;
  openDialogEl.hidden = true; openDialogEl = null;
  if (dialogOpener && dialogOpener.focus) dialogOpener.focus();
  dialogOpener = null;
}
function toast(text, kind) {
  var region = $('toasts');
  var el = document.createElement('div');
  el.className = 'toast' + (kind ? ' toast--' + kind : '');
  el.textContent = text;
  region.appendChild(el);
  setTimeout(function () { el.remove(); }, 3200);
}
function notice(text, tone) {
  var host = $('caseNotices');
  host.innerHTML = '<div class="notice notice--' + (tone || 'success') + ' mb-2" role="status" data-confirmation><svg class="icon" aria-hidden="true"><use href="#icon-check" /></svg><span>' + text + '</span><button type="button" class="dismiss" data-dismiss aria-label="Dismiss"><svg class="icon" aria-hidden="true"><use href="#icon-x" /></svg></button></div>';
  toast(text);
}

/* ----------------------------------------------------- mock actions ---- */
function mockAction(name, form) {
  switch (name) {
    case 'claim-lease': {
      var section = form.getAttribute('data-section');
      if (F.colleague) { toast('E Mawdsley is editing', 'warning'); return; }
      S.edit = 'on'; UI.dirty = false; apply();
      if (section) jumpTo(section);
      return;
    }
    case 'cancel-edit':
      if (UI.dirty) { $('edit-finish-confirm').hidden = false; return; }
      S.edit = 'off'; apply(); return;
    case 'save':
      UI.dirty = false; notice('Case data saved'); return;
    case 'hold': closeDialog(); S.state = 'held'; apply(); notice('The Case was placed on hold.'); return;
    case 'close-case': closeDialog(); S.state = 'closed'; S.edit = 'off'; apply(); notice('The Case was closed.'); return;
    case 'hand-to-engineer': case 'assign-to-me': { var pick = name === 'assign-to-me' ? 'A Patterson' : ($('case-handoff-engineer').selectedOptions[0] || {}).textContent || 'A Patterson'; UI.engineer = pick; if (S.p.signoff) { var so = $('f-sign-off-engineer'); Array.prototype.forEach.call(so.options, function (o) { if (o.textContent === pick) so.value = o.value; }); so.parentElement.querySelector('.fv').textContent = pick; } closeDialog(); S.state = 'with-engineer'; apply(); notice('The Case was handed to ' + pick + '.'); return; }
    case 'report-sent': closeDialog(); S.state = 'post-report'; apply(); notice('Report sent evidence confirmed.'); return;
    case 'create-audit': closeDialog(); UI.auditCreated = true; apply(); notice('Case ap.QDOS26214 was created.'); return;
    case 'release-hold': closeDialog(); S.state = 'not-ready'; apply(); notice('The hold was released.'); return;
    case 'mark-completed': closeDialog(); S.state = 'completed'; S.edit = 'off'; apply(); notice('The Case was marked completed.'); return;
    case 'return-to-review': closeDialog(); S.state = 'review'; apply(); notice('The Case was returned to Review.'); return;
    case 'return-to-engineer': closeDialog(); S.state = 'with-engineer'; apply(); notice('The Case was returned to the Engineer.'); return;
    case 'archive': closeDialog(); S.archived = '1'; S.edit = 'off'; apply(); notice('The Case was archived.'); return;
    case 'unlink-evidence': closeDialog(); toast('Live: unlinks the Sent item with the reason recorded.'); return;
    case 'generate': { if (S.p.reportdate) { var rd = $('f-report-date'); if (!rd.value) { rd.value = '2026-09-16'; rd.parentElement.querySelector('.fv').textContent = '16 Sep 2026'; rd.parentElement.querySelector('.fv').classList.remove('empty'); } } S.report = 'confirmed'; apply(); notice('The report was generated.'); return; }
    case 'prepare-delivery': S.report = 'prepared'; apply(); notice('Delivery prepared.'); return;
    case 'add-note': {
      var ta = form.querySelector('textarea'); var text = ta.value.trim(); if (!text) return;
      var tl = $('notesTimeline');
      var row = document.createElement('div'); row.className = 'note'; row.setAttribute('data-history-event', 'operator_note');
      row.innerHTML = '<div class="nw"><span>16 Sep 2026</span> <span>09:41</span></div><div class="nb"><b>' + T.userName + '</b> — ' + text.replace(/</g, '&lt;') + '</div>';
      tl.insertBefore(row, tl.firstChild); ta.value = ''; toast('Case note added.'); return;
    }
    case 'new-estimate': toast('Live: opens ?estimate=new — a blank Draft in the editor.'); return;
    case 'glass-launch': S.glass = 'open'; apply(); toast("Live: the Glass's estimator opens in its own window."); return;
    case 'add-line': {
      var body = $('estEditBody'); var phantom = body.querySelector('[data-estimate-phantom]');
      var row2 = phantom.cloneNode(true); row2.classList.remove('phantom'); row2.removeAttribute('data-estimate-phantom'); row2.setAttribute('data-estimate-line', '');
      body.insertBefore(row2, phantom); UI.dirty = true; return;
    }
    case 'remove-line': { var tr = form.closest ? form.closest('tr') : null; if (tr) { if (S.p.estdel) { undoRow = { node: tr, parent: tr.parentNode, next: tr.nextSibling }; var region = $('toasts'); var el = document.createElement('div'); el.className = 'toast'; el.innerHTML = 'Line removed <button type="button" data-undo-row>Undo</button>'; region.appendChild(el); setTimeout(function () { el.remove(); }, 6000); } tr.remove(); } UI.dirty = true; return; }
    case 'preview-draft': toast('Live: opens the working draft PDF in the page viewer.'); return;
    case 'artifact': toast('Live: downloads the retained artifact.'); return;
    case 'view-doc': toast('Live: opens the PDF in the page viewer.'); return;
    case 'save-as': toast('Live: audited download of the file.'); return;
    default: toast('Mock: ' + name);
  }
}

/* --------------------------------------------------------- damage ---- */
var PLAN = {
  body: 'M72 30 C90 16 150 16 168 30 L190 60 C196 70 198 80 198 100 L198 330 C198 352 194 366 186 380 C170 410 70 410 54 380 C46 366 42 352 42 330 L42 100 C42 80 44 70 50 60 Z',
  front: 'M58 118 H182 L172 160 C150 154 90 154 68 160 Z',
  rear: 'M68 300 C90 306 150 306 172 300 L168 334 H72 Z',
  seam: 'M64 46 C90 34 150 34 176 46 M64 46 L58 118 M176 46 L182 118 M176 46 L182 100 M64 46 L58 100 M58 100 H42 M182 100 H198 M68 160 V300 M172 160 V300 M42 160 H68 M172 160 H198 M42 238 H68 M172 238 H198 M42 300 H68 M172 300 H198 M42 366 H68 M172 366 H198 M72 334 H168 M68 366 Q120 392 172 366 M92 382 V402 M148 382 V402',
  soft: 'M102 54 L98 112 M138 54 L142 112',
  frontLamps: ['M74 34 Q86 27 100 28 L98 40 Q84 40 72 44 Z', 'M166 34 Q154 27 140 28 L142 40 Q156 40 168 44 Z'],
  rearLamps: ['M62 372 Q72 384 92 386 L92 396 Q70 392 58 380 Z', 'M178 372 Q168 384 148 386 L148 396 Q170 392 182 380 Z'],
  mirrors: [[22, 160, 22, 11], [196, 160, 22, 11]],
  zones: [
    ['front_centre', 'M64 0 H176 V46 C150 34 90 34 64 46 Z', 120, 30], ['front_right_corner', 'M176 0 H240 V100 H182 L176 46 Z', 184, 68], ['front_left_corner', 'M64 0 H0 V100 H58 L64 46 Z', 56, 68],
    ['bonnet', 'M64 46 C90 34 150 34 176 46 L182 118 H58 Z', 120, 84], ['windscreen', 'M58 118 H182 L172 160 C150 154 90 154 68 160 Z', 120, 140],
    ['right_front_wing', 'M182 100 H240 V160 H172 L182 118 Z', 188, 132], ['left_front_wing', 'M58 100 H0 V160 H68 L58 118 Z', 52, 132],
    ['right_front_door', 'M172 160 H240 V238 H172 Z', 186, 199], ['left_front_door', 'M68 160 H0 V238 H68 Z', 54, 199],
    ['right_rear_door', 'M172 238 H240 V300 H172 Z', 186, 269], ['left_rear_door', 'M68 238 H0 V300 H68 Z', 54, 269],
    ['roof', 'M68 160 C90 154 150 154 172 160 V300 C150 306 90 306 68 300 Z', 120, 232],
    ['right_quarter', 'M172 300 H240 V366 H172 Z', 186, 334], ['left_quarter', 'M68 300 H0 V366 H68 Z', 54, 334],
    ['rear_screen', 'M68 300 C90 306 150 306 172 300 L168 334 H72 Z', 120, 318], ['tailgate', 'M72 334 H168 L172 366 Q120 392 68 366 Z', 120, 356],
    ['rear_right_corner', 'M172 366 H240 V430 H148 V382 Q166 378 172 366 Z', 180, 390], ['rear_left_corner', 'M68 366 H0 V430 H92 V382 Q74 378 68 366 Z', 60, 390],
    ['rear_centre', 'M92 380 Q120 390 148 380 V430 H92 Z', 120, 400]
  ],
  wheels: [['wheel_left_front', 36, 112], ['wheel_right_front', 204, 112], ['wheel_left_rear', 36, 324], ['wheel_right_rear', 204, 324]]
};
var ZONES = { front: ['Front', 'front'], left_front: ['Left front', 'left_front'], right_front: ['Right front', 'right_front'], left_side: ['Left side', 'left_side'], right_side: ['Right side', 'right_side'], rear: ['Rear', 'rear'], left_rear: ['Left rear', 'left_rear'], right_rear: ['Right rear', 'right_rear'], front_left_corner: ['Front N/S corner', 'left_front'], front_centre: ['Front centre', 'front'], front_right_corner: ['Front O/S corner', 'right_front'], left_front_wing: ['N/S front wing', 'left_front'], left_front_door: ['N/S front door', 'left_side'], left_rear_door: ['N/S rear door', 'left_side'], left_quarter: ['N/S rear quarter', 'left_rear'], right_front_wing: ['O/S front wing', 'right_front'], right_front_door: ['O/S front door', 'right_side'], right_rear_door: ['O/S rear door', 'right_side'], right_quarter: ['O/S rear quarter', 'right_rear'], rear_left_corner: ['Rear N/S corner', 'left_rear'], rear_centre: ['Rear centre', 'rear'], rear_right_corner: ['Rear O/S corner', 'right_rear'], bonnet: ['Bonnet', 'front'], windscreen: ['Windscreen', 'front'], roof: ['Roof', 'roof'], rear_screen: ['Rear screen', 'rear'], tailgate: ['Boot / tailgate', 'rear'], wheel_left_front: ['Left front wheel', 'wheel'], wheel_right_front: ['Right front wheel', 'wheel'], wheel_left_rear: ['Left rear wheel', 'wheel'], wheel_right_rear: ['Right rear wheel', 'wheel'], underside: ['Underside', 'underside'], interior: ['Interior', 'interior'], mechanical: ['Mechanical', 'mechanical'] };
var LOCATIONS = { front: 'Front', left_front: 'Left front', right_front: 'Right front', left_side: 'Left side', right_side: 'Right side', rear: 'Rear', left_rear: 'Left rear', right_rear: 'Right rear', roof: 'Roof', wheel: 'Wheel', underside: 'Underside', interior: 'Interior', mechanical: 'Mechanical' };
var SEVERITIES = [['light', 'Light'], ['light_to_moderate', 'Light to moderate'], ['moderate', 'Moderate'], ['moderate_to_heavy', 'Moderate to heavy'], ['heavy', 'Heavy']];
var IMPACTS_FIXTURE = [{ zone: 'rear_centre', severity: 'moderate', note: 'Bumper displaced' }, { zone: 'tailgate', severity: 'moderate', note: 'Creased below the screen' }];
var impacts = IMPACTS_FIXTURE.map(function (i) { return Object.assign({}, i); });
function buildDamageSvg() {
  var svg = $('damageSvg');
  var out = '<defs><clipPath id="damage-plan-clip"><path d="' + PLAN.body + '" /></clipPath><linearGradient id="damage-plan-body" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#ffffff" /><stop offset="1" stop-color="#eaeef0" /></linearGradient><linearGradient id="damage-plan-glass" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#d7dfe4" /><stop offset="1" stop-color="#b9c5cc" /></linearGradient><filter id="damage-plan-shadow" x="-20%" y="-10%" width="140%" height="130%"><feDropShadow dx="0" dy="5" stdDeviation="5" flood-color="#1a2a33" flood-opacity=".16" /></filter></defs>';
  out += '<text class="damage-diagram-label" x="120" y="8" text-anchor="middle">FRONT</text>';
  PLAN.wheels.forEach(function (w) { out += '<rect class="dv-wheel" x="' + (w[1] - 8) + '" y="' + (w[2] - 24) + '" width="16" height="48" rx="5" />'; });
  PLAN.mirrors.forEach(function (m) { out += '<rect class="dv-mirror" x="' + m[0] + '" y="' + m[1] + '" width="' + m[2] + '" height="' + m[3] + '" rx="4" />'; });
  out += '<path class="dv-body" d="' + PLAN.body + '" fill="url(#damage-plan-body)" filter="url(#damage-plan-shadow)" />';
  out += '<path class="dv-glass" fill="url(#damage-plan-glass)" d="' + PLAN.front + '" /><path class="dv-glass" fill="url(#damage-plan-glass)" d="' + PLAN.rear + '" />';
  out += '<path class="dv-seam" d="' + PLAN.seam + '" /><path class="dv-seam dv-seam--soft" d="' + PLAN.soft + '" />';
  PLAN.frontLamps.forEach(function (l) { out += '<path class="dv-lamp" d="' + l + '" />'; });
  PLAN.rearLamps.forEach(function (l) { out += '<path class="dv-lamp dv-lamp--rear" d="' + l + '" />'; });
  out += '<g class="damage-zones" clip-path="url(#damage-plan-clip)">';
  PLAN.zones.forEach(function (z) { out += '<path data-damage-zone="' + z[0] + '" d="' + z[1] + '"><title>' + ZONES[z[0]][0] + '</title></path>'; });
  out += '</g><g class="damage-zones">';
  PLAN.wheels.forEach(function (w) { out += '<rect data-damage-zone="' + w[0] + '" x="' + (w[1] - 8) + '" y="' + (w[2] - 24) + '" width="16" height="48" rx="5"><title>' + ZONES[w[0]][0] + '</title></rect>'; });
  out += '</g><g class="damage-markers" aria-hidden="true">';
  PLAN.zones.forEach(function (z) { out += '<g class="damage-marker is-hidden" data-damage-marker="' + z[0] + '" transform="translate(' + z[2] + ' ' + z[3] + ')"><circle r="8" /><text y="3.2" text-anchor="middle"></text></g>'; });
  PLAN.wheels.forEach(function (w) { out += '<g class="damage-marker is-hidden" data-damage-marker="' + w[0] + '" transform="translate(' + w[1] + ' ' + w[2] + ')"><circle r="8" /><text y="3.2" text-anchor="middle"></text></g>'; });
  out += '</g><text class="damage-diagram-label" x="120" y="431" text-anchor="middle">REAR</text>';
  svg.innerHTML = out;
}
function renderDamage() {
  var list = F.empty ? [] : (S.clicker === 'zones' ? impacts : marksToImpacts());
  renderMarks();
  var svg = $('damageSvg');
  $$('[data-damage-zone]', svg).forEach(function (el) {
    var hit = list.filter(function (i) { return i.zone === el.getAttribute('data-damage-zone'); })[0];
    el.setAttribute('data-sev', hit ? hit.severity : '');
    el.classList.toggle('is-damaged', !!hit && S.clicker === 'zones');
  });
  $$('[data-damage-marker]', svg).forEach(function (m) {
    var idx = list.findIndex(function (i) { return i.zone === m.getAttribute('data-damage-marker'); });
    m.classList.toggle('is-hidden', idx < 0);
    m.querySelector('text').textContent = idx < 0 ? '' : String(idx + 1);
    if (idx >= 0) m.setAttribute('data-sev', list[idx].severity);
  });
  $$('.damage-extra [data-damage-zone]').forEach(function (b) { b.setAttribute('aria-pressed', list.some(function (i) { return i.zone === b.getAttribute('data-damage-zone'); }) ? 'true' : 'false'); });
  var heads = []; list.forEach(function (i) { var l = ZONES[i.zone][1]; if (heads.indexOf(l) < 0) heads.push(l); });
  var NAMES = S.clicker === 'zones' ? LOCATIONS : AREAS;
  $('section-damage').querySelector('[data-damage-location]').textContent = heads.length === 0 ? 'Not recorded' : heads.length === 1 ? NAMES[heads[0]] : 'Multiple · ' + heads.map(function (h) { return NAMES[h]; }).join(', ');
  var top = list.slice().sort(function (a, b) { return sevRank(b.severity) - sevRank(a.severity); })[0];
  $('section-damage').querySelector('[data-damage-severity]').textContent = top ? sevWord(top.severity) : 'Not recorded';
  $('section-damage').querySelector('[data-damage-count]').textContent = String(list.length);
  $('case-damage-impacts').value = JSON.stringify(list);
  var ul = $('section-damage').querySelector('[data-damage-impact-list]');
  if (S.clicker !== 'zones') { renderMarkRows(ul); }
  else if (!list.length) { ul.innerHTML = '<li class="muted" data-damage-empty>No damage recorded.</li>'; }
  else {
    ul.innerHTML = list.map(function (i, index) {
      var opts = SEVERITIES.map(function (s) { return '<option value="' + s[0] + '"' + (s[0] === i.severity ? ' selected' : '') + '>' + s[1] + '</option>'; }).join('');
      return '<li class="impact-row" data-damage-row="' + i.zone + '"><span class="zc"><i class="zn">' + (index + 1) + '</i>' + ZONES[i.zone][0] + '</span>'
        + '<span class="fc"><label class="sr-only" for="damage-severity-' + index + '">Severity</label><div class="fv">' + sevWord(i.severity) + '</div><select id="damage-severity-' + index + '" class="fi" data-damage-row-severity data-index="' + index + '">' + opts + '</select></span>'
        + '<span class="fc"><label class="sr-only" for="damage-note-' + index + '">Note</label><div class="fv' + (i.note ? '' : ' empty') + '">' + (i.note || 'No note') + '</div><input id="damage-note-' + index + '" class="fi" maxlength="200" value="' + (i.note || '').replace(/"/g, '&quot;') + '" placeholder="Note" data-damage-row-note data-index="' + index + '" /></span>'
        + '<button type="button" class="del" data-damage-row-remove data-index="' + index + '" aria-label="Remove ' + ZONES[i.zone][0] + '">×</button></li>';
    }).join('');
  }
  var narrative = $('section-damage').querySelector('[data-damage-narrative]');
  narrative.textContent = list.length ? 'The vehicle has suffered ' + (top ? sevWord(top.severity).toLowerCase() : '') + ' collision/impact damage to the ' + (heads.length === 1 ? NAMES[heads[0]].toLowerCase() : 'following areas: ' + heads.map(function (h) { return NAMES[h].toLowerCase(); }).join(', ')) + '.' : 'Not recorded';
  narrative.classList.toggle('empty', !list.length);
}
function sevRank(code) { return SEVERITIES.findIndex(function (s) { return s[0] === code; }); }
function sevWord(code) { var s = SEVERITIES.filter(function (x) { return x[0] === code; })[0]; return s ? s[1] : 'Not recorded'; }
function toggleZone(code) {
  if (!F.editEng || S.clicker !== 'zones') return;
  var idx = impacts.findIndex(function (i) { return i.zone === code; });
  if (idx >= 0) impacts.splice(idx, 1); else impacts.push({ zone: code, severity: 'moderate', note: '' });
  UI.dirty = true; renderDamage();
}

/* ------------------------------------ damage selector variants (v27) ---- */
/* Four ways to say "the damage is here" without picking a panel: a click
   anywhere on the silhouette. Every mark still resolves to Core's zones
   (the panel under the point, or the panels a stroke or area covers), so
   the derived location, severity and narrative are the live ones. */
var MARKS = [];
var MARK_FIXTURE = [
  { kind: 'pins', x: 120, y: 396, severity: 'moderate', note: 'Bumper displaced' },
  { kind: 'pins', x: 124, y: 352, severity: 'moderate', note: 'Creased below the screen' },
  { kind: 'brush', points: [[104, 392], [112, 398], [128, 399], [136, 393]], severity: 'moderate', note: 'Bumper displaced' },
  { kind: 'brush', points: [[106, 350], [120, 356], [134, 350]], severity: 'moderate', note: 'Creased below the screen' },
  { kind: 'area', x: 120, y: 392, r: 18, severity: 'moderate', note: 'Bumper displaced' },
  { kind: 'area', x: 120, y: 350, r: 16, severity: 'moderate', note: 'Creased below the screen' },
  { kind: 'arrow', x: 120, y: 396, dx: 0, dy: -46, severity: 'moderate', note: 'Struck from behind' }
];
function resetMarks() { MARKS = MARK_FIXTURE.filter(function (m) { return m.kind === S.clicker; }).map(function (m, i) { return Object.assign({ id: 'm' + (i + 1) }, JSON.parse(JSON.stringify(m))); }); }
var markSeq = 10;
function svgPoint(evt) {
  var svg = $('damageSvg'); var pt = svg.createSVGPoint(); pt.x = evt.clientX; pt.y = evt.clientY;
  var p = pt.matrixTransform(svg.getScreenCTM().inverse()); return [Math.round(p.x), Math.round(p.y)];
}
function onVehicle(x, y) {
  var svg = $('damageSvg'); var pt = svg.createSVGPoint(); pt.x = x; pt.y = y;
  if (svg.querySelector('.dv-body').isPointInFill(pt)) return true;
  return PLAN.wheels.some(function (w) { return Math.abs(x - w[1]) <= 8 && Math.abs(y - w[2]) <= 24; })
    || PLAN.mirrors.some(function (m) { return x >= m[0] && x <= m[0] + m[2] && y >= m[1] && y <= m[1] + m[3]; });
}
function zoneAt(x, y) {
  var svg = $('damageSvg'); var pt = svg.createSVGPoint(); pt.x = x; pt.y = y;
  var wheel = PLAN.wheels.filter(function (w) { return Math.abs(x - w[1]) <= 8 && Math.abs(y - w[2]) <= 24; })[0];
  if (wheel) return wheel[0];
  var hit = $$('.damage-zones path[data-damage-zone]', svg).filter(function (el) { return el.isPointInFill(pt); })[0];
  return hit ? hit.getAttribute('data-damage-zone') : null;
}
var AREAS = { front: 'Front', left_front: 'LH Front', left_rear: 'LH Rear', left_side: 'LH Side', rear: 'Rear', right_front: 'RH Front', right_rear: 'RH Rear', right_side: 'RH Side' };
var AREA_ORDER = ['front', 'left_front', 'left_rear', 'left_side', 'rear', 'right_front', 'right_rear', 'right_side'];
/* The silhouette cut into the eight areas: the front and rear thirds
   (y < 150, y > 300) and the sides between them; left of x 100, right of
   x 140, the middle in between. A middle-band click on the centre line
   (the roof) falls to the nearer side — open question G4. */
function areaAt(x, y) {
  if (!onVehicle(x, y)) return null;
  var band = y < 150 ? 'front' : y > 300 ? 'rear' : 'side';
  var lat = x < 100 ? 'left' : x > 140 ? 'right' : 'centre';
  if (band === 'side') return (lat === 'centre' ? (x < 120 ? 'left' : 'right') : lat) + '_side';
  if (lat === 'centre') return band;
  return lat + '_' + band;
}
function markPoints(m) {
  if (m.kind === 'pins' || m.kind === 'arrow') return [[m.x, m.y]];
  if (m.kind === 'brush') return m.points;
  var pts = [[m.x, m.y]]; for (var a = 0; a < 8; a++) pts.push([m.x + Math.round(m.r * Math.cos(a * Math.PI / 4)), m.y + Math.round(m.r * Math.sin(a * Math.PI / 4))]); return pts;
}
function markAreas(m) {
  var areas = []; markPoints(m).forEach(function (p) { var z = areaAt(p[0], p[1]); if (z && areas.indexOf(z) < 0) areas.push(z); });
  return areas.sort(function (a, b) { return AREA_ORDER.indexOf(a) - AREA_ORDER.indexOf(b); });
}
function markZones(m) {
  var pts;
  if (m.kind === 'pins' || m.kind === 'arrow') pts = [[m.x, m.y]];
  else if (m.kind === 'brush') pts = m.points;
  else { pts = [[m.x, m.y]]; for (var a = 0; a < 8; a++) pts.push([m.x + Math.round(m.r * Math.cos(a * Math.PI / 4)), m.y + Math.round(m.r * Math.sin(a * Math.PI / 4))]); }
  var zones = []; pts.forEach(function (p) { var z = zoneAt(p[0], p[1]); if (z && zones.indexOf(z) < 0) zones.push(z); });
  return zones;
}
function marksToImpacts() {
  var out = [];
  MARKS.forEach(function (m) { markAreas(m).forEach(function (z) { out.push({ zone: z, severity: m.severity, note: m.note }); }); });
  return out;
}
function arrowSeverity(dx, dy) { var len = Math.hypot(dx, dy); return len < 18 ? 'light' : len < 32 ? 'light_to_moderate' : len < 48 ? 'moderate' : len < 64 ? 'moderate_to_heavy' : 'heavy'; }
function renderMarks() {
  var svg = $('damageSvg'); var g = svg.querySelector('[data-damage-marks]');
  if (!g) { g = document.createElementNS('http://www.w3.org/2000/svg', 'g'); g.setAttribute('class', 'damage-marks'); g.setAttribute('data-damage-marks', ''); svg.appendChild(g); }
  var hint = $('section-damage').querySelector('[data-damage-hint]');
  if (S.clicker === 'zones' || F.empty) { g.innerHTML = ''; hint.hidden = true; return; }
  hint.hidden = !F.editEng;
  var guides = F.editEng ? '<g class="dm-guides" aria-hidden="true"><path d="M42 150 H198 M42 300 H198 M100 16 V410 M140 16 V410" /></g>' : '';
  hint.textContent = { pins: 'Click anywhere on the vehicle to drop a pin · drag to move', brush: 'Draw over the damage · one stroke, one mark', area: 'Press and drag to size the damaged area', arrow: 'Press where it was struck and drag in the direction of the impact · length sets severity' }[S.clicker];
  g.innerHTML = guides + MARKS.map(function (m, i) {
    var n = i + 1, sev = m.severity;
    if (m.kind === 'pins') return '<g class="dm dm-pin" data-mark="' + m.id + '" data-sev="' + sev + '" transform="translate(' + m.x + ' ' + m.y + ')"><circle class="halo sev" r="15" /><circle class="pin" r="9" /><text y="3.2" text-anchor="middle">' + n + '</text></g>';
    if (m.kind === 'brush') { var d = m.points.map(function (p, k) { return (k ? 'L' : 'M') + p[0] + ' ' + p[1]; }).join(' '); var f = m.points[0]; return '<g class="dm dm-brush" data-mark="' + m.id + '" data-sev="' + sev + '"><path class="sev" d="' + d + (m.points.length === 1 ? ' l0.1 0' : '') + '" stroke-width="16" /><circle class="n" r="8" cx="' + f[0] + '" cy="' + f[1] + '" /><text x="' + f[0] + '" y="' + (f[1] + 3.2) + '" text-anchor="middle">' + n + '</text></g>'; }
    if (m.kind === 'area') return '<g class="dm dm-area" data-mark="' + m.id + '" data-sev="' + sev + '"><circle class="area sev" cx="' + m.x + '" cy="' + m.y + '" r="' + m.r + '" /><circle class="n" r="8" cx="' + m.x + '" cy="' + m.y + '" /><text x="' + m.x + '" y="' + (m.y + 3.2) + '" text-anchor="middle">' + n + '</text></g>';
    var tx = m.x + m.dx, ty = m.y + m.dy, ang = Math.atan2(m.dy, m.dx), hx = m.x + m.dx * 0.55, hy = m.y + m.dy * 0.55;
    var a1 = [tx - 12 * Math.cos(ang - 0.5), ty - 12 * Math.sin(ang - 0.5)], a2 = [tx - 12 * Math.cos(ang + 0.5), ty - 12 * Math.sin(ang + 0.5)];
    return '<g class="dm dm-arrow" data-mark="' + m.id + '" data-sev="' + sev + '"><line class="sev" x1="' + m.x + '" y1="' + m.y + '" x2="' + tx + '" y2="' + ty + '" /><polygon class="sev" points="' + tx + ',' + ty + ' ' + a1[0].toFixed(1) + ',' + a1[1].toFixed(1) + ' ' + a2[0].toFixed(1) + ',' + a2[1].toFixed(1) + '" /><circle class="impact sev" cx="' + m.x + '" cy="' + m.y + '" r="6" /><circle class="n" r="8" cx="' + hx.toFixed(1) + '" cy="' + hy.toFixed(1) + '" /><text x="' + hx.toFixed(1) + '" y="' + (hy + 3.2).toFixed(1) + '" text-anchor="middle">' + n + '</text></g>';
  }).join('');
}
function renderMarkRows(ul) {
  if (!MARKS.length) { ul.innerHTML = '<li class="muted" data-damage-empty>No damage recorded.</li>'; return; }
  ul.innerHTML = MARKS.map(function (m, index) {
    var areas = markAreas(m); var where = areas.length ? areas.map(function (z) { return AREAS[z]; }).join(', ') : 'Off the vehicle';
    var opts = SEVERITIES.map(function (sv) { return '<option value="' + sv[0] + '"' + (sv[0] === m.severity ? ' selected' : '') + '>' + sv[1] + '</option>'; }).join('');
    var kind = { pins: 'Pin', brush: 'Stroke', area: 'Area', arrow: 'Impact' }[m.kind];
    return '<li class="impact-row" data-damage-row="' + m.id + '"><span class="zc"><i class="zn">' + (index + 1) + '</i>' + where + '</span>'
      + '<span class="fc"><label class="sr-only" for="mark-severity-' + index + '">Severity</label><div class="fv">' + sevWord(m.severity) + '</div><select id="mark-severity-' + index + '" class="fi" data-mark-severity data-index="' + index + '">' + opts + '</select></span>'
      + '<span class="fc"><label class="sr-only" for="mark-note-' + index + '">Note</label><div class="fv' + (m.note ? '' : ' empty') + '">' + (m.note || 'No note') + '</div><input id="mark-note-' + index + '" class="fi" maxlength="200" value="' + (m.note || '').replace(/"/g, '&quot;') + '" placeholder="Note" data-mark-note data-index="' + index + '" /></span>'
      + '<button type="button" class="del" data-mark-remove data-index="' + index + '" aria-label="Remove ' + kind + ' ' + (index + 1) + '">×</button></li>';
  }).join('');
}
var drawing = null;
function bindMarkPointer() {
  var svg = $('damageSvg');
  svg.addEventListener('pointerdown', function (e) {
    if (S.clicker === 'zones' || !F.editEng) return;
    var p = svgPoint(e); var markEl = e.target.closest('[data-mark]');
    if (markEl && (S.clicker === 'pins' || S.clicker === 'area')) { var m = MARKS.filter(function (x) { return x.id === markEl.getAttribute('data-mark'); })[0]; drawing = { move: m, ox: m.x - p[0], oy: m.y - p[1] }; markEl.classList.add('is-dragging'); svg.setPointerCapture(e.pointerId); return; }
    if (!onVehicle(p[0], p[1])) return;
    e.preventDefault();
    if (S.clicker === 'pins') { MARKS.push({ id: 'm' + (markSeq++), kind: 'pins', x: p[0], y: p[1], severity: 'moderate', note: '' }); UI.dirty = true; renderDamage(); return; }
    if (S.clicker === 'brush') drawing = { mark: { id: 'm' + (markSeq++), kind: 'brush', points: [p], severity: 'moderate', note: '' } };
    if (S.clicker === 'area') drawing = { mark: { id: 'm' + (markSeq++), kind: 'area', x: p[0], y: p[1], r: 12, severity: 'moderate', note: '' } };
    if (S.clicker === 'arrow') drawing = { mark: { id: 'm' + (markSeq++), kind: 'arrow', x: p[0], y: p[1], dx: 0, dy: -20, severity: 'light_to_moderate', note: '' } };
    MARKS.push(drawing.mark); svg.setPointerCapture(e.pointerId); renderMarks();
  });
  svg.addEventListener('pointermove', function (e) {
    if (!drawing) { if (S.clicker !== 'zones' && F.editEng) { var hp = svgPoint(e); var area = areaAt(hp[0], hp[1]); $('section-damage').querySelector('[data-damage-readout]').textContent = area ? AREAS[area] : ''; } return; }
    var p = svgPoint(e);
    if (drawing.move) { drawing.move.x = p[0] + drawing.ox; drawing.move.y = p[1] + drawing.oy; renderMarks(); return; }
    var m = drawing.mark;
    if (m.kind === 'brush') { var last = m.points[m.points.length - 1]; if (Math.hypot(p[0] - last[0], p[1] - last[1]) >= 4) m.points.push(p); }
    if (m.kind === 'area') m.r = Math.max(10, Math.round(Math.hypot(p[0] - m.x, p[1] - m.y)));
    if (m.kind === 'arrow') { m.dx = p[0] - m.x; m.dy = p[1] - m.y; if (Math.hypot(m.dx, m.dy) < 6) { m.dx = 0; m.dy = -20; } m.severity = arrowSeverity(m.dx, m.dy); }
    renderMarks();
  });
  var end = function () { if (!drawing) return; $$('.dm.is-dragging').forEach(function (el) { el.classList.remove('is-dragging'); }); drawing = null; UI.dirty = true; renderDamage(); };
  svg.addEventListener('pointerup', end); svg.addEventListener('pointercancel', end);
  svg.addEventListener('pointerleave', function () { if (S.clicker !== 'zones') $('section-damage').querySelector('[data-damage-readout]').textContent = ''; });
}
/* --------------------------------------------------------- images ---- */
var IMAGES = [
  { id: 'i1', name: 'rear-34-os.jpg', tag: 'Close-up', colour: 'amber', role: 'CloseUp', order: 1, hue: '#5b6472' },
  { id: 'i2', name: 'rear-straight.jpg', tag: 'Overview', colour: 'blue', role: 'Overview', order: 2, hue: '#6e7582' },
  { id: 'i3', name: 'tailgate.jpg', tag: 'Close-up', colour: 'amber', role: 'Supporting', order: 3, hue: '#7a8290' },
  { id: 'i4', name: 'rear-panel.jpg', tag: '', colour: '', role: 'NotUsed', order: null, hue: '#616977' },
  { id: 'i5', name: 'lamp-os.jpg', tag: 'Third party', colour: 'red', role: 'NotUsed', order: null, hue: '#8a93a1' },
  { id: 'i6', name: 'odometer.jpg', tag: '', colour: '', role: 'NotUsed', order: null, hue: '#565e6b' }
];
function placeholder(img, w, h) {
  var svg = '<svg xmlns="http://www.w3.org/2000/svg" width="' + w + '" height="' + h + '" viewBox="0 0 ' + w + ' ' + h + '"><defs><linearGradient id="g" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="' + img.hue + '"/><stop offset="1" stop-color="#2f343a"/></linearGradient></defs><rect width="100%" height="100%" fill="url(#g)"/><ellipse cx="' + (w * 0.5) + '" cy="' + (h * 0.62) + '" rx="' + (w * 0.34) + '" ry="' + (h * 0.18) + '" fill="rgba(255,255,255,.08)"/><text x="50%" y="52%" font-family="ui-monospace,Consolas,monospace" font-size="' + Math.round(h / 12) + '" fill="rgba(255,255,255,.85)" text-anchor="middle">' + img.name + '</text><text x="50%" y="66%" font-family="ui-monospace,Consolas,monospace" font-size="' + Math.round(h / 20) + '" fill="rgba(255,255,255,.55)" text-anchor="middle">synthetic scene · not a case file</text></svg>';
  return 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svg);
}
var TAG_VOCAB = [['Overview', 'blue'], ['Close-up', 'amber'], ['Third party', 'red'], ['Reflection', 'grey'], ['Market research', 'navy']];
function renderImages() {
  var grid = $('imageGrid');
  if (!grid) return;
  var custody = S.custody;
  grid.innerHTML = IMAGES.map(function (img, n) {
    var pending = custody !== 'confirmed' && n === 5;
    if (pending) return '<li class="image-tile" data-image-tile="' + img.id + '"><span class="gallery-item th th--placeholder" data-gallery-placeholder><span class="gallery-caption"><strong>' + img.name + '</strong><small>' + (custody === 'failed' ? 'Storage not ready' : 'Storing') + '</small></span></span></li>';
    var thumb = placeholder(img, 320, 240), full = placeholder(img, 1600, 1200);
    var chips = img.tag ? '<span class="tag-chips"><span class="tag-chip tag-chip--' + img.colour + '">' + img.tag + '</span></span>' : '';
    var picker = '';
    if (F.editCase) {
      picker = '<div class="image-tile-actions"><details class="tag-picker" data-tag-picker data-menu><summary class="btn btn--small">Tag</summary><div class="tag-picker-body">'
        + TAG_VOCAB.map(function (t) { var applied = t[0] === img.tag; return '<form method="post" data-mock-handler="' + (applied ? 'UntagImage' : 'TagImage') + '"><button type="submit" class="tag-option" aria-pressed="' + applied + '"><span class="tag-swatch tag-swatch--' + t[1] + '" aria-hidden="true"></span><span>' + t[0] + '</span>' + (applied ? '<svg class="icon" aria-hidden="true"><use href="#icon-check" /></svg>' : '') + '</button></form>'; }).join('')
        + '<form method="post" class="tag-picker-new" data-mock-handler="CreateImageTag"><label class="sr-only" for="tag-name-' + img.id + '">Name</label><input id="tag-name-' + img.id + '" name="name" maxlength="40" placeholder="New tag" /><label class="sr-only" for="tag-colour-' + img.id + '">Colour</label><select id="tag-colour-' + img.id + '" name="colour"><option>Blue</option><option>Green</option><option>Amber</option><option>Navy</option><option>Red</option><option>Grey</option></select><button type="submit" class="btn btn--small">Create</button></form>'
        + '</div></details><button type="button" class="btn btn--small" data-preparation-crop data-preparation-crop-occurrence="' + img.id + '">Crop</button></div>';
    }
    return '<li class="image-tile" data-image-tile="' + img.id + '"><a href="' + full + '" class="th' + (S.p.include && img.role === 'NotUsed' ? ' off' : '') + '" data-evidence-item data-download-href="' + full + '" data-media-type="image/jpeg" data-file-name="' + img.name + '" data-thumb="' + thumb + '" data-tag="' + img.tag + '" data-image-index="' + n + '"><img src="' + thumb + '" alt="' + img.name + '" loading="lazy" data-gallery-image data-tile-image /><span class="inc" aria-hidden="true">' + (img.role !== 'NotUsed' ? '✓' : '–') + '</span>' + (img.tag ? '<span class="tag">' + img.tag + '</span>' : '') + '<span class="rot" data-tile-rotation hidden></span><span class="th-name">' + img.name + '</span></a>' + chips + picker + '</li>';
  }).join('');
}
function renderIntakeImages() {
  var grid = $('intakeGrid'); if (!grid) return;
  var items = [{ id: 'k1', name: 'IMG_4471.jpg', hue: '#7d6a55' }, { id: 'k2', name: 'IMG_4472.jpg', hue: '#6a5d7d' }];
  grid.innerHTML = items.map(function (img, n) { var full = placeholder(img, 1600, 1200), thumb = placeholder(img, 320, 240); return '<li class="image-tile"><a href="' + full + '" class="th" data-evidence-item data-download-href="' + full + '" data-media-type="image/jpeg" data-file-name="' + img.name + '" data-thumb="' + thumb + '" data-image-index="' + n + '"><img src="' + thumb + '" alt="' + img.name + '" loading="lazy" data-gallery-image data-tile-image /><span class="th-name">' + img.name + '</span></a><div class="image-tile-source"><a href="#">IMG26-0413</a></div></li>'; }).join('');
}
function renderReportImages() {
  var strip = $('reportStrip'), prep = $('reportPrep');
  if (!strip) return;
  var inReport = IMAGES.filter(function (i) { return i.role !== 'NotUsed'; }).length;
  var count = $('section-report').querySelector('[data-report-image-count]'); if (count) count.textContent = inReport + ' of ' + IMAGES.length + ' in report';
  strip.innerHTML = IMAGES.map(function (img, n) {
    var included = img.role !== 'NotUsed';
    var role = { CloseUp: 'Close-up', Overview: 'Overview', Supporting: 'Supporting' }[img.role];
    return '<a class="th' + (included ? '' : ' off') + '" href="' + placeholder(img, 1600, 1200) + '" data-evidence-item data-media-type="image/jpeg" data-file-name="' + img.name + '" data-report-image="' + img.id + '" data-report-image-role="' + img.role + '" data-image-index="' + n + '"' + (F.editCase ? ' data-report-image-toggle="' + img.id + '"' : '') + '><img src="' + placeholder(img, 320, 240) + '" alt="" loading="lazy" data-gallery-image /><span class="inc" aria-hidden="true">' + (included ? '✓' : '–') + '</span>' + (included ? '<span class="tag">' + role + '</span>' : '') + '<span class="th-name">' + img.name + '</span></a>';
  }).join('');
  prep.innerHTML = IMAGES.filter(function (i) { return i.role !== 'NotUsed'; }).map(function (img) {
    var roles = ['NotUsed', 'CloseUp', 'Overview', 'Supporting'].map(function (r) { return '<option value="' + r + '"' + (r === img.role ? ' selected' : '') + '>' + ({ NotUsed: 'Not used', CloseUp: 'Close-up', Overview: 'Overview', Supporting: 'Supporting' })[r] + '</option>'; }).join('');
    var slug = 'report-' + img.id;
    return '<article class="report-image" data-preparation-card="" data-preparation-occurrence="' + img.id + '" data-report-image="' + img.id + '"><h3>' + img.name + '</h3><div class="report-image-preview th th--card" data-preparation-preview-box><img src="' + placeholder(img, 320, 240) + '" alt="" loading="lazy" data-preparation-preview-image data-gallery-image /></div>'
      + '<dl class="detail-list"><div><dt>Role</dt><dd data-preparation-role-label>' + ({ CloseUp: 'Close-up', Overview: 'Overview', Supporting: 'Supporting' })[img.role] + '</dd></div><div><dt>Rotation</dt><dd data-preparation-rotation-label>None</dd></div><div><dt>Crop</dt><dd data-preparation-crop-label>Full frame</dd></div></dl>'
      + '<div class="form-grid"><div class="field"><label for="report-image-role-' + slug + '">Role</label><select id="report-image-role-' + slug + '" data-preparation-role-select data-image="' + img.id + '">' + roles + '</select></div><div class="field"><label for="report-image-order-' + slug + '">Order</label><input id="report-image-order-' + slug + '" type="number" min="1" step="1" value="' + img.order + '" data-preparation-order /></div></div>'
      + '<div class="button-row"><button type="button" class="btn btn--small btn--primary" data-preparation-crop data-preparation-crop-occurrence="' + img.id + '">Crop</button><button type="button" class="btn btn--small" data-preparation-rotate="-90">Rotate left</button><button type="button" class="btn btn--small" data-preparation-rotate="90">Rotate right</button><button type="button" class="btn btn--small" data-preparation-reset>Reset</button></div></article>';
  }).join('');
}

/* --------------------------------------------------------- viewer ---- */
var viewer = { set: [], index: 0, rotation: 0, zoom: false, cropping: false };
function openViewer(index, setEl) {
  var v = $('viewer');
  var items = $$('[data-evidence-item]', setEl || $('imageGrid')).filter(function (a) { return a.getAttribute('data-media-type') === 'image/jpeg'; });
  if (!items.length) return;
  viewer.set = items; viewer.index = Math.max(0, Math.min(index, items.length - 1)); viewer.rotation = 0; viewer.zoom = false; viewer.cropping = false;
  v.hidden = false; document.body.classList.add('has-viewer');
  showViewer(); v.focus();
}
function showViewer() {
  var v = $('viewer'), a = viewer.set[viewer.index];
  var img = v.querySelector('[data-viewer-image]');
  img.hidden = false; img.src = a.getAttribute('href');
  img.style.transform = 'rotate(' + viewer.rotation + 'deg) scale(' + (viewer.zoom ? 2 : 1) + ')';
  v.querySelector('[data-viewer-name]').textContent = a.getAttribute('data-file-name');
  var tag = v.querySelector('[data-viewer-tag]'); var tagText = a.getAttribute('data-tag') || ''; tag.hidden = !tagText; tag.textContent = tagText;
  v.querySelector('[data-viewer-position]').textContent = (viewer.index + 1) + ' of ' + viewer.set.length;
  v.querySelector('[data-viewer-download]').setAttribute('href', a.getAttribute('href'));
  v.querySelector('[data-viewer-crop]').hidden = !(F.editCase && !viewer.cropping);
  var wrap = v.querySelector('[data-viewer-in-report-wrap]'); wrap.hidden = !F.editCase;
  var idx = parseInt(a.getAttribute('data-image-index'), 10); var image = IMAGES[idx];
  if (image) v.querySelector('[data-viewer-in-report]').checked = image.role !== 'NotUsed';
  v.querySelector('[data-viewer-view-tools]').hidden = viewer.cropping;
  v.querySelector('[data-viewer-crop-tools]').hidden = !viewer.cropping;
  v.querySelector('[data-viewer-crop-status]').textContent = viewer.cropping ? 'Full frame' : '';
  v.querySelector('[data-viewer-strip]').innerHTML = viewer.set.map(function (x, i) { var im = IMAGES[parseInt(x.getAttribute('data-image-index'), 10)]; return '<button type="button" class="' + (i === viewer.index ? 'is-active' : '') + (im && im.role === 'NotUsed' ? ' off' : '') + '" data-viewer-strip-index="' + i + '"><img src="' + x.querySelector('img').src + '" alt="" /></button>'; }).join('');
}
function closeViewer() { $('viewer').hidden = true; document.body.classList.remove('has-viewer'); }

/* ---------------------------------------------------- valuation calc ---- */
function recalcValuation() {
  var basis = document.querySelector('[data-valuation-basis]:checked');
  var host = document.querySelector('[data-valuation-lines-host]');
  if (!basis || !F.editEng) return;
  var retail = parseFloat(basis.getAttribute('data-retail')) || 0;
  var lines = [['Guide retail', money(retail)]]; var total = retail;
  var vat = document.querySelector('[name="selection.CommercialVat"]');
  if (vat && vat.checked) { var v = Math.round(retail * 0.2); lines.push(['Commercial VAT 20 %', '+ ' + money(v)]); total += v; }
  var ptl = $('f-valuation-ptl').value; if (ptl) { var p = Math.round(total * (parseInt(ptl, 10) / 100)); lines.push(['Previous total loss −' + ptl + ' %', '− ' + money(p)]); total -= p; }
  $$('[data-valuation-add]').forEach(function (row) {
    var cb = row.querySelector('[data-preset-toggle]'); if (!cb.checked) return;
    var label = row.querySelector('label') ? row.querySelector('label').textContent : (row.querySelector('input[type=text]').value || 'Other…');
    var amt = parseFloat(row.querySelector('.amt').value) || 0; lines.push([label, '+ ' + money(amt)]); total += amt;
  });
  var ded = parseFloat($('f-valuation-deduction').value) || 0; if (ded > 0) { lines.push(['Condition deduction', '− ' + money(ded)]); total -= ded; }
  var el = host.querySelector('[data-valuation-lines]:not([hidden])') || host.querySelector('[data-valuation-lines]');
  el.innerHTML = lines.map(function (l) { return '<div class="ln"><span>' + l[0] + '</span><b>' + l[1] + '</b></div>'; }).join('') + '<div class="ln tot"><span>Proposed Engineer\'s Value</span><b data-valuation-proposal>' + money(Math.round(total)) + '</b></div>';
  var apply = document.querySelector('[data-valuation-apply]'); if (apply) apply.disabled = false;
}

/* ------------------------------------------------------- navigation ---- */
function jumpTo(key) {
  if (S.p.nine && (key === 'damage' || key === 'valuation')) key = 'vehicle';
  UI.section = key;
  if (S.layout === 'tabs') { apply(); window.scrollTo({ top: 0 }); return; }
  apply();
  var el = $('section-' + key); if (el) { var top = el.getBoundingClientRect().top + window.scrollY - (parseFloat(getComputedStyle(el).scrollMarginTop) || 0); window.scrollTo({ top: top, behavior: 'instant' }); }
}
var observer = null;
function bindObserver() {
  if (!('IntersectionObserver' in window)) return;
  observer = new IntersectionObserver(function (entries) {
    if (S.layout !== 'scroll') return;
    entries.forEach(function (en) { if (en.isIntersecting) { var k = en.target.getAttribute('data-section'); if (S.p.nine && (k === 'damage' || k === 'valuation')) k = 'vehicle'; UI.section = k; $$('[data-section-link]').forEach(function (a) { a.setAttribute('aria-current', a.getAttribute('data-section-link') === UI.section ? 'true' : 'false'); }); $('case-record').setAttribute('data-section-current', UI.section); var s = $('mockSection'); if (s) s.value = UI.section; } });
  }, { rootMargin: '-45% 0px -50% 0px' });
  $$('.record-section').forEach(function (s) { observer.observe(s); });
}

/* ----------------------------------------------------------- events ---- */
document.addEventListener('click', function (e) {
  var t = e.target.closest ? e.target : null; if (!t) return;
  var b;
  if ((b = t.closest('[data-set]'))) { set(b.getAttribute('data-set'), b.getAttribute('data-value')); return; }
  if ((b = t.closest('[data-dialog-open]'))) { e.preventDefault(); openDialog(b.getAttribute('data-dialog-open'), b); return; }
  if ((b = t.closest('[data-dialog-close]'))) { e.preventDefault(); closeDialog(); return; }
  if (t.classList && t.classList.contains('dialog-backdrop')) { closeDialog(); return; }
  if ((b = t.closest('[data-dismiss]'))) { var n = b.closest('.notice,.stale-bar,[data-dismissable]'); if (n) n.remove(); return; }
  if ((b = t.closest('[data-collapse-toggle]'))) { var panel = b.closest('.panel'); var on = panel.classList.toggle('is-collapsed'); b.setAttribute('aria-expanded', on ? 'false' : 'true'); b.setAttribute('aria-label', on ? 'Expand section' : 'Collapse section'); measureSticky(); return; }
  if ((b = t.closest('[data-section-link],[data-section-jump]'))) { e.preventDefault(); jumpTo(b.getAttribute('data-section-link') || b.getAttribute('data-section-jump')); return; }
  if ((b = t.closest('[data-case-layout]'))) { S.layout = b.getAttribute('data-case-layout'); apply(); return; }
  if ((b = t.closest('[data-rail-toggle]'))) { S.rail = S.rail === 'collapsed' ? 'expanded' : 'collapsed'; apply(); return; }
  if ((b = t.closest('[data-file-tab]'))) { UI.fileTab = b.getAttribute('data-file-tab'); apply(); return; }
  if ((b = t.closest('[data-estimate-tab]'))) { e.preventDefault(); UI.estimateTab = b.getAttribute('data-estimate-tab'); apply(); return; }
  if ((b = t.closest('[data-estimate-expand]'))) { UI.expanded = !UI.expanded; $('section-estimate').classList.toggle('is-expanded', UI.expanded); document.body.classList.toggle('has-expanded', UI.expanded); b.setAttribute('aria-label', UI.expanded ? 'Close full screen' : 'Expand estimate'); b.title = UI.expanded ? 'Close full screen' : 'Expand'; return; }
  if ((b = t.closest('[data-mock-action]')) && b.tagName !== 'FORM') { if (b.tagName === 'A' || b.type === 'button') { e.preventDefault(); mockAction(b.getAttribute('data-mock-action'), b); return; } }
  if ((b = t.closest('[data-damage-zone]'))) { toggleZone(b.getAttribute('data-damage-zone')); return; }
  if ((b = t.closest('[data-damage-row-remove]'))) { impacts.splice(parseInt(b.getAttribute('data-index'), 10), 1); UI.dirty = true; renderDamage(); return; }
  if ((b = t.closest('[data-mark-remove]'))) { MARKS.splice(parseInt(b.getAttribute('data-index'), 10), 1); UI.dirty = true; renderDamage(); return; }
  if (t.id === 'mockClickerReset') { resetMarks(); renderDamage(); return; }
  if ((b = t.closest('[data-evidence-item]'))) {
    if (S.p.include && F.editCase && b.closest('#imageGrid')) { e.preventDefault(); var im0 = IMAGES[parseInt(b.getAttribute('data-image-index'), 10)]; if (im0) { im0.role = im0.role === 'NotUsed' ? 'Supporting' : 'NotUsed'; renderImages(); renderReportImages(); } return; }
    if (b.getAttribute('data-media-type') === 'image/jpeg') { e.preventDefault(); openViewer(parseInt(b.getAttribute('data-image-index'), 10) || 0, b.closest('[data-evidence-set]')); }
    return;
  }
  if ((b = t.closest('[data-preparation-crop]'))) { var idx = IMAGES.findIndex(function (i) { return i.id === b.getAttribute('data-preparation-crop-occurrence'); }); openViewer(Math.max(idx, 0), $('imageGrid')); viewer.cropping = true; showViewer(); return; }
  if ((b = t.closest('[data-viewer-close]'))) { closeViewer(); return; }
  if ((b = t.closest('[data-viewer-prev]'))) { viewer.index = (viewer.index - 1 + viewer.set.length) % viewer.set.length; showViewer(); return; }
  if ((b = t.closest('[data-viewer-next]'))) { viewer.index = (viewer.index + 1) % viewer.set.length; showViewer(); return; }
  if ((b = t.closest('[data-viewer-rotate]'))) { viewer.rotation = (viewer.rotation + 90) % 360; showViewer(); return; }
  if ((b = t.closest('[data-viewer-zoom]'))) { viewer.zoom = !viewer.zoom; showViewer(); return; }
  if ((b = t.closest('[data-viewer-crop]'))) { viewer.cropping = true; showViewer(); return; }
  if ((b = t.closest('[data-viewer-crop-cancel],[data-viewer-crop-save]'))) { viewer.cropping = false; showViewer(); if (b.hasAttribute('data-viewer-crop-save')) { UI.dirty = true; toast('Crop staged into the Case form.'); } return; }
  if ((b = t.closest('[data-viewer-strip-index]'))) { viewer.index = parseInt(b.getAttribute('data-viewer-strip-index'), 10); showViewer(); return; }
  if ((b = t.closest('[data-accept-proposal],[data-accept-all-proposals]'))) { $$('.prop[data-proposal-status="Awaiting"]').forEach(function (p) { p.setAttribute('data-proposal-status', 'Accepted'); var s = p.querySelector('.status'); s.className = 'status status--green'; s.textContent = 'Accepted'; var btn = p.querySelector('button,a.btn'); if (btn) btn.remove(); }); T.awaitingCount = '0'; $$('[data-text="awaitingCount"]').forEach(function (el) { el.textContent = '0'; }); UI.dirty = true; return; }
  if ((b = t.closest('[data-edit-finish-keep]'))) { $('edit-finish-confirm').hidden = true; return; }
  if ((b = t.closest('[data-edit-finish-discard]'))) { $('edit-finish-confirm').hidden = true; UI.dirty = false; S.edit = 'off'; apply(); return; }
  if ((b = t.closest('[data-edit-finish-save]'))) { $('edit-finish-confirm').hidden = true; UI.dirty = false; notice('Case data saved'); S.edit = 'off'; apply(); return; }
  if ((b = t.closest('[data-add-report-recipient]'))) { var host = $(b.getAttribute('data-add-report-recipient')); var input = document.createElement('input'); input.name = b.getAttribute('data-report-recipient-name'); input.type = 'email'; input.setAttribute('aria-label', 'Report recipient'); host.appendChild(input); input.focus(); return; }
  if ((b = t.closest('[data-vat-reset]'))) { $$('[data-vat-category]').forEach(function (c) { c.checked = true; }); document.querySelector('[data-vat-overridden]').hidden = true; b.hidden = true; return; }
  if ((b = t.closest('.tab-close'))) { e.preventDefault(); toast('Live: closes the record in the working set.'); return; }
  /* menus close on an outside click */
  if (!t.closest('details.menu')) { $$('details.menu[open]').forEach(function (d) { d.removeAttribute('open'); }); }
  if (t.id === 'mockCollapseBtn' || t.closest('#mockCollapseBtn')) { var m = $('mock'); var c = m.classList.toggle('is-collapsed'); $('mockCollapseLbl').textContent = c ? 'Show' : 'Hide'; return; }
  if (t.id === 'mockReset') { window.mock.reset(); return; }
  if (t.id === 'mockViewer') { openViewer(0, $('imageGrid')); return; }
});
document.addEventListener('submit', function (e) {
  var form = e.target;
  e.preventDefault();
  var submitter = e.submitter;
  if (submitter && submitter.getAttribute('data-mock-action')) { mockAction(submitter.getAttribute('data-mock-action'), submitter); return; }
  if (form.getAttribute('data-mock-action')) { mockAction(form.getAttribute('data-mock-action'), form); return; }
  var handler = (submitter && submitter.getAttribute('data-mock-handler')) || form.getAttribute('data-mock-handler') || 'Unknown';
  if (handler === 'GetValuation') { var card = form.closest('.valuation-card'); if (card) { var src = card.querySelector('h3 span').textContent; toast(src + ' is not connected', 'warning'); } return; }
  if (handler === 'CreateLinkedReplacement') { closeDialog(); S.state = 'created-in-error'; S.edit = 'off'; apply(); notice('Case QDOS26215 was created as the linked replacement.'); return; }
  if (handler === 'CreateRequestUploadLink') { closeDialog(); var host = $('caseNotices'); host.innerHTML = '<section class="panel form-panel mb-2" aria-labelledby="case-request-secret-title"><h2 id="case-request-secret-title" class="section-label">Copy this secret now</h2><p>It is shown once and is not available from case history.</p><input value="7Kp3-Q9xv-M2rt-A8bn" readonly aria-label="One-time request secret" /></section>'; toast('Upload link created.'); return; }
  if (handler === 'SaveValuation') { toast('The valuation card was recorded.'); UI.dirty = false; return; }
  if (handler === 'ApplyValuation') { toast("The Engineer's Value was applied."); return; }
  if (handler === 'SaveEstimate') { UI.dirty = false; notice('The estimate was saved.'); return; }
  if (handler === 'SetCurrentEstimate') { toast('Live: the Draft becomes Current after acceptance.'); return; }
  if (handler === 'StartMarketResearch') { toast('Researching · Aug 2026'); return; }
  if (handler === 'SendPreparedReport') { UI.sentCount++; UI.lastSentDate = '16 September 2026'; S.report = 'confirmed'; apply(); notice('The report send was accepted.'); return; }
  toast('Live: posts to the ' + handler + ' handler.');
});
document.addEventListener('change', function (e) {
  var t = e.target;
  if (t.closest('#case-record') && !t.closest('.mock')) UI.dirty = true;
  if (t.matches('[data-damage-row-severity]')) { impacts[parseInt(t.getAttribute('data-index'), 10)].severity = t.value; renderDamage(); }
  if (t.matches('[data-mark-severity]')) { MARKS[parseInt(t.getAttribute('data-index'), 10)].severity = t.value; renderDamage(); }
  if (t.matches('[data-outcome-select],[data-legal-select]')) applySettlementVisibility();
  if (t.id === 'f-settlement-claimant-vat-registered') apply();
  if (t.matches('[data-inspection-address-choice]')) { var opt = t.selectedOptions[0]; var input = document.querySelector('[data-inspection-address-input]'); if (opt && opt.getAttribute('data-address')) input.value = opt.getAttribute('data-address'); document.querySelector('[data-inspection-provider-default]').hidden = t.value !== 'ImageBasedAssessment'; }
  if (t.matches('[data-claim-source-select]')) { var notes = { cs1: 'Prefer total loss. CC report to rhs-claims@outlook.com.', cs2: 'Send reports to the hire desk only.', cs3: '' }[t.value] || ''; var cell = document.querySelector('[data-record-notes="claim-source"]'); cell.hidden = !notes; cell.querySelector('[data-record-notes-text]').textContent = notes; }
  if (t.matches('[data-valuation-input],[data-valuation-basis]')) recalcValuation();
  if (t.matches('[data-vat-category]')) { var over = $$('[data-vat-category]').some(function (c) { return !c.checked; }); document.querySelector('[data-vat-overridden]').hidden = !over; document.querySelector('[data-vat-reset]').hidden = !over; }
  if (t.matches('[data-report-switch]')) { var on = $$('[data-report-switch]:checked').map(function (c) { return c.nextElementSibling.textContent; }); document.querySelector('[data-report-content]').textContent = on.length ? on.join(' · ') : 'None'; }
  if (t.matches('[data-viewer-in-report]')) { var a = viewer.set[viewer.index]; var im = IMAGES[parseInt(a.getAttribute('data-image-index'), 10)]; if (im) { im.role = t.checked ? 'Supporting' : 'NotUsed'; renderReportImages(); showViewer(); } }
  if (t.matches('[data-preparation-role-select]')) { var im2 = IMAGES.filter(function (i) { return i.id === t.getAttribute('data-image'); })[0]; if (im2) { im2.role = t.value; renderReportImages(); } }
  if (t.matches('#mockSection')) jumpTo(t.value);
  if (t.matches('#mockDialog')) { if (t.value) { if (!openDialog(t.value)) toast('That dialog is not offered in this state.', 'warning'); } t.value = ''; }
});
document.addEventListener('input', function (e) {
  var t = e.target;
  if (t.matches('[data-damage-row-note]')) { impacts[parseInt(t.getAttribute('data-index'), 10)].note = t.value; }
  if (t.matches('[data-mark-note]')) { MARKS[parseInt(t.getAttribute('data-index'), 10)].note = t.value; }
  if (t.matches('[data-estimate-range]')) { $('ai-target-value').textContent = t.value + '%'; $('ai-target-amount').textContent = money(Math.round(2900 * t.value / 100)); }
});
document.addEventListener('keydown', function (e) {
  if (e.key === 'Escape') { if (!$('viewer').hidden) { closeViewer(); return; } if (openDialogEl) { closeDialog(); return; } $$('details.menu[open]').forEach(function (d) { d.removeAttribute('open'); }); }
  if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') { e.preventDefault(); openDialog('command-dialog'); }
  if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 's' && F.editing) { e.preventDefault(); mockAction('save'); }
});
window.addEventListener('resize', measureSticky);

/* --------------------------------------------------------- query ---- */
function applyQuery(q) {
  Object.keys(OPTIONS).forEach(function (k) { if (q.get(k)) S[k] = q.get(k); });
  if (q.get('edit') === '1') S.edit = 'on';
  if (q.get('proposal') === '1') S.proposal = '1';
  if (q.get('archived') === '1') S.archived = '1';
  if (q.get('audit') === '1') UI.auditCreated = true;
  if (q.get('estimate')) UI.estimateTab = q.get('estimate');
  if (q.get('tab')) UI.fileTab = q.get('tab');
  if (q.get('section')) UI.section = q.get('section');
  if (q.get('proposals') === 'none') S.p = allProposals(false); else if (q.get('proposals') === 'all') S.p = allProposals(true);
  if (q.get('p')) { S.p = allProposals(false); q.get('p').split(',').forEach(function (k) { if (k in S.p) S.p[k] = true; }); }
  if (!INITIAL_P) INITIAL_P = Object.assign({}, S.p);
  resetMarks();
  apply();
  if (q.get('collapse')) q.get('collapse').split(',').forEach(function (k) { var s = $('section-' + k); if (s) { s.classList.add('is-collapsed'); } }); if (q.get('collapse')) renderHeadTools();
  if (q.get('section')) jumpTo(q.get('section'));
  if (q.get('expand') === '1') { UI.expanded = true; $('section-estimate').classList.add('is-expanded'); document.body.classList.add('has-expanded'); }
  if (q.get('dialog')) openDialog(q.get('dialog'));
  if (q.get('notifications') === '1') openDialog('notifications-dialog');
  if (q.get('viewer')) openViewer(parseInt(q.get('viewer'), 10) - 1, $('imageGrid'));
  if (q.get('strip') === '0') { $('mock').classList.add('is-collapsed'); $('mockCollapseLbl').textContent = 'Show'; }
}

/*@@PROPOSALS@@*/
/* ----------------------------------------------------------- init ---- */
buildDamageSvg();
resetMarks();
bindMarkPointer();
apply();
bindObserver();
applyQuery(new URLSearchParams(location.search));
window.mock = { set: set, apply: apply, state: function () { return S; }, flags: function () { return F; }, text: function () { return T; }, ui: UI, openDialog: openDialog, closeDialog: closeDialog, jumpTo: jumpTo, openViewer: openViewer, closeViewer: closeViewer, applyQuery: applyQuery, reset: function () { S = Object.assign({}, DEFAULTS); S.p = Object.assign({}, INITIAL_P || allProposals(true)); UI.sentCount = 0; UI.cc = ['rhs-claims@outlook.com']; UI.suppPrint = false; UI.suppReason = 'estimate'; UI.uplift = false; UI.reportPane = 'report'; UI.attach = { report: true, fee: true, breakdown: false, images: false }; UI.engineer = null; UI.suppCompare = ''; var rd0 = $('f-report-date'); rd0.value = ''; rd0.parentElement.querySelector('.fv').textContent = '—'; rd0.parentElement.querySelector('.fv').classList.add('empty'); var so0 = $('f-sign-off-engineer'); so0.value = 's1'; so0.parentElement.querySelector('.fv').textContent = 'A Patterson'; wbReset(); $('f-assessment-outcome').value = 'total_loss'; $('f-assessment-legal-status').value = 'unroadworthy'; $('f-assessment-salvage-value').value = '725.00'; $('f-assessment-unroadworthy-reason').value = 'The rear lamp assemblies are inoperative and the rear impact has compromised the tailgate closure.'; IMAGES.forEach(function (im, i) { im.role = ['CloseUp', 'Overview', 'Supporting', 'NotUsed', 'NotUsed', 'NotUsed'][i]; }); UI.auditCreated = false; UI.estimateTab = 'e1'; UI.fileTab = 'documents'; UI.section = 'overview'; UI.dirty = false; impacts = IMPACTS_FIXTURE.map(function (i) { return Object.assign({}, i); }); $$('.record-section.is-collapsed').forEach(function (sec) { sec.classList.remove('is-collapsed'); }); resetMarks(); $('caseNotices').innerHTML = ''; if (UI.expanded) { UI.expanded = false; $('section-estimate').classList.remove('is-expanded'); document.body.classList.remove('has-expanded'); } closeDialog(); closeViewer(); apply(); }, impacts: function () { return impacts; }, marks: function () { return MARKS; }, resetMarks: resetMarks, areaAt: areaAt, markAreas: markAreas, wording: WORDING_API, images: IMAGES, mockAction: mockAction };
})();
