## Simplification pass (2026-09-03)

Run by the implementer (gpt-5.6-sol medium, in-flow with the implementation)
over the complete branch diff at completion, using reuse, simplification,
efficiency and altitude lenses:

- **Fixed** — the generic AI-job completion path (`pegasus_ai_job_complete` /
  the generic Core completion use case) initially admitted `MarketResearch`
  after the result vocabulary expanded to include it. Corrected so Core
  explicitly refuses `MarketResearch` on the generic path, and a regression
  test proves the typed `pegasus_ai_job_complete_market_research` tool is the
  only completion route for this kind.
- **Fixed** — removed a tautological Core assertion added in an earlier draft
  of the completion use case.
- **Kept** — one specialised EF transaction
  (`EfMarketResearchAiJobCompletionStore`) is necessary because the separate
  existing stores (`EfAiJobStore`, `EfValuationStore`,
  `EfDocumentCustodyStore`) cannot atomically compose Case version/lease,
  custody content, valuation, and job transition across a single serializable
  transaction; the shared custody-preparation helper (refactored out of
  `EfDocumentCustodyStore`) avoids a second implementation of document
  hashing/addressing/entity creation rather than duplicating it.
- **Kept** — typed persisted result columns (rather than generic JSON/text)
  are required for exact operation-key replay and to identify the retained
  document/valuation without re-parsing free text.
- No unapplied findings; no scope drift found (guide month, `ValuationSource`
  label map, and the Valuation-section caller stayed out, per the plan's
  Dependencies section).
