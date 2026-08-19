# Post-implementation report — INTK-006

## Summary

Implemented the non-blocking INTK-005 branch integration for grouped vehicle-image routing. Grouped receipts are now discovered from their stable child source tokens, all group members are evaluated together, and a single accepted VRM plus one eligible existing Case associates every member. Vision outcomes now retain distinct detector-empty versus recognizer-empty reasons. The authorized Image-Only Case fallback remains deferred to the existing Case owner/governing-document contract; this branch does not invent a principal or Case reference.

## Changes

| File | Change | Why |
|---|---|---|
| `src/Pegasus.Core/Intake/GroupedIntake.cs` | Added parent-group lookup for a processed child source identity. | Lets automation recover the durable upload group on every member replay. |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeSubmissionGroupStore.cs` | Implemented child-token to parent-group lookup. | Reuses INTK-005's persisted membership instead of adding a second grouping store. |
| `src/Pegasus.Core/Intake/IntakeContracts.cs` | Added source-identity lookup to receipt queries. | Loads every processed member in ordinal order. |
| `src/Pegasus.Core/ImageIntake/ImageIntakeGroupRouting.cs` | Added the Core-owned group decision table. | Prevents an unreadable close-up from being split from a readable overview and makes fallback reasons explicit. |
| `src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs` | Added group aggregation and all-member association path. | Routes the group once, then reuses the existing registration and candidate matcher for each member. |
| `src/Pegasus.Infrastructure/Vision/OnnxVrmRecognitionEngine.cs` | Records `detector_no_plate` versus `recognizer_no_readable_text`. | Confirms both vision layers ran and preserves an honest reason when no VRM is usable. |
| `tests/Pegasus.Core.Tests/ImageIntake/ImageIntakeGroupRoutingPolicyTests.cs` | Added routing matrix tests. | Covers overview + unreadable close-up, conflicting reads, and incomplete groups. |

## Governing docs

No protected operator or normative governing document was changed in this implementation branch. The implementation reuses the existing `IImageIntakeCaseCandidates`, `IRegisterImageIntake`, and `ICaseAcceptanceStore` boundaries. The latter requires a real principal and immutable Case identity, so an Image-Only Case cannot be fabricated safely. INTK-007 and the reviewed governing-document reconciliation must define that authorized fallback contract before it is wired.

## Risks / follow-ups

- INTK-005 PR #416 is the branch base at `ed04f498`; rebase this branch onto the reviewed INTK-005 result and reconcile conflicts before merge.
- The Image-Only Case branch and persisted group outcome/history are not claimed complete here; they require the authorized reference/principal/lifecycle contract. Do not merge this PR as the full INTK-006 acceptance until that review decision is resolved.
- INTK-007 owns the Unidentified queue and must receive terminal grouped failures without silently reverting to the old Needs sorting meaning.

## Verification hand-off

On merged `main`, run:

- `dotnet restore Pegasus.slnx`
- `dotnet build Pegasus.slnx --configuration Release`
- `dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --filter "FullyQualifiedName~ImageIntakeGroupRoutingPolicyTests|FullyQualifiedName~AutomaticImageIntakeTests|FullyQualifiedName~GroupedIntakeTests"`
- `dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --filter "FullyQualifiedName~VrmRecognitionEngineTests"`
- Full `dotnet test Pegasus.slnx --configuration Release`

Verify that a grouped overview plus no-plate damage close-up associates both receipts to one eligible existing Case, and capture the recorded detector/recognizer failure codes without exposing pixels or raw registration text.
