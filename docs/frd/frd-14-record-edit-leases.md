# FRD-14: Record edit leases

> Owner capabilities: CASE-27 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- To change a Case, a Triage Case or an Image Intake record, staff press
  **Edit** and get a lease. Everyone else sees who is editing and can only
  read.
- A lease lasts five minutes. The browser renews it every minute while the
  editing screen is open, so editing lasts as long as the session. Leaving a
  Case by a link ends edit mode; any other exit lets the lease expire by
  server time.
- The lease belongs to the staff member, not the window. Coming back to a
  record you are still editing puts you straight back in; you never take over
  your own lease.
- Any staff member with edit rights can **Take over** a colleague's lease.
  No reason is needed. The takeover goes into history, and the previous
  editor's next save is refused.
- Every save sends the lease token and the version that was loaded. Wrong
  holder, expired lease or stale version: the save is refused and nothing is
  overwritten.
- Automatic processing never pretends to hold a staff lease.

## Purpose

One rule for who may change a record at a time, so two people never
silently overwrite each other. It covers the Case edit lease and the
record-scoped edit scopes used by Triage Cases and Image Intake. Administration
settings use expected-version checks without edit leases
([FRD-17](frd-17-administration-workspace.md#administration)).

## Behaviour

### Case edit lease

Every staff change to a Case targets one Case through a named Core action and
needs the role allowed by the
[staff role access matrix](frd-04-parties-accounts-and-access.md#staff-role-access-matrix).

**Entering edit.** Pressing Edit claims the Case's one server-owned lease.
Other authorised staff stay read-only and can see who holds it. The Case
workspace and the assessment screen share one edit mode over one lease.

**How long it lasts.** The lease is five minutes long. While the editing
screen is open the browser sends a heartbeat every minute, which renews it.
Editing therefore lasts as long as the holder keeps the screen open. If the
holder leaves, the lease expires by server time and can then be claimed by
someone else. Because nobody can know when a present holder will leave, a
non-holder is told who is editing and is never given a time.

**Coming back.** The lease belongs to the staff member, not to a browser
window. When the holder opens the Case again, from a second tab or after
visiting another record, the page resumes the lease they hold: the same
token, renewed as a heartbeat renews it, with nothing recorded. Pressing Edit
on a Case they already hold does the same. The holder is never offered a
takeover of themselves.

**Leaving.** Leaving the Case by a link in Pegasus ends edit mode. With
unsaved changes the page first asks **Keep editing**, **Discard** or
**Save**; Save saves and then leaves. The lease is released as the operator
goes, so the Case is free for colleagues at once. A link to the same Case (a
section, a view or one of its own pages) keeps editing. Closing the tab,
Back, or typing an address can only show the browser's own leave prompt when
there are unsaved changes; the lease then expires by server time, and coming
back before then resumes it.

**Every save carries proof.** Each save, transition, assignment,
association, evidence change or other staff change sends both the lease token
and the Case version the editor loaded. Core checks both inside the
transaction. Create audit advances the Case version, so a change prepared
before it is refused as stale; every change after it edits the Audit
([FRD-13](frd-13-case-lifecycle-and-workflow.md#create-audit)).

**Same guard everywhere.** Web pages and MCP Automation Actor calls use the
same check. Background records that only append, such as receipts, dispatch
and document-processing records, are separate from editable Case state and
cannot change it around the version check.

**What goes in history.** A takeover, a deliberate recovery, and a refused or
failed save that matters are permanent, attributable history. Routine
renewal, expiry, heartbeats, polling and adapter mechanics are telemetry
only.

### Record edit scopes

An existing Triage Case or Image Intake record opens read-only. Its Edit
action claims a scope for that one record, with the same five-minute lease
and one-minute heartbeat as a Case. The scope covers the record only, not its
queue or a linked Case. A Triage Case has no Case edit lease: its Triage edit
scope and Triage version are its edit authority wherever a Case needs one,
including Case custody and upload association, and a custody completion by
the system never invalidates it.

- Save checks the holder token and the expected version inside the change
  transaction, applies the change, and releases the scope in the same
  transaction.
- Cancel releases the scope and changes nothing.
- A new record that has not been saved has no scope.
- The holder is shown by staff account name, never by an internal identifier.
- A command that touches both a Triage Case and an instruction Case checks
  both: the Triage edit scope and the Case edit lease.
- On a Vehicle images record, a Principal change, staff closure or
  staff-directed merge checks the holder, token and lifecycle version in its
  own transaction. Automatic image processing runs as the system worker; it
  never impersonates a staff scope.
- Disabling an account or revoking its sessions clears that account's
  record scopes, so a token from the old session cannot save later. The Case
  lease is owned by the Case workflow and is handled there.

### Take over

**Your own lease.** A staff member never takes over their own lease. A Case
they hold resumes as described under Coming back. A Triage Case or Image
Intake record still opens read-only; its **Edit** claims the scope back
silently with a new token, so another window of theirs on the same record
has its next heartbeat refused. Neither writes history.

**A colleague's lease.** Any staff member with edit rights for that record
may press **Take over** on a lease a colleague holds. No reason is asked for.
Pegasus records a history line naming who took over, from whom, and when.
The takeover is history rather than a change to the record, so it never
advances the record's version. The previous holder's next heartbeat or save
is refused. They keep their unsaved values on screen so they can copy them,
and must reload to edit again. There is no Administrator-only path; the rule
is the same for everyone with edit rights. A lease held by automatic
processing cannot be taken over.

### Refusals and recovery

Core refuses a change whose lease is missing, expired, held by someone else,
or whose version is stale. It never overwrites newer work. The refused editor
keeps the proposed values on screen for comparison. The reloaded page shows
the record as it now stands; if they still hold the lease they carry on
editing from it, and otherwise they claim a lease again. There is no merge
and no forced save.

The following do not exist: an Administrator bypass, collaborative merge,
bulk Case edits, editing lifecycle from a queue row, a provider Case-edit
route, or a direct edit through an external system or adapter.

**Lost release.** Leaving a Triage Case or Image Intake page releases its
scope through a `pagehide` beacon; leaving a Case by a link releases its
lease through a beacon sent as the link is followed. A lost beacon costs
nothing: the holder's own claim takes the lease back, and a colleague waits
at most five minutes or takes it over. A hidden browser tab keeps
heartbeating, though its timers are throttled.

## States and transitions

| Lease state | How it is reached | What non-holders see |
| --- | --- | --- |
| Free | No lease, or the last one expired or was released | Edit is offered |
| Held | Edit claimed it and heartbeats keep renewing it | Holder's name, read-only, Take over; the holder resumes it |
| Expired | Five minutes without a heartbeat | Edit is offered |
| Taken over | A colleague took it over, or the holder claimed a record scope back in another window | Previous holder's saves are refused |

## Edge cases and fail-closed behaviour

- A save with the wrong token or a stale version is refused; nothing is
  overwritten and the editor keeps their values.
- A non-holder is never shown a countdown or an expiry time.
- A second window of the same holder shares the Case lease; its saves are
  still checked against the version it loaded.
- A record-scope window of the same holder is refused after that holder
  claims the scope back in another window.
- A revoked session's token cannot save an existing record.
- Automatic processing has no lease and cannot claim one.

## Acceptance evidence

Core tests cover claim, resume, heartbeat, expiry, wrong-holder and
stale-version refusal for Cases and for record scopes. Integration tests
cover a colleague takeover on a Case whose current version already has its
own history, the holder's own claim and resume, the Case ribbon, the leaving
beacon and the record pages over real HTTP. Deployment and live acceptance
are separate evidence tiers
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `CASE-27` in [capabilities](../capabilities.md).
- Related FRDs: [FRD-03](frd-03-triage.md),
  [FRD-04](frd-04-parties-accounts-and-access.md),
  [FRD-10](frd-10-mcp-automation-and-actor-boundary.md),
  [FRD-13](frd-13-case-lifecycle-and-workflow.md),
  [FRD-16](frd-16-case-record-workspace.md),
  [FRD-17](frd-17-administration-workspace.md),
  [FRD-19](frd-19-image-led-intake-and-pairing.md).
- Technical constraints:
  [ADR-0031](../adr/0031-automation-actor-contract-without-eva-export-tools.md)
  (Automation Actor contract).
