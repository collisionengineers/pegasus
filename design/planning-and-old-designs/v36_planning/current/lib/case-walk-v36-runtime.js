// v36 mockup only: draws the captured Case page frames as today and as the
// proposal, with the strip, the query-string presets and the widgets.
(() => {
  'use strict';

  const F = window.v36Findings || [];
  const STATES = {
    engineer: 'With Engineer · populated (read and edit)',
    review: 'Review · Assign Engineer',
    held: 'Held · review on 20 May 2031',
    colleague: 'With Engineer · a colleague is editing',
    notready: 'Not ready · standalone Audit (Original report section)',
    views: 'Inspection + Audit · Audit view (read and edit)',
    viewsinsp: 'Inspection + Audit · Inspection view',
  };
  const PAGES = { case: 'Case page', 'work-centre': 'Work Centre', cases: 'Cases list' };
  const SECTIONS = ['overview', 'claim', 'original-report', 'inspection', 'vehicle', 'estimate', 'settlement', 'report', 'files', 'notes'];
  const text = (el) => (el ? el.textContent.replace(/\s+/g, ' ').trim() : '');

  const params = new URLSearchParams(location.search);
  const opts = {};
  for (const pair of (params.get('opt') || '').split(',')) {
    const [k, v] = pair.split(':');
    if (k) opts[k.trim()] = (v ?? 'on').trim();
  }
  const state = {
    design: params.get('design') === 'proposal' ? 'proposal' : 'today',
    page: PAGES[params.get('page')] ? params.get('page') : 'case',
    state: STATES[params.get('state')] ? params.get('state') : 'engineer',
    mode: params.get('mode') === 'edit' ? 'edit' : 'read',
    layout: params.get('layout') === 'tabs' ? 'tabs' : 'scroll',
    section: params.get('section') || '',
    dialog: params.get('dialog') || '',
    viewer: params.get('viewer') || '',
    diff: params.get('diff') === '1',
    split: params.get('split') === '1',
    ruler: params.get('ruler') === '1',
    busy: params.get('busy') === '1',
    flicker: params.get('flicker') === '1',
    landing: params.get('landing') || '',
    strip: params.get('strip') !== '0',
    opts,
  };

  const frameFor = (st, mode) => document.querySelector(`#frame-${st}-${mode}`) || document.querySelector(`#frame-${st}-read`);
  const hasEdit = (st) => !!document.querySelector(`#frame-${st}-edit`);

  // Which findings apply: the proposal applies every finding unless its switch
  // is "off"; today applies only switches set "on". A finding with a variable
  // follows that variable (today = untouched).
  function enabled(f) {
    if (f.states && !f.states.includes(state.state)) return false;
    if (f.variable) {
      const v = opts[f.variable];
      if (v === undefined) return state.design === 'proposal';
      return v !== 'today';
    }
    const sw = opts[f.id];
    if (sw === 'off') return false;
    if (sw === 'on') return true;
    return state.design === 'proposal';
  }

  function prepare(root) {
    // The page's own scripts are gone: show what they would have shown.
    const sw = root.querySelector('[data-case-layout-switch]');
    if (sw) sw.hidden = false;
    for (const b of root.querySelectorAll('[data-case-layout-switch] button')) {
      b.setAttribute('aria-pressed', String(text(b).toLowerCase() === state.layout));
    }
    const record = root.querySelector('[data-case-record]');
    if (record) record.dataset.layout = state.layout;
    const line = root.querySelector('[data-lease-line]');
    if (line && state.mode === 'edit') { line.hidden = false; line.textContent = 'Saved 10:04'; }
    const saveNow = root.querySelector('[data-case-save-now]');
    if (saveNow) saveNow.hidden = true;
    for (const d of root.querySelectorAll('.dialog-backdrop[data-dialog-open-on-load]')) d.hidden = true;
    // Tabs: one section shown, its link current (Damage and Valuation ride with Vehicle).
    if (state.layout === 'tabs') {
      const key = state.section || 'overview';
      for (const s of root.querySelectorAll('.record-section')) {
        const own = s.dataset.section === key || s.dataset.sectionParent === key;
        s.classList.toggle('is-active', own);
      }
      for (const a of root.querySelectorAll('.section-link')) {
        const current = a.dataset.sectionLink === key;
        if (current) { a.setAttribute('aria-current', 'true'); a.setAttribute('aria-selected', 'true'); } else { a.removeAttribute('aria-current'); a.removeAttribute('aria-selected'); }
      }
    }
    if (state.dialog) {
      const d = root.querySelector(`[data-dialog="${state.dialog}"]`);
      if (d) { d.hidden = false; const menu = root.querySelector('[data-case-actions]'); if (menu) menu.open = false; }
    }
    if (state.viewer) {
      const v = root.querySelector('.case-viewer');
      const tile = root.querySelectorAll('[data-evidence-item] img')[Number(state.viewer) - 1] || root.querySelector('[data-evidence-item] img');
      if (v && tile) {
        v.hidden = false;
        const name = v.querySelector('[data-viewer-name]'); if (name) name.textContent = tile.getAttribute('alt') || '';
        const stage = v.querySelector('.viewer-stage, [data-viewer-stage]');
        if (stage) { stage.innerHTML = ''; const img = document.createElement('img'); img.src = tile.src; img.alt = ''; img.style.maxWidth = '100%'; img.style.maxHeight = '100%'; stage.append(img); }
        document.body.classList.add('has-viewer');
      }
    }
    // Open a fold the operator asked for (section=…) in scroll layout: nothing to do, the page scrolls.
    return root;
  }

  function busy(root) {
    const targets = [
      ...root.querySelectorAll('[data-next-action] .next-step-go, [data-add-evidence], .wc-row a.btn, .wc-row button.btn, [data-quick-detail] a.btn, .quick-detail a.btn, a.btn.btn--dark'),
    ];
    let n = 0;
    for (const el of targets) {
      if (el.classList.contains('v36-busy-demo')) continue;
      el.classList.add('v36-busy-demo'); el.setAttribute('aria-busy', 'true');
      const label = el.querySelector('span') || el;
      const word = el.getAttribute('data-busy-label') || 'Opening…';
      label.textContent = word;
      const spin = document.createElement('span'); spin.className = 'v36-spin'; el.prepend(spin);
      el.dataset.v36Change = 'f50'; n += 1;
    }
    return n;
  }

  function ruler(root) {
    for (const e of root.querySelectorAll('button, a.btn, input:not([type=hidden]):not([type=checkbox]):not([type=radio]), select, textarea, .fv, .status, .tab, .section-link, .pick-radio')) {
      if (!e.offsetParent) continue;
      e.dataset.v36H = String(Math.round(e.getBoundingClientRect().height));
    }
    const key = document.createElement('div'); key.className = 'v36-ruler-key';
    key.innerHTML = '<div><i style="border-color:rgba(26,140,80,.75)"></i>36px control</div><div><i style="border-color:rgba(28,96,186,.75)"></i>32px small control</div><div><i style="border-color:rgba(100,100,100,.6)"></i>40px row</div><div><i style="border-color:rgba(210,52,58,.85)"></i>22 / 24 / 28 / 30px</div>';
    document.body.append(key);
  }

  function legend(applied) {
    const box = document.createElement('div'); box.className = 'v36-legend';
    box.innerHTML = '<div style="font-weight:700;margin-bottom:4px">Changed regions</div>' + applied.map((f) => `<div><b>${f.id}</b>${f.title}</div>`).join('');
    document.body.append(box);
  }

  function draw(container, design) {
    const tpl = state.page === 'case' ? frameFor(state.state, state.mode) : document.querySelector(`#page-${state.page}`);
    if (!tpl) { container.textContent = 'No frame.'; return []; }
    const root = document.importNode(tpl.content, true);
    container.append(root);
    prepare(container);
    // A variable finding's proposal default is its first alternative.
    const effective = { ...opts };
    if (design === 'proposal') for (const f of F) if (f.variable && effective[f.variable] === undefined) effective[f.variable] = f.options[1];
    const ctx = { state: state.state, mode: hasEdit(state.state) ? state.mode : 'read', opts: effective, design };
    const applied = [];
    if (state.page === 'case') {
      for (const f of F) {
        if (design === 'proposal' ? enabled(f) : (opts[f.id] === 'on' || (f.variable && opts[f.variable] && opts[f.variable] !== 'today'))) {
          try { if (f.apply(container, ctx)) applied.push(f); } catch (e) { window.mockupErrors.push(`${f.id}: ${e.message}`); }
        }
      }
    }
    if (state.busy) busy(container);
    return applied;
  }

  function render() {
    const frame = document.querySelector('#v36-frame');
    frame.innerHTML = '';
    document.body.className = '';
    document.querySelectorAll('.v36-legend, .v36-ruler-key').forEach((e) => e.remove());
    let applied = [];
    if (state.split && state.page === 'case') {
      document.body.classList.add('v36-split');
      for (const design of ['today', 'proposal']) {
        const pane = document.createElement('div'); pane.className = 'v36-pane'; pane.dataset.v36Pane = design === 'today' ? 'Today' : 'Proposal';
        frame.append(pane);
        const a = draw(pane, design);
        if (design === 'proposal') applied = a;
      }
    } else {
      applied = draw(frame, state.design);
    }
    if (state.diff) { document.body.classList.add('v36-diff'); legend(applied); }
    if (state.ruler) { document.body.classList.add('v36-ruler'); ruler(frame); }
    if (state.flicker) document.body.classList.add('v36-flicker');
    if (!state.strip) document.body.classList.add('v36-strip-hidden');
    if (state.section && state.layout === 'scroll') {
      const target = frame.querySelector(`#section-${state.section}`);
      if (target) { target.scrollIntoView({ block: 'start', behavior: 'instant' }); if (state.landing === 'section') target.classList.add('v36-landing-mark'); }
    } else if (state.landing === 'top') {
      frame.querySelector('.sticky-block')?.classList.add('v36-landing-mark');
    }
    document.documentElement.dataset.ready = `${state.design}:${state.page}:${state.state}:${state.mode}:${state.layout}`;
    window.v36Applied = applied.map((f) => f.id);
  }

  // ---- strip -------------------------------------------------------------------
  function url(changes) {
    const p = new URLSearchParams(location.search);
    for (const [k, v] of Object.entries(changes)) { if (v === '' || v === false || v === null) p.delete(k); else p.set(k, v === true ? '1' : v); }
    return `${location.pathname}?${p.toString()}`;
  }
  const go = (changes) => { location.href = url(changes); };
  const seg = (name, value, items, extra = {}) => `<div class="v36-row"><b>${name}</b><div class="v36-seg">${items.map(([k, label, disabled]) => `<button type="button" data-set="${name}" data-value="${k}" aria-pressed="${k === value}"${disabled ? ' disabled' : ''}>${label}</button>`).join('')}</div></div>`;

  function strip() {
    // Closed until opened: the open strip covers the Case details column.
    const el = document.createElement('details'); el.className = 'v36-strip'; el.open = localStorage.getItem('v36.strip') === 'open';
    el.addEventListener('toggle', () => localStorage.setItem('v36.strip', el.open ? 'open' : 'closed'));
    const optStr = (o) => Object.entries(o).map(([k, v]) => `${k}:${v}`).join(',');
    const groups = [...new Set(F.map((f) => f.surface))];
    const findingsHtml = groups.map((g) => `<div class="v36-group">${g}</div>` + F.filter((f) => f.surface === g).map((f) => {
      if (f.variable) {
        const v = opts[f.variable] || (state.design === 'proposal' ? f.options[1] : 'today');
        return `<label><span class="v36-tier" data-tier="${f.tier}">${f.tier}</span><small>${f.id}</small><span>${f.title}<br><select data-variable="${f.variable}">${f.options.map((o) => `<option value="${o}"${o === v ? ' selected' : ''}>${o}</option>`).join('')}</select></span></label>`;
      }
      const on = opts[f.id] === 'on' || (opts[f.id] !== 'off' && state.design === 'proposal');
      const na = f.states && !f.states.includes(state.state);
      return `<label><input type="checkbox" data-finding="${f.id}"${on ? ' checked' : ''}${na ? ' disabled' : ''}><span class="v36-tier" data-tier="${f.tier}">${f.tier}</span><span>${f.id} · ${f.title}${na ? ` <small>(${f.states.join(', ')} only)</small>` : ''}</span></label>`;
    }).join('')).join('');
    el.innerHTML = `<summary>Mockup controls · v36 Case walk <small>demo control, not product UI</small></summary><div class="v36-body">
      ${seg('design', state.design, [['today', 'Today'], ['proposal', 'Proposal']])}
      ${seg('page', state.page, Object.entries(PAGES))}
      <div class="v36-row"><b>Case state</b><select data-select="state">${Object.entries(STATES).map(([k, v]) => `<option value="${k}"${k === state.state ? ' selected' : ''}>${v}</option>`).join('')}</select></div>
      ${seg('mode', state.mode, [['read', 'Read'], ['edit', 'Edit', !hasEdit(state.state)]])}
      ${seg('layout', state.layout, [['scroll', 'Scroll'], ['tabs', 'Tabs']])}
      <div class="v36-row"><b>Section</b><select data-select="section"><option value="">(top)</option>${SECTIONS.map((s) => `<option value="${s}"${s === state.section ? ' selected' : ''}>${s}</option>`).join('')}</select></div>
      <div class="v36-row"><b>Widgets</b><div class="v36-seg">${[['diff', 'Diff'], ['split', 'Split'], ['ruler', 'Ruler'], ['busy', 'Busy'], ['flicker', 'Flicker']].map(([k, l]) => `<button type="button" data-toggle="${k}" aria-pressed="${state[k]}">${l}</button>`).join('')}<button type="button" data-set="landing" data-value="${state.landing === 'top' ? '' : 'top'}" aria-pressed="${state.landing === 'top'}">Landing: top</button><button type="button" data-set="landing" data-value="${state.landing === 'section' ? '' : 'section'}" aria-pressed="${state.landing === 'section'}">Landing: section</button></div></div>
      <div class="v36-row"><b>Dialog</b><select data-select="dialog"><option value="">(none)</option>${[...document.querySelectorAll('template')].flatMap((t) => [...t.content.querySelectorAll('[data-dialog]')].map((d) => d.dataset.dialog)).filter((v, i, a) => a.indexOf(v) === i).map((d) => `<option value="${d}"${d === state.dialog ? ' selected' : ''}>${d}</option>`).join('')}</select></div>
      ${seg('viewer', state.viewer, [['', 'Viewer closed'], ['1', 'Viewer: image 1']])}
      <div class="v36-findings">${findingsHtml}</div>
      <div class="v36-seg"><button type="button" data-reset>Reset to Today</button><button type="button" data-hide>Hide strip</button></div>
      <div class="v36-note">Every choice is also a query string: design, page, state, mode, layout, section, dialog, viewer, diff, split, ruler, busy, flicker, landing, opt=f14:on,getval:foot,choice:box.</div>
    </div>`;
    el.addEventListener('click', (e) => {
      const b = e.target.closest('button');
      if (!b) return;
      if (b.dataset.set) go({ [b.dataset.set]: b.dataset.value });
      else if (b.dataset.toggle) go({ [b.dataset.toggle]: !state[b.dataset.toggle] });
      else if (b.hasAttribute('data-reset')) go({ design: 'today', opt: '', diff: '', split: '', ruler: '', busy: '', flicker: '', landing: '' });
      else if (b.hasAttribute('data-hide')) go({ strip: '0' });
    });
    el.addEventListener('change', (e) => {
      const t = e.target;
      if (t.dataset.select) go({ [t.dataset.select]: t.value });
      else if (t.dataset.finding) { const o = { ...opts }; o[t.dataset.finding] = t.checked ? 'on' : 'off'; go({ opt: optStr(o) }); }
      else if (t.dataset.variable) { const o = { ...opts }; o[t.dataset.variable] = t.value; go({ opt: optStr(o) }); }
    });
    document.body.append(el);
  }

  document.addEventListener('click', (e) => {
    if (e.target.closest('.v36-strip')) return;
    const a = e.target.closest('a');
    if (a && a.getAttribute('href') === '#') { e.preventDefault(); return; }
    const jump = e.target.closest('a[href^="#section-"], [data-section-jump]');
    if (jump) { e.preventDefault(); const key = jump.dataset.sectionJump || jump.getAttribute('href').slice('#section-'.length); document.querySelector(`#section-${key}`)?.scrollIntoView({ block: 'start' }); return; }
    const link = e.target.closest('.section-link');
    if (link) { e.preventDefault(); if (state.layout === 'tabs') go({ section: link.dataset.sectionLink }); else document.querySelector(`#section-${link.dataset.sectionLink}`)?.scrollIntoView({ block: 'start' }); return; }
    const fold = e.target.closest('[data-collapse-toggle]');
    if (fold && !fold.closest('.v36-f39')) { const panel = fold.closest('[data-collapse]'); if (panel) { panel.classList.toggle('is-collapsed'); fold.setAttribute('aria-expanded', String(!panel.classList.contains('is-collapsed'))); } return; }
    const close = e.target.closest('[data-dialog-close], .dialog-backdrop');
    if (close && e.target === close) { const d = close.closest('[data-dialog]'); if (d) d.hidden = true; return; }
    const dismiss = e.target.closest('[data-dismiss]');
    if (dismiss) { (dismiss.closest('.notice, .toast') || dismiss).remove(); return; }
    const viewerClose = e.target.closest('.case-viewer [data-viewer-close], .case-viewer .case-viewer-close');
    if (viewerClose) { const v = viewerClose.closest('.case-viewer'); if (v) v.hidden = true; document.body.classList.remove('has-viewer'); return; }
    const opener = e.target.closest('[data-dialog-open]');
    if (opener) { e.preventDefault(); const d = document.querySelector(`[data-dialog="${opener.dataset.dialogOpen}"]`); if (d) d.hidden = false; return; }
    const layout = e.target.closest('[data-case-layout-switch] button');
    if (layout) { go({ layout: text(layout).toLowerCase() }); return; }
    if (e.target.closest('form button[type=submit], button[type=submit]')) e.preventDefault();
  });
  document.addEventListener('submit', (e) => e.preventDefault());
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') { document.querySelectorAll('[data-dialog]:not([hidden])').forEach((d) => { d.hidden = true; }); const v = document.querySelector('.case-viewer:not([hidden])'); if (v) { v.hidden = true; document.body.classList.remove('has-viewer'); } }
  });

  window.v36 = { state, STATES, PAGES, SECTIONS, findings: F, render };
  render();
  strip();
})();
