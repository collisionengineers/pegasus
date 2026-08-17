import { EmptyState, Panel } from '@pegasus/design-system';

/** Muted business-language copy for a zero result. */
export const Plain = () => <EmptyState>No cases match these filters.</EmptyState>;

/** Inside a panel, where the list would otherwise be. */
export const InPanel = () => (
  <div style={{ maxWidth: 480 }}>
    <Panel>
      <h2 style={{ margin: '0 0 8px', fontSize: '1rem' }}>Awaiting information</h2>
      <EmptyState>Nothing is waiting on a repairer or principal right now.</EmptyState>
    </Panel>
  </div>
);
