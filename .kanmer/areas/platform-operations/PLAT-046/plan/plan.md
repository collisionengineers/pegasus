# Plan — PLAT-046: destructive migration runtime shutdown

## Objective
Establish the operator-selected short-outage release route: destructive migration
is recognized during planning and both old runtimes are stopped before SQL changes,
then only compatible approved new packages resume.

## Starting state
Baseline origin/dev 9ae9db753e3a3ecce1d9735d5c2fbe6fb5b0ff2c, isolated untaken fix.
Evidence: `research/research.md`@`6e6613d4c3606741`, `files/files.md`@`d8b67e022854fd51`.
Current release section6 already migrates before provisioning/deploying packages.
Original per-tick schema service plan is superseded by 8 September operator policy.
No live state has been changed/read for this work; no host verification grant exists.
Preserve shared checkout foreign Principal-document changes.

## Governing docs
Modifies linked docs/runbook.md with explicit operator approval of shutdown policy.
New ADR0046 records this architectural/operational decision and partially supersedes
ADR0030's accepted transient old-runtime faults; update minimal old status metadata
and ADR index. Do not rewrite historical observations. Current pre-release principles
do not demand preservation machinery; actual released users/data obligations must be
assessed when they exist. A release is not inferred from alpha infrastructure.
Canonical release skill owns commands; runbook/AGENTS summarize and link.

## Required changes
- Planning (before outage approval or SQL) inspects candidate migrations against
  deployed migration identity and identifies destructive operations/non-compatible
  old-code usage, exact affected capability and forward-only rollback gap.
  Uncertainty blocks the destructive route until resolved. Additive unchanged
  route remains migrate/grants/head then package deployment.
- Record deliberate short Web/Worker outage and in-flight/retry backlog expectations.
  Actual post-release migration must have an approved window outside typical usage
  hours; no invented hours or unrequested live scheduling automation.
  Preparation authorization is never exact live-operation permission.
- Before destructive SQL require fresh intended subscription/resource/app inventory,
  Web Single mode and exactly one active revision and approved exact revision name.
  Refuse unexpected mode/count/revision instead of touching additional targets.
- Worker first: use canonical script-produced Disabled names to directly set every
  setting true on the exact approved Function App; suppress settings response to avoid
  printing secrets. Native command exit must succeed. Run existing full WorkerOnly
  smoke ExpectedWorkerActivation disabled WITHOUT ActivationOnly to verify exact
  names and values. Then az functionapp stop and bounded read-back state Stopped.
  Disabled flags alone do not prove shutdown (master-key REST invocation remains
  possible); prior host state is not assumed to survive configuration/deployment.
- Web: direct az containerapp revision deactivate on inventoried exact revision,
  then bounded polls for that revision inactive and az containerapp replica list
  --revision exact-name returning a valid empty array. Check all native exits and
  JSON shapes; null/missing/failed responses are not zero replicas. Require no
  other active revision appears. Timeout or drift stops before SQL.
  Use simple bounded inline recipe polling, not a new generic service/framework.
  No pre-migration azd provision and no PEGASUS_WEB_ACTIVATION=disabled misuse.
- Recheck both shutdown facts immediately before existing manifest-bound migration
  recipe, then existing runtime grants/bootstrap and exact migration-head check.
  SQL/native failure leaves old runtimes stopped and Worker settings disabled.
- After verified schema, deploy the exact manifest Worker ZIP while its existing
  disabled settings remain true, BEFORE azd provision can rewrite Worker settings.
  Check deployment completion and exact package identity through the existing route;
  verify full disabled census and actual host state. Do not start the old package
  merely to make deployment work; if the supported ZIP route cannot replace bytes
  while stopped, stop/report the deployment limitation before a live run is approved.
  Only approved new Worker bytes are eligible for start after this boundary.
  Then set desired Worker disabled in the azd environment and run PreProvision
  -WorkerActivation disabled -ExpectedLiveWorkerActivation disabled, same approved
  Web digest/revision suffix, azd provision and read new Web.
  Configuration/deployment may restart a host; do not assume stopped state persists.
  Destructive route intentionally deploys Worker before Web provisioning to avoid
  configuration-restarting the incompatible old Worker after SQL.
- Explicit approved activation: desired approved-live-worker; PreProvision with
  observed disabled; provision keeping identical Web digest/suffix, ensure exact
  new Worker Running (explicit approved start if stopped), then full enabled census
  and normal exact-release smoke. Do not treat ZIP success alone as completed release.
- Recovery is forward-only after destructive migration starts. Never revive old
  Web revision or old Worker package against changed/unknown schema. Fail closed
  on package uncertainty, missing grant/head or containment drift; report outage.
  Before SQL, any abort restoration is separately approved exact-target recovery,
  not automatic cleanup. No wipe/reset or alert-suppression authority is added.
- Add Get-PegasusWorkerDisabledSettingNames (or equally clear one canonical name)
  in existing PegasusPlatform.ps1, consumed by Test-AzureDeploymentPlan and full
  Invoke-ProductionSmoke plus direct recipe. Keep Bicep as deployment setting owner,
  existing activation parameters and strict exact census/value semantics unchanged.
  Extend existing offline Test-PegasusPlatform contracts to cover canonical wiring
  and missing/extra/duplicate/wrong values where existing mocked smoke supports it;
  no second test infrastructure or duplicate normative production list.
- Update affected skill/recipe, ADR/index, runbook and AGENTS outside managed block.
  Remove unresolved PLAT046 sentence, stale accepted transient-fault wording and
  obsolete plan policy. No Worker/Web application-code changes or alert retuning.

## Expected files
| Action | Repo-root-relative path | Responsibility |
| --- | --- | --- |
| Modify/Add | .agents/skills/pegasus-release/SKILL.md | Planning classification, exact-target approved shutdown/read-back and disabled-first new deployment/activation. |
| Modify/Add | .agents/skills/pegasus-release/references/database-migration.md | Require proven old-runtime shutdown before destructive SQL, preserve manifest/grants/head route, remove unresolved PLAT046 sentence. |
| Modify/Add | docs/adr/0046-destructive-migration-runtime-shutdown.md | Operator-selected short-outage policy, partial supersession, post-release scheduling, recovery boundary. |
| Modify/Add | docs/adr/0030-non-additive-schema-changes-before-cutover.md | Minimal metadata/status cross-reference replacing accepted transient old-runtime error window; historical body remains contextual. |
| Modify/Add | docs/adr/README.md | ADR0046 catalogue and ADR0030 supersession linkage. |
| Modify/Add | docs/runbook.md | Current migration/rollback guarantee agrees with shutdown and forward-only boundary, no duplicated command recipe. |
| Modify/Add | AGENTS.md | Outside managed block: destructive migrations planned, old runtimes stopped, actual post-release window outside typical usage. |
| Modify/Add | scripts/PegasusPlatform.ps1 | Canonical existing Worker Disabled names producer. |
| Modify/Add | scripts/Test-AzureDeploymentPlan.ps1 | Consume canonical names without weakening exact Bicep/activation gates. |
| Modify/Add | scripts/Invoke-ProductionSmoke.ps1 | Consume canonical names, retain full census and ActivationOnly distinction. |
| Modify/Add | scripts/Test-PegasusPlatform.ps1 | Existing lightweight offline script contracts prove census wiring and relevant fail-closed behavior. |

## Do not modify
- src/**
- infra/**
- corpus/**
- .github/**
- docs/operations.md
- docs/current-architecture.md
- docs/principal-profiles/**
- .agents/skills/pegasus-release/references/troubleshooting.md

## Constraints
Use kanmer-execute and kanmer-docs. Read full current release skill and migration
recipe before editing, no live deployment. No dependencies, schema, feature flags,
runtime readiness polling, new operational script entry point, alert or cloud write.
All scripts/tests/builds run only through sole host verifier with explicit current
canonical host-slot record (primary coordinates). Author does static reads/diffs.
No dotnet restore/build/test for this script/documentation-only change.
The whole ticket packet is the execution boundary, not per-step constrained dispatch.

## Ordered steps
1. Establish canonical Worker Disabled names producer and update its two real
   consumers, preserving strict census/activation behavior; extend offline contracts.
2. Update release planning, shutdown/read-back, migration boundary, disabled-first
   deployment and explicit compatible reactivation recipe with native exit handling.
3. Record ADR0046, minimal ADR0030 supersession linkage, index/runbook and AGENTS;
   cross-check commands and all old-runtime recovery wording for consistency.
4. Freeze inputs; obtain sole-verifier scoped offline script and documentation checks.
   Retain failures and report any contract conflict rather than weakening it.
5. Record report/checklist, commit/push branch, PR to dev; stop for independent review.

## Acceptance checks
- Shared function is used by two current runtime script callers and skill direct-write
  recipe; missing/extra/duplicate settings/wrong values still fail full smoke.
- Static semantic review traces every failure boundary from approval/inventory
  through true stopped/zero-replica evidence, migration/grants/head, new packages
  disabled, explicit activation and full final smoke. Unknown state fails closed.
- Both old runtimes stopped before destructive SQL, not merely unhealthy or
  trigger-disabled; no config/provision restarts old package after SQL changes.
- No acceptance of old-runtime fault window remains authoritative for this path.
  Post-release outside-usage scheduling stated without invented hours.
- No live test claim. Production no-exception guarantee is conditional procedure
  enforcement; exact live evidence belongs to separately authorized release.

## Commands
From recorded ticket worktree, PowerShell7, granted verifier only:
- pwsh ./scripts/Test-PegasusPlatform.ps1
- pwsh ./scripts/Test-MarkdownPlacement.ps1 -Base 9ae9db753e3a3ecce1d9735d5c2fbe6fb5b0ff2c -Head HEAD
- Existing relevant architecture documentation/ADR check only if it can run scoped
  without whole application rebuild; otherwise record static semantic validation
  and exact-head qualifying CI, not invented successful executable evidence.
- git diff --check is lightweight static and may run without host slot.
Native Azure/azd examples are reviewed against Microsoft primary documentation,
NOT executed. No packaging, Bicep provisioning or database test for this ticket.

## Failure and deviation rules
Stop on failed checks, unfamiliar source path, broader permission/config changes,
missing exact-state proof, contradictory current API semantics or need for external
write authority. Keep observed failures and source freeze; no silent redesign.
No stale plan readiness service, alert weakening or old-runtime fallback.

## Stop condition
Scoped implementation/evidence recorded and PR open to configured dev integration,
ready for independent kanmer-review. Do not merge, deploy, start another ticket or
clean foreign work.
