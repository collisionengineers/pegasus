// v28 proposals layer, part two: the Case record features first drawn in v27
// and asked for again on 18 September. Conceptual source: the operator's
// reference file pegasus_case_dashboard_2026-09-15.html. Every feature is built
// from the live record's own parts (.fc / .fv / .fi cells, .sub-panel, .dec,
// .src-tag, .status, .menu, .tabs), so it sits in the existing design instead
// of bringing its own. Ids P12 to P30; ?skip=P14 turns one off.
(function () {
  'use strict';
  var params = new URLSearchParams(window.location.search);
  if (params.get('proposals') === 'off') return;
  var skipped = (params.get('skip') || '').split(',').map(function (s) { return s.trim().toUpperCase(); });
  var on = function (id) { return skipped.indexOf(id) < 0; };
  var $ = function (id) { return document.getElementById(id); };
  var $$ = function (selector, root) { return Array.prototype.slice.call((root || document).querySelectorAll(selector)); };

  var record, editing;
  var money = function (n) { return '£' + Number(n || 0).toLocaleString('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 }); };
  var number = function (text) { var m = String(text || '').replace(/,/g, '').match(/-?[0-9]+(\.[0-9]+)?/); return m ? parseFloat(m[0]) : 0; };
  var clean = function (text) { return String(text || '').replace(/\s+/g, ' ').trim(); };
  var escape = function (text) { return String(text).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/"/g, '&quot;'); };
  var mark = function (el, id) { el.setAttribute('data-v28-proposal', id); return el; };

  // The cell that holds a field, found by its label, in read or edit mode.
  function cell(sectionId, label) {
    var section = $(sectionId); if (!section) return null;
    return $$('.fc', section).filter(function (fc) {
      var l = fc.querySelector('label, .lbl'); return l && clean(l.textContent) === label;
    })[0] || null;
  }
  // A field's current value: the control while editing, the read cell otherwise.
  function value(sectionId, label) {
    var fc = cell(sectionId, label); if (!fc) return '';
    var control = fc.querySelector('select.fi, input.fi, textarea.fi');
    if (editing && control) {
      if (control.tagName === 'SELECT') { var o = control.selectedOptions[0]; return o && o.value ? clean(o.textContent) : ''; }
      return clean(control.value);
    }
    var fv = fc.querySelector('.fv'); if (!fv || fv.classList.contains('empty')) return '';
    var copy = fv.cloneNode(true); $$('.src-tag, .prov', copy).forEach(function (n) { n.remove(); });
    return clean(copy.textContent);
  }
  function derivedCell(id, label, key, span) {
    var fc = document.createElement('div');
    fc.className = 'fc ro v28-composed' + (span ? ' span' + span : '');
    fc.innerHTML = '<span class="lbl">' + escape(label) + '</span><div class="fv derived" data-composed="' + key + '"></div>';
    return mark(fc, id);
  }
  function engineersValue() {
    var el = document.querySelector('[data-decision-engineer-value] span');
    return el ? number(el.textContent) : 0;
  }

  // ---- P12 · Composed sentences ------------------------------------------
  // Read-only cells in the live "derived" style that show, on the record, the
  // sentence the report will carry, composed from the fields beside them.
  var MILEAGE = {
    'online data': 'The mileage has been calculated from online data.',
    'owner': 'The mileage was advised by the owner.',
    'repairer': 'The mileage was advised by the repairer/storage yard.',
    'principal': 'The mileage was advised by our instructing principal.',
    'average': 'The mileage has been estimated from the average for the vehicle’s age.'
  };
  function p12Compose() {
    var set = function (key, text) { $$('[data-composed="' + key + '"]').forEach(function (el) { el.textContent = text; }); };
    var claimant = value('section-overview', 'Claimant') || value('section-claim', 'Claimant');
    var when = value('section-overview', 'Incident date');
    var date = when ? new Date(/^\d{4}-/.test(when) ? when + 'T00:00:00' : when) : null;
    var long = date && !isNaN(date) ? date.toLocaleDateString('en-GB', { day: 'numeric', month: 'long', year: 'numeric' }) : '';
    set('matter', 'Road Traffic Accident: ' + (claimant || '—') + (long ? ': ' + long : ''));

    var inspectAt = value('section-inspection', 'Inspect at');
    var address = value('section-inspection', 'Inspection address');
    set('located', 'Vehicle located at: ' + (/image based/i.test(inspectAt) ? 'Image Based Assessment' : (address || '—')) + '.');

    var perDay = number(value('section-inspection', 'Storage per day'));
    var recovery = number(value('section-inspection', 'Recovery charge'));
    set('charges', perDay && recovery ? 'We understand recovery and storage charges are accruing at ' + money(recovery) + ' and ' + money(perDay) + 'pd, plus VAT.'
      : recovery ? 'We understand recovery charges of ' + money(recovery) + ' have been incurred, plus VAT.'
      : perDay ? 'We understand storage charges are accruing at ' + money(perDay) + 'pd, plus VAT.'
      : 'No charges recorded, so no line appears on the report.');

    var source = value('section-vehicle', 'Mileage source').toLowerCase();
    set('mileage', MILEAGE[source] || 'No mileage source recorded, so no line appears on the report.');
    var condition = value('section-vehicle', 'Pre-incident condition').toLowerCase();
    set('condition', condition ? 'The vehicle is considered to be in ' + condition + ' condition for its age and type.' : 'No condition recorded, so no line appears on the report.');

    var vat = editing && $('estimate-vat-status') ? $('estimate-vat-status').value : '';
    if (!editing) { var read = $$('#section-estimate .est-head-read span').filter(function (s) { return /Repairer VAT status/.test(s.textContent); })[0]; vat = read ? clean(read.textContent).replace('Repairer VAT status', '').trim() : ''; }
    set('vat', /^registered/i.test(vat) ? 'Repairer VAT registered: VAT at 20 % applies to every category.'
      : /not/i.test(vat) ? 'Repairer not VAT registered: VAT applies to parts and materials only.'
      : 'No repairer VAT status recorded: no VAT is charged.');

    var disclose = document.querySelector('[data-report-switch^="report.disclose_guide"]');
    var disclosed = disclose ? disclose.checked : !/not disclosed/i.test(($$('[data-report-content]')[0] || {}).textContent || '');
    var card = document.querySelector('#section-valuation .valuation-card.is-basis, #section-valuation .valuation-card[data-valuation-card="glasses"], #section-valuation .valuation-card');
    var name = card ? clean((card.querySelector('h3 span') || card.querySelector('h3') || {}).textContent) : '';
    var retailInput = card && card.querySelector('[data-valuation-retail]'); var tradeInput = card && card.querySelector('[data-valuation-trade]');
    var figures = card ? (card.textContent.match(/£[0-9,]+\.[0-9]{2}/g) || []) : [];
    var retail = retailInput ? number(retailInput.value) : number(figures[0]);
    var trade = tradeInput ? number(tradeInput.value) : number(figures[1]);
    var ev = engineersValue();
    set('carries', (disclosed ? (name || 'Guide') : 'Source not disclosed') + ': Retail ' + (retail ? money(retail) : '—')
      + (trade ? ' · Trade ' + money(trade) : '') + ' · Engineer’s Value ' + (ev ? money(ev) : '—') + '.');
  }
  function p12Composed() {
    var add = function (grid, el, before) { if (grid) grid.insertBefore(el, before || null); };
    var caseGrid = document.querySelector('#section-overview .overview-grid .sub-panel .fg');
    add(caseGrid, derivedCell('P12', 'Matter line', 'matter', 2));
    var inspectionGrid = document.querySelector('#section-inspection .panel-body > .fg');
    add(inspectionGrid, derivedCell('P12', 'Assessment method', 'located', 4));
    add(document.querySelector('#section-inspection [data-inspection-storage] .fg'), derivedCell('P12', 'Recovery and storage charges', 'charges', 4));
    var mileageGrid = document.querySelector('#section-vehicle [data-vehicle-mileage] .fg');
    add(mileageGrid, derivedCell('P12', 'Mileage statement', 'mileage', 2));
    add(mileageGrid, derivedCell('P12', 'Pre-incident condition statement', 'condition', 2));
    var estHead = document.querySelector('#section-estimate .est-head');
    if (estHead) { var wrap = document.createElement('div'); wrap.className = 'fg'; wrap.appendChild(derivedCell('P12', 'Drives the calculation', 'vat', 0)); mark(wrap, 'P12'); estHead.parentNode.insertBefore(wrap, estHead.nextSibling); }
    var valuationBody = document.querySelector('#section-valuation > .panel-body');
    if (valuationBody) { var holder = document.createElement('div'); holder.className = 'fg'; holder.appendChild(derivedCell('P12', 'What the report carries', 'carries', 0)); mark(holder, 'P12'); valuationBody.appendChild(holder); }
    p12Compose();
    document.addEventListener('input', p12Compose);
    document.addEventListener('change', p12Compose);
  }

  // ---- P13 · CAP as a guide source ---------------------------------------
  function p13Cap() {
    var section = $('section-valuation'); if (!section) return;
    var template = section.querySelector('.valuation-card.entry[data-valuation-entry="super-cap"]') || section.querySelector('.valuation-card.entry[data-valuation-entry="brego"]');
    if (!template) return; // read mode shows a source only once it holds a guide
    var card = template.cloneNode(true);
    ['data-valuation-entry', 'data-valuation-card'].forEach(function (a) { card.setAttribute(a, 'cap'); });
    card.setAttribute('data-valuation-source-card', 'Cap');
    var title = card.querySelector('h3 span'); if (title) title.textContent = 'CAP';
    $$('[name="source"]', card).forEach(function (i) { i.value = 'Cap'; });
    $$('[data-valuation-source]', card).forEach(function (b) { b.setAttribute('data-valuation-source', 'cap'); });
    $$('[data-valuation-save]', card).forEach(function (b) { b.setAttribute('data-valuation-save', 'cap'); });
    $$('[data-valuation-retail], [data-valuation-trade]', card).forEach(function (i) { i.value = ''; });
    template.parentNode.insertBefore(mark(card, 'P13'), template.nextSibling);
  }

  // ---- P29 · Decisions as radio groups ------------------------------------
  // Outcome, Salvage category and Roadworthiness each take one answer, so the
  // control is a radio group, not tick boxes (a tick box says "any number of
  // these"). The select stays the control the form posts; the radios drive it.
  // Its empty option is offered as "Not recorded", so the unset state stays
  // reachable without a radio that un-checks itself, which no radio does.
  function p29Decisions() {
    if (!editing) return;
    ['f-assessment-outcome', 'f-assessment-category', 'f-assessment-legal-status'].forEach(function (id) {
      var select = $(id); if (!select) return;
      var row = document.createElement('div');
      row.className = 'v28-radiorow'; row.setAttribute('role', 'radiogroup');
      row.setAttribute('aria-label', clean((select.closest('.fc').querySelector('label') || {}).textContent));
      mark(row, 'P29');
      var values = $$('option', select).map(function (o) { return { value: o.value, label: clean(o.textContent) || 'Not recorded' }; });
      var draw = function () {
        var checkedAt = Math.max(0, values.map(function (v) { return v.value; }).indexOf(select.value));
        row.innerHTML = values.map(function (v, index) {
          var checked = v.value === select.value;
          return '<button type="button" class="v28-radio" role="radio" aria-checked="' + checked + '"'
            + ' tabindex="' + (index === checkedAt ? '0' : '-1') + '" data-value="' + escape(v.value) + '">'
            + '<span class="dot"></span>' + escape(v.label) + '</button>';
        }).join('');
      };
      var choose = function (button, focus) {
        select.value = button.getAttribute('data-value');
        select.dispatchEvent(new Event('input', { bubbles: true }));
        select.dispatchEvent(new Event('change', { bubbles: true }));
        draw();
        if (focus) { var again = row.querySelector('[aria-checked="true"]'); if (again) again.focus(); }
      };
      row.addEventListener('click', function (event) {
        var button = event.target.closest('.v28-radio'); if (button) choose(button, false);
      });
      // Arrow keys move and choose within the group, as a radio group does;
      // the group itself is one tab stop.
      row.addEventListener('keydown', function (event) {
        var buttons = $$('.v28-radio', row), at = buttons.indexOf(document.activeElement);
        if (at < 0) return;
        var to = null;
        if (event.key === 'ArrowRight' || event.key === 'ArrowDown') to = (at + 1) % buttons.length;
        else if (event.key === 'ArrowLeft' || event.key === 'ArrowUp') to = (at - 1 + buttons.length) % buttons.length;
        else if (event.key === 'Home') to = 0;
        else if (event.key === 'End') to = buttons.length - 1;
        else if (event.key === ' ' || event.key === 'Enter') { event.preventDefault(); choose(buttons[at], true); return; }
        if (to === null) return;
        event.preventDefault(); choose(buttons[to], true);
      });
      select.addEventListener('change', draw);
      select.classList.add('v28-driven');
      select.parentNode.insertBefore(row, select.nextSibling);
      draw();
    });
  }

  // ---- P14 · Salvage value as a share of the Engineer's Value ------------
  var SNAPS = [5, 10, 15, 20, 25];
  function p14Salvage() {
    var input = $('f-assessment-salvage-value'); if (!editing || !input) return;
    var wrap = document.createElement('div'); wrap.className = 'v28-salvage'; mark(wrap, 'P14');
    wrap.innerHTML = '<input type="range" min="0" max="100" step="1" aria-label="Salvage value as a percentage of the Engineer’s Value" />'
      + '<span class="snaps"></span><span class="read mono"></span>';
    input.parentNode.insertBefore(wrap, input.nextSibling);
    var range = wrap.querySelector('input'), snaps = wrap.querySelector('.snaps'), read = wrap.querySelector('.read');
    var refresh = function (fromSlider) {
      var ev = engineersValue();
      range.disabled = !ev;
      var amount = fromSlider ? Math.round(ev * range.value / 100) : number(input.value);
      if (fromSlider) { input.value = amount.toFixed(2); input.dispatchEvent(new Event('input', { bubbles: true })); }
      var percent = ev > 0 ? amount / ev * 100 : 0;
      range.value = Math.max(0, Math.min(100, Math.round(percent)));
      read.textContent = ev ? '= ' + (+percent.toFixed(1)) + '% of ' + money(ev) : 'Apply in Valuation';
      snaps.innerHTML = SNAPS.map(function (p) { return '<button type="button" class="' + (Math.abs(percent - p) < 0.5 ? 'on' : '') + '" data-snap="' + p + '"' + (ev ? '' : ' disabled') + '>' + p + '%</button>'; }).join('');
    };
    range.addEventListener('input', function () { refresh(true); });
    input.addEventListener('input', function () { refresh(false); });
    snaps.addEventListener('click', function (event) { var b = event.target.closest('[data-snap]'); if (b) { range.value = b.getAttribute('data-snap'); refresh(true); } });
    refresh(false);
  }

  // ---- P15 · Unroadworthy reason bank ------------------------------------
  var BANK = ['the steering and suspension geometry has been compromised', 'the rear lamp assemblies are inoperative', 'the headlamp assemblies are inoperative',
    'the vehicle presents sharp edges likely to cause injury', 'the supplementary restraint systems have deployed', 'structural distortion is evident to the body shell', 'there is a loss of essential fluids'];
  function p15Bank() {
    var area = $('f-assessment-unroadworthy-reason'); if (!editing || !area) return;
    var bank = document.createElement('div'); bank.className = 'v28-bank'; mark(bank, 'P15');
    area.parentNode.insertBefore(bank, area.nextSibling);
    var draw = function () {
      bank.innerHTML = BANK.map(function (reason, i) { return '<button type="button" data-bank="' + i + '">' + escape(reason) + '</button>'; }).join('')
        + '<button type="button" class="v28-bank-save" data-bank-save>Save this wording to the bank</button>';
    };
    bank.addEventListener('click', function (event) {
      var pick = event.target.closest('[data-bank]');
      if (pick) {
        var reason = BANK[+pick.getAttribute('data-bank')]; var now = clean(area.value);
        if (now.indexOf(reason) < 0) area.value = now ? now.replace(/\.$/, '') + ' and ' + reason : reason.charAt(0).toUpperCase() + reason.slice(1);
        area.dispatchEvent(new Event('input', { bubbles: true }));
      }
      if (event.target.closest('[data-bank-save]')) {
        var typed = clean(area.value).replace(/\.$/, ''); typed = typed.charAt(0).toLowerCase() + typed.slice(1);
        if (typed && BANK.indexOf(typed) < 0) { BANK.push(typed); draw(); }
      }
    });
    draw();
  }

  // ---- P16 · Delete all lines, and Undo after removing one ---------------
  function p16EstimateLines() {
    var body = document.querySelector('#section-estimate [data-estimate-grid-body]'); if (!editing || !body) return;
    var grid = body.closest('[data-estimate-grid]');
    var tools = document.createElement('div'); tools.className = 'button-row v28-est-tools'; mark(tools, 'P16');
    tools.innerHTML = '<button type="button" class="btn btn--small btn--danger" data-est-delete-all><svg class="icon" aria-hidden="true"><use href="#icon-trash-2" /></svg><span>Delete all lines</span></button>';
    var addLine = $$('#section-estimate button').filter(function (b) { return clean(b.textContent) === 'Add line'; })[0];
    var addRow = addLine && addLine.parentElement;
    if (addRow) { var del = tools.firstChild; mark(del, 'P16'); addRow.insertBefore(del, addLine); tools = addRow; }
    else grid.parentNode.insertBefore(tools, grid.nextSibling);
    var rows = function () { return $$('tr[data-estimate-line]', body); };
    var toast = function (text, undo) {
      var region = document.querySelector('[data-toast-region]') || document.body;
      var note = document.createElement('div'); note.className = 'toast v28-undo'; note.setAttribute('role', 'status'); mark(note, 'P16');
      note.innerHTML = '<span>' + escape(text) + '</span><button type="button" class="btn btn--small">Undo</button>';
      note.querySelector('button').addEventListener('click', function () { undo(); note.remove(); });
      region.appendChild(note); setTimeout(function () { note.remove(); }, 8000);
    };
    body.addEventListener('click', function (event) {
      var del = event.target.closest('button.del'); if (!del) return;
      var row = del.closest('tr[data-estimate-line]'); if (!row) return;
      event.preventDefault(); event.stopPropagation();
      var next = row.nextSibling; row.remove();
      toast('Line removed', function () { body.insertBefore(row, next); });
    }, true);
    tools.querySelector('[data-est-delete-all]').addEventListener('click', function () {
      var all = rows(); if (!all.length) return;
      var dialog = document.createElement('div'); dialog.className = 'reason-dialog-backdrop'; mark(dialog, 'P16');
      dialog.innerHTML = '<div class="reason-dialog" role="alertdialog" aria-modal="true"><h2>Delete all lines</h2><p>' + all.length + (all.length === 1 ? ' line' : ' lines') + ' will be removed from this draft.</p>'
        + '<div class="button-row"><button type="button" class="btn" data-no>Cancel</button><button type="button" class="btn btn--danger" data-yes>Delete all lines</button></div></div>';
      document.body.appendChild(dialog);
      dialog.addEventListener('click', function (event) {
        if (event.target.closest('[data-yes]')) { all.forEach(function (r) { r.remove(); }); toast(all.length + ' lines removed', function () { all.forEach(function (r) { body.appendChild(r); }); }); dialog.remove(); }
        else if (event.target.closest('[data-no]') || event.target === dialog) dialog.remove();
      });
    });
  }

  // ---- P17 · Regional uplift ---------------------------------------------
  var HC_FULL = 'SS RM DA BR CR SM KT TW SL UB HA WD AL EN IG LU HP GU RH BN TN ME CT E EC N NW SE SW W WC'.split(' ');
  var HC_DIST = { SG: [1, 2, 3, 4, 5, 9, 10, 11, 12, 13, 14], OX: [1, 3, 4, 5, 9, 10, 11, 14, 25, 39, 44, 49], RG: [1, 2, 4, 5, 6, 7, 8, 9, 10, 12, 18, 21, 22, 23, 24, 25, 27, 29, 30, 31, 40, 41, 42, 45], CM: [0, 9] };
  function outward(text) { var m = String(text || '').toUpperCase().match(/\b([A-Z]{1,2})(\d{1,2})[A-Z]?\s*\d[A-Z]{2}\b/); return m ? { area: m[1], district: parseInt(m[2], 10), raw: m[1] + m[2] } : null; }
  function homeCounties(o) { return !!o && (HC_FULL.indexOf(o.area) >= 0 || (HC_DIST[o.area] && HC_DIST[o.area].indexOf(o.district) >= 0)); }
  function p17Uplift() {
    var rate = $('estimate-labour-rate'); if (!editing || !rate) return;
    var base = number(rate.value);
    var fc = document.createElement('div'); fc.className = 'fc'; mark(fc, 'P17');
    fc.innerHTML = '<label for="v28-uplift">Regional uplift</label><div class="v28-uplift"><label class="choice"><input type="checkbox" id="v28-uplift" /><span>+ 15 %</span></label><span class="src-tag" data-uplift-chip></span></div>';
    rate.closest('.fg').appendChild(fc);
    var box = fc.querySelector('input'), chip = fc.querySelector('[data-uplift-chip]');
    var refresh = function () {
      var sources = [['Repairer', value('section-inspection', 'Address')], ['Claimant', value('section-overview', 'Address')], ['Storage', value('section-inspection', 'Storage location')]];
      var hits = sources.map(function (s) { return [s[0], outward(s[1])]; }).filter(function (s) { return homeCounties(s[1]); });
      chip.className = 'src-tag' + (hits.length ? ' src-tag--warn' : '');
      chip.textContent = hits.length ? 'Suggested · ' + hits.map(function (h) { return h[0] + ' (' + h[1].raw + ')'; }).join(', ') : 'London & Home Counties';
    };
    box.addEventListener('change', function () { rate.value = (box.checked ? base * 1.15 : base).toFixed(2); rate.dispatchEvent(new Event('input', { bubbles: true })); });
    rate.addEventListener('change', function () { if (!box.checked) base = number(rate.value); });
    document.addEventListener('input', refresh); refresh();
  }

  // ---- P18 · Import provenance on a line's Source chip -------------------
  // A hand-entered line says Manual, as live. An imported line names its
  // import. The fixture's lines are all hand-entered, so the rule shows only
  // when an estimate's own Source says where it came from.
  function p18Provenance() {
    var tab = document.querySelector('#section-estimate .estimate-tab[aria-selected="true"] .src-tag');
    var origin = tab ? clean(tab.textContent) : '';
    var code = /audatex/i.test(origin) ? 'AX' : /glass/i.test(origin) ? 'GL' : '';
    if (!code) return;
    $$('#section-estimate td .src-tag').forEach(function (chip) {
      if (/manual/i.test(chip.textContent)) return;
      chip.textContent = 'imported · ' + code; mark(chip, 'P18');
    });
  }

  // ---- The two fixture estimates, line by line ---------------------------
  // The captured page carries the selected estimate's lines only, so Compare
  // and Supplementary read both from here. They are exactly what
  // v28-build/enrich.mjs enters through the live editor.
  var ESTIMATES = [
    { name: 'Example Bodyshop estimate', rate: 48, materials: 185, lines: [['New part', 'Rear bumper cover', 'EX-1001', 1, 412.50, 2.5, 3.0]] },
    { name: 'Example Bodyshop supplementary', rate: 48, materials: 210, lines: [['New part', 'Rear bumper cover and reinforcement', 'EX-1002', 1, 538.00, 3.5, 3.0]] }
  ];
  function estimateNet(e) { return e.lines.reduce(function (sum, l) { return sum + l[3] * l[4] + l[5] * e.rate; }, 0) + e.materials; }
  function diffEstimates(from, to) {
    var key = function (l) { return l[0] + '|' + l[2]; };
    var byKey = {}; from.lines.forEach(function (l) { byKey[key(l)] = l; });
    var out = { added: [], changed: [], removed: [] };
    // A line is the same line when its type matches and either the part number or the description's first words do.
    var match = function (l) { return from.lines.filter(function (f) { return f[0] === l[0] && (f[2] === l[2] || l[1].indexOf(f[1]) === 0 || f[1].indexOf(l[1]) === 0); })[0]; };
    var used = [];
    to.lines.forEach(function (l) {
      var was = match(l);
      if (!was) { out.added.push(l); return; }
      used.push(was);
      var cols = []; for (var i = 1; i < l.length; i++) if (l[i] !== was[i]) cols.push(i);
      if (cols.length) out.changed.push([was, l, cols]);
    });
    from.lines.forEach(function (l) { if (used.indexOf(l) < 0) out.removed.push(l); });
    return out;
  }

  // ---- P19 · Compare: choose two versions and see them line by line ------
  function p19Compare() {
    var dialog = $('compare-estimates-dialog'); if (!dialog) return;
    var body = dialog.querySelector('.dialog-body'); if (!body) return;
    var names = ESTIMATES.map(function (e) { return e.name; });
    var listed = $$('tbody tr td:first-child', body).map(function (td) { return clean(td.firstChild ? td.firstChild.textContent : td.textContent); });
    if (!names.every(function (n) { return listed.indexOf(n) >= 0; })) return; // not this fixture
    var block = document.createElement('div'); block.className = 'stack v28-compare'; mark(block, 'P19');
    var options = function (selected) { return '<option value="">Choose…</option>' + ESTIMATES.map(function (e, i) { return '<option value="' + i + '"' + (i === selected ? ' selected' : '') + '>' + escape(e.name) + '</option>'; }).join(''); };
    block.innerHTML = '<div class="v28-cmphead"><label>From <select class="fi" data-from>' + options(-1) + '</select></label><label>To <select class="fi" data-to>' + options(-1) + '</select></label><span class="sum mono" data-sum></span></div><div data-diff></div>';
    body.appendChild(block);
    var foot = dialog.querySelector('.dialog-foot');
    var print = document.createElement('button'); print.type = 'button'; print.className = 'btn'; print.hidden = true; mark(print, 'P19');
    print.innerHTML = '<svg class="icon" aria-hidden="true"><use href="#icon-file-text" /></svg><span>Print comparison sheet</span>';
    if (foot) foot.insertBefore(print, foot.firstChild);
    print.addEventListener('click', function () { document.body.setAttribute('data-v28-print', 'compare'); window.print(); setTimeout(function () { document.body.removeAttribute('data-v28-print'); }, 500); });
    var draw = function () {
      var a = block.querySelector('[data-from]').value, b = block.querySelector('[data-to]').value;
      var target = block.querySelector('[data-diff]'), sum = block.querySelector('[data-sum]');
      if (a === '' || b === '' || a === b) { target.innerHTML = ''; sum.textContent = ''; print.hidden = true; return; }
      var from = ESTIMATES[+a], to = ESTIMATES[+b], d = diffEstimates(from, to), delta = estimateNet(to) - estimateNet(from);
      sum.innerHTML = (delta >= 0 ? '+' : '−') + money(Math.abs(delta)) + ' net · ' + d.added.length + ' added · ' + d.changed.length + ' changed · ' + d.removed.length + ' removed';
      var row = function (kind, l, cols) { return '<tr class="' + kind + '"><td>' + kind.charAt(0).toUpperCase() + kind.slice(1) + '</td>' + [0, 1, 2, 3, 4, 5, 6].map(function (i) {
        var text = i === 4 ? money(l[i]) : i >= 5 ? l[i].toFixed(1) : escape(l[i]);
        return '<td class="' + (i >= 3 ? 'num mono ' : '') + (cols && cols.indexOf(i) >= 0 ? 'diff' : '') + '">' + text + '</td>'; }).join('') + '</tr>'; };
      target.innerHTML = '<div class="table-wrap"><table class="table table--compact v28-cmp-diff"><thead><tr><th>Change</th><th>Type</th><th>Description</th><th>Part no.</th><th class="num">Qty</th><th class="num">Unit £</th><th class="num">Hours</th><th class="num">Paint h</th></tr></thead><tbody>'
        + d.added.map(function (l) { return row('added', l); }).join('') + d.changed.map(function (c) { return row('changed', c[1], c[2]); }).join('') + d.removed.map(function (l) { return row('removed', l); }).join('')
        + '</tbody></table></div>';
      print.hidden = false;
    };
    block.addEventListener('change', draw);
  }

  // ---- P20 · Supplementary -----------------------------------------------
  var SUPP_REASONS = [['estimate', 'Supplementary estimate received', 'Following receipt of a supplementary estimate'], ['dismantle', 'Further damage found on dismantling', 'Following dismantling of the vehicle, further damage was identified and'],
    ['inspect', 'Further inspection', 'Following a further inspection of the vehicle'], ['images', 'Further images received', 'Following receipt of further images']];
  function p20Supplementary() {
    var tabs = document.querySelector('#section-estimate [data-estimate-tabs]'); if (!tabs) return;
    var selected = clean((tabs.querySelector('.estimate-tab[aria-selected="true"] span') || {}).textContent);
    var current = ESTIMATES.filter(function (e) { return e.name === selected; })[0]; if (!current) return;
    var others = ESTIMATES.filter(function (e) { return e !== current; }); if (!others.length) return;
    var panel = document.createElement('div'); panel.className = 'sub-panel v28-supp'; mark(panel, 'P20');
    panel.innerHTML = '<h3>Supplementary</h3><div class="v28-supp-row"><label>Changes vs <select class="fi" data-vs><option value="">Choose…</option>' + others.map(function (e) { return '<option value="' + ESTIMATES.indexOf(e) + '">' + escape(e.name) + '</option>'; }).join('') + '</select></label></div><div data-out hidden></div>';
    var anchor = document.querySelector('#section-estimate [data-estimate-actions]') || tabs;
    anchor.parentNode.insertBefore(panel, anchor.nextSibling);
    var state = { reason: 'estimate', print: false };
    var draw = function () {
      var vs = panel.querySelector('[data-vs]').value, out = panel.querySelector('[data-out]');
      if (vs === '') { out.hidden = true; out.innerHTML = ''; return; }
      var from = ESTIMATES[+vs], d = diffEstimates(from, current), was = estimateNet(from), now = estimateNet(current);
      var lead = SUPP_REASONS.filter(function (r) { return r[0] === state.reason; })[0][2];
      var text = lead + (d.added.length ? ' the following additional items are now required: ' + d.added.map(function (l) { return l[1]; }).join('; ') + '.' : ' the repair specification has been revised.');
      var hours = d.changed.filter(function (c) { return c[2].indexOf(5) >= 0 || c[2].indexOf(6) >= 0; });
      if (hours.length) text += ' The repair time for ' + hours.map(function (c) { return c[1][1] + ' (' + (c[0][5] + c[0][6]) + ' h → ' + (c[1][5] + c[1][6]) + ' h)'; }).join('; ') + ' has been revised.';
      if (d.removed.length) text += ' ' + d.removed.map(function (l) { return l[1]; }).join('; ') + (d.removed.length > 1 ? ' are' : ' is') + ' no longer required.';
      text += ' The estimated repair cost has ' + (now >= was ? 'increased' : 'reduced') + ' from ' + money(was) + ' to ' + money(now) + '.';
      out.hidden = false;
      out.innerHTML = '<ul>' + d.added.map(function (l) { return '<li><span class="status status--plain status--red">Added</span>' + escape(l[1]) + '</li>'; }).join('')
        + d.changed.map(function (c) { return '<li><span class="status status--plain status--amber">Changed</span>' + escape(c[1][1]) + '</li>'; }).join('')
        + d.removed.map(function (l) { return '<li><span class="status status--plain status--neutral">Removed</span>' + escape(l[1]) + '</li>'; }).join('')
        + '<li class="total"><span>Net</span><span class="num mono">' + money(was) + ' → ' + money(now) + '</span></li></ul>'
        + (editing ? '<div class="v28-supp-row"><label class="choice"><input type="checkbox" data-print' + (state.print ? ' checked' : '') + ' /><span>Explain the change on the report</span></label>'
          + '<label>Reason <select class="fi" data-reason>' + SUPP_REASONS.map(function (r) { return '<option value="' + r[0] + '"' + (r[0] === state.reason ? ' selected' : '') + '>' + r[1] + '</option>'; }).join('') + '</select></label></div>' : '')
        + '<div class="fc ro"' + (state.print || !editing ? '' : ' hidden') + '><span class="lbl">Supplementary damage</span><div class="fv derived multi" data-composed="supp">' + escape(text) + '</div></div>';
    };
    panel.addEventListener('change', function (event) {
      if (event.target.matches('[data-reason]')) state.reason = event.target.value;
      if (event.target.matches('[data-print]')) state.print = event.target.checked;
      draw();
    });
  }

  // ---- P25 · Ribbon badges -----------------------------------------------
  function p25Badges() {
    var aside = $$('.record-section, section, .panel').filter(function (p) { var h = p.querySelector('h2'); return h && clean(h.textContent) === 'Figures'; })[0];
    var chips = document.querySelector('.ribbon .ribbon-chips'); if (!aside || !chips) return;
    var moved = $$('.status', aside).filter(function (s) { return !s.closest('.ribbon'); });
    var repair = 0; $$('.metric, div', aside).forEach(function () {});
    var lines = $$('div, li, tr', aside).filter(function (r) { return r.children.length >= 2 && /Repair cost inc VAT/i.test(r.textContent) && r.textContent.length < 80; })[0];
    if (lines) repair = number((lines.lastElementChild || lines).textContent);
    var ev = engineersValue();
    moved.forEach(function (chip) { var copy = chip.cloneNode(true); mark(copy, 'P25'); chips.insertBefore(copy, chips.firstChild); chip.setAttribute('data-v28-removed', 'P25'); chip.hidden = true; });
    if (ev && repair) {
      var share = Math.round(repair / ev * 100);
      var badge = document.createElement('span');
      badge.className = 'status status--' + (share >= 80 ? 'red' : share >= 66 ? 'amber' : 'green'); mark(badge, 'P25');
      badge.textContent = 'Repairs ' + share + '% of value · ' + money(repair) + ' / ' + money(ev);
      chips.insertBefore(badge, chips.querySelector('.status:not([data-v28-proposal])'));
    }
    var holder = moved[0] && moved[0].parentNode;
    if (holder && !$$('.status', holder).some(function (s) { return !s.hidden; })) holder.hidden = true;
  }

  // ---- P28 · Queries on Notes --------------------------------------------
  function p28Queries() {
    var body = document.querySelector('#section-notes .panel-body'); if (!body) return;
    var panel = document.createElement('div'); panel.className = 'sub-panel'; mark(panel, 'P28');
    panel.innerHTML = '<h3>Queries</h3><div class="estimate-empty empty">No queries recorded</div>';
    body.insertBefore(panel, body.querySelector('[data-case-history]'));
  }

  // ---- P24 · Report and Fee tabs -----------------------------------------
  function p24FeeTab() {
    var body = document.querySelector('#section-report > .panel-body'); if (!body) return;
    var tabs = document.createElement('div'); tabs.className = 'tabs v28-report-tabs'; tabs.setAttribute('role', 'tablist'); tabs.setAttribute('aria-label', 'Report and fee'); mark(tabs, 'P24');
    tabs.innerHTML = '<button type="button" class="tab" role="tab" aria-selected="true" data-pane="report">Report</button><button type="button" class="tab" role="tab" aria-selected="false" data-pane="fee">Fee</button>';
    var reportPane = document.createElement('div'); reportPane.className = 'stack'; reportPane.setAttribute('data-v28-pane', 'report');
    while (body.firstChild) reportPane.appendChild(body.firstChild);
    var feePane = document.createElement('div'); feePane.className = 'stack'; feePane.setAttribute('data-v28-pane', 'fee'); feePane.hidden = true; mark(feePane, 'P24');
    body.appendChild(tabs); body.appendChild(reportPane); body.appendChild(feePane);
    var principal = clean(($$('.ribbon .ribbon-item').filter(function (i) { return /Principal/i.test((i.querySelector('.ribbon-label') || {}).textContent || ''); })[0] || document.body).querySelector('.ribbon-value').textContent);
    var drawFee = function () {
      var agreed = number(value('section-report', 'Agreed fee'));
      var lines = value('section-report', 'Fee description');
      feePane.innerHTML = '<div class="sub-panel"><h3>Fee note<span class="rt">' + escape(principal) + ' fee table</span></h3>'
        + (agreed ? '<dl class="detail-list"><div><dt>' + escape(lines || 'Engineer’s report') + '</dt><dd class="mono">' + money(agreed) + '</dd></div><div><dt>VAT 20 %</dt><dd class="mono">' + money(agreed * 0.2) + '</dd></div><div><dt>Total</dt><dd class="mono"><strong>' + money(agreed * 1.2) + '</strong></dd></div></dl>'
          : '<div class="estimate-empty empty">No agreed fee recorded</div>') + '</div>';
    };
    tabs.addEventListener('click', function (event) {
      var tab = event.target.closest('[data-pane]'); if (!tab) return;
      $$('.tab', tabs).forEach(function (t) { t.setAttribute('aria-selected', String(t === tab)); });
      reportPane.hidden = tab.getAttribute('data-pane') !== 'report'; feePane.hidden = !reportPane.hidden; drawFee();
    });
    document.addEventListener('input', function () { if (!feePane.hidden) drawFee(); });
  }

  // ---- P21 to P23 · Delivery: address book, Attach row, re-send naming ---
  // The fixture has no generated report, so the live delivery form is not in
  // the capture. It is rebuilt here with the markup of _CaseReport.cshtml
  // (form.recip-form and its children) and then extended.
  var ABOOK = [['QDOS claims inbox', 'claims@qdos.example', 'Principal'], ['Case handler', 'handler@example.test', 'This case'], ['Collision Engineers reports', 'reports@collisionengineers.example', 'CE']];
  function p21Delivery() {
    if (!editing) return;
    var pane = document.querySelector('#section-report [data-v28-pane="report"]') || document.querySelector('#section-report > .panel-body'); if (!pane) return;
    var reference = clean((document.querySelector('.ribbon-ref .ribbon-value') || {}).textContent);
    var registration = clean(((document.querySelector('.ribbon-ref .ribbon-label') || {}).textContent || '').split('·').pop());
    var outcome = value('section-settlement', 'Outcome') || 'Assessment';
    var form = document.createElement('form'); form.className = 'recip-form v28-delivery'; form.setAttribute('method', 'post'); form.setAttribute('action', '/Cases/Details?handler=PrepareReportDelivery'); mark(form, 'P21');
    form.innerHTML = '<span class="lg">Reviewed recipients</span>'
      + '<span class="lbl">To</span><div class="v28-addr"><input name="toRecipients" type="email" autocomplete="off" aria-label="Report recipient" data-abook /><div class="v28-abook" hidden></div></div>'
      + '<span class="lbl">Cc</span><div class="stack"><div class="v28-cc" data-cc></div><input type="email" aria-label="Report copy recipient" placeholder="" data-cc-entry /><div class="v28-ccsugg" data-cc-suggest></div></div>'
      + '<span class="lbl" data-v28-proposal="P22">Attach</span><div class="v28-attach" data-v28-proposal="P22"><label class="choice"><input type="checkbox" checked data-att="Report" /><span>Report</span></label><label class="choice"><input type="checkbox" checked data-att="Fee note" /><span>Fee note</span></label><label class="choice"><input type="checkbox" data-att="Breakdown" /><span>Breakdown</span></label><label class="choice"><input type="checkbox" data-att="Images" /><span>Images</span></label></div>'
      + '<span class="lbl" data-v28-proposal="P23">File name</span><div class="fv mono" data-v28-proposal="P23" data-file-name-out></div>'
      + '<span class="lbl" data-v28-proposal="P23">Message</span><div class="fv derived multi" data-v28-proposal="P23" data-message-out></div>'
      + '<button type="submit" class="btn btn--small btn--primary"><svg class="icon" aria-hidden="true"><use href="#icon-send" /></svg><span>Prepare delivery</span></button>';
    pane.appendChild(form);
    var sent = 0, cc = [];
    var to = form.querySelector('[data-abook]'), book = form.querySelector('.v28-abook');
    var drawBook = function () {
      var q = to.value.toLowerCase();
      var hits = ABOOK.filter(function (a) { return !q || (a[0] + a[1]).toLowerCase().indexOf(q) >= 0; });
      book.innerHTML = hits.length ? hits.map(function (a) { return '<div class="abr" data-pick="' + escape(a[1]) + '"><span class="an">' + escape(a[0]) + '</span><span class="ae">' + escape(a[1]) + '</span><span class="at">' + escape(a[2]) + '</span></div>'; }).join('') : '<div class="abempty">No match</div>';
    };
    to.addEventListener('focus', function () { drawBook(); book.hidden = false; });
    to.addEventListener('input', drawBook);
    book.addEventListener('mousedown', function (event) { var pick = event.target.closest('[data-pick]'); if (pick) { to.value = pick.getAttribute('data-pick'); book.hidden = true; event.preventDefault(); } });
    to.addEventListener('blur', function () { setTimeout(function () { book.hidden = true; }, 120); });
    var drawCc = function () {
      form.querySelector('[data-cc]').innerHTML = cc.map(function (a, i) { return '<span class="ccchip">' + escape(a) + '<button type="button" data-cc-remove="' + i + '" aria-label="Remove ' + escape(a) + '">×</button><input type="hidden" name="ccRecipients" value="' + escape(a) + '" /></span>'; }).join('');
      form.querySelector('[data-cc-suggest]').innerHTML = ABOOK.filter(function (a) { return cc.indexOf(a[1]) < 0 && a[1] !== to.value; }).map(function (a) { return '<button type="button" data-cc-add="' + escape(a[1]) + '">+ ' + escape(a[0]) + '</button>'; }).join('');
    };
    form.addEventListener('click', function (event) {
      var add = event.target.closest('[data-cc-add]'), remove = event.target.closest('[data-cc-remove]');
      if (add) { cc.push(add.getAttribute('data-cc-add')); drawCc(); }
      if (remove) { cc.splice(+remove.getAttribute('data-cc-remove'), 1); drawCc(); }
    });
    form.querySelector('[data-cc-entry]').addEventListener('keydown', function (event) {
      if (event.key !== 'Enter') return; event.preventDefault();
      var typed = clean(event.target.value); if (typed && cc.indexOf(typed) < 0) { cc.push(typed); event.target.value = ''; drawCc(); }
    });
    var drawNaming = function () {
      form.querySelector('[data-file-name-out]').textContent = reference + ' ' + registration + ' ' + outcome + ' report' + new Array(sent + 1).join('.') + '.pdf';
      var today = new Date().toLocaleDateString('en-GB', { day: 'numeric', month: 'long', year: 'numeric' });
      form.querySelector('[data-message-out]').textContent = sent ? 'Please find attached our updated report, which supersedes our report dated ' + today + '.' : 'Please find attached our report.';
    };
    form.addEventListener('submit', function (event) {
      event.preventDefault(); event.stopImmediatePropagation();
      sent += 1; drawNaming();
      var old = form.parentNode.querySelector('[data-report-delivery]'); if (old) old.remove();
      var list = document.createElement('dl'); list.className = 'detail-list'; list.setAttribute('data-report-delivery', ''); mark(list, 'P22');
      var ticked = $$('[data-att]', form).filter(function (b) { return b.checked; }).map(function (b) { return b.getAttribute('data-att'); });
      list.innerHTML = '<div><dt>Delivery prepared</dt><dd>' + escape(to.value || '—') + '</dd></div>' + (cc.length ? '<div><dt>Cc</dt><dd>' + escape(cc.join(', ')) + '</dd></div>' : '') + '<div><dt>Attached</dt><dd>' + escape(ticked.join(', ') || '—') + '</dd></div>';
      form.parentNode.insertBefore(list, form.nextSibling);
    }, true);
    drawCc(); drawNaming();
  }

  // ---- P27 · Click to include an image in the report ---------------------
  function p27Include() {
    if (!editing) return;
    var tiles = $$('#section-files [data-evidence-item], #section-files .image-tile a[data-evidence-item]');
    var count = document.querySelector('[data-report-image-count]');
    var strip = $$('#section-report [data-report-images-strip] .th');
    if (!tiles.length) return;
    var included = {};
    strip.forEach(function (th, i) { included[i] = !th.classList.contains('off'); });
    var refresh = function () {
      var n = Object.keys(included).filter(function (k) { return included[k]; }).length;
      if (count) count.textContent = n + ' of ' + tiles.length + ' in report';
      strip.forEach(function (th, i) { th.classList.toggle('off', !included[i]); var inc = th.querySelector('.inc'); if (inc) inc.textContent = included[i] ? '✓' : '–'; });
    };
    tiles.forEach(function (tile, i) {
      var tick = document.createElement('span'); tick.className = 'v28-include'; mark(tick, 'P27');
      tile.appendChild(tick);
      var draw = function () { tick.textContent = included[i] ? '✓' : ''; tick.classList.toggle('on', !!included[i]); tile.setAttribute('aria-pressed', String(!!included[i])); };
      tile.addEventListener('click', function (event) { event.preventDefault(); event.stopPropagation(); included[i] = !included[i]; draw(); refresh(); }, true);
      draw();
    });
    refresh();
  }

  // ---- P26 · The nine-section map ----------------------------------------
  // Case details, Claim, Inspection details, Vehicle (with Damage and
  // Valuation inside it), Estimate, Decisions, Report, Files (Images first), Notes.
  function p26NineSections() {
    var main = $('case-main'), nav = document.querySelector('[data-section-nav]'); if (!main || !nav) return;
    var overview = $('section-overview'), vehicle = $('section-vehicle'), damage = $('section-damage'), valuation = $('section-valuation');
    if (!overview || !vehicle) return;
    // Claim: the claimant, the Case contact and the accident band leave Overview.
    var claim = document.createElement('section');
    claim.className = 'record-section panel'; claim.id = 'section-claim'; claim.setAttribute('data-section', 'claim'); claim.setAttribute('aria-labelledby', 'section-claim-title'); mark(claim, 'P26');
    claim.innerHTML = '<div class="panel-head"><h2 id="section-claim-title">Claim</h2></div><div class="panel-body stack"></div>';
    var claimBody = claim.querySelector('.panel-body');
    var subs = $$('.sub-panel', overview);
    var byTitle = function (title) { return subs.filter(function (p) { return clean((p.querySelector('h3') || {}).textContent) === title; })[0]; };
    [byTitle('Claimant'), byTitle('Case contact')].forEach(function (p) { if (p) claimBody.appendChild(p); });
    var accident = $$('#section-overview .fc').filter(function (fc) { return /Accident circumstances/i.test((fc.querySelector('label, .lbl') || {}).textContent || ''); })[0];
    if (accident) claimBody.appendChild(accident.closest('[data-accident-band]') || accident.closest('.fg') || accident.parentNode);
    // In a full-width section the claimant's cells sit four across, as cells do elsewhere.
    var claimantGrid = claimBody.querySelector('.sub-panel .fg'); if (claimantGrid && !/g[0-9]/.test(claimantGrid.className)) claimantGrid.classList.add('g4');
    overview.parentNode.insertBefore(claim, overview.nextSibling);
    var grid = overview.querySelector('.overview-grid'); if (grid) grid.classList.add('v28-two');
    // Damage and Valuation become sub-panels of Vehicle.
    [damage, valuation].forEach(function (section) { if (section) { section.classList.add('v28-nested'); vehicle.querySelector('.panel-body').appendChild(section); } });
    // Names, and one link per section in the new order.
    var rename = { overview: 'Case details', settlement: 'Decisions' };
    Object.keys(rename).forEach(function (key) { var h = $('section-' + key + '-title'); if (h) { h.setAttribute('data-v28-live-text', h.textContent); h.textContent = rename[key]; } });
    var order = ['overview', 'claim', 'inspection', 'vehicle', 'estimate', 'settlement', 'report', 'files', 'notes'];
    var links = {}; $$('[data-section-link]', nav).forEach(function (a) { links[a.getAttribute('data-section-link')] = a; });
    var claimLink = links.overview.cloneNode(true);
    claimLink.setAttribute('data-section-link', 'claim'); claimLink.removeAttribute('aria-current'); claimLink.setAttribute('href', '#section-claim');
    claimLink.querySelector('span').textContent = 'Claim'; var use = claimLink.querySelector('use'); if (use) use.setAttribute('href', '#icon-file-text');
    mark(claimLink, 'P26'); links.claim = claimLink;
    claimLink.addEventListener('click', function (event) { event.preventDefault(); event.stopPropagation(); claim.scrollIntoView({ block: 'start' }); });
    ['damage', 'valuation'].forEach(function (key) { if (links[key]) { links[key].setAttribute('data-v28-removed', 'P26'); links[key].hidden = true; } });
    order.forEach(function (key) { if (links[key]) { nav.appendChild(links[key]); var label = links[key].querySelector('span'); if (rename[key] && label) label.textContent = rename[key]; } });
    var imagesTab = $$('#section-files [role="tab"], #section-files .tab').filter(function (t) { return /^Images/i.test(clean(t.textContent)); })[0];
    if (imagesTab && imagesTab.parentNode.firstElementChild !== imagesTab) { imagesTab.parentNode.insertBefore(imagesTab, imagesTab.parentNode.firstElementChild); imagesTab.click(); }
  }


  // ---- P30 · Report wording ----------------------------------------------
  // Every narrative block the report prints, in print order, composed from the
  // fields. While editing a block can be reworded in place (it then stops
  // tracking its fields until recomposed), renamed, removed, brought back,
  // moved up or down, and a free paragraph added. Read mode shows the blocks
  // as derived cells. This crosses FRD-11's fixed-template rule (v27 item I2).
  var SALVAGE_TEXT = {
    a: 'Under the current salvage categorisation matrix, within the scope of our inspection, we consider that this is Category A (scrap only: the vehicle must be crushed in its entirety with no parts recovery). We suggest that the sale of the salvage will realise in the order of {s}. We have not taken any action towards removal of the salvage at this time.',
    b: 'Under the current salvage categorisation matrix, within the scope of our inspection, we consider that this is Category B (break for spare parts: the body shell must be crushed). We suggest that the sale of the salvage will realise in the order of {s}. We have not taken any action towards removal of the salvage at this time.',
    s: 'Under the current salvage categorisation matrix, within the scope of our inspection, we consider that this is Category S (structural damage) and can be sold as repairable salvage. Further information is available at www.abi.org.uk. We suggest that the sale of the salvage will realise in the order of {s}. We have not taken any action towards removal of the salvage at this time.',
    n: 'Under the current salvage categorisation matrix, within the scope of our inspection, we consider that this is Category N (non-structural damage) and can be sold as repairable salvage. Further information is available at www.abi.org.uk. We suggest that the sale of the salvage will realise in the order of {s}. We have not taken any action towards removal of the salvage at this time.'
  };
  function p30Wording() {
    var pane = document.querySelector('#section-report [data-v28-pane="report"]') || document.querySelector('#section-report > .panel-body'); if (!pane) return;
    var composed = function (key) { var el = document.querySelector('[data-composed="' + key + '"]'); return el ? el.textContent : ''; };
    var switchOn = function (path) { var box = document.querySelector('[data-report-switch^="' + path + '"]'); if (box) return box.checked; return new RegExp(path === 'report.valuation_comment' ? 'valuation commentary' : path === 'report.include_unrelated' ? 'unrelated damage' : 'xx', 'i').test(((document.querySelector('[data-report-content]') || {}).textContent || '')); };
    var repairCost = function () {
      var total = $$('#section-estimate *').filter(function (e) { return e.children.length === 0 && /^Repair cost inc VAT/.test(clean(e.textContent)); })[0];
      return total && total.parentNode ? number((total.parentNode.lastElementChild || total).textContent) : 0;
    };
    var outcome = function () { return value('section-settlement', 'Outcome').toLowerCase(); };
    var settlement = function () {
      var ev = engineersValue(), repair = repairCost(), salvage = number(value('section-settlement', 'Salvage value')), o = outcome();
      if (/total loss/.test(o)) return 'We consider that an equitable settlement would be ' + money(ev - salvage) + ', which represents the pre-accident engineer value of the vehicle of ' + money(ev) + ' less the value of the salvage of ' + money(salvage) + '.';
      if (/cash in lieu/.test(o)) return 'We recommend settlement by way of a cash in lieu payment based upon the estimated repair cost of ' + money(repair) + '.';
      if (/contract/.test(o)) return 'A contract repair has been agreed for the total sum of ' + money(repair) + '. Costs cannot increase above this figure.';
      if (/repairable/.test(o)) return 'This vehicle is considered a repairable proposition and we have calculated a repair cost of ' + money(repair) + '. We recommend a repair reserve of ' + money(Math.ceil(repair / 50) * 50) + ' would be reasonable on this occasion.';
      return '';
    };
    var BLOCKS = [
      { id: 'nature', title: 'Nature of incident', always: true, source: function () { var n = value('section-damage', 'Incident narrative'); if (n) return n; var where = clean((document.querySelector('[data-damage-location]') || {}).textContent); return where && where !== 'Not recorded' ? 'The vehicle has sustained collision/impact damage to the ' + where.replace('Multiple · ', '') + '.' : ''; } },
      { id: 'comments', title: 'Engineer\u2019s comments', always: true, source: function () { var bits = [composed('mileage'), value('section-report', 'Engineer\u2019s comments') || value('section-report', "Engineer's comments")]; if (/unroadworthy/i.test(value('section-settlement', 'Roadworthiness'))) { var r = value('section-settlement', 'Unroadworthy reason'); if (r) bits.push('Please note the vehicle is unroadworthy: ' + r.charAt(0).toLowerCase() + r.slice(1)); } return bits.filter(Boolean).join('\n\n'); } },
      { id: 'supplementary', title: 'Supplementary damage', auto: true, chip: 'from Estimate', source: function () { var el = document.querySelector('.v28-supp [data-composed="supp"]'); return el && !el.closest('[hidden]') ? el.textContent : ''; } },
      { id: 'commentary', title: 'Valuation commentary', optional: 'report.valuation_comment', source: function () { return value('section-report', 'Valuation commentary'); } },
      { id: 'unrelated', title: 'Unrelated damage', optional: 'report.include_unrelated', source: function () { var d = value('section-damage', 'Unrelated damage'); return d ? 'Unrelated pre-existing damage was noted: ' + d.replace(/\.$/, '') + '. This damage is inconsistent with the reported incident and has been disregarded for the purposes of this assessment.' : ''; } },
      { id: 'history', title: 'Vehicle history check', always: true, chip: 'pass-through', source: function () { return value('section-vehicle', 'Vehicle history'); } },
      { id: 'condition', title: 'Pre-incident condition', always: true, source: function () { return composed('condition'); } },
      { id: 'settlement', title: 'Settlement', always: true, source: settlement },
      { id: 'salvage', title: 'Salvage', visible: function () { return /total loss/.test(outcome()); }, source: function () { var c = (value('section-settlement', 'Salvage category').match(/\b([ABSN])\b/i) || [])[1]; return c && SALVAGE_TEXT[c.toLowerCase()] ? SALVAGE_TEXT[c.toLowerCase()].replace('{s}', money(number(value('section-settlement', 'Salvage value')))) : ''; } }
    ];
    var state = {}; BLOCKS.forEach(function (b) { state[b.id] = { edited: false, text: '', off: false, title: null }; });
    var order = BLOCKS.map(function (b) { return b.id; }); var custom = []; var sequence = 1;
    var all = function () { return BLOCKS.concat(custom); };
    var find = function (id) { return all().filter(function (b) { return b.id === id; })[0]; };
    var title = function (b) { return state[b.id].title || b.title; };
    var shown = function (b) { var st = state[b.id]; if (st.off) return false; if (b.custom) return true; if (b.visible) return b.visible(); if (b.always) return true; if (b.auto) return !!b.source(); return switchOn(b.optional); };

    var panel = document.createElement('div'); panel.className = 'sub-panel v28-wording'; mark(panel, 'P30');
    panel.innerHTML = '<h3>Report wording<span class="rt">in print order</span></h3><div class="stack" data-wb-list></div><div class="button-row" data-wb-add></div>';
    var images = pane.querySelector('[data-report-images]');
    pane.insertBefore(panel, images || null);
    var list = panel.querySelector('[data-wb-list]'), adder = panel.querySelector('[data-wb-add]');

    var draw = function () {
      var blocks = order.map(find).filter(Boolean).filter(shown);
      list.innerHTML = blocks.map(function (b, index) {
        var st = state[b.id], text = st.edited ? st.text : b.source();
        var chip = b.custom ? '<span class="src-tag">manual</span>' : st.edited ? '<span class="src-tag src-tag--warn">edited</span>' : '<span class="src-tag">' + (b.chip || 'tracks fields') + '</span>';
        if (!editing) return '<div class="fc ro"><span class="lbl">' + escape(title(b)) + ' ' + chip + '</span><div class="fv derived multi' + (text ? '' : ' empty') + '">' + escape(text || 'Not recorded') + '</div></div>';
        return '<div class="v28-wb' + (st.edited ? ' edited' : '') + '" data-wb="' + b.id + '"><div class="v28-wbh"><span class="v28-wbt" contenteditable="true" spellcheck="false" data-wb-title="' + b.id + '">' + escape(title(b)) + '</span>' + chip
          + (st.edited && !b.custom ? '<button type="button" class="link-button" data-wb-recompose="' + b.id + '">Recompose from fields</button>' : '')
          + '<span class="v28-wb-tools"><button type="button" class="icon-button" data-wb-up="' + b.id + '" aria-label="Move ' + escape(title(b)) + ' up"' + (index === 0 ? ' disabled' : '') + '>\u2191</button>'
          + '<button type="button" class="icon-button" data-wb-down="' + b.id + '" aria-label="Move ' + escape(title(b)) + ' down"' + (index === blocks.length - 1 ? ' disabled' : '') + '>\u2193</button>'
          + '<button type="button" class="icon-button" data-wb-remove="' + b.id + '" aria-label="Remove ' + escape(title(b)) + '">\u00d7</button></span></div>'
          + '<textarea class="fi" data-wb-text="' + b.id + '" rows="2">' + escape(text) + '</textarea></div>';
      }).join('');
      $$('textarea', list).forEach(function (area) { area.style.height = 'auto'; area.style.height = (area.scrollHeight + 2) + 'px'; });
      if (!editing) { adder.innerHTML = ''; return; }
      var back = BLOCKS.filter(function (b) { if (state[b.id].off) return b.visible ? b.visible() : true; return b.optional && !shown(b); });
      adder.innerHTML = back.map(function (b) { return '<button type="button" class="btn btn--small" data-wb-add-back="' + b.id + '">+ ' + escape(title(b)) + '</button>'; }).join('')
        + '<button type="button" class="btn btn--small" data-wb-new>+ New paragraph</button>';
    };
    var setSwitch = function (path, on) { var box = document.querySelector('[data-report-switch^="' + path + '"]'); if (box) { box.checked = on; box.dispatchEvent(new Event('change', { bubbles: true })); } };
    panel.addEventListener('click', function (event) {
      var t = event.target, hit;
      if ((hit = t.closest('[data-wb-remove]'))) { var b = find(hit.getAttribute('data-wb-remove')); if (b.custom) { custom = custom.filter(function (c) { return c !== b; }); order = order.filter(function (id) { return id !== b.id; }); } else if (b.optional) setSwitch(b.optional, false); else state[b.id].off = true; draw(); }
      else if ((hit = t.closest('[data-wb-add-back]'))) { var back = find(hit.getAttribute('data-wb-add-back')); state[back.id].off = false; if (back.optional) setSwitch(back.optional, true); draw(); }
      else if (t.closest('[data-wb-new]')) { var id = 'custom' + (sequence++); custom.push({ id: id, title: 'New paragraph', custom: true, source: function () { return ''; } }); state[id] = { edited: true, text: '', off: false, title: null }; order.push(id); draw(); var last = $$('.v28-wbt', list).pop(); if (last) last.focus(); }
      else if ((hit = t.closest('[data-wb-recompose]'))) { var r = hit.getAttribute('data-wb-recompose'); state[r].edited = false; state[r].text = ''; draw(); }
      else if ((hit = t.closest('[data-wb-up], [data-wb-down]'))) {
        var moving = hit.getAttribute('data-wb-up') || hit.getAttribute('data-wb-down'); var visible = order.filter(function (id) { return shown(find(id)); });
        var at = visible.indexOf(moving), swapWith = visible[at + (hit.hasAttribute('data-wb-up') ? -1 : 1)];
        if (swapWith) { var i = order.indexOf(moving), j = order.indexOf(swapWith); order[i] = swapWith; order[j] = moving; draw(); }
      }
    });
    panel.addEventListener('input', function (event) {
      var area = event.target.closest('[data-wb-text]'); if (!area) return;
      var b = find(area.getAttribute('data-wb-text')); area.style.height = 'auto'; area.style.height = (area.scrollHeight + 2) + 'px';
      state[b.id].text = area.value; if (!b.custom) { var was = state[b.id].edited; state[b.id].edited = area.value !== b.source(); if (was !== state[b.id].edited) { var start = area.selectionStart; draw(); var again = list.querySelector('[data-wb-text="' + b.id + '"]'); if (again) { again.focus(); again.setSelectionRange(start, start); } } }
    });
    panel.addEventListener('focusout', function (event) {
      var heading = event.target.closest && event.target.closest('[data-wb-title]'); if (!heading) return;
      var b = find(heading.getAttribute('data-wb-title')), typed = clean(heading.textContent);
      if (typed) { if (b.custom) b.title = typed; else state[b.id].title = typed === b.title ? null : typed; }
      draw();
    });
    var redrawFromFields = function (event) { if (event.target.closest && event.target.closest('.v28-wording')) return; draw(); };
    document.addEventListener('input', redrawFromFields); document.addEventListener('change', redrawFromFields);
    draw();
  }


  // ======================= Repair Spec (fourth pass) =======================
  // The Estimate section is renamed and reworked. Conceptual source: section 6
  // of the operator's reference file ("Repair specification").

  // ---- P31 · "Estimate" becomes "Repair Spec" -----------------------------
  function p31Rename() {
    var title = $('section-estimate-title'); if (title) { title.setAttribute('data-v28-live-text', title.textContent); title.textContent = 'Repair Spec'; mark(title, 'P31'); }
    var link = document.querySelector('[data-section-link="estimate"] span'); if (link) link.textContent = 'Repair Spec';
  }

  // ---- P32 · Fewer header fields; a tab is renamed by double-clicking it --
  function p32Trim() {
    var section = $('section-estimate'); if (!section) return;
    var hide = function (el) { if (el) { el.setAttribute('data-v28-removed', 'P32'); el.hidden = true; } };
    // Estimate notes go, in read and in edit. The control stays in the form, hidden.
    $$('.fc', section).forEach(function (fc) { var l = fc.querySelector('label, .lbl'); if (l && clean(l.textContent) === 'Estimate notes') hide(fc); });
    $$('.est-head-read span', section).forEach(function (span) { if (/^Repair days/.test(clean(span.textContent))) hide(span); });
    if (!editing) return;
    ['estimate-name', 'estimate-days'].forEach(function (id) { var input = $(id); if (input) hide(input.closest('.fc')); });
    var name = $('estimate-name');
    var tab = section.querySelector('.estimate-tab[aria-selected="true"]'); var label = tab && tab.querySelector('span');
    if (!name || !label) return;
    tab.setAttribute('title', 'Double-click to rename'); mark(label, 'P32');
    tab.addEventListener('dblclick', function (event) {
      event.preventDefault(); event.stopPropagation();
      label.setAttribute('contenteditable', 'true'); label.setAttribute('spellcheck', 'false'); label.focus();
      var range = document.createRange(); range.selectNodeContents(label); var sel = window.getSelection(); sel.removeAllRanges(); sel.addRange(range);
    }, true);
    tab.addEventListener('click', function (event) { if (label.isContentEditable) { event.preventDefault(); event.stopPropagation(); } }, true);
    var finish = function (keep) {
      label.removeAttribute('contenteditable');
      var typed = clean(label.textContent);
      if (keep && typed) { name.value = typed; name.dispatchEvent(new Event('input', { bubbles: true })); } else label.textContent = name.value;
    };
    label.addEventListener('blur', function () { finish(true); });
    label.addEventListener('keydown', function (event) { if (event.key === 'Enter') { event.preventDefault(); label.blur(); } if (event.key === 'Escape') { finish(false); label.blur(); } });
  }

  // ---- P33 · One labour rate control --------------------------------------
  // The rate card and the rate were two cells for one fact. The card chooses
  // the figure; typing a figure is the custom rate.
  function p33LabourRate() {
    var card = $('estimate-rate-card'), rate = $('estimate-labour-rate'); if (!editing || !card || !rate) return;
    var cardCell = card.closest('.fc'), rateCell = rate.closest('.fc');
    var label = cardCell.querySelector('label'); if (label) { label.textContent = 'Labour rate (\u00a3/h)'; label.setAttribute('for', 'estimate-labour-rate'); }
    var pair = document.createElement('div'); pair.className = 'v28-rate'; mark(pair, 'P33');
    card.parentNode.insertBefore(pair, card); pair.appendChild(card); pair.appendChild(rate);
    card.setAttribute('aria-label', 'Labour-rate card');
    rateCell.setAttribute('data-v28-removed', 'P33'); rateCell.hidden = true;
    rate.addEventListener('input', function () { if (card.options.length) card.selectedIndex = 0; });
  }

  // ---- Repair Spec arithmetic, for the slider's preview -------------------
  function specRows() {
    return $$('#section-estimate tr[data-estimate-line]').map(function (tr) {
      var get = function (n) { return tr.querySelector('[name="' + n + '"]'); };
      return { unit: get('linePartPounds'), qty: get('lineQuantity'), hours: get('lineLabourHours'), paint: get('linePaintHours') };
    }).filter(function (r) { return r.unit; });
  }
  function specGross(rows, prices, rate, materials, other) {
    var parts = 0, labour = 0;
    rows.forEach(function (r, i) { parts += (number(r.qty.value) || 1) * prices[i]; labour += number(r.hours.value) * rate; });
    var net = parts + labour + materials + other;
    var vatOn = function (id) { var b = $(id); return !!(b && b.checked); };
    var percent = number(($('estimate-vat') || {}).value) / 100;
    var vat = percent * ((vatOn('estimateVatParts') ? parts : 0) + (vatOn('estimateVatLabour') ? labour : 0) + (vatOn('estimateVatMaterials') ? materials : 0));
    return { parts: parts, labour: labour, materials: materials, net: net, vat: vat, gross: net + vat };
  }
  function showRollup(t) {
    var set = function (label, amount) { $$('#section-estimate [data-estimate-rollup] .rr').forEach(function (rr) { var dt = rr.querySelector('dt'); if (dt && clean(dt.textContent).indexOf(label) === 0) rr.querySelector('dd').textContent = money(amount); }); };
    set('Parts', t.parts); set('Panel labour', t.labour); set('Materials', t.materials); set('Net', t.net); set('VAT', t.vat); set('Repair cost inc VAT', t.gross);
  }

  // ---- P34 · Target % of value: scale the spec down -----------------------
  // One factor lowers the part prices and materials, and the labour rate, each
  // to its floor; hours are never touched. The slider previews; Apply keeps it;
  // Remove scaling puts the specification back exactly as it was.
  var scaleTo = null; // set by P34, used by P35
  function p34Target() {
    var grid = document.querySelector('#section-estimate [data-estimate-discounts]'); var rate = $('estimate-labour-rate'); if (!editing || !grid || !rate) return;
    var bar = document.createElement('div'); bar.className = 'bar v28-scale'; bar.id = 'v28-target'; mark(bar, 'P34');
    bar.innerHTML = '<span class="bl">Target % of value</span><span class="status status--amber status--plain" data-scaled hidden>Scaled</span>'
      + '<input type="range" min="0" max="100" step="1" aria-label="Target repair cost as a percentage of the Engineer\u2019s Value" data-range />'
      + '<span class="dsc"><input type="number" step="1" aria-label="Target percentage" data-percent /><label>%</label></span>'
      + '<span class="mono v28-scale-read" data-read></span><span class="status status--navy status--plain" data-preview hidden>Preview</span>'
      + '<span class="v28-floors">Floors <span class="dsc"><label for="v28-floor-rate">labour \u00a3/h</label><input id="v28-floor-rate" type="number" step="1" value="50" data-floor-rate /></span>'
      + '<span class="dsc"><label for="v28-floor-price">prices %</label><input id="v28-floor-price" type="number" step="5" value="65" data-floor-price /></span></span>'
      + '<button type="button" class="btn btn--small btn--primary" data-apply hidden>Apply</button><button type="button" class="btn btn--small" data-remove disabled>Remove scaling</button>';
    grid.parentNode.insertBefore(bar, grid);
    var q = function (sel) { return bar.querySelector(sel); };
    var materialsInput = $('estimate-materials'), otherInput = $('estimate-other');
    var original = null, applied = false;
    var snapshot = function () { var rows = specRows(); return { rows: rows, prices: rows.map(function (r) { return number(r.unit.value); }), rate: number(rate.value), materials: number(materialsInput.value), other: number(otherInput.value) }; };
    var at = function (o, k) {
      var floorPrice = number(q('[data-floor-price]').value) / 100, floorRate = number(q('[data-floor-rate]').value);
      var pk = Math.max(floorPrice, k);
      return { prices: o.prices.map(function (x) { return x * pk; }), rate: Math.max(Math.min(floorRate, o.rate), o.rate * k), materials: o.materials * pk, pk: pk };
    };
    var grossAt = function (o, k) { var a = at(o, k); return specGross(o.rows, a.prices, a.rate, a.materials, o.other).gross; };
    var refresh = function () {
      var ev = engineersValue(), o = original || snapshot();
      var enabled = ev > 0 && o.rows.length > 0;
      q('[data-range]').disabled = q('[data-percent]').disabled = !enabled;
      if (!enabled) { q('[data-read]').textContent = ev ? '' : 'Apply in Valuation'; return; }
      var top = grossAt(o, 1) / ev * 100, bottom = grossAt(o, 0) / ev * 100;
      q('[data-range]').min = q('[data-percent]').min = Math.ceil(bottom); q('[data-range]').max = q('[data-percent]').max = Math.ceil(top);
      if (!original) { q('[data-range]').value = q('[data-percent]').value = Math.round(top); q('[data-read]').textContent = money(grossAt(o, 1)) + (applied ? ' \u00b7 scaled' : ' \u00b7 as estimated'); }
    };
    scaleTo = function (target) {
      var ev = engineersValue(); if (!original) original = snapshot();
      var o = original, top = grossAt(o, 1), bottom = grossAt(o, 0);
      target = Math.max(bottom, Math.min(top, target));
      var lo = 0, hi = 1; for (var i = 0; i < 40; i++) { var mid = (lo + hi) / 2; if (grossAt(o, mid) > target) hi = mid; else lo = mid; }
      var k = (lo + hi) / 2, a = at(o, k);
      o.rows.forEach(function (r, i) { r.unit.value = a.prices[i].toFixed(2); });
      rate.value = a.rate.toFixed(2); materialsInput.value = a.materials.toFixed(2);
      var totals = specGross(o.rows, a.prices, a.rate, a.materials, o.other); showRollup(totals);
      var percent = ev ? totals.gross / ev * 100 : 0;
      q('[data-range]').value = q('[data-percent]').value = Math.round(percent);
      q('[data-read]').innerHTML = money(top) + ' \u2192 <b>' + money(totals.gross) + '</b>' + (ev ? ' (' + percent.toFixed(1) + '%)' : '') + ' \u00b7 prices \u00d7' + a.pk.toFixed(2) + (a.rate < o.rate && a.rate <= number(q('[data-floor-rate]').value) ? ' \u00b7 labour at floor' : '');
      q('[data-preview]').hidden = false; q('[data-apply]').hidden = false; q('[data-remove]').disabled = false;
      $('section-estimate').classList.add('v28-scaling');
    };
    var byPercent = function (v) { var ev = engineersValue(); if (ev) scaleTo(ev * number(v) / 100); };
    q('[data-range]').addEventListener('input', function (e) { byPercent(e.target.value); });
    q('[data-percent]').addEventListener('change', function (e) { byPercent(e.target.value); });
    q('[data-floor-rate]').addEventListener('change', function () { if (original) byPercent(q('[data-percent]').value); });
    q('[data-floor-price]').addEventListener('change', function () { if (original) byPercent(q('[data-percent]').value); });
    q('[data-apply]').addEventListener('click', function () {
      applied = true; q('[data-preview]').hidden = true; q('[data-apply]').hidden = true; q('[data-scaled]').hidden = false;
      $('section-estimate').classList.remove('v28-scaling');
    });
    q('[data-remove]').addEventListener('click', function () {
      if (!original) return; var o = original;
      o.rows.forEach(function (r, i) { r.unit.value = o.prices[i].toFixed(2); }); rate.value = o.rate.toFixed(2); materialsInput.value = o.materials.toFixed(2);
      showRollup(specGross(o.rows, o.prices, o.rate, o.materials, o.other));
      original = null; applied = false; q('[data-preview]').hidden = true; q('[data-apply]').hidden = true; q('[data-scaled]').hidden = true; q('[data-remove]').disabled = true;
      $('section-estimate').classList.remove('v28-scaling'); refresh();
    });
    refresh();
  }

  // ---- P35 · Contract repair agreed ---------------------------------------
  function p35ContractRepair() {
    var before = document.querySelector('#section-estimate [data-estimate-discounts]'); if (!editing || !before) return;
    var bar = document.createElement('div'); bar.className = 'bar v28-contract'; bar.id = 'v28-contract'; mark(bar, 'P35');
    bar.innerHTML = '<span class="bl">Contract repair</span><label class="choice"><input type="checkbox" data-agreed /><span>Contract repair agreed</span></label>'
      + '<span class="dsc"><label for="v28-agreed-sum">agreed total sum \u00a3</label><input id="v28-agreed-sum" type="number" step="0.01" disabled data-sum /></span>'
      + '<span class="status status--amber status--plain" data-mismatch hidden></span>'
      + '<div class="fc ro v28-composed" data-sentence hidden><span class="lbl">Contract repair</span><div class="fv derived" data-composed="contract"></div></div>';
    before.parentNode.insertBefore(bar, before);
    var tick = bar.querySelector('[data-agreed]'), sum = bar.querySelector('[data-sum]'), outcome = $('f-assessment-outcome'), previous = '';
    var gross = function () { var el = document.querySelector('#section-estimate [data-estimate-gross]'); return el ? number(el.textContent) : 0; };
    var setOutcome = function (wanted) {
      if (!outcome) return; var option = $$('option', outcome).filter(function (o) { return wanted ? /contract/i.test(o.textContent) : o.value === previous; })[0];
      if (option) { outcome.value = option.value; outcome.dispatchEvent(new Event('input', { bubbles: true })); outcome.dispatchEvent(new Event('change', { bubbles: true })); }
    };
    var refresh = function () {
      var on = tick.checked, agreed = number(sum.value);
      bar.querySelector('[data-sentence]').hidden = !on;
      bar.querySelector('[data-composed="contract"]').textContent = on ? 'A contract repair has been agreed for the total sum of ' + money(agreed) + '. Costs cannot increase above this figure.' : '';
      var diff = gross() - agreed, chip = bar.querySelector('[data-mismatch]');
      chip.hidden = !on || Math.abs(diff) <= 0.5;
      chip.textContent = diff > 0 ? 'Spec ' + money(gross()) + ' above agreed' : 'Spec ' + money(gross()) + ' below agreed';
    };
    tick.addEventListener('change', function () {
      sum.disabled = !tick.checked;
      if (tick.checked) { previous = outcome ? outcome.value : ''; sum.value = gross().toFixed(2); setOutcome(true); } else { sum.value = ''; setOutcome(false); }
      refresh();
    });
    sum.addEventListener('change', function () { var agreed = number(sum.value); if (agreed > 0 && scaleTo && Math.abs(agreed - gross()) > 0.005) scaleTo(agreed); refresh(); });
    if (outcome) outcome.addEventListener('change', function () { var is = /contract/i.test((outcome.selectedOptions[0] || {}).textContent || ''); if (is !== tick.checked) { tick.checked = is; sum.disabled = !is; if (is && !sum.value) sum.value = gross().toFixed(2); refresh(); } });
    refresh();
  }

  // ---- P36 · The Glass's slot and its session states ----------------------
  // Live behaviour, not a proposal: the fixture has no enabled Glass's account,
  // so the application rendered none of it. Drawn here with the markup, tones
  // and wording of _CaseEstimate.cshtml and GlassLabels. ?glass=<state> picks
  // the state: free (default), open, recording, waiting, recorded, failed,
  // unknown, cancelled.
  var GLASS = {
    open: ['Open', 'status--blue', null, null], recording: ['Recording', 'status--blue', null, null],
    waiting: ['Waiting', 'status--amber', 'notice--warning', 'The Glass\u2019s estimate is held. Not yet recorded.'],
    recorded: ['Recorded', 'status--blue', 'notice--success', 'The Glass\u2019s estimate was recorded as a draft.'],
    failed: ['Failed', 'status--red', 'notice--warning', 'The Glass\u2019s estimate was not recorded.'],
    unknown: ['Unknown', 'status--amber', 'notice--warning', 'The Glass\u2019s session result is not yet known.'],
    cancelled: ['Cancelled', 'status--blue', '', 'The Glass\u2019s session was closed.']
  };
  function p36Glass() {
    var section = $('section-estimate'); if (!section) return;
    var state = (params.get('glass') || 'free').toLowerCase(); var session = GLASS[state];
    var actions = section.querySelector('.panel-head .panel-actions'), body = section.querySelector('.panel-body');
    if (editing && actions && (state === 'free' || state === 'open' || state === 'failed' || state === 'waiting')) {
      var resume = state !== 'free';
      var form = document.createElement('form'); form.setAttribute('method', 'post'); form.setAttribute('target', '_blank');
      form.setAttribute('action', '/Cases/Details?handler=' + (resume ? 'ResumeGlass' : 'LaunchGlass')); form.setAttribute('data-glass-slot', resume ? 'resume' : 'launch'); mark(form, 'P36');
      form.innerHTML = '<button type="submit" class="btn btn--small"><svg class="icon" aria-hidden="true"><use href="#icon-' + (resume ? 'play' : 'external-link') + '" /></svg><span>' + (resume ? 'Resume' : 'Glass\u2019s') + '</span></button>';
      var importer = actions.querySelector('[data-estimate-import]'); var after = importer ? (importer.closest('form') || importer) : null;
      actions.insertBefore(form, after ? after.nextSibling : actions.firstChild);
    }
    if (!session || !body) return;
    if (session[3]) {
      var notice = document.createElement('div'); notice.className = 'notice ' + session[2]; notice.setAttribute('role', 'status'); notice.setAttribute('data-estimate-notice', ''); mark(notice, 'P36');
      notice.innerHTML = '<svg class="icon" aria-hidden="true"><use href="#icon-info" /></svg><span>' + session[3] + '</span><button type="button" class="dismiss" data-dismiss aria-label="Dismiss"><svg class="icon" aria-hidden="true"><use href="#icon-x" /></svg></button>';
      body.insertBefore(notice, body.firstChild);
    }
    if (state === 'recorded' || state === 'cancelled') return; // a settled session leaves only its sentence
    var line = document.createElement('div'); line.className = 'glass-session'; line.setAttribute('data-glass-session', state); mark(line, 'P36');
    line.innerHTML = '<span class="status ' + session[1] + '">Glass\u2019s session \u00b7 ' + session[0] + '</span><span class="muted grow">started 11:30</span>'
      + (state === 'failed' ? '<span class="glass-fail" data-glass-failure>provider_timeout</span>' : '')
      + (state === 'waiting' ? (editing ? '<form method="post" action="/Cases/Details?handler=CompleteEstimateImport"><button type="submit" class="btn btn--small btn--primary"><svg class="icon" aria-hidden="true"><use href="#icon-check" /></svg><span>Complete import</span></button></form>'
        : '<span class="gated"><svg class="icon" aria-hidden="true"><use href="#icon-lock" /></svg><span>Available while editing</span></span>') : '')
      + (state !== 'recording' ? '<details class="glass-close" data-glass-close><summary><svg class="icon" aria-hidden="true"><use href="#icon-x" /></svg>Close session</summary>'
        + '<form method="post" action="/Cases/Details?handler=CloseGlass" class="glass-close-form"><div class="fc"><label for="glass-close-reason">Reason</label><input id="glass-close-reason" class="fi" name="reason" maxlength="2000" required /></div>'
        + '<label class="choice" for="glass-external-closed"><input id="glass-external-closed" type="checkbox" name="externalSessionClosed" value="true" required /><span>Glass\u2019s is closed and no estimate remains open</span></label>'
        + '<p class="consequence">Closing this record releases the Glass\u2019s account for another estimate.</p><button type="submit" class="btn btn--danger">Close session</button></form></details>' : '');
    var tabs = body.querySelector('[data-estimate-tabs]'); body.insertBefore(line, tabs ? tabs.nextSibling : body.firstChild);
  }

  // ---- P8c · A valuation card is chosen by clicking it --------------------
  function p8cBasis() {
    var cards = $$('#section-valuation .valuation-card.entry, #section-valuation .valuation-card[data-valuation-card]'); if (!editing || !cards.length) return;
    cards.forEach(function (card) {
      var radio = card.querySelector('input[type="radio"]');
      var label = radio && radio.closest('label'); if (label) { label.setAttribute('data-v28-removed', 'P8'); label.hidden = true; }
      card.classList.add('v28-pickable'); card.setAttribute('tabindex', '0'); card.setAttribute('role', 'radio'); card.setAttribute('aria-checked', String(card.classList.contains('sel')));
      var choose = function (event) {
        if (event.target.closest('input, select, textarea, button, a')) return;
        var retail = card.querySelector('[data-valuation-retail]'); if (retail && !number(retail.value)) return; // nothing to base a value on yet
        cards.forEach(function (c) { c.classList.toggle('sel', c === card); c.setAttribute('aria-checked', String(c === card)); });
        if (radio) { radio.checked = true; radio.dispatchEvent(new Event('change', { bubbles: true })); }
      };
      card.addEventListener('click', choose);
      card.addEventListener('keydown', function (event) { if ((event.key === 'Enter' || event.key === ' ') && event.target === card) { event.preventDefault(); choose(event); } });
    });
  }

  function run() {
    record = document.querySelector('.case-record'); if (!record || !$('section-overview')) return;
    editing = record.classList.contains('is-editing');
    document.documentElement.setAttribute('data-v28-record', editing ? 'edit' : 'read');
    [['P13', p13Cap], ['P29', p29Decisions], ['P14', p14Salvage], ['P15', p15Bank], ['P16', p16EstimateLines], ['P17', p17Uplift], ['P18', p18Provenance],
      ['P19', p19Compare], ['P20', p20Supplementary], ['P25', p25Badges], ['P28', p28Queries], ['P24', p24FeeTab], ['P21', p21Delivery], ['P27', p27Include],
      ['P32', p32Trim], ['P33', p33LabourRate], ['P34', p34Target], ['P35', p35ContractRepair], ['P36', p36Glass], ['P8', p8cBasis],
      ['P12', p12Composed], ['P30', p30Wording], ['P26', p26NineSections], ['P31', p31Rename]].forEach(function (proposal) {
      if (!on(proposal[0])) return;
      try { proposal[1](); } catch (error) { console.error('v28 proposal ' + proposal[0] + ' failed: ' + error.message); }
    });
    // Review presets for states that need a gesture: ?demo=scale, ?demo=contract.
    var demo = params.get('demo');
    if (demo === 'scale') { var range = document.querySelector('.v28-scale [data-range]'); if (range && !range.disabled) { range.value = range.min; range.dispatchEvent(new Event('input', { bubbles: true })); } }
    if (demo === 'contract') { var agreed = document.querySelector('.v28-contract [data-agreed]'); if (agreed) { agreed.checked = true; agreed.dispatchEvent(new Event('change', { bubbles: true })); } }
  }
  // After proposals.js, which also waits for the document.
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', function () { setTimeout(run, 0); }); else setTimeout(run, 0);
})();
