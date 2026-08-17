import { StatusCard } from '@pegasus/design-system';

/** Navy: in-flight explanation the operator needs before acting. */
export const Info = () => (
  <StatusCard title="Export is offered in Review">
    The case will offer Export once every readiness item is met. Nothing here has been sent to the Principal.
  </StatusCard>
);

/** Amber: incomplete or pending — the operator has something to do. */
export const Attention = () => (
  <StatusCard variant="attention">
    Every enabled staff member needs at least one role. Removing the final enabled Administrator is denied. Role changes
    invalidate existing browser sessions.
  </StatusCard>
);

/** Red: the action failed and the record is unchanged. */
export const ErrorCard = () => (
  <StatusCard variant="error" title="Nothing was sent" role="alert">
    The case is unchanged. You can try again.
  </StatusCard>
);

/** Green tick, ink text: confirmed completion of an action taken on another page. */
export const Done = () => <StatusCard variant="done">Case CE-2026-01432 was reopened.</StatusCard>;

/** A card whose body carries more than one paragraph. */
export const WithHeadingAndBody = () => (
  <StatusCard variant="error" title="Cases are unavailable" role="alert">
    <p>The authorised case query could not be completed. Try again.</p>
    <p>If it keeps failing, tell your administrator the reference shown on the error page.</p>
  </StatusCard>
);
