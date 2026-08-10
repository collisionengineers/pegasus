# Report-renderer integration — consumers of a rendered report

This is a **draft supporting plan** for the `report-renderer-integration` task
(`task/report-renderer-integration`, taken 2026-08-03). It shares the master
plan's slug prefix, its planning-only scope, and its deletion by the post-merge
maintenance push. It changes no project, no runtime code, no capability status
and no capability band.

The existing plan set stops at the render seam. **Nothing in it plans anything
that consumes a rendered artifact.** This plan covers that chain: from "bytes
exist" to "a report was sent and its financial and management consequences were
recorded", including the `Later` capabilities.

The spine of this plan is one sentence from [requirements](../requirements.md):

> A Box report PDF, file upload, generated artifact, draft, queue result, or
> staff assertion alone proves neither sending nor external receipt.

Everything below is arranged so that sentence cannot be violated by accident.

## Verified basis

| Claim | Verification |
| --- | --- |
| Report correction/finality clause, including immutable artifact identity and hash | `docs/requirements.md:898-932` |
| "Report sent enters post-report work rather than closing the case" | `docs/requirements.md:909-910` |
| "A Box report PDF, file upload, generated artifact, draft, queue result, or staff assertion alone proves neither sending nor external receipt." | `docs/requirements.md:910-911` |
| Targeted-send transaction clause and the fee-note/invoice sentence | `docs/requirements.md:934-942` |
| CASE-23 is unresolved and no mailbox adapter may invent it | `docs/requirements.md:913-920`; `docs/open-decisions.md:235` |
| Sequencing: accepted report events/rendering precede `MAIL-17` and the `MI-*` path | `docs/requirements.md:53-56` |
| Report-sent evidence contract; confirmation proves only item existence | `docs/requirements.md:807-817` |
| Closed case and its files are read-only until reasoned reopen | `docs/requirements.md:403`, `:309` |
| Box failure after allocation retains the Case as `Not ready` with staff-initiated retry | `docs/requirements.md:401` |
| A finding correction must not create, alter, credit or void an invoice | `docs/requirements.md:539-543` |
| "Graph Sent-item evidence does not prove recipient delivery or automatic case matching." | `docs/operations.md#local-and-live-evidence-boundaries` |
| Every deferred UI capability re-enters the full design route | `docs/design.md:48` |
| UI-15 exists as routeless design markup | `docs/design.md:663-670` |
| UI-15 markup already carries a `Report content` section and no render/issue/send control | `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml:32`, `:715-772` |
| Lifecycle states, including `PostReport` and the four terminal outcomes | `src/Pegasus.Core/Workflow/CaseWorkflowContracts.cs:11-38` |
| `ReportApprovalEvidence` takes a caller-supplied `ArtifactIdentity` and `ArtifactSha256` | `src/Pegasus.Core/Workflow/CaseWorkflowContracts.cs:60-77` |
| The Web caller binds those two values from form input | `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs:501-527` |
| Approval is allowed only during `ReportPreparation` | `src/Pegasus.Core/Lifecycle/CaseLifecycle.cs:160-178` |
| Unlinking requires state `ReportPreparation` and the current association | `src/Pegasus.Core/Lifecycle/CaseLifecycle.cs:293-337` |
| Reopen to `PostReport` requires retained exact sent evidence; reopen to `ReportPreparation` requires an assigned Engineer | `src/Pegasus.Core/Lifecycle/CaseLifecycle.cs:505-523` |
| Matching uses reply-chain identities and `AuthoritativeCaseIdentities`, never content | `src/Pegasus.Core/Workflow/PollSentEvidence.cs:396-519` |
| Document semantic roles, sources, custody status, version identity | `src/Pegasus.Core/Documents/DocumentContracts.cs:5-74` |
| `AddCaseDocumentCommand` requires an expected case version and an edit-lease token | `src/Pegasus.Core/Documents/DocumentContracts.cs:77-88` |
| EVA bundle schema `eva-handoff-v2`; the proxy receipt disclaims delivery and assignment | `src/Pegasus.Core/Eva/EvaBundleSchema.cs:145-186` |
| `Principal` carries **no** CC, delivery or standing-note preference fields | `src/Pegasus.Core/Cases/CaseContracts.cs:19-28` |
| The renderer computes fee-note subtotal, VAT and total itself | `workspaces/report-renderer/src/CollisionRenderer.Core/Templating/HtmlComposer.cs:121-143` |
| `FeeNoteDocument.VatRate` defaults to `0.20m` inside the renderer model | `workspaces/report-renderer/src/CollisionRenderer.Core/Models/Documents.cs:153-165` |
| The dashboard `Reports sent` tiles render an absent status chip | `src/Pegasus.Web/Pages/Index.cshtml:111-124` |
| The last accepted ADR is 0020, not 0019 | `docs/adr/0020-accepted-qdos-case-association-predicates.md` |

Two facts found during this pass change the shape of the consumer chain and were
not anticipated by the plan set:

1. **The renderer performs fee arithmetic.** `HtmlComposer.FeeNote` sums the line
   items, computes VAT with `MidpointRounding.AwayFromZero`, and computes the
   total; the model carries a default VAT rate of `0.20m`. The seam plan's Core
   contract states that Core computes every figure once and the renderer performs
   no arithmetic, no rounding and no currency conversion. The `fee-note` template
   as imported contradicts that contract.
2. **Report approval today is a staff assertion about an external file.**
   `ReportApprovalEvidence` records a caller-supplied `ArtifactIdentity` string
   and a caller-supplied `ArtifactSha256`, both bound from a form post. Nothing
   observes the bytes. That is correct for a world in which the report is
   produced in EVA; it is the exact join that changes when Pegasus renders.

The seam plan's ADR placeholder says "the last accepted is ADR-0019". As of this
pass ADR-0020 exists, so the next available number is **0021**. That is a
correction to a sibling plan.

## Capabilities in scope

### The consumer chain proper

| ID | Band | Target | Durable outcome (quoted) |
| --- | --- | --- | --- |
| MAIL-17 | Later | 1.2.0 | "Idempotent report/fee-note send on the original Outlook thread or provider API using principal CC/delivery/standing-note preferences, followed by Box filing, completion, and management-event recording" |
| EXT-11 | Later | 1.2.0 | "Versioned fee/invoice and Engineer cost/payment inputs, accounting status, and role-restricted visibility" |
| MI-02 | Later | 1.2.0 | "Per-principal report counts, types, and periods feeding invoice generation" |
| MI-03 | Later | 1.2.0 | "Holding-pen age and instruction-to-images, ready-to-sent, and overall turnaround measures consuming accepted workflow events" |
| CASE-23 | Next | 0.4.0 | "Post-report query and dispute work on the existing case with retained report/reply-chain evidence and an explicit lifecycle" |
| MAIL-12 | Later | 0.5.0 | "Authenticated staff compose, reply, forward, and send email in Pegasus" |
| UI-15 | Later | 1.0.0 | "One case-centred progressive Engineer workbench for inspection, vehicle/damage, valuation, estimate/repairer, report, media, salvage, text, and administration" |
| CASE-22 | Later | 1.0.0 | "Replace EVA inspection and report-preparation work inside Pegasus" |

MAIL-17's note is load-bearing: *"Allocation only; exact destination, caller,
custody, Sent-item/reply evidence, finality, and recovery contract required."*
MI-03's: *"MAIL-17 owns report-send/completion event recording."* EXT-11's
forbids the inference this plan spends most of its length preventing: *"a
finding/report correction never infers an invoice change; invoice generation
consumes separately accepted events and rules."*

### Already accepted, and consumed by the chain

| ID | Band | Target | Durable outcome (quoted) |
| --- | --- | --- | --- |
| MAIL-14 | Now | 0.1.0-alpha.1 | "Detect an exact Outlook Sent item as report-sent evidence" |
| MAIL-15 | Now | 0.1.0-alpha.1 | "Manually link, unlink, or relink an exact Sent item with a reason" |
| MAIL-16 | Now | 0.1.0-alpha.1 | "Automatically match the exact report Sent item to its case" |
| CASE-24 | Now | 0.1.0-alpha.1 | "Post-report completion, provider cancellation, and Collision Engineers rejection outcomes" |
| DOC-02 | Now | 0.1.0-alpha.1 | "Store source emails, instruction documents, images, correspondence, and reports in Box" |
| DOC-03 | Now | 0.1.0-alpha.1 | "Retained document versions" |
| DOC-07 | Now | 0.1.0-alpha.1 | "Staff upload, view, download, and export actions" |
| UI-04 | Now | 0.1.0-alpha.1 | "New cases today, Sent to Engineer, and Reports sent day/week activity" |

MAIL-14 and MAIL-16 are explicitly not on the alpha path; post-report tracking
starts manual via MAIL-15. UI-04 is included because it is the one
already-accepted consumer of report-sent events with an operator surface, and
that surface is currently honest about having no data.

### The EVA handoff proxy — rigorously distinct

**A rendered report is not the EVA bundle, and no code path may treat one as the
other.**

| Dimension | EVA bundle (EXT-03) | Rendered report (EXT-08 / RPT-*) |
| --- | --- | --- |
| Schema | `eva-handoff-v2`, thirteen ordered keys plus images plus `manifest.sha256` | `pegasus-report-v1` (draft), one PDF artifact |
| Direction | Pegasus → the Engineer, an **input** to engineering work | Engineering work → the principal, an **output** |
| Transport | Manual operator drag-and-drop; no EVA network call | Undecided; MAIL-17 when allocated |
| Business event | `First sent to Engineer` handoff **proxy**, once per case | `Report sent`, requiring exact Sent-item evidence |
| Delivery claim | The receipt explicitly sets `ClaimsExternalDelivery` and `ClaimsEngineerAssignment` so the record states what it does not prove | No proxy exists |
| Regeneration | Later generations are **revisions** of the same handoff | A correction or addendum is a **new issue** retaining every earlier artifact |

CASE-22 would eventually collapse part of that distinction. Until it is accepted,
`NOW.md` records the current path as "EVA keeps engineering and reports".

## The four claims

| # | Claim | Proved by | Explicitly not proved by |
| --- | --- | --- | --- |
| 1 | **An artifact was generated** | A `RenderedReportArtifact` whose `ArtifactSha256`, `ContentLength`, template binding, payload hash and figures-policy version were observed by Core at render time | A template existing; a preview composing; a payload validating; a queue result |
| 2 | **The artifact is in Box custody** | A `DocumentVersion` at `Confirmed` under the case's custody root, with a remote id, content hash and ETag | The artifact existing; a `Pending` status; a staff upload; a local content-store write |
| 3 | **A report was sent** | Exactly one retained approved-mailbox Sent item, linked to exactly one case, with mailbox and Sent-folder scope, immutable item, conversation and reply-chain identities, and Outlook `sentDateTime` as the business time | Claims 1 and 2 in any combination; a send request; a provider `202`; a staff assertion; a Box file |
| 4 | **The principal received it** | **Nothing in Pegasus.** `docs/operations.md#local-and-live-evidence-boundaries`; `docs/requirements.md:813` | Everything above |

Claim 4 has no evidence route at any tier and this plan does not invent one. A
reply arriving on the same chain is evidence of a reply — a separate retained
fact — not retrospective proof of delivery.

Claim 3 is also **final against later mutation of its own source**:
`docs/requirements.md:906-908` states the event "remains final if Outlook later
moves or deletes the item", and the poller already models `Moved` and `Deleted`
as recorded outcomes rather than retractions.

## The event chain

| # | Event | Owner | Capability | State |
| --- | --- | --- | --- | --- |
| 1 | An accepted structured case/engineering record version exists | `Pegasus.Core.Cases` (future) | CASE-31, ENG-01, ENG-02 | Allocated only |
| 2 | Core computes every presentation figure once, under a named policy key and version | `Pegasus.Core.Reports` (future) | RPT-01 | Allocated only |
| 3 | An authorised staff action requests a render for one case, kind and issue identity | `Pegasus.Core.Reports` | EXT-08 | Allocated only |
| 4 | The wording gate is checked; an unaccepted set yields `RendererUnavailable` and no artifact | `Pegasus.Core.Reports` | a gate, not a capability | Drafted by the seam plan |
| 5 | An artifact is rendered; `RenderedReportArtifact` records issue identity, version, kind, template binding and hash | Core port; Infrastructure adapter | EXT-08, RPT-01–05 | Drafted by the seam plan |
| 6 | The report issue is persisted; every earlier artifact retained | `Pegasus.Core.Reports` (future) | EXT-08 | Allocated only; no table, no migration |
| 7 | The artifact becomes a managed case document version (`DocumentSource.Generated`) | `Pegasus.Core.Documents` | DOC-03 | Implemented for other sources; this path is new |
| 8 | Box custody confirms the version, or fails with staff retry | `Pegasus.Core.Custody` | DOC-02 | Implemented; the generated-report failure path is undefined |
| 9 | An authorised human approves exactly one immutable artifact | `Pegasus.Core.Lifecycle` | inside the accepted lifecycle | **Implemented and accepted** |
| 10 | A targeted send transaction submits the approved artifact idempotently | `Pegasus.Core` (future) | MAIL-17 | Allocated only |
| 11 | An exact Sent item is observed in an approved mailbox and retained unlinked | `Pegasus.Core.Workflow` | MAIL-14 | Implemented, not on the alpha path |
| 12 | The retained item is linked to exactly one case | `Pegasus.Core.Lifecycle` | MAIL-16 (auto), MAIL-15 (manual) | Implemented; MAIL-15 accepted |
| 13 | The case enters `PostReport`. **The case does not close.** | `Pegasus.Core.Lifecycle` | CASE-23 owns what follows | Transition **implemented**; the lifecycle is an **open decision** |
| 14 | Post-report work concludes in exactly one named terminal outcome | `Pegasus.Core.Lifecycle` | CASE-24 | **Implemented and accepted** |
| 15 | Financial and management consequences are recorded | future | EXT-11, MI-02, MI-03 | Allocated only |

Six properties that must survive implementation:

1. **Steps 5 and 9 are not the same act.** An artifact can exist without ever
   being approved; the correct operator presentation is "generated, not
   approved", never "ready to send".
2. **Steps 9 and 12 are not the same act, and neither depends on the other.**
   `LinkReportEvidence` requires only `ReportPreparation`; it does not require a
   recorded approval. That is deliberate today and a real gap once Pegasus
   renders — open question C4.
3. **Step 10 does not produce step 11.** A successful send transaction records
   that Pegasus asked. The business event arrives later, from the Sent folder.
4. **Step 13 does not close the case.** `docs/requirements.md:309-310`: a
   `Report sent` event is not a terminal case outcome.
5. **Every earlier artifact is retained at every step.** A correction re-enters
   at step 3 with a `SupersededIssueId`; it never overwrites steps 5–8 for the
   earlier issue, and never retracts step 12 for the issue actually sent.
6. **Step 15 consumes step 12, not step 5.** Counting rendered artifacts would
   let a re-render inflate a principal's report count.

### What exists today between steps 9 and 14

**The second half of the chain is already built.** Approval, retention,
automatic matching, manual link/unlink/relink, the `PostReport` state, the four
terminal outcomes, the reopen destination gates and the permanent history are
implemented Core actions with a Web caller. What does not exist is everything
from step 1 to step 8, plus step 10, plus step 15.

That asymmetry is the reason this plan exists. The renderer's arrival does not
build a new pipeline; it fills the front of an existing one, and step 9 is where
the two meet.

## The generated-report ↔ report-sent-evidence join

### What MAIL-14/15/16 assume today

They assume the artifact was produced elsewhere:

- `ReportApprovalEvidence` records `ArtifactIdentity` (free text, validated only
  as non-empty and ≤200 characters) and `ArtifactSha256` (validated only as
  well-formed). Both arrive as form fields. Nothing in Pegasus computed either.
- Automatic matching decides on **envelope facts only** — reply-chain identities
  and `AuthoritativeCaseIdentities` from the approved-mailbox source. It never
  inspects an attachment; the `MimeSha256` it retains hashes the MIME source
  occurrence, not any artifact inside it.
- Retained evidence has **no field for a report identity of any kind**.

### What changes when Pegasus generates the artifact

Exactly one thing changes with certainty, and it is not the matching rule. **The
approval submission stops being an assertion and becomes a reference.** When a
`ReportIssueId` exists with an `ArtifactSha256` Core observed at render time,
approval can be constrained to name that issue. The approval then means "this
human approved *this* issue", provably. That strengthens claim 1 plus a human
decision; it says nothing about claim 3.

Two further things become **possible** and must be decided rather than assumed:

**(a) The send transaction can record its own intent.** At submission MAIL-17
knows the case, the `ReportIssueId`, the approved destinations, and the outbound
message and conversation identities. Recording that tuple is exactly what
`docs/requirements.md:935-937` requires. A send-intent record is **not** evidence
of sending — it is the "queue result" that `docs/requirements.md:911` names by
that word.

**(b) The intent can supply the authoritative case identity to the matcher.**
For a Pegasus-originated send the case identity is known before the item exists.
Supplying it from the intent, keyed on the outbound message identity, makes
automatic matching deterministic without weakening anything: the matcher still
requires an exact item in the approved Sent scope, still requires exactly one
case identity, still yields `Ambiguous` on conflict.

### Does the artifact hash participate in matching?

**No, and it cannot today.**

1. **It is not observable from the evidence contract.** Nothing in the retained
   evidence exposes attachment bytes. Recomputing an artifact hash from a Sent
   item would need a new capability with its own mailbox scope, security review
   and failure contract.
2. **It would be the wrong kind of proof.** Hash equality proves a byte-identical
   file was attached to *some* item — not which case, issue or mailbox scope.
   `docs/requirements.md:100`: "Hashes may correlate equal bytes, but never
   replace visible placement or occurrence identity."
3. **It would create a bypass.** A staff member attaching the report to any
   message in the approved mailbox would manufacture a `Report sent` event.

**The recommended shape.** The artifact hash participates as an **annotation on
evidence already confirmed by the existing rule**, never as a term in the
confirmation:

- The matcher is unchanged.
- After a link commits, the send-intent record supplies `ReportIssueId` so the
  case can show *which issue* the confirmed send carried.
- If the intent is absent — a staff member sent by hand, which MAIL-15 already
  handles — the evidence is still valid and the annotation is simply absent.
  Absence must never downgrade the evidence.
- If an intent named issue X while the case's current issue is Y, that is a
  **displayed inconsistency for a human**, not an automatic unlink and not an
  automatic correction.

**Superseding must not retract evidence.** If issue 1 is sent and confirmed and
issue 2 is later rendered as a correction, the confirmed evidence still refers to
issue 1. The case's report history shows one sent issue and one unsent issue.
Nothing recomputes; nothing retracts.

## The reopen interaction that currently breaks

A concrete defect the chain will hit, found by reading the code.

To correct a report after it was sent, the case must return to
`ReportPreparation`. Two rules then collide:

- Reopening to `ReportPreparation` is permitted when an Engineer is assigned
  (`CaseLifecycle.cs:511-515`).
- `UnlinkReportEvidence` is permitted **only** while the state is
  `ReportPreparation` (`CaseLifecycle.cs:324-328`).

So the act of re-entering report preparation to produce a correction also makes
the original send evidence unlinkable. Unlinking evidence of a send that
genuinely happened would contradict `docs/requirements.md:906-907`. Nothing today
prevents it because no workflow currently returns a post-report case to report
preparation for a correction.

This is a lifecycle question inside CASE-23's open decision. Carried as C5.

## Fee note and invoice

### The arithmetic defect

`HtmlComposer.FeeNote` computes subtotal, VAT and total; `FeeNoteDocument.VatRate`
defaults to `0.20m`. Three problems in six lines:

1. **A money figure is computed in Infrastructure**, contradicting the seam
   plan's contract. Both cannot ship. This plan takes the seam plan's side.
2. **A rounding policy is expressed in a template helper.** VAT rounding on a
   supply is a finance decision with a right and wrong answer. It must be an
   accepted, versioned Core policy, not a default argument.
3. **A VAT rate has a default value**, so a fee note can render a
   plausible-looking total from an incomplete payload. The correct behaviour for
   a missing rate is refusal.

Before any fee note is issued, fee arithmetic moves to a Core figure policy with
a `PolicyKey` and `PolicyVersion`, and the renderer receives `subtotal`,
`vat_rate`, `vat` and `total` as literal pre-formatted strings.

### What rendering a fee note commits the business to

**Nothing.**

| Rendering a fee note **does** commit to | It does **not** commit to |
| --- | --- |
| Bytes existing, with an immutable identity and hash | A fee being charged |
| A record of which figures policy and template version produced them | A receivable, a debtor, or an accounting entry |
| A record of the actor and time | A principal having been billed |
| A retained artifact a later correction cannot overwrite | An invoice existing |
| Custody once filed | A management-information count |

`docs/requirements.md:938-940`: **"A correction does not silently alter an issued
fee note or invoice; later financial impact uses its own versioned, authorised
contract."** That sentence assumes a versioned, authorised financial contract
separate from the report. EXT-11 is that contract, and it is allocation-only.

### Four inferences to block by construction

1. **A rendered fee note is not an issued fee note.** "Issued" refers to a
   financial instrument under EXT-11, not to a PDF.
2. **A rendered fee note is not a sent fee note.** MAIL-17's "report/fee-note
   send" is the same transaction for both and needs the same Sent-item evidence.
3. **A rendered fee note is not an MI-02 count.** Counting renders rather than
   accepted send events would let a re-render, a correction or a failed send
   inflate a principal's invoice.
4. **A correction to a report is not a credit note.** The correct behaviour when
   a corrected report changes the financial picture is a separate, versioned,
   authorised human financial action.

## CASE-23 and the response templates

### The state of the decision

`docs/open-decisions.md:235` records what is unresolved: allowed
states/transitions and actors; case/report/reply-chain evidence;
correction/reopen and due/chaser interaction; response proof; closure; dispute
resolution. Its recommended default is preservation without invention.

CASE-23 is `Next / 0.4.0`, **before** the renderer's `1.1.0` band. The chain has
a segment scheduled earlier than its inputs and blocked on a decision rather than
on code.

### What the templates are

`part-35-response` and `response-letter` are both presets over
`expert_report.scriban` with model `ExpertReportDocument`, differing only in
display name, description and file-name suffix. Both map to **no Pegasus
capability**. They are letterhead-shaped layouts named after a lifecycle Pegasus
has not decided.

### What can be planned now

1. **Custody and identity are unconditional.** Any query-response document is an
   artifact with an immutable identity and hash, becomes a retained document
   version, and a revision is a new version retaining the earlier one.
2. **Sending one is the same event as sending a report.** There is exactly one
   `Report sent`-class event and it requires exact Sent-item evidence.
3. **The case is never created, replaced or re-referenced.**
4. **Correspondence is retained against the existing case regardless.**
5. **No mailbox adapter decides anything.** Classification may label an inbound
   message; labelling is not a lifecycle transition.

### What must not appear in any plan

Any new lifecycle state or transition; any rule about whether a query pauses,
extends or restarts due work and the chase schedule; any rule about whether
responding requires a reopen; any dispute-resolution outcome distinct from
CASE-24's four; any rule about whether a query response counts for MI-02 or
MI-03; any `DocumentSemanticRole` for a response document; any due-date, chaser
or escalation behaviour attached to a query.

### One thing already implied

The seam plan's draft `ReportKind` enum contains `Part35Response` and
`ResponseLetter`. Enum membership is a policy statement — the seam plan says so
itself. So the draft enum already asserts that Pegasus may issue documents for a
lifecycle it has not decided. Defensible, but it should be a conscious decision
rather than a side effect of importing twelve identifiers. Carried as C7.

## Custody

### The path from artifact to document version

```
RenderedReportArtifact (ArtifactSha256, ContentLength, FileName, MediaType)
   │
   ├─ AddCaseDocumentCommand
   │     Source                   = DocumentSource.Generated   (already exists)
   │     SemanticRole             = ??? (see below)
   │     SourceOccurrenceIdentity = derived from ReportIssueId
   │     ExpectedCaseVersion + EditLeaseToken required
   │
   ├─▶ DocumentVersion { Sha256, ContentLength, CustodyStatus = Pending }
   ├─▶ IDocumentContentStore.StoreAsync — verifies SHA-256 and length,
   │      treats identical content as replay rather than conflict
   └─▶ ICaseCustody / Box — CustodyDocumentVersion { RemoteId, ContentHash, ETag }
          CustodyStatus → Confirmed   (claim 2 is now provable)
                       → Failed       (staff-initiated retry)
```

**`DocumentSource.Generated` already exists** and is exactly right. No new source
member is needed.

**The semantic role is genuinely unresolved.** `DocumentSemanticRole` offers
`OriginalSource`, `Instruction`, `Image`, `Correspondence`, `EngineerReport`,
`AuditReport`, `Other`. `EngineerReport` fits an expert report and an addendum;
`AuditReport` fits audit output. **Nothing fits a fee note** — it is not an
Engineer report, not an audit report, and calling it `Correspondence`
misdescribes it. Carried as C10.

**The identity discipline is the anti-duplication mechanism.**
`SourceOccurrenceIdentity` must derive deterministically from `ReportIssueId` so
a repeated file of the same issue is a replay and a **correction is unambiguously
a different occurrence**. With the content store's identical-content-is-replay
rule and the seam plan's `EnsureArtifactImmutable`, that is three independent
defences against a duplicate report in a case file.

**The two hashes must be asserted equal.** `RenderedReportArtifact.ArtifactSha256`
and `DocumentVersion.Sha256` describe the same bytes. If they diverge the filing
is wrong and claim 2 is false even though custody reports success.

### The lease problem

`AddCaseDocumentCommand` requires `ExpectedCaseVersion` and an `EditLeaseToken`,
so filing is a leased case mutation under optimistic concurrency. But the seam
plan's R3/R4 conclude that a Chromium render "must be an explicitly asynchronous,
progress-reporting staff action, never a page load".

Those do not compose. An async render completing minutes later cannot assume the
staff member still holds a lease, and must not manufacture one. Two shapes exist:
the render is a foreground action holding the lease throughout; or the filing is
a separate, separately leased action, which makes "generated but not filed" a
visible state. The second is more honest about the four claims. Carried as C8.

### Closed-case lock and reopen-before-change

- Rendering a correction for a closed case requires a reasoned reopen **first**.
- `ReportPreparation` requires an assigned Engineer; `PostReport` requires
  retained exact sent evidence. So a post-report correction has a route, and it
  passes through the interaction defect above.
- **`Created in error` never reopens**, so a report can never be corrected on a
  case closed that way. The correct action is the linked replacement case.
- Export is a read and is unaffected. Export is also not sending.

### The Box-unavailable path

`docs/requirements.md:401` defines custody failure after allocation as retaining
the case as `Not ready` with staff-initiated retry and no automatic business
retry. **That rule cannot be applied literally to a report filed at step 8**:
sending a post-report case back to `Not ready` would discard its whole workflow
position.

Safely available today: `DocumentCustodyStatus.Failed` on the version with a
staff-initiated retry; `CaseCustodyUnavailableException` and the fail-closed
adapter; and the rule that **claim 2 is simply false** until custody confirms,
shown as false rather than as pending success.

Not available and not to be invented: whether a report whose filing failed may be
approved, may be sent, and whether the case may enter `PostReport`. Carried as
C9.

### Reconciliation with in-flight custody work

`NOW.md` records a live claim, `task/box-casepo-document-custody`, reshaping the
Core content-storage contract. **Any report-filing design must be written against
the reshaped contract.** Nothing here depends on the current signature.

## UI-15's report section

**This section is a design-route input only.** It specifies nothing, approves
nothing, and creates no control.

`docs/design.md:663-670` records the 2026-08-03 widening: UI-15 and AI-09 exist
as routeless design markup with no `@page`, no PageModel, no form, every field
empty, and no navigation link. `Index.cshtml` already carries a `Report content`
section with narrative fields, Engineer name, qualifications, signatory
selection, agreed fee, fee description lines and a statement of truth.

**That markup owns the arrangement, labelling and copy.** This plan adds nothing
and changes nothing in it.

What the consumer chain will eventually need the workbench to express, as design
input only:

1. **The four claims must be separately visible and never merged.** A single
   "Report" status collapsing generated, filed, approved and sent is the design
   failure this plan exists to prevent.
2. **Issue history, not a current report** — every issue with version, kind,
   issued-at, and earlier issues retained and reachable.
3. **Approval names exactly one issue.**
4. **Filing state is a document fact, not a case state**, never presented as
   progress toward sending.
5. **Sent evidence is distinct**, shows `sentDateTime` as the business time,
   shows discovery and link times separately, and survives display when Outlook
   later moved or deleted the source.
6. **Failure and validation presentation must not imply delivery.**
7. **Nothing implies receipt**, because nothing proves it.
8. **A render or preview surface needs its own allocated capability** before it
   is designed at all.

## Dependency graph

```
GATE  report-wording open decision
GATE  CASE-31 / ENG-01 / ENG-02
GATE  CASE-23 post-report lifecycle open decision
GATE  Chromium + font provisioning for the deployed image (seam R1, R2, R3)

  [GATE wording] ──┐
  [GATE CASE-31] ──┤
  [GATE ENG-01]  ──┼──▶ (A) Core figures policy
  [GATE ENG-02]  ──┘         │
                             ▼
  seam Stage 1 ──────▶ (B) render caller (EXT-08, RPT-01)
  (ports landed)             │
  [GATE Chromium] ───────────┤
                             ▼
                       (C) report-issue persistence + migration
                             │
                             ├──────────────▶ (D) custody filing (DOC-02/03)
                             │                      │  ← reshaped content-store
                             ▼                      ▼     contract, in flight
                       (E) approval join  ◀─────────┘
                             │
                             ▼
                       (F) MAIL-17 send transaction
                             │   requires: principal CC/delivery/standing-note
                             │             preference contract  (does not exist)
                             │   requires: a send route — MAIL-12 or provider API
             ┌───────────────┴───────────────┐
             ▼                               ▼
   (G) send-intent record          [existing] MAIL-14 poll
             └──────────────┬────────────────┘
                            ▼
              [existing] MAIL-15 / MAIL-16 link → PostReport
                            │
             ┌──────────────┼──────────────┬─────────────────┐
             ▼              ▼              ▼                 ▼
   [GATE CASE-23]    [existing]        (H) EXT-11      (I) MI-02 / MI-03
   post-report        CASE-24          fee/invoice     counts + turnaround
   query & dispute    closure          contract        (UI-04 surface exists)
```

Two things the graph shows that are easy to miss:

- **MAIL-17 has a prerequisite nobody has written down.** Its outcome names
  "principal CC/delivery/standing-note preferences". The `Principal` record
  carries no preference fields at all, and the string "standing note" appears
  nowhere in `docs/` except in MAIL-17's own row. That contract is unspecified,
  unallocated and unbuilt.
- **CASE-23 sits downstream of a link event that is itself non-blocking for the
  alpha.** MAIL-16 is off the alpha path, so CASE-23's inputs arrive through
  MAIL-15's manual link for the foreseeable future. Any CASE-23 design assuming
  automatic matching is designing on an unbuilt dependency.

## Staged delivery

Stages are named `C0`–`C6` so they cannot be confused with the seam plan's
Stage 1/2/3.

### C0 — this plan

Documentation only. **Advances:** nothing.

### C1 — issue identity and custody join

Persist the report issue; file the artifact as a document version; assert hash
agreement; decide the semantic role; decide the filing lease shape.

**Prerequisites:** seam Stage 2, (A), (B), (C); the wording decision;
CASE-31/ENG-01/ENG-02; the reshaped content-store contract.
**Advances:** `EXT-08` in part, `RPT-01` in part.
**Does not advance:** `DOC-02`, `DOC-03`, `DOC-07` — already accepted; a
generated report exercising them is new evidence under an existing capability,
not a capability advance.

### C2 — the approval join

Constrain approval to name an existing issue whose hash Core observed. Keep or
retire the free-text route — C4.

**Advances:** nothing. Report approval has no capability identifier of its own;
this strengthens an accepted capability without advancing any.

### C3 — the send transaction

MAIL-17: idempotent send, the send-intent record, Box filing, completion outcome
and partial-failure recovery.

**Prerequisites:** C1, C2; a principal preference contract that does not exist; a
send route; a decision on whether the alpha's no-mailbox-mutation rule is lifted.
**Advances:** `MAIL-17` only. **May advance:** `MAIL-12`.
**Does not advance:** `MAIL-14`, `MAIL-15`, `MAIL-16` — sending does not change
what proves a send.

### C4 — the fee and invoice contract

EXT-11, including moving fee arithmetic out of `HtmlComposer` into a versioned
Core figures policy. **Advances:** `EXT-11` only.

### C5 — management information

MI-02 and MI-03, consuming linked Sent-item evidence and lifecycle transitions,
never rendered artifacts. **Advances:** `MI-02`, `MI-03`.
**Does not advance:** `UI-04`, already accepted.

### C6 — post-report query and dispute work

CASE-23, in whatever shape the open decision takes. **Prerequisites:** the
decision, resolved by the operator. No amount of engineering substitutes.
**Advances:** `CASE-23` only.

### Never in this chain

`CASE-21`, `CASE-30` and `EXT-03` are the EVA handoff, complete and accepted on
their own terms. `CASE-22` is a separate programme. Neither is advanced, altered
or implied by anything above.

## Verification

| Check | Tier | Stage | What it proves |
| --- | --- | --- | --- |
| Report-issue identity: monotonic versions; a correction allocates a new issue naming a superseded one; a hash already seen under another issue is a replay | 2 | C1 | Issue identity and the no-overwrite rule |
| `ArtifactSha256` equals the filed `DocumentVersion.Sha256`; a deliberate mismatch fails closed | 2 | C1 | Claims 1 and 2 describe the same bytes |
| Re-filing the same issue is a replay; filing a correction is a distinct occurrence | 2, 4 | C1 | The chain cannot duplicate a report in a case file |
| Filing under an expired or absent lease is refused; a stale case version conflicts | 4 | C1 | Generated filing obeys the same concurrency rules as staff upload |
| Custody failure yields `Failed` with staff-initiated retry and no automatic business retry | 3, 4 | C1 | The no-automatic-retry rule survives the new path |
| Approval of a non-existent issue is refused; approval of an issue whose hash was not observed is refused | 2, 5 | C2 | The approval join is a reference, not an assertion |
| Send replay under the same operation key is idempotent and produces no second intent record | 2, 4 | C3 | The idempotency requirement |
| Partial failure is recoverable and visible | 4, 12 | C3 | The partial-failure recovery requirement |
| **A send-intent record with no corresponding Sent item leaves the case in `ReportPreparation` and produces no `Report sent` event** | 2, 12 | C3 | **The spine.** A queue result proves nothing |
| A Sent item confirmed with no intent record still links normally through MAIL-15 | 2 | C3 | The annotation is optional and never gates evidence |
| An artifact hash matching an attachment on an unrelated Sent item produces no link | 2 | C3 | Hash equality is not a matching term |
| Confirmed evidence survives a later `Moved` or `Deleted` observation | 2, 4 | C3 | Finality |
| A correction rendered after a confirmed send does not retract, unlink or re-point the evidence | 2 | C3 | Retention |
| Fee arithmetic comes from a Core policy with a key and version; a missing VAT rate refuses rather than defaulting | 2 | C4 | The `HtmlComposer.FeeNote` defect is closed |
| A report correction creates, alters, credits and voids **no** invoice | 2 | C4 | The finding-correction rule |
| MI-02 counts linked send events, not renders; a re-render, a failed send and an unlinked artifact each change no count | 2, 4 | C5 | The most likely MI-02 error is designed out |
| MI-03 ready-to-sent uses Outlook `sentDateTime`, never discovery or link time | 2 | C5 | The business-time rule |
| Closed-case lock: rendering, filing, approving or correcting on a closed case is refused before a reasoned reopen | 2, 5 | C1–C3 | The read-only rule |
| `Created in error` cannot be reopened to correct a report | 2 | C1 | The one non-reopenable outcome holds |
| Role matrix over the chain, including EXT-11's role-restricted financial visibility | 9 | C1–C4 | Authorisation, not just authentication |
| Operator presentation shows the four claims separately and never implies delivery | 7 | C1+ | The accessible-presentation rule |
| Deployed: render, file, send, poll, link, complete, on the real image with a real mailbox | 12 | C3 | The only tier that proves the chain end to end |

### Honestly unproved

- **Claim 4, at every tier, forever.**
- **Everything at C1 and beyond is unreachable** until the seam plan's Stage 2
  exists, itself blocked on CASE-31/ENG-01/ENG-02, the wording decision and the
  Chromium/font decision. No consumer evidence can be produced by the current
  task.
- **Tier 8** does not apply; the corpus discipline governs extraction, not report
  generation.
- **Tier 6** is unproved for any render path, and seam R6 records that Worker
  cannot host rendering at all.
- **Tier 10** for the send transaction depends on a provider decision that does
  not exist.
- **Tier 11** for report-issue persistence cannot be written before the table
  exists.
- **CASE-23 has no verification at all**, because it has no accepted behaviour.
  That is the correct state, not a gap.
- **MAIL-17's destination contract is unverifiable** while principal preferences
  do not exist as a modelled concept.

## Non-goals

- Activating, advancing or re-banding any capability identifier.
- Defining any CASE-23 state, transition, actor, due/chaser interaction, response
  proof, closure rule or dispute resolution.
- Defining the correction/reopen state machine.
- Choosing a `DocumentSemanticRole` for any generated report kind.
- Designing any UI-15 control, layout, label or copy, or touching the routeless
  markup under `src/Pegasus.Web/Pages/Cases/Assessment/`.
- Specifying the principal CC/delivery/standing-note preference contract.
- Specifying fee rules, VAT policy, accounting status or the finance role model.
- Defining MI-02 or MI-03 measure definitions, periods or visibility.
- Choosing a send transport, provider, mailbox scope or Graph operation.
- Blurring the EVA bundle and a rendered report in any direction.
- Creating any new case, reference, table, migration, project, store, runtime or
  deployment unit.
- Contradicting the in-flight custody contract reshape or the in-flight UI design
  pass.

## Stop conditions

1. A design would make a generated artifact, a Box file, a send request, a
   provider acknowledgement or a staff assertion sufficient to record `Report
   sent`.
2. A design would make the artifact hash a term in Sent-item matching rather than
   an annotation on already-confirmed evidence.
3. A design would require a new lifecycle state, terminal outcome, or transition
   into or out of `PostReport` before CASE-23 is decided.
4. A design would let a report correction create, alter, credit or void a fee
   note or invoice, or let MI-02 count anything other than accepted report-send
   events.
5. A design would allow a mailbox adapter to decide a lifecycle transition,
   create a case, or allocate a reference.
6. A design would mutate a closed case, its documents or its Box content without
   a prior reasoned reopen.
7. A design would send a post-report case to `Not ready` because a report failed
   to file.
8. A design would create a second owner of report policy alongside
   `Pegasus.Core`, or put a money computation in Infrastructure.
9. A design would add a UI-15 control, route or placeholder without an allocated
   capability identifier and a completed design route.
10. A design would treat a rendered report as an EVA bundle, or an EVA bundle as
    a report.

## Open questions

**C1. Does MAIL-17 send on the Outlook thread, through a provider API, or both,
and who decides per principal?** Different security boundaries, different
evidence contracts, and — for the provider route — possibly no Sent item at all,
which would leave claim 3 unprovable by the only mechanism Pegasus accepts.

**C2. What is the principal CC/delivery/standing-note preference contract?**
Named in MAIL-17's outcome, modelled nowhere, mentioned in no requirements
clause. Who owns it, what are its fields, and is it versioned and snapshotted
onto the case like the inspection address is?

**C3. Does a provider-API send without a Sent item constitute `Report sent`?** If
yes, an accepted second evidence form is needed for that route. If no, the
provider route cannot complete post-report work. There is no third answer.

**C4. Is the free-text `ArtifactIdentity` approval route retired once Pegasus
renders?** Retiring it makes approval provable but forbids approving an
externally produced report — which CASE-22 says remains the case until at least
`1.0.0`. Keeping both leaves a route that proves nothing.

**C5. How does a post-report correction re-enter report preparation without
making the original send evidence unlinkable?** Sits inside CASE-23's open
decision and cannot be answered by engineering.

**C6. Does a correction or an addendum count as a report for MI-02, and does it
start a new MI-03 turnaround clock?** Four defensible answers exist and they
produce materially different invoices.

**C7. Should `Part35Response` and `ResponseLetter` be members of the Core
`ReportKind` set before CASE-23 is decided?**

**C8. Is filing a generated report part of the render action under one lease, or
a separate leased staff action?** Determines whether "generated but not filed" is
a visible state and whether an async render is viable at all.

**C9. What is the correct behaviour when a generated report fails to reach Box
custody?** May it be approved, may it be sent, and may the case enter
`PostReport`?

**C10. What `DocumentSemanticRole` does a fee note carry?** None of the seven
existing members describes it.

**C11. Is `MAIL-12` the send route for `MAIL-17`, or does the report send get its
own?**

**C12. Does the alpha's no-mailbox-mutation rule survive MAIL-17?**
`docs/requirements.md:817` states "The local alpha must not mutate a mailbox." A
send transaction is a mailbox mutation by definition. What evidence and approval
lift it, and for which environments?

## Relationship to the rest of the plan set

| Plan | Relationship |
| --- | --- |
| Master | Its "Downstream consumers" table is the index this plan expands; this plan adds `CASE-24`, `DOC-02/03/07` and `UI-04` as consumers the master lists only as "already accepted, and distinct from rendering" |
| Seam | Supplies `RenderedReportArtifact`, `ReportIssueKind`, `ReportIssueVersioning` and the wording gate. This plan consumes them unchanged and reports one contradiction between its Core contract and the imported `fee-note` template, plus the ADR-number correction |
| Templates | Owns the twelve templates. This plan takes `fee-note`, `part-35-response` and `response-letter` only as evidence of what consumers would exist |
| Open questions | Should absorb `C1`–`C12`. `C10` sharpens its `M7`; `C7` extends its `M5`; `C4` is new |
| MCP | This plan adds no MCP tool and notes that no consumer in this chain requires an MCP caller |
| Runtime uplift, desktop removal, docs migration | No relationship |
