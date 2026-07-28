# Deferred-capability activation index

> **Archive status — non-authoritative planning evidence.** Revalidate against current product, roadmap, architecture, operations, design, decisions, and code before use.

Pre-conversion status: **Planned routing document.** This supersedes the former reconciliation README as the current route for deferred work. It is not a feature allocation, implementation plan, architecture decision, backlog, or evidence-status ledger. The [feature maturity map](../feature-maturity-map.md) remains the sole allocation owner; retained historical/reconciliation evidence is in [deferred-capability reconciliation](architecture/deferred-capability-reconciliation.md).

## Purpose

Deferral means preserve a non-foreclosing current boundary or name a future migration; it never means add dormant code, adapters, stores, flags, queues, accounts, resources or alternative policy owners. Every activated capability must still have one Core business-policy owner, a real caller proved separately, an explicit data/identity boundary, a failure/recovery contract and evidence appropriate to its horizon.

The current source-mapped mutating caller remains Development-gated `POST /Intake/Upload` to Core `ProcessIntake`. Worker, broader Web, MCP, provider API, Graph/Box/EVA/AI adapters and later user journeys are intended or absent callers until independently proved. Documentation, registration, Bicep and tests do not change that evidence state.

## Current activation routes

| Capability class | Current route | Activation evidence | Excluded now |
| --- | --- | --- | --- |
| `Next`/`unallocated` additional providers | [Additional-provider activation](../later-delivery/integrations/additional-provider-activation.md) | Accepted provider contract, bounded policy/reference data, caller-backed proof and approvals | Provider-specific engine/project or dormant connector |
| `Next`/`unallocated` classified mail workspace | [Email workspace and association](../later-delivery/integrations/email-workspace-and-association.md) and the [mailbox decision dossier](../mailbox-categorisation-and-email-matching/README.md) | Dossier for named automatic slices, `0.0.0-development` policy, exact Graph scope and caller evidence | Second classifier, sender or speculative Graph scope |
| `Next`/`unallocated` post-report work | [Post-report query and dispute](../later-delivery/casework/post-report-query-and-dispute.md) | Direct lifecycle decision, caller-backed operator evidence | Treating threads as case authority |
| `Later`/`unallocated` case types/communications/AI | [Diminution and Commercial](../later-delivery/casework/diminution-and-commercial.md), [communications](../later-delivery/integrations/communications-automation.md), [operator assistance](../later-delivery/ai-and-automation/operator-assistance.md) | Direct domain/channel/AI contract, exact external or model scope, evaluation and operator approval | Copied workflows, generic sender/channel/AI engine or autonomous action |
| `Later`/`unallocated` EVA and finance | [EVA replacement and engineering](../later-delivery/integrations/eva-replacement-and-engineering.md) and [accounting/invoicing](../later-delivery/integrations/accounting-and-invoicing.md) | Separate staged product/vendor/finance contracts, callers and acceptance | Assuming export equals replacement, or absorbing finance into EVA |
| Not planned / conditional activation | [Permanent and conditional boundaries](../../../product/boundaries.md) | Not planned: explicit authority change. Conditional: future direct decision before a focused activation plan. | Backlog, route, schema, adapter, account, flag or resource |

## Stable constraints retained across activation

- Principal/case/reference identity, source occurrence/provenance, custody identities, external message identities, report evidence and action history have distinct authorities; no later capability may silently merge them or reuse an allocated reference.
- Box remains the long-term file authority while the application owns workflow, relationships, processing state, permanent action history and external links. A later integration needs its own source/custody and permission contract.
- Case association, external-message identity, report evidence/delivery and lifecycle transitions remain separate facts. Later communication or EVA work must name any migration explicitly rather than pre-create universal fields.
- A future adapter calls the named Core owner; it cannot duplicate intake, classification, matching, allocation, lifecycle, permission or finance policy.
- Use the evidence labels `Planned`, `Implemented`, `Called`, `Locally verified`, `Deployed`, `Live verified`, and `Accepted` literally.

## Approval and recovery rule

Before an external, vendor, model, cloud, account, credential, data-transfer or deployment action, obtain exact-target authority plus the slice's product/architecture/security/privacy/licence/cost approval. Roll out one approved caller/scope at a time and recover by disabling only that route and reconciling durable outcomes. No route authorises case deletion, corpus transfer, public/external access, or a broad reset/cleanup.

## Historical reference

The detailed [reconciliation record](architecture/deferred-capability-reconciliation.md) is retained as historical/reference evidence. It neither allocates features nor amends the questionnaire, a decision dossier, or an ADR. Any unique finding still applicable to a future activation must be reconciled through the source-of-truth order and placed in that activation plan's canonical owner.
