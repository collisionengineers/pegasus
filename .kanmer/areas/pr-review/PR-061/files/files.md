# Files — PR-061

- `src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs` — return and validate the workflow state read under the existing UPDLOCK/HOLDLOCK transaction.
- `tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs` — deterministic SQL ordering test proving a demotion committed before Export obtains the recording lock fails closed without history/proxy.

Context: FRD-07 owns Review readiness; `CaseNotInReviewException` and `CaseLifecycleState` already exist. No other file is required.
