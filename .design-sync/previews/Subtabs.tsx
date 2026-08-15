import { Button, Subtabs } from '@pegasus/design-system';

/** Folder pills under an Inbox: the current pill is filled charcoal, counts in muted numerals. */
export const InboxFolders = () => (
  <Subtabs
    label="Folders"
    tabs={[
      { label: 'Needs sorting', href: '#', count: 3, current: true },
      { label: 'Blocked', href: '#', count: 1 },
      { label: 'Filed', href: '#', count: 42 },
    ]}
  />
);

/** Sub-states without counts, and a control pushed to the right. */
export const WithEndSlot = () => (
  <Subtabs
    label="Case stage"
    tabs={[
      { label: 'Not ready', href: '#' },
      { label: 'Review', href: '#', current: true },
      { label: 'Held', href: '#' },
    ]}
    end={<Button icon="file-text">Export list</Button>}
  />
);
