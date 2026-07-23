# Architecture, contracts and integrations

The predecessor architecture is not a starting point for v2. Current v2 architecture is the accepted .NET 10 modular monolith in `docs/architecture/decisions/`.

## Legacy contracts

| File | Brief contents | Current v2 comparison |
| --- | --- | --- |
| [`contracts/README.md`](../contracts/README.md) | Old contract-governance rules and runtime snapshot process. | **Predecessor-specific.** v2 has no equivalent generated runtime-contract ledger. |
| [`contracts/capture.v1.yaml`](../contracts/capture.v1.yaml) | OpenAPI contract for staff-created public guided vehicle-photo sessions. | **Deferred in v2.** Guided/mobile capture is not first-MVP scope. |
| [`contracts/eva-payload.schema.json`](../contracts/eva-payload.schema.json) | Twelve-field EVA JSON schema covering provider, claimant, vehicle, dates, accident, address, VAT and mileage. | **Useful review input.** EVA JSON/image export is planned, but this exact schema is not adopted or implemented. |
| [`contracts/vehicle-data-v1.schema.json`](../contracts/vehicle-data-v1.schema.json) | Canonical vehicle lookup, provider snapshots, displayed values and mileage-estimation warnings. | **Vehicle lookup is planned.** Exact contract and algorithms need current design and operator review. |
| [`contracts/runtime-contract.snapshot.json`](../contracts/runtime-contract.snapshot.json) | Generated snapshot of 191 old HTTP routes, DTOs, schemas, auth policies, resources, PostgreSQL and numeric codes. | **Conflicts with v2 architecture.** Do not recreate or treat it as an API inventory for v2. |
| [`contracts/runtime-contract.approved-deltas.json`](../contracts/runtime-contract.approved-deltas.json) | Old approvals for changes from a generated runtime baseline. | **Predecessor-specific.** It has no authority over v2. |

## Legacy ADRs

| File | Decision described | Current v2 position |
| --- | --- | --- |
| [`0001`](../docs/adr/0001-repairer-first-class-entity.md) | Repairer as a reusable organisation. | **Planned concept; not implemented.** Confirm required fields and relationships. |
| [`0002`](../docs/adr/0002-vrm-open-case-correlation.md) | VRM-based correlation limited to compatible open cases. | **Concept overlaps v2 matching.** Exact matching rules remain an operator decision. |
| [`0003`](../docs/adr/0003-channel-aware-chasers-whatsapp-constraint.md) | Chasers vary by source channel, with WhatsApp limits. | **Chasers planned; WhatsApp automation deferred.** Exact channel rules are not adopted. |
| [`0004`](../docs/adr/0004-parser-as-azure-function-inline.md) | Parsing as an inline Azure Function service boundary. | **Conflicts with v2.** Parsing belongs behind the shared Core use case and adapters; no separate parser service is approved. |
| [`0005`](../docs/adr/0005-eva-api-full-scope-test-environment.md) | Direct EVA Sentry API remains in scope and test-first. | **Deferred in v2.** First MVP uses reviewed JSON and images for manual transfer. |
| [`0006`](../docs/adr/0006-vehicle-enrichment-service-boundary.md) | One REST service for DVLA/DVSA and mileage. | **Capability planned, boundary not adopted.** A new service is not justified in the modular monolith. |
| [`0007`](../docs/adr/0007-receipt-of-images.md) | Five image-receipt channels and source tracking. | **Partly overlaps planned intake.** Guided capture and automated WhatsApp remain deferred. |
| [`0008`](../docs/adr/0008-tool-boundary-ends-at-eva-handoff.md) | Product responsibility through confirmed report delivery. | **Broadly overlaps current lifecycle.** Exact EVA/report evidence remains unresolved. |
| [`0009`](../docs/adr/0009-image-processing-suggestion-first.md) | Staged AI/image suggestions and human confirmation. | **Deferred in v2.** Ordinary image retention is locally implemented; AI/vision is not. |
| [`0010`](../docs/adr/0010-dedup-reference-disambiguated-no-time-window.md) | Reference-aware deduplication without a time window. | **Useful matching prompt.** Current local intake preserves occurrences; full case deduplication is not implemented. |
| [`0011`](../docs/adr/0011-work-provider-intermediary-garage-roles.md) | Distinct provider, intermediary, repairer and image-source roles. | **Review only.** Current documents name these parties but do not settle this exact model. |
| [`0012`](../docs/adr/0012-box-centric-intake-additive-hybrid.md) | Box as additive archive and intake surface with no automated deletion. | **Box custody is planned.** Old “Archive” model and Box intake routes are not automatically adopted. |
| [`0013`](../docs/adr/0013-loc-export-artifact-no-runtime-address-matching.md) | Staff chooses inspection address; retired `Loc` runtime matching. | **Partly aligned.** `Image Based Assessment` is current; prediction/mapping is deferred. |
| [`0014`](../docs/adr/0014-audit-case-type-second-inspection.md) | Audit shapes and derived QDOS audit identifier. | **Partly aligned, partly conflicting.** Current v2 uses `Inspection + Audit` and one shared principal/year sequence. |
| [`0015`](../docs/adr/0015-email-triage-inbox-management.md) | Deterministic category engine for every approved mailbox message. | **Planned capability, unresolved policy.** Category predicates and correction behavior remain open. |
| [`0016`](../docs/adr/0016-inspection-address-corpus-eva-export.md) | Suggestions from validated historic address exports. | **Deferred prediction/mapping.** Raw data may inform future operator review. |
| [`0018`](../docs/adr/0018-cedocumentmapper-dual-target-vendored-engine.md) | Vendored predecessor parser core. | **Conflicts with v2 clean-room direction.** v2 does not reuse `cedocumentmapper`. |
| [`0019`](../docs/adr/0019-triage-policy-stage-split.md) | Separate parsing, deterministic triage policy and AI suggestions. | **Some boundary ideas align; old policy does not.** Business Triage remains open. |
| [`0020`](../docs/adr/0020-provider-api-intake-channel.md) | Machine-to-machine provider intake. | **Planned in v2; not implemented.** Current authentication and response scope differ from the old contract. |
| [`0021`](../docs/adr/0021-case-po-marker-taxonomy.md) | Independent reference sequences per marker. | **Direct conflict.** v2 requires one shared three-digit sequence across all case types. |
| [`0022`](../docs/adr/0022-retroactive-case-reconstruction.md) | Reconstruct cases from old Box/Outlook evidence. | **Excluded for v2 cutover.** v2 starts without predecessor case/state migration. |
| [`0023`](../docs/adr/0023-mcp-server-hosting-and-auth.md) | MCP hosted with old Data API under tiered access. | **MCP is planned, old hosting/auth is not.** Current v2 requires separate internal staff OAuth and Core use cases. |
| [`0024`](../docs/adr/0024-assistant-write-tier-confirmation-protocol.md) | Propose, confirm, then execute assistant writes. | **Review only.** Current MCP still enforces roles and audits; exact confirmation UX is not adopted. |
| [`0025`](../docs/adr/0025-shared-capability-registry.md) | Shared registry across old AI surfaces. | **Predecessor-specific and speculative for v2.** No AI capability registry is planned now. |
| [`0026`](../docs/adr/0026-rls-as-final-authorization.md) | PostgreSQL row-level security as final authorization. | **Conflicts with v2.** v2 uses ASP.NET Core authorization and Azure SQL. |
| [`0027`](../docs/adr/0027-ship-dark-gate-model.md) | Default-off deployment gates for features. | **Not adopted.** v2 forbids dormant flags and registered-but-uncalled components. |
| [`0028`](../docs/adr/0028-three-tier-compute-topology.md) | SPA, TypeScript services and Python functions. | **Direct architecture conflict.** v2 has four .NET projects in one modular monolith. |
| [`0029`](../docs/adr/0029-staff-identity-jose-msal-pkce.md) | Entra/MSAL staff authentication. | **Direct product conflict.** v2 uses application-managed usernames and passwords. |
| [`0030`](../docs/adr/0030-outbox-generation-counter-reliability.md) | Old Box archive mirror outbox and generation counters. | **Predecessor-specific.** Retry/idempotency ideas may be reviewed for future Box work. |
| [`0031`](../docs/adr/0031-server-runtime-boundary.md) | Separate TypeScript server-runtime package. | **Direct architecture conflict.** |
| [`0032`](../docs/adr/0032-python-independent-packaging.md) | Independently packaged Python Function services. | **Direct architecture conflict.** No Python runtime unit is approved. |
| [`0033`](../docs/adr/0033-anti-drift-guard-doctrine.md) | Generated plan metadata drives drift guards. | **Predecessor delivery machinery.** v2 uses focused architecture/tests only for proven failures. |
| [`0034`](../docs/adr/0034-guided-capture-repository-consolidation.md) | Merge guided-capture browser app into old monorepo. | **Deferred capability and architecture conflict.** |
| [`0035`](../docs/adr/0035-cedocumentmapper-engine-repository-consolidation.md) | Merge the predecessor parser engine into the old repo. | **Conflicts with clean-room v2.** |
| [`0036`](../docs/adr/0036-parse-fed-unified-triage.md) | Parse before classification in a unified old triage pipeline. | **General ordering is relevant; implementation is not adopted.** Mailbox predicates remain open. |
| [`README`](../docs/adr/README.md) | Old ADR catalogue, statuses and conventions. | **Predecessor-specific.** Its “accepted” labels do not apply to v2. |
| [`active - Shortcut.lnk`](<../docs/adr/active%20-%20Shortcut.lnk>) | Windows shortcut to the old CollisionSuite `active` directory. | **No product meaning.** It is a workstation artefact. |

## Legacy architecture descriptions

| File | Brief contents | Current v2 comparison |
| --- | --- | --- |
| [`README.md`](../docs/architecture/README.md) | Old architecture navigation and SPA/TypeScript/Python/PostgreSQL summary. | **Conflicts with v2.** |
| [`system-overview.md`](../docs/architecture/system-overview.md) | Old components, runtime topology, data and service boundaries. | **Predecessor-specific architecture.** Do not use as a migration blueprint. |
| [`data-model.md`](../docs/architecture/data-model.md) | PostgreSQL entities, status codes, evidence, email, audit and RLS model. | **Concept discovery only.** v2 currently persists intake receipts/drafts/assets/audit events, not this case model. |
| [`integrations.md`](../docs/architecture/integrations.md) | Old Outlook, Box, EVA, parser, AI and vehicle integration paths. | **Systems overlap; adapters and flows do not.** None of these live integrations is implemented in v2. |
| [`eva-field-model.md`](../docs/architecture/eva-field-model.md) | Mapping between extracted case values and EVA payload fields. | **Useful for operator review.** Current EVA export is planned, not implemented. |
| [`eva-sentry-api.md`](../docs/architecture/eva-sentry-api.md) | Old EVA API endpoint and payload interpretation. | **Deferred direct integration.** Validate with vendor before any use. |
| [`inspection-address-corpus.md`](../docs/architecture/inspection-address-corpus.md) | Historical address export, validation and suggestion model. | **Prediction/mapping deferred.** |
| [`vehicle-data.md`](../docs/architecture/vehicle-data.md) | Provider lookups, canonical vehicle facts and mileage estimation. | **Capability planned; no adapter implemented.** |
| [`mcp-image-ingestion.md`](../docs/architecture/mcp-image-ingestion.md) | Old registration-based image ingestion through MCP. | **Not in current first-MVP MCP contract.** Needs explicit operator decision. |
| [`guided-capture.md`](../docs/architecture/guided-capture.md) | Public photo-capture PWA, sessions, storage and security. | **Deferred.** |

## Highest-risk contradictions

- Old TypeScript/SPA/Python/PostgreSQL topology versus the accepted .NET/Razor/Azure SQL modular monolith.
- Entra/MSAL staff sign-in versus application-managed staff accounts.
- Independent or four-digit Case/PO sequences versus the current shared three-digit sequence.
- Reusing or vendoring the predecessor parser versus the clean-room implementation.
- Direct EVA API, guided capture and AI/vision work presented as active old scope although they are deferred in v2.
