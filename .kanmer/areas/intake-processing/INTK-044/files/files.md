# Files — INTK-044

| Path | Role | Change |
| --- | --- | --- |
| `src/Pegasus.Core/Intake/IntakeAllocation.cs` | `Classify` (default arm → `Unexpected`/`Blocked`), `IntakeAllocationState.CanRetry`, `RetryAsync` | Default arm becomes `Unexpected`/`ReloadThenRetry`; `SequenceExhausted` stays `Blocked` |
| `src/Pegasus.Infrastructure/Persistence/EfCaseAcceptanceStore.cs` | Acceptance transaction (`Serializable`), 3-attempt loop, `IsRetryableConcurrencyFailure` | Root-cause fix: unwrap every inner exception (EF's `InvalidOperationException` transient wrapper hid the 1205) — the `EfIntakeReceiptStore` convention |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeAllocationStore.cs` | Persists attempts; `BeginAsync` already admits a `reload_then_retry` StaffRetry | Found by the reproduction: `BeginAsync` `Serializable` range locks deadlock across *different* receipts; dropped to read-committed — the per-receipt applock and the two unique indexes are the guard |
| `src/Pegasus.Web/Pages/Intake/Details.cshtml(.cs)` | Failure panel + `RetryAllocation` handler keyed on `CanRetry` | Read only — the existing form renders once `CanRetry` is true; no copy added |
| `src/Pegasus.Web/Pages/Cases/Create.cshtml.cs` | `ValidateAuditCannotBeManuallyCreated` | Read only (FRD-02: Audit is created only by the retained-email route) |
| `tests/Pegasus.Core.Tests/Intake/AllocateDefinitiveIntakeTests.cs` | Taxonomy tests | Assert new disposition; add "unexpected automatic Audit failure is retried with the same command" |
| `tests/Pegasus.IntegrationTests/QdosAllocationRecoveryTests.cs` | Taxonomy + `AllocationTestData` + `ThrowingAcceptIntake`/`CapturingLogger` | Update `Blocked` expectation; add concurrent same-principal audit+inspection reproduction; add `SeedAutomaticAuditEvidenceAsync`; `ThrowingAcceptIntake` made internal top-level |
| `tests/Pegasus.IntegrationTests/Browser/QdosAllocationRecoveryBrowserTests.cs` | Retry route browser proof | Add unexpected automatic-Audit failure → retry → `a.` case |
| `tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs` | Private `SeedAutomaticAuditEvidenceAsync` | Replaced by the shared `AllocationTestData` helper |
| `docs/frd/frd-02-intake-and-source-identity.md` | Governing FRD | Read only |
