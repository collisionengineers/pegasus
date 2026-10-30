---
id: ADR-0085
status: accepted
date: 2026-10-27
supersedes: []
superseded_by: []
related_capabilities: [CASE-16, CASE-23, MAIL-02]
related_frd: [FRD-10, FRD-12, FRD-13, FRD-15, FRD-16, FRD-17, FRD-21]
tags: [queries, mail, case-lifecycle, work-centre]
---

# ADR-0085: Mail is instructions or Case correspondence, and queries are assignable records

## Status

Accepted on 27 October 2026. The operator signed off the v42 design
("I sign off on this design"), confirming the 26–27 October rulings in the
design archive notes. This record amends
[ADR-0067](0067-report-sent-replaces-completed-and-queries-are-case-records.md):
a query is still a record on the Case, not a state, and the purple Query
stage still overlays the stored state while any query is open, but queries
are no longer resolved all at once and they are assignable. It supersedes
[ADR-0072](0072-only-images-become-unidentified-and-no-case-found-mail.md)
in part — the case-state classification arms that rewrote a linked e-mail's
category, and the Inbox-scope consequence of that rewrite. The three-category
mail model those arms sat inside is recorded here and implemented by the
following change.

## Context

ADR-0067 made a query a `CaseQueries` row and Resolve query closed every open
query on the Case together. A query had no assignee and could not be
reopened. Tag as query was a classification correction. The reply composer's
Also resolve query tick closed every open query when the Sent item was
observed. Eleven mail categories, plus a case-state rewrite, decided which
linked e-mail became a query.

## Decision

- A new query is Unassigned until a staff member takes it or assigns it.
  Assign and Take need no Case edit lease, no expected version and no reason.
  Assigning a query to someone other than the actor raises Query assigned,
  linking to `/Cases/{id}?section=queries`. Query received still notifies the
  Case's assigned Engineer when a query is opened from an e-mail.
- Each open query is marked done on its own. The closed chip and column read
  Done. Resolve query leaves the Actions menu. While a query is open the Next
  action reads Answer query: Reply when the oldest open query is an e-mail,
  Mark done when it was logged.
- Any staff role and Automation may reopen a Done query (resolved, not
  withdrawn) with no reason while the Case is not archived or closed.
- At most one non-withdrawn query exists per e-mail. Open query replaces Tag
  as query, offered once the report is sent and the e-mail holds no
  non-withdrawn query. A Done query is reopened from Queries. Unlinking the
  e-mail withdraws its open query. There is no separate withdraw button.
- Mark query done on a reply is offered only when replying to a message that
  holds an open query, and marks only that query done when the Sent item is
  observed.
- Queries open only once the report is sent. Query remains the purple stage
  word while any query is open.
- The Engineer activity report and the Case list count `CaseQueries` rows
  that are open or Done, not withdrawn, rather than messages classified Query.
- Work Centre Needs attention gains a Query kind. Office lists each
  Unassigned open query, with Take. Mine lists those assigned to the viewer.
  Cases › Queries stays one row per Case.
- Mail categories shrink to three: New instruction (with Case type), Triage
  request and Cancellation. Every other e-mail has no category: linked it is
  Case correspondence, unlinked it is No Case Found. Automatic queries come
  from deterministic criteria and the AI classifier only. That category
  change is the following delivery; this record is its decision. (A fourth
  category, Pre-Pegasus case, was added on 30 October 2026 under R550; FRD-08
  owns the list.)

## Consequences

- `IOpenCaseQuery`, `IMarkCaseQueryDone`, `IReopenCaseQuery` and
  `IAssignCaseQuery` replace `IRaiseCaseQuery` and `IResolveCaseQueries`.
  `CaseQueryLedger` remains the only writer.
- `pegasus_case_action` no longer logs or resolves queries.
  `pegasus_case_query` and `pegasus_mail_open_query` are the Automation
  routes. `pegasus_mail_send` takes `markQueryDone`.
- The migration `AssignableCaseQueries` adds the assignee columns and the
  Query assigned notification cause. It is additive.
