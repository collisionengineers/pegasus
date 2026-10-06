// v33 mockup runtime: one synthetic Case, the Valuation section drawn five
// ways inside the captured Case page. Nothing here is application code. The
// arithmetic mirrors ValuationCalculationPolicy.Calculate so the figures
// move as they would; the recording rule mirrors DetailsModel.ChosenCalculation.
(function () {
  'use strict';

  var cfg = window.valuationDesign;
  var params = new URLSearchParams(location.search);
  var host = document.getElementById('v33-frame');
  var live = cfg.id === 'live';

  // Exact strings from CaseWorkspaceLabels.Valuation, CaseWorkspaceLabels.Editors,
  // CaseWorkspaceLabels.Report, OperatorLabels and Core's own refusal.
  var L = {
    retail: 'Retail value', trade: 'Trade value', engineer: "Engineer's Value",
    valuationMonth: 'Valuation month', getValuation: 'Get valuation', ai: 'AI market research',
    cardRetail: 'Retail', cardTrade: 'Trade', guideMonth: 'Guide month',
    use: 'Use this value', using: 'Using this value', basis: 'Basis',
    calculation: 'Calculation', ptl: 'Previous total loss', ded: 'Condition deduction',
    vat: 'Commercial VAT', addVat: 'Add 20 % VAT', claimantVat: 'Claimant is VAT registered',
    increases: 'Value increases', other: 'Other…', guideRetail: 'Guide retail',
    appliedBy: 'Applied by', adjustments: 'Adjustments', none: 'None', applied: 'Applied',
    notApplied: 'Not applied', notRecorded: 'Not recorded', noneYet: 'None yet',
    researching: 'Researching',
    researchNote: 'The result is filed here without ending your edit. Save or refresh to see it.',
    onReport: 'On the report', disclose: 'Disclose guide source', commentary: 'Valuation commentary',
    unrelated: 'Unrelated damage', reportSummary: 'Guide source not disclosed',
    needsRetail: 'Enter the retail value on this card to use it.',
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

  function blankSelection(presets) {
    var adds = (presets || []).map(function (preset) {
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
      preset: name, mode: read ? 'read' : 'edit', cards: {}, ai: null, aiMonth: '2026-10',
      chosen: null, used: false, filled: false, presets: [], sel: null,
      boxes: { retail: '', trade: '', engineer: '' }, record: null,
      claimantVat: false, inspection: false, notices: {},
      report: { disclose: false, commentary: false, unrelated: false }
    };
    SOURCES.forEach(function (source) { s.cards[source.slug] = { retail: '', trade: '', month: '' }; });
    if (base !== 'empty') {
      s.cards.glasses = { retail: '9064.00', trade: '7347.00', month: '2026-10' };
      s.boxes = { retail: '9064.00', trade: '7347.00', engineer: '9064.00' };
      s.chosen = 'glasses';
    }
    if (base === 'recorded') {
      s.cards.glasses = { retail: '12500.00', trade: '10250.00', month: '2026-10' };
      s.ai = { retail: '12900.00', trade: '10400.00', month: '2026-10', recorded: '6 Oct 2026' };
      s.presets = [{ label: 'Tow bar', amount: 150 }, { label: 'Roof bars', amount: 90 }];
      s.sel = blankSelection(s.presets);
      s.sel.vat = true; s.sel.ptl = '10'; s.sel.ded = '250.00';
      s.sel.adds[0].on = true; s.sel.adds[0].amount = '175';
      s.boxes = { retail: '12500.00', trade: '10250.00', engineer: '13425.00' };
      s.filled = true;
      s.record = { basis: 'glasses', value: 13425, retail: 12500, by: 'alex', at: '06 Oct 2026 11:30', sel: copySelection(s.sel) };
    }
    if (!s.sel) { s.sel = blankSelection(s.presets); }
    if (base === 'own') { s.boxes.engineer = '8750.00'; }
    if (base === 'refused') { s.sel.ded = '12000.00'; }
    if (base === 'claimant-vat') { s.claimantVat = true; }
    if (base === 'pending') { s.ai = { pending: true }; }
    if (base === 'inspection') { s.inspection = true; }
    s.saved = { engineer: s.boxes.engineer };
    s.opening = canonical(s);
    return s;
  }

  function cardOf(s, slug) {
    return slug === 'ai' ? s.ai : s.cards[slug];
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
    var retail = card ? num(card.retail) : 0;
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
  // What the section shows: the calculator while editing, the recorded
  // calculation while reading.
  function shownSelection() {
    if (editing()) { return S.sel; }
    return recordShown() ? S.record.sel : blankSelection(S.presets);
  }
  function shownBasis() {
    if (editing()) { return S.chosen; }
    return recordShown() ? S.record.basis : null;
  }
  function current() { return calculate(shownSelection(), shownBasis()); }
  // The record is shown only while the Engineer's Value box holds its figure.
  function recordShown() {
    return !!S.record && num(S.boxes.engineer) === S.record.value;
  }
  // The box holds the calculation: it was filled from it in this edit, or it
  // is the recorded one.
  function boxIsCalculated() {
    var result = current();
    if (!result || result.error || num(S.boxes.engineer) !== result.proposal) { return false; }
    return editing() ? (S.filled || (recordShown() && S.record.basis === S.chosen)) : recordShown();
  }

  // ---- shared pieces ---------------------------------------------------------

  function cell(o) {
    var ed = editing() && !o.ro;
    var label = '';
    if (!o.nolabel) {
      var open = ed && o.id ? '<label for="' + o.id + '">' : '<span class="lbl">';
      label = open + esc(o.label) + (o.extra || '') + (ed && o.id ? '</label>' : '</span>');
    }
    return '<div class="fc' + (ed ? '' : ' ro') + (o.cls ? ' ' + o.cls : '') + '"' + (o.attr || '') + '>' + label
      + '<div class="fv' + (o.mono ? ' mono' : '') + (o.empty ? ' empty' : '') + '"' + (o.out ? ' data-out="' + o.out + '"' : '') + '>' + o.display + '</div>'
      + (ed ? o.control : '') + '</div>';
  }
  function moneyOr(value) { return value === '' || value == null ? L.notRecorded : money(value); }

  function box(kind) {
    var value = S.boxes[kind];
    var id = 'f-valuation-value-' + kind;
    return cell({
      id: id, label: L[kind], mono: true, empty: value === '', display: moneyOr(value),
      extra: kind === 'engineer' ? '<span data-out="eng-tag">' + out('eng-tag') + '</span>' : '',
      attr: ' data-field="assessment.values.' + kind + '"',
      control: '<input id="' + id + '" class="fi mono" type="number" min="0" step="0.01" value="' + esc(value) + '" data-bind="box.' + kind + '" data-valuation-value="' + kind + '" />'
    });
  }
  function boxes() {
    return '<div class="fg g3" data-valuation-values>' + box('retail') + box('trade') + box('engineer') + '</div>';
  }

  function useButton(slug) {
    var on = S.used && S.chosen === slug;
    return '<button type="button" class="btn btn--small" data-act="use" data-slug="' + slug + '" data-valuation-use aria-pressed="' + (on ? 'true' : 'false') + '"><span>' + (on ? L.using : L.use) + '</span></button>';
  }
  function getButton(slug) {
    return '<button type="button" class="btn btn--small" data-act="get" data-slug="' + slug + '" data-valuation-get data-valuation-source="' + slug + '"><span>' + L.getValuation + '</span></button>';
  }
  function unavailable(source) {
    return '<span data-valuation-unavailable>' + esc(source.name) + ' valuation is unavailable. Contact an administrator or <button type="button" class="link-button" data-act="problem">' + L.reportProblem + '</button>.</span>';
  }
  // The card's notice, as live: standing where no provider is connected,
  // otherwise only the answer to a press.
  function sourceNotice(source) {
    var said = S.notices[source.slug];
    if (said) { return '<div class="notice notice--danger" role="alert" data-valuation-notice><span>' + esc(said) + '</span></div>'; }
    if (!source.connected) { return '<div class="notice notice--info" data-valuation-notice data-valuation-not-connected>' + unavailable(source) + '</div>'; }
    return '';
  }
  function figure(slug, field, label, id, hook) {
    var card = S.cards[slug];
    var value = card[field];
    return cell({
      id: id, label: label, nolabel: !label, mono: true, empty: value === '', display: moneyOr(value),
      control: '<input id="' + id + '" class="fi mono" type="number" min="0" step="0.01" value="' + esc(value) + '" data-bind="card.' + slug + '.' + field + '" ' + hook + (label ? '' : ' aria-label="' + esc(sourceName(slug) + ' ' + (field === 'retail' ? L.cardRetail : L.cardTrade)) + '"') + ' />'
    });
  }
  function guideMonth(slug, label, id) {
    var value = S.cards[slug].month;
    return cell({
      id: id, label: label, nolabel: !label, empty: value === '', display: value === '' ? L.notRecorded : month(value),
      control: '<input id="' + id + '" class="fi" type="month" value="' + esc(value) + '" data-bind="card.' + slug + '.month" data-valuation-entry-month' + (label ? '' : ' aria-label="' + esc(sourceName(slug) + ' ' + L.guideMonth) + '"') + ' />'
    });
  }

  // One source as the live card.
  function sourceCard(source) {
    var slug = source.slug;
    var selected = shownBasis() === slug;
    var canBeBasis = num(S.cards[slug].retail) > 0;
    var prefix = 'f-valuation-' + slug + '-';
    return '<div class="valuation-card entry' + (selected ? ' sel' : '') + '"' + (editing() && canBeBasis ? ' tabindex="0"' : '') + ' data-pick="' + slug + '" data-valuation-entry="' + slug + '">'
      + '<h3><span>' + esc(source.name) + '</span></h3>'
      + '<div class="figs fg g2">' + figure(slug, 'retail', L.cardRetail, prefix + 'retail', 'data-valuation-retail') + figure(slug, 'trade', L.cardTrade, prefix + 'trade', 'data-valuation-trade') + '</div>'
      + '<div class="meta fg g2">' + guideMonth(slug, L.guideMonth, prefix + 'month') + '</div>'
      + (editing() ? sourceNotice(source) + '<div class="entry-actions">' + (source.connected ? getButton(slug) : '') + useButton(slug) + '</div>' : '')
      + '</div>';
  }

  // One source as a row: the same three boxes and the same two buttons.
  // The provider's part (Get valuation, or the notice that there is none)
  // and Use this value are separate cells, so each layout places them.
  function sourceRow(source) {
    var slug = source.slug;
    var selected = shownBasis() === slug;
    var canBeBasis = num(S.cards[slug].retail) > 0;
    var prefix = 'f-valuation-' + slug + '-';
    var notice = editing() ? sourceNotice(source) : '';
    return '<div class="v-src' + (selected ? ' sel' : '') + '"' + (editing() && canBeBasis ? ' tabindex="0"' : '') + ' data-pick="' + slug + '" data-valuation-entry="' + slug + '">'
      + '<div class="v-src-name">' + esc(source.name) + '</div>'
      + figure(slug, 'retail', '', prefix + 'retail', 'data-valuation-retail')
      + figure(slug, 'trade', '', prefix + 'trade', 'data-valuation-trade')
      + guideMonth(slug, '', prefix + 'month')
      + (editing() ? (source.connected ? '<div class="v-src-get">' + getButton(slug) + '</div>' : '') + '<div class="v-src-use">' + useButton(slug) + '</div>' : '')
      + (notice ? '<div class="v-src-note">' + notice + '</div>' : '')
      + '</div>';
  }
  function sourceRowsHead() {
    return '<div class="v-src v-src--head" aria-hidden="true"><span></span><span class="lbl">' + L.cardRetail + '</span><span class="lbl">' + L.cardTrade + '</span><span class="lbl">' + L.guideMonth + '</span><span></span></div>';
  }

  // AI market research. "tools" is today's row above the sources; "card" and
  // "row" give it the same place as a guide source.
  function researchControls() {
    return '<div class="fc val-month"><label for="f-valuation-month">' + L.valuationMonth + '</label><input id="f-valuation-month" class="fi" type="month" value="' + esc(S.aiMonth) + '" data-bind="aiMonth" data-valuation-month /></div>';
  }
  function researchButton(label) {
    return '<button type="button" class="btn btn--small" data-act="research" data-valuation-source="ai-market-research">' + (label === L.ai ? icon('sparkles') : '') + '<span>' + label + '</span></button>';
  }
  function aiTools() {
    if (!editing() || S.inspection) { return ''; }
    return '<div class="val-tools" data-valuation-tools>' + researchControls() + '<span class="val-get"><span class="lbl">' + L.getValuation + '</span>' + researchButton(L.ai) + '</span></div>';
  }
  function aiMeta() {
    return month(S.ai.month) + ' · 42,000 miles · ' + S.ai.recorded;
  }
  function aiHead() {
    return '<span>' + L.ai + '</span> <span class="src-tag src-tag--ai">AI</span>';
  }
  // Today's AI cards (recorded, or pending), shown only when there is one.
  function aiLiveCard() {
    if (!S.ai) { return ''; }
    if (S.ai.pending) {
      return '<div class="valuation-card is-pending"><h3>' + aiHead() + '</h3><div class="figs fg g2">'
        + '<div class="fc ro"><span class="lbl">' + L.cardRetail + '</span><div class="fv mono derived">—</div></div>'
        + '<div class="fc ro"><span class="lbl">' + L.cardTrade + '</span><div class="fv mono derived">—</div></div></div>'
        + '<div class="meta">' + L.researching + ' · ' + month(S.aiMonth, true) + '</div>'
        + (editing() ? '<div class="muted">' + L.researchNote + '</div>' : '') + '</div>';
    }
    var selected = shownBasis() === 'ai';
    return '<div class="valuation-card' + (selected ? ' sel' : '') + '"' + (editing() ? ' tabindex="0"' : '') + ' data-pick="ai">'
      + '<h3>' + aiHead() + (editing() ? (S.inspection ? '' : '<button type="button" class="btn btn--small btn--ghost" data-act="research"><span>' + L.getValuation + '</span></button>') + useButton('ai') : '') + '</h3>'
      + '<div class="figs fg g2"><div class="fc ro"><span class="lbl">' + L.cardRetail + '</span><div class="fv mono">' + money(S.ai.retail) + '</div></div>'
      + '<div class="fc ro"><span class="lbl">' + L.cardTrade + '</span><div class="fv mono">' + money(S.ai.trade) + '</div></div></div>'
      + '<div class="meta">' + aiMeta() + '</div></div>';
  }
  function aiFigure(field) {
    var pending = S.ai && S.ai.pending;
    var value = S.ai && !pending ? S.ai[field] : '';
    return '<div class="fv mono' + (pending ? ' derived' : (value === '' ? ' empty' : '')) + '">' + (pending ? '—' : moneyOr(value)) + '</div>';
  }
  // AI market research with its own standing place: figures, the month it
  // is asked for, and its one Get valuation.
  function aiOwnCard() {
    var pending = S.ai && S.ai.pending;
    var selected = shownBasis() === 'ai';
    var canAsk = editing() && !S.inspection && !pending;
    return '<div class="valuation-card entry v-ai' + (selected ? ' sel' : '') + (pending ? ' is-pending' : '') + '"' + (editing() && S.ai && !pending ? ' tabindex="0"' : '') + ' data-pick="ai" data-valuation-entry="ai">'
      + '<h3>' + aiHead() + '</h3>'
      + '<div class="figs fg g2"><div class="fc ro"><span class="lbl">' + L.cardRetail + '</span>' + aiFigure('retail') + '</div><div class="fc ro"><span class="lbl">' + L.cardTrade + '</span>' + aiFigure('trade') + '</div></div>'
      + (pending ? '<div class="meta">' + L.researching + ' · ' + month(S.aiMonth, true) + '</div>' + (editing() ? '<div class="muted">' + L.researchNote + '</div>' : '')
        : '<div class="meta fg g2">' + (canAsk ? researchControls() : '<div class="fc ro"><span class="lbl">' + L.valuationMonth + '</span><div class="fv' + (S.ai ? '' : ' empty') + '">' + (S.ai ? month(S.ai.month) : L.notRecorded) + '</div></div>') + '</div>')
      + (editing() && !pending ? '<div class="entry-actions">' + (canAsk ? researchButton(L.getValuation) : '') + (S.ai ? useButton('ai') : '') + '</div>' : '')
      + '</div>';
  }
  function aiOwnRow() {
    var pending = S.ai && S.ai.pending;
    var selected = shownBasis() === 'ai';
    var canAsk = editing() && !S.inspection && !pending;
    var monthCell = pending
      ? '<div class="fc ro"><div class="fv derived">' + L.researching + ' · ' + month(S.aiMonth, true) + '</div></div>'
      : (canAsk
        ? '<div class="fc"><input id="f-valuation-month" class="fi" type="month" value="' + esc(S.aiMonth) + '" data-bind="aiMonth" data-valuation-month aria-label="' + L.valuationMonth + '" /></div>'
        : '<div class="fc ro"><div class="fv' + (S.ai ? '' : ' empty') + '">' + (S.ai ? month(S.ai.month) : L.notRecorded) + '</div></div>');
    return '<div class="v-src v-src--ai' + (selected ? ' sel' : '') + (pending ? ' is-pending' : '') + '"' + (editing() && S.ai && !pending ? ' tabindex="0"' : '') + ' data-pick="ai" data-valuation-entry="ai">'
      + '<div class="v-src-name">' + aiHead() + '</div>'
      + '<div class="fc ro">' + aiFigure('retail') + '</div><div class="fc ro">' + aiFigure('trade') + '</div>' + monthCell
      + (canAsk ? '<div class="v-src-get">' + researchButton(L.getValuation) + '</div>' : '')
      + (editing() && S.ai && !pending ? '<div class="v-src-use">' + useButton('ai') + '</div>' : '')
      + (editing() && pending ? '<div class="v-src-note"><span class="muted">' + L.researchNote + '</span></div>' : '')
      + '</div>';
  }

  function cards() {
    return '<div class="guides" data-valuation-cards>' + SOURCES.map(sourceCard).join('') + (opt.ai === 'own' ? aiOwnCard() : aiLiveCard()) + '</div>';
  }
  function rows(variant) {
    return '<div class="v-srcs v-srcs--' + variant + '" data-valuation-cards>' + sourceRowsHead() + SOURCES.map(sourceRow).join('') + (opt.ai === 'own' ? aiOwnRow() : '') + '</div>'
      + (opt.ai === 'own' ? '' : (aiLiveCard() ? '<div class="guides v-ai-live">' + aiLiveCard() + '</div>' : ''));
  }
  function tools() { return opt.ai === 'own' ? '' : aiTools(); }

  // ---- calculation pieces ----------------------------------------------------

  function basisName() {
    var basis = shownBasis();
    return basis ? 'from ' + esc(sourceName(basis)) + ' retail' : '';
  }
  function amount(kind) {
    var result = current();
    if (!result || result.error) { return ''; }
    if (kind === 'retail') { return money(result.retail); }
    if (kind === 'vat') { return result.vatOn ? '+ ' + money(result.vat) : ''; }
    if (kind === 'ptl') { return result.ptlPct ? '− ' + money(result.ptl) : ''; }
    if (kind === 'ded') { return result.ded > 0 ? '− ' + money(result.ded) : ''; }
    return '';
  }
  function amt(kind) {
    return '<span class="v-amt" data-out="amt-' + kind + '">' + amount(kind) + '</span>';
  }
  function ptlCell(withAmount) {
    var value = shownSelection().ptl;
    return cell({
      id: 'f-valuation-ptl', label: L.ptl, extra: withAmount ? amt('ptl') : '', display: value ? '−' + value + ' %' : L.none,
      control: '<select id="f-valuation-ptl" class="fi" data-bind="sel.ptl" data-valuation-input>'
        + ['', '10', '20'].map(function (o) { return '<option value="' + o + '"' + (o === value ? ' selected' : '') + '>' + (o ? '−' + o + ' %' : L.none) + '</option>'; }).join('') + '</select>'
    });
  }
  function dedCell() {
    var value = shownSelection().ded;
    return cell({
      id: 'f-valuation-deduction', label: L.ded, mono: true, empty: !(num(value) > 0), display: num(value) > 0 ? money(value) : '—',
      control: '<input id="f-valuation-deduction" class="fi mono" type="number" min="0" step="0.01" placeholder="£" value="' + esc(value) + '" data-bind="sel.ded" data-valuation-input />'
    });
  }
  function vatCell(withAmount) {
    var on = shownSelection().vat && !S.claimantVat;
    var ed = editing();
    return '<div class="fc' + (ed ? '' : ' ro') + '"><span class="lbl">' + L.vat + (withAmount ? amt('vat') : '') + '</span>'
      + '<div class="fv">' + (on ? L.applied : L.notApplied) + '</div>'
      + (ed ? '<label class="fi chk choice" data-valuation-vat-wrap><input type="checkbox" id="f-valuation-vat" data-bind="sel.vat" data-valuation-input' + (on ? ' checked' : '') + (S.claimantVat ? ' disabled' : '') + ' /><span>' + L.addVat + '</span>'
        + (S.claimantVat ? '<span class="src-tag src-tag--warn">' + L.claimantVat + '</span>' : '') + '</label>' : '')
      + '</div>';
  }
  function addRows() {
    var selection = shownSelection();
    if (!editing()) {
      return selection.adds.filter(function (add) { return add.preset || add.on; }).map(function (add) {
        return '<div class="add' + (add.on ? ' on' : '') + '"><span class="add-check"' + (add.on ? '' : ' aria-hidden="true"') + '>' + (add.on ? icon('check') + '<span class="sr-only">' + L.applied + '</span>' : '') + '</span><span>' + esc(add.label) + '</span><span class="amt">' + money(num(add.amount)) + '</span></div>';
      }).join('');
    }
    return selection.adds.map(function (add, index) {
      var id = 'f-valuation-add-' + index;
      var check = '<input type="checkbox" id="' + id + '" data-bind="add.' + index + '.on" data-preset-toggle data-valuation-input' + (add.on ? ' checked' : '') + ' aria-label="' + esc(add.preset ? add.label : L.other) + '" />';
      var amountBox = '<input class="amt" type="number" min="0" step="0.01" placeholder="£" id="' + id + '-amount" value="' + esc(add.amount) + '" data-bind="add.' + index + '.amount" data-valuation-input aria-label="' + esc((add.preset ? add.label : L.other) + ' amount') + '" />';
      return add.preset
        ? '<div class="add' + (add.on ? ' on' : '') + '" data-valuation-add="' + index + '">' + check + '<label for="' + id + '">' + esc(add.label) + '</label>' + amountBox + '</div>'
        : '<div class="add add--custom' + (add.on ? ' on' : '') + '" data-valuation-add="' + index + '">' + check + '<input type="text" maxlength="200" id="' + id + '-label" placeholder="' + L.other + '" aria-label="' + L.other + '" value="' + esc(add.label) + '" data-bind="add.' + index + '.label" data-valuation-input />' + amountBox + '</div>';
    }).join('');
  }
  function hasIncreases() {
    return editing() || shownSelection().adds.some(function (add) { return add.preset || add.on; });
  }
  function increases() {
    return '<h3 class="adds-head' + (hasIncreases() ? '' : ' none') + '" data-valuation-adds-head>' + L.increases + '</h3><div class="adds" data-valuation-adds>' + addRows() + '</div>';
  }
  // Core's own reason when the calculation cannot be worked out; "None yet"
  // only when no card is chosen (FRD-24).
  function calcNote() {
    var result = current();
    if (result && result.error) { return '<div class="notice notice--danger" role="alert" data-valuation-error>' + esc(result.error) + '</div>'; }
    if (!result && editing()) { return '<div class="v-none muted">' + L.noneYet + '</div>'; }
    return '';
  }
  function linesBody(withTotal) {
    var result = current();
    if (!result || result.error) { return ''; }
    var html = '<div class="ln"><span>' + L.guideRetail + '</span><b>' + money(result.retail) + '</b></div>';
    if (result.vatOn) { html += '<div class="ln"><span>' + L.vat + ' 20 %</span><b>+ ' + money(result.vat) + '</b></div>'; }
    if (result.ptlPct) { html += '<div class="ln"><span>' + L.ptl + ' −' + result.ptlPct + ' %</span><b>− ' + money(result.ptl) + '</b></div>'; }
    result.adds.forEach(function (add) { html += '<div class="ln"><span>' + esc(add.label || L.other) + '</span><b>+ ' + money(num(add.amount)) + '</b></div>'; });
    if (result.ded > 0) { html += '<div class="ln"><span>' + L.ded + '</span><b>− ' + money(result.ded) + '</b></div>'; }
    return html;
  }
  function adjustments(selection) {
    var parts = [];
    if (selection.vat) { parts.push('+ VAT'); }
    if (selection.ptl) { parts.push('− ' + selection.ptl + '% PTL'); }
    selection.adds.forEach(function (add) { if (add.on) { parts.push('+ ' + esc(add.label || L.other)); } });
    if (num(selection.ded) > 0) { parts.push('− condition'); }
    return parts.length ? parts.join(', ') : L.none;
  }
  // The recorded calculation, in the live block's own three facts. Drawn
  // only while there is one and the box holds its figure.
  function recordLine() {
    if (opt.record !== 'line' || !recordShown()) { return ''; }
    var record = S.record;
    return '<dl class="definition-list v-record" data-valuation-history>'
      + '<div class="definition"><dt>' + L.basis + '</dt><dd>' + esc(sourceName(record.basis)) + ' retail ' + money(record.retail) + '</dd></div>'
      + '<div class="definition"><dt>' + L.appliedBy + '</dt><dd>' + esc(record.by) + ' · ' + record.at + '</dd></div>'
      + '<div class="definition"><dt>' + L.adjustments + '</dt><dd>' + adjustments(record.sel) + '</dd></div></dl>';
  }
  function engTag() {
    if (opt.record !== 'tag' || !boxIsCalculated()) { return ''; }
    var basis = shownBasis();
    return basis === 'ai' ? '<span class="src-tag src-tag--ai">AI</span>' : '<span class="src-tag">' + esc(sourceName(basis)) + '</span>';
  }

  function onReport() {
    var switches = [['disclose', L.disclose], ['commentary', L.commentary], ['unrelated', L.unrelated]];
    return '<div class="sub-panel v-report" data-collapse="case.valuation.report" data-field="report-content"><h3>' + L.onReport
      + ' <button type="button" class="icon-button panel-collapse" data-collapse-toggle aria-expanded="true" aria-label="Collapse section" title="Collapse section">' + icon('chevron-down') + '</button></h3>'
      + '<div class="fc' + (editing() ? '' : ' ro') + '"><div class="fv" data-report-content>' + L.reportSummary + '</div>'
      + (editing() ? '<div class="fi chk switches" data-report-switches>' + switches.map(function (item) {
        return '<label class="choice"><input type="checkbox" data-bind="report.' + item[0] + '" data-report-switch="' + item[0] + '"' + (S.report[item[0]] ? ' checked' : '') + ' /><span>' + item[1] + '</span></label>';
      }).join('') + '</div>' : '')
      + '</div></div>';
  }

  // ---- the five designs --------------------------------------------------------

  // The calculation as one strip of three cells, each amount in its label line.
  function calcStrip() {
    return '<div class="sub-panel calc-inputs v-calc" data-valuation-calc><h3>' + L.calculation + ' <span class="rt" data-out="basis-name">' + basisName() + '</span></h3>'
      + '<div class="fg g3">' + ptlCell(true) + dedCell() + vatCell(true) + '</div>'
      + increases()
      + '<div data-out="calc-note">' + calcNote() + '</div>'
      + '<div data-out="record">' + recordLine() + '</div></div>';
  }

  var designs = {
    // A: the three boxes, the calculation directly beneath them, then the
    // sources as today.
    a: function () {
      return boxes() + calcStrip() + tools() + cards() + onReport();
    },
    // B: sources first, then a worksheet whose total line is the three boxes.
    // Every figure stands in the right-hand column: typed ones as boxes,
    // worked-out ones as text.
    b: function () {
      var selection = shownSelection();
      var ed = editing();
      function row(label, control, figure, cls) {
        return '<div class="v-row' + (cls ? ' ' + cls : '') + '"><div class="v-row-label">' + label + '</div><div class="v-row-control">' + control + '</div><div class="v-row-amount">' + figure + '</div></div>';
      }
      var vatOn = selection.vat && !S.claimantVat;
      var vatControl = '<div class="fc' + (ed ? '' : ' ro') + '"><div class="fv">' + (vatOn ? L.applied : L.notApplied) + '</div>'
        + (ed ? '<label class="fi chk choice" data-valuation-vat-wrap><input type="checkbox" id="f-valuation-vat" data-bind="sel.vat" data-valuation-input' + (vatOn ? ' checked' : '') + (S.claimantVat ? ' disabled' : '') + ' /><span>' + L.addVat + '</span>'
          + (S.claimantVat ? '<span class="src-tag src-tag--warn">' + L.claimantVat + '</span>' : '') + '</label>' : '')
        + '</div>';
      var ptlControl = cell({
        id: 'f-valuation-ptl', nolabel: true, display: selection.ptl ? '−' + selection.ptl + ' %' : L.none,
        control: '<select id="f-valuation-ptl" class="fi" data-bind="sel.ptl" data-valuation-input aria-label="' + L.ptl + '">'
          + ['', '10', '20'].map(function (o) { return '<option value="' + o + '"' + (o === selection.ptl ? ' selected' : '') + '>' + (o ? '−' + o + ' %' : L.none) + '</option>'; }).join('') + '</select>'
      });
      var addHtml = selection.adds.map(function (add, index) { return { add: add, index: index }; })
        .filter(function (item) { return ed || item.add.preset || item.add.on; })
        .map(function (item, position) {
          var add = item.add;
          var id = 'f-valuation-add-' + item.index;
          var name = add.preset ? add.label : L.other;
          var control = ed
            ? '<div class="add' + (add.preset ? '' : ' add--custom') + (add.on ? ' on' : '') + '" data-valuation-add="' + item.index + '"><input type="checkbox" id="' + id + '" data-bind="add.' + item.index + '.on" data-preset-toggle data-valuation-input' + (add.on ? ' checked' : '') + ' aria-label="' + esc(name) + '" />'
              + (add.preset ? '<label for="' + id + '">' + esc(add.label) + '</label>' : '<input type="text" maxlength="200" id="' + id + '-label" placeholder="' + L.other + '" aria-label="' + L.other + '" value="' + esc(add.label) + '" data-bind="add.' + item.index + '.label" data-valuation-input />') + '</div>'
            : '<div class="add' + (add.on ? ' on' : '') + '"><span class="add-check"' + (add.on ? '' : ' aria-hidden="true"') + '>' + (add.on ? icon('check') + '<span class="sr-only">' + L.applied + '</span>' : '') + '</span><span>' + esc(add.label) + '</span></div>';
          var figure = cell({
            id: id + '-amount', nolabel: true, mono: true, cls: add.on ? '' : 'off', display: (add.on ? '+ ' : '') + money(num(add.amount)),
            control: '<input class="fi mono" type="number" min="0" step="0.01" placeholder="£" id="' + id + '-amount" value="' + esc(add.amount) + '" data-bind="add.' + item.index + '.amount" data-valuation-input aria-label="' + esc(name + ' amount') + '" />'
          });
          return row(position === 0 ? L.increases : '', control, figure, 'v-row--add');
        }).join('');
      var dedFigure = cell({
        id: 'f-valuation-deduction', nolabel: true, mono: true, empty: !(num(selection.ded) > 0), display: num(selection.ded) > 0 ? '− ' + money(selection.ded) : '—',
        control: '<input id="f-valuation-deduction" class="fi mono" type="number" min="0" step="0.01" placeholder="£" value="' + esc(selection.ded) + '" data-bind="sel.ded" data-valuation-input aria-label="' + L.ded + '" />'
      });
      return tools() + cards()
        + '<div class="v-sheet" data-valuation-calc><h3 class="v-sheet-head">' + L.calculation + '</h3>'
        + row(L.guideRetail, '<span class="v-basis" data-out="basis-name">' + basisName() + '</span>', '<span data-out="amt-retail">' + amount('retail') + '</span>')
        + row(L.vat, vatControl, '<span data-out="amt-vat">' + amount('vat') + '</span>')
        + row(L.ptl, ptlControl, '<span data-out="amt-ptl">' + amount('ptl') + '</span>')
        + addHtml
        + row(L.ded, '', dedFigure)
        + '<div data-out="calc-note" class="v-sheet-note">' + calcNote() + '</div>'
        + '<div class="v-total">' + boxes() + '<div data-out="record">' + recordLine() + '</div></div>'
        + '</div>' + onReport();
    },
    // C: the three boxes head three columns; sources under Retail and Trade,
    // the calculation under the Engineer's Value.
    c: function () {
      return boxes()
        + '<div class="v-cols"><div class="v-cols-left">' + tools() + rows('stack5') + '</div>'
        + '<div class="sub-panel calc-inputs v-calc v-cols-right" data-valuation-calc><h3>' + L.calculation + ' <span class="rt" data-out="basis-name">' + basisName() + '</span></h3>'
        + '<div class="fg v-one">' + ptlCell(true) + dedCell() + vatCell(true) + '</div>'
        + increases()
        + '<div data-out="calc-note">' + calcNote() + '</div>'
        + '<div data-out="record">' + recordLine() + '</div></div></div>'
        + onReport();
    },
    // D: sources as rows; the chosen one opens to hold the calculation and
    // the three boxes.
    d: function () {
      var basis = shownBasis();
      var open = '<div class="v-open"><div class="sub-panel calc-inputs v-calc" data-valuation-calc><h3>' + L.calculation + ' <span class="rt" data-out="basis-name">' + basisName() + '</span></h3>'
        + '<div class="fg g3">' + ptlCell(true) + dedCell() + vatCell(true) + '</div>' + increases()
        + '<div data-out="calc-note">' + calcNote() + '</div></div>'
        + boxes() + '<div data-out="record">' + recordLine() + '</div></div>';
      var list = SOURCES.map(function (source) { return sourceRow(source) + (basis === source.slug ? open : ''); }).join('');
      var aiRow = opt.ai === 'own' ? aiOwnRow() + (basis === 'ai' ? open : '') : '';
      var aiCards = opt.ai === 'own' ? '' : (aiLiveCard() ? '<div class="guides v-ai-live">' + aiLiveCard() + '</div>' + (basis === 'ai' ? open : '') : '');
      return tools() + '<div class="v-srcs v-srcs--wide" data-valuation-cards>' + sourceRowsHead() + list + aiRow + '</div>' + aiCards
        + (basis ? '' : open) + onReport();
    },
    // E: sources on the left; on the right one pane holding everything about
    // the value: Retail and Trade, the calculation, its lines, and the
    // Engineer's Value as the last line.
    e: function () {
      var result = current();
      return '<div class="v-panes">'
        + '<div class="sub-panel v-pane"><h3>' + L.basis + '</h3>' + tools() + rows('stack') + '</div>'
        + '<div class="sub-panel calc-inputs v-pane v-result" data-valuation-calc>'
        + '<div class="fg g2">' + box('retail') + box('trade') + '</div>'
        + '<h3 class="v-pane-head">' + L.calculation + ' <span class="rt" data-out="basis-name">' + basisName() + '</span></h3>'
        + '<div class="fg g2">' + ptlCell(false) + dedCell() + '</div><div class="fg v-one">' + vatCell(false) + '</div>' + increases()
        + '<div class="lines v-lines" data-out="lines"' + (result && !result.error ? '' : ' hidden') + '>' + linesBody() + '</div>'
        + '<div data-out="calc-note">' + calcNote() + '</div>'
        + '<div class="fg v-one v-result-total">' + box('engineer') + '</div>'
        + '<div data-out="record">' + recordLine() + '</div></div>'
        + '</div>' + onReport();
    }
  };

  // ---- derived nodes patched while typing --------------------------------------

  function out(key) {
    if (key === 'eng-tag') { return engTag(); }
    if (key === 'record') { return recordLine(); }
    if (key === 'calc-note') { return calcNote(); }
    if (key === 'basis-name') { return basisName(); }
    if (key === 'lines') { return linesBody(); }
    if (key.indexOf('amt-') === 0) { return amount(key.slice(4)); }
    return '';
  }
  function headText() {
    var value = live ? S.saved.engineer : S.boxes.engineer;
    return L.engineer + ' ' + (value === '' ? '—' : money(value));
  }
  function patch() {
    var section = document.getElementById('section-valuation');
    if (!section) { return; }
    section.querySelectorAll('[data-out]').forEach(function (node) {
      var key = node.getAttribute('data-out');
      node.innerHTML = out(key);
      if (key === 'lines') { node.hidden = node.innerHTML === ''; }
    });
    var engineer = section.querySelector('[data-valuation-value="engineer"]');
    if (engineer && document.activeElement !== engineer) { engineer.value = S.boxes.engineer; }
    ['retail', 'trade'].forEach(function (kind) {
      var input = section.querySelector('[data-valuation-value="' + kind + '"]');
      if (input && document.activeElement !== input) { input.value = S.boxes[kind]; }
    });
    // Use this value is withdrawn by a typed figure or a refused calculation.
    section.querySelectorAll('[data-valuation-use]').forEach(function (button) {
      var on = S.used && S.chosen === button.getAttribute('data-slug');
      var label = button.querySelector('span');
      button.setAttribute('aria-pressed', on ? 'true' : 'false');
      if (label) { label.textContent = on ? L.using : L.use; }
    });
    var head = section.querySelector('[data-valuation-head]');
    if (head) { head.textContent = headText(); }
  }

  // ---- behaviour, as case-workspace.js has it -----------------------------------

  // A change the calculation follows: its proposal fills the Engineer's
  // Value box; where it cannot be worked out the box goes back to the saved figure.
  function recalculate() {
    var result = calculate(S.sel, S.chosen);
    if (result && !result.error) {
      S.boxes.engineer = result.proposal.toFixed(2);
      S.filled = true;
    } else {
      if (S.filled) { S.boxes.engineer = S.saved.engineer; }
      S.filled = false;
      S.used = false;
    }
  }
  function fillFromCard(slug) {
    var card = cardOf(S, slug);
    S.boxes.retail = card.retail;
    S.boxes.trade = card.trade;
  }
  var commitTimer = null;
  function leaseLine(text) {
    var line = document.querySelector('[data-lease-line]');
    if (line) { line.hidden = !text; line.textContent = text || ''; }
  }
  // Save as you go: the change lands a moment after it is made. A
  // calculation is recorded when Use this value was pressed or the
  // calculator changed, and the box holds its figure.
  function scheduleCommit() {
    window.clearTimeout(commitTimer);
    leaseLine('Saving…');
    commitTimer = window.setTimeout(commit, 700);
  }
  function commit() {
    var result = calculate(S.sel, S.chosen);
    var changed = canonical(S) !== S.opening;
    S.saved.engineer = S.boxes.engineer;
    if (S.chosen && result && !result.error && (S.used || changed)
        && (S.boxes.engineer === '' || num(S.boxes.engineer) === result.proposal)) {
      S.record = { basis: S.chosen, value: result.proposal, retail: result.retail, by: 'alex', at: '06 Oct 2026 14:02', sel: copySelection(S.sel) };
      S.boxes.engineer = result.proposal.toFixed(2);
      S.saved.engineer = S.boxes.engineer;
      S.opening = canonical(S);
    }
    leaseLine('Saved');
    patch();
  }

  function bind(path, target) {
    var parts = path.split('.');
    var value = target.type === 'checkbox' ? target.checked : target.value;
    if (parts[0] === 'box') {
      S.boxes[parts[1]] = value;
      if (parts[1] === 'engineer') { S.filled = false; S.used = false; }
      return 'patch';
    }
    if (parts[0] === 'card') {
      S.cards[parts[1]][parts[2]] = value;
      delete S.notices[parts[1]];
      if (parts[1] === S.chosen && parts[2] !== 'month') { fillFromCard(parts[1]); recalculate(); }
      return 'patch';
    }
    if (parts[0] === 'sel') { S.sel[parts[1]] = value; recalculate(); return target.type === 'checkbox' || target.tagName === 'SELECT' ? 'render' : 'patch'; }
    if (parts[0] === 'add') {
      S.sel.adds[Number(parts[1])][parts[2]] = value;
      recalculate();
      return parts[2] === 'on' ? 'render' : 'patch';
    }
    if (parts[0] === 'report') { S.report[parts[1]] = value; return 'none'; }
    if (parts[0] === 'aiMonth') { S.aiMonth = value; return 'none'; }
    return 'none';
  }

  function choose(slug, used) {
    var card = cardOf(S, slug);
    if (!card || card.pending) { return; }
    if (!(num(card.retail) > 0)) {
      if (used) { S.notices[slug] = L.needsRetail; render(); }
      return;
    }
    delete S.notices[slug];
    S.chosen = slug;
    S.used = !!used;
    fillFromCard(slug);
    recalculate();
    render();
    scheduleCommit();
  }

  function act(name, slug) {
    if (name === 'use') { choose(slug, true); return; }
    if (name === 'get') {
      // The connected source answers the demo figures for the card's month.
      S.cards[slug] = { retail: slug === 'glasses' && S.preset.indexOf('recorded') === 0 ? '12500.00' : '9064.00', trade: slug === 'glasses' && S.preset.indexOf('recorded') === 0 ? '10250.00' : '7347.00', month: S.cards[slug].month || '2026-10' };
      delete S.notices[slug];
      if (S.chosen === slug) { fillFromCard(slug); recalculate(); }
      render();
      scheduleCommit();
      return;
    }
    if (name === 'research') { S.ai = { pending: true }; if (S.chosen === 'ai') { S.chosen = null; } render(); return; }
    if (name === 'problem') { boundary('Report a problem', 'problem-dialog'); }
  }
  function boundary(label, route) {
    var region = document.getElementById('v33-live');
    if (region) { region.textContent = label + ': ' + route + ' (not part of this mockup)'; }
  }

  // ---- mounting ------------------------------------------------------------------

  function liveFragment() {
    var map = {
      'empty-read': 'read-empty', 'fetched-read': 'read-fetched', fetched: 'edit-fetched',
      'recorded-read': 'read-applied', recorded: 'edit-applied', pending: 'edit-pending'
    };
    var template = document.getElementById('tpl-live-' + (map[S.preset] || 'edit-fetched'));
    return template ? template.innerHTML : '';
  }

  function mount() {
    var template = document.getElementById(S.mode === 'edit' ? 'tpl-frame-edit' : 'tpl-frame-read');
    host.innerHTML = template.innerHTML;
    var save = host.querySelector('[data-case-save-now]');
    if (save) { save.hidden = true; }
    if (live) {
      var placeholder = host.querySelector('#section-valuation');
      placeholder.outerHTML = liveFragment();
    }
    var section = host.querySelector('#section-valuation');
    section.setAttribute('data-design', cfg.id);
    section.classList.remove('is-collapsed');
    render();
  }

  function render() {
    var section = document.getElementById('section-valuation');
    if (!section || live) { return; }
    var active = document.activeElement && section.contains(document.activeElement) ? document.activeElement.id : null;
    section.querySelector('.panel-body').innerHTML = designs[cfg.id]();
    var head = section.querySelector('[data-valuation-head]');
    if (head) { head.textContent = headText(); }
    if (active) {
      var again = document.getElementById(active);
      if (again) { again.focus(); }
    }
  }

  function setMode(mode) {
    if (live) {
      var twin = { 'fetched-read': 'fetched', fetched: 'fetched-read', 'recorded-read': 'recorded', recorded: 'recorded-read', 'empty-read': 'empty-read', pending: 'fetched-read' };
      load(twin[S.preset] || S.preset);
      return;
    }
    window.clearTimeout(commitTimer);
    if (S.mode === 'edit') { commit(); }
    S.mode = mode;
    if (mode === 'edit') { S.sel = recordShown() ? withBlankRows(S.record.sel) : S.sel; S.chosen = S.chosen || firstBasis(); S.opening = canonical(S); }
    mount();
    syncStrip();
  }
  function withBlankRows(selection) {
    return copySelection(selection);
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
    if (!path || live) { return; }
    if (target.type === 'checkbox' || target.tagName === 'SELECT') { return; }
    if (bind(path, target) !== 'none') { patch(); scheduleCommit(); }
  });
  host.addEventListener('change', function (event) {
    var target = event.target;
    var path = target.getAttribute && target.getAttribute('data-bind');
    if (!path || live) { return; }
    if (target.type !== 'checkbox' && target.tagName !== 'SELECT') { return; }
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
    if (button && !live) { event.preventDefault(); act(button.getAttribute('data-act'), button.getAttribute('data-slug')); return; }
    var pick = target.closest('[data-pick]');
    if (pick && !live && editing() && !target.closest('input,button,select,label,a')) {
      if (pick.getAttribute('data-pick') !== S.chosen) { choose(pick.getAttribute('data-pick'), false); }
      return;
    }
    var link = target.closest('a[href]');
    if (link) { event.preventDefault(); }
  });
  host.addEventListener('keydown', function (event) {
    var pick = event.target.getAttribute && event.target.getAttribute('data-pick');
    if (!pick || live || !editing() || (event.key !== 'Enter' && event.key !== ' ')) { return; }
    event.preventDefault();
    if (pick !== S.chosen) { choose(pick, false); }
  });
  host.addEventListener('submit', function (event) { event.preventDefault(); });

  // ---- mockup strip ----------------------------------------------------------------

  function syncStrip() {
    var state = document.querySelector('[data-state-picker]');
    if (state) { state.value = S.preset; }
    document.querySelectorAll('[data-option-picker]').forEach(function (picker) {
      picker.value = opt[picker.getAttribute('data-option-picker')];
    });
    document.documentElement.setAttribute('data-v33-state', S.preset);
    document.documentElement.setAttribute('data-v33-mode', S.mode);
  }
  function readOptions() {
    cfg.options.forEach(function (option) { opt[option.key] = cfg.defaults[option.key]; });
    (params.get('opt') || '').split(',').forEach(function (pair) {
      var parts = pair.split(':');
      if (parts.length === 2 && Object.prototype.hasOwnProperty.call(opt, parts[0])) { opt[parts[0]] = parts[1]; }
    });
  }
  var statePicker = document.querySelector('[data-state-picker]');
  if (statePicker) { statePicker.addEventListener('change', function () { load(statePicker.value); }); }
  document.querySelectorAll('[data-option-picker]').forEach(function (picker) {
    picker.addEventListener('change', function () { opt[picker.getAttribute('data-option-picker')] = picker.value; render(); patch(); });
  });

  readOptions();
  var wanted = params.get('state') || 'fetched';
  var known = cfg.presets.some(function (item) { return item[0] === wanted; });
  load(known ? wanted : cfg.presets[0][0]);
  window.v33 = { state: function () { return S; }, options: function () { return opt; } };
})();
