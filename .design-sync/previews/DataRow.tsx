import { Button, DataRow, Provenance } from '@pegasus/design-system';

/** A recorded value with its provenance at the end. */
export const Recorded = () => (
  <DataRow field="Accident date" value="6 Aug 2026" end={<Provenance word="Extracted" />} />
);

/** Nothing recorded yet: the quiet `Not recorded`, then the suggestion beside it. */
export const NotRecordedWithSuggestion = () => (
  <DataRow field="Pre-accident value" suggested="£14,250" end={<Provenance word="AI" />} />
);

/** An action at the end instead of a provenance icon. */
export const WithAction = () => (
  <DataRow field="Inspection address" value="14 Ridgeway Close, Reading" end={<Button>Change</Button>} />
);

/** Several rows stacked as they appear inside a record body. */
export const Stacked = () => (
  <div>
    <DataRow field="Registration" value="LM19 KXR" end={<Provenance word="Lookup" />} />
    <DataRow field="Claimant" value="J. Okafor" end={<Provenance word="E-mail" />} />
    <DataRow field="Accident date" value="6 Aug 2026" end={<Provenance word="Extracted" />} />
    <DataRow field="Pre-accident value" suggested="£14,250" end={<Provenance word="AI" />} />
    <DataRow field="Repairer" end={<Provenance word="Staff" />} />
    <DataRow
      field="Inspection address"
      value="14 Ridgeway Close, Reading"
      end={
        <>
          <Provenance word="Staff" />
          <Button>Change</Button>
        </>
      }
    />
  </div>
);
