# Mileage Estimation Engine

## Concept

Use MOT odometer history to estimate case mileage when the instruction or images do not provide a reliable mileage. This is an evidence-backed estimate, not a verified dashboard reading.

## Business fit

The EVA User Guide already says mileage should be copied from a dashboard image where present, and if not present can be estimated through Percayso using VRM and accident date. The Phase 5 plan proposes MOT mileage trend analysis. These should be combined into a formal engine that produces a conservative, source-backed mileage estimate and records how it was produced.

## Data inputs

- normalised VRM;
- incident date;
- inspection date;
- latest verified dashboard mileage if available;
- MOT odometer readings and dates;
- DVLA first registration date/month;
- make/model/fuel/year/vehicle class;
- optional historical CE case mileage for the same VRM.

## Estimation methods

### 1. Direct latest-before method

If the latest MOT before the incident date is recent, use it as baseline:

```text
estimated_mileage = latest_mot_mileage + expected_daily_mileage * days_since_latest_mot
```

### 2. Linear vehicle-specific trend

If there are multiple MOT readings:

```text
fit mileage = a + b * date
```

Use robust fitting to reduce the influence of suspect readings.

### 3. Cohort fallback

If the vehicle’s own MOT history is sparse, use cohort priors:

- make/model;
- fuel type;
- age band;
- vehicle class;
- previous annual mileage distribution.

### 4. Conservative banding

Always output a range, not only a single number:

```json
{
  "estimate": 53600,
  "low": 49750,
  "high": 57400,
  "basis": "mot_trend_plus_cohort_prior",
  "confidence": "medium"
}
```

## Outputs for staff

Use clear labels:

- `Verified from dashboard image`;
- `Extracted from instruction`;
- `Estimated from MOT history`;
- `Estimated from cohort mileage model`;
- `Requires review`.

## Engineer pack wording

Do not bury estimation. Example:

> Mileage is not verified from a dashboard image. The displayed mileage has been estimated from MOT history using readings dated [dates]. Review required if mileage materially affects valuation.

## Integration with CE Document Mapper

CE Document Mapper already treats mileage as a strict numeric field and supports mileage unit handling. It can export a blank mileage when source documents do not contain it. The enrichment layer can then fill `mileage_estimate` separately from `mileage_reported`.

Recommended fields:

```json
"mileage": {
  "reported_value": null,
  "reported_source": null,
  "estimated_value": 53600,
  "estimate_source": "dvsa_mot_history",
  "confidence": "medium",
  "requires_review": true
}
```

## Controls

- Never overwrite a verified dashboard mileage with an estimate.
- Treat unreadable/no-odometer MOT records as missing.
- Flag decreases or implausible jumps.
- Keep evidence dates and readings visible.
- Require human approval before using an estimated mileage in valuation.

## MVP

Build the first version as a deterministic rules engine, not a machine-learning model:

1. fetch MOT readings;
2. filter invalid/unreadable;
3. identify closest reading before incident date;
4. calculate annualised trend;
5. output estimate and confidence band;
6. add a review flag if data is sparse or inconsistent.

This is likely to deliver value before more complex modelling is justified.


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
- National Archives Open Government Licence `v3.0`: https://www.nationalarchives.gov.uk/doc/open-government-licence/version/3/
- Auto Trader Connect valuations overview: https://help.autotrader.co.uk/hc/en-gb/articles/21923133513117-Introduction-to-Current-Valuations
- Auto Trader Historic Valuations overview: https://help.autotrader.co.uk/hc/en-gb/articles/21945683119389-Introduction-to-Historic-Valuations
- Glass's / Autovista API overview: https://glass.co.uk/product/autovista-api/
