import { Metric, MetricStrip, SectionLabel } from '@pegasus/design-system';

/** The dashboard: three case-stage metrics on the state channel, each a link to the exact filtered list, then the e-mail activity strip. */
export const DashboardStrips = () => (
  <div>
    <SectionLabel>Active cases</SectionLabel>
    <MetricStrip columns={3}>
      <Metric label="Not ready" icon="alert-triangle" state="not-ready" value={7} href="#" />
      <Metric label="Review" icon="info" state="review" value={12} href="#" />
      <Metric label="Held" icon="clock" state="held" value={3} href="#" />
    </MetricStrip>
    <SectionLabel>E-mail activity</SectionLabel>
    <MetricStrip columns={3}>
      <Metric label="Received today" icon="file-text" value={41} href="#" />
      <Metric label="Needs sorting" icon="alert-triangle" state="needs-sorting" value={4} />
      <Metric label="Blocked" icon="alert-circle" state="blocked" value={0} />
    </MetricStrip>
  </div>
);

/** Five columns (`secondary`): today and this week — plain tiles that have no filtered list behind them. */
export const FiveColumns = () => (
  <MetricStrip columns={5}>
    <Metric label="New cases today" icon="file-text" value={9} href="#" />
    <Metric label="Sent to Engineer today" icon="arrow-right" value={6} />
    <Metric label="Sent to Engineer this week" icon="arrow-right" value={27} />
    <Metric label="Reports sent today" icon="upload" value={4} />
    <Metric label="Reports sent this week" icon="upload" value={19} />
  </MetricStrip>
);

/** Seven columns (default): the operations queues on one row, one absent datum stating its state. */
export const SevenColumns = () => (
  <MetricStrip>
    <Metric label="Needs sorting" icon="alert-triangle" state="needs-sorting" value={4} href="#" />
    <Metric label="Blocked" icon="alert-circle" state="blocked" value={1} href="#" />
    <Metric label="Not ready" icon="alert-triangle" state="not-ready" value={7} href="#" />
    <Metric label="Review" icon="info" state="review" value={12} href="#" />
    <Metric label="Held" icon="clock" state="held" value={3} href="#" />
    <Metric label="Completed today" icon="check-circle" state="completed" value={5} href="#" />
    <Metric label="Awaiting instruction" icon="clock" absent="Unavailable" />
  </MetricStrip>
);
