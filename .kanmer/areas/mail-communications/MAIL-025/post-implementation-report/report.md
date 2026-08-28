# Post-implementation report — MAIL-025

2026-08-28. Branch `task/mail-025-inbox-port` (includes the recovered dead
agent's `4a272967`), merged `origin/dev` cleanly (no `Migrations/*`
conflicts; dev's 16 wave-3 commits simply fast-in).

## Delivered

- **Core** (`RetainedMail.cs`): `UnreadOnly`/`OldestFirst` scope members;
  `CountAsync` on `IRetainedMailQueries` and `ListRetainedMail` (same
  `Normalize` + `PerformCasework` authorization as the list).
- **Infrastructure** (`EfRetainedMailboxMessageStore.cs`): the scope filter
  pipeline extracted to `BuildMatches`, shared by `ListAsync` and the new
  `CountAsync`; unread filter and ascending order applied in `ListAsync`.
- **List page** (`Pages/Mail/Index.*`): header, filter bar (auto-submit,
  no-JS Search fallback), scope rail (7 scopes, icon wells, per-scope counts
  respecting the current mailbox + search), messages pane (sort toggle,
  rows, bounded pagination), server-rendered preview pane for `?selected=`;
  deleted-items search in the messages pane; site.js hover preview and the
  JSON `Preview` handler unchanged and still exercised.
- **Message page** (`Pages/Mail/Message.cshtml`): page header, record head,
  tabs with attachment count, decision card, corrections timeline,
  attachments table, case tab (definition-list summary + Open Case +
  existing link/unlink/search flows). Correction/move dialogs restyled to
  the `dialog-*` vocabulary; every form posts exactly as before.
- **Tests**: `MailWorkspaceWebTests` updated to the ported markup with all
  behaviour assertions kept and one new list-surface test (scope rail
  counts, unread scope, sort flip, strict parsing of `sort`/`unread`);
  `RetainedMailTests` fake implements `CountAsync` plus two new count
  tests; `Browser/MailWorkspaceBrowserTests` updated to the pane layout and
  the select-then-open no-JS flow.

## Omitted as drawn

Record bar Reply/Forward/Compose/Flag/Delete (wave 4, MAIL-026 — no
handlers exist) and the attachments-table Preview column (no handler, not a
D7 seam). The whole record bar is omitted rather than rendered empty.

## Verification

- `dotnet restore ./Pegasus.slnx --locked-mode` — OK.
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore` —
  succeeded, 0 warnings, 0 errors.
- Tests/browser/snapshot runs deliberately not executed (subagents build
  only); the wave orchestrator runs the loop.

## Out-of-scope findings (for other tickets)

- site.js hover preview fills only the seven contracted fields; the preview
  pane's Folder and Search match facts can read stale during a transient
  hover. Extends only via a PLAT-029/site.js follow-up.
- `aria-selected` on role-less `row-button` divs is vocabulary PLAT-029
  shipped (CSS keys on it); if the wave accessibility walk objects, the fix
  is a design-system change, not a page change.
- PR: https://github.com/collisionengineers/pegasus/pull/597
