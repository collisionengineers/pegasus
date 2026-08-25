# Research

## Question

How can Pegasus guarantee that a case never reaches `Review` with incomplete instructions or no complete image evidence, regardless of administrator workflow configuration, without creating a second export-readiness policy?

## Findings

### 1. The accepted requirement is unconditional

- `docs/frd/frd-01-case-identity-and-lifecycle.md:46` makes instruction completeness and image completeness mandatory before Engineers-queue eligibility and says provider policy may define evidence but may not remove either gate.
- `docs/capabilities.md:139-140` repeats that both judgements and both completeness gates are mandatory.
- `docs/frd/frd-07-eva-and-external-engineering-handoff.md:13-19` makes `Review` the single export-readiness decision and says reaching it requires complete instructions and at least one eligible case image. Export itself must not duplicate the readiness rules.

Implication: this ticket changes the existing Review-entry policy. It must not add field checks to EVA Export.

### 2. Two administrator booleans can currently waive mandatory evidence

At PR #539 commit `cf28b8b0`, `CaseCompletenessPolicy.EvaluateAcceptanceCommand` in `src/Pegasus.Core/Cases/CaseDataOperations.cs:79-88` treats instructions or images as passing when the corresponding `RequireComplete...` configuration value is false.

That result directly controls both main persistence paths:

- `EfCaseAcceptanceStore.cs:262-264` creates a new case in `Review` when `SatisfiesPolicy` is true.
- `EfCaseDataStore.cs:107-120` moves an existing case to `Review` when the same result is true.

The administrator page exposes both values as editable checkboxes and can display `Not required`:
`src/Pegasus.Web/Pages/Administration/Configuration.cshtml:39-47,70-79`.

Implication: changing only the seeded defaults is insufficient. A stored or submitted false value can still waive a mandatory gate.

### 3. The other Review-entry paths already require both completeness facts

- PR #539 changes `CaseLifecycleRules.ValidateReviewReadiness` so return/reopen to `Review` requires `InstructionsComplete && ImagesComplete`; staff-review flags cannot substitute for either.
- `EfQueuedCustodyProcessor.cs:588-597` uses `CaseCompleteness.IsReadyForReview`, whose Core definition always requires both completeness values.
- Engineer assignment already requires the case to be in `Review`; its separate configured validation should nevertheless retain the same unconditional completeness invariant rather than accepting a contradictory request shape.

Implication: the defect is the configurable waiver in the shared workflow configuration/evaluation path, not a need for a new readiness service.

### 4. CASE-013's automatic-intake exception remains valid

[[CASE-013]] deliberately waives staff confirmation for an automatically definitive intake, but its tests and Core predicate still require both `InstructionComplete` and `ImagesComplete`. This ticket must preserve that distinction:

- completeness evidence is always mandatory;
- staff review may still follow the existing automatic-intake and configured rules.

### 5. Smallest coherent pre-release target

The product is unreleased and no current requirement supports making either completeness gate optional. Keeping administrator switches that no longer change behaviour would leave a misleading UI and dead contract.

The coherent target is therefore to remove the two `RequireCompleteInstructions...` and `RequireCompleteImages...` options from the workflow configuration surface and make Core always require both facts. Retain the two staff-review configuration options and the existing automatic-intake exception. Use the normal EF migration mechanism to remove obsolete columns; no compatibility shim, dual path, rollback preservation, feature flag, or new abstraction is needed.

## Verification needed

- A Core theory covers incomplete instruction/image combinations while the remaining staff-review settings vary, proving missing evidence never satisfies `CaseCompletenessPolicy`.
- Existing return/reopen Review tests remain green.
- Persistence tests prove new-case acceptance and later completeness confirmation cannot produce `Review` with either completeness fact false.
- Administration tests prove only the still-supported staff-review settings can be changed.
- Focused Core, Web/architecture as applicable, and integration tests pass.

## Open questions

None. The operator explicitly made both completeness facts mandatory and instructed a simple pre-release implementation.
