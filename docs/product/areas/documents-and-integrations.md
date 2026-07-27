# Documents and integrations

## Outcome

CollisionSpike processes supported sources without losing occurrences or
provenance, stores long-term originals and case files in Box, enriches vehicle
data where authorized, and produces the bounded EVA handoff required before any
later downstream replacement.

## Settled requirements

- Supported email/document/image shapes preserve original sources, visible
  occurrences, extraction evidence, and bounded failure outcomes.
- The supplied provider corpus and inspection-location corpus are imported as
  normalized reference data with source provenance. Provider/location
  relationships and observed frequency support reviewable suggestions; they do
  not silently become permanent `always use` rules.
- Box is long-term case-file custody; SQL owns workflow identity/history;
  transient Azure storage is not long-term custody.
- Vehicle enrichment uses DVLA/DVSA and MOT evidence where available without
  replacing operator review or inventing missing facts.
- The first-release EVA boundary is reviewed JSON/image handoff and exact report
  evidence. Direct EVA API use and EVA replacement remain later capabilities.
- Estimating, valuation, finance/invoicing, automated report generation,
  WhatsApp automation, and extra providers activate independently.
- Malware scanning, redaction, signatures, automated retention/deletion, legal
  hold, subject-request, and dedicated compliance workflows are not planned.

The stable `DOC-*`, `BOX-*`, `VEH-*`, `EVA-*`, and `EXT-*` outcomes and
allocations live in the [capability inventory](../capabilities.md). Current
system roles are authoritative in the [systems map](../../operator-notes/systems-and-integrations/README.md).

## Current state and activation

Repository ports/adapters and source readers are evidence, but no production
Graph, Box-write, vehicle-data, or EVA caller is accepted or deployed. A change
record must name the exact external contract, credential/RBAC owner, idempotency,
failure/recovery behavior, caller, and separately authorized live validation.

Former [integration plans](../../history/plans/remainder-delivery/integrations/)
and [later integration activations](../../history/plans/later-delivery/integrations/)
remain historical planning evidence only.
