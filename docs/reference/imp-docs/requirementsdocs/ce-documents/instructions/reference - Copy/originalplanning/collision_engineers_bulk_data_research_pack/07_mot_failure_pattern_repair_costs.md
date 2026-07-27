# MOT Failure Patterns and Repair Cost Intelligence

## Concept

Combine MOT failure/advisory patterns with CE’s historical claim and repair estimate data to identify vehicle-specific repair-cost and damage-review prompts.

## Why it matters

The Phase 5 plan suggests analysing failure patterns and correlating them with repair costs. This becomes more valuable when joined with CE’s own historical case outcomes, Audatex/Glass’s estimates, and engineer reports.

## Use cases

### 1. Pre-existing condition prompts

If the claim includes damage to tyres, suspension, brakes, steering or structure, and the MOT history shows repeated advisories in the same area, the engineer pack can prompt review.

### 2. Repair-cost benchmarking

For common models, build benchmarks:

- average estimate value by damage zone;
- frequency of ADAS/alignment/SRS flags;
- parts/labour split;
- total loss likelihood by vehicle age/value/repair estimate.

### 3. Estimate-review rules

If a vehicle model often requires calibration, alignment or safety-system checks after a given impact zone, flag estimates that omit those lines.

### 4. Model-specific weak points

Recurring MOT failures by make/model can inform engineer questions, not conclusions. Example: repeated suspension advisories in a model cohort may prompt closer review of suspension photographs.

## Data sources

- DVSA MOT defects/advisories.
- CE historical reports.
- Audatex/Glass’s estimate outputs.
- EVA report retrieval data where available.
- Image-review damage zones.

## Implementation

### Normalise defects

MOT text needs category mapping:

```text
defect_text -> category -> component -> side/position -> severity
```

Example categories:

- brakes;
- tyres;
- steering;
- suspension;
- lighting;
- exhaust/emissions;
- structure/corrosion;
- glass/wipers;
- registration/VIN.

### Link to estimate lines

Estimate parsers should categorise lines similarly:

```text
estimate_line -> operation_category -> component -> labour/parts/paint/other
```

### Build comparative summaries

For each make/model/year band:

- most common MOT defect categories;
- common claim repair categories;
- average estimate and total loss rate;
- review prompts with observed evidence.

## Outputs

Avoid prescriptive statements. Use prompts:

> This vehicle/model has repeated MOT suspension advisories and the current estimate includes suspension-related lines. Review whether any pre-existing condition is relevant.

## MVP

Start with only CE historical cases and MOT categories for the same VRM. Then expand to make/model cohorts once enough data is stored.

## Risk controls

- No automatic repair-cost conclusion.
- No automatic fraud/pre-existing damage allegation.
- Always show source readings and estimate lines.
- Require engineer sign-off before report wording uses the insight.


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
