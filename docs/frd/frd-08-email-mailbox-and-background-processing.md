# FRD-08: Email, mailbox, and background processing
> Owner capabilities: MAIL · Migrated from docs/requirements.md · UI behaviour: docs/design.md

## Email, mailbox, and background processing

The target product covers the approved mailbox estate and full source messages; the focused alpha mailbox is only the first caller. Mailbox inventory and current-system roles remain in [operator notes](../operator-notes.md).

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

At the allocated `Next / 0.3.0` mailbox-workspace activation, each approved mailbox has an exact mailbox filter and queue scope. The email quick preview is keyboard- and screen-reader-accessible, opens on pointer or keyboard intent without clipping or obscuring adjacent controls, and dismisses when focus moves away. It is evidence navigation only: previewing never changes classification, association, read state, Case state, or source custody.
The workspace does not include `View in Outlook`: operator review accepted that
the in-app full message, attachment and thread view provides the needed value.
It therefore creates no Outlook-navigation integration, action, or external
access requirement.

The default workspace view is the incoming Inbox across all approved mailboxes;
folder-specific, mailbox-specific, queue and search views are explicit
refinements. Sent mail and read-only Deleted Items search remain separate
folder scopes. General mailbox search includes retained message bodies,
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
data. It does not refresh automatically while an operator is reading or acting.
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
