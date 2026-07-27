# Implementation Roadmap for Bulk Data Usage

## Concept

A staged build plan for turning Phase 5 from ideas into operational tools without destabilising current CE workflows.

## Phase A — Foundation and ingestion

### Build

- Source registry.
- Raw landing zone.
- MOT bulk/delta ingestion.
- DVLA VRM cache.
- Review flag table.
- Case enrichment object.

### Exit criteria

- MOT data can be queried by VRM.
- DVLA response can be cached by VRM.
- Basic case enrichment object can be generated.
- All outputs include evidence and source metadata.

## Phase B — High-value deterministic enrichments

### Build

- mileage estimate engine;
- mileage anomaly review;
- vehicle identity mismatch flags;
- MOT condition summary;
- missing-evidence checks.

### Exit criteria

- Every new case can show vehicle/mileage/MOT context.
- No automatic report wording without review.
- Staff can accept/reject flags.

## Phase C — Integration with current tools

### Build

- CE Document Mapper sidecar audit output;
- enrich JSON exports after mapper processing;
- engineer pack enrichment blocks;
- job sheet mirror/export;
- provider/preset quality dashboard.

### Exit criteria

- Mapper outputs can feed the bulk-data layer.
- Corrections are captured.
- Provider template quality can be measured.

## Phase D — EVA/Sentry report mining

### Build

- `GetAvailableReports` sync;
- `GetReport` retrieval worker;
- raw report store;
- report summary normalisation;
- historical same-VRM search;
- valuation/salvage history.

### Exit criteria

- Released EVA reports are searchable.
- Historical valuation/salvage values are available for review.
- Prior case review pack works for same VRM.

## Phase E — Market and external context

### Build

- approved valuation API integration;
- valuation evidence store;
- salvage data integration, if licensed;
- road-safety taxonomy;
- weather enrichment only where source availability is confirmed.

### Exit criteria

- Valuation evidence can be stored with source/mileage/date.
- External data terms are documented.
- Engineer pack can show valuation-source variance.

## Phase F — Advanced analytics and modelling

### Build

- cohort mileage models;
- repair-cost benchmarks;
- duplicate image detection;
- workload forecasting;
- management dashboards;
- model monitoring.

### Exit criteria

- False-positive rates measured.
- Human review outcomes feed rule/model improvement.
- Dashboards prove time saved and quality improvement.

## Recommended first 30 days

1. Confirm DVSA API access.
2. Build MOT ingestion spike.
3. Build DVLA lookup/cache spike.
4. Create `review_flag` schema.
5. Enrich 20–50 historical/sample cases manually.
6. Validate mileage estimate logic against known dashboard mileages.

## Recommended first 60 days

1. Add CE Document Mapper sidecar audit export.
2. Build deterministic mileage and identity flags.
3. Add engineer pack enrichment prototype.
4. Mirror job sheet operational fields.
5. Start provider/preset quality dashboard.

## Recommended first 90 days

1. Add Sentry/EVA report retrieval sync.
2. Build prior-case search.
3. Build valuation evidence store.
4. Run shadow-mode trial.
5. Create go/no-go metrics for assisted mode.

## Success metrics

- fewer missing mileages;
- lower manual lookup time;
- higher extraction accuracy;
- fewer late/duplicate cases;
- documented valuation evidence;
- engineer pack flags accepted as useful;
- no uncontrolled automated conclusions.


## Source notes

Internal sources used: `phase_bulk_data.md`, `claudechat.md`, `handover.docx`, `EVA User Guide.pdf`, `Sentry_API_Complete_Guide.md`, `evaapidocs.pdf`, and the two Collision Engineers context packs.

Public/researched sources used across this pack:

- DVSA new MOT history API documentation: https://documentation.history.mot.api.gov.uk/
- DVSA bulk/delta download documentation: https://documentation.history.mot.api.gov.uk/mot-history-api/download-vehicle-mot-history-data/
- DVSA MOT history API authentication: https://documentation.history.mot.api.gov.uk/mot-history-api/authentication/
- DVSA MOT history API rate limits: https://documentation.history.mot.api.gov.uk/mot-history-api/rate-limits/
- GOV.UK MOT history service: https://www.gov.uk/check-mot-history
- DVLA Vehicle Enquiry Service API Guide: https://developer-portal.driver-vehicle-licensing.api.gov.uk/apis/vehicle-enquiry-service/vehicle-enquiry-service-description.html
- DfT Road Safety Open Data: https://www.gov.uk/government/statistical-data-sets/road-safety-open-data
- DfT Road Traffic Statistics data downloads: https://roadtraffic.dft.gov.uk/downloads
- OS Open Roads: https://www.ordnancesurvey.co.uk/products/os-open-roads
- Met Office Weather DataHub: https://datahub.metoffice.gov.uk/
- Met Office observations overview: https://datahub.metoffice.gov.uk/docs/g/category/observations/overview
- ICO anonymisation guidance: https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/data-sharing/anonymisation/
- ICO personal data guidance: https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/personal-information-what-is-it/what-is-personal-data/what-is-personal-data/
- National Archives Open Government Licence `Later`/`unallocated`.0: https://www.nationalarchives.gov.uk/doc/open-government-licence/version/3/
- Auto Trader Connect valuations overview: https://help.autotrader.co.uk/hc/en-gb/articles/21923133513117-Introduction-to-Current-Valuations
- Auto Trader Historic Valuations overview: https://help.autotrader.co.uk/hc/en-gb/articles/21945683119389-Introduction-to-Historic-Valuations
- Glass's / Autovista API overview: https://glass.co.uk/product/autovista-api/
