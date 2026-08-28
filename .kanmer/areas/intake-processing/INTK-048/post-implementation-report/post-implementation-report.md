# Post-implementation report — INTK-048

## Summary

Manual Case associations now count as real destinations when the existing
Unidentified reconciliation owner evaluates a receipt. A receipt can retain its
immutable `NeedsSorting` processing decision while its effective
`CurrentCaseId` causes the stale U-item to resolve to the Instruction Case.
Focused Core and real-SQL coverage reproduce the U38/U39 shape and prove the
association, Case event, resolution history, and replay behavior.

## Changes

| File | Change | Why |
| --- | --- | --- |
| `src/Pegasus.Core/Intake/ReconcileUnidentifiedDestinations.cs` | Reuses `CurrentCaseId` and `CurrentCaseReference` before original-decision eligibility. | A staff link is an effective Case destination without rewriting the source processing outcome. |
| `tests/Pegasus.Core.Tests/Intake/ReconcileUnidentifiedDestinationsTests.cs` | Adds the manually linked, still-eligible receipt mapping regression. | Pins the minimal Core rule and QDOS reference mapping. |
| `tests/Pegasus.IntegrationTests/UnidentifiedReconciliationTests.cs` | Drives the production upload-decision/link path and verifies persisted association, Case event, resolution history, and replay. | Proves the live failure shape against real SQL and checks reconciliation preserves the committed link evidence. |

## Governing docs

The change meets `docs/frd/frd-02-intake-and-source-identity.md`: an open
Unidentified item whose receipt reaches a formal Case is automatically resolved
with the destination recorded in history, while material with no real
destination remains open. The FRD already described this behavior and was not
changed.

## Risks / follow-ups

- No schema, DI, timer, API, or operator-surface changes.
- The first canonical test run hit the existing [[DELIV-031]] SQL post-login
  timeout in an unrelated Due Chaser test (Integration 1,102/1,103). That test
  passed 1/1 in isolation; the final canonical rerun passed Integration
  1,103/1,103. Both results are retained.
- Production U38/U39 repair requires an approved deployment. No live write or
  manual SQL repair was performed by this ticket.

## Verification hand-off

At commit `14e0ad6f522a8b39c735f31535e842d8b0738fc8`:

- `dotnet restore ./Pegasus.slnx --locked-mode` — exit 0.
- Release build — exit 0, 0 warnings, 0 errors.
- Core reconciliation tests — 9/9 passed.
- SQL `UnidentifiedReconciliationTests` — 3/3 passed.
- Previously failing Due Chaser test — 1/1 passed in isolation.
- Final canonical non-Corpus run — Core 1,096/1,096, Architecture 100/100,
  Integration 1,103/1,103; exit 0.

Post-merge verification should rerun the focused Core and SQL reconciliation
tests at the merged SHA. After a separately approved production release, verify
U38 and U39 resolve to QDOS26030 while their active manual associations and
`intake_case_linked` events remain intact.
