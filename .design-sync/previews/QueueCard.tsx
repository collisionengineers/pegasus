import { QueueCard, QueueGrid, StatusChip } from '@pegasus/design-system';

/** Linked cards with an icon square, tabular count and trailing chevron; the top rail takes the state colour. */
export const LinkedWithIcon = () => (
  <QueueGrid>
    <QueueCard label="Needs sorting" icon="alert-triangle" state="needs-sorting" value={4} detail="Oldest 3 days" href="#" />
    <QueueCard label="Review" icon="info" state="review" value={12} detail="Oldest 2 days" href="#" />
    <QueueCard label="Blocked" icon="alert-circle" state="blocked" value={1} href="#" />
  </QueueGrid>
);

/** Plain (unlinked) cards: no chevron, no hover fill; one with no icon at all. */
export const PlainCards = () => (
  <QueueGrid>
    <QueueCard label="Held" icon="clock" state="held" value={3} detail="Oldest 12 Aug 09:14" />
    <QueueCard label="Completed today" state="completed" value={5} />
    <QueueCard label="Awaiting instruction" icon="clock" value={0} detail="Nothing waiting" />
  </QueueGrid>
);

/** Datum absent: a quiet em dash instead of a count, on a neutral rail. */
export const Unavailable = () => (
  <QueueGrid>
    <QueueCard label="Awaiting instruction" icon="clock" unavailable detail="Count not available" href="#" />
    <QueueCard label="Not ready" icon="alert-triangle" state="not-ready" value={7} href="#" />
  </QueueGrid>
);

/** A StatusChip child under the count names the state the queue is waiting on. */
export const WithStatusChip = () => (
  <QueueGrid>
    <QueueCard label="Held" icon="clock" state="held" value={3} href="#">
      <StatusChip state="Awaiting information" />
    </QueueCard>
    <QueueCard label="Not ready" icon="alert-triangle" state="not-ready" value={7} href="#">
      <StatusChip state="Registration missing" count={2} />
    </QueueCard>
  </QueueGrid>
);
