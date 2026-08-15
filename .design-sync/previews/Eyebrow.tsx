import { Eyebrow } from '@pegasus/design-system';

/** The small uppercase muted label above a heading. */
export const AboveHeading = () => (
  <div>
    <Eyebrow>Cases</Eyebrow>
    <h1>Case CE-2026-01432</h1>
  </div>
);

/** Above a figure. */
export const AboveFigure = () => (
  <div>
    <Eyebrow>In Review</Eyebrow>
    <strong style={{ fontSize: 28, fontWeight: 600 }}>12</strong>
  </div>
);
