2026-09-08 implementation handoff

Implemented the approved existing-helper extension in `tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs`: `CreateDefinitiveQdosInstructionDocument(...)` produces a real PDF document containing formal notification title plus QDOS extraction signals. It accepts scenario fields and optional extra lines; no production policy/parser/source change.

Updated the affected existing transport paths in CustodyOutbox, InstructionDraft, ImageViewing, ImageIntake shared case seed (therefore MailWorkspace/TriageQueues/UploadConfirmation/TestUi callers), MailboxIntake, MultiFormat, QdosTriage, Recovery, SendToAi and UploadConfirmation. Where a test’s asserted path was an actual MIME/PDF/DOCX/MSG/mailbox/upload flow, the formal content is an actual document attachment or structured document, not EmailBody. Direct display/setup continues to use its existing callers; no corpus gate or request bypass was introduced.

SendToAi retains absent-dialog and NotFound POST assertions, and removes only the stale disabled/gated-control markup expectation.

Static evidence only:
- `git diff --check`: PASS (no diagnostic; Git emitted only CRLF checkout warnings).
- Exact changed paths are a subset of the approved fourteen-file map: 11 test files, all under `tests/Pegasus.IntegrationTests/`; no production/docs/scripts/corpus changes.
- Remaining `QDOS instruction` / `Vehicle Registration:` markers are intentional negative/limit probes, a forwarded-address display string, or invalid field input that is mirrored into its attached formal instruction document.
- No build, test, browser, capture, package, deployment, commit, push, PR, merge, or verification run by this worker.

Await parent-coordinated independent review and designated host verification of the exact existing 13-class selection.

2026-09-08 parent static-review correction

Parent identified two helper-root-cause defects before verification. Fixed them:
- No fixture facts are defaulted. Registration and vehicle now default to null; their structural labels are blank only where the prior scenario had no corresponding fact. Callers with an original registration pass it explicitly. Removed introduced Ford Focus/default registration and the invented mailbox claim number.
- Additional document lines split on '\r' and '\n' characters with the existing repository pattern, so raw C# LF strings cannot reach PdfPig as multiline/control-character text.
- InstructionDraft now adds the formal title only and carries its own normalized QDOS/signature lines with addSignatureLines false, avoiding duplicate nonblank registration/vehicle content. Its added vehicle label is intentionally blank.

Repeated static git diff --check and approved-scope census: PASS (only CRLF checkout warnings). No build/test/browser/other host-owned verification was run.
