import { FailureDetail } from '@pegasus/design-system';

/** The red-railed detail block under a failed action: what happened and what to do next. */
export const UnderFailedAction = () => (
  <div style={{ maxWidth: 640 }}>
    <FailureDetail>
      <strong>Nothing was sent.</strong>
      <span>The case is unchanged. Try again, or ask the office to check the mailbox connection.</span>
    </FailureDetail>
  </div>
);

/** With a time the operator can quote. */
export const WithReference = () => (
  <div style={{ maxWidth: 640 }}>
    <FailureDetail>
      <strong>The lookup for LM19 KXR did not complete.</strong>
      <span>Recorded 12 Aug 09:14. Enter the vehicle details by hand or retry the lookup.</span>
    </FailureDetail>
  </div>
);
