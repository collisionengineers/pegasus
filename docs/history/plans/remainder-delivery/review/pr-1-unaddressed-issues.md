# Historical PR #1 review snapshot

> **Archive status — non-authoritative planning evidence.** Revalidate against current product, roadmap, architecture, operations, design, decisions, and code before use.

Pre-conversion status: **Historical snapshot at `5e0aa7a` — not current issue state**

Supersession/current owners: [plans index](../../README.md) and [validation guidance](../../../../agent-guidance/validation.md). The findings below remain evidence from that exact review point; current disposition must be established from current source, tests, and review.

PR [#1](https://github.com/collisionengineers/collisionspike_v2/pull/1) at `5e0aa7a` had **15 confirmed unaddressed issues**, plus one contract ambiguity. The latest commit changed documentation only, so these were discovered at that commit, not introduced by it.

The source locations below are historical labels from that checkout, not current links or current-disposition claims. Use the current owners linked above to re-establish each finding before acting.

### Latest review round: 3 unaddressed P2s

1. HTML-only email stripping uses an unbounded backtracking regex; hostile input can cause quadratic CPU use. Historical target: `src/Pegasus.Infrastructure/Intake/Qdos/MimeKitPdfPigQdosSourceReader.cs:590`.
2. Attached `MessagePart` emails are reserialized instead of retaining their original decoded bytes, potentially changing their hash and provenance. Historical target: `src/Pegasus.Infrastructure/Intake/Qdos/MimeKitPdfPigQdosSourceReader.cs:657`.
3. DOCX parsing receives no cancellation token and performs synchronous package, XML, and image processing after the caller is cancelled. Historical target: `src/Pegasus.Infrastructure/Intake/Qdos/MimeKitPdfPigQdosSourceReader.cs:85`.

### Earlier feedback still present at HEAD

4. An artifact-storage failure is finalized as `TechnicalFailure`; replaying the same occurrence returns that receipt and never retries storage. Historical target: `src/Pegasus.Core/Intake/Qdos/ProcessQdosIntake.cs:38`.
5. Untrusted attachment filenames/source labels are assigned directly to SQL columns limited to 260/500 characters, producing SQL Server-only failures after bytes are stored. Historical target: `src/Pegasus.Infrastructure/Persistence/EfQdosIntakeStore.cs:204`.
6. PDF pixel limits are now checked before conversion, but `TryGetPng` still allocates the full output before enforcing the 25 MiB extracted-byte limit. Historical target: `src/Pegasus.Infrastructure/Intake/Qdos/MimeKitPdfPigQdosSourceReader.cs:320`.
7. Unsupported MIME attachments such as XLSX, ZIP, or WebP are silently skipped instead of retained as review occurrences. Historical target: `src/Pegasus.Infrastructure/Intake/Qdos/MimeKitPdfPigQdosSourceReader.cs:709`.
8. Candidate conflict detection compares lightly normalized strings before typed canonicalization, so equivalent VRMs or dates can be reported as conflicts. Historical target: `src/Pegasus.Core/Intake/Qdos/ProcessQdosIntake.cs:418`.
9. A generic MIME attachment named `.eml` re-enters email processing with nesting depth reset to zero. Historical target: `src/Pegasus.Infrastructure/Intake/Qdos/MimeKitPdfPigQdosSourceReader.cs:559`.
10. DOCX expansion and image limits are recreated for every attachment instead of being shared across the intake. Historical target: `src/Pegasus.Infrastructure/Intake/Qdos/MimeKitPdfPigQdosSourceReader.cs:334`.
11. DOCX extraction concatenates only `<w:t>` nodes, dropping manual breaks, carriage returns, and tabs that may separate fields. Historical target: `src/Pegasus.Infrastructure/Intake/Qdos/MimeKitPdfPigQdosSourceReader.cs:371`.
12. `OcrRequired`, `Unsupported`, and `TechnicalFailure` receipts have no normal queue filter or count and can disappear behind the 100-row unfiltered cap. Historical target: `src/Pegasus.Web/Pages/Intake/Queue.cshtml:16`.
13. GUID receipt tokens are validated but not canonicalized, allowing case-variant identities under SQLite. Historical target: `src/Pegasus.Web/Pages/Intake/Qdos.cshtml.cs:24`.
14. Queue retrieval still materializes the complete matching table before sorting and taking 100. Historical target: `src/Pegasus.Infrastructure/Persistence/EfQdosIntakeStore.cs:74`.
15. The genuine DOCX corpus test still accepts either `DraftReady` or `NeedsSorting`, so it cannot detect the recorded wrong-outcome regression. Historical target: `tests/Pegasus.IntegrationTests/MultiFormatGenuineCorpusWebTests.cs:56`.

The repeated-DOCX-image thread needs a decision rather than an automatic fix: [ADR-0005](../../../../architecture/decisions/ADR-0005-multiformat-intake-assets.md) says retain every occurrence but later explicitly describes URI-deduplicated traversal. Clarify whether “occurrence” means an image package part or every document placement.

GitHub currently shows all 12 inline threads unresolved. One is marked outdated because its lines moved, but its storage-retry behavior remains. I made no edits, replies, or thread-resolution changes and did not use the five unstaged files when assessing committed HEAD.
