# FRD-08: Email, mailbox, and background processing

> Owner capabilities: MAIL-01–MAIL-05, MAIL-07–MAIL-09, MAIL-20–MAIL-23, INT-33, OPS-02, OPS-22, EVAL-01–EVAL-05 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- Every retained message keeps its mailbox, folder, item, Message-ID,
  conversation and content hash as separate facts. Mailbox plus Message-ID
  is what makes a message unique.
- Every message gets a named classification from the settled taxonomy, or is
  left as Unidentified. Nothing is hidden in a generic Other.
- Classification, application queue, Triage routing and Outlook folder are
  four separate facts. Every decision and every correction stays in history.
- The Worker alone reads mailboxes. Graph notifications only wake it up. Each
  mailbox has its own cursor, lease and activation start.
- Automatic Case association happens only when the registration or the exact
  thread points at exactly one Case.

What Unidentified means, and when mail goes there, is defined in
[FRD-02](frd-02-intake-and-source-identity.md#unidentified-destination-and-reference).

## Purpose

Instructions, cancellations, queries and images mostly arrive by email. This
document says how Pegasus identifies, retains, classifies and routes that
mail, and how the background Worker keeps up with each approved mailbox. The
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

### Settled mailbox taxonomy and correction

The operator confirmed this taxonomy from the retained current-tree
evidence. This section is its only owner. The decision dossier in git
history (`docs/history/plans/mailbox-categorisation-and-email-matching/`)
keeps the research context and is not a competing owner.

| Received family | Confirmed examples or subtypes |
| --- | --- |
| `General` | `autoreply`; `undeliverable`; acknowledgements such as "thank you"; `general-chase`; `case-summary` |
| `billing` | payment notifications; remittances; invoice requests; `billing-query`; `general-billing` |
| `new-instruction-received` | initial work instructions: `audit`, `diminution`, `inspection`, `new-client`, `website-enquiry` |
| `non-client-related` | internal or company email from tools, services, software packages and similar sources |
| `in-progress-cases` | `cancellation`; `case-update`; `client-chasing-for-update`; `provider-chasing-for-update`; other ongoing correspondence |
| `post-report-emails` | queries; disputes; amendment requests; similar post-report correspondence |
| `pre-instruction-emails` | Triage requests; pre-formal-instruction handling requests; images received before formal instructions |
| `internal-cc` | internal copied correspondence |

Each example is a named classification, never material hidden in a generic
`Other`. The canonical subtype spellings are `acknowledgement` for the
General example; `payment-notification`, `remittance` and `invoice-request`
for billing; `ongoing-correspondence` for the remaining in-progress example;
`query`, `dispute` and `amendment-request` for post-report mail; and
`triage-request`, `pre-formal-instruction-request` and `images-received` for
pre-instruction mail. A family whose row names no subtype needs none.

| Sent family | Confirmed meaning |
| --- | --- |
| `Report sent` | Collision Engineers' email sending the Engineer report |
| `case-rejected` | Collision Engineers rejects a case |
| `query-sent` | Collision Engineers sends an additional query or information request |
| `additional-image-request` | existing images are insufficient and better or additional images are requested |

Reply is not a recorded type of its own. A Collision Engineers reply to a
Received message takes the Received category with reply context. A
correspondent's reply to a Sent message takes the Sent category with reply
context. The taxonomy also allows `Other`, which needs both a new category
name and a reason.

### Classification, destination, and folder catalogue

A known classification has its own typed destination. It is never collapsed
into a generic Other queue. `Other` exists only to extend the taxonomy with
a reasoned new class. `Unidentified` is an abstention, used when evidence is
missing, unsupported, contradictory or ambiguous. It is never a
classification.

Classification may use mailbox and message identity, direction, headers,
sender and domain, fresh body text, attachment and document evidence,
provider-route tells, reply and thread signals, and a separately produced
Case correlation. `In-Reply-To` and `References` establish reply context.
`RE:` is a fallback. `FW:` or `FWD:` alone does not make a reply. Quoted or
attached old content is not evidence of fresh work. A deterministic rule
names its policy, version and predicates. Otherwise an authorised staff
member records the decision and the reason. The history rules below keep
the evidence, actor, time, policy version and later corrections.

| Classification | Positive criteria and exclusions | Method | Operational destination | Outlook folder type |
| --- | --- | --- | --- | --- |
| `General/autoreply` | Generated automatic-reply evidence; never quoted new-work text | route predicate or staff | Detailed: `General/autoreply` | No action |
| `General/undeliverable` | Delivery-status or non-delivery evidence for the exact message | transport evidence or staff | Detailed: `General/undeliverable` | No action |
| `General/acknowledgement` | Acknowledges receipt without a request, new work, dispute, amendment or cancellation | staff until a predicate is accepted | Detailed: `General/acknowledgement` | No action |
| `General/general-chase` | General chase, including one about several Cases; never one-to-many association | staff | Detailed: `General/general-chase` | Case queries |
| `General/case-summary` | Informational summary with no new instruction or actionable request | staff | Detailed: `General/case-summary` | No action |
| `billing/payment-notification` | Payment notification, excluding a question or request | predicate or staff | Detailed: `billing/payment-notification` | Billing |
| `billing/remittance` | Remittance advice or evidence, excluding a billing question | predicate or staff | Detailed: `billing/remittance` | Billing |
| `billing/invoice-request` | Requests an invoice or invoice action | predicate or staff | Detailed: `billing/invoice-request` | Billing |
| `billing/billing-query` | Asks a billing, invoice, payment or remittance question | predicate or staff | Queries | Billing |
| `billing/general-billing` | Billing mail fitting no more specific billing subtype | reasoned staff decision | Detailed: `billing/general-billing` | Billing |
| `new-instruction-received/audit` | Accepted provider Audit instruction evidence; a body keyword or quoted old instruction is not enough | route predicate or staff | Receiving work | Audits |
| `new-instruction-received/diminution` | Accepted provider diminution instruction evidence | route predicate or staff | Receiving work | Diminution |
| `new-instruction-received/inspection` | Accepted provider Inspection instruction evidence | route predicate or staff | Receiving work | Instructions |
| `new-instruction-received/new-client` | Initial work from a client with no accepted route | staff | Receiving work | New clients |
| `new-instruction-received/website-enquiry` | Website-origin evidence meeting the accepted independent fingerprints | route predicate or staff | Receiving work | Enquiries |
| `non-client-related` | Internal, company, tool, service or software mail unrelated to client work | sender or route evidence or staff | Detailed: `non-client-related` | Other |
| `in-progress-cases/cancellation` | Explicit cancellation; it wins over quoted old instructions | route predicate or staff | Detailed: `in-progress-cases/cancellation` | Cancellations |
| `in-progress-cases/case-update` | Update on ongoing work, excluding a new instruction or a post-report challenge | staff | Detailed: `in-progress-cases/case-update` | Case updates |
| `in-progress-cases/client-chasing-for-update` | Client asks for progress on ongoing work | staff | Detailed: `in-progress-cases/client-chasing-for-update` | Case updates |
| `in-progress-cases/provider-chasing-for-update` | Provider asks for progress on ongoing work | staff | Detailed: `in-progress-cases/provider-chasing-for-update` | Case updates |
| `in-progress-cases/ongoing-correspondence` | Other ongoing correspondence once more specific subtypes are excluded | reasoned staff decision | Detailed: `in-progress-cases/ongoing-correspondence` | Case updates |
| `post-report-emails/query` | Question about a delivered report | route or thread evidence or staff | Queries | Case queries |
| `post-report-emails/dispute` | Challenge to a delivered report or finding | route or thread evidence or staff | Queries | Case queries |
| `post-report-emails/amendment-request` | Request to amend a delivered report | route or thread evidence or staff | Queries | Case queries |
| `pre-instruction-emails/triage-request` | Accepted Triage request; a missing VRM stays Unidentified under FRD-03 | route predicate or staff | Triage | Pre-instructions |
| `pre-instruction-emails/pre-formal-instruction-request` | Known pre-formal handling request, excluding Triage | staff | Detailed: `pre-instruction-emails/pre-formal-instruction-request` | Pre-instructions |
| `pre-instruction-emails/images-received` | Images before a formal instruction, excluding an accepted instruction | attachment or route evidence or staff | Detailed: `pre-instruction-emails/images-received` | Images |
| `internal-cc` | Internal copied correspondence, not the primary actionable occurrence | header or recipient evidence or staff | Detailed: `internal-cc` | Other |
| Sent: `Report sent` | Exact sent report correspondence; classification alone does not prove delivery | immutable Sent-item evidence or staff | Detailed: Sent/`Report sent` | Other |
| Sent: `case-rejected` | Exact outbound rejection | immutable Sent-item evidence or staff | Detailed: Sent/`case-rejected` | Other |
| Sent: `query-sent` | Exact outbound query or information request | immutable Sent-item evidence or staff | Detailed: Sent/`query-sent` | Other |
| Sent: `additional-image-request` | Exact outbound request for better or additional images | immutable Sent-item evidence or staff | Detailed: Sent/`additional-image-request` | Other |
| reasoned `Other` | No registry entry fits; needs a new name and reason and may not hide a known class | authorised staff only | Other | Other |
| `Ambiguous` / `Unclassified` | Several or no accepted predicates, or missing or conflicting evidence; no winner is invented | explicit abstention | Unidentified | none automatically |

The approved logical folder types are `Instructions`, `Audits`,
`Diminution`, `New clients`, `Case queries`, `Enquiries`, `Billing`,
`Pre-instructions`, `No action`, `Images`, `Cancellations`, `Case updates`
and `Other`. MAIL-23 binds each type to an Administrator-approved exact
Outlook folder identity per mailbox. MAIL-05 derives the per-message folder
recommendation; MAIL-07 owns the separate confirmed move. Triage and
Unidentified get no automatic folder recommendation just because they are
application destinations.

Worked examples: one accepted Audit instruction goes to Receiving work and
the Audits folder; a billing question goes to Queries and the Billing
folder; an accepted Triage request goes to the separate Triage workflow. A
body that merely says "audit", a forwarded old instruction, two accepted
matches at once, or incomplete route evidence must never be promoted by
guesswork.

A `general-chase` message may mention several Cases but stays one unlinked
General occurrence. Pegasus neither copies it nor links it to many Cases. A
`case-summary` is retained as non-actionable General correspondence and
creates no intake, Triage or Case work.

Classification, application queue, Triage routing and Outlook folder are
four separate facts. `new-instruction-received` is a Received family with no
confirmed Sent equivalent, and that boundary permits no conflicting rules.
Accepted predicates must not overlap. An unexpected overlap is a defect: it
fails closed with visible evidence, and no confidence score or invented
winner resolves it. [FRD-09](frd-09-provider-and-intermediary-routes.md)
owns the route predicates.

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

**Category allowlist.** Administrators keep one global allowlist of exact
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

### Mailbox allowlist, activation and wipe

The approved mailbox allowlist, not deployment configuration, decides which
mailboxes an Outlook/Graph route reads. `ApprovedMailbox.Id` is the durable
identity. The Graph mailbox and folder coordinates are replaceable cursor
scope. Each mailbox holds its own lease and its own cursor, so one mailbox's
failure or backlog never affects another.

Each mailbox has its own fresh-start activation cycle. Enabling starts a new
cycle at a recorded UTC activation time. Mail received before that time
moves the cursor forward but is not retained, quarantined, passed to intake
or allocated. Disabling a mailbox stops polling at the next tick and deletes
nothing: retained messages, receipts, assets, quarantined items and Case
associations all stay visible. Re-enabling starts a new fresh-start cycle
rather than resuming the old cursor, so mail received while disabled never
becomes a backlog.

The global Worker switch, individual function switches and per-mailbox
switches are separate. Sent-evidence polling stays off unless separately
approved. Approving a mailbox in Pegasus grants no Exchange access; the
Microsoft 365 tenant must separately admit the application to that mailbox.
Until it does, polling that mailbox fails and says so. An approved intake
mailbox becomes pollable only after an address check has stored its mailbox
identity and activation time. Before that, the administration list reports
it as not activated, not as awaiting a first poll, because no poll is
pending.

**Wipe.** An explicitly authorised intake-data wipe records one UTC
receive-time cutoff in the existing Inbox poll state, in the same
transaction that clears the SQL data. It does not change mailbox identity,
approval, onboarding time or subscriptions. The effective start is the later
of activation and that cutoff, including for a mailbox never yet polled.
Clearing occurrence identities, rebinding cursor scope, resetting an expired
Graph delta token or receiving an old queued notification must never lower
the cutoff or bring cleared mail back. Mail received at or after the cutoff
stays eligible, and forwarding an old email creates a newly received message
that is evaluated normally. An ordinary deployment or Worker restart never
moves this boundary. Wipes run only with the Worker stopped and application
writes excluded for maintenance.

### Mailbox wake-up and recovery

Each enabled approved Inbox has one Microsoft Graph basic change-notification
subscription. Its record uses the approved-mailbox identity and stores the
Graph subscription id, resource scope, expiry, lifecycle state and last
maintenance result in SQL. The shared client-state secret is protected
configuration. It is never stored in the subscription row, logged, sent to
a browser or put on a queue.

`POST /hooks/microsoft-graph/mail` on the Web host is a wake-up endpoint,
not a mail reader. For Graph validation it returns the validation token as
plain text within the protocol deadline. For a notification it checks the
clientState and the known active subscription, queues only the subscription
and approved-mailbox identifiers, and acknowledges promptly. It does not
download, classify, extract, associate, allocate or change a message.
Unknown, expired, malformed or wrongly scoped notifications fail closed
without queuing work or revealing the secret.

The Worker alone owns the mailbox lease, the cursor or delta read,
retention, the shared intake call and the retry outcome. A creation
notification with an immutable message ID fetches and processes that exact
message only, still subject to the receive-time cutoff. It does not scan the
Inbox or move the recovery cursor. Duplicate notifications are safe. A
notification may name a message the mail source cannot show yet, because
the tenant accepted it before replicating it to the folder the delta reads.
That empty read is not a failure: the cursor stays where it was, the message
is still ahead of the next delta sweep, and the mailbox-scoped source
identity that both routes derive keeps exactly one occurrence however many
notifications arrive. Lifecycle `missed`, `subscriptionRemoved` and
reauthorization events schedule the same delta resynchronisation; they are
not another processing route.

Subscription maintenance runs every six hours and renews an enabled Inbox
before it comes within 48 hours of expiry. A failure is visible per mailbox
and leaves the five-minute per-mailbox fallback poll running. That fallback
moves the same cursor and is recovery only: it creates no second receipt and
does not bypass the fresh-start boundary. Disabling a mailbox stops
notification work at claim time as well as its next fallback poll.

Before activation, an Outlook/Graph route must:

- use an approved test or live mailbox and an exact operation;
- keep message, conversation, folder, attachment, sender and recipient, and
  received and sent identity;
- keep a durable cursor or checkpoint and process each occurrence
  idempotently;
- separate read and intake scopes from draft, send and administrative
  scopes;
- queue only stable work identifiers, never full source payloads;
- record poison, retry, dead-letter and operator recovery behaviour;
- prove the real Worker timer or queue caller;
- obtain exact Sent-item and reply-chain evidence wherever delivery is part
  of a completion gate.

### QDOS evaluation boundary

The Development/local email evaluation workbench is a separately delivered
evidence harness. It is not a product surface, a caller or an acceptance
checkpoint for the QDOS mail route. The QDOS route adds and claims no
evaluator route, no `unchecked`/`checked` workspace workflow, no evaluator
command, no reviewer report campaign and no Administrator evaluator
approval. A separately delivered evaluator may exercise the shared policy
and produce accepted, source-labelled evidence where the shared mail policy
needs it. That call and its review mechanics are evaluator evidence, not
delivery or activation proof. The capability inventory's evaluator
allocation boundary owns the evaluator allocations. Shared Core mail policy,
production intake, Graph replay and live adapters, and their genuine-evidence
and caller requirements stay in QDOS scope.

## Mail identity and repeated receipts

Provider coordinates identify a mailbox occurrence for reading and polling;
they are not the business duplicate identity. The shared intake identity
policy recognises the same message within its durable mailbox while keeping
each receipt, its mailbox, folder and item identity, and its received and
discovery times. Equal content alone never erases an occurrence or creates a
new Case. [ADR-0044](../adr/0044-mail-occurrence-and-business-identity.md) records the
technical separation.

A query received for or attached to a Completed Case, and the reply that
returns it, follow
[FRD-13](frd-13-case-lifecycle-and-workflow.md#completed-and-query). The
received query and the actual reply are retained as Case correspondence.

## States and transitions

| Thing | States |
| --- | --- |
| A retained message | Processing (sender not yet established); classified into one named class, reasoned `Other`, or Unidentified; Case-associated or not; folder move recommended, confirmed, failed or done |
| An approved mailbox | not activated; enabled with a UTC activation time; disabled (nothing deleted); re-enabled with a new cycle |
| A Graph subscription | active; within 48 hours of expiry and due for renewal; `missed`, `subscriptionRemoved` or reauthorization pending resynchronisation |

Case states are owned by
[FRD-13](frd-13-case-lifecycle-and-workflow.md#states-and-labels).

## Edge cases and fail-closed behaviour

- A message with no Message-ID, or one that contradicts a retained message:
  refused, never guessed.
- A comparison key over 500 characters: refused before storage.
- Two accepted predicates matching one message: a defect that fails closed
  with visible evidence.
- A registration with zero or several Case candidates, or thread and
  registration disagreeing: no automatic association.
- Mail received before activation or before a wipe cutoff: cursor moves,
  nothing retained.
- A notification for a message the delta cannot show yet: not a failure;
  the next sweep picks it up.
- A tenant that has not admitted the application: that mailbox alone fails
  and says so.

## Acceptance evidence

- Core tests for the Message-ID comparison key, the 500-character limit and
  the duplicate boundary across two mailboxes.
- Classification predicate tests for each row of the catalogue, including
  the refused promotions in the worked examples.
- Persistence tests for activation, disable, re-enable and wipe cutoff
  behaviour on the cursor.
- A real Graph subscription validation and notification against the
  approved test mailbox, proving the Worker processes the exact message and
  the endpoint touches no mail.
- Deployment and live acceptance are separate evidence tiers
  ([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `MAIL-01`–`MAIL-05`, `MAIL-07`–`MAIL-09`, `MAIL-20`–`MAIL-23`,
  `INT-33`, `OPS-02`, `OPS-22`, `EVAL-01`–`EVAL-05` in
  [capabilities](../capabilities.md).
- Related FRDs: [FRD-02](frd-02-intake-and-source-identity.md)
  (Unidentified, intake receipts),
  [FRD-03](frd-03-triage.md) (Triage requests),
  [FRD-09](frd-09-provider-and-intermediary-routes.md) (route predicates),
  [FRD-13](frd-13-case-lifecycle-and-workflow.md) (Case states),
  [FRD-20](frd-20-mailbox-workspace.md) (mail screens),
  [FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md) (sending
  and Sent evidence).
- Technical constraints:
  [ADR-0044](../adr/0044-mail-occurrence-and-business-identity.md) (mail identity
  separation), [ADR-0036](../adr/0036-outbound-mail-via-approved-mailbox.md)
  (approved mailbox sending),
  [ADR-0052](../adr/0052-dismiss-by-logical-folder.md) (no deletion).
