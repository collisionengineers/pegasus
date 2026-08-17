import { AppNav, AppShell, PageHeading, Panel, Refresh, SectionLabel } from '@pegasus/design-system';

const routes = ['Dashboard', 'Inbox', 'Upload', 'Queues', 'Cases', 'Operations'].map((label) => ({
  label,
  href: '#',
  current: label === 'Queues',
}));

/** The authenticated frame: skip link, AppNav, main on the paper ground, footer. A screen renders as heading + panel inside main. */
export const AuthenticatedScreen = () => (
  <AppShell nav={<AppNav items={routes} userName="Alex Mercer" onSignOut={() => {}} />}>
    <PageHeading title="Queues" refresh={<Refresh updatedAt="14 Aug 09:32" />} />
    <Panel>
      <SectionLabel>Not ready</SectionLabel>
      <p style={{ margin: 0 }}>7 cases are waiting for information from the instructing principal.</p>
    </Panel>
  </AppShell>
);

/** The external shell a third party sees: brand-only nav, company footer, and no product name anywhere. */
export const ExternalUpload = () => (
  <AppShell nav={<AppNav items={[]} brandOnly />} footer="Collision Engineers">
    <PageHeading title="Upload images for LM19 KXR" />
    <Panel>
      <SectionLabel>Your images</SectionLabel>
      <p style={{ margin: 0 }}>Add photographs of the damage, the registration plate and the odometer.</p>
    </Panel>
  </AppShell>
);

/** A navless surface with the footer omitted — the sign-in and error family. */
export const NavlessNoFooter = () => (
  <AppShell footer={null}>
    <Panel style={{ maxWidth: 420 }}>
      <SectionLabel>Sign in</SectionLabel>
      <p style={{ margin: 0 }}>Use your Collision Engineers account.</p>
    </Panel>
  </AppShell>
);
