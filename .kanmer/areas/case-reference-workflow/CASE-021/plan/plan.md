# Plan

## The defect in one sentence

Export counts photographs; Review asserts a constant.

`EfCaseAcceptanceStore.cs:262` picks `Review` or `NotReady` from
`CompletenessEvaluation.SatisfiesPolicy`, which — with
`RequireCompleteImagesBeforeEngineerAssignment` seeded `true` — reduces to
`completeness.ImagesComplete`. That value is the hardcoded `true` at
`IntakeAllocation.cs:226`. **The Review gate never looks at an image.** The EVA
export refuses the same case at `EvaHandoffStore.cs:717` because it counts
custody-confirmed `DocumentSemanticRole.Image` occurrences and finds none.

The operator's framing is exactly right, and `operator-notes.md` settles which
side wins:

> *"**Not ready** means something is missing, almost always images or
> instructions. **Ready** means ready to enter into EVA but not yet entered."*

Review *is* "ready to enter into EVA". If the bundle refuses, the case is Not
ready. FRD-01 says the same normatively.

## Verified before planning

- **Production config is not the cause.** Read-only query against
  `WorkflowConfigurations`: `RequireCompleteInstructionsBeforeEngineerAssignment`
  and `RequireCompleteImagesBeforeEngineerAssignment` are both `True`. The fault
  is code, and this fix will bite.
- **`ImagesComplete` is observable at allocation.** `AttemptAutomaticAsync`
  already holds the receipt, and `IntakeReceipt.AssetRecords` is the retained
  asset list. No extra load, no new query.
- **The genuine-photograph rule already exists**, from INTK-030:
  `InstructionEvidenceImages.Select` — attachments must be `image/*`, embedded
  images must clear the 40 KB floor, both must pass `IsPhotographShaped` (side
  ratio < 3.0, which is what separates a letterhead banner from a photograph),
  inline signature graphics never qualify, duplicates collapse by hash.

## The change

Delete the constant. Build the record at the call site:

```csharp
new CaseCompleteness(
    InstructionComplete: true,
    ImagesComplete: InstructionEvidenceImages.Select(receipt.AssetRecords).Count > 0,
    InstructionConfirmedByStaff: false,
    ImagesConfirmedByStaff: false)
```

`InstructionComplete: true` **stays**. The receipt reached
`IntakeDecision.CaseCreated` — a definitive authorised instruction. That is a
real observation, not an assertion.

## Why not derive at Review time instead

Because Review is not a transition an automatic case *attempts*.
`EfCaseAcceptanceStore.cs:262` decides it as the case is created, before custody
has run and before a single `DocumentOccurrence` exists. There is nothing to
derive from at that instant except the receipt's own assets. Deferring the
decision into custody promotion would be a much larger change and is not needed:
custody promotion is already the path for material that arrives later.

## Regression risk — not reintroducing CASE-013

CASE-013's failure was structural, not a value: `IsReadyForReview` had no callers,
and two layers each carried a stricter copy of the rule demanding staff
confirmation nobody would ever give. Its fix was the automatic-definitive waiver
in `CaseCompletenessPolicy`, the actor test in `AcceptIntake`, and giving Core's
rule a caller. **This change touches none of that.**

CASE-013 in fact already decided this boundary, and its own test pins it —
`TheWaiverCoversStaffReviewOnlyAndNotMissingEvidence` passes all four flags false
with `automaticallyDefinitive: true` and asserts the policy is *not* satisfied. So
CASE-013 established that false evidence flags must block an automatic case. It
simply never supplied an honest one. This does.

Three guarantees:

1. The four existing regression tests construct `CaseCompleteness` directly and
   call the policy — they never touch `AllocateIntake`. The constant was untested
   input to a tested rule.
2. A forwarded instruction carrying photographs still evaluates `true`, still
   satisfies the policy under the waiver, still reaches Review with no staff
   action. That is the exact case CASE-013 existed to unstick.
3. No case is stranded. An image-free case sits in `NotReady` with its seven-day
   chase already scheduled by `EfCaseAcceptanceStore.cs:311-321`, and staff have
   two existing routes out — `ValidateReviewReadiness` is an **OR**, so confirming
   both readiness boxes drives a legitimately image-free case to Review.

## Is any legitimate case image-free?

Creatable without images: yes — an Audit arrives as instruction plus original
report, and `a.QDOS26013` is that shape. Deliverable without them: no. The
operator answered it directly: *"Lacking images should keep the case in 'Not
Ready' … Images are an EVA requirement / Report Requirement."* The rule is
absolute at the gate, with the existing staff escape above. **No operator question
is open.**

## Verification

- `dotnet build --configuration Release`
- `dotnet test tests/Pegasus.Core.Tests`
- `dotnet test tests/Pegasus.IntegrationTests --filter "Category!=Corpus"` (CI's
  three shards on the exact SHA are the authority)
- Simplification pass over the branch diff before the PR, recorded here.

## Known residual, not fixed here

The due-work reason is the flat string `"Details are incomplete"`, so the operator
sees a Not-ready case without being told it is the images. FRD-01 dislikes exactly
that opaque aggregate, and CASE-013 deferred naming the missing evidence as
"worth doing once the flags mean something". This ticket is what makes them mean
something. Follow-up, not scope creep here.
