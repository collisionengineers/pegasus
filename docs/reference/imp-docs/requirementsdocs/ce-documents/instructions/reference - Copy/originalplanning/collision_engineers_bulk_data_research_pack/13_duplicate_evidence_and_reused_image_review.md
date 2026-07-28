# Duplicate Evidence and Reused Image Review

## Concept

Use bulk historical case data and image/document fingerprints to detect duplicate submissions, reused images, repeated reports, and case-matching errors.

## Why this belongs in Phase 5

The wider project context already identifies image/evidence matching as a high-value automation, and warns against automatic fraud decisions. Bulk historical data makes this more powerful: the system can search across prior cases instead of only matching the current instruction to the current image set.

## Data inputs

- file checksums;
- image perceptual hashes;
- EXIF metadata where available;
- OCR-visible VRM from images;
- extracted claim/vehicle fields;
- email sender/thread metadata;
- Box folder metadata;
- EVA report IDs;
- CE Document Mapper exported JSON.

## Use cases

### 1. Duplicate document detection

Same checksum or near-identical content across cases.

### 2. Duplicate image detection

Same or near-identical image in more than one case.

### 3. Mismatched image-to-case detection

Image plate OCR or filename VRM differs from instruction VRM.

### 4. Reused estimate/report detection

Estimate PDF or report text appears in more than one case with altered references.

### 5. Holding-pen matching

Link separate image emails to instructions using VRM, claim reference, sender, thread, timestamp and visual plate OCR.

## Implementation

### File fingerprinting

```text
sha256 for exact file duplicate
text_hash for document duplicate after removing whitespace/reference noise
perceptual_hash for image near-duplicates
embedding/similarity search for visual repeats
```

### Review flag

```json
{
  "flag_type": "possible_reused_image",
  "severity": "review",
  "current_case_id": "WI-123",
  "matched_case_id": "WI-099",
  "similarity": 0.97,
  "evidence": "perceptual image hash match"
}
```

## Human workflow

This should be a review queue, not an automated rejection. Staff should see:

- current evidence;
- matched prior evidence;
- reason for match;
- confidence;
- action buttons: `accept`, `not duplicate`, `needs engineer review`, `link cases`.

## Controls

- Image metadata can include sensitive information; store and show only necessary fields.
- Avoid broad access to all historical images.
- No automatic fraud wording.
- Preserve original files unchanged.

## MVP

Start with exact duplicate detection using SHA-256 for documents/images and normalised VRM matching. Add perceptual image hashing only after the exact-hash system is working.


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
