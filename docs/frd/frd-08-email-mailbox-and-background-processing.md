# FRD-08: Email, mailbox, and background processing

> Owner capabilities: MAIL-01 to MAIL-04, MAIL-09, MAIL-21, MAIL-22, MAIL-26, UI-14 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- Every retained message keeps its mailbox, folder, item, Message-ID,
  conversation and content hash as separate facts. Mailbox plus Message-ID
  is what makes a message unique.
- Every received message gets one of three mail categories — New instruction,
  Triage request or Cancellation — or no category. No category is Case
  correspondence when the message is linked, and No Case Found when it is not.
  A decision may also set Opens query. Sent items carry no category.
- A received message linked to no Case waits in the Inbox's No Case Found
  scope ([FRD-20](frd-20-mailbox-workspace.md#inbox-scopes-and-filters)).
  An e-mail becomes an Unidentified item only when it is image-only material
  (photographs with no instruction fields).
- Every decision and every correction stays in history. Sending is the only
  mailbox write.
- The Worker alone reads mailboxes. Which mailboxes it reads, when each one
  starts and how it is woken are in
  [FRD-26](frd-26-mailbox-allowlist-activation-wake-up-and-recovery.md).
- Automatic Case association happens only when the registration or the exact
  thread points at exactly one Case.
- Interrupted intake work is resumed by the reconcile sweep within a minute
  (operator, 11 October 2026).

What Unidentified means, and which material goes there, is defined in
[FRD-02](frd-02-intake-and-source-identity.md#unidentified-destination-and-reference).

## Purpose

Instructions, cancellations, queries and images mostly arrive by email. This
document says how Pegasus identifies, retains, classifies and routes that
mail, and how retained mail is associated with a Case. The mailbox
allowlist, activation, wipe cutoff, Graph wake-up and recovery are in
[FRD-26](frd-26-mailbox-allowlist-activation-wake-up-and-recovery.md). The
screens are in [FRD-20](frd-20-mailbox-workspace.md). Sending mail and
proving a report was sent are in
[FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md).

## Email, mailbox, and background processing

The product covers the whole approved mailbox estate and full source
messages. The first mailbox is only the first caller. The PRD owns the
mailbox target; [operations](../operations.md) owns dated observations of
deployed mailboxes.

### Inbound mailbox identity

Every retained inbound message keeps these identities separately, and none
stands in for another:

- the durable Pegasus mailbox identity and mailbox address;
- the exact folder identity;
- the provider's immutable item identity;
- the RFC Internet Message-ID;
- the provider conversation identity, when supplied;
- the SHA-256 of the retained source.

Sender, recipient, attachment and received-time facts are evidence, not
identity keys.

**Mailbox plus Message-ID is the duplicate boundary.** Pegasus keeps the
transport value exactly as received and derives one comparison key from it:
trim surrounding whitespace, apply Unicode compatibility normalization, then
invariant uppercase. That key drives the intake receipt, retained-message
comparison and a binary-collated database uniqueness constraint. So values
that differ only in case, normalization form or surrounding whitespace are
one message, and distinct keys stay distinct. Both the raw value and the key
must fit the 500-character identity limit; if normalization pushes it past
that, the message fails closed before anything is stored.

The provider's immutable item identity is a separate coordinate used to read
the item. A change in that coordinate cannot create a second business
occurrence of the same mailbox and Message-ID. The same Message-ID may
legitimately occur in two approved mailboxes. A retained message with no
Message-ID, or an item, Message-ID and content combination that contradicts
a message already retained, fails closed. Pegasus never guesses or
overwrites.

**Thread identity is evidence only.** A thread view may join only retained
messages that share a conversation identity inside the same mailbox and
folder scope. It never reaches into another mailbox or fetches an
unretained item. `In-Reply-To` and `References` help classify and correlate;
they do not loosen this rule.

### Mail categories and correction

A received message has one of three categories, or none. None is
correspondence: linked, it is Case correspondence; unlinked, it waits in
No Case Found. A message nothing has decided yet is Unclassified, not a
category. Unidentified is not a category. Sent items carry no category.
This section is the only owner of the list (operator, 27 October 2026).
[ADR-0069](../adr/0069-one-flat-mail-category.md) records the earlier flat
list and is historical. The decision dossier in git history
(`docs/history/plans/mailbox-categorisation-and-email-matching/`) keeps the
research context and is not a competing owner.

Classification may use mailbox and message identity, direction, headers,
sender and domain, fresh body text, attachment and document evidence,
Principal-route tells, reply and thread signals, and a separately produced
Case correlation. `In-Reply-To` and `References` establish reply context.
`RE:` is a fallback. `FW:` or `FWD:` alone does not make a reply. A reply is
not a category of its own. Quoted or attached old content is not evidence of
fresh work. A deterministic rule names its policy, version and predicates.
Otherwise an authorised staff member records the decision and the reason.
The history rules below keep the evidence, actor, time, policy version and
later corrections.

| Category | Positive criteria and exclusions | Method | What it does |
| --- | --- | --- | --- |
| New instruction | Accepted Principal Audit, diminution or Inspection instruction evidence; initial work from a client with no accepted route; website-origin evidence meeting the accepted independent fingerprints. A body keyword or quoted old instruction is not enough | route predicate or staff | Carries a work type: Inspection, Audit, or Inspection and Audit. Offers **Create case**; automatic case allocation goes by the work type |
| Triage request | Accepted Triage request; with a missing VRM it waits in No Case Found under FRD-03 | route predicate or staff | No automatic case: opens the Triage workflow on its accepted Triage match evidence ([FRD-03](frd-03-triage.md)); it gets no Unidentified item |
| Cancellation | Explicit cancellation; it wins over quoted old instructions | route predicate or staff | The Case aside's Cancellation received row and the Cancellation received staff notice |
| Correspondence | Everything else. Not a category. Garage allocation, repairs authorised, report chase and automatic replies are correspondence | route predicate or staff | Linked, it is Case correspondence. Unlinked, it is No Case Found. Register images is offered only while the message is unlinked, image-only and has no category ([FRD-19](frd-19-image-led-intake-and-pairing.md)) |

Unclassified is the explicit abstention when no accepted predicate matches
or evidence is missing or conflicting. Several matching candidates are
Unclassified too, with the candidates named; no winner is invented. Any
classified message blocks automatic image registration: only Unclassified or
undecided mail is eligible.

Mail from every approved mailbox is classified by the one policy,
`IMailClassificationPolicy`, built per Principal, whichever mailbox received
it (operator ruling, 15 October 2026).

Every retained received message carries a classification decision. A
message whose sender matches no accepted route, or whose route has no
classification policy, is recorded Unclassified under the
`no_mail_classification_policy` rule with the route's reason, so staff can
correct it and the workspace lists it; nothing is left without a decision.

Linking a message does not classify it. A query is not a category.
`OpensQuery` is a deterministic criterion on the classification decision.
When a retained message whose decision has `OpensQuery` joins a Case whose
report is sent, and that message does not already hold a query, the link
opens one. The actor is `system-worker:query-criteria` and the reason is
`Matched query criteria`. A missing retained message opens nothing. A
classification correction does not open or withdraw a query. The query rule
is [FRD-13](frd-13-case-lifecycle-and-workflow.md#report-sent-and-queries).

A body that merely says "audit", a forwarded old instruction, two accepted
matches at once, or incomplete route evidence must never be promoted by
guesswork. A chase may mention several Cases but stays one unlinked
correspondence. Pegasus neither copies it nor links it to many Cases. An
informational summary is retained as correspondence and creates no intake,
Triage or Case work.

Accepted predicates must not overlap. An unexpected overlap is a defect: the
message is Unclassified with the candidates named, and no confidence score or
invented winner resolves it.
[FRD-09](frd-09-principal-and-intermediary-routes.md) owns the route
predicates.

**Correction.** Staff, or the Automation actor, may correct a message to one
of the three categories or to correspondence, with a reason; the history is
kept. A removed code is refused. A
correction to New instruction names the work type the instruction carries,
Inspection, Audit or Inspection and Audit, which Create case preselects and
allocation reads. A correction to or from Triage request writes or removes
the receipt's accepted Triage match, so Open the Triage reads one fact. A
re-evaluation reads a corrected classification in place of the classifier and
never removes or overwrites it or its history.

The mail classifier agent is an Automation MCP client under `automation.mail`;
it classifies and links only mail that is Unclassified or has no Case and was
decided by the pipeline, and leaves a decision by staff, an Automation caller
or a Principal alone (operator, 15 October 2026).

**History.** Every automated or human classification decision keeps the
source identity, policy key and version, outcome, evidence references,
confidence or ambiguity facts, the actor or automated identity, and the
time. A correction, override, reversal, link, unlink or relink keeps the
original decision and adds the reason, structured before and after values,
actor, time, outcome and policy or evidence references to permanent history.
Queues, routes, counts and events are recalculated from that history without
deleting anything.

A rule change never silently reinterprets old decisions. Re-evaluating a
cohort needs an explicit approved operation. A technical replay is
idempotent and is not a new business decision. A wrong Case allocation
follows the reasoned `Created in error` replacement route in
[FRD-01](frd-01-case-identity-and-lifecycle.md) and never reuses a
reference. Message and file bodies, credentials, tokens and secrets never go
into action history. Polling, retry, lease and adapter mechanics stay in
telemetry.

### Automatic Case association of retained mail

Automatic association of inbound mail is deliberately cautious. Every linking
signal that has any candidate contributes its whole candidate set, and the
message is linked only when their intersection is exactly one Case that can
take an association — current and not archived. A reply into a thread that
holds a Triage links to the Triage as a reply links to a Case (operator,
28 October 2026; R469, superseding R455). The signals are:

- the Cases the other messages of its exact mailbox-and-conversation thread
  stand linked to; a message is never thread evidence for itself, so the link
  an earlier attempt wrote cannot become evidence for its own redelivery;
- the Cases of the retained received messages and Pegasus Sent items, in any
  approved mailbox, that its `In-Reply-To` and `References` headers point at;
- the Cases whose indexed durable claim token its subject or current body
  names, across every Principal; or
- the Cases carrying its normalised vehicle registration, narrowed by the
  message's incident date first: candidates with a different date are dropped,
  and an equal date wins when exactly one remains. A date that leaves no
  candidate removes the registration from the decision; it never vetoes
  another agreeing signal. A message that names no registration of its own
  has the one the photographs inside its PDF read, text layer or images
  only (operator, 28 October 2026, R474).

Pegasus abstains when no signal has a candidate, when the intersection is not
exactly one Case — signals that disagree, or a set that stays several — or when
the evidence changes before the serializable write. Mail
association never re-decides a Case the message's case match eliminated, or an
`Ambiguous` match
([FRD-09](frd-09-principal-and-intermediary-routes.md#accepted-principal-email-routes-and-automatic-association)).
A claim reference is read only as a bounded token compared with the indexed
durable claim token; the message's free text is never matched as an unbounded
subject or body. So a first message may qualify by its unique registration
before its thread is linked, and a later message with no registration may
qualify from the exact thread, from the message it answers, or from the claim
reference it names. The system-worker association is append-only and
idempotent, follows the ordinary current-association and staff-reversal
precedence, and never changes the mailbox.

### Mail identity and repeated receipts

Provider coordinates identify a mailbox occurrence for reading and polling;
they are not the business duplicate identity. The shared intake identity
policy recognises the same message within its durable mailbox while keeping
each receipt, its mailbox, folder and item identity, and its received and
discovery times. Equal content alone never erases an occurrence or creates a
new Case. [ADR-0044](../adr/0044-mail-occurrence-and-business-identity.md) records the
technical separation.

A query received for or attached to a Report sent Case, and the reply
that resolves it, follow
[FRD-13](frd-13-case-lifecycle-and-workflow.md#report-sent-and-queries). The
received query and the actual reply are retained as Case correspondence.

## States and transitions

| Thing | States |
| --- | --- |
| A retained message | Processing (sender not yet established); one of three categories, correspondence, or Unclassified; Case-associated or not |

Case states are owned by
[FRD-13](frd-13-case-lifecycle-and-workflow.md#states-and-labels). Approved
mailbox and Graph subscription states are owned by
[FRD-26](frd-26-mailbox-allowlist-activation-wake-up-and-recovery.md#states-and-transitions).

## Edge cases and fail-closed behaviour

- A message with no Message-ID, or one that contradicts a retained message:
  refused, never guessed.
- A comparison key over 500 characters: refused before storage.
- Two accepted predicates matching one message: a defect; the message is
  Unclassified with the candidates named.
- A registration with zero or several Case candidates, or thread and
  registration disagreeing: no automatic association.
- A reply with no registration in a thread that holds a Triage: linked to
  the Triage as a reply links to a Case, never a processing failure
  (operator, 28 October 2026; R469, R468).

## Acceptance evidence

- Core tests for the Message-ID comparison key, the 500-character limit and
  the duplicate boundary across two mailboxes.
- Classification predicate tests for each mail category, including the
  refused promotions.
- Deployment and live acceptance are separate evidence tiers
  ([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `MAIL-01`–`MAIL-04`, `MAIL-09`, `MAIL-21`,
  `MAIL-22`, `MAIL-26`, `UI-14` in
  [capabilities](../capabilities.md).
- Related FRDs: [FRD-02](frd-02-intake-and-source-identity.md)
  (Unidentified, intake receipts),
  [FRD-03](frd-03-triage.md) (Triage requests),
  [FRD-09](frd-09-principal-and-intermediary-routes.md) (route predicates),
  [FRD-13](frd-13-case-lifecycle-and-workflow.md) (Case states),
  [FRD-20](frd-20-mailbox-workspace.md) (mail screens),
  [FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md) (sending
  and Sent evidence),
  [FRD-26](frd-26-mailbox-allowlist-activation-wake-up-and-recovery.md)
  (mailbox allowlist, activation, wake-up and recovery).
- Technical constraints:
  [ADR-0044](../adr/0044-mail-occurrence-and-business-identity.md) (mail identity
  separation), [ADR-0036](../adr/0036-outbound-mail-via-approved-mailbox.md)
  (approved mailbox sending),
  [ADR-0052](../adr/0052-dismiss-by-logical-folder.md) (no Delete control),
  [ADR-0069](../adr/0069-one-flat-mail-category.md) (one flat mail
  category).
