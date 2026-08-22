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
