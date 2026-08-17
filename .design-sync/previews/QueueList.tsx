import { QueueList, QueueListRow, StatusChip } from '@pegasus/design-system';

/** A Held queue: reference and reason on the left, the state chip and next chase on the right, every row a full-row link. */
export const HeldQueue = () => (
  <div style={{ maxWidth: 960 }}>
    <QueueList>
      <QueueListRow
        href="#"
        title="CE-2026-01432"
        subtitle="AXA · LM19 KXR · Waiting on repairer images"
        end={<><StatusChip state="Awaiting information" /><small>Next chase 18 Aug</small></>}
      />
      <QueueListRow
        href="#"
        title="CE-2026-01418"
        subtitle="Direct Line · YD68 TFA · Total-loss valuation queried"
        end={<><StatusChip state="Held" /><small>Next chase 17 Aug</small></>}
      />
      <QueueListRow
        href="#"
        title="CE-2026-01407"
        subtitle="Aviva · KX21 PLR · Salvage category disputed"
        end={<><StatusChip state="Held" /><small>Next chase 20 Aug</small></>}
      />
      <QueueListRow
        href="#"
        title="CE-2026-01399"
        subtitle="LV= · BN70 WQF · Awaiting engineer allocation"
        end={<><StatusChip state="Not ready" /><small>Since 12 Aug 09:14</small></>}
      />
    </QueueList>
  </div>
);

/** The mail workspace: sender left, subject and excerpt in the middle, received time right; unread rows carry weight and the word. */
export const MailWorkspace = () => (
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
      <QueueListRow
        href="#"
        state="unread"
        title="motorclaims@directline.com · Unread"
        subtitle="Direct Line"
        middle={<><strong>YD68 TFA — request for desktop assessment</strong><small>Please confirm whether a desktop assessment is possible from the enclosed…</small></>}
        end={<time dateTime="2026-08-13T16:40">13 Aug 16:40</time>}
      />
    </QueueList>
  </div>
);
