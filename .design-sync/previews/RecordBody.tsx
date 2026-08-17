import {
  Button,
  DataRow,
  Facts,
  Provenance,
  Record,
  RecordBar,
  RecordBody,
  RecordHead,
  StatusChip,
} from '@pegasus/design-system';

/** The body in context: a Facts block followed by DataRows, under the head and bar. */
export const FactsAndDataRows = () => (
  <Record state="review">
    <RecordHead
      reference="CE-2026-01432"
      identity={[<b>AXA</b>, 'LM19 KXR', 'J. Okafor', 'Total loss']}
      end={<StatusChip state="Review" />}
    />
    <RecordBar end={<Button variant="dark">Export</Button>}>
      <Button>Actions</Button>
    </RecordBar>
    <RecordBody>
      <Facts
        groups={[
          {
            title: 'Vehicle',
            items: [
              { term: 'Registration', value: 'LM19 KXR' },
              { term: 'Make', value: 'Volkswagen' },
              { term: 'Model', value: 'Golf GTD' },
              { term: 'Mileage', value: '48,210' },
            ],
          },
          {
            title: 'Instruction',
            items: [
              { term: 'Principal', value: 'AXA' },
              { term: 'Claim', value: 'AX/44/210983' },
              { term: 'Received', value: '12 Aug 09:14' },
              { term: 'Engineer', value: 'Not assigned', quiet: true },
            ],
          },
        ]}
      />
      <DataRow field="Accident date" value="6 Aug 2026" end={<Provenance word="Extracted" />} />
      <DataRow field="Claimant" value="J. Okafor" end={<Provenance word="E-mail" />} />
      <DataRow field="Pre-accident value" suggested="£14,250" end={<Provenance word="AI" />} />
    </RecordBody>
  </Record>
);

/** A plain body holding DataRows only — a Triage record with a reading order and no tab row. */
export const DataRowsOnly = () => (
  <Record state="pending">
    <RecordHead
      reference="YD68 TFA"
      identity={[<b>Triage</b>, 'Opened 14 Aug 08:52', 'Unassigned']}
      end={<StatusChip state="Awaiting information" />}
    />
    <RecordBar>
      <Button>Record finding</Button>
      <Button>Assign to me</Button>
    </RecordBar>
    <RecordBody>
      <DataRow field="Roadworthiness" end={<Provenance word="Staff" />} />
      <DataRow field="Assessment" value="Images requested" end={<Provenance word="Staff" />} />
      <DataRow field="Repairer" value="Kingsway Accident Repair" end={<Provenance word="E-mail" />} />
    </RecordBody>
  </Record>
);
