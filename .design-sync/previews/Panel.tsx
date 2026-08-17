import { Panel, SectionLabel } from '@pegasus/design-system';

/** A white card on the paper ground with a section label and body copy. */
export const WithSectionLabel = () => (
  <Panel>
    <SectionLabel>Next chase</SectionLabel>
    <p style={{ margin: 0 }}>Repairer images requested on 12 Aug; next chase due 15 Aug.</p>
  </Panel>
);

/** An iconed section label and a short definition list inside the panel. */
export const IconedLabel = () => (
  <Panel>
    <SectionLabel icon="clock">Received</SectionLabel>
    <dl style={{ margin: 0, display: 'grid', gridTemplateColumns: 'max-content 1fr', columnGap: 16, rowGap: 4 }}>
      <dt>Instruction</dt>
      <dd style={{ margin: 0 }}>12 Aug 09:14</dd>
      <dt>Principal</dt>
      <dd style={{ margin: 0 }}>Direct Line</dd>
      <dt>Registration</dt>
      <dd style={{ margin: 0 }}>YD68 TFA</dd>
    </dl>
  </Panel>
);
