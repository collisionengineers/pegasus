# Proof — TICK-017 (DOC-01): Automatic Box case-folder creation using the Case/PO name

Replaces the prior placeholder ("Operator confirmed"), which was not evidence.
Local caller-proof evidence below. The live Box target / migration / deployment
/ operator acceptance tier is NOT covered here — see "Not covered".

## What was verified

The four locally-provable boundary behaviours are implemented and green at the
local caller-proof tier, exercised through the real
acceptance-enqueue → worker → `EfQueuedCustodyProcessor` → `CreateCaseRootAsync`
caller against Box fakes and real LocalDB:

- **Immutable Case/PO naming** — folder name IS the reference
  (`CaseFolderName = CustodyNames.SafeName(reference)`, `BoxCaseCustody.cs:886`)
  with an immutable `pegasus-case-binding.json` byte-verified on reuse.
- **Response-loss-safe binding** — predeclared creation-owner token +
  `.pegasus-create-{token}` staging + ETag-guarded promotion; lost create/upload
  responses reconcile without duplication (`GetOrCreateBoundFolderAsync`
  :645-731).
- **Fail-closed conflict handling** — occupied name, duplicate child, wrong
  type, trashed / outside approved root all throw with zero mutation and no
  background retry.
- **Human reasoned recovery** — `RetryCaseCustody.ExecuteAsync`
  (`CustodyContracts.cs:329-379`): staff actor + `PerformCasework` + non-blank
  reason + edit-lease + version CAS; `CustodyRetryPolicy.Decide` only re-arms
  `failed` work.
- **Behaviours confirmed by named passing tests**: immutable naming
  (`ProductionBoxCustodyTests.ExactBusinessHierarchyBindsCaseSourceDocuments…`);
  response-loss reconcile
  (`...TerminationAndLostResponsesReconcileOnlyPredeclared…`); fail-closed zero
  mutation (`...WrongBindingTypeBytesAndAncestryFailClosedWithoutMutation`,
  `...BoxFailureRemainsVisibleToTheCallerWithoutBackgroundRetry`); reasoned
  staff recovery taxonomy
  (`CustodyOutboxIntegrationTests.CancellationSqlFaultAndLeaseLossUseExactTaxonomyAndRequireStaffRecovery`,
  `...ReasonedRetryReplayConflictConcurrencyAndSecondFailureHaveExactCounts`);
  Box adapter resolves from the production composition with no network call
  (`ProductionCompositionTests`).

**Reference-format handoff from INT-25 (blocks edge) confirmed.**
`CustodyNames.SafeName` (`CustodyNames.cs:16-31`) treats only
`" < > | : * ? \ /` and control chars as invalid, and rejects only
empty/`.`/`..`/trailing-dot/`>120 chars`. The dot is NOT invalid, so INT-25's
minted forms all pass: `QDOS25007` (case folder), and `a.QDOS25007` /
`ap.QDOS25007` (audit reference folder via
`CustodyNames.SafeName(auditReference)`, `BoxCaseCustody.cs:615`) — dot
preserved, ~9–12 chars, no reserved-name or trailing-dot risk.

## Evidence

Environment: .NET SDK 10.0.303, Windows, SQL Server Express LocalDB
(`MSSQLLocalDB`). Worktree `pegasus-worktrees/int-25-doc-01-planning` on branch
`task/int-25-doc-01-planning` from `origin/dev` (no source changes). Release
build: `Build succeeded. 0 Warning(s) 0 Error(s)` (00:00:51.47).

Focused custody suites (`ProductionBoxCustodyTests`,
`CustodyOutboxIntegrationTests`, `ProductionCompositionTests`):
```
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build \
  --filter "(Category!=Corpus&Category!=Browser)&(FullyQualifiedName~ProductionBoxCustodyTests|FullyQualifiedName~CustodyOutboxIntegrationTests|FullyQualifiedName~ProductionCompositionTests)"

Passed!  - Failed:     0, Passed:    34, Skipped:     0, Total:    34, Duration: 1 m 32 s - Pegasus.IntegrationTests.dll (net10.0)
```

(These 34 are the DOC-01 subset of a combined INT-25+DOC-01 custody/recovery run
that reported `Passed: 56` total; 22 INT-25 + 34 DOC-01 = 56.)

Local caller-proof total for DOC-01: **34 passed, 0 failed**, plus a 0-error
Release build and the reference-format handoff check above.

## Not covered

Everything the activation boundary lists as pending — all `requires-live-approval`:

- **Live controlled Box target proof** — no test or composition ever calls the
  real Box API. Every Box behaviour is verified against a fake `HttpMessageHandler`
  / in-memory `StatefulBox` with a stub bearer. The approved disposable test
  subtree `392761581105` (runbook :754) is documented but NOT wired into any
  composition; only the production root `405543781910` is pinned. `docs/operations.md:96`
  lists "Real custody, permissions, versions, recovery, production target, and
  caller evidence" as the pending tier.
- **Migration / deployment** — no Production-profile host with live Box secrets
  has been deployed and run; local/CI composition resolves
  `LocalCaseCustody`/`UnavailableCaseCustody`.
- **Operator acceptance** — not recorded (the prior "Operator confirmed" line was
  a placeholder, not an acceptance record against a live target).

Because the entire remaining tier is live-approval-gated and cannot run without
explicit per-target approval (CLAUDE.md: "Local alpha work must not mutate … any
Box location"), the ticket holds at `review`, not `done`. A one-off live
create/reconcile smoke against the disposable subtree `392761581105` is possible
ONLY with explicit operator approval naming that target and operation.
