import { Button, Lede } from '@pegasus/design-system';

/** One muted sentence beside a consequential control — the only place a lede belongs; screens carry none. */
export const BesideControl = () => (
  <div style={{ maxWidth: 560 }}>
    <h2>Close as created in error</h2>
    <Lede>The reference CE-2026-01432 will not be reused and this case will not reopen.</Lede>
    <div style={{ marginTop: 12 }}>
      <Button variant="primary">Close case</Button>
    </div>
  </div>
);
