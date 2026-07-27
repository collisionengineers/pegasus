# Market Valuation Evidence Store

## Concept

Create an evidence store for valuation lookups from approved sources such as Glass's/Autovista, Auto Trader, Cazana/Percayso or other licensed valuation providers.

## Why this matters

The EVA User Guide says valuation currently uses VRM, mileage and incident date in Glass's and Percayso, then carries selected retail/trade values into the EVA valuation tab. The project planning notes also treat valuation as evidence-gathering, not an automatic final valuation.

A market valuation evidence store allows CE to keep a defensible trail of what was checked, when, against which mileage and vehicle assumptions.

## Data captured

For every valuation lookup:

```json
{
  "case_id": "WI-2026-000123",
  "normalised_vrm": "AB12CDE",
  "valuation_source": "glass_autovista",
  "lookup_time": "2026-05-22T12:00:00Z",
  "incident_date_used": "2026-04-14",
  "mileage_used": 53600,
  "vehicle_description_used": "Skoda Superb",
  "retail_value": 8500,
  "trade_value": 7200,
  "market_value": null,
  "raw_response_id": "...",
  "licence_notes": "commercial_source_do_not_republish"
}
```

## Use cases

### 1. Evidence-backed valuation summary

Generate a draft valuation evidence section that lists each source, date, mileage and value. Engineer confirms final wording.

### 2. Variance review

Flag if valuation sources differ materially.

### 3. Mileage sensitivity

Show how the valuation changes if mileage is verified, estimated or disputed.

### 4. Historic trend support

Auto Trader Historic Valuations and trended valuation capabilities may support historic market position where licensed.

### 5. Audit defence

If a valuation is challenged, CE can show which source was used, the input mileage/date, and the value retrieved.

## Implementation

- Do not scrape websites where APIs/licensing are not approved.
- Store raw API responses in controlled storage.
- Store parsed values in queryable tables.
- Keep source attribution and licence restrictions.
- Attach valuation evidence to case record and engineer pack.

## Controls

- Engineer owns final valuation.
- The system should not auto-select the highest value as final without human confirmation.
- Valuation source names and values may be commercially sensitive; protect access.
- Keep versioned extraction and valuation rules.

## MVP

Start with manual import of valuation results into a structured table if API access is not yet available. That still creates a searchable evidence base and prepares the system for later API integration.


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
