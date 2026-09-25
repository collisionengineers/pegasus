// Offline interaction model. All mutations affect synthetic in-memory fixtures.
(() => {
  'use strict';
  const cfg = window.workCentreDesign;
  const params = new URLSearchParams(location.search);
  const preset = cfg.presets.some(([key]) => key === params.get('state')) ? params.get('state') : 'default';
  const key = 'v30.work-centre-designs.scope';
  let remembered = 'office';
  try { remembered = localStorage.getItem(key) || 'office'; } catch { /* file storage may be refused */ }
  const state = {
    scope: params.get('scope') || (preset === 'mine' ? 'mine' : params.has('state') ? 'office' : remembered),
    kinds: preset === 'filtered' ? ['held', 'review'] : (params.get('kinds') || '').split(',').filter(Boolean),
    search: '', tab: preset === 'new-cases' || preset === 'ai-jobs' ? preset : 'attention',
    selected: params.get('selected') || (preset === 'assign' || preset === 'conflict' ? 'r3' : cfg.id === 'a' ? 'r1' : null),
    freshness: ['stale','partial','unavailable'].includes(preset) ? preset : 'current',
    updated: '09:41', busy: false, banner: '', failure: preset === 'conflict',
    items: preset === 'empty' ? [] : structuredClone(cfg.items).filter(item=>preset!=='quiet'||(item.group!=='overdue'&&item.kind!=='ai')),
    arrivals: ['empty','quiet'].includes(preset) ? [] : structuredClone(cfg.arrivals),
    jobs: ['empty','quiet'].includes(preset) ? [] : structuredClone(cfg.jobs),
    empty: preset === 'empty',
  };
  const root = document.querySelector('#wc-design-root');
  const esc = value => String(value ?? '').replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;');
  const icon = name => `<svg class="icon" aria-hidden="true"><use href="#icon-${name}"/></svg>`;
  const kindName = key => cfg.kinds.find(([id]) => id === key)?.[1] || key;
  const canTake = item => !item.owner && ['unassigned','triage'].includes(item.kind);
  const owner = item => item.owner || (item.kind === 'unassigned' ? 'No Engineer' : 'No owner');
  const inScope = () => state.items.filter(item => state.scope === 'office' || item.owner === 'alex' || canTake(item));
  const visible = () => inScope().filter(item => (!state.kinds.length || state.kinds.includes(item.kind)) && (!state.search || [item.ref,item.title,item.subject,item.owner,item.principal,kindName(item.kind)].filter(Boolean).join(' ').toLowerCase().includes(state.search.toLowerCase())));
  const selected = () => state.items.find(item => item.id === state.selected);
  const populatedGroups = () => cfg.groups.filter(([group])=>visible().some(item=>item.group===group));
  const badge = (text, tone = 'neutral') => `<span class="status status--${tone}">${esc(text)}</span>`;
  const destination = (label, route, css='btn btn--dark') => `<a class="${css}" href="${esc(route)}" data-destination="${esc(label)}">${esc(label)}</a>`;
  const notice = (text, tone='warning') => `<div class="wd-notice ${tone}" role="${tone === 'danger' ? 'alert' : 'status'}">${icon(tone === 'success' ? 'check' : 'alert-circle')}<p>${esc(text)}</p></div>`;
  function syncUrl() {
    const q = new URLSearchParams(location.search);
    q.set('scope',state.scope);
    state.kinds.length ? q.set('kinds',state.kinds.join(',')) : q.delete('kinds');
    state.selected ? q.set('selected',state.selected) : q.delete('selected');
    // file:// history changes are not supported consistently across browsers.
    if(location.protocol !== 'file:') history.replaceState(null,'','?'+q);
  }
  function header() {
    const freshness = state.busy ? 'Refreshing…' : state.freshness === 'stale' ? 'Last updated 09:41' : state.freshness === 'partial' ? 'Partially updated 09:41' : state.freshness === 'unavailable' ? 'Attention unavailable' : `Updated ${state.updated}`;
    return `<header class="page-header"><div class="page-title"><p class="eyebrow">Office-wide work</p><h1>Work Centre</h1></div><div class="page-actions"><span class="wd-freshness" role="status">${icon('clock')}${freshness}</span><button class="btn" data-refresh ${state.busy?'aria-disabled="true"':''}>${icon('refresh-cw')}<span>${state.busy?'Refreshing…':'Refresh'}</span></button>${destination('Create Case','/Cases/Create','btn btn--primary')}</div></header>`;
  }
  function metrics() {
    if(state.freshness === 'unavailable') return '';
    return `<nav class="wd-metrics" aria-label="Office queue totals">${cfg.metrics.map(([id,label,value]) => `<a class="wd-metric" href="/Cases?tab=${id}" data-destination="${label} queue"><span class="wd-metric-label">${label}<small>Office queue</small></span><strong class="wd-metric-value">${state.empty?0:value}</strong></a>`).join('')}</nav>`;
  }
  function toolbar() {
    const count = visible().length;
    return `<div class="wd-toolbar"><div class="wd-toolbar-left"><h2>Needs attention</h2><span class="wd-total" data-visible-count>${count} ${count === 1?'item':'items'}</span><div class="wd-segment" role="group" aria-label="Attention scope">${['office','mine'].map(scope => `<button data-scope="${scope}" data-focus="scope-${scope}" aria-pressed="${state.scope === scope}">${scope === 'office'?'Office':'Mine'}</button>`).join('')}</div></div><label class="wd-search">${icon('search')}<span class="sr-only">Find in Needs attention</span><input type="search" data-search data-focus="search" value="${esc(state.search)}" placeholder="Find in this work"></label></div><div class="wd-filters" role="group" aria-label="Work kinds">${cfg.kinds.map(([id,label]) => `<button class="wd-filter" data-kind="${id}" data-focus="kind-${id}" aria-pressed="${state.kinds.includes(id)}">${label}<span class="n">${inScope().filter(i=>i.kind===id).length}</span></button>`).join('')}${state.kinds.length||state.search?'<button class="wd-clear" data-clear>Clear filters</button>':''}</div>`;
  }
  function facts(item) {
    const subjectLabel = {review:'Vehicle',unassigned:'Vehicle',held:'Claimant',triage:'Registration',ai:'Instruction',unidentified:'Source',case:'Missing'}[item.kind];
    const second = item.principal ? ['Principal',item.principal] : item.sender ? ['Sender',item.sender] : item.kind === 'triage' ? ['State','Open'] : item.kind === 'case' ? ['Chase','Awaiting response'] : null;
    const pairs = [[subjectLabel,item.subject],second,['Owner',owner(item)],['Received',item.received+' ago'],item.group==='later'?['Due',item.due]:null].filter(Boolean);
    return `<dl class="wd-facts">${pairs.map(([label,value])=>`<div><dt>${esc(label)}</dt><dd>${esc(value)}</dd></div>`).join('')}</dl>`;
  }
  function context(item) {
    if(!item) return `<div class="wd-inspector-empty">${icon('list-checks')}<p>Select an item to see its details and next action.</p></div>`;
    const action = item.kind === 'unassigned' ? `<button class="btn btn--dark" data-assign="${item.id}" data-focus="assign-${item.id}">${icon('user')}Assign Engineer</button>` : destination(item.action,item.route);
    return `<div class="wd-context" data-detail="${item.id}"><div class="wd-context-kind"><span>${esc(kindName(item.kind))}</span>${item.group === 'overdue' ? badge(item.due,'red') : item.group === 'today' ? badge('Due today','amber') : ''}</div><div><h3>${esc(item.title)}</h3><p class="wd-context-sub mono">${esc(item.ref)}</p></div>${facts(item)}<div class="wd-actions">${action}${canTake(item)?`<button class="btn" data-take="${item.id}" data-focus="take-${item.id}">${icon('user')}Assign to me</button>`:''}</div>${item.reason?`<p class="wd-context-note">${esc(item.reason)}</p>`:''}</div>`;
  }
  function row(item, board=false) {
    const expanded = state.selected === item.id;
    return `<button class="wd-row" data-select="${item.id}" data-focus="row-${item.id}" ${cfg.id==='c'?`aria-haspopup="dialog" aria-expanded="${expanded && document.querySelector('#wd-drawer')?.open}"`:`aria-pressed="${expanded}"`}><span><span class="wd-row-title">${esc(item.title)}${board?icon('chevron-right'):''}</span><span class="wd-row-sub"><span class="mono">${esc(item.ref)}</span> · ${esc(kindName(item.kind))}<br>${esc(item.subject)}</span></span><span class="wd-row-side"><span class="wd-due ${item.group}">${esc(item.due)}</span><span>${esc(owner(item))} · Received ${esc(item.received)} ago</span></span></button>`;
  }
  function emptyList() {
    return `<div class="wd-empty">${state.items.length?'No work matches these filters.':'Nothing needs attention.'}${(state.kinds.length||state.search)?'<br><button class="wd-clear" data-clear>Clear filters</button>':''}</div>`;
  }
  const paging = () => `<div class="wd-paging"><span>Page 1 of 1 · earliest due first</span><span>${visible().length} shown</span></div>`;
  function attentionList() {
    return visible().length ? populatedGroups().map(([group,label])=>`<h3 class="wd-group-label">${label}<strong>${visible().filter(i=>i.group===group).length}</strong></h3>${visible().filter(i=>i.group===group).map(i=>row(i)).join('')}`).join('') : emptyList();
  }
  function arrivalsPanel(wide=false) {
    if(!state.arrivals.length) return '';
    const rows = state.arrivals.map((item,index) => `${index===2?'<div class="wd-divider" data-since-divider>Since you last looked</div>':''}<a class="wd-arrival" href="/Cases/demo-${esc(item.ref)}" data-destination="Open Case ${esc(item.ref)}"><span><strong class="mono">${esc(item.ref)}</strong>${item.change?'<span class="wd-changed">Changed by automation</span>':''}<p>${esc(item.reg)} · ${esc(item.claimant)} · ${esc(item.principal)}${item.change?`<br>${esc(item.change)}`:''}</p></span><span class="wd-arrival-side">${badge(item.source)}<time>${item.time}</time></span></a>`).join('');
    return `<section class="wd-panel ${wide?'wd-ledger-support':''}" id="wd-new-cases" data-section="new-cases"><div class="wd-panel-head"><h2>New cases</h2><span class="meta">Last 7 days · ${state.arrivals.length} rows · ${state.freshness==='stale'?'Last updated':'Updated'} ${state.updated}</span></div><div class="wd-feed-body">${rows||'<div class="wd-empty">No new cases in the last 7 days.</div>'}</div><div class="wd-paging">Page 1 of 1 · newest first</div></section>`;
  }
  function jobsPanel(wide=false) {
    const unavailable = state.freshness === 'partial';
    if(!state.jobs.length && !unavailable) return '';
    const metadata = unavailable ? 'Unavailable' : `${state.jobs.filter(j=>j.state==='Draft ready').length} draft ready · ${state.jobs.filter(j=>j.state==='Failed').length} failed · ${state.freshness==='stale'?'Last updated':'Updated'} ${state.updated}`;
    return `<section class="wd-panel ${wide?'wd-ledger-support':''}" id="wd-ai-jobs" data-section="ai-jobs"><div class="wd-panel-head"><h2>AI jobs</h2><span class="meta">${metadata}</span></div>${unavailable?notice('AI jobs are unavailable. Refresh to try again.'):`<div class="wd-feed-body">${state.jobs.map(job=>`<article class="wd-job" data-job="${job.id}"><div class="wd-job-top"><strong>${esc(job.kind)}</strong>${badge(job.state,job.state==='Failed'?'red':job.state==='Taken'?'blue':'navy')}</div><p>${esc(job.instruction)}</p><div class="wd-job-meta"><span class="mono">${esc(job.ref)}</span><span>· Started by ${esc(job.by)}</span><span>· ${job.created}</span></div>${job.lease?`<p>Lease expires ${job.lease}</p>`:''}${job.reason?`<p class="wd-failure">${esc(job.reason)}</p>`:''}<div class="wd-job-actions">${job.action?destination(job.action,job.route,'btn btn--small btn--dark'):''}${job.canComplete?`<button class="btn btn--small" data-complete="${job.id}">Complete job</button>`:''}</div></article>`).join('')||'<div class="wd-empty">No AI jobs.</div>'}</div>`}</section>`;
  }
  function ledger() {
    if(!visible().length) return emptyList();
    return `<div class="wd-ledger-wrap" tabindex="0" aria-label="Needs attention table"><table class="wd-ledger"><thead><tr><th class="task-col" scope="col">Next action</th><th class="record-col" scope="col">Record / detail</th><th class="owner-col" scope="col">Owner</th><th class="due-col" scope="col">Due</th><th class="received-col" scope="col">Received</th></tr></thead><tbody>${populatedGroups().map(([group,label])=>`<tr class="wd-group-row"><td colspan="5">${label} (${visible().filter(i=>i.group===group).length})</td></tr>${visible().filter(i=>i.group===group).map(item=>`<tr data-selected="${item.id===state.selected}"><td><button class="wd-task-link" data-select="${item.id}" data-focus="row-${item.id}" aria-expanded="${item.id===state.selected}" aria-controls="detail-${item.id}">${esc(item.title)}</button><small>${esc(kindName(item.kind))}</small></td><td><span class="mono">${esc(item.ref)}</span><small>${esc(item.subject)}</small></td><td>${esc(owner(item))}</td><td><span class="wd-due ${item.group}">${esc(item.due)}</span></td><td>${esc(item.received)} ago</td></tr><tr class="wd-inline-detail" id="detail-${item.id}" ${item.id!==state.selected?'hidden':''}><td colspan="5">${item.id===state.selected?context(item):''}</td></tr>`).join('')}`).join('')}</tbody></table></div>${paging()}`;
  }
  function board() {
    if(!visible().length) return emptyList();
    return `<div class="wd-board" style="--wd-lanes:${populatedGroups().length}">${populatedGroups().map(([group,label])=>`<section class="wd-lane" data-group="${group}"><div class="wd-lane-head"><h2>${label}</h2><strong>${visible().filter(i=>i.group===group).length}</strong></div>${visible().filter(i=>i.group===group).map(i=>row(i,true)).join('')}</section>`).join('')}</div><div class="wd-paging wd-board-paging"><span>Page 1 of 1 · earliest due first within each group</span><span>${visible().length} shown</span></div>`;
  }
  function render(focusKey) {
    const oldSearch = document.querySelector('[data-search]');
    const position = oldSearch?.selectionStart;
    const message = state.freshness === 'stale' ? notice('Refresh failed. Showing the last successful read from 09:41. Your filters and selected work have been kept.') : state.freshness === 'partial' ? notice('Partially updated. Needs attention and New cases are current; AI jobs could not be read.') : '';
    const unavailable = notice('Work Centre attention is unavailable. Refresh to run the live queues again.');
    const banner = state.banner ? `<div class="wd-notice success" role="status">${icon('check')}<p>${esc(state.banner)}</p><button class="wd-dismiss" data-dismiss-banner aria-label="Dismiss confirmation">${icon('x')}</button></div>` : '';
    const hasAttention=state.items.length>0;
    let content;
    if(cfg.id === 'a') {
      content = state.freshness==='unavailable' ? unavailable : !hasAttention ? '' : `<div class="wd-grid"><section class="wd-panel" data-section="attention">${toolbar()}<div class="wd-list">${attentionList()}</div>${paging()}</section>${selected()?`<section class="wd-panel" data-section="selected"><div class="wd-panel-head"><h2>Selected work</h2><span class="meta">${selected()?esc(kindName(selected().kind)):''}</span></div>${context(selected())}</section>`:''}</div>`;
      content += `<div class="wd-bottom">${arrivalsPanel()}${jobsPanel()}</div>`;
    } else if(cfg.id === 'b') {
      const tabs = [['attention','Needs attention',state.freshness==='unavailable'?'—':inScope().length],['new-cases','New cases',state.arrivals.length],['ai-jobs','AI jobs',state.freshness==='partial'?'—':state.jobs.length]].filter(([tab])=>tab==='attention'?(hasAttention||state.freshness==='unavailable'):tab==='new-cases'?state.arrivals.length:(state.jobs.length||state.freshness==='partial'));
      if(!tabs.some(([tab])=>tab===state.tab))state.tab=tabs[0]?.[0]||'attention';
      content = !tabs.length ? '' : `<section class="wd-panel"><div class="wd-tabs" role="tablist" aria-label="Work Centre sections">${tabs.map(([id,label,count])=>`<button class="wd-tab" id="tab-${id}" role="tab" data-tab="${id}" data-focus="tab-${id}" tabindex="${state.tab===id?0:-1}" aria-selected="${state.tab===id}" aria-controls="panel-${id}">${label}<span class="n">${count}</span></button>`).join('')}</div>${tabs.map(([id])=>`<div role="tabpanel" id="panel-${id}" aria-labelledby="tab-${id}" ${state.tab!==id?'hidden':''}>${id==='attention'?(state.freshness==='unavailable'?unavailable:toolbar()+ledger()):id==='new-cases'?arrivalsPanel(true):jobsPanel(true)}</div>`).join('')}</section>`;
    } else {
      content = state.freshness==='unavailable' ? unavailable : !hasAttention ? '' : `<section data-section="attention"><div class="wd-panel wd-board-toolbar">${toolbar()}</div>${board()}</section>`;
      content += `<div class="wd-bottom">${arrivalsPanel()}${jobsPanel()}</div>`;
    }
    const nothing = !hasAttention && !state.arrivals.length && !state.jobs.length && !['partial','unavailable'].includes(state.freshness);
    root.innerHTML = header()+message+banner+metrics()+content+(nothing?'<p class="wd-nothing" role="status">No work to show.</p>':'');
    const shellFreshness = state.freshness === 'current' ? 'Current' : state.freshness === 'stale' ? 'Stale' : 'Partial';
    document.querySelectorAll('.utility-freshness,.rail-health-line').forEach(el => { el.innerHTML = `<span class="health-dot${state.freshness==='current'?'':' partial'}"></span><span>${shellFreshness} · ${state.updated}</span>`; });
    if(focusKey) root.querySelector(`[data-focus="${focusKey}"]`)?.focus({preventScroll:true});
    if(focusKey === 'search' && position != null) { try { root.querySelector('[data-search]').setSelectionRange(position,position); } catch { /* search inputs may not expose a selection range */ } }
    document.querySelector('[data-demo-conflict]').checked = state.failure;
    syncUrl();
  }
  function reconcile() {
    if(!visible().some(item=>item.id===state.selected)) state.selected = cfg.id === 'a' ? visible()[0]?.id || null : null;
  }
  let returnFocus = null;
  const drawer = document.querySelector('#wd-drawer');
  const assignment = document.querySelector('#wd-assignment');
  const boundary = document.querySelector('#wd-boundary');
  function openDialog(dialog,opener) {
    returnFocus = opener || document.activeElement;
    dialog.showModal();
    (dialog.querySelector('[data-initial-focus]') || dialog.querySelector('button'))?.focus();
  }
  function closeDialog(dialog) {
    dialog.close();
    if(dialog === drawer) { const itemId=state.selected;state.selected=null;render(`row-${itemId}`); }
    else if(returnFocus?.isConnected) returnFocus.focus({preventScroll:true});
  }
  function openDrawer(item) {
    drawer.querySelector('[data-drawer-body]').innerHTML = context(item);
    openDialog(drawer,document.querySelector(`[data-select="${item.id}"]`));
  }
  function openAssignment(item,opener) {
    assignment.dataset.item=item.id;
    assignment.querySelector('h2').textContent='Assign Engineer · '+item.ref;
    assignment.querySelector('[data-assignment-facts]').innerHTML=`<dl class="wd-facts">${[['Registration',item.subject.split(' · ')[0]],['Claimant',item.claimant||'Not recorded'],['Principal',item.principal||'Not recorded'],['Engineer',item.owner||'Not recorded']].map(([k,v])=>`<div><dt>${k}</dt><dd>${esc(v)}</dd></div>`).join('')}</dl>`;
    assignment.querySelector('form').reset();
    assignment.querySelector('[data-assignment-error]').hidden=true;
    assignment.querySelector('[data-engineer-error]').hidden=true;
    assignment.querySelector('select').removeAttribute('aria-invalid');
    openDialog(assignment,opener);
  }
  function assign(itemId,name) {
    const item=state.items.find(i=>i.id===itemId);
    if(!item || !canTake(item)) return;
    if(state.failure) {
      const message=item.kind==='triage'?'The Triage was not assigned because it changed or the action is not permitted.':'The Case was not assigned because it changed, someone is editing it, or the action is not permitted.';
      if(!assignment.open) openAssignment(item,document.activeElement);
      const error=assignment.querySelector('[data-assignment-error]');error.innerHTML=notice(message,'danger');error.hidden=false;error.focus();return;
    }
    item.owner=name;
    if(item.kind==='unassigned') {item.kind='review';item.title='Review Case';item.action='Review Case';}
    state.banner=`${item.ref} assigned to ${name}.`;
    if(assignment.open) assignment.close();
    if(drawer.open) drawer.close();
    reconcile();render();
    const target=root.querySelector(`[data-select="${itemId}"]`) || root.querySelector('[data-scope]');
    target?.focus({preventScroll:true});
  }
  function showBoundary(label,route,opener) {
    boundary.querySelector('h2').textContent=label;
    boundary.querySelector('[data-boundary-copy]').textContent=label==='Create Case'?'This action opens the Create Case form in Pegasus.':label.includes('queue')?'This count opens its complete Cases queue, keeping the exact queue filter.':label==='Review estimate'?'This action opens the Case’s Repair Spec section to review the estimate draft.':label==='Open query'?'This action opens the message the draft answers, or its Case when there is no message.':label==='Review source'?'This action opens the Unidentified item and its retained source.':'This action opens the named record or application page in Pegasus.';
    boundary.dataset.route=route;
    openDialog(boundary,opener);
  }
  async function refresh() {
    if(state.busy) return;
    if(document.querySelector('dialog[open]')) { document.querySelector('#wd-live').textContent='Refresh deferred while a dialog is open.'; return; }
    const focusKey=document.activeElement?.dataset.focus;
    const scrolls=[...root.querySelectorAll('.wd-list,.wd-feed-body')].map(el=>el.scrollTop);
    state.busy=true;render(focusKey);
    await new Promise(resolve=>setTimeout(resolve,450));
    state.busy=false;state.freshness='current';state.updated='09:42';render(focusKey);
    [...root.querySelectorAll('.wd-list,.wd-feed-body')].forEach((el,i)=>el.scrollTop=scrolls[i]||0);
    document.querySelector('#wd-live').textContent='Work Centre updated. Filters, selection and the Since you last looked divider were retained.';
  }
  document.addEventListener('click',event=>{
    const button=event.target.closest('button,a'); if(!button) return;
    if(button.matches('[data-scope]')) {state.scope=button.dataset.scope;try{localStorage.setItem(key,state.scope);}catch{}reconcile();render(button.dataset.focus);}
    else if(button.matches('[data-kind]')) {const kind=button.dataset.kind;state.kinds=state.kinds.includes(kind)?state.kinds.filter(k=>k!==kind):[...state.kinds,kind];reconcile();render(button.dataset.focus);}
    else if(button.matches('[data-clear]')) {state.kinds=[];state.search='';reconcile();render('search');}
    else if(button.matches('[data-select]')) {const id=button.dataset.select;state.selected=cfg.id==='b'&&state.selected===id?null:id;render(`row-${id}`);if(cfg.id==='c')openDrawer(selected());}
    else if(button.matches('[data-tab]')) {state.tab=button.dataset.tab;render(button.dataset.focus);}
    else if(button.matches('[data-refresh]')) refresh();
    else if(button.matches('[data-assign]')) openAssignment(state.items.find(i=>i.id===button.dataset.assign),button);
    else if(button.matches('[data-take]')) assign(button.dataset.take,'alex');
    else if(button.matches('[data-dialog-take]')) assign(assignment.dataset.item,'alex');
    else if(button.matches('[data-close]')) closeDialog(button.closest('dialog'));
    else if(button.matches('[data-complete]')) {const job=state.jobs.find(j=>j.id===button.dataset.complete);if(job?.canComplete){state.jobs=state.jobs.filter(j=>j.id!==job.id);state.items=state.items.filter(i=>i.job!==job.id);state.banner=`${job.kind} job for ${job.ref} completed.`;reconcile();render();const next=root.querySelector('#wd-ai-jobs h2')||root.querySelector('[role=tab][aria-selected=true]')||root.querySelector('h1');next.setAttribute('tabindex','-1');next.focus({preventScroll:true});}}
    else if(button.matches('[data-dismiss-banner]')) {state.banner='';render();}
    else if(button.matches('[data-destination]')) {event.preventDefault();showBoundary(button.dataset.destination,button.getAttribute('href'),button);}
    else if(button.matches('[data-rail-toggle]')) {const shell=document.querySelector('.app-shell');const collapsed=shell.classList.toggle('rail-collapsed');button.setAttribute('aria-expanded',String(!collapsed));button.setAttribute('aria-label',collapsed?'Expand navigation':'Collapse navigation');try{localStorage.setItem('v30.work-centre-designs.rail',String(collapsed));}catch{}}
    else if(button.matches('[data-dialog-open]')) {const existing=document.querySelector(`[data-dialog="${button.dataset.dialogOpen}"] .dialog`);if(existing){const dialog=document.querySelector('#wd-shell-dialog');dialog.innerHTML=existing.innerHTML;openDialog(dialog,button);}}
    else if(button.matches('[data-dialog-close]')) {const native=button.closest('dialog');if(native)closeDialog(native);}
    else if(button.closest('#wd-shell-dialog')) {event.preventDefault();showBoundary(button.textContent.trim(),'',button);}
    else if(button.matches('.utility-bar a,.primary-nav a,.brand')) {event.preventDefault();const label=button.getAttribute('aria-label')||button.textContent.trim();showBoundary(label,button.getAttribute('href'),button);}
  });
  root.addEventListener('input',event=>{if(event.target.matches('[data-search]')){state.search=event.target.value;reconcile();render('search');}});
  assignment.querySelector('form').addEventListener('submit',event=>{event.preventDefault();const select=assignment.querySelector('select');if(!select.value){assignment.querySelector('[data-engineer-error]').hidden=false;select.setAttribute('aria-invalid','true');select.focus();return;}assign(assignment.dataset.item,select.value);});
  document.querySelector('.utility-search')?.addEventListener('submit',event=>{event.preventDefault();showBoundary('Search Pegasus','/Search',document.activeElement);});
  for(const dialog of document.querySelectorAll('dialog')) {
    dialog.addEventListener('keydown',event=>{
      if(event.key!=='Tab')return;
      const controls=[...dialog.querySelectorAll('button,a[href],input,select,textarea,[tabindex="0"]')].filter(el=>!el.disabled&&el.offsetParent!==null);
      const first=controls[0],last=controls[controls.length-1];
      if(!first)return;
      if(event.shiftKey&&document.activeElement===first){event.preventDefault();last.focus();}
      else if(!event.shiftKey&&document.activeElement===last){event.preventDefault();first.focus();}
    });
    dialog.addEventListener('cancel',event=>{event.preventDefault();closeDialog(dialog);});
    dialog.addEventListener('click',event=>{if(event.target!==dialog)return;const r=dialog.getBoundingClientRect();if(event.clientX<r.left||event.clientX>r.right||event.clientY<r.top||event.clientY>r.bottom)closeDialog(dialog);});
  }
  document.addEventListener('keydown',event=>{
    if(event.key==='F5'){event.preventDefault();refresh();}
    if(event.target.matches('[role=tab]')&&['ArrowLeft','ArrowRight','Home','End'].includes(event.key)){event.preventDefault();const ids=[...root.querySelectorAll('[role=tab]')].map(tab=>tab.dataset.tab);let next=ids.indexOf(state.tab);next=event.key==='Home'?0:event.key==='End'?ids.length-1:(next+(event.key==='ArrowRight'?1:ids.length-1))%ids.length;state.tab=ids[next];render('tab-'+state.tab);}
  });
  const controls=document.querySelector('.wd-mock-controls');controls.hidden=params.get('embed')==='1';
  document.querySelector('[data-state-picker]').value=preset;
  document.querySelector('[data-state-picker]').addEventListener('change',event=>{const q=new URLSearchParams();q.set('state',event.target.value);location.search=q.toString();});
  document.querySelector('[data-demo-conflict]').addEventListener('change',event=>state.failure=event.target.checked);
  const role=['Administrator','Engineer','User'].includes(params.get('role'))?params.get('role'):'Administrator';
  document.querySelector('[data-role-picker]').value=role;
  document.querySelector('[data-role-picker]').addEventListener('change',event=>{params.set('role',event.target.value);location.search=params.toString();});
  document.querySelectorAll('.rail-user small').forEach(el=>el.textContent=role);
  if(role!=='Administrator') document.querySelectorAll('.primary-nav a').forEach(el=>{if(el.textContent.includes('Administration')){el.previousElementSibling?.classList.contains('nav-label')&&el.previousElementSibling.remove();el.remove();}});
  try {if(params.get('embed')!=='1'&&localStorage.getItem('v30.work-centre-designs.rail')==='true')document.querySelector('.app-shell').classList.add('rail-collapsed');}catch{}
  // Work Centre already owns Create Case in its page header (proposal WC6).
  document.querySelector('.utility-actions>a.btn')?.remove();
  reconcile();render();
  if(preset==='assign'||preset==='conflict') {if(cfg.id==='c')openDrawer(selected());openAssignment(selected(),document.querySelector('[data-assign="r3"]'));if(preset==='conflict')assign('r3','alex');}
  else if(cfg.id==='c'&&params.has('selected')&&selected())openDrawer(selected());
  if(cfg.id!=='b'&&['new-cases','ai-jobs'].includes(preset)) document.querySelector(`#wd-${preset}`)?.scrollIntoView({block:'start'});
  window.workCentreDemo = { getState:()=>structuredClone(state), visibleIds:()=>visible().map(i=>i.id), refresh };
  window.mockupReady=true;
})();
