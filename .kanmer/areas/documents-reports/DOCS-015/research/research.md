# Research — DOCS-015

## Question

How can the EVA API PDF be converted into normalized Markdown without losing
source data or interfering with the active EVA implementation ticket?

## Findings

- The source is a 99-page A4 PDF titled *Sentry API Documentation*, authored by
  Minotaur Software Ltd and labelled version 1.2 in page furniture.
- Extractable text totals 89,951 characters. Four embedded images occur on
  pages 1 and 61; nine link annotations occur on page 2.
- The PDF uses a single dominant 12-point font with four 18-point heading spans,
  so semantic structure must also be inferred from recurring labels, endpoint
  lines, tables and page position.
- Extracted glyph mapping replaces bullets and some punctuation with replacement
  characters. Visual checks are required to normalize those glyphs without
  dropping surrounding text.
- The repeated page number and document-version furniture can be normalized into
  one source note while retaining the version and original page boundaries as
  HTML comments.
- [[TICK-077]] owns implementation. This ticket changes only the source reference
  PDF and its Markdown transcription.

## Implications

Generate one Markdown file beside an unchanged copy of the PDF. Preserve every
meaningful heading, field, type, required marker, description, example, URL and
enumerated value. Retain source-page boundaries for auditability, and validate
page-by-page text coverage plus all images and annotations.
