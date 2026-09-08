# Plan — DELIV-056: Align intake regression fixtures with definitive instruction evidence

## Objective

Make the listed D2 regression scenarios reach their current assertions through the real intake paths, using a document that both selects QDOS and is formally classified as definitive. Align the one stale pre-handoff Send to Claude assertion.

## Governing decisions

- `docs/frd/frd-02-intake-and-source-identity.md`: normal automatic allocation requires definitive evidence; transport evidence is not a substitute.
- Extraction and classification are distinct: the actual document content needs the QDOS signature (`QDOS`, `Registration:`, `Our Client’s Vehicle:`) and a formal `ENGINEER NOTIFICATION` title.
- Parent authorization permits one extension to the existing test helper, not a new fixture framework.

## Required changes

1. Add `IntakeTestEvidence.DefinitiveQdosInstructionDocument(...)` in `tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs`. Its optional scenario-field parameters preserve intentional missing-field cases. It emits documented formal document text only; it does not generate a replacement mail path.
2. Update the 13 named callers to place that text in their existing document transport (attachment, uploaded structured document, or equivalent existing document builder). Preserve input filename/sender/route/attachment counts and roles unless the specific scenario explicitly needs the instruction document asset.
3. For display/setup-only case seeds, use the existing `AllocationTestData.StoreDefinitiveReceiptAsync` only where that preserves the original claim and deliberately does not bypass an intake-pipeline scenario.
4. In SendToAI retain absent-dialog and denied-POST checks, remove only the stale disabled-control markup expectation.

## Expected files

| Action | Path | Responsibility |
|---|---|---|
| Modify | `tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs` | One shared documented formal-instruction text helper. |
| Modify | `tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs` | Existing mail/PDF custody inputs retain their path and asset assertions. |
| Modify | `tests/Pegasus.IntegrationTests/InstructionDraftWebTests.cs` | Existing structured document draft inputs. |
| Modify | `tests/Pegasus.IntegrationTests/ImageViewingWebTests.cs` | Existing image-view case seeds. |
| Modify | `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Existing workspace intake caller. |
| Modify | `tests/Pegasus.IntegrationTests/QdosTriageIntegrationTests.cs` | Existing QDOS/Triage caller. |
| Modify | `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs` | Existing queue caller. |
| Modify | `tests/Pegasus.IntegrationTests/ImageIntakeWebTests.cs` | Existing image-intake case seed. |
| Modify | `tests/Pegasus.IntegrationTests/MailboxIntakeIntegrationTests.cs` | Existing mailbox caller. |
| Modify | `tests/Pegasus.IntegrationTests/MultiFormatIntakeWebTests.cs` | Existing DOCX/DOC/MSG/EML/PDF and guard inputs. |
| Modify | `tests/Pegasus.IntegrationTests/RecoveryTests.cs` | Existing recovery seed. |
| Modify | `tests/Pegasus.IntegrationTests/SendToAiIntegrationTests.cs` | Accepted-case seed and stale display assertion. |
| Modify | `tests/Pegasus.IntegrationTests/UploadConfirmationWebTests.cs` | Existing confirmation/attachment inputs. |
| Modify | `tests/Pegasus.IntegrationTests/TestUiFocusedRenderTests.cs` | Existing focused-render seed. |

## Ordered checklist

### 1. Add shared documented instruction text

- Extend the existing helper only; no dependency or framework.
- Required text: `ENGINEER NOTIFICATION`, `QDOS`, `Our Client’s Vehicle:`, `Registration:`.
- Scenario caller arguments retain existing claimant, claim, registration and optional missing fields.

### 2. Correct pipeline fixtures

- For each scenario that claims real intake behavior, retain its existing MIME/upload/mailbox/queued parser path.
- Put the helper text in actual `DocumentContent`/`PdfContent`: do not place it in EmailBody.
- Preserve original attachment cardinality and role assertions; where a test is specifically about an attachment, make that attachment the instruction content rather than adding an unrelated one.
- Leave intentional malformed, truncation, limit and negative inputs negative.

### 3. Correct display-only setup and SendToAI

- Use the direct receipt helper only where no intake-pipeline assertion is being made.
- Do not render an unavailable action. Retain missing-dialog and unauthorized POST denial checks.

### 4. Static review and handoff

- Run only static scope/text/diff checks in this worker.
- Do not run builds, tests, browser, capture, package, release or deploy operations.
- The designated host verifier runs the exact existing 13-class selection after its required sequential build; any failure is reported without assertion weakening.

## Non-goals

No `src/**`, policy, schema, docs, package, corpus, parser, source-inventory or product changes; no test files beyond the fourteen listed files.

## Stop condition

After fixture-only changes and static scope review, leave the ticket in implementing for parent-coordinated commit, review and verification. Do not merge or start another ticket.
