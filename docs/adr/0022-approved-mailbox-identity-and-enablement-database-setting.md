---
id: ADR-0022
status: accepted
date: 2026-08-06
supersedes: []
superseded_by: [ADR-0024]
related_capabilities: []
related_frd: [frd-08]
tags: [mailbox, config]
---
# ADR-0022: Approved-mailbox identity and enablement as an administrator-editable database setting

- Date: 2026-08-06
- Status: accepted
- Owners: Collision Engineers product owner and Pegasus development team
- Relation: a second scoped exception to ADR-0008's code-owned-configuration
  consequence, and the intake-side counterpart to ADR-0018; ADR-0008's
  route-selection predicates remain code-owned and unaffected

## Context

The approved-mailbox administration surface shipped with migration
`20260729180000_AdministrationPolicies`: an administrator can already add an
address, choose its read-only route scopes, and set it `Approved` or
`Disabled`, all versioned and recorded in `ActionHistory`. It drove nothing.
Inbound-intake polling read its mailbox from deployment configuration —
`Graph:MailboxId` and `Graph:InboxFolderId` — so the allowlist decided whether
a mailbox was *permitted*, while an app setting decided which single mailbox
was actually *read*. An administrator could neither add a second mailbox nor
stop the configured one being polled; disabling the only approved row made the
poll throw rather than stop.

Two ways of closing it were rejected:

- Seeding the deployed mailbox's Graph identities in a migration. The real
  identities are not in this repository: `infra/main.parameters.json` reads
  them from azd environment variables. Inventing them, or committing them,
  would be fabricated configuration.
- Letting the Worker write the identities it discovers. Migration
  `20260729199000_RuntimeRoleReconciliation` grants `pegasus_worker_runtime`
  only `SELECT` on `ApprovedMailboxes`; Web holds `SELECT`, `INSERT` and
  `UPDATE`. The Worker cannot bootstrap a row, and widening that grant to make
  self-healing possible would put estate authorship in the unattended runtime.

## Decision

1. `ApprovedMailbox` carries the exact tenant identity a poll needs: a mailbox
   identity, an Inbox folder identity, and a Sent folder identity, each
   nullable. Three nullable columns on the existing table; no new store and no
   new migration stream.
2. A row saved `Approved` must carry its mailbox identity, plus the Inbox
   folder identity if it is scoped to inbound Intake and the Sent folder
   identity if it is scoped to Sent evidence. Core fails closed on the rest.
   An administrator awaiting the tenant grant saves the row `Disabled` with the
   identities blank and fills them in later.
3. An identity is immutable once saved, and the address is immutable once a
   mailbox identity is bound. The mailbox identity is the primary key of that
   mailbox's `ApprovedInboxPollStates` cursor row, so rebinding would orphan or
   alias a cursor. Moving a mailbox is disable-and-add, never edit. A mailbox
   identity is unique across rows, enforced by a unique index filtered on
   `IS NOT NULL` so many rows may await their identities at once.
4. The Graph client and Inbox source stop closing over one mailbox. Every
   mailbox and folder identity is passed per call, taken from the lease. The
   exact-folder guarantee is unchanged — the same canonical OData path shapes,
   the same host and scheme checks, the same per-item parent-folder check — but
   it is now enforced against the lease's folder rather than a configured one.
5. Deployment configuration is retained, not retired, and becomes a read-only
   fallback. A row with saved identities is used as saved. A row with none
   whose address matches the configured mailbox is polled under exactly the
   identities the deployment already uses, logged once by address and never by
   identity value. A row with none and no match is skipped and reported. The
   database always wins; configuration never overrides a saved identity. The
   fallback is composed only into a polling host, so Web never borrows a
   mailbox identity from configuration.
6. Failures and in-flight work are bounded per mailbox. Across a multi-mailbox
   tick, a single failure is rethrown with its original type and two or more
   raise an `AggregateException`. An in-flight lease is not revoked when a
   mailbox is disabled: a poll already inside a page finishes that page
   normally, so disabling is eventually effective within one poll page, never
   mid-message.

Functional behaviour: see [FRD-08](../frd/frd-08-email-mailbox-and-background-processing.md).

## Consequences

- Three nullable columns and one filtered unique index on an existing table.
  No new top-level directory, project, store, migration stream, or deployment
  unit; no new grants, because the existing table-level grants cover new
  columns.
- Per-tick Graph cost grows linearly with the estate: the message bound applies
  per mailbox, so an estate of *n* mailboxes may read *n* × 50 messages a
  minute rather than 50.
- A mailbox identity may not be rebound, so a mailbox move is disable-and-add.
  The old row keeps its cursor and its retained material.
- Saving a real identity over the configuration fallback is the one permitted
  change of a poll state's key, and it must carry the cursor with it or the
  first save would re-enumerate the folder. One address has one poll state, so
  the claim adopts the existing row by address and re-keys it rather than
  inserting a second one, which the unique index on the address would refuse
  anyway. Both tables referencing that key — quarantined messages and retained
  messages — therefore cascade on update
  (`20260806090000_ApprovedInboxPollStateIdentityAdoption`). Deletes stay
  restricted, and because the engine performs the cascade the Worker needs no
  wider grant on either table.
- The Worker keeps `SELECT`-only on `ApprovedMailboxes` and can never
  self-heal a missing identity. That is deliberate, and it is why the
  configuration fallback is read-only.
- Identity values appear in `ActionHistory` before/after snapshots. They are
  exact tenant identifiers, not credentials, and the versioned record of who
  bound which mailbox is the intended outcome. They are shown in full only on
  `/Administration/Mailboxes`, which already requires Administrator and
  `ManageApprovedMailboxes`, and are never written to a log.
- `Graph:MailboxId` and `Graph:InboxFolderId` become vestigial for the Inbox
  route once identities are saved, but are retained for the Sent route and for
  the bootstrap fallback, so `infra/` is unchanged by this decision.
- `IApprovedSentSourceSettings` no longer inherits the Inbox settings, because
  the two routes no longer answer the same question.

## Deferred-capability impact

MAIL-01, MAIL-11, and UI-10 all assume a multi-mailbox estate. This decision
supplies the estate model — identity, enablement, and per-mailbox cursor
isolation — without claiming any of their behaviour. Moving Sent-evidence
polling onto the same estate is deferred and needs no further ADR: it is the
same decision applied to the second route. Nothing here is claimed at a
deployment evidence tier; the estate is proven by a local caller and a green
build only.
