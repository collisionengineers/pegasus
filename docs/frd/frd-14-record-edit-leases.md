# FRD-14: Record edit leases

> Owner capabilities: CASE-27 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- To change a Case or an Image Intake record, staff press **Edit** and get a
  lease. Everyone else sees who is editing and can only read.
- A Triage Case has no Edit. Each change holds the record for its one save.
- A lease lasts five minutes. The browser renews it every minute while the
  editing screen is open, so editing lasts as long as the session. A lease
  that lapsed while nobody claimed it (a laptop asleep) is picked up again by
  the next heartbeat or save; there is no Renew. Leaving a Case by a link
  ends edit mode; any other exit lets the lease expire by server time.
- The lease belongs to the staff member, not the window. Coming back to a
  record you are still editing puts you straight back in; you never take over
  your own lease.
- A second editor on a Case a colleague holds **asks to edit**. The holder's
  open Case page shows the request with Accept and Deny. Accept hands the
  lease over; after 45 seconds unanswered the member of staff who asked may
  **Take over anyway**. A lease the Automation Actor holds is taken over at
  once. The hand-over or takeover goes into history, and the previous
  editor's next save is refused (R550 to R557, 30 October 2026).
- Every save sends the lease token and the version that was loaded. A
  wrong or superseded lease, or a save that would overwrite a value the system
  wrote since the page loaded it, is refused and nothing is overwritten.
- System work (custody, filing, a vehicle lookup) never ends an edit session:
  it moves the Case version under the lease, the holder's next save lands, and
  the page catches up with what changed.
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
The claim is made at the Case's current version: a page drawn before system
work or a colleague's save moved the Case still enters edit mode and is drawn
again as the Case now stands, and only a page from the future is refused
(operator, 6 October 2026). Other authorised staff stay read-only and can see
who holds it. The Case
workspace and the assessment screen share one edit mode over one lease.

**How long it lasts.** The lease is five minutes long. While the editing
screen is open the browser sends a heartbeat every minute, which renews it.
Editing therefore lasts as long as the holder keeps the screen open. A
heartbeat or save that finds the holder's lease lapsed, with nobody having
claimed it since, carries on with the same token, so a machine that slept
keeps editing when it wakes; there is no Renew (operator, 6 October 2026). If
the holder leaves, the lease expires by server time and can then be claimed by
someone else. Because nobody can know when a present holder will leave, a
non-holder is told who is editing and is never given a time.

**Coming back.** The lease belongs to the staff member, not to a browser
window. When the holder opens the Case again, from a second tab or after
visiting another record, the page resumes the lease they hold: the same
token, renewed as a heartbeat renews it, with nothing recorded. Pressing Edit
on a Case they already hold does the same. The holder is never offered a
takeover of themselves. Only the Case page resumes: a one-off change made
from elsewhere, such as the Work Centre, linking an item to the Case, or an
item of the Case's own Actions menu taken outside a session, is refused
while the lease is live, so it never ends the holder's edit session. Such a
change claims the lease for its one command and consumes it; a refused
command frees the lease it claimed.
Automatic processing never resumes a lease; each of its sessions claims and
is refused while any lease is live, unless the Automation Actor asks to take
over a lease a staff member holds (operator, 7 October 2026). An Automation
lease that lapsed, with nobody having claimed the Case since, carries on with
the same token, as a staff lease does. An Automation write presented without a
lease token claims the lease for that one command and releases it afterwards;
the explicit Automation lease tools remain for multi-step work
([FRD-10](frd-10-mcp-automation-and-actor-boundary.md#edit-leases)). A write
under an Automation lease keeps it, unlike a staff change: the Automation
Actor's lease stands until it is released or lapses (operator, 8 October
2026).

**Leaving.** Leaving the Case by a link in Pegasus ends edit mode. A change
not yet sent lands first ([FRD-16](frd-16-case-record-workspace.md#case-workspace)),
then the lease is released as the operator goes, so the Case is free for
colleagues at once. A link to the same Case (a section, a view or one of its
own pages) keeps editing. Closing the tab, Back, or typing an address sends
a change not yet sent as the page hides and shows no prompt; the lease then
expires by server time, and coming back before then resumes it.

**Every save carries proof.** Each save, transition, assignment,
association, evidence change or other staff change sends both the lease token
and the Case version the editor loaded. Core checks both inside the
transaction. Every staff change ends the lease it was made under and the
holder's page receives the next one, so a token that still matches proves no
staff change has landed since the page loaded its version. Core's Case edit
authority owns this rule, the five-minute lease length and the two writes that
keep the lease (an Automation write and a Glass's return), and every store
applies it (R514, 29 October 2026). Create audit is a
staff change, so a change prepared before it is refused; every change after it
edits the Audit
([FRD-13](frd-13-case-lifecycle-and-workflow.md#create-audit)).

**System work keeps the session.** Automatic processing that changes the
Case (custody confirmed or failed, an original report recognised, a vehicle
lookup filling empty fields, a mail correction or association raising a
query, a flagged reply resolving one, an automatic link) advances
the Case version and leaves the editor's lease standing (operator, 6 October
2026). The holder's next change, sent with the version they loaded, lands. The
one exception is a value the system filled: the Case save sends every field
the page shows, so a field still showing what it held before a lookup filled
it keeps the lookup's value, and a different value typed over it is refused
until the page has caught up. The heartbeat answers the Case's version, and
the page redraws each section that holds nothing unsent as the Case now
stands ([FRD-16](frd-16-case-record-workspace.md#case-workspace)).
System work on a Case either yields to a held lease (an image merge or an
automatic link waits while a member of staff edits) or advances the version
under it, and never ends it; system work on a Triage Case never yields to its
Triage scope and leaves the Triage version alone. The two stay different
(R522, 29 October 2026).

**Same guard everywhere.** Web pages and MCP Automation Actor calls use the
same check. Background records that only append, such as receipts, dispatch
and document-processing records, are separate from editable Case state and
cannot change it around the version check.

**One command's authority.** A change that holds the record for its one
command, from a staff page or an Automation write given no lease token,
claims the Case edit lease, or for a Triage Case its Triage edit scope, runs
the command, and always releases the authority afterwards, whatever the
command answered. A release that finds the authority already ended by the
command's own staff write is the expected outcome and is not a failure. Any
other failed release never changes the command's outcome: it is logged as a
Warning without the exception, and an authority still held lapses by itself
(R523, 29 October 2026).

**What goes in history.** A takeover, a deliberate recovery, and a refused or
failed save that matters are permanent, attributable history. Routine
renewal, expiry, heartbeats, polling and adapter mechanics are telemetry
only.

### Record edit scopes

An existing Image Intake record opens read-only. Its Edit action claims a
scope for that one record, with the same five-minute lease and one-minute
heartbeat as a Case. A Triage Case shows no Edit. Each change claims its
Triage edit scope for that one save and releases it in the same save; a
refused change releases it at once. A scope held by someone else, such as an
Automation session, refuses the change. The scope covers the record only,
not its queue or a linked Case. A Triage Case has no Case edit lease: its
Triage edit scope and Triage version are its edit authority wherever a Case
needs one,
including Case custody and upload association, and a custody completion by
the system never invalidates it: system work on a Triage Case never yields to
the scope and leaves the Triage version alone (R522, 29 October 2026).

- Save checks the holder token and the expected version inside the change
  transaction, applies the change, and releases the scope in the same
  transaction.
- On an Image Intake record, Cancel releases the scope and changes nothing.
- A new record that has not been saved has no scope.
- The holder is shown by staff account name, never by an internal identifier.
  A takeover history line names the previous holder the same way: one whose
  name cannot be resolved is Unnamed, never an identifier (R520, 29 October
  2026).
- A command that touches both a Triage Case and an instruction Case checks
  both: the Triage edit scope and the Case edit lease.
- On a Vehicle images record, a Principal change, staff closure or
  staff-directed merge checks the holder, token and lifecycle version in its
  own transaction. Automatic image processing runs as the system worker; it
  never impersonates a staff scope.
- Disabling an account or revoking its sessions clears that account's
  record scopes, so a token from the old session cannot save later. Its Case
  leases are not cleared: the Case workflow owns them, and each lapses within
  five minutes. The two stay different (R521, 29 October 2026).

A record scope follows the Case lease's rules:

- A heartbeat or save that finds the holder's scope lapsed, with nobody
  having claimed the record since, carries on with the same token (R515,
  29 October 2026).
- A scope is claimed at the record's current version, and a claim or a save
  under it is refused only when it expects a version newer than the record's
  (R516, 29 October 2026). A Triage or Image Intake save still checks, in its
  own store, the record version it was loaded at, and a Triage save, which has
  no Edit step to draw the record again, is refused as changed when the
  Triage moved past the version it was posted with.
- A claim sent again with the same operation key and details returns the
  same token while the scope it issued is live (R519, 29 October 2026).

### Take over

**Your own lease.** A staff member never takes over their own lease. A Case
they hold resumes as described under Coming back. An Image Intake record
still opens read-only; its **Edit** on a scope they hold resumes it with the
same token, so another window of theirs on the same record keeps editing
(R518, 29 October 2026). Neither writes history. A one-off change made from
elsewhere is refused while their scope is live.

**A colleague's lease.** Any staff member or the Automation Actor with edit
rights who finds a Case held by a member of staff **asks to edit** instead of
seizing it (R550, 30 October 2026). The Case page shows the holder's name and
**Ask to edit**; while the request stands it says "Waiting for {name} to
answer…". The holder's open Case page shows the request in a dialog with
**Accept** and **Deny**. The dialog has no close control and Escape does
nothing (R557, 30 October 2026). A page held in a hidden tab flashes its title
and raises a browser system notification where permission is granted;
permission is asked once, on the first gesture inside edit mode, and when it
is refused the title flash is all there is (R555, 30 October 2026). The page
learns of a request, and the requester's page of its answer, through a long
poll held up to about 25 seconds, so a request shows within a second (R551,
30 October 2026).

Accept lands any change not yet sent, hands the lease to the requester in one
transaction and releases the holder's session; the holder's page redraws
read-only with "{requester} is editing". The hand-over is Case history under
the existing takeover event type with the sentence "Edit lease handed over to
{name} at their request." and never advances the Case's version (R553, 30
October 2026). Deny closes the dialog, the holder carries on, and the
requester's page says so and offers nothing further until it asks again.

The holder has 45 seconds to answer. After that the requester's ribbon offers
**Take over anyway**, the forced takeover. It is open only to the member of
staff who asked, and only while the holder asked still holds the lease and
has not answered; it has no time cap (R552, 30 October 2026). No reason is
asked for. Pegasus records a history line naming who took over, from whom,
and when. The takeover is history rather than a change to the record, so it
never advances the record's version. The previous holder's next heartbeat or
save is refused. The value they were typing stays on screen so they can copy
it, and they must reload to edit again. There is no Administrator-only path;
the rule is the same for everyone with edit rights.

One request stands per Case. Another editor is refused while the holder is
being asked and is told it has already been asked; a request the holder never
answered gives way to the next, and the Automation Actor's request gives way
the same way (R550, 30 October 2026). A request is addressed to the holder
asked and ends when the holder changes, when the lease lapses, or when the
holder finishes with Done. The holder's own save, which ends and re-claims
the lease, leaves it standing. A request, a denial, a withdrawal or silence
is telemetry; an accepted hand-over and a forced takeover are history (R550,
30 October 2026).

**The Automation Actor asks and never forces.** The Automation Actor asks for
a lease a member of staff holds through `pegasus_edit_begin` with `takeOver`.
The call raises the request and waits inside it for the answer up to 45
seconds, returning the lease when the holder accepts and a refusal naming
declined, no answer or already asked otherwise. It has no force and no
Take over anyway (R554, 30 October 2026;
[FRD-10](frd-10-mcp-automation-and-actor-boundary.md#edit-leases)). A Triage
scope is unchanged: the Automation Actor takes over a Triage scope a member
of staff holds at once (R556, 30 October 2026).

**An Automation lease.** Staff take over a Case lease or record scope the
Automation Actor holds at once, with no request, since nobody can be asked,
and only staff may: the Automation Actor never takes over an Automation
holder (R517, 29 October 2026; R554, 30 October 2026;
[ADR-0075](../adr/0075-staff-take-over-of-an-automation-lease.md)). The
history line names the Automation Actor as AI (operator, 15 and 16 October
2026). Otherwise that lease lapses within five minutes, or ends at once when
an Administrator stops the automation client
([FRD-10](frd-10-mcp-automation-and-actor-boundary.md#edit-leases)).

**Image Intake and Triage.** The request flow applies to the Case lease only.
An Image Intake record keeps an immediate **Take over** by any staff member
with edit rights, recorded in history as above, and a Triage Case's scope is
unchanged; issue #1797 records standardising the request flow across record
types (R556, 30 October 2026; R517, 29 October 2026). A member of staff's
Triage save never takes over: its scope lasts that one save.

### Refusals and recovery

Core refuses a change whose lease is missing, released, held by someone
else, or superseded by a later staff change, or which would overwrite a value
the system filled since the page loaded it. It never overwrites newer work. The refused editor
keeps the proposed values on screen for comparison. The reloaded page shows
the record as it now stands; if they still hold the lease they carry on
editing from it, and otherwise they claim a lease again. There is no merge
and no forced save beyond keeping the values the system filled.

The following do not exist: an Administrator bypass, collaborative merge,
bulk Case edits, editing lifecycle from a queue row, a Principal Case-edit
route, or a direct edit through an external system or adapter.

**Lost release.** Leaving an Image Intake page releases its scope through a
`pagehide` beacon; leaving a Case by a link releases its
lease through a beacon sent as the link is followed. A lost beacon costs
nothing: the holder's own claim takes the lease back, and a colleague waits
at most five minutes or takes it over. A hidden browser tab keeps
heartbeating, though its timers are throttled.

## States and transitions

| Lease state | How it is reached | What non-holders see |
| --- | --- | --- |
| Free | No lease, or the last one expired or was released | Edit is offered |
| Held | Edit claimed it and heartbeats keep renewing it | Holder's name, read-only, Ask to edit; the holder resumes it |
| Requested | A colleague asked to edit a Case the holder holds (R550, 30 October 2026) | A request stands; the holder's page shows Accept and Deny; after 45 seconds the requester is offered Take over anyway |
| Handed over | The holder accepted (R553, 30 October 2026) | The requester holds a new lease; the previous holder's saves are refused |
| Expired | Five minutes without a heartbeat | Edit is offered; until someone claims it, the holder's own heartbeat or save picks it up again |
| Taken over | A colleague took it over, or forced it after 45 seconds (R552, 30 October 2026) | Previous holder's saves are refused |

## Edge cases and fail-closed behaviour

- A save with the wrong token, or one over a value the system filled since
  the page loaded it, is refused; nothing is overwritten and the editor keeps
  their values.
- A non-holder is never shown a countdown or an expiry time.
- A second window of the same holder shares the Case lease; its saves are
  still checked against the version it loaded. Leaving by a link from either
  window ends edit mode in the other. A save in either moves the lease on:
  the other window's next heartbeat is refused, it catches up with the Case
  as it stands, and the value it was typing stays on screen.
- A second window of the same holder shares the record scope: claiming it
  again in one resumes it in both (R518, 29 October 2026).
- A revoked session's token cannot save an existing record.
- Automatic processing has no lease and cannot claim one.
- Two colleagues ask for the same Case: the first request stands while it is
  pending and the second is told the holder has already been asked (R550, 30
  October 2026).
- The holder has the Case open in two tabs: both show the request, and the
  first answer wins; the other tab's dialog closes when it learns of the
  answer (R551, 30 October 2026).
- The requester leaves the page: a beacon withdraws the request. A lost beacon
  leaves it to the 45-second window.
- The holder's lease lapses while a request stands: the request ends, and the
  requester's page offers Edit. It never claims on its own.
- The holder's tab is asleep: the dialog is never shown, and the 45-second
  window with Take over anyway is the design (R552, 30 October 2026).
- Notification permission is refused: the tab title flash is the only alert
  (R555, 30 October 2026).

## Acceptance evidence

Core tests cover claim, resume, heartbeat, a lapsed lease picked up again,
wrong-holder and stale-version refusal for Cases and for record scopes, and a
version moved only by system work. Integration tests
cover a colleague takeover on a Case whose current version already has its
own history, the holder's own claim and resume, the Case ribbon, the leaving
beacon and the record pages over real HTTP. Core tests cover the request
policy (one request per Case, the 45-second window, what ends a request),
persistence tests cover the atomic hand-over and its history line, and Web
tests cover the long poll, the holder's dialog and the requester's ribbon
states (R550 to R557, 30 October 2026). Deployment and live acceptance
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
