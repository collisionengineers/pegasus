# Vault Agent Instructions

## Purpose

This vault is a managed source corpus for a future RAG database. Treat incoming files as source material: preserve provenance, keep processing reproducible, and make every ingestion discoverable in the log.

## Folder conventions

- `to-ingest/` is the intake queue. Files placed directly here are pending ingestion.
- `to-ingest/image-compare-in-progress/` holds scanned PDFs and image-based documents while their three vision-AI extraction passes are being compared. Do not scan or move these files through the ordinary intake workflow until comparison is complete.
- `to-ingest/not-done/` holds intake deferred for follow-up work. Do not automatically retry these files; return a file to the root intake queue only when it is ready to be processed.
- `to-ingest/processed/YYYY-MM/` holds successfully ingested originals. Do not scan this subtree as pending intake.
- `to-ingest/failed/` holds originals that could not be ingested. Do not retry these automatically; log the failure and leave the original intact.
- `corpus/<category>/` contains normalized, RAG-ready artifacts grouped by the nature of their source material. Markdown is the default format; preserve a stable, readable filename and source reference.
- `logs/ingestion-log.md` is the append-only ingestion ledger. Create its parent folder and the file on the first ingestion if they do not exist.

Create missing workflow folders only when they are needed. Do not use `.obsidian/` for corpus content or ingestion records.

### Corpus categories

- Choose one primary category from the source's subject matter, purpose, and audience—not merely its file extension. Reuse an existing category whenever it fits.
- Create a new category only when no existing category accurately represents the material. Use concise, lowercase snake_case names, such as `research_papers/`, `technical_documentation/`, or `meeting_notes/`.
- Place each generated artifact in exactly one primary category. Do not duplicate artifacts across categories; record cross-cutting subjects in frontmatter instead.
- Use `corpus/uncategorized/` only when the source cannot be classified confidently. Include a brief reason in its ledger entry so it can be reviewed later.
- Do not create empty category folders in advance. Categories should emerge from the actual intake corpus.

### Filename normalization

- All ingestion-managed filenames and category-folder names must use lowercase `snake_case`. This applies to intake files when they are staged, generated corpus artifacts, and files moved to `processed/` or `failed/`.
- First separate the final extension, including a meaningful compound extension such as `.tar.gz`. Then lowercase the basename, replace every run of spaces or non-alphanumeric characters with one underscore, trim leading and trailing underscores, and append the lowercased extension.
- If normalization leaves an empty basename, use `document_<first_8_sha256>` as the basename after calculating the source hash.
- Retain the original received filename and relative intake path in artifact metadata and the ledger before normalizing it. The normalized name is operational; the original name is provenance.
- If normalization produces a name collision, compare source hashes. Reuse the existing successful ingestion for identical content; otherwise add a short SHA-256 prefix to the normalized basename before its extension. Never overwrite a collision.
- Do not rename fixed vault infrastructure explicitly established by this workflow, including `AGENTS.md`, `to-ingest/`, `image-compare-in-progress/`, `not-done/`, `corpus/`, and `logs/`.

## Source conversion and RAG readiness

- Expected intake includes PDFs, DOCX files, and similar office, text, presentation, spreadsheet, HTML, and image-based documents. Identify the actual format before selecting an extractor.
- Convert text-bearing sources to Markdown by default. Retain document structure needed for meaningful chunking: document title, headings, paragraphs, ordered and unordered lists, tables, quotations, code blocks, and link targets where available.
- Preserve the source's reading order and distinguish body text from headers, footers, captions, tables, and appendices when the extractor can do so. Do not silently invent, merge, or paraphrase missing content.
- For every scanned PDF or image-based document, run three independent vision-AI extraction passes using three different vision models before accepting extracted text. Record each model's name, version, and configuration. If three distinct vision models are unavailable, do not mark the ingestion successful; log the limitation and route the source to `failed/`.
- Compare all three vision-AI outputs page by page for page count, reading order, non-whitespace character count, and substantive textual differences. Visually verify each materially divergent page against the source; do not treat repeated output as evidence when all three passes share the same likely error.
- Accept extracted text only after the three-pass comparison supports it. Use the best-supported reading, preserve uncertainty rather than guessing, and treat unresolved material discrepancies as an ingestion failure rather than producing a misleading corpus artifact.
- Record each vision model's version, configuration, completion result, comparison outcome, and any limitations in the ledger. Retain only the verified normalized artifact in `corpus/`; temporary pass outputs must not become corpus sources.
- Use another RAG-suitable format only where Markdown would materially lose useful structure, such as CSV or JSON for intrinsically tabular or structured data. Store a companion Markdown description when needed to make the artifact discoverable and chunkable.
- Keep generated artifacts chunking-friendly: use hierarchical headings, concise section boundaries, stable table representations, and fenced code blocks. Do not pre-split source content into arbitrary fixed-size chunks unless an ingestion task explicitly requires it.
- Preserve the original in `to-ingest/processed/YYYY-MM/`; the corpus artifact is a normalized derivative, not a replacement.

### Approved conversion tools

- Use [MarkItDown](https://github.com/microsoft/markitdown) as the default local converter for supported PDFs, Word documents, presentations, spreadsheets, HTML, and text-based formats. Invoke it as `python -m markitdown` so the active Python environment is used even when its scripts directory is not on `PATH`.
- Install the document-format extras with `python -m pip install "markitdown[pdf,docx,pptx,xlsx]"`. Use its Markdown output as a source-faithful starting point, then apply the RAG-readiness rules above; do not treat it as a substitute for the required vision-AI comparison.
- PaddlePaddle is an approved optional runtime for an OCR implementation. Install the pinned CPU build only from a Python environment compatible with its available wheels:

  ```bash
  python -m pip install paddlepaddle==3.3.1 -i https://www.paddlepaddle.org.cn/packages/stable/cpu/
  ```

- PaddlePaddle alone does not satisfy the three-pass requirement: a scanned PDF or image-based document still needs three distinct vision-AI models and the documented comparison before ingestion can succeed.

## Ingestion workflow

1. Inspect files directly in `to-ingest/`, excluding `image-compare-in-progress/`, `not-done/`, `processed/`, and `failed/`. Do not modify a file until its type, likely content, and destination are understood.
2. Check `logs/ingestion-log.md` for the same intake path and content hash before processing. Do not create duplicate corpus artifacts for an already successful ingestion.
3. Classify the source using the corpus-category rules; preserve its received path and filename as provenance; normalize its operational filename; then convert it to Markdown or another RAG-suitable format and save the result in `corpus/<category>/`.
4. Add YAML frontmatter to each generated Markdown artifact with at least `source_original_path`, `source_original_filename`, `source_normalized_filename`, `source_type`, `source_sha256`, `ingested_at`, `category`, `topics`, and `extraction_method`. Use ISO 8601 timestamps with a timezone.
5. After the corpus artifact is written successfully, append a log entry and then move the original to `to-ingest/processed/YYYY-MM/`. Never delete an original as part of ingestion.
6. If processing fails, do not create a partial corpus artifact. Move the original to `to-ingest/failed/` only when safe, append a failure entry, and record a concise actionable reason.

## Ledger format

Keep `logs/ingestion-log.md` append-only. Add one dated section per ingestion attempt, using this shape:

```markdown
## 2026-07-28T10:39:35+01:00 — success

- Intake path: `to-ingest/example.pdf`
- Processed path: `to-ingest/processed/2026-07/example.pdf`
- Corpus artifact: [[corpus/example]]
- SHA-256: `<hex digest>`
- Source type: `application/pdf`
- Notes: `Extracted text; retained original.`
```

For failures, set the status to `failed`, omit `Corpus artifact`, and state the error in `Notes`. Use wiki-links for vault artifacts and backticked relative paths for non-Markdown files.

## Safety and quality rules

- Never overwrite, rename, or delete existing corpus artifacts or log entries without explicit instruction.
- Always log any and all content verbatim
- Preserve directory context when filenames would otherwise collide.
- Keep ingestion output focused on source fidelity; do not add unsupported interpretations, summaries, or metadata.
- Before reporting an ingestion complete, verify that the corpus artifact exists, the ledger entry is present, and the original is in its recorded location.
