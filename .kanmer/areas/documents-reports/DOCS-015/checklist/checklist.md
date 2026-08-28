# Checklist — DOCS-015

- [x] Copy the source PDF unchanged into the ticket worktree and verify its hash.
- [x] Extract all 99 pages, links and images.
- [x] Produce normalized Markdown with page-boundary traceability.
- [x] Resolve replacement glyphs through visual inspection.
- [x] Verify page-by-page data, fields, examples, URLs, enumerations and media coverage.
- [x] Run Markdown formatting and final diff checks.
- [x] Record the docs-only simplification pass and verification evidence.

## Progress

- 2026-08-28: The PDF artifact marker accepted only its built-in `pdf`
  format label; it was run successfully before authoring. The requested output
  remains Markdown.
- 2026-08-28: Visual review found blank extracted emoji glyphs in Required
  columns. Recovered 55 green checks as `Yes` and 553 red crosses as `No`.
- 2026-08-28: Simplification pass: n/a - docs-only extraction. The generator
  and render intermediates are ignored and not part of the staged diff.
