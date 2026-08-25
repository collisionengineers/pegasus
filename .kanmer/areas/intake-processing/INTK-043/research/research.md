# Research — INTK-043: ordinary intake source-reading latency

## Question

Which part of the shared queued intake path accounts for the observed delay after durable staging, what evidence exists today, and what must be measured before changing the source reader?

## Findings

- **Measured observation supplied with the ticket:** one observed intake spent about 17 seconds between staging and the later post-reader marker, while classification and case creation then took about one second. This is a single observation, not a percentile baseline, and no checked-in raw trace or query result was found that reproduces it (INTK-043 body; repository search under artifacts, tests, source and docs).
- **The named interval is broader than the source reader.** ProcessQueuedIntake reads the staged artifact, hashes it, stores the same bytes under their durable content hash, calls ProcessIntake, then completes the work item. ProcessIntake performs a source-identity query, source read, sequential retention of every extracted asset, policy evaluation and receipt persistence. The caller then deletes staging, associates mail/cases and attempts allocation (src/Pegasus.Core/Intake/DurableIntake.cs; src/Pegasus.Core/Intake/ProcessIntake.cs). The observation cannot currently be attributed exactly to MimeKit, PdfPig, Blob I/O, SQL, or host delay.
- **E-mail and manual upload already converge on one business implementation.** Both are durably staged and later enter ProcessQueuedIntake, which delegates identification, parsing, classification and extraction to the single ProcessIntake/IIntakeSourceReader path (DurableIntake.cs; MailboxIntake.cs; Upload.cshtml.cs; AGENTS.md).
- **Current telemetry is too coarse to select a safe optimization.** ProcessIntake creates one process_intake Activity and records intake.duration_ms for the whole inner operation. There are no child spans around staged download, durable promotion, MIME parse, attachment decode, per-format extraction, asset retention, assessment, receipt persistence, or allocation (ProcessIntake.cs; no Activity instrumentation in DurableIntake.cs, the source reader, or AzureBlobIntakeArtifactStore.cs). Worker Application Insights export already exists in src/Pegasus.Worker/Program.cs.
- **The production Blob path performs several network operations before classification.** A queued attempt downloads and verifies staging bytes, then StoreAsync uploads the same payload to a content-addressed key. If that key exists, the immutable-write conflict path downloads it again to verify length/hash. Staging cleanup follows processing. These operations preserve custody/integrity, but their latency is unmeasured (AzureBlobIntakeArtifactStore.cs; DurableIntake.cs).
- **The EML reader is bounded but sequential.** It copies root bytes for MimeKit, walks MIME entities in order, decodes each retained attachment to a MemoryStream and array, then recursively reads supported attachments. PDF reading copies bytes again, extracts every page, enumerates and decodes every image, and returns candidates that ProcessIntake retains one at a time (MimeKitPdfPigOpenXmlIntakeSourceReader.cs). These are concrete work/copy sites, not proven p95 bottlenecks.
- **Large/complex inputs have explicit safety bounds.** MIME traversal is capped at 128 entities, 25 MB decoded content and depth 8; PDF processing has text/image/pixel/byte limits and a 30-second deadline. Optimization must keep those fail-closed limits and truthful Processing behavior (source reader; FRD-02).
- **Existing tests prove correctness, not latency.** QdosEmailCohortTests, QdosExtractionCoverageTests, QdosMappingExtractionTests, MailboxIntakeIntegrationTests and upload outcome tests exercise the reader/shared flow. Corpus outputs contain classification/extraction data but no elapsed-time distribution. No representative p50/p95/worst-case intake evidence was found.
- **Linked [[AUTO-008]] reached the same measurement boundary.** Its research separates acceptance, dispatch, processing, allocation and terminal persistence and notes that timer-free integration tests do not represent queue latency. INTK-043 is narrower: after [[INTK-041]]/[[INTK-042]] remove healthy dispatch delay, measure internal queued-processing stages before removing verified work.
- **Dependency order matters.** [[INTK-041]] and [[INTK-042]] block this ticket, while it blocks [[DELIV-021]]. A baseline before immediate-publication changes would mix old queue delay with processing cost (get_links INTK-043).

## Implications

1. Add correlated stage timing first using the existing Activity/Application Insights route and stable low-cardinality stage names. Separate staged download/integrity, durable promotion, identity lookup, source read, asset retention, assessment/receipt persistence, and association/allocation.
2. Establish a repeatable ordinary QDOS EML and manual-upload baseline after [[INTK-041]]/[[INTK-042]]. Report sample count, p50, p95 and worst case by channel/size class; keep retries, cold starts and intentionally large inputs separate.
3. Change only the stage proven to dominate. Byte-array copies, sequential candidate retention, and staged-to-durable Blob promotion are hypotheses, not selected fixes.
4. Preserve the single Core route, integrity checks, bounded traversal, idempotency and truthful Processing. Do not add another queue, worker, cache or format implementation.
5. Treat ten-second p95 as receipt/wake-to-truthful terminal state for ordinary healthy inputs, with per-stage traces explaining failures.

## Open questions

No operator decision is required for planning. Root-cause selection is evidence-gated: instrumentation and baseline collection precede optimization, and the selected change must name the measured stage and preserve the constraints above.
