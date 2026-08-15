import { StatusChip } from '@pegasus/design-system';

/** The case-lifecycle chips, tone chosen from the settled state text. */
export const CaseLifecycle = () => (
  <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
    <StatusChip state="Not ready" />
    <StatusChip state="Review" />
    <StatusChip state="Held" />
    <StatusChip state="Completed" />
    <StatusChip state="Cancelled" />
    <StatusChip state="Created in error" />
  </div>
);

/** Intake and Triage states — amber for incomplete, red for the failure boundary. */
export const IntakeAndTriage = () => (
  <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
    <StatusChip state="Needs sorting" />
    <StatusChip state="Blocked" />
    <StatusChip state="Draft ready" />
    <StatusChip state="Awaiting information" />
    <StatusChip state="Finding recorded" />
    <StatusChip state="Registration missing" />
  </div>
);

/** Query freshness — only non-current states earn a chip on a screen. */
export const QueryFreshness = () => (
  <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
    <StatusChip state="Refreshing" />
    <StatusChip state="Stale" />
    <StatusChip state="Partial" />
    <StatusChip state="Unavailable" />
    <StatusChip state="Failed" />
  </div>
);

/** Access, leases and mutation outcomes. */
export const AccessAndLeases = () => (
  <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
    <StatusChip state="Denied" />
    <StatusChip state="Lease held" />
    <StatusChip state="Lease expired" />
    <StatusChip state="Conflict" />
    <StatusChip state="Disabled" />
    <StatusChip state="Approved" />
  </div>
);

/** A count in brackets, and an explicit tone override for a state the map does not know. */
export const WithCountAndOverride = () => (
  <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
    <StatusChip state="Review" count={12} />
    <StatusChip state="Needs sorting" count={3} />
    <StatusChip state="Automation" tone="navy" icon="refresh-cw" />
    <StatusChip state="No longer polled" tone="neutral" icon="clock" />
  </div>
);
