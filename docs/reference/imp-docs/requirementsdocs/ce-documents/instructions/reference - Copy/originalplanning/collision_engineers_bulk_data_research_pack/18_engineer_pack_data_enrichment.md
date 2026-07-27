# Engineer Pack Data Enrichment

## Concept

Use the bulk-data layer to enrich engineer packs with concise, source-backed context and review prompts.

## Why it matters

The wider project plan already identifies engineer pack generation as a key automation. Bulk data becomes most useful when it is presented in the engineer’s workflow at the moment of review, not hidden in dashboards.

## Enrichment sections

### 1. Vehicle identity validation

- extracted VRM;
- DVLA make/colour/fuel/year;
- MOT make/model where available;
- conflicts.

### 2. Mileage context

- source mileage;
- dashboard/verified vs estimated;
- MOT reading timeline;
- estimated mileage band;
- anomaly flags.

### 3. MOT condition context

- recent pass/fail history;
- recurring advisories;
- safety-critical advisories;
- maintenance score.

### 4. Valuation evidence

- Glass's/Percayso/Autovista/Auto Trader outputs where licensed;
- mileage/date used;
- source variance;
- engineer approval status.

### 5. Prior case context

- same VRM historical CE cases;
- previous reports;
- prior total-loss indicators;
- similar damage zones.

### 6. Evidence completeness

- instruction present;
- images present;
- odometer image present;
- estimate present;
- valuation lookup present;
- missing items.

## UI approach

Use a traffic-light review model but avoid over-simplifying:

- Green: no obvious data conflict.
- Amber: review recommended.
- Red: blocking or conflicting data.
- Grey: insufficient data.

Each flag must expand to show source evidence.

## Example engineer pack block

```text
Mileage context: Review recommended
- Instruction mileage: not present
- Latest MOT before incident: 49,200 miles on 2025-02-01
- Estimated mileage at incident: 53,600 miles (medium confidence)
- Dashboard image: not found
Suggested action: confirm mileage before final valuation.
```

## Implementation

- Create a `case_enrichment` object before pack generation.
- Attach `evidence` arrays for every statement.
- Keep raw source links.
- Record whether the engineer accepted, edited or ignored each flag.

## Controls

- No automatic final opinion.
- No hidden data used in report wording.
- Engineer/admin can mark flags as irrelevant.
- The generated pack should clearly distinguish facts from suggestions.

## MVP

Add only three enrichment blocks first:

1. vehicle identity validation;
2. mileage/MOT context;
3. missing-evidence checklist.

These are likely to deliver immediate value and are easier to verify.


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
