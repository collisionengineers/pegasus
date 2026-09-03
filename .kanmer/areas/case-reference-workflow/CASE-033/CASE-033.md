---
id: CASE-033
type: ticket
title: >-
  Cases/Workflow and Cases/Closure are bodyless unreachable pages;
  IRecordEngineerFinding has no reachable caller
status: backlog
area: case-reference-workflow
order: 130
assignee: ''
profile: fix
labels:
  - ui
  - case
  - wave-5
  - unreachable
groups:
  - EPIC-011
links:
  - CASE-012
  - UIIMP-009
refs:
  - docs/frd/frd-01-case-identity-and-lifecycle.md
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-08-29T14:00:43.795Z'
updated: '2026-09-03T15:15:27.167Z'
---

## What

`src/Pegasus.Web/Pages/Cases/Workflow.cshtml` and
`src/Pegasus.Web/Pages/Cases/Closure.cshtml` are each **two lines** on merged `dev`
at `b92cb9a7` — an `@page` directive and an `@model` directive, with no body:

```
@page "/Cases/{id:guid}/Workflow"
@model Pegasus.Web.Pages.Cases.WorkflowModel
```

Nothing links to either route: `git grep -n 'asp-page="/Cases/Workflow"'` and the
same for `/Cases/Closure` return no hits anywhere in `src/`.

The consequence is an orphaned Core port. `IRecordEngineerFinding` is declared at
`src/Pegasus.Core/Cases/CaseContracts.cs:398`, implemented at
`src/Pegasus.Infrastructure/Persistence/EfRecordEngineerFinding.cs:15`, registered at
`src/Pegasus.Infrastructure/DependencyInjection.cs:369` — and its **only** consumer
is `src/Pegasus.Web/Pages/Cases/Workflow.cshtml.cs:22`, a page model whose view
renders nothing and which no operator can reach.

Under D20/D21 that is registered-but-unreachable code: recording an engineer finding
is not a delivered capability today.

## Why

`waves.md` assigns `Workflow.*` and `Closure.*` to wave-2 lane E1 (CASE-012). PR #599
left both at base, and the CASE-012 lane-E1 completion (PR #615) delivered
`Create.cshtml` and `Eva/Send.*` but reported these two rather than resolving them,
because the disposition is a design decision rather than a port.

## Approach

Settle which of the two readings is right, then act on it — do not leave the seam.

1. **Either** the lifecycle dialogs CASE-012 shipped on `Cases/Details.cshtml` fully
   subsume both pages, in which case delete `Workflow.*` and `Closure.*`, delete
   `IRecordEngineerFinding` and its implementation and registration if the finding is
   genuinely recorded elsewhere, and hand the deletions to [[UIIMP-009]]'s removal
   list.
2. **Or** recording an engineer finding is a real operator capability with no other
   home, in which case give it a reachable surface per `context.md` §1.8 and link it
   from the Case workspace.

Check first whether any *other* path records an engineer finding — if one does,
option 1 is right and the port is simply dead. `docs/design/test-ui/catalogue.json`
classifies both pages as `redirect`, which the CASE-012 review found to be untrue;
correct that entry either way.

Do not delete anything under option 1 without confirming the capability is not lost —
a capability quietly dropped during a "cleanup" is the failure mode this programme
keeps hitting.

## Verification

- [ ] Neither `/Cases/{id}/Workflow` nor `/Cases/{id}/Closure` is a bodyless routed
      page: each is either removed, or renders its contracted surface and is linked
      from the Case workspace.
- [ ] `IRecordEngineerFinding` either has a reachable production caller, or is removed
      along with its implementation and DI registration.
- [ ] `docs/design/test-ui/catalogue.json` classifies both routes truthfully.
