# Post-implementation report — ENG-025

Branch `task/eng-025-assessment-shell`, worktree
`../pegasus-worktrees/eng-025-assessment-shell`, PR
[#616](https://github.com/collisionengineers/pegasus/pull/616) → `dev`.
Merged `origin/dev` @ `9868cf58`.

## Reconciliation note

The pipeline documents were written before this session; the work landed
across several sessions and the last of it preceded this report. This
report is written retroactively against the branch as pushed, and it names
where the branch departed from the plan rather than restating the plan.

## What shipped

| Commit | What |
| --- | --- |
| `7b919b69` | Core D11 access policy + policy tests |
| `36655f26` | The `assessment-v3` page: gate surface, ribbon, record bar, evidence rail, Estimates pane, dialogs |
| `065c18ef` | Web, browser and Send-to-AI suites retargeted |
| `8315b2f7` | `origin/dev` merge (brought ENG-026's Core estimates) |
| `5611f316` | **Out of scope** — the multi-estimate editor (see below) |
| `bc16d8fa` | Revert of `5611f316` |
| `93766579` | `origin/dev` @ `9868cf58` merge |
| `c9e90360` | Page comments name ENG-028 as the editor's ticket |
| `d5dd2c3f` | One `main` landmark |
| `22dd1870` | Four test assertions corrected against merged `dev` |
| `5d3b658c` | Report-draft controls conditioned on the Current estimate |

## Departures from the plan

1. **Scope split.** `5611f316`, subject-labelled "(ENG-026)", added the
   multi-estimate editor — estimate tabs, per-estimate editor
   (Delete/Duplicate/Use estimate/Save), "New estimate", import name and
   source. That is wave-4 ENG-028 ("Assessment: multi-estimate editor and
   the Send to Claude job dialog"); this ticket's body and step 0 of the
   plan both put the editor in wave 4, and ENG-026 owns Core estimates, not
   the page. Reverted whole in `bc16d8fa` — the shell needs none of it: the
   Estimates pane still renders the accepted/draft specification, lines,
   basis, the Engineer acceptance and the empty state, and the focused
   suite is green without it. `5611f316` stays reachable; the work is
   carried on `task/eng-028-estimate-editor` (`6b4d11db`, pushed, no PR),
   with its state recorded on ENG-028's `scratch/salvaged-editor.md`.
2. **Tests were run.** The plan said build-only. Running the focused
   Assessment/SendToAi filter is what exposed the three defects below; the
   plan's build-only rule would have shipped them.
3. **`AssessmentPersistenceIntegrationTests.cs` was edited** — a file this
   ticket's files map does not list, added to `dev` by ENG-026. The D11
   change is what broke it (three fixtures opened the workspace in Review).
   The retarget is the minimum: the helper sets Report preparation instead
   of Review and is renamed to say so. Every assertion in those tests is
   unchanged.

## Defects found and fixed

1. **Two `main` landmarks** — the Estimates pane was a `<main>` nested in
   the shell's. axe: `landmark-no-duplicate-main`,
   `landmark-main-is-top-level`. Now a `<section>`, following CASE-012's
   `8603f945`.
2. **Report-draft controls could never enable** — `OnGetAsync` prepared
   readiness with `costs: null` and no estimate, so ENG-026's
   `Current estimate required` fired for every case, disabling Generate and
   Preview report draft even where generation would have succeeded. It now
   passes `AcceptedSpecification` (the `IsCurrent` specification) as the
   Current estimate — the same inputs `EfAssessmentReportProjectionSource`
   hands `Project`, so the control's condition and the generation decision
   cannot disagree.
3. **Four wrong assertions**, all written before the `origin/dev` merge and
   never run. None weakened:
   - the switched-off condition is read off the Send to Claude control
     instead of the page's first `data-condition` (which is the Import
     estimate seam's);
   - Core's refusal sentence is compared against the decoded page, because
     Razor encodes the apostrophe in "Engineer's";
   - the readiness reason is taken from
     `AssessmentReportProjection.RepairCostRequirement` rather than a
     literal ENG-026 renamed (`bcee2ae2`, "Repair cost figures" →
     "Current estimate required");
   - the three `AssessmentPersistenceIntegrationTests` access fixtures
     start at Report preparation (D11).

## Verification

Windows, PowerShell 7, in the task worktree.

```
dotnet build ./Pegasus.slnx --configuration Release
  → Build succeeded, 0 warnings, 0 errors, exit 0

dotnet test ./Pegasus.slnx --configuration Release --no-build \
  --filter "FullyQualifiedName~Assessment|FullyQualifiedName~SendToAi"
  → Pegasus.Core.Tests        Failed 0, Passed 88, Total 88
  → Pegasus.IntegrationTests  Failed 0, Passed 49, Total 49
     (includes AssessmentReadinessSummaryBrowserTests, axe-clean)
  → exit 0
```

The full suite, the Browser category as a whole, and the snapshot and
catalogue scripts are the orchestrator's gates and were not run.

## Ticket verification items

- **"No inert control remains; access matches FRD-11" — met.** All thirteen
  `type="button"` elements on the page resolve: two open dialogs, five are
  conditioned or approved disabled seams (Import estimate when refused,
  Glass's and Audatex per D7/EXT-09, Send to Claude when refused, Generate
  report draft when not ready), one is the rail toggle, one the dropzone
  browse, four close dialogs — every one bound to a `site.js` module or
  stating its condition. Forms post to real handlers (`ClaimLease`,
  `ReleaseLease`, `AcceptSpecification`, `ImportEstimate`, `SendToClaude`,
  `GenerateReportDraft`) and `PreviewReportDraft` is a GET handler. Access
  matches FRD-11/D11 in Core, with the policy theory rows proving it.
  `Suggestions.cshtml` is untouched and still carries no `@page`
  directive, so no route activates.
- **"No clipped text/overflow at 1580/1100/760" — NOT proven.** The one
  browser test on this page runs at 1920×1080. The three-width walk needs a
  browser run this lane does not own; it is left for the orchestrator.

## Reported, not fixed (other lanes)

- `RepairSpecificationSourceRoute.Json` and `.AiDraft` (added by ENG-026)
  have no arm in `OperatorLabels.RepairSpecificationRoute`, so they render
  as "recorded before source tracking". The labels exist on
  `task/eng-028-estimate-editor`; on `dev` the gap is open. ENG-026 or
  ENG-028 owns it — not fixed here.
- `5611f316` also introduced a UTF-8 BOM on
  `Presentation/OperatorLabels.cs`, a file four wave-2 lanes share. The
  revert removed it here and the salvage branch was stripped of it, so
  neither branch carries it.
