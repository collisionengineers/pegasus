# FRD-08: Email, mailbox, and background processing

## Unidentified mail destination

Mailbox material that is safely retained but has no unique accepted classification,
identity, owner, or destination is registered once in Unidentified with its U
reference and canonical reason. Retryable processing remains retryable; a terminal
technical failure after custody uses `TechnicalProcessingFailure`. Mail projections
link to the same Unidentified item rather than synthesising a second queue row.
> Owner capabilities: MAIL · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · UI behaviour: docs/design/README.md

## Email, mailbox, and background processing

The target product covers the approved mailbox estate and full source messages; the focused alpha mailbox is only the first caller. Mailbox inventory and current-system roles remain in [operator notes](../operator-notes.md).

### Inbound mailbox identity

Every retained inbound message keeps separate, explicitly named identities:
the durable Pegasus mailbox identity and mailbox address, the exact folder
identity, the provider's immutable item identity, the RFC Internet Message-ID,
the provider conversation identity when supplied, and the retained source
SHA-256. None of those fields substitutes for another. Sender, recipient,
attachment and received-time facts remain message evidence rather than identity
keys.

Mailbox identity plus RFC Internet Message-ID is the durable message and intake
duplicate boundary. Pegasus retains the transport value verbatim as evidence
and derives one comparison key by trimming surrounding whitespace, applying
Unicode compatibility normalization, and invariant uppercase case folding.
That same canonical key drives the Core intake receipt, retained-message
comparison, and a binary-collated database uniqueness constraint; case-only,
normalization-equivalent, or surrounding-whitespace variants are therefore one
message, while distinct canonical values remain distinct. Both the raw value
and its canonical output must fit the 500-character retained identity bound;
normalization expansion beyond it fails closed before persistence. The raw
transport value is retained verbatim as evidence beside the canonical key. The provider immutable item identity remains a separately
retained coordinate used to read the item; a provider-coordinate change cannot
create a second business occurrence for the same mailbox/RFC message. The same
RFC identity may occur independently in two approved mailboxes. A retained
message without an RFC identity, or an immutable-item/RFC/content combination
that contradicts an already retained message, fails closed rather than being
guessed or overwritten.

Thread identity is evidence, not message or Case identity. A thread view may
join only retained messages with the same conversation identity inside the
same durable mailbox and folder scope. It never reaches across another mailbox
or fetches an unretained provider item. `In-Reply-To` and `References` remain
classification/correlation evidence and do not weaken this boundary.

### Settled mailbox taxonomy and correction

The user directly confirmed this taxonomy from the retained current-tree
evidence. This subsection is the sole
product-behavior owner. The [operator confirmation](../operator-notes.md#confirmed-mailbox-categorisation)
and retained decision dossier (git history: `docs/history/plans/mailbox-categorisation-and-email-matching/`)
preserve provenance and research context without becoming competing policy
owners.

| Received family | Confirmed examples or subtypes |
| --- | --- |
| `General` | `autoreply`; `undeliverable`; acknowledgements such as “thank you”; `general-chase`; `case-summary` |
| `billing` | payment notifications; remittances; invoice requests; `billing-query`; `general-billing` |
| `new-instruction-received` | initial work instructions: `audit`, `diminution`, `inspection`, `new-client`, `website-enquiry` |
| `non-client-related` | internal/company email from tools, services, software packages, and similar sources |
| `in-progress-cases` | `cancellation`; `case-update`; `client-chasing-for-update`; `provider-chasing-for-update`; other ongoing correspondence |
| `post-report-emails` | queries; disputes; amendment requests; similar post-report correspondence |
| `pre-instruction-emails` | Triage requests; pre-formal-instruction handling requests; images received before formal instructions |
| `internal-cc` | internal copied correspondence |

Each example is a named classification rather than material hidden in generic
`Other`. The canonical subtype spellings are: `acknowledgement` for the General
example; `payment-notification`, `remittance`, and `invoice-request` for the
billing examples; `ongoing-correspondence` for the remaining in-progress
example; `query`, `dispute`, and `amendment-request` for post-report mail; and
`triage-request`, `pre-formal-instruction-request`, and `images-received` for
pre-instruction mail. Families whose table entry names no subtype require none.

| Sent family | Confirmed meaning |
| --- | --- |
| `Report sent` | Collision Engineers’ email sending the Engineer report |
| `case-rejected` | Collision Engineers rejects a case |
| `query-sent` | Collision Engineers sends an additional query or information request |
| `additional-image-request` | existing images are insufficient and better or additional images are requested |

Reply is not a standalone recorded type. Collision Engineers’ replies to
Received messages mirror the underlying Received category with reply context;
a correspondent’s replies to Sent messages mirror the underlying Sent category
with reply context. The settled taxonomy also permits `Other`, which requires
both a new category name and reasoning.

### Classification, destination, and folder catalogue

A known classification has its own typed detailed-classification destination;
it is never collapsed into an aggregate Other queue. `Other` is only the
reasoned taxonomy extension and destination for a genuinely new
classification. `Unidentified` (formerly `Needs sorting`) is an operational
abstention when evidence is missing, unsupported, contradictory, or
ambiguous; it is never a classification.

Classification may use attributable mailbox/message identity, direction,
headers, sender/domain, fresh body text, attachment/document evidence,
provider-route tells, reply/thread signals, and a separately produced Case
correlation. `In-Reply-To` and `References` establish reply context; `RE:` is a
fallback, while `FW:`/`FWD:` does not by itself establish a reply. Quoted or
attached historic content is not fresh-work evidence. A deterministic rule
names its policy/version and predicates; otherwise an authorised staff member
records the decision and reason. The correction/history contract above retains
the evidence, actor, time, policy version, and later corrections.

| Classification | Positive criteria and exclusions | Method | Operational destination | Outlook folder type |
| --- | --- | --- | --- | --- |
| `General/autoreply` | Generated automatic-reply evidence; never quoted new-work text | route predicate or staff | Detailed: `General/autoreply` | No action |
| `General/undeliverable` | Delivery-status/non-delivery evidence for the exact message | transport evidence or staff | Detailed: `General/undeliverable` | No action |
| `General/acknowledgement` | Acknowledges receipt without a request, new work, dispute, amendment, or cancellation | staff until a predicate is accepted | Detailed: `General/acknowledgement` | No action |
| `General/general-chase` | General chase, including one referring to several Cases; never one-to-many association | staff | Detailed: `General/general-chase` | Case queries |
| `General/case-summary` | Informational summary with no new instruction or actionable request | staff | Detailed: `General/case-summary` | No action |
| `billing/payment-notification` | Payment notification, excluding a question/request | predicate or staff | Detailed: `billing/payment-notification` | Billing |
| `billing/remittance` | Remittance advice/evidence, excluding a billing question | predicate or staff | Detailed: `billing/remittance` | Billing |
| `billing/invoice-request` | Requests an invoice or invoice action | predicate or staff | Detailed: `billing/invoice-request` | Billing |
| `billing/billing-query` | Asks a billing, invoice, payment, or remittance question | predicate or staff | Queries | Billing |
| `billing/general-billing` | Billing mail fitting no more specific billing subtype | reasoned staff decision | Detailed: `billing/general-billing` | Billing |
| `new-instruction-received/audit` | Accepted provider Audit instruction evidence; a body keyword or quoted old instruction is insufficient | route predicate or staff | Receiving work | Audits |
| `new-instruction-received/diminution` | Accepted provider diminution instruction evidence | route predicate or staff | Receiving work | Diminution |
| `new-instruction-received/inspection` | Accepted provider Inspection instruction evidence | route predicate or staff | Receiving work | Instructions |
| `new-instruction-received/new-client` | Initial work from a client not represented by an accepted route | staff | Receiving work | New clients |
| `new-instruction-received/website-enquiry` | Website-origin evidence satisfying accepted independent fingerprints | route predicate or staff | Receiving work | Enquiries |
| `non-client-related` | Internal/company, tool, service, or software mail unrelated to client work | sender/route evidence or staff | Detailed: `non-client-related` | Other |
| `in-progress-cases/cancellation` | Explicit cancellation; it wins over quoted historic instructions | route predicate or staff | Detailed: `in-progress-cases/cancellation` | Cancellations |
| `in-progress-cases/case-update` | Update on ongoing work, excluding new instruction/post-report challenge | staff | Detailed: `in-progress-cases/case-update` | Case updates |
| `in-progress-cases/client-chasing-for-update` | Client asks for progress on ongoing work | staff | Detailed: `in-progress-cases/client-chasing-for-update` | Case updates |
| `in-progress-cases/provider-chasing-for-update` | Provider asks for progress on ongoing work | staff | Detailed: `in-progress-cases/provider-chasing-for-update` | Case updates |
| `in-progress-cases/ongoing-correspondence` | Other ongoing correspondence after more-specific subtypes are excluded | reasoned staff decision | Detailed: `in-progress-cases/ongoing-correspondence` | Case updates |
| `post-report-emails/query` | Question about a delivered report | route/thread evidence or staff | Queries | Case queries |
| `post-report-emails/dispute` | Challenge to a delivered report/finding | route/thread evidence or staff | Queries | Case queries |
| `post-report-emails/amendment-request` | Request to amend a delivered report | route/thread evidence or staff | Queries | Case queries |
| `pre-instruction-emails/triage-request` | Accepted Triage request; missing VRM remains Unidentified under FRD-03 | route predicate or staff | Triage | Pre-instructions |
| `pre-instruction-emails/pre-formal-instruction-request` | Known pre-formal handling request, excluding Triage | staff | Detailed: `pre-instruction-emails/pre-formal-instruction-request` | Pre-instructions |
| `pre-instruction-emails/images-received` | Images before formal instruction, excluding an accepted instruction | attachment/route evidence or staff | Detailed: `pre-instruction-emails/images-received` | Images |
| `internal-cc` | Internal copied correspondence, not the primary actionable occurrence | header/recipient evidence or staff | Detailed: `internal-cc` | Other |
| Sent: `Report sent` | Exact sent report correspondence; classification alone does not prove delivery | immutable Sent-item evidence or staff | Detailed: Sent/`Report sent` | Other |
| Sent: `case-rejected` | Exact outbound rejection | immutable Sent-item evidence or staff | Detailed: Sent/`case-rejected` | Other |
| Sent: `query-sent` | Exact outbound query/information request | immutable Sent-item evidence or staff | Detailed: Sent/`query-sent` | Other |
| Sent: `additional-image-request` | Exact outbound request for better/additional images | immutable Sent-item evidence or staff | Detailed: Sent/`additional-image-request` | Other |
| reasoned `Other` | No registry entry fits; requires a new name/reason and may not mask a known class | authorised staff only | Other | Other |
| `Ambiguous` / `Unclassified` | Multiple/no accepted predicates, or missing/conflicting evidence; no winner is invented | explicit abstention | Unidentified | none automatically |

The approved logical folder types are `Instructions`, `Audits`, `Diminution`,
`New clients`, `Case queries`, `Enquiries`, `Billing`, `Pre-instructions`, `No
action`, `Images`, `Cancellations`, `Case updates`, and `Other`. MAIL-23 binds
these types to administrator-approved exact Outlook folder identities and owns
the mailbox-scoped binding. MAIL-05 derives the message-level recommendation;
MAIL-07 owns the separate confirmed move. Triage and Unidentified receive no
automatic folder recommendation merely because they are application destinations.

Acceptance examples are a single accepted Audit instruction mapping to
Receiving work/Audits, a billing question to Queries/Billing, and an accepted
Triage request to the separate Triage workflow. A body merely mentioning
“audit”, a forwarded old instruction, simultaneous accepted matches, or
incomplete route evidence must not be promoted by guesswork.

A `general-chase` message may refer to several Cases but remains a single unlinked General source occurrence: Pegasus neither copies it nor creates one-to-many Case associations. A `case-summary` is likewise retained as non-actionable General correspondence and creates no intake, Triage, or Case work.

Classification, application queue, Triage routing, and Outlook folder
destination are separate facts. `new-instruction-received` is a Received family
and no equivalent Sent family is confirmed. That direction boundary does not
choose between multiple simultaneously matching rules: exact multi-rule
precedence and any confidence display remain unresolved in [open
decisions](../open-decisions.md#mailbox-rule-activation-automatic-matching-and-confidence-display);
the delivered QDOS classification policy records simultaneous category matches
as the explicit ambiguity outcome with no invented winner.

Every automated or human categorisation decision retains the source identity,
policy key and version, outcome, material evidence references, applicable
confidence or ambiguity facts, actor or automated identity, and time. An
authorised correction, override, reversal, link, unlink, or relink preserves the
original decision and appends the reason where it overrides or reverses a prior
decision, structured before/after values, actor, event time, outcome, and
policy/evidence references to permanent business history. Dependent queues,
routes, counts, and events recompute deterministically without deleting source
or decision history.

A rule change never silently reinterprets historical decisions. Cohort
re-evaluation requires an explicit approved operation; a technical replay is
idempotent and is not a new business decision. A wrong case allocation follows
the reasoned `Created in error` replacement route and never reuses a reference.
Message/file bodies, credentials, tokens, and secrets do not belong in
permanent action history; routine polling, retry, lease, and adapter mechanics
remain telemetry.

Administrators maintain one global Pegasus allowlist of exact Outlook category
display names. Each entry has a server-owned internal identifier and is Active
or Disabled; entries are disabled rather than deleted. MAIL-13 accepts only the
internal identifier and Core reloads an Active entry's display name before any
exact-message action. The catalogue stores no Graph identifier or colour,
performs no Outlook master-category synchronization, and supplies no search,
Case-linking, or generic mailbox-rule behaviour.

At the allocated `Next / 0.3.0` mailbox-workspace activation, each approved mailbox has an exact mailbox filter and queue scope. The email quick preview is keyboard- and screen-reader-accessible, opens on pointer or keyboard intent without clipping or obscuring adjacent controls, and restores the selected message when that intent moves away; the pane stays visible with its navigation links rather than dismissing to blank. It is evidence navigation only: previewing never changes classification, association, read state, Case state, or source custody.
The workspace does not include `View in Outlook`: operator review accepted that
the in-app full message, attachment and thread view provides the needed value.
It therefore creates no Outlook-navigation integration, action, or external
access requirement.

The default workspace view is the incoming Inbox across all approved mailboxes;
folder-specific, mailbox-specific, queue and search views are explicit
refinements. Sent mail and read-only Deleted Items search remain separate
folder scopes. There is no historical backfill (D22, 2026-09-01): the
workspace shows retained mail from the genuine retention-start boundary of
each approved mailbox onwards, names that boundary, and states that mail
before it is absent. Nothing earlier is reconstructed or implied. General mailbox search includes retained message bodies,
attachment filenames and searchable attachment content. An unsupported or
unsearchable attachment remains visibly so; it is not silently omitted.
Search remains within the current mailbox/folder scope unless the operator
explicitly broadens it.
Search returns individual messages, not collapsed conversation groups, because
classification, association and folder actions apply to exact message identity.
Each result identifies whether its match is in the message body, an attachment
filename or an attachment's searchable content, naming the matching attachment
where applicable.
The Inbox and search-result lists use accessible pagination, not infinite
scrolling.
The all-Inboxes view defaults to newest received message first.
Active mailbox, folder, queue and search filters remain visible and are
preserved when returning from message or Case detail.
On a fresh visit, the workspace resets to the default all-Inboxes view rather
than retaining a cross-session user preference.
The workspace provides an explicit manual refresh, last successful update time,
and distinct stale and unavailable states rather than silently presenting old
data. The stale threshold is a fixed 15 minutes since the last successful
update (D22, 2026-09-01); it is not configurable and is not derived from load.
It does not refresh automatically while an operator is reading or acting.
Refresh preserves the active mailbox, folder, queue, search filters,
page and open-message context when that message remains available.
If it no longer remains in that scope, its detail stays visible with an
explicit no-longer-in-this-view state and a return-to-list action.
Each Inbox row includes a short message-body excerpt beneath sender and subject.
Inbox rows visibly distinguish retained read and unread state, but this
workspace does not change that state.
Opening a message preserves the originating list filter and position, shows the
full retained message, attachments and a chronological
thread, and exposes current classification, queue, processing outcome and Case
association before any action. A quick preview remains evidence navigation
only: it shows sender, subject, timestamp, excerpt, classification,
association and attachment names, but no mutation controls. Case linking starts
with deliberate Case search, then a target summary,
reason and explicit confirmation; it may occur while classification remains
unresolved when the link evidence itself is sufficient.
Thread display includes only retained messages within approved mailbox/folder
scope; a matching thread identity never fetches or exposes other messages.
Classification, linking and folder-move actions are available only from opened
message detail, never from an Inbox row or quick preview.
UI-10 provides no bulk classification, linking or folder-move action: each
decision applies to one exact message.
After a classification change is saved, a recommended Outlook-folder move is a
separate explicit confirmation; it is not part of classification confirmation.
Staff may confirm only the designated folder from the applicable classification
policy. A different destination requires correction of that classification, not
an arbitrary folder choice.
If a later reclassification produces a different designated folder, Pegasus
offers another separate explicit move confirmation and never moves it
automatically.
If that move fails, the saved classification remains intact, the failure is
visible, and only a staff-initiated retry may repeat the move.
After a successful move, the message leaves the Inbox view and remains
findable through its destination-folder scope or search; it is not duplicated.
For retained inbound mail, automatic Case association is deliberately
conservative. A message may associate only when its normalised vehicle
registration identifies exactly one current, non-archived Case system-wide, or
when its exact mailbox-and-conversation thread identifies exactly one current
Case. If both forms of evidence identify a Case, they must agree. A supplied
registration with zero or several candidates, several thread candidates,
contradictory candidates, or evidence that changes before the serializable
write causes abstention. The inbound Case/PO text is never a matching key. A
first message may therefore qualify by unique registration before its thread
has an association; a later message without a registration may qualify from
the exact thread. The system-worker association is append-only, idempotent and
uses the ordinary current-association and staff reversal precedence. It does
not mutate the mailbox.
Selecting a Case association opens that Case workspace in the same tab; Back
returns to the exact message detail and originating list context.
Each Case workspace also exposes its associated correspondence as a contextual
filtered view in one chronological history of linked received and Sent items;
it defaults to newest first with an explicit oldest-first option. Cross-mailbox
browsing and reconciliation remain in the email-management workspace.

The allocated workspace includes read-only search of Deleted Items within each
exact approved mailbox/folder scope. It does not introduce a backlog scan,
reconstruction, bulk replay, Case allocation, or mailbox mutation.

Which mailboxes an Outlook/Graph inbound route reads is settled by the approved
mailbox allowlist, not by deployment configuration. `ApprovedMailbox.Id` is the
durable source identity; the Graph mailbox and folder coordinates are replaceable
cursor scope, and each mailbox holds its own lease and its own durable cursor, so
one mailbox's failure or backlog never affects another. Each mailbox has its own
fresh-start activation cycle: enabling begins a new cycle at a recorded UTC
activation time, and mail received before that time advances the cursor but is
not retained, quarantined, passed to intake, or allocated. Disabling a mailbox
stops polling at the next tick and deletes nothing — retained messages, receipts,
assets, quarantined items, and case associations all remain visible — and
re-enabling begins a new fresh-start cycle rather than resuming the old cursor, so
mail received while disabled never becomes a backlog. Global Worker,
individual-function, and per-mailbox controls are separate, and Sent-evidence
polling stays off unless separately approved. Approving a mailbox in Pegasus
never grants Exchange access; the Microsoft 365 tenant must separately admit the
application to that mailbox, and until it does, polling that mailbox alone fails
and says so.

### Mailbox wake-up and recovery

Each enabled approved Inbox has one Microsoft Graph basic change-notification
subscription. Its durable record uses the existing approved-mailbox identity and
stores the Graph subscription id, resource scope, expiry, lifecycle state, and
last maintenance result in SQL. The shared client-state secret is protected
configuration and is never stored in the subscription row, logged, returned to a
browser, or placed on a queue.

`POST /hooks/microsoft-graph/mail` is a wake-up boundary on the Web host, not a
mail reader. For Graph validation it returns the supplied validation token as
plain text within the protocol deadline. For notifications it validates
clientState and the known active subscription, queues only the subscription and
approved-mailbox identifiers, and acknowledges promptly. It does not download,
classify, extract, associate, allocate, or mutate a mailbox message. Unknown,
expired, malformed, or wrongly scoped notifications fail closed without queuing
work or disclosing the secret.

The Worker remains the sole owner of the mailbox lease, cursor/delta read,
retention, shared intake call, and retry outcome. A wake message causes the same
idempotent delta/cursor pass as polling; duplicate or coalesced notifications are
safe. Lifecycle `missed`, `subscriptionRemoved`, and reauthorization events
schedule the same delta resynchronisation rather than introducing another mail
processing route.

Subscription maintenance runs every six hours and renews an enabled Inbox before
it is within 48 hours of expiry. Failure is visible per mailbox and leaves the
five-minute per-mailbox fallback poll active. That fallback advances the same
cursor and is recovery only; it does not create a second receipt or bypass the
fresh-start activation boundary. Disabling a mailbox prevents notification work
at claim time as well as stopping its next fallback poll.

Until the retained message has yielded an original effective sender, Inbox and
intake projections show a neutral Processing state. They never present the
forwarding desk address as the sender and later replace it as though an accepted
identity changed; the original sender appears only when established from retained
evidence.

An Outlook/Graph route must, before activation:

- use an approved test/live mailbox and exact operation;
- preserve message, conversation, folder, attachment, sender/recipient, and received/sent identity;
- maintain a durable cursor/checkpoint and idempotent occurrence processing;
- separate read/intake scopes from draft/send and administrative scopes;
- queue only stable work identifiers, never full source payloads;
- record poison/retry/dead-letter and operator recovery behavior;
- prove the real Worker timer/queue caller;
- obtain exact Sent-item/reply-chain evidence when delivery is part of a completion gate.

### QDOS-alpha evaluation boundary

The Development/local email evaluation workbench is a separately delivered
evidence harness and is not a QDOS-alpha product surface, caller, or acceptance
checkpoint. QDOS adds and claims no evaluator route, `unchecked`/`checked`
workspace workflow, evaluator command, reviewer report campaign, or
Administrator evaluator approval. A separately delivered evaluator may exercise
shared policy and produce accepted, source-labelled evidence where the shared
mail policy requires it; that call and its review mechanics remain evaluator
evidence, not QDOS delivery or activation proof. The capability inventory's
evaluator allocation boundary owns the unchanged evaluator allocations. Shared Core
mail policy, production intake, Graph replay/live adapters, and their
genuine-evidence and caller requirements remain in QDOS scope.

### Outbound correspondence evidence

Report-sent evidence associates one exact immutable Outlook Sent item from a mailbox on the Administrator-maintained allowlist with exactly one Case. The record retains the mailbox and Sent-folder scope, immutable item and conversation/reply-chain identities, authoritative Outlook `sentDateTime`, separate discovery/link times, actor or matcher identity, Case relationship, reason where required, and available recipient/artifact evidence without storing a message body in action history.

When automatic matching is absent, ambiguous, late, duplicated, or conflicting, the item remains unconfirmed until any authorised staff member reasonedly links the exact item. Any staff role may unlink or relink it with a reason; prior and current associations remain permanent, and dependent events and counts recompute deterministically. A confirmed event remains final if Outlook later moves or deletes the source item.

Confirmation proves only that the exact item existed in the approved Sent scope at confirmation. It does not prove recipient delivery, reading, content correctness, post-report completion, or another terminal outcome. Preparing, viewing, copying, or acknowledging a chaser or other message is also not evidence of sending or closure; a staff-recorded outbound action remains an attributable assertion unless the applicable exact external evidence is retained.

Triage completion uses its separate exact reply-chain evidence contract and has no subject, VRM, manual-item-selection, or manual “sent” fallback.

The local alpha must not mutate a mailbox. A Worker project, queue registration, or timer configuration is not caller proof.

### Outbound correspondence

The v1 transport has one durable operation keyed by staff actor, mailbox and
operation key, with a server-computed payload hash. Reusing the key for a
different payload fails. Reply, Reply all, Forward and Compose use an approved
mailbox with verified send capability and an encoded-message ceiling. Draft
creation and upload progress survive restart without a second send attempt.
Ambiguous provider writes remain Unknown until exact evidence resolves them.

Graph `202 Accepted` means Submitted, never Sent. Sent requires the existing
retained-MIME pipeline to correlate the immutable item, operation marker,
mailbox generation and attachment hashes. Provider sent time and observation
time remain distinct. Each enabled Sent mailbox owns its cursor and activation
boundary; old items advance the cursor without historic backfill. One failing
mailbox does not starve the others.

Report sends revalidate B's persisted report readiness and exact generation,
versions and artifacts immediately before the provider effect. Staff authority
and mailbox generation are also rechecked. No connector may invoke this send.

Staff-initiated Reply, Forward and Compose exist on the Inbox message and the
Case correspondence surfaces, and are the technical decision of
[ADR-0036](../adr/0036-outbound-mail-via-approved-mailbox.md). They are
present only when the outbound capability is composed; when it is not, the
surfaces carry no send, Flag or Delete control and no composer.

- **Who may send.** A signed-in staff member with the casework right. There is
  no autonomous, scheduled, or Automation Actor send.
- **From which mailbox.** Reply and Forward send as the approved mailbox the
  retained message belongs to; Compose sends as the default approved mailbox.
  The sending identity is never a staff member's own address or any mailbox
  outside the approved allowlist, and the composer shows it read-only.
- **What the composer carries.** To, Subject, Message, Case, and From
  (read-only). Reply and Forward preserve the retained message's reply-chain
  and conversation identity; Case defaults to the message's current
  association and may be changed before sending.
- **Reply targets.** Retain the structured MIME Reply-To addresses in source
  order; use the original From addresses only when that header is absent.
  A present but unusable header remains empty. The reply surface shows the
  retained targets and selected recipients for staff confirmation. Missing
  retained metadata or an empty target list refuses Reply and Reply All;
  neither transport Sender nor To/Cc substitutes for those targets. Starting
  a new message is an explicit staff action.
- **What is retained.** The immutable Sent item Graph writes for the send is
  the evidence, retained by the existing Sent-evidence poll under
  [Outbound correspondence evidence](#outbound-correspondence-evidence) and
  auto-linked to the Case named at send time. The draft text is not evidence
  until that Sent item exists; a send that Graph refuses leaves no evidence
  and is visible as a failure on the composer, not recorded as sent.
- **Flag.** A mailbox mutation through the same seam as the confirmed folder
  move, recorded in the message's history with actor and time; it changes no
  classification, association, or Case state.
- **Delete.** Requires a reason, moves the exact item to Deleted Items — never
  a hard delete — and is recorded in the message's history. The retained
  message, its evidence, and its associations remain; the item stays reachable
  through the existing read-only Deleted Items search. Permanent deletion is
  absent: no surface, action or tool removes a mailbox item irrecoverably
  (D22, 2026-09-01).

Local alpha and every test profile use the unavailable implementation of the
seam and never mutate a mailbox. Production activation is a separately
approved live write, and requires the Sent-evidence poll to be enabled for
the sending mailbox so that every send has its evidence.

### EVA-sent report detection

A report sent through EVA rather than from Pegasus is detected, not asserted
(EPIC-011 D10). The Sent-evidence poll recognises a report mail in the
approved mailbox when the exact Sent item matches a Case reference and
carries a PDF attachment classified as a report. On that match Pegasus
attaches the PDF to the Case as the report document, links the Sent item as
`Report sent` evidence under
[Outbound correspondence evidence](#outbound-correspondence-evidence), and
records the report-sent event that moves the Case into post-report work; the
Case's own closure outcome remains a separate reasoned step.

A Sent item with no Case-reference match, with several candidate Cases, with
a Case already holding report-sent evidence, or with no attachment classified
as a report is retained unlinked and surfaces on the Case for staff
confirmation through the existing reasoned link; it never completes
automatically. There is no manual "report sent" assertion without the Sent
item.
