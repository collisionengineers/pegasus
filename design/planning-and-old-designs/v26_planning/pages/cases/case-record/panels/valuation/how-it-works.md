# Valuation — how it works today

Read from the live source on 13 September 2026 (`src/Pegasus.Web/Pages/Cases/Shared/_CaseValuation.cshtml`, `src/Pegasus.Core/Assessment/Valuations.cs`).

- Sources are Glass's, Cazana, Engineer's Value, AI market research, Brego and Super CAP (`ValuationSource`). A valuation is a record of source, date and time entered, mileage, retail, trade and an optional **guide month** held as the first day of the month, "the month the guide figure was published".
- Every valuation is typed in by hand through **Add valuation** (Source, Guide month as a month input, Mileage, Date, Time, Retail, Trade). Nothing on the page fetches a figure from a source; Glass's here is a guide value typed from the guide, separate from Glass's estimating.
- The calculator (basis card, commercial VAT, previous total loss, presets, deduction, Apply as Engineer's Value) is the Core policy shape, already mocked in v25 and v26.
- Governing: [FRD-06 § Valuation sources](../../../../../../../../docs/frd/frd-06-vehicle-and-engineering-evidence.md#valuation-sources).
