# Case workspace v25 mockup — discussion log

Session date: 2026-09-11/12. Participant: Alex (operator). This is a chronological record
of the request, the exploration, and each round of feedback that shaped
`pegasus_case_workspace_v25.html`. It is a scratch record of *how* the mockup came to be,
not design authority — see `v25-notes.md` for the design decisions themselves and what
still needs operator sign-off.

## 1. Starting request

Alex asked for a comparison of `pegasus_pack/ui/pegasus_case_dashboard_v24.html` — a
private, fixture-driven, single-file mockup of one Case page — against the current
Pegasus Case record UI, plus suggested integration steps.

Two exploration agents mapped:
- the live Case record structure (`src/Pegasus.Web/Pages/Cases/Details.cshtml` and its
  `Shared/_Case*.cshtml` partials): one scrolling record, sticky ribbon/action bar/edit
  bar/section jump-nav, ten fixed sections (Overview, Inspection details, Vehicle,
  Damage, Valuation, Estimate, Settlement, Report, Files, Notes);
- which v24 mockup features already exist in the domain/UI, which exist only in Core with
  no Case-page surface (the valuation calculator), and which are absent, design-only, or
  in conflict with binding design authority (the "Brian" AI decision panel, per-field
  padlocks, send-as-approval, on-page "composed" sentences, product types, the Fee tab).

Alex chose scope: **presentation refinements + surfacing the existing valuation
calculator**, explicitly deferring the AI decision panel.

## 2. Widening the review before planning

Alex pushed back: *"You've focused too much on what's literally named... consider UX,
ease of use, general poor design... simplicity... the big difference in look between
non-edit and edit mode is bad... pressing certain edit-mode buttons teleporting back to
the top/overview is bad... having edit mode disabled is bad."*

Two more agents audited the live record's actual UX mechanics against `origin/dev` (the
deployed head — the checked-out branch was 16 commits behind and had defects dev had
already fixed) and against the operator's own 9 September UI review and the
`Pegasus-v2-Refined-Pack` durable design rules. Findings, in outline:

- **Teleporting** — `RedirectToDetails` (no `section` param) is the default return path for
  most Case handlers; only Estimate/Report/Valuation carry a section. Report-draft errors
  redirect to *Estimate*, not Report. No JS restores scroll position.
- **Two different pages for read and edit** — read rows are `detail-list` (10rem/1fr grid,
  legacy tokens); edit rows are `.field` stacks in a different grid; several sections
  render both the read panel and a separate "Edit …" panel at once.
- **Editing silently unavailable** — engineering fields are read-only outside
  With Engineer/Post report with no visible reason; a colleague's lease renders no
  control at all; every save opens a mandatory "Reason for changes" dialog; ~23 controls
  (suggestion chips, lookups, estimate actions) post immediately and end the edit session
  on success, discarding unsaved work elsewhere.
- **Repetition** — Engineer/Sign-off/State/Due appear three times; Registration twice;
  Repairer in two sections; storage split across two sections.
- **Sticky block height** — ~250px before the operator's own WP6 fix, ~136px after it on
  `origin/dev`, still five stacked rows (ribbon, presence, action bar, edit bar, nav).

This became a two-stage plan: **Stage 1** — build one merged visual mockup and stop for
approval; **Stage 2** — the Razor implementation, planned but not started until Stage 1 is
approved. Alex approved the plan, then added mid-turn: *"first stage should be making a
visual mockup"* and *"pause, show mockup, if approved, move on"* — confirming the
two-gate structure already chosen.

## 3. Building the mockup

The mockup (`pegasus_case_workspace_v25.html`) was assembled as one self-contained,
offline HTML file using the live shell's actual tokens, rail, utility bar, workspace tabs,
Lucide glyphs and the real `DamageDiagramGeometry` SVG geometry, all pulled from
`origin/dev`. Fixture values reuse v24's synthetic MA59BDY/QDOS26214 case.

Design choices taken (full rationale in `v25-notes.md`):
- two-row sticky block (ribbon + section nav) instead of five stacked rows;
- one geometry for read and edit — every fact is a labelled cell whose value box and
  control box occupy the same space, so entering/leaving edit mode never reflows the page
  or moves the scroll position;
- per-section availability stated as a label ("Available With Engineer") rather than
  silent disablement;
- the valuation calculator (guide selection, commercial VAT, previous total loss 10/20%,
  value-increase presets, custom additions, condition deduction, Apply) built against the
  actual Core policy shape (`ValuationCalculations.cs`), not v24's simplified version;
- storage money fields relocated to Inspection details; one mileage box with a source
  chip replacing four boxes; Settlement/Figures de-duplicated against the ribbon.
- explicitly **not** adopted: "Brian" naming/panel, padlocks, send-as-approval, on-page
  composed sentences, product types, average-mileage arithmetic, a Fee tab, v24's own
  tokens/fonts.

Six items were flagged as needing Alex's sign-off rather than decided unilaterally (save
without a reason dialog; Sign-off Engineer's ribbon placement; Back to Cases removed from
the record frame; outcome/legal chips' location; storage fields moving section; whether
immediate-post actions should end the edit session) — see `v25-notes.md` §"Decisions that
need your sign-off."

The mockup and screenshots (rendered with headless Chromium via Playwright) were sent to
Alex for review.

## 4. Feedback round 1 — header clutter and navigation

Alex, quoting a cropped screenshot of the action row: *"this random jumble of buttons is
really bad. I also think the left nav bar needs a collapse button."*

Changes made:
- collapsed the action cluster from six separate buttons to two: the primary action
  (Edit Case → Editing/Cancel/Save) plus one **Actions** menu holding Send to EVA, Mark
  report sent, Place on Hold, Create upload link, Correct principal, and — below a divider,
  in red — Close case;
- moved Refresh out of the ribbon row onto the section-nav row, beside Scroll/Tabs;
- added a rail **Collapse** control at the rail foot, narrowing it to 64px (icons and
  counts only), remembered via `localStorage`.

Both were recorded as new sign-off items (C2: Close case now one click further into the
menu than the operator's 9 September review asked for; C3: the rail collapse itself).

## 5. Feedback round 2 — remaining clutter, and a coverage check

Alex, from a second cropped screenshot: *"editing chip largely redundant so should be
removed for space"*; *"the total-loss cat N and unroadworthy [chips] should be moved to
the side container"*; and, invoking the Explore agent by name: *"Glasses button got
removed — check any other features not missed."*

- Removed the standalone "Editing" status chip (Cancel/Save already say the same thing).
- Moved the Total loss·Cat N and Unroadworthy chips out of the section-nav row into the
  **Figures** card in the context aside.
- Ran a full audit agent enumerating every control and displayed fact on the live
  `origin/dev` Case record, diffed against the mockup's inventory. Result: ~90 items the
  live record has that the mockup lacked (mostly dialog contents, by design) and a smaller
  "new in the mockup, not live" list. Restored the substantive drops: Glass's
  launch/session line, New estimate, Import, Send to AI, Compare, estimate header fields
  (name, repair days, rate card, labour rate, paint materials, other costs, VAT%,
  repairer VAT status) and notes; tyre/belt/spare/centre-belt fields, material transfer,
  unrelated-damage deduction, incident narrative; Settlement's excess, betterment,
  reserve, diminution, hire fields, delays, derived storage charge/repair days, and the
  seven salvage fields; Report's Preview draft, Fee note only, generation facts, fee
  description, override-report-date, Engineer's comments (moved back from Settlement),
  valuation commentary text, statement of truth, Cc; Files' Open in Box, Open Operations,
  Save as, Remove, the public-upload-requests table, Queries/Compose; Notes' Record chase;
  Overview's workflow stepper, outstanding-requirements panel, Due, Contact; Inspection's
  principal default and repairer-contact select; Vehicle's Year; and a state-dependent
  Actions menu (its contents now change with lifecycle state — Hand to Engineer only in
  Review; Send to EVA/Mark report sent/Mark completed/Return to Review With Engineer;
  Return to Engineer/Archive when Complete; Hold ⇄ Release Hold).
- Noted one vocabulary correction while restoring these: the live button is labelled
  "Send to Claude"; `CONTEXT.md` bans that phrase in operator-facing copy, so the mockup
  uses "Send to AI".

## 6. Feedback round 3 — full-screen estimate

Alex: *"expandable / full screen for the estimate bit too would be good."*

Added an **Expand** control to the Estimate panel head. It pins the panel over the full
viewport (its own head stays sticky) so the estimate tabs, header fields, line grid,
discounts and totals get full width; Escape or the same control (now a ×) restores the
page. Verified the edit session and scroll position are unchanged after collapsing back.
Recorded as sign-off item C4 (a presentation-only toggle on the existing section host, no
new form, for Stage 2).

## 7. This commit

Alex: *"stage, commit, and push the mockup with any other changes made too. Create a log
of the discussion and requirements, and keep the mockup, this log, and any other key
files related to the mockup in its own distinct folder."*

`pegasus_pack/` is blanket git-ignored and its own README says private pack material must
not be published to the tracked repository. `docs/index.md` provides the resolving
mechanism — an explicit operator request may create a temporary review artifact at a
requested path. This folder is that artifact: a new branch
(`task/case-workspace-v25-mockup`, off `origin/dev`) carrying this mockup, this log,
`v25-notes.md` and the session's screenshots, at
`design/planning-and-old-designs/case-workspace-v25-mockup/` — a path already
pre-provisioned in `scripts/Test-MarkdownPlacement.ps1`'s allow-list for exactly this kind
of material, even though no top-level `design/` folder existed before this commit.

`pegasus_case_dashboard_v24.html` (the operator's own private reference file that v25 was
built from) was initially left in `pegasus_pack/ui/`, described but not published, on the
assumption that only the merged result was in scope. Alex asked for it to be included too
("that reference should be there too"), so it was moved into this folder in a follow-up
commit alongside the others.

## 8. 12 September — usability pass and v26

Alex, on `dev`: *"examine the UI and its process in general… imagine you're a person
trying to use it. whats good. whats bad. is it easy? whats awkward? We need to fold in
the Glass's changes on our old UI, into this new one. we need solid layout. we need the
management UI integrated enough to keep him happy (v24), whilst still maintaining some
competency."*

The pass found that v25's frame, one-geometry edit and availability labels hold, but:
edit was only reachable from the ribbon; images sat ninth of ten sections; the Estimate
head repeated the round-one button jumble; read mode rendered edit furniture (seven
presets, eight header fields, a permanent drop zone); explanatory copy had crept back;
and the Glass's line was a two-button toy that contradicted the six Glass's commits
that landed on `origin/dev` on 11 September (own window, ten states, guarded Close,
held-elsewhere, one outcome sentence per state, Complete import).

Against v24, the manager's largest distinct element — the decision review — was the one
thing v25 lacked that is worth adding; Core already has an AI suggestions page with
Accept and Correct, so a proposal column can render only from a real reviewed proposal.

Alex: *"okay go for it."* v26 was built as the same single file (see `v26-notes.md`),
rendered across 22 states into `v26-shots/`, and committed on
`task/case-workspace-v26-mockup` off `dev`.

## 9. 12 September — every page mocked

Alex: *"thats what i meant. i want everything mocked"*, after the self-check report had
said the rail's other destinations were placeholder links.

Two agents inventoried every routed Razor page on `origin/dev` (headings, controls,
columns, chips, empty states, onward links, admin groups and role gates). The pack's
EPIC-012 v2 prototype already had the same pages on the same tokens, so the shell was
built from those modules with a recorded correction list against the inventories, the
vendor name replaced, and the Case route handed to the v26 file. The administration
hub, its three-group navigation, Contacts absorbing Principals, AI jobs, the three
Reports panels, Default sender, Valuation presets and the Glass's login reached from
the account were added to match live. `pegasus_shell_v26.html`, `v26-shell-selfcheck.html`
and the `s*.png` shots were committed on the same branch.

## 10. 12 September — damage clickers, the working set, the viewer

The operator asked for three expert-tier variants of the vehicle damage
clicker, selectable in the mockup: Plan, Elevations and Dial were built on the
same zone model with severity-graded fills, numbered markers and hover naming
(§ Damage clicker in `v26-notes.md`).

Then: "focus on the tabs. I liked the idea a lot but I'm struggling to see how
to make it good." The assessment named the problems (a grey band with dead
space, Work Centre pretending to be a tab, the active tab repeating the header,
an ambiguous "+ Open", no state on the tabs, nothing to switch between) and
proposed a working-set-only strip fused into the record, stateful tabs and no
fake Open tab, with a rail list or a breadcrumb menu as cheaper alternatives.
"Put your good ideas in" — the fused working set was built across both files
and persisted between them. "Also make the image viewer work" — the toast was
replaced by a real viewer on both files, with synthetic images since the mockup
has no files behind it. "Actually put all the options in on the chooser
widget" — layout (fused, band, rail list, breadcrumb), label, icons, density,
visible count, glyphs, close behaviour, Work Centre and Open tabs became
variables on the strip of both files. A screenshot showed the Cases Quick
detail card escaping its pane (an unbreakable e-mail address widening the
grid track) and no crop or tag on images: the pane was fixed and both viewers
gained a stored-rectangle crop and a tag select.

## Outstanding

Stage 1 is at v26 with every page mocked; it has not yet been given final approval to proceed to Stage 2 (the
Razor implementation). Sign-off items A–F (`v25-notes.md`) and G–Q (`v26-notes.md`)
are open. This folder is temporary per `docs/index.md`'s carve-out — remove it once
Stage 2 lands or the design is formally accepted or rejected.
