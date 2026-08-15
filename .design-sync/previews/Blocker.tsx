import { Blocker, BlockerList } from '@pegasus/design-system';

/** One unmet requirement naming its field and its resolution. */
export const SingleRequirement = () => (
  <div style={{ maxWidth: 480 }}>
    <BlockerList>
      <Blocker title="Vehicle registration">Enter the registration on the Vehicle tab.</Blocker>
    </BlockerList>
  </div>
);

/** The state channel picks the tone: not-ready amber, blocked red, review navy. */
export const Tones = () => (
  <div style={{ maxWidth: 480 }}>
    <BlockerList>
      <Blocker state="not-ready" title="Claimant name">
        Record the claimant on the Instruction tab.
      </Blocker>
      <Blocker state="blocked" title="Principal identity">
        The sender does not match a known principal. Confirm the instructing insurer.
      </Blocker>
      <Blocker state="review" title="Engineer sign-off">
        Awaiting the engineer&apos;s assessment.
      </Blocker>
    </BlockerList>
  </div>
);
