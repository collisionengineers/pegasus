# Research — UI-14

## Question

How should Pegasus present operational queues for Receiving work, Queries and Other while preserving Needs sorting and Triage as distinct destinations?

## Verified findings

- FRD-08 is the governing behavioural owner and EPIC-006 requires UI, infrastructure and Automation callers to reuse one Core implementation.
- Current repository state: The current Inbox has mailbox/folder filters but no operational-destination projection/filter; UI must consume MAIL-02 policy output rather than reproduce its mapping.
- The previous-implementation material added to MAIL-01–04 is useful reference evidence for durable identity, fail-closed routing and append-only history, but its taxonomy/folder tree is not Pegasus authority.
- Repository implementation and local verification are activated by the operator's EPIC-006 instruction. Real Outlook, Graph or cloud mutation remains separately approval-gated.

## Implications

Reuse `src/Pegasus.Core/Intake/RetainedMail.cs` and the existing caller/store conventions. Keep exact-message identity, classification, operational routing, folder recommendation, Case association and transport mutation as separate facts and commands. Fail closed on missing identity, ambiguity, stale versions, unauthorized actors or unsupported mailbox state.

## Acceptance direction

Focused Core tests prove policy and validation; integration tests prove persistence/concurrency and the real Web caller; no deployment or external write is claimed by local evidence.

## Refresh — 2026-08-20 (supersedes the earlier Needs sorting assumptions)

### Question

What remains for UI-14 on current `origin/dev`/`origin/main` after MAIL-02 and INTK-007, and which existing owners and ticket branches must it reuse or sequence around?

### Verified findings

- `INTK-007` commit `abd8a923` is contained by both `origin/dev` (`b36c6666`) and `origin/main` (`2325ed4a`). The binding operator-facing abstention is **Unidentified** with an immutable `U<n>` reference and canonical reason. It replaces only the old broad `Needs sorting` meaning; **Triage remains a separate workflow and destination**. Sources: `docs/operator-notes.md#unidentified-received-material`, `docs/prd/pegasus-product.md`, FRD-08 “Unidentified mail destination”, INTK-007 body/research/proof.
- MAIL-21/22 and MAIL-02 are complete. `MailOperationalDestinationPolicy` is the one Core mapping owner and returns Receiving work, Queries, a typed detailed classification, reasoned Other, Unidentified, or Triage. `/Inbox/{id}` already computes that result live from the persisted classification dossier; no destination column or second mapping is required. Sources: `src/Pegasus.Core/Intake/Classification/MailOperationalDestinationPolicy.cs`, `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs`, TICK-044 proof.
- The remaining gap is list/query capability. On `origin/dev`, `MailWorkspaceScope` represents only mailbox and folder; `RetainedMailSummary` carries no classification/destination; and `EfRetainedMailboxMessageStore.ListAsync` filters, counts and pages only retained-message rows before later receipt/case projection. `/Inbox` therefore has mailbox/folder navigation but no queue or detailed-classification refinement. Sources: `RetainedMail.cs`, `EfRetainedMailboxMessageStore.cs`, `Pages/Mail/Index.cshtml(.cs)`.
- FRD-08 requires queue filters to coexist with mailbox, folder and later search filters; remain visible; survive detail/return and manual refresh; and apply to exact messages with accessible pagination. Known classifications keep their named detailed views, `Other` is only a reasoned new category, Unidentified is not a classification, and Triage is not collapsed into Unidentified.
- Queue filtering must occur before total-count and paging are calculated. Filtering a 25-row page after retrieval would produce false counts and omit matching mail. Translating the mapping independently inside EF would create the prohibited second policy list. Planning must choose the smallest query shape that reuses one Core-owned mapping definition and remains SQL-filtered.
- Existing conventions should be extended, not replaced: `MailWorkspaceScope`/ `ListRetainedMail` for authorized list input, `IRetainedMailQueries` for the port, `EfRetainedMailboxMessageStore` for mailbox-scoped query execution, `OperatorLabels.MailOperationalDestinationLabel` for operational labels, and the current Razor query-string/refresh/detail-return pattern for filter preservation.
- Current branch comparison matters: the local `dev` checkout is 103 commits behind `origin/dev`. The remote branches differ on `RetainedMail.cs`, message detail, and `MailWorkspaceWebTests.cs` because newer actor-display work is on dev. Ticket implementation must start from current `origin/dev`, not the local checkout or `origin/main`.
- Exact dependency/overlap audit:
  - Hard behavioural prerequisites are complete: [[TICK-009]] (MAIL-21), [[TICK-010]] (MAIL-22), [[TICK-044]] (MAIL-02), and [[INTK-007]].
  - [[TICK-053]] and [[TICK-056]] name the same four core surfaces as UI-14: `RetainedMail.cs`, `EfRetainedMailboxMessageStore.cs`, `Mail/Index.cshtml.cs`, and `MailWorkspaceWebTests.cs`.
  - [[TICK-064]] also names `EfRetainedMailboxMessageStore.cs` and `Mail/Index.cshtml.cs`; [[TICK-050]] names `RetainedMail.cs` and the retained-mail store.
  - MAIL-23 is not a semantic prerequisite for application queue filtering: FRD-08 assigns it administrator-approved Outlook-folder identity binding. It should nevertheless land first under the accepted programme order, then UI-14 must refresh against the merged files. TICK-053 should also land first because its remaining list/search work has the same read-model surface. UI-10 is the downstream assembly ticket and must consume UI-14 rather than implement its queue policy.

### Implications

UI-14 is a focused read-model/UI slice, not a new workspace framework. Extend the existing list scope and projection just enough to express one operational destination or one named detailed classification, apply that filter before SQL count/paging, and preserve the active queue through paging, refresh and message-detail return. Reuse the Core policy and existing labels; add no stored destination, migration, generic filter abstraction, bulk action, Graph write, or separate Triage/Unidentified policy.

Detailed views must expose the canonical MAIL-22 category/subtype identity. The queue navigation must include Receiving work, Queries, reasoned Other, Unidentified, and Triage as distinct choices; it must not render the superseded operator-facing phrase `Needs sorting`.

### Open questions

None. The governing terminology, mapping owner, filter persistence, paging behaviour, and dependency order are settled. Any implementation design that would duplicate the mapping or persist derived destination state must stop and return to planning.

## Focused post-merge refresh — 2026-08-20

### Exact base and landed overlap

- Read-only fetch resolves current origin/dev to 4baae5f0, the merge of TICK-052 / PR #490. TICK-053 landed at c025b39a, TICK-049 at e4d56d9e, TICK-050 at 09b42a57, TICK-051 at 708706b8, and TICK-052 at 4baae5f0.
- TICK-053 owns the current mailbox/folder/search/page list shape and the query-string return convention. TICK-049 adds current logical-folder projection; TICK-050 adds the read-only detail advisory; TICK-051 adds current automatic Case association; TICK-052 adds manual association. UI-14 must preserve all of those fields and handlers rather than redesign them.
- INTK-007 is Done. Canonical operator wording is Unidentified for the broad abstention only; Triage remains a distinct accepted workflow. The old ticket title, plan and checklist wording is superseded.

### Existing owners to reuse

- MailOperationalDestinationPolicy is the sole mapping from current MailClassificationResult to Receiving work, Queries, named detailed classification, reasoned Other, Unidentified or Triage.
- EfIntakeReceiptStore.MapMailClassificationDecision is the current persisted-decision projection already used by retained-message detail. The list must reuse it after paging for display.
- MailTaxonomy is the sole Core category/subtype catalogue. MailClassificationSelection is the existing presentation option projection and parser; detailed list views may filter its canonical non-Other options only when the policy maps them to DetailedClassification.
- MailWorkspaceScope, ListRetainedMail, IRetainedMailQueries, EfRetainedMailboxMessageStore and the existing Index/Message query-string convention are the narrow extension seams.

### Smallest SQL-safe design

Add an optional operational destination or exact canonical detailed classification to MailWorkspaceScope. ListRetainedMail rejects unknown, contradictory, reasoned-Other-as-detail, and non-detailed category scopes. MailOperationalDestinationPolicy exposes its own small immutable query criteria, and Map consumes the same criteria; Infrastructure translates those criteria against the existing classification-decision columns. Thus the mapping remains one Core-owned list while SQL filters before Count/Skip/Take. No destination is stored.

After paging, the existing receipt projection also maps the current classification and calls the policy for row display. Web parses one visible queue key, renders the five aggregate destinations plus policy-qualified named detailed views from MailClassificationSelection, and carries the key through mailbox/folder/search/page/refresh/detail/POST return context.

### Verified boundaries

This is read-only local Core/SQL/Web work. No taxonomy change, second policy, destination column, migration, write transaction, message action, generic filter framework, MCP surface, Graph/Box/cloud/deployment or external write. Documentation changes are limited to canonical UI-14 capability/title wording and stale operator-facing Needs sorting phrasing.
