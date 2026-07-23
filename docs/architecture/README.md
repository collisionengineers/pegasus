# CollisionSpike v2 architecture

This directory contains the mutable technical architecture for CollisionSpike v2.
Business requirements and operating rules remain under `docs/operator-notes` and
must not be edited from here.

## Architecture decisions

| Decision | Status | Summary |
| --- | --- | --- |
| [ADR-0001: Hybrid PDF extraction](decisions/ADR-0001-hybrid-pdf-extraction.md) | Accepted | Use an embedded-text engine, custom deterministic provider rules, and Azure Read OCR only when required. |
| [ADR-0002: .NET modular monolith on Azure App Service](decisions/ADR-0002-dotnet-modular-monolith-on-azure.md) | Accepted | Build one .NET application core with Razor Pages, Azure SQL, F1/B1 App Service, and a Functions worker. |
| [ADR-0003: PdfPig for the first QDOS embedded-text slice](decisions/ADR-0003-pdfpig-for-first-qdos-slice.md) | Accepted for the local slice | PdfPig won the genuine-QDOS embedded-text comparison; production still requires a human-reviewed field cohort and untouched holdout. |

## Open architecture work

1. Prove PdfPig and the deterministic field rules against a frozen human-reviewed expected-value cohort and untouched holdout; reopen ADR-0003 if another engine materially reduces silent errors.
2. Prove migrations and reference allocation against SQL Server/Azure SQL, including real concurrency and duplicate delivery.
3. Add authenticated staff operation and durable original-source custody before enabling intake in a deployed environment.

The complete product gap is maintained in `docs/plans/remaining-requirements.md`.
