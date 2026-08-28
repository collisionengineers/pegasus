---
kind: review-attestation
pr: "576"
head_sha: "46a21f926753506291d9e8668cb5dd7894b60674"
verdict: pass
reviewer: "claude-fable-5 independent reviewer (kanmer-review, 2026-08-27)"
independent: true
plan_hash: "020380b92374b3cd"
ticket_updated: "2026-08-27T17:10:03.029Z"
findings:
  - id: R1
    severity: minor
    summary: "docs/operations.md release-19 correction has one 90-column prose line (convention: hard-wrap near 78)"
    disposition: accepted-risk
    reason: "Living-snapshot prose, no CI gate on width, meaning unaffected; the release that provisions the cap refreshes this paragraph and rewraps it. Reviewer does not edit code."
  - id: R2
    severity: note
    summary: "First CI run 33096961779: documentation job cancelled in actions/checkout after the 10-minute timeout (stale merge-ref hang, scripts never ran); sql-integration (1) failed 1/314 on VehicleLookupGapFillTests post-login connection timeout, unrelated to the diff"
    disposition: fixed
    reason: "Both docs scripts pass locally on the PR head (exit 0). PR closed/reopened once to force a fresh merge ref; run 33098647645 at the same head is green on every job."
  - id: R3
    severity: minor
    summary: "Codex inline P2 (docs/operations.md:724): docs/current-architecture.md lines 163-170 still attribute the telemetry outage and alert blindness to the workspace quota, the diagnosis this PR corrects"
    disposition: accepted-risk
    reason: "Same surface as plan simplification finding 9. current-architecture.md is the as-built snapshot; its cap value (0.1 GB) is still true, and CLAUDE.md requires the release that provisions the declared 0.5 GB cap to refresh current-architecture.md and operations.md in the same task, where the diagnosis sentence is corrected. operations.md now carries the corrected record in the release-19 paragraph. Non-blocking; not fixed in this PR because the reviewer does not edit code."
---

# MAIL-020 — review of PR #576

## Change reviewed

Head `46a21f926753506291d9e8668cb5dd7894b60674`, base `dev`, four files:

- `src/Pegasus.Worker/SqlDependencyTelemetryFilter.cs` — new sealed
  `ITelemetryProcessor`; drops only `DependencyTelemetry { Type: "SQL",
  Success: true }`. Failed SQL (`Success == false`), unknown-success SQL
  (`null`), HTTP dependencies, requests, exceptions and traces reach `next`.
- `src/Pegasus.Worker/Program.cs` — registered through the SDK's own
  `AddApplicationInsightsTelemetryProcessor<T>()` in the Worker composition
  root: a named production caller, no parallel mechanism.
- `infra/modules/platform.bicep` — `var telemetryDailyCapGb = json('0.5')`
  bound to `workspaceCapping.dailyQuotaGb` and to a
  `Microsoft.Insights/components/pricingPlans@2017-10-01` child `current`
  (`planType: 'Basic'`, `warningThreshold: 90`,
  `stopSendNotificationWhenHitCap: false`).
- `docs/operations.md` — release-19 paragraph corrected (component cap,
  00:00Z reset, was the limit hit); Monitoring/cost bullet states declared
  0.5 GB with live caps still 0.1 GB until provisioned or operator-approved
  `az` update.

## Acceptance checks (reviewer-run)

- `az bicep build --file infra/main.bicep --stdout` on the PR worktree: exit
  0; compiled ARM shows `pricingPlans`, `planType: Basic`, `dailyQuotaGb`
  bound to the variable.
- `scripts/Test-AzureDeploymentPlan.ps1` asserts only adaptive sampling on
  `platform.bicep`; unaffected, and the CI `infrastructure` job is green.
- `scripts/Test-TestMarkdownPlacement.ps1` and
  `scripts/Test-DocumentationLinks.ps1` locally on the PR head: exit 0.
- Diff matches `files`, `plan` steps 1–5 and the post-implementation report;
  no packages added; no Azure write performed; every Azure write in the
  runbook text is phrased as requiring operator approval (exact targets
  named). Scope is bounded to the brief; the Web SQL-dependency volume is
  explicitly deferred (plan finding 11).
- Plan simplification pass: 11 findings, each applied, rejected with reason,
  kept with reason, or deferred — honest dispositions.
- Post-implementation report states test evidence honestly: attempt 1
  failed 7/987 under LocalDB contention and was superseded; controller
  serial run Core 1001/1001, Architecture 100/100, Integration 987/987,
  exit 0.
- Group context (EPIC-010): no new feature, file-disjoint lane, cap change
  is plan + approval — all honoured.

## CI

- Run 33096961779 (first): 8 jobs green; `documentation` cancelled in
  checkout at the 10-minute timeout; `sql-integration (1)` 1 failure
  (`VehicleLookupGapFillTests`, SQL post-login timeout, same flaky test
  noted for MAIL-017). Recorded as R2.
- Run 33098647645 (after one close/reopen, same head): all jobs green —
  changes, documentation, local-development-scripts, reference-data, unit,
  infrastructure, browser, sql-integration (1)(2)(3),
  sql-integration-coverage.
- Review threads: one Codex automated COMMENTED review with one inline P2
  (R3). No human threads.

## Merge

Merged 2026-08-27T17:44:20Z via `gh pr merge 576 --merge` into `dev`;
merge commit `14c6fd4155cf3dd63b33049b05caa370d5d9b94d`. Branch retained
for closeout.

## Residual risk

The live caps remain 0.1 GB until the operator approves the `az` updates or
the next `azd provision`; until then the Worker filter reduces volume but a
full day may still hit the cap. `docs/current-architecture.md` and
`docs/open-decisions.md` still state the as-built 0.1 GB and the earlier
workspace-quota diagnosis (R3) and are refreshed by the provisioning
release. The `pricingPlans` PUT is what-if'd at the release's pre-provision
step (plan finding 5).
