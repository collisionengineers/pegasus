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

## Correction freeze — 2026-09-08

Static review found and corrected three procedure gaps before verification: the ordinary unchanged/additive route is again explicit (provision approved Web/Worker, then Worker ZIP and full smoke); destructive containment binds to the approved old Web revision and rejects malformed/missing rows, revision-mode drift, any active revision, or non-empty replicas; and activation starts the Worker only when the observed state is exactly Stopped, rejecting unknown states. Migration classification, affected capability, forward-only gap, and outage-window record now occur in preflight before promotion, artifact build, live-write approval, or Azure mutation. The runbook Worker-disabled statement is scoped to destructive containment, and ADR-0046 metadata records its partial ADR-0030 supersession.

Git diff check passed again with LF-to-CRLF advisories only. No test, build, script execution, cloud operation, checklist write, commit, push, or PR was run. Inputs are re-frozen for the named verifier.

Independent pre-publication static audit by /root/agent_config_review on unchanged11path freeze (skillSHA2564291e080de6dad3fa6c4e946d80f532a7102cf0e64ff72b67091fa6af4d65083; diffhash4e7a8b7df302ed8d127816630709dbbec6d22f6d) found three MAJOR issues: (1) 'follow every remaining section' conflicts with normal/destructive route branches; (2) post-provision exact active/healthy Web revision/digest proof missing/weak across normal, disabled-first and activation provisions; (3) immediate pre-SQL fresh containment readback is prose only, allowing stale WorkerStopped from before Web drain. Root authorizes one bounded same-file correction batch: explicit route skips, one small reusable inline strict post-provision Web readback called at all three boundaries, executable fresh Worker/Web containment check immediately before SQL. Native exits/JSON shapes/expected revision/digest must failclosed. Worker re-enable/Running/exactallfalse otherwise reviewed sound. This is a static audit, not formal PR attestation or host test PASS. No live/test/script commands occurred.

## Independent audit correction freeze — 2026-09-08

Applied the three bounded findings without changing the approved file set. Explicit routing now sends unchanged/additive releases through normal provision then Worker ZIP/smoke, and destructive releases through stage/contain, migration, disabled new-Web provision and explicit activation. A reusable in-skill Web revision read-back now runs after every relevant provision. It requires a valid full revision array, the project double-hyphen revision identity, one expected active revision, exact approved image, and Healthy/Running/Provisioned state. Known readiness states use bounded polling; malformed, unexpected, terminal, or unknown state fails closed. A fresh pre-SQL read-back now checks Worker Stopped, Web Single mode, exact old inactive revision with no active revisions, and valid empty replica array immediately before migration.

Primary Microsoft documentation was consulted for Container Apps revision health, running and provisioning properties. Project Bicep and current operations evidence establish the exact deployed double-hyphen revision convention. Git diff check passed with LF-to-CRLF advisories only. No host script, test, build, cloud operation, commit, push, or PR was run. Inputs are frozen for verifier handoff.

## Commit freeze — 2026-09-08

Corrected the release-skill helper fence and the section-1 route wording. After exact eleven-file scope verification and git diff check with LF-to-CRLF advisories only, committed the frozen implementation locally as bbae334ca33c1f89617dfe458d8d7ac45dff24a0, Document destructive migration runtime shutdown. The recorded branch PLAT-046-destructive-migration-shutdown is clean. No push, PR, host script, test, build, or cloud operation was run. This exact local head is ready for verifier binding.

## Static correction dispositions — 2026-09-08

Independent `/root/agent_config_review` completed bounded delta review at clean commit `bbae334ca33c1f89617dfe458d8d7ac45dff24a0`: PASS, no remaining material findings. All three classes fixed by that commit: mutually-exclusive route selection (including section 1), one fenced exact-name/digest healthy Web readback helper at three provision boundaries with bounded pending and fail-closed unknown handling, and executable fresh pre-SQL Worker Stopped plus old Web inactive/zero replicas/no active revisions containment. Double-hyphen revision name matches Bicep. Root also caught and author fixed missing helper fence before commit. This is preimplementation-handoff static evidence, not a formal Kanmer PR attestation or postmerge proof. Sole verifier is running only the scoped offline queue under DELIV-053/scratch/execution; no live operations.

## Transitions

- 2026-09-08T18:25:47.531Z lease-phase implementing → running-command (lease f52130e7-d7ab-40ac-aa69-edb643bda825 rev 5; expires 2026-09-08T18:55:47.518Z)

## Sole-host verification attempt — stopped on first failure — 2026-09-08

Canonical grant: DELIV-053 `scratch/execution` version `33c59faa785d673c`.
Ready PLAT-046 execution packet: ticket revision `rev1:2d08604afca6b66b`; plan `6dc328062a78eda0`; checklist `cde62a0c45158a1f`; files `d8b67e022854fd51`.
Frozen base/head: `9ae9db753e3a3ecce1d9735d5c2fbe6fb5b0ff2c` / `bbae334ca33c1f89617dfe458d8d7ac45dff24a0`.
Recorded worktree/branch: `.worktrees/plat-046` / `PLAT-046-destructive-migration-shutdown`.

Preflight at 2026-09-08T18:25:42.0295735Z exited 0: exact worktree root and common repository, exact branch/head, clean status, base is an ancestor, exactly the packet's eleven changed paths, distinct active-ticket worktree ownership, and zero dotnet/MSBuild/testhost/vstest processes. Lease `f52130e7-d7ab-40ac-aa69-edb643bda825` renewed to revision 5 in `running-command` phase before execution.

Sequential commands:

1. 2026-09-08T18:26:00.2490218Z–2026-09-08T18:26:01.2686877Z — `pwsh -NoProfile -File ./scripts/Test-PegasusPlatform.ps1` — exit 0.
   Output: `Release workstation and manifest contract passed (win-x64); Windows/Linux mappings checked.` and `Pegasus platform LocalDB state classification passed.`
2. 2026-09-08T18:26:08.7763664Z–2026-09-08T18:26:09.7411358Z — `pwsh -NoProfile -File ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local` — exit 1.
   Output: `Test-AzureDeploymentPlan.ps1: Cannot bind argument to parameter 'Actual' because it is an empty array.`

Disposition: **FAIL / stopped at the first genuine failure**. No retry, diagnosis, source correction, or inferred PASS. The granted documentation-links, Markdown-placement, and all-PowerShell-fence parse checks were not started. At 2026-09-08T18:26:30.8723382Z the exact HEAD remained clean and zero heavy processes remained.

No dotnet, cloud, Azure, recipe execution, SQL, browser, source edit, commit, push, or live operation occurred. PLAT-046 remains Implementing for primary disposition.

- 2026-09-08T18:27:31.925Z lease-phase running-command → implementing (lease f52130e7-d7ab-40ac-aa69-edb643bda825 rev 6; expires 2026-09-08T18:57:31.916Z)

## Failure diagnosis / bounded correction authorization — 2026-09-08

Root statically located first Local failure: Test-AzureDeploymentPlan lines362–372 still regex-extracts literal AzureWebJobs.Disabled names from production smoke after smoke correctly moved to the canonical helper. This obsolete parser returns empty Actual; parameter binding fails. Author authorized to reconcile that affected assertion with smoke's canonical helper assignment while retaining exact Bicep/producer census and every live missing/extra/duplicate/value failure check. Do not allow empty census or restore duplicate runtime name lists. No test or live rerun authorized until corrected local commit freezes and a fresh canonical host grant. The bbae334 failure remains genuine retained evidence; platform PASS remains its exact-head result only.

## Offline verification failure and correction — 2026-09-08

The canonical host verifier recorded a platform PASS followed by Test-AzureDeploymentPlan Local failure on frozen commit bbae334ca33c1f89617dfe458d8d7ac45dff24a0. The failure is retained: Test-AzureDeploymentPlan still regex-extracted literal AzureWebJobs Disabled names from Invoke-ProductionSmoke after smoke moved to the canonical producer, so its extracted census was empty.

Root authorized the bounded affected-consumer correction. Test-AzureDeploymentPlan now asserts that production smoke assigns expectedWorkerSettings from Get-PegasusWorkerDisabledSettingNames, while retaining the existing smoke assertions for live settings read, activation value mapping, ordinal name set, WorkerOnly, ActivationOnly, default exact census, disabled-setting query and recovery-timer behavior. Its Bicep source and compiled-template exact censuses remain unchanged. No empty-census allowance or duplicate list was added.

Git diff check passed with an LF-to-CRLF advisory. Committed the one-file correction locally as 7e5aff7cf9c2bb69710ec962c4b5e18ded9fde08, Keep deployment plan smoke census canonical. Branch is clean. No rerun, host command, cloud operation, push, or PR was performed; this exact head requires fresh verifier binding.

- 2026-09-08T18:31:13.712Z lease-phase implementing → running-command (lease f52130e7-d7ab-40ac-aa69-edb643bda825 rev 7; expires 2026-09-08T19:01:13.703Z)

## Corrected-head sole-host verification attempt — parser invocation stopped — 2026-09-08

Canonical re-grant: DELIV-053 `scratch/execution` version `9647ea38643ea120`.
Ready packet: ticket revision `rev1:1dd2d374bb6421aa`; unchanged plan/checklist/files versions `6dc328062a78eda0` / `cde62a0c45158a1f` / `d8b67e022854fd51`.
Exact base/head: `9ae9db753e3a3ecce1d9735d5c2fbe6fb5b0ff2c` / `7e5aff7cf9c2bb69710ec962c4b5e18ded9fde08`.

Preflight at 2026-09-08T18:31:08.1871315Z exited 0: exact clean worktree/common repository/branch/head; base ancestor; exactly eleven scoped paths; distinct active worktrees; zero dotnet/MSBuild/testhost/vstest processes. Lease renewed to revision 7 in running-command phase.

Sequential results:

1. 2026-09-08T18:31:23.0904723Z–2026-09-08T18:31:24.1829861Z — `pwsh -NoProfile -File ./scripts/Test-PegasusPlatform.ps1` — exit 0. Release workstation/manifest and LocalDB classification passed.
2. 2026-09-08T18:31:31.7929840Z–2026-09-08T18:31:41.0457546Z — `pwsh -NoProfile -File ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local` — exit 0. Local deployment-plan validation passed; Bicep emitted only its available-upgrade warning.
3. 2026-09-08T18:31:48.1255391Z–2026-09-08T18:31:50.0158811Z — `pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1` — exit 0. All relative Markdown links resolved, 141 files checked.
4. 2026-09-08T18:31:58.9855541Z–2026-09-08T18:31:59.8943552Z — `pwsh -NoProfile -File ./scripts/Test-MarkdownPlacement.ps1 -Base 9ae9db753e3a3ecce1d9735d5c2fbe6fb5b0ff2c -Head 7e5aff7cf9c2bb69710ec962c4b5e18ded9fde08` — exit 0.
5. 2026-09-08T18:33:02.3666581Z–2026-09-08T18:33:03.0475087Z — ignored bounded PowerShell-fence parser harness invocation — exit 1 before any fence parse. PowerShell passed both comma-separated path values as one native argument, and `Resolve-Path` could not find the combined literal `./.agents/skills/pegasus-release/SKILL.md,./.agents/skills/pegasus-release/references/database-migration.md`. No recipe content executed.
   Harness SHA-256 `1A176843649694551EE115D7474F6735E860A5E9B7812A27E65AECFD3F348910`; release skill `9A180D5EDE0D944F3990B9488F81B5BA8F0A87A4062621B581C065C605D4C789`; migration recipe `B48BB3D81CFA97D14FC038AA1C626A7F94FC3024B6E47A4E477E41B4BF650DEA`.

Disposition: repository script/document checks PASS; fence-parse obligation remains **INCONCLUSIVE / NOT RUN** because of the verifier invocation failure. Per stop/no-autonomous-retry, the invocation was not corrected or retried. The ignored harness was removed. Postcheck at 2026-09-08T18:33:29.5019317Z confirmed exact clean HEAD, harness absent, and zero heavy processes. The earlier bbae334 Local failure remains retained and is not erased by the corrected-head PASS.

No product fix, dotnet, browser, cloud, recipe execution, SQL, commit, push, PR, or live action occurred.

- 2026-09-08T18:34:41.102Z lease-phase running-command → implementing (lease f52130e7-d7ab-40ac-aa69-edb643bda825 rev 8; expires 2026-09-08T19:04:41.093Z)
