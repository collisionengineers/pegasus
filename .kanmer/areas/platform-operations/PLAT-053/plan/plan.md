# Plan — PLAT-053

## Search before you build

Checked `src/Pegasus.Core` for an existing owner of the persisted
`ExternalWorkItems.State` vocabulary (`pending`, `dispatching`, `queued`,
`processing`, `completed`, `failed`): no enum or constant set names these six
persisted codes. Core does own `EvaSubmissionWorkState`
(`Pending`/`RetryScheduled`/`Processing`/`Completed`/`Failed`) as the business
state for an EVA submission, but that is a narrower, already-mapped concept —
`RetryScheduled` collapses onto the persisted `pending` code, and Core has no
type for the three purely-persistence phases `dispatching`/`queued`/
`processing` that only Infrastructure's dispatch/lease machinery needs.

The codebase's existing convention for this shape is a per-store private
static `ToCode`/`ParseState` pair (`EfCaseAcceptanceStore.ToCode`,
`EfIntakeReceiptStore.ToCode`, `EfAiWorkRequestStore.ParseState`,
`EfApprovedMailboxStore.ParseState`) — but those all serve a single store
class. PLAT-053's vocabulary is read and written by three separate classes,
so a single store's private helper cannot be the shared owner.

## Decision

Persisted string forms are a persistence-level concern with no full Core
owner (see above), so the single owner is one `internal static class`
(`ExternalWorkStatePersistence`) in Infrastructure, next to the three
callers:

- Six `const string` fields for the persisted codes (the ticket's suggested
  `ExternalWorkStates` name was renamed to make clear it owns *persistence*
  codes, not a Core business concept).
- `ParseEvaSubmission(string, int attemptCount)` / `FormatEvaSubmission(...)`
  — the one place that maps the persisted string to/from Core's
  `EvaSubmissionWorkState`, replacing the inline switch that used to live in
  `EfEvaSubmissionWorkStore` and the duplicated unknown-state guard.

This is the smaller of the two options in the ticket brief and matches its
own suggested shape. No new interface, no mapper hierarchy — one class.

## Steps

1. Add `ExternalWorkStatePersistence.cs` with the six constants and the two
   EVA-submission mapping methods.
2. Replace every literal in `EfExternalWorkStore.cs` with the constants;
   leave the two `Case.CustodyState = "failed"` sites alone (different
   vocabulary).
3. Replace the inline switch/guard in `EfEvaSubmissionWorkStore.cs` with
   `ParseEvaSubmission`/`FormatEvaSubmission`; replace remaining literal
   comparisons with the constants.
4. Replace the two terminal-state literals in
   `EfEvaSubmissionQueries.GetActivityAsync` with the constants.
5. Build Release; run the EVA-submission / custody-outbox / service-health
   focused test filter to prove behaviour is unchanged.
6. Commit, push. Do not touch the fourth-plus copies found elsewhere (see
   `files` doc) — out of scope for this ticket's named "Owns" list; flagged
   for a follow-up ticket instead of silently absorbed.

## Out-of-scope defects found (not fixed here)

The same `ExternalWorkItems.State` literals also appear in
`EfVehicleLookupWorkStore.cs`, `EfAutomaticEvaSubmissionStore.cs`,
`EfQueuedCustodyProcessor.cs`, `EfOperationsStore.cs`,
`EfCaseWorkflowStore.cs`, and other external-work producers. This ticket's
"Owns" list named exactly three files; expanding to the rest is materially
more surface than this fix was scoped for. Recommend a follow-up ticket
(e.g. PLAT-05x) to fold every remaining producer/consumer onto
`ExternalWorkStatePersistence`.

## Simplification pass

n/a at plan time — the implementation is already the minimal shape (one
constants+mapping class, no new abstraction). Reuse note: no new
abstraction layer, interface, or mapper hierarchy introduced, per the
ticket's explicit prohibition.
