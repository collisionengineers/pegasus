# 0006 — Page furniture via Chromium header/footer templates + paged-media CSS

## Status

Accepted

## Context

Documents must hold their house style across multiple pages. A valuation with a long
comparable-advert table, or a long expert report, will overflow a single A4 page, and when it
does the firm's furniture must remain correct on every page: the running footer (a thin red
rule, the `Collision Engineers Ltd | www.CollisionEngineers.co.uk | engineers@…` strapline —
swapped for the VAT number on fee notes — and an `— n of N —` page marker), repeating table
headers, and blocks that must never be split across a page boundary. A requirement of the
product is explicit: **long documents must not garble the look.**

## Decision

Handle page furniture with Chromium's paged-media facilities, configured by `PdfPageSettings`
and applied by `ChromiumPdfEngine`:

- **Chromium running header/footer templates** (`PdfPageSettings.HeaderHtml` /
  `FooterHtml`) repeat on every page and carry the page-number markers, on an `@page` A4 box
  with the configured margins.
- **Paged-media CSS** controls flow within the body:
  - `thead { display: table-header-group }` so table headers repeat across page breaks;
  - `break-inside: avoid` on table rows, value boxes and media rows so they are never split.

This was validated: a 36-row valuation flows to three pages with a repeating header and
footer and no garbling.

## Consequences

- Multi-page documents keep the firm's furniture intact — repeating footers with correct
  `— n of N —` markers, repeating table headers, and unbroken rows/value boxes/media rows.
- The behaviour is declarative (CSS + header/footer templates) rather than custom pagination
  code, so it follows the design system and is cheap to extend to new templates.
- Footer content is per-document (e.g. the fee-note VAT-number variant), driven by
  `PdfPageSettings.FooterHtml` supplied by the composer.
- The approach depends on Chromium's print pipeline; correct margins (notably
  `MarginBottom = 22mm` to clear the running footer) must be respected when composing.

## Alternatives considered

- **Single-flow HTML with no paged-media rules:** simplest, but rows split across pages and
  headers/footers do not repeat — exactly the garbling the requirement forbids. Rejected.
- **Manual pagination in C# (pre-splitting content into per-page chunks):** brittle, must
  re-measure on every content change, and duplicates what the browser's print engine already
  does well. Rejected.
- **Drawing furniture per page in a layout API:** ties back to QuestPDF/PdfSharp, already
  rejected for discarding the CSS design system (ADRs 0001, 0005). Rejected.
