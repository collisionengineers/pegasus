# Later-delivery activation index

> **Archive status — non-authoritative planning evidence.** Revalidate against current product, roadmap, architecture, operations, design, decisions, and code before use.

Pre-conversion status: **Planned documentation route.** This is a dependency index, not a second allocation map, backlog, or implementation-status ledger. The [feature maturity map](../feature-maturity-map.md) remains the sole owner of the 213-row allocation.

## Purpose and evidence boundary

These plans cover the `Next`/`unallocated`, `Later`/`unallocated` and `Later`/`unallocated` rows whose primary owner is not an existing `0.1.0-alpha.1` plan. Current source evidence proves only the Development-gated `POST /Intake/Upload` to Core `ProcessIntake`; every caller below is intended until independently exercised. A document, DI registration, test, Bicep resource, or adapter is not caller evidence.

## Dependency route

1. [Additional provider activation](integrations/additional-provider-activation.md) (`INT-04`, `Next`/`unallocated`) follows accepted `0.1.0-alpha.1` intake, identity, configured reference data and acceptance evidence.
2. `Next`/`unallocated` can branch into [email workspace and association](integrations/email-workspace-and-association.md) (`INT-05`–`INT-07`, `MAIL-01`–`MAIL-11`, `MAIL-13`, `MAIL-23`, `UI-10`, `UI-14`, `MCP-05`) and [post-report query/dispute](casework/post-report-query-and-dispute.md) (`CASE-23`). The named automatic mailbox slices wait for the combined mailbox decision dossier; post-report work does not otherwise wait for it.
3. `Later`/`unallocated` branches into [communications automation](integrations/communications-automation.md) (`MAIL-19`, `EXT-15`) and [Diminution/Commercial](casework/diminution-and-commercial.md) (`CASE-05`, `CASE-06`). [Operator assistance](ai-and-automation/operator-assistance.md) owns `AI-01`–`AI-04` and `AI-06` activation/evaluation only. Automatic report sending (`MAIL-17`) remains a separately gated `Later`/`unallocated` continuation in the communications plan.
4. `Later`/`unallocated` proceeds through [EVA replacement and engineering](integrations/eva-replacement-and-engineering.md) (`CASE-22`, `EXT-04`–`EXT-10`, `EXT-12`, `EXT-13`, `AI-07`), then independently through [accounting and invoicing](integrations/accounting-and-invoicing.md) (`EXT-11`).
5. [Permanent and conditional boundaries](../../../product/boundaries.md) routes Never and conditional `Later`/`unallocated` rows. It creates no delivery work.

The existing [mailbox decision dossier](../mailbox-categorisation-and-email-matching/README.md) and UI/UX route are secondary gates, not primary owners here. Each later plan must retain one Core policy owner, use existing composition roots for adapters, and acquire a fresh product/architecture decision if a proposed runtime, store, migration stream, deployment unit, or vendor boundary cannot fit the approved modular monolith.

## Common activation rule

Before a later slice becomes implementation-ready, its owner must have an accepted contract/decision, a named Core owner and intended caller, exact data/permission/vendor scope, fail-closed behavior, permanent-action-history versus telemetry design, focused and integration evidence, and the relevant security, privacy, licence, cost and operator/release approvals. Activation is reversible by disabling only the approved caller/configuration and reconciling durable outcomes; it never authorises deletion or reuse of case identity.

No plan in this index proves implementation, calling, deployment, live verification, or acceptance.
