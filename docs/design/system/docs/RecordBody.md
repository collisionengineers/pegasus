---
category: Record
---

The record container's content area (`.record__body`, 20/24px padding on the panel ground). It is the last part of `Record` — after `RecordBar`, or after `Tabs` when the sections are alternatives — and holds the section content: a `Facts` block, `DataRow`s, `DetailList`s, tables and panels. A record whose sections form a reading order gets a body and no tab row.

**Rules**

- One body per record; sections inside it are ordered by what the operator reads first (identity facts, then field/value rows, then evidence).
- Start with `Facts` for the identity columns and follow with `DataRow`s for fields that carry provenance or an action; do not repeat a fact in both.
- Do not nest another `Record`, page heading or lede inside the body; sibling panels below the record are for material that is not about this record.
- Body content is business language: values, office times (`12 Aug 09:14`), `Not recorded` — never processing terms.

**Examples**

```tsx
<Record state="review">
  <RecordHead reference="CE-2026-01432" identity={[<b>AXA</b>, 'LM19 KXR']} end={<StatusChip state="Review" />} />
  <RecordBar end={<Button variant="dark">Export</Button>}><Button>Actions</Button></RecordBar>
  <RecordBody>
    <Facts groups={[{ title: 'Vehicle', items: [{ term: 'Registration', value: 'LM19 KXR' }] }]} />
    <DataRow field="Accident date" value="6 Aug 2026" end={<Provenance word="Extracted" />} />
    <DataRow field="Pre-accident value" suggested="£14,250" end={<Provenance word="AI" />} />
  </RecordBody>
</Record>
```
