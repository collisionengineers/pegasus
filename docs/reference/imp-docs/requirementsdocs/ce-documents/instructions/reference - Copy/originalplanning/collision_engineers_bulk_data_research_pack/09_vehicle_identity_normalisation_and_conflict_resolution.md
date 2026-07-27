# Vehicle Identity Normalisation and Conflict Resolution

## Concept

Create a vehicle identity resolver that combines VRM extraction, DVLA data, MOT data, image OCR, instruction text and EVA records into one reviewable vehicle identity profile.

## Why this matters

Many downstream bulk-data features fail if the vehicle identity is wrong. A single OCR error in a registration can pull the wrong MOT history, wrong valuation, wrong prior case and wrong EVA report.

## Inputs

- CE Document Mapper extracted VRM.
- VRM from image OCR/plate detection.
- VRM from email subject/body/attachments.
- DVLA VES response.
- DVSA MOT history response.
- EVA/Sentry report data.
- Work provider reference.
- VIN, if available and lawful to use.

## Canonical identity object

```json
{
  "normalised_vrm": "AB12CDE",
  "display_vrm": "AB12 CDE",
  "confidence": "high",
  "sources": [
    {"source": "instruction_pdf", "value": "AB12 CDE"},
    {"source": "image_plate_ocr", "value": "AB12CDE"},
    {"source": "dvla", "value": "AB12CDE"}
  ],
  "conflicts": []
}
```

## Conflict examples

- Instruction VRM differs from image plate.
- DVLA make differs from instruction make.
- MOT history make/model looks inconsistent with instruction vehicle model.
- EVA report for same claim reference has different registration.
- VRM in file name differs from document body.

## Resolution workflow

1. Gather all candidate identifiers.
2. Normalise all candidates.
3. Score each source by reliability.
4. Group candidates by exact normalised VRM and fuzzy similarity.
5. Use deterministic rules before AI.
6. If conflict remains, require human review.

## Suggested source ranking

High confidence:

- validated DVLA response for the same normalised VRM;
- clear instruction label;
- manually confirmed user entry.

Medium confidence:

- email subject/body;
- file name;
- plate OCR from a clear image.

Lower confidence:

- OCR from scanned PDF;
- partial/fuzzy registration;
- ambiguous text near a registration-like string.

## Outputs

The resolver should produce:

- accepted registration;
- rejected candidates;
- conflict flags;
- evidence snippets;
- confidence.

## Use in bulk data

All enrichment services should require a resolved vehicle identity object. If identity confidence is low, the bulk data layer should not pull and apply MOT/valuation/prior claim results automatically.

## MVP

Implement simple conflict detection first:

- compare CE Document Mapper VRM with DVLA found/not found;
- compare instruction make with DVLA make;
- compare image plate OCR if available;
- flag mismatch for review.

This protects every other Phase 5 feature.


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
