# DVLA/DVSA Vehicle Intelligence MCP

Generated: 2026-05-22

**Type:** MCP server
**Priority:** Immediate

## Objective

Industrialise the existing DVLA/DVSA MCP into a core vehicle-enrichment service for make/model verification, MOT/mileage history, MOT status, discrepancy checks, and valuation inputs.

## Why it matters for Collision Engineers

The plan already identifies DVLA/DVSA as a key MCP. DVLA VES can return vehicle details from registration, and DVSA MOT history includes MOT tests and mileage readings. This is foundational for mileage estimation, vehicle-model cross-checks, and evidence-backed notes.

## Proposed shape

One MCP server wraps DVLA VES and DVSA MOT History. It normalises VRM, caches responses, records source timestamps, and returns an evidence object rather than just raw values.

## Candidate tools / MCP methods / skill actions

- `lookup_vehicle(vrm)`
- `get_mot_history(vrm)`
- `estimate_current_mileage(vrm, incident_date?)`
- `compare_instruction_vehicle(vrm, extracted_make_model)`
- `vehicle_discrepancy_report(work_item_id)`
- `cache_status(vrm)`

## Inputs

- VRM
- incident date
- inspection date
- extracted vehicle model/mileage/unit

## Outputs

- Vehicle spec
- tax/MOT status where available
- MOT mileage timeline
- estimated mileage
- confidence/evidence snippets
- discrepancy flags

## Guardrails

- Flag estimated mileage clearly.
- Do not overwrite dashboard/image mileage without review.
- Cache responses with expiry and audit.
- Avoid exposing API keys to agents.
- Normalise VRM before lookup.

## MVP implementation path

1. Start with lookup_vehicle and get_mot_history.
2. Add mileage interpolation from last MOT date to incident/inspection date.
3. Add evidence text for engineer notes when mileage is estimated.
4. Feed valuation and EVA setup workflows.

## Test / acceptance criteria

- Known VRM returns expected make/model.
- Missing mileage triggers MOT estimate.
- Dashboard mileage overrides estimated mileage with evidence.
- Model mismatch creates review flag.

## Risks and open questions

- API access/registration.
- MOT data can be stale or absent for new vehicles.
- Mileage extrapolation can be wrong if vehicle usage changed.
- Northern Ireland/vehicle class differences.

## Project source basis

- phase_ai_tools.md
- EVA User Guide.pdf
- DVLA VES docs
- DVSA MOT History docs

## External reference basis

- Model Context Protocol documentation: https://modelcontextprotocol.io/docs/getting-started/intro
- OpenAI tools / Agents documentation: https://developers.openai.com/api/docs/guides/tools
- DVLA Vehicle Enquiry Service API: https://developer-portal.driver-vehicle-licensing.api.gov.uk/apis/vehicle-enquiry-service/vehicle-enquiry-service-description.html
- DVSA MOT History API: https://documentation.history.mot.api.gov.uk/
