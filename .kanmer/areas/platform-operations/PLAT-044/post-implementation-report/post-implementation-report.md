# Post-implementation report — PLAT-044

## Outcome

Implemented and committed as `8a9508f6` on `task/plat-044-assessment-open-latency`.

Opening Assessment now loads one page-specific relational workspace in exactly six reader commands and does not invoke document-content storage. Report readiness uses only post-Review assessment/report work; requirements already established by entry to Review are not recalculated. Actual report generation loads confirmed photographs through one ordered batch.

Managed Box content now uses the persisted case-root remote ID directly. A single read costs three Box requests instead of four; a five-image batch costs seven instead of eight and never lists the approved root. The Box store fails before remote I/O when the root ID is absent, while the supported local content store remains independent of Box identity.

## Changed authority

- Recorded the operator's 2026-08-25 report-readiness statement in `docs/operator-notes.md`.
- Updated FRD-11 so Review-entry requirements are invariants consumed at generation, not duplicate report-readiness items.
- Kept the original assessment readiness policy intact for its existing consumers; the report path uses the separately named post-Review evaluation.

## Verification

- Restore succeeded.
- Release build succeeded with 0 warnings and 0 errors.
- Core: 974 passed, 0 failed.
- Architecture: 99 passed, 0 failed.
- Integration: 949 passed, 16 corpus-dependent skips, 0 failed.
- Focused evidence:
  - `AssessmentWorkspaceLoadsInExactlySixReaderCommands`
  - `ReportProjectionReadsAllPhotographsThroughOneOrderedBatch`
  - `ReviewTransitionRequirementsAreNotRecalculatedByReportReadiness`
  - `ManagedReadUsesPersistedCaseRootWithoutListingApprovedRoot`
  - `OneManagedReadCostsThreeBoxRoundTrips`
  - `BoxContentFailsClosedWithoutThePersistedCaseRoot`
  - Assessment report web tests use a throwing content store and still open successfully.

## Simplification and diagnostic scan

The dated simplification dispositions are recorded in the plan. The requested .NET performance scan found no critical defects in the changed production files; its one actionable hot-path allocation was removed.

## Deployment

Not deployed. No Azure or other external state was changed.

## Review correction outcome — 2026-08-25

Assessment access now requires Review or Report preparation plus a successful EVA export from the current Review cycle. Re-entering Review invalidates the previous export until a new one succeeds. Later workflow changes do not invalidate it. Engineer assignment remains optional and does not affect access, readiness, or export.

The Case-page control, Assessment workspace and mutating page handlers, and report generation all enforce the rule. NotReady automation/MCP assessment writes remain available outside the operator Assessment page.

Verification after the correction:

- Release solution build: 0 warnings, 0 errors.
- Core: 981 passed.
- Architecture: 99 passed.
- Full integration baseline after fixture corrections: 954 passed, 16 expected skips, 0 failed.
- Final focused integration against the latest query shape: 24 passed, 0 failed.
- EF migration model check: no pending model changes.
- `git diff --check`: clean.
