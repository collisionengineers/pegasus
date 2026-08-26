# Research

## Question

Why did release 32 stop before provisioning, and what is the smallest safe pre-release correction?

## Verified findings

- Production readback showed nine enabled `AzureWebJobs.*.Disabled=false` settings using the release-31 name `PendingWorkDispatchFunction`.
- Release 32 intentionally replaces that timer with `PendingWorkRecoveryFunction` and changes its schedule from five seconds to one minute.
- `Test-AzureDeploymentPlan.ps1 -Mode PreProvision` calls the release-32 `Invoke-ProductionSmoke.ps1 -WorkerOnly`, which requires the release-32 exact name census before provisioning can create it. The check is therefore impossible for an intentional rename.
- Provisioning and Worker ZIP upload are separate, but this pre-release app uses durable mailbox, queue and SQL/outbox state. A short Worker interruption delays work; it does not erase committed intake.

## Decision

Pre-provision should verify only that the currently deployed Worker has discoverable activation settings and that every one matches the expected enabled/disabled value. The normal post-deployment smoke remains strict about the exact nine release names. No compatibility implementation or dual timer is needed.
