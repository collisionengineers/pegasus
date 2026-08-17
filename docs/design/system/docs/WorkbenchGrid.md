---
category: Layout
---

The Engineers assessment workbench: `.workbench-grid` is a 320px sticky readiness rail (`aside`, rendered as `<aside>` first in the DOM) beside a `minmax(0, 2fr)` main column (`children`), each a vertical stack of panels. Under 1280px it becomes one column with the rail on top, so what is outstanding sits above the section being worked on. Use it for the assessment surface only, not as a generic sidebar layout.

**Rules**

- `aside` holds readiness: a `Panel` with a `SectionLabel`, a `BlockerList` of `Blocker`s naming each unmet field and its resolution, and the `SendToClaudeButton` (which stretches to the rail's full width here).
- `children` is the section being edited: `Panel`s with `SectionLabel` and a `FormGrid` of `Field`s; keep the save `ButtonRow` inside the main column.
- Blockers speak business language and name the field and the tab that resolves it; never queue or engine terms.
- Wide component: render at page width; two columns need at least 1280px.

**Examples**

```tsx
<WorkbenchGrid
  aside={
    <Panel>
      <SectionLabel>Outstanding</SectionLabel>
      <BlockerList>
        <Blocker title="Vehicle registration">Enter the registration on the Vehicle tab.</Blocker>
      </BlockerList>
      <SendToClaudeButton />
    </Panel>
  }
>
  <Panel>
    <SectionLabel>Vehicle</SectionLabel>
    <FormGrid>
      <Field label="Registration" htmlFor="reg"><Input id="reg" defaultValue="LM19 KXR" /></Field>
    </FormGrid>
  </Panel>
</WorkbenchGrid>
```
