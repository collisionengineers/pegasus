# Files — MAIL-032

*The files document. Not the research — this is the **surface area** of the change, not the findings behind it.*

This ticket **adopts an existing implementation**. The surface area is therefore
not a forecast: it is the exact changed-file set of PR #640 at head
`ed19e77ff2da8c6a5f87eb20a0222eae17ff15b2` (branch
`task/mail-028-inbox-preview-pin`, base `dev`, `mergeStateStatus: CLEAN`, every
required check green, 2 commits behind `origin/dev`
`9b8f78a36151313bc6d48625edee7f13a2173127`). Evidence:
`gh pr view 640 --json files,commits,headRefOid,mergeStateStatus` and
`git diff origin/dev...HEAD --stat` in
`C:\Users\PGUSER\Documents\github\pegasus-worktrees\mail-028-inbox-preview-pin`
(13 files, +218 / -87).

## Where the change lands

Every row below is **already changed on the branch**. The implementer inspects
and verifies it; a further edit is a deviation (see the plan's Failure and
deviation rules).

| Path | Why |
|---|---|
| `src/Pegasus.Web/wwwroot/js/site.js` | The fix itself, inside the UI-10 `data-mail-preview-workspace` IIFE only (+57 / −20). `resetSelection()` (which set `panel.hidden = true`) is deleted; setup resolves `selectedRow` from the trigger carrying `aria-current="true"` and returns early when there is none; the selected row's projection is seeded into `cache` from the pane's own rendered fields so restore never waits on the network; `restoreSelection()` calls `select(selectedRow)` and is wired into `pointerleave` (guarded by `event.relatedTarget.closest('[data-mail-preview-row]')` so row-to-row movement does not restore) and into the trigger `blur` timeout; `select()` hides `data-mail-preview-actions` while a transient row is shown. Breakage risk: `select()` no-ops when `activeRow === row`, so the restore path depends on that identity check staying intact. |
| `src/Pegasus.Web/Pages/Mail/Index.cshtml` | Markup the script keys on: `data-mail-preview-actions` on the pane's button row, `tabindex="0"` on both `.pane-body.pane-scroll` bodies (height-capped panes were pointer-only), and the Case-association cell now calls `MessageModel.AssociationLabel(...)`. Routed Razor page — pinned Test UI snapshots follow from it. |
| `src/Pegasus.Web/Pages/Mail/Index.cshtml.cs` | `OnGetPreviewAsync` projection label `"Not associated"` → `MessageModel.AssociationLabel(summary.CaseReference)`, so a hover-and-restore cannot flip vocabulary between the JSON projection and the pane. |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` | Adds the single owner of that wording: `public static string AssociationLabel(string? caseReference) => caseReference ?? "No case";` (+9, XML doc included). |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml` | Message page's no-Case cell routed through the same `AssociationLabel` helper (one owner, three call sites). |
| `src/Pegasus.Web/wwwroot/css/site.css` | `aria-current="true"` joins the `.row-button[aria-selected="true"]` selected-row rules (and the `.queue-layout` title rule); the dead `tr.is-preview-selected` block and its `forced-colors` companion are deleted with the class that set them. Shared stylesheet — the added selectors must not alter any non-mail row surface. |
| `tests/Pegasus.IntegrationTests/Browser/MailWorkspaceBrowserTests.cs` | Two-message browser seed; the focus-away assertion flips from pane-hidden to pane-restored; new `HoverPreviewRestoresTheSelectedMessageAndKeepsThePaneActionsReachable` proves pointer + keyboard restore, `aria` state, and that the pane's **Open full message** link targets the selected message (+136 / −44). |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | One assertion updated to the `No case` projection label. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Quick-preview clause: "dismisses when focus moves away" → restores the selected message and stays visible with its navigation links. The hover-era spec sentence is what made the old behaviour look correct. |
| `docs/design/README.md` | Mail preview row of the cross-cutting table records the restore behaviour. |
| `docs/design/test-ui/pages/inbox--default.html` | Pinned Test UI snapshot; the only content change is `tabindex="0"` on the Inbox pane scroll body. Generated artifact — regenerate with the script, never hand-edit. |
| `docs/design/test-ui/pages/inbox--empty.html` | As above. Generated artifact. |
| `docs/design/test-ui/pages/inbox--unavailable.html` | As above. Generated artifact. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `AGENTS.md` (branch copy, §Commands) | The canonical `--locked-mode` restore / Release build / `Category!=Corpus` rail, and the rule that a routed Razor page change is regenerated with `./scripts/Update-TestUiSnapshots.ps1` and proved with `-Verify` plus `./scripts/Test-UiCatalogue.ps1`, with `docs/design/test-ui/` committed alongside. Also the 24 agent-conduct rules the PR is judged against. |
| `docs/runbook.md#locked-restore-build-and-test` | The exact focused per-project forms and the two complementary integration filters (`Category!=Corpus&Category!=Browser`, and `Category=Browser&Category!=Corpus` with `-- xUnit.MaxParallelThreads=2`). Copy them verbatim; do not invent a filter. |
| `docs/frd/frd-12-operator-experience.md` | The operator-experience FRD this ticket refs: it owns the shell, the `/Inbox` route entry and the pane vocabulary. It contains no clause contradicting a persistent preview, so the adoption **meets** it with no document change. |
| EPIC-011 `context.md` §1.3 (Inbox) | The binding acceptance sentence, already written against this ticket: "The selected preview survives pointer leave and blur until another explicit selection or navigation (MAIL-032)", inside the three-pane Inbox contract (Scope / Messages / Message preview with `Open full message`, `Open linked Case`). |
| MAIL-025 / PR #597 | The port that made the preview pane a permanent server-rendered fixture of the URL-selected message. It is why the residual hover-era `resetSelection()` became a defect rather than a design choice — the pane it hides is no longer a tooltip. |
| `src/Pegasus.Web/wwwroot/js/site.js` (UI-10 IIFE, lines ~687–870) | The whole state machine in one place: `activeRow`, `cache`, `request`, `select()`'s `activeRow === row` no-op, and the `pointerenter` / `pointerleave` / `blur` wiring. There is exactly **one** preview-state owner and the fix must not create a second. |

## Ripple effects

- **Pinned Test UI snapshots.** `Pages/Mail/Index.cshtml` is routed, so the three
  `docs/design/test-ui/pages/inbox--*.html` files are generated consequences of
  it. They are already committed at the branch head and `test-ui` is green
  there; they only need regeneration if the merge from `origin/dev` changes a
  rendered page.
- **Shared stylesheet.** The `aria-current="true"` selectors live in
  `site.css` beside `.work-item` rules, so the Work Centre and Cases row
  surfaces are in the blast radius of that one line.
- **`AssociationLabel` call sites.** Three (`Index.cshtml`,
  `Index.cshtml.cs`, `Message.cshtml`) plus the assertion in
  `MailWorkspaceWebTests.cs`. Any fourth copy of the wording is the drift the
  helper exists to prevent.
- **Governing documents.** FRD-08 and `docs/design/README.md` are already
  corrected on the branch; no further document work follows.
- **Kanmer traceability.** PR #640's title and body footer still name MAIL-028,
  which live MAIL-028 (production retained-mail folder mover) must keep; the
  adoption re-homes that metadata onto MAIL-032.

## Out of scope

- **Any repository code change.** The implementation is complete and green. The
  implementer merges `origin/dev`, verifies, and re-homes the PR metadata. A
  code edit is permitted only when the merge or a demonstrated defect forces
  it, and it is reported as a deviation.
- **The remaining Inbox markup, CSS and endpoints outside the 13 paths above** —
  in particular any other `src/Pegasus.Web/Pages/Mail/*.cshtml`, any further
  `site.css` rule, and every `OnGet…`/`OnPost…` handler other than the one
  label line in `OnGetPreviewAsync`.
- **`Pegasus.Core`, `Pegasus.Infrastructure`, `Pegasus.Worker`** — the fix is
  entirely a Web-layer presentation concern.
- **MAIL-028's own subject matter** (production retained-mail folder-mover
  activation) and MAIL-025's port design.
- **Rewriting branch history.** Refresh is `git merge --no-edit origin/dev`;
  never rebase, and the three existing commits keep their MAIL-028 subjects —
  the PR body and Kanmer records carry the correction.
- **Any redesign of the preview** (a second state owner, a persisted
  selection, new transitions) or an unrelated Inbox UI improvement.
