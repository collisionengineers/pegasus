# Checklist — TICK-058

The contract this checklist described was superseded on 2026-08-28. The items
below describe the **declared JSON instruction** that replaced it.

- [x] Settle and govern the exact route/auth/media/idempotency/response/error wire contract (FRD-09 § Accepted API-01 submission contract).
- [x] Replace the document-only multipart contract with a declared JSON instruction carrying its files inline, and record why (the extraction-driven shape could not create a case for any Principal without an extraction policy — every provider the route exists for).
- [x] Integrate TICK-061 and compose authentication with the first real endpoint (`PegasusProviderApi` scheme, `Features:ProviderApi` gate).
- [x] One submission is one intake receipt: retain the request exactly as it arrived and carry the submitted files as that receipt's attachments (`SubmitProviderInstruction` → `IIntakeSubmission`, `ActorKind.Provider`).
- [x] One substitution, not a second pipeline: `ProcessIntake.AssessAsync` returns a declared assessment for the `provider_api` channel and never routes, classifies or extracts; allocation, Triage creation, custody, action history and the durable Worker path are unchanged.
- [x] Preserve durability, replay/conflict, limits, pause/revoke, custody, and disclosure-safe failures.
- [x] Reuse existing Azure resources; application-level throttling per key id (60/min default; live values parked).
- [x] Prove no processing-status vocabulary of its own, no general lookup, no file/report response, no outbound delivery (result reuses `QueuedIntakeStatusKind`/`IntakeDecision`/`IntakeAllocationFailureKind`; GET is per-submission, own Principal only).
- [x] Add Core and SqlServer integration tests (orchestrator runs them in the wave loop).
- [x] Third migration `20260828185508_ProviderDeclaredInstruction` recorded in the committed-schema assertion.
- [x] Push the branch so the recorded commits are reachable and the PR head is the real implementation (AGENTS.md rule 17).
- [x] Fix the confirmed read-back defect: `EfCaseDataStore` could not parse the `provider_api` origin channel, so a provider-created case threw on every case-data read. Covered by a test that reads a provider-created case's snapshot back.
- [x] Merge `origin/dev` (9868cf58) and prove the generated model snapshot survived the textual merge.
- [x] Restore the UTF-8 BOM that commit 2804ebb6 stripped from `docs/capabilities.md`.
- [x] Record dispositions for all 25 codex findings under a dated heading in the plan (AGENTS.md rule 22), including the missing simplification-pass entry for 387f5e26.
- [ ] **Independent review of the rewritten contract.** No review — codex or human — has run against the current head; all 25 findings predate the rewrite. Required by AGENTS.md step 5 before merge.
- [ ] **Resolve the confirmed live P1s before activation:** the missing UPDATE grant on `ProviderSubmissions`, the pre-authentication rate-limit partition, and the non-atomic `Accepted` history write. See the plan's dispositions.
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
- 2026-08-28 (reachability, defect, merge): four commits (2804ebb6, 387f5e26,
  f021095e, ae35c34d) existed only in the worktree, so the ticket's recorded SHAs
  were unreachable and the PR still showed the superseded multipart contract.
  Pushed as a plain fast-forward (`ba3a0e92..ae35c34d`); the PR head is now the
  real implementation. Fixed the `EfCaseDataStore` provider-channel read-back
  defect by delegating to `EfIntakeReceiptStore.ParseSourceChannel` rather than
  adding a fifth copy of the vocabulary, and proved the new test fails without
  it (`Unknown persisted intake source channel 'provider_api'`). Merged
  `origin/dev` 9868cf58; the one conflict was the migration-name list, resolved
  by inserting `20260828112103_NamedEstimates` in timestamp order. Regenerated
  the EF model snapshot afterwards: the probe migration's `Up`/`Down` were empty
  and the regenerated file was byte-identical to the auto-merged one, so the
  textual merge of the generated file is semantically correct. Restored the
  stripped BOM on `docs/capabilities.md`. Build succeeded (0 warnings, 0 errors);
  `FullyQualifiedName~ProviderApi` 17/17 Core + 9/9 integration;
  `FullyQualifiedName~IntakePersistenceIntegrationTests` 10/10. Not merged: the
  rewritten contract has never been reviewed.
