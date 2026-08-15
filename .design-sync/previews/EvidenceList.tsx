import { EvidenceList } from '@pegasus/design-system';

/** Evidence linked to a Triage response: the office time, then what it showed. */
export const TriageEvidence = () => (
  <div style={{ maxWidth: 720 }}>
    <EvidenceList
      items={[
        { term: '14 Aug 08:52', value: 'Evidence 1041: repairer images received, four angles' },
        { term: '14 Aug 11:20', value: 'Evidence 1042: engineer noted nearside sill deformation' },
        { term: '15 Aug 09:05', value: 'Evidence 1043: claimant confirmed vehicle is off the road' },
      ]}
    />
  </div>
);

/** Conflicting values from a file, each with where it came from. */
export const ConflictingAddresses = () => (
  <div style={{ maxWidth: 720 }}>
    <EvidenceList
      items={[
        { term: '14 Ridgeway Close, Reading', value: 'Extracted from Instruction letter (page 1)' },
        { term: '22 Mill Lane, Wokingham', value: 'Extracted from Repairer estimate' },
        { term: 'Kingsway Accident Repair, Theale', value: 'Extracted from Booking confirmation' },
      ]}
    />
  </div>
);
