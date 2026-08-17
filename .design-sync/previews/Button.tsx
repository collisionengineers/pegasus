import { Button } from '@pegasus/design-system';

/** The four variants side by side: hairline default, charcoal committed action, Collision-red primary, and light on the record band. */
export const Variants = () => (
  <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 8 }}>
    <Button>Actions</Button>
    <Button variant="dark">Export</Button>
    <Button variant="primary">Create case</Button>
    <div style={{ background: '#1b1e23', padding: 12 }}>
      <Button variant="light">Original case</Button>
    </div>
  </div>
);

/** With a leading glyph, and icon-only with an accessible name. */
export const WithIcons = () => (
  <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 8 }}>
    <Button icon="upload">Upload evidence</Button>
    <Button icon="calendar">Set appointment</Button>
    <Button variant="dark" icon="check-circle">
      Approve
    </Button>
    <Button icon="refresh-cw" iconOnly aria-label="Refresh" />
    <Button icon="filter" iconOnly aria-label="Filter cases" />
  </div>
);

/** A disabled action stays visible and states its condition; the tooltip itself shows on hover and focus. */
export const DisabledStatesCondition = () => (
  <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 8 }}>
    <Button disabled>Assign to me</Button>
    <Button variant="dark" href="#" disabled condition="Available in Review">
      Export
    </Button>
    <Button variant="primary" disabled condition="Available once a finding is recorded">
      Complete
    </Button>
  </div>
);

/** As a link: the same shape carries an href, and a disabled link gets aria-disabled. */
export const AsLink = () => (
  <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 8 }}>
    <Button href="#">Open case</Button>
    <Button href="#" variant="dark" icon="arrow-right">
      Go to Inbox
    </Button>
    <Button href="#" disabled condition="Reference not yet allocated">
      Original case
    </Button>
  </div>
);
