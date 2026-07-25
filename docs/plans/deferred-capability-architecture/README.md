# Deferred-capability architecture reconciliation

Status: **Draft plan — reference review refined and independently reviewed; canonical follow-up unapproved**

## Finish line

This slice finishes when the requested sources have been compared with the saved plan, the temporary review is complete, and each supported correction is expressed as a separately approvable proposal without changing product authority or accepted ADRs. A later, separately authorised documentation task may make canonical product scope consistent and add only decision-local architecture impacts. Both outcomes preserve the source-of-truth order, distinguish current requirements from deferrals and exclusions, and make no claim that a deferred capability is implemented or called.

## Authority and boundaries

- The current user instruction is that deferred items must be factored into architecture from the start and that deferral does not mean ignoring them. It authorises refinement of this saved plan pack against the requested evidence; it does not itself amend a product requirement or accept an ADR proposal.
- Apply the [source-of-truth order](../../agent-guidance/source-of-truth.md) and the [repository deferred-capability contract](../../../AGENTS.md#deferred-capability-discipline).
- Product classifications remain owned by operator truth and the [discovery questionnaire](../../../PROJECT_DISCOVERY_QUESTIONNAIRE.md). The [open-decision register](../open-decisions.md) tracks unresolved ambiguity, and the remaining-requirements document is planning evidence; neither is promoted above the repository's source-of-truth order.
- The specifically reviewed material under `docs/reference/` supplies candidate shapes, conflicts, and failure evidence only. A report labelled as an accepted operator finding is reconciled against operator notes and the questionnaire before any supported part is promoted; the label does not elevate the whole report or its predecessor design.
- Previous-CollisionSpike migration/shutdown exclusions do not decide an eventual EVA replacement, migration, coexistence period, or cutover. First-MVP exclusions are not permanent exclusions, and ADR-local alternatives such as Graph webhooks, PDF-engine replacement, module extraction, or generic capacity upgrades are not product classifications.
- This plan authorises local documentation work only. It does not authorise product implementation, corpus changes, Azure changes, deployment, external calls, credential work, or operator-note edits.
- Required-now work blocked by an unresolved decision is not to be relabelled as deferred. Explicit exclusions are not to be promoted into future commitments.

## Stable invariants

- One Core-owned policy remains authoritative for each business rule; a deferred channel or integration cannot become a parallel intake, matching, numbering, workflow, or permission engine.
- Principal identity, case identity, shared principal/year sequencing, explicit replacement-case relationships, and the named case relationships already required for principals, claimants, staff/Engineers, repairers/bodyshops, insurers, and contacts remain representable across future capability additions; source/provenance and recorded changes remain in permanent case action history. This does not authorise a generic party master, CRM, import, deduplication policy, cardinality, prescribed temporal schema or reference aliases.
- Original source occurrences, intake origin, evidence provenance, and Box folder/file/version identifiers remain distinct from content hashes and downstream copies. Box remains the long-term file authority while the application owns workflow, relationships, processing state, permanent action history, and external links.
- Application case association, external channel/message identity, Box custody, prepared or copied communication, authoritative sent-report evidence, post-report work, and business lifecycle transitions are not interchangeable. The Box report file/version, EVA report-generation authority, and application workflow facts are likewise distinct. Future reply/thread, external-delivery, and report-release contracts may require new data and migrations; this separation does not require every possible fact to be persisted before its operation is authorised.
- A compatibility contract is either an existing current-scope identity, data, contract, or adapter boundary, or an explicit future migration. It is not dormant implementation.
- `Planned`, `Implemented`, `Called`, `Locally verified`, `Deployed`, `Live verified`, and `Accepted` remain distinct evidence states.

## Delivery order

| Order | Area or task | Requires | Real or intended caller | Unlocks |
|---|---|---|---|---|
| 1 | [Reconcile product classifications and ADR impacts](architecture/deferred-capability-reconciliation.md) | Current questionnaire, operator notes, planning evidence, ambiguity register, accepted ADRs, the authorised reference-review findings, and caller inventory | Architecture authors and reviewers; there is no runtime caller for this documentation-only outcome | Review-complete, separately approvable proposals that preserve named future capability paths without speculative code or parallel authority |

## Ownership and merge hotspots

| Boundary | Single owner | Consumers | Coordination rule |
|---|---|---|---|
| Product-scope classification | Project/product owner through the discovery questionnaire | ADRs and delivery plans | Reconcile wording against higher-authority operator notes before changing a classification |
| Cross-cutting architecture invariants | Existing owners of ADR-0002 | Focused ADRs and future feature plans | Record only the modular-monolith, ownership, identity, data-authority, integration, and deployment constraints ADR-0002 actually owns; link canonical product scope and do not create an evergreen capability catalogue |
| Focused architecture decisions | Existing owner of each accepted ADR | Implementers and independent reviewers | Preserve original dates/statuses and record amendments explicitly |
| Current caller evidence | Current feature owner | ADR evidence limits and delivery plans | Re-inventory callers immediately before editing; never preserve a stale implementation claim merely because it appears in this draft |

## Approval boundaries

| Action | Exact scope required | Approval/evidence required |
|---|---|---|
| Propose a change to settled product classification | The affected questionnaire statement only | A separately approved follow-up with direct product-owner decision reconciled with operator notes |
| Propose an amendment to an accepted ADR | The existing decision and its relevant deferred-capability impact | A separately approved follow-up with existing ADR owner review; a new ADR is required if runtime, identity, data ownership, or deployment boundaries change |
| Implement or activate a deferred capability | A separately scoped feature, caller, data, adapter, and environment | Named activation evidence plus the applicable product, licence, cost, security, architecture, or cloud approval |
| Mutate Azure or another external system | Not included in this plan | Fresh exact-target approval; this plan grants none |

## Evidence language

Use `Planned`, `Implemented`, `Called`, `Locally verified`, `Deployed`, `Live verified`, and `Accepted` literally. Record execution evidence in the [owning area task](architecture/deferred-capability-reconciliation.md), not in this index.

## Integrated acceptance journey

An independent reviewer reads the current authoritative sources, checks every named deferral in temporary clause-level evidence, confirms that ADR-0002 contains only the cross-cutting constraints it owns, and confirms that each proposed focused-ADR impact is decision-local. Reference-only concepts remain rejected or withheld. Negative review proves that previous-CollisionSpike exclusions are not applied to EVA, direct EVA API use is separate from EVA replacement, ADR-local alternatives are not product scope, required-now ambiguity is not relabelled as deferred, and unapproved legacy fields/statuses/contracts remain absent. The reviewer also confirms that unsupported current paths remain absent and runs scoped Markdown and repository checks. This proves plan consistency only; authoritative amendments and each later capability still require separate approval, real-caller evidence, and operator-visible acceptance.

## Plan maintenance

Reconcile this plan whenever operator truth, the questionnaire, the canonical deferral list, an accepted ADR, or caller reality changes. Do not copy mutable workspace status, cloud inventory, prices, or implementation evidence into this index. Once separately approved findings are transferred to their canonical owners, remove or consolidate this temporary review material so it does not become a second product or architecture authority.
