# Weather Context Enrichment

## Concept

Add weather context to cases using the incident date, time and location where available.

## Why this may help

Weather context can support factual case understanding: wet roads, heavy rain, freezing conditions, low visibility or strong winds may be relevant to accident circumstances. The Phase 5 plan correctly identifies weather as a potential contextual dataset.

## Important caveat

Do not overstate weather data. Public Weather DataHub observation endpoints may have limited historical range for recent observations. Older accident dates may require:

- an archive product;
- a commercial arrangement;
- a local capture process started prospectively;
- alternative open climate datasets where suitable.

## Inputs

- incident date;
- incident time if present;
- incident location/postcode/coordinates if present;
- inspection address if incident location is missing;
- claim circumstances text;
- nearest weather station.

## Data sources

- Met Office Weather DataHub observations for recent data.
- Met Office DataHub forecast/site-specific products for forward-looking scheduling contexts.
- Met Office climate data portal or other historical sources for older context, subject to licence and suitability.

## Outputs

```json
{
  "weather_context": {
    "available": true,
    "source": "met_office_observation",
    "nearest_station": "...",
    "distance_km": 12.4,
    "observation_time": "2026-04-14T15:00:00Z",
    "summary": "Rain observed near incident time.",
    "parameters": {
      "precipitation": "...",
      "temperature": "...",
      "wind_speed": "..."
    },
    "confidence": "medium"
  }
}
```

## Use cases

### 1. Engineer pack context

Add a factual note:

> Weather data near the reported incident time indicates rain in the area. This is contextual only and has not been used as a conclusion.

### 2. Claim circumstances consistency

If the accident circumstances mention heavy rain but available weather context shows dry conditions at the nearest observation point, flag for human review, not rejection.

### 3. Inspection scheduling

Use forecast data to help schedule physical inspections or travel-heavy work.

### 4. Operational analysis

Track claim volume and severity patterns against weather periods, but only after controlling for seasonal and geographic effects.

## Implementation

1. Geocode incident location or postcode.
2. Find nearest appropriate observation source.
3. Query/capture weather around incident time.
4. Store raw response and derived summary.
5. Surface only concise context in case pack.

## Controls

- Weather data does not prove causation.
- Never use weather alone to make a liability or fraud conclusion.
- Track uncertainty from missing incident time/location.
- Store source/time/station/distance.

## MVP

Start with a manual or semi-automated enrichment for cases where incident date/time/location are known. Do not attempt full historical reconstruction until a suitable historical weather source is confirmed.


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
