---
category: Record
---

A two-column list of evidence facts (`.evidence-list`): each `<li>` is a grid of a bold 10rem term (`<strong>`) and a plain value (`<span>`), with 8px between items. Use it where a record shows what was seen and when — evidence linked to a Triage response, decision evidence on a receipt, or the conflicting values a file offered — as a light list rather than a table.

**Rules**

- The term is the anchor the operator scans by: an office time (`14 Aug 08:52`), the conflicting value itself, or the outcome word; the value is the reason or where it came from.
- Values are business language (`Extracted from Repairer estimate`); never engine names, versions or dispositions.
- Three to eight items; an empty list is an `EmptyState` sentence, not an empty `<ul>`.
- Items carry no actions; a dismiss or link action belongs in a `DataRow` or a `ButtonRow` under the list.

**Examples**

```tsx
<EvidenceList
  items={[
    { term: '14 Aug 08:52', value: 'Evidence 1041: repairer images received, four angles' },
    { term: '14 Aug 11:20', value: 'Evidence 1042: engineer noted nearside sill deformation' },
    { term: '15 Aug 09:05', value: 'Evidence 1043: claimant confirmed vehicle is off the road' },
  ]}
/>
```
