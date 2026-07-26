# Email workspace and association

Primary plan: `P-V2MAIL`

Features: `INT-05`; `INT-06`; `INT-07`; `MAIL-01`; `MAIL-02`; `MAIL-03`; `MAIL-04`; `MAIL-05`; `MAIL-06`; `MAIL-07`; `MAIL-08`; `MAIL-09`; `MAIL-10`; `MAIL-11`; `MAIL-13`; `MAIL-23`; `UI-10`; `UI-14`; `MCP-05` — V2 beta.
Status: **Planned.** The combined mailbox categorisation and automatic-matching dossier is an open gate for only its named categorisation/automatic-association slices.

## Boundary, dependencies and intended callers

One Core classification/association policy consumes the accepted V0 classification foundation and decision dossier. Worker/Graph ingestion, Web workspace and staff MCP are separate intended callers/adapters that call named Core use cases; none may host a second classifier, queue policy, case lifecycle, or sender. Dependencies include V0 classification evidence, V1 source identity/custody, identity/action history, Outlook/background processing, lifecycle, and the approved V2 UI route.

## Slices

### Ingest all four mailboxes

Features: `INT-05`; `INT-06`; `INT-07` — V2 beta.

The Worker/Graph adapter is intended to ingest `desk@`, `engineers@`, and `info@` only after exact mailbox, folder, RBAC, cursor/replay, retention and failure-scope approval. It must preserve mailbox/thread/message identity, reject unapproved scope before client calls, and make transient/terminal/unknown outcomes visible. This does not create classification or case association by itself.

### Classify and explain mail

Features: `MAIL-01`; `MAIL-02`; `MAIL-03`; `MAIL-04` — V2 beta.

Core owns identity, taxonomy mapping, explainable evidence, policy version and correction history. The dossier must first settle predicates, precedence, ambiguity, governance, cohort, rollout/rollback and Graph scope. Until then, ambiguous/unsupported material remains reviewable with no automatic case action, move or association. Evaluator/holdout, then intended Worker evidence, prove only the accepted policy/caller boundary.

### Recommend, confirm and move Outlook items

Features: `MAIL-05`; `MAIL-06`; `MAIL-07` — V2 beta.

Core produces an approved recommendation; the authenticated staff Web caller confirms it, and the narrow Outlook adapter performs the exact allowed move. Refuse stale, unauthorised, unknown-folder or ambiguous recommendations with zero external call. A recommended action is not an automatic send or case transition.

### Suggest next actions

Feature: `MAIL-08` — V2 beta. The lifecycle Core owner supplies suggestions through the approved workspace; suggestions cannot automatically transition a case or send a message.

### Associate email and cases

Features: `MAIL-09`; `MAIL-10` — V2 beta.

Core owns case association, manual link/unlink/relink and correction history. Automatic association waits for the dossier; manual association may proceed only under the lifecycle/custody contract. Uncertain association creates no case/reference and no hidden lifecycle transition. External identity, source occurrence and case association remain distinct.

### Deliver the email workspace

Features: `MAIL-11`; `MAIL-13`; `UI-10`; `UI-14` — V2 beta.

The Web workspace may browse/search/view and make approved read-state/category/flag/delete changes; UI queues/folders are operational views, not a second taxonomy or Triage workflow. The UI route must specify roles, error/recovery and accessibility before implementation.

### Map taxonomy to operational queues and folders

Feature: `MAIL-23` — V2 beta. The accepted mailbox policy maps the taxonomy; a UI view does not redefine that policy or Triage.

### Expose classified email actions through existing staff MCP

Feature: `MCP-05` — V2 beta. The MCP adapter exposes only the same named Core use cases and authorisation/action history.

## Proof, rollout and deferral

For every slice, prove Core negatives, adapter scope/idempotency and caller-visible recovery before a limited exact-mailbox shared-development smoke. Record permanent action history separately from telemetry and never log message/case content or secrets. Roll out one mailbox/action at a time; recover by disabling that caller/action, retaining durable receipts/outcomes and reconciling rather than deleting messages or associations. Required approvals are the dossier where named, mailbox/Graph scope, UI/MCP authorisation, data/privacy/security/cost, and operator/release acceptance.

Excluded now: a generic rule engine, account-wide search, unsupported mailbox/folder access, an in-app sender (`MAIL-12` is Never), automated case creation from ambiguity, dormant Graph resources, or a separate email lifecycle. Stable identities are mailbox, thread, message, source occurrence, policy version, correction and case-association identity. Future activation needs the accepted dossier, caller-backed evidence, exact external scope and operator acceptance.
