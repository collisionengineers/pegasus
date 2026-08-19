# Files — TICK-205

## Where the change lands

| Path | Why |
|---|---|
| `docs/frd/frd-06-vehicle-and-engineering-evidence.md` | Clarify ENG-01's canonical-specification rule: one accepted version per role/purpose, with Audit owning the accepted conservative and maximised pair; define provenance, acceptance, correction and fail-closed constraints. |
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | Define the RPT-03 input/output behaviour: bind exact accepted pair, compute uplift once, retain identities/versions, and never render from missing or ambiguous inputs. |
| `docs/capabilities.md` | Reconcile ENG-01 and RPT-03 notes so the registry no longer reads as contradictory; keep canonical owners unchanged. |
| `src/Pegasus.Core/Assessment/**` | Later implementation surface for the versioned repair-specification aggregate, closed conservative/maximised roles, acceptance/compatibility policy, computed totals/uplift and query/command contracts. Reuse existing line vocabulary and actor confirmation rules. |
| `src/Pegasus.Infrastructure/Persistence/Assessment*.cs` | Later persistence surface for specification identity, role, version, source/provenance, ordered lines, totals, acceptance and supersession without overwriting history. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/**` | Ordered schema migration for the versioned aggregate; exact migration belongs to implementation planning. |
| `tests/Pegasus.Core.Tests/Assessment/**` | Prove coexistence, role uniqueness, acceptance gates, compatible-basis checks, derived uplift, correction/versioning and fail-closed selection. |
| `tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs` (or focused report-spec tests) | Prove durable pair/history, concurrency, ordering, source identity and reload behaviour. |

## Context files

| Path | What it tells the implementer |
|---|---|
| EPIC-004 `context.md` | Reports use Core-owned accepted data, immutable identity/provenance and fail-closed selection; rendererref1 is evidence only. |
| SIMPLI-014 `open-questions` | Binding 2026-08-19 operator answer: retain two immutable Audit specifications and compute uplift; Audit template separately deferred. |
| `docs/operator-notes.md` | Audit means Collision Engineers audits another engineering firm's original report; original evidence and case/reference semantics remain distinct. |
| `docs/frd/frd-06-vehicle-and-engineering-evidence.md` | Human/Core authority, source-labelled evidence and correction-by-superseding-version rules. |
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | Report version/hash/provenance/finality rules and compute-once deterministic boundary. |
| `docs/capabilities.md` ENG-01, RPT-01 and RPT-03 | The apparent conflict and its intended downstream acceptance boundary. |
| [[TICK-093]] | Owning ENG-01 capability ticket and its source-route activation constraints. |
| `src/Pegasus.Core/Assessment/AssessmentContracts.cs` | Current singleton ordered estimate-line collection, line vocabulary and confirmation model that must be evolved rather than duplicated. |
| `src/Pegasus.Core/Assessment/AssessmentPolicy.cs` | Existing validation/readiness convention and single Core owner to extend. |
| `src/Pegasus.Infrastructure/Persistence/AssessmentEntities.cs` and `EfCaseAssessmentStore.cs` | Current storage/history transaction shape and where singleton assumptions exist. |
| `src/Pegasus.Core/Cases/CaseContracts.cs` | Audit outcome/reference identity is a different concept; do not overload it as a repair-specification role. |
| `workspaces/report-renderer/src/CollisionRenderer.Core/TemplateCatalog.cs` and `Models/Documents.cs` | No Audit pair/uplift model or accepted template exists in the imported renderer. |
| `reference/rendererref1/report_data_schema.json` | Assessment evidence has one worklist and cannot be treated as the Audit contract. |
| [[TICK-207]] | Audit rendering stays deferred pending supplied/approved representative template. |

## Ripple effects

- [[TICK-093]] must implement the shared canonical/versioned repair-specification owner before RPT-03 can consume two accepted roles without duplication.
- [[SIMPLI-014]] must expose a renderer contract capable of carrying exact Audit specification identities/versions later, but must not invent the missing Audit template.
- [[TICK-207]] supplies/approves Audit presentation; it is a separate blocker for rendered acceptance.
- DOCS-001/report generation must bind the pair and calculation-rule version into immutable report provenance.
- Estimate UI, Automation Actor writes/import routes, Audatex/Glass's mapping, action history, concurrency, migrations, case projection and report-readiness rules will all be affected by later implementation.
- Tests must distinguish “two intentional role records” from “two competing canonical current records.”

## Out of scope

- No Audit layout, legal wording, template or sample artifact is invented.
- No renderer source, template catalogue or Azure runtime change is part of this decision ticket.
- No live Glass's/Audatex/AI integration, external call, mailbox/Box/Azure write or deployment.
- No change to Audit case/reference allocation, original-report intake evidence or custody hierarchy.
- No percentage-uplift display unless its denominator and rounding rule are separately accepted; monetary difference is the only presently implied computation.
