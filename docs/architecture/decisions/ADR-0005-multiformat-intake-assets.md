# ADR-0005: Multi-format intake and review assets

Status: **Accepted for the local first-MVP slice**

Date: 2026-07-23

## Context

QDOS instructions and evidence arrive as email bodies, nested email, PDF, Word
documents, Outlook containers, and separate or embedded images. The first local
slice read only EML bodies and PDF embedded text. It skipped nested messages,
non-PDF attachments, and images, while its low-text threshold could send an
ordinary low-text PDF to OCR without first establishing that the page was a
scan.

The application needs one extraction route with visible provenance. Extracted
or duplicate evidence must never replace the authoritative source occurrence.
The first MVP does not need automated interpretation of every historical file
container.

## Decision

1. Keep `ProcessIntake` as the single Core intake use case called by the
   Development-only Web upload and, later, the Worker.
2. Use MimeKit for bounded recursive EML traversal, PdfPig 0.1.15 for PDF
   embedded text and discrete image streams, and Open XML SDK 3.5.1 for DOCX
   text and internal image parts. PyMuPDF is not part of the application stack.
3. Support direct EML, PDF, DOCX, JPEG, and PNG in the first MVP. Retain legacy
   DOC and MSG sources with an explicit deferred-format reason in `Needs
   sorting`; do not allocate a case or reference from those containers.
4. Bound EML processing to eight nested attached-message levels, 128 MIME
   entities, and 25 MB of decoded parts per intake. Stop the affected branch and
   surface the limit to the operator. A breached bound makes the intake
   incomplete and must take precedence over otherwise confirming content, so no
   case or reference is allocated.
5. Treat each MIME attachment, inline image, DOCX internal image, and discrete
   PDF image object as a separate asset occurrence with source label, media
   type, hash, disposition, and available page/bounds/sample dimensions. Do not
   segment a flattened collage or infer several photographs from one raster.
6. Group exact content hashes for review but retain every occurrence and its
   provenance. Exact matching is not deletion.
7. Use OCR only when a PDF page has fewer than 80 non-whitespace embedded-text
   characters and a raster image covers at least 80 percent of the page. Persist
   the page candidate explicitly. A low-text page without a dominant raster is
   manual review, not OCR. Ordinary attached, embedded, inline, or direct images
   are not sent to OCR. Automated vehicle-registration OCR/VLM is later scope.
8. The local Web proof retains immutable content-addressed bytes under ignored
   `artifacts/` and stores only asset metadata and opaque storage keys in SQL.
   Production staging must use private Blob storage through the Infrastructure
   adapter: Web's managed identity stages manual/provider uploads, while the
   Worker's managed identity stages Graph sources and reads queued work. Long-term
   case custody remains Box and is not proved by this local store.
9. Never fetch DOCX external relationships. A corrupt top-level DOCX is a visible
   terminal unsupported outcome; attachment failures retain the surrounding
   email for review.
10. Preflight DOCX packages at 512 entries, 50 MB aggregate uncompressed bytes,
    10 MB per XML/relationship part, and 25 MB aggregate extracted images. Open
    XML also enforces the per-part character limit; URI-deduplicated part traversal
    is iterative and already bounded by the package-entry ceiling.
11. Verify content hashes when reusing or reading local content-addressed files.
    Refuse to serve a retained asset whose bytes no longer match its key.
12. Read every page of each PDF; do not truncate an otherwise processable file at
    an arbitrary page count. Apply one aggregate PDF budget across a complete
    intake, including PDF attachments: 5 Mi characters of extracted text, 512
    discrete image occurrences, 100 million decoded image sample pixels, and
    25 MiB of retained extracted-image bytes. Apply one 30-second processing
    deadline across those PDFs and check it plus caller cancellation before and
    after page work and between images. If any budget is exceeded, retain the
    source, mark the intake incomplete, and allocate no case/reference; never
    accept the partial extraction as complete. These initial limits are adapter
    safety bounds, not business completeness rules, and may change only with
    representative PDF evidence and the target host resource envelope.

### 2026-07-25 DOCX placement clarification

Every visible placement of a DOCX image is an asset occurrence. If the same bytes
are placed twice, both placements are retained; a hash may group them for review
but must not collapse them. The current URI-deduplicated part traversal is not
compliant when a part is reused at more than one visible placement. A repeated-
placement regression fixture is required before that behavior can be claimed as
implemented. This clarification does not introduce a general layout engine.

## Evidence and limits

The real `POST /Intake/Upload` caller has synthetic before/after integration
coverage for DOCX, deferred DOC/MSG, direct JPEG/PNG, mixed and nested EML,
exact duplicate occurrences, malformed/resource-heavy DOCX, MIME limits even
when earlier content confirms QDOS, and local artifact integrity. This proves
format routing and visible retention, not field accuracy on the genuine corpus.

PDF processing is all-pages-or-incomplete: the adapter has no page-count cut-off,
and aggregate expansion/time limits cannot be reset by placing PDFs in separate
email attachments. In-process deadline and cancellation checks are cooperative
between PdfPig page/image operations; hard CPU and memory isolation for a
pathological single-page decode remains a production Worker hosting concern.

PdfPig image extraction is implemented through `Page.GetImages()`, using a raw
JPEG stream where present or PdfPig's PNG conversion. Unsupported image encodings
remain a visible decode issue. No PDF collage segmentation or image OCR is added.

Document Intelligence OCR, Graph mailbox delivery, private Blob staging, Box
custody, and automated DOC/MSG extraction remain separate caller-backed
increments. A registered but uncalled cloud adapter would not complete them.

## Consequences

- Core contracts stay engine-neutral and carry review assets and scanned-page
  candidates rather than PdfPig, MimeKit, or Open XML types.
- Incomplete bounded processing is an explicit contract state that prevents
  reference allocation without discarding the retained source.
- The local manual route can review source and extracted images without putting
  file bytes in SQL.
- DOC and MSG are visible and safe to sort but are not misrepresented as parsed.
- OCR volume is narrowed to scan-like PDF pages and can be evaluated before any
  billed Azure call.
- Production intake remains disabled until authentication, Worker delivery,
  durable cloud staging, Box custody, and operator-accepted extraction evidence
  exist.
