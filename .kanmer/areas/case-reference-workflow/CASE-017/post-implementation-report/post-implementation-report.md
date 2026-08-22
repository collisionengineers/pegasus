# Post-implementation report

**Branch:** `task/qdos26009-operator-fixes` · **PR:** #506 · **Commit:** `5414997d`

## What was built

The tab reads **Notes**. A note is a `CaseHistoryEntity` with event type `operator_note`,
so it joins the same ordered, attributed, append-only timeline as everything Pegasus does
to the case. Core `AddCaseNote` validates and trims it, bounds it at 2000 characters and
refuses an empty one; `EfCaseNoteStore` writes it, idempotent by operation key.

**No new table and no migration.** That was the design decision, not a shortcut: a separate
notes store would have needed its own ordering, its own attribution, its own append-only
guarantee and a merge at read time — four things to keep in step with the timeline that
already has them.

## Three judgements, stated so a reviewer can disagree

**No edit lease, no expected version.** A note adds to the record rather than changing the
case. Requiring the lease would make writing one contend with an engineer editing the same
case, for no safety gain.

**Staff only.** The first draft relied on `StaffAuthorization.Require(…, PerformCasework)`
alone — and that admits the Automation Actor. A test written for exactly that case failed,
which is how the gap was found rather than shipped. Automation already records what it does
on this timeline under its own events; letting it author a *note* would put machine text
where a colleague's words are expected.

**The last column is now "Detail" rather than "Reason".** For a system entry it is still
the reason; for a note it is the note. One column, honest for both.

## Departure from the plan

The plan named `Cases/Tasks.cshtml.cs` as the handler's home, which is where it went — but
without the `ExecuteCaseCommandAsync` helper every other handler there uses, because that
helper is built around the edit lease this action deliberately does not take.

## Evidence

- `Pegasus.Core.Tests` — 916 passed, 4 new in `AddCaseNoteTests`
- Full solution builds clean
- Live: a note added through the UI appearing beside the DVLA lookup entry — Phase 6

## Design compliance

The surface is a label, a textarea and a button. No guidance sentence was written; the
approved necessary-copy list is closed.
