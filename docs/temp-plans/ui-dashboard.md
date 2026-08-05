# UI Dashboard — task plan

Branch `task/ui-dashboard`. Second of the seven page PRs in the UI
implementation programme (`NOW.md`). Pages 1 and 7.

## What this PR owns

1. **The counts that did not exist.** `IDashboardQueries` in Core with an EF
   implementation: case-stage counts (Not ready / Review / Held), day-and-week
   activity (new cases, sent to Engineer, reports sent), and mail activity
   (received today, needs sorting). Every one is a single aggregate query.
2. **The office day and week.** `GetOperationsSnapshot` derives the boundaries
   in Europe/London with a Monday week start, so "today" is the office's today.
3. **The three-section rebuild** of the dashboard: Active cases · E-mail
   activity · Today and this week.
4. **Page 7** becomes Upload links and External work: two tables, MB sizes, a
   one-post withdraw, and no concurrency vocabulary.

## Defects closed

| Ref | Finding | How |
|---|---|---|
| B3 | Nine tiles and both workspace cards hardcoded to "Unavailable" | The counts exist; every tile renders a number, and 0 renders as 0 |
| M1 | The "Review" tile shows an intake count, not the Review case stage | Rebacked onto `CaseStageCounts.Review`, linking to the filtered case list |
| M2 | Two working screens advertised as "Unavailable" | Both are plain links; a card is the wrong shape for a link with no metric |
| M7 | Silent cap on the due-work tile | The tile is gone; the list renders only when there is something in it |
| M8 | "Staged intake artifacts" is a dead-end diagnostic | Removed from the dashboard and from the snapshot |
| page-7 | Byte counts, "Limits version", edit-mode vocabulary, raw failure codes, Box section | MB to one decimal, version integer dropped, one-post withdraw, recorded human reason, Box absent |

## What is deliberately not shipped

- **"Queries outstanding"** (an E-mail activity tile in the mockup). A query is
  outstanding when a sent `query-sent` message has no reply linked back, and
  the reply link (`EmailResponseEvidence`) hangs off `SentEmailEvidence`, which
  belongs to a Triage record — and no composition can create a Triage record
  (defect B2). The count cannot be computed truthfully, so under rule 2 the
  tile is not shipped. **Blocked** takes its place, which is a real count. This
  needs re-visiting when B2 is resolved.
- **The Engineer "To do" section.** It needs an assigned-reports-and-queries
  query scoped to the signed-in Engineer, which does not exist; the same rule
  applies. Recorded as a follow-up.
- The Active-cases tiles link to the filtered **Cases** list rather than to
  Queues tabs, because the Queues tabs do not exist until the Queues PR. They
  move when that lands.

## Also carried

- `CaseDueWork` gains `Reference`. A due-work row could previously only offer
  "Open case", which names nothing.
- `OperatorLabels.ChaseState` — the chase schedule's own state, which is not
  the case stage.

## Verification

- `dotnet build --configuration Release` — clean
- Core 403/403 (three new boundary tests: BST day start, Monday week start, and
  the Monday-itself case), architecture 73/73, integration 388 passed / 0 failed
- The browser suite's boundary test is inverted to the new invariant: zero
  `unavailable` tiles, and every rendered metric is a number
- `OperationsWebTests` now asserts the absence of every edit-mode string, the
  absence of byte counts and the limits-version integer, and the one-post
  withdraw reaching Core with the same expected versions and lease token

## Follow-ups recorded

- The `ClaimLease` / `RenewLease` / `ReleaseLease` / `RevokeBox` handlers on
  the Requests page are no longer reached from its markup. They are left in
  place in this PR because `OperationsWebTests` exercises them as Core wiring
  and the Box revoke path still exists on the case workspace; removing the
  dead Web surface belongs with the Cases work.
