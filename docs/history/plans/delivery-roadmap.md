# CollisionSpike delivery roadmap

> **Archive status — non-authoritative planning evidence.** Revalidate against current product, roadmap, architecture, operations, design, decisions, and code before use.

Pre-conversion status: **Ready dependency route — planning only**

## Purpose and reading contract

This is the sole dependency-ordered route from the present repository state through `Later`/`unallocated` and the separately activation-gated `Later`/`unallocated` continuation. It orders bounded plans, decision gates, safe parallel work and integrated acceptance journeys. It is not an implementation-status board, release ledger or second requirements owner.

- The [feature maturity map](feature-maturity-map.md) alone owns the 213 allocations and each row's primary plan link.
- The linked capability plans own intended behavior, callers, negative cases, validation, rollout/recovery and deferred-capability impact.
- Current implementation evidence belongs in source, tests and dated task/validation artifacts. `Planned`, `Implemented`, `Called`, `Locally verified`, `Deployed`, `Live verified` and `Accepted` remain distinct.
- A plan link does not activate a feature, external service, migration, Azure change or UI direction.
- [Permanent and conditional boundaries](../../product/boundaries.md) are not backlog work.

## Present repository boundary

As source-mapped on 2026-07-26, the only mutating business caller is the Development-only `POST /Intake/Upload` Razor Page calling Core `ProcessIntake` when `Features:LocalIntake` is enabled. Dashboard, queue, review and asset handlers query retained intake receipts. The Worker has no trigger or Core caller. Identity, cases/references, lifecycle, Triage, Box, Graph, EVA, staff MCP, provider API and the `0.1.0-alpha.1` staff UI are still intended rather than called.

The current intake slice proves provider-neutral receipt, bounded source reading, retained local occurrences, typed read-only QDOS draft/evidence and explicit pre-case outcomes. It does not prove case creation, reference allocation, production custody, a deployed environment or operator acceptance. [Remaining requirements](../../product/qdos-alpha-gap.md) owns that `0.1.0-alpha.1` gap baseline.

## Dependency shape

```text
current provider-neutral proof
  -> V0 evaluator + accepted classification policy
  -> V1 identity/custody/registration/reference/acceptance spine
  -> V1 case files/lifecycle/UI/Worker/Triage/EVA/MCP/release acceptance
  -> V1.x provider activation through the same owners
  -> V2 parallel source, email, matching, provider and post-report branches
  -> V3 case-type, channel-automation and AI activation plans
  -> independently gated V3+ EVA/engineering/report/finance work
```

No horizon creates a second intake engine, allocator, lifecycle, classifier, sender, permission model or migration stream.

## `0.0.0-development` pre-alpha

| Order | Plan | Requires | Intended real caller and outcome | Gate / proof limit |
| ---: | --- | --- | --- | --- |
| 1 | [Caller-backed local evidence](../../runbooks/testing/local-testing.md#caller-backed-local-and-live-evidence-gates) | Current repository checks and immutable local inputs | Repository and actual delivered callers produce reproducible evidence | Local checks do not prove cloud, operator or production behavior |
| 2 | [Local EML evaluator and classification foundation](mailbox-categorisation-and-email-matching/development-classification-foundation-and-evaluator.md) | Accepted `0.0.0-development` predicates/evidence from the [combined dossier](mailbox-categorisation-and-email-matching/README.md) | Local folder evaluator calls one versioned Core classification policy and compares human/rule evidence | No Outlook, Graph or mock future caller; corpus originals remain immutable/local |
| 3 | [ADR-0009 local deployment foundation](remainder-delivery/platform/azure-observability-and-release.md#reconcile-infrastructure-and-identity-boundaries) | Caller-backed application slice and local repository checks | Local procedure produces separately hashed Web, Worker and migration bundles with pinned tool/runtime provenance and defines separated deployment, migrator and runtime identities | Local package/identity proof authorises no Azure read or write; the later production release section still depends on shared-development proof |
| 4 | [`0.0.0-development` shared-development target](remainder-delivery/platform/azure-observability-and-release.md#provision-and-prove-shared-development) | Order 3 complete, then exact subscription/tenant/environment/resource-group/SKU/cap approval | Authorised terminal previews/provisions, applies the immutable migration bundle, then deploys the same hashed Web/Worker packages through `azd` service routes | Planning does not authorise Azure read/write; deployment does not prove production acceptance |

The mailbox decision blocks only the classification and automatic matching slices that depend on its predicates. It does not block local harness work or independently definitive/manual workflows.

## `0.1.0-alpha.1` live QDOS alpha

The [`0.1.0-alpha.1` pack](remainder-delivery/README.md) remains the detailed release owner. Its dependency spine is:

| Order | Plan slice | Requires | Intended caller / unlock |
| ---: | --- | --- | --- |
| 1 | [Relational intake draft](remainder-delivery/casework/intake-and-case-acceptance.md#review-and-resolve-an-intake-draft) | Current provider-neutral slice | Existing Development upload/query path evolves without allocating a case/reference |
| 2 | [Staff identity and role enforcement](remainder-delivery/identity-and-access/staff-identity-authorisation-and-action-history.md#authenticate-staff-and-enforce-role-boundaries) | Relational draft | Authenticated Web actors and protected actions |
| 3 | [Action history and administration](remainder-delivery/identity-and-access/staff-identity-authorisation-and-action-history.md#attribute-permanent-action-history-and-automation) | Trusted actors | Permanent business attribution separated from security logs and telemetry |
| 4 | [Principal/configuration administration](remainder-delivery/identity-and-access/staff-identity-authorisation-and-action-history.md#administer-principals-and-live-operational-configuration) and [reviewed provider data](remainder-delivery/casework/intake-and-case-acceptance.md#prepare-reviewed-provider-reference-data) | Administrator identity and one-time reviewed preparation | Stable principal/configuration inputs; no runtime spreadsheet importer |
| 5 | [Durable source custody](remainder-delivery/integrations/source-custody-and-document-processing.md#durable-source-receipt-processing-and-custody-hand-off) | Stable source identity and actors | Web stages manual bytes; future triggered Worker stages Graph bytes; one custody outbox |
| 6 | [Ordinary-image registration](remainder-delivery/casework/intake-and-case-acceptance.md#read-vehicle-registration-from-ordinary-images) and [provisional image identity](remainder-delivery/casework/intake-and-case-acceptance.md#establish-provisional-image-identity-before-acceptance) | Durable image occurrence/provenance | Reviewed registration suggestion identifies pre-case image work; no case/reference or silent overwrite |
| 7 | [Inspection-address reference preparation](remainder-delivery/integrations/vehicle-data-and-eva-export.md#prepare-reviewed-inspection-address-reference-data) | Supplied spreadsheets and authorised one-time reviewer | Versioned local reference output with provenance; no runtime upload/job/sync |
| 8 | [Case identity and reference contracts](remainder-delivery/casework/case-identity-and-references.md) | Configured principal, actor and definitive source contract | One SQL allocator, immutable case identity, Audit references and linked replacement behavior |
| 9 | [Definitive acceptance transaction](remainder-delivery/casework/intake-and-case-acceptance.md#accept-a-definitive-case-transaction) | Orders 1–8 | Intended Worker/Web resolution reaches one Core owner and atomically creates one incomplete `Not ready` case/reference/history/outbox result |
| 10 | [Box case files](remainder-delivery/integrations/box-case-files.md) | Accepted case, custody outbox and separately approved exact Box scope | Named Core case-file use case and bounded adapter |
| 11 | [Exclusive case editing](remainder-delivery/casework/case-editing-concurrency.md#acquire-renew-and-release-one-case-edit-lease) | Authenticated case and action history | One server lease plus row version; other viewers remain read-only |
| 12 | [Lifecycle and work management](remainder-delivery/casework/lifecycle-and-work-management.md) | Case identity and edit guard | Named terminal/reopen/archive, due/chasing/held/task behavior; no second workflow |
| 13 | [Reviewed `0.1.0-alpha.1` UI route](ui-ux/README.md) and [operator workspace](remainder-delivery/casework/operator-workspace.md) | Query/command owners and UI direction approval | Authenticated Operations, Intake, Triage, Case and Administration surfaces |
| 14 | [Scoped Outlook Worker](remainder-delivery/integrations/outlook-and-background-processing.md#scoped-inbound-outlook-receipt-and-processing) | Accepted relevant mailbox contract, allowlist, custody and a real Function trigger | Idempotent staff-forwarded `instructions@` receipt through Core |
| 15 | [Triage](remainder-delivery/casework/triage-workflow.md) | Actors, source identity, registration and accepted exact reply-chain matcher | Separate pre-case roadworthiness workflow; no due date, chasers or case/reference creation |
| 16 | [Local inspection-address resolution](remainder-delivery/integrations/vehicle-data-and-eva-export.md#resolve-inspection-address-from-reviewed-reference-data), [vehicle/MOT lookup](remainder-delivery/integrations/vehicle-data-and-eva-export.md#look-up-vehicle-and-mot-data) and [`0.1.0-alpha.1` EVA bundle](remainder-delivery/integrations/vehicle-data-and-eva-export.md#export-the-alpha-eva-bundle) | Confirmed case data and separately accepted external contracts | Staff-reviewed address or enrichment result and operator-approved EVA JSON/images | No external call or acceptance without the named adapter and exact approved target |
| 17 | [Staff MCP](remainder-delivery/integrations/staff-mcp.md) | Existing staff Core actions, OAuth/roles and edit guard | `/mcp` delegates `0.1.0-alpha.1` case/document/intake actions; no parallel policy |
| 18 | [Azure, observability and release](remainder-delivery/platform/azure-observability-and-release.md) | Caller-backed slices and explicit environment approval | Managed runtime, migration, telemetry, recovery and immutable direct-terminal release evidence |
| 19 | [Operator acceptance and cutover](remainder-delivery/platform/acceptance-and-cutover.md#complete-operator-acceptance-and-production-cutover) | Every `0.1.0-alpha.1` slice through its actual caller | Every active QDOS case type reaches successful EVA export; management acceptance remains separate |

Every definitive authorised instruction creates one incomplete `Not ready` case. Only explicit staff confirmation of separate instruction and image completeness can move that existing case to `Review`. Missing registration, ambiguous standalone Audit evidence, incomplete source processing, custody failure or uncertain principal stays pre-case and allocates nothing.

## `Next`/`unallocated` provider activation

[Additional provider activation](later-delivery/integrations/additional-provider-activation.md#activate-an-additional-provider) starts only after `0.1.0-alpha.1` acceptance. Each provider supplies bounded reference data, contained provider policy, genuine representative evidence and one real caller through the same provider-neutral intake/case owners. Activation must not add another engine, project, allocator, lifecycle or speculative provider framework.

## `Next`/`unallocated` branches

The branches may proceed in parallel only after their shared actor, source, case, Worker and action-history contracts are stable.

| Branch | Ordered plans | Rejoin evidence |
| --- | --- | --- |
| Email | [Four-mailbox identity/classification](later-delivery/integrations/email-workspace-and-association.md#ingest-all-four-mailboxes) -> [classification evidence](later-delivery/integrations/email-workspace-and-association.md#classify-and-explain-mail) -> [reviewed folder moves/actions](later-delivery/integrations/email-workspace-and-association.md#recommend-confirm-and-move-outlook-items) -> [association/workspace/MCP](later-delivery/integrations/email-workspace-and-association.md#deliver-the-email-workspace) | Accepted mailbox predicates for affected automatic slices; exact Graph scope; staff-confirmed mutations; negative mailbox/case isolation; actual Worker/Web/MCP callers |
| Source formats | [DOC/MSG automation](remainder-delivery/integrations/source-custody-and-document-processing.md#automate-legacy-doc-and-msg) and [scan-like-PDF OCR](remainder-delivery/integrations/source-custody-and-document-processing.md#later-targeted-scanned-pdf-ocr) | Bounded genuine local evidence, no ordinary-image OCR conflation, one custody/processing owner |
| Matching and vision | [Image/instruction matching](remainder-delivery/casework/intake-and-case-acceptance.md#later-match-image-led-and-instruction-led-records) then [reviewed image assistance](remainder-delivery/casework/intake-and-case-acceptance.md#later-assist-vehicle-image-and-damage-review) | Accepted definitive predicate permits automatic association; otherwise explainable suggestion, staff confirmation/correction/reversal, provenance and evaluation; no second identity or case engine |
| Provider API | [Submission](remainder-delivery/integrations/provider-submissions.md#receive-principal-scoped-submissions) -> [status/result](remainder-delivery/integrations/provider-submissions.md#return-provider-receipt-status-and-result) -> [credential lifecycle](remainder-delivery/integrations/provider-submissions.md#issue-rotate-and-revoke-provider-credentials) | Accepted versioned wire contract, principal isolation, replay/conflict proof, shared Core intake/query callers |
| Post-report | [Query and dispute workflow](later-delivery/casework/post-report-query-and-dispute.md#resolve-post-report-queries-and-disputes) | Case lifecycle remains authoritative; only automatic email association/evidence inherits the mailbox gate |

`Next`/`unallocated` completes only when the named actual callers and operator/security negative paths are accepted. Registration, an adapter test or a generated UI cannot complete a branch.

## `Later`/`unallocated` release work

1. [Diminution](later-delivery/casework/diminution-and-commercial.md#add-diminution-cases) and [Commercial](later-delivery/casework/diminution-and-commercial.md#add-commercial-cases) begin with direct domain decisions over the existing case identity/lifecycle ports. Their names do not authorise copied QDOS rules.
2. [Automated chasers](later-delivery/integrations/communications-automation.md#automate-chasers) depends on accepted manual chasing, durable outbox, exact external identity, idempotency and recovery. [WhatsApp coexistence](later-delivery/integrations/communications-automation.md#automate-whatsapp-intake-and-coexistence) requires separately approved channel access and source identity. These remain separate Core owners/adapters, not one sender engine.
3. [In-app staff assistance](later-delivery/ai-and-automation/operator-assistance.md#in-app-staff-assistant) requires privacy, licence, cost, evaluation, explanation, review/correction and rollback approval. The additional proof that rules are insufficient applies only to [email assistance](later-delivery/ai-and-automation/operator-assistance.md#assist-email-identification-and-actions), [document assistance](later-delivery/ai-and-automation/operator-assistance.md#assist-document-extraction-and-review) and [address assistance](later-delivery/ai-and-automation/operator-assistance.md#assist-inspection-address-selection), matching `AI-02`, `AI-03`, `AI-04` and `AI-06`.

## `Later`/`unallocated` independently gated continuation

`Later`/`unallocated` is included so every allocated feature has a plan; it is not pulled into `Later`/`unallocated` and does not activate as one programme.

1. [Direct EVA API](later-delivery/integrations/eva-replacement-and-engineering.md#activate-direct-eva-api) requires a usable approved vendor contract and can precede replacement without changing case authority.
2. [EVA engineering replacement](later-delivery/integrations/eva-replacement-and-engineering.md#replace-eva-engineering-workflow) follows accepted coexistence/migration/cutover decisions and precedes actual Engineer assignment, estimating, valuation and report authority.
3. [Estimating and valuation](later-delivery/integrations/eva-replacement-and-engineering.md#deliver-estimating-and-valuation-workflows) and approved [external services](later-delivery/integrations/eva-replacement-and-engineering.md#integrate-approved-estimating-and-valuation-services) activate independently.
4. [Staff-selected AI Assessor](later-delivery/integrations/eva-replacement-and-engineering.md#offer-staff-selected-ai-assessor) exists only after CollisionSpike owns Engineer assignment. It is never automatic and is not an estimating service.
5. [Automatic reports](later-delivery/integrations/communications-automation.md#automate-report-sending) requires accepted report generation/output, exact recipient/delivery evidence and separate dispatch approval.
6. [Accounting and invoicing](later-delivery/integrations/accounting-and-invoicing.md#deliver-accounting-and-invoicing-workflow) has its own contract, security, correction, reconciliation and recovery decisions; it is not hidden inside EVA replacement.

## Permanent and conditional routes

- A `Not planned` row creates no backlog task, placeholder navigation, flag, port, schema, adapter, credential, resource or acceptance gate.
- `EXT-16`, `EXT-17` and `EXT-19` retain no implementation plan or current caller. Only already-required stable source/case/data identities survive until a future direct decision and full planning/UI/architecture route.
- The [activation index](deferred-capability-architecture/README.md) records stable seams and routes; it does not authorise dormant implementation.

## Cross-cutting approval gates

| Gate | Required before action |
| --- | --- |
| Mailbox/Graph | Accepted predicates/evidence for the affected automatic slice; exact mailbox/folder/action scope; approved allowlist and Exchange application RBAC; real trigger and rollback |
| Corpus/genuine material | Frozen local copy/cohort/holdout, hashes and bounds; no source mutation or external transfer |
| Box | Direct approval naming identity, root ID/type/name, descendant targets and permitted operations |
| Azure/deployment | Fresh inventory/current guidance, exact tenant/subscription/environment/resource group/region/SKU/cap and separate preview/write/deploy approval |
| AI/vision | Named capability, lawful data/licence/cost approval, evaluation/holdout, explanation/review/correction, rollback and real caller |
| Vendor/API/channel | Accepted versioned contract, identifiers, credentials, limits, failure/recovery, target environment and separately approved live smoke |
| Production | Migration/rollback/smoke evidence, actual caller journey, operator acceptance, management approval and exact predecessor impact |

## Integrated acceptance journeys

- **`0.0.0-development`:** the local evaluator processes approved ignored working copies, preserves human/rule evidence and proves the actual Core classification policy only after its decision gate.
- **`0.1.0-alpha.1`:** every active QDOS type—Inspection, standalone Audit and Inspection + Audit—travels through real intake, source custody, exactly one reference, reviewed case workflow, Box, exact evidence, staff MCP and successful EVA JSON/image export. Fail-closed paths allocate nothing.
- **`Next`/`unallocated`:** each additional provider repeats the same journey through the same owners with provider-specific evidence and caller proof.
- **`Next`/`unallocated`:** each parallel branch proves its actual Web/Worker/API/MCP entry point, isolation/correction/failure paths and operator-visible result before the branches are accepted together.
- **`Later`/`unallocated`:** each new domain, channel, AI or vendor capability first proves its direct decisions and activation evidence, then its actual caller and rollback; no umbrella plan substitutes for slice acceptance.

## UI/UX gate

The reviewed direction-neutral [UI specification](../../../design/product/ui-spec.md), [feature traceability](../../../design/product/traceability-matrix.md) and three [candidate directions](ui-ux/README.md) cover the `0.1.0-alpha.1` shell. No direction is approved yet. Explicit user selection must precede visual generation and manual visual review. Every `Next`/`unallocated` and `Later`/`unallocated` UI change re-enters the complete UI/UX route; `0.1.0-alpha.1` approval grants no later surface.

## Deferred-capability impact

- **Named deferrals:** all `Next`/`unallocated`, `Later`/`unallocated` and `Later`/`unallocated` plans linked above, plus the three conditional `Later`/`unallocated` rows.
- **Stable seams retained:** principal/case/reference/source/document/external-message identities, typed reviewed data, permanent business history, one Core owner per business policy, bounded ports and one migration stream.
- **Excluded now:** dormant services, projects, schemas, routes, flags, credentials, adapters, generic engines and later UI placeholders.
- **Activation evidence:** the direct decision/contract/approval, actual caller, focused and integration evidence, genuine local evaluation where relevant, operator-visible result, rollback and acceptance named by the owning plan.
- **Irreversible choice:** none is authorised by this roadmap. A new runtime, project, store, migration stream, deployment unit or boundary-changing integration still requires an accepted ADR.

## Maintenance and recovery

Before implementing a plan, re-read its authority, the maturity row, current callers, dependencies and dirty paths. Update the owning plan rather than this route when behavior or acceptance changes. Reconcile allocation changes in the worksheet/map first. Keep commits and staging scoped; never reset, clean, stash, move or delete unrelated work. Run `pwsh ./scripts/Invoke-RepoCheck.ps1` and report repository consistency separately from caller, corpus, cloud, live and operator evidence.
