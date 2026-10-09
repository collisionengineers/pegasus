# FRD-13: Case lifecycle and workflow

> Owner capabilities: CASE-01, CASE-13 to CASE-20, CASE-24 to CASE-26, CASE-32, EXT-05, MAIL-18 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- A Case moves `Not ready` → `Review` → `With Engineer` → `Completed`.
  `Query` is a return trip from Completed. `Held` is a pause.
- Not ready becomes Review by itself when every required instruction item
  and image is present. Nobody ticks a "reviewed" box.
- **Assign Engineer** is the only way from Review to With Engineer. The Case
  export never moves a Case.
- **Mark report sent** needs a real Sent email item. **Mark completed** can be
  undone. A Case is never permanently closed.
- **Close case** records a cancellation or a rejection with a reason.
  **Archive** hides a closed Case from queues. Nothing is ever deleted.
- **Create audit** adds the Audit to an Inspection + Audit Case in work,
  whether or not its report is sent, and returns the Case to With Engineer
  for the Audit. It is always listed on such a Case; when refused it is
  greyed out and states why on hover.

## Purpose

This document owns how a Case moves through work: its states, what unlocks
each step, the actions staff can take, chasing for missing material, and what
happens after the report. Identity and Case types are in
[FRD-01](frd-01-case-identity-and-lifecycle.md). Edit leases are in
[FRD-14](frd-14-record-edit-leases.md). A Triage Case does not use these
states; its own are in [FRD-03](frd-03-triage.md).

## Behaviour

### States and labels

Every screen uses these words, taken from one presentation map. Internal enum
names never leak into labels.

| Label | Meaning |
| --- | --- |
| `Not ready` | Something required is still missing. Chasing runs here. |
| `Review` | Everything required is present. Staff hand the Case to an Engineer. |
| `With Engineer` | An Engineer is working on it, before or after the report. |
| `Completed` | The current work is done. Reversible. |
| `Query` | A query arrived on a Completed Case. Reversible. |
| `Held` | Paused by staff with a reason. |

Pegasus stores With Engineer as two internal states, one before the report
and one after it. Screens show them as one word.

A Case can also carry one of four closed dispositions: `Principal cancelled`,
`Collision Engineers rejected`, `Created in error` and `Source email
unlinked`. Each records a reason. The Case stays on record and can be
returned to work with a reason. No Case is ever shown as "Closed".

Every state change and disposition is a Core transition. It records who did
it, when, why, the state before and after, and any evidence, permanently.
Screens, the Worker, APIs and MCP tools all call the same Core use cases;
none of them has its own version of the rules. The Automation Actor takes the
Case's Actions-menu lifecycle acts as staff do
([FRD-10](frd-10-mcp-automation-and-actor-boundary.md#case-lifecycle)).

### Readiness and Review

**Blockers are specific.** Each unmet requirement is one named blocker. The
screen shows exactly which field or material is missing, where it should come
from and why it is required; its control is what clears it, with no sentence
saying how (operator, 9 October 2026). Pegasus never shows an
overall score, a percentage, or a summary such as "3 items outstanding". On
the Case record each report blocker links to the section that clears it. A
recorded value is never a blocker because of who recorded it: there is no
per-field review, and Assign Engineer is the only review (operator, 25
September 2026). While the report is not ready the aside's **Report not
ready** card lists every report blocker, each linking to its section, and the
Report section's head keeps the one **Not ready** availability label. The
Next action is one step (operator, 8 October 2026): in Not ready the first
outstanding requirement, in Review Assign Engineer, which opens the Actions
menu's dialog, and With Engineer the first blocker ([FRD-16](frd-16-case-record-workspace.md#case-workspace),
[FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#report-readiness)).

**Required items are configuration.** Instruction completeness and image
completeness are each a versioned list of items marked required or not
required in Workflow configuration
([FRD-17](frd-17-administration-workspace.md#workflow-configuration)). The
gate names every required item that is still missing.

**How a Case reaches Review.** When every required instruction item and every
required image is present, the Case moves from Not ready to Review on its
own. There is no staff act of reviewing instructions or images: no checkbox,
no dialog, no history line. The transition checks the stored facts inside its
own transaction and records which readiness policy and version it used. A
claim of readiness sent from a screen is not accepted as fact.

**Photographs that arrive later.** When follow-up photographs are matched to
a Case automatically, images count as complete only after the selected
photographs and any required source files are confirmed in Case custody.
Filing that is pending or failed does not clear the Images blocker.
Photographs from a merged Vehicle images record count the same way: they
complete the Case's images when the fold files them as Case images
([FRD-19](frd-19-image-led-intake-and-pairing.md#pairing-and-merge)). This
completion is recorded once, with its actor; running it again does not
override a later staff change. It does not clear other blockers, assign an
Engineer, or skip Review.

**Saving does not unlock.** An action is available exactly when its stated
prerequisites are met. Saving unchanged or unrelated data never unlocks an
action and never resets readiness, lifecycle or advisory state. A saved
required fact clears its blocker at once: on the Case record every change is
saved as it is made, so the blocker goes, the state moves and the action
appears within the round trip
([FRD-16](frd-16-case-record-workspace.md#case-workspace)).

### Assign Engineer

In Review, staff choose **Assign Engineer** and pick an eligible enabled
staff account. In one operation, under a Case edit lease — the session's, or
one claimed for the action ([FRD-16](frd-16-case-record-workspace.md#actions-menu))
— and the current version, Pegasus assigns the Engineer, sets the Sign-off
Engineer, and moves the Case
to With Engineer. A headless start command can hand a Review Case
to its already-assigned eligible staff member; it is not a second screen step.

Replaying the same request does not hand off twice. A request that is
incomplete, stale or unauthorised changes nothing.

This is the only route out of Review. The handoff records the Case's
`First sent to Engineer` the first time it enters With Engineer; the Work
Centre counts it ([FRD-15](frd-15-work-centre-queues-and-search.md)). The
Case export ([FRD-07](frd-07-case-export.md)) is available in every state,
never changes the Case state and records no handoff.

### Actions

- **Place on Hold** records a reason and an optional **Review on** date
  (today or later, a Europe/London calendar date). The held Case is due for
  its decision at the end of that date. Without a date it is due at the Held
  decision target after the hold was placed. The record reads "Held · review
  on 24 Sep". **Release Hold** records a reason.
- **Damage, Valuation, Estimate, Settlement and Report** can always be
  viewed. Staff with `PerformCasework` may edit them in Not ready, Review and
  With Engineer under the normal edit authority. They are read-only in Held
  and Completed. Staff and the Automation Actor record the Engineer's Value
  ([FRD-10](frd-10-mcp-automation-and-actor-boundary.md#assessment-writes)).
- **Report approval** names one immutable report file and who approved it:
  a staff member or the Automation actor
  ([FRD-10](frd-10-mcp-automation-and-actor-boundary.md#reports)).
- **Mark report sent** needs exact retained Sent evidence
  ([FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md#outbound-correspondence-evidence)).
  A generated file, an export, a draft, a queue result or a manual statement
  is not enough. Report sent moves the Case into its post-report phase,
  which screens still show as With Engineer. Sent evidence belongs to the
  report it proves: after Create audit, the Case's own Mark report sent takes
  the Audit report's evidence, sent after the Audit was created, while the
  Inspection view's Mark report sent takes the Inspection report's, on the
  Inspection's own work and without changing the Case's state (operator,
  1 October 2026).
- **Mark completed** records that the current work is complete. It is a
  reversible work state, not a closure. It needs no Audit.
- **Create audit** adds the Audit to an Inspection + Audit Case
  ([Create audit](#create-audit)).
- **Return to Engineer** records a reason and applies the destination gates
  when more engineering changes are needed. It is not needed to receive,
  attach or answer a query.
- **A Case save** needs no reason. Its history line names the fields that
  changed, for example "Vehicle registration, Incident date". Reasons are
  required where an action's rule asks for one: holds and releases, closure,
  corrections, returns to engineering, removals.

### Close case

**Close case** is the one adverse action, kept apart from ordinary progress.
It offers exactly the closed dispositions Core allows for that Case:
`Principal cancelled` and `Collision Engineers rejected`. A reason is
required. An outcome that is missing or unrecognised is refused; it never
falls back to a default.

Two dispositions are not offered here. `Created in error` needs its own
corrected-Principal replacement action
([FRD-01](frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity)).
`Source email unlinked` happens when staff unlink the email that created the
Case ([FRD-22](frd-22-pre-case-gates-matching-and-association.md#matching-conflicts-and-reversible-association)).
Mark completed is ordinary progress, not a closure.

A closed disposition locks nothing permanently and deletes no evidence. The
Case can return to work with a reason through the normal gates.

### Archive

A Case in one of the four closed dispositions can be archived. Archive is a
reasoned marker: it removes the Case from working queues and default search
results. Nothing is deleted; the Case, its files and its history stay
viewable. An already-archived Case is refused a second archive. Automatic
association never links new material to an archived Case.

### Completed and Query

The Engineer answers queries and disputes from the Principal, a third-party
insurer or the claimant. When a query is received for, or attached to, a
Completed Case, the Case moves to Query. Replying moves it back to Completed.
Correcting a linked message's classification to a post-report family attaches
a query in the same way. Correcting it away, or unlinking it, before any reply
was sent, while no other post-report message is linked, returns the Case to
Completed. Both transitions are recorded as Case history.
A draft or an acknowledgement is not a reply. Pegasus keeps the received
query and the actual reply as Case correspondence. Neither transition erases
earlier completion or query history. Further engineering edits go through
Return to Engineer.

### Create audit

**When it is offered.** Create audit is listed in the Actions menu of every
Inspection + Audit Case, in every state, in and out of an edit session
(operator, 1 October 2026); no other Case type lists it. It is live while
the Case is in work: Not ready, Review, With Engineer before or after the
report, Completed or Query. Whether the Inspection report has been sent does
not matter. It is refused, and the item is greyed out with the refusal as
its hover text, on a Held Case ("A held case cannot have an audit created
from it."), a Case with a closed disposition ("A closed case cannot have an
audit created from it."; Created in error keeps its own refusal), an
archived Case, a Case that already has its Audit, and while a colleague
holds the edit lease (the sections' own editing wording). A held Case is
live again after Release Hold. It uses the same staff authorisation as the
other Actions-menu progressions. Once the Inspection report is sent, Create
audit is the Case's Next action, stated in the aside and the Cases quick
detail with the same control (operator, 2 October 2026).
With no assigned Engineer it is refused with Return to Engineer's refusal,
"Report preparation requires an assigned Engineer.", and the assigned
Engineer must still be eligible, as for Return to Engineer. It asks for no
reason and no outcome.

**What it does.** In one operation, under a Case edit lease (the session's,
or one claimed for the action) and the current version, Create audit:

- adds the Audit to the Case under the Audit reference `a.{Case/PO}` and
  copies the Inspection's values into it
  ([FRD-01](frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity));
- moves the Case to With Engineer before the report, keeping the assigned
  Engineer and the Sign-off Engineer;
- keeps the Inspection's report approval and Sent evidence, where they
  exist, with the Inspection; the Inspection report is still generated,
  sent and marked sent from the Inspection view afterwards, again if
  needed ([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md));
- starts the creation of the `a.` Box subfolder
  ([FRD-05](frd-05-documents-extraction-and-custody.md#custody-and-derived-reads));
- records one history line, "Audit {Audit reference} created by {name}".

There is no separate success message. Replaying the same request creates no
second Audit, and an edit prepared before Create audit is refused as stale.

**After it.** The Audit drives the Case: its state, queues, Actions menu and
the Audit view's Next action follow the Audit's report, and the Case's report
generation, approval, Mark report sent and Mark completed act on the Audit
report. The Inspection's values are edited from the Inspection view, on the
Inspection's own work and without any effect on the Case's state, due date,
completeness or matching; its report is generated, sent and marked
sent from that view, again when needed, and that view's Next action states
the Inspection report's own step (operator, 2 October 2026). While the Audit
report is being
prepared, image intake association and evidence promotion are open again, as
for any Case before its report is sent.

### Sign-off Engineer

Sign-off Engineer is a Case field beside Engineer. Only enabled staff accounts
flagged as Sign-off Engineer are offered
([FRD-04](frd-04-parties-accounts-and-access.md#staff-accounts)). The
default is the assigned Engineer when that account is flagged; otherwise it
is the account marked as the default Sign-off Engineer in Administration. No
name is built into the software. Reports render the Case's sign-off name,
qualifications and signature image
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#initial-renderer-activation)).
The Engineer who issues a report is not automatically its signatory.

### Due work and chasing

**Due by** comes from the inspection date or the accepted equivalent
deadline. On an Inspection + Audit Case, Due by and the completeness gate
follow the Inspection's values; editing the Audit's copy changes neither.

**Chase interval.** One global setting in Workflow configuration, in whole
calendar days, range 1 to 365, default 7, calculated in Europe/London. When a
Case enters Not ready, the first chase falls that many days later at the same
local time, and repeats at the same interval. Changing the setting affects
schedules calculated after the change; a chase already calculated keeps its
date. Held keeps the remaining interval, and release to Not ready resumes it.
Review, accepted material arriving, completion, or a cancellation or
rejection stops the chase schedule.

**Chasing is manual.** A staff member, or the Automation Actor through
`pegasus_mail_send`
([FRD-10](frd-10-mcp-automation-and-actor-boundary.md#sending)), sends each
chaser. Pegasus records what
was attempted, by whom, through which channel, to which party and address,
when, and with what evidence. A recorded chase is not proof that it was
delivered. Each chaser keeps its recipient, channel, prepared draft or draft
reference, staff disposition and timestamps. Free-text notes may sit beside a
chaser without implying it was sent or answered.

**Send chaser** on the Case record opens the composer with the Case chaser
template ([FRD-17](frd-17-administration-workspace.md#e-mail-templates)) and
the Case's recorded addresses, and staff send it
([FRD-16](frd-16-case-record-workspace.md#actions-menu)). When that send's
exact Sent evidence is observed and the Case is Not ready with a chase
scheduled, Pegasus records the chase itself — channel E-mail, the addresses
sent to, outcome Sent, at the provider's sent time, by the staff member or
the Automation Actor who sent it — and schedules the next chase at the interval; in any other state
the Sent item is correspondence evidence and no chase is recorded (operator,
5 October 2026). Submitted is not Sent: nothing is recorded until the
evidence exists.

**What staff see.** For each item awaiting material, the work view shows the
missing-material reason, Due by, the next chase, the most recent channel and
outcome, an optional note, and the next permitted action. Prepared or copied
text always looks different from sent, delivered, answered or completed work.

**General Case tasks are unfinished (`CASE-20`).** `Pegasus.Core` holds a
Case task record with Open, Completed and Cancelled states, an optional
assignee, and create, assign, complete and cancel use cases. No screen shows
or uses them, which is why the Case record has no tasks panel
([FRD-16](frd-16-case-record-workspace.md)). A task has no due date, and
there are no reminders. Chasing above is the only due work staff see today.

### Cancellation messages

An incoming cancellation never changes a Case by itself. Mailbox processing
records the settled classification of each accepted message. Automatic
association uses the instruction profiles and match keys in
[FRD-22](frd-22-pre-case-gates-matching-and-association.md#matching-conflicts-and-reversible-association);
QDOS keeps its accepted correspondence predicates under
[ADR-0020](../adr/0020-accepted-qdos-case-association-predicates.md). Quoted
historical instructions inside a message are ignored. Only an incoming
instruction creates intake work.

A cancellation that has been retained and associated with a reason may
support a staff or Automation Actor action on a pre-report Case: place it on Hold with the
cancellation as the reason, confirm `Principal cancelled`, or release it.
Release needs the message recategorised, unlinked or reassociated first. Every
original and corrected classification, with actor, time, reason and evidence,
stays in history.

While a linked message's current classification is a cancellation and the
Case is open, the Case page's Next action names it, **Cancellation received**,
with **Open message**, and the Case's Engineer is told through the bell
([FRD-12](frd-12-operator-experience.md#the-shell)). Correcting the message
away clears the row. The Case's state still changes only by a staff or
Automation Actor action.

## States and transitions

| From | To | Trigger |
| --- | --- | --- |
| Not ready | Review | Every required item present (automatic) |
| Review | With Engineer | Assign Engineer, or the headless start command |
| Not ready, Review, With Engineer | Held | Place on Hold (reason) |
| Held | previous state | Release Hold (reason) |
| With Engineer | Completed | Mark completed |
| Completed | Query | Query received, attached, or corrected onto a linked message |
| Query | Completed | Reply sent, or the last post-report message unlinked or corrected away before any reply |
| Completed, Query | With Engineer | Return to Engineer (reason) |
| Not ready, Review, With Engineer, Completed, Query | With Engineer before the report | Create audit, once, on an Inspection + Audit Case |
| pre-report states | Principal cancelled, Collision Engineers rejected | Close case (reason) |
| any open state | Created in error | Corrected-Principal replacement action |
| any open state | Source email unlinked | Unlink the source email |
| closed disposition | archived | Archive |
| closed disposition | working state | Return to work (reason), through the normal gates |

## Edge cases and fail-closed behaviour

- A readiness claim posted from a screen is ignored; only stored facts count.
- Pending or failed filing never clears the Images blocker.
- A stale lease, stale version or unauthorised actor changes nothing.
- Close case with an unknown outcome is refused.
- A second archive on an archived Case is refused.
- A chase already calculated keeps its date when the interval changes.
- A cancellation message never changes state without a staff or Automation
  Actor action.
- Create audit on a Held, closed or archived Case, without an assigned
  Engineer, or a second time, is refused; the listed item is greyed out and
  states the refusal on hover.
- Sent evidence for the Audit report that predates the Audit, or that proves
  the Inspection report, is refused.

## Acceptance evidence

Core tests cover every transition in the table above, readiness from stored
facts, chase scheduling across the Held boundary, the four dispositions, and
Create audit's offer and refusal in each state. Integration tests cover Assign
Engineer under a lease, Mark report sent against retained Sent evidence,
Create audit with its replay and refusals, and Archive. Deployment and live
acceptance are separate evidence tiers
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `CASE-01`, `CASE-13`–`CASE-20`, `CASE-24`–`CASE-26`,
  `CASE-32`, `EXT-05`, `MAIL-18` in [capabilities](../capabilities.md).
- Related FRDs: [FRD-01](frd-01-case-identity-and-lifecycle.md),
  [FRD-02](frd-02-intake-and-source-identity.md),
  [FRD-22](frd-22-pre-case-gates-matching-and-association.md),
  [FRD-04](frd-04-parties-accounts-and-access.md),
  [FRD-07](frd-07-case-export.md),
  [FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md),
  [FRD-14](frd-14-record-edit-leases.md),
  [FRD-16](frd-16-case-record-workspace.md),
  [FRD-17](frd-17-administration-workspace.md),
  [FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md).
- Technical constraints:
  [ADR-0020](../adr/0020-accepted-qdos-case-association-predicates.md),
  [ADR-0065](../adr/0065-case-export-replaces-eva-routes.md),
  [ADR-0056](../adr/0056-one-case-per-work-data-and-triage-case-type.md).
