# Identity and access

## Outcome

Collision Engineers staff use self-managed application accounts with the
Administrator, Engineer, and User roles needed for authorized case work.
Administrators alone manage accounts, principals, role assignment, operational
configuration, and the approved Outlook mailbox allowlist.

## Settled requirements

- Every non-public page and action requires the appropriate staff role.
- Account creation, disabling, access review, and role assignment are
  controlled administrative actions.
- Principal codes, replacement history, and sequence continuity follow the
  immutable case-identity rules.
- Business changes, exports, and material automation failures have permanent
  attributable action history; authentication/security events remain separate.
- External/customer accounts, public registration, and staff MFA are not
  planned capabilities, not backlog omissions.

The stable `ACC-*` outcomes and allocations live in the [capability
inventory](../capabilities.md). Operator authority and the settled questionnaire
remain higher authority than this route.

## Current state and activation

No authentication or authorization caller is implemented. Activation requires
a decision-complete change record, real Web/Worker caller mapping, negative
permission tests, bootstrap/recovery evidence, and updated architecture and
operations in the same pull request.

The pre-onboarding [identity plan](../../history/plans/remainder-delivery/identity-and-access/staff-identity-authorisation-and-action-history.md)
is retained as historical planning evidence only; it cannot activate work.
