---
id: PLAT-045
type: ticket
title: Fourth test-data wipe of the Azure estate after release 34
status: done
area: platform-operations
order: 2500
assignee: ''
profile: chore
stageEntered:
  preparing: '2026-08-27T09:35:59.946Z'
  implementing: '2026-08-27T09:36:23.682Z'
  review: '2026-08-27T09:36:42.103Z'
  verifying: '2026-08-27T09:37:10.210Z'
  done: '2026-08-27T09:37:14.295Z'
labels:
  - operations
  - azure
  - operator-requested
  - destructive
links:
  - PLAT-040
  - DELIV-027
refs:
  - docs/runbook.md
deployment: production
archived: false
created: '2026-08-27T08:17:14.924Z'
updated: '2026-09-01T14:44:34.075Z'
---

## Why

Operator, 2026-08-27: "Existing data on azure spawned from intakes (e-mail and
uploads) cleared for sterile test environment." Exact-target approval granted
with the plan that scoped this ticket. Same shape as [[PLAT-040]], re-resolved
against the release-34 schema.

## Targets — Azure SQL `pegasus` on `pegasus-prod-sql-252ow37gij`

Wipe = `sys.tables` minus the preserve list. Preserve list = PLAT-040's 31
tables **plus** every `ApprovedMailbox*` table added by MAIL-013 (Graph
subscription state is mailbox configuration, not intake data):

```
__EFMigrationsHistory
AspNetRoleClaims  AspNetRoles  AspNetUserClaims  AspNetUserLogins
AspNetUserRoles   AspNetUsers  AspNetUserTokens
OpenIddictApplications  OpenIddictAuthorizations  OpenIddictScopes  OpenIddictTokens
ApprovedInboxPollStates  ApprovedMailboxes  ApprovedMailboxFolderBindings
ApprovedOutlookCategories  ApprovedSentPollStates  ApprovedMailbox* (new)
Organizations  OrganizationRoles  OrganizationAdministrationOperations
Principals  PrincipalSequenceLineages
ProviderDomainEvidence  ProviderDomainPackages  ProviderReferences
WorkflowConfigurations  SendToAiControl  SecurityEvents
CaseSequences  ImageIntakeSequences  UnidentifiedSequences
```

Sequences preserved (no reference reuse). Poll cursors preserved (no
re-ingest). The split is asserted before any delete; constraints re-enabled
`WITH CHECK`.

## Targets — Blob / queues

- `pegcustody252ow37gij/transient-intake` — all blobs deleted.
- `authentication-ring`, `box-links`, all `pegtrans252ow37gij` containers — untouched.
- Queues `intake-work`, `intake-work-poison`, `external-work`, `external-work-poison` — verified empty before/after.

## Out of scope

Outlook and Box untouched.

## How to verify

Wiped tables 0 rows; preserved count unchanged; `CaseSequences` unchanged;
`transient-intake` 0; smoke passes after; sign-in works with empty Inbox,
Queues, Cases.

## Outcome
