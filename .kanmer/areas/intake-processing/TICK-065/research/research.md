## Research — TICK-065 (INT-32) — verified 2026-08-20

**Question:** Do instruction/image halves each retain separate age and chase state, with definitive pairing notifying staff that the job is ready?

### What exists (shipped, release 12, `main`/`dev` = `ed3be51c`, present at `2325ed4a`)
- Derived lifecycle states for the image half: `ImageInitiatedCaseState { AwaitingInstruction, MergedIntoInstructionCase, StaffClosed }` — `src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs:31-35`.
- Migration `src/Pegasus.Infrastructure/Persistence/Migrations/20260819112914_ImageInitiatedLifecycle.cs`.
- Pairing visibility label: `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs:209` — `summary.AssociatedCaseId is null ? "Image intake registered" : "Associated with Case"`. This is the "definitive pairing" visibility half of INT-32's contract — derived, never independently set, so it can't disagree with the origin receipt (per `ImageIntakeContracts.cs`'s own doc comment).

### What is missing
- **No age projection for the image half.** `ImageIntakeRecord`/`ImageIntakeSummary` (`ImageIntakeContracts.cs:19-29, 88-97`) carry only `RegisteredAtUtc` and `State` — no derived age, no due-work timer.
- **No chase state for the image half.** `grep -n "age|chase"` over `src/Pegasus.Web/Pages/Triage/Index.cshtml.cs` (the Not-ready/image queue page) returns zero hits. The case-side chase machinery (`src/Pegasus.Core/Tasks/CaseWorkScheduling.cs` — `CaseDueWork`, `NextChaseAtUtc`, `FirstChaseAt`, `RunDueChasers.cs`, `RecordManualCaseChase.cs`) is keyed only on formal Cases in `NotReady`; it has no image-intake counterpart.
- **No "ready" notification.** There is no push/flag telling staff a pairing just completed beyond the passive `Associated with Case` label a user would see by revisiting the Cases list.

### Implications
INT-32 is genuinely **partial**: the pairing-visibility half of its contract shipped as a side effect of INTK-008 (Image-initiated Case lifecycle, release 12); the age/chase-state half was never built. This ticket should not be closed — it needs an implementation lane to add an image-half due-work projection (modelled on `ICaseDueWorkQueries.GetDueAsync`) and a ready-notification, per the seam already identified in `capability-survey.md` §4.

### Open questions
None — this is implementation-scope work, not a design question. `docs/frd/frd-02-intake-and-source-identity.md` already owns the contract.
