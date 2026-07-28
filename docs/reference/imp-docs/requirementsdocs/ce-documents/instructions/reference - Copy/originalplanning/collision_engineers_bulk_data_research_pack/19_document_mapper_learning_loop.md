# CE Document Mapper Learning Loop

## Concept

Use outputs and corrections from CE Document Mapper as bulk training and QA data for extraction improvement.

## Internal basis

The CE Document Mapper is already a Windows desktop app that imports PDFs/DOC/DOCX/EML/MSG, extracts text/images, applies provider presets, exports JSON, supports batch mode, and has Engineer Report/Audit Mode behaviour. The conversation history shows it has been iteratively improved for PDF table extraction, OCR fallback, mileage parsing, provider/preset separation and JSON export controls.

That makes it a strong operational source of labelled extraction data.

## What to capture

For every import/export:

- source file hash;
- file type;
- detected provider preset;
- extracted fields;
- manual edits before export;
- export success/failure;
- source text evidence where available;
- provider mapping version;
- OCR used/not used;
- engineer report overrides;
- user corrections.

## Use cases

### 1. Provider preset quality dashboard

Show which providers/fields have the most manual corrections.

### 2. Extraction regression testing

Build a test corpus from real documents and expected JSON outputs. This protects future versions of the mapper.

### 3. Bulk field accuracy metrics

Track precision/recall by field:

- VRM;
- vehicle model;
- claimant name;
- reference;
- incident date;
- inspection address;
- mileage.

### 4. Template drift detection

If a provider changes document format, correction rate or missing-field rate will spike.

### 5. Training data for future extraction services

Use corrected exports as labelled examples for a future cloud extraction pipeline.

## Implementation

Add optional telemetry/export logs to a local or central store:

```json
{
  "mapper_version": "Vxx",
  "provider_preset": "FW (Garage)",
  "work_provider": "FW",
  "field": "Mileage",
  "extracted_value": "28487",
  "final_value": "28487",
  "changed_by_user": false,
  "source_method": "single_label",
  "ocr_used": false
}
```

## Privacy and governance

- Do not transmit full source files without approval.
- Start with anonymised field-level quality metrics.
- If centralising correction data, define purpose and retention.
- Redact personal fields where metrics do not require them.

## MVP

Create an `Export Audit JSON` sidecar file per export, stored locally or in the case folder. Later, centralise sidecar files into the bulk-data lake.

## Opportunity

This creates a feedback loop: staff corrections become the evidence for improving provider presets, parser rules and future AI extraction models.


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
