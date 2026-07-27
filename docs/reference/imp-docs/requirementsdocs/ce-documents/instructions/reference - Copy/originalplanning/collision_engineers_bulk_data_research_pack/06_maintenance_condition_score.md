# Maintenance and Condition Score from MOT History

## Concept

Summarise a vehicle’s MOT history into a review-friendly maintenance/condition indicator for use in engineer packs and valuation support.

## Purpose

MOT history can reveal recurring advisories, repeated failures, long gaps, corrosion warnings, tyre/brake issues, and signs of poor maintenance. This can support valuation/condition context, but should not become an automatic deduction without engineer review.

## Candidate signals

- Number of MOT failures in last 3–5 tests.
- Defect severity distribution.
- Recurring advisories across multiple years.
- Safety-critical categories: brakes, tyres, suspension, steering, lights, structural corrosion.
- Time between failure and pass.
- Mileage between tests.
- MOT expiry gaps or late tests.
- Advisory-to-failure progression.

## Output model

```json
{
  "maintenance_score": 62,
  "score_band": "review",
  "signals": [
    "Repeated tyre advisories",
    "Suspension advisory repeated over 2 MOTs",
    "Recent MOT failure corrected within 7 days"
  ],
  "evidence": [
    {"test_date": "2025-03-01", "item": "Nearside rear tyre worn close to legal limit"}
  ]
}
```

## Suggested scoring bands

- `green`: no obvious issue in MOT history.
- `amber`: repeated advisories or moderate failure pattern.
- `red`: recurring safety-critical advisories/failures or unresolved severe pattern.
- `insufficient_data`: too little MOT data.

## Use cases

### Engineer pack context

Add a concise section:

> MOT history review: repeated advisories noted for tyres and suspension. This may be relevant to pre-accident condition and valuation. Engineer review required.

### Valuation support

If a vehicle is otherwise being valued as clean retail, recurring MOT condition issues can prompt a condition adjustment review. This should be evidence-backed, not automatic.

### Prior maintenance prompts

Where accident damage relates to components with repeated MOT issues, flag for review. Example: suspension damage claim on a car with repeated suspension advisories.

## Implementation

1. Map DVSA defect/advisory text into standard categories.
2. Count recurrence by category and side/position where extractable.
3. Weight recent tests more heavily.
4. Surface evidence snippets rather than just a score.
5. Allow staff to mark flags as `useful`, `not relevant`, or `false positive`.

## Data caveats

- MOT inspection is not a full mechanical assessment.
- MOT advisories are tester-recorded and may vary in wording.
- Accident damage may supersede prior condition.
- Absence of advisories does not prove excellent condition.

## MVP

Create a simple rules-based indicator:

- repeated advisory category over 2+ tests;
- any structural corrosion advisory/failure;
- 2+ failures in the last 3 MOTs;
- unresolved advisory appearing again at next test.

This is sufficiently explainable for early operational use.


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
