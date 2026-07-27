# EVA Report Mining and Sentry Bulk Sync

## Concept

Use the Sentry/EVA API to retrieve released reports and turn them into CE’s internal historical intelligence dataset.

## Why this is high value

External datasets are useful, but CE’s own historic reports are likely the best source for:

- actual case types;
- engineer values;
- repair/salvage values;
- damage zones;
- report types;
- valuation adjustments;
- client/principal patterns;
- cycle time and report release dates.

The Sentry API guide says report retrieval follows a two-step pattern: retrieve the available reports list, then retrieve a specific report by ID. The guide also states that the API does not provide native batch endpoints and that batch processing must be implemented at the client/integration layer.

## Use cases

### 1. Build CE historical case lake

Download all released reports and normalise:

- registration;
- claim number;
- incident date;
- inspection date;
- report type;
- claim type;
- valuation fields;
- repair/salvage/financial fields;
- damage areas;
- parts list;
- report text where lawful and useful.

### 2. Same-VRM prior case search

Search historical EVA reports for same registration.

### 3. Valuation and salvage benchmarks

Use historical valuation/salvage fields as internal comparables.

### 4. Report quality analytics

Track missing fields, common corrections, supplementary report patterns and turnaround.

### 5. Model training/evaluation dataset

Use released reports as labelled outputs for extraction, classification and pack-generation evaluation. Keep engineer conclusions human-owned.

## Implementation

### Safe sync worker

```text
GET /Report/GetAvailableReports
for each id not already downloaded:
    GET /Report/GetReport?id={id}
    store raw response
    normalise fields
    emit audit event
```

### Sync metadata

- API response ID;
- released date;
- retrieval time;
- checksum of raw JSON;
- schema version;
- normalisation version;
- processing status.

### Batch controls

- refresh token as required;
- sequential mode first;
- small delay between requests;
- concurrency only after testing;
- retry 5xx with backoff;
- log every response.

## Data model

```sql
eva_report_raw(
  report_id,
  registration,
  released_date,
  raw_json,
  checksum,
  retrieved_at
)

eva_report_summary(
  report_id,
  normalised_vrm,
  claim_no,
  incident_date,
  inspection_date,
  report_type,
  claim_type,
  val_market,
  val_retail,
  val_trade,
  val_salvage,
  fee_net,
  fee_vat,
  fee_gross
)
```

## Controls

- Use a dedicated integration identity.
- Store secrets in a vault.
- Keep raw report data access controlled.
- Do not auto-generate final engineering conclusions from historic reports.
- Be careful using report text for model training; confirm privacy and contractual terms.

## MVP

Start by syncing only report metadata and valuation/salvage fields. Add report text, damage areas and parts lists after governance is confirmed.


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
