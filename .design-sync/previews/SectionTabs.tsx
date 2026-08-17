import { SectionTabs } from '@pegasus/design-system';

/** Assessment sections: page-level navigation with the active-route underline. */
export const AssessmentSections = () => (
  <SectionTabs
    label="Assessment sections"
    tabs={[
      { label: 'Vehicle', href: '#', current: true },
      { label: 'Damage', href: '#' },
      { label: 'Valuation', href: '#' },
      { label: 'Repair method', href: '#' },
      { label: 'Report', href: '#' },
    ]}
  />
);

/** Operations sections; a later tab is current. */
export const OperationsSections = () => (
  <SectionTabs
    label="Operations sections"
    tabs={[
      { label: 'Mailboxes', href: '#' },
      { label: 'Uploads', href: '#' },
      { label: 'Automation', href: '#', current: true },
    ]}
  />
);
