import { AppNav } from '@pegasus/design-system';

const routes = (current: string) =>
  ['Dashboard', 'Inbox', 'Upload', 'Queues', 'Cases', 'Operations'].map((label) => ({
    label,
    href: '#',
    current: label === current,
  }));

/** The staff shell bar: logo + product name, the settled route order, and the user group behind a hairline. Dashboard is current. */
export const StaffSignedIn = () => (
  <AppNav items={routes('Dashboard')} userName="alex.mercer@collisionengineers.co.uk" onSignOut={() => {}} />
);

/** An administrator sees the same routes plus Administration; Cases is the current route. */
export const AdministratorOnCases = () => (
  <AppNav
    items={[...routes('Cases'), { label: 'Administration', href: '#' }]}
    userName="Alex Mercer"
    onSignOut={() => {}}
  />
);

/** Nobody signed in: the user group collapses to a single Sign in link. */
export const SignedOut = () => <AppNav items={routes('Dashboard')} />;

/** The public upload shell: logo only, no product name, links or user menu. */
export const BrandOnly = () => <AppNav items={[]} brandOnly />;
