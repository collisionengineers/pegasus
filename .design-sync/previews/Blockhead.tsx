import { Blockhead, Button, DataRow, Provenance } from '@pegasus/design-system';

/** A block header on its own: the uppercase muted title above the block's rows. */
export const TitleOnly = () => (
  <div style={{ maxWidth: 720 }}>
    <Blockhead title="Vehicle" />
    <DataRow field="Registration" value="LM19 KXR" end={<Provenance word="Extracted" />} />
    <DataRow field="Make" value="Volkswagen" end={<Provenance word="Lookup" />} />
  </div>
);

/** Title with a trailing control pushed to the right. */
export const WithEndControl = () => (
  <div style={{ maxWidth: 720 }}>
    <Blockhead title="Instruction" end={<Button>Edit</Button>} />
    <DataRow field="Principal" value="AXA" end={<Provenance word="Principal" />} />
    <DataRow field="Claim" value="AX/44/210983" end={<Provenance word="Extracted" />} />
  </div>
);
