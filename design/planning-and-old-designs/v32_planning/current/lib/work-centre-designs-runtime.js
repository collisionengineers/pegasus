// Offline interaction model for the v32 Work Centre proposals. Every action
// changes only the synthetic fixtures on this page. One runtime draws the five
// designs and the live baseline from the same state, so their behaviour
// (scope, kinds, Find, the open row, Dismiss, Assign, Complete job, Refresh)
// can be compared like for like.
(() => {
  'use strict';
  const cfg = window.workCentreDesign;
  const D = cfg.id;
  const params = new URLSearchParams(location.search);
  const presetId = cfg.presets.some(([key]) => key === params.get('state')) ? params.get('state') : 'default';
  const defaults = D === 'live'
    ? { dismiss: 'live', stats: 'none', clock: 'section', taken: 'lease' }
    : { dismiss: 'icon', stats: 'own', clock: 'header', taken: 'until' };
  const opt = { ...defaults };
  for (const pair of (params.get('opt') || '').split(',')) {
    const [key, value] = pair.split(':');
    if (key in opt && value) opt[key] = value;
  }
  const roles = ['Administrator', 'Engineer', 'User'];
  const role = roles.includes(params.get('role')) ? params.get('role') : 'Administrator';
  const me = 'alex';
  const SCOPE_KEY = 'v32.work-centre.scope';
  const kindControl = D === 'b' || D === 'd' ? 'select' : 'chips';
  const esc = (value) => String(value ?? '').replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;');
  const icon = (name, cls = 'icon') => `<svg class="${cls}" aria-hidden="true"><use href="#icon-${name}" /></svg>`;
  const clone = (value) => JSON.parse(JSON.stringify(value));

  /* ---------- State ---------- */
  const state = {
    scope: 'office', kinds: [], search: '', tab: 'attention', selected: null, assignOpen: false,
    items: clone(cfg.items), arrivals: clone(cfg.arrivals), jobs: clone(cfg.jobs), dismissed: [],
    attentionFailed: false, aiFailed: false, newCasesFailed: false, statsFailed: false, allEmpty: false,
    updated: cfg.clock.updated, busy: false, collapsed: [],
  };
  const kindName = (slug) => cfg.kinds.find(([key]) => key === slug)?.[1] ?? slug;
  const canTake = (item) => !item.owner && (item.kind === 'unassigned' || item.kind === 'triage');
  const isMine = (item) => item.owner === me || canTake(item);
  const ownerLabel = (item) => item.owner ?? (item.kind === 'unidentified' || item.kind === 'ai' ? 'No owner' : 'No Engineer');
  const inScope = () => state.items.filter((item) => state.scope === 'office' || isMine(item));
  const matches = (item) => !state.search || [item.ref, item.title, item.subject, item.secondFact?.[1], ownerLabel(item), kindName(item.kind)]
    .filter(Boolean).join(' ').toLowerCase().includes(state.search.toLowerCase());
  const kindOn = (item) => !state.kinds.length || state.kinds.includes(item.kind);
  const visible = () => inScope().filter((item) => kindOn(item) && matches(item));
  const kindCount = (slug) => inScope().filter((item) => item.kind === slug).length;
  const filtered = () => state.kinds.length > 0 || state.search !== '';
  const selectedItem = () => state.items.find((item) => item.id === state.selected) ?? null;
  const groupLabel = (group, count) => `${{ overdue: 'Overdue', today: 'Due today', later: 'Later' }[group]} (${count})`;
  const newerArrivals = () => state.arrivals.filter((row) => row.newer);
  const jobsInProgress = () => state.jobs.filter((job) => job.state !== 'Draft ready');

  function dismissRecord(record) {
    state.dismissed.push(record);
    state.items = state.items.filter((item) => item.record !== record);
    state.arrivals = state.arrivals.filter((row) => row.record !== record);
    state.jobs = state.jobs.filter((job) => job.record !== record);
    if (!selectedItem()) { state.selected = null; state.assignOpen = false; }
  }

  let remembered = null;
  try { remembered = localStorage.getItem(SCOPE_KEY); } catch { /* file storage may be refused */ }
  const roleDefault = D === 'b' && role === 'Engineer' ? 'mine' : 'office';
  state.scope = params.has('state') ? (D === 'b' ? roleDefault : 'office') : (['office', 'mine'].includes(remembered) ? remembered : roleDefault);
  switch (presetId) {
    case 'mine': state.scope = 'mine'; break;
    case 'filtered': state.kinds = kindControl === 'select' ? ['held'] : ['held', 'review']; break;
    case 'find': state.search = 'BH17RZV'; break;
    case 'selected': state.selected = 'r5'; break;
    case 'dismissed': dismissRecord('QDOS26203'); break;
    case 'empty-office': state.items = []; break;
    case 'empty-mine': state.scope = 'mine'; state.items = state.items.filter((item) => !isMine(item)); break;
    case 'attention-unavailable': state.attentionFailed = true; break;
    case 'ai-unavailable': state.aiFailed = true; break;
    case 'stats-unavailable': state.statsFailed = true; break;
    case 'all-empty': state.items = []; state.arrivals = []; state.jobs = []; state.allEmpty = true; break;
    case 'assign': state.selected = 'r5'; state.assignOpen = true; break;
    case 'new-cases': case 'ai-jobs': state.tab = presetId; break;
    default: break;
  }
  if (params.get('scope')) state.scope = params.get('scope') === 'mine' ? 'mine' : 'office';
  if (params.get('tab')) state.tab = params.get('tab');
  if (params.get('selected')) state.selected = params.get('selected');
  if (params.get('assign') === '1' && selectedItem()?.kind === 'unassigned') state.assignOpen = true;

  /* ---------- Shared markup ---------- */
  const chip = (label, tone) => `<span class="status status--${tone}">${esc(label)}</span>`;
  const href = (extra) => { const q = new URLSearchParams(); q.set('state', presetId); for (const [k, v] of Object.entries(extra)) if (v != null && v !== '') q.set(k, v); return `?${q}`; };
  const destination = (label, route, cls, inner) => `<a class="${cls}" href="${esc(route)}" data-destination="${esc(label)}">${inner ?? esc(label)}</a>`;

  // The one row end (item B): an icon-only X named for its record, or the text button for comparison.
  function dismissControl(record, ref, liveStyle) {
    const label = `Dismiss ${ref}`;
    const text = liveStyle === 'text' || (liveStyle !== 'icon' && opt.dismiss === 'text');
    const cls = liveStyle === 'text-full' ? 'btn' : text ? 'btn btn--small' : 'btn btn--small btn--icon';
    const inner = text || liveStyle === 'text-full' ? `${icon('x')}<span>Dismiss</span>` : icon('x');
    return `<form class="wc-dismiss" data-wc-dismiss><button type="button" class="${cls}" data-dismiss="${esc(record)}" aria-label="${esc(label)}" title="${esc(label)}" data-busy-label="Dismissing…">${inner}</button></form>`;
  }

  function actionControl(item, size = '') {
    if (item.kind === 'unassigned') {
      return `<a class="btn btn--dark${size}" href="${href({ selected: item.id, assign: 1 })}" data-assign="${item.id}" data-focus="assign-${item.id}${size ? '' : '-detail'}" data-wc-dialog="wc-assign-dialog">${icon('arrow-right')}<span>Assign Engineer</span></a>`;
    }
    return `<a class="btn btn--dark${size}" href="${esc(item.route)}" data-destination="${esc(item.action)}">${icon('arrow-right')}<span>${esc(item.action)}</span></a>`;
  }
  const completeControl = (item) => { const job = item.job && state.jobs.find((row) => row.id === item.job); return job?.canComplete ? `<form data-wc-complete><button type="button" class="btn" data-complete="${job.id}" data-busy-label="Completing…">Complete job</button></form>` : ''; };
  const takeControl = (item) => `<form data-wc-take><button type="button" class="btn" data-take="${item.id}" data-busy-label="Assigning…">${icon('user')}<span>Assign to me</span></button></form>`;

  function context(item) {
    const facts = [['Reference', item.ref, true], item.subjectFact, item.secondFact, ['Owner', ownerLabel(item)], ['Due', item.due], ['Received', item.receivedText]];
    const tone = item.group === 'overdue' ? chip(item.due, 'red') : item.group === 'today' ? chip(item.due, 'amber') : '';
    const liveDismiss = D === 'live' ? dismissControl(item.record, item.ref, 'text-full') : '';
    return `<div class="wc-context" data-wc-context="${item.id}"><div class="wc-context-kind"><span class="eyebrow">${esc(kindName(item.kind))}</span>${tone}</div><h3>${esc(item.title)}</h3><dl class="fact-grid wc-facts">${facts.map(([label, value, mono]) => `<div class="fact"><dt>${esc(label)}</dt><dd${mono ? ' class="mono"' : ''}>${esc(value || 'Not recorded')}</dd></div>`).join('')}</dl><div class="wc-context-actions" data-wc-actions>${actionControl(item)}${completeControl(item)}${canTake(item) ? takeControl(item) : ''}${liveDismiss}</div></div>`;
  }

  const jobNote = (job) => job.state === 'Taken' ? (opt.taken === 'lease' ? `Lease expires ${job.taken}` : `Taken until ${job.taken}`) : job.state === 'Failed' ? (job.reason || 'No reason recorded') : '';
  function jobActions(job, size = ' btn--small') {
    const parts = [];
    if (job.action) parts.push(destination(job.action, job.route, `btn${size} btn--dark`));
    if (job.canComplete) parts.push(`<form data-wc-complete><button type="button" class="btn${size}" data-complete="${job.id}" data-busy-label="Completing…">Complete job</button></form>`);
    if (job.state === 'Failed' && job.ref.startsWith('QDOS')) parts.push(destination('Open Case', `/Cases/demo-${job.ref}`, `btn${size}`));
    return parts.join('');
  }

  const sectionHead = (section, title, meta, headCls = 'wc-section-head') => `<div class="${headCls}"><h2 id="wc-${section}-title" data-focus="head-${section}" tabindex="-1">${esc(title)}</h2>${opt.clock === 'section' ? `<span class="meta wc-freshness" data-wc-freshness>Updated ${state.updated}</span>` : ''}${meta ? `<span class="meta">${meta}</span>` : ''}</div>`;
  const panelHead = (section, title, count, extra = '') => `<div class="wc-panel-head"><h2 id="wc-${section}-title" data-focus="head-${section}" tabindex="-1">${esc(title)}</h2>${count != null ? `<span class="tab-count">${count}</span>` : ''}${opt.clock === 'section' ? `<span class="meta wc-freshness" data-wc-freshness>Updated ${state.updated}</span>` : ''}${extra}</div>`;
  const notice = (text) => `<div class="notice notice--warning" role="status">${icon('alert-triangle')}<span>${esc(text)}</span></div>`;
  const none = (text, attr = '') => `<div class="wc-none"${attr}>${esc(text)}</div>`;
  const paging = (order) => `<div class="pagination" data-wc-paging><span>Page 1 of 1 · ${order}</span><div class="button-row"></div></div>`;

  /* ---------- Header, metrics, activity ---------- */
  function header() {
    const outcome = state.busy ? 'Refreshing' : `Updated ${state.updated}`;
    const refresh = `<div class="refresh-button" data-refresh-region role="status" aria-live="polite"><button type="button" class="btn btn--small" data-refresh data-focus="refresh" title="Refresh">${icon('refresh-cw', 'icon icon--spin')}<span data-refresh-label>${state.busy ? 'Refreshing…' : 'Refresh'}</span></button></div>`;
    return `<header class="page-header"><div class="page-title"><p class="eyebrow">Office-wide work</p><h1>Work Centre</h1></div><div class="page-actions">${D === 'b' ? scopeSwitch() : ''}<div class="wc-refresh"><span class="meta wc-refresh-outcome" data-wc-refresh-outcome-label>${outcome}</span>${refresh}</div>${destination('Create Case', '/Cases/Create', 'btn btn--primary', `${icon('plus')}<span>Create Case</span>`)}</div></header>`;
  }
  const metricLink = ([id, label, value], cls = 'metric') => `<a class="${cls}" data-metric="${id}" data-value="${id}" href="/Cases?tab=${id}" data-destination="${esc(label)}"><span class="metric-label"><span>${esc(label)}</span></span><span class="metric-value">${state.allEmpty ? 0 : value}</span></a>`;
  function metrics() {
    if (state.attentionFailed) return '<div class="wc-metrics-section" data-wc-refresh-section="metrics" data-wc-refresh-state="unavailable"></div>';
    return `<div class="wc-metrics-section" data-wc-refresh-section="metrics" data-wc-refresh-state="current"><nav class="wc-metrics" aria-label="Office queue totals">${cfg.metrics.map((m) => metricLink(m)).join('')}</nav></div>`;
  }
  const figureLabel = (f) => f.goto ? `<a href="#wc-${f.goto}-title" data-goto="${f.goto}">${esc(f.label)}</a>` : esc(f.label);
  const figure = (n) => n == null ? '' : `<strong>${n}</strong>`;
  const activityNotice = () => notice('Activity is unavailable.');
  function activity(variant) {
    if (opt.stats === 'none') return '';
    const v = opt.stats === 'strip' ? 'strip' : variant;
    const stateAttr = `data-wc-activity data-wc-refresh-section="activity" data-wc-refresh-state="${state.statsFailed ? 'unavailable' : 'current'}"`;
    if (v === 'strip') {
      const body = state.statsFailed ? `<div class="panel-body">${activityNotice()}</div>`
        : `<table class="wc-activity-strip-table"><thead><tr><th scope="col"><span class="sr-only">Activity</span></th>${cfg.activity.map((f) => `<th scope="col" data-figure="${f.key}">${figureLabel(f)}</th>`).join('')}</tr></thead><tbody><tr><th scope="row">Today</th>${cfg.activity.map((f) => `<td>${figure(f.today)}</td>`).join('')}</tr><tr><th scope="row">This week</th>${cfg.activity.map((f) => `<td>${figure(f.week)}</td>`).join('')}</tr></tbody></table>`;
      return `<section class="panel wc-activity wc-activity--strip" aria-labelledby="wc-activity-title" ${stateAttr}>${panelHead('activity', 'Activity', null)}${body}</section>`;
    }
    if (v === 'block') {
      const body = state.statsFailed ? activityNotice()
        : `<table><thead><tr><th scope="col"><span class="sr-only">Activity</span></th>${cfg.activity.map((f) => `<th scope="col">${figureLabel(f)}</th>`).join('')}</tr></thead><tbody><tr><td>Today</td>${cfg.activity.map((f) => `<td data-figure="${f.key}-today">${figure(f.today)}</td>`).join('')}</tr><tr><td>This week</td>${cfg.activity.map((f) => `<td data-figure="${f.key}-week">${figure(f.week)}</td>`).join('')}</tr></tbody></table>`;
      return `<div class="wc-activity wc-activity--block" role="group" aria-label="Activity" ${stateAttr}>${body}</div>`;
    }
    return activityTable(cfg.activity, stateAttr, true);
  }
  function activityTable(figures, stateAttr, panel) {
    const body = state.statsFailed ? `<div class="panel-body">${activityNotice()}</div>`
      : `<table class="table wc-activity-table"><thead><tr><th scope="col"><span class="sr-only">Activity</span></th><th scope="col">Today</th><th scope="col">This week</th></tr></thead><tbody>${figures.map((f) => `<tr data-figure="${f.key}"><td>${figureLabel(f)}</td><td>${figure(f.today)}</td><td>${figure(f.week)}</td></tr>`).join('')}</tbody></table>`;
    return panel ? `<section class="panel wc-activity" aria-labelledby="wc-activity-title" ${stateAttr}>${panelHead('activity', 'Activity', null)}${body}</section>` : body;
  }

  /* ---------- Toolbar ---------- */
  const scopeSwitch = () => `<div class="wc-switch" role="group" aria-label="Scope">${['office', 'mine'].map((s) => `<a href="${href({ scope: s })}" data-wc-scope-link="${s}" data-focus="scope-${s}" aria-current="${state.scope === s}">${s === 'mine' ? 'Mine' : 'Office'}</a>`).join('')}</div>`;
  const chips = () => `<nav class="chips wc-filters" aria-label="Kinds">${cfg.kinds.map(([key, label]) => `<a class="chip${state.kinds.includes(key) ? ' on' : ''}" href="${href({ kind: key })}" aria-current="${state.kinds.includes(key)}" data-wc-kind="${key}" data-focus="kind-${key}">${esc(label)}<span class="n">${kindCount(key)}</span></a>`).join('')}${filtered() ? `<a class="chip chip--clear" href="${href({})}" data-wc-clear data-focus="clear">Clear filters</a>` : ''}</nav>`;
  function kindSelect(extra = []) {
    const options = [['', `All kinds (${inScope().length + extra.reduce((n, [, , count]) => n + count, 0)})`], ...cfg.kinds.map(([key, label]) => [key, `${label} (${kindCount(key)})`]), ...extra.map(([key, label, count]) => [key, `${label} (${count})`])];
    return `<label class="wc-kind-select"><span>Kind</span><select data-wc-kind-select data-focus="kind-select">${options.map(([value, label]) => `<option value="${value}"${state.kinds[0] === value || (!value && !state.kinds.length) ? ' selected' : ''}>${esc(label)}</option>`).join('')}</select></label>${filtered() ? `<a class="chip chip--clear" href="${href({})}" data-wc-clear data-focus="clear">Clear filters</a>` : ''}`;
  }
  function toolbar({ withSwitch = true, control = 'chips', count, extraKinds = [], title = 'Needs attention', section = 'attention', withFind = true } = {}) {
    const find = withFind ? `<form class="wc-search" role="search" data-wc-search-form><label class="sr-only" for="wc-search">Find in Needs attention</label>${icon('search')}<input id="wc-search" type="search" name="q" value="${esc(state.search)}" placeholder="Find in this work" autocomplete="off" data-wc-search data-focus="search" /></form>` : '';
    return `<div class="wc-toolbar"><div class="wc-toolbar-left"><h2 id="wc-${section}-title" data-focus="head-${section}" tabindex="-1">${esc(title)}</h2>${count != null ? `<span class="meta" data-wc-count>${count} ${count === 1 ? 'item' : 'items'}</span>` : ''}${opt.clock === 'section' ? `<span class="meta wc-freshness" data-wc-freshness>Updated ${state.updated}</span>` : ''}${withSwitch ? scopeSwitch() : ''}${control === 'select' ? kindSelect(extraKinds) : ''}</div>${find}</div>${control === 'chips' ? chips() : ''}`;
  }

  /* ---------- Rows ---------- */
  const rowAttrs = (item, open) => `data-wc-row="${item.id}" data-wc-row-kind="${item.kind}" data-wc-record="${esc(item.record)}" data-wc-ref="${esc(item.ref)}" aria-selected="${open}"`;
  function attentionRow(item, v) {
    const open = state.selected === item.id;
    const task = `<td class="wc-col-task"><a class="wc-task-link" href="${href({ selected: open ? '' : item.id })}" data-select="${item.id}" data-focus="row-${item.id}" aria-expanded="${open}" aria-controls="wc-detail-${item.id}">${esc(item.title)}</a><small>${esc(kindName(item.kind))}</small>${v !== 'live' ? `<span class="wc-fold">${esc(ownerLabel(item))} · ${esc(item.received)}</span>` : ''}</td>`;
    const record = `<td class="wc-col-record"><span class="mono">${esc(item.ref)}</span>${item.subject ? `<small>${esc(item.subject)}</small>` : ''}</td>`;
    const owner = `<td class="wc-col-owner">${esc(ownerLabel(item))}</td>`;
    const due = `<span class="wc-due wc-due--${item.group}">${esc(item.due)}</span>`;
    const dueCell = v === 'c' ? `<td class="wc-col-due"><span class="wc-due-received">${due}<small>${esc(item.received)}</small></span></td>` : `<td class="wc-col-due">${due}</td>`;
    const received = v === 'c' ? '' : `<td class="wc-col-received">${esc(item.received)}</td>`;
    const action = v === 'a' || v === 'd' ? `<td class="wc-col-action">${actionControl(item, ' btn--small')}</td>` : '';
    const dismiss = v === 'live' ? '' : `<td class="wc-col-dismiss">${dismissControl(item.record, item.ref)}</td>`;
    const span = colspan(v);
    return `<tr ${rowAttrs(item, open)}>${task}${record}${owner}${dueCell}${received}${action}${dismiss}</tr>${open ? `<tr class="wc-inline-detail" id="wc-detail-${item.id}" data-wc-detail><td colspan="${span}">${context(item)}</td></tr>` : ''}`;
  }
  const colspan = (v) => ({ a: 7, d: 7, c: 5, b: 6, live: 5 })[v];
  function ledgerHead(v) {
    const cells = [['wc-col-task', 'Next action'], ['wc-col-record', 'Record / detail'], ['wc-col-owner', 'Owner'], ['wc-col-due', 'Due']];
    if (v !== 'c') cells.push(['wc-col-received', 'Received']);
    if (v === 'a' || v === 'd') cells.push(['wc-col-action', null, 'Action']);
    if (v !== 'live') cells.push(['wc-col-dismiss', null, 'Dismiss']);
    return `<thead><tr>${cells.map(([cls, label, hidden]) => `<th scope="col" class="${cls}">${label ? esc(label) : `<span class="sr-only">${hidden}</span>`}</th>`).join('')}</tr></thead>`;
  }
  const groupRow = (key, label, span, collapsible) => `<tr class="wc-group-row" data-wc-group="${key}"><td colspan="${span}"><h3>${collapsible ? `<button type="button" data-group-toggle="${key}" data-focus="group-${key}" aria-expanded="${!state.collapsed.includes(key)}">${icon('chevron-down')}<span>${esc(label)}</span></button>` : esc(label)}</h3></td></tr>`;
  function attentionTable(v, rows, extraCls = '') {
    const span = colspan(v);
    const body = cfg.groups.map(([group]) => {
      const inGroup = rows.filter((item) => item.group === group);
      return inGroup.length ? groupRow(group, groupLabel(group, inGroup.length), span, false) + inGroup.map((item) => attentionRow(item, v)).join('') : '';
    }).join('');
    return `<div class="wc-table"><table class="table wc-ledger-table wc-table--fold ${extraCls}">${ledgerHead(v)}<tbody>${body}</tbody></table></div>`;
  }
  function attentionSection(v, toolbarOptions = {}) {
    if (state.attentionFailed) return `<div data-wc-section="attention">${notice('Work Centre is unavailable. Refresh to run the live queues again.')}</div>`;
    const rows = visible();
    const body = rows.length ? attentionTable(v, rows) : none(filtered() ? 'No work matches these filters.' : 'Nothing needs attention', ' data-wc-empty');
    return `<div data-wc-section="attention">${toolbar({ count: rows.length, control: kindControl, ...toolbarOptions })}${body}${paging('earliest due first')}</div>`;
  }

  // New cases.
  const arrivalSub = (row) => [row.reg, row.claimant, row.principal, row.change].filter(Boolean).join(' · ');
  const arrivalAttrs = (row) => `data-wc-arrival="${row.id}" data-wc-record="${esc(row.record)}" data-wc-ref="${esc(row.ref)}"`;
  function newCaseRow(row) {
    return `<div class="wc-new-case" ${arrivalAttrs(row)}><a class="row-button" href="/Cases/demo-${esc(row.ref)}" data-destination="Open Case ${esc(row.ref)}" data-wc-new-case="${row.change ? 'changed' : 'new'}" data-focus="arr-${row.id}"><span class="title">${esc(row.ref)}${row.change ? ' · Changed by automation' : ''}</span><span class="sub">${esc(arrivalSub(row))}</span><span class="side">${chip(row.arrival, row.tone)}<time>${esc(row.time)}</time></span></a>${dismissControl(row.record, row.ref, D === 'live' ? 'icon' : undefined)}</div>`;
  }
  const divider = () => `<div class="wc-divider" data-wc-divider><span>Since you last looked</span></div>`;
  function newCaseList(rows, compact = false, withDivider = true) {
    if (!rows.length) return none('No Case was created in the last 7 days', ' data-wc-empty');
    const anyNewer = rows.some((row) => row.newer);
    let divided = false;
    return `<div class="row-list wc-list${compact ? ' wc-list--compact' : ''}">${rows.map((row) => { let out = ''; if (withDivider && anyNewer && !divided && !row.newer) { divided = true; out += divider(); } return out + newCaseRow(row); }).join('')}</div>`;
  }
  function newCaseTable(rows) {
    if (!rows.length) return none('No Case was created in the last 7 days', ' data-wc-empty');
    const anyNewer = rows.some((row) => row.newer);
    let divided = false;
    const body = rows.map((row) => { let out = ''; if (anyNewer && !divided && !row.newer) { divided = true; out += `<tr class="wc-group-row" data-wc-divider><td colspan="5"><h3>Since you last looked</h3></td></tr>`; } return out + `<tr ${arrivalAttrs(row)}><td class="wc-col-ref"><a class="wc-task-link" href="/Cases/demo-${esc(row.ref)}" data-destination="Open Case ${esc(row.ref)}" data-focus="arr-${row.id}">${esc(row.ref)}</a>${row.change ? '<small>Changed by automation</small>' : ''}</td><td>${esc(arrivalSub(row))}</td><td class="wc-col-arrival">${chip(row.arrival, row.tone)}</td><td class="wc-col-time"><time>${esc(row.time)}</time></td><td class="wc-col-dismiss">${dismissControl(row.record, row.ref)}</td></tr>`; }).join('');
    return `<div class="wc-table"><table class="table wc-ledger-table wc-new-cases-table"><thead><tr><th scope="col" class="wc-col-ref">Case</th><th scope="col">Detail</th><th scope="col" class="wc-col-arrival">Arrival</th><th scope="col" class="wc-col-time">Received</th><th scope="col" class="wc-col-dismiss"><span class="sr-only">Dismiss</span></th></tr></thead><tbody>${body}</tbody></table></div>`;
  }
  const newCasesMeta = () => `Last 7 days · ${state.arrivals.length} ${state.arrivals.length === 1 ? 'row' : 'rows'}`;

  // AI jobs.
  const jobAttrs = (job) => `data-wc-job="${job.id}" data-wc-record="${esc(job.record)}" data-wc-ref="${esc(job.ref)}" data-wc-job-state="${esc(job.state)}"`;
  function jobRow(job) {
    const note = jobNote(job);
    return `<div class="wc-row wc-row--job" ${jobAttrs(job)} tabindex="-1" data-focus="job-${job.id}"><div class="wc-row-main"><span class="title">${esc(job.kind)} ${chip(job.state, job.tone)}</span><small>${esc(job.instruction)}</small><small><span class="mono">${esc(job.ref)}</span> · Started by ${esc(job.by)} · <time>${esc(job.created)}</time></small>${note ? `<small${job.state === 'Failed' ? ' class="wc-failed"' : ''}>${esc(note)}</small>` : ''}</div><div class="wc-row-actions">${jobActions(job)}</div>${dismissControl(job.record, job.ref)}</div>`;
  }
  function jobCard(job) {
    const note = jobNote(job);
    return `<article class="wc-job" ${jobAttrs(job)} tabindex="-1" data-focus="job-${job.id}"><div class="wc-job-top"><strong>${esc(job.kind)}</strong>${chip(job.state, job.tone)}</div><p class="wc-job-instruction">${esc(job.instruction)}</p><div class="wc-job-meta"><span class="mono">${esc(job.ref)}</span><span>· Started by ${esc(job.by)}</span><span>· <time>${esc(job.created)}</time></span></div>${note ? `<p class="wc-job-note${job.state === 'Failed' ? ' wc-failed' : ''}">${esc(note)}</p>` : ''}<div class="wc-job-actions">${jobActions(job)}${dismissControl(job.record, job.ref, 'text')}</div></article>`;
  }
  function jobTable(rows) {
    if (!rows.length) return none('No AI job is waiting', ' data-wc-empty');
    return `<div class="wc-table"><table class="table wc-ledger-table wc-jobs-table"><thead><tr><th scope="col" class="wc-col-job">Job</th><th scope="col" class="wc-col-state">State</th><th scope="col" class="wc-col-instruction">Instruction</th><th scope="col" class="wc-col-record">Record</th><th scope="col" class="wc-col-started">Started</th><th scope="col" class="wc-col-note">Note</th><th scope="col" class="wc-col-action"><span class="sr-only">Action</span></th><th scope="col" class="wc-col-dismiss"><span class="sr-only">Dismiss</span></th></tr></thead><tbody>${rows.map((job) => { const note = jobNote(job); return `<tr ${jobAttrs(job)} tabindex="-1" data-focus="job-${job.id}"><td class="wc-col-job"><strong>${esc(job.kind)}</strong></td><td class="wc-col-state">${chip(job.state, job.tone)}</td><td class="wc-col-instruction">${esc(job.instruction)}</td><td class="wc-col-record"><span class="mono">${esc(job.ref)}</span></td><td class="wc-col-started">${esc(job.by)}<small><time>${esc(job.created)}</time></small></td><td class="wc-col-note${job.state === 'Failed' ? ' wc-failed' : ''}">${esc(note)}</td><td class="wc-col-action"><div class="wc-row-actions">${jobActions(job)}</div></td><td class="wc-col-dismiss">${dismissControl(job.record, job.ref)}</td></tr>`; }).join('')}</tbody></table></div>`;
  }
  const jobsMeta = () => `${state.jobs.filter((job) => job.state === 'Draft ready').length} draft ready · ${state.jobs.filter((job) => job.state === 'Failed').length} failed`;
  const jobsBody = (variant, rows = state.jobs) => state.aiFailed ? notice('AI jobs are unavailable.') : !rows.length ? none('No AI job is waiting', ' data-wc-empty') : variant === 'table' ? jobTable(rows) : variant === 'cards' ? `<div class="wc-jobs">${rows.map(jobCard).join('')}</div>` : `<div class="wc-rows">${rows.map(jobRow).join('')}</div>`;
  const newCasesBody = (variant, rows = state.arrivals) => state.newCasesFailed ? notice('New cases are unavailable.') : variant === 'table' ? newCaseTable(rows) : newCaseList(rows, variant === 'compact');

  /* ---------- Tabs (A and live) ---------- */
  function visibleTabs() {
    const tabs = [];
    if (state.items.length || state.attentionFailed || filtered() || state.scope === 'mine') tabs.push(['attention', 'Needs attention', state.attentionFailed ? '—' : visible().length]);
    if (state.arrivals.length || state.newCasesFailed) tabs.push(['new-cases', 'New cases', state.newCasesFailed ? '—' : state.arrivals.length]);
    if (state.jobs.length || state.aiFailed) tabs.push(['ai-jobs', 'AI jobs', state.aiFailed ? '—' : state.jobs.length]);
    if (!tabs.some(([id]) => id === state.tab)) state.tab = tabs[0]?.[0] ?? 'attention';
    return tabs;
  }
  const tabList = (tabs) => `<div class="tabs wc-tabs" role="tablist" aria-label="Work Centre sections">${tabs.map(([id, label, count]) => `<a class="tab" role="tab" id="wc-tab-${id}" href="${href({ tab: id })}" data-wc-tab-link="${id}" data-focus="tab-${id}" aria-selected="${state.tab === id}" aria-controls="wc-panel-${id}" tabindex="${state.tab === id ? 0 : -1}">${esc(label)}<span class="tab-count">${count}</span></a>`).join('')}</div>`;
  const tabPanel = (id, inner) => `<div role="tabpanel" id="wc-panel-${id}" aria-labelledby="wc-tab-${id}" data-wc-refresh-section="${id}"${state.tab === id ? '' : ' hidden'}>${inner}</div>`;
  const nothingToShow = () => !state.items.length && !state.arrivals.length && !state.jobs.length && !state.attentionFailed && !state.aiFailed && !state.newCasesFailed;
  const nothing = () => `<p class="wc-nothing" role="status" data-wc-nothing>No work to show.</p>`;

  /* ---------- Layouts ---------- */
  const layouts = {
    live() {
      if (nothingToShow()) return header() + metrics() + nothing();
      const tabs = visibleTabs();
      return header() + metrics() + `<section class="panel wc-ledger">${tabList(tabs)}${tabPanel('attention', attentionSection('live'))}${tabPanel('new-cases', `<div data-wc-section="new-cases">${sectionHead('new-cases', 'New cases', newCasesMeta())}${newCasesBody('rows')}${state.arrivals.length ? paging('newest first') : ''}</div>`)}${tabPanel('ai-jobs', `<div data-wc-section="ai-jobs">${sectionHead('ai-jobs', 'AI jobs', state.aiFailed ? '' : jobsMeta())}${jobsBody('cards')}</div>`)}</section>`;
    },
    a() {
      const top = header() + metrics() + activity('strip');
      if (nothingToShow()) return top + nothing();
      const tabs = visibleTabs();
      return top + `<section class="panel wc-ledger">${tabList(tabs)}${tabPanel('attention', attentionSection('a'))}${tabPanel('new-cases', `<div data-wc-section="new-cases">${sectionHead('new-cases', 'New cases', newCasesMeta())}${newCasesBody('table')}${state.arrivals.length ? paging('newest first') : ''}</div>`)}${tabPanel('ai-jobs', `<div data-wc-section="ai-jobs">${sectionHead('ai-jobs', 'AI jobs', state.aiFailed ? '' : jobsMeta())}${jobsBody('table')}</div>`)}</section>`;
    },
    b() {
      const top = header() + metrics();
      const mine = state.scope === 'mine';
      const jobs = mine ? state.jobs.filter((job) => job.by === me) : state.jobs;
      const newer = newerArrivals();
      const rail = `<aside class="wc-rail" aria-label="Activity and arrivals">${activity('table')}${(newer.length || state.newCasesFailed) ? `<section class="panel" data-wc-section="new-cases" data-wc-refresh-section="new-cases">${panelHead('new-cases', 'New since you last looked', state.newCasesFailed ? '—' : newer.length, destination(`New cases (${state.arrivals.length})`, '/?tab=new-cases', 'wc-rail-link'))}${newCasesBody('compact', newer)}</section>` : ''}${(jobs.length || state.aiFailed) ? `<section class="panel" data-wc-section="ai-jobs" data-wc-refresh-section="ai-jobs">${panelHead('ai-jobs', mine ? 'My AI jobs' : 'AI jobs', state.aiFailed ? '—' : jobs.length)}${jobsBody('rows', jobs)}</section>` : ''}</aside>`;
      if (nothingToShow()) return top + nothing() + `<div class="wc-grid"><div></div>${rail}</div>`;
      const ledger = `<section class="panel wc-ledger" data-wc-refresh-section="attention">${attentionSection('b', { withSwitch: false, control: 'select' })}</section>`;
      return top + `<div class="wc-grid">${ledger}${rail}</div>`;
    },
    c() {
      const top = header() + metrics();
      const rail = `<aside class="wc-rail" aria-label="Activity, new cases and AI jobs">${activity('table')}${(state.arrivals.length || state.newCasesFailed) ? `<section class="panel" data-wc-section="new-cases" data-wc-refresh-section="new-cases">${panelHead('new-cases', 'New cases', state.newCasesFailed ? '—' : state.arrivals.length, `<span class="meta">Last 7 days</span>`)}${newCasesBody('compact')}${state.arrivals.length ? paging('newest first') : ''}</section>` : ''}${(state.jobs.length || state.aiFailed) ? `<section class="panel" data-wc-section="ai-jobs" data-wc-refresh-section="ai-jobs">${panelHead('ai-jobs', 'AI jobs', state.aiFailed ? '—' : state.jobs.length, state.aiFailed ? '' : `<span class="meta">${jobsMeta()}</span>`)}${jobsBody('rows')}</section>` : ''}</aside>`;
      if (nothingToShow()) return top + nothing() + `<div class="wc-grid"><div></div>${rail}</div>`;
      const showLedger = state.items.length || state.attentionFailed || filtered() || state.scope === 'mine';
      const ledger = showLedger ? `<section class="panel wc-ledger" data-wc-refresh-section="attention">${attentionSection('c')}</section>` : '<div></div>';
      return top + `<div class="wc-grid">${ledger}${rail}</div>`;
    },
    d() {
      const strip = `<div class="wc-combined">${state.attentionFailed ? '<div></div>' : `<nav class="wc-metrics" aria-label="Office queue totals" data-wc-refresh-section="metrics">${cfg.metrics.map((m) => metricLink(m)).join('')}</nav>`}${activity('block')}</div>`;
      const top = header() + strip;
      if (nothingToShow()) return top + nothing();
      if (state.attentionFailed) return top + `<section class="panel wc-ledger wc-merged" data-wc-section="attention">${notice('Work Centre is unavailable. Refresh to run the live queues again.')}</section>`;
      const only = state.kinds[0];
      const attention = only === 'new-case' || only === 'ai-job' ? [] : visible();
      const newer = (only && only !== 'new-case') ? [] : newerArrivals().filter((row) => !state.search || arrivalSub(row).toLowerCase().includes(state.search.toLowerCase()) || row.ref.toLowerCase().includes(state.search.toLowerCase()));
      const progress = (only && only !== 'ai-job') ? [] : jobsInProgress().filter((job) => !state.search || `${job.ref} ${job.kind} ${job.instruction} ${job.by}`.toLowerCase().includes(state.search.toLowerCase()));
      const span = 7;
      const section = (key, label, rows, render) => rows.length ? groupRow(key, `${label} (${rows.length})`, span, true) + (state.collapsed.includes(key) ? '' : rows.map(render).join('')) : '';
      const arrivalRow = (row) => `<tr ${arrivalAttrs(row)}><td class="wc-col-task"><a class="wc-task-link" href="/Cases/demo-${esc(row.ref)}" data-destination="Open Case ${esc(row.ref)}" data-focus="arr-${row.id}">Open Case</a><small>${row.change ? 'Changed by automation' : 'New case'}</small></td><td class="wc-col-record"><span class="mono">${esc(row.ref)}</span><small>${esc(arrivalSub(row))}</small></td><td class="wc-col-owner"></td><td class="wc-col-due">${chip(row.arrival, row.tone)}</td><td class="wc-col-received"><time>${esc(row.time)}</time></td><td class="wc-col-action"></td><td class="wc-col-dismiss">${dismissControl(row.record, row.ref)}</td></tr>`;
      const jobLedgerRow = (job) => `<tr ${jobAttrs(job)} tabindex="-1" data-focus="job-${job.id}"><td class="wc-col-task"><span class="wc-task-link">${esc(job.kind)}</span><small>${esc(job.state)}</small></td><td class="wc-col-record"><span class="mono">${esc(job.ref)}</span><small>${esc(job.instruction)}</small></td><td class="wc-col-owner">${esc(job.by)}</td><td class="wc-col-due${job.state === 'Failed' ? ' wc-failed' : ''}">${esc(jobNote(job))}</td><td class="wc-col-received"><time>${esc(job.created)}</time></td><td class="wc-col-action"><div class="wc-row-actions">${jobActions(job)}</div></td><td class="wc-col-dismiss">${dismissControl(job.record, job.ref)}</td></tr>`;
      const body = section('new', 'New since you last looked', newer, arrivalRow)
        + cfg.groups.map(([group]) => { const rows = attention.filter((item) => item.group === group); return section(group, groupLabel(group, rows.length).replace(/ \(\d+\)$/, ''), rows, (item) => attentionRow(item, 'd')); }).join('')
        + (state.aiFailed ? groupRow('progress', 'AI jobs in progress (—)', span, false) + `<tr><td colspan="${span}">${notice('AI jobs are unavailable.')}</td></tr>` : section('progress', 'AI jobs in progress', progress, jobLedgerRow));
      const total = attention.length + newer.length + progress.length;
      const table = total || state.aiFailed ? `<div class="wc-table"><table class="table wc-ledger-table wc-table--fold">${ledgerHead('d')}<tbody>${body}</tbody></table></div>` : none(filtered() ? 'No work matches these filters.' : 'Nothing needs attention', ' data-wc-empty');
      return top + `<section class="panel wc-ledger wc-merged" data-wc-section="attention" data-wc-refresh-section="attention">${toolbar({ count: total, control: 'select', extraKinds: [['new-case', 'New case', newerArrivals().length], ['ai-job', 'AI job', jobsInProgress().length]] })}${table}${paging('earliest due first')}</section>`;
    },
    e() {
      const top = header();
      const bar = `<div class="panel wc-lanes-toolbar">${toolbar({ control: 'chips', count: null, title: 'Needs attention' })}</div>`;
      const rows = visible();
      const laneRow = (item) => { const open = state.selected === item.id; return `<div class="wc-row" ${rowAttrs(item, open)} tabindex="-1"><div class="wc-row-main"><span class="title"><a class="wc-task-link" href="${href({ selected: open ? '' : item.id })}" data-select="${item.id}" data-focus="row-${item.id}" aria-expanded="${open}" aria-controls="wc-detail-${item.id}">${esc(item.title)}</a></span><span class="meta-line"><span class="mono">${esc(item.ref)}</span>${item.subject ? `<span>${esc(item.subject)}</span>` : ''}<span>${esc(ownerLabel(item))}</span><span class="wc-due wc-due--${item.group}">${esc(item.due)}</span></span></div>${dismissControl(item.record, item.ref)}</div>${open ? `<div id="wc-detail-${item.id}" data-wc-detail>${context(item)}</div>` : ''}`; };
      const laneBody = (stage) => { if (state.allEmpty) return ''; if (state.attentionFailed) return notice('Work Centre is unavailable. Refresh to run the live queues again.'); const inLane = rows.filter((item) => item.stage === stage); return inLane.length ? `<div class="wc-rows">${inLane.map(laneRow).join('')}</div>` : none(filtered() ? 'No work matches these filters.' : 'Nothing needs attention', ' data-wc-empty'); };
      const laneHead = (key, title, count, figures) => `<div class="wc-lane-head"><div class="wc-lane-title"><h2 id="wc-${key}-title" data-focus="head-${key}" tabindex="-1">${esc(title)}</h2>${count != null ? `<span class="tab-count">${count}</span>` : ''}</div>${figures ? `<div class="wc-lane-figures">${figures}</div>` : ''}</div>`;
      const metricInline = ([id, label, value]) => `<a data-metric="${id}" data-value="${id}" href="/Cases?tab=${id}" data-destination="${esc(label)}">${esc(label)} <strong>${state.allEmpty ? 0 : value}</strong></a>`;
      const act = (key) => cfg.activity.find((f) => f.key === key);
      const figurePair = (f) => state.statsFailed ? '' : `<span data-figure="${f.key}">${esc(f.label)}${f.today != null ? ` · Today <strong>${f.today}</strong>` : ''}${f.week != null ? ` · This week <strong>${f.week}</strong>` : ''}</span>`;
      const waiting = rows.filter((item) => item.stage === 'waiting').length;
      const review = rows.filter((item) => item.stage === 'review').length;
      const progress = jobsInProgress();
      const mtr = (ids) => state.attentionFailed ? '' : cfg.metrics.filter(([id]) => ids.includes(id)).map(metricInline).join('');
      const arrived = `<section class="wc-lane" data-lane="arrived" data-wc-section="new-cases" data-wc-refresh-section="new-cases">${laneHead('new-cases', 'Arrived', state.newCasesFailed ? '—' : state.arrivals.length, opt.stats === 'strip' ? '' : [figurePair(act('new-cases')), figurePair(act('emails'))].join(''))}${state.allEmpty ? '' : newCasesBody('compact')}</section>`;
      const waitingLane = `<section class="wc-lane" data-lane="waiting" data-wc-section="waiting">${laneHead('waiting', 'Waiting', state.attentionFailed ? '—' : waiting, mtr(['not_ready', 'held', 'unidentified', 'triage']))}${laneBody('waiting')}</section>`;
      const reviewLane = `<section class="wc-lane" data-lane="review" data-wc-section="review">${laneHead('review', 'Review', state.attentionFailed ? '—' : review, mtr(['review']))}${laneBody('review')}${(progress.length || state.aiFailed) ? `<div data-wc-section="ai-jobs" data-wc-refresh-section="ai-jobs"><div class="wc-lane-sub" id="wc-ai-jobs-title" data-focus="head-ai-jobs" tabindex="-1">With AI</div>${jobsBody('rows', progress)}</div>` : ''}</section>`;
      const outFigures = cfg.activity.filter((f) => ['sent', 'reports', 'completed'].includes(f.key));
      const out = `<section class="wc-lane" data-lane="out" ${opt.stats === 'strip' ? '' : 'data-wc-activity data-wc-refresh-section="activity"'} data-wc-refresh-state="${state.statsFailed ? 'unavailable' : 'current'}">${laneHead('activity', 'Out', null)}<div class="wc-lane-out">${opt.stats === 'strip' ? none('Shown in the strip above') : state.statsFailed ? `<div class="panel-body">${activityNotice()}</div>` : activityTable(outFigures, '', false)}</div></section>`;
      const strip = opt.stats === 'strip' ? activity('strip') : '';
      if (nothingToShow()) return top + strip + nothing() + `<div class="wc-lanes">${arrived}${waitingLane}${reviewLane}${out}</div>`;
      return top + strip + bar + `<div class="wc-lanes">${arrived}${waitingLane}${reviewLane}${out}</div>`;
    },
  };

  /* ---------- Dialogs ---------- */
  function assignDialog() {
    const item = selectedItem();
    if (!item || item.kind !== 'unassigned') return '';
    return `<div id="wc-assign-dialog" class="dialog-backdrop" data-dialog="wc-assign-dialog"${state.assignOpen ? '' : ' hidden'}><section class="dialog" role="dialog" aria-modal="true" aria-labelledby="wc-assign-title"><div class="dialog-head"><h2 id="wc-assign-title" tabindex="-1">Assign Engineer · ${esc(item.ref)}</h2><button type="button" class="dialog-close" data-dialog-close aria-label="Close dialog">${icon('x')}</button></div><div class="dialog-body stack"><dl class="fact-grid"><div class="fact"><dt>Registration</dt><dd class="mono">${esc(item.registration || 'Not recorded')}</dd></div><div class="fact"><dt>Claimant</dt><dd>${esc(item.claimant || 'Not recorded')}</dd></div><div class="fact"><dt>Principal</dt><dd>${esc(item.principal || 'Not recorded')}</dd></div><div class="fact"><dt>Engineer</dt><dd>${esc(item.owner || 'Not recorded')}</dd></div></dl><form id="wc-assign-form" class="stack" data-assign-form><div class="field"><label class="req" for="wc-assign-engineer">Engineer</label><select id="wc-assign-engineer" name="engineerId" required data-dialog-initial-focus><option value="">Choose an Engineer</option>${cfg.engineers.map((name) => `<option>${esc(name)}</option>`).join('')}</select></div></form></div><div class="dialog-foot"><button type="button" class="btn" data-dialog-take data-busy-label="Assigning…">Assign to me</button><button type="button" class="btn" data-dialog-close>Cancel</button><button type="submit" form="wc-assign-form" class="btn btn--primary" data-busy-label="Assigning…">Assign</button></div></section></div>`;
  }
  const root = document.querySelector('#wc-root');
  const dialogs = document.querySelector('#wc-dialogs');
  const live = document.querySelector('#wc-live');
  let openLayer = null;
  let returnFocus = null;
  const focusables = (layer) => [...layer.querySelectorAll('a[href],button,input,select,textarea,[tabindex="0"],[tabindex="-1"]')].filter((el) => !el.disabled && !el.hidden && el.offsetParent !== null && el.tabIndex >= 0);
  function showLayer(layer, opener) {
    returnFocus = opener ?? document.activeElement;
    openLayer = layer;
    layer.hidden = false;
    (layer.querySelector('[data-dialog-initial-focus]') || layer.querySelector('h2[tabindex]') || focusables(layer)[0])?.focus();
  }
  function closeLayer(restore = true) {
    if (!openLayer) return;
    const layer = openLayer;
    openLayer = null;
    if (layer.id === 'wc-assign-dialog') { state.assignOpen = false; render(); }
    else layer.hidden = true;
    if (restore && returnFocus?.isConnected) returnFocus.focus({ preventScroll: true });
    else if (restore) { const again = returnFocus?.dataset?.focus && root.querySelector(`[data-focus="${returnFocus.dataset.focus}"]`); again?.focus({ preventScroll: true }); }
  }
  function boundary(label, route, opener) {
    const layer = document.querySelector('#wc-boundary');
    layer.querySelector('[data-boundary-label]').textContent = label;
    layer.querySelector('[data-boundary-route]').textContent = route || '—';
    showLayer(layer, opener);
  }
  function openAssign(id, opener) {
    state.selected = id;
    state.assignOpen = true;
    returnFocus = opener;
    render();
  }

  /* ---------- Actions ---------- */
  function assignTo(id, name) {
    const item = state.items.find((row) => row.id === id);
    if (!item) return;
    item.owner = name;
    if (item.kind === 'unassigned') { item.kind = 'review'; item.title = 'Review Case'; item.action = 'Review Case'; item.stage = 'review'; }
    state.assignOpen = false;
    openLayer = null;
    render(`row-${id}`);
    live.textContent = name === me ? 'The Case was assigned to you.' : 'The Case was assigned.';
  }
  function completeJob(id) {
    const job = state.jobs.find((row) => row.id === id);
    if (!job?.canComplete) return;
    state.jobs = state.jobs.filter((row) => row.id !== id);
    state.items = state.items.filter((item) => item.job !== id);
    if (!selectedItem()) state.selected = null;
    render('head-ai-jobs');
  }
  const focusKeyOf = (row) => row.querySelector('[data-focus]')?.dataset.focus ?? row.dataset.focus ?? null;
  function dismissFrom(button) {
    const record = button.dataset.dismiss;
    const row = button.closest('[data-wc-record]');
    let next = row?.nextElementSibling ?? null;
    while (next && (!next.hasAttribute('data-wc-record') || next.dataset.wcRecord === record)) next = next.nextElementSibling;
    const section = row?.closest('[data-wc-section]')?.dataset.wcSection;
    const key = next ? focusKeyOf(next) : section ? `head-${section}` : 'h1';
    button.setAttribute('data-busy', 'true');
    dismissRecord(record);
    render(key);
  }
  async function refresh() {
    if (state.busy) return;
    state.busy = true;
    render('refresh');
    await new Promise((resolve) => setTimeout(resolve, 400));
    state.busy = false;
    state.updated = cfg.clock.next;
    render('refresh');
    live.textContent = `Updated ${state.updated}`;
  }

  /* ---------- Render ---------- */
  function render(focusKey) {
    const search = root.querySelector('[data-wc-search]');
    const caret = search && document.activeElement === search ? search.selectionStart : null;
    root.innerHTML = layouts[D]();
    dialogs.innerHTML = assignDialog();
    root.setAttribute('data-wc-scope', state.scope);
    root.setAttribute('data-wc-tab', state.tab);
    document.querySelectorAll('.utility-freshness span:last-child, .rail-health-line span:last-child').forEach((el) => { el.textContent = `Current · ${state.updated}`; });
    if (state.assignOpen) {
      const layer = dialogs.querySelector('#wc-assign-dialog');
      if (layer) { openLayer = layer; layer.querySelector('[data-dialog-initial-focus]')?.focus({ preventScroll: true }); }
    } else if (focusKey) {
      const target = focusKey === 'h1' ? root.querySelector('h1') : root.querySelector(`[data-focus="${focusKey}"]`);
      if (target && target.offsetParent !== null) { if (!target.hasAttribute('tabindex') && !target.matches('a,button,input,select')) target.setAttribute('tabindex', '-1'); target.focus({ preventScroll: true }); }
      else { const h1 = root.querySelector('h1'); h1?.setAttribute('tabindex', '-1'); h1?.focus({ preventScroll: true }); }
      if (focusKey === 'search' && caret != null) { try { root.querySelector('[data-wc-search]').setSelectionRange(caret, caret); } catch { /* not every input exposes a selection */ } }
    }
    window.workCentreMockup.renders += 1;
  }

  /* ---------- Events ---------- */
  document.addEventListener('click', (event) => {
    const t = event.target.closest('a,button');
    if (!t || t.closest('.wc-mock')) return;
    if (t.matches('[data-wc-scope-link]')) { event.preventDefault(); state.scope = t.dataset.wcScopeLink; try { localStorage.setItem(SCOPE_KEY, state.scope); } catch { /* ignore */ } if (!visible().some((i) => i.id === state.selected)) state.selected = null; render(`scope-${state.scope}`); }
    else if (t.matches('[data-wc-kind]')) { event.preventDefault(); const k = t.dataset.wcKind; state.kinds = state.kinds.includes(k) ? state.kinds.filter((x) => x !== k) : [...state.kinds, k]; render(`kind-${k}`); }
    else if (t.matches('[data-wc-clear]')) { event.preventDefault(); state.kinds = []; state.search = ''; render('search'); }
    else if (t.matches('[data-select]')) { event.preventDefault(); const id = t.dataset.select; state.selected = state.selected === id ? null : id; render(`row-${id}`); }
    else if (t.matches('[data-wc-tab-link]')) { event.preventDefault(); state.tab = t.dataset.wcTabLink; render(`tab-${state.tab}`); }
    else if (t.matches('[data-assign]')) { event.preventDefault(); openAssign(t.dataset.assign, t); }
    else if (t.matches('[data-take]')) { assignTo(t.dataset.take, me); }
    else if (t.matches('[data-dialog-take]')) { assignTo(state.selected, me); }
    else if (t.matches('[data-dialog-close]')) { closeLayer(); }
    else if (t.matches('[data-dismiss]')) { dismissFrom(t); }
    else if (t.matches('[data-complete]')) { completeJob(t.dataset.complete); }
    else if (t.matches('[data-group-toggle]')) { const g = t.dataset.groupToggle; state.collapsed = state.collapsed.includes(g) ? state.collapsed.filter((x) => x !== g) : [...state.collapsed, g]; render(`group-${g}`); }
    else if (t.matches('[data-goto]')) { event.preventDefault(); const section = t.dataset.goto; if (D === 'a' || D === 'live') { state.tab = section; render(`head-${section}`); } else { const head = root.querySelector(`[data-focus="head-${section}"]`); head?.scrollIntoView({ block: 'start' }); head?.focus({ preventScroll: true }); } }
    else if (t.matches('[data-refresh]')) { refresh(); }
    else if (t.matches('[data-rail-toggle]')) { const shell = document.querySelector('.app-shell'); const collapsed = shell.classList.toggle('rail-collapsed'); t.setAttribute('aria-expanded', String(!collapsed)); t.setAttribute('aria-label', collapsed ? 'Expand navigation' : 'Collapse navigation'); }
    else if (t.matches('[data-dialog-open]')) { const layer = document.querySelector(`.dialog-backdrop[data-dialog="${t.dataset.dialogOpen}"]`); if (layer) showLayer(layer, t); }
    else if (t.closest('#wc-boundary')) { event.preventDefault(); }
    else if (t.closest('.dialog-backdrop') && !t.closest('#wc-assign-dialog')) { event.preventDefault(); boundary(t.textContent.trim() || t.getAttribute('aria-label'), t.getAttribute('href') || '', t); }
    else if (t.matches('[data-destination]')) { event.preventDefault(); boundary(t.dataset.destination, t.getAttribute('href'), t); }
    else if (t.matches('.utility-bar a, .primary-nav a, .brand')) { event.preventDefault(); boundary(t.getAttribute('aria-label') || t.textContent.trim(), t.getAttribute('href'), t); }
  });
  document.addEventListener('input', (event) => { if (event.target.matches('[data-wc-search]')) { state.search = event.target.value; render('search'); } });
  document.addEventListener('change', (event) => { if (event.target.matches('[data-wc-kind-select]')) { state.kinds = event.target.value ? [event.target.value] : []; render('kind-select'); } });
  document.addEventListener('submit', (event) => {
    if (event.target.matches('[data-wc-search-form]')) { event.preventDefault(); render('search'); return; }
    if (event.target.matches('[data-assign-form]')) {
      event.preventDefault();
      const select = event.target.querySelector('select');
      if (!select.value) { select.setAttribute('aria-invalid', 'true'); select.focus(); return; }
      assignTo(state.selected, select.value);
    }
  });
  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape' && openLayer) { event.preventDefault(); closeLayer(); return; }
    if (event.key === 'Tab' && openLayer) {
      const items = focusables(openLayer);
      if (!items.length) return;
      const first = items[0];
      const last = items[items.length - 1];
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus(); }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
      else if (!openLayer.contains(document.activeElement)) { event.preventDefault(); first.focus(); }
      return;
    }
    const tab = event.target.closest?.('[data-wc-tab-link]');
    if (tab && ['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) {
      event.preventDefault();
      const ids = [...root.querySelectorAll('[data-wc-tab-link]')].map((el) => el.dataset.wcTabLink);
      let index = ids.indexOf(state.tab);
      index = event.key === 'Home' ? 0 : event.key === 'End' ? ids.length - 1 : (index + (event.key === 'ArrowRight' ? 1 : ids.length - 1)) % ids.length;
      state.tab = ids[index];
      render(`tab-${state.tab}`);
    }
    if (event.key === 'F5') { event.preventDefault(); refresh(); }
  });
  document.querySelector('.utility-search')?.addEventListener('submit', (event) => { event.preventDefault(); boundary('Search Pegasus', '/Search', document.activeElement); });

  /* ---------- Shell role and mockup controls ---------- */
  document.querySelectorAll('.rail-user small').forEach((el) => { el.textContent = role; });
  if (role !== 'Administrator') document.querySelectorAll('.primary-nav .nav-link').forEach((el) => { if (el.textContent.includes('Administration')) { if (el.previousElementSibling?.classList.contains('nav-label')) el.previousElementSibling.remove(); el.remove(); } });
  const mock = document.querySelector('.wc-mock');
  if (mock) {
    mock.hidden = params.get('embed') === '1';
    const go = (mutate) => { const u = new URL(location.href); mutate(u.searchParams); location.href = u.toString(); };
    const statePicker = mock.querySelector('[data-state-picker]');
    statePicker.value = presetId;
    statePicker.addEventListener('change', () => go((q) => { q.set('state', statePicker.value); q.delete('selected'); q.delete('scope'); q.delete('tab'); q.delete('assign'); }));
    const rolePicker = mock.querySelector('[data-role-picker]');
    rolePicker.value = role;
    rolePicker.addEventListener('change', () => go((q) => q.set('role', rolePicker.value)));
    mock.querySelectorAll('[data-option-picker]').forEach((select) => {
      select.value = opt[select.dataset.optionPicker];
      select.addEventListener('change', () => { opt[select.dataset.optionPicker] = select.value; render(); });
    });
    const opener = mock.querySelector('[data-dialog-picker]');
    opener?.addEventListener('change', () => {
      const value = opener.value; opener.value = '';
      if (value === 'assign') { const item = state.items.find((row) => row.kind === 'unassigned'); if (item) openAssign(item.id, opener); }
      else if (value) { const layer = document.querySelector(`.dialog-backdrop[data-dialog="${value}"]`); if (layer) showLayer(layer, opener); }
    });
  }

  window.workCentreMockup = { renders: 0, state: () => state, visible: () => visible().map((item) => item.id), options: opt, preset: presetId, role, design: D, dismiss: (record) => { dismissRecord(record); render(); } };
  render();
  if (['new-cases', 'ai-jobs'].includes(presetId) && D !== 'a' && D !== 'live') { const head = root.querySelector(`[data-focus="head-${presetId}"]`); head?.scrollIntoView({ block: 'start' }); }
  window.mockupReady = true;
})();
