import { Button, Record, RecordBar, RecordBody, RecordHead, StatusChip } from '@pegasus/design-system';

/** State actions on the left; the record-level commitment right-aligned in charcoal behind the hairline rule. */
export const ActionsAndCommit = () => (
  <Record state="review">
    <RecordHead
      reference="CE-2026-01432"
      identity={[<b>AXA</b>, 'LM19 KXR', 'J. Okafor', 'Total loss']}
      end={<StatusChip state="Review" />}
    />
    <RecordBar end={<Button variant="dark">Export</Button>}>
      <Button>Actions</Button>
      <Button>Original case</Button>
      <Button>Assign to me</Button>
    </RecordBar>
    <RecordBody>
      <p>Overview</p>
    </RecordBody>
  </Record>
);

/** The committed action is not available yet: it stays visible, disabled, and states its condition. */
export const DisabledWithCondition = () => (
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
      <Button>Record registration</Button>
    </RecordBar>
    <RecordBody>
      <p>Overview</p>
    </RecordBody>
  </Record>
);

/** A bar with state actions only — no record-level commitment, so no rule. */
export const ActionsOnly = () => (
  <Record state="held">
    <RecordHead
      reference="CE-2026-01299"
      identity={[<b>LV=</b>, 'RJ70 WPD', 'M. Brennan', 'Repair']}
      end={<StatusChip state="Held" />}
    />
    <RecordBar>
      <Button>Actions</Button>
      <Button>Release hold</Button>
      <Button icon="clock">History</Button>
    </RecordBar>
    <RecordBody>
      <p>Overview</p>
    </RecordBody>
  </Record>
);
