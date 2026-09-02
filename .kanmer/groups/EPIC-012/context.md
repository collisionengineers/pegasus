# EPIC-012 context — Case Workspace v2 (single-scroll Engineer workbench)

Inherits EPIC-011 `context.md` and `waves.md` in full. Source of record:
`Downloads/Pegasus_UI_v2.html` + `Downloads/Pegasus_UI_v2_src/` (2 September
2026, second pass) and `Downloads/Pegasus_UI_v2_notes.md`. Execution plan:
`Downloads/Pegasus_UI_v2_implementation_plan.md`. Decisions D29–D43 (recorded in
the governing documents by the Phase 0 docs chore) govern where they differ
from EPIC-011 D1–D28.

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
  offered; default is the assigned Engineer when flagged, otherwise A Patterson;
  reports render the sign-off tuple.
- Damage map, settlement figures and the fee note appear on the report preview
  from the accepted record.
- Cropping never changes source bytes; issued reports keep their curation
  snapshot.
- Awaiting instruction is a Pre-case queue; Operations carries no service
  health table.

## Non-goals

A Scroll/Tabs switch; savings or comparison figures (D17); CAP HPI, AutoTrader
as a manual source, Vehicle data, valuation adjustments, rationale and
revaluation history (stay with EXT-10, TICK-083, later); Cazana and Experian
integrations (seams); AutoTrader scraping inside Pegasus; autonomous sending;
task UI.

## Shared decisions (D29–D43, confirmed by the operator 2026-09-02)

| # | Decision |
| --- | --- |
| D29 | The Case record is one scrolling page: sticky identity ribbon, action bar and section jump-nav with scroll-spy; sections below the fold render lazily; `?section=` jumps. The design README's "sections as tabs" rule is superseded for the Case record. No layout switch ships. |
| D30 | The Engineer workbench lives on the Case page as sections Damage, Valuation, Estimate, Settlement, Report. `/Cases/{id}/Assessment` becomes a 301. Every section is always viewable; Engineer sections are read-only once Complete (D11 becomes a read-only rule). Section order: Overview, Engineer notes, Inspection, Vehicle, Damage, Valuation, Estimate, Settlement, Report, Files, Notes. |
| D31 | Sign-off Engineer is a Case field beside Engineer. A staff account carries a "Sign-off Engineer" flag with qualifications and a signature image; only flagged accounts appear in the list. Default: the assigned Engineer when flagged, otherwise A Patterson. Reports render the sign-off tuple. Supersedes D18. Andy, Neil and Ed sign; Andy is the default; Neil's qualifications are recorded later by an Administrator. |
| D32 | Engineer notes: append-only, attributed staff notes to the Engineer, a separate section from the case Notes history. |
| D33 | Inspect at is a fast-update choice: Image Based Assessment, Claimant address, Repairer location, Storage location, previous addresses used for this principal, Manual entry; options without a value are disabled. A Case records a storage location. |
| D34 | One "Look up DVLA & MOT" action; looked-up values appear as per-field chips that fill the field when chosen; no checks panel and no suggestion table. Experian stays a disabled seam. |
| D35 | AI market research is an AI job kind (`MarketResearch`) created from the Valuation section. The research runs outside Pegasus: the operator's Claude Cowork connector claims the job through the Automation Actor tools, searches AutoTrader, and completes it with a findings document plus retail and trade figures. Pegasus retains the document as Case evidence and records a valuation row of source "AI market research"; proposal only. No scraping or AutoTrader integration inside Pegasus. |
| D36 | Send to EVA is offered in Review and With Engineer (re-send); the dialog holds Engineer, Sign-off Engineer and Download ZIP / Send via API (API disabled unless the principal enables it). "Download EVA package" is retired. |
| D37 | Service health is Administration-only; Operations shows a one-line partial-data notice linking to it. |
| D38 | Awaiting instruction (image-initiated cases) is a Pre-case queue on Cases beside Triage. |
| D39 | Damage is a list of zones (front, left/right front, left/right side, left/right rear, rear, roof, four wheels, underside, interior, mechanical) each with severity, type and note, plus tyres and seat belts per corner, spare tyre, centre belt, unrelated damage with deduction, and paint or material transfer; `impact_location` and `impact_severity` are derived. The report prints the marked diagram. |
| D40 | Valuation sources in this programme: Glass's (valuation), Cazana (seam), Engineer's Value, AI market research; guide month and mileage per entry. Glass's valuation and Glass's repair estimating are two systems and both are used; the valuation source and the estimate import source keep separate label entries. EXT-10 stays later. |
| D41 | Settlement fields: outcome, category, salvage value, excess, betterment, claimant VAT registered, reserve, equity (derived), repair duration and delays, report delay, storage per day, recovery, hire start and daily cost, diminution, salvage logistics. Financial ratio lines are permitted; the "no percentage" rule is about completeness only. |
| D42 | A fee note preview renders on the Report section from the agreed fee and description lines; sending stays MAIL-17. |
| D43 | Test fixtures and snapshot states may use the mockup's corpus-derived values (`Pegasus_UI_v2_src/src/04-fixtures.js`); the `corpus/` folder stays local, ignored and immutable, and no corpus file is committed. Those values include real claimant names and phone numbers; the docs chore states this plainly for operator sign-off before values are copied. |

## Constraints

No explanatory copy; labels only in `Presentation/OperatorLabels.cs`; a ticket
owns whole files; shared-lock paths (`Pages/Shared/*`, `Pages/Cases/Shared/*`,
`Pages/Administration/Shared/*`, `wwwroot/css/site.css`, `wwwroot/js/site.js`,
`Presentation/OperatorLabels.cs`, `docs/design/test-ui/**`,
`Persistence/Migrations/**`, the governing docs) have capacity one; migrations
serialized; lanes refresh with `git merge --no-edit origin/dev`; three lanes,
no token budget; one edit mode over one lease covers every section.

## Model allocation

Research and planning: gpt-5.6-terra (medium / high / xhigh by difficulty).
Implementation: gpt-5.6-sol (low for fixes and chores, medium for features) on
roughly half the lanes including hard ones; Claude on the frame, the vocabulary,
the damage map, the crop tool and the sign-off case field. Review: the other
family (sol xhigh reviews Claude-built PRs; Claude reviews Codex-built PRs).
Codex runs inside a Claude wrapper that owns the board writes and re-runs the
build and tests itself.

## Risks

Lazy sections versus unsaved edits and the lease; report template growth;
D18 supersession; Codex lanes cannot reach the board (wrapper pattern);
personal data entering the repository under D43.

## Dependency map

Docs chore → all; frame → sections, Engineer notes UI, sign-off case field,
inspect-at UI, CASE-029; vocabulary → sections move, damage map, ENG-029;
sections move → damage map, ENG-031, ENG-029, fee note; DOCS-017 → sign-off
case field and account setting; CASE-032 → Awaiting instruction queue;
AUTO-011 and ENG-027 → market research; TICK-082 → Estimate card select.
Exact ids: see each ticket's `blocks` and `blockedBy` via `get_links`.

## Rollout & rollback

Waves 0–5 merge to `dev`; one production release after all PRs; `dev` → `main`
needs `MERGE AUTH GRANTED`; migrations additive; a failed wave is reverted
PR-by-PR on `dev`.

## Definition of done

Every member Done with proof on merged main; UIIMP-010 walk passed; DELIV-030
docs refreshed; the adversarial claims in the run record all survived.
