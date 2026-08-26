# Post-implementation report — INTK-042

## Summary

Replaced normal timer-first dispatch with immediate exact-ID publication after the durable receipt or external-work transaction commits. Manual uploads, grouped submissions, mailbox intake, accepted/replacement cases, vehicle requests, and image custody now use the existing outboxes without a broad pending-work scan. The Worker keeps sole processing ownership; its timer is now a one-minute recovery sweep.

## Changes

| Files | Change | Why |
|---|---|---|
| `src/Pegasus.Core/Intake/DurableIntake.cs`, `EfIntakeWorkStore.cs` | Exact receipt claim and immediate publish after `ReceiveIntake` commit. | Covers the shared manual/grouped/mailbox route without waiting for the old timer. |
| `src/Pegasus.Core/Custody/CustodyContracts.cs`, `EfExternalWorkStore.cs` | Exact external-work claim and immediate publisher. | Reuses the existing durable outbox/lease protocol for committed custody and vehicle work. |
| `AcceptIntake.cs`, `CreateLinkedReplacement.cs`, `VehicleWorkflow.cs`, image-intake Core/EF files | Publish existing committed custody/vehicle/image work IDs. | Keeps queue calls outside transactions and preserves truthful successful outcomes on send failure. |
| `src/Pegasus.Infrastructure/Transport/AzureQueueWorkEnqueuers.cs`, Worker composition | Shared Azure Queue identifier senders replace Worker-only copies. | Web and Worker reuse transport adapters while Core retains policy. |
| `src/Pegasus.Web/Program.cs`, `infra/modules/platform.bicep` | Web composes queue senders; production gives it only sender permission on `intake-work` and `external-work`. | Enables direct Web publication without queue receive/delete/processing access. |
| Worker function/configuration and release scripts | `PendingWorkRecoveryFunction` runs every minute; exact activation smoke census follows the renamed function. | Makes the timer explicitly recovery-only and removes the five-second normal-path stage. |
| Core/architecture/test-double updates | Exact dispatch ordering/failure tests plus adapted composition contracts. | Proves no broad scan, enqueue-before-mark, recovery on failed send, and the renamed/shared architecture. |

## Governing docs

- `docs/frd/frd-02-intake-and-source-identity.md`: publication is after the durable commit; queue failure leaves the receipt durable and recoverable; Worker remains the processor.
- `docs/frd/frd-05-documents-extraction-and-custody.md`: custody remains asynchronous and a send fault never undoes immutable case/reference work.
- `docs/adr/0032-near-real-time-durable-intake-triggering.md`: there is one immediate path and one slow recovery sweep, not a second timer-first delivery path.

No current-state operations document was changed: this source/Bicep change is not deployed. DELIV-021 owns deployment, live identity assignment, latency, and cost proof.

## Risks / follow-ups

- The selected SQL integration subset (mailbox/upload/custody/image) stalled without output while other worktrees had active integration hosts. It was interrupted after several minutes; this is not a passing result. Re-run it in a free local SQL/test-host session and in CI before merge.
- Queue send failure is intentionally acknowledged as a durable receipt/case success and released immediately for the one-minute recovery sweep; production telemetry and p95 proof remain [[DELIV-021]].
- Graph mailbox notification and truthful sender/status work remain [[MAIL-013]] and [[INTK-001]] after this prerequisite merges.

## Verification hand-off

Run on the merged commit:

```powershell
dotnet restore
dotnet build Pegasus.slnx --configuration Release --no-restore
dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build
dotnet test tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build
pwsh -NoLogo -NoProfile -File scripts/Test-AzureDeploymentPlan.ps1 -Mode Local -WorkerActivation disabled
dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MailboxIntakeIntegrationTests|FullyQualifiedName~UploadConfirmationWebTests|FullyQualifiedName~CustodyOutboxIntegrationTests|FullyQualifiedName~ImageCaseCustodyIntegrationTests"
```

Expected: build/plan/Core/architecture commands pass; integration output is captured to replace the local-host stall evidence. Before deployment, confirm the Web identity has only the two Storage Queue Data Message Sender assignments, then DELIV-021 measures received-to-complete latency and queue failures.

## Review remediation — 2026-08-26

The first independent review blocked the PR. The branch now:
- makes committed receipt/external publication a required Core dependency rather than a nullable optional service;
- treats a recoverable release failure after a failed send as lease-expiry recovery, preserving the already-committed acknowledgement;
- emits correlated, bounded `Pegasus.Core.Intake` and `Pegasus.Core.Custody` publication activities with identifier, path, and outcome tags;
- adds focused route tests for manual receipt, acceptance, replacement, vehicle request, image registration, image merge, failed release, and a deployment-plan assertion for Web's two queue-scoped Message Sender assignments.

Validation after remediation: Core tests passed **999**; Architecture tests passed **100**; the local Bicep deployment-plan validation passed. The integration subset remains deliberately pending, not passed.

## Browser-CI remediation — 2026-08-26

Browser CI proved that DevelopmentOffline TestServer hosts have no Azurite service: Web mutations waited on local queue transport and timed out. `dfda320d` replaces only the test-host publisher registrations with the existing in-memory publisher double. Production Web/Worker still require and compose their real queue publishers; Core route tests remain the proof of exact post-commit publication. The affected integration project builds successfully. The earlier CI run is invalid for merge and a fresh run is required.
