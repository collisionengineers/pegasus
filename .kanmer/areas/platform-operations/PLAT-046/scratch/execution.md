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
