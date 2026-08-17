---
category: Record
---

The dark header band of the record container (`.record__head`, followed by `.record__accent`): the reference as the screen's `<h1>` in tabular numerals, the identity facts beside it behind a faint rule (principal in bold, registration, claimant, case type), and the stage `StatusChip` right-aligned in the `end` slot. An optional `note` renders under the band in the band colour before the 3px stage accent. Use it only as the first child of `Record`; the parent's `state` colours the accent.

**Rules**

- Keep the head to one line of facts: reference, principal (`<b>`), registration, claimant, case type. Nothing else — no lede, no page heading above or below.
- The state must appear as text in the chip; the accent colour is a second signal only. Missing facts are stated in words (`No registration`, `No claimant recorded`) rather than left blank.
- `note` is a short muted business sentence about the record's current condition (what it is waiting on); it is not a subtitle.
- Pass `accent={false}` only when the record has no stage; every case has one.

**Examples**

```tsx
<Record state="review">
  <RecordHead
    reference="CE-2026-01432"
    identity={[<b>AXA</b>, 'LM19 KXR', 'J. Okafor', 'Total loss']}
    end={<StatusChip state="Review" />}
  />
  …
</Record>

<RecordHead
  reference="YD68 TFA"
  identity={[<b>Triage</b>, 'Opened 14 Aug 08:52', 'Unassigned']}
  end={<StatusChip state="Awaiting information" />}
  note="Waiting on the repairer's images before a finding can be recorded."
/>
```
