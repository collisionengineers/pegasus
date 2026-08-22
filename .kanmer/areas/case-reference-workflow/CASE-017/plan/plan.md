# Plan

Committed in `5414997d`.

## One timeline, two kinds of entry

The operator asked for notes to sit *alongside* system messages, not in a separate list. So
a note is recorded as a `CaseHistoryEntity` with event type `operator_note`, and the Actor
column — which already renders an automation actor with its own chip — is what
distinguishes them.

That choice is what avoids a new table, a migration, a second query, a second ordering and
a second set of retention rules. It also means a note is append-only for free, which the
ticket required: a note must not become a way to revise the record.

## Three judgements worth stating

**No edit lease, no expected version.** A note adds to the case's record rather than
changing the case, so writing one must not contend with an engineer editing it. Requiring a
lease would have made notes something you fight for.

**Idempotent by operation key**, as every other mutation is, so a resubmitted form cannot
leave the case wearing the same note twice.

**Staff only.** The Automation Actor holds `PerformCasework` and already records what it
does on this timeline under its own events. Letting it author a *note* would put machine
text where a colleague's words are expected. The first draft relied on `StaffAuthorization`
alone and let automation through; a test written for that case caught it.

## Acceptance

- The tab reads **Notes**. ✅
- System entries and operator notes appear in one ordered, attributed timeline. ✅
- A staff member can add a note; it is trimmed, bounded at 2000 characters, and empty is
  refused. ✅
- Automation cannot author one. ✅
- Notes cannot be edited or deleted — the timeline is append-only and no surface offers it. ✅
- Live: a note added through the UI appears beside the DVLA lookup entry — Phase 6.

## Simplification pass

2026-08-22. The whole design is the simplification: reusing the history row instead of
introducing a parallel notes concept removed a table, a migration, a query and a merge step
that a separate store would have needed. No findings deferred.
