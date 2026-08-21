# Plan

Committed in `1a86f5db`, corrected in `fef817b8`.

## The operator's fear was unfounded, and saying so is part of the fix

The operator reported an "EVA Handoff is not switched on" warning and asked that it not
block review or export. It never did. Verified by citation, not assumption:

- review is reached in `CompleteCaseCustodyAsync:445-452` with **no** EVA condition;
- `Documents/Export` has **no** EVA reference at all.

`CaseEvaMapping.ActivationGateReason` gates EVA **bundle generation** only, and the panel
rendered it for every editable case. It was pure UI noise. The record should show it was
noise rather than leaving the operator believing a block was removed.

## The change

Hide the panel while the hand-off is switched off. Cases that already have revisions keep
theirs — that history stays visible.

## The correction, and why it matters

The first attempt hid the panel by returning `null` from
`EvaHandoffStore.GetPreparationAsync`. That broke `AutomationAssessmentIngressTests`: the
MCP status tool then reported a case that plainly exists as *"The case was not found"* —
exactly the kind of dishonest signal this batch of work exists to remove. `null` goes back
to meaning **no such case**, and whether the hand-off is switched on is its own fact the
view reads (`HandOffSwitchedOn` / `IsWorthShowing`).

The failing test was the check that caught it.

## Acceptance

- A case with EVA switched off renders no EVA panel. ✅
- A case with existing revisions still shows them. ✅
- The MCP status tool still finds a case that exists. ✅
- Enforcement is unchanged. ✅
- Live: no panel, and review and export both work — Phase 6.

## Simplification pass

2026-08-21. One line in the view, one predicate in Core. The alternative — a null return —
was rejected precisely because it overloaded an existing meaning to carry a second one.
No findings deferred.

## Design check

The change **removes** operator-facing copy, which is the direction
`docs/design/README.md#no-explanatory-copy-and-page-economy` requires, and removes an
empty-state panel from a read-only view, which that section names as a defect.
