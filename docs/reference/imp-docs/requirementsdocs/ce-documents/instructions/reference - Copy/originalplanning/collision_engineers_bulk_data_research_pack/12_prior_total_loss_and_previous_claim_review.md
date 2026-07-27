# Prior Total Loss and Previous Claim Review

## Concept

Use CE historical case data, EVA reports, VRM notes and approved vehicle history sources to flag vehicles with prior total-loss or prior claim indicators for review.

## Internal basis

The EVA User Guide says staff should use `Eng Notes on Vehicle` when the vehicle is a previous total loss, and must verify that the previous total-loss date is not the same or very similar to the current accident date. That is exactly the kind of process that should be supported by a bulk data layer.

## Use cases

### 1. Same-VRM historical CE case search

When a new instruction arrives, search CE history for the same VRM:

- previous reports;
- previous valuations;
- previous damage zones;
- previous images;
- previous claim dates;
- previous total-loss notes.

### 2. Prior total-loss date sanity check

If a vehicle is marked previous total loss, compare previous loss date with current incident date. If they are close, flag that the note may refer to the current case, not a prior case.

### 3. Damage recurrence review

If previous damage zone is similar to current damage zone, present prior evidence to engineer.

### 4. Duplicate current claim detection

Same VRM + similar date + same principal/reference pattern may indicate duplicate instruction or duplicate report workflow.

## Output language

Avoid making allegations. Use neutral review prompts:

> Previous CE case found for same VRM. Review previous report and images before finalising current assessment.

> Prior total-loss note date appears close to current incident date. Confirm whether this relates to the current case or a previous event.

## Data sources

- CE case database.
- EVA/Sentry report retrieval.
- CE Document Mapper exports.
- image metadata and image similarity results.
- approved commercial vehicle history data, where licensed.

## Implementation

### Matching keys

- normalised VRM;
- claim reference;
- date of loss;
- claimant/client name where lawful and necessary;
- image hashes;
- document checksums;
- EVA reference.

### Review pack

Create a `prior_case_review_pack` containing:

- prior case dates;
- report type;
- damage zone summary;
- valuation/salvage values;
- image thumbnails or links;
- notes;
- confidence score.

## Controls

- Do not auto-label fraud.
- Limit personal data shown in prior-case comparisons.
- Maintain audit trail when staff open/use prior evidence.
- Avoid showing irrelevant old case data to users who do not need it.

## MVP

Start with internal CE history only:

1. same VRM search;
2. current vs previous incident date comparison;
3. previous total-loss flag;
4. link to prior report/evidence.

External history providers can be layered later.


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
