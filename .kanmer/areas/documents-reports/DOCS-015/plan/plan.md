# Plan — DOCS-015

## Approach

Copy the supplied PDF byte-for-byte into the ticket worktree, extract all 99
pages with the installed PDF libraries, and convert layout into one normalized
Markdown document. Preserve page boundaries and every meaningful source datum.
Use visual renders for pages containing images and representative dense tables,
then run automated page coverage, field/example, link, image and replacement
glyph checks.

## Steps

1. Create the isolated ticket worktree and verify the copied PDF hash matches
   the operator-supplied file.
2. Extract page text, spans, tables, links and images without altering the PDF.
3. Normalize headings, whitespace, lists, code examples and tables while
   retaining page-boundary comments and source metadata.
4. Resolve extraction replacement glyphs by visual inspection; never delete
   surrounding content or silently guess an API field.
5. Compare every PDF page against its Markdown section and confirm all source
   fields, examples, URLs, enumerated values, annotations and image information
   are represented.
6. Run Markdown format checks, inspect the final diff, and record verification.

## Governing docs

The output is a reference transcription supporting FRD-07. It does not change
the FRD, the EVA contract, or [[TICK-077]]'s implementation.

## Risks

- Layout-driven tables may span pages; page markers and row-level coverage
  checks prevent dropped continuations.
- Corrupt glyph mappings may obscure punctuation; visual renders decide the
  normalized character and unresolved glyphs fail verification.
- Repeated page furniture may appear as content; its version is retained once
  and page numbering remains auditable through comments.
