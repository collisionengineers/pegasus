# FRD-13: Case lifecycle and workflow

> Owner capabilities: CASE-13 to CASE-20, CASE-24 to CASE-27, CASE-30, CASE-32, MAIL-18 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- A Case moves `Not ready` → `Review` → `With Engineer` → `Completed`.
  `Query` is a return trip from Completed. `Held` is a pause.
- Not ready becomes Review by itself when every required instruction item
  and image is present. Nobody ticks a "reviewed" box.
- **Hand to Engineer** is the only way from Review to With Engineer. Sending
  work to EVA never moves a Case.
- **Mark report sent** needs a real Sent email item. **Mark completed** can be
  undone. A Case is never permanently closed.
- **Close case** records a cancellation or a rejection with a reason.
  **Archive** hides a closed Case from queues. Nothing is ever deleted.

## Purpose

This document owns how a Case moves through work: its states, what unlocks
each step, the actions staff can take, chasing for missing material, and what
happens after the report. Identity and Case types are in
[FRD-01](frd-01-case-identity-and-lifecycle.md). Edit leases are in
[FRD-14](frd-14-record-edit-leases.md).

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

A Case can also carry one of four closed dispositions: `Provider cancelled`,
`Collision Engineers rejected`, `Created in error` and `Source email
unlinked`. Each records a reason. The Case stays on record and can be
returned to work with a reason. No Case is ever shown as "Closed".

Every state change and disposition is a Core transition. It records who did
it, when, why, the state before and after, and any evidence, permanently.
Screens, the Worker, APIs and MCP tools all call the same Core use cases;
none of them has its own version of the rules.

### Readiness and Review

**Blockers are specific.** Each unmet requirement is one named blocker. The
screen shows exactly which field or material is missing, where it should come
from, why it is required, and what would clear it. Pegasus never shows an
overall score, a percentage, or a summary such as "no unresolved field
reviews".

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
Filing that is pending or failed does not clear the Images blocker. This
completion is recorded once, with its actor; running it again does not
override a later staff change. It does not clear other blockers, assign an
Engineer, or skip Review.

**Saving does not unlock.** An action is available exactly when its stated
prerequisites are met. Saving unchanged or unrelated data never unlocks an
action and never resets readiness, lifecycle or advisory state.

### Hand to Engineer

In Review, staff choose **Hand to Engineer** and pick an eligible Pegasus
Engineer. In one operation, under the current Case edit lease and version,
Pegasus assigns the Engineer, sets the Sign-off Engineer, and moves the Case
to With Engineer. An Engineer may use **Assign to me** wherever the same
assignment would be accepted. A headless start command can hand a Review Case
to its already-assigned eligible Engineer; it is not a second screen step.

Replaying the same request does not hand off twice. A request that is
incomplete, stale or unauthorised changes nothing.

This is the only route out of Review. EVA work is optional in Review or With
Engineer and follows the Principal's report-generation policy
([FRD-07](frd-07-eva-and-external-engineering-handoff.md)). Sending to EVA,
by ZIP or by API, never changes the Case state and never proves that EVA
received or assigned anything.

### Actions

- **Place on Hold** records a reason and an optional **Review on** date
  (today or later, a Europe/London calendar date). The held Case is due for
  its decision at the end of that date. Without a date it is due at the Held
  decision target after the hold was placed. The record reads "Held · review
  on 24 Sep". **Release Hold** records a reason.
- **Damage, Valuation, Estimate, Settlement and Report** can always be
  viewed. Staff with `PerformCasework` may edit them in Not ready, Review and
  With Engineer under the normal edit authority. They are read-only in Held
  and Completed. Adopting the Engineer's Value is an Engineer act.
- **Report approval** names one immutable report file and the staff member
  who approved it.
- **Mark report sent** needs exact retained Sent evidence
  ([FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md#outbound-correspondence-evidence)).
  A generated file, an export, a draft, a queue result or a manual statement
  is not enough. Report sent moves the Case into its post-report phase,
  which screens still show as With Engineer.
- **Mark completed** records that the current work is complete. It is a
  reversible work state, not a closure.
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
`Provider cancelled` and `Collision Engineers rejected`. A reason is
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
A draft or an acknowledgement is not a reply. Pegasus keeps the received
query and the actual reply as Case correspondence. Neither transition erases
earlier completion or query history. Further engineering edits go through
Return to Engineer.

### Sign-off Engineer

Sign-off Engineer is a Case field beside Engineer. Only staff accounts
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
deadline.

**Chase interval.** One global setting in Workflow configuration, in whole
calendar days, range 1 to 365, default 7, calculated in Europe/London. When a
Case enters Not ready, the first chase falls that many days later at the same
local time, and repeats at the same interval. Changing the setting affects
schedules calculated after the change; a chase already calculated keeps its
date. Held keeps the remaining interval, and release to Not ready resumes it.
Review, accepted material arriving, completion, or a cancellation or
rejection stops the chase schedule.

**Chasing is manual.** A staff member sends each chaser. Pegasus records what
was attempted, by whom, through which channel, to which party and address,
when, and with what evidence. A recorded chase is not proof that it was
delivered. Each chaser keeps its recipient, channel, prepared draft or draft
reference, staff disposition and timestamps. Free-text notes may sit beside a
chaser without implying it was sent or answered.

**What staff see.** For each item awaiting material, the work view shows the
missing-material reason, Due by, the next chase, the most recent channel and
outcome, an optional note, and the next permitted action. Prepared or copied
text always looks different from sent, delivered, answered or completed work.

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
support a staff action on a pre-report Case: place it on Hold with the
cancellation as the reason, confirm `Provider cancelled`, or release it.
Release needs the message recategorised, unlinked or reassociated first. Every
original and corrected classification, with actor, time, reason and evidence,
stays in history.

## States and transitions

| From | To | Trigger |
| --- | --- | --- |
| Not ready | Review | Every required item present (automatic) |
| Review | With Engineer | Hand to Engineer, Assign to me, or the headless start command |
| Not ready, Review, With Engineer | Held | Place on Hold (reason) |
| Held | previous state | Release Hold (reason) |
| With Engineer | Completed | Mark completed |
| Completed | Query | Query received or attached |
| Query | Completed | Reply sent |
| Completed, Query | With Engineer | Return to Engineer (reason) |
| pre-report states | Provider cancelled, Collision Engineers rejected | Close case (reason) |
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
- A cancellation message never changes state without a staff action.

## Acceptance evidence

Core tests cover every transition in the table above, readiness from stored
facts, chase scheduling across the Held boundary, and the four dispositions.
Integration tests cover Hand to Engineer under a lease, Mark report sent
against retained Sent evidence, and Archive. Deployment and live acceptance
are separate evidence tiers
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `CASE-13`–`CASE-20`, `CASE-24`–`CASE-27`, `CASE-30`,
  `CASE-32`, `MAIL-18` in [capabilities](../capabilities.md).
- Related FRDs: [FRD-01](frd-01-case-identity-and-lifecycle.md),
  [FRD-02](frd-02-intake-and-source-identity.md),
  [FRD-22](frd-22-pre-case-gates-matching-and-association.md),
  [FRD-04](frd-04-parties-accounts-and-access.md),
  [FRD-07](frd-07-eva-and-external-engineering-handoff.md),
  [FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md),
  [FRD-14](frd-14-record-edit-leases.md),
  [FRD-16](frd-16-case-record-workspace.md),
  [FRD-17](frd-17-administration-workspace.md),
  [FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md).
- Technical constraints:
  [ADR-0020](../adr/0020-accepted-qdos-case-association-predicates.md),
  [ADR-0048](../adr/0048-principal-report-generation-policies.md).
