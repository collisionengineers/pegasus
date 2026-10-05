// One synthetic office, shared by the five v32 designs and the live baseline.
// Clock: Wednesday 7 October 2026 09:41 Europe/London (a Monday would make
// Today and This week read the same). Read against origin/dev 81b571c36:
// FRD-15 "Work Centre", NeedsAttentionPresentation and OperatorLabels.WorkCentre.
export const clock = { updated: '09:41', next: '09:42', today: 'Wed 7 Oct 2026', weekStart: 'Mon 5 Oct 2026', lastLooked: '07 Oct 2026 08:10' };

export const designs = [
  { id: 'a', name: 'Even ledger', subtitle: 'Today’s page, every list the same table.', description: 'Keeps the five counts and the three tabs. Needs attention, New cases and AI jobs all become the same kind of table, with the next action visible in the row and one Dismiss at the end of every row. The activity figures sit in a strip under the counts.', benefit: 'The smallest change from the page you have now. Nothing moves; everything that was inconsistent becomes consistent.', tradeoff: 'New cases and AI jobs stay one click away behind their tabs.' },
  { id: 'b', name: 'Morning brief', subtitle: 'Your own day first; the office one click away.', description: 'Office / Mine moves into the header and scopes the whole page. Engineers open on Mine, Administrators on Office. The ledger takes the left two thirds; the right column holds the activity table, what is new since you last looked, and the AI jobs you started.', benefit: 'An Engineer sees only what is theirs the moment the page opens, with the office figures beside it.', tradeoff: 'An Administrator switches to Office to see everyone’s work, and the full seven-day New cases list leaves the first screen.' },
  { id: 'c', name: 'Split desk', subtitle: 'No tabs. The whole office on one screen.', description: 'The Needs attention ledger keeps its toolbar, chips and groups on the left. A right-hand rail holds the activity table, New cases and AI jobs as compact rows, each section drawn only when it has something to show.', benefit: 'Everything the office needs is visible at once at 1580 and 1440; nothing is hidden behind a tab.', tradeoff: 'The ledger is narrower, so Due and Received share a cell, and a long attention list pushes New cases down the rail.', recommendation: true },
  { id: 'd', name: 'One list', subtitle: 'All office work in one time-ordered ledger.', description: 'One toolbar (Office / Mine, Find, a Kind dropdown) and one table. New cases since you last looked lead, then Overdue, Due today and Later, then the AI jobs still in progress. The activity figures share the strip with the five counts.', benefit: 'One place to look and one Dismiss rule; the tabs and the chip row both go.', tradeoff: 'An arrival sits in the same list as a task, and paging across three sources is a bigger change behind the page.' },
  { id: 'e', name: 'Flow lanes', subtitle: 'Work by stage, with each stage’s throughput in its head.', description: 'Four lanes: Arrived (new cases), Waiting (chases, holds, images, Unidentified, Triage), Review (Review, Unassigned, AI drafts and the AI jobs in progress) and Out (what left the office today and this week). The five counts move into the lane heads.', benefit: 'The shape of the office’s day is visible at a glance, and the figures sit where the work they count happens.', tradeoff: 'There is no single due order across the page, the lanes are narrow at 1440, and the count strip you placed in September moves.' },
];

export const presets = [
  ['default', 'Office overview'], ['mine', 'Mine'], ['filtered', 'Held + Review'], ['find', 'Find BH17RZV'],
  ['selected', 'Open row'], ['dismissed', 'QDOS26203 dismissed'],
  ['empty-office', 'Nothing needs attention'], ['empty-mine', 'Nothing of mine'],
  ['attention-unavailable', 'Needs attention unavailable'], ['ai-unavailable', 'AI jobs unavailable'], ['stats-unavailable', 'Activity unavailable'],
  ['all-empty', 'No work to show'], ['assign', 'Assign Engineer dialog'],
  ['new-cases', 'New cases'], ['ai-jobs', 'AI jobs'],
];

// Undecided choices, each a strip switch and an opt= key (item letters in v32-notes.md).
export const options = [
  { key: 'dismiss', label: 'Dismiss control (item B)', values: [['icon', 'Icon-only X'], ['text', 'Text "Dismiss"']] },
  { key: 'stats', label: 'Activity placement (item I)', values: [['own', 'This design’s placement'], ['strip', 'One-line strip under the counts']] },
  { key: 'clock', label: 'Updated clock (item J)', values: [['header', 'Header only'], ['section', 'Header and every section (live)']] },
  { key: 'taken', label: 'Taken job note (item K)', values: [['until', '"Taken until HH:MM"'], ['lease', '"Lease expires HH:MM" (live)']] },
];

export const kinds = [['case', 'Case'], ['held', 'Held'], ['review', 'Review'], ['unassigned', 'Unassigned'], ['images', 'Vehicle images paired'], ['unidentified', 'Unidentified'], ['triage', 'Triage'], ['ai', 'AI draft']];
export const groups = [['overdue', 'Overdue'], ['today', 'Due today'], ['later', 'Later']];
export const metrics = [['not_ready', 'Not ready', 7], ['review', 'Review', 4], ['held', 'Held', 2], ['unidentified', 'Unidentified', 3], ['triage', 'Triages', 2]];

// The restored activity figures (operator, 5 October 2026): the original set
// plus Completed this week. A missing half is simply not drawn.
export const activity = [
  { key: 'new-cases', label: 'New cases', today: 3, week: 5, goto: 'new-cases' },
  { key: 'sent', label: 'Sent to Engineer', today: 6, week: 17 },
  { key: 'reports', label: 'Reports sent', today: 3, week: 11 },
  { key: 'completed', label: 'Completed', today: null, week: 9 },
  { key: 'emails', label: 'E-mails received', today: 18, week: null },
];

export const engineers = ['R. Khan', 'S. Patel', 'alex'];

// Needs attention: fourteen rows across all eight kinds and three due groups,
// ordered by due instant (undated last), then received, then reference.
// `record` is the record a dismissal belongs to (FRD-15 "Dismiss").
// `stage` is design E's lane. Titles and facts follow NeedsAttentionPresentation.
export const items = [
  { id: 'r1', group: 'overdue', kind: 'case', title: 'Chase due', ref: 'QDOS26203', record: 'QDOS26203', subject: null, subjectFact: ['Missing', 'Images'], secondFact: ['Chase', 'Chase due'], owner: 'S. Patel', due: '2 days overdue', received: '6 d ago', receivedText: 'Received 6 d ago', action: 'Open Case', route: '/Cases/demo-203', stage: 'waiting' },
  { id: 'r2', group: 'overdue', kind: 'review', title: 'Review Case', ref: 'QDOS26214', record: 'QDOS26214', subject: 'MA59BDY · Ford Focus', subjectFact: ['Vehicle', 'MA59BDY · Ford Focus'], secondFact: ['Principal', 'Principal A'], owner: 'R. Khan', due: '1 day overdue', received: '6 d ago', receivedText: 'Received 6 d ago', action: 'Review Case', route: '/Cases/demo-214', stage: 'review', registration: 'MA59BDY', claimant: 'M. Osei', principal: 'Principal A' },
  { id: 'r3', group: 'overdue', kind: 'unidentified', title: 'No usable identification', ref: 'U142', record: 'U142', subject: 'Photos of damage', subjectFact: ['Source', 'Photos of damage'], secondFact: ['Sender', 'a.taylor@example.com'], owner: null, due: '1 day overdue', received: '3 d ago', receivedText: 'Received 3 d ago', action: 'Review source', route: '/Unidentified/demo-142', stage: 'waiting' },
  { id: 'r4', group: 'overdue', kind: 'held', title: 'Held decision', ref: 'QDOS26201', record: 'QDOS26201', subject: 'J. Morgan', subjectFact: ['Claimant', 'J. Morgan'], secondFact: ['Principal', 'Principal A'], owner: 'alex', due: '1 day overdue', received: '9 d ago', receivedText: 'Received 9 d ago', action: 'Open Case', route: '/Cases/demo-201', stage: 'waiting' },
  { id: 'r5', group: 'today', kind: 'unassigned', title: 'Assign Engineer', ref: 'QDOS26210', record: 'QDOS26210', subject: 'BH17RZV · Vauxhall Corsa', subjectFact: ['Vehicle', 'BH17RZV · Vauxhall Corsa'], secondFact: ['Principal', 'Principal A'], owner: null, due: 'Due today', received: '1 d ago', receivedText: 'Received 1 d ago', action: 'Assign Engineer', route: '/Cases/demo-210', stage: 'review', registration: 'BH17RZV', claimant: 'J. Morgan', principal: 'Principal A' },
  { id: 'r6', group: 'today', kind: 'triage', title: 'Finding required', ref: 't.QDOS26211', record: 't.QDOS26211', subject: 'MJ19XRP', subjectFact: ['Registration', 'MJ19XRP', true], secondFact: ['State', 'Finding required'], owner: null, due: 'Due today', received: 'Today', receivedText: 'Received today', action: 'Open Triage', route: '/Cases/demo-triage-211', stage: 'waiting' },
  { id: 'r7', group: 'today', kind: 'ai', title: 'Query response draft ready', ref: 'QDOS26207', record: 'j4', job: 'j4', subject: 'Draft a reply to the repairer’s query', subjectFact: ['Job', 'Query response'], secondFact: ['Instruction', 'Draft a reply to the repairer’s query'], owner: 'alex', due: 'Due today', received: 'Today', receivedText: 'Received today', action: 'Open query', route: '/Inbox/demo-query-207', stage: 'review' },
  { id: 'r8', group: 'today', kind: 'case', title: 'Chase due', ref: 'QDOS26209', record: 'QDOS26209', subject: null, subjectFact: ['Missing', 'Instruction'], secondFact: ['Chase', 'Chase due'], owner: 'alex', due: 'Due today', received: '2 d ago', receivedText: 'Received 2 d ago', action: 'Open Case', route: '/Cases/demo-209', stage: 'waiting' },
  { id: 'r9', group: 'today', kind: 'review', title: 'Review Case', ref: 'QDOS26212', record: 'QDOS26212', subject: 'BK22XRD · Kia Sportage', subjectFact: ['Vehicle', 'BK22XRD · Kia Sportage'], secondFact: ['Principal', 'Principal B'], owner: 'alex', due: 'Due today', received: 'Today', receivedText: 'Received today', action: 'Review Case', route: '/Cases/demo-212', stage: 'review', registration: 'BK22XRD', claimant: 'L. Evans', principal: 'Principal B' },
  { id: 'r10', group: 'later', kind: 'held', title: 'Held decision', ref: 'QDOS26171', record: 'QDOS26171', subject: 'A. Taylor', subjectFact: ['Claimant', 'A. Taylor'], secondFact: ['Principal', 'Principal B'], owner: 'S. Patel', due: 'Due Fri', received: '12 d ago', receivedText: 'Received 12 d ago', action: 'Open Case', route: '/Cases/demo-171', stage: 'waiting' },
  { id: 'r11', group: 'later', kind: 'triage', title: 'Finding required', ref: 't.QDOS26205', record: 't.QDOS26205', subject: 'KX68PLM', subjectFact: ['Registration', 'KX68PLM', true], secondFact: ['State', 'Finding required'], owner: null, due: 'Due Mon', received: '1 d ago', receivedText: 'Received 1 d ago', action: 'Open Triage', route: '/Cases/demo-triage-205', stage: 'waiting' },
  { id: 'r12', group: 'later', kind: 'unidentified', title: 'Unreadable or corrupt content', ref: 'U145', record: 'U145', subject: 'Estimate PDF', subjectFact: ['Source', 'Estimate PDF'], secondFact: ['Sender', 'repairs@example.co.uk'], owner: null, due: 'Due tomorrow', received: 'Today', receivedText: 'Received today', action: 'Review source', route: '/Unidentified/demo-145', stage: 'waiting' },
  { id: 'r13', group: 'later', kind: 'ai', title: 'Estimate draft ready', ref: 'QDOS26190', record: 'j1', job: 'j1', subject: 'Draft the estimate from the images', subjectFact: ['Job', 'Estimate'], secondFact: ['Instruction', 'Draft the estimate from the images'], owner: 'R. Khan', due: 'Due 14 Oct', received: '1 d ago', receivedText: 'Received 1 d ago', action: 'Review estimate', route: '/Cases/demo-190?section=repair-spec', stage: 'review' },
  { id: 'r14', group: 'later', kind: 'images', title: 'Vehicle images paired', ref: 'QDOS26213', record: 'QDOS26213', subject: 'I26042', subjectFact: ['Image reference', 'I26042', true], secondFact: ['Principal', 'Principal C'], owner: null, due: 'No due date', received: 'Today', receivedText: 'Received today', action: 'Open Case', route: '/Cases/demo-213', stage: 'waiting' },
];

// New cases: every Case except a Triage Case created in the last 7 calendar
// days, newest first, plus a change the Automation actor made. The person
// last looked at 08:10 today, so the divider falls after the second row.
export const arrivals = [
  { id: 'a1', ref: 'QDOS26213', record: 'QDOS26213', reg: 'RJ19TTR', claimant: 'P. Novak', principal: 'Principal C', arrival: 'Principal API', tone: 'navy', time: '07 Oct 2026 08:52', newer: true },
  { id: 'a2', ref: 'QDOS26212', record: 'QDOS26212', reg: 'BK22XRD', claimant: 'L. Evans', principal: 'Principal B', arrival: 'Manual', tone: 'neutral', time: '07 Oct 2026 08:31', newer: true },
  { id: 'a3', ref: 'QDOS26210', record: 'QDOS26210', reg: 'BH17RZV', claimant: 'J. Morgan', principal: 'Principal A', arrival: 'E-mail', tone: 'neutral', time: '07 Oct 2026 07:58' },
  { id: 'a4', ref: 'QDOS26209', record: 'QDOS26209', reg: 'WR70KLM', claimant: 'M. Osei', principal: 'Principal B', arrival: 'Automation', tone: 'blue', time: '06 Oct 2026 14:03', change: 'Case data saved' },
  { id: 'a5', ref: 'QDOS26209', record: 'QDOS26209', reg: 'WR70KLM', claimant: 'M. Osei', principal: 'Principal B', arrival: 'E-mail', tone: 'neutral', time: '05 Oct 2026 11:20' },
  { id: 'a6', ref: 'QDOS26207', record: 'QDOS26207', reg: 'LK21XYZ', claimant: 'D. Hughes', principal: 'Principal C', arrival: 'E-mail', tone: 'neutral', time: '05 Oct 2026 09:05' },
  { id: 'a7', ref: 'QDOS26203', record: 'QDOS26203', reg: 'YT66HGF', claimant: 'A. Taylor', principal: 'Principal C', arrival: 'Manual', tone: 'neutral', time: '01 Oct 2026 10:05' },
];

// AI jobs: Draft ready, then Taken, then Queued, then Failed, newest first
// (FRD-27). A job's `id` is the record its dismissal belongs to.
export const jobs = [
  { id: 'j4', kind: 'Query response', state: 'Draft ready', tone: 'amber', instruction: 'Draft a reply to the repairer’s query', ref: 'QDOS26207', record: 'j4', by: 'alex', created: '07 Oct 2026 07:30', action: 'Open query', route: '/Inbox/demo-query-207', canComplete: true },
  { id: 'j1', kind: 'Estimate', state: 'Draft ready', tone: 'amber', instruction: 'Draft the estimate from the images', ref: 'QDOS26190', record: 'j1', by: 'R. Khan', created: '06 Oct 2026 16:40', action: 'Review estimate', route: '/Cases/demo-190?section=repair-spec' },
  { id: 'j2', kind: 'Query response', state: 'Taken', tone: 'navy', instruction: 'Draft a reply to the Principal’s query', ref: 'QDOS26212', record: 'j2', by: 'alex', created: '07 Oct 2026 09:10', taken: '09:55' },
  { id: 'j5', kind: 'Estimate', state: 'Queued', tone: 'amber', instruction: 'Draft the estimate from the images', ref: 'QDOS26213', record: 'j5', by: 'alex', created: '07 Oct 2026 09:30' },
  { id: 'j3', kind: 'Unidentified resolution', state: 'Failed', tone: 'red', instruction: 'Identify the vehicle from the photos', ref: 'U142', record: 'j3', by: 'S. Patel', created: '06 Oct 2026 11:02', reason: 'Vehicle could not be identified.' },
];

// Labels that do not exist in OperatorLabels.WorkCentre today. Each is a
// plain noun or noun phrase; the notes list them for approval (item C2).
export const newLabels = ['Activity', 'Today', 'This week', 'Sent to Engineer', 'Reports sent', 'Completed', 'E-mails received', 'Activity is unavailable.', 'Kind', 'All kinds', 'New case', 'AI job', 'New since you last looked', 'AI jobs in progress', 'My AI jobs', 'Arrived', 'Waiting', 'Out', 'With AI', 'Taken until', 'Case', 'Detail', 'Arrival', 'Job', 'Instruction', 'Record', 'Started', 'Note', 'Action'];
