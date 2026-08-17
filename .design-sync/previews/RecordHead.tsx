import { Record, RecordHead, StatusChip } from '@pegasus/design-system';

/** The dark band as it sits at the top of a case: reference, identity facts, stage chip, then the 3px stage accent. */
export const CaseBand = () => (
  <Record state="review">
    <RecordHead
      reference="CE-2026-01432"
      identity={[<b>AXA</b>, 'LM19 KXR', 'J. Okafor', 'Total loss']}
      end={<StatusChip state="Review" />}
    />
  </Record>
);

/** A Triage record whose head carries a muted note under the band, before the accent. */
export const WithNote = () => (
  <Record state="pending">
    <RecordHead
      reference="YD68 TFA"
      identity={[<b>Triage</b>, 'Opened 14 Aug 08:52', 'Unassigned']}
      end={<StatusChip state="Awaiting information" />}
      note="Waiting on the repairer's images before a finding can be recorded."
    />
  </Record>
);

/** A record that is not yet a case: identity facts state what is missing, and the chip says so in words. */
export const NotReady = () => (
  <Record state="not-ready">
    <RecordHead
      reference="CE-2026-01507"
      identity={[<b>Direct Line</b>, 'No registration', 'No claimant recorded', 'Repair']}
      end={<StatusChip state="Not ready" />}
    />
  </Record>
);

/** Without the stage accent (`accent={false}`) — the band ends flush, for a record that has no stage yet. */
export const WithoutAccent = () => (
  <Record>
    <RecordHead
      reference="CE-2026-01388"
      identity={[<b>Aviva</b>, 'KX21 HGV', 'P. Sandhu', 'Repair']}
      end={<StatusChip state="Completed" />}
      accent={false}
    />
  </Record>
);
