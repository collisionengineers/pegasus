---
category: Metrics
---

One row of a `QueueList`: identity on the left (`title` in bold with an optional muted `subtitle`), an optional `middle` column of the same strong/small shape (mail: subject and excerpt), and `end` on the right (a `StatusChip` with a small line, or a `<time>`). With `href` the whole row is an `<a>` and gets the trailing `›` and hover rail; without it renders as an `<article>`. `state="unread"` sets `data-state` so the title renders at weight 800.

**Rules**

- `title` is the record identity: a case reference (`CE-2026-01432`), a registration, or a sender; `subtitle` is one muted line — principal · registration · reason — never a paragraph.
- Keep `end` to a state and one qualifier (`Next chase 18 Aug`, `Since 12 Aug 09:14`, `2 attachments`); the state is a `StatusChip` so it is read as text, not colour.
- Unread mail says `Unread` in the row as well as carrying `state="unread"`.
- The row is the target: do not put buttons or a second link inside a linked row.

**Examples**

```tsx
<QueueListRow
  href="/Cases/CE-2026-01432"
  title="CE-2026-01432"
  subtitle="AXA · LM19 KXR · J. Okafor"
  end={<><StatusChip state="Awaiting information" /><small>Next chase 18 Aug</small></>}
/>

<QueueListRow
  href="/Mail/1042"
  state="unread"
  title="claims.engineering@axa.co.uk · Unread"
  subtitle="AXA"
  middle={<><strong>New instruction — LM19 KXR</strong><small>Please find attached the engineer instruction…</small></>}
  end={<time dateTime="2026-08-14T09:14">14 Aug 09:14</time>}
/>
```
