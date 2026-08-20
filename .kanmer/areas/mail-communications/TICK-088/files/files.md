# Files — MAIL-12

## Inspected ref

`origin/dev` at `a3c88a7bbdb43cf4cbd9303022397f6e028d7bf9`. Refresh TICK-054-owned symbol names after its merge; current `origin/dev` has no Graph write adapter or outbound-mail persistence.

## Change surface

| Path | Purpose / reuse |
|---|---|
| `src/Pegasus.Core/Mail/OutboundMail.cs` (new focused file) | One Core owner for Compose/Reply/Forward draft, update, exact confirmation fingerprint, send/idempotency and evidence state. Closed kinds/states only; no generic mail-action framework. |
| `src/Pegasus.Core/Identity/ApprovedMailboxAdministration.cs` | Add an explicit outbound-send route scope and versioned mailbox correspondence-signature configuration. Reuse `ManageApprovedMailboxes`; never infer write authority from `SentEvidence`. |
| TICK-054's landed exact-message action/operation contracts beside `src/Pegasus.Core/Intake/RetainedMail.cs` | Reuse internal retained-message resolution, actor/authorization, operation fingerprint, replay/conflict and Unknown-result conventions. Reply/forward source identities stay server-owned. |
| `src/Pegasus.Core/Workflow/PollSentEvidence.cs` | Reuse immutable Sent provenance and add only outbound-operation reconciliation by immutable draft/Sent id before leaving existing Triage/report/unmatched outcomes unchanged. |
| `src/Pegasus.Core/Workflow/ApprovedMailboxReportSentEvidence.cs` | Context only. Do not widen or reuse this report-specific contract as general outbound evidence. |
| `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` | Reuse token acquisition, Graph HTTPS confinement, URI escaping, immutable-id header, safe errors and exact mailbox/folder conventions. Keep GET readers intact. |
| `src/Pegasus.Infrastructure/Email/GraphOutboundMailAdapter.cs` (new) | Focused create/createReply/createForward, patch draft, attachment/upload-session, send and immutable-id probe adapter. One writer, not methods scattered through Web. |
| `src/Pegasus.Infrastructure/Persistence/AdministrationPolicyEntities.cs`, `AdministrationPolicyModelConfiguration.cs`, `EfApprovedMailboxStore.cs` | Persist outbound scope and mailbox signature content/hash/version with the existing approved-estate optimistic concurrency and history. |
| `src/Pegasus.Infrastructure/Persistence/OutboundMailEntities.cs`, `EfOutboundMailStore.cs` (new) | Purpose-built durable drafts and external operations/evidence: exact content/recipients/attachment references, confirmation fingerprint, provider immutable id, state, actor and timestamps. Do not store message bodies in `ActionHistory`. |
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`, one new migration, `Migrations/PegasusDbContextModelSnapshot.cs` | Map the mailbox-setting additions and outbound draft/operation/evidence records, unique fingerprint/idempotency constraints and exact Web/Worker grants. |
| `src/Pegasus.Infrastructure/Persistence/EfSentEvidencePollStore.cs` and/or the landed outbound-store query seam | Reconcile the existing Sent poll with a pending outbound operation by exact immutable id; no subject/recipient fuzzy match. |
| `src/Pegasus.Infrastructure/DependencyInjection.cs`, `src/Pegasus.Web/Program.cs` | Compose Core/store/Graph ports in the existing host. Do not enable production credentials or make cloud writes as a code-side effect. |
| `src/Pegasus.Web/Pages/Administration/Mailboxes.cshtml(.cs)` | Existing administrator-only setting for outbound enablement and versioned configured signature; never accept Graph identities from the browser. |
| `src/Pegasus.Web/Pages/Mail/Compose.cshtml(.cs)` (new) | Thin draft/edit/confirmation/result surface for all three kinds. Server derives mailbox and exact source; the final summary binds the sent version. |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml(.cs)` | Add Reply/Forward entry links from the exact retained message only. Do not duplicate send validation or provider calls. |
| `tests/Pegasus.Core.Tests/Mail/OutboundMailTests.cs` (new) | Compose/reply/forward validation, recipient sets, signature/source/version requirements, fingerprint, replay/conflict, authorization and failed/unknown retry rules. |
| `tests/Pegasus.Core.Tests/Workflow/PollSentEvidenceTests.cs` | Exact immutable-id reconciliation while preserving Triage/report/unmatched behavior and evidence limits. |
| `tests/Pegasus.IntegrationTests/ProductionGraphSourceTests.cs` | Existing fake-HTTP convention; prove exact paths/headers/bodies, immutable IDs, reply/forward drafts, patch, small/large attachments, 202 mapping, probe/reconciliation and outside-mailbox refusal. |
| `tests/Pegasus.IntegrationTests/ApprovedMailboxEstateIntegrationTests.cs`, `ApprovedMailboxAdministrationWebTests.cs` | Outbound scope/signature persistence, version/history, administrator caller and fail-closed missing config. |
| `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` | Exact reply/forward source resolution and no mutation of retained evidence. |
| `tests/Pegasus.IntegrationTests/OutboundMailPersistenceTests.cs` (new) | Draft/operation durability, concurrency, identical replay, conflicting key, pending/failed/unknown/Sent reconciliation and safe history. |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Real authenticated compose/detail callers, confirmation version, recipient/body/attachment/signature summary, anti-forgery, visible failures and no duplicate send. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Canonicalize general authenticated draft/send behavior, separate outbound scope, evidence limits and fail-closed behavior. |
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | Preserve the explicit MAIL-17/CASE-23 boundary; change only if a clarifying cross-link is needed. |
| `docs/capabilities.md` | Record evidence/status only after delivery; its MAIL-12 row already points to FRD-11's non-overlap rule. |
| `docs/current-architecture.md`, `docs/operations.md` | Refresh only after a deployment/live permission or runtime-state change, never from local adapter tests. |

## Exact overlaps and serialization

- **TICK-054 / MAIL-13 — hard predecessor.** Exact overlap: TICK-054's Core exact-message/external-operation contracts; `RetainedMail.cs`; `GraphApprovedSources.cs`; mailbox operation/history entities, configuration, store, `PegasusDbContext.cs`, migration/snapshot; `DependencyInjection.cs`; `Program.cs`; `Message.cshtml(.cs)`; `ProductionGraphSourceTests.cs`; `RetainedMailPersistenceTests.cs`; `MailWorkspaceWebTests.cs`; FRD-08; capabilities. Land/rebase TICK-054 first and reuse its actual names. MAIL-12 adds separate draft/send state rather than widening MAIL-13's closed state-action vocabulary.
- **TICK-049 / MAIL-07 — transitive predecessor through TICK-054.** Same Graph host/immutable-id/error, operation recovery, reasoned confirmation and current-location seams. Do not add a third external-operation convention.
- **TICK-053 / MAIL-11.** Exact overlap in `RetainedMail.cs`, `EfRetainedMailboxMessageStore.cs`, `Message.cshtml(.cs)`, retained source/thread identity, `RetainedMailPersistenceTests.cs` and `MailWorkspaceWebTests.cs`. Stabilize read/detail shapes before reply/forward.
- **TICK-056 / UI-10.** Downstream overlap in Message/Compose presentation and `MailWorkspaceWebTests.cs`; UI consumes MAIL-12 Core results and owns no send policy.
- **AUTO-003.** Downstream Automation caller only; it reuses the Core commands and cannot call Graph/EF directly.
- **TICK-075 / MAIL-17.** Potentially shares the outbound Graph adapter and general immutable Sent-evidence facts only. It must keep its own targeted report transaction, artifact/version, principal destinations, Box filing, completion and CASE-23 effects. Serialize later if it edits the adapter; do not generalize MAIL-12 around its future scope.
- **TICK-066 / MAIL-19.** Potentially shares the final low-level outbound adapter/evidence only. Its Worker schedule/template/eligibility/retry policy remains a separate later Core use case; no current file overlap justifies adding it now.

## Context and verified conventions

| Path | What it establishes |
|---|---|
| `src/Pegasus.Core/Identity/StaffAuthorization.cs` | Existing administrator mailbox-setting right and ordinary staff/Automation casework authorization. |
| `src/Pegasus.Infrastructure/Persistence/EfCaseReportSentEvidenceStore.cs`, `DocumentActionHistory.cs` | Stable request fingerprint/replay/history convention; report semantics remain isolated. |
| `docs/design/README.md` | Confirmation/accessibility convention and explicit current deferral of authenticated send. |
| `docs/runbook.md#live-operation-approval-matrix`, `docs/operations.md`, `docs/current-architecture.md` | Current read-only Graph authority and separate permission/RBAC/write approvals. |
| EPIC-006 `context.md` | One Core implementation, no local-alpha mailbox mutation, report/chaser separation. |

## Out of scope

No report/fee-note dispatch or post-report transition, Box filing, scheduled/automatic chaser, background retry, free-form sender mailbox, arbitrary Graph identity, generic mail-command framework, reuse of report-evidence storage for all sends, subject-based Sent matching, delivery/read proof claim, operational correspondence, production permission/cloud/deployment write, or live Outlook write without exact just-in-time approval.
