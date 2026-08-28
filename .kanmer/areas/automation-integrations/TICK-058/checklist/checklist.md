# Checklist — TICK-058

- [x] Settle and govern the exact route/auth/media/idempotency/response/error wire contract (FRD-09 § Accepted API-01 submission contract) — **rewritten 2026-08-28 for the declared JSON instruction**.
- [x] Integrate TICK-061 and compose authentication with the first real endpoint (`PegasusProviderApi` scheme, `Features:ProviderApi` gate).
- [x] Accept a declared instruction rather than reading one back out of documents; one submission is one receipt, files are its attachments (`SubmitProviderInstruction` → `IIntakeSubmission`, `ProviderApiIntakeSourceReader`, `ActorKind.Provider`).
- [x] Substitute the declared assessment at one point in `ProcessIntake`; leave allocation, Triage creation, custody, action history and the Worker path unchanged.
- [x] Map the wire vocabulary onto the domain: `auditreport` → Inspection + Audit, `triage` → a Triage with no Case/PO, declared verdict derives `a.`/`ap.`.
- [x] Preserve durability, replay/conflict, limits, pause/revoke, custody, and disclosure-safe failures; refuse a body naming another Principal (403, recorded).
- [x] Reuse existing Azure resources; application-level throttling per key id (60/min default; live values parked).
- [x] Prove no processing-status vocabulary of its own, no general lookup, no file/report response, no outbound delivery.
- [x] Add Core and SqlServer integration tests.
- [x] Amend FRD-01 (Audit verdict), FRD-03 (Triage may begin from a provider submission), FRD-09 (contract), `docs/capabilities.md` (API-01 note).
- [x] Simplification pass over the branch diff, findings and dispositions recorded in the plan.
- [x] Locked restore, Release build, Core and Architecture suites green.
- [ ] Full `Category!=Corpus` run clean, including the integration project under parallel load.
- [ ] Operator to confirm the envelope bound and whether `operator-notes.md` records the declared-verdict ruling.
- [ ] Refresh current-state docs after deployment (DELIV-030 owns).

## Progress notes

- 2026-08-28 (first pass): merged `origin/task/tick-061-provider-credentials`; slice 1 e56bb469, slice 2 a5af5fd9. Document-only contract.
- 2026-08-28 (rewrite): operator replaced the contract with a declared JSON instruction — the document-only shape could not create a case for any Principal without an extraction policy, and had no caller. Commits 2804ebb6 (implementation) and 387f5e26 (simplification pass).
- Evidence: `dotnet restore --locked-mode` and `build -c Release` succeeded; `Pegasus.Core.Tests` 1118/1118; `Pegasus.ArchitectureTests` 100/100; `ProviderApiSubmissionTests` 8/8 against SQL; `Test-MigrationGrants.ps1` and `Test-MarkdownPlacement.ps1` pass.
- Two defects fixed on the way: the duplicated `IntakeEvidenceSource` code maps, and the scaffolded migration re-adding a merged-in column (follow-up **DELIV-032**).
- One pre-existing flaky assertion fixed in this file: the "wrong secret" was built as `secret[..^1] + "A"`, which is the *same* secret whenever the issued one already ends in `A` — about one run in sixty-four, and the likely cause of the single integration failure seen under the full parallel run.
