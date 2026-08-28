# Checklist — TICK-058

- [x] Settle and govern the exact route/auth/media/idempotency/response/error wire contract (FRD-09 § Accepted API-01 submission contract).
- [x] Integrate TICK-061 and compose authentication with the first real endpoint (`PegasusProviderApi` scheme, `Features:ProviderApi` gate).
- [x] Translate to the existing grouped intake owner with Principal/client attribution (`SubmitProviderInstruction` → `IGroupedIntakeSubmission`, `ActorKind.Provider`).
- [x] Preserve durability, replay/conflict, limits, pause/revoke, custody, and disclosure-safe failures.
- [x] Reuse existing Azure resources; application-level throttling per key id (60/min default; live values parked).
- [x] Prove no processing-status vocabulary of its own, no general lookup, no file/report response, no outbound delivery (result reuses `QueuedIntakeStatusKind`/`IntakeDecision`/`IntakeAllocationFailureKind`; GET is per-submission, own Principal only).
- [x] Add Core and SqlServer integration tests (orchestrator runs them in the wave loop).
- [x] Fix the three CI failures at their roots (fake winner-row keying, uncomposed-surface 404 gate, moving clock for history order) and run the simplification pass over the branch diff, recorded in the plan.
- [ ] Orchestrator-run wave tests green on PR #594 and locked verification; DELIV-030 owns the post-deploy current-state docs refresh, then `proof/proof.md` on merged main.

## Progress notes

- 2026-08-28: merged `origin/task/tick-061-provider-credentials` into the branch (PR #592 not yet on dev at start); built on it. Slice 1 e56bb469 (Core/Infra/Web/migration), slice 2 (classification/allocation binding, FRD-09, tests). Build green; `Test-MigrationGrants.ps1` and `Test-AzureDeploymentPlan.ps1 -Mode Local` pass. No `dotnet test` run here by instruction.
- 2026-08-28 (CI fixes): failure 1 was a test bug — the concurrent-insert fake filed its
  winner row under a fresh Guid key while giving the record a different Id, so the
  assertion compared a throwaway key against the resolved submission; the fake now keys
  the row by its own Id. Failure 2 was a production bug — with the surface not composed,
  the static-assets fallback's GET/HEAD-only `{**path:file}` catch-all turned the POST
  into 405 (reproduced locally: 405 + `Allow: GET, HEAD`); Program.cs now answers 404
  under `/api/provider/v1` before routing, the file's established absence-gate pattern;
  verified locally flag-off 404 and flag-on 401 unchanged. Failure 3 was a test bug —
  the fixture's fixed clock pins all three history rows to one `OccurredAtUtc`, so the
  `OrderBy(OccurredAtUtc)` read a tie; that test's host now composes
  `TimeProvider.System` (suite precedent) and orders by the history's own time.
  Locked restore/build green (Release). Commits aeb123f9, acf97d41, d4d66347 pushed;
  simplification pass recorded in the plan. Wave tests left to the orchestrator.
