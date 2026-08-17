import { PrimaryAction } from '@pegasus/design-system';

/** The one page-level submit in Collision red. */
export const Submit = () => <PrimaryAction>Save changes</PrimaryAction>;

/** With a leading glyph, as a submit and as a link. */
export const WithIconAndAsLink = () => (
  <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 10 }}>
    <PrimaryAction icon="upload">Upload evidence</PrimaryAction>
    <PrimaryAction href="#" icon="arrow-right">
      Continue to Review
    </PrimaryAction>
  </div>
);
