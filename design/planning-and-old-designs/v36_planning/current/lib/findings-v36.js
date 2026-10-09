// v36 mockup only: one function per finding, applied to a captured Case
// page frame. Each transform uses the live classes and tokens, marks what it
// changed with data-v36-change, and returns true when it changed anything.
// Every word written here is the application's own label or a fixture.
(() => {
  'use strict';

  const text = (el) => (el ? el.textContent.replace(/\s+/g, ' ').trim() : '');
  const mark = (el, id) => { if (el && !el.dataset.v36Change) el.dataset.v36Change = id; return el; };
  const icon = (id) => `<svg class="icon" aria-hidden="true"><use href="#${id}"></use></svg>`;
  const ABSENT = 'Not recorded';

  // The cell whose label line starts with the words.
  function cell(root, words) {
    for (const fc of root.querySelectorAll('.fc')) {
      const label = fc.querySelector(':scope > label, :scope > .lbl, :scope > span.lbl');
      if (!label) continue;
      const t = text(label).replace(/\s*(Extracted|AI|E-mail|Lookup|Principal|Automatic|Principal API)$/, '');
      if (t === words || t.startsWith(words + ' ')) return fc;
    }
    return null;
  }
  const sub = (root, key) => root.querySelector(`[data-collapse="${key}"]`);
  const section = (root, key) => root.querySelector(`#section-${key}`);
  const subByTitle = (root, words) => [...root.querySelectorAll('.sub-panel > h3')].find((h) => text(h).startsWith(words))?.parentElement ?? null;
  const absentWord = (fc) => text(fc?.querySelector('.fv')) || ABSENT;

  const F = [];
  const add = (id, surface, tier, title, apply, extra = {}) => F.push({ id, surface, tier, title, apply, ...extra });

  // ---- ribbon and section row ------------------------------------------------
  add('f01', 'Section row', 'a', 'The section row never clips: no icons below 1500px, tighter links, and it wraps below 980px', (root) => {
    const nav = root.querySelector('.section-nav');
    if (!nav) return false;
    nav.classList.add('v36-f01'); mark(nav, 'f01'); return true;
  });
  add('f02', 'Ribbon', 'a', 'The Saving / Saved word is body-size sans in a fixed cell, so Done never moves', (root, ctx) => {
    const line = root.querySelector('[data-lease-line]');
    if (!line || ctx.mode !== 'edit') return false;
    line.classList.add('v36-f02'); mark(line, 'f02'); return true;
  });
  add('f03', 'Ribbon', 'a', 'Principal is a code and takes natural width; Claimant and Engineer get the room', (root) => {
    const item = [...root.querySelectorAll('.ribbon .ribbon-item')].find((i) => text(i.querySelector('.ribbon-label')) === 'Principal');
    if (!item) return false;
    item.classList.add('v36-f03'); mark(item, 'f03'); return true;
  });
  add('f04', 'Section heads', 'b', '"{Name} is editing" is said once, on the ribbon; section heads drop the repeated label', (root, ctx) => {
    let changed = false;
    for (const s of root.querySelectorAll('.record-section > .panel-head .gated.avail')) {
      if (/ is editing$/.test(text(s))) { mark(s.closest('.panel-head'), 'f04'); s.remove(); changed = true; }
    }
    return changed;
  }, { states: ['colleague'] });
  add('f53', 'Ribbon', 'b', 'Below 980px only the section row stays sticky; the ribbon scrolls away (345px of sticky chrome today)', (root) => {
    const block = root.querySelector('.sticky-block');
    if (!block) return false;
    block.classList.add('v36-f53'); mark(block, 'f53'); return true;
  });

  // ---- aside -------------------------------------------------------------------
  add('f06', 'Aside · Figures', 'a', 'An absent figure is one glyph in one weight', (root) => {
    let changed = false;
    for (const dd of root.querySelectorAll('.figures dd')) {
      if (/^[—–-]$/.test(text(dd))) { dd.textContent = '—'; dd.classList.add('v36-absent'); mark(dd.closest('.figure'), 'f06'); changed = true; }
    }
    return changed;
  });
  add('f07', 'Aside · folded strip', 'a', 'Below 1441px the cards keep their own height and Report not ready spans the strip', (root) => {
    const aside = root.querySelector('.workspace-aside');
    if (!aside) return false;
    aside.classList.add('v36-f07'); mark(aside, 'f07'); return true;
  });
  add('f08', 'Aside', 'a', 'Aside card heads share the 51px section head height', (root) => {
    const aside = root.querySelector('.workspace-aside');
    if (!aside) return false;
    aside.classList.add('v36-f08'); return true;
  });
  add('f09', 'Aside · Next action', 'b', 'On a Held Case the step names the review date and its control is Release Hold, not Case details', (root) => {
    const step = root.querySelector('[data-next-action] .next-step');
    if (!step || text(step.querySelector('[data-next-label]')) !== 'Held') return false;
    // The fixture's review date (HoldReviewOn 20 May 2031); the live chip reads "Held" alone here.
    step.querySelector('[data-next-label]').textContent = 'Held · review on 20 May 2031';
    const go = step.querySelector('.next-step-go');
    if (go) { const b = document.createElement('button'); b.type = 'button'; b.className = 'btn next-step-go'; b.textContent = 'Release Hold'; go.replaceWith(b); }
    step.classList.add('v36-f09'); mark(step, 'f09'); return true;
  }, { states: ['held'] });

  // ---- Case details ------------------------------------------------------------
  add('f10', 'Case details', 'a', 'Matter line spans two columns at the foot of the Case sub-panel, so it reads on one line', (root) => {
    const panel = sub(root, 'case.overview.case');
    const fc = panel && cell(panel, 'Matter line');
    if (!fc) return false;
    fc.classList.add('span2'); fc.parentElement.append(fc); mark(fc, 'f10'); return true;
  });
  add('f12', 'Every section', 'a', 'A select\'s empty option carries the cell\'s own absent word, so the control reads as the box does', (root, ctx) => {
    if (ctx.mode !== 'edit') return false;
    let changed = false;
    for (const select of root.querySelectorAll('.fc select.fi')) {
      const first = select.options[0];
      if (first && first.value === '' && text(first) === '') {
        first.textContent = absentWord(select.closest('.fc'));
        if (!select.value) select.selectedIndex = 0;
        mark(select.closest('.fc'), 'f12'); changed = true;
      }
    }
    return changed;
  });
  add('f13', 'Case details', 'b', 'Case type, Our ref and Principal are the ribbon\'s facts: the three greyed copies go', (root) => {
    let changed = false;
    for (const words of ['Case type', 'Our ref', 'Principal']) {
      const panel = words === 'Principal' ? sub(root, 'case.overview.principal') : sub(root, 'case.overview.case');
      const fc = panel && cell(panel, words);
      if (fc && text(fc.querySelector('label, .lbl')).startsWith(words)) { mark(fc.parentElement, 'f13'); fc.remove(); changed = true; }
    }
    return changed;
  });

  // ---- Claim -------------------------------------------------------------------
  add('f14', 'Claim', 'a', 'Address spans two columns, so it neither wraps reading nor clips editing, and the VAT pair shares the second row', (root) => {
    const panel = sub(root, 'case.claim.claimant');
    const fc = panel && cell(panel, 'Address');
    if (!fc) return false;
    fc.classList.add('span2'); mark(fc, 'f14'); return true;
  });

  // ---- Inspection details --------------------------------------------------------
  add('f16', 'Inspection details', 'b', 'Storage per day and Recovery charge are Engineer figures: they move to Decisions\' Costs, hire & delays; Storage location keeps the row', (root) => {
    const storage = root.querySelector('[data-inspection-storage]');
    const costs = subByTitle(root, 'Costs, hire');
    if (!storage || !costs) return false;
    const grid = costs.querySelector('.fg');
    let changed = false;
    for (const words of ['Storage per day', 'Recovery charge']) {
      const fc = cell(storage, words);
      if (fc && grid) { grid.append(fc); mark(fc, 'f16'); changed = true; }
    }
    const loc = cell(storage, 'Storage location');
    if (loc) { loc.classList.remove('span2'); loc.classList.add('span4'); }
    mark(storage, 'f16');
    return changed;
  });

  // ---- Vehicle -------------------------------------------------------------------
  add('f18', 'Vehicle', 'b', 'Experian is not connected is said once, on the Vehicle history sub-panel; the section head drops its pill', (root) => {
    const pill = root.querySelector('#section-vehicle > .panel-head [data-vehicle-experian-seam]');
    if (!pill) return false;
    mark(pill.closest('.panel-head'), 'f18'); pill.remove(); return true;
  });
  add('f19', 'Vehicle', 'a', 'The vehicle grid keeps the editable facts together and the lookup-only facts together, so the lone last cell is a lookup cell', (root) => {
    const grid = root.querySelector('[data-vehicle-facts]');
    if (!grid) return false;
    const order = ['Registration', 'Make', 'Model', 'Year', 'VIN', 'Vehicle type', 'Body type', 'Transmission', 'Engine', 'Fuel', 'Colour', 'Tax expiry', 'MOT expiry'];
    for (const words of order) { const fc = cell(grid, words); if (fc) grid.append(fc); }
    mark(grid, 'f19'); return true;
  });

  // ---- Damage --------------------------------------------------------------------
  add('f21', 'Damage', 'a', 'Unrelated damage and its deduction share one row at half width each, so the deduction label is never cut', (root) => {
    const a = cell(section(root, 'damage') ?? root, 'Unrelated damage');
    const b = cell(section(root, 'damage') ?? root, 'Unrelated-damage deduction');
    if (!a || !b) return false;
    a.classList.remove('span3'); a.classList.add('span2'); b.classList.add('span2');
    mark(a, 'f21'); mark(b, 'f21'); return true;
  });
  add('f22', 'Damage', 'b', 'The Recorded areas count box goes; the list under it already says what is recorded', (root) => {
    const fc = cell(section(root, 'damage') ?? root, 'Recorded areas');
    if (!fc) return false;
    mark(fc.parentElement, 'f22'); fc.remove(); return true;
  });
  add('f23', 'Damage', 'a', 'Reset sits in the row of area chips under the plan, not on a line of its own', (root, ctx) => {
    const reset = root.querySelector('[data-damage-reset]');
    const chips = root.querySelector('.damage-extra');
    if (!reset || !chips) return false;
    chips.append(reset); chips.classList.add('v36-f23'); mark(chips, 'f23'); return true;
  });
  add('f24', 'Damage', 'a', 'Material transfer is one cell, so Airbags deployed joins its row', (root) => {
    const fc = cell(section(root, 'damage') ?? root, 'Material transfer');
    if (!fc || !fc.classList.contains('span2')) return false;
    fc.classList.remove('span2'); mark(fc, 'f24'); return true;
  });

  // ---- Valuation -------------------------------------------------------------------
  // f25: the operator's item. Glass's is drawn as a connected card (a fixture
  // departure: the host has no provider), and Get valuation takes the chosen place.
  add('f25', 'Valuation', 'b', 'Get valuation: head-small / foot / inline (operator, 9 October 2026)', (root, ctx) => {
    const variant = ctx.opts.getval || 'today';
    if (ctx.mode !== 'edit') return false;
    const card = root.querySelector('[data-valuation-card="glasses"]');
    if (!card) return false;
    card.querySelector('[data-valuation-not-connected]')?.remove();
    let btn = card.querySelector('.valuation-card-head .btn');
    if (!btn) {
      btn = document.createElement('button'); btn.type = 'button'; btn.className = 'btn btn--small';
      btn.innerHTML = '<span>Get valuation</span>';
      card.querySelector('.valuation-card-head').append(btn);
    }
    mark(card, 'f25');
    if (variant === 'head-link') {
      // The head keeps the action as a text link beside the title, not a boxed button over the figures.
      const link = document.createElement('button'); link.type = 'button'; link.className = 'link-button v36-getval-link'; link.textContent = 'Get valuation';
      btn.replaceWith(link);
    } else if (variant === 'foot') { btn.classList.add('v36-getval-foot'); card.querySelector('.valuation-card-figs').after(btn); }
    else if (variant === 'inline') {
      // A fourth figure row: the button sits in the box column under Guide month, at the boxes' width.
      const row = document.createElement('div'); row.className = 'valuation-card-fig v36-getval-inline';
      const lbl = document.createElement('span'); lbl.className = 'lbl'; lbl.textContent = '';
      row.append(lbl, btn); card.querySelector('.valuation-card-figs').append(row);
    }
    return true;
  }, { variable: 'getval', options: ['today', 'head-link', 'foot', 'inline'] });
  add('f26', 'Valuation', 'b', 'An unconnected card states the approved sentence as one quiet line, not a blue notice box five times', (root, ctx) => {
    let changed = false;
    for (const note of root.querySelectorAll('.valuation-card-note[data-valuation-not-connected]')) {
      note.classList.add('v36-quiet'); mark(note, 'f26'); changed = true;
    }
    return changed;
  });
  add('f27', 'Valuation', 'a', 'Card boxes sit on the 32px small-control step beside 36px cells, not 28px', (root) => {
    const cards = root.querySelector('.valuation-cards');
    if (!cards) return false;
    cards.classList.add('v36-f27'); mark(cards, 'f27'); return true;
  });
  add('f28', 'Valuation', 'b', 'Reading shows Value increases and the deductions in the edit geometry, greyed, so Edit grows nothing', (root, ctx) => {
    if (ctx.mode !== 'read') return false;
    const source = document.querySelector(`#frame-${ctx.state}-edit`)?.content;
    if (!source) return false;
    const inc = source.querySelector('.sub-panel.valuation-increases');
    const ded = source.querySelector('.sub-panel.valuation-deductions');
    const target = root.querySelector('.sub-panel.valuation-deductions');
    if (!inc || !ded || !target) return false;
    const inc2 = inc.cloneNode(true); const ded2 = ded.cloneNode(true);
    for (const el of [inc2, ded2]) {
      el.classList.add('v36-f28');
      for (const c of el.querySelectorAll('input, select, textarea')) { c.setAttribute('readonly', ''); c.tabIndex = -1; }
      mark(el, 'f28');
    }
    target.replaceWith(ded2); ded2.before(inc2);
    // The same for what else only edit draws in this section: each card's
    // quiet sentence (f26's form) and the On the report switches, greyed.
    for (const card of root.querySelectorAll('.valuation-card[data-valuation-card]')) {
      const key = card.dataset.valuationCard;
      const srcNote = source.querySelector(`.valuation-card[data-valuation-card="${key}"] .valuation-card-note[data-valuation-not-connected]`);
      if (srcNote && !card.querySelector('.valuation-card-note')) { const n = srcNote.cloneNode(true); n.classList.add('v36-quiet'); n.querySelectorAll('button').forEach((b) => b.replaceWith(b.textContent)); card.append(n); }
    }
    // The Calculation line beside the Engineer's Value, which read omits
    // when nothing is recorded (_CaseValuationCalculation.cshtml:202).
    const srcCalc = source.querySelector('.valuation-value-calc');
    const values = root.querySelector('.valuation-value');
    if (srcCalc && values && !values.querySelector('.valuation-value-calc')) {
      const calc = srcCalc.cloneNode(true); calc.classList.add('v36-f28'); values.append(calc); mark(calc, 'f28');
    }
    // On the report is one row in both modes already (the value box reading,
    // the switches editing), so it is left as it is.
    return true;
  });

  // ---- Repair Spec -------------------------------------------------------------------
  add('f32', 'Repair Spec', 'a', 'The line grid aligns: figures and their headers right-aligned, two decimals, the part number left, and "Unit £" reads "Unit (£)" (operator, 9 October 2026)', (root, ctx) => {
    const table = root.querySelector('#section-estimate table');
    if (!table) return false;
    const variant = ctx.opts.grid || 'aligned';
    const heads = [...table.querySelectorAll('thead th')];
    const idx = Object.fromEntries(heads.map((th, i) => [text(th), i]));
    const numeric = ['Qty', 'Unit £', 'Hours', 'Paint h', 'Material £'];
    for (const h of heads) {
      const t = text(h);
      if (numeric.includes(t)) h.classList.add('v36-num');
      if (variant === 'brackets' || variant === 'aligned') {
        if (t === 'Unit £') h.textContent = 'Unit (£)';
        if (t === 'Material £') h.textContent = 'Material (£)';
      }
      if (variant === 'prefix') {
        if (t === 'Unit £') h.textContent = 'Unit';
        if (t === 'Material £') h.textContent = 'Material';
      }
    }
    for (const tr of table.querySelectorAll('tbody tr')) {
      const cells = [...tr.children];
      for (const name of numeric) {
        const td = cells[idx[name]];
        if (!td) continue;
        td.classList.add('v36-num');
        const field = td.querySelector('input, .gv');
        if (field && name !== 'Qty') {
          const v = field.value ?? text(field);
          const n = Number(v);
          if (v !== '' && Number.isFinite(n)) {
            const s = n.toFixed(2);
            if (field.value !== undefined && field.tagName === 'INPUT') field.value = s; else field.textContent = s;
          }
          if (variant === 'prefix' && /£/.test(name) && v !== '') {
            if (field.tagName === 'INPUT') { const w = document.createElement('span'); w.className = 'input-money v36-money'; field.replaceWith(w); w.append(field); }
            else field.textContent = '£' + (field.textContent || '');
          }
        }
      }
      const part = cells[idx['Part no.']];
      if (part) part.classList.add('v36-part');
    }
    table.classList.add('v36-f32'); mark(table, 'f32'); return true;
  }, { variable: 'grid', options: ['today', 'aligned', 'prefix'] });

  // ---- Decisions -----------------------------------------------------------------------
  add('f34', 'Decisions', 'b', '"Not recorded" is a value, not a chosen segment: box / segments-none / select (operator, 9 October 2026)', (root, ctx) => {
    const variant = ctx.opts.choice || 'box';
    let changed = false;
    for (const dec of root.querySelectorAll('.decisions .dec')) {
      const live = dec.querySelector('.radiorow:not(.is-static)');
      const stat = dec.querySelector('.radiorow.is-static');
      const row = live ?? stat;
      if (!row) continue;
      const picks = [...row.querySelectorAll('.pick-radio')];
      const none = picks.find((p) => text(p) === ABSENT);
      const chosen = picks.find((p) => p !== none && (p.getAttribute('aria-checked') === 'true' || p.dataset.on === 'true'));
      const valueBox = document.createElement('div');
      valueBox.className = 'fv' + (chosen ? '' : ' empty') + ' v36-choice-value';
      valueBox.textContent = chosen ? text(chosen) : ABSENT;
      mark(dec, 'f34');
      if (variant === 'box') {
        if (ctx.mode === 'read' || !live) { row.replaceWith(valueBox); }
        else { none?.remove(); if (!chosen) for (const p of picks) p.setAttribute('aria-checked', 'false'); }
        dec.classList.add('v36-choice-box');
      } else if (variant === 'segments-none') {
        none?.remove();
        const wrap = document.createElement('div'); wrap.className = 'v36-choice-none';
        row.replaceWith(wrap); wrap.append(valueBox, row);
      } else if (variant === 'select') {
        if (ctx.mode === 'read' || !live) { row.replaceWith(valueBox); }
        else {
          const select = document.createElement('select'); select.className = 'fi';
          for (const p of picks) { const o = document.createElement('option'); o.textContent = text(p); o.value = p.dataset.radioValue ?? ''; if (p === chosen) o.selected = true; select.append(o); }
          row.replaceWith(select);
        }
      }
      changed = true;
    }
    return changed;
  }, { variable: 'choice', options: ['today', 'box', 'segments-none', 'select'] });
  add('f35', 'Decisions', 'b', 'The metric strip goes (Figures is the home); Labour hours becomes a cell beside Excess', (root) => {
    const strip = root.querySelector('[data-settlement-figures]');
    if (!strip) return false;
    const hours = [...strip.querySelectorAll('.metric')].find((m) => text(m.querySelector('.metric-label')) === 'Labour hours');
    const grid = cell(section(root, 'settlement') ?? root, 'Excess')?.parentElement;
    if (hours && grid) {
      const fc = document.createElement('div'); fc.className = 'fc ro';
      const v = text(hours.querySelector('.metric-value'));
      fc.innerHTML = `<span class="lbl">Labour hours</span><div class="fv mono${/^[—–-]$/.test(v) ? ' empty' : ''}">${/^[—–-]$/.test(v) ? ABSENT : v}</div>`;
      grid.append(fc); mark(fc, 'f35');
    }
    mark(strip.parentElement, 'f35'); strip.remove(); return true;
  });
  add('f36', 'Decisions', 'a', 'Repair delays and Report delay, the two multi-line cells, take a row of their own under the single-line costs', (root) => {
    const costs = subByTitle(root, 'Costs, hire');
    if (!costs) return false;
    let changed = false;
    for (const words of ['Repair delays', 'Report delay']) {
      const fc = cell(costs, words);
      if (fc) { fc.classList.add('span2'); fc.parentElement.append(fc); mark(fc, 'f36'); changed = true; }
    }
    return changed;
  });

  // ---- Report -----------------------------------------------------------------------------
  add('f38', 'Report', 'a', 'Engineer\'s comments takes a full row like Valuation commentary; Report date keeps its own', (root) => {
    const fc = cell(section(root, 'report') ?? root, "Engineer's comments");
    if (!fc) return false;
    fc.classList.remove('span3'); fc.classList.add('span4'); mark(fc, 'f38'); return true;
  });
  add('f39', 'Report', 'b', 'The Statement of truth folds under its own sub-panel head, closed until opened', (root) => {
    const fc = cell(section(root, 'report') ?? root, 'Statement of truth');
    if (!fc) return false;
    const panel = document.createElement('div'); panel.className = 'sub-panel span4 is-collapsed v36-f39';
    panel.innerHTML = `<h3>Statement of truth <button type="button" class="icon-button panel-collapse" aria-expanded="false" aria-label="Expand section" title="Expand section">${icon('icon-chevron-down')}</button></h3><div class="fg"></div>`;
    fc.replaceWith(panel); panel.querySelector('.fg').append(fc);
    fc.classList.remove('span4'); fc.querySelector('.lbl')?.remove();
    panel.querySelector('button').addEventListener('click', () => { panel.classList.toggle('is-collapsed'); panel.querySelector('button').setAttribute('aria-expanded', String(!panel.classList.contains('is-collapsed'))); });
    mark(panel, 'f39'); return true;
  });

  // ---- Files and Notes -----------------------------------------------------------------------
  add('f42', 'Notes', 'b', 'Add Case note is a secondary button: Files\' Add evidence stays the one red primary on that screen', (root) => {
    const btn = root.querySelector('.note-form .btn--primary');
    if (!btn) return false;
    btn.classList.remove('btn--primary'); mark(btn, 'f42'); return true;
  });
  add('f43', 'Files', 'a', 'A read-mode tile keeps the edit panel\'s height, so Edit does not grow the grid', (root, ctx) => {
    let changed = false;
    for (const p of root.querySelectorAll('.image-tile-status')) { p.classList.add('v36-f43'); mark(p, 'f43'); changed = true; }
    return changed;
  });
  add('f45', 'Notes', 'a', 'A timeline entry is one line: when, who, what; the actor no longer sits centred on a line of its own', (root) => {
    const tl = root.querySelector('.notes-timeline');
    if (!tl) return false;
    for (const nb of tl.querySelectorAll('.note .nb')) {
      nb.innerHTML = nb.innerHTML.replace(/\s+/g, ' ').trim();
    }
    tl.classList.add('v36-f45'); mark(tl, 'f45'); return true;
  });
  add('f46', 'Every section', 'a', 'One empty state: a muted sentence in a 36px row; no dashed box, no icon', (root) => {
    let changed = false;
    for (const e of root.querySelectorAll('.estimate-empty, #section-notes .empty')) {
      e.classList.remove('empty'); e.classList.add('v36-empty-line'); mark(e, 'f46'); changed = true;
    }
    return changed;
  });
  add('f55', 'Repair Spec', 'a', 'The spec origin line names the person, never an account id', (root) => {
    let changed = false;
    for (const el of root.querySelectorAll('#section-estimate *')) {
      if (el.children.length) continue;
      if (/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i.test(el.textContent)) {
        el.textContent = el.textContent.replace(/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i, text(root.querySelector('.ribbon .ribbon-item:last-child .ribbon-value')) || 'the Engineer');
        mark(el, 'f55'); changed = true;
      }
    }
    return changed;
  });

  window.v36Findings = F;
})();
