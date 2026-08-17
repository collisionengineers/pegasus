import { QueueList, QueueListRow, StatusChip } from '@pegasus/design-system';

/** Linked rows: identity left, chip and a muted line right, the › affordance at the end. */
export const LinkedRows = () => (
  <div style={{ maxWidth: 960 }}>
    <QueueList>
      <QueueListRow
        href="#"
        title="CE-2026-01432"
        subtitle="AXA · LM19 KXR · J. Okafor"
        end={<><StatusChip state="Awaiting information" /><small>Next chase 18 Aug</small></>}
      />
      <QueueListRow
        href="#"
        title="CE-2026-01418"
        subtitle="Direct Line · YD68 TFA"
        end={<><StatusChip state="Held" /><small>Next chase 17 Aug</small></>}
      />
      <QueueListRow
        href="#"
        title="CE-2026-01399"
        subtitle="LV= · BN70 WQF"
        end={<><StatusChip state="Not ready" /><small>Since 12 Aug 09:14</small></>}
      />
    </QueueList>
  </div>
);

/** Mail rows with a `middle` column; `state="unread"` renders the sender at weight 800 and the row says "Unread" in words. */
export const MailRows = () => (
  <div style={{ maxWidth: 960 }}>
    <QueueList>
      <QueueListRow
        href="#"
        state="unread"
        title="claims.engineering@axa.co.uk · Unread"
        subtitle="AXA"
        middle={<><strong>New instruction — LM19 KXR</strong><small>Please find attached the engineer instruction for policyholder J. Okafor…</small></>}
        end={<time dateTime="2026-08-14T09:14">14 Aug 09:14</time>}
      />
      <QueueListRow
        href="#"
        title="repairs@northgatebodyshop.co.uk"
        subtitle="Repairer"
        middle={<><strong>RE: CE-2026-01418 images</strong><small>Images of the offside rear quarter and boot floor as requested…</small></>}
        end={<time dateTime="2026-08-14T08:52">14 Aug 08:52</time>}
      />
    </QueueList>
  </div>
);

/** Unlinked rows (`article`): a read-only listing with no destination and so no › affordance. */
export const PlainRows = () => (
  <div style={{ maxWidth: 960 }}>
    <QueueList>
      <QueueListRow title="CE-2026-01380" subtitle="Admiral · RJ19 HNB · Completed 13 Aug" end={<StatusChip state="Completed" />} />
      <QueueListRow title="CE-2026-01377" subtitle="Aviva · KX21 PLR · Cancelled by principal" end={<StatusChip state="Cancelled" />} />
    </QueueList>
  </div>
);
