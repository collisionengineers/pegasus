# Plan — MAIL-025

Branch `task/mail-025-inbox-port`, worktree
`../pegasus-worktrees/mail-025-inbox-port`. Build only (no tests/snapshots —
orchestrator runs the wave loop). Commits `feat(mail): … (MAIL-025)`, PR to
`dev`, stop at the open PR.

## Steps

1. **Core scope/count slice** — `RetainedMail.cs`: drop the unused
   `AttachmentFileNames`/`AttachmentNames`; keep `UnreadOnly`/`OldestFirst`
   and `CountAsync` (reuses `Normalize` and `StaffAuthorization.Require`,
   the same guards `ExecuteAsync` uses). Core test fake + tests updated in
   the same slice. Reuse: existing `NormalizeSearchTerm`/`Normalize`.
2. **Infrastructure slice** — `EfRetainedMailboxMessageStore`: extract the
   ListAsync filter body into `BuildMatches(context, scope)`; `CountAsync`
   runs `CountAsync` on it; `UnreadOnly` adds `!IsRead`; `OldestFirst`
   flips the two `OrderByDescending` calls. Reuse: the existing filter
   pipeline verbatim — no second implementation of any filter concept.
3. **List page slice** — `Index.cshtml.cs` + `Index.cshtml` port per the
   file map. Reuse: `_FreshnessBanner`, `_StatusChip`, `OperatorLabels`
   time/size helpers, existing `AggregateViews`/`DetailedViews`/
   `TryParseQueue`, `MailBodyPresentation`, site.js `[data-mail-preview-*]`
   contract and `data-auto-submit`. No new CSS, no inline styles, no
   page-specific classes.
4. **Message page slice** — `Message.cshtml` port; handlers untouched.
   Reuse: `_ReasonDialog` (link/unlink), existing correction/move dialog
   forms byte-identical inside restyled wrappers, `_StatusChip`,
   `MailBodyPresentation`, `MailLogicalFolders.Definition`.
5. **Test slice** — update both owned Web/Browser test files and the Core
   fake/tests to the ported markup; keep every behavioural assertion.
6. **Build** — `dotnet restore --locked-mode`, `dotnet build -c Release
   --no-restore`; fix until green. No test run.
7. **Simplification pass** over the branch diff (reuse, simplification,
   efficiency, altitude); dispositions recorded below under a dated heading.
8. **PR** — "MAIL-025: Port the Inbox list and message pages to the
   Integrated Operations Workspace" → `dev`; report PR URL, diff stat,
   dead-agent work disposition, out-of-scope findings.

## Acceptance conditions

- Inbox list renders header/filter bar/scope rail/messages/preview per
  EPIC-011 context §1.3 with PLAT-029 classes only.
- Sort toggle flips newest/oldest server-side (`sort` param); pagination
  bounded; Unread scope works; counts per scope render.
- Message page: record head, tabs with count, decision card, corrections
  timeline, attachments table, case tab machinery — all existing handlers
  (correction, folder move, link/unlink) keep antiforgery, version, and
  reason behaviour.
- Solution builds Release without warnings introduced by this branch.
- Record-bar buttons and attachment Preview column omitted (no handlers);
  omission noted in the ticket and PR.

## Verification commands

```
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
```

(Build only; test/browser/snapshot runs belong to the wave orchestrator.)
Both ran green on 2026-08-28: 0 warnings, 0 errors.

## Simplification pass — 2026-08-28

Lenses: reuse, simplification, efficiency, altitude. Applied:

- Scope forms: dropped the hardcoded `action="/Inbox"` — a bare GET form
  already submits to the current path (the filter bar already relied on
  that).
- `LoadSelectedDetailAsync` doc comment corrected: the code (and the drawn
  prototype) fall back to the page's first row when the selection is not
  on the page; the comment claimed the opposite.
- Dead-agent `RetainedMailSummary.AttachmentFileNames`/`AttachmentNames`
  removed (no caller; preview reads names from `RetainedMailDetail`).

Findings with dispositions (not applied):

- 7 sequential `CountAsync` round trips per list render. A single grouped
  query would need a second SQL encoding of the destination→classification
  filter, duplicating the pipeline this ticket exists to share. Kept
  sequential; revisit only if page latency says so.
- Rows carry `aria-selected` on a `div` (no role). This is the
  `scope-button`/`row-button` vocabulary PLAT-029 shipped (its CSS keys on
  `[aria-selected="true"]`), and the drawn prototype does the same on
  buttons. Uniform across wave-2 lanes; if the wave walk's accessibility
  scan objects, the fix belongs to the design system (PLAT-029 follow-up),
  not this page.
- Hover preview (site.js, PLAT-029 file) fills only
  sender/subject/received/excerpt/classification/association/attachments;
  the pane's Folder and Search match facts can read stale during a
  transient hover. Extending site.js is outside this lane; noted to the
  orchestrator for the wave-5 design-system pass.
- Page header (h1 subject) and record head (h2 subject) duplicate the
  subject on the message page. As drawn in §1.3; kept.
