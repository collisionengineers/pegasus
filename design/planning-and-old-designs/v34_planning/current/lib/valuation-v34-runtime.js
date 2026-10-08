// v34 mockup runtime: the Valuation section as guide cards, drawn inside the
// captured Case page. Nothing here is application code. The arithmetic
// mirrors ValuationCalculationPolicy.Calculate; the recording rule mirrors
// DetailsModel.ChosenCalculation, with a card click standing for Use this
// value (item E).
(function () {
  'use strict';

  var cfg = window.valuationDesign;
  var params = new URLSearchParams(location.search);
  var host = document.getElementById('v34-frame');

  // Exact strings from CaseWorkspaceLabels.Valuation, CaseWorkspaceLabels.Editors,
  // CaseWorkspaceLabels.Report, OperatorLabels and Core's own refusal. The
  // two words the operator's screenshot brings (Selected, Manual) sit behind
  // strip switches (items B and C).
  var L = {
    engineer: "Engineer's Value", valuationMonth: 'Valuation month', getValuation: 'Get valuation',
    lookingUp: 'Looking up…', starting: 'Starting…', ai: 'AI market research',
    retail: 'Retail', trade: 'Trade', guideMonth: 'Guide month', basis: 'Basis', selected: 'Selected', manual: 'Manual',
    calculation: 'Calculation', ptl: 'Previous total loss', ded: 'Condition deduction',
    addVat: 'Add 20 % VAT', claimantVat: 'Claimant is VAT registered',
    increases: 'Value increases', other: 'Other…', applied: 'Applied', notRecorded: 'Not recorded', noneYet: 'None yet',
    researching: 'Researching',
    researchNote: 'The result is filed here without ending your edit. Save or refresh to see it.',
    onReport: 'On the report', disclose: 'Disclose guide source', commentary: 'Valuation commentary',
    unrelated: 'Unrelated damage', reportSummary: 'Guide source not disclosed',
    exceeds: 'The valuation deductions exceed the value, so there is no figure to apply.',
    reportProblem: 'report a problem'
  };
  var SOURCES = [
    { slug: 'glasses', name: "Glass's", connected: true },
    { slug: 'brego', name: 'Brego', connected: false },
    { slug: 'super-cap', name: 'Super CAP', connected: false },
    { slug: 'cap', name: 'CAP', connected: false },
    { slug: 'cazana', name: 'Cazana', connected: false }
  ];
  var PRESETS = [
    { label: 'Tow bar', amount: 300 }, { label: 'Decals', amount: 500 }, { label: 'Camper conversion', amount: 0 },
    { label: 'PCO plated', amount: 1500 }, { label: 'Driving tuition', amount: 500 }
  ];
  var MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
  var LONG_MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];

  function esc(value) {
    return String(value).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
  }
  function num(value) {
    var parsed = parseFloat(value);
    return isFinite(parsed) ? parsed : 0;
  }
  function money(value) {
    return '£' + Number(value).toLocaleString('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }
  function whole(value) {
    return value < 0 ? -Math.round(-value) : Math.round(value);
  }
  function month(value, long) {
    if (!value) { return ''; }
    var parts = value.split('-');
    return (long ? LONG_MONTHS : MONTHS)[Number(parts[1]) - 1] + ' ' + parts[0];
  }
  function icon(id) {
    return '<svg class="icon" aria-hidden="true"><use href="#icon-' + id + '" /></svg>';
  }
  function sourceName(slug) {
    if (slug === 'ai') { return L.ai; }
    for (var i = 0; i < SOURCES.length; i++) {
      if (SOURCES[i].slug === slug) { return SOURCES[i].name; }
    }
    return 'Guide';
  }

  // ---- state ---------------------------------------------------------------

  var S;
  var opt = {};

  function blankSelection() {
    var adds = PRESETS.map(function (preset) {
      return { label: preset.label, amount: String(preset.amount), on: false, preset: true };
    });
    adds.push({ label: '', amount: '', on: false, preset: false });
    adds.push({ label: '', amount: '', on: false, preset: false });
    return { ptl: '', ded: '', vat: false, adds: adds };
  }
  function copySelection(selection) {
    return {
      ptl: selection.ptl, ded: selection.ded, vat: selection.vat,
      adds: selection.adds.map(function (add) {
        return { label: add.label, amount: add.amount, on: add.on, preset: add.preset };
      })
    };
  }

  function preset(name) {
    var read = /-read$/.test(name);
    var base = name.replace(/-read$/, '');
    var s = {
      preset: name, mode: read ? 'read' : 'edit', cards: {}, ai: null, earlier: [], aiMonth: '2026-10',
      chosen: null, used: false, filled: false, sel: blankSelection(),
      engineer: '', record: null, claimantVat: false, inspection: false, busy: null,
      report: { disclose: false, commentary: false, unrelated: false }
    };
    SOURCES.forEach(function (source) { s.cards[source.slug] = { retail: '', trade: '', month: '' }; });
    if (base !== 'empty') {
      s.cards.glasses = { retail: '2950.00', trade: '2180.00', month: '2026-10' };
      s.cards.cap = { retail: '3100.00', trade: '2320.00', month: '2026-10' };
      s.cards.cazana = { retail: '2875.00', trade: '2090.00', month: '2026-10' };
      s.cards.brego = { retail: '3050.00', trade: '2260.00', month: '2026-10' };
      s.ai = { retail: '3195.00', trade: '', month: '2026-10', mileage: '84,200', recorded: '7 Oct 2026' };
      s.chosen = 'cap';
      s.engineer = '3100.00';
    }
    if (base === 'recorded') {
      s.earlier = [{ retail: '3240.00', trade: '2400.00', month: '2026-09', mileage: '84,200', recorded: '30 Sep 2026' }];
      s.sel.ptl = '10'; s.sel.ded = '200.00';
      s.sel.adds[0].on = true;
      s.engineer = '2890.00';
      s.filled = true;
      s.record = { basis: 'cap', value: 2890, by: 'alex', sel: copySelection(s.sel) };
    }
    if (base === 'own') { s.engineer = '2900.00'; }
    if (base === 'refused') { s.sel.ded = '4000.00'; }
    if (base === 'claimant-vat') { s.claimantVat = true; }
    if (base === 'pending') { s.ai = { pending: true }; s.earlier = []; }
    if (base === 'inspection') { s.inspection = true; }
    s.saved = s.engineer;
    s.opening = canonical(s);
    // A reading of "fetched" holds no recorded calculation: the box is the
    // Engineer's own figure, so the cards show no chosen one.
    return s;
  }

  function cardOf(s, slug) {
    if (slug === 'ai') { return s.ai; }
    if (/^ai-/.test(slug)) { return s.earlier[Number(slug.slice(3))]; }
    return s.cards[slug];
  }
  function canonical(s) {
    var card = s.chosen ? cardOf(s, s.chosen) : null;
    return JSON.stringify([s.chosen, card ? card.retail : null, card ? card.trade : null, s.sel.vat && !s.claimantVat, s.sel.ptl, num(s.sel.ded),
      s.sel.adds.filter(function (add) { return add.on; }).map(function (add) { return [add.label, num(add.amount)]; })]);
  }

  // The calculation over the chosen card's retail, or null with no card
  // chosen, or { error } where Core would refuse it.
  function calculate(selection, basis) {
    if (!basis) { return null; }
    var card = cardOf(S, basis);
    var retail = card && !card.pending ? num(card.retail) : 0;
    if (!(retail > 0)) { return null; }
    var vatOn = selection.vat && !S.claimantVat;
    var vat = vatOn ? whole(retail * 0.2) : 0;
    var withVat = retail + vat;
    var ptl = selection.ptl ? whole(withVat * Number(selection.ptl) / 100) : 0;
    var adds = selection.adds.filter(function (add) { return add.on; });
    var addsTotal = adds.reduce(function (sum, add) { return sum + num(add.amount); }, 0);
    var ded = num(selection.ded);
    var proposal = whole(withVat - ptl + addsTotal - ded);
    if (proposal < 0) { return { error: L.exceeds }; }
    return { retail: retail, vatOn: vatOn, vat: vat, ptlPct: selection.ptl, ptl: ptl, adds: adds, ded: ded, proposal: proposal };
  }
  function editing() { return S.mode === 'edit'; }
  function shownSelection() {
    if (editing()) { return S.sel; }
    return recordShown() ? S.record.sel : blankSelection();
  }
  // While reading, the chosen card is the recorded calculation's basis.
  function shownBasis() {
    if (editing()) { return S.chosen; }
    return recordShown() ? S.record.basis : null;
  }
  function current() { return calculate(shownSelection(), shownBasis()); }
  function recordShown() {
    return !!S.record && num(S.engineer) === S.record.value;
  }
  function boxIsCalculated() {
    var result = current();
    if (!result || result.error || num(S.engineer) !== result.proposal) { return false; }
    return editing() ? (S.filled || (recordShown() && S.record.basis === S.chosen)) : recordShown();
  }

  // ---- pieces ------------------------------------------------------------------

  function figure(slug, field, typed) {
    var card = cardOf(S, slug);
    var pending = card && card.pending;
    var value = card && !pending ? card[field] : '';
    var id = 'f-valuation-' + slug + '-' + field;
    var label = field === 'retail' ? L.retail : L.trade;
    var ed = editing() && typed;
    return '<div class="gc-fig' + (ed ? ' is-input' : '') + '">'
      + (ed ? '<label for="' + id + '">' + label + '</label>' : '<span class="lbl">' + label + '</span>')
      + (ed
        ? '<input id="' + id + '" class="fi mono" type="number" min="0" step="0.01" placeholder="£" value="' + esc(value) + '" data-bind="card.' + slug + '.' + field + '" data-valuation-' + field + ' />'
        : '<span class="gc-val mono' + (value === '' ? ' is-blank' : '') + (pending ? ' derived' : '') + '">' + (pending ? '—' : (value === '' ? '—' : money(value))) + '</span>')
      + '</div>';
  }
  function guideMonth(slug, typed) {
    var card = cardOf(S, slug);
    var value = card ? card.month : '';
    var id = 'f-valuation-' + slug + '-month';
    if (editing() && typed) {
      return '<div class="gc-fig gc-month is-input"><label for="' + id + '">' + L.guideMonth + '</label><input id="' + id + '" class="fi" type="month" value="' + esc(value) + '" data-bind="card.' + slug + '.month" data-valuation-entry-month /></div>';
    }
    return '<div class="gc-fig gc-month"><span class="lbl">' + L.guideMonth + '</span><span class="gc-val' + (value ? '' : ' is-blank') + '">' + (value ? month(value) : L.notRecorded) + '</span></div>';
  }
  function chosenWord() {
    return '<span class="gc-word" data-valuation-chosen-word>' + (opt.word === 'basis' ? L.basis : L.selected) + '</span>';
  }
  function getButton(slug, research) {
    var busy = S.busy === slug;
    return '<button type="button" class="btn btn--small" data-act="' + (research ? 'research' : 'get') + '" data-slug="' + slug + '" data-valuation-get data-valuation-source="' + (research ? 'ai-market-research' : slug) + '"' + (busy ? ' aria-busy="true" disabled' : '') + '><span>' + (busy ? (research ? L.starting : L.lookingUp) : L.getValuation) + '</span></button>';
  }
  function unavailable(source) {
    return '<div class="notice notice--info gc-note" data-valuation-notice data-valuation-not-connected><span data-valuation-unavailable>' + esc(source.name) + ' valuation is unavailable. Contact an administrator or <button type="button" class="link-button" data-act="problem">' + L.reportProblem + '</button>.</span></div>';
  }
  function pickable(slug) {
    var card = cardOf(S, slug);
    return editing() && card && !card.pending && num(card.retail) > 0;
  }

  // One guide source's card. Clicking a card with a retail chooses it.
  function sourceCard(source) {
    var slug = source.slug;
    var selected = shownBasis() === slug;
    var manualTag = !source.connected && opt.manual === 'tag';
    var head = '<div class="gc-head"><h3>' + esc(source.name) + '</h3>'
      + (manualTag ? '<span class="src-tag" data-valuation-not-connected>' + L.manual + '</span>' : '')
      + (selected ? chosenWord() : '')
      + (editing() && source.connected ? '<span class="gc-get">' + getButton(slug, false) + '</span>' : '')
      + '</div>';
    return '<div class="gc' + (selected ? ' sel' : '') + '"' + (pickable(slug) ? ' tabindex="0" role="button" aria-pressed="' + selected + '"' : '') + ' data-pick="' + slug + '" data-valuation-entry="' + slug + '">'
      + head
      + '<div class="gc-figs">' + figure(slug, 'retail', true) + figure(slug, 'trade', true) + guideMonth(slug, true) + '</div>'
      + (editing() && !source.connected && opt.manual === 'sentence' ? unavailable(source) : '')
      + '</div>';
  }

  function researchMeta(card) {
    return month(card.month) + ' · ' + card.mileage + ' miles · ' + card.recorded;
  }
  // AI market research: its figures, the month it is asked for and its own
  // Get valuation, which starts the research job. Not offered in the
  // Inspection view.
  function researchCard() {
    var card = S.ai;
    var pending = card && card.pending;
    var selected = shownBasis() === 'ai';
    var asks = editing() && !S.inspection;
    var head = '<div class="gc-head"><h3>' + L.ai + '</h3><span class="src-tag src-tag--ai">AI</span>' + (selected ? chosenWord() : '')
      + (asks ? '<span class="gc-get">' + getButton('ai', true) + '</span>' : '') + '</div>';
    var monthCell = asks
      ? '<div class="gc-fig gc-month is-input"><label for="f-valuation-month">' + L.valuationMonth + '</label><input id="f-valuation-month" class="fi" type="month" value="' + esc(S.aiMonth) + '" data-bind="aiMonth" data-valuation-month /></div>'
      : '<div class="gc-fig gc-month"><span class="lbl">' + L.guideMonth + '</span><span class="gc-val' + (card && !pending ? '' : ' is-blank') + '">' + (card && !pending ? month(card.month) : L.notRecorded) + '</span></div>';
    var foot = pending
      ? '<div class="gc-meta researching">' + L.researching + ' · ' + month(S.aiMonth, true) + '</div>' + (editing() ? '<div class="gc-meta muted" data-valuation-pending-note>' + L.researchNote + '</div>' : '')
      : (card ? '<div class="gc-meta">' + researchMeta(card) + '</div>' : '');
    return '<div class="gc gc--ai' + (selected ? ' sel' : '') + (pending ? ' is-pending' : '') + '"' + (pickable('ai') ? ' tabindex="0" role="button" aria-pressed="' + selected + '"' : '') + ' data-pick="ai" data-valuation-entry="ai">'
      + head + '<div class="gc-figs">' + figure('ai', 'retail', false) + figure('ai', 'trade', false) + monthCell + '</div>' + foot + '</div>';
  }
  // Each earlier research stays as a card of its own.
  function earlierCard(card, index) {
    var slug = 'ai-' + index;
    var selected = shownBasis() === slug;
    return '<div class="gc gc--ai' + (selected ? ' sel' : '') + '"' + (pickable(slug) ? ' tabindex="0" role="button" aria-pressed="' + selected + '"' : '') + ' data-pick="' + slug + '">'
      + '<div class="gc-head"><h3>' + L.ai + '</h3><span class="src-tag src-tag--ai">AI</span>' + (selected ? chosenWord() : '') + '</div>'
      + '<div class="gc-figs">' + figure(slug, 'retail', false) + figure(slug, 'trade', false) + '</div>'
      + '<div class="gc-meta">' + researchMeta(card) + '</div></div>';
  }
  function cards() {
    return '<div class="gcs" data-valuation-cards>' + SOURCES.map(sourceCard).join('') + researchCard() + S.earlier.map(earlierCard).join('') + '</div>';
  }

  // ---- the calculation ----------------------------------------------------------

  function amount(kind) {
    var result = current();
    if (!result || result.error) { return ''; }
    if (kind === 'vat') { return result.vatOn ? '+ ' + money(result.vat) : ''; }
    if (kind === 'ptl') { return result.ptlPct ? '− ' + money(result.ptl) : ''; }
    return '';
  }
  function amt(kind) {
    return '<span class="calc-amount" data-out="amt-' + kind + '">' + amount(kind) + '</span>';
  }
  function addRow(add, index) {
    var id = 'f-valuation-add-' + index;
    if (!editing()) {
      return '<div class="add' + (add.on ? ' on' : '') + '"><span class="add-check"' + (add.on ? '' : ' aria-hidden="true"') + '>' + (add.on ? icon('check') + '<span class="sr-only">' + L.applied + '</span>' : '') + '</span><span>' + esc(add.label) + '</span><span class="amt">' + money(num(add.amount)) + '</span></div>';
    }
    var check = '<input type="checkbox" id="' + id + '" data-bind="add.' + index + '.on" data-preset-toggle data-valuation-input' + (add.on ? ' checked' : '') + ' aria-label="' + esc(add.preset ? add.label : L.other) + '" />';
    var amountBox = '<input class="amt" type="number" min="0" step="0.01" placeholder="£" id="' + id + '-amount" value="' + esc(add.amount) + '" data-bind="add.' + index + '.amount" data-valuation-input aria-label="' + esc((add.preset ? add.label : L.other) + ' amount') + '" />';
    return add.preset
      ? '<div class="add' + (add.on ? ' on' : '') + '" data-valuation-add="' + index + '">' + check + '<label for="' + id + '">' + esc(add.label) + '</label>' + amountBox + '</div>'
      : '<div class="add add--custom' + (add.on ? ' on' : '') + '" data-valuation-add="' + index + '">' + check + '<input type="text" maxlength="200" id="' + id + '-label" placeholder="' + L.other + '" aria-label="' + L.other + '" value="' + esc(add.label) + '" data-bind="add.' + index + '.label" data-valuation-input />' + amountBox + '</div>';
  }
  // Commercial VAT as one row of the list (item F): its amount stands where
  // an increase's figure stands; a VAT-registered claimant never has it.
  function vatRow() {
    var on = shownSelection().vat && !S.claimantVat;
    if (!editing()) {
      if (!on) { return ''; }
      return '<div class="add on add--vat"><span class="add-check">' + icon('check') + '<span class="sr-only">' + L.applied + '</span></span><span>' + L.addVat + '</span><span class="amt" data-out="amt-vat">' + amount('vat') + '</span></div>';
    }
    return '<div class="add add--vat' + (on ? ' on' : '') + '" data-valuation-vat-wrap><input type="checkbox" id="f-valuation-vat" data-bind="sel.vat" data-valuation-input' + (on ? ' checked' : '') + (S.claimantVat ? ' disabled' : '') + ' />'
      + '<label for="f-valuation-vat">' + L.addVat + (S.claimantVat ? ' <span class="src-tag src-tag--warn">' + L.claimantVat + '</span>' : '') + '</label>'
      + '<span class="amt amt--figure" data-out="amt-vat">' + amount('vat') + '</span></div>';
  }
  function increases() {
    var selection = shownSelection();
    var rows = selection.adds.map(function (add, index) { return { add: add, index: index }; })
      .filter(function (item) { return editing() || item.add.on; })
      .map(function (item) { return addRow(item.add, item.index); });
    var vat = vatRow();
    // VAT follows the presets, ahead of the Other… rows.
    var presetCount = selection.adds.filter(function (add) { return add.preset && (editing() || add.on); }).length;
    if (vat) { rows.splice(presetCount, 0, vat); }
    if (!rows.length) { return ''; }
    return '<div class="sub-panel v-adds"><h3 class="adds-head" data-valuation-adds-head>' + L.increases + '</h3><div class="adds" data-valuation-adds>' + rows.join('') + '</div></div>';
  }
  // Condition deduction and Previous total loss (item G). The amount of the
  // loss stands in its label line, as live.
  function deductions() {
    var selection = shownSelection();
    var ed = editing();
    var ptl = selection.ptl;
    var ded = selection.ded;
    var dedCell = '<div class="fc' + (ed ? '' : ' ro') + ' v-ded">'
      + (ed ? '<label for="f-valuation-deduction">' + L.ded + '</label>' : '<span class="lbl">' + L.ded + '</span>')
      + '<div class="fv mono' + (num(ded) > 0 ? '' : ' empty') + '" data-valuation-deduction-read>' + (num(ded) > 0 ? money(ded) : '—') + '</div>'
      + (ed ? '<input id="f-valuation-deduction" class="fi mono" type="number" min="0" step="0.01" placeholder="£" value="' + esc(ded) + '" data-bind="sel.ded" data-valuation-input />' : '')
      + '</div>';
    // Editing, the tick box carries the words and the label line keeps only
    // the amount, so the cell stands where the reading one does.
    var ptlCell = '<div class="fc' + (ed ? '' : ' ro') + ' v-ptl"><span class="lbl"' + (ed ? ' aria-hidden="true"' : '') + '>' + (ed ? '&#8203;' : L.ptl) + amt('ptl') + '</span>'
      + (ed
        ? '<div class="v-ptl-row"><label class="choice"><input type="checkbox" id="f-valuation-ptl" data-bind="ptl.on" data-valuation-input' + (ptl ? ' checked' : '') + ' /><span>' + L.ptl + '</span></label>'
          + '<span class="case-layout-switch v-ptl-switch" role="group" aria-label="' + L.ptl + '">'
          + ['10', '20'].map(function (pct) {
            return '<button type="button" data-act="ptl" data-slug="' + pct + '" aria-pressed="' + (ptl === pct) + '"' + (ptl ? '' : ' disabled') + '>−' + pct + ' %</button>';
          }).join('') + '</span></div>'
        : '<div class="fv" data-valuation-ptl-read>' + (ptl ? '−' + ptl + ' %' : 'None') + '</div>')
      + '</div>';
    return '<div class="sub-panel v-deds"><div class="v-deds-row">' + dedCell + ptlCell + '</div></div>';
  }
  function calcNote() {
    var result = current();
    if (result && result.error) { return '<div class="notice notice--danger" role="alert" data-valuation-error>' + esc(result.error) + '</div>'; }
    if (!result && editing()) { return '<span class="muted" data-valuation-none>' + L.noneYet + '</span>'; }
    return '';
  }
  function basisName() {
    var basis = shownBasis();
    return basis ? 'from ' + esc(sourceName(basis)) + ' retail' : '';
  }
  // The recorded calculation's source as one word on the label (v33 item D).
  function engTag() {
    if (!boxIsCalculated()) { return ''; }
    var basis = shownBasis();
    return /^ai/.test(basis) ? '<span class="src-tag src-tag--ai" data-valuation-recorded-word>AI</span>' : '<span class="src-tag" data-valuation-recorded-word>' + esc(sourceName(basis)) + '</span>';
  }
  // The Engineer's Value: the one box, the calculation's source beside it.
  function engineerPanel() {
    var ed = editing();
    var value = S.engineer;
    return '<div class="v-ev" data-valuation-values><div class="fc' + (ed ? '' : ' ro') + ' v-ev-cell" data-field="assessment.values.engineer">'
      + (ed ? '<label for="f-valuation-value-engineer">' : '<span class="lbl">') + L.engineer + '<span data-out="eng-tag">' + engTag() + '</span>' + (ed ? '</label>' : '</span>')
      + '<div class="fv mono' + (value === '' ? ' empty' : '') + '">' + (value === '' ? L.notRecorded : money(value)) + '</div>'
      + (ed ? '<input id="f-valuation-value-engineer" class="fi mono" type="number" min="0" step="0.01" value="' + esc(value) + '" data-bind="engineer" data-valuation-value="engineer" />' : '')
      + '</div><div class="v-ev-calc"><span class="lbl">' + L.calculation + '</span> <span class="v-ev-basis" data-out="basis-name">' + basisName() + '</span>'
      + '<div data-out="calc-note">' + calcNote() + '</div></div></div>';
  }

  // DetailsModel.ReportContentSummary, word for word.
  function reportSummary() {
    var parts = [S.report.disclose ? 'Guide source disclosed' : L.reportSummary];
    if (S.report.commentary) { parts.push('valuation commentary'); }
    if (S.report.unrelated) { parts.push('unrelated damage'); }
    return parts.join(' · ');
  }
  function onReport() {
    var switches = [['disclose', L.disclose], ['commentary', L.commentary], ['unrelated', L.unrelated]];
    var ed = editing();
    return '<div class="v-report" data-field="report-content"><span class="lbl">' + L.onReport + '</span>'
      + (ed
        ? '<div class="switches" data-report-switches>' + switches.map(function (item) {
          return '<label class="choice"><input type="checkbox" data-bind="report.' + item[0] + '" data-report-switch="' + item[0] + '"' + (S.report[item[0]] ? ' checked' : '') + ' /><span>' + item[1] + '</span></label>';
        }).join('') + '</div>'
        : '<div class="fv" data-report-content>' + reportSummary() + '</div>')
      + '</div>';
  }

  function body() {
    return cards() + increases() + deductions() + engineerPanel() + onReport();
  }

  // ---- derived nodes patched while typing --------------------------------------

  function out(key) {
    if (key === 'eng-tag') { return engTag(); }
    if (key === 'calc-note') { return calcNote(); }
    if (key === 'basis-name') { return basisName(); }
    if (key.indexOf('amt-') === 0) { return amount(key.slice(4)); }
    return '';
  }
  function headText() {
    return L.engineer + ' ' + (S.saved === '' ? '—' : money(S.saved));
  }
  function patch() {
    var section = document.getElementById('section-valuation');
    if (!section) { return; }
    section.querySelectorAll('[data-out]').forEach(function (node) { node.innerHTML = out(node.getAttribute('data-out')); });
    var engineer = section.querySelector('[data-valuation-value="engineer"]');
    if (engineer && document.activeElement !== engineer) { engineer.value = S.engineer; }
    var head = section.querySelector('[data-valuation-head]');
    if (head) { head.textContent = headText(); }
  }

  // ---- behaviour -------------------------------------------------------------------

  // A change the calculation follows: its proposal fills the Engineer's Value
  // box at once (no Apply, 23 September 2026); where it cannot be worked out
  // the box goes back to the saved figure.
  function recalculate() {
    var result = calculate(S.sel, S.chosen);
    if (result && !result.error) {
      S.engineer = result.proposal.toFixed(2);
      S.filled = true;
    } else {
      if (S.filled) { S.engineer = S.saved; }
      S.filled = false;
      S.used = false;
    }
  }
  var commitTimer = null;
  function leaseLine(text) {
    var line = document.querySelector('[data-lease-line]');
    if (line) { line.hidden = !text; line.textContent = text || ''; }
  }
  function scheduleCommit() {
    window.clearTimeout(commitTimer);
    leaseLine('Saving…');
    commitTimer = window.setTimeout(commit, 700);
  }
  // A calculation is recorded when a card was clicked (item E) or the
  // calculator changed, and the box holds its figure.
  function commit() {
    var result = calculate(S.sel, S.chosen);
    var changed = canonical(S) !== S.opening;
    S.saved = S.engineer;
    if (S.chosen && result && !result.error && (S.used || changed)
        && (S.engineer === '' || num(S.engineer) === result.proposal)) {
      S.record = { basis: S.chosen, value: result.proposal, by: 'alex', sel: copySelection(S.sel) };
      S.engineer = result.proposal.toFixed(2);
      S.saved = S.engineer;
      S.opening = canonical(S);
    }
    S.used = false;
    leaseLine('Saved');
    patch();
  }

  function bind(path, target) {
    var parts = path.split('.');
    var value = target.type === 'checkbox' ? target.checked : target.value;
    if (parts[0] === 'engineer') { S.engineer = value; S.filled = false; S.used = false; return 'patch'; }
    if (parts[0] === 'card') {
      S.cards[parts[1]][parts[2]] = value;
      if (parts[1] === S.chosen && parts[2] !== 'month') { recalculate(); }
      return 'patch';
    }
    if (parts[0] === 'ptl') { S.sel.ptl = value ? (S.sel.ptl || '10') : ''; recalculate(); return 'render'; }
    if (parts[0] === 'sel') { S.sel[parts[1]] = value; recalculate(); return target.type === 'checkbox' ? 'render' : 'patch'; }
    if (parts[0] === 'add') {
      S.sel.adds[Number(parts[1])][parts[2]] = value;
      recalculate();
      return parts[2] === 'on' ? 'render' : 'patch';
    }
    if (parts[0] === 'report') { S.report[parts[1]] = value; return 'none'; }
    if (parts[0] === 'aiMonth') { S.aiMonth = value; return 'none'; }
    return 'none';
  }

  function choose(slug) {
    if (!pickable(slug)) { return; }
    S.chosen = slug;
    S.used = true;
    recalculate();
    render();
    scheduleCommit();
  }

  function act(name, slug) {
    if (name === 'ptl') { S.sel.ptl = slug; recalculate(); render(); scheduleCommit(); return; }
    if (name === 'get') {
      // The connected source answers fixed demo figures after a moment.
      S.busy = slug;
      render();
      window.setTimeout(function () {
        S.busy = null;
        S.cards[slug] = { retail: '2950.00', trade: '2180.00', month: S.cards[slug].month || '2026-10' };
        if (S.chosen === slug) { recalculate(); }
        render();
        scheduleCommit();
      }, 400);
      return;
    }
    if (name === 'research') {
      if (S.ai && !S.ai.pending) { S.earlier.unshift(S.ai); }
      if (S.chosen === 'ai') { S.chosen = null; recalculate(); }
      S.ai = { pending: true };
      render();
      return;
    }
    if (name === 'problem') { boundary('Report a problem', 'problem-dialog'); }
  }
  function boundary(label, route) {
    var region = document.getElementById('v34-live');
    if (region) { region.textContent = label + ': ' + route + ' (not part of this mockup)'; }
  }

  // ---- mounting ------------------------------------------------------------------

  function mount() {
    var template = document.getElementById(S.mode === 'edit' ? 'tpl-frame-edit' : 'tpl-frame-read');
    host.innerHTML = template.innerHTML;
    var save = host.querySelector('[data-case-save-now]');
    if (save) { save.hidden = true; }
    var section = host.querySelector('#section-valuation');
    section.setAttribute('data-design', 'v34');
    section.classList.remove('is-collapsed');
    render();
  }
  function render() {
    var section = document.getElementById('section-valuation');
    if (!section) { return; }
    var active = document.activeElement && section.contains(document.activeElement) ? document.activeElement.id : null;
    section.querySelector('.panel-body').innerHTML = body();
    var head = section.querySelector('[data-valuation-head]');
    if (head) { head.textContent = headText(); }
    if (active) {
      var again = document.getElementById(active);
      if (again) { again.focus(); }
    }
  }
  function setMode(mode) {
    window.clearTimeout(commitTimer);
    if (S.mode === 'edit') { commit(); }
    S.mode = mode;
    if (mode === 'edit') {
      if (recordShown()) { S.sel = copySelection(S.record.sel); S.chosen = S.record.basis; }
      S.chosen = S.chosen || firstBasis();
      S.opening = canonical(S);
    }
    mount();
    syncStrip();
  }
  function firstBasis() {
    for (var i = 0; i < SOURCES.length; i++) {
      if (num(S.cards[SOURCES[i].slug].retail) > 0) { return SOURCES[i].slug; }
    }
    return null;
  }
  function load(name) {
    window.clearTimeout(commitTimer);
    S = preset(name);
    mount();
    syncStrip();
  }

  // ---- events ----------------------------------------------------------------------

  host.addEventListener('input', function (event) {
    var target = event.target;
    var path = target.getAttribute && target.getAttribute('data-bind');
    if (!path || target.type === 'checkbox') { return; }
    if (bind(path, target) !== 'none') { patch(); scheduleCommit(); }
  });
  host.addEventListener('change', function (event) {
    var target = event.target;
    var path = target.getAttribute && target.getAttribute('data-bind');
    if (!path || target.type !== 'checkbox') { return; }
    var todo = bind(path, target);
    if (todo === 'render') { render(); }
    if (todo !== 'none') { patch(); }
    scheduleCommit();
  });
  host.addEventListener('click', function (event) {
    var target = event.target;
    var toggle = target.closest('[data-collapse-toggle]');
    if (toggle) {
      var panel = toggle.closest('[data-collapse]');
      if (panel) { panel.classList.toggle('is-collapsed'); toggle.setAttribute('aria-expanded', panel.classList.contains('is-collapsed') ? 'false' : 'true'); }
      event.preventDefault();
      return;
    }
    if (target.closest('[data-case-done]')) { event.preventDefault(); setMode('read'); return; }
    if (target.closest('[data-case-edit], [data-section-edit]')) { event.preventDefault(); setMode('edit'); return; }
    var button = target.closest('[data-act]');
    if (button) { event.preventDefault(); act(button.getAttribute('data-act'), button.getAttribute('data-slug')); return; }
    var pick = target.closest('[data-pick]');
    if (pick && editing() && !target.closest('input,button,select,label,a')) {
      if (pick.getAttribute('data-pick') !== S.chosen) { choose(pick.getAttribute('data-pick')); }
      return;
    }
    var link = target.closest('a[href]');
    if (link) { event.preventDefault(); }
  });
  host.addEventListener('keydown', function (event) {
    var pick = event.target.getAttribute && event.target.getAttribute('data-pick');
    if (!pick || !editing() || (event.key !== 'Enter' && event.key !== ' ')) { return; }
    event.preventDefault();
    if (pick !== S.chosen) { choose(pick); }
  });
  host.addEventListener('submit', function (event) { event.preventDefault(); });

  // ---- mockup strip ----------------------------------------------------------------

  function syncStrip() {
    var state = document.querySelector('[data-state-picker]');
    if (state) { state.value = S.preset; }
    document.querySelectorAll('[data-option-picker]').forEach(function (picker) {
      picker.value = opt[picker.getAttribute('data-option-picker')];
    });
    document.documentElement.setAttribute('data-v34-state', S.preset);
    document.documentElement.setAttribute('data-v34-mode', S.mode);
  }
  function readOptions() {
    var stored = {};
    try { stored = JSON.parse(localStorage.getItem('v34.options') || '{}'); } catch (error) { stored = {}; }
    cfg.options.forEach(function (option) { opt[option.key] = stored[option.key] || cfg.defaults[option.key]; });
    (params.get('opt') || '').split(',').forEach(function (pair) {
      var parts = pair.split(':');
      if (parts.length === 2 && Object.prototype.hasOwnProperty.call(opt, parts[0])) { opt[parts[0]] = parts[1]; }
    });
  }
  var statePicker = document.querySelector('[data-state-picker]');
  if (statePicker) { statePicker.addEventListener('change', function () { load(statePicker.value); }); }
  document.querySelectorAll('[data-option-picker]').forEach(function (picker) {
    picker.addEventListener('change', function () {
      opt[picker.getAttribute('data-option-picker')] = picker.value;
      try { localStorage.setItem('v34.options', JSON.stringify(opt)); } catch (error) { /* file:// may refuse storage */ }
      render(); patch();
    });
  });
  var reset = document.querySelector('[data-strip-reset]');
  if (reset) {
    reset.addEventListener('click', function () {
      try { localStorage.removeItem('v34.options'); } catch (error) { /* ignore */ }
    });
  }

  readOptions();
  var wanted = params.get('state') || 'fetched';
  var known = cfg.presets.some(function (item) { return item[0] === wanted; });
  load(known ? wanted : cfg.presets[0][0]);
  window.v34 = { state: function () { return S; }, options: function () { return opt; } };
})();
