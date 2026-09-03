---
id: PLAT-040
type: ticket
title: 'Wipe email, intake, case and document test data from the Azure estate'
status: done
area: platform-operations
order: 1720
assignee: ''
profile: chore
stageEntered:
  preparing: '2026-08-23T14:46:52.548Z'
  implementing: '2026-08-23T14:46:56.628Z'
  review: '2026-08-23T14:47:00.663Z'
  verifying: '2026-08-23T14:47:05.392Z'
  done: '2026-08-23T14:47:32.473Z'
labels:
  - operations
  - azure
  - operator-requested
  - destructive
links: []
refs:
  - docs/runbook.md
deployment: production
archived: false
created: '2026-08-23T13:06:52.408Z'
updated: '2026-09-03T09:06:51.727Z'
---

## Why

Operator, 2026-08-23, verbatim: *"fully wipe existing azure storage for emails,
blob, intake, and anything that is not configuration level data (ie anything
that has been entered into storage on the basis of an e-mail or an upload being
received). This is test data and we are wiping to get a sterile environment for
the next deployment."*

Explicit operator approval for destructive writes on the exact targets below.
Same shape as [[PLAT-017]] (2026-08-20), re-resolved against today's schema.

## Targets — Azure SQL `pegasus` on `pegasus-prod-sql-252ow37gij`

99 tables. **68 wiped, 31 preserved.** Resolved against `sys.tables` at run
time against an explicit *preserve* list, so anything added since is wiped by
default — the safe default for "sterile".

**Preserved (31)** — identity, automation clients, mailbox configuration,
principals, provider reference data, workflow configuration, the security audit
trail, schema history, and the three sequence tables:

```
__EFMigrationsHistory
AspNetRoleClaims  AspNetRoles  AspNetUserClaims  AspNetUserLogins
AspNetUserRoles   AspNetUsers  AspNetUserTokens
OpenIddictApplications  OpenIddictAuthorizations  OpenIddictScopes  OpenIddictTokens
ApprovedInboxPollStates  ApprovedMailboxes  ApprovedMailboxFolderBindings
ApprovedOutlookCategories  ApprovedSentPollStates
Organizations  OrganizationRoles  OrganizationAdministrationOperations
Principals  PrincipalSequenceLineages
ProviderDomainEvidence  ProviderDomainPackages  ProviderReferences
WorkflowConfigurations  SendToAiControl  SecurityEvents
CaseSequences  ImageIntakeSequences  UnidentifiedSequences
```

**Sequences are deliberately preserved.** A reference is never reused — that is
a product invariant, not a cleanliness preference. The next case is QDOS26013,
not QDOS26001. Sterile means no residual *data*, not recycled identifiers.

**Poll states are deliberately preserved too.** Clearing the Graph delta cursor
would make the next poll re-ingest every message still in the mailbox, which is
the opposite of sterile. The consequence is that the existing messages are
**not** reprocessed under the new code: proving [[MAIL-011]] and [[MAIL-012]]
live needs the operator to forward a fresh message.

## Targets — Blob storage

- `pegcustody252ow37gij` / `transient-intake` — **77 blobs**, retained intake
  artifacts. Wiped.
- `pegcustody252ow37gij` / `authentication-ring` — data-protection keys.
  **Preserved**; clearing it invalidates every signed-in session.
- `pegcustody252ow37gij` / `box-links` — empty, and configuration. Untouched.
- `pegtrans252ow37gij` / `app-package`, `azure-webjobs-hosts`,
  `azure-webjobs-secrets` — Functions runtime and the deployed worker package.
  **Preserved**; deleting `azure-webjobs-secrets` breaks the Function App.

## Targets — Queues

`intake-work`, `intake-work-poison`, `external-work`, `external-work-poison` on
`pegtrans252ow37gij`: **all four verified empty** before the wipe. Nothing to
delete; recorded so the inventory is complete rather than silently skipped.

## Explicitly out of scope

- **Outlook.** Not Azure, and the standing rule forbids mutating a mailbox.
  The messages stay where they are.
- **Box.** Not Azure storage. The case folders under root `405543781910` still
  hold the QDOS26009–26012 files, plus the `a.QDOS26001–4` orphans left by
  [[PLAT-017]]'s wipe. Deleting them is a separate operator decision and is
  **not** covered by this authorisation — raised with the operator, not assumed.

## How to verify

Row counts: the 68 tables at 0, the 31 preserved unchanged (notably
`CaseSequences` still at its current value); `transient-intake` blob count 0;
queues still empty; staff login still works and the live app renders empty
queues, inbox and case list.

## Outcome
