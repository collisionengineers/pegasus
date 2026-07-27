# Documents and integrations

## Outcome

Pegasus processes supported sources without losing occurrences or provenance,
stores long-term originals and case files in Box, enriches vehicle data where
authorized, and produces the bounded manual EVA handoff required before any
later downstream replacement.

## Settled requirements

- Supported email/document/image shapes preserve original sources, visible
  occurrences, extraction evidence and bounded failure outcomes.
- Approved provider spreadsheets produce immutable cumulative provider-domain
  snapshots with source provenance. Reference presence is candidate evidence,
  not route activation or automatic provider resolution.
- Inspection-location/repairer reference data, history, defaults and Case-ID
  mapping remain deferred pending accepted evidence and activation.
- Box is long-term case-file custody; SQL owns workflow identity/history;
  transient Azure storage is not long-term custody.
- Vehicle enrichment uses DVLA/DVSA and MOT evidence where available without
  replacing operator review or inventing missing facts.
- Estimating, valuation, finance/invoicing, report generation, WhatsApp
  automation and additional providers activate independently.

## Current manual EVA handoff

`EXT-03` produces deterministic UTF-8 JSON after the pre-assignment review gate,
with these keys in this exact observed order:

1. `Work Provider`
2. `VRM`
3. `Vehicle Model`
4. `Claimant Name`
5. `Reference`
6. `Incident Date`
7. `Instruction Date`
8. `Inspection Date`
9. `Inspection Address`
10. `Accident Circumstances`
11. `VAT Status`
12. `Mileage`
13. `Mileage Unit`

The values are operator-reviewed case data, not a vendor schema promoted into
Core. The bundle also contains only selected custody-confirmed images and a
SHA-256 manifest. Generation makes no EVA network call. First success records
the once-per-case handoff proxy; retry/regeneration records a revision without
duplicating that event.

This remains the current handoff until the EVA development team provides a
usable operation and a separate change accepts the exact contract, caller,
coexistence, recovery and live evidence. EVA retains assignment, estimating,
valuation and reporting until each slice transfers authority independently.

## Repair specification and reports

- `EXT-06` replaces EVA estimating; `EXT-09` owns versioned repair-estimate
  lines, source versions, approvals and original-versus-assessed comparison.
- `EXT-12` owns Audatex/PDF ingestion and must retain the source artifact and
  mapped version.
- `ENG-01` owns one canonical repair specification with route provenance for
  Glass's, Audatex PDF or an approved AI proposal.
- `EXT-08` activates deterministic report generation through `RPT-01`–`RPT-05`.
  Source availability does not prove a caller.

## Valuation evidence and Engineer decision

Valuation is a versioned evidence/decision flow, not a vendor call. Every source
observation retains:

- source key and vehicle identity;
- mileage and unit;
- guide/effective date or month/code;
- retail, trade, mid, private, disposal, research and advert values when
  supplied;
- the source artifact, retrieval/entry time and immutable version.

The Engineer explicitly accepts one source/version or enters a supported value,
then records original/final value; mileage, condition, other, modification and
previous-total-loss adjustments; rationale; and revaluation history. No
adapter, highest/lowest/first-source rule or missing value chooses the
Engineer's decision.

Derived pre-accident value, repair-cost percentage, net-of-salvage percentage
and equity consume only accepted estimate, valuation and salvage versions and
are computed once. CAP, Glass's and Cazana are accepted dependency candidates.
EVA-observed `VEHICLE DATA`, Parkers and AutoTrader remain source evidence until
separately selected. `EXT-07` replaces EVA valuation, `EXT-10` owns evidence and
Engineer acceptance, and `EXT-13` owns independently licensed source adapters.

## Accounts and invoicing

`EXT-11` retains versioned fee/invoice number, recipient/principal, report type,
net, VAT rate, VAT, total, credited/paid/outstanding amount, date, reference and
status, plus Engineer cost/payment inputs with role-restricted visibility.
Invoice generation consumes accepted per-principal report events and fee rules.
Screenshots are field evidence, not authority for formulas or permissions.

## Data-handling activation boundary

Personal data and vehicle images retain role-based access protection across
email, request-scoped upload, AI processing and Box. Before each external flow
is activated, its owning change must record the applicable retention rule and
confirm processor terms. Until those inputs are accepted, that external flow
remains off. This policy does not activate the `DOC-12` automated-retention or
`DOC-15` dedicated-compliance workflows; both remain `Not planned` boundaries.

## Current state and activation

Repository ports, adapters, imported libraries and source readers are evidence,
but no production Graph, Box-write, vehicle-data, EVA, renderer, valuation,
invoice or estimating caller is accepted or deployed. Activation names the
external contract, permission/credential owner, idempotency, failure/recovery,
caller and separately authorized live validation.
