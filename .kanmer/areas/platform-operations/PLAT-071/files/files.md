# Files — PLAT-071

## Where the change lands

| Path | Why |
|---|---|
| `docs/operations.md` | Correct the two stale current-state claims at the dated-evidence and deferred-capability sections. State that the in-process DOC/MSG parser has been deployed since release 14, while preserving the narrower unproved claims: no demonstrated genuine-cohort accuracy and no confirmed production DOC/MSG use. This is the canonical deployed/runtime record and the ticket's primary change surface. |
| `docs/current-architecture.md` | Remove the old “planned or absent” DOC/MSG bullet at line 162. The detailed reader section at lines 237-269 already describes the correct as-built state and should remain the single architecture description. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `docs/index.md` | Defines the authority chain: operations owns deployed/runtime state, current-architecture owns as-built shape, FRDs own required behavior, and ADRs own technical decisions. |
| `docs/engineering.md` | Defines the evidence tiers and prevents treating registration or tests as deployment proof; the correction must cite the real caller and active release evidence. |
| `docs/frd/frd-05-documents-extraction-and-custody.md` | Owns the required format boundary and already assigns DOC/MSG to the CollisionDocNet-derived readers. It is context, not a planned edit. |
| `docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md` | Records the accepted integrate-behind-Core-port decision and the one-engine-per-format constraint. Its historical context says DOC/MSG were parked before activation; that context is not a current-state claim. |
| `src/Pegasus.Core/Intake/IntakeContracts.cs` | Defines `IIntakeSourceReader`, the Core port reused by the implementation. |
| `src/Pegasus.Core/Intake/ProcessIntake.cs` | Proves the business workflow invokes the reader rather than leaving it registered but unreachable. |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | Shows the concrete reader is composed as `IIntakeSourceReader` through the Provider API decorator and supplied to `ProcessIntake`. |
| `src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.cs` | Shows DOC/MSG detection, dispatch, retained-attachment handling, and the persisted `collisiondocnet-doc-msg-0.1` provenance marker. |
| `src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.DocMsg.cs` | Contains the active DOC and MSG adapters and their fail-closed behavior. |
| `src/Pegasus.Infrastructure/Intake/DocumentExtraction/Cfb/` | Contains the bounded compound-file substrate shared by DOC and MSG. |
| `src/Pegasus.Infrastructure/Intake/DocumentExtraction/Word/` | Contains the bounded legacy Word binary extraction implementation. |
| `src/Pegasus.Infrastructure/Intake/DocumentExtraction/Msg/` | Contains the Outlook MSG/MAPI/RTF extraction implementation. |
| `tests/Pegasus.IntegrationTests/MultiFormatIntakeWebTests.cs` | Pins DOC/MSG behavior through the real Web upload caller, including extraction and unreadable-container fallback. |
| `tests/Pegasus.IntegrationTests/DocumentExtraction/` | Pins parser correctness and resource-bound behavior with controlled fixtures. |
| Kanmer ticket `SIMPLI-013` | Its research, review, post-implementation report, and proof record the integration, PR 449, release-14 deployment, test counts, and production promotion. |
| `artifacts/releases/release-37-0b3ec847/` | Retained release artifacts are context only if a later verifier wants an additional immutable-manifest check; the active release-38 Azure digest and source ancestry already establish current deployment. |

## Ripple effects

- Documentation links and Markdown conventions must remain valid; run the
  repository's relevant documentation checks identified during planning.
- No application caller, parser, persistence model, migration, package lock,
  deployment artifact, or Azure setting should change.
- The correction affects capability-status readers: future reports must no
  longer present DOC/MSG extraction as absent. It must still avoid claiming
  accuracy acceptance or observed use on a genuine production document.
- Because the discrepancy appears twice in `docs/operations.md` and once in
  `docs/current-architecture.md`, correcting only the ticket's originally
  quoted sentence would leave the repository internally inconsistent.

## Out of scope

- Parser enhancements, new supported formats, embedded DOC image extraction,
  OCR, external document-processing services, or accuracy evaluation.
- A genuine-cohort or untouched-holdout exercise; `corpus/` remains
  immutable and untouched.
- A production DOC/MSG submission or any Outlook, Box, SQL, Blob, or Azure
  mutation.
- Changes to FRD-05, ADR-0025, capabilities, runbook, dependencies, source code,
  tests, deployment manifests, release history, or
  `docs/operator-notes.md`, unless planning discovers a direct mechanical
  documentation-reference failure. Broader unrelated stale claims should be
  reported as separate tickets.
