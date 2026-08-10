# Manifest-bound intake cleanup and fresh mailbox baseline

## Scope

- Add `scripts/Invoke-ProductionIntakeCleanBaseline.ps1` as a one-off
  maintenance CLI with `ValidateAccess`, `Plan`, `Execute`, and `Verify`
  operations. Every operation requires the exact tenant, subscription,
  resource group, database, storage account/container, operator, public-client,
  mailbox, Inbox folder, and non-target mailbox identities before constructing
  a client.
- Authenticate only the allowlisted named operator through the dedicated Entra
  public client and interactive MFA. Reject reusable application credentials,
  application or managed identities, cross-tenant/wrong-operator tokens,
  mailbox send/delete scopes, readable non-target mailboxes, missing required
  SQL or storage roles, and the explicitly prohibited broader roles.
- Validate a fresh local ignored administrative role-readback artifact and its
  operator-approved SHA-256 before constructing any external client. Bind its
  complete direct/inherited role census to the exact operator object, public
  client, tenant, subscription, SQL target, and storage account. The operator
  retains live data-plane capability checks but receives no permanent Azure
  control-plane Reader grant merely to self-census.
- Keep cleanup authority hash-bound: `Plan` writes an ignored, content-safe
  manifest; `Execute` accepts only its operator-approved SHA-256, re-reads all
  row versions, Blob ETags, queue identities, reference counts, and resource
  identities before changing anything. Hold an exclusive SQL maintenance
  transaction across the recheck and destructive phase, acquire a finite
  renewable lease on every exact Blob before the first delete, and prepare only
  quiescent exact target queues. Attempt every outstanding lease release with
  an independent bounded token so a cancelled caller or one release fault
  cannot orphan an indefinite lock; `Verify` independently proves the manifest
  outcome and retained-record invariants.
- Derive the SQL dependency graph from SQL Server foreign-key metadata rather
  than a maintained table subset. Stop the whole operation on an unenumerated
  dependent, Case/PO or Triage link, custody/Box identity, non-target channel,
  shared content-addressed Blob, unknown queue message, or any drift.
- Delete only exact manifest-listed rows, queue messages, and exclusively owned
  `transient-intake` Blobs. Establish a fresh Inbox delta cursor using only
  read requests and update only the exact approved Inbox poll-state row. Never
  move, mark, categorise, send, or delete Outlook content.
- Add focused local fixtures/tests that exercise LocalDB, Azurite, and a local
  Graph fake, including all required authentication, authorization, linked-row,
  shared-Blob, queue, drift, hash, exact-deletion, cursor, Outlook-immutability,
  and idempotency cases.
- Add the complete decision-ready clean-baseline procedure to `docs/runbook.md`,
  including onboarding/readback/revocation, access validation, separate plan
  and execution approvals, exact commands, ordering, stop conditions, recovery
  limits, and content-safe evidence handling.

## Safety and compatibility

- This task performs no live login, grant, deployment, cleanup, or external
  service call. Production grants and execution remain a separate exact-target
  operation.
- The script accepts no storage key, connection string, client secret,
  certificate, application token, or broad deletion selector. Local tests use
  explicit disposable fixture switches that are rejected for production
  identities.
- Case/PO, Triage, Principal, organisation, user, security, custody, Box,
  Sent-mail, and Outlook content are retained boundaries, never cleanup
  targets.
- Preserve concurrent work. Before PR preparation, merge fresh `origin/dev`,
  take its `NOW.md` on conflict, accommodate non-overlapping runbook changes,
  and remove only this task's claim line.

## Verification

1. Run the script contract and focused integration tests against disposable
   LocalDB, Azurite, and the local Graph fake, including every stop condition
   named by the ticket.
2. Run canonical locked restore, Release build, focused tests, and the full
   non-corpus test profile required by `docs/runbook.md`.
3. Run documentation-link validation, `git diff --check`, staged sensitive-path
   and secret scans, and a literal parameter/approval/stop-condition comparison
   between the ticket, runbook, script, and tests.
4. Obtain an independent destructive-scope review against this plan before any
   merge. A green local suite proves implementation consistency only; no live
   permission, production cleanup, Outlook immutability, or operator acceptance
   claim is made by Ticket 2.
