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
