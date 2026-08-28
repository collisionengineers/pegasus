# Checklist — TICK-058

- [x] Settle and govern the exact route/auth/media/idempotency/response/error wire contract (FRD-09 § Accepted API-01 submission contract).
- [x] Integrate TICK-061 and compose authentication with the first real endpoint (`PegasusProviderApi` scheme, `Features:ProviderApi` gate).
- [x] Translate to the existing grouped intake owner with Principal/client attribution (`SubmitProviderInstruction` → `IGroupedIntakeSubmission`, `ActorKind.Provider`).
- [x] Preserve durability, replay/conflict, limits, pause/revoke, custody, and disclosure-safe failures.
- [x] Reuse existing Azure resources; application-level throttling per key id (60/min default; live values parked).
- [x] Prove no processing-status vocabulary of its own, no general lookup, no file/report response, no outbound delivery (result reuses `QueuedIntakeStatusKind`/`IntakeDecision`/`IntakeAllocationFailureKind`; GET is per-submission, own Principal only).
- [x] Add Core and SqlServer integration tests (orchestrator runs them in the wave loop).
- [ ] Refresh current-state docs after deployment (DELIV-030 owns) and run simplification plus locked verification.

## Progress notes

- 2026-08-28: merged `origin/task/tick-061-provider-credentials` into the branch (PR #592 not yet on dev at start); built on it. Slice 1 e56bb469 (Core/Infra/Web/migration), slice 2 (classification/allocation binding, FRD-09, tests). Build green; `Test-MigrationGrants.ps1` and `Test-AzureDeploymentPlan.ps1 -Mode Local` pass. No `dotnet test` run here by instruction.
