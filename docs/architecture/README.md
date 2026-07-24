# CollisionSpike v2 architecture

This directory contains the mutable technical architecture for CollisionSpike v2.
Business requirements and operating rules remain under `docs/operator-notes` and
must not be edited from here.

## Architecture decisions

| Decision | Status | Summary |
| --- | --- | --- |
| [ADR-0001: Hybrid PDF extraction](decisions/ADR-0001-hybrid-pdf-extraction.md) | Accepted | Use an embedded-text engine, custom deterministic provider rules, and Azure Read OCR only when required. |
| [ADR-0002: .NET modular monolith on Azure App Service](decisions/ADR-0002-dotnet-modular-monolith-on-azure.md) | Accepted; API/MCP authentication partially superseded | Build one .NET application core with Razor Pages, Azure SQL, F1/B1 App Service, and a Functions worker. |
| [ADR-0003: PdfPig for the first QDOS embedded-text slice](decisions/ADR-0003-pdfpig-for-first-qdos-slice.md) | Accepted for the local slice | PdfPig won the genuine-QDOS embedded-text comparison; production still requires a human-reviewed field cohort and untouched holdout. |
| [ADR-0004: Provider API and staff MCP authentication](decisions/ADR-0004-provider-api-and-staff-mcp-authentication.md) | Accepted | Keep the principal-scoped provider API separate from a per-staff OAuth-authorised remote MCP surface. |
| [ADR-0005: Multi-format intake and review assets](decisions/ADR-0005-multiformat-intake-assets.md) | Accepted for the local slice; decision 1 superseded by ADR-0006 | Extract EML/PDF/DOCX and discrete images through one intake route, defer DOC/MSG parsing, and restrict OCR candidates to scan-like PDF pages. |
| [ADR-0006: Provider-neutral intake with a contained QDOS policy](decisions/ADR-0006-provider-neutral-intake-with-contained-qdos-policy.md) | Accepted for the pre-release local slice | Keep reusable intake contracts, persistence, and callers provider-neutral while retaining QDOS as the sole concrete extraction policy. |

## Open architecture work

1. Prove PdfPig and the deterministic field rules against a frozen human-reviewed expected-value cohort and untouched holdout; reopen ADR-0003 if another engine materially reduces silent errors.
2. Prove migrations and reference allocation against SQL Server/Azure SQL, including real concurrency and duplicate delivery.
3. Replace ignored local artifact retention with managed-identity Blob staging and Box source custody before enabling intake in a deployed environment.
4. Implement targeted Document Intelligence OCR through the Worker for the persisted scan-like PDF page candidates.
5. Implement and independently verify the ADR-0004 provider API and staff OAuth/MCP boundaries through the Web composition root and shared Core use cases.

The complete product gap is maintained in `docs/plans/remaining-requirements.md`.
