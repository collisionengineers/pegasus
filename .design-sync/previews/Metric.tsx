import { Metric, MetricStrip } from '@pegasus/design-system';

/** Linked metrics on the state channel: the top rail and icon tint carry the state, the label carries it in words. */
export const LinkedStates = () => (
  <MetricStrip columns={3}>
    <Metric label="Not ready" icon="alert-triangle" state="not-ready" value={7} href="#" />
    <Metric label="Review" icon="info" state="review" value={12} href="#" />
    <Metric label="Held" icon="clock" state="held" value={3} href="#" />
  </MetricStrip>
);

/** Plain (unlinked) figures: a neutral rail when there is no state, and a count of zero renders 0. */
export const PlainFigures = () => (
  <MetricStrip columns={3}>
    <Metric label="Sent to Engineer today" icon="arrow-right" value={6} />
    <Metric label="Reports sent today" icon="upload" value={0} />
    <Metric label="Blocked" icon="alert-circle" state="blocked" value={0} />
  </MetricStrip>
);

/** An absent datum: the state text replaces the value — never a dash pretending to be a number. */
export const AbsentDatum = () => (
  <MetricStrip columns={3}>
    <Metric label="Received today" icon="file-text" value={41} href="#" />
    <Metric label="Awaiting instruction" icon="clock" absent="Unavailable" />
    <Metric label="Needs sorting" icon="alert-triangle" state="needs-sorting" absent="Refreshing" />
  </MetricStrip>
);
