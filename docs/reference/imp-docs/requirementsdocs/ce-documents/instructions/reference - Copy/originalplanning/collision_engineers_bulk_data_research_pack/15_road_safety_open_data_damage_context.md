# Road Safety Open Data as Damage Context

## Concept

Use DfT road safety open data to build broad contextual models about collision types, vehicles involved and conditions. Use it for pattern context, not case-specific proof.

## Why it matters

The Phase 5 plan suggests accident statistics could inform likely damage based on accident circumstances. DfT road safety open data provides record-level collision, vehicle and casualty files. This can support broad analytics, but it has major scope limitations.

## Data caveat

DfT road safety open data relates to personal injury collisions on public roads reported to police and recorded through STATS19. Many insurance damage claims do not involve personal injury or police reporting. Therefore, it should not be used as a direct representation of CE’s full claim population.

## Useful applications

### 1. Contextual feature library

Build a mapping between coded collision circumstances and broad factors:

- road type;
- junction detail;
- light conditions;
- weather conditions;
- vehicle manoeuvre;
- vehicle type;
- impact context where available.

### 2. Narrative parser training/evaluation

Use road safety fields to define common accident circumstance categories. Then classify CE accident circumstances text into categories such as:

- rear-end;
- parked/unattended;
- reversing;
- lane change;
- roundabout;
- junction emerging;
- hit while parked;
- low-speed impact.

### 3. Damage expectation prompts

Once CE’s own cases have damage zones, combine them with accident categories to produce prompts:

> Circumstances suggest rear impact; check whether rear bumper, tailgate/boot floor, parking sensors, exhaust and rear panel evidence are covered.

This prompt should be trained primarily on CE data, with public road data only as supplementary context.

### 4. Operational hotspot context

Use open data for regional context or to understand common collision environments, not to score individual claim validity.

## Implementation

1. Download DfT collisions, vehicles and casualties datasets.
2. Decode coded fields using the official data guide.
3. Create reference tables for accident context.
4. Build CE-specific accident-circumstance classifier.
5. Validate against real CE cases and engineer feedback.

## Outputs

- accident circumstance category;
- expected evidence checklist;
- missing evidence prompts;
- management dashboard by circumstance type.

## Controls

- Do not infer liability from STATS19 context.
- Do not use public injury-collision data to challenge non-injury claim facts directly.
- Treat it as a taxonomy and context source, not a case truth source.

## MVP

Use DfT data to build a clean collision-circumstance taxonomy, then train/test it on CE accident-circumstances text extracted by CE Document Mapper.


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
