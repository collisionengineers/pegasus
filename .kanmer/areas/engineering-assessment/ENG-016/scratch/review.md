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

## Independent re-review — PR #539 at c86b803c — NEEDS CHANGES

### Changes
Reviewed the complete 73-file diff against origin/dev, the amended blocker commit, governing FRD/ADR changes, migrations, Export/readiness implementation, tests, PR description, ticket plans/checklists/reports, and live GitHub checks.

### Comments and dispositions
- **Blocking — filed as [[PR-061]]:** `ExecuteAsync` checks `caseData.State == Review` before package construction, but `RecordExportAsync` later locks `CaseWorkflows` and never re-reads the locked state. A concurrent edit can commit a demotion to `Not ready` before Export obtains the lock, after which Export still writes history/proxy and returns the stale package. This conflicts with FRD-07's “available only while ... Review” rule.
- **Blocking — remains owned by [[PR-059]]:** the evidence reconciliation is not complete. All PR-055–PR-060 checklist acceptance items remain unticked, ENG-016's report does not contain the plan-required complete changed-file/rationale inventory or governing-doc compliance section, and final CI is not yet recorded.
- **Non-blocking / fixed in PR:** atomic same-key replay, unconditional completeness, ADR-0031 supersession, batch image reads, and migration roll-forward commentary are present on the amended head.
- **Non-blocking / pending external evidence:** live CI currently has unit/infrastructure/docs/support checks green, with browser and three SQL shards still running.

### Plan/report/simplification
The product scope is appropriately simple and no speculative compatibility machinery was added. Implementation missed the implied atomic Review-state gate above. PR-059 missed its evidence/checklist plan. The simplification note is directionally honest for code, but the identical generic note copied to every blocker does not substitute for completing each ticket checklist and exact evidence.

### Verdict
**NEEDS CHANGES.** Do not merge until PR-061 is fixed, PR-059 evidence/checklists are reconciled, final CI is green, and a fresh independent review passes.
