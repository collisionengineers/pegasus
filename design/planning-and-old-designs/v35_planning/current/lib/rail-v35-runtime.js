// v35 mockup only: draws the Case aside (Figures and Next action) as live
// does today and as six proposals over the captured Case page. Every word is
// the application's own; the fixtures are synthetic.
(() => {
  'use strict';

  const SECTIONS = {
    overview: 'Case details', claim: 'Claim', inspection: 'Inspection details', vehicle: 'Vehicle',
    damage: 'Damage', valuation: 'Valuation', estimate: 'Repair Spec', settlement: 'Decisions',
    report: 'Report', files: 'Files', notes: 'Notes',
  };
  // Damage and Valuation nest under Vehicle in the section row.
  const NAV = { damage: 'vehicle', valuation: 'vehicle' };
  const ACCOUNTS = 'Staff accounts & roles';
  const OUTSTANDING = 'Outstanding requirements';
  const REPORT_NOT_READY = 'Report not ready';
  const NO_VALUE = 'No value is recorded.';
  const field = (requirement, section, how) => ({ requirement, source: 'Assessment record', why: NO_VALUE, how, section });
  const caseFact = (requirement, section, why, how) => ({ requirement, source: 'Case record', why, how, section });

  // A Review Case as the operator's screenshot of 8 October 2026 shows it,
  // in page order (FRD-16).
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

  // A standalone Audit just in, Not ready: the report still needs nearly
  // everything, Case facts included (AssessmentPolicy.EvaluateReadiness).
  const EMPTY = [
    caseFact('Claim reference', 'overview', 'The report prints the claim reference as Your Ref and none is recorded.', 'Record it on the Case details section.'),
    caseFact('Incident date', 'overview', 'The report prints the incident date and none is recorded.', 'Record it on the Case details section.'),
    caseFact('Sign-off Engineer', 'overview', 'The Case has no Sign-off Engineer.', 'Choose the Sign-off Engineer on Case details.'),
    caseFact('Claimant name', 'claim', "The report prints the claimant's name and none is recorded.", 'Record it on the Claim section.'),
    caseFact('Inspection type', 'inspection', 'The report says how the vehicle was assessed and no inspection type is recorded.', 'Choose Inspect at on the Inspection details section.'),
    caseFact('Inspection date', 'inspection', 'The report says the damage was assessed on the Inspection date and none is recorded.', 'Record it on the Inspection details section.'),
    caseFact('Vehicle registration', 'vehicle', 'The report prints the registration and none is recorded.', 'Record it on the Vehicle section.'),
    caseFact('Vehicle make', 'vehicle', 'No confirmed make is recorded.', 'Record it on the Vehicle section.'),
    caseFact('Vehicle model', 'vehicle', 'No confirmed model is recorded.', 'Record it on the Vehicle section.'),
    caseFact('Vehicle year', 'vehicle', 'No confirmed year is recorded.', 'Record it on the Vehicle section.'),
    field('Vehicle type', 'vehicle', 'Record it on the Vehicle section.'),
    ...FRESH,
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

  // The Case requirements (DetailsModel.OutstandingRequirements). Today the
  // step names the first, linking to Case details; the proposals list every
  // one (item J) and send Original report missing to Files, where Mark as
  // original report is (item K).
  const REQUIREMENTS = [
    { title: 'Original report missing', source: 'Audit', why: null, section: 'files' },
    { title: 'Images incomplete', source: 'Case requirements', why: 'Details are incomplete', section: 'overview' },
  ];

  const PRESETS = {
    notready: { label: 'Not ready · original report, images, Case facts missing', chip: 'Not ready', engineer: 'Not recorded',
      step: { label: REQUIREMENTS[0].title, control: 'Case details', kind: 'section', section: 'overview' },
      requirements: REQUIREMENTS, blockers: EMPTY },
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
  const DESIGNS = {
    live: 'Live today',
    1: '1 · Grouped by section',
    2: '2 · One line each, opens for detail',
    3: '3 · Report not ready as its own card',
    4: '4 · One row per section, opens for detail',
    5: '5 · First item in full, the rest one line each',
    6: '6 · Follows the page',
  };
  const TONE = { 'Not ready': 'amber', Review: 'navy', 'With Engineer': 'navy', Complete: 'green' };

  const esc = (v) => String(v).replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;');
  const icon = (id) => `<svg class="icon" aria-hidden="true"><use href="#${id}" /></svg>`;
  const money = (v) => '£' + v.toLocaleString('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

  const params = new URLSearchParams(location.search);
  const state = {
    design: DESIGNS[params.get('design')] ? params.get('design') : 'live',
    preset: PRESETS[params.get('state')] ? params.get('state') : 'notready',
    tone: params.get('tone') === 'primary' ? 'primary' : 'plain',
    role: params.get('role') === 'user' ? 'user' : 'admin',
  };

  // ---- pieces every drawing shares --------------------------------------------

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

  // Item A: where the control says what the step says, the step is that one
  // control at full width rather than the words twice.
  function step(s) {
    if (!s) return '';
    if (s.label !== s.control) return liveStep(s);
    const tone = state.tone === 'primary' ? ' btn--primary' : '';
    return `<div class="rail-step"><button type="button" class="btn${tone}" data-next-step>${esc(s.control)}</button></div>`;
  }

  // What clears an item: the Repair Spec claim with focus, a section jump
  // (and its tab), Accounts for an Administrator, or nothing.
  function target(b) {
    if (b.focus) return { kind: 'claim', label: SECTIONS.estimate };
    if (b.section) return { kind: 'section', label: SECTIONS[b.section] };
    if (b.accounts && state.role === 'admin') return { kind: 'accounts', label: ACCOUNTS };
    return null;
  }

  // The item's control, its content the target's label unless given.
  function control(b, cls, inner) {
    const t = target(b);
    if (!t) return '';
    const content = inner ?? esc(t.label);
    if (t.kind === 'claim') return `<button type="button" class="${cls}" data-section-edit="estimate" data-edit-focus="${esc(b.focus)}">${content}</button>`;
    if (t.kind === 'accounts') return `<a class="${cls}" href="#" data-blocker-accounts>${content}</a>`;
    return `<a class="${cls}" href="#section-${b.section}" data-section-jump="${b.section}"${b.tab ? ` data-section-tab="${b.tab}"` : ''}>${content}</a>`;
  }

  const head = (words, tag = 'h3') => `<${tag} class="rail-head">${icon('icon-alert-triangle')}${esc(words)}</${tag}>`;
  const asRow = (r) => ({ requirement: r.title, source: r.source, why: r.why, how: null, section: r.section, requirementRow: true });
  const mark = (b) => b.requirementRow ? 'data-case-requirement' : `data-report-blocker="${b.section ?? ''}"`;
  const sourceWhy = (b) => b.why ? `${esc(b.source)} · ${esc(b.why)}` : esc(b.source);
  const how = (b) => b.how ? `<small class="rail-how">${esc(b.how)}</small>` : '';
  const name = (b, cls) => control(b, cls, esc(b.requirement)) || `<span class="${cls}">${esc(b.requirement)}</span>`;
  const requirementsOf = (p) => (p.requirements ?? []).map(asRow);
  const groupKey = (b) => b.section ?? (b.accounts ? 'accounts' : 'none');
  const groupLabel = (key) => SECTIONS[key] ?? (key === 'accounts' ? ACCOUNTS : '');

  // Adjacent items of one section: the list is in page order, so a
  // section's items already sit together.
  function groups(items) {
    const out = [];
    for (const b of items) {
      const key = groupKey(b);
      if (!out.length || out.at(-1).key !== key) out.push({ key, items: [] });
      out.at(-1).items.push(b);
    }
    return out;
  }

  // One item in full: its requirement (the link), source and reason, and
  // what clears it.
  const fullItem = (b) => `<li class="rail-item" ${mark(b)}>${name(b, 'rail-name')}<small>${sourceWhy(b)}</small>${how(b)}</li>`;
  const fullList = (items, words, attr) => `<div class="rail-block" ${attr}>${head(words)}<ul class="rail-items">${items.map(fullItem).join('')}</ul></div>`;

  // One item on one line that opens for its detail (designs 2 and 5).
  const lineItem = (b) => `<details class="rail-line" ${mark(b)}><summary>${icon('icon-chevron-right')}<span class="rail-line-name">${esc(b.requirement)}</span>${control(b, 'rail-line-go')}</summary><div class="rail-line-detail"><b>Source:</b> ${esc(b.source)}${b.why ? `<br /><b>Why:</b> ${esc(b.why)}` : ''}${b.how ? `<br />${esc(b.how)}` : ''}</div></details>`;
  const lineList = (items, words, attr) => `<div class="rail-block" ${attr}>${head(words)}<div class="rail-lines">${items.map(lineItem).join('')}</div></div>`;

  const nextCard = (p, body) => `<section class="panel context-card" data-next-action><div class="panel-head"><h2>Next action</h2></div><div class="panel-body">${extras(p)}${body}</div></section>`;
  // The proposals list every Case requirement in place of the one step.
  const firstOf = (p, list) => p.requirements?.length ? list(requirementsOf(p), OUTSTANDING, 'data-case-requirements') : step(p.step);

  // ---- live today ------------------------------------------------------------

  function live(p) {
    const list = p.blockers.length ? `<div class="sub-panel blockers" data-report-not-ready><h3>Report not ready</h3><ul class="blocker-list">${
      p.blockers.map((b) => {
        const c = control(b, 'btn btn--small');
        return `<li class="blocker" data-report-blocker="${b.section ?? ''}"><strong>${esc(b.requirement)}</strong><small><b>Source:</b> ${esc(b.source)}<br /><b>Why:</b> ${esc(b.why)}<br />${esc(b.how)}</small>${c ? `<div class="blocker-actions">${c}</div>` : ''}</li>`;
      }).join('')}</ul></div>` : '';
    return `${figures(p)}${nextCard(p, liveStep(p.step) + list)}`;
  }

  // ---- 1: grouped by section ---------------------------------------------------
  // The blockers sit under the section that clears them; every item in full.

  function design1(p) {
    const list = p.blockers.length ? `<div class="rail-block" data-report-not-ready>${head(REPORT_NOT_READY)}${groups(p.blockers).map((g) =>
      `<div class="rail-group" data-rail-group="${g.key}">${groupLabel(g.key) ? `<div class="rail-group-head">${esc(groupLabel(g.key))}</div>` : ''}<ul class="rail-items">${g.items.map(fullItem).join('')}</ul></div>`).join('')}</div>` : '';
    return `${figures(p)}${nextCard(p, firstOf(p, fullList) + list)}`;
  }

  // ---- 2: one line each, opens for detail --------------------------------------

  function design2(p) {
    const list = p.blockers.length ? lineList(p.blockers, REPORT_NOT_READY, 'data-report-not-ready') : '';
    return `${figures(p)}${nextCard(p, firstOf(p, lineList) + list)}`;
  }

  // ---- 3: Report not ready as its own card ---------------------------------------
  // Next action holds the requirements or the step. The blockers are a card
  // of their own, each row one whole link; at 1441px and above only that
  // card scrolls, so Figures and Next action stay in view.

  function rows3(items) {
    return `<ul class="rail-rows">${items.map((b) => {
      const t = target(b);
      const inner = `<span class="rail-row-top"><strong>${esc(b.requirement)}</strong>${t ? `<span class="rail-row-where">${esc(t.label)}${icon('icon-chevron-right')}</span>` : ''}</span><small>${sourceWhy(b)}</small>${how(b)}`;
      return `<li ${mark(b)}>${control(b, 'rail-row', inner) || `<div class="rail-row">${inner}</div>`}</li>`;
    }).join('')}</ul>`;
  }

  function design3(p) {
    let next = '';
    if (p.requirements?.length) {
      next = `<section class="panel context-card rail-card rail-card--next" data-next-action data-case-requirements><div class="panel-head"><h2>Next action</h2></div>${extras(p) ? `<div class="panel-body">${extras(p)}</div>` : ''}${rows3(requirementsOf(p))}</section>`;
    } else if (extras(p) || p.step) {
      next = nextCard(p, step(p.step));
    }
    const card = p.blockers.length ? `<section class="panel context-card rail-card" data-report-not-ready><div class="panel-head">${head(REPORT_NOT_READY, 'h2')}</div>${rows3(p.blockers)}</section>` : '';
    return `${figures(p)}${next}${card}`;
  }

  // ---- 4: one row per section, opens for detail ----------------------------------
  // Each section with blockers is one row: its name (the link) and what it
  // is missing, by name. The row opens to every item in full.

  function sectionRows(items, opened) {
    return groups(items).map((g) => {
      const label = groupLabel(g.key);
      const link = g.key in SECTIONS
        ? `<a class="rail-name" href="#section-${g.key}" data-section-jump="${g.key}">${esc(label)}</a>`
        : g.key === 'accounts' && state.role === 'admin' ? `<a class="rail-name" href="#" data-blocker-accounts>${esc(label)}</a>` : `<span class="rail-name">${esc(label)}</span>`;
      return `<details class="rail-sect" data-rail-group="${g.key}"${opened === g.key ? ' open' : ''}><summary><span class="rail-sect-main">${link}<small>${g.items.map((b) => esc(b.requirement)).join(' · ')}</small></span>${icon('icon-chevron-down')}</summary><ul class="rail-items">${g.items.map(fullItem).join('')}</ul></details>`;
    }).join('');
  }

  function design4(p) {
    const list = p.blockers.length ? `<div class="rail-block" data-report-not-ready>${head(REPORT_NOT_READY)}<div class="rail-sects">${sectionRows(p.blockers)}</div></div>` : '';
    return `${figures(p)}${nextCard(p, firstOf(p, fullList) + list)}`;
  }

  // ---- 5: first item in full, the rest one line each -----------------------------
  // The first thing to do (the step, else the first requirement, else the
  // first blocker) is drawn in full with its control at full width; every
  // other item is one line that opens for its detail.

  function featured(b) {
    const tone = state.tone === 'primary' ? ' btn--primary' : '';
    return `<div class="rail-first" ${mark(b)}><strong>${esc(b.requirement)}</strong><small>${sourceWhy(b)}</small>${how(b)}${control(b, `btn${tone} rail-first-go`)}</div>`;
  }

  function design5(p) {
    const reqs = requirementsOf(p);
    const blockers = [...p.blockers];
    let first;
    if (reqs.length) first = featured(reqs.shift());
    else if (p.step) first = step(p.step);
    else if (blockers.length) first = featured(blockers.shift());
    else first = '';
    const rest = (reqs.length ? lineList(reqs, OUTSTANDING, 'data-case-requirements') : '')
      + (blockers.length ? lineList(blockers, REPORT_NOT_READY, 'data-report-not-ready') : '');
    return `${figures(p)}${nextCard(p, first + rest)}`;
  }

  // ---- 6: follows the page ----------------------------------------------------
  // The section row marks each section that clears a blocker. In the aside
  // the blockers are one row per section, as in 4, and the row of the
  // section in view opens by itself as the page scrolls.

  function design6(p) {
    const list = p.blockers.length ? `<div class="rail-block" data-report-not-ready>${head(REPORT_NOT_READY)}<div class="rail-sects rail-sects--follow">${sectionRows(p.blockers)}</div></div>` : '';
    return `${figures(p)}${nextCard(p, firstOf(p, fullList) + list)}`;
  }

  function markSectionRow(frame, p) {
    const marked = new Set(p.blockers.map((b) => NAV[b.section] ?? b.section).filter(Boolean));
    for (const link of frame.querySelectorAll('[data-section-link]')) {
      if (marked.has(link.dataset.sectionLink)) link.insertAdjacentHTML('beforeend', '<span class="rail-nav-mark" aria-hidden="true"></span>');
    }
  }

  // The section in view: the last whose top is in the upper third of the
  // window, below the sticky block.
  function sectionInView() {
    const line = window.innerHeight * 0.35;
    let current = null;
    for (const s of document.querySelectorAll('section.record-section[data-section]')) {
      if (s.getBoundingClientRect().top <= line) current = s.dataset.section;
    }
    return current ?? document.querySelector('section.record-section[data-section]')?.dataset.section;
  }

  let followed = null;
  function follow() {
    if (state.design !== '6') return;
    const here = sectionInView();
    if (here === followed) return;
    followed = here;
    for (const d of document.querySelectorAll('.rail-sects--follow .rail-sect')) {
      const on = d.dataset.railGroup === here;
      d.open = on;
      d.classList.toggle('is-here', on);
      // The sticky aside scrolls itself to bring the opened row into view.
      const aside = d.closest('.workspace-aside');
      if (on && aside.scrollHeight > aside.clientHeight) {
        aside.scrollTop += d.getBoundingClientRect().top - aside.getBoundingClientRect().top - aside.clientHeight / 3;
      }
    }
  }

  const DRAW = { live, 1: design1, 2: design2, 3: design3, 4: design4, 5: design5, 6: design6 };

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
    if (state.design === '6') markSectionRow(frame, p);
    followed = null;
    follow();
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
      pick('tone', 'Step control (1–6)', { plain: 'Secondary button', primary: 'Primary button' })}${
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
  window.addEventListener('scroll', follow, { passive: true });

  window.v35 = { PRESETS, DESIGNS, state, render, follow };
  strip();
  render();
})();
