# Use Case Backlog and Prioritisation

## Concept

Prioritise the Phase 5 bulk-data ideas by operational value, implementation complexity, data availability and governance risk.

## Priority matrix

| Priority | Use case | Value | Complexity | Risk | Recommendation |
|---|---|---:|---:|---:|---|
| 1 | MOT bulk/delta ingestion | High | Medium | Medium | Build first |
| 2 | Mileage estimate engine | High | Medium | Medium | Build after MOT ingestion |
| 3 | Mileage anomaly review | High | Low/Medium | Medium | Deterministic MVP |
| 4 | DVLA VRM cache | High | Low/Medium | Low/Medium | Build early |
| 5 | Vehicle identity resolver | High | Medium | Medium | Build early |
| 6 | Engineer pack enrichment | High | Medium | Medium | Build after flags |
| 7 | CE Document Mapper learning loop | High | Low/Medium | Medium | Build as sidecar logs |
| 8 | EVA/Sentry report mining | High | Medium | Medium/High | Build once API access is stable |
| 9 | Valuation evidence store | High | Medium | High/licensing | Start manual, then API |
| 10 | Prior claim/total-loss review | Medium/High | Medium | High | Internal data first |
| 11 | Duplicate evidence detection | Medium/High | Medium/High | High | Exact hashes first |
| 12 | MOT condition score | Medium | Medium | Medium | Rules-only MVP |
| 13 | Salvage benchmarking | Medium | Medium/High | High/licensing | Internal reports first |
| 14 | Job sheet operations analytics | Medium/High | Medium | Medium | Mirror first, no writeback |
| 15 | Provider/principal dashboards | Medium | Low/Medium | Low/Medium | Build from extraction logs |
| 16 | Road-safety open-data context | Medium | Medium | Medium | Taxonomy use, not case proof |
| 17 | Weather enrichment | Low/Medium | Medium | Medium | Only after source confirmed |
| 18 | Traffic/road network context | Low/Medium | Medium | Medium | Useful for scheduling/analytics |
| 19 | ML repair-cost predictions | Future | High | High | Defer until data mature |
| 20 | Automated fraud scoring | Not recommended | High | Very high | Do not build as decision tool |

## Build first

The best first group is:

1. MOT ingestion.
2. DVLA cache.
3. vehicle identity validation.
4. mileage estimate/anomaly flags.
5. review flag schema.
6. mapper correction logging.

This creates immediate operational value and a foundation for later data products.

## Build later

After CE has a central case database and report sync:

- salvage benchmarking;
- repair-cost intelligence;
- prior case review packs;
- provider/principal dashboards;
- valuation source variance;
- duplicate image detection.

## Defer or avoid

Avoid anything that sounds like an automatic adverse decision:

- automated fraud labels;
- automatic valuation finalisation;
- automatic repair-cost rejection;
- automated report conclusions;
- external auto-communications based on flags.

## Suggested MVP backlog tickets

1. Create source registry table.
2. Register DVSA MOT API credentials and test authentication.
3. Download and store first MOT bulk file.
4. Parse MOT records for one VRM.
5. Build DVLA cache for one VRM.
6. Build `vehicle_identity` object.
7. Build simple mileage estimate from MOT readings.
8. Build two mileage review flags.
9. Render flags into a prototype engineer pack.
10. Capture staff accept/reject outcome.

## Definition of done for early features

- Every output has source evidence.
- Every flag can be dismissed or accepted.
- Every rule has tests.
- Every source has licence notes.
- No critical field is silently overwritten.
- Engineer/admin approval is preserved.


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
