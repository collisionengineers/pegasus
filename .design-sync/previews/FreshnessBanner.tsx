import { Button, FreshnessBanner, StatusChip } from '@pegasus/design-system';

const refreshButton = <Button icon="refresh-cw">Refresh</Button>;

/** Current: hairline strip with the last-good time and the manual refresh. */
export const Current = () => (
  <FreshnessBanner action={refreshButton}>
    Updated <time>14 Aug 09:32</time> London
  </FreshnessBanner>
);

/** Stale: amber rail, chip beside the time. */
export const Stale = () => (
  <FreshnessBanner status="stale" action={refreshButton}>
    Updated <time>14 Aug 08:05</time> London <StatusChip state="Stale" />
  </FreshnessBanner>
);

/** Loading: navy ground while the same filter reruns; last-good data stays visible and the button is held. */
export const Loading = () => (
  <FreshnessBanner
    status="loading"
    action={
      <Button icon="refresh-cw" disabled>
        Refreshing
      </Button>
    }
  >
    Updated <time>14 Aug 09:32</time> London <StatusChip state="Refreshing" />
  </FreshnessBanner>
);

/** Failed: red rail; the last-good time is kept and the operator can try again. */
export const Failed = () => (
  <FreshnessBanner status="failed" action={refreshButton}>
    Updated <time>14 Aug 07:48</time> London <StatusChip state="Failed" />
  </FreshnessBanner>
);
