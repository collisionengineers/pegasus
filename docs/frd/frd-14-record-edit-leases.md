# FRD-14: Record edit leases

> Owner capabilities: CASE-27 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- To change a Case, a Triage Case or an Image Intake record, staff press
  **Edit** and get a lease. Everyone else sees who is editing and can only
  read.
- A lease lasts five minutes. The browser renews it every minute while the
  editing screen is open, so editing lasts as long as the session. Leave, and
  it expires by server time.
- Any staff member with edit rights can **Take over** another person's lease.
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

**Your own lease in another window.** If the same staff member opens a
record they already hold in another window, the page says "You are editing
this record in another window" and offers **Take over**. It reclaims the
scope and rotates the token, so the other window's next heartbeat is refused
and its Save is disabled. A Case behaves the same way through the ordinary
Edit Case control, which replays the retained lease for the same claim.

**Someone else's lease.** Any staff member with edit rights for that record
may also press **Take over** on a lease another person holds. No reason is
asked for. Pegasus records a history line naming who took over, from whom,
and when. The previous holder's next heartbeat or save is refused. They keep
their unsaved values on screen so they can copy them, and must reload to
edit again. There is no Administrator-only path; the rule is the same for
everyone with edit rights.

### Refusals and recovery

Core refuses a change whose lease is missing, expired, held by someone else,
or whose version is stale. It never overwrites newer work. The refused editor
keeps the proposed values on screen for comparison and must reload and claim
a lease again. There is no merge and no forced save.

The following do not exist: an Administrator bypass, collaborative merge,
bulk Case edits, editing lifecycle from a queue row, a provider Case-edit
route, or a direct edit through an external system or adapter.

**Lost release.** Leaving a page releases its scope through a `pagehide`
beacon. If that beacon was lost and the same holder comes back while their
scope is in its final heartbeat interval (four minutes into the five), the
page reclaims it silently. A hidden browser tab keeps heartbeating, though
its timers are throttled.

## States and transitions

| Lease state | How it is reached | What non-holders see |
| --- | --- | --- |
| Free | No lease, or the last one expired or was released | Edit is offered |
| Held | Edit claimed it and heartbeats keep renewing it | Holder's name, read-only, Take over |
| Expired | Five minutes without a heartbeat | Edit is offered |
| Taken over | Another person or window claimed it | Previous holder's saves are refused |

## Edge cases and fail-closed behaviour

- A save with the wrong token or a stale version is refused; nothing is
  overwritten and the editor keeps their values.
- A non-holder is never shown a countdown or an expiry time.
- A second window of the same holder is refused after a takeover.
- A revoked session's token cannot save an existing record.
- Automatic processing has no lease and cannot claim one.

## Acceptance evidence

Core tests cover claim, heartbeat, expiry, wrong-holder and stale-version
refusal for Cases and for record scopes. Integration tests cover the Case
ribbon and the record pages over real HTTP. Taking over another person's
lease is a required change that Core does not yet implement; its acceptance
needs Core and Web evidence before this document is fully met. Deployment and
live acceptance are separate evidence tiers
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
