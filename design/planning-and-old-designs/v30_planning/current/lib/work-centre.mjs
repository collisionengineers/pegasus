// Work Centre (Pages/_WorkCentreBody.cshtml at origin/dev, with the Triages
// metric and the one Refresh partial). Baseline is the live markup with a
// synthetic fixture; the proposal layer carries items Q to T.
import { chip, esc, icon, refreshButton } from './shared.mjs';

const metrics = [
  { key: 'not_ready', label: 'Not ready', value: 7 },
  { key: 'review', label: 'Review', value: 4 },
  { key: 'held', label: 'Held', value: 2 },
  { key: 'unidentified', label: 'Unidentified', value: 3 },
  { key: 'triage', label: 'Triages', value: 1 },
];
const kinds = [
  { slug: 'case', label: 'Case', count: 1 },
  { slug: 'held', label: 'Held', count: 2 },
  { slug: 'review', label: 'Review', count: 2 },
  { slug: 'unassigned', label: 'Unassigned', count: 1 },
  { slug: 'unidentified', label: 'Unidentified', count: 1 },
  { slug: 'triage', label: 'Triage', count: 1 },
  { slug: 'ai', label: 'AI draft', count: 1 },
];
// RowTitle / RowDetail ("Kind · reference · subject") / due / owner · received.
const items = [
  { id: 'a1', group: 'overdue', kind: 'review', title: 'Review Case', sub: 'Review · QDOS25984 · MA59BDY Ford Focus', due: '2 days overdue', owner: 'S. Patel', received: 'Received 6 d ago', mine: false },
  { id: 'a2', group: 'overdue', kind: 'held', title: 'Held decision', sub: 'Held · QDOS26001 · J. Morgan', due: '1 day overdue', owner: 'No owner', received: 'Received 9 d ago', mine: true },
  { id: 'a3', group: 'today', kind: 'unassigned', title: 'Assign Engineer', sub: 'Unassigned · QDOS26010 · BH17RZV Vauxhall Corsa', due: 'Due today', owner: 'No Engineer', received: 'Received 1 d ago', mine: true },
  { id: 'a4', group: 'today', kind: 'unidentified', title: 'No usable identification', sub: 'Unidentified · Photos of damage · a.taylor@example.com', due: 'Due today', owner: 'No owner', received: 'Received 2 d ago', mine: true },
  { id: 'a5', group: 'today', kind: 'triage', title: 'Finding required', sub: 'Triage · t.QDOS26011 · MA59BDY', due: 'Due today', owner: 'alex', received: 'Received 3 h ago', mine: true },
  { id: 'a6', group: 'later', kind: 'ai', title: 'Estimate draft ready', sub: 'AI draft · QDOS25990 · Draft the estimate from the images', due: 'Due Fri', owner: 'R. Khan', received: 'Received 1 d ago', mine: false },
  { id: 'a7', group: 'later', kind: 'case', title: 'Missing images', sub: 'Case · QDOS26003', due: 'Due Mon', owner: 'S. Patel', received: 'Received 4 d ago', mine: false },
  { id: 'a8', group: 'later', kind: 'review', title: 'Review Case', sub: 'Review · QDOS26007 · LK21XYZ Nissan Qashqai', due: 'Due 2 Oct', owner: 'R. Khan', received: 'Received 2 d ago', mine: false },
  { id: 'a9', group: 'later', kind: 'held', title: 'Held decision', sub: 'Held · QDOS25971 · A. Taylor', due: 'Due 3 Oct', owner: 'alex', received: 'Received 12 d ago', mine: true },
];
const groups = [
  { key: 'overdue', label: 'Overdue', empty: 'Nothing overdue' },
  { key: 'today', label: 'Due today', empty: 'Nothing due today' },
  { key: 'later', label: 'Later', empty: 'Nothing later' },
];
const newCases = [
  { ref: 'QDOS26012', sub: 'BH17RZV · J. Morgan · Principal A', arrival: 'Manual', time: '25 Sep 09:12' },
  { ref: 'QDOS26010', sub: 'BH17RZV · J. Morgan · Principal A', arrival: 'E-mail', time: '24 Sep 08:52' },
  { divider: true },
  { ref: 'QDOS26009', sub: 'KX68PLM · M. Osei · Principal B', arrival: 'Provider API', time: '23 Sep 14:03' },
  { ref: 'QDOS26008', changed: true, sub: 'RJ19TTR · P. Novak · Principal A · Case data saved', arrival: 'Automation', time: '23 Sep 11:20' },
  { ref: 'QDOS26007', sub: 'LK21XYZ · D. Hughes · Principal C', arrival: 'E-mail', time: '22 Sep 16:47' },
  { ref: 'QDOS26003', sub: 'WR70KLM · A. Taylor · Principal C', arrival: 'Manual', time: '19 Sep 10:05' },
];
const aiJobs = [
  { kind: 'Estimate', instruction: 'Draft the estimate from the images', record: 'QDOS25990', by: 'R. Khan', created: '24 Sep 16:40', state: 'Draft ready', action: '<a class="btn btn--small btn--dark" href="#">Review estimate</a>' },
  { kind: 'Query response', instruction: 'Draft a reply to the repairer’s query', record: 'QDOS26003', by: 'alex', created: '25 Sep 08:10', state: 'Taken', lease: 'Lease expires 25 Sep 09:55', action: '' },
  { kind: 'Unidentified resolution', instruction: 'Identify the vehicle from the photos', record: null, by: 'S. Patel', created: '24 Sep 11:02', state: 'Failed', action: '<span class="wc-failed">No reason recorded</span>' },
];

const metricStrip = () => `<div class="metric-strip metric-strip--5 wc-metrics">${metrics.map((m) => `<a class="metric" data-value="${m.key}" href="#"><span class="metric-label"><span>${m.label}</span></span><span class="metric-value">${m.value}</span></a>`).join('')}</div>`;

function attentionRows(visible, selectedId, state) {
  return groups.map((g) => {
    const rows = visible.filter((i) => i.group === g.key);
    const total = items.filter((i) => i.group === g.key && visible.includes(i)).length;
    return `<div class="wc-group" data-wc-group="${g.key}"><h3>${g.label} (${total})</h3></div>
${rows.length === 0 ? `<div class="wc-none">${g.empty}</div>` : rows.map((item) => `<a class="row-button" href="?state=${state}&amp;selected=${item.id}" data-wc-row="${item.id}" data-wc-row-kind="${item.kind}" aria-current="${item.id === selectedId ? 'true' : 'false'}"><span class="title">${esc(item.title)}</span><span class="sub">${esc(item.sub)}</span><span class="side"><span class="wc-due wc-due--${g.key}">${esc(item.due)}</span><span>${esc(item.owner)} · ${esc(item.received)}</span></span></a>`).join('\n')}`;
  }).join('\n');
}

function today(selected, proposal) {
  if (!selected) {
    return proposal ? '<div class="pane-empty"><p class="muted">Select an item</p></div>' : '<div class="wc-none">Select an item</div>';
  }
  return `<div class="wc-today-head"><div><p class="eyebrow">Unassigned · QDOS26010</p><h3>Assign Engineer</h3></div>${chip('Today', 'amber')}</div>
<dl class="fact-grid wc-facts">
  <div class="fact"><dt>Vehicle</dt><dd>BH17RZV Vauxhall Corsa</dd></div>
  <div class="fact"><dt>Principal</dt><dd>Principal A</dd></div>
  <div class="fact"><dt>Reference</dt><dd class="mono">QDOS26010</dd></div>
  <div class="fact"><dt>Owner</dt><dd>No Engineer</dd></div>
  <div class="fact"><dt>Due</dt><dd>Due today</dd></div>
  <div class="fact"><dt>Received</dt><dd>Received 1 d ago</dd></div>
</dl>
<div class="wc-today-actions" data-wc-actions>
  <a class="btn btn--dark" href="?state=assign" data-wc-dialog="wc-assign-dialog">${icon('arrow-right')}<span>Assign Engineer</span></a>
  <form method="post" data-wc-take onsubmit="return false"><button type="submit" class="btn">${icon('user')}<span>Assign to me</span></button></form>
</div>`;
}

const assignDialog = (open) => `<div id="wc-assign-dialog" class="dialog-backdrop" data-dialog="wc-assign-dialog"${open ? ' data-dialog-open-on-load="true"' : ' hidden'}>
  <section class="dialog" role="dialog" aria-modal="true" aria-labelledby="wc-assign-title">
    <div class="dialog-head"><h2 id="wc-assign-title" tabindex="-1">Assign Engineer · QDOS26010</h2><button type="button" class="dialog-close" data-dialog-close aria-label="Close dialog">${icon('x')}</button></div>
    <div class="dialog-body stack">
      <dl class="fact-grid"><div class="fact"><dt>Registration</dt><dd class="mono">BH17RZV</dd></div><div class="fact"><dt>Claimant</dt><dd>J. Morgan</dd></div><div class="fact"><dt>Principal</dt><dd>Principal A</dd></div><div class="fact"><dt>Engineer</dt><dd>Not recorded</dd></div></dl>
      <form method="post" id="wc-assign-form" class="stack" onsubmit="return false"><div class="field"><label class="req" for="wc-assign-engineer">Engineer</label><select id="wc-assign-engineer" name="engineerId" required data-dialog-initial-focus><option value="">Choose an Engineer</option><option>R. Khan</option><option>S. Patel</option><option>priya</option></select></div></form>
    </div>
    <div class="dialog-foot"><button type="submit" form="wc-assign-form" class="btn">Assign to me</button><button type="button" class="btn" data-dialog-close>Cancel</button><button type="submit" form="wc-assign-form" class="btn btn--primary">Assign</button></div>
  </section>
</div>`;

export const workCentre = {
  key: 'work-centre',
  file: 'pegasus_work_centre_v30.html',
  title: 'Work Centre',
  frame: 'shell',
  route: 'work-centre',
  states: [
    { id: 'default', label: 'Office, Unassigned item selected', expect: ['.metric-strip--5', '.row-button[aria-current="true"]', '.wc-facts'] },
    { id: 'mine', label: 'Mine scope', expect: ['.wc-switch a[aria-current="true"][href*="mine"]'] },
    { id: 'filtered', label: 'Held and Review chips on', expect: ['.chip.on', '.chip--clear'] },
    { id: 'empty', label: 'Nothing needs attention', expect: ['[data-wc-empty]'] },
    { id: 'unavailable', label: 'Attention unavailable', expect: ['[data-wc-refresh-state="unavailable"] .notice--warning'] },
    { id: 'assign', label: 'Assign Engineer dialog', expect: ['#wc-assign-dialog:not([hidden])'] },
  ],
  layers: ['baseline', 'proposal'],
  opts: [{ key: 'metrics', label: 'Metric tiles', values: ['plain', 'toned'], item: 'R' }],
  render(state, layer) {
    const proposal = layer === 'proposal';
    const mine = state === 'mine';
    const filtered = state === 'filtered';
    const empty = state === 'empty';
    const unavailable = state === 'unavailable';
    const activeKinds = filtered ? ['held', 'review'] : [];
    const visible = empty ? [] : items.filter((i) => (!mine || i.mine) && (activeKinds.length === 0 || activeKinds.includes(i.kind)));
    const selectedId = state === 'default' || state === 'assign' ? 'a3' : null;
    const updated = '09:41';
    const header = `<header class="page-header">
  <div class="page-title"><p class="eyebrow">Office-wide work</p><h1>Work Centre</h1></div>
  <div class="page-actions">
    <div class="wc-refresh"><span class="meta wc-refresh-outcome" data-wc-refresh-outcome-label>${proposal && !unavailable ? `Updated ${updated}` : ''}</span>${refreshButton()}</div>
    <a class="btn btn--primary" href="#">${icon('plus')}<span>Create Case</span></a>
  </div>
</header>`;
    const attention = unavailable
      ? `<div class="notice notice--warning" role="status">${icon('alert-triangle')}<span>Work Centre is unavailable. Refresh to run the live queues again.</span></div>`
      : `<section class="metric-section" aria-label="Work requiring attention">${proposal ? '' : `<span class="meta wc-freshness" data-wc-freshness data-wc-freshness-state="current">Updated ${updated}</span>`}${metricStrip()}</section>
<div class="pane-layout pane-layout--2 wc-panes">
  <section class="pane" aria-labelledby="wc-attention-title" data-wc-attention>
    <header class="pane-head"><h2 id="wc-attention-title">Needs attention</h2><span class="meta">${visible.length === 1 ? '1 item' : `${visible.length} items`}</span><div class="wc-switch" role="group" aria-label="Scope"><a href="?state=default" data-wc-scope-link="office" aria-current="${mine ? 'false' : 'true'}">Office</a><a href="?state=mine" data-wc-scope-link="mine" aria-current="${mine ? 'true' : 'false'}">Mine</a></div></header>
    <nav class="chips wc-filters" aria-label="Kinds">${kinds.map((k) => `<a class="chip${activeKinds.includes(k.slug) ? ' on' : ''}" href="?state=${activeKinds.includes(k.slug) ? 'default' : 'filtered'}" aria-current="${activeKinds.includes(k.slug) ? 'true' : 'false'}" data-wc-kind="${k.slug}">${k.label}<span class="n">${empty ? 0 : k.count}</span></a>`).join('')}${activeKinds.length ? '<a class="chip chip--clear" href="?state=default">All kinds</a>' : ''}</nav>
    <div class="row-list pane-scroll wc-list" data-row-list>
${empty ? '<div class="wc-none" data-wc-empty>Nothing needs attention</div>' : attentionRows(visible, selectedId, state)}
    </div>
    <div class="pagination" data-wc-paging><span>Page 1 of 1 · earliest due first</span><div class="button-row"></div></div>
  </section>
  <section class="pane" aria-labelledby="wc-today-title" data-wc-today>
    <header class="pane-head"><h2 id="wc-today-title">Today</h2><span class="meta">Selected work</span></header>
    <div class="pane-body detail-canvas">${today(selectedId ? items.find((i) => i.id === selectedId) : null, proposal)}</div>
  </section>
</div>
${assignDialog(state === 'assign')}`;
    const newCasesBody = empty
      ? '<div class="row-list wc-list"><div class="wc-none">No Case was created in the last 7 days</div></div>'
      : `<div class="row-list wc-list">${newCases.map((row) => row.divider
        ? '<div class="wc-divider" data-wc-divider><span>Since you last looked</span></div>'
        : `<a class="row-button" href="#" data-wc-new-case="${row.changed ? 'changed' : 'new'}"><span class="title">${row.ref}${row.changed ? ' · Changed by automation' : ''}</span><span class="sub">${esc(row.sub)}</span><span class="side">${chip(row.arrival)}<time>${row.time}</time></span></a>`).join('')}</div>
<div class="pagination"><span>Page 1 of 1 · newest first</span><div class="button-row"></div></div>`;
    const aiBody = empty
      ? '<div class="wc-none">No AI job is waiting</div>'
      : `<div class="wc-table"><table class="table"><thead><tr><th scope="col">Job</th><th scope="col">Record</th><th scope="col">Started by</th><th scope="col">Created</th><th scope="col">State</th><th scope="col"><span class="sr-only">Action</span></th></tr></thead><tbody>${aiJobs.map((job) => `<tr data-wc-job-state="${job.state}"><td><strong>${job.kind}</strong><br /><small class="muted">${esc(job.instruction)}</small></td><td class="mono">${job.record ? `<a href="#">${job.record}</a>` : '<span>Unidentified queue</span>'}</td><td>${esc(job.by)}</td><td><time>${job.created}</time></td><td>${chip(job.state)}${job.lease ? `<br /><small class="muted">${job.lease}</small>` : ''}</td><td><div class="row-actions">${job.action}</div></td></tr>`).join('')}</tbody></table></div>`;
    const metaGroup = (parts) => proposal
      ? `<span class="meta p30-head-meta">${parts.join(' · ')}</span>`
      : parts.map((p, i) => `<span class="meta${i === 0 ? ' wc-freshness' : ''}">${p}</span>`).join('');
    const panels = `<section class="panel wc-section" id="wc-new-cases" aria-labelledby="wc-new-cases-title" data-wc-new-cases data-wc-refresh-section="new-cases" data-wc-refresh-state="current">
  <div class="panel-head"><h2 id="wc-new-cases-title">New cases</h2>${metaGroup([`Updated ${updated}`, `Last 7 days · ${empty ? '0 rows' : '6 rows'}`])}</div>
  ${newCasesBody}
</section>
<section class="panel wc-section" id="wc-ai-jobs" aria-labelledby="wc-ai-jobs-title" data-wc-ai-jobs data-wc-refresh-section="ai-jobs" data-wc-refresh-state="current">
  <div class="panel-head"><h2 id="wc-ai-jobs-title">AI jobs</h2>${metaGroup([`Updated ${updated}`, empty ? '0 draft ready · 0 failed' : '1 draft ready · 1 failed'])}</div>
  ${aiBody}
</section>`;
    return `<div class="work-centre" data-work-centre data-wc-scope="${mine ? 'mine' : 'office'}">
${header}
<div data-wc-refresh-section="attention" data-wc-refresh-state="${unavailable ? 'unavailable' : 'current'}">
${attention}
</div>
${panels}
</div>`;
  },
};
