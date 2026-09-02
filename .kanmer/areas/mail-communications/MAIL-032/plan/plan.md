# Plan — MAIL-032: Keep the selected Inbox preview available after pointerleave or blur

**Plan sizing (diff estimate).** **Zero authored repository lines.** This is the
adoption of an already-implemented, already-green pull request. The adopted diff
is **+218 / −87 across 13 files**, committed on `task/mail-028-inbox-preview-pin`
at head `ed19e77ff2da8c6a5f87eb20a0222eae17ff15b2`. The implementer's own
contribution is **one merge commit** bringing `origin/dev`'s 2 commits into the
branch (14 files on the dev side, **none of which intersects the 13 branch
paths**, so no conflict is expected), plus PR metadata and Kanmer records. A
source edit is a **deviation**, not a step.

*Overlay applied: `assets/brief-fix.md` (this ticket corrects existing
behaviour). Reproduction, root cause, regression boundary and negative test are
stated in Starting state and Acceptance checks.*

## Objective

Adopt PR #640 under its correct owner MAIL-032 — refreshed onto `origin/dev`,
verified against the Inbox preview contract, re-titled and re-footered away from
MAIL-028, with a real simplification pass recorded — and hand it to the
independent reviewer.

## Starting state

Evidence: `files`@`1c769989de8352f0`; EPIC-011 `context.md` §1.3 (Inbox) read
2026-09-02; PR #640 inspected at `headRefOid`
`ed19e77ff2da8c6a5f87eb20a0222eae17ff15b2`; `origin/dev`
`9b8f78a36151313bc6d48625edee7f13a2173127`.

**Workspace (already re-homed by the controller — do not create or take
anything).** Worktree
`C:\Users\PGUSER\Documents\github\pegasus-worktrees\mail-028-inbox-preview-pin`,
branch `task/mail-028-inbox-preview-pin` (verified: `rev-parse --show-toplevel`
matches, `--git-common-dir` is `C:/Users/PGUSER/Documents/github/pegasus/.git`
for both the worktree and the primary checkout, `branch --show-current` matches).
The branch keeps the slug `mail-028-…`; the correction is carried by the PR
metadata and the board, **not** by renaming the branch or rewriting history.

**PR #640** — base `dev`, state OPEN, `mergeStateStatus` CLEAN, 2 commits behind
`origin/dev`. All required checks green at the head: `unit`, `browser`,
`sql-integration (1..3)`, `sql-integration-coverage`, **`test-ui`**, `changes`,
`documentation`, `local-development-scripts`, `reference-data`
(`infrastructure` skipping). The `test-ui` failure named in the ticket body has
already been resolved by the third commit.

**Reproduction (before-fix).** On `/Inbox`, select a message (`?selected=`), then
move the pointer off the rows or tab focus away: the whole preview pane was
hidden, so **Open full message** and **Open linked Case** could not be reached.

**Root cause.** MAIL-025's port (PR #597) made the pane a permanent
server-rendered fixture of the URL-selected message, but the older UI-10 hover
enhancement in `site.js` still called `resetSelection()` from `pointerleave` and
`blur`, which set `panel.hidden = true` on the whole pane.

**Three commits, as they stand:**

1. `df9716e3` — `resetSelection()` replaced by a restore path in the
   `data-mail-preview-workspace` IIFE; `OnGetPreviewAsync`'s association label
   changed to match the pane's wording; browser/web tests updated; FRD-08 and
   `docs/design/README.md` corrected.
2. `ad3779c9` — review findings F1–F7: the selected message's projection is
   seeded into `cache` from the pane's own rendered fields (restore repaints in
   the same tick, a failed fetch can no longer strand the pane); row-to-row
   pointer movement no longer restores between every pair of rows; the pane's
   actions (`data-mail-preview-actions`) step aside while a transient preview
   shows another row; the selected row's affordance is `aria-current` and the
   dead `tr.is-preview-selected` CSS is deleted; `AssociationLabel` becomes the
   one owner of the no-Case wording across pane, projection and message page;
   `resetSelection` is deleted as unreachable; the Inbox scroll bodies gain
   `tabindex="0"` because the width-capped panes were pointer-only.
3. `ed19e77f` — the three pinned `docs/design/test-ui/pages/inbox--*.html`
   snapshots regenerated for that `tabindex="0"`, via
   `./scripts/Update-TestUiSnapshots.ps1`.

**Regression boundary (must remain unchanged).** Hover and focus previews stay
transient and change no message, Case, read or custody state; the subject stays
an ordinary full-detail link; there remains exactly **one** preview-state owner
(`activeRow` / `cache` inside the single IIFE); `select()` keeps its
`activeRow === row` no-op, which is what makes leaving the selected row itself a
no-op; the shared `site.css` `aria-current="true"` selectors must not change any
non-mail row surface.

**What the ticket's Verification boxes require.** (a) the selected preview
survives pointerleave and blur until another row is selected or the view
changes; (b) both preview actions remain keyboard **and** pointer reachable;
(c) UI snapshots/tests and the full required PR checks are green.

## Governing docs

- **`docs/frd/frd-12-operator-experience.md` (the ticket's `refs`) — Meets.**
  FRD-12 owns the shell, the `/Inbox`, `/Inbox/{id}` route entry and the
  operator-facing vocabulary. It contains no clause requiring the preview to
  dismiss, so a pane that stays visible with its navigation links satisfies it
  as written. **No document change, no authorization needed, no new ADR.**
- **EPIC-011 `context.md` §1.3 (Inbox) — Meets.** The group contract already
  states the exact acceptance sentence for this ticket: "The selected preview
  survives pointer leave and blur until another explicit selection or navigation
  (MAIL-032)", inside the three-pane Scope / Messages / Message preview contract
  whose preview carries **Open full message** and **Open linked Case**.
- **`docs/frd/frd-08-email-mailbox-and-background-processing.md` — Modified, and
  the modification is already committed on the branch** (commit `df9716e3`): the
  quick-preview clause's hover-era phrase "dismisses when focus moves away"
  became "restores the selected message when that intent moves away; the pane
  stays visible with its navigation links rather than dismissing to blank". This
  is a correction of a falsified as-built statement — the class of documentation
  change the repository requires with the behaviour change, in the same diff, and
  the `documentation` check is green on it. The implementer **introduces no
  further governing-document change**; if verification shows one is needed, that
  is a STOP for authorization (see Failure and deviation rules).
- **`docs/design/README.md` (Mail preview row) — Modified, already committed**
  (`df9716e3`), same reason and same diff.
- **New ADR: none.** No architectural decision is taken here; the port decision
  was MAIL-025's.

## Required changes

For the implementer, in terms of observable end state:

1. The branch head is a **merge commit** of `origin/dev` into
   `task/mail-028-inbox-preview-pin`, pushed to the PR, with the 13 adopted paths
   unchanged by that merge.
2. PR #640's title is exactly
   `fix(mail): keep the selected Inbox preview available after pointerleave or blur (MAIL-032)`
   and its body's trailing line
   `Ticket: MAIL-028 (simplification pass recorded in its plan, dated 2026-09-01).`
   is replaced by `Kanmer: MAIL-032`. No other body claim is altered except a
   verification line the merge invalidates.
3. Kanmer MAIL-032 records the branch commit SHAs in `commits` and keeps
   `prs: ["640"]`.
4. A **real** simplification pass over the actual diff is recorded under this
   plan's `## Simplification pass` heading (the pass PR #640's body claims lives
   in a MAIL-028 plan that does not exist).
5. The `post-implementation-report` document exists, written from
   `kanmer-execute`'s template, and MAIL-032 is in `review`.
6. **No repository source change.** The behavioural audit in Step 2 is
   read-only.

## Expected files

Every row is **already changed on the branch**; the action is *inspect and
verify*, and an edit is a reportable deviation.

| Action | Repo-root-relative path | Responsibility |
|---|---|---|
| Inspect | `src/Pegasus.Web/wwwroot/js/site.js` | The fix, inside the mail-preview IIFE only. |
| Inspect | `src/Pegasus.Web/Pages/Mail/Index.cshtml` | `data-mail-preview-actions`, `tabindex="0"` scroll bodies, `AssociationLabel` cell. Routed Razor page. |
| Inspect | `src/Pegasus.Web/Pages/Mail/Index.cshtml.cs` | `OnGetPreviewAsync` association label. |
| Inspect | `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` | `AssociationLabel` helper — the one owner of the no-Case wording. |
| Inspect | `src/Pegasus.Web/Pages/Mail/Message.cshtml` | Message page routed through that helper. |
| Inspect | `src/Pegasus.Web/wwwroot/css/site.css` | `aria-current="true"` selected-row rules; dead `tr.is-preview-selected` rules removed. Shared stylesheet. |
| Inspect | `tests/Pegasus.IntegrationTests/Browser/MailWorkspaceBrowserTests.cs` | Restore + actions-reachable proof, pointer and keyboard. |
| Inspect | `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Projection label assertion. |
| Inspect | `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Quick-preview clause correction. |
| Inspect | `docs/design/README.md` | Mail preview row correction. |
| Inspect | `docs/design/test-ui/pages/inbox--default.html` | **Generated artifact** — script-regenerated only. |
| Inspect | `docs/design/test-ui/pages/inbox--empty.html` | **Generated artifact** — script-regenerated only. |
| Inspect | `docs/design/test-ui/pages/inbox--unavailable.html` | **Generated artifact** — script-regenerated only. |

## Do not modify

**Every path not listed in Expected files.** In particular:

- `docs/operator-notes.md` — never edited by an agent.
- `AGENTS.md` — this PR changes no command or convention (rule 24).
- `src/Pegasus.Core/**`, `src/Pegasus.Infrastructure/**`,
  `src/Pegasus.Worker/**` — the fix is a Web presentation concern.
- `corpus/**`, `reference/**` — read-only inputs.
- `.kanmer/**`, `.worktrees/**`, `.github/**`, `scripts/**`.
- `docs/prd/**`, `docs/adr/**`, `docs/index.md`, `docs/current-architecture.md`,
  `docs/operations.md`, `docs/runbook.md`.
- Within the Expected files themselves, everything outside the changes the three
  commits already made: no other `site.js` IIFE, no further `site.css` rule, no
  other Mail page markup, no other `OnGet…`/`OnPost…` handler.
- Branch history: no rebase, no force push, no commit-message rewrite.

## Constraints

- **Refresh is `git merge --no-edit origin/dev`.** Never rebase; merge commits
  only. PR targets `dev`. Never push to `dev` or `main`. The only push is
  `git push -u origin task/mail-028-inbox-preview-pin` from this worktree.
- **Branch and worktree are pre-existing.** Assert them (M4) and reuse; never
  create, take, or re-home.
- **Build only for the implementer.** `dotnet build ./Pegasus.slnx
  --configuration Release --no-restore`. The **test runner** owns every
  `dotnet test`, snapshot and catalogue invocation; the shell guard denies them
  to other roles. Record "tests: controller wave loop".
- **Generated artifacts.** `docs/design/test-ui/pages/*.html` are produced by
  `./scripts/Update-TestUiSnapshots.ps1` and are never hand-edited. They are
  already committed and `test-ui` is green; capture again **only** if the merge
  from `origin/dev` changes a rendered page.
- **One preview-state owner.** Any fix must stay inside the existing
  `activeRow` / `cache` machine; a second owner is a redesign.
- **Accessibility.** The pane's actions must stay reachable by keyboard and
  pointer; `aria-current` / `aria-expanded` semantics as committed.
- **Traceability.** Live MAIL-028 keeps its own meaning (production
  retained-mail folder mover); nothing in this ticket may write to it.
- **Kanmer writes are version-aware** (`expected_version` / `expected_updated` /
  `expected_project`), and the implementer moves **one** boundary only:
  `implementing` → `review`.

## Ordered steps

### Step 1 — Refresh the branch onto `origin/dev` with a merge commit

- Preconditions: M4 worktree assertions pass; `git status --porcelain` is empty;
  `git rev-parse HEAD` is `ed19e77ff2da8c6a5f87eb20a0222eae17ff15b2`.
- Files: (none — this step authors no repository file; the merge is mechanical.)
- Symbols: (none.)
- Change: `git fetch origin`, then `git merge --no-edit origin/dev`. The 2 dev
  commits (`9b8f78a3` board-branch env, `5a40d157` principal-identification
  corpus) touch 14 paths, none of them among the 13 adopted paths, so a
  conflict-free merge commit is expected. Then
  `git push -u origin task/mail-028-inbox-preview-pin`.
- Preserved behaviour: `git diff <merge-parent-1>..HEAD -- <the 13 paths>` is
  empty — the merge changes none of the adopted work.
- Forbidden: rebase, force push, history rewrite, squash, amend, pushing any
  branch other than this one.
- Negative cases: a conflict in any of the 13 paths, or a merge that alters
  them, is a **STOP** — report the conflicting paths, do not resolve by
  preferring one side unread.
- Tests: none at this step (the runner's rail follows).
- Commands: `git -C <worktree> fetch origin`;
  `git -C <worktree> merge --no-edit origin/dev`;
  `git -C <worktree> push -u origin task/mail-028-inbox-preview-pin`.
- Expected output: a merge commit with two parents; `git status` clean; push
  accepted; `gh pr view 640 --json headRefOid` shows the new head.
- Done when: PR #640's head is the merge commit and it is 0 commits behind
  `origin/dev`.
- Deviation stop: any conflict, any non-empty diff against the 13 paths, or a
  guard denial.

### Step 2 — Verify the adopted diff against the contract, read-only

- Preconditions: Step 1 done.
- Files: (none — this step reads the Expected files and authors nothing.)
- Symbols: `select`, `restoreSelection`, `render`, `activeRow`, `cache`,
  `selectedRow`, `MessageModel.AssociationLabel` — read for audit only.
- Change: none. Build for compiler feedback
  (`dotnet build ./Pegasus.slnx --configuration Release --no-restore`), then
  read `gh pr diff 640` against (a) the ticket body's three Verification boxes,
  (b) FRD-12 §Inbox / the `/Inbox` route contract, (c) EPIC-011 `context.md`
  §1.3's sentence "The selected preview survives pointer leave and blur until
  another explicit selection or navigation". Confirm each of: pointerleave
  restores; trigger `blur` restores; row-to-row movement does not restore;
  `data-mail-preview-actions` returns when the selected row is active again;
  both actions are reachable by keyboard (`tabindex="0"` scroll bodies) and by
  pointer; the no-Case wording has exactly one owner.
- Preserved behaviour: hover/focus previews remain transient and state-free;
  exactly one preview-state owner; the subject remains an ordinary link.
- Forbidden: editing any file "while here"; adding a test the branch does not
  need; touching a neighbouring Inbox concern.
- Negative cases: a behavioural gap against (a)–(c), or a documented
  as-built statement the diff falsifies and does not correct, is a **STOP**
  with the exact gap named — not a silent code fix.
- Tests: the branch's own
  `tests/Pegasus.IntegrationTests/Browser/MailWorkspaceBrowserTests.cs`
  (`HoverPreviewRestoresTheSelectedMessageAndKeepsThePaneActionsReachable` is
  the negative-case proof that the pane does not blank) and
  `MailWorkspaceWebTests.cs`. The **runner** executes them.
- Commands: `dotnet build ./Pegasus.slnx --configuration Release --no-restore`;
  `gh pr diff 640`; `git -C <worktree> diff origin/dev...HEAD --stat`.
- Expected output: build 0 errors; every Verification box traceable to a named
  line of the diff; no gap.
- Done when: each of the ticket's three Verification boxes has a named evidence
  line, and no governing-document statement is left falsified.
- Deviation stop: any gap, or any temptation to change code — report instead.

### Step 3 — Run the simplification pass over the real diff and record it

- Preconditions: Step 2 found no gap.
- Files: (none — the record is a Kanmer document, not a repository file.)
- Symbols: (none.)
- Change: apply the repository's simplification lenses to the **actual** 13-file
  diff — duplication (is `AssociationLabel` now the single owner, are there
  further copies?), dead code (is anything else left over from the hover era
  besides the deleted `resetSelection` and `tr.is-preview-selected`?), the
  smallest change that satisfies the contract (does the cache seeding earn its
  complexity against a plain re-fetch?), naming, comment truth (do the comments
  describe what the code now does?), and test-assertion strength (is the new
  test asserting restoration and reachability, not merely non-hidden?). Record
  each lens with its disposition — **applied / rejected, with the reason** —
  by appending under this plan's `## Simplification pass` heading with
  `set_ticket_doc(id: "MAIL-032", doc: "plan", append: true)`. PR #640's body
  claims this pass lives in a MAIL-028 plan that does not exist; that claim is
  what this step replaces.
- Preserved behaviour: recording a rejection is a legitimate disposition — a
  lens that would require a code change becomes a **reported** finding, not an
  edit.
- Forbidden: recording "no findings" without naming the lenses; applying a
  simplification as a code edit under this plan.
- Negative cases: a lens that finds a real defect is a STOP-and-report (the
  reviewer decides), not a quiet fix.
- Tests: none.
- Commands: `gh pr diff 640`;
  `bash <kanmer-call.sh> set_ticket_doc "$(cat <args.json>)"`.
- Expected output: a dated block under `## Simplification pass` naming every
  lens and its disposition.
- Done when: the plan document carries that block.
- Deviation stop: a lens finding that cannot be dispositioned without changing
  code.

### Step 4 — Re-home PR #640's title and body footer

- Preconditions: Steps 1–3 done.
- Files: (none — GitHub PR metadata only.)
- Symbols: (none.)
- Change: `gh pr edit 640 --title "fix(mail): keep the selected Inbox preview
  available after pointerleave or blur (MAIL-032)"`, and replace the body's
  final line `Ticket: MAIL-028 (simplification pass recorded in its plan, dated
  2026-09-01).` with `Kanmer: MAIL-032`. Refresh any verification line the merge
  from `origin/dev` invalidated (state the new head). Leave the What / Root
  cause / Change narrative intact — it is accurate.
- Preserved behaviour: the three commit subjects keep their MAIL-028 text
  (history is not rewritten); live MAIL-028's own record is untouched.
- Forbidden: `gh pr merge` (reviewer-only), closing or reopening the PR,
  changing its base, editing any other PR or issue, writing to MAIL-028.
- Negative cases: if the body no longer contains that exact footer line, STOP
  and report rather than guessing which line to replace.
- Tests: none.
- Commands: `gh pr view 640 --json title,body`; `gh pr edit 640 --title …
  --body-file <file>`; `gh pr view 640 --json title,body` to confirm.
- Expected output: title and footer read as specified; no MAIL-028 reference
  remains in the title or body.
- Done when: `gh pr view 640` shows the MAIL-032 title and `Kanmer: MAIL-032`.
- Deviation stop: a missing footer line, or any `gh` write beyond title/body.

### Step 5 — Record commits and PR on MAIL-032

- Preconditions: Step 4 done; the merge commit SHA is known.
- Files: (none — Kanmer board record.)
- Symbols: (none.)
- Change: `update_item` MAIL-032 with `commits` = the branch SHAs
  (`df9716e3…`, `ad3779c9…`, `ed19e77f…`, plus the Step 1 merge commit) and
  `prs` = `["640"]`, passing `expected_updated` and `expected_project`.
- Preserved behaviour: `refs`, `groups`, `links` and the body are unchanged.
- Forbidden: `take_ticket`, `set_group_doc`, `dispatch_task`, editing MAIL-028,
  changing group membership.
- Negative cases: an `expected_updated` conflict means re-read and retry once;
  a second conflict is a STOP.
- Tests: none.
- Commands: `bash <kanmer-call.sh> get_item '{"id":"MAIL-032"}'`;
  `bash <kanmer-call.sh> update_item "$(cat <args.json>)"`.
- Expected output: the write returns the new `updated` / `revision`.
- Done when: `get_item MAIL-032` shows the four SHAs and `prs: ["640"]`.
- Deviation stop: repeated conflict, or any refusal.

### Step 6 — Write the post-implementation report

- Preconditions: Steps 1–5 done.
- Files: (none — Kanmer document.)
- Symbols: (none.)
- Change: write `post-implementation-report` from `kanmer-execute`'s template:
  what was adopted rather than authored, the merge commit and new head SHA, the
  13-path evidence, the Step 2 audit result per Verification box, the Step 3
  simplification dispositions, the exact commands run with cwd and exit codes
  (`INCONCLUSIVE` is not `PASS`, and a first failure is kept even if a retry
  passes), the CI check names green at the new head, "tests: controller wave
  loop", and every deviation or open question.
- Preserved behaviour: no fabricated output; no claim the runner has not
  actually produced.
- Forbidden: claiming a test result the implementer did not observe; omitting a
  deviation.
- Negative cases: a red or missing required check at the new head is a
  **STOP** — report it; do not move the ticket.
- Tests: none authored.
- Commands: `gh pr checks 640`; `gh pr view 640 --json headRefOid`;
  `bash <kanmer-call.sh> set_ticket_doc "$(cat <args.json>)"`;
  `bash <kanmer-call.sh> get_doc_gates '{"id":"MAIL-032"}'`.
- Expected output: `get_doc_gates` shows `enter-review` `passable: true`.
- Done when: the report exists and the boundary is passable.
- Deviation stop: a non-green check, or a gate that stays unmet.

### Step 7 — Move MAIL-032 to `review` and stop

- Preconditions: Step 6 done; `enter-review` passable; every checklist box
  ticked.
- Files: (none.)
- Symbols: (none.)
- Change: `move_item` MAIL-032 to `review` with `expected_updated` and
  `expected_project`. Then stop.
- Preserved behaviour: exactly one gated boundary is crossed.
- Forbidden: merging the PR, moving to `verifying` or `done`, starting or taking
  another ticket, dispatching.
- Negative cases: a refusal naming an unmet requirement is a STOP with the
  refusal quoted verbatim.
- Tests: none.
- Commands: `bash <kanmer-call.sh> move_item "$(cat <args.json>)"`.
- Expected output: `status: "review"`.
- Done when: the Stop condition below holds.
- Deviation stop: any refusal.

## Acceptance checks

- **Production entry point.** The behaviour ships through the routed Razor page
  `/Inbox` (`src/Pegasus.Web/Pages/Mail/Index.cshtml`, `IndexModel`) and its
  `OnGetPreviewAsync` JSON handler; the enhancement is registered by the
  `data-mail-preview-workspace` attribute the page renders and the
  `site.js` bundle the layout already serves. No new registration or
  composition entry is required.
- **Runtime dependencies.** None added — no package, no script, no asset. The
  changed `site.js` and `site.css` are existing `wwwroot` static assets already
  in the published output.
- **Schema.** No schema, migration, grant or role change; not applicable.
- **Verification box (a).** `MailWorkspaceBrowserTests` asserts that after
  pointer leave and after focus-away the pane is **restored** to the selected
  message (the assertion previously asserted the opposite) — a genuine negative
  test for the failure that must not recur, not a happy-path check.
- **Verification box (b).** The same suite asserts the pane's **Open full
  message** link navigates to the selected message, and that restore happens on
  both the pointer and the keyboard path; the `tabindex="0"` scroll bodies make
  the width-capped panes keyboard-scrollable.
- **Verification box (c).** Snapshots are committed at `ed19e77f` and `test-ui`
  is green; after Step 1, **CI `repository-check` at the new merged head with
  every required check green is the merge gate**.
- **Assertions are not weakened.** No test is deleted, skipped, or loosened;
  the browser seed grows from one message to two so restore is observable.
- **Governing docs.** FRD-12 is met; FRD-08 and `docs/design/README.md` are
  corrected in the same diff as the behaviour change.
- **Traceability.** No MAIL-028 reference remains in PR #640's title or body,
  and live MAIL-028 is untouched.

## Commands

Implementer, in the worktree
`C:\Users\PGUSER\Documents\github\pegasus-worktrees\mail-028-inbox-preview-pin`:

```powershell
git -C <worktree> rev-parse --show-toplevel
git -C <worktree> rev-parse --path-format=absolute --git-common-dir
git -C <worktree> branch --show-current
git -C <worktree> fetch origin
git -C <worktree> merge --no-edit origin/dev
git -C <worktree> push -u origin task/mail-028-inbox-preview-pin
dotnet build ./Pegasus.slnx --configuration Release --no-restore
gh pr diff 640
gh pr view 640 --json title,body,headRefOid,mergeStateStatus
gh pr checks 640
```

**Test runner only** (cwd = the worktree; the shell guard denies these to every
other role — record "tests: controller wave loop"):

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build
dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "Category=Browser&Category!=Corpus" -- xUnit.MaxParallelThreads=2
./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture
./scripts/Test-UiCatalogue.ps1
```

The snapshots are already committed on the branch, so `-SkipCapture` verifies
the retained capture; run a fresh capture (drop `-SkipCapture`) **only** if the
merge from `origin/dev` changes a rendered page. `Invoke-LocalDevelopment` and
every other host/browser script stay out of scope.

**Post-merge / environment:** none. `deployment` is `not-deployed`; this is a
Web-layer presentation fix with no infrastructure or data step.

## Failure and deviation rules

Stop and report — never improvise — on: a merge conflict, or a merge that
changes any of the 13 adopted paths; a behavioural gap between the diff and the
ticket body, FRD-12 §Inbox or EPIC-011 `context.md` §1.3; a red, missing or
`INCONCLUSIVE` required check at the new head; a file outside Expected files
needing a change (including an obvious neighbouring fix); any dependency
addition; any governing-document conflict or a needed document change beyond the
two already committed; a guard denial; any Kanmer refusal or version conflict.

**A repository code change is itself a deviation.** The implementation is
adopted, not authored. If the merge from `origin/dev` or a demonstrated defect
genuinely forces an edit, keep it minimal, confine it to Expected files, and
report it explicitly as a deviation with the forcing evidence — do not fold it
into the narrative. Deviations are never silent redesigns, and a first failure is
recorded even when a retry passes.

Headless assumption rule: where a decision this plan does not settle is
unavoidable, choose the option most consistent with the governing documents,
record it immediately in `open-questions` as
`- [ ] ASSUMPTION <n> (<role>, attempt <n>): …`, and continue; a second decision
that depends on the first is a **BLOCKED** stop naming both.

## Stop condition

PR #640 retitled to
`fix(mail): keep the selected Inbox preview available after pointerleave or blur (MAIL-032)`,
its body footer replaced with `Kanmer: MAIL-032`, updated at the merged-from-dev
head with every required check green, and MAIL-032 moved `implementing` →
`review`. **Stop there for the independent reviewer.** Do not merge the PR, do
not cross a second gated boundary, do not start or take another ticket, and do
not dispatch.

## Simplification pass

*Dated record required before the PR opens for review. The implementer runs the
lenses over the real 13-file diff in Step 3 and appends the dated block below
with `set_ticket_doc(id: "MAIL-032", doc: "plan", append: true)`.*

**Planned 2026-09-02 (pegasus-planner, attempt 1) — plan-level pass.** Lenses
over this plan itself: (1) *Smallest change* — applied: the plan authors zero
repository lines; the adoption is a merge, a metadata correction and a record.
(2) *Duplication* — applied: the file map lives once, in `files`; this plan
references it rather than restating it. (3) *Dead ceremony* — applied: no
`research` document is created, because no material hole exists (the
implementation is complete, green and readable in the diff). (4) *Unresolved
planner work* — applied: no `investigate` / `decide` / `choose` step remains;
the one discrepancy found (the dispatch's illustrative "CSS and Razor markup are
out of scope" reading versus the diff, which does change
`Pages/Mail/Index.cshtml`, `Message.cshtml`, `Message.cshtml.cs` and `site.css`)
is resolved in favour of the verified PR file list and recorded in
`open-questions`. (5) *Second state owner* — rejected as a change, retained as a
constraint: the existing single `activeRow` / `cache` owner is preserved by
instruction, not by refactoring.

*Implementation pass (Step 3) — to be appended below, dated, one line per lens
with its disposition.*
