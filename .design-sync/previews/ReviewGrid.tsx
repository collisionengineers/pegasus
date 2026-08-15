import { Blocker, BlockerList, DataRow, Panel, Provenance, ReviewGrid, SectionLabel } from '@pegasus/design-system';

/** Two equal columns for side-by-side review: the result and what is still missing. */
export const ResultAndMissing = () => (
  <div style={{ maxWidth: 960 }}>
    <ReviewGrid>
      <Panel>
        <SectionLabel>Result</SectionLabel>
        <DataRow field="Registration" value="LM19 KXR" end={<Provenance word="Extracted" />} />
        <DataRow field="Claimant" value="J. Okafor" end={<Provenance word="Extracted" />} />
        <DataRow field="Principal" value="AXA" end={<Provenance word="Principal" />} />
      </Panel>
      <Panel>
        <SectionLabel>Missing fields</SectionLabel>
        <BlockerList>
          <Blocker title="Accident date">Enter the date on the Instruction tab.</Blocker>
          <Blocker title="Claim number">Confirm the number from the instructing e-mail.</Blocker>
        </BlockerList>
      </Panel>
    </ReviewGrid>
  </div>
);
