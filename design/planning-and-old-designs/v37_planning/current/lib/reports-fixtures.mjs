// One synthetic office, shared by the three v37 designs and the live baseline.
// Clock: Friday 9 October 2026 09:41 Europe/London. Read against origin/dev
// 970ef9f10: FRD-17 "Management Reports", Pages/Administration/Reports.cshtml,
// Core/Reports/CaseListColumns.cs and OperatorLabels. Staff names and figures
// are invented; the Principal codes are the integration tests' own.
export const clock = {
  updated: '09:41',
  from: '2026-09-08T09:41', to: '2026-10-09T09:41',
  fromText: '08 Sep 2026 09:41', toText: '09 Oct 2026 09:41',
  caseFrom: '2026-09-08', caseTo: '2026-10-09',
};

export const designs = [
  {
    id: 'a', name: 'Tidied sections',
    subtitle: 'Today’s four reports, one period bar, narrower tables.',
    description: 'The page keeps its four sections in their order. The period moves out of Engineer activity into one bar under the title that governs every section, and Person sits on the one section it filters. Inspection and Audit become one Work choice per table, so the 10- and 12-column tables drop to four and six. Every section has its own Download CSV in its head; Download workbook moves to the page head.',
    benefit: 'Nothing is lost or moved far: an Administrator who knows today’s page finds every figure where it was. It is the smallest Stage 2.',
    tradeoff: 'Still one long page: Turnaround and the Case list sit below two wide sections, and the headline figures are spread across three tile rows.',
  },
  {
    id: 'b', name: 'Overview first',
    subtitle: 'The period’s headline figures, then one report at a time.',
    description: 'Five tiles give the period at a glance. A tile, or the Report choice beside it, opens one report below: Engineer activity, Reports by Principal, By month, Turnaround or the Case list. The period bar and Download workbook are shared; each report has its own Download CSV.',
    benefit: 'The page fits one screen at 1440; the figure you came for is one click away, and the tiles are the totals every report agrees on.',
    tradeoff: 'Only one report is visible at a time, so comparing Turnaround against Reports by Principal needs two views or the workbook.',
    recommendation: true,
  },
  {
    id: 'c', name: 'Month ledger',
    subtitle: 'Whole months by Principal, for the fee notes and the invoice run.',
    description: 'The period is a range of whole London months. The main table is Principal by month for one chosen measure, with the month totals drawn as navy bars. Choosing a Principal opens its months, its turnaround and its held Cases under the ledger. Engineer activity, Turnaround and the Case list follow below for the same months.',
    benefit: 'Matches how fees are invoiced: whole months, by Principal, with the month-on-month trend visible without a spreadsheet.',
    tradeoff: 'A period that is not whole months (a week, or a fortnight to date) cannot be asked for, and the bars are a new kind of figure for the page (item I).',
  },
];

// Every page state is also ?state=…, so a screenshot needs no click.
export const presets = [
  ['default', 'Populated period'],
  ['empty', 'Nothing in the period'],
  ['person', 'Person r.khan'],
  ['engineer-unavailable', 'Engineer activity unavailable'],
  ['principal-unavailable', 'Reports by Principal unavailable'],
  ['monthly-unavailable', 'By month unavailable'],
  ['invalid', 'From after To'],
  ['preset', 'Case list preset chosen'],
  ['refused', 'Case list refused'],
  ['busy', 'Download running'],
  ['done', 'Download finished'],
  ['proposals', 'Every proposal on'],
];

// Undecided choices, each a strip switch and an opt= key (letters in v37-notes.md).
// The first value is the proposal's; "live" marks today's behaviour.
export const options = [
  { key: 'work', item: 'D', label: 'Inspection and Audit (item D)', values: [['select', 'One Work choice per table'], ['columns', 'Three columns each (live)']] },
  { key: 'notes', item: 'C', label: 'Explanatory notes (item C)', values: [['drop', 'Not drawn'], ['keep', 'Kept (live)']] },
  { key: 'badges', item: 'M', label: 'MI01–MI04 labels (item M)', values: [['drop', 'Not drawn'], ['keep', 'Kept (live)']] },
  { key: 'person', item: 'N', label: 'Person choices (item N)', values: [['active', 'People with activity in the period'], ['all', 'Every enabled account (live)']] },
  { key: 'queues', item: 'E', label: 'Queues now (item E)', values: [['off', 'Off'], ['on', 'On']] },
  { key: 'pipeline', item: 'F', label: 'Cases by stage (item F)', values: [['off', 'Off'], ['on', 'On']] },
  { key: 'compare', item: 'G', label: 'Previous period (item G)', values: [['off', 'Off'], ['on', 'On']] },
  { key: 'periods', item: 'H', label: 'Period choice (item H)', values: [['off', 'From and To only'], ['on', 'Period presets']] },
  { key: 'bars', item: 'I', label: 'Month bars (item I)', values: [['on', 'Drawn'], ['off', 'Not drawn']] },
  { key: 'outcomes', item: 'O', label: 'Outcomes (item O)', values: [['off', 'Off'], ['on', 'On']] },
];

// The Person select: enabled accounts by UserName (live lists every role).
export const people = [
  { id: 'p-alex', name: 'alex', role: 'Administrator' },
  { id: 'p-dward', name: 'd.ward', role: 'User' },
  { id: 'p-jokafor', name: 'j.okafor', role: 'Engineer' },
  { id: 'p-mlewis', name: 'm.lewis', role: 'Engineer' },
  { id: 'p-rkhan', name: 'r.khan', role: 'Engineer' },
  { id: 'p-spatel', name: 's.patel', role: 'Engineer' },
];

// MI-01 for 08 Sep 2026 09:41 – 09 Oct 2026 09:41. Days are ReportTurnaround's words.
export const engineers = [
  { name: 'alex', queries: 3, disputes: 1, amendments: 1, sent: 22, audit: 4, toSent: '6 days' },
  { name: 'j.okafor', queries: 5, disputes: 2, amendments: 1, sent: 31, audit: 6, toSent: '5 days' },
  { name: 'm.lewis', queries: 1, disputes: 0, amendments: 0, sent: 9, audit: 0, toSent: '8 days' },
  { name: 'r.khan', queries: 4, disputes: 1, amendments: 2, sent: 27, audit: 5, toSent: '5 days' },
  { name: 's.patel', queries: 2, disputes: 0, amendments: 1, sent: 18, audit: 3, toSent: '7 days' },
];

// MI-02 for the same period: [Inspection, Audit] pairs; totals are their sums.
// Reports sent counts three Automation sends that MI-01 leaves out (110 vs 107).
export const principals = [
  { code: 'ALPHA', produced: [18, 3], sent: [17, 3], fees: [3240, 540] },
  { code: 'PCH', produced: [12, 2], sent: [13, 2], fees: [2100, 350] },
  { code: 'QDOS', produced: [55, 13], sent: [53, 13], fees: [9900, 1950] },
  { code: 'ROUTE', produced: [9, 0], sent: [9, 0], fees: [1755, 0] },
];

// By month for the same period: the partial October first, then September.
// Fee notes are [Inspection, Audit] too (item D asks for that split).
export const months = [
  { month: 'Oct 2026', code: 'ALPHA', produced: [4, 1], feeNotes: [4, 1], sent: [3, 1], fees: [720, 180] },
  { month: 'Oct 2026', code: 'PCH', produced: [3, 0], feeNotes: [3, 0], sent: [4, 0], fees: [525, 0] },
  { month: 'Oct 2026', code: 'QDOS', produced: [14, 3], feeNotes: [13, 3], sent: [12, 3], fees: [2520, 450] },
  { month: 'Oct 2026', code: 'ROUTE', produced: [2, 0], feeNotes: [2, 0], sent: [2, 0], fees: [390, 0] },
  { month: 'Sep 2026', code: 'ALPHA', produced: [14, 2], feeNotes: [14, 2], sent: [14, 2], fees: [2520, 360] },
  { month: 'Sep 2026', code: 'PCH', produced: [9, 2], feeNotes: [9, 2], sent: [9, 2], fees: [1575, 350] },
  { month: 'Sep 2026', code: 'QDOS', produced: [41, 10], feeNotes: [41, 10], sent: [41, 10], fees: [7380, 1500] },
  { month: 'Sep 2026', code: 'ROUTE', produced: [7, 0], feeNotes: [7, 0], sent: [7, 0], fees: [1365, 0] },
];

// MI-03: held is now, the three averages are the period's.
export const turnaround = [
  { code: 'ALPHA', held: 1, oldestHeld: '02 Oct 2026 14:20', produce: '4 days', ready: '3 days', send: '5 days' },
  { code: 'PCH', held: 0, oldestHeld: null, produce: '6 days', ready: '5 days', send: '7 days' },
  { code: 'QDOS', held: 3, oldestHeld: '21 Sep 2026 10:05', produce: '5 days', ready: '4 days', send: '6 days' },
  { code: 'ROUTE', held: 0, oldestHeld: null, produce: '3 days', ready: '2 days', send: '4 days' },
];

// Item E: Triage figures PrincipalReportActivity already reads, and the
// Unidentified count the Work Centre already shows. Now, not the period.
export const queues = {
  triage: [
    { code: 'ALPHA', open: 0, oldest: null },
    { code: 'PCH', open: 1, oldest: '08 Oct 2026 11:30' },
    { code: 'QDOS', open: 2, oldest: '07 Oct 2026 16:12' },
    { code: 'ROUTE', open: 0, oldest: null },
  ],
  unidentified: 3, unidentifiedOldest: '06 Oct 2026 08:55',
};

// Item F: open Cases by stage now (CaseStageCounts' stages, status chip words).
export const stages = ['Not ready', 'Review', 'With Engineer', 'Held', 'Query'];
export const pipeline = [
  { code: 'ALPHA', counts: [3, 1, 6, 1, 1] },
  { code: 'PCH', counts: [2, 0, 4, 0, 1] },
  { code: 'QDOS', counts: [9, 3, 17, 3, 2] },
  { code: 'ROUTE', counts: [1, 1, 3, 0, 0] },
];

// Item O: the period's reports produced by outcome (CaseReportDeliveryNaming.OutcomeWords)
// and the Audits' agreement with the original (CaseListPolicy.Agrees/Differs).
export const outcomeWords = ['Repairable', 'Total loss', 'Cash in lieu', 'Contract repair'];
export const outcomes = [
  { code: 'ALPHA', counts: [13, 6, 2, 0], agrees: 2, differs: 1 },
  { code: 'PCH', counts: [9, 4, 1, 0], agrees: 2, differs: 0 },
  { code: 'QDOS', counts: [41, 20, 4, 3], agrees: 10, differs: 3 },
  { code: 'ROUTE', counts: [6, 3, 0, 0], agrees: 0, differs: 0 },
];

// Item G: the 31 days before the period.
export const previous = { produced: 104, sent: 101, fees: 18420, queries: 19, engineerSent: 99, audit: 15 };
// Item G for design C: the six whole months before May 2026.
export const previousLedger = { produced: 471, sent: 466, fees: 81345, queries: 66, engineerSent: 571, audit: 82 };

// Design C: whole months, May–October 2026 (October to date), per Principal.
export const ledgerMonths = ['May 2026', 'Jun 2026', 'Jul 2026', 'Aug 2026', 'Sep 2026', 'Oct 2026'];
export const ledger = {
  ALPHA: { produced: [[15, 2], [17, 3], [16, 2], [12, 1], [14, 2], [4, 1]], feeNotes: [[15, 2], [17, 3], [16, 2], [12, 1], [14, 2], [4, 1]], sent: [[14, 2], [18, 3], [15, 2], [13, 1], [14, 2], [3, 1]], fee: [180, 180] },
  PCH: { produced: [[8, 1], [10, 2], [11, 1], [7, 1], [9, 2], [3, 0]], feeNotes: [[8, 1], [10, 2], [11, 1], [7, 1], [9, 2], [3, 0]], sent: [[8, 1], [9, 2], [11, 1], [8, 1], [9, 2], [4, 0]], fee: [175, 175] },
  QDOS: { produced: [[38, 8], [44, 9], [47, 11], [36, 7], [41, 10], [14, 3]], feeNotes: [[38, 8], [44, 9], [46, 11], [36, 7], [41, 10], [13, 3]], sent: [[37, 8], [43, 9], [48, 11], [35, 7], [41, 10], [12, 3]], fee: [180, 150] },
  ROUTE: { produced: [[5, 0], [6, 0], [8, 0], [4, 0], [7, 0], [2, 0]], feeNotes: [[5, 0], [6, 0], [8, 0], [4, 0], [7, 0], [2, 0]], sent: [[5, 0], [6, 0], [7, 0], [5, 0], [7, 0], [2, 0]], fee: [195, 0] },
};
// Engineer activity and turnaround for the six months (design C).
export const engineersLedger = [
  { name: 'alex', queries: 14, disputes: 4, amendments: 5, sent: 121, audit: 19, toSent: '6 days' },
  { name: 'j.okafor', queries: 23, disputes: 8, amendments: 6, sent: 168, audit: 31, toSent: '5 days' },
  { name: 'm.lewis', queries: 6, disputes: 1, amendments: 2, sent: 57, audit: 2, toSent: '8 days' },
  { name: 'r.khan', queries: 19, disputes: 6, amendments: 7, sent: 149, audit: 24, toSent: '5 days' },
  { name: 's.patel', queries: 11, disputes: 3, amendments: 4, sent: 99, audit: 13, toSent: '7 days' },
];
export const turnaroundLedger = [
  { code: 'ALPHA', held: 1, oldestHeld: '02 Oct 2026 14:20', produce: '4 days', ready: '3 days', send: '5 days' },
  { code: 'PCH', held: 0, oldestHeld: null, produce: '5 days', ready: '4 days', send: '6 days' },
  { code: 'QDOS', held: 3, oldestHeld: '21 Sep 2026 10:05', produce: '5 days', ready: '4 days', send: '6 days' },
  { code: 'ROUTE', held: 0, oldestHeld: null, produce: '4 days', ready: '3 days', send: '5 days' },
];

// MI-04's catalogue, in Core order, by group (CaseListColumns.Build).
const sided = (key, title) => [[`${key}.inspection`, `${title} · Inspection`], [`${key}.audit`, `${title} · Audit`]];
export const caseListGroups = [
  ['Case', [['case.reference', 'Case/PO'], ['case.audit_reference', 'Audit reference'], ['case.principal', 'Principal'], ['case.type', 'Case type'], ['case.received', 'Received date'], ['case.stage', 'Stage'], ...sided('sent', 'Report first sent')]],
  ['Outcomes', [...sided('outcome', 'Outcome'), ...sided('salvage_category', 'Salvage category'), ['original.firm', 'Original firm'], ['original.outcome', 'Original outcome'], ['original.agrees', 'Audit agrees with original']]],
  ['Engineers', [['engineer.assigned', 'Assigned engineer'], ['engineer.sign_off', 'Sign-off engineer'], ...sided('sent_by', 'Report sent by')]],
  ['Claim and vehicle', [['claim.number', 'Claim number'], ['claim.claimant_name', 'Claimant name'], ['claim.claimant_contact_number', 'Claimant contact number'], ['claim.claimant_address', 'Claimant address'], ['claim.incident_date', 'Incident date'], ['vehicle.registration', 'Registration'], ['vehicle.make', 'Make'], ['vehicle.model', 'Model'], ['vehicle.year', 'Year'], ['vehicle.mileage', 'Mileage'], ['inspection.date', 'Inspection date']]],
  ['Money', [...sided('agreed_fee', 'Agreed fee'), ...sided('engineers_value', 'Engineer\'s Value'), ...sided('retail_value', 'Retail value'), ...sided('trade_value', 'Trade value'), ...sided('repair_cost', 'Repair cost'), ...sided('salvage_value', 'Salvage value'), ...sided('recovery_charge', 'Recovery charge'), ...sided('storage_charge', 'Storage charge')]],
  ['Parties and activity', [['party.repairer', 'Repairer'], ['party.claim_source', 'Claim source'], ['party.storage', 'Storage'], ['activity.images', 'Images'], ['activity.images_in_report', 'Images in report'], ['activity.documents', 'Documents'], ['activity.queries', 'Queries'], ['activity.disputes', 'Disputes'], ['activity.amendment_requests', 'Amendment requests'], ['activity.emails_sent', 'E-mails sent'], ['activity.chases', 'Chases'], ['activity.open_tasks', 'Open tasks'], ['activity.notes', 'Notes']]],
];
export const caseListPresets = [
  { id: '3f6c1b8e-2d4a-4c1e-9b7a-1a2b3c4d5e6f', name: 'Monthly invoicing', columns: ['case.reference', 'case.principal', 'case.type', 'case.received', 'sent.inspection', 'sent.audit', 'agreed_fee.inspection', 'agreed_fee.audit'] },
  { id: '8a1d2e3f-4b5c-4d6e-8f7a-9b0c1d2e3f4a', name: 'Total losses', columns: ['case.reference', 'case.principal', 'outcome.inspection', 'salvage_category.inspection', 'engineers_value.inspection', 'salvage_value.inspection'] },
];
