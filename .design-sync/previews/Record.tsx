import {
  Button,
  Crumb,
  DataRow,
  Facts,
  Provenance,
  Record,
  RecordBar,
  RecordBody,
  RecordHead,
  StatusChip,
  Tabs,
} from '@pegasus/design-system';

/** A case in Review: dark band, identity facts, stage chip, action bar with the committed action behind the rule, tabs. */
export const CaseInReview = () => (
  <div>
    <Crumb parents={[{ label: 'Cases', href: '#' }]} current="CE-2026-01432" />
    <Record state="review">
      <RecordHead
        reference="CE-2026-01432"
        identity={[<b>AXA</b>, 'LM19 KXR', 'J. Okafor', 'Total loss']}
        end={<StatusChip state="Review" />}
      />
      <RecordBar end={<Button variant="dark">Export</Button>}>
        <Button>Actions</Button>
        <Button>Original case</Button>
      </RecordBar>
      <Tabs
        label="Case sections"
        tabs={[
          { label: 'Overview', href: '#', current: true },
          { label: 'Evidence', href: '#', count: 7 },
          { label: 'History', href: '#', count: 12 },
        ]}
      />
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
  </div>
);

/** A case that is Not ready: the export action stays visible, disabled, and states its condition. */
export const CaseNotReady = () => (
  <Record state="not-ready">
    <RecordHead
      reference="CE-2026-01507"
      identity={[<b>Direct Line</b>, 'No registration', 'No claimant recorded', 'Repair']}
      end={<StatusChip state="Not ready" />}
    />
    <RecordBar
      end={
        <Button variant="dark" href="#" disabled condition="Available in Review">
          Export
        </Button>
      }
    >
      <Button>Actions</Button>
    </RecordBar>
    <RecordBody>
      <DataRow field="Registration" end={<Provenance word="Staff" />} />
      <DataRow field="Claimant" end={<Provenance word="Staff" />} />
      <DataRow field="Accident date" suggested="3 Aug 2026" end={<Provenance word="Extracted" />} />
    </RecordBody>
  </Record>
);

/** A record with a reading order — plain body, no tab row — plus a note under the band. */
export const TriageRecord = () => (
  <Record state="pending">
    <RecordHead
      reference="LM19 KXR"
      identity={[<b>Triage</b>, 'Opened 14 Aug 08:52', 'Unassigned']}
      end={<StatusChip state="Awaiting information" />}
      note="Waiting on the repairer's images before a finding can be recorded."
    />
    <RecordBar
      end={
        <Button variant="dark" href="#" disabled condition="Available once a finding is recorded">
          Complete
        </Button>
      }
    >
      <Button>Record finding</Button>
      <Button>Assign to me</Button>
    </RecordBar>
    <RecordBody>
      <DataRow field="Roadworthiness" end={<Provenance word="Staff" />} />
      <DataRow field="Assessment" value="Images requested" end={<Provenance word="Staff" />} />
    </RecordBody>
  </Record>
);
