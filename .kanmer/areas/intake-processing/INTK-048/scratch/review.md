Primary session authored the implementation; the blocking verdict below comes from fresh independent reviewer `review_pr601_independent`.

## Review — PR #601 at 14e0ad6f522a8b39c735f31535e842d8b0738fc8

### Changes

- `src/Pegasus.Core/Intake/ReconcileUnidentifiedDestinations.cs` treats the receipt's effective `CurrentCaseId` as an Instruction Case destination before original-decision eligibility and uses `CurrentCaseReference`.
- `tests/Pegasus.Core.Tests/Intake/ReconcileUnidentifiedDestinationsTests.cs` covers a still-eligible receipt with an active manual Case association.
- `tests/Pegasus.IntegrationTests/UnidentifiedReconciliationTests.cs` exercises the real manual-link path, sweep, persisted association/event, resolution history, and replay.

### Comments and dispositions

- **Blocking:** the supported `ReverseIntakeLink` path can clear `CurrentCaseId` after the new sweep permanently resolves the U-item. The resolved row is not reopened or resynchronized, registration replay returns it unchanged, and a later relink can leave the current resolution target pointing to the first Case. **Disposition:** filed as [[PR-069]], which blocks [[INTK-048]].
- **Non-blocking:** none.

### Gate checks

- The post-implementation report accurately lists all three changed files and their rationale.
- The implementation meets every recorded plan step, but the plan missed the reversible association lifecycle required by FRD-02 and linked [[INTK-029]].
- The simplification pass was honestly recorded: its test-claim gap was fixed and there are no undisposed simplification findings. The blocker is a correctness finding, not simplification feedback.
- No open-questions document exists; the questions-resolved gate is satisfied.
- GitHub reports the PR mergeable and all applicable checks successful; infrastructure is skipped by change classification.

### Verdict

**Needs changes.** Do not merge PR #601 until [[PR-069]] lands in this PR and an independent re-review passes at the new head SHA.

## 2026-08-29 — second independent review of PR #601, cross-model

`gpt-5.6-terra` (high) reviewed PR #601 after it was merged forward onto current
`dev`. Verdict **REQUEST_CHANGES**, and the reviewer independently traced the
mechanism in source rather than repeating the earlier review's assertion.

### Blocker — [[PR-069]] is real, is NEW, and is not fixed at HEAD

The reviewer walked the whole path and named every file and line:

1. A NeedsSorting receipt with an open U-item is manually linked to Case A
   (`UploadCaseDecision.AttachAsync` → `LinkIntake`).
2. The 10-second sweep (`src/Pegasus.Worker/IntakeFunctions.cs:191`) takes the
   new branch and resolves the U-item to Case A.
3. Staff unlink via `ReverseIntakeLink`.
   `EfIntakeMutationStore.ReverseLinkAsync` (~354-400) sets
   `association.IsActive = false` and **touches no Unidentified owner**.
4. `EfIntakeReceiptStore.Map` (~559-561) gates `ManualLinkedCaseId` on
   `IsActive` but sets `ManualAssociationVersion` unconditionally, so
   `IntakeContracts.cs:445-446` now yields `CurrentCaseId == null`.
5. `UnidentifiedState` is `Open | Resolved` only (`UnidentifiedContracts.cs:21-25`)
   with **no reopen path anywhere in `src/`**. `EfUnidentifiedStore.RegisterAsync`
   (~50-61) returns the existing resolved row on replay, and
   `ResolveForReceiptAsync` returns early (~103) for a non-Open row.

The retained material is then gone from the open queue with no destination at
all, and a later relink to Case B leaves the resolution pointing permanently at
Case A. `Resolved` is terminal.

**The reviewer established this is introduced by this diff, not pre-existing.**
Before it, `if (ProcessIntake.IsUnidentifiedEligible(receipt)) return false;`
blocked every NeedsSorting / Unsupported / OcrRequired / TechnicalFailure
receipt, and the Case branch additionally required `Decision == CaseCreated`, so
an eligible manually linked receipt could never be auto-resolved and
unlink-after-resolve was unreachable. That distinction is what makes this a
blocker rather than a known bug.

**Disposition: FIX IN THE LANE, inside PR #601.** [[PR-069]] carries
`blocks: [INTK-048]` and the earlier independent review had already said "do not
merge PR #601 until PR-069 lands in this PR". Merging now would merge past a
live blocking edge on the board. Per EPIC-011 decision D19 the preference is to
fix rather than defer, and deferring here would ship a data-loss path into
release 37. A `gpt-5.6-terra` (high) lane is implementing it on this branch, with
independent Claude verification.

### Finding — branch precedence changed silently (non-blocking, must be disposed)

The Case branch no longer requires `Decision == CaseCreated` and now runs first,
so **any** receipt with a non-null `CurrentCaseId` is recorded as
`InstructionCase`. A Triage-request receipt — for which `IsUnidentifiedEligible`
is false because `IsDeferredForAutomation` is true, so the old guard never
blocked it — that staff also manually linked previously resolved to `Triage` and
now resolves to `InstructionCase`. The same applies to a manually linked
`ImageIntakeRegistered` receipt, because `EnforceImageIntakeEligibilityAsync`
only checks case lifecycle state, not intake decision.

The Triage and ImageIntake branches remain in the file, so the vocabulary is not
structurally collapsed, and preferring the explicitly chosen Case is defensible.
But it is **named nowhere** — not in `plan.md`, not in `files.md`, not in the
post-implementation report — and **no test pins it**.
`src/Pegasus.Web/Pages/Intake/Details.cshtml.cs:645-664`
(`CloseUnidentifiedForTriageAsync`) is documented as closing the item "against
the Triage that now exists" and can now write `InstructionCase` instead.

**Disposition: decide and pin.** The lane must either pin the intended
precedence with a test and record the decision here, or restore the previous
precedence. An unrecorded, untested behaviour change is not an acceptable
disposition.

### Finding — test coverage matches the plan but not the risk

The new Core test and the new SQL integration test both cover
link → sweep → replay only. Neither covers unlink or relink — exactly the
lifecycle that exposes the blocker. **Disposition: fixed by the PR-069 lane**,
which is required to add both a Core and a real-persistence test for
link → reconcile → unlink and relink-to-another-Case.

### The three standing questions

1. **Did the plan miss anything implied by the ticket? YES.** The ticket's brief
   is the effective-destination contract; the plan only planned the forward
   link. It never considered `ReverseIntakeLink`, even though `research.md` had
   already named `CurrentCaseId` as the *reversible* effective association and
   the ticket links [[INTK-029]], the unlink/cancel owner. The gap is in the
   plan, not in the execution against it.
2. **Did the implementation miss anything in the plan? NO.** All five planned
   steps are present — guard reorder, `CurrentCaseReference` reuse, Core
   regression, SQL integration regression, simplification pass.
3. **Honest simplification dispositions? YES.** `plan.md` records the pass, names
   one applied finding (the integration test now re-checks the association and
   the `intake_case_linked` event *after* reconciliation, not only before) and
   states "No unapplied findings". Rule 22 was met on process — the blocker was
   filed as a ticket rather than silenced; the reviewer simply overrode the
   chosen disposition from "defer" to "blocking", which is the reviewer's call
   to make.

### Clean

**Second-implementation check passes.** `ReconcileUnidentifiedDestinations` is
the sole automatic derivation of a receipt's Unidentified destination. The
PR-069 fix must not change that — teaching `ReverseLinkAsync` or a Razor handler
to decide Unidentified state as well would be a stop condition, and the
verification lane is instructed to treat it as a blocker.
