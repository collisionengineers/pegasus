import { AdminCard, AdminWorkspaces } from '@pegasus/design-system';

/** The Administration landing: one card per workspace, the whole card is the target. */
export const Administration = () => (
  <AdminWorkspaces>
    <AdminCard icon="user" title="Staff accounts" href="#">Enable, disable and reset staff sign-in.</AdminCard>
    <AdminCard icon="shield" title="Roles" href="#">Assign the roles that decide what each person can do.</AdminCard>
    <AdminCard icon="file-text" title="Principals" href="#">Instructing insurers and their case reference formats.</AdminCard>
    <AdminCard icon="filter" title="Organisations" href="#">Repairers, salvage agents and other parties on a case.</AdminCard>
  </AdminWorkspaces>
);
