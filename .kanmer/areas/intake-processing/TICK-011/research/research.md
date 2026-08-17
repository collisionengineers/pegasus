# Research — TICK-011: INT-17 ordinary-image VRM recognition

## Question

Does `dev` still need implementation for automatic vehicle-registration reading from ordinary vehicle images, and, if so, where must the caller and acceptance boundary be changed?

## Findings

- `docs/frd/frd-06-vehicle-and-engineering-evidence.md` and `docs/adr/0019-in-process-onnx-vrm-recognition.md` require source-image-bound, in-process, suggestion-first recognition with no external upload; the accepted automatic-action bar is 0.80.
- `src/Pegasus.Core/ImageIntake/VrmRecognition.cs` defines the Core port and the closed outcome taxonomy: suggestion, no readable result, technical failure, and unavailable. It also defines the 0.80 automatic bar and accepted near-match rules.
- `src/Pegasus.Infrastructure/Vision/OnnxVrmRecognitionEngine.cs` is the ADR-0019 in-process ONNX implementation. It returns candidates or abstentions from supplied bytes, verifies its model set on construction, and has no network call path.
- `src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs` is the real post-persistence caller. It applies only to image-only `Needs sorting` receipts, checks stored image hashes, scans every retained asset, records each outcome, and automatically registers only one distinct candidate at or above 0.80. Recoverable recognition or recording failures preserve the intake outcome rather than failing it open.
- `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs` persists the immutable registration and its Image Intake Reference transactionally. `src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs` keeps registration distinct from later Case association.
- The implementation landed on `dev` in `ae6f0c2d` and was tightened by `ef3eb4c7`; the acceptance of the 0.80 bar was recorded in `f7d99b18`.
- `dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --filter "FullyQualifiedName~ImageIntake"` passed 78/78 on 2026-08-17. The wider integration subset exceeded a 120-second bound before producing a final result, so it is not claimed as passing evidence.

## Implications

INT-17 is already implemented on `dev`. No new engine, network adapter, model change, or application caller should be introduced under this ticket. Any execution work is limited to reconciling the ticket record with the shipped implementation and preserving focused regression evidence; a real code change requires a newly identified defect or a separately authorised engine decision.

## Open questions

- None. The bounded integration run is verification follow-up, not a product or design question.
