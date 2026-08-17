---
category: Record
---

THE RECORD CONTAINER. A screen about one record is one container, never a vertical stack of sibling panels. It has three parts and only three: `RecordHead` (dark band with the reference, identity facts and the state chip, followed by the 3px stage accent), `RecordBar` (every action valid for the current state, with the record-level commitment right-aligned behind a hairline rule), and either `Tabs` + `RecordBody` or a plain `RecordBody`. Tabs appear when the sections are alternatives; a record whose sections form a reading order gets a body and no tab row.

**Rules**

- Set `state` to the record's stage on the state channel (`review`, `not-ready`, `pending`, `held`, `completed`…); it colours the accent, and the chip in the head carries the same state as text.
- The operator reaches identity, state, available actions and main content without scrolling: keep the head to one line of facts and the bar to the actions that are valid now.
- A disabled action states its condition (`Button` with `condition`) rather than disappearing — removing it would say the action is impossible, which is false when the record will offer it once the condition is met.
- Put a `Crumb` above the record (`Cases / CE-2026-01432`); do not add a page heading or lede.

**Examples**

```tsx
<Crumb parents={[{ label: 'Cases', href: '/Cases' }]} current="CE-2026-01432" />
<Record state="review">
  <RecordHead
    reference="CE-2026-01432"
    identity={[<b>AXA</b>, 'LM19 KXR', 'J. Okafor', 'Total loss']}
    end={<StatusChip state="Review" />}
  />
  <RecordBar end={<Button variant="dark">Export</Button>}>
    <Button>Actions</Button>
  </RecordBar>
  <Tabs label="Case sections" tabs={[
    { label: 'Overview', href: '?tab=overview', current: true },
    { label: 'Evidence', href: '?tab=evidence', count: 7 },
  ]} />
  <RecordBody>…</RecordBody>
</Record>
```
