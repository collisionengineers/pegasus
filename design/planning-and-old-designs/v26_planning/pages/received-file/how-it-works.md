# Received file — how it works (Blocked so far)

Read from the live source on 13 September 2026. Only the Blocked outcome is written up; the rest of the received-file record is still to come.

## What "Blocked" is

`Blocked intake` is one of seven outcomes a received item (an intake receipt: an e-mail, an attachment, an uploaded file, a provider API submission) can carry. The glossary defines it as "a pre-Case failure boundary where required processing, identity, limits, custody or evidence is incomplete or unsafe", distinct from Unidentified (cannot tell what or whose it is) and Triage (a finding is needed).

The seven outcomes (`IntakeDecision`, `src/Pegasus.Core/Intake/IntakeContracts.cs`):

| Decision | Meaning | Can become a Case |
| --- | --- | --- |
| CaseCreated | definitive instruction; a Case reference was allocated | yes (already is one) |
| NeedsSorting | ambiguous provider, instruction type or case evidence, or any unidentified e-mail; goes to Unidentified | yes |
| **BlockedIntake** | a reasoned refusal: a person decided it must not become a Case as it stands | no |
| Unsupported | the file type or source is not supported | no |
| OcrRequired | a scanned document is waiting for OCR | no |
| TechnicalFailure | processing failed | no |
| ImageIntakeRegistered | registered as vehicle images (an Image-initiated Case) | no |

`IntakeDecisionPolicy.CanBecomeCase` is the one rule: only CaseCreated and NeedsSorting can.

## How an item becomes Blocked

Automatic processing never produces Blocked. Unreadable, oversized, unsupported or failed material becomes Unsupported, OcrRequired or TechnicalFailure; ambiguity becomes NeedsSorting. Blocked is written only by `EfIntakeMutationStore`, in three situations:

1. **A person presses Block** on the received item with a reason (`OnPostBlockAsync`, `IntakeResolutionKind.Block`). Decision becomes Blocked, the reason is stored as the decision reason and the failure reason, failure code `blocked_intake`.
2. **A person records a corrected draft that still lacks an identity-critical fact** (`IntakeResolutionKind.CorrectDraft`). If `InstructionDraftCompleteness.MissingIdentityCriticalFieldNames` is non-empty the decision becomes Blocked with "… remains unresolved"; otherwise it becomes CaseCreated and allocation runs. Thin ordinary detail is deliberately not a block: that is carried on the Case as Not ready.
3. **Re-evaluate with current policy** sets the decision to Blocked *while the re-evaluation is queued* (failure code `reevaluation_pending`, reason "A policy re-evaluation of the retained source is queued."); processing then rewrites the decision to whatever it finds.

There is no Unblock. A blocked item leaves Blocked only through Correct draft (route 2, succeeding) or Re-evaluate (route 3).

Separately, a **failed Case allocation** on the same page (`IntakeAllocationProjectionStatus.FailedBlocked`: the allocation attempt failed with no recovery disposition) shows as "Case not created" with Retry allocation. That is an allocation failure on a CaseCreated receipt, not a Blocked decision, though the page shows both under the same Decision panel.

## Where it shows

- **Received file record** (`Pages/Intake/Details.cshtml`): decision chip "Blocked", the recorded reason, and the Decision panel with Record corrected draft (only offered when Blocked), Re-evaluate with current policy, and Block. "Create a case from this item" is absent; `IntakeCannotBecomeCaseReason` says "This item was blocked, with the reason recorded. It cannot become a case until it is corrected on the received item."
- **Work Centre**: the Blocked metric is `IntakeQueueCounts.BlockedIntake` — every receipt whose decision is Blocked and which has no Case link, cumulative for all time, no age window. It links to Cases › Unidentified.
- **Cases › Unidentified tab**: blocked receipts are listed after the Unidentified items with the chip "Blocked intake", facts File, E-mail, Received, Reason, and the action "Open received item". They are listed but never counted in the tab or rail count (D14), so the two meanings stay distinct.
- **Message record**: the decision label "Blocked" on the attachment's outcome.
- **Unidentified resolve**: a Blocked receipt is a valid resolution target (`UnidentifiedResolutionTargetKind.BlockedIntake`), so an Unidentified item can be resolved "as" an already-blocked item.
- Automation (MCP intake tools) can read the decision as `blocked_intake`.

## How you get to the received-file record

There is no list of received files and no rail entry. `/Intake/Details/{id}` is reached only by following a link from something that already knows the receipt id:

| From | Link | When |
| --- | --- | --- |
| Cases › Unidentified tab | "Open received item" on a Blocked intake row | the only route that starts from a list; only Blocked receipts appear there |
| Work Centre | the Blocked metric, via that tab | same rows |
| Unidentified record | "View received item"; a candidate row in the resolve search; "Open image registration" | the Unidentified item's own receipt, or a receipt it may be resolved to |
| Triage record | "View received item" (the Triage's origin receipt) | always |
| Image-initiated Case record | "View received item" (the registration's origin receipt); the primary button when awaiting instruction | always |
| Create case | "Open the received item" on the "This item cannot become a case" refusal; "Reject proposal" on a seeded proposal; the redirect after a refused acceptance | when creating from a received file |
| Upload confirmation | the per-file outcome's attach-with-override and reversal actions (`UploadOutcome`) | after an upload |

Not linked from: the Inbox message (attachments show their outcome label but do not link to the receipt), Search (returns Cases and image intakes only), Operations, the Case record. So a receipt that ended Unsupported, OCR required or Technical failure, and that is not the origin of a Triage, Unidentified or image record, has no page anyone can reach except through the upload confirmation at the moment it was uploaded. E-mailed material with those outcomes is reachable from nowhere.

## Implications

- **Blocked is a human verdict, not a system failure.** Every real block has a person's reason behind it. The name and glossary suggest limits, custody and safety failures, but the code sends those to Unsupported and TechnicalFailure, which the Work Centre does not count at all. An unreadable PDF is therefore invisible on the Work Centre; a deliberately blocked one shows as a metric.
- **It is not terminal and not archived.** A blocked item counts forever until someone corrects it into a Case or a re-evaluation changes the decision. There is no close, no age, no owner and no due date.
- **The transient re-evaluation state pollutes the count.** While a re-evaluation is queued the item is Blocked with a different failure code, so the metric can rise for a moment for reasons that are not a refusal.
- **Correct draft is the only route to a Case.** Missing identity-critical facts (which claim it is about) keep it blocked; everything else becomes Not ready on the Case.
- **No reuse of identity.** FRD-02: a Blocked item never allocates a reusable identity as a convenience; a later correction allocates once.

## Governing documentation

| Document | What it settles |
| --- | --- |
| [CONTEXT.md](../../../../../CONTEXT.md) § Blocked intake | the term, and that it is distinct from Unidentified and Triage |
| [FRD-02 Intake and source identity](../../../../../docs/frd/frd-02-intake-and-source-identity.md) | "records a reason and visible warning, offers reasoned resolve and retry actions, retains the resolution evidence and each retry result; never allocates a reusable identity as a convenience"; the four received-file outcomes on the surface, "Cannot become a case" reported plainly with no offer |
| [FRD-12 § Work Centre](../../../../../docs/frd/frd-12-operator-experience.md#work-centre) | the Blocked metric links to Cases › Unidentified; blocked rows listed uncounted with their own chip |
| [FRD-01](../../../../../docs/frd/frd-01-case-identity-and-lifecycle.md) | never reuse a Case reference; Not ready carries thin detail |

## Source

| Layer | File | Owns |
| --- | --- | --- |
| Core | `src/Pegasus.Core/Intake/IntakeContracts.cs` | `IntakeDecision`, `IntakeResolutionKind` (CorrectDraft, Block), `ResolveIntakeRequest`, `IntakeQueueCounts` |
| Core | `src/Pegasus.Core/Intake/IntakeDecisionPolicy.cs` | `CanBecomeCase` |
| Core | `src/Pegasus.Core/Intake/InstructionDraftCompleteness.cs` | identity-critical fields |
| Core | `src/Pegasus.Core/Intake/IntakeAllocation.cs` | allocation attempt statuses incl. `FailedBlocked` |
| Core | `src/Pegasus.Core/Intake/Unidentified/UnidentifiedContracts.cs` | Blocked receipt as a resolution target |
| Infrastructure | `src/Pegasus.Infrastructure/Persistence/EfIntakeMutationStore.cs` | the three writes of Blocked |
| Infrastructure | `src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs` | `GetCountsAsync` (the metric), decision codes |
| Web | `src/Pegasus.Web/Pages/Intake/Details.cshtml(.cs)` | the record, Block, Correct draft, Re-evaluate |
| Web | `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs` | `BlockedRow`, listed uncounted on the Unidentified tab |
| Web | `src/Pegasus.Web/Presentation/OperatorLabels.cs` | `IntakeFailure`, `IntakeCannotBecomeCaseReason` |
