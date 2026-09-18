# v28 notes

As-is capture, no proposed changes. Built 18 September 2026 from `origin/dev`
at `6c02a8608` (worktree `task/ui-baseline-capture`, HEAD equal to
`origin/dev`).

## 1. Scope relative to the previous round

There is no "previous problem → change" table this round: v28 proposes
nothing. What changed in *scope* rather than in the product:

| Previous round | Scope | This round |
| --- | --- | --- |
| v27 | Case record only, with a togglable proposals overlay | v28 covers every routed page (the "whole shell"), with no proposals overlay at all |
| v26 | Whole shell as one hand-written client-side SPA (two files, ~11,000 lines of bespoke JS) | Whole shell as nine independent, self-contained files sharing tokens/chrome but no client router or shared JS state (see §9 and `discussion-log.md` for the reasoning) |

Every page family below was read fresh from the current live `.cshtml`/`.cs`
sources on 18 September 2026, not carried forward from v26 or v27's content
(those files were consulted only for CSS-class/markup-pattern reference,
per each lane's brief).

## 2. Files and what they capture

| File | Live pages | Shots |
| --- | --- | --- |
| `pegasus_work_centre_v28.html` | Work Centre | s13-s15 |
| `pegasus_cases_index_v28.html` | Cases Index, Create Case | s16-s18 |
| `pegasus_case_record_v28.html` | Case record + Eva/Send + Create audit (Vehicle/Workflow/Tasks/Closure/Custody are mutation-only, no markup — see §5) | s19-s24 |
| `pegasus_triage_unidentified_v28.html` | Triage, Unidentified (both live as Cases-index tabs; the standalone Index pages are dead routes — see §5) | s25-s28 |
| `pegasus_image_intake_v28.html` | Vehicle images (ImageIntake/Details, PreCaseImages) | s29-s30 |
| `pegasus_mail_upload_v28.html` | Inbox, Message, Compose, Upload, UploadStatus, UploadGroupStatus | s31-s35 |
| `pegasus_search_operations_v28.html` | Search, Operations | s01-s05 |
| `pegasus_administration_v28.html` | Administration hub + 11 composed sub-areas | s36-s48 |
| `pegasus_account_shell_v28.html` | Sign in, Signed out, Access denied, Password change (forced), Connector consent, Error, Status codes | s06-s12 |

## 3. Live rules the mockup mirrors

- Rail order and gating: `_Layout.cshtml` — Work Centre, Inbox, Upload,
  Cases, Search, Operations always; Administration only when
  `User.IsInRole(StaffRoleNames.Administrator)`; Inbox/Upload/Operations
  only when `ViewData["ShellInboxEnabled"]` (`Environment.IsProduction() ||
  (IsDevelopment() && Features:LocalIntake)`) — captured as always-on since
  the deployed instance is Production.
- Rail counts: `RailCountsPageFilter.cs` supplies real counts; an absent
  count renders nothing, never a stale `0` (`_Layout.cshtml` `CountFor`).
- Status-chip tone table: `Shared/_StatusChip.cshtml`'s `key switch` —
  amber for incomplete/pending, navy for Review/in-flight, green only for
  confirmed completion, red for blocked/failed, neutral otherwise. Two
  states fall through to neutral with no explicit entry (Query — item B;
  every `ImageIntakeLifecycleState` wording — noted in
  `pages/image-intake/how-it-works.md`).
- Case record frame: `Details.Frame.cs`/`Details.cshtml` — the sticky
  ribbon + section row, one page-wide edit session, the Actions menu gated
  to permitted progressions, Scroll default with a session-only Tabs choice.
- Administration nav order and gating: `Shared/_AdminNav.cshtml` — People
  and access (Staff accounts & roles, Contacts) → Configuration (Workflow
  configuration, Mail settings, Valuation presets) → Operations and
  oversight (Service health, Logs, Reports, AI jobs, Automation & AI —
  composed-only).
- `AccessDenied.cshtml` carries no `Layout` override and so renders inside
  the ordinary authenticated shell, not the navless `_LayoutAuth` frame —
  item A.

## 4. Frame rules, as numbers

Unchanged from the design authority, verified against `site.css` /
`case-workspace.css` by every lane: `--rail` 220px (64px collapsed),
ribbon 56px, section row 40px, `--content-max` 1580px, `--gap` 12px,
`--page-pad` 18px, `--row` 40px, body 13.5px, controls 36px, aside 285px
folding above 1441px, breakpoints at 1360/1180/1100/980/900/760px per
`docs/design/README.md`.

## 5. Decisions taken

**None.** This round proposes no design decisions; every choice below is a
build-mechanics choice (how to capture), not a product choice, and is
recorded for reproducibility only:

- Nine independent self-contained files rather than one hash-routed SPA
  (discussion-log.md, 18 September).
- Shared `v28-build/build.py` + `shell-chrome.html` + `navless-chrome.html`
  + `mock-engine.js` infrastructure, adapted from `v27-build`'s Python/Node
  pattern, owned by the integrator; each lane wrote only its own
  `pages/<name>.{body.html,js,strip.html}` and page-folder docs.
- Two genuinely dead-route findings, confirmed from source rather than
  guessed: `Triage/Index.cshtml`, `Unidentified/Index.cshtml` and
  `PreCaseImages/Index.cshtml` redirect/404 (the real lists are Cases-index
  tabs); `Cases/Vehicle.cshtml`, `Workflow.cshtml`, `Tasks.cshtml`,
  `Closure.cshtml`, `Custody.cshtml` are POST-only mutation endpoints with
  `OnGet() => NotFound()`, not separate screens.

## 6. Deliberate departures from live, with reasons

Consolidated from every lane's page-folder Notes (see each `README.md` for
the full detail):

- **Search** — the "Selected Case" preview swap on row click is a static
  toast rather than a live client-side content swap (`pages/search/README.md`).
- **Work Centre** — filter chips/links render but do not dynamically
  re-filter (no server); the preview panel shows the hero Case rather than
  literal list order (`pages/work-centre/README.md`).
- **Cases Index** — only the selected row's Quick detail renders per tab;
  Principal filter options are representative, not derived from loaded rows.
- **Create Case** — only the manual-creation branch is captured; the
  receipt-seeded variant is a different screen with no direct link from
  Cases Index/Upload/the shell's New case action in this pass.
- **Mail** — Category filter shows a representative subset, not the exact
  `MailOperationalDestinationPolicy`-derived list (item E); Deleted Items
  always shows as already-searched.
- **Upload** — `UploadStatus`'s 11 `UploadOutcomeKind` variants are all
  modelled; `UploadGroupStatus`'s per-file button visibility is one
  representative case, not the full boolean cross-product (item F).
- **Triage/Unidentified** — list-tab visual fidelity depends on
  `cases-index.css`; **settled during integration** by adding
  `cases-index.css` to `build.py`'s `triage_unidentified` entry and
  rebuilding (verified clean by re-running `smoke.mjs`), so this is no
  longer an open gap.
- **Vehicle images** — the Image-initiated Case record's lifecycle chip
  renders neutral grey for all three states (no `_StatusChip` tone entry
  matches `OperatorLabels.ImageIntakeLifecycleState`'s wording), captured
  as-is rather than smoothed to a tone.
- **Administration** — full dialogs are built for one representative row
  per list (Accounts: Sam Whitlock; Mailboxes: one mailbox/category;
  Contacts: Wingrove Insurance); other rows toast "Demo". Lease/heartbeat/
  "Take over" concurrency mechanics are not modelled anywhere in this file.
- **Case record** — the Damage Plan clicker uses a simplified rectangle-
  zone grid rather than the exact `DamagePlanGeometry` SVG paths (the real
  severity codes/labels/CSS colouring are used); `AssessmentCanOpen`/
  `AssessmentIsReadOnly` is approximated (item J); per-outcome closure
  gating is approximated (item K); Create audit's precondition set
  collapses to one toggle; the viewer's crop is a tool-switch stub, not
  live drag geometry.
- **Account/navless family** — `PasswordChange`'s voluntary (signed-in,
  full-shell) variant is not separately captured; its form fields are
  identical to the forced variant shown, and the app-shell chrome around it
  is already captured on every other page in this round.

## 7. The sign-off list

Every item below is a genuine ambiguity in **how to capture** current live
behaviour — none changes an FRD, audit event, vocabulary term or an
operator-placed control, since this round proposes nothing. Phrased
"Confirm, or …" per the sign-off protocol.

**A.** `Account/AccessDenied.cshtml` carries no `Layout` override, so
ASP.NET's default (`_Layout`, the full authenticated shell with rail and
utility bar) applies live. `docs/design/README.md`'s "Case record frame"
section states "`_LayoutAuth` remains the navless frame: sign-in, the
signed-out confirmation, access denied and the error family are not places
in the application" — but Access denied is demonstrably not navless in the
live source, unlike `Error.cshtml`, `StatusCode.cshtml` and
`Connect/Authorize.cshtml`, which do carry the override. Captured in
`pegasus_account_shell_v28.html` (the navless file, for grouping
convenience) with an inline note that live rendering actually keeps the
full shell. **Confirm, or**: is this an intentional exception the design
doc should record, or is `AccessDenied.cshtml` missing its
`Layout = "Shared/_LayoutAuth"` line?

**B.** The case-lifecycle `Query` state has no explicit entry in
`_StatusChip.cshtml`'s tone table, so its chip renders neutral (grey) while
every sibling workflow state (Review navy, With Engineer navy, Held amber,
Completed green) has an explicit tone. Captured as the live neutral
rendering. **Confirm, or**: is Query's neutral tone intentional, or a gap in
`_StatusChip`'s switch?

**C.** The live Work Centre AI jobs table shows "Lease expires HH:MM" for a
Taken job (`OperatorLabels.WorkCentre.LeaseExpires`) — genuine, existing
product copy, but "lease" is on this skill's banned-word list for *invented*
UI text. Kept verbatim per "faithfully mirror current live behaviour; use
only `OperatorLabels` strings." **Confirm, or**: should a future round
propose renaming this live string, given the banned-word list's own
rationale (operator-facing text should not expose internal/technical
vocabulary)?

**D.** The manual Create Case form's Case-type `<select>` silently omits
Audit (the server rejects it too), with no on-screen explanation of why.
Captured as the live omission. **Confirm, or**: should this gap get a named
decision (an explicit "Audit case created from an Inspection + Audit Case
only" note, or similar) in a future round?

**E.** The Mail Category filter's exact live option set (driven by
`MailOperationalDestinationPolicy`) was not independently traced by the
lane that built it; the mockup shows a representative subset using real
category names. **Confirm, or**: should a follow-up pass verify the exact
live list before this capture is relied on for that filter's completeness?

**F.** `UploadGroupStatus`'s per-file action-button visibility depends on
several overlapping conditions (`UploadOutcomeCompact`,
`OpenGroupDecision`, `RefreshAutomatically`) that were not fully cross-
verified against every combination; one representative "decided" case is
shown. **Confirm, or**: is deeper verification of this cross-product
warranted before Stage 2, given it governs a real staff decision point?

**G.** `docs/design/README.md` documents `accounts.png`, `configuration.png`,
`mailboxes.png` and `automation.png` as live on Administration panel heads,
but no live `.cshtml` under `Pages/Administration/**` actually references
`images/marks/` anywhere — every area head uses a Lucide icon only.
Captured as live (no marks in use). **Confirm, or**: were these marks
removed from Administration at some point without the design doc being
updated, or were they never actually wired in?

**H.** Several already-shipped Administration strings contain words this
skill's banned-word list forbids in *invented* copy: "Failed intake",
"Oldest pending intake", "Cache bytes", "Verified encoded-message size
limit (bytes)". Captured verbatim as existing live copy (same reasoning as
item C). **Confirm, or**: should a future round propose relabelling these?

**I.** The Administration hub's card icons diverge from `_AdminNav`'s icons
for the same area, for at least Service health, Reports, and Automation &
AI. Captured as the live inconsistency. **Confirm, or**: should hub and nav
icons be reconciled in a future round?

**J.** The Case record's Engineer sections (Damage/Valuation/Estimate/
Settlement/Report) become editable per `AssessmentCanOpen`/
`AssessmentIsReadOnly`, which read from an injected `assessmentAccess`
service outside `Details.cshtml.cs` that was not traced further. Captured
as "assessable from Review state onward" — a judgment call, not a traced
fact. **Confirm, or**: should a follow-up pass open that service to state
the exact rule?

**K.** Per-outcome Case closure gating (`CaseLifecycleRules.ValidateClose`/
`RequireClosureIsAllowed`, in Core) was not opened; the capture offers the
full adverse-closure action group (Archive, Correct principal, Close case,
etc.) whenever an edit session is open and the Case is not already closed,
rather than a state-derived subset. **Confirm, or**: should a follow-up pass
trace the exact per-state permitted set?

_No items are rejected or settled with a date yet — this is the first
round._

## 8. Self-check result

18 September 2026: `RESULT {"fail":[],"okCount":240}` —
`node design/planning-and-old-designs/v28_planning/current/v28-build/selfcheck-runner.mjs`,
run twice for stability, both clean. 240 assertions across all nine files:
each file's default load, `window.MOCK` presence, a page-specific content
assertion, zero console errors, and (on every shell page) the Notifications
dialog opening via its query-string preset — each repeated across roughly
4-13 state presets per file (more for the Case record, given its size).

Screenshots: 144 PNGs in `v28-shots/`, at 1580×1000, 1440×900 and 760×1000
for every state cited above, taken by
`node design/planning-and-old-designs/v28_planning/current/v28-build/shoot.mjs shots.json`
— all 144 reported clean (zero console/log errors during capture).

## 9. Known limits

- No cross-file client router or shared JS state exists — a "case opened
  from Search" does not actually carry into `pegasus_case_record_v28.html`
  the way the real working-set strip would; each file's fixtures are
  independent synthetic data using the same hero Case identity
  (`QDOS26214` / `MA59 BDY`) by convention, not by shared runtime state.
- Font fallback, synthetic images (no real evidence photography), a fixed
  demo return for every "Get valuation"/lookup action, representative
  (not exhaustive) failure codes, and several dialogs stubbed to a toast
  rather than a full flow (noted per-page in §6) are not shown.
- The mockup strip's Role/Data/Freshness toggles are demo-only; they do not
  prove server-side authorisation, a real empty database, or a real clock.
- Process note, recorded for the operator rather than hidden: during the
  Administration lane, a sub-agent it dispatched (explicitly instructed not
  to write files) wrote files anyway, causing a brief collision the lane
  resolved by adopting the sub-agent's version as final after independent
  verification; separately, repeated headless-Chromium smoke runs across
  lanes leaked orphaned processes that briefly filled the workstation's
  `C:` drive, resolved by killing the orphaned processes and clearing the
  NuGet HTTP cache. Neither affected the files kept in this round — both
  are noted for awareness, not as defects in the capture itself.
- A completely independent, unrelated Claude session was found mid-task
  editing `triage_unidentified`'s files in this same worktree; the two
  sessions coordinated by message and the peer stood down. Recorded in
  `discussion-log.md`.

## 18 September 2026 — round complete

Nine files built, 240/240 self-check assertions pass, 144 screenshots taken
clean, every routed page and every genuinely dead route accounted for (§
"Not given a mockup surface, and why" in `pages/README.md`). Zero design
decisions were made; eleven genuine capture ambiguities (A-K) are recorded
above for operator sign-off. Stage 2 is not started.
