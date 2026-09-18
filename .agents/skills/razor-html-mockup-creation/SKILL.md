---
name: razor-html-mockup-creation
description: Run a Stage 1 design round for Pegasus. Build the self-contained offline HTML mockup of a Razor page or of the whole shell, its scripted self-check, screenshots, notes, discussion log, page planning folders and the lettered operator sign-off list, then stop for approval. Use when a mockup, HTML mockup, vNN planning round, or design-before-implementation is asked for; not for the Razor implementation, which razor-html-mockup-conversion owns.
---

# Razor HTML mockup creation

Design is settled on a mockup the operator can open in a browser, not on prose.
This skill owns the round from the first inventory to the approval that lets
Stage 2 begin. The v26 round is the worked example:
[notes](../../../design/planning-and-old-designs/v26_planning/current/v26-notes.md),
[discussion log](../../../design/planning-and-old-designs/v26_planning/current/discussion-log.md),
[page folders](../../../design/planning-and-old-designs/v26_planning/pages/README.md).
Reuse its shape; do not copy its decisions.

## Two gates

1. **Stage 1** delivers the mockup, its evidence, the notes and a lettered
   sign-off list, then stops. The operator's instruction was "pause, show
   mockup, if approved, move on".
2. **Stage 2** (the Razor implementation) is planned during Stage 1 but not
   started until every lettered item is settled and the operator has approved.
   Hand over to [razor-html-mockup-conversion](../razor-html-mockup-conversion/SKILL.md).

Judgment on flow, hierarchy and states comes from
[razor-pages-ui-design](../razor-pages-ui-design/SKILL.md); this skill supplies
the procedure.

## Inputs before drawing

- [Design authority](../../../docs/design/README.md) binds; [FRD-12](../../../docs/frd/frd-12-operator-experience.md),
  [FRD-15](../../../docs/frd/frd-15-work-centre-queues-and-search.md),
  [FRD-16](../../../docs/frd/frd-16-case-record-workspace.md),
  [FRD-17](../../../docs/frd/frd-17-administration-workspace.md)
  and the owning FRDs settle behaviour; [CONTEXT.md](../../../CONTEXT.md) owns
  reserved terms.
- The live Razor sources (`src/Pegasus.Web/Pages/**/*.cshtml`, the
  `Shared/_Case*.cshtml` partials) and `src/Pegasus.Web/Presentation/OperatorLabels.cs`
  read from `origin/dev`, not the checked-out branch. In v25 the checkout was
  16 commits behind and carried defects dev had already fixed.
- The operator's reference file when one is supplied (v24's dashboard, v27's
  `pegasus_case_dashboard_2026-09-15.html`). Private pack material under
  `pegasus_pack/` is published only when the operator asks for that file by
  name; `corpus/` is never read into a mockup.
- The previous mockup version, kept alongside for comparison.

## Widen beyond what is named

Before proposing, audit the live UX mechanics, not only the features the
request names: where each handler redirects (`RedirectToDetails` without a
section teleports to the top), whether read and edit render as two different
pages, what is silently disabled and why, which facts repeat across ribbon,
aside and body, and how tall the sticky block really is. The v25 round was
corrected for "focusing too much on what's literally named"; the usability walk
is "imagine you're a person trying to use it: what's good, what's bad, is it
easy, what's awkward".

## Build the mockup

Follow [references/mockup-build.md](references/mockup-build.md): one offline
file per surface built from the live shell, a mockup strip and query-string
presets for every state, strip variables for undecided choices, the self-check
harness and the screenshot set. Versioned file names; the operator's reference
and the previous version stay in the same folder.

## Coverage audit

Enumerate every control, column, chip, empty state, onward link and displayed
fact on the live surface and diff it against the mockup's inventory. Restore
substantive drops (v25 lost Glass's, Import, Send to AI, the estimate header
fields and ~90 more before the audit). Record deliberate departures from live in
the notes with the reason, and vocabulary corrections where a live label breaks
CONTEXT.md ("Send to AI", never the vendor name).

## Evidence

- The self-check prints `RESULT {"fail":[],"okCount":N}` with no console error
  on any rendered route or state. Record the dated result and count in the notes.
- Screenshots at 1580×1000, 1440×900 and 760×1000 for every state the notes
  cite, numbered and listed on the page READMEs.
- Neither is application evidence. Say so in the folder README.

## Documentation set and page folders

Follow [references/planning-folder.md](references/planning-folder.md): the
`current/` README, `vNN-notes.md`, `discussion-log.md`, and one `pages/`
folder per page with `dialogs/`, `states/`, `panels/`, `how-it-works.md` and
`how-it-should-work.md`. Write `how-it-works.md` from the live source with the
read date before proposing changes to that page.

## Sign-off protocol

- Every decision that changes an FRD, an audit event, a vocabulary term, or
  moves a control the operator has previously placed is a lettered item
  (A, B, C, C2 …) phrased "Confirm, or …". Never take it unilaterally.
- Feedback rounds are logged in the discussion log with the operator's words,
  the changes made and the new items raised.
- A rejected item stays in the list, marked with the date and the decision.
- Before asking for Stage 2 approval, read the whole list out. Record the
  approval and its settlements in the notes and in each affected
  `how-it-should-work.md` under a dated "Decided" heading.

## Delegation

Lanes may build pages in parallel only when each writes its own lane files and
one integrator splices them, extends the self-check with each lane's checks and
reruns it. Assignment, file overlap and integration follow the repository
instructions; do not restate them here.

## Placement and branch

- `design/planning-and-old-designs/vNN_planning/` with `README.md`, `current/`
  and `pages/`, on `task/<slug>` from `origin/dev`.
- The folder is a temporary review artifact under the
  [index](../../../docs/index.md) carve-out: mark it temporary, and remove or
  retain it by operator instruction in the final Stage 2 PR.
- New Markdown must pass `scripts/Test-MarkdownPlacement.ps1` (the path is on
  its allow-list) and `scripts/Test-DocumentationLinks.ps1`.
