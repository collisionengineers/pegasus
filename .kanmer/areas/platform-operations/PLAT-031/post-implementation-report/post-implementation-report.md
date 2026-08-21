# Post-implementation report

**Branch:** `task/qdos26008-regressions` · **PR:** #505 · **Commits:** `1a86f5db`, corrected in `fef817b8`

## The finding is as much the deliverable as the code

The operator feared the "EVA hand-off is not switched on" warning was blocking review and
export. **It was not**, and the record now says so with citations rather than leaving them
believing a block was removed:

- review is reached in `CompleteCaseCustodyAsync:445-452` with **no** EVA condition;
- `Documents/Export` contains **no** EVA reference at all.

`ActivationGateReason` gates EVA bundle generation only. It was pure UI noise.

## What was built

`CaseEvaMapping.IsSwitchedOn`, `HandOffSwitchedOn`/`IsWorthShowing` on the preparation, and
the panel gated on `IsWorthShowing` — one line in `_CaseWorkflow.cshtml`. Cases that
already have revisions keep them; that history stays visible. `ActivationGateReason` is
untouched: this changes what is **displayed**, not what is **enforced**.

## The correction, and what caught it

The first implementation hid the panel by returning `null` from
`EvaHandoffStore.GetPreparationAsync`. `AutomationAssessmentIngressTests` failed: the MCP
status tool then reported a case that plainly exists as *"The case was not found."*

That is exactly the kind of dishonest signal this batch of work exists to remove — the same
shape as ENG-010's silent empty MOT list. `null` goes back to meaning **no such case**, and
whether the hand-off is switched on became its own fact the view reads.

Fixed in `fef817b8`. A failing test caught it before it shipped.

## Design compliance

The change **removes** operator-facing copy and removes an empty-state panel from a
read-only view. Both are directions
`docs/design/README.md#no-explanatory-copy-and-page-economy` requires; the second it names
explicitly as a defect.

## Evidence

- `Pegasus.Core.Tests` — 908 passed
- `AutomationAssessmentIngressTests` passing again after the correction
- Full integration suite: recorded before merge
- Live: no EVA panel on a case with the hand-off off, and review **and** export both
  working — Phase 6. Proving export still works matters more than proving the panel is
  gone, because that was the operator's actual worry.
