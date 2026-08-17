# Plan — TICK-011: INT-17 ordinary-image VRM recognition

## Approach

Reconcile the ticket with the implementation already shipped on `dev`; do not create a redundant recognition engine, caller, or code-only PR. The source/history review and focused Core regression run show that the defined feature is present. Any later source change must be a separately demonstrated defect fix or an ADR-authorised mechanism/threshold decision.

## Governing docs

- **Meets `docs/frd/frd-06-vehicle-and-engineering-evidence.md`** — the reviewed implementation retains source-image-bound results, separately persists unavailable/no-readable/failure outcomes, automatically acts only at the accepted 0.80 bar and only for one unambiguous registration, and does not allocate a Case/PO.
- **Meets `docs/adr/0019-in-process-onnx-vrm-recognition.md`** — `OnnxVrmRecognitionEngine` is the in-process Infrastructure adapter behind the Core port; no external image upload, new runtime, or model decision is introduced.
- No governing document is modified and no new ADR is required.

## Steps

1. Verify the current `dev` implementation and its history map to the linked FRD and ADR without broadening the recognised scope.
2. Run the focused ImageIntake Core regression suite and retain its result; do not represent the bounded integration timeout as a pass.
3. Reconcile this ticket as already shipped rather than creating a no-op worktree, empty commit, or PR. If independent review is requested, supply the research and test result as the review brief.

## Verification

- `dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --filter "FullyQualifiedName~ImageIntake" --verbosity minimal` must pass.
- Source inspection confirms the real caller remains `ImageIntakeAutomation` from the durable intake pipeline and the implementation remains the ADR-0019 in-process adapter.
- No code diff is produced solely to satisfy ticket mechanics.

## Risks / open questions

- The wider ImageIntake/Vrm integration subset exceeded the 120-second local bound without a final result. It is follow-up verification only; this ticket must not claim it passed.
- Altering the engine, model bytes, threshold, or external-upload boundary is outside scope and requires new evidence and an architectural decision.
