---
id: ADR-0050
status: accepted
date: 2026-09-13
supersedes: [ADR-0028]
superseded_by: []
related_capabilities: [EXT-08, RPT-01, RPT-02]
related_frd: [frd-11]
tags: [architecture, renderer, questpdf, reports]
---

# ADR-0050: Render reports with QuestPDF inside the application

## Status

Accepted. This record refines ADR-0025 (the renderer stays behind the
`Pegasus.Core` render contract inside the application) and supersedes ADR-0028
(the Chromium execution boundary).

## Context

ADR-0028 rendered reports by composing HTML from Scriban templates and printing
it to PDF with a pinned headless Chromium through Playwright. That required a
custom Linux container image with the browser, its native libraries and fonts,
and made the Web boundary a container for that reason alone. The operator has
chosen to leave container hosting (ADR-0049) and to replace the browser with
QuestPDF, a .NET layout library that renders through SkiaSharp, which the
Infrastructure project already references for image thumbnails.

FRD-11 governs the output: one assessment-report layout for every outcome,
Audit parity, the sign-off tuple, the fee note, deterministic and versioned
generation returning bytes, SHA-256, page count, template version and engine
version, and fail-closed behaviour on missing or unsupported inputs. None of
these rules name the engine.

## Decision

`IAssessmentReportRenderer` is implemented by a QuestPDF renderer in
`Pegasus.Infrastructure`. The assessment report and fee note are composed as
QuestPDF documents from the accepted `AssessmentReportSnapshot`, on A4 with the
existing margins, two type registers and "page x of y" footers. Fonts are
embedded resources registered with QuestPDF (Liberation Sans, metric-compatible
with the Arial stack the templates used); no system font is relied on. The
logo remains an embedded resource; signature and evidence image bytes are
supplied per render. `EngineVersion` identifies QuestPDF and its version. The
page count is read from the produced PDF with PdfPig; PDFsharp and Playwright
leave the solution. The QuestPDF Community licence is declared in code at
composition, as the operator's organisation qualifies.

The HTML and CSS templates, the Playwright package, the browser base image,
the local browser install and doctor steps, and the `.playwright` artifact
assertions are removed.

## Consequences

- The Web boundary has no native browser dependency and can be code-deployed
  (ADR-0049); render memory and cold-start cost fall.
- Report layout is C# composition, versioned with the application; template
  changes are code changes with tests, not stylesheet edits.
- Rendered bytes are not byte-identical across renders (document metadata),
  as before; identity remains the SHA-256 of the produced artifact, re-verified
  by Core.
- A move to a different engine again requires a new accepted ADR.
