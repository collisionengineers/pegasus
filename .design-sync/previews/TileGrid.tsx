import { MetricTile, TileGrid } from '@pegasus/design-system';

/** Tiles sharing hairline borders in two columns: one needing attention, one spanning the full width. */
export const CaseFigures = () => (
  <div style={{ maxWidth: 640 }}>
    <TileGrid>
      <MetricTile label="Open cases" value={124} icon="file-text" href="#" />
      <MetricTile label="Awaiting information" value={9} icon="clock" attention href="#" />
      <MetricTile label="Reports sent this month" value={118} icon="upload" span href="#" />
      <MetricTile label="Completed this week" value={31} icon="check-circle" />
      <MetricTile label="Sent to Engineer this week" value={27} icon="arrow-right" />
    </TileGrid>
  </div>
);
