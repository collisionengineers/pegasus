# Plan — ENG-041

## Objective

Recover interrupted Glass launches safely and make estimate-save retries retain
submitted intent without duplicating provider work or reverting later edits.

## Starting state

Base origin/dev 1d972f05c0f10c2ecf804f271a4fd3155242f1ef.
Evidence: research/research.md@ee71a48bec3203bf,
files/files.md@d538881bb9ea8c67. Material holes are resolved in research.
Prepared/Launching can strand the account; lost provider IDs cannot be queried.
Estimate replay currently enriches/hash-checks mutable state and forgets K2's
result after K3.

## Governing docs

Meets FRD-06 engineer-owned source-labelled drafts and provenance retention.
Clarifies interrupted Glass recovery and current-aggregate operation replay in
that FRD under the operator's explicit v1 remediation authority. No architectural
decision or new mechanism warrants an ADR. Existing Razor form conventions and
design/README no-explanatory-copy rules apply to the approved Close session
action, which has a required reason and explicit confirmation.

## Required changes

Persist protected callback identity before work, vehicle identity immediately
after its response, and an estimate-start marker before the external write.
Resume Prepared and known provider stages using CAS and the original identities.
A lost provider write response remains Unknown and account-occupying; cancellation
settles durably and unknown state never silently expires into a new launch.
Expose existing resume stages through the Case page. Add an own-Unknown closure
command with CAS, reason and explicit engineer confirmation that no external
estimate remains open; record permanent content-safe action history.

Carry submitted Case version and line identities unchanged from the save form.
After hash replay detection, apply existing-line provenance/rate/source retention
and amendment timestamps through EstimatePolicy. Resolve replay results from the
existing permanent operation action record, returning the same estimate Id in its
current state without reapplying the operation.

## Expected files

- `src/Pegasus.Core/Assessment/GlassRepairEstimates.cs`
- `src/Pegasus.Core/Assessment/Estimates.cs`
- `src/Pegasus.Infrastructure/Glass/GlassRepairEstimateGateway.cs`
- `src/Pegasus.Infrastructure/Glass/GlassMvaClient.cs`
- `src/Pegasus.Infrastructure/Persistence/EfGlassRepairEstimateSessionStore.cs`
- `src/Pegasus.Infrastructure/Persistence/EfRepairSpecificationStore.cs`
- `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs`
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseEstimate.cshtml`
- `src/Pegasus.Web/Presentation/CaseWorkspaceLabels.cs`
- `docs/frd/frd-06-vehicle-and-engineering-evidence.md`
- `tests/Pegasus.Core.Tests/Assessment/EstimateTests.cs`
- `tests/Pegasus.IntegrationTests/GlassRepairEstimateGatewayTests.cs`
- `tests/Pegasus.IntegrationTests/GlassRepairEstimatePersistenceTests.cs`
- `tests/Pegasus.IntegrationTests/GlassRepairEstimateCallbackWebTests.cs`
- `tests/Pegasus.IntegrationTests/AssessmentEstimateImportWebTests.cs`
- `tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs`

## Do not modify

- `src/Pegasus.Core/Cases/CaseContracts.cs`
- `src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs`
- `src/Pegasus.Infrastructure/Persistence/EfDocumentCustodyStore.cs`
- `docs/operator-notes.md`

## Constraints

No new provider APIs, dependency, schema, history table, migration or generic
recovery infrastructure. Use existing ports, transaction/history and Core policy.
Never reveal credentials, callback tokens or raw provider payloads in history.
Keep unrelated report/intake code untouched. Do not launch provider/cloud calls.

## Ordered steps

1. Extend the existing Glass contracts/gateway/store with durable known-stage
   recovery, explicit owner-only Unknown closure and content-safe transition
   history; preserve callback and import idempotency.
2. Make estimate requests stable, move editor carry-forward behind replay
   detection, and resolve all estimate operation results by permanent recorded Id.
3. Wire submitted version, known-state Resume and explicit Close session into
   existing Case estimate forms/handlers/labels; update FRD-06 behavior.
4. Add focused scripted-provider, SQL, Core and Web regression cases. Run only
   lightweight diff checks locally; give root exact focused test and case-details
   snapshot capture filters with the candidate implementation.

## Acceptance checks

Prepared interruption resumes without credential reset; interrupted create/start
without a returned Id stays Unknown and does not repeat provider writes. Known
vehicle and estimate checkpoints survive a recreated scope. Same launch op
returns the prior session. Different-user/stale/unconfirmed closure fails and
does not release the account; approved own closure is permanently reasoned.
Case page exposes the named recovery caller. Unknown is not silently expired.

Identical save POST uses the original version/intent; later mutable edits do not
change its fingerprint. K1 create/K2 update/K3 update/K2 replay returns the same
estimate Id at K3 state with no extra history/workflow/line write. Changed intent
under one key conflicts; stale new operation still fails. Imported provenance,
rate and amendment evidence remain correct on legitimate edits.

## Commands

Author: `git diff --check` and bounded source inspection only, Windows PowerShell.
Root sole verifier: focused Core EstimateTests; Integration filters
GlassRepairEstimateGatewayTests, GlassRepairEstimatePersistenceTests,
GlassRepairEstimateCallbackWebTests, AssessmentEstimateImportWebTests and
AssessmentPersistenceIntegrationTests (SQL category as applicable). Root owns
case-details snapshot capture using the capturing Web cohort and verify/catalogue.
No duplicate build/test invocation by this worker.

## Failure and deviation rules

Report failing checks, unowned shared files, unknown provider identities, missing
history association, conflicting authority or required scope expansion before
proceeding. Unknown outcomes are not success. Do not weaken tests to pass.

## Stop condition

Stop with code and focused tests ready for root verification, before commit/PR,
review, merge, deployment or any other ticket. Keep the worktree and claim for
resume; record exact evidence, unrun tests and the bounded next action.
