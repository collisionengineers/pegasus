# Files — TICK-057: UI-14 categorised mail queues

*Surveyed on current `origin/dev` (`b36c6666`) and compared with `origin/main` (`2325ed4a`). The local `dev` checkout is stale and is not an implementation base.*

## Where the change lands

| Path | Why |
|---|---|
| `src/Pegasus.Core/Intake/RetainedMail.cs` | Extend the existing authorized workspace scope and list projection to represent an optional operational destination or named detailed classification. Preserve mailbox/folder scope and reject invalid filter values in `ListRetainedMail`. |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` | Join the current classification decision and apply the selected queue/category before SQL count and pagination. Reuse the Core mapping definition; do not persist a derived destination or duplicate the classification-to-destination table. |
| `src/Pegasus.Web/Pages/Mail/Index.cshtml.cs` | Parse the queue query parameter, pass it through the Core use case, and preserve it through mailbox/folder changes, pagination and manual refresh. |
| `src/Pegasus.Web/Pages/Mail/Index.cshtml` | Render accessible queue navigation, active-filter state, detailed classification identity, and honest filtered empty states using existing tab/table conventions. Use **Unidentified** and keep **Triage** distinct. |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` and `Message.cshtml` | Carry the originating queue/category through opened-message detail, correction posts, and return links so classification changes do not silently lose list context. Do not add another destination calculation. |
| `tests/Pegasus.Core.Tests/Intake/Classification/MailOperationalDestinationPolicyTests.cs` | Extend only if the existing policy needs a reusable query description; keep one exhaustive mapping table and prove Unidentified/Triage separation. |
| `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` | Prove queue/category filtering happens before count/page, remains mailbox/folder scoped, and returns the current corrected classification. |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Prove the real authenticated caller, named detailed views, separate Receiving work/Queries/Other/Unidentified/Triage filters, paging/refresh/detail-return preservation, invalid filter refusal, and empty states. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `src/Pegasus.Core/Intake/Classification/MailOperationalDestinationPolicy.cs` | The sole classification-to-operational-destination owner. Its result retains the detailed `MailCategory`; list filtering must consume this owner rather than recreate its switch in Web or Infrastructure. |
| `src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs` | MAIL-22’s canonical Received/Sent families, subtypes and reasoned Other validation. Do not create a second category list. |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | Existing operational-destination labels, including canonical Unidentified and distinct Triage. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Governs the catalogue, detailed views, queue/filter persistence, exact-message semantics and no automatic folder action. MAIL-23 owns approved Outlook-folder identity binding, not UI-14’s queue filter. |
| `docs/operator-notes.md` and `docs/prd/pegasus-product.md` | INTK-007’s binding vocabulary: Unidentified replaces only the old broad Needs sorting outcome; U-reference identity and Triage remain distinct. |
| `docs/design/README.md` | Existing tabs, keyboard navigation, empty/error/focus states, desktop/zoom acceptance and the rule against duplicated UI policy. |
| EPIC-006 `context.md` | One Core implementation across Web/Worker/Automation and no local-alpha mailbox mutation. |
| [[INTK-007]] proof and [[TICK-044]] proof | Confirm the canonical rename is on both remote branches and the Core destination policy already has a real message-detail caller. |

## Ripple effects and exact overlaps

- Hard prerequisites already complete: [[TICK-009]], [[TICK-010]], [[TICK-044]], and [[INTK-007]].
- [[TICK-053]] and downstream [[TICK-056]] overlap exactly on `RetainedMail.cs`, `EfRetainedMailboxMessageStore.cs`, `Mail/Index.cshtml.cs`, and `MailWorkspaceWebTests.cs`.
- [[TICK-064]] overlaps on `EfRetainedMailboxMessageStore.cs` and `Mail/Index.cshtml.cs`; [[TICK-050]] overlaps on `RetainedMail.cs` and the retained-mail store.
- [[TICK-047]], [[TICK-049]], [[TICK-051]], [[TICK-052]], [[TICK-054]], and [[TICK-088]] all name `Mail/Message.cshtml.cs`; do not run UI-14 concurrently with those branches without explicit coordination.
- Accepted sequencing: land MAIL-23 and MAIL-11 first, refresh this map from merged `origin/dev`, then implement UI-14 before UI-10 assembly. MAIL-23 is a coordination/file-overlap predecessor, not a behavioural prerequisite for queue mapping.
- `docs/capabilities.md` changes only when delivered evidence changes the UI-14 row. Deployment/current-state documentation belongs to the owning release ticket.

## Out of scope

No taxonomy change, new queue policy, stored destination column, migration, generic mail-filter framework, folder recommendation/move, search implementation, Case association, message mutation, compose/send, bulk action, Automation tool, Graph/cloud write, deployment claim, or rewrite of internal legacy enum names whose operator presentation is already canonical.
