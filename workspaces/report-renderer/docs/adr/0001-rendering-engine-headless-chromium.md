# 0001 — Rendering engine: headless Chromium via Playwright

## Status

Accepted

## Context

Collision Renderer must produce branded PDF documents — valuation reports, advert evidence
packs, fee notes and expert reports — that go to courts, solicitors and insurers and must be
faithful to the firm's house style every time. Two facts shape the rendering choice:

1. The brand's design system (`collision-engineers-design`) is **CSS-native**, and the
   preferred sample outputs were produced by an HTML/CSS renderer (the prior Python
   `report-renderer`, built on WeasyPrint). The look is defined in CSS, not in a procedural
   layout API.
2. The product must run as a clean Windows desktop application **and** in a Linux container,
   from a single shared engine, with no fragile native dependencies.

The prior WeasyPrint pipeline relied on GTK/Pango native libraries. On Windows this meant
hunting for MSYS2 DLLs and falling back to ReportLab when they could not be found — fragile
and inappropriate for a clean Windows desktop app.

## Decision

Render HTML to PDF using **headless Chromium driven by Microsoft.Playwright**. The
`ChromiumPdfEngine` is the default `IPdfEngine` implementation behind `IDocumentRenderer`.
The pipeline is: typed C# document models → HTML (Scriban templates + the brand CSS) →
PDF via headless Chromium. Chromium and its dependencies are provisioned on first use
(`collisionrenderer install-browser`, ~90 MB) and, in the cloud, by the Playwright base image.

## Consequences

- Exact fidelity to the CSS design system, because the document is laid out by the same
  browser engine the design was authored against.
- Cross-platform and self-contained: the same `CollisionRenderer.Core` engine runs on
  Windows and in a Linux container with no GTK/Pango/MSYS2 hunting and no ReportLab fallback.
- New templates are cheap to add, since layout is CSS rather than imperative drawing code.
- The engine is large: a ~90 MB Chromium download is required on first run, and the cloud
  image bundles a browser. This is an accepted cost for fidelity and portability.
- The PDF backend is abstracted behind `IPdfEngine`, so tests can substitute a
  `FakePdfEngine` and the Chromium dependency is not on every test's hot path.

## Alternatives considered

- **WeasyPrint (the prior renderer):** CSS-capable, but depends on GTK/Pango native
  libraries that are fragile on Windows (MSYS2 DLL hunting, ReportLab fallback). Rejected as
  unsuitable for a clean Windows desktop app.
- **QuestPDF:** a strong .NET PDF library, but its fluent layout API would mean
  re-implementing the brand look in C# and discarding the CSS design system. Rejected.
- **PdfSharp:** low-level PDF construction; even further from the CSS design system and far
  more code to reach parity. Rejected.
