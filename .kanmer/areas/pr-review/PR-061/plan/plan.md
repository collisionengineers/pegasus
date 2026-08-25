# Plan — PR-061

## Approach
Reuse the existing short serializable Export transaction and CaseWorkflow row lock. Read the locked row's state and reject anything except Review before replay, proxy or history persistence. Keep package/image work outside the lock and add no abstraction.

## Governing docs
Meets `docs/frd/frd-07-eva-and-external-engineering-handoff.md`: Export succeeds only while the locked current Case state is Review.

## Steps
1. Make the current lock helper return the locked state and fail with `CaseNotInReviewException` when it is absent/not Review.
2. Add deterministic SQL coverage that holds the row lock, starts Export, demotes and commits, then proves failure and zero export records.
3. Run Release build, focused export integration test, diff/simplicity review, and report exact evidence.

## Verification
`dotnet build --configuration Release --no-restore --disable-build-servers`
`dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --disable-build-servers --filter "FullyQualifiedName~CustodyOutboxIntegrationTests.ExportingACaseProducesTheEvaFormatArchive"`

No open questions.
