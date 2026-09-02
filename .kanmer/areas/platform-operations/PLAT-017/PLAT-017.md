---
id: PLAT-017
type: ticket
title: 'Wipe all test case, intake, image and mail data from the Azure estate'
status: done
area: platform-operations
order: 1580
assignee: claude-code
profile: chore
stageEntered:
  preparing: '2026-08-20T19:59:21.680Z'
  review: '2026-08-20T20:01:10.363Z'
  verifying: '2026-08-20T20:53:10.885Z'
  done: '2026-08-20T20:53:20.349Z'
labels:
  - operations
  - azure
  - operator-requested
links: []
refs:
  - docs/runbook.md
deployment: production
archived: false
created: '2026-08-20T19:59:01.329Z'
updated: '2026-09-01T14:44:33.165Z'
---

## Why

Operator, 2026-08-20 (feedback round 2, plan T9, verbatim): *"wipe all test e-mails stored azure-side, intake receipts, images, or any other case data… This is all test data. This is being done to keep the test environment sterile."* Explicit operator approval for the destructive writes on the exact targets in the plan.

## What

- Azure SQL `pegasus` (server `pegasus-prod-sql-252ow37gij`, rg-pegasus-prod): delete all rows from the case/intake/image/mail/unidentified data tables. **Preserved**: `__EFMigrationsHistory`, AspNet* (staff accounts/roles), ApprovedMailboxes + poll states, OpenIddict* (automation clients), Organizations/OrganizationRoles/Principals/PrincipalSequenceLineages, ProviderDomain*/ProviderReferences, WorkflowConfigurations, SecurityEvents, and **all sequence tables** (CaseSequences, ImageIntakeSequences, UnidentifiedSequences) so references are never reused (product invariant).
- Storage `pegcustody252ow37gij`: delete all blobs in `transient-intake` (retained intake artifacts). `authentication-ring` (data-protection keys) and `box-links` are configuration and stay.
- Storage `pegtrans252ow37gij` queues (intake-work/external-work + poisons): verified empty.
- Outlook mailbox and Box are untouched here (Box binding-file deletion is DOCS-005's listed release step).

## How to verify

Row counts: data tables 0, preserved tables unchanged; `transient-intake` blob count 0; app queues empty; the live app renders empty queues/inbox and staff login still works.

## Outcome
