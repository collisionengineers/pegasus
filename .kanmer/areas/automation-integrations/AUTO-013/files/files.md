# Files — AUTO-013

## Changed

| Path | Change | Risk |
| --- | --- | --- |
| `src/Pegasus.Infrastructure/Persistence/CaseDataSnapshotFactory.cs` | `AddProviderFact` takes the `CaseAcceptanceRequest` and writes a work provider row for a declared instruction, guarded to the automatic path | Writing it on the staff path would export a provenance no credential supplied |
| `src/Pegasus.Core/ProviderApi/ProviderSubmission.cs` | The paused-credential check no longer runs here | Moving a refusal must not change its status code |
| `src/Pegasus.Web/ProviderApi/ProviderApiEndpoints.cs` | `MaySubmit` now runs before the body is read | A check that moved but still sits after a read is not fixed |
| `docs/frd/frd-09-provider-and-intermediary-routes.md` | Records that the Work Provider is written from the submission binding | Must not overstate: only the automatic path |

## Tests

| Path | Change |
| --- | --- |
| `tests/Pegasus.IntegrationTests/ProviderApiCaseDataSnapshotPersistenceTests.cs` | New. Real persistence: the automatic path records the fact; the staff path does not |
| `tests/Pegasus.IntegrationTests/ProviderApiSubmissionTests.cs` | A paused credential is refused before the body is parsed |
| `tests/Pegasus.IntegrationTests/CaseDataCompletenessPersistenceTests.cs` | Completeness still holds with the new field present |
| `tests/Pegasus.Core.Tests/ProviderApi/ProviderSubmissionTests.cs` | Core-side refusal ordering |

## Notably NOT changed

No migration. `CaseDataCodes.ProviderApi` already exists, is already parsed by
`EfCaseDataStore.ParseSourceKind`, already rendered by
`OperatorLabels.Provenance`, and already inside the
`CK_CaseDataFields_SourceKind` check constraint — so the new row needs no schema
change and no grant.

The ticket's stated path for `CaseDataSnapshotFactory` was wrong: it is in
`src/Pegasus.Infrastructure/Persistence/`, not `src/Pegasus.Core/Cases/`.

`SubmitProviderInstruction.ExecuteAsync`'s four-write ordering is untouched —
that is [[AUTO-012]], worked in parallel in a separate worktree.
