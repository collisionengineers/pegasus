# Pegasus — product requirements
> Product intent (what & why). Business truth: docs/operator-notes.md · Behaviour: docs/frd/ · Schedule/IDs: docs/capabilities.md

## Purpose, users, and outcomes

Pegasus is Collision Engineers’ clean-room case-management and reporting application. It must replace fragmented intake, case tracking, document custody, correspondence, engineering workflow, and reporting with one auditable system while preserving operator authority and human approval.

Primary users are authorised Collision Engineers staff. The alpha is an Operations-first staff service focused on a QDOS intake route; that focused caller is the first exercised slice, not the limit of the intended mailbox, provider, casework, or reporting model.

Required outcomes:

The 6 September 2026 v1 scope completes engineering and final reports in
Pegasus; EVA is optional. Staff initiate every report/chaser send. Per-Engineer
Glass's repair estimates are included, while its valuation service and the
additional spreadsheet-driven workflow automation remain deferred. The later
[operator decisions](../operator-notes.md#current-v1-decisions--6-september-2026)
govern conflicting earlier alpha descriptions.

- make receiving work, incomplete intake, Triage, active cases, due work, queries, and completed work visible without reconstructing state from multiple systems;
- retain source identity, chronology, custody, decisions, corrections, and action history;
- fail closed before source receipt or reference allocation when safe persistence, identity-critical route facts, limits, or processing are incomplete or ambiguous; once safe processing establishes Principal and Case type, allocate the Case/PO and retain incomplete ordinary detail, images, or checks as `Not ready`; missing or ambiguous standalone Audit evidence withholds only the later Audit reference;
- keep business decisions in `Pegasus.Core`, with infrastructure, UI, Worker, MCP, imported workspaces, skills, prompts, and models subordinate to Core policy and human approval;
- support deterministic, repeatable local verification and separately authorised live verification;
- preserve deferred capability seams and data identities without building dormant capability.

## Product invariants

### Terminology and outcomes

`Audit`, `Triage`, `Unidentified`, `Image Intake`, and `Blocked intake` have distinct meanings. `Triage` is the only current term for the operator workflow described below.

- `Audit` is standalone reviewed work with its own evidence and acceptance boundary; it is not a synonym for Triage or generic sorting.
- `Triage` is a staff workflow for a recorded matter requiring a finding and, where applicable, exact reply-chain Sent evidence.
- `Unidentified` is the receiving/intake outcome when evidence can be persisted safely but its identity, meaning, ownership, or destination cannot yet be established. Each item or inseparable group receives an immutable `U<n>` tracking reference and a required canonical reason; that reference is never a Case/PO, Audit, Image Intake, or principal identity.
- `Image Intake` is the image-initiated pre-instruction outcome when a usable VRM exists but no unique formal instruction Case can be matched; it retains its VRM reference and is not Unidentified.
- `Blocked intake` is a pre-case failure boundary where required processing, identity, limits, custody, or evidence is incomplete or unsafe.

## Quality, capacity, security, and evidence

Pegasus is designed for the observed office workload of roughly 1,000–1,200 matters per month and a 2,000-per-month capacity target. These are observed workload and design capacity, not throughput proof.

Required qualities:

- deterministic, bounded, cancellable processing;
- truthful near-real-time intake: ordinary QDOS email and manual-upload receipts progress through identification, classification, extraction, case creation, and best-effort Box confirmation within a five-second p95 from Pegasus durable receipt; provider delay is separately attributed, while larger or retrying work remains visibly Processing rather than showing stale or inferred identity;
- durable intake wake-up and recovery without making aggressive polling the normal scheduler, with normalized idle Function cost no greater than GBP 0.50 per day over a seven-day observation;
- least privilege and fail-closed authorization;
- encrypted transport and protected storage appropriate to the data boundary;
- resolved and recorded retention rules for personal data and vehicle images before activating each external flow; this does not create an automated retention workflow;
- confirmation of applicable processor terms before activating any external email, upload, AI, Box, or other external processing;
- no secrets in source, logs, proof artifacts, URLs, or client-rendered configuration;
- immutable source and action provenance;
- structured diagnostics without source-content leakage;
- a 15-minute database recovery-point objective and four-hour restoration objective, proved through the operator-run [production recovery procedure](../runbook.md#production-recovery) (OPS-09 — deferred; gates no release);
- reasoned recovery, restore, and replay proof without duplicate case/reference allocation;
- local development on a supported platform, with package-pinned Playwright
  Chromium automation as the release accessibility evidence for authenticated
  routes, keyboard and focus behavior, responsive reflow, forced colours,
  reduced motion, semantics, and axe rules; this evidence does not claim
  screen-reader interoperability, complete WCAG conformance, subjective
  usability, or operator acceptance;
- independently buildable source workspaces with no application reference, dynamic load, dependency hoist, or deployment inclusion;
- explicit test/evidence scope and limits rather than evergreen counts.

## Permanent boundaries

The `Not planned` capability rows are boundaries, not backlog. They receive no activation issue or release target. They include permanently excluded or intentionally unsupported behaviors identified in the capability inventory. In particular:

- no case deletion or reference reuse;
- no silent principal/reference mutation;
- no dormant provider, OCR, AI, external-system, migration, or automation scaffolding;
- no workspace as a Pegasus runtime, deployment unit, or business-policy owner;
- no synthetic historical-case reconstruction;
- no local-alpha Outlook or Box mutation outside an exact separately approved target and operation;
- no model, skill, prompt, or external source issuing an accepted case, engineering, economic, legal, or report outcome;
- no broad production-data import from raw reference workbooks or evidence.

## Acceptance model

A feature is accepted only when its owning requirement and capability are linked to:

- one Core policy/use-case owner;
- the actual Web, Worker, API, or MCP caller;
- infrastructure/persistence behavior where applicable;
- observable success, boundary, authorization, conflict, and recovery tests;
- current docs/design/operations documentation;
- exact-head review;
- separately authorised live proof and operator/management acceptance where the feature depends on an external system or deployment.

Allocation, a file, registration, a green structural check, a source pull request, deployment, and operator acceptance are separate evidence states.

Evidence states remain distinct:

1. allocated to `Now` or a version;
2. implemented in source;
3. exercised through the real caller;
4. deployed;
5. live-verified;
6. accepted by an authorised operator or management.

A lower state never implies a higher one.

### Image-initiated Case origin

Pegasus has two Case-origin records. Instruction-initiated Cases are the main
formal type and may initially have no images; they alone receive Principal and
Case/PO identity. Image-initiated Cases begin with vehicle images, receive a
VRM-sequenced Image Intake Reference when the registration is usable, and have
no Case/PO. They remain searchable until merged into a matching formal Case or
staff-closed with a reason. Both origins and their history remain attributable.

A usable registration on received images settles into exactly one of two
outcomes (operator ruling, 2026-08-19): a registration that matches an
existing Case attaches the images to that Case as evidence; a registration
that matches no existing Case creates an Image-initiated Case under its own
reference. Neither outcome is a third case-origin record — an Image-initiated
Case that later matches a Case still merges into it, per the paragraph above.
