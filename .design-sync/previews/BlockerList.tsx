import { Blocker, BlockerList } from '@pegasus/design-system';

/** The readiness rail: three unmet requirements, each with its resolution. */
export const ReadinessRail = () => (
  <div style={{ maxWidth: 480 }}>
    <BlockerList>
      <Blocker title="Vehicle registration">Enter the registration on the Vehicle tab.</Blocker>
      <Blocker title="Accident date">Confirm the date from the instruction e-mail.</Blocker>
      <Blocker title="Pre-accident value">Record the value or accept the suggested £14,250.</Blocker>
    </BlockerList>
  </div>
);
