import { DataTable, StatusChip } from '@pegasus/design-system';

type TriageRow = { registration: string; opened: string; state: string; assignee: string };

const triageRows: TriageRow[] = [
  { registration: 'LM19 KXR', opened: '12 Aug 09:14', state: 'Awaiting information', assignee: 'Unassigned' },
  { registration: 'YD68 TFA', opened: '12 Aug 10:02', state: 'Needs sorting', assignee: 'Unassigned' },
  { registration: 'KP21 WRZ', opened: '13 Aug 08:47', state: 'Draft ready', assignee: 'S. Patel' },
  { registration: 'HN17 QLB', opened: '13 Aug 11:30', state: 'Finding recorded', assignee: 'M. Hughes' },
  { registration: 'BV70 EJM', opened: '14 Aug 08:52', state: 'Blocked', assignee: 'Unassigned' },
];

/** The Queues table: a linked registration, office time, state as a chip, and the assignee — with a hidden caption naming the table. */
export const TriageQueue = () => (
  <div style={{ maxWidth: 960 }}>
    <DataTable<TriageRow>
      caption="Triage work"
      rowKey={(r) => r.registration}
      columns={[
        { header: 'Registration', cell: (r) => <a href="#">{r.registration}</a> },
        { header: 'Opened', cell: (r) => <time>{r.opened}</time> },
        { header: 'State', cell: (r) => <StatusChip state={r.state} /> },
        { header: 'Assigned to', cell: (r) => r.assignee },
      ]}
      rows={triageRows}
    />
  </div>
);

type ItemRow = { line: string; description: string; hours: string; rate: string; total: string };

const items: ItemRow[] = [
  { line: '1', description: 'Front bumper — remove and refit', hours: '1.2', rate: '£42.00', total: '£50.40' },
  { line: '2', description: 'Bonnet — repair and paint', hours: '3.6', rate: '£42.00', total: '£151.20' },
  { line: '3', description: 'N/S headlamp — replace', hours: '0.8', rate: '£42.00', total: '£33.60' },
  { line: '4', description: 'Paint materials', hours: '', rate: '', total: '£96.00' },
];

/** A numeric table: `tabular` columns keep the figures aligned, and a `footer` row carries the calculated total. */
export const NumericWithFooter = () => (
  <div style={{ maxWidth: 720 }}>
    <DataTable<ItemRow>
      caption="Repair estimate lines"
      rowKey={(r) => r.line}
      columns={[
        { header: 'Line', cell: (r) => r.line, tabular: true },
        { header: 'Description', cell: (r) => r.description },
        { header: 'Hours', cell: (r) => r.hours, tabular: true },
        { header: 'Rate', cell: (r) => r.rate, tabular: true },
        { header: 'Total', cell: (r) => r.total, tabular: true },
      ]}
      rows={items}
      footer={['', <b>Estimate total</b>, <b>5.6</b>, '', <b>£331.20</b>]}
    />
  </div>
);

/** No rows: the muted `empty` sentence renders in place of the table, in business language. */
export const Empty = () => (
  <div style={{ maxWidth: 960 }}>
    <DataTable<TriageRow>
      caption="Cases in this queue"
      columns={[{ header: 'Reference', cell: (r) => r.registration }]}
      rows={[]}
      empty="No cases are ready to confirm."
    />
  </div>
);
