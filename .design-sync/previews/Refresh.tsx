import { Refresh } from '@pegasus/design-system';

/** Current: last-good time and the refresh button, no chip. */
export const Current = () => <Refresh updatedAt="14 Aug 09:32" />;

/** Stale: the last-good time stays visible and the chip names the state. */
export const Stale = () => <Refresh updatedAt="14 Aug 08:05" status="stale" />;

/** Loading: `.is-refreshing` spins the icon while the Refreshing chip carries the state in text. */
export const Loading = () => <Refresh updatedAt="14 Aug 09:32" status="loading" />;

/** No successful load yet. */
export const NeverUpdated = () => <Refresh />;

/** The failure family: partial, unavailable and failed keep the last-good time. */
export const PartialUnavailableFailed = () => (
  <div style={{ display: 'flex', flexDirection: 'column', gap: 12, alignItems: 'flex-start' }}>
    <Refresh updatedAt="14 Aug 09:32" status="partial" />
    <Refresh updatedAt="14 Aug 09:32" status="unavailable" />
    <Refresh updatedAt="14 Aug 07:48" status="failed" />
  </div>
);
