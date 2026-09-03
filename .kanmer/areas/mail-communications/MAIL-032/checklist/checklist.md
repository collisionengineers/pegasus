# Checklist — MAIL-032

*Adoption of PR #640 (already implemented, green at `ed19e77f`). One box per
ordered plan step, plus the acceptance checks and the report. Append progress
notes rather than rewriting. `[pre-review]` / `[post-merge]` are plain-text
labels; `get_doc_gates` is the live authority.*

- [x] Step 1 — [pre-review] Assert the worktree (`rev-parse --show-toplevel`, both `--git-common-dir` values, `branch --show-current`), confirm HEAD is `ed19e77ff2da8c6a5f87eb20a0222eae17ff15b2` and the tree is clean.
- [x] Step 1 — [pre-review] `git fetch origin` then `git merge --no-edit origin/dev` (never rebase); confirm the merge is conflict-free and `git diff <merge-parent-1>..HEAD` touches none of the 13 adopted paths.
- [x] Step 1 — [pre-review] `git push -u origin task/mail-028-inbox-preview-pin`; PR #640's head is the merge commit and it is 0 commits behind `origin/dev`.
- [x] Step 2 — [pre-review] `dotnet build ./Pegasus.slnx --configuration Release --no-restore` gives 0 errors on the merged head.
- [x] Step 2 — [pre-review] Audit `gh pr diff 640` against the ticket's three Verification boxes, FRD-12 §Inbox and EPIC-011 `context.md` §1.3; name the evidence line for each and confirm no behavioural gap. Read-only: no file is edited.
- [x] Step 2 — [pre-review] Confirm the regression boundary holds: hover/focus previews stay transient and state-free, exactly one preview-state owner (`activeRow`/`cache`), `select()` keeps its `activeRow === row` no-op, row-to-row pointer movement does not restore.
- [x] Step 3 — [pre-review] Run the simplification lenses (duplication, dead code, smallest change, naming, comment truth, assertion strength) over the real 13-file diff and append the dated dispositions under the plan's `## Simplification pass` heading via `set_ticket_doc(doc: "plan", append: true)`.
- [x] Step 4 — [pre-review] `gh pr edit 640` sets the title to `fix(mail): keep the selected Inbox preview available after pointerleave or blur (MAIL-032)` and replaces the body footer `Ticket: MAIL-028 …` with `Kanmer: MAIL-032`; no MAIL-028 reference remains in title or body, and live MAIL-028 is untouched.
- [x] Step 5 — [pre-review] `update_item` MAIL-032 records `commits` (the three branch SHAs plus the merge commit) and `prs: ["640"]`, with `expected_updated` and `expected_project`.
- [x] Acceptance [pre-review] — Name the production entry point (`/Inbox` routed page + `OnGetPreviewAsync`, enhancement registered by `data-mail-preview-workspace` in the already-served `site.js`); no new registration or runtime dependency, no schema change.
- [x] Acceptance [pre-review] — Confirm the negative test exists and is not weakened: the focus-away assertion asserts a **restored** pane, and `HoverPreviewRestoresTheSelectedMessageAndKeepsThePaneActionsReachable` proves pointer + keyboard restore, `aria` state and that **Open full message** targets the selected message.
- [x] Acceptance [pre-review] — `gh pr checks 640` at the merged head: `unit`, `browser`, `sql-integration (1..3)`, `sql-integration-coverage`, `test-ui`, `changes`, `documentation`, `local-development-scripts`, `reference-data` all green (`infrastructure` skipping). CI `repository-check` at the new head is the merge gate.
- [x] Acceptance [pre-review] — Snapshots: `docs/design/test-ui/pages/inbox--*.html` are already committed and generated-only; re-capture only if the merge from `origin/dev` changed a rendered page. Runner rail recorded as "tests: controller wave loop".
- [x] Step 6 — [pre-review] Write `post-implementation-report` from the `kanmer-execute` template: adopted-not-authored framing, merge and new head SHAs, per-box audit result, Step 3 dispositions, exact commands with cwd/exit/result, green check names, deviations and open questions.
- [x] Step 6 — [pre-review] `get_doc_gates MAIL-032` shows `enter-review` `passable: true`.
- [x] Step 7 — [pre-review] `move_item` MAIL-032 to `review` (one boundary only), then stop for the independent reviewer: do not merge PR #640, do not move further, do not start or take another ticket, do not dispatch.
- [x] [pre-review] Any repository code change, merge conflict, behavioural gap, non-green check or out-of-scope file need is reported explicitly as a deviation — not folded into the narrative.

## Progress notes

Append with `set_ticket_doc(doc: "checklist", append: true)`.

## Progress notes

- 2026-09-02 (implementer-a1, from the post-implementation report and reviewer-a1's attestation): merge from origin/dev conflict-free (head 3bf28244, pushed); Release build 0 errors; audit against the three Verification boxes, FRD-12 §Inbox and context §1.3 recorded in the report; simplification pass (8 lenses) appended to the plan; PR #640 retitled and re-footered via `gh api PATCH`; `commits` and `prs` recorded; CI run 33581617718 green at the head; snapshots unchanged. Finding S1 (CSS selector reach) accepted-risk by the reviewer; follow-up ticket allocated by the controller.
- 2026-09-02T03:50Z (controller): this document was overwritten at 02:59Z with PR-069's checklist by a scratchpad filename collision between two concurrent agents; restored from the board branch history (commit 37da319b) with every box ticked per the evidence above. PR #640 merged as 2a48be0456e42d22994193b35d6b4cc33bc90a59; ticket in Verifying.

## Closeout — MAIL-032

- [x] PR merge verified ([#640](https://github.com/collisionengineers/pegasus/pull/640), merged 2026-09-02T03:11:14Z)
- [x] proof.md finalised with PR URL and merge date
- [x] Moved to final stage
- [x] Outcome recorded in ticket body
- [ ] cd out of worktree; remove recorded ticket worktree
- [ ] delete local branch (and merged remote branch)
- [ ] prune stale worktree metadata
- [ ] release ticket claim

### Closeout completion — 2026-09-03

- [x] Recorded implementation and verification worktrees removed cleanly.
- [x] Local ticket branch deleted.
- [x] Merged remote ticket branch deleted and remote refs pruned.
- [x] Ticket claim released after Git cleanup.
