# FRD-08: Email, mailbox, and background processing

> Owner capabilities: MAIL-01 to MAIL-04, MAIL-09, MAIL-13, MAIL-21, MAIL-22, UI-14 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- Every retained message keeps its mailbox, folder, item, Message-ID,
  conversation and content hash as separate facts. Mailbox plus Message-ID
  is what makes a message unique.
- Every received message gets one of eleven mail categories, or is left
  Unclassified and shown as Unidentified. Sent items carry no category.
- Every decision and every correction stays in history. Sending is the only
  mailbox write.
- The Worker alone reads mailboxes. Which mailboxes it reads, when each one
  starts and how it is woken are in
  [FRD-26](frd-26-mailbox-allowlist-activation-wake-up-and-recovery.md).
- Automatic Case association happens only when the registration or the exact
  thread points at exactly one Case.
- Interrupted intake work is resumed by the reconcile sweep within a minute
  (operator, 11 October 2026).

What Unidentified means, and when mail goes there, is defined in
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

Every retained received message has one mail category from the table below.
A message no classification has decided is Unclassified and shows as
Unidentified; Unidentified is not a category. Sent items carry no category.
This section is the only owner of the list (operator, 12 October 2026,
[ADR-0069](../adr/0069-one-flat-mail-category.md)). The decision dossier in
git history (`docs/history/plans/mailbox-categorisation-and-email-matching/`)
keeps the research context and is not a competing owner.

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
| Triage request | Accepted Triage request; a missing VRM stays Unidentified under FRD-03 | route predicate or staff | No automatic case: opens the Triage workflow on its accepted Triage match evidence ([FRD-03](frd-03-triage.md)) and defers Unidentified registration |
| Pre-instruction | Known pre-formal handling request, excluding Triage | staff | Label |
| Images received | Images before a formal instruction, excluding an accepted instruction | attachment or route evidence or staff | Offers staff **Register images** for image-only material with no Case and no Triage ([FRD-19](frd-19-image-led-intake-and-pairing.md)) |
| Case update | Update on ongoing work, excluding a new instruction or a post-report challenge; a client or Principal asking for progress on ongoing work; other ongoing correspondence once the more specific categories are excluded | route predicate, the state of the linked Case, or staff | Label |
| Cancellation | Explicit cancellation; it wins over quoted old instructions | route predicate or staff | The Case aside's Cancellation received row and the Cancellation received staff notice |
| Query | Question about, or challenge to, a delivered report or finding, a dispute included; a request to amend a delivered report | route predicate, the state of the linked Case, thread evidence or staff | On a Case in Report sent it raises a Case query ([FRD-13](frd-13-case-lifecycle-and-workflow.md#report-sent-and-queries)), the Query received notice and the Query response AI job source; other rows offer **Tag as query** |
| Billing | Payment notification or remittance advice or evidence; an invoice request or invoice action; a billing, invoice, payment or remittance question; other billing mail | predicate or staff | Label |
| General | Generated automatic-reply evidence, never quoted new-work text; delivery-status or non-delivery evidence for the exact message; an acknowledgement of receipt without a request, new work, query or cancellation; a general chase, including one about several Cases; an informational summary with no new instruction or actionable request | route predicate, transport evidence or staff | Label |
| Not client related | Internal, company, tool, service or software mail unrelated to client work | sender or route evidence or staff | Label |
| Internal CC | Internal copied correspondence, not the primary actionable occurrence | header or recipient evidence or staff | Label |

Unclassified is the explicit abstention when no accepted predicate matches
or evidence is missing or conflicting. Several matching candidates are
Unclassified too, with the candidates named; no winner is invented. Any
classified message blocks automatic image registration: only Unclassified or
undecided mail is eligible.

Every retained received message carries a classification decision. A
message whose sender matches no accepted route, or whose route has no
classification policy, is recorded Unclassified under the
`no_mail_classification_policy` rule with the route's reason, so staff can
correct it and the workspace lists it; nothing is left without a decision.

**Case-state classification** (`case_state_mail_classification` v1,
operator 8 October 2026, [ADR-0070](../adr/0070-case-state-mail-classification.md)).
When a received mail message joins a Case, by automatic association or by a
staff link, and its decision is still Unclassified, the Case's lifecycle
state classifies it: a Case in Report sent makes it Query; a Case still in
progress (Not ready, Held, Review or With Engineer) makes it Case update; a
closed Case classifies nothing. The Principal's own case-match keys found
the Case, so this is per-Principal evidence without a per-Principal
predicate. A classified decision, including a staff correction, is never
overridden. The decision is written in the same transaction as the link,
before the query rule in
[FRD-13](frd-13-case-lifecycle-and-workflow.md#report-sent-and-queries) reads it.

A body that merely says "audit", a forwarded old instruction, two accepted
matches at once, or incomplete route evidence must never be promoted by
guesswork. A general chase may mention several Cases but stays one unlinked
General occurrence. Pegasus neither copies it nor links it to many Cases. An
informational summary is retained as non-actionable General correspondence
and creates no intake, Triage or Case work.

Accepted predicates must not overlap. An unexpected overlap is a defect: the
message is Unclassified with the candidates named, and no confidence score or
invented winner resolves it.
[FRD-09](frd-09-principal-and-intermediary-routes.md) owns the route
predicates.

**Correction.** Staff, or the Automation actor, may correct a message to any
category or to Unclassified, with a reason; the history is kept. A
correction to New instruction names the work type the instruction carries,
Inspection, Audit or Inspection and Audit, which Create case preselects and
allocation reads. A correction to or from Triage request writes or removes
the receipt's accepted Triage match, so Open the Triage reads one fact. A
re-evaluation reads a corrected classification in place of the classifier and
never removes or overwrites it or its history.

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

**Outlook category allowlist.** Administrators keep one global allowlist of exact
Outlook category display names. Each entry has a server-owned internal
identifier and is Active or Disabled; entries are disabled, never deleted.
MAIL-13 accepts only the internal identifier, and Core reloads an Active
entry's display name before any action on an exact message. The catalogue
stores no Graph identifier or colour, does not synchronise Outlook master
categories, and provides no search, Case linking or generic mailbox-rule
behaviour.

### Automatic Case association of retained mail

Automatic association of inbound mail is deliberately cautious. A message
may be linked automatically only when:

- its normalised vehicle registration identifies exactly one current,
  non-archived Case across the whole system; or
- its exact mailbox-and-conversation thread identifies exactly one current
  Case.

If both kinds of evidence point at a Case, they must agree. Pegasus abstains
when a registration has zero or several candidates, when the thread has
several candidates, when the candidates disagree, or when the evidence
changes before the serializable write. The Case/PO text inside an inbound
message is never a matching key. So a first message may qualify by its
unique registration before its thread is linked, and a later message with no
registration may qualify from the exact thread. The system-worker
association is append-only and idempotent, follows the ordinary
current-association and staff-reversal precedence, and never changes the
mailbox.

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
| A retained message | Processing (sender not yet established); classified into one mail category, or Unclassified and shown as Unidentified; Case-associated or not |

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

## Acceptance evidence

- Core tests for the Message-ID comparison key, the 500-character limit and
  the duplicate boundary across two mailboxes.
- Classification predicate tests for each mail category, including the
  refused promotions.
- Deployment and live acceptance are separate evidence tiers
  ([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `MAIL-01`–`MAIL-04`, `MAIL-09`, `MAIL-13`, `MAIL-21`,
  `MAIL-22`, `UI-14` in
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
