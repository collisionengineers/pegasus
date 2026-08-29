---
id: PLAT-061
type: ticket
title: '.gated::after paints an empty tooltip pill when data-condition is absent'
status: backlog
area: platform-operations
assignee: ''
profile: fix
labels:
  - ui
  - design-system
  - css
groups:
  - EPIC-011
links:
  - INTK-046
  - PLAT-029
docs_todo: true
archived: false
created: '2026-08-29T08:30:06.977Z'
updated: '2026-08-29T08:30:06.977Z'
---

## What

`src/Pegasus.Web/wwwroot/css/site.css:1893-1911` defines the gated-control
tooltip with no attribute guard:

```
.gated { position: relative; display: inline-flex; }
.gated::after { ... padding: 3px var(--sp-2); background: var(--band);
                content: attr(data-condition); opacity: 0; ... }
.gated:hover::after, .gated:focus-within::after { opacity: 1; }
```

`grep -n "gated" site.css` returns only lines 1893, 1895, 1911 and 1961 — there
is no `[data-condition]` selector anywhere. So when the gating condition is met
and Razor emits no `data-condition` attribute, `attr()` resolves to the empty
string but the pseudo-element keeps its padding and `--band` background: hovering
the **enabled** control paints a small empty dark pill.

## Where it bites

Every `.gated` wrapper whose `data-condition` is conditional. Confirmed:

- `src/Pegasus.Web/Pages/Cases/Details.cshtml:269` — `@(Model.CanOpenAssessment ? null : "…")` (merged).
- `src/Pegasus.Web/Pages/Triage/Details.cshtml:198` — `@(canComplete ? null : "…")` ([[INTK-046]]).

`Pages/Cases/Assessment/Index.cshtml:765` passes a constant string, so it is not
affected.

## Fix shape

Guard the pseudo-element on the attribute, e.g. scope the `::after` rules (and
the forced-colours case at 1961) to `.gated[data-condition]`. One rule, one list;
no page changes needed.

## Why this ticket and not a lane fix

`site.css` is [[PLAT-029]]'s file. INTK-046 reported it rather than editing it,
per EPIC-011's "report what belongs to another ticket; do not fix it".

## Secondary note (same area, decide together)

`.gated:focus-within::after` never fires for the disabled case: the disabled
control is a real `<button disabled>`, so it is not focusable and the condition
is hover-only for keyboard users. Same shape at
`Pages/Cases/Assessment/Index.cshtml:766`, so it is established convention rather
than a lane defect — but it is an accessibility gap worth settling with the guard
fix.

## Verification

- [ ] Hovering an enabled `.gated` control paints nothing.
- [ ] Hovering a disabled `.gated` control still shows its condition.
- [ ] Forced-colours case still applies to the guarded selector.
