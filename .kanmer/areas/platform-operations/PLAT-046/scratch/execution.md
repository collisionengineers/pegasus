## Operator decision — 8 September 2026

Destructive database migrations must be identified during planning. For these migrations, temporarily stop both old Web and Worker while changing the database and accept a short planned outage. The project is not released yet; a post-release migration must be scheduled outside typical usage hours. This supersedes choosing a transient old-version error window for the destructive cutover. Prepare and implement the bounded release procedure under this ticket; no live outage, Azure change, migration or promotion is authorized by this planning decision. Retain unchanged-schema/additive supported paths only where genuinely safe, and do not introduce compatibility or preservation machinery for disposable pre-release state.

## Step-1 source freeze — 2026-09-08

Recorded worktree: `.worktrees/plat-046`; branch:
`PLAT-046-destructive-migration-shutdown`; frozen base:
`9ae9db753e3a3ecce1d9735d5c2fbe6fb5b0ff2c`.

Only the authorized step-1 script slice is dirty: `scripts/PegasusPlatform.ps1`,
`scripts/Test-AzureDeploymentPlan.ps1`,
`scripts/Invoke-ProductionSmoke.ps1`, and
`scripts/Test-PegasusPlatform.ps1`. It centralizes the existing seven Worker
Disabled setting names and adds static offline consumer/census contracts.
`git diff --check` passed, with LF-to-CRLF advisories only. No test, build,
script execution, cloud operation, checklist write, commit, push, or PR was run.

Release procedure/ADR work is paused awaiting a supported exact Flex Worker
package-replacement state proof. Conflicting generic and Flex Microsoft guidance
does not establish that a stopped host can receive new bytes without old-code
startup, so no unsupported safety guarantee has been written.

Root disposition of stopped-Flex deployment uncertainty: use researched supported config-zip with exact triggers disabled BEFORE whole-app stop and SQL, not against a stopped app after SQL. Then full disabled census, Worker stop/Stopped plus Web inactive/zero replicas, SQL/grants/head, new Web provisioning with NEW Worker still disabled, explicit activation/smoke. This bounded staging exception modifies old migrate-before-packages wording only for destructive Worker preparation, not permission for early business invocation; master-key/portal invocation forbidden during maintenance. Current research9c3f0e67cf9c3ddc and amended whole plan govern. Author may resume remaining mapped docs/procedure after fresh exact resumed packet. Four-script slice preserved, no host grant/no live approval. No stopped-app deployment support assertion or obsolete-worker restart allowed.

Operator reinforcement on 8 September 2026: 'it needs to include re-enabling it', followed by 'continue'. Root confirmed the procedure is not complete while either host remains stopped/disabled. Required terminal state is approved new Web revision active/healthy plus approved new Worker Running with every expected function enabled and exact full smoke PASS. This explicitly reinforces planned restart/re-enable/read-back, not permission for live Azure operations or old-package fallback.

## Implementation source freeze — 2026-09-08

Added the approved destructive-route documentation and completed the prior canonical-census script slice in the recorded worktree. The release procedure now requires: disabled new Worker staging while the old schema remains intact; full disabled smokes; Worker `Stopped` and exact old-Web inactive/zero-replica evidence before SQL; migration/grants/head; new approved Web with Worker still disabled; explicit Worker re-enable/provision, `Running` read-back, and full smoke. It calls any failed terminal activation an unfinished outage and forbids old-byte revival after destructive SQL.

Changed paths are limited to the ticket packet's eleven expected files: release skill and migration recipe; ADR-0046, ADR-0030 and ADR index; runbook; AGENTS outside the managed block; and the four existing scripts. `git diff --check` passed with LF-to-CRLF advisories only. Static cross-check confirmed the two existing script consumers and direct recipe use `Get-PegasusWorkerDisabledSettingNames`; the obsolete unresolved-containment sentence is absent. No test, build, script execution, cloud operation, checklist write, commit, push, or PR was run because no host-verifier or live-operation grant exists.

Inputs are frozen for the named verifier. Retain every result; do not weaken contracts or execute live examples.

Root static inspection of first untested source freeze found bounded correctness gaps before granting verification: rewritten skill lost the ordinary additive/no-migration path (it incorrectly required observed disabled Worker for all releases); Web containment poll could accept missing inventoried revision/empty revision inventory as inactive; and reactivation tried start for any non-Running value rather than refusing unknown state. Author instructed to preserve original normal route, validate exact approved revision/mode/boolean/list shape and only start observed Stopped. These are existing plan requirements, not new scope. First freeze and static finding retained; author may correct in same mapped files/claim. No host command or live action was run.
