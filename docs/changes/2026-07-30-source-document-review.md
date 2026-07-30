# Change: Resolve source-document review decisions

```yaml
id: 2026-07-30-source-document-review
type: decision
status: in_review
risk: medium
created: 2026-07-30
updated: 2026-07-30
issue: pending explicit authorisation for a GitHub write
pull_request: none
baseline: local reviewed documentation head
target_release: existing capability allocations
roadmap_horizon: Now, Next, Later
mode: development
supersedes: none
superseded_by: none
```

## Required outcome

Compare the retained pre-consolidation documentation corpus document-by-document
with the current canonical owners, carrying forward only user-confirmed material
that is compatible with current Pegasus policy. The supplied corpus remains
reference evidence, not current product authority.

## Affected canonical owners

- [Domain glossary](../../CONTEXT.md): Repairer and vehicle-enrichment terms.
- [Product requirements](../requirements.md): repairer relationships, chaser
  history, matching safeguards, inspection-address reference data, manual EVA
  package representation, mailbox workspace, and deferred AI capability policy.
- [Capability inventory](../capabilities.md): UI-10 email workspace and MAIL-11 mailbox-search allocations.
- [Design authority](../../design/README.md): queue pagination and Case activity
  summary contracts.

## Accepted direct decisions

- A Repairer is a reusable organisation with full address and contacts. Its
  many-to-many relationship with Principals does not rewrite historical Case
  party/address snapshots.
- Chaser history keeps recipient, channel, prepared draft or reference, staff
  disposition, attributable timestamps, and optional notes without claiming that
  the communication was sent or answered.
- Matching must not associate or consolidate material merely because arrivals
  are close in time. An incident-date mismatch may remove a candidate; a
  matching date still requires corroborating accepted evidence.
- The future `DATA-02` inspection-address pipeline preserves operator-confirmed
  full-address rows across refresh, uses normalized postcodes, and may rank only
  by frequency, recency, proximity, accepted Principal, Repairer, Image Source,
  and normalized search text. It never selects an address. `Image Based
  Assessment` needs an attributed staff reason.
- The manual EVA handoff download contains the generated JSON, selected images,
  and manifest, but its container is intentionally unspecified. A single archive
  is to be evaluated for usability without changing package content or the
  manual-handoff boundary.
- UI-10 includes an evaluated, read-only `View in Outlook` action for the exact
  associated message when an approved integration can target it. It is not a
  mailbox-write capability and may be omitted if the workspace makes it
  unnecessary.
- MAIL-11 includes read-only search of Deleted Items within each exact approved
  mailbox/folder scope. It does not create a backlog scan, reconstruction, bulk
  replay, Case allocation, or mailbox mutation.
- Queues require bounded accessible pagination, with page size owned by surface
  design, and each Case row exposes a read-only latest attributable
  activity/evidence summary with timestamp.

## Deferred and excluded material

- API-key provider intake and direct EVA API contracts remain unresolved; legacy
  schemas, URLs, limits, and error codes are not adopted.
- Address active-state and verification metadata remain implementation-open.
- A shared AI usage ledger is excluded until a specific AI capability is
  accepted; that capability must define its own capacity measurement evidence.
- Email-type selector treatment is deferred to the UI-10 workspace design.
- Backlog reconstruction is explicitly deferred; the read-only Deleted Items
  search allocation does not activate it.
- Historical deployment, cloud, legacy implementation, completed-ticket, and
  dated-review claims remain source evidence only unless reconciled with a
  current owner.

## Deferred-capability impact

No project, runtime, store, integration, migration stream, deployment unit, or
caller is created. DATA-02 retains reviewed-address and normalized-postcode
identity only, UI-10/MAIL-11 retain exact message and approved mailbox/folder
scope only, and AI capabilities retain typed evidence/proposal/review identity
only. Activation still requires the existing release allocation, accepted Core
boundary, real caller, visual review, and operator acceptance.

## Evidence and review state

This record captures direct user decisions and local canonical-document changes
only. It does not prove implementation, a Core caller, test coverage,
deployment, or operator acceptance. The source review remains in progress; no
external issue has been created because no explicit authorisation named a
GitHub-write target.
