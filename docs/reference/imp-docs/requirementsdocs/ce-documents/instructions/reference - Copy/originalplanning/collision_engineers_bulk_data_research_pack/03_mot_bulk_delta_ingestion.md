# MOT Bulk and Delta Ingestion

## Concept

Build a DVSA MOT data ingestion service that downloads the weekly bulk file and then applies daily delta files to maintain a local searchable copy of MOT and vehicle-history records.

## Why this should be first

MOT data underpins several high-value ideas in the Phase 5 plan:

- missing mileage estimation;
- mileage anomaly review;
- condition and maintenance context;
- failure/advisory pattern analysis;
- vehicle age and usage profile;
- prior-history explanation in the engineer pack.

The current EVA User Guide already indicates that mileage may be estimated from MOT data when the dashboard mileage is not visible. Formalising this into a repeatable data service would reduce manual lookups and make the source of any estimated mileage auditable.

## Data source

Use the current DVSA MOT history API documentation, not the deprecated GitHub documentation. The new API provides a `/v1/trade/vehicles/bulk-download` endpoint that returns URLs for a weekly bulk file and daily delta files.

## Implementation pattern

### Weekly job

1. Authenticate to the DVSA MOT API using client credentials and API key.
2. Call the bulk-download endpoint.
3. Download the current Sunday bulk file.
4. Store it in raw object storage with checksum and source metadata.
5. Parse into staging tables.
6. Upsert into `mot_vehicle` and `mot_test` tables.
7. Run quality checks.
8. Publish a new `mot_data_snapshot_version`.

### Daily job

1. Call the bulk-download endpoint after 8am.
2. Identify any delta files not yet processed.
3. Download and checksum each delta.
4. Apply delta rows to staging.
5. Merge into the normalised MOT tables.
6. Record affected VRMs/vehicle IDs.
7. Recompute enrichments for open cases involving affected vehicles.

## Operational controls

- Track file names, URLs, checksums and processed timestamps.
- Make the job idempotent. Reprocessing the same delta must not duplicate tests.
- Store parse errors by source file and row/object path.
- Notify admin only on actual failure; do not create noisy success messages.
- Use exponential backoff for API or download errors.
- Respect API quota, burst and requests-per-second limits.

## Validation checks

- No duplicate MOT test numbers for the same vehicle.
- Odometer values must be non-negative.
- Test dates must be plausible.
- Defect/advisory arrays must parse.
- Unknown units/result types must be retained but flagged.
- Bulk and delta versions must be traceable.

## Outputs

The ingestion should not directly make case decisions. It should expose query services:

```text
GET /vehicle/{vrm}/mot-history
GET /vehicle/{vrm}/latest-mileage-before?date=YYYY-MM-DD
GET /vehicle/{vrm}/mileage-trend
GET /vehicle/{vrm}/condition-signals
```

## MVP

For the first build, support only:

- lookup by VRM;
- latest MOT mileage before incident date;
- latest MOT mileage after incident date, if any;
- annualised mileage estimate;
- advisories/failures summary.

Everything else can be layered later.


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
