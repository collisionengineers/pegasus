# Files — DELIV-056

*The files document. This is the bounded surface area of the current fixture correction.*

Surveyed against current `dev` worktree source and CI run `34196369756`.

## Where the change lands

| Path | Why |
|---|---|
| `tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs` | Replace the old QDOS shorthand in direct and queued custody fixture builders with signature-complete QDOS document evidence; preserve custody, attachment, audit and replay assertions. |
| `tests/Pegasus.IntegrationTests/InstructionDraftWebTests.cs` | Make the complete and controlled-invalid document fixtures signature-complete while retaining field extraction and allocation assertions. |
| `tests/Pegasus.IntegrationTests/ImageViewingWebTests.cs` | Correct the instruction-backed gallery and asset fixture bodies without changing image-viewing assertions. |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Correct its automatic-allocation fixture evidence only; retain mail workspace behavior assertions. |
| `tests/Pegasus.IntegrationTests/QdosTriageIntegrationTests.cs` | Correct the direct QDOS allocation fixture inputs while preserving Triage decision and Notes-panel assertions. |
| `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs` | Correct current fixture input only; do not alter CASE-045's separate image-principal coverage. |
| `tests/Pegasus.IntegrationTests/ImageIntakeWebTests.cs` | Correct automatic-case seed evidence and preserve image-intake lifecycle/route assertions. |
| `tests/Pegasus.IntegrationTests/MailboxIntakeIntegrationTests.cs` | Correct mailbox automatic-allocation fixture evidence without changing route/classification assertions. |
| `tests/Pegasus.IntegrationTests/MultiFormatIntakeWebTests.cs` | Correct synthetic DOCX, DOC, MSG, EML, PDF and guard inputs with the signature-complete QDOS evidence while preserving format and limit coverage. |
| `tests/Pegasus.IntegrationTests/RecoveryTests.cs` | Correct recovery seed evidence while retaining recovery and retained-evaluation assertions. |
| `tests/Pegasus.IntegrationTests/SendToAiIntegrationTests.cs` | Correct accepted-case seed evidence and align only the stale pre-handoff Send to Claude display expectation with the current absent-control/access boundary. |
| `tests/Pegasus.IntegrationTests/UploadConfirmationWebTests.cs` | Correct instruction inputs used by attachment/confirmation scenarios, preserving receipt and decision assertions. |
| `tests/Pegasus.IntegrationTests/TestUiFocusedRenderTests.cs` | Correct its intake-backed render fixture evidence only; preserve focused rendering expectations. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs` | The three exact case-insensitive required signals: `QDOS`, `Registration:`, and `Our Client’s Vehicle:`. |
| `src/Pegasus.Core/Intake/InstructionExtractionPolicySelector.cs` | Selection requires every signature signal and deliberately excludes transport evidence. |
| `tests/Pegasus.IntegrationTests/RetainedInstructionAnalysisTests.cs` | Existing QDOS-shaped evidence pattern that matches the current document profile. |
| `docs/frd/frd-02-intake-and-source-identity.md` | A definitive instruction follows the normal automatic allocation policy; receipt or source alone is not case creation. |
| `tests/Pegasus.IntegrationTests/SendToAiIntegrationTests.cs` | Current display/access test shape; the separate display correction must preserve its absent-dialog and refused-POST checks. |
| `docs/engineering.md` | Verification is selected by affected scope and the host verifier serializes required build/test work. |

## Ripple effects

- The scoped existing integration selections should advance past fixture setup and exercise their existing assertions.
- No production caller, documentation, database schema, package, build artifact, source inventory, or generated UI output changes.
- The host verifier owns the required sequential build and focused existing-test execution; this ticket does not run them locally.

## Out of scope

- Any product/intake policy change, including the QDOS signature.
- The independent migration failures and ENG retained-fixture failures in run `34196369756`.
- ENG-029's retained fixtures and INTK-065's source inventory.
- Historical INTK-060, INTK-047, CASE-032, UIIMP-012 and CASE-045 worktrees, claims, plans and different scenario assertions.
- Full Triage, Query, Audit, browser, capture, packaging or release work.
