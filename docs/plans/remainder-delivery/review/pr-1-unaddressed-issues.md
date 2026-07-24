PR [#1](https://github.com/collisionengineers/collisionspike_v2/pull/1) at `5e0aa7a` has **15 confirmed unaddressed issues**, plus one contract ambiguity. The latest commit changed documentation only, so these were discovered at that commit, not introduced by it.

### Latest review round: 3 unaddressed P2s

1. HTML-only email stripping uses an unbounded backtracking regex; hostile input can cause quadratic CPU use. [Reader](C:/Users/Alex/Documents/GitHub/collisionspike_v2/src/CollisionSpike.Infrastructure/Intake/Qdos/MimeKitPdfPigQdosSourceReader.cs:590)
2. Attached `MessagePart` emails are reserialized instead of retaining their original decoded bytes, potentially changing their hash and provenance. [Reader](C:/Users/Alex/Documents/GitHub/collisionspike_v2/src/CollisionSpike.Infrastructure/Intake/Qdos/MimeKitPdfPigQdosSourceReader.cs:657)
3. DOCX parsing receives no cancellation token and performs synchronous package, XML, and image processing after the caller is cancelled. [Reader](C:/Users/Alex/Documents/GitHub/collisionspike_v2/src/CollisionSpike.Infrastructure/Intake/Qdos/MimeKitPdfPigQdosSourceReader.cs:85)

### Earlier feedback still present at HEAD

4. An artifact-storage failure is finalized as `TechnicalFailure`; replaying the same occurrence returns that receipt and never retries storage. [ProcessQdosIntake.cs](C:/Users/Alex/Documents/GitHub/collisionspike_v2/src/CollisionSpike.Core/Intake/Qdos/ProcessQdosIntake.cs:38)
5. Untrusted attachment filenames/source labels are assigned directly to SQL columns limited to 260/500 characters, producing SQL Server-only failures after bytes are stored. [EfQdosIntakeStore.cs](C:/Users/Alex/Documents/GitHub/collisionspike_v2/src/CollisionSpike.Infrastructure/Persistence/EfQdosIntakeStore.cs:204)
6. PDF pixel limits are now checked before conversion, but `TryGetPng` still allocates the full output before enforcing the 25 MiB extracted-byte limit. [Reader](C:/Users/Alex/Documents/GitHub/collisionspike_v2/src/CollisionSpike.Infrastructure/Intake/Qdos/MimeKitPdfPigQdosSourceReader.cs:320)
7. Unsupported MIME attachments such as XLSX, ZIP, or WebP are silently skipped instead of retained as review occurrences. [Reader](C:/Users/Alex/Documents/GitHub/collisionspike_v2/src/CollisionSpike.Infrastructure/Intake/Qdos/MimeKitPdfPigQdosSourceReader.cs:709)
8. Candidate conflict detection compares lightly normalized strings before typed canonicalization, so equivalent VRMs or dates can be reported as conflicts. [ProcessQdosIntake.cs](C:/Users/Alex/Documents/GitHub/collisionspike_v2/src/CollisionSpike.Core/Intake/Qdos/ProcessQdosIntake.cs:418)
9. A generic MIME attachment named `.eml` re-enters email processing with nesting depth reset to zero. [Reader](C:/Users/Alex/Documents/GitHub/collisionspike_v2/src/CollisionSpike.Infrastructure/Intake/Qdos/MimeKitPdfPigQdosSourceReader.cs:559)
10. DOCX expansion and image limits are recreated for every attachment instead of being shared across the intake. [Reader](C:/Users/Alex/Documents/GitHub/collisionspike_v2/src/CollisionSpike.Infrastructure/Intake/Qdos/MimeKitPdfPigQdosSourceReader.cs:334)
11. DOCX extraction concatenates only `<w:t>` nodes, dropping manual breaks, carriage returns, and tabs that may separate fields. [Reader](C:/Users/Alex/Documents/GitHub/collisionspike_v2/src/CollisionSpike.Infrastructure/Intake/Qdos/MimeKitPdfPigQdosSourceReader.cs:371)
12. `OcrRequired`, `Unsupported`, and `TechnicalFailure` receipts have no normal queue filter or count and can disappear behind the 100-row unfiltered cap. [Queue.cshtml](C:/Users/Alex/Documents/GitHub/collisionspike_v2/src/CollisionSpike.Web/Pages/Intake/Queue.cshtml:16)
13. GUID receipt tokens are validated but not canonicalized, allowing case-variant identities under SQLite. [Qdos.cshtml.cs](C:/Users/Alex/Documents/GitHub/collisionspike_v2/src/CollisionSpike.Web/Pages/Intake/Qdos.cshtml.cs:24)
14. Queue retrieval still materializes the complete matching table before sorting and taking 100. [EfQdosIntakeStore.cs](C:/Users/Alex/Documents/GitHub/collisionspike_v2/src/CollisionSpike.Infrastructure/Persistence/EfQdosIntakeStore.cs:74)
15. The genuine DOCX corpus test still accepts either `DraftReady` or `NeedsSorting`, so it cannot detect the recorded wrong-outcome regression. [MultiFormatGenuineCorpusWebTests.cs](C:/Users/Alex/Documents/GitHub/collisionspike_v2/tests/CollisionSpike.IntegrationTests/MultiFormatGenuineCorpusWebTests.cs:56)

The repeated-DOCX-image thread needs a decision rather than an automatic fix: [ADR-0005](C:/Users/Alex/Documents/GitHub/collisionspike_v2/docs/architecture/decisions/ADR-0005-multiformat-intake-assets.md:36) says retain every occurrence but later explicitly describes URI-deduplicated traversal. Clarify whether “occurrence” means an image package part or every document placement.

GitHub currently shows all 12 inline threads unresolved. One is marked outdated because its lines moved, but its storage-retry behavior remains. I made no edits, replies, or thread-resolution changes and did not use the five unstaged files when assessing committed HEAD.
