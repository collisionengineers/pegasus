import { Button, Gated } from '@pegasus/design-system';

/** A disabled control wrapped so its unlocking condition appears as a tooltip on hover and keyboard focus. */
export const DisabledWithCondition = () => (
  <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 8 }}>
    <Gated condition="Available in Review">
      <Button variant="dark" disabled>
        Export
      </Button>
    </Gated>
    <Gated condition="Needs a vehicle registration">
      <Button disabled>Run lookup</Button>
    </Gated>
  </div>
);
