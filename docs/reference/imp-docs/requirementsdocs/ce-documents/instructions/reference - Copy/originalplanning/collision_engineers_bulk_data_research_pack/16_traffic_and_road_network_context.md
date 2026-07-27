# Traffic and Road Network Context

## Concept

Use road network and traffic datasets to add context to incident location, inspection allocation, travel planning and management analytics.

## Data sources

- DfT road traffic statistics and count-point downloads.
- OS Open Roads for a high-level representation of the road network in Great Britain.
- National Highways open data services for strategic road network context.
- Geocoding/postcode datasets where licensed/available.

## Use cases

### 1. Engineer allocation and travel clustering

If physical inspections are required, use location data to cluster appointments by region, road network and travel constraints.

### 2. Incident location enrichment

If accident circumstances include a postcode or location, attach road type and broad network context.

### 3. Workload planning

Track instruction volumes by region, road type or distance from engineer base.

### 4. Forecasted bottleneck analysis

Combine open case locations and engineer calendars to identify inefficient travel patterns.

### 5. Claims environment context

For road-traffic incidents, classify location context:

- urban/rural;
- motorway/A-road/local road;
- junction proximity;
- high-traffic corridor.

## Implementation

1. Normalise addresses and postcodes from CE Document Mapper/EVA.
2. Geocode to approximate coordinates.
3. Join to OS Open Roads or road traffic count points.
4. Store road-context object in case enrichment.
5. Use only broad context unless precise geocoding is validated.

## Output example

```json
{
  "location_context": {
    "postcode_area": "CH49",
    "road_network_context": "local road / urban",
    "nearest_major_route": "A-road within 2km",
    "traffic_context_available": true,
    "confidence": "low_to_medium"
  }
}
```

## Controls

- Inspection address and incident location are often not the same. Keep these separate.
- Geocoding precision may expose personal location data; apply access controls.
- Do not use road context to make liability conclusions.
- Keep original address data minimised and permission-controlled.

## MVP

Use postcode sector or outward postcode only for management analytics, not exact address-level mapping. This reduces privacy risk while still supporting workload and regional insight.


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
