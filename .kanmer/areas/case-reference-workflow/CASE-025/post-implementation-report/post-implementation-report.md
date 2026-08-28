# Post-implementation report — CASE-025 (2026-08-28)

PR: https://github.com/collisionengineers/pegasus/pull/596 (to `dev`,
open — stopped there per lane rules). Branch
`task/case-025-cases-queues` at `c56b5d5b`.

## What shipped

- `Pages/Cases/Index.cshtml(.cs)` — the §1.4 three-pane queue: rail
  (Workflow / Pre-Case work / Exceptions with icon wells and queried
  counts), Principal + Not-ready-only Missing filters (exclusive
  options) with Clear, per-kind rows (full-row links to the record's
  detail), quick detail with compact stepper, Outstanding requirements
  and Current work; D14 blocked-intake rows listed uncounted in the
  Unidentified scope.
- Core/Infra (recovered from 95f69958, kept): completeness projection
  on `CaseSearchItem`, `CaseStageCounts.Complete`, EF count/projection.
- `OperatorLabels.TriageState` + `CaseRequirements` (the label map is
  the one owner; two compile-forced one-liners in Intake/Triage detail
  files delegate to it).
- `TriageQueuesWebTests` rewritten to the contract (7 tests).

## Verification actually run

- `dotnet restore ./Pegasus.slnx --locked-mode` — clean.
- `dotnet build ./Pegasus.slnx -c Release --no-restore` — 0 errors
  (compiler tier; lane rule forbids running tests/snapshots here).
- Read-only checks recorded in research (routes, contracts, CSS
  vocabulary, sprite icons, wave-3 non-merge).

## Deviations / disclosures

1. Principal select renders on Case scopes only, not Triage/Unidentified
   (FRD-12 "every queue" read literally would draw an inert control).
2. Image rows show the file count but no custody line: no persisted
   custody projection exists for image-intake rows (out-of-scope note
   in plan; FRD-12 names both).
3. `sort=` parameter removed (no sort control in the §1.4 design; old
   bookmark links degrade to newest-first rather than erroring).
4. Neighbour-lane one-liners: `Pages/Intake/Details.cshtml` (95f69958)
   and `Pages/Triage/Details.cshtml.cs` (this pass) — both
   compile-forced by the `StateLabel` move to OperatorLabels.
5. Rows are links straight to the record's detail (FRD-12 "a row links
   to its detail and nothing else"); quick-detail selection follows
   `?selected=` (first row default) rather than prototype-only click
   selection, so every state works without script.

## Recovery audit verdict on 95f69958

Kept: Core/Infra counts+projection, OperatorLabels additions, page-model
skeleton (rail, queue/state loading, D14 merge, redirects). Repaired:
image-row nonsense expression, inclusive Missing semantics, quick-detail
string surgery, filter retention per current (not target) scope,
leftover sort plumbing. Removed: nothing wholesale. Detail in research.

## Left for the wave loop / reviewer

- Run the test suite + browser walk (1580/1100/760) and snapshot regen.
- Independent review of the PR diff and the plan's dispositions.
