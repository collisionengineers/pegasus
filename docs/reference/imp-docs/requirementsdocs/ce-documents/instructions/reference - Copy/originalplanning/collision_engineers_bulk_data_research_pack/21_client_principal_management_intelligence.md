# Client and Principal Management Intelligence

## Concept

Use bulk case data to understand performance, quality and workload patterns by work provider, principal, document format and report type.

## Why it matters

The CE Document Mapper discussion highlighted the need to separate preset names from strict Work Provider output values because some providers have multiple document formats. That distinction is important for analytics: the system should separately track `work_provider`, `provider_preset`, `document_format`, `principal_code` and `case_type`.

## Use cases

### 1. Template/document quality

Which provider presets have the most missing or corrected fields?

### 2. Provider missing-information rate

Which work providers most often omit:

- mileage;
- inspection address;
- incident date;
- images;
- estimate;
- VAT status;
- claim reference.

### 3. Turnaround by principal

Track time from instruction to:

- file captured;
- data extracted;
- images matched;
- EVA created;
- engineer pack generated;
- report released.

### 4. Revenue/fee intelligence

Where billing data is available from invoices or EVA report fields, analyse work type and profitability by client/principal.

### 5. Escalation/chaser templates

Use missing-information history to draft better chasers and improve onboarding with clients.

## Data fields

```json
{
  "work_provider": "SBL",
  "provider_preset": "SBL - format 2",
  "principal_code": "QCL",
  "document_type": "instruction_pdf",
  "case_type": "desktop_inspection",
  "missing_fields": ["mileage", "inspection_address"],
  "manual_corrections": ["vehicle_model"]
}
```

## Dashboard ideas

- Top 10 providers by volume.
- Highest correction-rate templates.
- Average missing fields per provider.
- Time-to-ready-for-engineer by principal.
- Repeat VRM rate by provider.
- Report type mix.

## Controls

- Use management intelligence internally.
- Avoid overinterpreting small sample sizes.
- Distinguish process issues from client fault.
- Keep claimant/client personal data out of management dashboards unless necessary.

## MVP

Start with provider/preset-level extraction quality:

- count imports;
- count JSON exports;
- count missing critical fields;
- count manual corrections;
- trend by week.

This directly improves the mapper and the automation pipeline.


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
