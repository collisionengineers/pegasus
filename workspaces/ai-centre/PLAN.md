# Collision AI Workstation Delivery Plan

- **Updated:** 21 July 2026
- **Status:** Repository foundation; only Collision Brain is implemented
- **Product:** Windows desktop workstation for Collision Engineers

> **Imported source plan — non-authoritative and non-caller.** This document preserves the
> source workspace's ML sequencing only. Pegasus application behavior is owned by
> `docs/product/`, case policy by `Pegasus.Core`, UI by `design/`, and document rendering by
> `workspaces/report-renderer`. The desktop/case/report workstreams below are historical
> proposals, not permission to build parallel owners.

## Outcome

Deliver a case-centred workstation in which an engineer can receive and organise instructions,
review vehicle and damage evidence, use specialist agents, retrieve cited knowledge, draft and
validate correspondence and reports, and explicitly approve every external or professional action.

The first production slice is deliberately narrower than “an agent that does everything”. It must
prove one safe end-to-end case workflow with traceable evidence and measurable engineer benefit.

## Workstreams

| Workstream | First durable outcome | Gate |
|---|---|---|
| Product and desktop | Consume the accepted Pegasus UI and Core contracts; no parallel workstation | Accepted Pegasus caller and design mapping |
| Case platform | Consume versioned `Pegasus.Core` case/evidence contracts | No duplicate case schema or mutation policy |
| Agents and skills | Narrow agents for assessment support, correspondence, reports, and QA | Tool allow-lists and scenario evaluations pass |
| Connectors | AI-specific adapters behind accepted Pegasus ports | Least privilege, case isolation, audit, and revoke paths proven |
| Knowledge | Collision Brain integrated with permission-aware, cited retrieval | Citation correctness and leakage tests pass |
| Documents | Consume `workspaces/report-renderer` | Engineer review and pre-issue checks pass |
| ML operations | Approved datasets, baselines, evaluation, training, registry, and rollback | A challenger beats simpler baselines on a sealed holdout |
| Governance | AI-workspace data, evaluation, incident, and model-release controls | Named owners approve the bounded AI pilot |

## Phase 0 — Foundation and decisions

- Preserve this imported repository map and its AI data boundaries.
- Keep private evaluation material outside the repository under the ignored, immutable `corpus/`
  boundary; do not import Box or Outlook archives.
- Resolve any credential-bearing evaluation source before processing it.
- Assign AI product, engineering, domain, data-protection, security, and release owners.
- Record permitted users, data sources, prohibited outputs, and success measures for each accepted
  AI caller.
- Reuse accepted Pegasus Core, UI, and renderer contracts rather than creating source-workspace
  replacements.

**Exit gate:** repository and security controls are in place; the pilot purpose and data authority
are recorded; no unresolved secret is in an ingestible corpus.

## Phase 1 — Safe vertical slice

Build a synthetic-data path through the real architecture:

1. create or open a case in the Windows app;
2. import a synthetic instruction and attachments through a connector contract;
3. normalise them into the canonical case record with lineage;
4. retrieve cited, approved knowledge from Collision Brain;
5. run deterministic identity, completeness, and arithmetic checks;
6. let agents propose a case summary, missing-evidence request, and report section;
7. show evidence, confidence, changes, and limitations in the review surface; and
8. export a watermarked draft PDF after explicit approval, without sending or signing it.

**Exit gate:** contract, security, accessibility, cross-case isolation, citation, and approval-flow
tests pass; engineers can explain where every material statement came from.

## Phase 2 — Operational workstation pilot

- Add delegated Outlook read/search/import and draft creation; keep sending separately approved.
- Add live vehicle-data connectors with caching, provenance, effective dates, and rate limits.
- Add durable local/offline behaviour, encrypted storage, session recovery, updates, diagnostics,
  and controlled export.
- Implement the intake, assessment-copilot, report-author, correspondence, and quality-review agents
  as separate policies rather than one all-powerful agent.
- Integrate the deterministic report renderer and CE letterhead templates.
- Instrument time saved, acceptance/override reasons, evidence gaps, and failure categories.

**Exit gate:** an authorised small group completes representative cases in shadow/draft mode with no
cross-case leakage, silent external action, or loss of auditability.

## Phase 3 — Governed knowledge and workflow scale

- Ingest only approved, licence-compatible knowledge with permissions and effective dates.
- Add case timeline, supersession, amendment, and dispute workflows.
- Add client segregation, retention/deletion propagation, operational monitoring, backup/restore,
  and incident rehearsal.
- Expand agents only when a named evaluation demonstrates a bounded benefit.

**Exit gate:** service, human-factor, privacy, quality, and economic criteria are met over a
representative pilot period.

## Phase 4 — Owned model programme

- Build case/time/source-aware datasets from reviewed, authorised records.
- Establish deterministic, classical, and frontier-model baselines first.
- Fine-tune open-weight models only where the baseline evidence justifies it.
- Start with narrow tasks such as view/quality classification, identifier OCR/conflict detection,
  source-role classification, comparable ranking, and constrained style adaptation.
- Promote portable bundles with licences, hashes, model cards, sealed evaluation results, offline
  smoke tests, monitoring, and rollback instructions.

**Exit gate:** each owned model beats the simpler approved baseline on its intended slice and passes
safety, calibration, abstention, licensing, privacy, and reproducibility gates.

## Authorised data scope

- the complete current source corpus;
- the complete Collision Engineers Box archive;
- the complete Collision Engineers Outlook archive; and
- repository inclusion, ingestion, transformation, retrieval, dataset construction, training,
  fine-tuning, and evaluation using those sources.

See [the recorded authorisation](docs/governance/data-authorisation.md).

## Separately controlled operations

- connecting to or writing through a live mailbox or case system;
- sending messages or issuing reports;
- deployment or billed/cloud work without an approved account, region/SKU, corpus size, cost estimate,
  and hard spending cap;
- selecting a production model/provider, account, region, or SKU; or
- treating historical reports, client text, repairer material, or third-party documents as accepted
  facts without source-role, version, and evidence review.

This plan owns ML sequencing for the imported source workspace.
