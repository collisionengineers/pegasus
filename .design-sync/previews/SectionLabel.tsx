import { Panel, SectionLabel } from '@pegasus/design-system';

/** A plain section label naming a section of a panel. */
export const Plain = () => (
  <Panel style={{ maxWidth: 480 }}>
    <SectionLabel>Reading order</SectionLabel>
    <p>The instruction e-mail is read first, then the images, then the repairer&apos;s estimate.</p>
  </Panel>
);

/** With a Lucide glyph before the text. */
export const WithIcon = () => (
  <Panel style={{ maxWidth: 480 }}>
    <SectionLabel icon="alert-triangle">Outstanding</SectionLabel>
    <p>The registration and the accident date are still needed before this case can move to Review.</p>
  </Panel>
);
