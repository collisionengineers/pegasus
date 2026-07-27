# Research Sources and Constraints

## Concept

This note sets the factual baseline for the Phase 5 plan so that implementation decisions do not depend on stale assumptions.

## Key public-source findings

### MOT history is now available through the new DVSA MOT history API

The current DVSA documentation states that the new MOT history API combines vehicle types into a single API and includes data for cars, motorcycles and vans in Great Britain since 2005 and Northern Ireland since 2017. It also includes HGVs, trailers, buses and coaches in Great Britain since 2018 and Northern Ireland since 2017.

This matters because the Phase 5 plan correctly refers to MOT history from 2005 onwards, but implementation should use the new DVSA documentation rather than the older GitHub-hosted documentation. The older documentation explicitly says it is deprecated as of 1 September 2025.

### DVSA supports a bulk-and-delta pattern

The new DVSA bulk-download endpoint provides a bulk file and daily delta files. The documentation says the bulk file is generated every Sunday and daily delta files contain data for the past 24 hours. This is operationally useful: CE can build a local MOT lake once, then maintain it through deltas without repeatedly downloading the full dataset.

### MOT API authentication and rate limits need proper engineering

The new MOT API uses OAuth 2.0 client credentials flow and requires both bearer token and API key headers. The rate-limit page lists daily quota, burst, and RPS limits. This supports a formal ingestion worker rather than ad-hoc scripts.

### DVLA Vehicle Enquiry Service is live lookup, not a full bulk vehicle catalogue

The DVLA VES API is a RESTful JSON service that takes a vehicle registration number in the request body and returns vehicle details such as make, colour, fuel type, engine capacity, CO2 emissions, MOT status, tax status, year of manufacture, euro status, and related attributes. It is best treated as a cacheable lookup service keyed by normalised VRM.

### Road safety open data is useful but not a direct insurance-claim model

DfT road safety open data provides record-level files for personal-injury road collisions in Great Britain from 1979, including collisions, vehicles and casualties. It is useful for contextual and statistical analysis, but it only covers personal injury collisions reported to police and then recorded through STATS19. It should not be used as if it covers all non-injury motor claims.

### Weather data needs source-specific caution

Met Office Weather DataHub provides API access to weather data. Its Land Observations API is useful for recent hourly observations, but the public FAQ indicates recent observation responses cover a limited historical range. Historic accident-date weather enrichment therefore needs either a continuously maintained local capture, a separate historical archive, or a commercial/historical data arrangement.

### Open public data still requires licence management

Many UK public datasets are under the Open Government Licence, but this still requires attribution and does not override data protection requirements. Any CE-derived dataset must also respect client confidentiality and GDPR constraints.

## Internal-context findings

The current CE workflow and previous planning material repeatedly emphasise that:

- Data extraction, evidence matching, valuation lookups and engineer packs are useful, but final engineering conclusions remain human-owned.
- Current systems include Outlook, Box, EVA, Excel/job sheet, CE Document Mapper, Audatex/Glass's, Autovista/Glass's, Cazana, Percayso, and WhatsApp Desktop.
- EVA setup currently depends on mileage, VRM, incident date, valuation sources and notes about previous total loss / VRM notes.
- Sentry/EVA API documentation supports report retrieval and report submission, but batch behaviour must be implemented at the client/integration layer.
- CE Document Mapper already has a strong role as an extraction-to-JSON bridge and can become an input producer for the bulk-data layer.

## Implementation constraint

Do not build bulk-data features as isolated scripts. Build them as reusable data products:

- `vehicle_profile`
- `mot_history_snapshot`
- `case_enrichment`
- `valuation_evidence`
- `review_flags`
- `audit_events`

This keeps the future automation centre consistent with the existing canonical JSON direction.

## Immediate practical implication

The first research-backed implementation should be a local MOT data ingestion and update pipeline using DVSA bulk plus daily deltas. That enables mileage estimation, anomaly review, prior maintenance context, and vehicle-history enrichment without waiting for a complete platform replacement.


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
