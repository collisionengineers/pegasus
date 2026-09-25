# v30 notes: five surfaces, polished

Temporary Stage 1 review artifact. Opened 24 September 2026 for the Upload
flow ([issue #830](https://github.com/collisionengineers/pegasus/issues/830))
and widened on 25 September 2026 to the sign-in frame, the Inbox, the Work
Centre and Administration → Staff accounts. These offline mockups show
proposed presentation with synthetic fixtures. They are not application,
deployment, or acceptance evidence.

Part 1 is the Upload pass of 24 September (items A to G). Part 2, appended
on 25 September, is the polish pass over the other four surfaces (items H to
Z) and the shell correction that applies to every file.

# Part 1 · Upload (24 September 2026)

## Five alternatives

| Option | Decision view | Processing view | Tradeoff |
| --- | --- | --- | --- |
| A · split | [A](v30-shots/a-decision-1580.png) | [A](v30-shots/a-processing-1580.png) | Files and action both visible on desktop; decision goes first at 760 px. |
| B · guided | [B](v30-shots/b-decision-1580.png) | [B](v30-shots/b-processing-1580.png) | Clear sequence; uses more vertical space. |
| C · destination first | [C](v30-shots/c-decision-1580.png) | [C](v30-shots/c-processing-1580.png) | Fastest access to action; compact file index needs careful failure visibility. |
| D · operations table | [D](v30-shots/d-decision-1580.png) | [D](v30-shots/d-processing-1580.png) | Best comparison for mixed results; wide action area can feel sparse. |
| E · gallery | [E](v30-shots/e-decision-1580.png) | [E](v30-shots/e-processing-1580.png) | Helps only if real thumbnail content adds recognition; JPEG placeholders cannot prove that. |

The [page README](../pages/upload/README.md) links all three widths. The
query strip exposes the remaining states without changing business data.

## Source and frame rules

The shell is taken from the v28 server capture of `/Upload/Group/{id}`. Current
`origin/dev` `site.css` and Inter font files are inlined into each HTML file.
The rail is 220 px on desktop, the utility bar 48 px, body type 13.5 px,
controls 36 px, dense file rows about 40 px, and content is capped at 1580 px.
At 760 px the real shell reflows and the active decision precedes the file
list. The footer strip is mockup control only.

FRD-18 owns the 100 MiB per-file, 20-file and 200 MiB submission limits, the
single submission decision, member outcomes and explicit destination review.
The options do not implement a second custody or policy path. A live single
file still has its own status route; `?state=single` shows the proposed visual
result only.

## Deliberate departures from live

- The legalistic discard checkbox and repeated prose are replaced in the
  proposals by one sentence and a danger action. This changes FRD-18's
  page-shape clause and needs operator sign-off.
- The open decision is put before the file list at 760 px; option C also puts
  it first on desktop. Current P11 says files first and wide, so placement
  needs operator sign-off.
- “vehicle-image case” becomes Vehicle images and Image reference in the
  proposals, per `CONTEXT.md`. The current code and FRD still use the older
  words in places.
- Thumbnail boxes during processing are removed. E explores a contact sheet
  after processing but has no actual images in this artifact.

## Sign-off list (A to G)

Layout items remain **open**. Item E is rejected and replaced below.

**A.** Confirm A, B, C, D or E as the Upload layout, or identify elements to
combine. A is the current recommendation because the compact file ledger
and decision stay visible together on desktop.

**B.** Confirm removal of the discard checkbox and the repeated consequence
copy, or specify the exact confirmation required. FRD-18 currently mandates
the checkbox.

**C.** Confirm the short operator labels “Vehicle images”, “Image reference”,
“Leave undecided” and “Upload more files”, or give replacement words. These
change current visible copy.

**D.** Confirm placing the decision above the file list at 760 px, and in C
on desktop, or retain the current files-first P11 placement.

**E. Rejected — 24 September 2026.** Manual Vehicle images registration was
the wrong primary flow. FRD-18 registers usable image identity automatically.
Matching Cases must be offered first, with explicit selection and confirmation.

**F.** Confirm whether real image thumbnails merit the E contact sheet, or
drop E after reviewing the other four. The JPEG tiles are placeholders.

**G.** Confirm applying the selected presentation to the single-file status
page as well, or keep its existing distinct layout.

## Verification and limits

The 24 September 2026 self-check returned `RESULT {"fail":[],"okCount":705}`
after rendering every option and state. `shoot.ps1` generated 195 captures:
five options, thirteen states, and three widths. There is no server, file upload,
Case search, Worker, saved
receipt, or live confirmation behind these mockups. File names, Case/PO and
Image reference are synthetic. E has placeholders rather than real thumbnails.
Shell navigation and account controls are visual context in this offline file;
the upload controls and state presets are the active review surface.
Stage 2 has not started.

## Refinement — 24 September 2026

All five options retain the actual Pegasus shell, typography and colour tokens.
The second pass increases panel spacing, aligns fields and file metadata, uses
consistent image icons and quieter status badges, and makes Upload more files
a secondary action. A keeps the split workspace; B refines the step indicator;
C keeps per-file failures visible; D aligns the decision above a dense ledger;
E uses evenly spaced JPEG tiles instead of numbered boxes.

The duplicate Upload eyebrow and repeated discard explanation are removed.
The retention consequence appears once in the confirmation. Mobile badges no
longer stretch across a row. Uploading does not claim a fabricated stored count.
This remains a visual proposal; the sign-off items above remain open.

## Process correction — 24 September 2026

Operator: “It should propose a case if that is possible but this is shown akin
to more of a secondary option.” This settles the hierarchy across all five
options. The prior form-first interpretation was incorrect.

- A viable proposed Case is visible first, with Case/PO, registration, claimant
  and stage. Review and add leads to explicit confirmation; nothing auto-links.
- Multiple viable Cases appear together, with none selected by default.
- No match opens Case lookup without inventing a proposal.
- Automatic Vehicle images registration is secondary context for these image
  fixtures. The manual registration and Reason form are removed.
- Confirmation reports the selected Case. Discard and Leave undecided remain
  secondary.

The fixtures remain images. Eligible non-image material uses the existing
extracted new-Case proposal screen under FRD-18; this pass does not invent
an image-to-new-Case form. Verification: 705 offline checks passed.

# Part 2 · Sign in, Inbox, Work Centre, Staff accounts (25 September 2026)

Operator, 25 September 2026: "add polish and improvement to: the login
screen, the Inbox page, the Work Centre page, the administration pages
(editing a staff account), and the already existing Upload flow", with the
latest `dev` changes factored in and one combined walkthrough. No
screenshot set was asked for; the mockups are for examination first.

## 1. What changed and why

| Surface | Today (live, `origin/dev` `32dabfc59`) | Proposed | Item |
| --- | --- | --- | --- |
| Every file | The Upload mockups framed the v28 capture, which still carried the working-set strip removed on 24 September | The shell is rebuilt from `_Layout.cshtml`: no strip, the one Refresh partial, the refined marks, the current rail | — (correction, not a decision) |
| Sign in | 96px mark beside PEGASUS; h1 "Sign in to Pegasus" | The brand row offered as the rail lockup (64px mark, PEGASUS, "Case management"); the h1 offered as "Sign in" | H, I |
| Sign in | Password is typed blind | A show-password control (`eye`) inside the field | J |
| Forced password change | One explanatory paragraph under the h1 | The paragraph goes (page economy); h1, two fields, one button | K |
| Inbox | Sort toggle draws a Unicode arrow | The `arrow-up-down` glyph beside "Received" | L |
| Inbox | Search input and a separate Search button share the filter row | The Search field is the wide cell with its button attached | M |
| Inbox | "N attachments" text; loose meta line | `paperclip` and the count; fixed time column; chip · classification · Case reference | N |
| Inbox | Dismiss × at full weight on every row | Quiet until the row is hovered or focused, never hidden | O |
| Inbox | Empty state explains retention in three sentences | "No mail has been received." | P |
| Work Centre | "Updated HH:MM" floats above the metric strip | It sits in the header beside Refresh, as FRD-15 reads | Q |
| Work Centre | Metric tiles are label and figure | Unchanged by default; a `metrics:toned` switch adds a 3px top bar in the state tone | R |
| Work Centre | Row right column widths vary; three heads carry their metas differently | Fixed right column, tabular figures; one right-aligned meta group per head | S |
| Work Centre | "Select an item" as a bordered line | The shared `pane-empty` treatment | T |
| Staff accounts | Dialog body opens with a Username/State fact grid under a head that already names the account | The state chip joins the head; the fact grid goes | U |
| Staff accounts | Role is a disabled select on one's own account | Role renders as a greyed value box in the same cell | V |
| Staff accounts | Printed name half width; Qualifications full width below | Both share the second row of the two-column grid | W |
| Staff accounts | A native file input labelled "Replace signature" | A settings line like the Glass's line: "Signature image · On file" and **Replace signature** | X |
| Staff accounts | Two foot rows on another account's dialog | One foot: Disable/Enable, Force logout, Reset password, a gap, Delete; then Cancel, Save settings | Y |
| Staff accounts | The temporary password is a panel below the table | A success notice at the top of the list's stack, the password in mono | Z |

## 2. Live rules the mockups mirror

| Rule | Source |
| --- | --- |
| Rail order, counts absent when unqueried, the bell's unread count, no working-set strip | `Pages/Shared/_Layout.cshtml`; design authority § Authenticated shell |
| The auth card: 440px, 4px red top border, 96px mark | `site.css` `.external-shell`, `.auth-card`, `.auth-brand` |
| The invalid-credentials sentence | `Pages/Account/SignIn.cshtml.cs` |
| Inbox scopes, in order, with counts | `Pages/Mail/Index.cshtml.cs` `ScopeDefinitions` |
| Outcome chip words (Case created, Creating case, Case not created, Triage, Unidentified) and their tones | `Pages/Mail/Message.cshtml.cs` `OutcomeLabel`; `Shared/_StatusChip.cshtml` |
| Date above time in a row; the row's link carries `aria-current` | `Pages/Mail/Index.cshtml` |
| Five metrics in order, Triages last; kind chips in order; group heads with counts; "Kind · reference · subject" | `Pages/_WorkCentreBody.cshtml`; `Presentation/NeedsAttentionPresentation.cs`; `OperatorLabels.WorkCentre` |
| Due text words (Overdue, N days overdue, Due today, Due Fri, Due 2 Oct) | `OperatorLabels.WorkCentre.DueText` |
| Arrival chips (Manual, E-mail, Provider API, Automation) and tones | `OperatorLabels.WorkCentre.Arrival`, `ArrivalTone` |
| Administration nav groups and labels | `Pages/Administration/Shared/_AdminNav.cshtml`; `OperatorLabels.Admin` |
| Staff account labels (Sign-off Engineer, Printed name, Qualifications, Signature image, On file, Replace signature, Default sign-off Engineer) | `OperatorLabels.StaffAccounts` |
| Every Administration action posts on the click; Delete alone confirms, in a native dialog | `Pages/Administration/Accounts/Index.cshtml` |

## 3. Frame rules

Unchanged from Part 1 and the design authority: rail 220px (64px collapsed),
utility bar 48px, body 13.5px, controls 36px, dense rows 40px, content
capped at 1580px with 18px page padding, no working-set strip, dialogs in
`.dialog-backdrop` with focus on the first control and Escape to close.
Below 980px the rail lies down; below 760px every layout is one column.

## 4. Decisions taken and their authority

- The shell rebuild is a correction to the live contract (design
  authority, "There is no working-set strip", operator 24 September 2026),
  not a proposal.
- L follows the icon rule (Lucide only; no Unicode dingbats).
- Q follows FRD-15 § Work Centre ("Its head reads 'Updated HH:MM' with
  Create Case and Refresh").
- U follows the guardrail "one fact, one home".
- V follows the guardrails "never use mystery-disabled controls" and "read
  and edit look the same" (operator, 23 September 2026).
- K and P follow the page-economy rule (operator direction, 20 August 2026).
  Both remove sentences; neither adds one.

## 5. Deliberate departures from live

Every departure is a lettered item above. No label, grouping or column was
renamed; no route, control or workflow was added beyond the show-password
control (J) and the metric tone switch (R), both of which the operator may
reject.

## Sign-off list

Items A to G are in Part 1. Every item below is **open**; each is phrased
"Confirm, or …". The mockup shows each as a layer or a switch so the two
readings can be compared side by side.

**H.** Sign in: confirm the brand row as the rail lockup (64px mark,
PEGASUS, "Case management"), or keep the 96px mark and PEGASUS as today.
Switch `brand:compact`.

**I.** Sign in: confirm the h1 "Sign in", or keep "Sign in to Pegasus".
Switch `title:short`.

**J.** Sign in: confirm a show-password control inside the password field
(also on the forced-change card's new password), or leave the field as it
is. Switch `reveal:on`.

**K.** Forced password change: confirm removing the explanatory paragraph,
or keep it.

**L.** Inbox: confirm the `arrow-up-down` glyph beside "Received" in place
of the Unicode arrow, or specify another treatment.

**M.** Inbox: confirm the Search field as the wide filter cell with its
button attached, or keep the separate Search button.

**N.** Inbox: confirm the row meta (paperclip and count, fixed time column,
chip · classification · Case reference), or keep "N attachments" in words.

**O.** Inbox: confirm the quiet Dismiss/Restore button until hover or focus,
or keep it at full weight.

**P.** Inbox: confirm "No mail has been received." as the one Inbox empty
sentence, or keep the retention explanation.

**Q.** Work Centre: confirm "Updated HH:MM" in the header beside Refresh,
or keep it above the metric strip.

**R.** Work Centre: confirm plain metric tiles (the default), or adopt the
toned top bar. Switch `metrics:toned`.

**S.** Work Centre: confirm the fixed right column on attention rows and
one meta group per head, or keep today's flow.

**T.** Work Centre: confirm the shared `pane-empty` treatment for "Select
an item", or keep the bordered line.

**U.** Staff accounts: confirm the state chip in the dialog head and the
removal of the Username/State fact grid, or keep the grid.

**V.** Staff accounts: confirm Role as a greyed value box on one's own
account, or keep the disabled select.

**W.** Staff accounts: confirm Printed name and Qualifications side by side,
or keep Qualifications full width.

**X.** Staff accounts: confirm the signature settings line ("Signature
image · On file", Replace signature / Upload signature), or keep the native
file input.

**Y.** Staff accounts: confirm one foot row (Disable/Enable, Force logout,
Reset password, gap, Delete; then Cancel, Save settings), or keep the two
rows.

**Z.** Staff accounts: confirm the temporary password as a success notice
at the top of the list, or keep the panel below the table.

## Self-check

25 September 2026: `RESULT {"fail":[],"okCount":1743}` over every surface ×
state × layer, the switches, the dialog open/close mechanics and the 65
Upload option × state combinations (their Part 1 checks are carried
forward). Headless Chromium, file access allowed, virtual time budget
120 s. No console error on any rendered state.

## Known limits

- No screenshot set for Part 2 (not requested). Twelve verification
  captures at 1580 and one at 760 are in `v30-shots/`; the 195 Upload
  captures predate the shell rebuild and show the strip.
- Fixtures are synthetic: addresses end in `.example`, names and references
  are invented, counts are chosen to exercise the layouts.
- The Compose dialog, the message record, the Case record, Search and the
  other Administration areas are links that go nowhere.
- Rail collapse, dialogs, the show-password control and the toast region
  work; Refresh, Search, Assign, Save and every post are inert.
- Select controls show their options collapsed; the Category select's two
  optgroups carry a representative subset of the live options.
- Font fallback: Inter Variable is inlined, so the capture matches the
  live face; a browser that refuses data-URI fonts falls back to Segoe UI.

## Part 3 — initial login design alternatives, 25 September 2026

The operator requested three versions specifically for the initial login
page. Open the [comparison](pegasus_signin_designs_v30.html) for A, Quiet
focus; B, Brand split; and C, Charcoal frame. The
[proposal notes](signin-design-proposals.md) contain the rationale, frame
measurements, live-control coverage, departures, handoff and limits.

| Previous problem | Proposed change | Screenshots |
| --- | --- | --- |
| Small white card is the only composition available | A light open form, B split identity/form, C refined charcoal card | 01–03, each at 1580, 1440 and 760px |
| Brand and repeated product title compete in the same stack | Deliberate brand composition and shorter “Sign in” heading | 01–03 |
| Earlier proposals did not expose required-field feedback | Exact messages below each field, with invalid styling and focus | 04–06 |
| Credential failure needs to work within each new geometry | Same generic message, username retained, password cleared | 07–09 |
| Signed-out confirmation must remain legible in each design | Existing green check and heading retained | 10–12 |

The screenshot inventory is in the
[sign-in page README](../pages/sign-in/README.md#three-designs--25-september-2026).
This pass reads the same `origin/dev` snapshot, `32dabfc59`, and preserves
the earlier baseline beside the alternatives.

### Additional sign-off items

**H2.** Confirm A, B or C as the initial login composition, or specify a
combination. This expands open H; B is recommended, not approved.

**H3.** Confirm the Collision Engineers caption, or omit it.

Existing **I** (short heading) and **J** (Show / Hide password) remain open.
K and the earlier other-surface items are not settled by this login pass.
No application implementation has begun.

### Evidence

25 September 2026: `RESULT {"fail":[],"okCount":564}`. The focused harness
covered A/B/C × four states × four widths (1580, 1440, 760 and 390px), plus
validation, password visibility, submission feedback and retry. Actual
keyboard traversal passed for all three. All 36 planned screenshots were
captured at 1580×1000, 1440×900 and 760×1000. No browser console errors or
external requests occurred. See the
[evidence record](v30-signin-shots/verification.json).

This is offline mockup evidence only. The demo sends no credentials and
intentionally ends filled submissions in the incorrect-credentials state.
Forced password change and access denied remain in the prior preview.


## Part 4 — three Work Centre designs, 25 September 2026

The operator requested the same three-design process for the Work Centre,
including appropriate functionality changes. Open the
[comparison](pegasus_work_centre_designs_v30.html): A, Priority desk;
B, Office ledger; C, Due-date board. The
[proposals](work-centre-design-proposals.md) own this pass's rationale,
measurements, complete live-control coverage, functionality proposals,
implementation handoff and limitations. The previous baseline remains
alongside the new files.

| Problem | Proposal | Screenshot numbers |
| --- | --- | --- |
| Current list/detail hierarchy leaves different tasks competing for space | A persistent detail pane, B inline table expansion, C due-date lanes and drawer | 01–03; full-page A/B/C |
| Finding one item requires scanning scope and kind results | Search within Needs attention, combined with current filters | 04–09 plus interaction checks |
| Empty groups consume space | Omit empty groups, feeds and tabs; reflow remaining lanes | 10–12, 34–36 |
| A later refresh failure can be confused with an empty queue | Preserve last-good data; name partial/unavailable sections | 13–21 |
| The old mockup reused one selection's facts | Per-item details and working assignment, including validation and conflict | 22–27 plus interaction checks |
| Arrivals and AI jobs need a deliberate place | Supporting panels in A/C; section tabs in B | 28–33 |

See the [screenshot inventory](../pages/work-centre/README.md#three-designs--25-september-2026).

### Decisions

WA (composition), WB (search), WC (Selected work wording), WD (feed
placement), WE (one Create Case action), and WF (compact queue totals)
remain open. A is the recommendation.

**WG — decided with the operator, 25 September 2026.** Empty due groups and
empty supporting sections are omitted. Filters that find no matches retain
Clear filters; unavailable data retains its failure notice. The quiet
fixture demonstrates a page without Overdue, New cases or AI jobs. The
page's [how-it-should-work](../pages/work-centre/how-it-should-work.md#decided--25-september-2026-empty-sections)
records the settled rule. This needs no further approval.

### Evidence and limits

25 September 2026: `RESULT {"fail":[],"okCount":1905}`. All A/B/C × twelve
states × three widths were checked. The runner captured 108 state shots
and three full-page shots. Actual keyboard traversal, dialog focus
containment, Escape/focus return and section-tab navigation passed. No
console errors or external requests occurred. The
[evidence record](v30-work-centre-shots/verification.json) records the run.

Evidence covers local mockups only. Assignments and AI-job completion
mutate synthetic in-memory fixtures; linked application destinations end
at an explicit preview boundary. Server policy, data access, concurrency,
idempotency, multi-page retrieval and automatic refresh are not executed.


## Part 5 — decisions and Stage 2, 25 September 2026

The operator selected **B** for both surfaces and asked for the full
end-to-end implementation, replacing the current pages' function and
design, in one PR.

### Sign in

| Item | Outcome |
| --- | --- |
| H2 | **B — Brand split.** The charcoal identity panel (96px mark, PEGASUS, Case management, Collision Engineers) beside the white form panel, 42 / 58, stacking below 980px. |
| H / H3 | Settled by B: the vertical lockup and the company caption. |
| I | **"Sign in"**; the identity panel names the product. |
| J | **Show / Hide password**, shipped hidden and revealed by script. |
| K | Not decided; the forced-change page keeps its paragraph and renders in the new frame. |
| Frame scope | **Whole navless family** (operator, 25 September 2026): sign in, signed out, forced password change, access denied, the error family and the consent screen all render in `_LayoutAuth`'s split frame. |

### Work Centre

| Item | Outcome |
| --- | --- |
| WA | **B — Office ledger.** A full-width table; the chosen row opens its facts and next action in place; nothing opens by itself. |
| WB | **Accepted.** Find in Needs attention: a term on the Core query, matched on reference, title, detail and owner before paging; chip counts stay over the scope. |
| WC | Not applicable to B (no Selected work heading); the open row carries the kind eyebrow. |
| WD | **Accepted.** Needs attention, New cases and AI jobs as tabs with counts; AI jobs as compact rows. |
| WE | **Accepted.** Create Case once, in the page header; the utility bar's New case is omitted on this page. |
| WF | **Accepted.** The compact five-count strip. |
| WG | Settled earlier: empty due groups, sections and tabs are omitted; an unavailable section keeps its tab with a dash; a wholly empty page reads "No work to show." |
| Q–T | Superseded by B (Q lands as "Updated HH:MM" in the head; R, S, T fall away with the pane layout). |

### Where it landed

`Pages/Shared/_LayoutAuth.cshtml`, `Pages/Account/SignIn.cshtml`,
`wwwroot/css/site.css` (external frames block), `wwwroot/js/site.js`
(password reveal); `Core/Operations/OperationsSnapshot.cs` (`Search`,
`NeedsAttentionPolicy.Matches`), `Pages/Index.cshtml(.cs)`,
`Pages/_WorkCentreBody.cshtml`, `Presentation/NeedsAttentionPresentation.cs`,
`Presentation/OperatorLabels.cs`, `wwwroot/css/work-centre.css`,
`wwwroot/js/work-centre.js`, `Pages/Shared/_Layout.cshtml`. FRD-15's Work
Centre section, FRD-12's shell sentences and the design README's navless
and class tables were updated in the same change. Inbox, Staff accounts and
Upload (items A–G, L–P, U–Z) remain open in this folder.

### Stage 2 evidence

Focused verification (`scripts/Invoke-Verification.ps1`): Release build clean;
`DashboardBoundaryTests`, `ShellAndStatusPageWebTests`,
`StaffSignInSecurityTests` and `WorkCentreWebTests` green (20 Core, 33 web).
Routed walk on the synthetic hosts (auto-Administrator on 5240, real sign in
on 5241) with [walk-v30-b.py](v30-live-shots/walk-v30-b.py):
`RESULT checks=63 failed=0 shots=33`, record in
[walk.json](v30-live-shots/walk.json), captures in `v30-live-shots/` at
1580, 1440 and 760 for sign in (default, required fields, refusal, signed
out), the 404 and access-denied family, and the Work Centre (default, open
row, Mine, no match, New cases tab). Behaviours proven on the routed page:
Show / Hide, refusal notice with username kept, a row opening and closing in
place, Clear filters clearing kinds and term, tabs switching in place with
the address updated, Home key returning to the first tab, no console errors,
no horizontal spill at any width. Three defects the walk caught were fixed
before commit: the focused input painted over the Show button (global
focus-visible z-index), the narrow identity strip hid "Case management"
(shell rule), and the tab click handler matched the root's `data-wc-tab`
attribute and swallowed row links. The fixture host had no Unassigned row,
so the Assign Engineer dialog is covered by the web tests, not the walk.
