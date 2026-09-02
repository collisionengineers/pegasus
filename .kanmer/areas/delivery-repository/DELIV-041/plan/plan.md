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
