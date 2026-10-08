// v35 mockup only: draws the Case aside (Figures and Next action) as live
// does today and as the three proposals A, B and C, over the captured Case
// page. Every word is the application's own; the fixtures are synthetic.
(() => {
  'use strict';

  const SECTIONS = {
    overview: ['Case details', 'icon-layout-dashboard'],
    claim: ['Claim', 'icon-clipboard-list'],
    inspection: ['Inspection details', 'icon-map-pin'],
    vehicle: ['Vehicle', 'icon-car'],
    damage: ['Damage', 'icon-alert-triangle'],
    valuation: ['Valuation', 'icon-file-text'],
    estimate: ['Repair Spec', 'icon-list'],
    settlement: ['Decisions', 'icon-check-circle'],
    report: ['Report', 'icon-file'],
    files: ['Files', 'icon-folder'],
    notes: ['Notes', 'icon-history'],
  };
  const ACCOUNTS = 'Staff accounts & roles';
  const NO_VALUE = 'No value is recorded.';
  const field = (requirement, section, how) => ({ requirement, source: 'Assessment record', why: NO_VALUE, how, section });

  // A fresh Case as the operator's screenshot of 8 October 2026 shows it,
  // in page order (FRD-16): the blockers a Review Case typically still has.
  const FRESH = [
    field('Pre-incident condition', 'vehicle', 'Record it on the Vehicle section.'),
    field('Vehicle history check', 'vehicle', 'Record the vehicle history on the Vehicle section.'),
    field('Impact location', 'damage', 'Record a damage on the Damage section; the impact location is derived from it.'),
    field('Impact severity', 'damage', 'Record a damage on the Damage section; the impact severity is derived from it.'),
    field('Retail value', 'valuation', 'Enter it on the Valuation section.'),
    field('Trade value', 'valuation', 'Enter it on the Valuation section.'),
    field("Engineer's Value", 'valuation', 'Enter it on the Valuation section.'),
    { requirement: 'Current repair spec', source: 'Estimates', why: 'No repair spec is Current on the Case.',
      how: "Import an estimate, bring one back from Glass's or add a new repair spec on the Repair Spec section; Use repair spec switches to an existing one.",
      section: 'estimate' },
    field('Assessment outcome', 'settlement', 'Record it on the Decisions section.'),
    field('Roadworthiness', 'settlement', 'Record it on the Decisions section.'),
    { requirement: 'Overview image', source: 'Case files',
      why: 'The report prints one Overview image and no image in the report is tagged Overview.',
      how: 'Tag one Case image Overview on the Files section.', section: 'files', tab: 'images' },
  ];

  // A With Engineer Case near the end: a blocker the Repair Spec clears by
  // one control (issue 898), a tab-opening blocker each for Report and Files,
  // and the Accounts blocker no Case section clears, which comes last.
  const NEAR = [
    { requirement: 'Repairer VAT status', source: 'Estimates',
      why: 'The Current repair spec does not say whether the repairer is VAT registered, so the report cannot work out the VAT.',
      how: 'Choose Registered or Not registered as the Repairer VAT status on the Repair Spec section.',
      section: 'estimate', focus: '#estimate-vat-status' },
    { ...field('Agreed fee', 'report', 'Record it on the Fee tab of the Report section.'), tab: 'fee' },
    FRESH[10],
    { requirement: 'Sign-off Engineer', source: 'Case sign-off account',
      why: "The Sign-off Engineer's account has no name or signature the report can print.",
      how: 'An Administrator sets a name and signature on the account in Accounts.', section: null, accounts: true },
  ];

  const PRESETS = {
    review: { label: 'Review · Assign Engineer + blockers', chip: 'Review', engineer: 'Not recorded',
      step: { label: 'Assign Engineer', control: 'Assign Engineer', kind: 'dialog' }, blockers: FRESH },
    engineer: { label: 'With Engineer · blockers only', chip: 'With Engineer', engineer: 'A. Engineer', step: null, blockers: FRESH },
    near: { label: 'With Engineer · AI draft, cancellation, 4 blockers', chip: 'With Engineer', engineer: 'A. Engineer',
      ai: true, cancellation: true, step: null, blockers: NEAR },
    ready: { label: 'With Engineer · Generate report', chip: 'With Engineer', engineer: 'A. Engineer',
      step: { label: 'Generate report', control: 'Report', kind: 'section', section: 'report' }, blockers: [], repair: 4218.6, value: 9150 },
    stale: { label: 'With Engineer · stale generation', chip: 'With Engineer', engineer: 'A. Engineer', stale: true,
      step: { label: 'Generate report', control: 'Report', kind: 'section', section: 'report' }, blockers: [], repair: 4218.6, value: 9150 },
    audit: { label: 'Complete · Create audit', chip: 'Complete', engineer: 'A. Engineer',
      step: { label: 'Create audit', control: 'Create audit', kind: 'dialog' }, blockers: [], repair: 4218.6, value: 9150 },
  };
  const DESIGNS = { live: 'Live today', a: 'A · Grouped by section', b: 'B · One line each, opens for detail', c: 'C · Report not ready as its own card' };
  const TONE = { Review: 'navy', 'With Engineer': 'navy', Complete: 'green' };

  const esc = (v) => String(v).replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;');
  const icon = (id) => `<svg class="icon" aria-hidden="true"><use href="#${id}" /></svg>`;
  const money = (v) => '£' + v.toLocaleString('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  const href = (b) => `#section-${b.section}`;
  const attrs = (b) => `data-section-jump="${b.section}"${b.tab ? ` data-section-tab="${b.tab}"` : ''}`;

  const params = new URLSearchParams(location.search);
  const state = {
    design: DESIGNS[params.get('design')] ? params.get('design') : 'live',
    preset: PRESETS[params.get('state')] ? params.get('state') : 'review',
    tone: params.get('tone') === 'primary' ? 'primary' : 'plain',
    role: params.get('role') === 'user' ? 'user' : 'admin',
  };

  // ---- pieces every design shares ------------------------------------------

  function figures(p) {
    const ratio = p.repair && p.value ? `${Math.round(p.repair / p.value * 100)}%` : '—';
    return `<section class="panel context-card" data-figures>
      <div class="panel-head"><h2>Figures</h2></div>
      <div class="panel-body"><dl class="figures">
        <div class="figure figure--big"><dt>Repair cost inc VAT</dt><dd class="mono">${p.repair ? money(p.repair) : '—'}</dd></div>
        <div class="figure"><dt>Engineer's Value</dt><dd class="mono">${p.value ? money(p.value) : '—'}</dd></div>
        <div class="figure"><dt>Repair cost of value</dt><dd class="mono">${ratio}</dd></div>
      </dl></div></section>`;
  }

  function extras(p) {
    let html = '';
    if (p.ai) {
      html += `<div class="next-row ai-next" data-ai-draft><span><span class="src-tag src-tag--ai">AI</span> Estimate draft ready</span><a class="btn btn--small btn--primary" href="#section-estimate">Review estimate</a></div>`;
    }
    if (p.cancellation) {
      html += `<div class="next-row" data-cancellation-received><span>Cancellation received</span><a class="btn btn--small" href="#">Open message</a></div>`;
    }
    if (p.stale) {
      html += `<div class="notice notice--warning" role="status" data-dismissable data-report-stale>${icon('icon-alert-triangle')}<span>A newer fact changed after this generation. Generate again before delivery.</span><button type="button" class="dismiss" data-dismiss aria-label="Dismiss">${icon('icon-x')}</button></div>`;
    }
    return html;
  }

  function liveStep(s) {
    if (!s) return '';
    const control = s.kind === 'section'
      ? `<a class="btn btn--small" href="#section-${s.section}" data-section-jump="${s.section}">${esc(s.control)}</a>`
      : `<button type="button" class="btn btn--small" data-next-step>${esc(s.control)}</button>`;
    return `<div class="next-row"><span data-next-label>${esc(s.label)}</span>${control}</div>`;
  }

  // Proposed in A, B and C: where the control says what the step says, the
  // step is that one control at full width rather than the words twice.
  function step(s) {
    if (!s) return '';
    if (s.label !== s.control) return liveStep(s);
    const tone = state.tone === 'primary' ? ' btn--primary' : '';
    return `<div class="rail-step"><button type="button" class="btn${tone}" data-next-step>${esc(s.control)}</button></div>`;
  }

  // What clears a blocker: the Repair Spec claim with focus, a section jump,
  // Accounts for an Administrator, or nothing.
  function target(b) {
    if (b.focus) return { kind: 'claim', label: SECTIONS.estimate[0] };
    if (b.section) return { kind: 'section', label: SECTIONS[b.section][0] };
    if (b.accounts && state.role === 'admin') return { kind: 'accounts', label: ACCOUNTS };
    return null;
  }

  // The blocker's control, its content the target's label unless given.
  function control(b, cls, inner) {
    const t = target(b);
    if (!t) return '';
    const content = inner ?? esc(t.label);
    if (t.kind === 'claim') return `<button type="button" class="${cls}" data-section-edit="estimate" data-edit-focus="${esc(b.focus)}">${content}</button>`;
    if (t.kind === 'accounts') return `<a class="${cls}" href="#" data-blocker-accounts>${content}</a>`;
    return `<a class="${cls}" href="${href(b)}" ${attrs(b)}>${content}</a>`;
  }

  const notReadyHead = (tag = 'h3') => `<${tag} class="rail-head">${icon('icon-alert-triangle')}Report not ready</${tag}>`;

  // ---- live today ------------------------------------------------------------

  function live(p) {
    const list = p.blockers.length ? `<div class="sub-panel blockers" data-report-not-ready><h3>Report not ready</h3><ul class="blocker-list">${
      p.blockers.map((b) => {
        const c = control(b, 'btn btn--small');
        return `<li class="blocker" data-report-blocker="${b.section ?? ''}"><strong>${esc(b.requirement)}</strong><small><b>Source:</b> ${esc(b.source)}<br /><b>Why:</b> ${esc(b.why)}<br />${esc(b.how)}</small>${c ? `<div class="blocker-actions">${c}</div>` : ''}</li>`;
      }).join('')}</ul></div>` : '';
    return `${figures(p)}<section class="panel context-card" data-next-action><div class="panel-head"><h2>Next action</h2></div><div class="panel-body">${extras(p)}${liveStep(p.step)}${list}</div></section>`;
  }

  // ---- A: grouped by section ------------------------------------------------
  // One Next action card. Below the step, the blockers sit under the section
  // that clears them (the list is already in page order, so a section's rows
  // are adjacent). The requirement is the link; source and reason share one
  // line; what clears it follows.

  function designA(p) {
    let list = '';
    if (p.blockers.length) {
      const groups = [];
      for (const b of p.blockers) {
        const key = b.section ?? (b.accounts ? 'accounts' : 'none');
        if (!groups.length || groups.at(-1).key !== key) groups.push({ key, items: [] });
        groups.at(-1).items.push(b);
      }
      list = `<div class="rail-a" data-report-not-ready>${notReadyHead()}${groups.map((g) => {
        const head = SECTIONS[g.key]
          ? `<div class="rail-a-group-head">${esc(SECTIONS[g.key][0])}</div>`
          : g.key === 'accounts' && state.role === 'admin' ? `<div class="rail-a-group-head">${esc(ACCOUNTS)}</div>` : '';
        return `<div class="rail-a-group" data-rail-group="${g.key}">${head}<ul>${g.items.map((b) => {
          const name = control(b, 'rail-a-name', esc(b.requirement)) || `<span class="rail-a-name">${esc(b.requirement)}</span>`;
          return `<li data-report-blocker="${b.section ?? ''}">${name}<small>${esc(b.source)} · ${esc(b.why)}</small><small class="rail-how">${esc(b.how)}</small></li>`;
        }).join('')}</ul></div>`;
      }).join('')}</div>`;
    }
    return `${figures(p)}<section class="panel context-card" data-next-action><div class="panel-head"><h2>Next action</h2></div><div class="panel-body">${extras(p)}${step(p.step)}${list}</div></section>`;
  }

  // ---- B: one line each, opens for detail ----------------------------------
  // Each blocker is one line: its requirement and, at the right, the section
  // that clears it. The line opens to show source, reason and what clears it.

  function designB(p) {
    const list = p.blockers.length ? `<div class="rail-b" data-report-not-ready>${notReadyHead()}<div class="rail-b-list">${
      p.blockers.map((b) => `<details class="rail-b-row" data-report-blocker="${b.section ?? ''}"><summary>${icon('icon-chevron-right')}<span class="rail-b-name">${esc(b.requirement)}</span>${control(b, 'rail-b-go')}</summary><div class="rail-b-detail"><b>Source:</b> ${esc(b.source)}<br /><b>Why:</b> ${esc(b.why)}<br />${esc(b.how)}</div></details>`).join('')
    }</div></div>` : '';
    return `${figures(p)}<section class="panel context-card" data-next-action><div class="panel-head"><h2>Next action</h2></div><div class="panel-body">${extras(p)}${step(p.step)}${list}</div></section>`;
  }

  // ---- C: Report not ready as its own card ----------------------------------
  // Next action holds only the step. The blockers are a card of their own
  // below it, each row one whole link to the section that clears it; on a
  // tall list only this card scrolls, so Figures and the step stay in view.

  function designC(p) {
    const next = (extras(p) || p.step)
      ? `<section class="panel context-card" data-next-action><div class="panel-head"><h2>Next action</h2></div><div class="panel-body">${extras(p)}${step(p.step)}</div></section>` : '';
    const card = p.blockers.length ? `<section class="panel context-card rail-c" data-report-not-ready><div class="panel-head">${notReadyHead('h2')}</div><ul class="rail-c-list">${
      p.blockers.map((b) => {
        const t = target(b);
        const inner = `<span class="rail-c-top"><strong>${esc(b.requirement)}</strong>${t ? `<span class="rail-c-where">${esc(t.label)}${icon('icon-chevron-right')}</span>` : ''}</span><small>${esc(b.source)} · ${esc(b.why)}</small><small class="rail-how">${esc(b.how)}</small>`;
        const row = control(b, 'rail-c-row', inner) || `<div class="rail-c-row">${inner}</div>`;
        return `<li data-report-blocker="${b.section ?? ''}">${row}</li>`;
      }).join('')}</ul></section>` : '';
    return `${figures(p)}${next}${card}`;
  }

  const DRAW = { live, a: designA, b: designB, c: designC };

  // ---- page --------------------------------------------------------------------

  function render() {
    const p = PRESETS[state.preset];
    const frame = document.getElementById('v35-frame');
    frame.replaceChildren(document.getElementById('tpl-frame').content.cloneNode(true));
    const chips = frame.querySelector('[data-case-ribbon-chips]');
    if (chips) chips.innerHTML = `<span class="status status--${TONE[p.chip] ?? 'neutral'}">${esc(p.chip)}</span>`;
    const engineer = [...frame.querySelectorAll('.ribbon-item')].find((i) => i.querySelector('.ribbon-label')?.textContent === 'Engineer');
    if (engineer) engineer.querySelector('.ribbon-value').innerHTML = `<span>${esc(p.engineer)}</span>`;
    const aside = frame.querySelector('aside[data-case-aside]');
    aside.dataset.rail = state.design;
    aside.innerHTML = DRAW[state.design](p);
    const q = new URLSearchParams({ design: state.design, state: state.preset });
    if (state.tone !== 'plain') q.set('tone', state.tone);
    if (state.role !== 'admin') q.set('role', state.role);
    history.replaceState(null, '', `?${q}`);
    for (const [key, value] of Object.entries(state)) {
      const picker = document.querySelector(`[data-pick="${key}"]`);
      if (picker) picker.value = value;
    }
    document.documentElement.dataset.ready = `${state.design}:${state.preset}`;
  }

  function strip() {
    const pick = (key, label, options) => `<label>${label}<select data-pick="${key}">${Object.entries(options).map(([v, l]) => `<option value="${v}">${esc(l)}</option>`).join('')}</select></label>`;
    const el = document.createElement('details');
    el.className = 'v35-mock';
    el.open = params.get('strip') !== '0';
    el.innerHTML = `<summary>Mockup controls · v35 Case aside</summary><div class="v35-mock-body">${
      pick('design', 'Design', DESIGNS)}${
      pick('preset', 'Case state', Object.fromEntries(Object.entries(PRESETS).map(([k, v]) => [k, v.label])))}${
      pick('tone', 'Step control (A–C)', { plain: 'Secondary button', primary: 'Primary button' })}${
      pick('role', 'Viewer', { admin: 'Administrator', user: 'User' })
    }<p>Demo control, not product UI. Synthetic data.</p></div>`;
    el.addEventListener('change', (e) => {
      const key = e.target.dataset.pick;
      if (key) { state[key] = e.target.value; render(); }
    });
    document.body.append(el);
  }

  document.addEventListener('click', (e) => {
    const dismiss = e.target.closest('[data-dismiss]');
    if (dismiss) { dismiss.closest('[data-dismissable]')?.remove(); return; }
    const jump = e.target.closest('a[href^="#section-"]');
    if (jump) {
      e.preventDefault();
      document.querySelector(jump.getAttribute('href'))?.scrollIntoView({ block: 'start' });
      return;
    }
    if (e.target.closest('a[href="#"], [data-next-step], [data-section-edit]')) e.preventDefault();
  });

  window.v35 = { PRESETS, DESIGNS, state, render };
  strip();
  render();
})();
