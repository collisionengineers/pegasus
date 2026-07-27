# Bulk Data Schema Contracts

## Concept

Define schema contracts so that CE Document Mapper, EVA/Sentry sync, valuation tools and future dashboards can exchange data safely.

## Why this matters

The existing project direction already favours canonical JSON before mapping to EVA. Bulk data should follow the same principle. Each data product needs a stable contract.

## Core objects

### `vehicle_identity`

```json
{
  "normalised_vrm": "AB12CDE",
  "display_vrm": "AB12 CDE",
  "identity_confidence": "high",
  "sources": [],
  "conflicts": []
}
```

### `vehicle_profile`

```json
{
  "normalised_vrm": "AB12CDE",
  "make": "SKODA",
  "model": "SUPERB",
  "colour": "BLUE",
  "fuel_type": "DIESEL",
  "year_of_manufacture": 2012,
  "source": "dvla_ves",
  "retrieved_at": "2026-05-22T12:00:00Z"
}
```

### `mot_summary`

```json
{
  "normalised_vrm": "AB12CDE",
  "latest_test_date": "2025-02-01",
  "latest_mileage": 49200,
  "latest_result": "PASS",
  "reading_count": 8,
  "mileage_trend": {
    "annualised_mileage": 7200,
    "confidence": "medium"
  },
  "condition_signals": []
}
```

### `case_enrichment`

```json
{
  "case_id": "WI-2026-000123",
  "schema_version": "bulk-enrichment/1.0.0",
  "vehicle_identity": {},
  "vehicle_profile": {},
  "mot_summary": {},
  "mileage_estimate": {},
  "valuation_evidence": [],
  "review_flags": []
}
```

### `review_flag`

```json
{
  "flag_type": "reported_mileage_outside_mot_band",
  "severity": "review_required",
  "summary": "Reported mileage is lower than latest MOT mileage before incident.",
  "evidence": [],
  "suggested_action": "Check mileage unit and dashboard image before valuation.",
  "status": "not_reviewed"
}
```

## Versioning

Use semantic versioning:

- patch: documentation or optional field;
- minor: new optional field;
- major: field rename, type change or meaning change.

## Field rules

- Never store a model output without `method`, `source`, and `created_at`.
- Never store a review flag without evidence.
- Keep raw values and normalised values separate.
- Keep reported mileage and estimated mileage separate.
- Keep current incident address and inspection address separate.

## Integration boundaries

### CE Document Mapper -> Bulk layer

Mapper exports known fields. Bulk layer adds enrichment and review flags.

### Bulk layer -> EVA adapter

Only approved or non-blocking values should map to EVA payloads.

### Bulk layer -> Engineer pack

All facts must show source. All review prompts must remain editable/ignorable.

### Bulk layer -> Dashboards

Dashboards should use minimised, aggregated fields where possible.

## MVP

Implement `vehicle_identity`, `mot_summary`, `mileage_estimate`, and `review_flag` first. These support the highest-value early use cases.


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
