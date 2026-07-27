# Job Sheet and Operations Analytics

## Concept

Use the job sheet and status/control data as a bulk operational dataset for workload, turnaround, bottleneck and duplicate-registration analytics.

## Internal basis

The handover documents describe the job sheet as a key operational surface that must be opened in Excel Desktop because it uses VBA. It also contains conditional formatting for aged dates and repeated registrations. This implies the sheet already carries operational risk signals, but they are embedded in Excel rules rather than stored as reusable analytics.

## Use cases

### 1. Aged-work dashboard

Replace conditional formatting-only logic with a queryable SLA model:

- instruction age;
- awaiting images;
- awaiting estimate;
- ready for EVA;
- ready for engineer;
- report overdue.

### 2. Duplicate registration detection

Current conditional formatting highlights repeated regs. A central data store can make this more useful by distinguishing:

- genuine repeat case;
- duplicate entry;
- reopened/supplementary report;
- same vehicle across different principals.

### 3. Work-provider performance

Track:

- volume by work provider/principal;
- missing-information frequency;
- average time to images;
- average time to report;
- correction rate by provider template.

### 4. Staff workload planning

Track open cases by state, user, engineer and region.

### 5. Automation ROI measurement

Compare manual vs automated stages:

- extraction time saved;
- correction rate;
- cases auto-enriched;
- missing-info chasers drafted;
- EVA entry time reduced.

## Implementation

1. Export or mirror job sheet rows to a controlled database table.
2. Normalise VRM and references.
3. Track row state changes as events.
4. Reproduce Excel conditional-formatting rules as named rules.
5. Build dashboard views.

## Data model

```sql
work_item_status_event(
  case_id,
  old_status,
  new_status,
  changed_at,
  changed_by,
  source
)
```

## Controls

- Do not break the current Excel workflow until the replacement is proven.
- Preserve the sheet as a familiar surface initially.
- Avoid writing back to VBA workbook unless thoroughly tested.
- Treat the database as the eventual source of truth, not the macro sheet.

## MVP

Create a one-way export/mirror from the job sheet into a reporting table and reproduce the existing four conditional-formatting rules as dashboard metrics.


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
