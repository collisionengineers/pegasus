# Mileage Anomaly Review

## Concept

Use MOT history, instruction-extracted mileage, dashboard-image mileage and valuation-input mileage to flag inconsistencies for review.

## Purpose

This is not an automatic fraud detector. It is a review prompt that helps staff and engineers notice potential odometer, extraction, unit, or input problems before the valuation/report is finalised.

## Why this matters

Mileage strongly affects valuation. The CE Document Mapper has recently had special mileage parsing and mileage-unit rules added, which implies mileage is operationally important and easy to misread. MOT data provides an independent chronology that can catch errors such as:

- km/miles confusion;
- comma/format extraction issues;
- dashboard image OCR mistakes;
- stale instruction mileage;
- odometer rollback signals;
- accidentally using a previous case’s mileage;
- improbable mileage for vehicle age or use.

## Detection rules

### 1. MOT decrease rule

Flag if an MOT odometer reading decreases materially compared with a prior reading.

```text
if current_mileage + tolerance < previous_mileage:
    flag = "mot_mileage_decrease"
```

### 2. Case vs MOT projection rule

Project expected mileage at incident date. Flag if the reported mileage is outside the expected band.

```text
if reported_mileage < low_band or reported_mileage > high_band:
    flag = "reported_mileage_outside_mot_band"
```

### 3. Unit-conversion suspicion

If reported mileage is close to MOT estimate multiplied or divided by 1.609, flag as possible miles/km confusion.

### 4. Sudden mileage jump

Flag if annualised mileage between two MOTs is extreme compared with the cohort and vehicle history.

### 5. Date-order issue

Flag if a post-incident MOT reading is lower than a case mileage that is supposedly earlier.

## Review output

Do not say `clocked` or `fraud`. Use neutral language:

```json
{
  "flag_type": "mileage_variance_requires_review",
  "severity": "medium",
  "evidence": [
    {"date": "2025-02-01", "source": "MOT", "mileage": 49200},
    {"date": "2026-04-14", "source": "instruction", "mileage": 28487}
  ],
  "suggested_action": "Check mileage unit and dashboard image before valuation."
}
```

## UI opportunity

In the engineer/admin pack, show:

- a small timeline of MOT readings;
- the case-reported mileage;
- confidence band;
- review reason;
- source links/evidence.

## Implementation

### Data required

- case mileage and unit;
- MOT readings and units;
- incident date;
- instruction date;
- inspection date;
- vehicle age and class;
- valuation source mileage.

### Batch process

Run the anomaly rules whenever:

- a new case is imported;
- a mileage field is manually changed;
- the MOT delta updates a relevant VRM;
- a valuation lookup is saved.

## Human workflow

The system should produce one of:

- `No mileage issue detected`;
- `Mileage estimated, review recommended`;
- `Mileage variance, review required`;
- `Unable to evaluate mileage`.

## MVP

Start with deterministic flags:

1. decreasing MOT readings;
2. reported mileage lower than last MOT mileage before incident;
3. possible miles/km conversion;
4. sparse MOT history.

Machine learning can come later once CE has stored enough review outcomes to evaluate false positives.


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
