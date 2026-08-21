# Files — ENG-009

## Where the change lands

| Path | Why |
|---|---|
| `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml` | Add the Engineer-only Cazana-request form within the existing valuation section; no vehicle-data controls or figure display. |
| `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs` | Inject ENG-008's command, add the POST handler, apply existing actor/operation-key/error/redirect conventions. |
| `tests/Pegasus.IntegrationTests/Assessment*WebTests.cs` | Cover the form, authorization, command forwarding, no-direct-provider boundary, and status outcomes. |
| `docs/frd/frd-13-cazana-valuation.md` | New governing behaviour required before the feature may move into implementation; created by PLAT-022. |
| `docs/capabilities.md` and `docs/frd/README.md` | Register the new FRD and its EXT-07/10/13 ownership. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs` | Existing POST operations acquire actor/context and return short `TempData` statuses; estimate import is the Engineer-role precedent. |
| `src/Pegasus.Core/Cases/CaseDataContracts.cs` | The case owns registration, mileage/unit, and incident date; these facts must never originate in the Razor form. |
| `reference/cazana-api-spec.json` | Exact supported endpoint and parameters; ENG-009 deliberately does not speak this HTTP contract. |
| `docs/frd/frd-06-vehicle-and-engineering-evidence.md` | Vehicle enrichment does not activate valuation and source-labelled evidence remains separate from Engineer selection. |
| `docs/frd/frd-07-eva-and-external-engineering-handoff.md` | External adapters require a caller, vendor access, mapping, recovery, and acceptance evidence. |
| `docs/design/README.md` | The UI must remain compact and must not add explanatory copy. |

## Ripple effects

- ENG-008 must provide the Core command and its direct backend request tests, including the incident-date mapping.
- PLAT-022 owns credential/configuration and activation evidence; INTK-026 owns canonical miles and conversion provenance.
- The new FRD becomes the governing ref for ENG-008, ENG-009, and PLAT-022.

## Out of scope

- Any Cazana HTTP client, Worker scheduling/queue, authentication configuration, Azure change, key retrieval, sandbox/live call, result persistence, guide-figure rendering, Engineer selection, and legacy mileage conversion.
