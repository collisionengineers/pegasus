## 2026-08-22 — what is left, exactly

Deployed in release 18 (`1f3be493`), carried to release 20 (`05fe7a7f`).
Confirmed present at the deployed HEAD:

```
_CaseHistory.cshtml:10   <h2 id="case-notes-title" class="section-label">Notes</h2>
```

The tab reads **Notes**, the add-note form is on it, and operator notes are
written as `CaseHistoryEntity` rows with `EventType = "operator_note"` so they
share the one append-only timeline with system entries. No new table and no
migration — the timeline stayed one list.

The companion copy fix is done and closed ([[CASE-016]]): grepping the deployed
`Pages/` tree for "Immutable" now returns only C# identifiers and a
`System.Collections.Immutable` using — no operator-facing text. The nearest case,
`Mailboxes.cshtml.cs:356`, maps its enum to *"This mailbox's address cannot be
changed once saved. Disable it and add a new one."*, so nothing leaks.

**Single gate:** the rendered page has not been viewed, because the case
workspace requires an authenticated staff sign-in I must not perform. What is
unverified is the rendering, not the change.

## 2026-08-22 — a defect found by running the page, and fixed

The earlier note here said the only unverified thing was "the rendering, not
the change". That was wrong, and running it proved it.

Local `DevelopmentOffline` run against LocalDB, posting a note through the real
form:

```
POST /Cases/{id}/Tasks?handler=AddNote   -> 302, "The note was added."
CaseHistory                              -> 1 row, EventType = operator_note
Notes tab                                -> "Notes 0 … Nothing is recorded yet."
```

`EfCaseNoteStore` wrote to `CaseHistory`. The Notes tab reads
`CaseWorkflowEvents` (`EfCaseQueryStore.cs:181`). Two different tables. Every
note was saved, acknowledged, and never shown.

Nothing threw and CI was green, because the Core command's tests drive a
`RecordingStore` fake and no test asserted the note came back through the query
the page uses. That gap is the real lesson: a fake at the port boundary proves
the command, never the wiring.

**Fixed** in PR #513 — the write moves to `CaseWorkflowEvents`, matching every
other writer of that timeline, with `CaseNotePersistenceTests` pinning it
against real SQL. No schema change; both runtime roles already hold
`SELECT, INSERT` there.

Verified after the fix on the same route:

```
Notes 1
When                 Event  Actor                              Detail
22 Aug 2026 07:36    Note   development-offline-administrator  Chased the third-party engineer…
```

The event renders through the operator-label map as **Note**, not
`operator_note`, and the time is Europe/London.

## Design-system check, same run

Rendered HTML for the case page and the Notes tab carries **none** of the
banned vocabulary (`intake`, `projection`, `lease`, `durable`, `caller`,
`bytes`, `artifact`, …) and no "Immutable". The note field is a label and a
control with no hint sentence. The panel renders with an available action, so
its empty state is permitted rather than a bare empty panel.
