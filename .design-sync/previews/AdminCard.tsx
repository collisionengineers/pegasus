import { AdminCard, AdminWorkspaces } from '@pegasus/design-system';

/** A workspace entry: icon square, linked title, one-line description. */
export const WithDescription = () => (
  <AdminWorkspaces>
    <AdminCard icon="user" title="Staff accounts" href="#">Enable, disable and reset staff sign-in.</AdminCard>
    <AdminCard icon="shield" title="Roles" href="#">Assign the roles that decide what each person can do.</AdminCard>
  </AdminWorkspaces>
);

/** Title only: a card whose name is its whole description. */
export const TitleOnly = () => (
  <AdminWorkspaces>
    <AdminCard icon="file-text" title="Principals" href="#" />
    <AdminCard icon="lock" title="Access review" href="#" />
  </AdminWorkspaces>
);
