# Independent review — PR #554 / DELIV-023 — 2026-08-26

## Changes

- `Invoke-ProductionSmoke.ps1` adds `-ActivationOnly` only to the mandatory `WorkerOnly` parameter set, refuses an empty AzureWebJobs inventory, and bypasses only the exact-name comparison. It still requires every returned setting value to match the expected enabled/disabled value.
- `Test-AzureDeploymentPlan.ps1` passes that switch only from `PreProvision` and statically asserts that exact census remains the smoke default.
- Runbook and both release-skill copies describe the same pre/post distinction.

## Findings

1. **BLOCKING — post-deployment smoke does not enforce the one-minute recovery schedule required by the ticket and its plan proof.** `Invoke-ProductionSmoke.ps1` queries only `AzureWebJobs.*` disabled settings. It never reads `PendingWorkRecoverySchedule`, and neither the release skill’s strict smoke command nor `Test-AzureDeploymentPlan.ps1` asserts the live value `0 * * * * *`. The Bicep source contains that value, but source/template evidence is not deployed-state smoke. Therefore acceptance “Post-deployment smoke still requires the exact nine names declared by the release and the one-minute recovery schedule” is only half satisfied.

2. **BLOCKING — “at least one activation setting” is implemented as “at least one arbitrary AzureWebJobs-prefixed setting.”** Activation-only queries every name beginning `AzureWebJobs.` and merely requires a non-empty result. It does not require a `.Disabled` activation setting. A lone unrelated `AzureWebJobs.*` setting with value `false` could pass enabled pre-provision even when the activation census is absent. Filter or validate the returned names as disabled activation settings before applying the uniform-value check.

## Required review answers

- **Did the plan miss anything implied by the ticket?** Its Proof section names the one-minute post-deploy schedule, but the ordered implementation steps and test detail omit the live schedule read/assertion. That gap propagated into the diff.
- **Did implementation miss anything in the plan?** Yes: the plan’s Proof requires post-deploy smoke to pass only with the new exact census **and one-minute schedule**; only the census is checked.
- **Did the simplification pass run honestly?** Mostly. Its reuse/scope claims match the five-file diff, exact census remains the default, empty result and mixed values fail, and both skill copies are synchronized. The claim “activation-only” is slightly broader than reality because arbitrary `AzureWebJobs.*` names qualify; that is captured as finding 2.
- **Is weakening limited to pre-provision while strict post-deploy census remains?** Yes for function-name census: only `PreProvision` passes `-ActivationOnly`, and normal smoke still performs exact ordinal nine-name comparison. But strict post-deploy **schedule** validation never existed and remains missing.

## Verdict

**FAIL / NEEDS CHANGES.** Do not merge. Add a live post-deployment assertion for `PendingWorkRecoverySchedule = '0 * * * * *'`, add focused distinction tests/static guards for it, and make activation-only prove that the non-empty inventory consists of actual `.Disabled` activation settings. Release 32 and current-state documentation remain honestly listed as post-merge work and are not claimed by this PR.

# Independent re-review — remediation c2c4bcc4 — 2026-08-26

## Disposition

- **Activation inventory blocker resolved.** The Azure query now includes only names beginning `AzureWebJobs.` and ending `.Disabled`, so the non-empty inventory is genuinely a function activation inventory. Uniform expected values still apply.
- **Schedule blocker not fully resolved.** The new live `PendingWorkRecoverySchedule` read is nested inside `if ($WorkerOnly)`. The canonical release skill’s post-deployment smoke invokes the default `WebAndWorker` parameter set (BaseUri/SHA/version) without `-WorkerOnly`, so it skips the schedule read entirely. That means the actual full post-deployment smoke can still pass with a wrong/missing recovery schedule. The static regex check only proves the schedule code exists somewhere; it does not prove the normal smoke path executes it.

## Verdict

**FAIL / NEEDS CHANGES.** Move the strict schedule assertion onto every non-`ActivationOnly` smoke path (including the default WebAndWorker post-deployment command), while keeping it skipped only for activation-only pre-provision. Then add a static/behavioral guard that ties the schedule assertion to `-not $ActivationOnly`, not merely to the presence of the schedule string.

No implementation edit or merge was performed.
