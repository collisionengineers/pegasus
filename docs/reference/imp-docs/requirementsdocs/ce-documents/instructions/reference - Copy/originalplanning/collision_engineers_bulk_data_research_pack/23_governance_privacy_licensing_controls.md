# Governance, Privacy and Licensing Controls

## Concept

Define mandatory controls for bulk data before the data lake grows. This is not optional; bulk data increases both usefulness and risk.

## Main risks

- storing more personal data than necessary;
- blending public, commercial and internal data without licence controls;
- treating estimates/flags as final decisions;
- using outdated or deprecated APIs;
- retaining source data indefinitely;
- exposing claimant/client data in dashboards;
- using AI or automation without audit trail.

## Privacy controls

### Data minimisation

Only extract/store the data needed for the process. If a dashboard only needs provider, date, status and vehicle segment, it does not need claimant name or full address.

### Pseudonymisation

For analytics, use case IDs and hashed identifiers where possible. Keep personal fields in controlled case tables, not broad reporting tables.

### Anonymisation caution

ICO guidance makes clear that anonymised data is outside UK GDPR only if individuals are no longer identifiable. CE should not casually label pseudonymised datasets as anonymised.

### Access control

Separate permissions for:

- raw source documents;
- case data;
- bulk reference data;
- management dashboards;
- engineer pack evidence;
- model training/evaluation datasets.

## Licensing controls

### Public OGL data

Open Government Licence sources still require attribution and compliance with terms.

### Commercial sources

Glass's/Autovista, Auto Trader, Cazana/Percayso, HPI, salvage feeds and similar providers may restrict storage, redistribution, automated access, publication and model training. Store licence notes per source.

### Source registry

Create:

```sql
data_source_registry(
  source_name,
  owner,
  licence_type,
  permitted_use,
  prohibited_use,
  attribution_text,
  retention_limit,
  commercial_terms_reference,
  last_reviewed_at
)
```

## Automation controls

- No automatic final engineering conclusions.
- No automatic fraud decisions.
- No auto-send external communications unless separately approved.
- No uncontrolled agent access to external tools.
- Every enrichment and export must be logged.

## Retention controls

Define retention periods for:

- raw public data;
- raw commercial API responses;
- source files;
- extracted JSON;
- audit logs;
- model training sets;
- dashboards.

## Model governance

If machine-learning models are introduced:

- version the model and training data;
- record validation metrics;
- monitor drift;
- audit bias/false positive rates;
- require human review for adverse or material flags.

## MVP governance checklist

Before production ingestion:

1. confirm legal basis/purpose;
2. approve data source registry;
3. define access roles;
4. define retention;
5. create audit events;
6. use source attribution;
7. document human-review boundaries;
8. prevent deprecated API usage.


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
