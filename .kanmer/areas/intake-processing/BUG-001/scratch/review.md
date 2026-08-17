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
