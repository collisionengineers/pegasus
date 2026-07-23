# CollisionSpike v2 architecture

This directory contains the mutable technical architecture for CollisionSpike v2.
Business requirements and operating rules remain under `docs/operator-notes` and
must not be edited from here.

## Architecture decisions

| Decision | Status | Summary |
| --- | --- | --- |
| [ADR-0001: Hybrid PDF extraction](decisions/ADR-0001-hybrid-pdf-extraction.md) | Accepted; PDF engine selection pending | Use a proven PDF engine, custom deterministic provider rules, and Azure Read OCR only when required. |
| [ADR-0002: .NET modular monolith on Azure App Service](decisions/ADR-0002-dotnet-modular-monolith-on-azure.md) | Accepted | Build one .NET application core with Razor Pages, Azure SQL, F1/B1 App Service, and a Functions worker. |

## Open architecture work

1. Select the embedded PDF engine through a benchmark using genuine QDOS documents.
2. Scaffold the accepted solution boundaries, validation harness, and Bicep/`azd` infrastructure skeleton.
3. Define and build the first end-to-end QDOS vertical slice from genuine repository-provided examples.
