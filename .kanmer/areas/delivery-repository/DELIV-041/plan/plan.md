# Plan — DELIV-041 record D29–D43 in the governing documents

Docs-only chore. Wording source: EPIC-012 `context.md` §Shared decisions (the
operator's confirmed 2026-09-02 decisions). One PR to `dev`, branch
`task/deliv-041-case-workspace-decisions`, worktree `.worktrees/deliv-041`.

## Steps (each names what it reuses)

1. **EPIC-011 `context.md`** (board doc, via `set_group_doc`): append rows
   D29–D43 to the §2 decisions table and a dated note that D18 is superseded by
   D31 and that §1.8/§1.9 are superseded by EPIC-012 for the Case record.
2. **`docs/frd/frd-12-operator-experience.md`**: Case workspace section becomes
   the eleven-section single-scroll record (D29, D30, D32, D33, D34, D36, D37,
   D38); the `/Cases/{id}/Assessment` route is recorded as a 301; Cases queue
   gains the Pre-case "Awaiting instruction" tab; Operations loses the service
   health table. Reuse the existing section headings; edit in place.
3. **`docs/frd/frd-01-case-identity-and-lifecycle.md`**: Sign-off Engineer as a
   Case field with the default rule (D31); Engineer notes append-only (D32);
   storage location and inspect-at choices (D33); Send to EVA offered in Review
   and With Engineer with re-send (D36).
4. **`docs/frd/frd-04-parties-accounts-and-access.md`**: staff account
   "Sign-off Engineer" setting (flag, qualifications, signature image),
   Administrator-only, recorded in Action Logs (D31).
5. **`docs/frd/frd-06-vehicle-and-engineering-evidence.md`**: damage list and
   derived impact location/severity, tyres and belts, unrelated damage,
   material transfer (D39); one DVLA & MOT lookup with per-field suggestions
   (D34); valuation sources incl. AI market research and the two Glass's systems
   (D40); settlement fields and permitted ratio lines (D41).
6. **`docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`**:
   reports render the sign-off tuple (D31 supersedes D18); MarketResearch AI job
   kind completed by an external connector through the Automation Actor (D35);
   fee note preview (D42); the marked damage diagram on the report (D39).
7. **`docs/design/README.md`**: scope "sections as tabs" to non-Case records;
   add the Case record scroll rule (sticky ribbon/action bar/section nav,
   lazy sections); add component classes `case-sticky`, `section-nav`,
   `section-link`, `suggest-btn`, `damage-diagram`, `impact`, `tyre-card`,
   `valuation-card`, `outcome-option`, `derived`, `report-image`, `cropper`;
   keep every other rule.
8. **`docs/capabilities.md`**: rows for the new capabilities with their canonical
   owner (FRD section) and the EPIC-012 ticket ids; EXT-10 unchanged (later).
9. **`docs/boundaries.md`**: AutoTrader scraping inside Pegasus excluded (the
   external connector does the research); no layout switch; EXT-10 adjustments
   deferred.
10. **`docs/open-decisions.md`**: close the tabs-vs-scroll and signatory
    questions with the D29/D31 references.
11. **`docs/engineering.md`**: D43 fixture rule — the mockup's corpus-derived
    fixture values may be used in tests and snapshots; `corpus/` itself stays
    local, ignored and immutable; state plainly that the values include real
    claimant names and phone numbers.
12. Markdown convention check (H1 line 1, blank line before headings, compact
    table delimiters, ~78-column wrap); docs-only CI.

## Acceptance

- Every decision id D29–D43 is findable by grep in EPIC-011 context.md and in
  at least one governing doc.
- `docs/operator-notes.md` untouched.
- PR body carries `Kanmer: DELIV-041`; docs-only CI green.

## Stop condition

Stop after the PR is open and the ticket is in Review; an independent
reviewer of the other model family (gpt-5.6-sol, xhigh) reviews.

## Simplification pass

n/a — docs-only.

## Review (2026-09-02, gpt-5.6-sol xhigh; dispositions by the controller)

PR #647 review returned REQUEST CHANGES. Dispositions applied in commit
`2944cbf14851c1a79f5ad53df28c059bb3666aee` on the same branch and worktree;
`Test-DocumentationLinks.ps1` exit 0, `Test-MarkdownPlacement.ps1 -Base
origin/dev -Head HEAD` exit 0. FRD-07 was added to the ticket refs by the
controller (`link_doc`) as a reviewed scope amendment.

| # | Finding | Disposition |
| --- | --- | --- |
| 1 | Blocker, D43: `docs/design/README.md` § Imagery still said prototype fixture data "is never copied"; `docs/engineering.md` § D43 invented an operator sign-off gate. | **Fixed.** README sentence scoped: "…never copied, except the Case Workspace v2 fixture set permitted by D43 (engineering.md)". engineering.md paragraph replaced with the reviewer's wording: values may be used, include real claimant names and telephone numbers, D43 supersedes the EPIC-011 rule for this fixture set only, `corpus/` unchanged. No sign-off gate. |
| 2 | Blocker, D36: FRD-07 still said export only in `Review` and a submitted case is never resubmitted by either route. | **Fixed (reviewed scope amendment).** FRD-07: Send to EVA offered in `Review` and again in `With Engineer` as a re-send; dialog holds Engineer, Sign-off Engineer, Download ZIP and, when the Principal enables it, Send via API; "at most once" narrowed to automatic submission; the never-resubmitted statement superseded by the re-send rule (a re-send over the API is a new, separately recorded submission that creates a second EVA claim as the operator's deliberate act); gate sentence and Manual API submission bullet updated. Every other FRD-07 rule kept; D36 cited. |
| 3 | Blocker: design README duplicate summaries (icons table `download`, Access table, Administration Service health bullet, UI specification § Cases/Case/Operations, Contracts § Identity ribbon) still named Download EVA package, placed Service health on Operations, omitted Awaiting instruction, said Assessment opens from With Engineer, described a 7-item Assessment ribbon. | **Fixed.** Each updated to D29/D30/D36/D37/D38 wording: Download ZIP in the Send to EVA dialog; Awaiting instruction beside Triage; single-scroll Case record with the permanent Assessment 301 and read-only-once-Complete Engineer sections; Administration-only Service health; ribbon with Engineer and Sign-off Engineer, no separate Assessment ribbon. |
| 4 | Should-fix, D31: initial Sign-off Engineer accounts not named; FRD-11 fail-closed incomplete-tuple rule contradicted "Neil's qualifications recorded later". | **Fixed.** FRD-04 § Staff accounts: A Patterson, N O'Reilly, E Mawdsley (Andy, Neil, Ed); Andy default; Neil's qualifications recorded later and until then his reports print the name without a qualification line. FRD-11 § Initial renderer activation cites it and states the tuple is complete with name and signature image, the qualification line optional (D31); fail-closed list narrowed to missing name/signature and unknown/mismatched/substituted values. |
| 5 | Should-fix, D35: MarketResearch row said input "Case reference only". | **Fixed.** Input column now states the connector polls the job ledger through the Automation Actor, searches AutoTrader and completes the job with a findings document plus retail and trade figures; result column carries the retained document and the `AI market research` valuation entry. |
| 6 | Should-fix, D40: Date, Time and Mileage valuation fields were dropped in favour of guide month. | **Fixed.** FRD-12, FRD-06, design README (`valuation-card` row and Valuation bullet), capabilities `EXT-10` note and boundaries row keep date, time, mileage, retail and trade per entry; guide month is an additional per-entry field owned by `CASE-029` (EPIC-012 context), not by D40. |
| 7 | Should-fix, D41: ratio lines written as required, with prescribed operands and mandatory Core computation. | **Fixed.** FRD-06 § Settlement: equity derived; ratio lines permitted, not required; "no percentage" applies only to completeness. Design README `derived` row and Settlement bullet, and capabilities `ENG-04`, aligned; operands removed. |
| 8 | D39 "clickable" diagram wording unauthorised. | **Rejected.** The operator asked for a clickable vehicle SVG (2026-09-02) and ENG-036's title records it; wording kept. |
| 9 | Nit: reflow frd-01 ~39, frd-12 ~158, open-decisions ~378 to ~78 columns; `|---|` delimiter rows in open-decisions at ~331, ~358, ~381. | **Fixed.** Three paragraphs reflowed; the three delimiter rows changed to `| --- |` form (now lines 331, 358, 388 after the reflow). Other pre-existing `|---|` rows in that file were not touched (outside the reviewed diff). |
| 10 | Implementer's own flags. | **None beyond 2 and 4**, both fixed above. The Kanmer 0.3.3 `get_execution_packet` absence and the kebab-case route keys remain as recorded in the post-implementation report. |
