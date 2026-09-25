// Synthetic fixtures, ordered by due instant, then received, then reference.
// Read against origin/dev 32dabfc59, FRD-15 and NeedsAttentionPresentation.
export const designs = [
  { id: 'a', name: 'Priority desk', subtitle: 'A queue and a clear next action.', description: 'A compact work list beside a persistent detail pane. Recent arrivals and AI jobs sit below, ready when you need them.', benefit: 'The strongest balance of overview and focused casework. You can inspect each item without losing the queue.', tradeoff: 'The detail pane takes some list width; supporting feeds sit further down the page.', recommendation: true },
  { id: 'b', name: 'Office ledger', subtitle: 'More work, easier to compare.', description: 'A full-width table makes reference, owner and due date easy to compare. Open a row to act in place; tabs switch between attention, arrivals and AI jobs.', benefit: 'Best for scanning a busy office and checking ownership across several records.', tradeoff: 'New cases and AI jobs are one tab away. An expanded row temporarily makes the table taller.' },
  { id: 'c', name: 'Due-date board', subtitle: 'See the shape of the day.', description: 'Three flat lists put Overdue, Due today and Later side by side. A detail drawer opens the selected work while the board stays in place.', benefit: 'Makes the distribution of due work immediately visible, with fewer repeated group headings.', tradeoff: 'Long lists need more vertical scanning. The drawer covers part of the board while you act.' },
];
export const presets = [
  ['default', 'Office overview'], ['mine', 'Mine'], ['filtered', 'Held + Review'],
  ['empty', 'Empty office'], ['stale', 'Refresh failed / last good'],
  ['partial', 'AI jobs unavailable'], ['unavailable', 'Attention unavailable'],
  ['assign', 'Assign Engineer'], ['conflict', 'Assignment conflict'],
  ['new-cases', 'New cases'], ['ai-jobs', 'AI jobs'],
  ['quiet', 'No overdue / empty supporting feeds'],
];
export const kinds = [['case','Case'],['held','Held'],['review','Review'],['unassigned','Unassigned'],['unidentified','Unidentified'],['triage','Triage'],['ai','AI draft']];
export const groups = [['overdue','Overdue'],['today','Due today'],['later','Later']];
export const metrics = [['not_ready','Not ready',7],['review','Review',4],['held','Held',2],['unidentified','Unidentified',3],['triage','Triages',2]];
export const items = [
  { id:'r1', group:'overdue', kind:'review', title:'Review Case', ref:'QDOS26214', subject:'MA59BDY · Ford Focus', principal:'Principal A', claimant:'M. Osei', owner:'S. Patel', due:'2 days overdue', received:'6 d ago', action:'Review Case', route:'/Cases/demo-214' },
  { id:'r2', group:'overdue', kind:'held', title:'Held decision', ref:'QDOS26201', subject:'J. Morgan', principal:'Principal A', owner:null, due:'1 day overdue', received:'9 d ago', action:'Open Case', route:'/Cases/demo-201', reason:'Awaiting a decision from the Principal.' },
  { id:'r3', group:'today', kind:'unassigned', title:'Assign Engineer', ref:'QDOS26210', subject:'BH17RZV · Vauxhall Corsa', principal:'Principal A', claimant:'J. Morgan', owner:null, due:'Due today', received:'1 d ago', action:'Assign Engineer', route:'/Cases/demo-210' },
  { id:'r4', group:'today', kind:'unidentified', title:'No usable identification', ref:'U142', subject:'Photos of damage', sender:'a.taylor@example.com', owner:null, due:'Due today', received:'2 d ago', action:'Review source', route:'/Unidentified/demo-142' },
  { id:'r5', group:'today', kind:'triage', title:'Finding required', ref:'t.QDOS26211', subject:'MJ19XRP', owner:null, due:'Due today', received:'3 h ago', action:'Open Triage', route:'/Cases/demo-triage-211' },
  { id:'r10', group:'today', kind:'ai', title:'Query response ready', ref:'QDOS26203', subject:'Draft a reply to the repairer’s query', owner:'alex', due:'Due today', received:'2 h ago', action:'Open query', route:'/Inbox/demo-query-203', job:'j4' },
  { id:'r6', group:'later', kind:'ai', title:'Estimate draft ready', ref:'QDOS26190', subject:'Draft the estimate from the images', owner:'R. Khan', due:'Due 28 Sep', received:'1 d ago', action:'Review estimate', route:'/Cases/demo-190?section=repair-spec', job:'j1' },
  { id:'r7', group:'later', kind:'case', title:'Missing images', ref:'QDOS26203', subject:'Images', owner:'S. Patel', due:'Due 28 Sep', received:'4 d ago', action:'Open Case', route:'/Cases/demo-203' },
  { id:'r8', group:'later', kind:'review', title:'Review Case', ref:'QDOS26207', subject:'LK21XYZ · Nissan Qashqai', principal:'Principal C', owner:'alex', due:'Due 2 Oct', received:'2 d ago', action:'Review Case', route:'/Cases/demo-207' },
  { id:'r9', group:'later', kind:'held', title:'Held decision', ref:'QDOS26171', subject:'A. Taylor', principal:'Principal B', owner:'alex', due:'Due 3 Oct', received:'12 d ago', action:'Open Case', route:'/Cases/demo-171', reason:'Awaiting further instruction.' },
];
export const arrivals = [
  { ref:'QDOS26212', reg:'BK22XRD', claimant:'L. Evans', principal:'Principal A', source:'Manual', time:'25 Sep 09:12', fresh:true },
  { ref:'QDOS26210', reg:'BH17RZV', claimant:'J. Morgan', principal:'Principal A', source:'E-mail', time:'24 Sep 08:52', fresh:true },
  { ref:'QDOS26209', reg:'KX68PLM', claimant:'M. Osei', principal:'Principal B', source:'Provider API', time:'23 Sep 14:03' },
  { ref:'QDOS26208', reg:'RJ19TTR', claimant:'P. Novak', principal:'Principal A', source:'Automation', time:'23 Sep 11:20', change:'Case data saved' },
  { ref:'QDOS26207', reg:'LK21XYZ', claimant:'D. Hughes', principal:'Principal C', source:'E-mail', time:'22 Sep 16:47' },
  { ref:'QDOS26203', reg:'WR70KLM', claimant:'A. Taylor', principal:'Principal C', source:'Manual', time:'19 Sep 10:05' },
];
export const jobs = [
  { id:'j1', kind:'Estimate', instruction:'Draft the estimate from the images', ref:'QDOS26190', by:'R. Khan', created:'24 Sep 16:40', state:'Draft ready', action:'Review estimate', route:'/Cases/demo-190?section=repair-spec' },
  { id:'j2', kind:'Query response', instruction:'Draft a reply to the repairer’s query', ref:'QDOS26207', by:'alex', created:'25 Sep 08:10', state:'Taken', lease:'25 Sep 09:55' },
  { id:'j3', kind:'Unidentified resolution', instruction:'Identify the vehicle from the photos', ref:'U142', by:'S. Patel', created:'24 Sep 11:02', state:'Failed', reason:'Vehicle could not be identified.' },
  { id:'j4', kind:'Query response', instruction:'Draft a reply to the repairer’s query', ref:'QDOS26203', by:'alex', created:'25 Sep 07:30', state:'Draft ready', action:'Open query', route:'/Inbox/demo-query-203', canComplete:true },
];
