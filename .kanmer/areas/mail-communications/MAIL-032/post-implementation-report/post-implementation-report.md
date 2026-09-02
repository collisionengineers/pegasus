# Post-implementation report — MAIL-032

## Summary

MAIL-032 **adopts** PR #640 rather than authoring it. The 13-file
implementation (+218 / −87) was already committed and green on
`task/mail-028-inbox-preview-pin` at `ed19e77f` under the wrong ticket id
(MAIL-028, which live on the board owns production retained-mail folder-mover
activation and keeps that meaning). This run's own contribution is exactly one
merge commit, the PR's metadata correction, and the board records: it changed
**no repository line**. The branch head is now
`3bf282441ddd3ba8c0355b8e59d06bea3d501cfb`, a merge of `origin/dev`
(`9b8f78a36151313bc6d48625edee7f13a2173127`) into the branch; that merge is
conflict-free and touches **none** of the 13 adopted paths. PR #640 is OPEN
against `dev`, retitled to
`fix(mail): keep the selected Inbox preview available after pointerleave or blur (MAIL-032)`,
its footer now `Kanmer: MAIL-032`, with no MAIL-028 reference left in title or
body, and every required CI check green at the merged head.

## Changes

| File | Change | Why |
|---|---|---|
| *(none — this ticket)* | — | The implementer authored no repository line. The only Git act was the refresh merge of `origin/dev`, producing merge commit `3bf28244`; its diff against `ed19e77f` lists exactly the 14 `origin/dev` paths and no adopted path. |

Adopted on the branch before this run (inspected, verified, not edited):

| File | Change | Why |
|---|---|---|
| `src/Pegasus.Web/wwwroot/js/site.js` | modified (+57 / −20) | `resetSelection()` (which set `panel.hidden = true`) deleted; setup resolves `selectedRow` from the `aria-current="true"` trigger and returns early when absent; the selected message's projection is seeded into `cache` from the pane's rendered fields; `restoreSelection()` calls `select(selectedRow)` from `pointerleave` (guarded by `relatedTarget.closest('[data-mail-preview-row]')`) and from the trigger `blur` timeout; `select()` hides `data-mail-preview-actions` while a transient row shows. |
| `src/Pegasus.Web/Pages/Mail/Index.cshtml` | modified | `data-mail-preview-actions` on the pane's button row; `tabindex="0"` on both height-capped scroll bodies; association cell routed through `MessageModel.AssociationLabel`. |
| `src/Pegasus.Web/Pages/Mail/Index.cshtml.cs` | modified | `OnGetPreviewAsync` label `"Not associated"` → `AssociationLabel(...)`, so restore cannot flip vocabulary. |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` | modified (+9) | Adds `public static string AssociationLabel(string?)` — the single owner of the no-Case wording. |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml` | modified | Message page routed through the same helper. |
| `src/Pegasus.Web/wwwroot/css/site.css` | modified | `aria-current="true"` joins the selected-row rules; dead `tr.is-preview-selected` block and its `forced-colors` companion deleted. **See finding S1.** |
| `tests/Pegasus.IntegrationTests/Browser/MailWorkspaceBrowserTests.cs` | modified (+136 / −44) | Two-message seed; the focus-away assertion inverted from pane-hidden to pane-restored; new `HoverPreviewRestoresTheSelectedMessageAndKeepsThePaneActionsReachable`. |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | modified | Projection label assertion `No case`. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | modified | Quick-preview clause corrected from "dismisses when focus moves away" to the restore behaviour. |
| `docs/design/README.md` | modified | Mail preview row records the restore behaviour. |
| `docs/design/test-ui/pages/inbox--{default,empty,unavailable}.html` | modified (generated) | Pinned snapshots regenerated for `tabindex="0"`; script-generated, never hand-edited. |

## Governing docs

- **`docs/frd/frd-12-operator-experience.md` (the ticket's `refs`) — met.** It
  owns the shell, the `/Inbox` and `/Inbox/{id}` route entries and the pane
  vocabulary, and contains no clause requiring the preview to dismiss. A pane
  that stays visible with its navigation links satisfies it as written. No
  change, no authorization needed.
- **EPIC-011 `context.md` §1.3 (Inbox) — met.** Its acceptance sentence "The
  selected preview survives pointer leave and blur until another explicit
  selection or navigation (MAIL-032)" is exactly what `restoreSelection()`
  produces on both paths.
- **`docs/frd/frd-08-…` and `docs/design/README.md` — modified, already on the
  branch** (`df9716e3`), correcting as-built statements the behaviour change
  falsified. The implementer introduced no further document change.
- **New ADR: none.** No architectural decision is taken; the port decision was
  MAIL-025's.

## Verification audit (read-only, per plan Step 2)

Each of the ticket's three Verification boxes, traced to a named line:

- **(a) The selected preview survives pointerleave and blur.** `site.js`
  `restoreSelection = function () { select(selectedRow); }` is wired into the
  `pointerleave` handler (which now returns early only when `activeRow !== row`
  or focus is still inside the row) and into the `blur` `setTimeout`. Both
  former call sites of `resetSelection()` are replaced; `resetSelection` no
  longer exists. Until another explicit selection: `select()`'s
  `activeRow === row` early return makes leaving the selected row itself a
  no-op; navigation re-renders the page server-side.
- **(b) Both preview actions remain keyboard and pointer reachable.**
  `data-mail-preview-actions` is hidden only while `row !== selectedRow`, so it
  returns with the restore; the browser test asserts it hidden during the
  transient preview and visible after restore, and clicks the pane's
  `a.btn--dark` through to `/Inbox/{newestId}`. `tabindex="0"` on both
  height-capped scroll bodies makes the width-capped panes keyboard-scrollable.
- **(c) Snapshots/tests and required checks green.** CI run `33581617718` at
  `3bf28244` — see Evidence below.

Regression boundary confirmed unchanged: hover/focus previews remain transient
and touch no message, Case, read or custody state; exactly one preview-state
owner (`activeRow` / `cache`) inside the single IIFE; `select()` keeps its
`activeRow === row` no-op; row-to-row pointer movement does not restore
(`relatedTarget.closest('[data-mail-preview-row]')` guard); the subject remains
an ordinary full-detail link. A repository-wide search for
`is-preview-selected` across `src/`, `tests/` and the pinned snapshot pages
returns nothing, and the no-Case wording has exactly one owner
(`Message.cshtml.cs:1154`).

## Simplification pass

Recorded in full under the plan's `## Simplification pass` heading, dated
2026-09-02: eight lenses, each with a disposition. Seven were already satisfied
on the branch or rejected with a reason. One finding is **reported, not fixed**:

**S1 — the two added `aria-current` CSS selectors invert their stated purpose.**
`.row-button[aria-current="true"]` (`site.css:270`) and
`.queue-layout .row-button[aria-current="true"] .row-title` (`site.css:653`)
match **no Inbox element**: the Inbox puts `aria-current="true"` on the inner
`a.row-title[data-mail-preview-trigger]` (`Pages/Mail/Index.cshtml:253`), while
the `.row-button` is the container `div` and carries none. The only matching
element in the repository is the **Cases** list's selected row
(`Pages/Cases/Index.cshtml:126`, inside `.queue-layout`), which therefore gains
a selected background and inset rule it did not have before. The plan's
regression boundary states these selectors "must not change any non-mail row
surface". The change is plausibly a desirable consistency improvement, but it is
unasserted — the pinned Test UI snapshots compare HTML, so a CSS-only visual
change cannot fail them. **No code change was made**: this ticket authors no
repository line, and the plan's Step 3 rule sends such a finding to the reviewer.
Nothing in the three Verification boxes depends on those selectors.

## Deviations

1. **Lease renew refused** — the packet reported `claim.state: expired`, and the
   skill's resumed path calls for a renew, but the shell guard denies
   `take_ticket` to workers (`kanmer 2 - take_ticket is controller-only`). No
   renew was performed; the controller holds the lease. Recorded, not worked
   around.
2. **A dependency refresh was needed before the build** — the plan's Step 2
   requires a 0-error Release build, but the worktree had never been restored,
   so the `--no-restore` build failed first with 7 × NETSDK1004 (missing
   `obj/project.assets.json`). One locked-mode dependency refresh was run, then
   the build succeeded. The first failure is retained here per M9. Recorded as
   ASSUMPTION 2 in `open-questions`.
3. **`gh pr edit` failed on token scope** (`missing required scopes
   [read:project]`); the title and body were set with
   `gh api -X PATCH repos/collisionengineers/pegasus/pulls/640` instead.
4. **One PR-body claim was corrected beyond the plan's letter.** The plan says
   to leave the What / Root cause / Change narrative intact because "it is
   accurate", but the Change section's "No markup, CSS, or endpoint change"
   describes only the first commit `df9716e3` and is falsified by `ad3779c9`
   (markup and CSS). Leaving a false statement in the review evidence was the
   worse option, so the bullet was corrected and the markup/CSS changes listed.
   The Verification section — invalidated by the merge, as the plan
   anticipated — was replaced with the merged-head results below.
5. **Title wording.** The controller's dispatch message asked for
   "Keep the selected Inbox preview available after pointerleave or blur
   (MAIL-032)"; the packet's binding `## Stop condition` specifies
   `fix(mail): keep the selected Inbox preview available after pointerleave or blur (MAIL-032)`.
   The stop condition was followed (M7), and it also matches the repository's
   conventional-commit PR titles. One API call reverses this if the controller
   prefers its wording.

## Evidence (commands, cwd `…\pegasus-worktrees\mail-028-inbox-preview-pin`)

| Command | Exit | Result |
|---|---|---|
| Worktree assertions: toplevel, both common-dir values, current branch | 0 | PASS — M4 assertions all match the recorded pair; both common dirs are `…/pegasus/.git` |
| HEAD revision + porcelain status | 0 | PASS — `ed19e77f`, tree clean |
| Fetch from origin | 0 | PASS |
| Refresh merge of `origin/dev` (`--no-edit`, never rebase) | 0 | PASS — conflict-free; merge `3bf28244`, parents `ed19e77f` + `9b8f78a3` |
| Name-only diff `ed19e77f..HEAD` | 0 | PASS — exactly the 14 dev paths; none of the 13 adopted paths |
| Push `-u origin task/mail-028-inbox-preview-pin` | 0 | PASS — `ed19e77f..3bf28244` |
| Release build, `--no-restore` (first attempt) | 1 | **FAIL** — 7 × NETSDK1004, never-restored worktree (retained per M9) |
| Locked-mode dependency refresh | 0 | PASS — 7 projects |
| Release build, `--no-restore` (after refresh) | 0 | PASS — 0 warnings, 0 errors |
| Branch diff against `origin/dev` + targeted searches (audit) | 0 | PASS — 13 files, +218 / −87; findings above |
| `gh pr edit 640 --title … --body-file …` | 1 | **FAIL** — token scope (deviation 3) |
| `gh api -X PATCH …/pulls/640` | 0 | PASS — title and body set |
| `gh pr view 640` (title/head/base/state, MAIL-028 search) | 0 | PASS — MAIL-032 title, `3bf28244`, base `dev`, OPEN, 0 MAIL-028 references |
| `gh pr checks 640` | 0 | PASS — see below |

**Tests: controller wave loop.** The implementer ran no verification rail (M6).
The controller's test runner reported at the merged head: dependency refresh
PASS, build PASS, Core 1185/1185 PASS, Architecture 100/100 PASS, UI catalogue
PASS; SQL-integration INCONCLUSIVE (no LocalDB on this host) and pinned-UI
verify INCONCLUSIVE (no local capture) — for both, the CI jobs at the PR head
are the evidence, and INCONCLUSIVE is not recorded as PASS.

**CI run `33581617718` at `3bf28244`:** `unit` pass, `browser` pass,
`sql-integration (1)`, `(2)`, `(3)` pass, `sql-integration-coverage` pass,
`test-ui` pass, `changes` pass, `documentation` pass,
`local-development-scripts` pass, `reference-data` pass; `infrastructure`
skipping. `mergeStateStatus: CLEAN`, 0 commits behind `origin/dev`.

## Risks / follow-ups

- **S1 (above) is the one open question for the reviewer**: accept the Cases
  selected-row restyle as a deliberate improvement, narrow the selectors to the
  Inbox trigger, or drop them as Inbox no-ops. Any of the three needs a code
  change this ticket is not authorized to make.
- The three commit subjects still read `(MAIL-028)`; history was deliberately
  not rewritten (no rebase, no force push). Traceability is carried by the PR
  title/footer and the board record. Live MAIL-028 was not written to.
- The pinned snapshots were not re-captured: the merge changed no rendered page
  (its 14 paths are corpus, scripts, docs and Core/test files), and `test-ui` is
  green at the merged head.

## Verification hand-off

For `kanmer-verify` on the merged result:

- Re-run the canonical rail on merged `dev`: Core and Architecture projects, the
  two complementary integration filters, then the pinned-UI verify and catalogue
  scripts per `docs/runbook.md#locked-restore-build-and-test`.
- The behavioural proof is
  `MailWorkspaceBrowserTests.HoverPreviewRestoresTheSelectedMessageAndKeepsThePaneActionsReachable`
  plus the inverted focus-away assertion in the accessibility/overflow test;
  both are `Category=Browser`.
- Visual check worth capturing for a `proof:visual` requirement: `/Inbox` with a
  message selected and the pointer moved off the list — the pane still shows the
  selected message with **Open full message** and **Open linked Case** — and,
  for S1, the `/Cases` list with a row selected.
