# Files mapping — INTK-008

## Implementation files

| Path | Change | Risk |
|---|---|---|
| src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs | Add lifecycle state, merge/close commands/results, history records, store/query ports. | Core policy owner; all fakes/callers must preserve idempotency. |
| src/Pegasus.Core/ImageIntake/ImageIntakeLifecycle.cs | Centralize legal transitions, actor/reason validation, and terminal rejection. | Must not duplicate formal Case lifecycle or allow reopen/reference reuse. |
| src/Pegasus.Core/ImageIntake/ImageIntakeCasePairing.cs | After exact unambiguous accepted-Case association, invoke ImageIntake merge projection. | Formal acceptance remains non-blocking and replay-safe. |
| src/Pegasus.Infrastructure/Persistence/ImageIntakeEntities.cs | Add current lifecycle projection and append-only lifecycle event entity. | Existing rows default to AwaitingInstruction. |
| src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs | Map lifecycle/event tables, keys, indexes, and concurrency. | Migration and SQL expectations change. |
| src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs | Implement transition, query, history, replay, and CAS transaction. | Retain receipt-derived association and unique reference allocation. |
| src/Pegasus.Infrastructure/Persistence/Migrations/*ImageInitiatedLifecycle*.cs | Add additive schema migration. | Migration ordering and integration expectations. |
| src/Pegasus.Core/Custody/CustodyContracts.cs and src/Pegasus.Infrastructure/Custody/BoxCaseCustody.cs | Extend the existing Box boundary for a VRM-reference custody target if case-scoped port cannot safely express it. | Requires ADR; preserve root fence and local-alpha safety. |
| src/Pegasus.Web/Pages/ImageIntake/* | Rename route labels; show lifecycle, grouped evidence, history, and reasoned staff-close form. | Keep Administrator/Engineer/User and casework authorization. |
| src/Pegasus.Web/Pages/Cases/Details.cshtml(.cs) | Show Image-initiated reference and merge event in formal Case history. | Never replace formal Case identity. |
| src/Pegasus.Web/Pages/Cases/Index.cshtml(.cs) | Keep Image-initiated rows in search with state labels. | Preserve exact accessible search identity. |
| tests/Pegasus.Core.Tests/ImageIntake/* | Transition matrix, invalid terminal changes, replay, history, pairing. | Business policy regression coverage. |
| tests/Pegasus.IntegrationTests/ImageIntake* and web tests | SQL migration, persistence, search, merge, closure, and authorization. | No real Box mutation; use existing local/fake boundary. |

## Governing documentation files

| Path | Required amendment |
|---|---|
| docs/prd/pegasus-product.md | Add the two Case origins and Image-initiated outcome. |
| docs/frd/frd-01-case-identity-and-lifecycle.md | Formal Instruction-initiated Case remains the only Case/PO allocator; Image-initiated merge/closure is a separate projection shown in formal history. |
| docs/frd/frd-02-intake-and-source-identity.md | Replace pre-Case-only wording with Image-initiated lifecycle, VRM sequence, matching, and preserved source identity. |
| docs/frd/frd-05-documents-extraction-and-custody.md | Define Box custody for Image-initiated references under the existing root and retain staging/local-alpha restrictions. |
| docs/frd/frd-06-vehicle-and-engineering-evidence.md | Readable VRM may create Image-initiated reference before instructions; unreadable/conflicting evidence goes to INTK-007. |
| docs/frd/frd-12-operator-experience.md | Define searchable rows, states, merge history, and reasoned staff closure. |
| docs/design/README.md | Update vocabulary, list/detail surfaces, search, lifecycle action, and custody presentation. |
| docs/capabilities.md, docs/index.md, CONTEXT.md | Reconcile ownership and glossary. |
| docs/adr/0029-image-initiated-case-projection.md | New accepted ADR superseding ADR-0013 for this durable technical boundary; update ADR index. |

## Context files

AGENTS.md governs formal Case invariants, one Core owner, Box/local-alpha safety, and ADR rules. EPIC-007/context.md binds grouped outcomes and conflicting-VRM boundaries. docs/index.md governs authority and Markdown placement. FRD-01 prevents a principal-less formal Case and generic close. FRD-02 governs source identity and association history. FRD-05 governs Box/staging. design/README.md governs UI labels and action rules. ADR-0013 is accepted stale wording to supersede, never edit in place. Existing ImageIntake Core/infrastructure files are the owner and reuse seam.

## Out of scope

INTK-006 recognition/grouping; INTK-007 U<n> and conflicting_vrms persistence; formal Case/PO, Principal, Audit, or report allocation; a new runtime/database/deployment unit; a second Box client; and external Box mutation in local alpha.
