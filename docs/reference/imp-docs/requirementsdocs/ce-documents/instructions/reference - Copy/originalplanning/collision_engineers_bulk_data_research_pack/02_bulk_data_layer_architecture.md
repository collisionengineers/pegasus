# Bulk Data Layer Architecture

## Concept

Create a dedicated bulk-data layer that sits beside the current CE Document Mapper, job sheet, Box storage, and EVA workflow. The layer should enrich cases and surface review flags, not replace engineer judgement.

## Why this matters

The Phase 5 plan lists multiple data sources. Without a common architecture, each source will become a separate script and the outputs will be inconsistent. CE needs one canonical enrichment layer that can be reused by:

- CE Document Mapper JSON export.
- Future Outlook intake pipeline.
- EVA / Sentry API adapter.
- Engineer pack generator.
- Valuation evidence workflow.
- Management dashboards.

## Proposed architecture

```text
External/public data sources
    -> ingestion workers
    -> raw landing tables/files
    -> normalised reference tables
    -> case enrichment service
    -> review flags + evidence snippets
    -> engineer/admin UI, JSON export, EVA payload, reports
```

## Core storage zones

### 1. Raw landing zone

Stores data exactly as received:

- MOT bulk file.
- MOT delta files.
- DVLA VES responses.
- EVA report export JSON.
- valuation API responses.
- road-safety CSVs.
- weather lookup responses.

Each raw object needs:

- source name.
- source URL/API endpoint.
- retrieval time.
- file checksum or response hash.
- licence/usage status.
- processing status.

### 2. Normalised reference zone

Stores queryable facts:

- normalised VRM.
- vehicle make/model/fuel/year/body type.
- MOT tests, odometer readings, advisories and defects.
- valuation source, value, date, mileage, model/version assumptions.
- accident/weather/traffic context.

### 3. Case enrichment zone

Stores case-specific outputs:

- estimated mileage.
- mileage confidence.
- mileage anomaly flag.
- maintenance condition indicator.
- vehicle identity conflicts.
- likely evidence gaps.
- valuation evidence summary.
- prior case/history links.

### 4. Audit and review zone

Stores decision support outputs:

- flags raised.
- evidence used.
- human reviewer decision.
- changed fields.
- values accepted/rejected.
- timestamp and actor.

## MVP data tables

```sql
vehicle_profile(
  normalised_vrm,
  make,
  model,
  colour,
  fuel_type,
  year_of_manufacture,
  first_registration_month,
  source,
  last_refreshed_at
)

mot_test(
  normalised_vrm,
  test_date,
  expiry_date,
  result,
  odometer_value,
  odometer_unit,
  odometer_result_type,
  defects_json,
  advisory_json,
  source_file_id
)

case_enrichment(
  case_id,
  normalised_vrm,
  enrichment_version,
  mileage_estimate,
  mileage_estimate_band_low,
  mileage_estimate_band_high,
  flags_json,
  evidence_json,
  created_at
)
```

## Implementation notes

- Use normalised VRM as the main join key, but do not treat it as perfect identity. Vehicles can have plate changes; MOT vehicle IDs and VIN data, where available and lawful, can improve identity resolution.
- Keep raw responses. If a data source changes schema, historical enrichments can still be explained.
- Do not store full source emails or personal data in generic logs.
- Use schema versions for enrichment outputs so engineer packs can identify which model or ruleset generated a flag.

## Human review model

Every enrichment output should be labelled as one of:

- `info_only`
- `review_recommended`
- `blocking_missing_data`
- `conflict_requires_resolution`

Avoid labels such as `fraud` or `wrong`. Use neutral language such as `mileage_variance_requires_review`.

## Opportunity

The architecture can become CE’s internal historical intelligence layer. Once report outcomes, claim types, valuations, salvage values and repair-cost outcomes are stored, CE can create its own domain-specific dataset that is more valuable than external sources alone.


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
