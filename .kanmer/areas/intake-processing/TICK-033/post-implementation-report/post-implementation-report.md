# Post-implementation report — TICK-033

## Summary

Reconciled INT-31’s capability-inventory boundary statement with the implemented request-scoped upload caller. The inventory now accurately records that the superseded Box File Request UI and persistence path are removed in source, while retaining the distinction between implementation, deployment, and operator acceptance.

## Changes

| File | Change | Why |
|---|---|---|
| `docs/capabilities.md` | Replaced “UI removal pending” with the verified source-state fact and explicit deployment/acceptance boundary. | Commit `f43e3a2b` removed the predecessor path; the former wording was stale and could mislead delivery tracking. |

## Governing docs

- `docs/frd/frd-02-intake-and-source-identity.md`: no behavioural contract changed. The existing Core-owned link policy, authenticated staff controls and isolated `/Uploads/{token}` caller remain the implementation of the FRD’s requirements.
- No ADR or new governing document was needed: this is a documentation reconciliation with no architectural or product decision.

## Risks / follow-ups

- The targeted integration test commands for `CaseDetailsWebTests` and `DocumentCustodyDurabilityTests` exceeded the local two-minute timeout before a result. This is recorded as a local environment limitation, not a passing test.
- Local Release build and the request-upload Core contract suite passed. CI must provide the integration verdict before merge.
- No live activation, production custody test, browser acceptance exercise, cloud mutation, or operator acceptance was performed. Those remain separately approved work.

## Verification hand-off

On the PR commit and after merge, run:

- `dotnet build --configuration Release --no-restore` — expect success with no warnings/errors.
- `dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~QdosBoundaryContractTests"` — expect 7 passing tests, including revoked-link rejection and exact-operation replay.
- CI / a prepared integration environment: `dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --filter "FullyQualifiedName~CaseDetailsWebTests|FullyQualifiedName~DocumentCustodyDurabilityTests"` — required to replace the local timeout with an actual result.
