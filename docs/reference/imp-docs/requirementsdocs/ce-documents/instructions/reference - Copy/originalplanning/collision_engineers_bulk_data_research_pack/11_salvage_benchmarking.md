# Salvage Benchmarking

## Concept

Build a controlled salvage-value benchmarking dataset from CE historical total-loss cases, EVA report outputs, and approved salvage auction or commercial data sources.

## Why it matters

The Phase 5 plan identifies salvage auction prices as a useful benchmark. This is particularly relevant for total-loss or near-total-loss assessments where salvage value and disposal value are part of the financial position.

The Sentry/EVA report documentation includes valuation fields such as market, retail, trade, mid-point, salvage and disposal values. If CE can retrieve released reports and store those fields historically, CE can build its own internal salvage intelligence even before purchasing external auction data.

## Data sources

### Internal

- EVA/Sentry released reports.
- CE historical total-loss cases.
- Engineer report values.
- Repair estimate totals.
- vehicle age/mileage/fuel/category.
- damage category and impact area.

### External/commercial

- salvage auction feeds where licensed;
- commercial vehicle history/valuation providers;
- insurer/client-provided salvage outcomes.

## Use cases

### 1. Salvage reasonableness review

Flag if proposed salvage value is materially outside historical CE ranges for similar vehicles and damage profiles.

### 2. Total-loss pack support

Provide engineer with comparable salvage values and source evidence.

### 3. Supplier/client trend analysis

Identify how salvage values differ by principal, vehicle segment, age, damage type and time period.

### 4. Settlement challenge support

Where a salvage deduction is challenged, provide a transparent evidence summary.

## Data model

```sql
salvage_evidence(
  case_id,
  normalised_vrm,
  vehicle_make,
  vehicle_model,
  first_registration_year,
  mileage,
  damage_zone,
  claim_type,
  market_value,
  salvage_value,
  salvage_percentage_of_market,
  source,
  source_date,
  evidence_reference
)
```

## Implementation approach

1. Mine existing EVA reports for salvage and valuation fields.
2. Store total-loss and repair/disposal outcomes.
3. Build simple comparable search by make/model/age/fuel/body/value band.
4. Only then consider external auction integrations.

## Controls

- External auction data is likely commercial; confirm licence before storing or reusing.
- Salvage comparables should be evidence, not automatic conclusion.
- Avoid overfitting from small samples.
- Record whether values are estimates, actual sale outcomes, or report opinions.

## MVP

Build an internal CE salvage comparables dashboard using historical reports only. This produces value without waiting for external feed procurement.


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
