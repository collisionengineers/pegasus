import { EvidenceFigure } from '@pegasus/design-system';

/** A row of guide figures with their sources. */
export const GuideFigures = () => (
  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, minmax(0, 1fr))', gap: 12 }}>
    <EvidenceFigure label="CAP retail" value="£14,250" source="CAP, 12 Aug" />
    <EvidenceFigure label="Glass's trade" value="£12,900" source="Glass's guide, 12 Aug" />
    <EvidenceFigure label="Mileage-adjusted" value="£13,780" source="48,210 miles against guide" />
  </div>
);

/** No figure recorded yet: an em-dash in the value slot and the words that say so underneath. */
export const NoFigureRecorded = () => (
  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, minmax(0, 1fr))', gap: 12 }}>
    <EvidenceFigure label="CAP retail" value="£14,250" source="CAP, 12 Aug" />
    <EvidenceFigure
      label="CAP trade"
      value={
        <>
          <span aria-hidden="true">—</span>
          <span className="vh">No value</span>
        </>
      }
      source="No figure recorded"
    />
    <EvidenceFigure
      label="Glass's retail"
      value={
        <>
          <span aria-hidden="true">—</span>
          <span className="vh">No value</span>
        </>
      }
      source="No figure recorded"
    />
  </div>
);

/** A single figure without a source line. */
export const Single = () => (
  <div style={{ maxWidth: 260 }}>
    <EvidenceFigure label="Pre-accident value" value="£14,250" />
  </div>
);
