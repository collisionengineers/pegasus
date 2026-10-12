// v38 mockup only: draws the captured Case page frame (read or edit), puts
// the fixture's Repair Spec section into it as today or as the proposal,
// and wires the strip, the query-string presets, the Diff and Split
// widgets and the section's interactions (tabs, the grid, the rate, the
// Target % preview, the agreed sum, Supplementary, Versions and Compare).
(() => {
  'use strict';

  const M = window.v38Model;
  const R = window.v38Render;
  const text = (el) => (el ? el.textContent.replace(/\s+/g, ' ').trim() : '');

  // The five proposals: a variable each, "today" being the live behaviour.
  const PROPOSALS = [
    { id: 'P1', variable: 'cols', title: 'Columns by line type: a cell the type does not price reads as a dash', options: ['today', 'dash'] },
    { id: 'P2', variable: 'cmp', title: 'Compare over versions: From and To side by side, Compare with current, a print sheet', options: ['today', 'side'] },
    { id: 'P3', variable: 'supp', title: 'Supplementary filled after an import, against the version the report used', options: ['today', 'auto'] },
    { id: 'P4', variable: 'agreed', title: 'The agreed sum drives the spec: typing it previews the scaling, and the mismatch chip rescales', options: ['today', 'drives'] },
    { id: 'P5', variable: 'rate', title: 'The labour rate bar under the grid: a segment per rate card, Custom typed, the uplift tick, the figure at the right', options: ['head', 'bar', 'both'] },
    { id: 'P6', variable: 'scale', title: 'Target % of value as the reference scales: floor to estimate, Apply while previewing, Remove scaling, the floors under the slider', options: ['today', 'ref'] },
  ];
  const DEFAULTS = { cols: 'dash', cmp: 'side', supp: 'auto', agreed: 'drives', rate: 'bar', scale: 'ref' };
  const TODAY = { cols: 'today', cmp: 'today', supp: 'today', agreed: 'today', rate: 'head', scale: 'today' };

  const params = new URLSearchParams(location.search);
  const opts = {};
  for (const pair of (params.get('opt') || '').split(',')) {
    const [k, v] = pair.split(':');
    if (k) opts[k.trim()] = (v ?? '').trim();
  }
  const q = {
    design: params.get('design') === 'proposal' ? 'proposal' : 'today',
    mode: params.get('mode') === 'edit' ? 'edit' : 'read',
    spec: params.get('spec') === '1' ? 0 : 1,
    dialog: params.get('dialog') || '',
    diff: params.get('diff') === '1',
    split: params.get('split') === '1',
    strip: params.get('strip') !== '0',
    section: params.get('section') ?? 'estimate',
    scale: params.get('scale') ? Number(params.get('scale')) : null,
    sum: params.get('sum') ? Number(params.get('sum')) : 0,
    cmpFrom: params.get('cmpfrom') || '',
    cmpTo: params.get('cmpto') || '',
    opts,
  };

  function switchesFor(design) {
    const p = {};
    for (const f of PROPOSALS) {
      const v = opts[f.variable];
      p[f.variable] = v && f.options.includes(v) ? v : design === 'proposal' ? DEFAULTS[f.variable] : TODAY[f.variable];
    }
    return p;
  }

  // One state per drawn pane (Split draws two).
  function newState(design) {
    const p = switchesFor(design);
    // P3 (the reference's model): the supplementary estimate was imported
    // onto the existing spec, so the Case holds one spec with three versions.
    const oneSpec = p.supp === 'auto';
    const specs = M.clone(oneSpec ? M.SPECS_ONE : M.SPECS);
    const specIndex = oneSpec ? 0 : q.spec;
    const spec = specs[specIndex];
    const st = {
      design, mode: q.mode, spec: specIndex, specs, p,
      preview: null, scaled: false, preScale: null,
      contract: q.sum > 0, sum: q.sum,
      suppOf: null, suppReason: 'estimate', suppExplain: params.get('explain') === '1', suppDismissed: false,
      cmpFrom: q.cmpFrom, cmpTo: q.cmpTo,
      floors: { ...M.FLOORS },
    };
    // P3: the import onto the spec fills Supplementary against the version
    // the last sent report used, else the grid it replaced.
    if (oneSpec) {
      const sent = R.lastSent(specs);
      const before = spec.versions.length > 1 ? spec.versions[spec.versions.length - 2] : null;
      st.suppOf = sent ? `${sent.spec.id}:v${sent.version.n}` : before ? `${spec.id}:v${before.n}` : null;
    }
    // P2: Compare opens on the last sent version against the working draft.
    if (p.cmp === 'side' && !st.cmpFrom && !st.cmpTo) {
      const sent = R.lastSent(specs);
      const other = specs.find((s) => s !== spec);
      st.cmpFrom = sent ? `${sent.spec.id}:v${sent.version.n}` : other ? `${other.id}:working` : (spec.versions.length ? `${spec.id}:v${spec.versions[spec.versions.length - 1].n}` : '');
      st.cmpTo = `${spec.id}:working`;
    }
    if (q.scale != null && q.mode === 'edit') st.preview = M.scaleTo(spec.lines, spec.header, M.ENGINEER_VALUE * q.scale / 100, st.floors);
    if (q.sum > 0 && q.mode === 'edit' && p.agreed === 'drives') st.preview = M.scaleTo(spec.lines, spec.header, q.sum, st.floors);
    return st;
  }

  const frameFor = (mode) => document.querySelector(`#frame-engineer-${mode}`);

  // The frame as the tip renders the rest of the page: no Scroll/Tabs switch
  // (PR 1145), no Decisions metric strip (v36 item I), the dialogs the
  // section owns drawn by the renderer.
  function prepare(root) {
    const sw = root.querySelector('[data-case-layout-switch]'); if (sw) sw.remove();
    const strip = root.querySelector('.metric-strip'); if (strip) strip.remove();
    const line = root.querySelector('[data-lease-line]'); if (line) { line.hidden = false; line.textContent = 'Saved 09:41'; }
    const saveNow = root.querySelector('[data-case-save-now]'); if (saveNow) saveNow.hidden = true;
    for (const d of root.querySelectorAll('[data-dialog="versions-dialog"], [data-dialog="compare-estimates-dialog"], [data-dialog="delete-lines-dialog"], [data-dialog="delete-estimate-dialog"], [data-dialog="send-to-ai-dialog"]')) d.remove();
    for (const d of root.querySelectorAll('.dialog-backdrop[data-dialog-open-on-load]')) d.hidden = true;
  }

  function patchFigures(root, gross) {
    const ev = M.ENGINEER_VALUE;
    const share = Math.round(gross / ev * 100);
    const dds = root.querySelectorAll('[data-figures] .figures dd');
    if (dds.length >= 3) { dds[0].textContent = M.money(gross); dds[1].textContent = M.money(ev); dds[2].textContent = `${share}%`; }
    const chips = root.querySelector('[data-case-ribbon-chips]');
    if (chips) {
      let chip = chips.querySelector('[data-repairs-of-value-chip]');
      if (!chip) { chip = document.createElement('span'); chip.setAttribute('data-repairs-of-value-chip', ''); chips.append(chip); }
      chip.className = `status ${share >= 80 ? 'status--red' : share >= 66 ? 'status--amber' : 'status--green'}`;
      chip.textContent = `Repairs ${share}% of value`;
    }
  }

  function drawSection(pane, st) {
    const section = pane.querySelector('#section-estimate');
    if (!section) return null;
    const out = R.render(st);
    const body = section.querySelector('.panel-body');
    body.innerHTML = out.body;
    const meta = section.querySelector('[data-estimate-meta]'); if (meta) meta.textContent = out.meta;
    // The More menu: Versions (n); Compare, offered with two specs today and
    // with a frozen version under P2 (an item, never disabled; v36 item Y).
    const selected = st.specs[st.spec];
    const versions = section.querySelector('[data-estimate-versions] span:last-child'); if (versions) versions.textContent = `Versions (${selected.versions.length})`;
    const compareOld = section.querySelector('[data-estimate-compare]');
    if (compareOld) {
      const offered = st.p.cmp === 'side' ? st.specs.length > 1 || selected.versions.length > 0 : st.specs.length > 1;
      if (offered) {
        const a = document.createElement('a'); a.className = 'btn'; a.href = '#'; a.setAttribute('data-dialog-open', 'compare-estimates-dialog'); a.setAttribute('data-estimate-compare', '');
        a.innerHTML = '<svg class="icon" aria-hidden="true"><use href="#icon-list"></use></svg><span>Compare</span>';
        compareOld.replaceWith(a);
      } else compareOld.remove();
    }
    for (const d of pane.querySelectorAll('[data-v38-dialogs]')) d.remove();
    const holder = document.createElement('div'); holder.setAttribute('data-v38-dialogs', ''); holder.innerHTML = out.dialogs;
    section.after(holder);
    // The aside and the ribbon read the recorded spec; a preview changes
    // only the section until Apply.
    const recorded = st.specs[st.spec];
    patchFigures(pane, M.totals(recorded.lines, recorded.header).printed.gross);
    return out;
  }

  const panes = new Map(); // pane element → state

  function draw(container, design) {
    const tpl = frameFor(q.mode) || frameFor('read');
    const root = document.importNode(tpl.content, true);
    container.append(root);
    prepare(container);
    const st = newState(design);
    panes.set(container, st);
    drawSection(container, st);
    return st;
  }

  function applied(st) {
    return PROPOSALS.filter((f) => st.p[f.variable] !== TODAY[f.variable]).map((f) => f.id);
  }

  function legend(ids) {
    const box = document.createElement('div'); box.className = 'v38-legend';
    box.innerHTML = '<div style="font-weight:700;margin-bottom:4px">Changed regions</div>' + ids.map((id) => `<div><b>${id}</b>${PROPOSALS.find((f) => f.id === id).title}</div>`).join('');
    document.body.append(box);
  }

  function render() {
    const frame = document.querySelector('#v38-frame');
    frame.innerHTML = '';
    panes.clear();
    document.body.className = '';
    document.querySelectorAll('.v38-legend').forEach((e) => e.remove());
    let st;
    if (q.split) {
      document.body.classList.add('v38-split');
      for (const design of ['today', 'proposal']) {
        const pane = document.createElement('div'); pane.className = 'v38-pane'; pane.dataset.v38Pane = design === 'today' ? 'Today' : 'Proposal';
        frame.append(pane);
        const s = draw(pane, design);
        if (design === 'proposal') st = s;
      }
    } else {
      st = draw(frame, q.design);
    }
    if (q.diff) { document.body.classList.add('v38-diff'); legend(applied(st)); }
    if (!q.strip) document.body.classList.add('v38-strip-hidden');
    if (q.dialog) openDialog(q.dialog);
    if (q.section) {
      const target = frame.querySelector(`#section-${q.section}`);
      if (target) target.scrollIntoView({ block: 'start', behavior: 'instant' });
    }
    document.documentElement.dataset.ready = `${q.design}:${q.mode}:${st.spec + 1}`;
    window.v38Applied = applied(st);
  }

  // Redraw the section of the pane that holds the element, keeping the open dialog.
  function redraw(el, keepDialog) {
    const pane = [...panes.keys()].find((p) => p.contains(el)) || [...panes.keys()][0];
    const st = panes.get(pane);
    const open = keepDialog || [...pane.querySelectorAll('[data-dialog]:not([hidden])')].map((d) => d.dataset.dialog)[0];
    drawSection(pane, st);
    if (open) openDialog(open, pane);
    if (q.diff) { document.querySelectorAll('.v38-legend').forEach((e) => e.remove()); legend(applied(st)); }
    window.v38Applied = applied(st);
    return st;
  }
  const stateOf = (el) => panes.get([...panes.keys()].find((p) => p.contains(el)) || [...panes.keys()][0]);

  function openDialog(name, pane) {
    const scope = pane || document;
    const d = scope.querySelector(`[data-dialog="${name}"]`);
    if (d) { d.hidden = false; const menu = scope.querySelector('[data-estimate-more]'); if (menu) menu.open = false; const h = d.querySelector('h2'); if (h) h.focus(); }
  }

  function toast(title, undo) {
    const region = document.querySelector('[data-toast-region]') || (() => { const r = document.createElement('div'); r.className = 'toast-region'; r.setAttribute('data-toast-region', ''); document.body.append(r); return r; })();
    // As case-workspace.js draws it (v28 P16): the toast carries the one Undo.
    const el = document.createElement('div'); el.className = undo ? 'toast toast--undo' : 'toast'; el.setAttribute('role', 'status'); el.setAttribute('data-dismissable', '');
    el.innerHTML = `<strong>${title}</strong>${undo ? '<button type="button" class="btn btn--small" data-undo-line>Undo</button>' : ''}<button type="button" class="dismiss" data-dismiss aria-label="Dismiss"><svg class="icon" aria-hidden="true"><use href="#icon-x"></use></svg></button>`;
    region.append(el);
    setTimeout(() => el.remove(), 6000);
    return el;
  }

  // ---- interactions ------------------------------------------------------------
  const numOrNull = (v) => (v === '' || v == null ? null : Number(v));
  let removed = null;

  document.addEventListener('change', (e) => {
    const t = e.target;
    const st = stateOf(t);
    if (!st || t.closest('.v38-strip')) return;
    const spec = st.specs[st.spec];
    if (t.dataset.field && t.dataset.line != null) {
      const line = spec.lines[Number(t.dataset.line)];
      const k = t.dataset.field;
      if (k === 't') { line.t = t.value; if ((M.COLS[line.t] || {}).q && !line.q) line.q = 1; }
      else if (k === 'd' || k === 'p') line[k] = t.value;
      else line[k] = numOrNull(t.value);
      if (line.src === 'imported') line.src = 'amended';
      st.preview = null; redraw(t); return;
    }
    if (t.hasAttribute('data-phantom') && (t.value || '').trim()) {
      const tr = t.closest('tr');
      const type = tr.querySelector('select').value;
      const desc = tr.querySelector('input[name="lineDescription"]').value;
      if (desc.trim()) { spec.lines.push({ t: type, d: desc.trim(), p: '', q: (M.COLS[type] || {}).q ? 1 : null, u: null, h: null, ph: null, m: null, src: 'manual' }); redraw(t); }
      return;
    }
    // The rate (the reference's rateBand / rateTyped): a card sets the figure; typing keeps the typed figure and lets the card and the uplift go.
    if (t.hasAttribute('data-rate-card')) { const c = M.RATE_CARDS.find((x) => x.key === t.value); if (c) { spec.header.band = c.key; spec.header.rate = c.rate; } else { spec.header.rate = M.effectiveRate(spec.header); spec.header.band = null; spec.header.uplift = false; } st.preview = null; redraw(t); return; }
    if (t.hasAttribute('data-rate-input')) { spec.header.band = null; spec.header.uplift = false; spec.header.rate = Number(t.value) || 0; st.preview = null; redraw(t); return; }
    if (t.hasAttribute('data-uplift')) { spec.header.uplift = t.checked; st.preview = null; redraw(t); return; }
    if (t.dataset.discount) { spec.header.discounts[t.dataset.discount] = Number(t.value) || 0; redraw(t); return; }
    if (t.dataset.vatCategory) { spec.header.vat[t.dataset.vatCategory] = t.checked; spec.header.vatOverridden = true; redraw(t); return; }
    if (t.hasAttribute('data-vat-status')) { spec.header.repairerStatus = t.value === 'Not registered' ? 'NotRegistered' : t.value; redraw(t); return; }
    if (t.hasAttribute('data-scale-percent')) { preview(st, Number(t.value)); redraw(t); return; }
    if (t.hasAttribute('data-scale-floor-rate') || t.hasAttribute('data-scale-floor-price')) {
      const pane = t.closest('[data-estimate-scale]');
      st.floors = { rate: Number(pane.querySelector('[data-scale-floor-rate]').value) || 0, price: Number(pane.querySelector('[data-scale-floor-price]').value) || 0 };
      if (st.preview) preview(st, st.preview.grossAfter / M.ENGINEER_VALUE * 100);
      redraw(t); return;
    }
    if (t.hasAttribute('data-contract-agreed')) {
      st.contract = t.checked;
      if (st.contract && !(st.sum > 0)) st.sum = M.totals(spec.lines, spec.header).printed.gross;
      if (!st.contract) st.preview = null;
      redraw(t); return;
    }
    if (t.hasAttribute('data-contract-sum')) {
      st.sum = Number(t.value) || 0;
      if (st.p.agreed === 'drives' && st.sum > 0) st.preview = M.scaleTo(spec.lines, spec.header, st.sum, st.floors);
      redraw(t); return;
    }
    if (t.hasAttribute('data-supp-of')) { st.suppOf = t.value || null; redraw(t); return; }
    if (t.hasAttribute('data-supp-reason')) { st.suppReason = t.value; redraw(t); return; }
    if (t.hasAttribute('data-supp-explain')) { st.suppExplain = t.checked; if (st.p.supp === 'auto') redraw(t); return; }
    if (t.hasAttribute('data-cmp-from')) { st.cmpFrom = t.value; return; }
    if (t.hasAttribute('data-cmp-to')) { st.cmpTo = t.value; return; }
  });

  function preview(st, percent) {
    const spec = st.specs[st.spec];
    const gross = M.totals(spec.lines, spec.header).printed.gross;
    // Today's slider runs from 1 %; the reference's from the floor-scaled minimum.
    const top = st.p.scale === 'ref' ? gross / M.ENGINEER_VALUE * 100 : Math.max(1, M.percentOfValue(M.ENGINEER_VALUE, gross));
    const bottom = st.p.scale === 'ref' ? M.grossAt(spec.lines, spec.header, 0, st.floors) / M.ENGINEER_VALUE * 100 : 1;
    const pct = Math.min(top, Math.max(bottom, percent || bottom));
    st.preview = pct >= top ? null : M.scaleTo(spec.lines, spec.header, M.ENGINEER_VALUE * pct / 100, st.floors);
  }

  document.addEventListener('input', (e) => {
    const t = e.target;
    if (!t.hasAttribute('data-scale-range')) return;
    const st = stateOf(t);
    preview(st, Number(t.value));
    // The range keeps its thumb: redraw the section, then put focus back.
    const value = t.value;
    redraw(t);
    const again = document.querySelector('[data-scale-range]'); if (again) { again.value = value; again.focus(); }
  });

  document.addEventListener('click', (e) => {
    if (e.target.closest('.v38-strip')) return;
    const a = e.target.closest('a');
    if (a && a.getAttribute('href') === '#' && !a.hasAttribute('data-dialog-open') && !a.hasAttribute('data-estimate-tab')) e.preventDefault();
    const tab = e.target.closest('[data-estimate-tab]');
    if (tab) { e.preventDefault(); go({ spec: Number(tab.dataset.specIndex) + 1 }); return; }
    const jump = e.target.closest('a[href^="#section-"], [data-section-jump], .section-link');
    if (jump) { e.preventDefault(); const key = jump.dataset.sectionJump || jump.dataset.sectionLink || (jump.getAttribute('href') || '').slice('#section-'.length); document.querySelector(`#section-${key}`)?.scrollIntoView({ block: 'start' }); return; }
    const fold = e.target.closest('[data-collapse-toggle]');
    if (fold) { const panel = fold.closest('[data-collapse]'); if (panel) { panel.classList.toggle('is-collapsed'); fold.setAttribute('aria-expanded', String(!panel.classList.contains('is-collapsed'))); } return; }
    const close = e.target.closest('[data-dialog-close], .dialog-backdrop');
    if (close && (e.target === close || close.hasAttribute('data-dialog-close'))) { const d = close.closest('[data-dialog]'); if (d && !e.target.closest('[data-delete-lines-confirm]')) d.hidden = true; if (!e.target.closest('[data-delete-lines-confirm]')) return; }
    const dismiss = e.target.closest('[data-dismiss]');
    if (dismiss) { (dismiss.closest('.notice, .toast') || dismiss).remove(); return; }
    const opener = e.target.closest('[data-dialog-open]');
    if (opener) { e.preventDefault(); openDialog(opener.dataset.dialogOpen, [...panes.keys()].find((p) => p.contains(opener))); return; }

    const st = stateOf(e.target);
    if (!st) return;
    const spec = st.specs[st.spec];
    const band = e.target.closest('[data-rate-band]');
    if (band) {
      const c = M.RATE_CARDS.find((x) => x.key === band.dataset.rateBand);
      if (c) { spec.header.band = c.key; spec.header.rate = c.rate; }
      else { spec.header.rate = M.effectiveRate(spec.header); spec.header.band = null; spec.header.uplift = false; }
      st.preview = null; redraw(e.target);
      if (!c) { const box = document.querySelector('[data-estimate-rate-bar] [data-rate-input]'); if (box) { box.focus(); box.select(); } }
      return;
    }
    const apply = e.target.closest('[data-scale-apply]');
    if (apply) {
      if (st.preview) { st.preScale = { lines: M.clone(spec.lines), header: M.clone(spec.header) }; spec.lines = st.preview.lines.map((l) => ({ ...l, scaled: undefined })); spec.header = st.preview.header; st.scaled = true; st.preview = null; }
      redraw(e.target); return;
    }
    const remove = e.target.closest('[data-scale-remove]');
    // Remove scaling (the reference's scaleOff): a live preview is dropped; an applied scaling returns the spec to the estimate.
    if (remove) { if (st.preview) { st.preview = null; } else if (st.preScale) { spec.lines = st.preScale.lines; spec.header = st.preScale.header; st.scaled = false; st.preScale = null; } redraw(e.target); return; }
    const rescale = e.target.closest('[data-contract-rescale]');
    if (rescale) { if (st.sum > 0) st.preview = M.scaleTo(spec.lines, spec.header, st.sum, st.floors); redraw(e.target); return; }
    const suppDismiss = e.target.closest('[data-supp-dismiss]');
    if (suppDismiss) { st.suppDismissed = true; redraw(e.target); return; }
    const suppCompare = e.target.closest('[data-supp-compare]');
    if (suppCompare) { e.preventDefault(); st.cmpFrom = st.suppOf; st.cmpTo = `${spec.id}:working`; redraw(e.target, 'compare-estimates-dialog'); return; }
    const vc = e.target.closest('[data-version-compare]');
    if (vc) { st.cmpFrom = `${spec.id}:v${vc.dataset.versionCompare}`; st.cmpTo = `${spec.id}:working`; redraw(e.target, 'compare-estimates-dialog'); return; }
    const go2 = e.target.closest('[data-estimate-compare-go]');
    if (go2) { e.preventDefault(); const form = go2.closest('form'); st.cmpFrom = form.querySelector('[data-cmp-from]').value; st.cmpTo = form.querySelector('[data-cmp-to]').value; redraw(e.target, 'compare-estimates-dialog'); return; }
    const print = e.target.closest('[data-compare-print]');
    if (print) {
      // The sheet is the dialog alone: it is lifted to the body for the print and put back after.
      const dialog = print.closest('[data-dialog]'); const holder = dialog.parentElement; const next = dialog.nextSibling;
      document.body.append(dialog); document.body.classList.add('v38-printing');
      const back = () => { document.body.classList.remove('v38-printing'); holder.insertBefore(dialog, next); window.removeEventListener('afterprint', back); };
      window.addEventListener('afterprint', back);
      window.print();
      setTimeout(() => { if (document.body.classList.contains('v38-printing')) back(); }, 1500);
      return;
    }
    const rm = e.target.closest('[data-remove-line]');
    if (rm) { e.preventDefault(); const i = Number(rm.dataset.removeLine); removed = { i, line: spec.lines[i], st }; spec.lines.splice(i, 1); st.preview = null; redraw(e.target); toast('Line removed', true); return; }
    const undo = e.target.closest('[data-undo-line]');
    if (undo) { if (removed) { removed.st.specs[removed.st.spec].lines.splice(removed.i, 0, removed.line); removed.st.preview = null; removed = null; } undo.closest('.toast').remove(); redraw(document.querySelector('#section-estimate')); return; }
    const add = e.target.closest('[data-estimate-add-line]');
    if (add) { spec.lines.push({ t: 'Replace', d: '', p: '', q: 1, u: null, h: null, ph: null, m: null, src: 'manual' }); st.preview = null; redraw(e.target); const last = document.querySelectorAll('input[name="lineDescription"]'); if (last.length) last[last.length - 2]?.focus(); return; }
    const delAll = e.target.closest('[data-delete-lines-confirm]');
    if (delAll) { const n = spec.lines.length; spec.lines = []; st.preview = null; redraw(e.target); toast(n === 1 ? 'Line removed' : `${n} lines removed`, false); return; }
    const reset = e.target.closest('[data-vat-reset]');
    if (reset) { const reg = spec.header.repairerStatus === 'Registered'; spec.header.vat = { Labour: reg, Parts: true, Materials: true, Specialist: reg }; spec.header.vatOverridden = false; redraw(e.target); return; }
    if (e.target.closest('form button[type=submit], button[type=submit]')) e.preventDefault();
  });
  document.addEventListener('submit', (e) => e.preventDefault());
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') document.querySelectorAll('[data-dialog]:not([hidden])').forEach((d) => { d.hidden = true; });
  });

  // ---- strip -------------------------------------------------------------------
  function url(changes) {
    const p = new URLSearchParams(location.search);
    for (const [k, v] of Object.entries(changes)) { if (v === '' || v === false || v === null || v === undefined) p.delete(k); else p.set(k, v === true ? '1' : v); }
    return `${location.pathname}?${p.toString()}`;
  }
  const go = (changes) => { location.href = url(changes); };
  const seg = (name, value, items) => `<div class="v38-row"><b>${name}</b><div class="v38-seg">${items.map(([k, label]) => `<button type="button" data-set="${name}" data-value="${k}" aria-pressed="${String(k) === String(value)}">${label}</button>`).join('')}</div></div>`;

  function strip() {
    const el = document.createElement('details'); el.className = 'v38-strip'; el.open = localStorage.getItem('v38.strip') === 'open';
    el.addEventListener('toggle', () => localStorage.setItem('v38.strip', el.open ? 'open' : 'closed'));
    const optStr = (o) => Object.entries(o).filter(([, v]) => v).map(([k, v]) => `${k}:${v}`).join(',');
    const sw = switchesFor(q.design);
    const proposals = PROPOSALS.map((f) => `<label><small>${f.id}</small><span>${f.title}<br><select data-variable="${f.variable}">${f.options.map((o) => `<option value="${o}"${o === sw[f.variable] ? ' selected' : ''}>${o}</option>`).join('')}</select></span></label>`).join('');
    const dialogs = ['versions-dialog', 'compare-estimates-dialog', 'delete-lines-dialog', 'delete-estimate-dialog'];
    el.innerHTML = `<summary>Mockup controls · v38 Repair Spec <small>demo control, not product UI</small></summary><div class="v38-body">
      ${seg('design', q.design, [['today', 'Today'], ['proposal', 'Proposal']])}
      ${seg('mode', q.mode, [['read', 'Read'], ['edit', 'Edit']])}
      ${sw.supp === 'auto' ? '<div class="v38-row"><b>Spec</b><div class="v38-note">One spec: the supplementary estimate was imported onto it (P3)</div></div>' : seg('spec', q.spec + 1, [['1', 'Audatex est. 88214-01'], ['2', 'Audatex supp. est. 88214-02']])}
      <div class="v38-row"><b>Presets</b><div class="v38-seg"><button type="button" data-set="scale" data-value="${q.scale ? '' : '60'}" aria-pressed="${!!q.scale}">Target 60 % preview</button><button type="button" data-set="sum" data-value="${q.sum ? '' : String(M.AGREED_SUM)}" aria-pressed="${!!q.sum}">Agreed sum £${M.AGREED_SUM.toLocaleString('en-GB')}</button><button type="button" data-set="explain" data-value="${params.get('explain') === '1' ? '' : '1'}" aria-pressed="${params.get('explain') === '1'}">Explain on report ticked</button></div></div>
      <div class="v38-row"><b>Widgets</b><div class="v38-seg">${[['diff', 'Diff'], ['split', 'Split']].map(([k, l]) => `<button type="button" data-toggle="${k}" aria-pressed="${q[k]}">${l}</button>`).join('')}</div></div>
      <div class="v38-row"><b>Dialog</b><select data-select="dialog"><option value="">(none)</option>${dialogs.map((d) => `<option value="${d}"${d === q.dialog ? ' selected' : ''}>${d}</option>`).join('')}</select></div>
      <div class="v38-findings"><div class="v38-group">Proposals</div>${proposals}</div>
      <div class="v38-seg"><button type="button" data-reset>Reset to Today</button><button type="button" data-hide>Hide strip</button></div>
      <div class="v38-note">Every choice is also a query string: design, mode, spec, dialog, diff, split, scale, sum, explain, cmpfrom, cmpto, opt=cols:dash,cmp:side,supp:auto,agreed:drives,rate:bar,scale:ref.</div>
    </div>`;
    el.addEventListener('click', (e) => {
      const b = e.target.closest('button');
      if (!b) return;
      if (b.dataset.set) go({ [b.dataset.set]: b.dataset.value });
      else if (b.dataset.toggle) go({ [b.dataset.toggle]: !q[b.dataset.toggle] });
      else if (b.hasAttribute('data-reset')) go({ design: 'today', opt: '', diff: '', split: '', scale: '', sum: '', explain: '', dialog: '', cmpfrom: '', cmpto: '' });
      else if (b.hasAttribute('data-hide')) go({ strip: '0' });
    });
    el.addEventListener('change', (e) => {
      const t = e.target;
      if (t.dataset.select) go({ [t.dataset.select]: t.value });
      else if (t.dataset.variable) { const o = { ...opts }; o[t.dataset.variable] = t.value; go({ opt: optStr(o) }); }
    });
    document.body.append(el);
  }

  window.v38 = { q, PROPOSALS, DEFAULTS, TODAY, render, panes, stateOf };
  render();
  strip();
})();
