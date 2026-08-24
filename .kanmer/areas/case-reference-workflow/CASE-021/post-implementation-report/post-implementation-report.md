# Post-implementation report

**Branch** `task/case-021-observed-images` · **PR** [#528](https://github.com/collisionengineers/pegasus/pull/528) → `dev`
**Commits** `e1cb3f94` (the change) · `603948a4` (simplification pass) · fixture correction after CI

## What was wrong, in one line

Export counts photographs; Review asserted a constant.

The Review admission reduces — under the seeded configuration — to
`CaseCompleteness.ImagesComplete`, and automatic allocation supplied it as a
hardcoded `true`. So an audit with an instruction, an original report and no
photographs was born Review-ready while the EVA export refused the same case for
having no images.

## What landed

`ImagesComplete` is read from the receipt's own retained assets through
`InstructionEvidenceImages.Select` — already the single owner of "which retained
assets are this record's photographs", and already the rule custody uses to
decide what becomes an `Image` document, which is exactly the population the
export counts. Review and export now agree **by construction** rather than by a
second rule.

`InstructionComplete: true` stays. The receipt reached `CaseCreated` only because
a definitive authorised instruction was identified — an observation, not an
assertion.

## Not a return to CASE-013

CASE-013's failure was structural: `IsReadyForReview` had no callers and two
layers each carried a stricter copy demanding staff confirmation nobody would
give. Its waiver, actor test and single owner are **untouched**. CASE-013 in fact
already decided this boundary — its own test passes all four flags false with
`automaticallyDefinitive: true` and asserts the policy is *not* satisfied. It
established that false evidence must block an automatic case; it simply never
supplied an honest value.

No case is stranded: an image-free case sits in `NotReady` with the seven-day
chase acceptance already schedules, and `ValidateReviewReadiness` is an **OR**, so
staff confirming both readiness boxes drive a legitimately image-free case to
Review.

## Verified rather than argued

- **Production has `RequireCompleteImagesBeforeEngineerAssignment = True`**
  (read-only query). The fix bites, and the fault was code rather than
  configuration.
- The receipt at the call site is a fresh load with assets eagerly included,
  committed before allocation runs, so the flag cannot be spuriously false from
  staleness.

## Simplification pass — 2026-08-24

Findings and dispositions are in the plan. The substantive one: **the tests
asserted around the wiring rather than exercising it.** They called
`AutomaticCompleteness` and `CaseCompletenessPolicy.Evaluate` separately,
re-implementing by hand the line the diff changed, so nothing proved
`AllocateIntake` feeds the observed value into the acceptance command. They now
live in `AllocateDefinitiveIntakeTests`, drive `AttemptAutomaticAsync`, and assert
on the completeness `RecordingAcceptance` actually received. That also removed a
duplicated receipt builder and the need to widen `AutomaticCompleteness` to
`internal`, which is `private` again.

Two suggestions were **declined with reasons**: `Select(...).Count > 0` allocates
an array to test emptiness, but there is no `Any`-style predicate on that type and
adding one for a single caller is an abstraction with no second concrete caller;
and the doc comment was trimmed rather than expanded.

## What CI caught, and what it turned out to be

**`sql-integration (2)` failed** on
`InterruptedPendingOperationResumesThroughIdempotentAtomicAcceptance` with
`IntakeAllocationOperationConflictException`.

**Not a production defect.** That test hand-seeds a pending allocation attempt,
and its seeded command must match the one the automatic route builds. The fixture
carried `ImagesComplete: true` — with a comment saying it had to match — because
`true` is what the production path used to hardcode. The seeded receipt has no
assets, so the honest value is now `false` and the hashes disagreed.

Checked rather than assumed: observing the flag does **not** make the resume path
less stable. The command already carries `receipt.Version`, so any change to a
receipt's assets alters the hash regardless. Fixture corrected;
`QdosAllocationRecoveryTests` 17/17 green.

## Known consequences, pinned rather than discovered later

Three shapes now evaluate `false` where they previously sailed through. All three
are the flag finally telling the truth:

1. Photographs **embedded in the message body** rather than attached — inline
   images never counted. Pinned by a test.
2. Embedded PDF images under the 40 KB floor.
3. Photographs arriving on a **later receipt** — grouped image intake runs after
   allocation and nothing recomputes the flag.

Shape 3 is the likeliest in practice. None strands a case.

## Verification

| Check | Result |
| --- | --- |
| `dotnet build --configuration Release` | green |
| `dotnet test tests/Pegasus.Core.Tests` | **941 passed**, including all four CASE-013 guards |
| `dotnet test tests/Pegasus.IntegrationTests --filter "FullyQualifiedName~QdosAllocationRecoveryTests"` | **17 passed** after the fixture fix |
| CI on the pushed SHA | re-running after the fixture correction |

Local full-suite runs on this machine were contended (several agents and suites
at once) and produced failures — SQL post-login timeouts and a regex timeout —
that pass cleanly in isolation. CI's three shards on the exact SHA are the
authority and are what this report rests on.
