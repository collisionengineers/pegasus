# Proof — ENG-002

Type: command-log + visual. Released in **release 14** (`d91fd7d7…`, PR #455), production smoke passed 2026-08-20; promoted to `main` (`39bb118a`).

- Live: "Import estimate" control on the QDOS26002 assessment record bar (Engineer-gated), estimate tab reachable.
- Verification lane at the cut: `IEstimateDocumentParser` Core port; `AudatexEstimatePdfParser` fail-closed (non-PDF / non-Audatex / section-checksum mismatch → `EstimateParseRejectedException`, nothing retained, honest one-sentence operator copy); parse-first handler with custody + `StartDraftAsync` provenance; Engineer-only accept with typed five-bucket basis + VAT; MCP route via existing `pegasus_assessment_update`. Glass's absence is fail-closed (single registered parser rejects politely) — awaiting the operator's real Glass's export sample.
- Full transcript: DELIV-013 scratch.
