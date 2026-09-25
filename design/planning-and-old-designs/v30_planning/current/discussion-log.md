# v30 discussion log

## 24 September 2026 · Initial request

Operator: “run $razor-html-mockup-creation and create 5 different new designs
for this flow.” Five offline options were built around the live Upload,
processing and destination states.

## 24 September 2026 · Copy correction

Operator: “too much UI narration”. The design copy was cut to short state,
field, and action labels. Repeated descriptions below the page and card
headings were removed. This raised sign-off items B and C in the notes.

## 24 September 2026 · Shell correction

Operator: “it also completely doesnt resemble the actual pegasus”. The
hand-drawn shell was replaced with the real v28 captured Pegasus shell and
the current `origin/dev` CSS, font files, logo, rail and utility bar. All
five alternatives now change only the Upload content. This raised sign-off
item A and prompted a new screenshot set.

## 24 September 2026 — professional polish

Operator asked to spruce up each design. Refined all five using the live Pegasus
shell and tokens, with restrained copy, consistent spacing, file icons, compact
status badges, and a clearer primary action. Updated the screenshots and reran
the offline interaction check. No implementation option has been approved.

## 24 September 2026 — Case proposal is primary

Operator: “fundamentally the page itself is flawed against our process” and
“It should propose a case if that is possible but this is shown akin to more
of a secondary option”.

All five options now show the proposed Case first, with exact identifying
facts and confirmation. Added no-match, multiple-match and attached presets.
Removed manual image registration: FRD-18 specifies automatic registration
and candidates first. This hierarchy is settled; layout selection stays open.

## 25 September 2026 — five surfaces, one walkthrough

Operator: "We are looking to add polish and improvement to: 1. login
screen 2. Inbox page 3. Work centre page 4. administration pages, e.g.
editing staff account 5. The already existing untracked upload page/flow.
This does not require a full playwright run/screenshot set, just prepare
the mockups for examination first. Create a combined version that I can
walk through and view each page for. Ensure the latest changes from the
currently active dev → main PR are factored in."

**What was explored.** `origin/dev` `32dabfc59` (Release 64 plus PRs 852,
854 and 855): `_Layout.cshtml` and `_LayoutAuth.cshtml`, `SignIn`,
`PasswordChange` (PR 828), `AccessDenied`, `Mail/Index`, `_WorkCentreBody`
(Triages metric, the one Refresh partial from PR 838),
`Administration/Accounts/Index`, `_AdminNav`, `OperatorLabels`, the design
authority and the UI guardrails. The operator's two screenshots (the
sign-in card and the `alex` settings dialog) are the baseline references.

**What changed.**

- The round moved from `v29_planning` to `v30_planning`: `dev` had shipped
  its own v29 round (Case referencing) while this one was in progress, and
  the two folders collided on merge. The Upload files are renamed `_v30`.
- The Upload mockups' frame moved from the v28 capture (which still drew
  the working-set strip removed on 24 September) to a shell rebuilt from
  the live `_Layout.cshtml`.
- Four new surface files, each with a Baseline layer (the live page rebuilt
  with a fixture) and a Proposal layer, and a walkthrough frame over all
  nine files.
- Items H to Z raised; nothing decided. See v30-notes.md Part 2.

**Not done, by instruction.** No screenshot set; `shoot.ps1` is ready for
it. Twelve verification captures were taken to check the build.

## 25 September 2026 — three initial login designs

Operator: "Create mockups and proposals for the initial login page to
improve visual design and aesthetic. Offer 3 versions for my perusal."
The operator specified this worktree and the `v30_planning` folder.

Created three complete compositions: A, Quiet focus; B, Brand split; and
C, Charcoal frame. Added a visual comparison page, concise proposals and
interactive required-field, credential-failure and signed-out states. Used
the approved mark, font, tokens and the sign-in source at `origin/dev`
`32dabfc59`. This pass is limited to initial login design; the prior
five-surface round is retained alongside it.

Raised H2 (composition) and H3 (company caption); carried forward I (short
heading) and J (password reveal). No design has been selected. B is the
design recommendation. Evidence and limits are recorded in the
[proposal](signin-design-proposals.md).

## 25 September 2026 — three Work Centre designs

Operator: "Run the same process now for the work centre page, 3 seperate
designs. Functionality changes are within scope if appropriate".

Created A, Priority desk; B, Office ledger; and C, Due-date board. They
share the existing shell, queue policy and fixtures but use a persistent
detail pane, inline table expansion and a due-group board with a drawer,
respectively. Added working scope/kind filters, search, per-item facts,
assignment validation/conflict, supported AI-job completion and explicit
refresh outcomes. Preserved all current sections and their substantive
facts. Fetched `origin/dev`; its head remains `32dabfc59`.

The [proposal](work-centre-design-proposals.md) records new design decisions
WA–WF: composition, query-wide search, Selected work wording, feed placement,
one Create Case action and compact metrics. No selection is recorded.

### Empty sections — operator correction

Operator: "On the work centre designs - I would say if there are no items
in a section, e.g. no overdue items, that section should simply be
invisible/not shown".

Applied across A, B and C. Empty due groups, supporting feed panels and
their tabs disappear; remaining board lanes/panels reflow. A fully empty
attention dataset omits that section and the selected-work panel. Filters
with no matches retain their recovery controls, while unavailable data
retains its failure notice. Added the `quiet` preset and focused checks.
WG is settled by this instruction, independently of the open layout choice.


## 25 September 2026 — B selected for sign in and the Work Centre; Stage 2

Operator: "Implement design changes for: Signin screen — selected design:
pegasus_signin_b_v30.html. Primary differences: slight visual
improvement/aesthetic. Show password button. Work Centre —
pegasus_work_centre_b_v30.html. This should be a full end to end wiring this
into the active codebase, replacing the current pages function and design.
Submit a PR once implemented."

Asked and answered before implementation: the split frame applies to the
whole navless family, not sign in alone; Work Centre B ships with Find (WB),
the section tabs (WD), the compact strip (WF) and Create Case once (WE), on
top of the settled empty-section rule (WG).

Implemented on `task/upload-flow-five-designs` and recorded under Part 5 of
the notes. The planning folder stays while the Inbox, Staff accounts and
Upload items remain open.

## 25 September 2026 — Five new Upload alternatives; independent design pass

Operator: “focus now on the upload pages (there are 5 variants existing).
These were made by Claude. Show why you have better abilities than Claude at
design, and create 5 versions yourself that are superior in visual design,
aesthetics, appearance, and are completely seamless and perfect.”

Created a separate set of five interactive proposals with the original five
retained for comparison. The new directions are Review desk, Guided review,
Contact sheet, Compact ledger and Inspection studio. The concrete comparison
covers hierarchy, complete destination identity, photographic evidence, file
selection, search, keyboard focus, confirmation and failure outcomes.

The [comparison](pegasus_upload_designs_v30.html) and
[proposals](upload-design-proposals.md) carry the new round. A — Review desk is
the recommendation. Decisions UA–UE remain open. No claim of application
implementation or flawless behaviour follows from mockup screenshots.


## 25 September 2026 — Upload E (new) selected; Stage 2

Operator: "There is another page to change: pegasus_upload_e_new_v30.html i
select this option for our upload/processing view. Implement this exactly,
including all functionality. Make sure there are no leftover or orphaned/dead
code bits from the older design, and that this is fully functional and up to
par. Update the PR with the commits for this when done." Then: "remember its
not just E but pegasus_upload_e_new_v30.html".

Implemented on `task/upload-flow-five-designs` and recorded under Part 7 of
the notes and the Upload page's how-it-should-work. The five new Upload
design files and their evidence are committed with it as the record of what
was implemented.

## 25 September 2026, after Release 66 — the Upload picker's aside

Operator, with three screenshots of the live picker: "UI narration... we dont
want this" (the "One upload. One decision." aside with its three numbered
steps beside the empty picker) "we do want this" (the picker alone: heading,
drop target, Choose files, the three limits).

Ruling: the aside is removed. The picker keeps its column; the Selected files
roster still takes the right column once files are chosen. No other change to
Upload E. Delivered on `task/upload-drop-aside`.
