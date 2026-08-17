# Review — 2026-08-17

**Reviewer independence:** self-review by the implementing agent. This is not the independent review required by AGENTS.md or the BUG-001 plan.

## Changes

- `IntakeContracts.cs`: adds a provider-neutral established-principal context and changes every extraction-policy caller contract.
- `ProcessIntake.cs`: derives that context from an accepted route, blocks extraction when none exists, and also evaluates sender-bearing non-mail inputs.
- `QdosInstructionExtractionPolicy.cs`: removes QDOS content markers/duplicate route evaluation and extracts fields once QDOS context is supplied.
- `IntakeAllocation.cs`: makes accepted persisted route identity mandatory for automatic allocation and rejects draft mismatch.
- Seven Core/integration test files: update selected fixtures and add sender-only, mismatch, and recovery coverage.

The post-implementation report describes these categories honestly, but not every changed file individually. It also misses the unchanged standalone desktop caller broken by the interface change and the many unchanged integration fixtures identified by CI. The files document explicitly required those ripple effects to be audited.

## Comments and dispositions

1. **Blocking — CI regression (17 failures).** Browser: 1/32 failed. SQL integration shard 1: 8/165 failed. Shard 2: 8/163 failed. Failures show unchanged manual/multi-format fixtures still assuming content-derived QDOS identity, plus two existing negative tests that now become `CaseCreated`. **Disposition:** needs changes; must be filed as a PR Review blocking ticket, but this board has no PR Review area and the review skill forbids inventing a different area.
2. **Blocking — legacy receipt replay (unanswered GitHub comment).** `ProcessQueuedIntake` re-drives allocation for completed `CaseCreated` receipts. A pre-change receipt may legitimately have no persisted route; `AllocateIntake` now throws outside the worker's processing try/catch, so it neither allocates nor durably enters manual review. Plainly: an old unfinished record can become stuck after deployment. **Disposition:** needs a defined safe recovery outcome and blocking ticket.
3. **Blocking — standalone desktop evaluator no longer builds.** After locked restore, `dotnet build scripts/email-eval-desktop/Pegasus.EmailEvaluation.Desktop.csproj --configuration Release --no-restore` fails at `EmailEvaluationWorkflow.cs:234` because the changed extraction interface was not followed through. **Disposition:** needs changes and a blocking ticket. The operator replied “NO” to the bot suggestion, so no fix is silently applied; nevertheless the concrete repository build regression prevents a passing review.
4. **Non-blocking / won't-do — require a new non-instruction-email predicate.** The operator explicitly stated that non-instruction emails have not yet been defined and are future work. Adding a new predicate here would invent product behaviour and contradict “once QDOS is identified, that is it.” The browser expectation must be reconciled without inventing that future policy.
5. **Non-blocking / won't-do — reject sender-bearing uploaded EML solely because its source channel is ManualUpload.** The operator rejected this as overengineering. Current code uses parsed transport sender identity rather than document/body text. No broader authentication framework is added in this ticket.
6. **Non-blocking / fixed in PR — sender-only QDOS identity.** All three exact suffixes and the proved prior-sender forward path remain owned by the existing route policy; extraction no longer scans content for QDOS identity.
7. **Non-blocking / fixed in PR — route/draft consistency.** New receipts require accepted route identity and reject mismatch before allocation.

## Governing-doc and plan check

- FRD-02/FRD-09 alignment is correct for new QDOS sender identity: route, classification, association, and extraction remain distinct and ambiguity fails closed.
- FRD-05 custody sequencing is not directly changed.
- No ADR or governing-doc edit is required for the intended QDOS correction.
- The implementation does not yet satisfy the plan's explicit requirements to audit all multi-format/manual/web fixtures, preserve a buildable caller surface, run green CI, and obtain independent review.
- No open question is unticked; live deployment remains separately approval-gated.

## Verdict

**Needs changes. Do not merge.** The PR is `UNSTABLE`, with browser and two SQL integration jobs failing; a standalone repository tool does not compile; legacy completed receipts without route persistence have no safe replay outcome; and this is not an independent review. BUG-001 remains in Review.

The required PR Review blocking tickets could not be created because the board has no `PR Review` area/prefix. Board restructuring requires operator direction.

## Operator correction — 2026-08-17

The operator confirmed Pegasus is still pre-release and there are no legacy live receipts requiring compatibility or migration. The earlier “legacy receipt replay” item was based on an inapplicable deployment assumption.

**Revised disposition:** won't-do / not applicable. No migration, legacy recovery path, production-data ticket, or live remediation is required for BUG-001. This point is removed as a review blocker.

The needs-changes verdict remains only for the observed red CI/test-fixture ripple, the standalone desktop evaluator compile regression, and the lack of an independent reviewer.

# CI investigation and revised review — 2026-08-17

**Reviewer independence:** this is a self-review by the implementing agent, not the independent review required before merge.

## CI evidence

GitHub Actions run: https://github.com/collisionengineers/pegasus/actions/runs/32021866902

- `unit`, repository/document/reference checks, SQL shard 3, and integration coverage partition checks passed.
- `browser`: 1 failed / 31 passed.
- `sql-integration (1)`: 8 failed / 157 passed.
- `sql-integration (2)`: 8 failed / 154 passed / 1 skipped.
- Total observed test failures: 17.

## Root-cause classification

### A. Stale content-derived QDOS fixtures — expected fallout, tests need deliberate correction

Bare DOCX/PDF inputs and uploaded EML fixtures from `synthetic@example.test` or `protocol-sender@example.invalid` still expect `CaseCreated` solely because their text says QDOS. Under the settled operator rule, those inputs do not establish QDOS. This explains the multi-format and instruction-draft failures. Tests whose actual subject is parsing, provenance, replay, conflicts, custody, or typed fields must either:
- use a synthetic EML with one of the exact accepted QDOS senders when a definitive QDOS path is required; or
- assert `NeedsSorting`/no QDOS draft when the fixture intentionally has no authorised sender.

This is a test-fixture ripple explicitly anticipated by plan steps 3 and 9, but it was not completed.

### B. Existing “ordinary correspondence” expectations — obsolete for this ticket

`IntakeTestEvidence.CreateEmail` uses `instructions@qdosassist.co.uk`. Two tests expect that sender's ordinary correspondence to remain `NeedsSorting`, while the changed policy returns `CaseCreated`. The operator explicitly confirmed that non-instruction-email rules are not defined yet and are future work; BUG-001 must not invent such a predicate. For this ticket, those expectations must be revised or moved to the future capability that defines non-instruction mail.

### C. OCR decision masked by missing principal — genuine unintended regression

Two senderless scanned-PDF tests expected `OcrRequired` but now receive `NeedsSorting`. `ProcessIntake.AssessAsync` returns early when no principal route exists, before its existing `RequiresOcr` outcome can be selected. OCR status is document-processing state, not QDOS identity. The plan explicitly excluded changing PDF/OCR behaviour, so this early return is an unintended scope regression and must be corrected without allowing OCR/content to establish QDOS.

### D. Custody fixture — incomplete test migration

The failing custody test creates a manual-upload EML using the accepted QDOS sender helper, then expects the direct processing path to be definitive. Its failure needs focused reproduction while updating the fixture set; it is part of the plan's required custody ripple, not evidence that Box/custody policy itself changed.

### E. Standalone desktop evaluator — concrete caller regression

`scripts/email-eval-desktop` is an accepted ADR-0016 Windows tool and is documented in the runbook. After locked restore, its Release build fails at `EmailEvaluationWorkflow.cs:234` because it still calls the old extraction interface. This is not a GitHub CI failure because the project is outside `Pegasus.slnx`, but it is a real repository caller regression. The operator previously rejected the proposed update, so no fix is applied silently; the review cannot claim all callers were preserved.

### F. Legacy live receipts — not applicable

The operator confirmed Pegasus is pre-release and there are no legacy live receipts. No compatibility migration or production-data recovery is required.

## Report, plan, and governing-doc check

The intended sender-only QDOS change conforms to FRD-02, FRD-05, and FRD-09. No new ADR is needed. The report is not yet a complete account of the delivered state because it says the affected integration subsets passed while the actual PR CI later exposed 17 failures, and it does not record the desktop caller regression. Plan steps 8, 9, 11, 12, and 13 remain incomplete.

## Review comments and disposition

- **Blocking:** green CI/test-fixture migration, including preserving sender-only negatives. Disposition: needs changes.
- **Blocking:** preserve senderless OCR processing outcome without using OCR to identify QDOS. Disposition: needs changes.
- **Blocking:** standalone desktop caller compile regression. Disposition: needs explicit operator decision or repair; currently unresolved.
- **Won't do:** invent a non-instruction-email predicate. Future product work.
- **Won't do:** legacy-data migration. No released/live legacy data exists.
- **Fixed in PR:** exact three-domain/prior-sender QDOS identity and removal of content identity markers.
- **Fixed in PR:** route/draft mismatch rejection for new automatic allocations.

## Verdict

**Needs changes; do not merge.** PR #386 remains in Review. CI is red, OCR behaviour regressed outside ticket scope, an accepted standalone caller does not compile, and the current review is not independent.

The Kanmer workflow calls for blocking items in a `PR Review` area, but this board still has no such area. No substitute area or board restructuring was invented.

## Review-fix implementation — 2026-08-17

Approved fixes were applied in the existing PR:

- stale content-derived QDOS fixtures now either carry an exact accepted sender or assert no QDOS draft;
- ordinary correspondence fixtures that need Needs sorting use a neutral non-QDOS sender, without defining future non-instruction policy;
- senderless scanned PDFs retain OcrRequired independently of principal identity;
- the custody failure was proved to be case-match ambiguity from reused synthetic keys; unique claimant/claim keys make the focused test pass;
- the accepted desktop evaluator builds after removal of an obsolete extraction call whose result was discarded.

Local affected checks are green as recorded in the post-implementation report. Verdict remains pending until the amended commit is pushed and GitHub CI completes; this agent's review remains a self-review and cannot satisfy the independent-review requirement.

# Final review disposition — 2026-08-17

## Independent review

The GitHub Codex reviewer independently reviewed amended commit `b17cd78e` (it did not implement the change). Its two latest comments were dispositioned:

- scan-only accepted-QDOS mail: won't-do because the operator explicitly settled “once identified, that's it”; a second instruction/non-instruction or scan gate is future product work. Senderless scans still preserve OcrRequired.
- synthetic unit-test values: no change because the cited Review Claimant/Q-423 convention already existed on `origin/dev`; no retained/corpus/production evidence was introduced or committed.

Earlier feedback was also dispositioned: desktop caller fixed, legacy migration not applicable to the pre-release app, uploaded EML authentication framework rejected by the operator, and non-instruction-email policy deferred.

## Evidence

GitHub Actions run 32034500979 is green:

- changes, documentation, reference-data: pass;
- unit: pass;
- browser: pass;
- SQL integration shards 1, 2, and 3: pass;
- SQL integration coverage/partition: pass;
- infrastructure: correctly skipped because no infrastructure files changed.

The PR is CLEAN and `git diff origin/dev...HEAD --check` passes.

## Final verdict

**Pass.** The diff implements sender-only QDOS identity for the three exact recorded domains and proved staff-forward prior sender; content does not establish QDOS; accepted route remains allocation authority; senderless OCR status is preserved; no Box/deployment/live-data scope entered. All review comments have explicit dispositions, independent automated review covered the amended commit, and CI is green. Merge to `dev` is authorised by the operator's “proceed” instruction under the repository workflow.
