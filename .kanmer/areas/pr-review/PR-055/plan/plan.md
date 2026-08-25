# Plan — PR-055: Make EVA export replay atomic under concurrent same-key requests

## Approach

Keep the existing Export operation and exact-replay record, but serialize only its short database-recording section using Pegasus's established SQL Server pattern: a serializable transaction plus `UPDLOCK, HOLDLOCK` on the existing case workflow row before the replay lookup. This makes a second same-case request wait and then observe the first committed history row. It is smaller than adding a table, schema migration, generic idempotency service or retry system, and it leaves archive construction and image reads outside the lock.

## Governing docs

- **Meets — `docs/frd/frd-07-eva-and-external-engineering-handoff.md`:** Step 1 makes the required per-export action history replay-safe at the database boundary while preserving the FRD's once-per-case `First sent to Engineer` proxy and non-mutating Export behavior. Step 2 proves identical replay produces one history record and conflicting reuse is rejected. The governing behavior does not change, so this ticket does not modify the FRD or require an ADR.

## Steps

1. In `src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs`, open a serializable transaction at the start of `RecordExportAsync`, acquire the existing case workflow row with SQL Server `UPDLOCK, HOLDLOCK` before querying `ActionHistory`, and commit on both verified-replay and successful-save paths. Keep bundle generation/image loading outside the transaction; reuse `DocumentActionHistory.RequireExactReplay` and the current proxy/history entities without changing schemas or callers.
2. In `tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs`, exercise two overlapping `ExecuteAsync` calls using the same case, actor and operation key after the first-send proxy exists. Assert both return the same successful bundle and only one matching `eva_bundle_exported` history row is added; then reuse that key with a different actor and assert the existing exact-replay conflict.
3. Review the focused diff for scope and simplicity, then run the Release build and the focused SQL integration test. Record the changed files, test results and any honest limitations in `post-implementation-report`.

## Verification

Run from the ticket worktree:

```powershell
$env:MSBUILDDISABLENODEREUSE = '1'
dotnet build --configuration Release --no-restore --disable-build-servers
dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --disable-build-servers --filter "FullyQualifiedName~CustodyOutboxIntegrationTests.ExportingACaseProducesTheEvaFormatArchive"
```

The focused SQL test is the acceptance evidence: simultaneous identical exports both succeed, exactly one history row exists for the shared key, and materially different reuse fails. `kanmer-verify` will rerun the relevant evidence on the merged branch and write `proof.md`.

## Risks / open questions

- **Lock breadth:** locking by case serializes only the short export record write for that case; archive/image work remains outside the transaction so unrelated cases and slow content reads are unaffected.
- **Replay path transaction:** every early replay return must commit (or safely dispose) its transaction; the focused test exercises this path.
- **Provider behavior:** `UPDLOCK, HOLDLOCK` is SQL Server-specific, matching production. Non-SQL test providers skip the explicit hint, while the required regression runs against the repository's SQL integration harness.
- No open questions. This is a behavior-preserving correctness fix for the already-authorized FRD requirement.
