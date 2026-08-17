import { MetricTile, TileGrid } from '@pegasus/design-system';

/** Default tiles: icon square, big tabular count, label; one linked, one plain. */
export const Default = () => (
  <div style={{ maxWidth: 640 }}>
    <TileGrid>
      <MetricTile label="Open cases" value={124} icon="file-text" href="#" />
      <MetricTile label="Completed this week" value={31} icon="check-circle" />
    </TileGrid>
  </div>
);

/** `attention`: amber rail and icon tint for a figure the operator should act on. */
export const Attention = () => (
  <div style={{ maxWidth: 640 }}>
    <TileGrid>
      <MetricTile label="Awaiting information" value={9} icon="clock" attention href="#" />
      <MetricTile label="Needs sorting" value={4} icon="alert-triangle" attention />
    </TileGrid>
  </div>
);

/** `span`: one tile takes the full grid width under a pair. */
export const Span = () => (
  <div style={{ maxWidth: 640 }}>
    <TileGrid>
      <MetricTile label="Open cases" value={124} icon="file-text" />
      <MetricTile label="Awaiting information" value={9} icon="clock" attention />
      <MetricTile label="Reports sent this month" value={118} icon="upload" span href="#" />
    </TileGrid>
  </div>
);
