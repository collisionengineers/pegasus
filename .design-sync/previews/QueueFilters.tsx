import { QueueFilters } from '@pegasus/design-system';

/** Filter links above a queue list; the current one carries `aria-current="page"`. */
export const TriageFilters = () => (
  <QueueFilters
    filters={[
      { label: 'All', href: '#', current: true },
      { label: 'Awaiting instruction', href: '#' },
      { label: 'Associated with Case', href: '#' },
    ]}
  />
);

/** The mail workspace: read state and sorting queues, with Unread current. */
export const MailFilters = () => (
  <QueueFilters
    aria-label="Mail filters"
    filters={[
      { label: 'All mail', href: '#' },
      { label: 'Unread', href: '#', current: true },
      { label: 'Needs sorting', href: '#' },
      { label: 'Blocked', href: '#' },
    ]}
  />
);
