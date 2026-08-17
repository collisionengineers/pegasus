---
category: Layout
---

Two equal columns for side-by-side review: `.review-grid` is `grid-template-columns: 1fr 1fr` with the section gap, collapsing to a single column under 1280px. Use it when the operator compares two things at once — the result beside what is still missing, an original beside a proposal — each column a `Panel`.

**Rules**

- Exactly two children, each a `Panel` starting with a `SectionLabel`; more than two belongs in `DashboardGrid` or a table.
- Give the two columns parallel content (result / missing, before / after); a list beside the form that adds to it is `SplitMain`, not this.
- Under 1280px the columns stack in DOM order, so put the column the operator needs first, first.
- Wide component: render at page width (wrap at ~960px if it stands alone).

**Examples**

```tsx
<ReviewGrid>
  <Panel>
    <SectionLabel>Result</SectionLabel>
    <DataRow field="Registration" value="LM19 KXR" end={<Provenance word="Extracted" />} />
  </Panel>
  <Panel>
    <SectionLabel>Missing fields</SectionLabel>
    <BlockerList>
      <Blocker title="Accident date">Enter the date on the Instruction tab.</Blocker>
    </BlockerList>
  </Panel>
</ReviewGrid>
```
