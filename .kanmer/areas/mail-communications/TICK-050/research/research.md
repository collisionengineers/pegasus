# Research — MAIL-08

## Question

How should Pegasus derive safe suggested next actions from the canonical classification, operational destination, association and processing state?

## Verified findings

- FRD-08 is the governing behavioural owner and EPIC-006 requires UI, infrastructure and Automation callers to reuse one Core implementation.
- Current repository state: There is no Core next-action policy; the retained detail already projects classification, route, processing and Case association facts, so suggestions can be a pure projection rather than stored business state.
- The previous-implementation material added to MAIL-01–04 is useful reference evidence for durable identity, fail-closed routing and append-only history, but its taxonomy/folder tree is not Pegasus authority.
- Repository implementation and local verification are activated by the operator's EPIC-006 instruction. Real Outlook, Graph or cloud mutation remains separately approval-gated.

## Implications

Reuse `src/Pegasus.Core/Intake/RetainedMail.cs` and the existing caller/store conventions. Keep exact-message identity, classification, operational routing, folder recommendation, Case association and transport mutation as separate facts and commands. Fail closed on missing identity, ambiguity, stale versions, unauthorized actors or unsupported mailbox state.

## Acceptance direction

Focused Core tests prove policy and validation; integration tests prove persistence/concurrency and the real Web caller; no deployment or external write is claimed by local evidence.

## Current-state refresh — 2026-08-20 (supersedes speculative persistence/framework assumptions)

### Question

Which landed Core facts can safely derive MAIL-08 advice, which concrete action must exist first, and how can Pegasus add the smallest advisory surface without inventing a generic action framework?

### Verified findings

- Inspected `origin/dev` `b36c6666` and `origin/main` `2325ed4a`; the local `dev` checkout is 103 commits behind and is not source truth.
- The current exact-message read model already assembles the canonical inputs in `src/Pegasus.Core/Intake/RetainedMail.cs`:
  - `RetainedMailDetail.Classification.Current` is MAIL-04's current corrected classification and evidence.
  - `MailOperationalDestinationPolicy` is MAIL-02's one application-destination owner.
  - `RetainedMailSummary` carries processing outcome, allocation state and current Case association.
  - `RetainedMailDetail` also carries route disposition and exact mailbox/folder/message scope.
  - `GetRetainedMail` authorizes the actor and is called by `MessageModel.OnGetAsync` for the real `/Inbox/{id}` staff surface.
- There is no next-action policy, action registry, suggested-action entity, persistence table, or adapter on `origin/dev`. Advice is derived display state and creates no business history. Persisting it would become stale when classification, association, binding or mailbox state changes.
- FRD-08 and the operator's 2026-08-19 decision settle one concrete advisory action: an eligible folder recommendation may render **Move**, and that control must invoke MAIL-07's separately confirmed move workflow. It may not inline a mutation or accept a client-selected destination.
- That confirmed Move advice has two hard prerequisites:
  - [[TICK-047]]/MAIL-05 provides the current exact-message recommendation and unavailable/provenance state; it is itself blocked by MAIL-23.
  - [[TICK-049]]/MAIL-07 provides the actual confirmation/eligibility/move use case that the button invokes. MAIL-08 must consume its landed eligibility rather than reproduce stale-version, destination, authorization or retry rules.
  These structured dependency edges now block [[TICK-050]].
- MAIL-21/22, MAIL-02, MAIL-04 and INTK-007 are completed foundations, not new dependencies. They provide classification, destination, correction evidence and canonical Unidentified vocabulary.
- FRD-08 does not currently state that manual Case association, read/category/flag/delete, compose/reply/forward/send, or any other action appears in MAIL-08 advice. Their tickets ([[TICK-051]], [[TICK-052]], [[TICK-054]], [[TICK-088]]) are therefore context/possible future extensions, not blockers. In particular MAIL-12 is Later/0.5.0 while MAIL-08 is Next/0.3.0; making it a prerequisite would contradict the capability schedule without an operator scope change.
- The minimum honest MAIL-08 policy is consequently small: from the current detail plus landed MAIL-05/07 eligibility, return no suggestion or the concrete confirmed-move entry. It must remain advisory, re-derived on every read, and ordered deterministically without a plugin/handler/registry framework.
- A later accepted suggestion matrix may add a branch only after that action's Core contract lands. Each branch calls or consumes that action owner's eligibility/result; MAIL-08 never becomes an authorization, validation or execution owner.
- Exact overlap audit:
  - `RetainedMail.cs`: [[TICK-047]], [[TICK-049]], [[TICK-053]], [[TICK-054]], and downstream [[TICK-056]].
  - `Mail/Message.cshtml.cs` (and the same detail markup): [[TICK-047]], [[TICK-049]], [[TICK-051]], [[TICK-052]], [[TICK-054]], [[TICK-057]], and [[TICK-088]].
  - `RetainedMailTests.cs`: [[TICK-047]] and likely the action contracts that extend retained-mail state.
  - `MailWorkspaceWebTests.cs`: [[TICK-047]], [[TICK-053]], [[TICK-056]], and [[TICK-057]].
  - `EfRetainedMailboxMessageStore.cs` is context only for existing projection. MAIL-08 needs no schema, query, transaction or adapter change.

### Implications

Wait for MAIL-05 and MAIL-07, then refresh their actual merged recommendation and eligibility symbols. Add one concrete Core advisory projection on the existing exact-message read path, with zero-or-one confirmed Move suggestion for this slice. The Web detail renders that Core result and delegates the button to MAIL-07. Add no new persistence, Infrastructure implementation, generic action descriptor framework, dynamic registry, optional callbacks, external write, or Automation tool.

Tests prove no suggestion when recommendation/move eligibility is unavailable or stale; exactly one Move suggestion when the landed owner says it is eligible; deterministic re-derivation after classification/folder/move state changes; the button targets MAIL-07 rather than mutating inline; and reading suggestions writes no history or mailbox state.

### Open questions

None for the minimum confirmed Move slice. Broader advisory actions are explicitly deferred until an operator-owned matrix names them and their Core action contracts have landed; they must not be inferred from the mere existence of action tickets.
