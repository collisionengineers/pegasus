---
id: CASE-035
type: ticket
title: >-
  Case Files "Open Operations" link has an unresolvable page name, and no
  snapshot state covers the section
status: backlog
area: case-reference-workflow
order: 150
assignee: ''
profile: fix
labels:
  - ui
  - case
  - deployed-defect
  - test-ui
groups:
  - EPIC-011
links:
  - UIIMP-008
  - CASE-027
  - UIIMP-005
refs:
  - docs/frd/frd-12-operator-experience.md
deployment: production
archived: false
created: '2026-08-30T20:20:03.370Z'
updated: '2026-09-03T15:15:27.206Z'
---

## What

`src/Pegasus.Web/Pages/Cases/Shared/_CaseDocuments.cshtml:40` renders

```html
<a class="btn" asp-page="/Operations">
    <svg class="icon" aria-hidden="true"><use href="#icon-arrow-right" /></svg>
    <span>@OperatorLabels.CaseWorkspace.OpenOperations</span>
</a>
```

`"/Operations"` is **not a Razor page name**. The page's name is
`/Operations/Index`; `@page "/Operations"` sets the *URL template*, not the
name, and `asp-page` resolves names. The anchor therefore renders with no
usable target — the "Open Operations" button on the Case workspace does
nothing.

## Why this matters now

It is **live in production**. It shipped in release 37 and is reachable:

```
Pages/Cases/Details.cshtml:318   -> <partial name="Cases/Shared/_CaseFiles">
Pages/Cases/Shared/_CaseFiles.cshtml:17 -> <partial name="Cases/Shared/_CaseDocuments">
```

rendered when `Model.Section == "case-files"`, i.e.
`/Cases/{id}/Details?section=case-files`.

## This is the third instance of one defect, and the only survivor

[[UIIMP-008]] was **held out of Done** for exactly this defect at two other call
sites. PR #628 ("name the Operations page so its links resolve") fixed
`Pages/Index.cshtml.cs`'s `RecordPage` and `Pages/Intake/Details.cshtml:36`.
This third site was missed. A sweep of every `asp-page` value in the
application against its backing `.cshtml` on `origin/main` at `fb3f07ac`
confirms it is now the **only** unresolvable page name left:

```
for each asp-page value: git cat-file -e origin/main:src/Pegasus.Web/Pages<value>.cshtml
  -> 1 miss: "/Operations"   (this one)
```

The file is [[CASE-027]]'s, which is why it was not fixed under UIIMP-008 —
rule 2, not an oversight in the audit.

## The second half: the gate could not have caught it

**No captured snapshot state covers `?section=case-files`.** The corpus holds
`case-details--default`, `case-details--conflict` and `case-details--unavailable`
only, and none of them renders `_CaseDocuments`. So neither the UIIMP-008 fix
nor [[UIIMP-005]]'s new Test UI CI gate could have detected this, and the same
blind spot covers every other control in that section.

Adding a `case-details--case-files` state is the durable half of this fix. A
one-line link repair leaves the section as untested as it is today.

## Approach

- Change the attribute to `asp-page="/Operations/Index"`, matching
  `_Layout.cshtml:96` and `Intake/Details.cshtml:36`.
- Add a captured state for the `case-files` section to
  `docs/design/test-ui/catalogue.json` and regenerate, per the convention in
  `CLAUDE.md`.
- Consider whether an assertion should pin that **every** `asp-page` in the
  application resolves to a real page — this defect class has now recurred three
  times and a static census catches it in one pass. That would belong beside the
  catalogue check rather than in a page test.

## Verification

- [ ] The Case Files "Open Operations" button navigates to `/Operations`
- [ ] A `case-files` snapshot state exists and the catalogue check passes
- [ ] No `asp-page` value in the application lacks a backing page
