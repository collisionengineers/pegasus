## Files — TICK-022 (EXT-03) — retrospective backfill

| Path | Why |
|---|---|
| `src/Pegasus.Core/Eva/EvaBundleSchema.cs` | Owns the 13-field ordered schema, deterministic JSON/manifest writer, SHA-256 hashing. |
| `tests/Pegasus.Core.Tests/Qdos/EvaBundleContractTests.cs` | Contract test asserting exact field count/order and byte-identical replay. |
| `src/Pegasus.Web/Pages/Cases/Eva/Download.cshtml.cs` | Staff UI caller. |
| `src/Pegasus.Web/Mcp/AssessmentMcpTools.cs` | `pegasus_eva_bundle_generate` MCP caller — same Core path. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/20260819180000_GrantEvaHandoffDownloadOperations.cs` | Release-12 grant repair for the download path. |

No source change is proposed; this ticket reconciles the board record with already-shipped code.
