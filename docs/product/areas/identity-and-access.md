# Identity and access

## Outcome

Collision Engineers staff use self-managed Pegasus accounts with Administrator,
Engineer and User roles. Administrator is the superuser role and alone manages
accounts, principals, role assignment, operational configuration and the
approved Outlook mailbox allowlist.

## Settled requirements

- Every non-public page and action requires the appropriate staff role.
- Account creation, disabling, access review and role assignment are controlled
  administrative actions.
- Principal codes, replacement history and sequence continuity follow immutable
  case-identity rules.
- Business changes, exports and material automation failures have permanent
  attributable action history; authentication/security events remain separate.
- Andrew and Alex are initial Administrator assignments held in application
  data/configuration. No person, name, email address or bypass is hard-coded
  into authorization.
- External/customer accounts and public registration remain `Not planned`.
  Staff MFA remains its existing `Not planned` capability.
- `INT-31` request-scoped upload links are not accounts. An unauthenticated link
  grants only the bounded upload action and immediate result for that request.

The stable `ACC-*` outcomes and allocations live in the
[capability inventory](../capabilities.md).

## Current state and activation

No authentication or authorization caller is implemented. Activation requires
a decision-complete change record, real Web/Worker caller mapping, negative
permission tests, bootstrap/recovery evidence and current architecture and
operations in the same pull request.
