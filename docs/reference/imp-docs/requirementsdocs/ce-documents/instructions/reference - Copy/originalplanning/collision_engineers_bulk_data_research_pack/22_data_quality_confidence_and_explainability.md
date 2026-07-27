# Data Quality, Confidence and Explainability

## Concept

Every bulk-data enrichment should carry quality, confidence and evidence metadata. This prevents the system from becoming an opaque black box.

## Why this matters

The wider CE project plan requires human review, audit logging, traceability and source references. Bulk data can easily create false confidence if a flag or estimate appears without its source and limitations.

## Field-level confidence object

```json
{
  "field": "mileage_estimate",
  "value": 53600,
  "confidence": "medium",
  "method": "mot_linear_projection",
  "evidence": [
    {"source": "MOT", "date": "2025-02-01", "mileage": 49200},
    {"source": "MOT", "date": "2024-02-03", "mileage": 42100}
  ],
  "limitations": ["No dashboard image available", "MOT data is not verified odometer proof"],
  "review_required": true
}
```

## Confidence dimensions

- source reliability;
- data recency;
- number of supporting sources;
- source agreement/conflict;
- extraction confidence;
- format validation;
- business-criticality;
- human review state.

## Review states

- `not_reviewed`;
- `accepted`;
- `edited`;
- `rejected`;
- `not_relevant`;
- `requires_engineer`.

## Explainability requirements

Every flag should answer:

1. What was detected?
2. Why does it matter?
3. What evidence supports it?
4. What is the suggested action?
5. What should the user not conclude from it?

## Example: safe flag wording

Poor:

> Fraud suspected.

Better:

> Mileage variance requires review. Reported mileage is lower than the latest MOT mileage before the incident date. Check mileage unit, source document and dashboard image before valuation.

## Implementation

- Store `evidence_json` with every enrichment.
- Render evidence in the engineer pack.
- Require reviewer action for red/blocking flags.
- Log reviewer changes.
- Feed reviewer outcomes into model/rule QA.

## Metrics

Track:

- flag volume;
- accepted vs rejected flags;
- false positive rate by rule;
- manual correction rate;
- time saved;
- report issue/challenge rate.

## MVP

Create a single `review_flags` table used by every bulk-data feature.

```sql
review_flag(
  flag_id,
  case_id,
  flag_type,
  severity,
  summary,
  evidence_json,
  status,
  created_at,
  reviewed_by,
  reviewed_at
)
```


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
