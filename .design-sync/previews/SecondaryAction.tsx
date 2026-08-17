import { SecondaryAction } from '@pegasus/design-system';

/** The hairline companion to the primary action. */
export const Cancel = () => <SecondaryAction type="button">Cancel</SecondaryAction>;

/** With a leading glyph, and as a link back to the list. */
export const WithIconAndAsLink = () => (
  <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 10 }}>
    <SecondaryAction type="button" icon="clock">
      Save as draft
    </SecondaryAction>
    <SecondaryAction href="#" icon="arrow-right">
      Back to Cases
    </SecondaryAction>
  </div>
);
