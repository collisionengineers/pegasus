# DVLA VRM Attribute Cache

## Concept

Build a local cache of DVLA Vehicle Enquiry Service responses for VRMs seen in CE cases.

## Why it matters

CE’s document extraction pipeline depends on vehicle identity fields: VRM, make, model, fuel type, year, colour and sometimes emissions/tax/MOT status. The DVLA VES API returns JSON vehicle details from a registration number and is well suited to a cache keyed by normalised VRM.

## Use cases

- Validate extracted make/model against DVLA make/year/fuel/colour.
- Catch OCR errors in VRM extraction.
- Populate missing vehicle attributes.
- Segment workload by vehicle fuel, age, body/registration era, tax/MOT status.
- Improve valuation lookup parameters.
- Flag mismatches between instruction, image plate, MOT and DVLA data.

## Implementation

### Normalise registration before lookup

Use the CE Document Mapper’s existing VRM normalisation approach:

```text
remove spaces -> uppercase -> remove non-alphanumeric where appropriate
```

### Cache response

```sql
dvla_vehicle_cache(
  normalised_vrm primary key,
  raw_response_json,
  make,
  colour,
  fuel_type,
  engine_capacity,
  co2_emissions,
  mot_status,
  tax_status,
  year_of_manufacture,
  month_of_first_registration,
  euro_status,
  real_driving_emissions,
  date_of_last_v5c_issued,
  retrieved_at,
  expires_at
)
```

### Refresh policy

- Refresh on first sight of VRM.
- Refresh if cache is older than a defined period, for example 30–90 days.
- Refresh before final report only if tax/MOT status matters.
- Store historical cache records if changes are useful for audit.

## Data caveat

Do not treat the DVLA response as owner/keeper data. The public vehicle enquiry service provides vehicle details, not keeper identity. CE should avoid storing any unnecessary personal data.

## Review flags

- `vrm_not_found_dvla`;
- `make_mismatch_instruction_vs_dvla`;
- `colour_mismatch_image_vs_dvla`;
- `fuel_type_mismatch_valuation_source`;
- `vehicle_age_unexpected_for_claim`;
- `mot_status_requires_review`.

## Integration points

### CE Document Mapper

After JSON export, call the DVLA cache and attach a `vehicle_validation` object.

### EVA adapter

Use DVLA data to reduce missing fields, but do not overwrite user-verified fields without explicit rules.

### Engineer pack

Show a concise vehicle identity block:

```text
Registration: AB12CDE
DVLA make: FORD
Instruction model: Ford Focus Titanium
Colour: BLUE
Fuel: DIESEL
First registered: 2018-03
Validation: no conflict detected
```

## MVP

Build cache and simple mismatch flags for:

- VRM found/not found;
- make mismatch;
- colour mismatch;
- fuel type;
- year/first registration.


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
