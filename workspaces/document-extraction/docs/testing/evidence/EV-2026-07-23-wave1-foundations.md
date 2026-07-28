# EV-2026-07-23 — Wave 1 hostile-input foundations

## Scope

- Units: `EXT-FND-001`, `EXT-FND-002`, `EXT-FND-003`, `EXT-MOD-001`.
- Host: Windows x64, .NET SDK `10.0.302`, `net10.0`.
- Test platform: Microsoft.Testing.Platform with MSTest SDK `4.0.2`.
- Inputs: controlled in-memory unit values and hostile boundary values only; no genuine corpus, `sample-doc-files/` or CollisionSpike input was inspected.

## Implemented boundary

- checked half-open byte ranges and random-access primitive reads;
- bounded sequential stream loading without stream ownership, including cancellation-token interruption of a blocked read for caller cancellation and monotonic deadline expiry;
- cumulative input/decoded/object/text/asset/depth budgets;
- cooperative caller cancellation and monotonic elapsed deadlines;
- canonical SHA-256 over already-bounded memory and length-prefixed, cross-platform filename-safe stable identities that reject Windows device basenames; no unbounded stream-hashing API is exposed;
- strict/replacing UTF-8, UTF-16 LE/BE and Windows-1252 with every invalid sequence's byte offset reported, plus FILETIME UTC conversion;
- versioned NFC and LF normalisation;
- immutable extraction inputs, policies, checked source locations, validated evidence collections, result-local unique asset identities, nested results and all ten outcomes; and
- source-generated deterministic semantic JSON with total evidence ordering, asset bytes excluded from JSON bundles and volatile elapsed milliseconds retained in memory but deliberately excluded from canonical semantic JSON.

The July 23 independent review initially rejected the foundation slice. This correction pass addresses its blocked-I/O cancellation, total-ordering, elapsed-time determinism, checked-range, unsafe stream-hash, materialisation, stable-ID, invalid-encoding-offset and invalid-public-state findings. Independent re-review remains required before restoring a `Locally verified` label.

## Validation

| Command | Exit | Result |
|---|---:|---|
| `dotnet build src\CollisionDocNet.Core\CollisionDocNet.Core.csproj --configuration Release` | 0 | Build passed with 0 warnings and 0 errors |
| `dotnet build src\CollisionDocNet.Model\CollisionDocNet.Model.csproj --configuration Release` | 0 | Build passed with 0 warnings and 0 errors |
| `dotnet test --project tests\unit\CollisionDocNet.Core.Tests\CollisionDocNet.Core.Tests.csproj --configuration Release` | 0 | 58 passed, 0 failed, 0 skipped; includes deliberately blocked stream cancellation/deadline tests and multi-error offset tests for every declared encoding |
| `dotnet test --project tests\unit\CollisionDocNet.Model.Tests\CollisionDocNet.Model.Tests.csproj --configuration Release` | 0 | 44 passed, 0 failed, 0 skipped; includes reversed-input byte tests for unique assets and same-hash nested results, duplicate asset-ID rejection, portable filename tokens and rejected public invalid states |

The requested standard .NET performance-pattern scan covered the Core and Model production C# files. Its checklist found: `IndexOf` without comparison 0; `Substring` 0; `StartsWith`/`EndsWith` without comparison 0; string `Contains` without comparison 0; `async void` 0; culture-case conversion 0; chained replacement 0; LINQ char scans 0; LINQ chains 0; mutable/frozen static dictionaries 0/0; per-call list/dictionary construction 0/0; `CurrentCulture` comparers 0; per-call JSON options 0; `HttpClient` construction 0; sync-over-async signals 0; old array-based async reads 0; and unsealed ordinary classes 0 versus 12 sealed classes. The one `params` hit is the allocation-free `params ReadOnlySpan<string>` stable-identity API. The one source-generated JSON call supplies its generated context. The four-byte `stackalloc` occurs in a short non-async helper invocation and not in a loop body.

## Limitations and next gates

- These are unit/local-verification results, not format conformance or acceptance.
- Additional code pages, format date syntaxes and parser-specific source locations remain owned by later format units.
- No fuzz campaign, corpus evaluation, semantic differential comparison, benchmark, concurrency-host measurement or independent API acceptance was performed.
- The default 10 MB input policy bounds retained input and the transient materialisation copy, but a separate measured process-memory and CPU budget is not implemented in this foundation slice; Wave 13 must establish and enforce those host bounds.
- Elapsed milliseconds are diagnostic in-memory telemetry, not part of canonical semantic JSON. A later diagnostic/bundle contract must decide how to persist volatile telemetry without weakening retry determinism.
- `ExtractionResult` guarantees unique asset identities within one result. The future `EXT-API-001` orchestrator and `EXT-CLI-002` bundle writer must preserve one collision-free identity namespace across the root result and every nested result before creating asset files.
- Wave 2 detection and container work is not implemented by this evidence.
- A full-solution or repository check was not rerun during this correction because other delegated waves were modifying independent projects concurrently. This pass proves only the focused Model tests plus the Core evidence already recorded above.
