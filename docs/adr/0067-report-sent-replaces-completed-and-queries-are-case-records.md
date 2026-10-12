---
id: ADR-0067
status: accepted
date: 2026-10-11
supersedes: []
superseded_by: []
related_capabilities: [CASE-16, CASE-23]
related_frd: [FRD-13, FRD-15, FRD-16, FRD-21, FRD-10]
tags: [case-lifecycle, queries, persistence, migration]
---

# ADR-0067: Report sent replaces Completed, and queries are Case records

## Status

Accepted on 11 October 2026. The operator ruled that sending a report does
not complete a Case: the last stage is **Report sent**, and a query is a
record on the Case with a true/false "active query" mark, not a sibling
state. [FRD-13](../frd/frd-13-case-lifecycle-and-workflow.md#report-sent-and-queries)
owns what staff see and do. This record owns how the states collapsed, where
a query lives and which code may write it.

## Context

Until this change the lifecycle stored a post-report state (shown as With
Engineer), a Completed state reached by **Mark completed**, and a Query
state entered when a post-report e-mail joined a Completed Case and left
when any reply to it was observed. Three readers of "is the Case in Query"
drifted from three writers, Completed and Query were mutually exclusive, a
query logged by phone had no home, and a reply resolved the query whether
or not it answered it.

## Decision

1. `CaseLifecycleState` loses `PostReport`, `PostReportComplete` and `Query`
   and gains `ReportSent`. `CaseClosureOutcome.PostReportComplete` and the
   `case_completed` event go. Report sent keeps the post-report state's
   editability.
2. A query is a `CaseQueries` row: `Source` (Mail or Staff), the tagged
   receipt or the logged channel and details, who raised it and when, and
   how and when it closed (Resolved or Withdrawn). `CaseWorkflows.HasOpenQuery`
   is a copy kept in step so every workflow read, queue count and filter has
   the Query stage without a join.
3. One Core rule, `CaseQueryRules`, says when a joining post-report e-mail
   raises a query (only on a Report sent Case, once per receipt), when a
   leaving one withdraws it, when a flagged reply resolves, and that the
   Query stage is any open Case with an open query. One Infrastructure
   ledger, `CaseQueryLedger`, is the only writer of the rows and the mark;
   it runs inside the transaction that links, corrects, unlinks, mutates or
   observes the Sent item.
4. Resolution is all-at-once and comes from three callers of the same
   ledger: the Resolve query action (staff page and MCP), a reply whose
   `StaffMailSendOperations.ResolvesCaseQueries` is set once its Sent item
   is observed, and nothing else. The automatic resolve-on-reply is gone.
5. The migration `20261011191701_ReportSentAndCaseQueries` is destructive
   and forward-only: every post-report, Completed and Query Case becomes
   Report sent (held Cases through `PreHoldState`), a Query Case keeps one
   open Mail query per linked post-report receipt, and the Completed closure
   outcome is cleared. History rows keep their old event names as
   provenance; no reader parses them.

## Consequences

- The Work Centre's Completed figure has no meaning and goes; Reports sent
  counts each Case's first report send, so it no longer agrees with MI-01
  in a week with a re-send.
- A Report sent Case has no queue; the Query queue lists every open Case
  with an open query, whatever its state, and a query outlives Return to
  Engineer.
- `CaseLifecycleRules.IsClosed` (staff removal) treats Report sent as the
  resting state; the cancellation notice, original-report recognition and
  the Engineer's mail notifications treat it as open, as they treated the
  post-report state.
- Open PR 1126's case-state mail classification rebases onto this: a mail
  it classifies post-report on a Report sent Case raises a query through the
  same ledger, which is the only automatic entry so far.
