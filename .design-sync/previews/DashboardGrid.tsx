import { DashboardGrid, Panel, SectionLabel } from '@pegasus/design-system';

/** Two equal columns of panels; single column under 1280px. */
export const TwoPanels = () => (
  <DashboardGrid>
    <Panel>
      <SectionLabel>Active cases</SectionLabel>
      <p style={{ margin: 0 }}>Not ready 7 · Review 4 · Held 2</p>
    </Panel>
    <Panel>
      <SectionLabel>E-mail activity</SectionLabel>
      <p style={{ margin: 0 }}>Received today 18 · Needs sorting 3 · Blocked 1</p>
    </Panel>
  </DashboardGrid>
);
