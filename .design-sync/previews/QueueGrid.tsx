import { QueueCard, QueueGrid, StatusChip } from '@pegasus/design-system';

/** The Inbox queues: four linked cards, each on the state it stands for. */
export const InboxQueues = () => (
  <QueueGrid>
    <QueueCard label="Needs sorting" icon="alert-triangle" state="needs-sorting" value={4} detail="Oldest 3 days" href="#" />
    <QueueCard label="Blocked" icon="alert-circle" state="blocked" value={1} detail="Oldest 12 Aug 09:14" href="#" />
    <QueueCard label="Not ready" icon="alert-triangle" state="not-ready" value={7} detail="Oldest 6 days" href="#" />
    <QueueCard label="Review" icon="info" state="review" value={12} detail="Oldest 2 days" href="#" />
  </QueueGrid>
);

/** Three cards: one carrying a chip for its dominant state, one confirmed completion, one whose datum is absent. */
export const MixedAvailability = () => (
  <QueueGrid>
    <QueueCard label="Held" icon="clock" state="held" value={3} href="#">
      <StatusChip state="Awaiting information" />
    </QueueCard>
    <QueueCard label="Completed today" icon="check-circle" state="completed" value={5} href="#" />
    <QueueCard label="Awaiting instruction" icon="clock" unavailable detail="Count not available" href="#" />
  </QueueGrid>
);
