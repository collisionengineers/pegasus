# ADR-0024: Stable approved-mailbox identity and explicit inbound baseline

- Date: 2026-08-10
- Status: proposed — requires explicit Collision Engineers product-owner acceptance
- Owners: Collision Engineers product owner and Pegasus development team
- Relation: on acceptance, narrowly supersedes ADR-0022's inbound Graph
  mailbox/folder immutability and disable-and-add consequence, use of the Graph
  mailbox identity as the inbound poll-state key, cursor-carrying identity
  adoption, and unconditional cursor-preservation consequences; ADR-0022 remains
  the historical authority for the administrator-owned approved-mailbox estate
  and every clause not named here

## Context

ADR-0022 correctly moved the choice of approved inbound mailboxes into the
administrator-owned `ApprovedMailboxes` estate and kept the Worker read-only on
that policy table. It also made the current Graph mailbox identity the key of
`ApprovedInboxPollStates`, carried the existing delta cursor when the deployed
fallback identity was replaced by a saved identity, and promised that disabling
and re-enabling a mailbox preserved that cursor.

Those three consequences couple Pegasus identity to replaceable provider
coordinates. A Graph delta cursor is valid only for the mailbox, folder, and
request shape that minted it. The current adoption path re-keys the state but
carries the old cursor, after which the Graph adapter rejects the cursor against
the new path and polling stalls. Clearing the cursor is not safe: the current
external receipt token embeds the mutable Graph mailbox identity, so a replay
under another identity can create a second receipt for the same source message.
A folder-only change is not represented on the poll state at all.

Production containment on 2026-08-10 left all nine Worker functions disabled.
That live state permits this authority decision to be settled before any schema,
runtime, deployment, mailbox, or baseline change. This ADR records a target
contract only; it is not implementation, deployment, live verification, or
product-owner acceptance.

## Decision

### 1. Stable source identity

`ApprovedMailbox.Id` (`Guid`) is the durable Pegasus identity of one approved
mail source. Inbound poll state, poison occurrences, retained messages, source
duplicate protection, baseline state, freshness, and telemetry relate to that
stable ID.

Graph mailbox and Inbox-folder identities are replaceable provider coordinates,
not Pegasus identities. They remain administrator-controlled fields on the
existing `ApprovedMailboxes` row. Replacing either coordinate requires the row
to be `Disabled`, an authenticated reason, optimistic-version agreement, and
the existing permanent action-history record. It does not create a new
`ApprovedMailbox.Id` and never re-keys operational state.

ADR-0022's approval re-check inside the claiming transaction remains. The
Worker continues to read which rows are approved; it does not author, repair,
or replace their coordinates.

These identities are deliberately different:

| Concept | Identity | May change? | Purpose |
| --- | --- | --- | --- |
| Pegasus mail source | `ApprovedMailbox.Id` | No | Durable relationships and duplicate protection |
| Provider coordinates | Graph mailbox identity and Inbox-folder identity | Yes, only through the disabled administrator contract | Locate the current Graph scope |
| Cursor scope | Versioned fingerprint of the exact Graph coordinates and delta route | Changes when any scoped input changes | Decide whether a cursor is valid |
| Receipt identity | Stored v1 token or newly minted `mail:v2:` token | No after receipt creation | Exactly-once source occurrence |
| Baseline deployment | Baseline generation ID plus UTC cutoff | New for each approved baseline | Audit and resume one fresh-only waterline |

The mailbox address remains operator-visible routing and diagnostic data. This
ADR does not make an address, Graph identity, folder identity, cursor
fingerprint, receipt token, or baseline generation interchangeable with the
stable ID.

### 2. Cursor scope is explicit and versioned

Each inbound cursor is stored with the stable mailbox ID, the raw current Graph
mailbox and Inbox-folder identities used to obtain it, and a
`CursorScopeFingerprint`.

The first fingerprint algorithm is:

```text
graph-inbox-scope:v1:<base64url-no-padding(
  SHA-256(
    UTF8("graph-inbox-scope\nv1\n") ||
    length32be(UTF8(mailboxIdentity)) || UTF8(mailboxIdentity) ||
    length32be(UTF8(inboxFolderIdentity)) || UTF8(inboxFolderIdentity) ||
    length32be(UTF8("approved-inbox-messages-delta-v1")) ||
      UTF8("approved-inbox-messages-delta-v1")
  )
)>
```

Identity text is used exactly as validated and saved; it is not case-folded or
trimmed again. `length32be` is the unsigned four-byte big-endian byte length of
the following UTF-8 field. A change to the canonical Graph delta route,
including material query shape, requires a new route-shape identifier and scope
version. Raw coordinates remain separately available to administrators for
diagnosis. Cursors themselves are never logged.

Before an `Active` Graph call, the Worker computes the expected fingerprint
from the current approved coordinates and compares it using ordinal equality
with the stored active fingerprint. A mismatch records `baseline_required`,
releases the lease, and makes no Graph call. It never carries, clears, or
rewrites the active cursor.

During `Baseline`, the deployed generation's expected fingerprint must instead
match the candidate scope. A new generation may use Graph's initial delta route
only because `Disabled → Baseline` explicitly authorized that exact scope and
cutoff. A resumed generation may use only its matching candidate cursor.

Graph `410 Gone` has the same policy outcome. The Worker records
`baseline_required` and stops that mailbox; it does not silently fall back to an
initial delta enumeration in `Active` or `Baseline`. Other independently valid
mailboxes may finish their bounded tick, but estate activation cannot pass while
any approved inbound mailbox requires a baseline.

### 3. A scope change requires an explicit fresh-only baseline

A provider-coordinate change, cursor-scope mismatch, Graph `410 Gone`, missing
scope evidence, or missing completed generation requires a new explicit
baseline. Changing coordinates never deletes the old cursor as an
administrative side effect. The next baseline is authorized only through the
governed Worker modes below.

One deployment-bound baseline generation applies to the complete set of
approved inbound mailboxes. It carries:

- `BaselineGenerationId`: a new opaque `Guid` supplied by the reviewed
  deployment;
- `BaselineCutoffUtc`: one immutable UTC instant supplied with that generation;
- the expected scope fingerprint for each approved inbound mailbox;
- per-mailbox status `Pending | Running | Complete | Failed`;
- a resumable candidate cursor separate from the last active cursor; and
- completion time and content-safe page/item counts.

The cutoff is selected only after the exact production Worker has a verified
durable `Disabled` readback and before `Baseline` is enabled. It cannot precede
that readback or be changed after the generation is supplied. The approved
inbound membership and each member's coordinates must remain unchanged for the
generation; a membership or coordinate change fails the generation and requires
a return to `Disabled` plus a new generation.

The same generation, cutoff, and per-mailbox scope is idempotent and resumable.
Reusing a generation with another cutoff or scope fails closed. A different
generation is accepted only after the deployed mode has returned to
`Disabled`.

During `Baseline`, each page advances only the candidate cursor. A message with
`ReceivedAtUtc < BaselineCutoffUtc` advances the candidate cursor but is not
retained, quarantined, passed to Intake, or allocated. A message at or after the
cutoff follows the normal exactly-once path, including while older pages are
still draining. Completion is recorded only after Graph returns the terminal
delta link for the exact scope. Completion atomically promotes the candidate
cursor and its scope fingerprint to active state; only that explicit promotion
replaces the prior active cursor.

`Active` requires every currently approved inbound mailbox to have `Complete`
for the deployed generation, to match its current scope fingerprint, and to
have no `baseline_required` failure. A missing or inconsistent mailbox fails
the activation gate rather than being skipped.

### 4. Receipt token version 2 preserves immutable history

Existing external receipt tokens are immutable historical facts. No migration
rewrites a v1 token already attached to a retained message, intake receipt,
case, or action history.

For a message not already known under its stable mailbox/message identity, new
inbound receipt tokens use:

```text
mail:v2:<base64url-no-padding(
  SHA-256(
    UTF8(lowercase ApprovedMailbox.Id in Guid "D" form) ||
    0x00 ||
    UTF8(Graph ImmutableMessageId)
  )
)>
```

The prefix separates mailbox v2 tokens from manual uploads and v1 tokens. The
hash is fixed length and independent of Graph mailbox and folder coordinates.
`ImmutableMessageId` remains exact provider text; it is not case-folded.

Stable `(ApprovedMailbox.Id, ImmutableMessageId)` uniqueness is authoritative
for inbound messages in addition to the existing intake source-channel/token
uniqueness. On replay, an existing stable mailbox/message occurrence returns
its already stored token — including a v1 token — and cannot mint a second v2
receipt. Migration/backfill must stop on a missing or ambiguous approved-mailbox
relationship; it never invents an ID, drops a row, or rewrites a historical
token.

### 5. Worker operation has three governed modes

The existing Worker and its exact nine functions use one repository-owned mode:

| Mode | Function state | Inbound behavior |
| --- | --- | --- |
| `Disabled` | all nine functions disabled | No trigger execution. Required initial, rollback, and pre-baseline state. |
| `Baseline` | only `InboxPollFunction` enabled; the other eight disabled | Run the one supplied fresh-only generation; pre-cutoff mail advances the candidate cursor only, while at/after-cutoff mail follows normal intake. |
| `Active` | all nine functions enabled | Normal operation from the completed generation's active cursor. |

Allowed transitions are `Disabled → Baseline`, `Baseline → Active`,
`Baseline → Disabled`, and `Active → Disabled`. There is no direct
`Active → Baseline` transition and no arbitrary partial-function combination.
`Disabled → Baseline` requires the new generation and cutoff together.
`Baseline → Active` requires the complete-estate gate above. Any invalid or
missing mode/generation/cutoff combination renders all nine functions disabled
and fails release validation.

The generation identifies one deployment-controlled baseline attempt. It is
not the mailbox identity, cursor scope, receipt identity, release commit, or
Worker package identity, and evidence must report those concepts separately.

### 6. Existing boundaries carry the change

Implementation uses `Pegasus.Core` policy and ports, the existing
`Pegasus.Infrastructure` adapters and EF migration stream, the existing
`Pegasus.Worker` composition root and Function App, the existing nine
functions, and the existing production SQL database.

No new top-level directory, project, store, migration stream, runtime, process,
Function App, deployment unit, operator-side cursor tool, Outlook
mailbox/message mutation, or credential is authorized. Poll, poison,
retained-message, scope, and baseline state remain columns and relationships in
the existing inbound operational tables and production database boundary;
there is no separate baseline store.

The Worker retains `SELECT` only on `ApprovedMailboxes`. It may insert or update
only its existing operational state tables under the reviewed runtime-role
matrix. Web remains the author of administrator-approved mailbox policy. No
broader SQL, Graph, Azure, Outlook, Principal, Box, or operator authority follows
from this decision.

This decision applies to inbound Intake. Sent-evidence polling is unchanged
except for any separately reviewed mechanical contract adaptation needed to
compile; no Sent behavior, identity migration, or activation is accepted here.

## Consequences

- ADR-0022 remains intact as historical authority. On acceptance, only the
  named inbound Graph-coordinate immutability/disable-and-add,
  Graph-primary-key, cursor-adoption, and unconditional-preservation
  consequences are superseded.
- Mailbox moves and folder changes no longer alias source identity, but they
  deliberately stop inbound processing until an explicit baseline completes.
- A baseline may enumerate old folder contents, but pre-cutoff material cannot
  enter Pegasus business state. At/after-cutoff delivery remains exactly once.
- Graph `410 Gone` becomes an operator-visible baseline requirement rather than
  an automatic replay.
- v1 evidence remains byte-for-byte stable while new messages gain a short,
  provider-coordinate-independent token.
- Implementation requires a normal forward EF migration and caller-backed
  tests in later tickets. This ADR adds no schema or runtime itself.
- Production remains in the separately evidenced `Disabled` containment state
  until later tickets implement, deploy, baseline, activate, and live-verify
  the accepted contract under their own approvals.

## Acceptance boundary

This ADR remains proposed until the Collision Engineers product owner explicitly
accepts it. Opening, reviewing, or merging a documentation PR must not be
reported as that acceptance unless the product owner's exact acceptance is
separately recorded. No application, infrastructure, test, Azure, SQL, Outlook,
Principal, or Box change is authorized by this document.
