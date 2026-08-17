import { Tabs } from '@pegasus/design-system';

/** Link tabs with counts; Overview is current (red underline, red count pill). */
export const CaseSections = () => (
  <Tabs
    label="Case sections"
    tabs={[
      { label: 'Overview', href: '#', current: true },
      { label: 'Evidence', href: '#', count: 7 },
      { label: 'History', href: '#', count: 12 },
      { label: 'Documents', href: '#', count: 0 },
    ]}
  />
);

/** Button tabs (aria-selected) for sections switched in place; Evidence is selected. */
export const ButtonTabs = () => (
  <Tabs
    label="Triage sections"
    tabs={[
      { label: 'Overview', onClick: () => {} },
      { label: 'Evidence', count: 4, current: true, onClick: () => {} },
      { label: 'History', count: 2, onClick: () => {} },
    ]}
  />
);

/** No counts — plain alternatives. */
export const WithoutCounts = () => (
  <Tabs
    label="Account sections"
    tabs={[
      { label: 'Details', href: '#', current: true },
      { label: 'Roles', href: '#' },
      { label: 'Activity', href: '#' },
    ]}
  />
);
