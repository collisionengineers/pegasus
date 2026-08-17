import { Crumb } from '@pegasus/design-system';

/** One parent, above a case record. */
export const CaseCrumb = () => <Crumb parents={[{ label: 'Cases', href: '#' }]} current="CE-2026-01432" />;

/** Two levels: a queue and the record reached from it. */
export const QueueThenRecord = () => (
  <Crumb parents={[{ label: 'Queues', href: '#' }, { label: 'Not ready', href: '#' }]} current="CE-2026-01507" />
);

/** Administration: the current item is a person, not a reference. */
export const AdminCrumb = () => (
  <Crumb parents={[{ label: 'Administration', href: '#' }, { label: 'Staff accounts', href: '#' }]} current="J. Okafor" />
);
