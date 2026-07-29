# Collision AI Centre

Collision AI Centre is an imported engineering source workspace for a possible Windows AI workstation for collision engineers and remote vehicle assessment. Its retained long-term objective is a case-centred experience that can organise instructions and evidence, use governed agents and permissioned connectors, retrieve cited technical knowledge, support assessment and valuation, draft correspondence, and produce reviewable reports and PDFs.

The collision engineer remains the decision-maker and author of record. Every external or professional action requires explicit approval.

## Scope and evidence

The workspace remains at repository-foundation stage. The supplied source records only [Collision Brain](services/collision-brain/README.md) as an implemented and tested standalone component: a provider-neutral ingestion and retrieval service with MCP interfaces, citations, and deletion. That component evidence does not prove Pegasus integration.

No Pegasus caller, dependency, deployment, or business-policy authority is evidenced here. Nothing in this workspace is caller-proved, deployed to Pegasus, or accepted for production merely because source, tests, research, or a proposal exists. No model is approved for production. `Pegasus.Core` and the operator’s explicit decisions prevail.

Keep these evidence states distinct:

- **Intended** describes a direction or retained plan.
- **Implemented** requires implementation evidence; scaffolds and proposals do not qualify.
- **Caller-proved** requires an exercised real caller, not only tests or an interface.
- **Deployed** requires deployment evidence.
- **Accepted** requires the applicable product, architecture, security, operational, and business approvals.

Current rules belong in the canonical documentation. Rationale belongs in [decision records](../../docs/decisions/README.md), and historical change evidence belongs in [change records](../../docs/changes/README.md). Neither history nor a decision record by itself proves implementation, a caller, deployment, or acceptance.

The imported delivery plan was last marked **21 July 2026** and remains sequencing and research for this source workspace, not an activated Pegasus caller.

## Canonical navigation

| Concern | Canonical owner |
|---|---|
| Documentation entry point | [Documentation map](../../docs/index.md) |
| Product and control requirements | [Requirements](../../docs/requirements.md) |
| Bounded capabilities and evidence | [Capabilities](../../docs/capabilities.md) |
| Unresolved allocations and choices | [Open decisions](../../docs/open-decisions.md) |
| System, runtime, and desktop boundaries | [Architecture](docs/architecture.md) |
| Data, security, release, and service procedures | [Operations](../../docs/operations.md) |
| Development and evaluation practice | [Engineering](../../docs/engineering.md) |
| Human operation and approval | [Operator notes](../../docs/operator-notes.md) |
| Architecture and policy rationale | [Decision records](../../docs/decisions/README.md) |
| Historical change evidence | [Change records](../../docs/changes/README.md) |
| Durable visual authority | [Root design](../../design/README.md) |
| Qualified reference material | [Reference index](../../docs/reference/README.md) |
| Azure-specific guidance | [Azure guidance](../../docs/azure/README.md) |
| Repository workspace allocation | [Workspace map](../README.md) |
| ML research, governance, and sequencing | [ML operations](ml-ops/README.md) |
| Self-contained reusable skills | [Skills](skills/README.md) |
| Implemented standalone retrieval service | [Collision Brain](services/collision-brain/README.md) |

Contributors and coding agents must read and follow the applicable workspace instructions before changing this workspace.

## Authority boundaries

| Concern | Authority and local constraint |
|---|---|
| Application behaviour | Canonical Pegasus requirements and accepted application contracts; this source workspace has no business-policy authority. |
| Case domain | `Pegasus.Core` solely owns case, party, vehicle, instruction, artifact, evidence, fact, finding, calculation, decision, report-version, and cutoff contracts. Do not create or consume a parallel case domain here. |
| Mutation and workflow | Future AI adapters must consume Core-owned work-request, proposal, review, approval, and action-history identities. They cannot mutate Pegasus or an external system without an accepted caller, exact authority, and applicable policy. |
| Audit | Pegasus action history remains owned by `Pegasus.Core` and its persistence adapter. AI evaluation records may reference immutable action identities but cannot create a parallel case-audit model. |
| UI and application state | [Root design](../../design/README.md) is the durable visual authority, with the Web application as its runtime mapping. Do not duplicate tokens, components, accessibility rules, layouts, UI policy, or application state here. |
| Documents | Deterministic assembly belongs to `workspaces/report-renderer`; durable visual and letterhead authority belongs to root `design/`. Do not create a duplicate renderer or a model-calling report path here. |
| Connectors | External-system and provider SDK ownership stays behind narrow accepted ports. A connector cannot make a vendor payload the Pegasus domain model. |
| Professional decisions | The operator reviews evidence, confidence, changes, and limitations and remains responsible for approval, issue, signing, or sending. |

## Intended composition and runtime map

The long-term composition is:

1. a collision engineer uses a Windows-installed, case-centred workstation;
2. the workstation consumes accepted Pegasus UI, Core, service API, and renderer contracts;
3. a governed runtime invokes self-contained typed skills;
4. skills use permissioned connectors, cited Collision Brain retrieval, deterministic Core operations, and only approved hosted or owned models;
5. proposed outputs return to an evidence and approval surface before any professional or external action.

The first production slice is intentionally narrower than “an agent that does everything”: it must prove one safe end-to-end case workflow with traceable evidence and measurable engineer benefit.

| Area | Runtime meaning and boundary |
|---|---|
| `services/collision-brain/` | The only component with supplied implementation and test evidence. It provides provider-neutral document ingestion, retrieval, citations, and deletion over MCP. No Pegasus caller or deployment is proved. Services must expose versioned contracts and remain usable independently of a desktop UI. |
| `apps/` | Documentation-only. The very-long-term desktop direction may provide local integration and offline or degraded operation, but no desktop runtime is implemented or activated here. |
| `agents/` | Historical AI-role proposals, not production components or autonomous callers. Intake, assessment, correspondence, report drafting, and quality review are evaluation categories only; they own no case, correspondence, report, valuation, approval, or UI policy. |
| `skills/` | Reusable skill packages remain self-contained and independently validated. Their presence does not activate an agent or prove a caller. |
| `connectors/` | Planned AI-specific adapters that isolate external systems and provider SDKs. No live mailbox or case system is authorised by the scaffold. |
| `packages/` | This workspace does not own Pegasus application contracts. A package may exist here only when needed by at least two AI-workspace consumers and when it does not duplicate Core, audit, design, or rendering ownership. |
| `models/` | Definitions, licences, model cards, inference contracts, promotion manifests, and small configuration only. No production model is approved. |
| `ml-ops/` | Opportunity assessment, governance analysis, training strategy, evaluation framework, and phased delivery research; it does not establish a production caller or release. |
| `corpus/ai-centre/` | Ignored repository-root boundary for authorised private development and evaluation inputs, not a runtime package or Git-tracked archive. |

### Desktop prerequisites

Any framework selection or desktop implementation requires all of the following:

- root product allocation;
- an accepted architecture decision;
- an exercised real caller;
- mapping to the root design contract;
- accepted Core, service API, and renderer contracts; and
- installation, update, security, offline/degraded, storage, recovery, and operational evidence.

The desktop must not duplicate Core policy, application state, audit identity, connector ownership, document rendering, or the root UI contract.

## Agent, package, and connector contracts

### Agent contracts

The only prospective shared package is `packages/agent-contracts/`, for typed AI-specific inputs, proposed changes, evidence references, confidence and limitations, tool calls, approvals, events, abstention, errors, and failure envelopes. Its contracts must work with deterministic test agents as well as model-backed agents.

It requires a separately accepted caller and contract before implementation. The imported `case-domain`, `audit`, `design-system`, and `report-renderer` package proposals are superseded by their Pegasus owners.

### Connector invariants

Every connector must provide:

- a narrow typed contract and test double;
- delegated, least-privilege authentication;
- explicit tenant and case context;
- provenance and audit events;
- rate-limit handling;
- redacted logs; and
- revocation and case-isolation behaviour appropriate to the accepted caller.

Do not place credentials, unrestricted Graph clients, browser automation, or raw vendor response objects in agent prompts.

| Connector | Required boundary |
|---|---|
| Outlook | Planned delegated mailbox and thread search, message reading, attachment import, and draft creation. Sending is a distinct operation: the user must review and approve the exact recipients, subject, body, and attachments. Before implementation, resolve tenant ownership, mailbox types, delegated scopes, retention, audit, shared-mailbox behaviour, and the local mock contract. No real mailbox is authorised here. |
| Case systems | The imported proposal has no Pegasus caller. A future adapter may translate vendor payloads only through an accepted `Pegasus.Core` port and cannot own case contracts or make a vendor schema canonical. Live reading or write-back requires separate exact-target approval. |
| Vehicle data | Planned adapters may cover identity, history, specification, valuation, repair, salvage, MOT, and related dated sources. Every returned fact must carry provider, lookup time, effective date where available, licence class, and expiry or cache policy. Prices and values are sourced inputs to deterministic calculations, not model memory. |

## Retained delivery sequence

This sequence preserves the source workspace’s ML and desktop direction. It does not permit parallel policy, application-state, UI, domain, audit, connector, or renderer owners.

### Workstream gates

| Workstream | First durable outcome | Required gate |
|---|---|---|
| Product and desktop | Long-term composition of accepted Pegasus UI, Core, API, and renderer contracts | Accepted root allocation, caller, decision, and design mapping |
| Case platform | Consumption of versioned `Pegasus.Core` case and evidence contracts | No duplicate case schema or mutation policy |
| Agents and skills | Narrow assessment-support, correspondence, report, and QA policies | Tool allow-lists and scenario evaluations pass |
| Connectors | AI-specific adapters behind accepted Pegasus ports | Least privilege, case isolation, audit, and revocation paths are proved |
| Knowledge | Permission-aware, cited Collision Brain retrieval | Citation-correctness and leakage tests pass |
| Documents | Consumption of `workspaces/report-renderer` | Engineer review and pre-issue checks pass |
| ML operations | Approved datasets, baselines, evaluation, training, registry, and rollback | A challenger beats simpler baselines on a sealed holdout |
| Governance | AI data, evaluation, incident, and model-release controls | Named owners approve the bounded AI pilot |

### Phase 0 — Foundation and decisions

- Preserve the imported repository map and AI data boundaries.
- Keep private evaluation material under the ignored, immutable repository-root `corpus/ai-centre/` boundary.
- Do not import complete Box or Outlook archives.
- Resolve any credential-bearing evaluation source before processing it.
- Assign AI product, engineering, domain, data-protection, security, and release owners.
- For each accepted AI caller, record permitted users, data sources, prohibited outputs, and success measures.
- Reuse accepted Core, UI, and renderer contracts rather than creating source-workspace replacements.

**Gate:** repository and security controls exist; pilot purpose and data authority are recorded; no unresolved secret is present in an ingestible corpus.

### Phase 1 — Safe synthetic vertical slice

Run synthetic data through the real intended boundaries:

1. create or open a case in the Windows application;
2. import a synthetic instruction and attachments through a connector contract;
3. normalise them into the canonical case record with lineage;
4. retrieve cited, approved knowledge from Collision Brain;
5. perform deterministic identity, completeness, and arithmetic checks;
6. have agents propose a case summary, missing-evidence request, and report section;
7. show evidence, confidence, changes, and limitations in the review surface; and
8. after explicit approval, export a watermarked draft PDF without sending or signing it.

**Gate:** contract, security, accessibility, cross-case isolation, citation, and approval-flow tests pass, and engineers can explain the source of every material statement.

### Phase 2 — Operational workstation pilot

- Add delegated Outlook read, search, import, and draft creation while keeping sending separately approved.
- Add live vehicle-data connectors with caching, provenance, effective dates, and rate limits.
- Add durable local or offline behaviour, encrypted storage, session recovery, updates, diagnostics, and controlled export.
- Keep intake, assessment-copilot, report-author, correspondence, and quality-review agents as separate policies rather than one all-powerful agent.
- Integrate the deterministic report renderer and CE letterhead templates.
- Measure time saved, acceptance and override reasons, evidence gaps, and failure categories.

**Gate:** an authorised small group completes representative cases in shadow or draft mode without cross-case leakage, silent external action, or loss of auditability.

### Phase 3 — Governed knowledge and workflow scale

- Ingest only approved, licence-compatible knowledge with permissions and effective dates.
- Add case timeline, supersession, amendment, and dispute workflows.
- Add client segregation, retention and deletion propagation, operational monitoring, backup and restore, and incident rehearsal.
- Expand an agent only when a named evaluation demonstrates bounded benefit.

**Gate:** service, human-factor, privacy, quality, and economic criteria are met over a representative pilot period.

### Phase 4 — Owned model programme

- Build case-, time-, and source-aware datasets only from reviewed, authorised records.
- Establish deterministic, classical, and frontier-model baselines before fine-tuning.
- Fine-tune open-weight models only when baseline evidence justifies it.
- Begin with bounded tasks such as view or quality classification, identifier OCR and conflict detection, source-role classification, comparable ranking, and constrained style adaptation.
- Promote only portable bundles with licences, immutable hashes, model cards, sealed evaluation results, offline smoke tests, monitoring, rollback instructions, and reproducible inference contracts.

**Gate:** every owned model beats the simpler approved baseline on its intended slice and passes safety, calibration, abstention, licensing, privacy, and reproducibility gates.

## Data, model, and operational controls

Management’s recorded authorisation permits bounded development and evaluation use of approved source material. It does not authorise copying the private corpus or complete Box or Outlook archives into Git history.

Private inputs remain under the ignored, immutable repository-root `corpus/ai-centre/` boundary. Only manifests, schemas, synthetic fixtures, and generated results may be tracked. Historical reports, client text, repairer material, and third-party documents are not accepted facts without source-role, version, and evidence review.

Model weights, checkpoints, adapters, exported binaries, run output, and caches belong in the approved external artifact store and must be referenced by immutable hash. The repository may retain model-family definitions, licences, model cards, inference contracts, promotion manifests, and small configuration files.

The following remain separately controlled operations:

- connecting to or writing through a live mailbox or case system;
- sending messages, signing documents, or issuing reports;
- selecting a production model, provider, account, region, or SKU;
- deployment or billed/cloud work without an approved account, region or SKU, corpus size, cost estimate, and hard spending cap; and
- treating historical or third-party content as accepted fact without qualification and evidence review.

This workspace owns only the retained ML sequencing for its imported source scope. Core and operator authority remain controlling.