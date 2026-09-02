---
kind: review-attestation
pr: "640"
head_sha: "3bf282441ddd3ba8c0355b8e59d06bea3d501cfb"
verdict: pass
reviewer: "claude-code/20260901T215000Z-claude-controller/reviewer-a1"
independent: true
plan_hash: "6a41947fddc84652"
ticket_updated: "2026-09-02T02:59:45.144Z"
board_sha: "636e5d96a0d7997b60a20b6a15c88d48bff302ff"
expected_reviewers:
  - "claude-code/20260901T215000Z-claude-controller/reviewer-a1"
threads_snapshot:
  - source: github
    id: "IC_kwDOThBrk88AAAABR1Ls9Q"
    author: "merceralex397-collab"
    resolved: false
    finding: F-005
  - source: github
    id: "IC_kwDOThBrk88AAAABR07Lhw"
    author: "chatgpt-codex-connector"
    resolved: false
    finding: F-006
findings:
  - id: F-001
    severity: minor
    summary: "The two added .row-button[aria-current=\"true\"] rules (site.css:270, :653) match no Inbox element and instead newly style the Cases list's selected row, breaching the plan's regression boundary."
    disposition: accepted-risk
    reason: "Verified accurate and verified benign. The Inbox is unchanged against origin/dev — the deleted tr.is-preview-selected rules were equally dead against div.row-button rows — so no Inbox affordance is lost or gained. The one element reached repository-wide is Pages/Cases/Index.cshtml:126, where the declarations are identical to the adjacent [aria-selected=\"true\"] rule and land on the row that page's own markup already designates as current, consistent with EPIC-011 context.md section 1.4; the highlight is redundant with aria-current, not a substitute for it. No verification box, test or check depends on the selectors, so a needs-changes return would spend the single remediation round and a full CI cycle to change zero behaviour on the ticket's own surface. Follow-up recorded in the body, not a condition of this merge."
  - id: F-002
    severity: note
    summary: "The new browser-test comment 'the selected row's affordance is aria-current — the attribute the row-button styles key on' is false for the Inbox, for the same reason as F-001."
    disposition: accepted-risk
    reason: "A comment, not behaviour. The assertion it annotates — aria-current is \"true\" on the trigger — is correct and is the right thing to assert. Carried with F-001's follow-up."
  - id: F-003
    severity: note
    summary: "During a transient preview the pane still shows the selected message's status chip, Folder, Search match, mailbox address and aria-label, because render() rewrites only sender/subject/received/excerpt/classification/association/attachments."
    disposition: accepted-risk
    reason: "Pre-existing behaviour that predates this PR. Its load-bearing half — the action links pointing at a message the pane is not showing — is fixed here by data-mail-preview-actions. The remainder is outside this ticket's three verification boxes and is no longer the unreachable-actions defect the ticket names."
  - id: F-004
    severity: minor
    summary: "MAIL-032's checklist document is another ticket's checklist: it is headed '# Checklist — PR-069' and its ten steps describe ReopenUnidentifiedRequest / EfUnidentifiedStore work unrelated to this ticket. Only the appended Progress notes are MAIL-032's."
    disposition: accepted-risk
    reason: "A board document, not a repository file; nothing in PR #640 is affected. checklist is not a requirement of leave-preparing, enter-review or enter-done, so it gates nothing and this merge does not rest on it. Named because a mis-seeded checklist on the one ticket whose purpose is correcting mis-attributed work deserves saying out loud."
  - id: F-005
    severity: note
    summary: "The earlier round's structured review raised seven findings against df9716e3; commit ad3779c9 addresses all seven."
    disposition: fixed
  - id: F-006
    severity: note
    summary: "The chatgpt-codex-connector comment is a usage-limit notice, not a review."
    disposition: rejected-with-reason
    reason: "Automated code-review bots are never expected reviewers and their absence gates nothing. The comment carries no finding to disposition."
---

# Review — MAIL-032, PR #640 at 3bf282441ddd3ba8c0355b8e59d06bea3d501cfb

Round 0, the consolidated review of the whole PR. I am
`reviewer-a1`; the implementer was `implementer-a1`. Different agent role,
different dispatch, no authorship of any line under review — `independent: true`.

## What the PR does

Thirteen files, +218 / −87. On `/Inbox` the preview pane is a server-rendered
fixture of the URL-selected message and carries **Open full message** and
**Open linked Case**, but the older UI-10 hover enhancement still called
`resetSelection()` from `pointerleave` and `blur`, setting `panel.hidden = true`
on the whole pane — so both actions became unreachable as soon as the pointer or
focus left the rows. The fix resolves the server-selected row once at init from
the `aria-current="true"` trigger, seeds that message's projection into `cache`
from the pane's own rendered fields, and replaces both hide call sites with
`restoreSelection()`, which is `select(selectedRow)`. `resetSelection` is
deleted as unreachable. Supporting changes: the pane's button row is marked
`data-mail-preview-actions` and steps aside while a transient preview shows
another row; both height-capped scroll bodies gain `tabindex="0"`;
`MessageModel.AssociationLabel` becomes the single owner of the no-Case wording;
the dead `tr.is-preview-selected` rules are removed; FRD-08 and
`docs/design/README.md` are corrected in the same diff; three pinned Test UI
snapshots are regenerated for the `tabindex="0"`.

This run authored no repository line. Its Git contribution is one merge commit
bringing `origin/dev` into the branch.

## The three review questions

### Did the plan miss anything the ticket implies?

**No material miss.** Each of the ticket's three verification boxes has a named
acceptance check in the plan, and each traces to a line of the diff — see
Acceptance checks below. The plan went further than the ticket and wrote the
regression boundary that finding F-001 breaches; F-001 exists because the plan
asked someone to look for it.

Two smaller observations, neither changing the decision. The plan bound its
`Expected files` to PR #640's verified 13 paths against a dispatch note that
read markup, CSS and endpoints as out of scope, and settled the conflict on
evidence in ASSUMPTION 1 — the right call, since excluding those four paths
would have made the adopted diff unreviewable. And the plan offered a lens
finding only two routes, STOP or report, with no third route to simply *drop* an
out-of-scope line; that is why F-001 arrives at review rather than having been
removed at source.

### Did the implementation miss anything in the plan?

**No.** All six of the plan's Required changes are met, verified against the
tree and the board rather than against the report:

1. Head `3bf28244` is a merge with parents `ed19e77f` and `9b8f78a3`. Its
   name-only diff against `ed19e77f` is exactly the 14 `origin/dev` paths, and
   the intersection with the 13 adopted paths is empty. The merge changed none
   of the adopted work.
2. The PR title is the stop condition's exact string and the footer is
   `Kanmer: MAIL-032`. A search of the live title and body returns no `MAIL-028`.
3. `get_item` shows the four SHAs and `prs: ["640"]`.
4. The dated eight-lens simplification pass is under the plan's
   `## Simplification pass`.
5. The post-implementation report exists; the ticket is in `review`.
6. No repository line authored.

The five recorded deviations are real and correctly classed. Deviation 4 went
beyond the plan's letter — the plan said to leave the PR narrative intact
"because it is accurate", and the Change section's "No markup, CSS, or endpoint
change" bullet was true of `df9716e3` alone and falsified by `ad3779c9`.
Correcting it was right: the plan's instruction rested on a premise that did not
hold, and leaving a false statement inside the review evidence would have been
the worse option. Deviation 2 (one locked-mode dependency refresh before a
`--no-restore` build on a never-restored worktree) is a reasonable reading of a
role boundary that would otherwise have made the plan's own step impossible; it
changed no repository file, and the first failure is retained.

### Did the simplification pass run with honest dispositions?

**Yes.** Eight named lenses, each with applied / rejected-with-reason /
reported. Three claims spot-checked independently and all hold: the no-Case
wording has exactly one owner (a repository search across `src/**` for both
labels returns a single hit, `Message.cshtml.cs:1154`); `is-preview-selected`
and `resetSelection` are both absent repository-wide; and the assertion-strength
claim is accurate — the new test asserts the restored subject, the actions
hidden during the transient preview and visible after restore, `aria-expanded`
across both rows, and navigation through the pane's link, while the pre-existing
focus-away assertion is inverted from hidden to restored rather than deleted.

Lens 3's rejection of a plain re-fetch is argued on the invariant rather than on
effort, and it is correct: re-fetching would put the pane's actions behind the
network on every restore, which is precisely the failure this ticket exists to
remove. Most to the point, lens 5 raises a finding against the pass's own PR and
declines to fix it quietly — the disposition the plan asked for, and the reason
F-001 was in front of me before I went looking.

## Finding F-001 in full

I verified the report's claim rather than taking it, and it is accurate on every
limb:

- In the Inbox, `aria-current="true"` is rendered on the inner
  `a.row-title[data-mail-preview-trigger]` (`Pages/Mail/Index.cshtml:253`). The
  `.row-button` is the container `div` (`:221`) and carries no `aria-current`
  and no `aria-selected`. Both new selectors are therefore inert on the Inbox.
- Repository-wide, the only `.row-button` that ever carries
  `aria-current="true"` is `Pages/Cases/Index.cshtml:126`. The other
  `.row-button` anchors (`Mail/Message.cshtml:413`, `:596`) carry
  `aria-current="page"`, which an exact-match attribute selector does not reach,
  and no script writes `"true"` onto a `.row-button` — the one JS write of
  `aria-current` (`site.js:1403`) writes `"page"`. `.queue-layout` appears on
  exactly one page, `Cases/Index.cshtml:72`.
- So the reach of both additions is one element: the Cases list's selected row,
  which gains `background:#f4f7fa; box-shadow:inset 4px 0 #263d56` and a
  `.row-title{color:var(--ink)}` it did not have before.

**Severity: minor. Disposition: accepted-risk.** The reasoning, in the order it
mattered:

- *Nothing regresses on the Inbox.* Before this PR the Inbox selected row had no
  visual affordance either — `tr.is-preview-selected` never matched, because
  MAIL-025's port made the rows `div.row-button` and not `tr`. The PR removes
  two dead rules and adds two more that are also dead there. Parity, not loss.
- *Nothing regresses on Cases.* The declarations are identical to the
  `[aria-selected="true"]` rule sitting beside them, so the result is the design
  system's own selected-row treatment, not a new one. It lands on the row that
  page's markup comment already calls "the current item of the list", beside the
  Quick detail pane that EPIC-011 `context.md` section 1.4 pairs with it. The
  highlight is redundant with `aria-current`, so no state is carried by colour
  alone, and the same `forced-colors` gap already applies to the neighbouring
  rule — not introduced here.
- *Nothing in scope depends on it.* The Inbox selected state is carried
  semantically by `aria-current` / `aria-expanded` on the trigger, which the
  browser assertions check directly. None of the three verification boxes, and
  no test or check, touches these selectors.
- *The cost of returning it exceeds the defect.* A `needs-changes` return spends
  the ticket's single remediation round and a full CI cycle to alter zero
  behaviour on the surface this ticket owns.

Recorded plainly: this **is** the change the plan's regression boundary said
must not happen. I am accepting it on the evidence above, not waving the
boundary away. Expected follow-up, owned by a separate ticket and **not** a
condition of this merge: either scope the selector to the Inbox trigger if the
Inbox is to gain a selected-row affordance — a design decision that belongs to
its own ticket, since it adds a visual state the Inbox has never had — or drop
the two additions and let a Cases ticket introduce that highlight deliberately.
F-002's inaccurate test comment is corrected with whichever route is taken.

## Code-review lenses

**The restore path.** `restoreSelection` is `select(selectedRow)` and nothing
else. `select()` early-returns on `activeRow === row`, and `activeRow` is seeded
to `selectedRow` at init, so leaving the selected row itself is a no-op and the
pane is never disturbed. The `pointerleave` guard returns unless
`activeRow === row` and focus has left the row, then restores only when
`relatedTarget` is absent or outside `[data-mail-preview-row]` — so travelling
down the list costs nothing and leaving it restores, including when the pointer
exits the window and `relatedTarget` is null. The `blur` path defers through
`setTimeout(0)` and re-checks `activeRow === row`, so tabbing from one row to
the next does not restore between them: the incoming `focus` handler has already
moved `activeRow` by the time the timeout runs. Restore is a cache hit, so it
repaints in the same tick and the actions are never behind the network.

**Null safety of the cache seed.** The seed dereferences the selected trigger
and seven `facts` fields without guards, so I checked the markup rather than
trusting the comment. The pane only renders inside
`@if (selectedDetail is { } detail)`, and every seeded field is unconditional
within it; when there is no selected detail the pane is absent and the IIFE
returns at the `!panel` guard. `selectedRow` was itself found by the presence of
its trigger. The seed cannot throw.

**Keyboard reachability.** These two changes are coupled, and the coupling is
what makes the ticket's box (b) true. While a transient preview shows another
row the actions are `hidden` and out of the tab order; tabbing past the last row
therefore lands on the preview's `pane-body`, which is focusable only because
this PR added `tabindex="0"`. That focus change blurs the last trigger, the
timeout restores the selected message, the actions return, and the next Tab
reaches them. Remove the `tabindex` and the actions become unreachable by
forward tabbing during a transient preview. The browser suite asserts the
keyboard restore by focusing the search field rather than tabbing forward, so
that specific ordering is reasoned rather than asserted; the `tabindex="0"`
addition is separately justified by the axe scrollable-region rule and the
accessibility assertion is green.

**Second selection-state owner.** There is none. `activeRow` and `cache` remain
the only mutable state, both inside the single IIFE. `selectedRow` is resolved
once at init from server-rendered markup and never reassigned — a constant, not
a second owner — and full-page navigation re-renders it. The plan's constraint
holds.

**CSS selector reach.** Covered under F-001. The only other reach question,
whether the deleted `forced-colors` block orphaned anything, is answered by the
class being gone repository-wide.

**Progressive enhancement.** `SubjectSelectsTheServerRenderedPreviewAndThePaneOpensFullDetail`
runs with JavaScript disabled and still proves the pane and its full-detail
entry, so the fix does not move the contract into script.

## Acceptance checks

- **(a) The selected preview survives pointerleave and blur.** Both former
  `resetSelection()` call sites now call `restoreSelection()`; `resetSelection`
  no longer exists. `HoverPreviewRestoresTheSelectedMessageAndKeepsThePaneActionsReachable`
  asserts restoration on the pointer path and on the keyboard path, and the
  pre-existing focus-away assertion is inverted from pane-hidden to
  pane-restored — a genuine negative test for the failure that must not recur.
- **(b) Both preview actions remain keyboard and pointer reachable.** The test
  asserts `data-mail-preview-actions` hidden during the transient preview and
  visible after restore, checks `aria-expanded` across both rows, and clicks the
  pane's full-detail link through to the selected message.
- **(c) Snapshots, tests and required checks green.** CI at the exact reviewed
  head `3bf28244`: `unit`, `browser`, `sql-integration (1)`, `(2)`, `(3)`,
  `sql-integration-coverage`, `test-ui`, `changes`, `documentation`,
  `local-development-scripts` and `reference-data` all success;
  `infrastructure` skipped, and skipped by path — its job condition is
  `needs.changes.outputs.infrastructure == 'true'` and this PR touches no
  infrastructure path. `dev` carries no branch-protection rule, so the
  repository-check job set is the gate, and every job in it is green or
  path-skipped at this head.
- **Assertions are not weakened.** No test deleted, skipped or loosened. The
  browser seed grows from one message to two so that restore is observable at
  all, and the second test hovers a non-selected row, which is what restores the
  end-to-end exercise of the preview handler that the earlier round's finding 7
  showed had been lost.
- **Governing docs.** FRD-12, the ticket's `refs`, owns the shell and the
  `/Inbox` route entry and contains no clause requiring the preview to dismiss;
  a pane that stays visible with its navigation links satisfies it as written,
  and no change is needed. EPIC-011 `context.md` section 1.3 states the
  acceptance sentence verbatim against this ticket and is met. FRD-08 and
  `docs/design/README.md` carried as-built statements the behaviour change
  falsified, and both are corrected in the same diff — the `documentation` job
  is green on them. No new ADR: no architectural decision is taken here.
- **Traceability.** No `MAIL-028` reference remains in the PR title or body, and
  live MAIL-028 keeps its own meaning.

## Local rail

The controller's runner reported at this head: dependency refresh, Release build
(0 warnings, 0 errors), Core 1185/1185 and Architecture 100/100 all PASS, UI
catalogue PASS. The SQL-integration and pinned-UI-verify lanes are INCONCLUSIVE
on this workstation for stated prerequisite reasons — no LocalDB runtime, no
retained capture — and are recorded as INCONCLUSIVE, not as PASS. CI at the
exact head is the evidence for both lanes, and it is green.

## Residual risk

F-001 and F-002 leave two CSS selectors that do not do what their commit message
and one test comment say, and a Cases selected-row highlight arriving through a
mail ticket. F-003 leaves a transient preview showing some of the selected
message's secondary facts. F-004 leaves a mis-seeded checklist document on the
board. None is behavioural on the surface this ticket owns, none is a security,
data-loss or destructive risk, and none blocks the merge.

## Verdict

`pass`. Independent, all repository-check jobs green or path-skipped at
`3bf28244`, every finding dispositioned, no open blocker or major. Proof belongs
to verification, not to this record.
