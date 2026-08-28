# FRD-01: Case identity and lifecycle

## Unidentified boundary

Unidentified material never allocates a Case/PO, Principal identity, or Audit
reference. Missing, conflicting, or ambiguous identity-critical evidence is retained
under its immutable `U<n>` reference with a canonical reason; only a later authorised
resolution can link it to a supported destination, without changing that U-reference.
> Owner capabilities: CASE (principal/reference identity, case types, lifecycle, edit/recovery, chasing) · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · UI behaviour: docs/design/README.md

### Principal, reference, organisation, and case-party identity

- Principal and internal reference are immutable after allocation.
- Reference allocation occurs once safe source processing establishes an unambiguous Principal and Case type and all identity-critical gates pass. Incomplete ordinary business detail, images, or required external checks create or retain the Case as `Not ready`; they do not leave a valid instruction pre-Case.
- The normal Case/PO is `{principal code}{YY}{shared sequence}` with a three-digit minimum: `001` through `999`, then `1000` through `9999`. Inspection, standalone Audit, and Inspection + Audit consume one principal/year sequence. Exhaustion at `9999` is visible and blocks allocation; references and sequence values never wrap or return to use.
- An Audit requires two separate document attachments: the Audit instruction and the original report to be audited. Pegasus reads the literal outcome in that original report: `repairable` derives `a.{Case/PO}` and `total loss` derives `ap.{Case/PO}`. A missing, conflicting, or ambiguous report is `Needs sorting`; it does not create a case or reference. No staff confirmation is an intake gate.
- Inspection + Audit begins with the normal Inspection Case/PO reference. After Collision Engineers’ Engineer produces the later Audit report through EVA, the Engineer manually creates the applicable `a.{Case/PO}` or `ap.{Case/PO}` Box subfolder under that existing Box folder. Pegasus does not create that later folder until it replaces EVA under a separately accepted integration.
- A used principal code is replaced by one linked successor in an atomic Core transaction: deactivate the predecessor, continue its next unused sequence in the Europe/London cutover year, and begin later years at `001`. Both identities and the reason remain permanent.
- A wrong-principal case closes as `Created in error`, with a reason and a linked replacement. Neither reference is reused; the original never reopens.
- A case is never deleted. Reopening requires a reason and the normal destination gates.
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

A case owns immutable identity, principal, internal reference, type, accepted source links, snapshotted parties/addresses, vehicle identity, work state, due work, documents, correspondence, findings, decisions, action history, and closure history.

### Lifecycle closure and correspondence

The lifecycle must support:

- pre-case receiving, and the sorting of material that is not definitive (this is the `Needs sorting`/`Blocked intake` path and its reasoned resolution, not a manual acceptance step applied to definitive intake — see the allocation rule above);
- active work, `Not ready`, `Held`, `Review`, due-work visibility, and separate mandatory instruction-completeness, image-completeness, and staff-review gates before Engineers-queue eligibility; provider policy may define accepted gate evidence but may not remove a gate;

- manual chasing with the exact schedule below;
- inspection/report preparation appropriate to desktop assessment;
- report approval and delivery evidence without adding a separate pre-send case-review gate;
- post-report queries, corrections, addenda, disputes, and reasoned closure where allocated;
- four distinct instructed-Case terminal outcomes: `Post-report complete`, `Provider cancelled`, `Collision Engineers rejected`, and `Created in error`; Image-initiated merge/closure is a separate image-origin lifecycle outcome, not a fifth formal Case closure state;
- reasoned reopen through normal destination gates, excluding `Created in error` and `Held` as a reopen destination.

Each unmet progression requirement is an individual actionable blocker. The UI identifies its exact field or material, source/provenance, reason, and permitted resolution; an opaque aggregate such as “no unresolved field reviews” is prohibited. An action is enabled exactly when its current explicit prerequisites are satisfied. Saving unchanged or unrelated data must neither unlock it nor reset lifecycle, readiness, or advisory state.

Durable receipt acknowledgement, retained correspondence, prepared or copied text, the `First sent to Engineer` export proxy, and a `Report sent` event are not terminal case outcomes. Report-sent evidence enters post-report work; post-report completion is a separate named closure action.

The named Core workflow records the policy key and version used for every configured readiness gate. It permits Engineer assignment only when the configured instruction-completeness, image-completeness, instruction-review, and image-review gates each pass; no caller, assignment, prepared artifact, or later workflow event supplies a missing gate by implication. A Report approval identifies one immutable artifact and its approving staff actor. `Report sent` requires one retained exact approved-mailbox Sent item with its mailbox/Sent-folder scope, immutable item, conversation/reply-chain identities, authoritative Sent time, and separate link time; an assertion, draft, queue result, generated file, or export proxy fails closed.

Every closure selects exactly one named terminal outcome, records the authenticated actor, time, reason and prior/new state in permanent history, and leaves the Case, Case/PO, source relationships, and closure chronology intact. A closed case and its files remain application-level read-only until an authorised, reasoned reopen passes the normal destination gates. `Created in error` never reopens.

Every Image-intake association, reversal, or correction records the same attributable relationship evidence without closing or creating a Case. The Case, Case/PO, Image Intake Reference, source relationships, and chronology remain intact.

An Image-initiated Case is a separate image-first lifecycle projection over the
ImageIntake record. It never allocates a Principal, Case/PO, or formal Case row.
Its immutable VRM reference remains visible when the record is merged into one
eligible Instruction-initiated Case. Merge and staff closure are named,
reasoned history events; the formal Case history shows the merged reference and
the original image record shows its formal Case target.

State changes are explicit Core transitions. UI labels, Worker handlers, APIs, and MCP tools call the same use cases; they do not implement parallel policy.

When a Case passes its staff-review gate, it becomes visible in the Engineers
queue. Assignment is optional: staff may assign an eligible Pegasus Engineer
directly when required, or an Engineer may take an unassigned case when starting
work. Assignment is ownership, not readiness, and never substitutes for a
missing Review or export gate. Pegasus assignment does not prove EVA receipt or
an external EVA assignment.

Incoming cancellation classification or association never changes a Case automatically. In the focused alpha, mailbox processing records the settled classification for every route-accepted received message and may automatically associate QDOS-direct correspondence with its Case under the accepted ADR-0020 predicates, but only an incoming instruction creates intake work and no classification or association mutates Case state; a separately retained and reasonedly associated cancellation message may support an authorised staff action to place a pre-report Case in `Held pending staff decision`, confirm `Provider cancelled`, or release it. Release requires the message to be reasonedly recategorised, unlinked, or reassociated first. Every original and corrected classification/association, actor, time, reason, and evidence remains permanent history.

### Workflow display labels and stage-bound actions

Core lifecycle states are unchanged. The operator sees display labels only,
owned by the single code-to-words map
`Pegasus.Web.Presentation.OperatorLabels` named in the design README's
[Enforced presentation rules](../design/README.md#enforced-presentation-rules):

| Core state | Display label |
| --- | --- |
| `Not ready` | Not ready |
| `Review` | Review |
| `Report preparation`, `Post report` | With Engineer |
| `Post-report complete` | Complete |
| `Held` | Held (exception, never a workflow step) |
| `Provider cancelled`, `Collision Engineers rejected`, `Created in error`, source e-mail unlinked | Closed · `<outcome>` |

The Cases workflow rail lists Not ready, Review, With Engineer and Complete,
with Held as an exception group; the other terminal outcomes never appear in
that rail and render as Closed · `<outcome>` in Search. A label is never a
state: every transition remains a named Core action, and history records the
Core state, not the label.

- **Send to EVA** is offered in Review. The handoff carries the Engineer
  assignment and offers ZIP export or API submission; the package, mapping,
  and per-Principal submission policy are owned by
  [FRD-07](frd-07-eva-and-external-engineering-handoff.md#eva-and-external-engineering-handoff).
- **Assessment** availability by state is owned by
  [FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#report-draft-entry-point):
  With Engineer or onwards, never Review, read-only once Complete.
- **Report sent** is evidence-driven; no manual "sent" assertion exists. A
  report sent from Pegasus through an approved mailbox
  ([FRD-08 outbound correspondence](frd-08-email-mailbox-and-background-processing.md#outbound-correspondence-evidence))
  links its retained Sent item to the Case automatically. A report sent
  through EVA is detected by the approved-mailbox report-mail match — the Case
  reference and a PDF attachment — whereupon the PDF is attached to the Case
  as the report document and the Sent item is linked. Either path enters
  post-report work, still displayed as With Engineer. The Case action offers
  only confirmation of detected evidence; `Post-report complete` remains the
  separate, reasoned closure that ends post-report work.
- **Return to Engineer** is the reasoned reopen from Complete to Report
  preparation; it requires an assigned Engineer and the normal destination
  gates.
- **Close Case** is offered while the Case is not Complete and selects one
  named terminal outcome as above.

### Case edit authority and recovery

Every staff case mutation targets one identified case through a named Core action and requires the role permitted by the [staff role access matrix](frd-04-parties-accounts-and-access.md#staff-role-access-matrix). Entering edit mode acquires the case’s one server-owned expiring lease. Other authorised staff remain read-only and can see the holder and recovery state. Every save, transition, assignment, association, evidence change, and other staff mutation presents both the lease token and the Case version loaded by that editor.

The holder may leave editing; an abandoned lease expires by server time and may then be reacquired. Core refuses a missing, expired, wrong-holder, or stale-version mutation without overwriting newer work. The rejected editor keeps proposed values for comparison and must reload and reacquire rather than merge or force the save. There is no Administrator bypass, forced takeover, collaborative merge, bulk case mutation, queue-inline lifecycle edit, provider case-edit route, or direct external-system or adapter edit.

Web and MCP Automation Actor callers use the same guard. Background append-only receipt, dispatch, and document-processing records remain separate from editable Case state and cannot bypass Case versions to alter it. A deliberate recovery or material denial/failure is attributable permanent history; routine renewal, expiry, heartbeat, polling, and adapter mechanics remain telemetry.

### Due work, chasing, and action history

`Due by` comes from the inspection date or accepted equivalent deadline. For a case entering `Not ready`, the first chase occurs at the same Europe/London local time seven calendar days later and repeats every seven calendar days. `Held` preserves the remaining interval; release to `Not ready` resumes it. `Review`, accepted material arrival, or terminal closure stops the schedule.

Manual chasing remains a staff action in the alpha unless an allocated capability and accepted integration explicitly authorize automation. The history records what was attempted, by whom, through which channel, against which party/address, when, and with what evidence. A recorded action is not proof of external delivery.

Each chaser retains its recipient, channel, prepared draft or draft reference,
staff disposition, and attributable timestamps. Free-text notes may accompany a
structured chaser without implying that it was sent or answered.

For each item awaiting material, the current work projection keeps the
missing-material reason, `Due by`, next chase, most recent recorded
channel/outcome, optional note, and next permitted action together. Prepared or
copied text remains visibly distinct from sent, delivered, answered, or
completed work.
