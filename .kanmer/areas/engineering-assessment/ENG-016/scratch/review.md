## Independent review — 2026-08-25

Reviewer was independent from implementation.

### Changes checked

Reviewed PR #539 at `cf28b8b0` against ENG-016 plan, files, post-implementation report, FRD-07, ADR-0021, capabilities, current architecture, migration, callers and all green CI checks.

### Comments and disposition

1. **Blocking — export replay is not concurrency-safe.** The history pre-check and insert are not protected by a unique database boundary; concurrent same-key exports can create duplicate ActionHistory rows. Disposition: filed [[PR-055]], blocks ENG-016.
2. **Blocking — incomplete Cases can still enter Review.** Administrator completeness switches can waive instruction/image completeness before `EfCaseDataStore` promotes the Case. Disposition: filed [[PR-056]], blocks ENG-016.
3. **Blocking — deletion conflicts with accepted ADR-0021.** The accepted decision and MCP-06 still require/advertise the removed EVA automation tools. Disposition: filed [[PR-057]], blocks ENG-016.
4. **Blocking — serial image reads regress the established Box batch path.** The implementation uses per-image `OpenReadVersionAsync` rather than `ReadVersionsAsync`. Disposition: filed [[PR-058]], blocks ENG-016.
5. **Blocking — ticket evidence is internally inconsistent.** Governing refs are absent; files.md contradicts the final plan; the report is not an exact diff inventory and omits review conflicts. Disposition: filed [[PR-059]], blocks ENG-016.
6. **Blocking — migration commentary is inaccurate.** It denies the operation key and describes rollback cleanup contrary to the roll-forward-only decision. Disposition: filed [[PR-060]], blocks ENG-016.
7. **Resolved — Content-Digest.** Present in the current head; no action.
8. **Superseded — earlier missing-field warning.** Superseded by the operator's clarified Review/export rules; no action.

### Evidence

All 11 GitHub checks are green and the PR is mergeable. Green CI does not exercise the same-key concurrency race or the configuration waiver path.

### Verdict

**NEEDS CHANGES.** Do not merge, release, verify or close out ENG-016 until PR-055 through PR-060 are resolved and the PR passes a fresh independent review.
