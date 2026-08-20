# Post-implementation report — INTK-016

PR: #465 (`task/intk-016-upload-confirmation` → `dev`). 21 files, +1279/−45.

## What shipped vs the plan

Every plan step landed, plus three findings the work surfaced:

1. **The decision surface.** `ReadyToCreate`, `PossibleMatch`, and a still-awaiting `ImageCaseRegistered` outcome now carry `UploadOutcomeAttach`; `_UploadOutcome.cshtml` renders the suggested action, a native-`<details>` "Add to an existing case" search form (works without script via typed-reference resolution), and a Cancel link to `/Upload`. `Attached` stays a pure report; `NeedsReview` keeps routing to Unidentified (INTK-018's surface). Post-INTK-015 report-not-reoffer holds: a registration or automatic association is reported with its link, never re-offered — and a merged group member now reports its destination case instead of a stale "registered as new".
2. **The endpoint and the attach.** One `UploadConfirmationPageModel` base (the `CaseMutationPageModel` precedent) owns `OnGetCaseSearchAsync` (JSON over `ISearchCases`, staff-only, ≥2-char terms, 8 suggestions of reference/registration/claimant/stage — no GUIDs displayed) and `OnPostAttachAsync` for both status pages. `Presentation/UploadCaseDecision.cs` orchestrates: replay short-circuit → `IGetCase` → `IAcquireCaseEditLease` → `ILinkIntake` (which itself runs `SyncMergeAfterLinkAsync`, so the image-group merge is the same one-owner transition, and the link's operation-key dedupe plus the short-circuit make the decision replay-safe under `upload-attach[-lease]:{receiptId:N}:{caseId:N}`). Recoverable failures fail closed to an honest error; required reason per the existing staff-mutation convention.
3. **The combobox** (first in the app): `site.js` enhancement per the file's conventions — debounced (250ms) abortable fetch, listbox rendering, ArrowUp/Down/Enter/Escape, `role`/`aria-expanded`/`aria-controls`/`aria-activedescendant` added by script only, selection fills a hidden case id, typing again invalidates it. Auto-refresh pauses while a `[data-refresh-hold]` disclosure is open so a reload never wipes operator input.
4. **Core additions** (both minimal, derivation-only): `IntakeReceipt.ManualAssociationActorKind` (projected from the association's recorded actor kind, which the automatic paths write as system-worker) and `AssociationWasStaffDecision` beside `CurrentCaseId` — so a staff link is never worded as automation's doing. One sentence owner: `OperatorLabels.AssociatedWithCase`.
5. **Docs in the same PR:** FRD-02's Upload confirmation surface now states the on-surface attach contract (replacing "the confirmation step itself never mutates anything") and row 2's awaiting/merged behaviour; FRD-12's Upload section states the three options and the search behaviour. No new capability row: the staff decision is the "anything else stays a reasoned staff decision" half of INT-28's own row, through existing Core use cases.

## Findings surfaced by the work (fixed in this PR)

- `.case-search-list { display: grid }` sat after the stylesheet's `[hidden]` ordering contract, keeping the suggestion box visible; per-component override with a comment citing the file's own contract.
- The green Success chip's text (`--success` on `--success-tint`) sat just under the 4.5:1 floor — caught by the new browser axe scan; new `--success-fg` (#11672e, ~6.1:1) following the established amber/red darker-fg pattern.
- A group member's drain order can leave its group outcome pending for the Worker's `ReconcileGroupedImageIntake` sweep (the ordinal-zero member's group lookup resolves only through it; the store already adopts a single-path row into the group). The web test now runs that sweep exactly as production does (`IntakeWebDriver.ReconcileGroupedImageIntakeAsync`); the surface itself also consults the group registration through the Core-owned `IsImageOnlyMaterial` rule so a member with a stale pre-group decision still reports the settled truth.

## Evidence

- Release build: zero warnings. Core.Tests 715/715. ArchitectureTests 97/97.
- `UploadConfirmationWebTests` 6/6 (authorised search + short-term floor; anonymous → sign-in redirect; roleless → 403; instruction attach end-to-end + replay; image-group merge by typed reference incl. merged/linked store state; report-not-reoffer; fail-closed unresolvable reference). Merge test 5/5 consecutive after stabilisation.
- `UploadOutcomeQueriesTests` 13/13 (4 new). `UploadCaseSearchBrowserTests` 1/1 (keyboard + ARIA + completed decision + axe). `AccessibilityTests` 24/24. Upload browser suites 7/7. Neighbouring suites green (Grouped/Qdos/ImageIntake/Negative/Shell/CaseEditMode/InstructionDraft — 6 corpus-dependent tests skip in the worktree, which has no local `corpus/`).

## Deliberately left out / follow-up candidates

- Unifying `Intake/Details`' two-step leased link flow onto `UploadCaseDecision` (reuse finding; behaviour-affecting on an untouched page).
- Making `EfImageIntakeStore.FindForReceiptAsync` itself test image-only material (altitude finding; untouched INTK-015 query).
- The INTK-015 registration race itself (single-path-first vs group-path ordering) — converges via the store's adopt path and the Worker sweep, but the ordinal-zero member's group lookup miss (`GroupedIntakeMemberToken` bare token vs `FindForMemberSourceAsync`'s `:{ordinal}` parse) deserves its own ticket if the sweep latency ever matters operationally.
