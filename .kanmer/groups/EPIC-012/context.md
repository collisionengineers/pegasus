# EPIC-012 context — Case Workspace v2 (single-scroll Engineer workbench)

Inherits EPIC-011 `context.md` and `waves.md` in full. Source of record:
`Downloads/Pegasus_UI_v2.html` + `Downloads/Pegasus_UI_v2_src/` (2 September
2026, second pass; amended 3 September 2026) and
`Downloads/Pegasus_UI_v2_notes.md` (its "Amendments (3 September 2026)"
section binds). Execution plan: `Downloads/Pegasus_UI_v2_implementation_plan.md`
as adjusted on 3 September 2026 (model policy, D44–D46). Decisions D29–D43 are
recorded in the governing documents (DELIV-041, PR #647); D44–D46 are recorded
by PLAT-070's PR and D47 by CASE-040's. They govern where they differ from
EPIC-011 D1–D28.

## Feature outcome

The whole Case, including the Engineer's work, is one scrolling record with
Overview, Engineer notes, Inspection, Vehicle, Damage, Valuation, Estimate,
Settlement, Report, Files, Notes; every field the mockup shows has a Core owner,
a production caller and a snapshot state.

## Users affected

Staff (Users), Engineers, Administrators. No external surface changes.

## Acceptance criteria

- `/Cases/{id}` renders all eleven sections in every state; Engineer sections
  are read-only once Complete; `/Cases/{id}/Assessment` redirects to
  `/Cases/{id}?section=estimate`.
- Sign-off Engineer: separate field beside Engineer; only flagged accounts
  with a signature on file are offered; default is the assigned Engineer when
  flagged, otherwise the account carrying the Administrator-set "Default
  sign-off Engineer" designation; reports render the sign-off tuple.
- Damage map, settlement figures and the fee note appear on the report preview
  from the accepted record.
- Cropping never changes source bytes; issued reports keep their curation
  snapshot; the crop tool is reachable from Files and Report.
- Awaiting instruction is a Pre-case queue; Operations carries no service
  health table; no staff review flag, checkbox or dialog exists.
- The first Send to EVA moves the case from Review to With Engineer by either
  route (D47).

## Non-goals

A Scroll/Tabs switch; savings or comparison figures (D17); CAP HPI, AutoTrader
as a manual source, Vehicle data, valuation adjustments, rationale and
revaluation history (stay with EXT-10, TICK-083, later); Cazana and Experian
integrations (seams); AutoTrader scraping inside Pegasus; autonomous sending;
task UI; a staff "review instructions/images" action (D44); a damage type
(D45); creating a formal Case from an image-initiated record (D50); the
vehicle-record extension beyond make, model and mileage (D49, CASE-043); a
persisted repairer address (D48, INTK-058); the reverse "Add evidence" route
(D50, CASE-044).

## Shared decisions (D29–D43 confirmed 2026-09-02; D44–D50 confirmed 2026-09-03)

| # | Decision |
| --- | --- |
| D29 | The Case record is one scrolling page: sticky identity ribbon, action bar and section jump-nav with scroll-spy; sections below the fold render lazily; `?section=` jumps. The design README's "sections as tabs" rule is superseded for the Case record. No layout switch ships. |
| D30 | The Engineer workbench lives on the Case page as sections Damage, Valuation, Estimate, Settlement, Report. `/Cases/{id}/Assessment` becomes a 301. Every section is always viewable; Engineer sections are read-only once Complete (D11 becomes a read-only rule). Section order: Overview, Engineer notes, Inspection, Vehicle, Damage, Valuation, Estimate, Settlement, Report, Files, Notes. |
| D31 | Sign-off Engineer is a Case field beside Engineer. A staff account carries a "Sign-off Engineer" flag with qualifications, a signature image and the printed signatory name; only flagged accounts with a signature on file appear in the list (qualifications optional). Default: the assigned Engineer when flagged, otherwise the one account carrying the Administrator-set "Default sign-off Engineer" designation (Andy). Reports render the sign-off tuple. Supersedes D18. Andy, Neil and Ed sign; Neil's qualifications are recorded later by an Administrator; an Administrator uploads each signature (no migration seed). |
| D32 | Engineer notes: append-only, attributed staff notes to the Engineer, a separate section from the case Notes history; nothing about them appears in the Notes history. |
| D33 | Inspect at is a fast-update choice: Image Based Assessment, Claimant address, Repairer location, Storage location, previous addresses used for this principal, Manual entry; options without a value are disabled. A Case records a storage location. |
| D34 | One "Look up DVLA & MOT" action; looked-up values appear as per-field chips that fill the field when chosen; no checks panel and no suggestion table. Experian stays a disabled seam. |
| D35 | AI market research is an AI job kind (`MarketResearch`) created from the Valuation section. The research runs outside Pegasus: the operator's Claude Cowork connector polls the job ledger through the Automation Actor, searches AutoTrader, and completes the job with a findings document plus retail and trade figures. Pegasus retains the document as Case evidence and records a valuation row of source "AI market research"; proposal only — the job lands in the existing review state and is completed by the existing staff confirmation. No scraping or AutoTrader integration inside Pegasus. |
| D36 | Send to EVA is offered in Review and With Engineer (re-send); the dialog holds Engineer, Sign-off Engineer and Download ZIP / Send via API (API disabled unless the principal enables it). "Download EVA package" is retired. |
| D37 | Service health is Administration-only; Operations shows a one-line partial-data notice (Partial or Failed only) linking to it. |
| D38 | Awaiting instruction (image-initiated cases) is a Pre-case queue on Cases beside Triage, under the shipped `Pre-Case work` group label. |
| D39 | Damage is a list of zones (front, left/right front, left/right side, left/right rear, rear, roof, four wheels, underside, interior, mechanical) each with severity and note (no type — D45), plus tyres and seat belts per corner, spare tyre, centre belt, unrelated damage with deduction, and paint or material transfer; `impact_location` and `impact_severity` are derived (severity = the highest zone severity; Core's existing codes are canonical). The report prints the marked diagram. |
| D40 | Valuation sources in this programme: Glass's (valuation), Cazana (seam), Engineer's Value, AI market research; date, time and mileage per entry, with guide month added by CASE-029. Glass's valuation and Glass's repair estimating are two systems and both are used; the valuation source and the estimate import source keep separate label entries. EXT-10 stays later. |
| D41 | Settlement fields: outcome, category, salvage value, excess, betterment, claimant VAT registered, reserve, equity (derived: Engineer's value − (repair cost − betterment) − salvage; excess is not part of it), repair duration and delays, report delay, storage per day, recovery, hire start and daily cost, diminution, salvage logistics. Financial ratio lines are permitted, not required; the "no percentage" rule is about completeness only. |
| D42 | A fee note preview renders on the Report section from the agreed fee and description lines; sending stays MAIL-17. |
| D43 | Test fixtures and snapshot states may use the mockup's corpus-derived values (`Pegasus_UI_v2_src/src/04-fixtures.js`) as they are, including real claimant names and phone numbers (operator sign-off 2026-09-03); the `corpus/` folder stays local, ignored and immutable, and no corpus file is committed. |
| D44 | "Review" is a stage, not an action; pressing Send to EVA is the implicit review. There is no staff act of reviewing instructions or images: no review flag, checkbox, dialog or history line. Not ready → Review is decided by completeness only. PLAT-070 removes the existing `RequireStaffImageReviewBeforeEngineerAssignment` / `ImagesReviewedByStaff` function and the Workflow configuration review panel. |
| D45 | A damage zone records zone, severity and note only; there is no damage type field, label list or report column (every case is collision work). |
| D46 | The crop tool behaves like any photo-editing cropper (drag the frame, resize by handles, rotate, aspect lock, reset, live preview) and is reachable from the Files section's image viewer and from the Report section's image cards without first pressing Edit Case; saving a crop starts the edit lease. One curation record per image. |
| D47 | Send to EVA moves the case state. The first send from `Review`, by either route (Download ZIP or Send via API), performs the existing `StartCaseWork` transition to `With Engineer` atomically with the handoff record; a failure of either half leaves the case in `Review` with no partial handoff. A re-send from `With Engineer` changes no state. FRD-07's two statements that neither route changes the Case state or version are wrong and are corrected by CASE-040's PR. Operator, 2026-09-03. |
| D48 | The repairer location is extracted from the instruction material by the existing extraction process, not entered by hand. Until INTK-058 delivers it, CASE-041 offers Repairer location disabled with its condition under D33; no repairer address is persisted by this programme. Operator, 2026-09-03. |
| D49 | The case vehicle record is extended beyond registration, make, model and mileage by a separate ticket, CASE-043, not by CASE-029. Population order for those fields is extraction from the supplied instruction or data first, then an automatic DVLA/DVSA lookup on intake for what extraction did not fill. CASE-029 ships suggestion chips for make, model and mileage only. Operator, 2026-09-03. |
| D50 | Create Case is dropped from the Awaiting instruction quick view: image-only material joins an instructed Case, it does not create one (FRD-02, `IntakeDecisionPolicy.CanBecomeCase`). The reverse route — an instructed case adding evidence by upload or by absorbing an image-initiated case, reachable from the case and the main rail — is CASE-044 and is outside this epic. Operator, 2026-09-03. |

## Constraints

No explanatory copy; labels only in `Presentation/OperatorLabels.cs`; a ticket
owns whole files; shared-lock paths (`Pages/Shared/*`, `Pages/Cases/Shared/*`,
`Pages/Administration/Shared/*`, `wwwroot/css/site.css`, `wwwroot/js/site.js`,
`Presentation/OperatorLabels.cs`, `docs/design/test-ui/**`,
`Persistence/Migrations/**`, the governing docs) have capacity one; migrations
serialized; lanes refresh with `git merge --no-edit origin/dev`; three lanes,
no token budget; one edit mode over one lease covers every section.

## Model allocation (operator, 2026-09-03)

No Fable agent runs inside a workflow. Codex does 75 % or more of the work;
Claude is a thin wrapper around it. Research and plans: gpt-5.6-terra (medium /
high / xhigh) under Sonnet wrappers. Plan review: gpt-5.6-sol xhigh reads,
Opus dispositions. Implementation: gpt-5.6-sol (low for fixes and chores,
medium for features, high for ENG-034) on fifteen lanes; Opus high on
CASE-038, ENG-036 and ENG-031; Sonnet wrappers own the board writes, the
worktree, the packet file and re-run the build and tests themselves.
Simplification pass: sol low. PR review: terra xhigh reads Codex-built PRs,
sol xhigh reads Claude-built PRs; Opus dispositions, watches CI and merges to
`dev`. Critic: terra xhigh, three lenses. Verify: Sonnet runs the commands,
terra high audits the proof. Adversarial claims: sol xhigh, terra xhigh, Opus.

## Risks

Lazy sections versus unsaved edits and the lease; report template growth;
D18 supersession; Codex lanes cannot reach the board (wrapper pattern);
personal data in the repository under D43 (accepted by the operator);
Kanmer MCP degraded mid-run (writes land, reads return nothing — wrappers
read the board worktree files read-only); `origin/main` carried two direct
pushes on 2026-09-03 that `origin/dev` does not have (test material and a
skills merge), so `dev` is behind `main` by two commits — reconciling that is
an operator/administrator action, not a lane's.

## Dependency map

Docs chore → all; PLAT-070 → frame; frame → sections, Engineer notes UI,
sign-off case field, inspect-at UI, CASE-029; vocabulary → sections move,
damage map, ENG-029; sections move → damage map, ENG-031, ENG-029, fee note;
DOCS-017 → sign-off case field and account setting; PLAT-068 → CASE-040;
CASE-032 → Awaiting instruction queue; AUTO-011 and ENG-027 → market
research; TICK-082 → Estimate card select; CASE-029 → CASE-043 (vehicle
record extension, D49). ENG-034 runs serial in wave 3 after CASE-038 and
CASE-039 because it takes the `Details.cshtml.cs` lease to move the
Assessment handler surface. Exact ids: see each ticket's `blocks` and
`blockedBy`.

## Rollout & rollback

Waves 0–5 merge to `dev`; one production release after all PRs; `dev` → `main`
needs `MERGE AUTH GRANTED`; migrations additive; a failed wave is reverted
PR-by-PR on `dev`.

## Definition of done

Every member Done with proof at its merge SHA; UIIMP-010 walk passed; DELIV-030
docs refreshed; the adversarial claims in the run record all survived.
