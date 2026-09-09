# FRD-01: Case identity and lifecycle

## Unidentified boundary

Unidentified material never allocates a Case/PO, Principal identity, or Audit
reference. Missing, conflicting, or ambiguous identity-critical evidence is retained
under its immutable `U<n>` reference with a canonical reason; only a later authorised
resolution can link it to a supported destination, without changing that U-reference.
> Owner capabilities: CASE (principal/reference identity, case types, lifecycle, edit/recovery, chasing) · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · UI behaviour: docs/design/README.md

### Principal, reference, organisation, and case-party identity

- Principal and internal reference are immutable after allocation.
- Reference allocation occurs once safe source processing establishes an unambiguous Principal and Case type and all identity-critical gates pass. Manual upload additionally requires explicit staff acceptance under [FRD-02](frd-02-intake-and-source-identity.md); its extracted proposal remains pre-Case with no reserved Case/PO until acceptance. Incomplete ordinary business detail, images, or required external checks create or retain an accepted Case as `Not ready`; they do not otherwise leave a valid instruction pre-Case.
- The normal Case/PO is `{principal code}{YY}{shared sequence}` with a three-digit minimum: `001` through `999`, then `1000` through `9999`. Inspection, standalone Audit, and Inspection + Audit consume one principal/year sequence. Exhaustion at `9999` is visible and blocks allocation; references and sequence values never wrap or return to use.
- An Audit requires two separate document attachments: the Audit instruction and the original report to be audited. Who states that report's outcome depends on the route. On the retained-email route Pegasus reads the literal outcome in the report itself: `repairable` derives `a.{Case/PO}` and `total loss` derives `ap.{Case/PO}`, and missing, conflicting or ambiguous original-report evidence withholds only the later Audit reference. A definitive instruction still creates the normal Case/PO once Principal and identity-critical gates pass. On the Provider API route the authenticated Principal declares the verdict and that declaration derives the reference (operator decision, 2026-08-28); the original report is still required as an attachment, because the Engineer needs the report they are auditing, but it is not parsed to decide the prefix. No staff confirmation is an intake gate on either route.
- Inspection + Audit begins with the normal Inspection Case/PO reference. After Collision Engineers’ Engineer produces the later Audit report through EVA, the Engineer manually creates the applicable `a.{Case/PO}` or `ap.{Case/PO}` Box subfolder under that existing Box folder. Pegasus does not create that later folder until it replaces EVA under a separately accepted integration.
- A used principal code is replaced by one linked successor in an atomic Core transaction: deactivate the predecessor, continue its next unused sequence in the Europe/London cutover year, and begin later years at `001`. Both identities and the reason remain permanent.
- A wrong-principal Case records `Created in error`, a reason and a linked
  replacement. Neither identity changes and neither reference is reused. This
  correction is a recorded disposition, not an irreversible Case closure.
- A Case is never deleted or terminally closed. A return to engineering work
  records a reason and uses the normal destination gates; the query cycle below
  does not require a separate manual reopen.
- Principal is the instructing and paying party. An Intermediary supplies a route without thereby becoming Principal. Repairer identifies the vehicle holder or repair organisation; Image Source identifies the actual supplier of images. One organisation may hold several case roles, but an ambiguous sender never establishes Principal.
- Every case snapshots the inspection address, organisation identities, and party roles accepted for that case. Later reusable-directory corrections never rewrite historical case evidence.
- Source messages, files, visible placements, attachments, images, and subsequent correspondence retain stable source identities and provenance.
- Hashes may correlate equal bytes, but never replace visible placement or occurrence identity.
- Historical correspondence is not reconstructed into synthetic historical cases. New correspondence about historical work may be handled under the current process with explicit provenance.

## Case identity and lifecycle

### Case types

The active alpha types are:

- **Inspection:** Collision Engineers prepares accepted work for its Engineer’s desktop assessment and returns that Engineer’s report to the provider.
- **Audit:** another engineering firm has already inspected the vehicle; Collision Engineers receives that firm’s original Engineer report with the Audit instruction and audits or double-checks the work.
- **Inspection + Audit:** Collision Engineers completes an Inspection report and then immediately performs a distinct Audit of that report in the same Case; the Audit retains its own identity, evidence, and acceptance boundary.

Diminution and Commercial remain deferred unless their capability rows and activation evidence say otherwise. They are not active alpha aliases or generic case types.

A case owns immutable identity, principal, internal reference, type, accepted
source links, snapshotted parties/addresses, vehicle identity, work state,
due work, documents, correspondence, findings, decisions, action history, and
closure history. It also owns its assigned Engineer and Sign-off Engineer
(D31), its one Case Notes history, and its storage location and inspect-at
choice (D33).

### Lifecycle closure and correspondence

The lifecycle must support:

- pre-case receiving, and the sorting of material that is not definitive (this is the `Unidentified`/`Blocked intake` path and its reasoned resolution, not a manual acceptance step applied to definitive intake — see the allocation rule above);
- active work, `Not ready`, `Held`, `Review`, due-work visibility, and
  mandatory instruction- and image-completeness before Review; there is no
  separate staff act of reviewing instructions or images (D44, 2026-09-03);

- manual chasing with the exact schedule below;
- inspection/report preparation appropriate to desktop assessment;
- report approval and delivery evidence without adding a separate pre-send case-review gate;
- post-report queries, corrections, addenda, disputes, and reasoned closure where allocated;
- `Completed` and `Query` as reversible post-report work states; cancellation,
  rejection and Created in error remain reasoned history dispositions, not
  terminal closure states;
- a reasoned return to engineering through the normal destination gates.
  Image-intake merge/staff closure is a separate pre-Case lifecycle.

Each unmet progression requirement is an individual actionable blocker. The UI identifies its exact field or material, source/provenance, reason, and permitted resolution; an opaque aggregate such as “no unresolved field reviews”, and a completeness percentage or score of any kind, are prohibited. Instruction completeness and image completeness are each a versioned set of required and not-required items with exact named blockers (D23, 2026-09-01): the configuration records which items are required, the gate names every required item that is unmet, and nothing is reduced to a single figure. An action is enabled exactly when its current explicit prerequisites are satisfied. Saving unchanged or unrelated data must neither unlock it nor reset lifecycle, readiness, or advisory state.

A receipt acknowledgement, prepared text, export or generated file does not
prove report sending or complete a Case. Post-report work can be marked
`Completed`; this is reversible work status, never terminal closure.

The named Core workflow records the policy key and version used for readiness.
Review-gated transitions evaluate instruction and image completeness from
persisted facts inside the transaction; posted readiness claims are not
authority, and staff-confirmation checkboxes are retired (CASE-046, PLAT-072).
Complete instructions and images move a Case from Not ready to Review. In
Review, **Hand to Engineer** is the review action: selecting an eligible
Engineer assigns the Case and moves it to With Engineer atomically. No
separate reviewed checkbox or start-work action is required. No EVA export
or submission is required (CASE-049, current operator direction). A Report
approval identifies one immutable artifact and its approving staff actor.
`Report sent` requires one retained exact
approved-mailbox Sent item with its mailbox/Sent-folder scope, immutable item,
conversation/reply-chain identities, authoritative Sent time, and separate
link time; an assertion, draft, queue result, generated file, or export proxy
fails closed.

Every Case disposition records the actor, time, reason, prior/current state and
retained evidence in permanent history. Completed Cases accept query receipt
or attachment and move to `Query`; replying to the query returns the Case to
`Completed`. Neither transition erases earlier completion or query history.

Every Image-intake association, reversal, or correction records the same attributable relationship evidence without closing or creating a Case. The Case, Case/PO, Image Intake Reference, source relationships, and chronology remain intact.

An Image-initiated Case is a separate image-first lifecycle projection over the
ImageIntake record. It never allocates a Principal, Case/PO, or formal Case row.
Its immutable VRM reference remains visible when the record is merged into one
eligible Instruction-initiated Case. Merge and staff closure are named,
reasoned history events; the formal Case history shows the merged reference and
the original image record shows its formal Case target.

An existing Image-initiated Case is read-only until an authorised staff member
selects Edit. The resulting record-scoped edit lease has the same five-minute
duration and one-minute heartbeat convention as Case editing. Principal changes,
staff closure and a staff-directed merge recheck the holder, token and current
lifecycle version in their mutation transaction; Save releases the lease and
Cancel changes nothing. Automatic image processing remains a SystemWorker path
and does not impersonate a staff edit lease.

State changes are explicit Core transitions. UI labels, Worker handlers, APIs, and MCP tools call the same use cases; they do not implement parallel policy.

When a Case has complete instructions and images, it enters Review. Staff
hand it to an eligible Pegasus Engineer under the current Case edit lease
and version. Assignment, Sign-off Engineer selection and entry to With
Engineer are one attributable operation, making native engineering work
available without EVA. Exact replay does not hand off twice. Incomplete,
stale or unauthorized requests change nothing. An existing headless start
command can hand a Review Case to its already-assigned eligible Engineer;
it is not a second UI step. Neither route proves EVA receipt or an external
EVA assignment.

Incoming cancellation classification or association never changes a Case
automatically. Mailbox processing records the settled classification for
route-accepted received messages. Automatic principal-scoped association uses
the supported current-instruction profiles and unambiguous typed match keys
defined in [FRD-02](frd-02-intake-and-source-identity.md); QDOS retains its
accepted correspondence predicates under ADR-0020. This does not enable
arbitrary non-QDOS correspondence or extend QDOS cancellation recognition to
other principals. Current-envelope boundaries still exclude quoted historical
instructions. Only an incoming instruction creates intake work, and classification alone does not mutate Case state. A query received for or
attached to a Completed Case follows the explicit Query transition below. A separately retained and
reasonedly associated cancellation message may support an authorised staff
action to place a pre-report Case in `Held pending staff decision`, confirm
`Provider cancelled`, or release it. Release requires the message to be
reasonedly recategorised, unlinked, or reassociated first. Every original and
corrected classification/association, actor, time, reason, and evidence remains
permanent history.

### Workflow display labels and stage-bound actions

The required workflow labels are `Not ready`, `Review`, `With Engineer`,
`Completed` and `Query`, with `Held` as an exception. Use the single presentation
map for code-to-words translation; existing enum names do not override these
requirements. No formal Case appears as terminally Closed.

- **Hand to Engineer** in Review assigns an eligible Engineer and starts native
  engineering work through the Case lease and version gates.
- **Send to EVA** is optional in Review or With Engineer. Download ZIP and Send
  via API share [FRD-07](frd-07-eva-and-external-engineering-handoff.md); the
  Principal must enable the API route. EVA never gates native work.
- Damage, Valuation, Estimate, Settlement and Report are always viewable.
  Engineering edits require With Engineer and the normal Case edit authority.
  No pre-assignment valuation check may depend on an Engineer-only edit.
- **Report sent** confirms retained exact Sent evidence under FRD-08; generation,
  an export or a manual assertion is insufficient.
- **Completed** records that the current work is complete. Query receipt or
  attachment moves a Completed Case to Query. Replying returns it to Completed.
- **Return to Engineer** records a reason and applies the destination gates
  when further engineering changes are required. It is not a prerequisite for
  receiving, attaching or replying to a query.
- Cancellation, rejection and Created in error record their reason and source
  without permanently locking the Case or deleting any evidence.

### Sign-off Engineer

Sign-off Engineer is a Case field beside Engineer (D31, 2026-09-02). Only
staff accounts flagged as Sign-off Engineer
([FRD-04](frd-04-parties-accounts-and-access.md#staff-accounts)) are offered.
The default is the assigned Engineer when that account is flagged, otherwise
A Patterson; the initial flagged accounts are held as application data, never
hard-coded. Reports render the Case's sign-off tuple — name, qualifications
and signature image
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#initial-renderer-activation)).
D31 supersedes D18 (2026-09-02): reports no longer render typed Engineer
identity alone, and the Engineer who issues a report is not thereby its
signatory.

### Case edit authority and recovery

Every staff case mutation targets one identified case through a named Core action and requires the role permitted by the [staff role access matrix](frd-04-parties-accounts-and-access.md#staff-role-access-matrix). Entering edit mode acquires the case’s one server-owned expiring lease. Other authorised staff remain read-only and can see the holder and recovery state. Every save, transition, assignment, association, evidence change, and other staff mutation presents both the lease token and the Case version loaded by that editor.

Editing is held for as long as the holder's editing session stays open, however long the work takes; the case workspace and the assessment surface enter the same one edit mode over the same lease. The holder may leave editing; an abandoned lease expires by server time and may then be reacquired. Because the moment editing becomes available again is therefore not knowable while a holder is present, a non-holder is told who is editing and is never given a time. Core refuses a missing, expired, wrong-holder, or stale-version mutation without overwriting newer work. The rejected editor keeps proposed values for comparison and must reload and reacquire rather than merge or force the save. There is no Administrator bypass, forced takeover, collaborative merge, bulk case mutation, queue-inline lifecycle edit, provider case-edit route, or direct external-system or adapter edit.

Web and MCP Automation Actor callers use the same guard. Background append-only receipt, dispatch, and document-processing records remain separate from editable Case state and cannot bypass Case versions to alter it. A deliberate recovery or material denial/failure is attributable permanent history; routine renewal, expiry, heartbeat, polling, and adapter mechanics remain telemetry. A lease retained before the holder's actor kind was recorded on the claim identifies nobody until it expires, so a holder still editing across that recording's deployment is refused heartbeat and save for at most the remaining lease lifetime.

### Due work, chasing, and action history

`Due by` comes from the inspection date or accepted equivalent deadline. The chase interval is one global whole-calendar-day value in workflow configuration, range 1 to 365 days, default 7, calculated in Europe/London (D23, 2026-09-01). For a case entering `Not ready`, the first chase occurs at the same Europe/London local time that many calendar days later and repeats at the same interval. A configuration change applies to schedules calculated after it; a chase already calculated keeps its date. `Held` preserves the remaining interval; release to `Not ready` resumes it. `Review`, accepted material arrival, completion, or a reasoned cancellation
or rejection stops the current missing-material chase schedule.

Manual chasing remains a staff action in the alpha unless an allocated capability and accepted integration explicitly authorize automation. The history records what was attempted, by whom, through which channel, against which party/address, when, and with what evidence. A recorded action is not proof of external delivery.

Each chaser retains its recipient, channel, prepared draft or draft reference,
staff disposition, and attributable timestamps. Free-text notes may accompany a
structured chaser without implying that it was sent or answered.

For each item awaiting material, the current work projection keeps the
missing-material reason, `Due by`, next chase, most recent recorded
channel/outcome, optional note, and next permitted action together. Prepared or
copied text remains visibly distinct from sent, delivered, answered, or
completed work.

## Post-report responsibility

The Engineer responds to queries or disputes from the Principal, third-party
insurer or claimant. There is no terminally closed Case state. Receipt of a
query for, or attachment of a query to, a Completed Case moves it to `Query`.
Replying to that query moves it back to `Completed`; a draft or acknowledgement
is not a reply. The linked correspondence and attributable transitions remain
in the Case history. Further engineering edits use the reasoned Return to
Engineer action and normal edit authority.
