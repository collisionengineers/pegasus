## What changes

Two independent pieces sharing one task line (`Image-led intake`), because the
second unblocks work the first does not need:

### A. Image intake domain (INT-13, INT-27, INT-29, INT-30, UI-07 — Now/alpha, unblocked)

Today, image-only material with no formal instruction falls through to the
generic `NeedsSorting` outcome (`ProcessIntake.cs:262-283`); no persisted
"Image intake" concept exists anywhere in `src/`. Per
`docs/requirements.md:118,204,311` and `CONTEXT.md`'s glossary, an Image
intake is a durable pre-Case record keyed by a manually-confirmed normalised
VRM, carrying an **Image Intake Reference** allocated as
`{normalised VRM}-{sequence}` (two-digit minimum, expanding past `-99`,
never reused — `CONTEXT.md:23-24`), that may associate with at most one
eligible pre-report instructed Case.

VRM extraction is deliberately **not** automated here: `INT-17` (automatic
VRM reading from images) is explicitly off the alpha path
(`NOW.md:43`) and gated by open decision 1 below. Registration is a manual
staff action: a reviewer looking at retained image evidence in the intake
workbench confirms the VRM and registers the Image intake.

New `Pegasus.Core.ImageIntake` module (mirrors the shape of
`Pegasus.Core.Triage`, but is a distinct concept — no shared type):

- `ImageIntakeContracts.cs`: `ImageIntakeOrigin` (ReceiptId, SourceIdentity,
  SourceHash, EvaluationRevisionId), `ImageIntakeRecord` (Id, Origin,
  NormalizedVehicleRegistration, ImageIntakeReference, LinkedCaseId?,
  Version), `RegisterImageIntakeRequest`, `LinkImageIntakeCaseRequest`,
  `UnlinkImageIntakeCaseRequest`, version-conflict/operation-conflict
  exceptions (same pattern as `TriageContracts.cs:43-73`).
- `ImageIntakeLifecycle.cs`: validation — automatic association requires an
  unambiguous normalised-VRM match to exactly one eligible pre-report
  instructed Case with no contradictory identity evidence
  (`requirements.md:204`); anything less is a reasoned manual staff decision;
  a Case past report delivery is not eligible; unlink/relink permitted only
  before report delivery, always reasoned, always retains prior history
  (mirrors `TriageLifecycleRules` reasoning, not its code).
- `IImageIntakeStore` / `EfImageIntakeStore` (`Pegasus.Infrastructure`):
  persistence plus the reference allocator. New tables: `ImageIntakes`
  (entity) and `ImageIntakeSequences` (one row per normalised VRM,
  `LastAllocatedSequence`, allocated under the same
  read-increment-save-under-transaction pattern as `CaseSequenceEntity` in
  `EfCaseAcceptanceStore.cs:177-197` — no fixed-width cap, unlike the
  Case/PO `999` ceiling, because the reference format explicitly expands
  past two digits instead of throwing).
- EF migration adding both tables plus the `LinkedCaseId` FK.
- DI wiring in `Pegasus.Infrastructure/DependencyInjection.cs` alongside the
  existing Triage registrations.
- `Pegasus.Web`: a manual "Register Image intake" action on the Intake
  review workbench (`Pages/Intake/Details.cshtml(.cs)`) for retained image
  evidence with an operator-entered VRM; a link/unlink/relink action
  (reasoned, mirrors `ILinkTriageCase`'s existing UI wiring pattern in
  `Pages/Triage/Details.cshtml.cs`) available from both the Image intake and
  Case views; the exact outcome labels required by `requirements.md:944`
  (`Image intake registered`, `Associated with Case`).
- UI-07: extend the existing Cases/Intake search
  (`Pages/Cases/Index.cshtml.cs` and the intake query surface) to accept and
  display an Image Intake Reference alongside Case/PO.

### B. Open decision 1 research (unblocks INT-17, INT-28, INT-32 — Next/0.2.0)

`docs/open-decisions.md:32-38` blocks automatic VRM recognition on a choice
between an in-process model (reviewed origin/licence/hash/RIDs, no Python
service or runtime download) and a guarded external adapter (image-egress/
credential/retention/latency/cost contract), gated by a frozen genuine
labelled cohort + untouched holdout meeting preaccepted accuracy/abstention
gates.

This task produces a written, evidence-backed comparison
(candidate in-process ANPR/OCR models and their licences vs. candidate
external adapter vendors and their contract shape) as a addition to
`docs/open-decisions.md`'s item 1, narrowing the evidence — **not** a unilateral
accept. No vendor is selected, no credential is requested, and no code
activates automatic recognition or automatic matching (INT-28/32 stay
unimplemented) until the operator accepts a direction. If the operator
accepts one during this task, the follow-on ADR and INT-17/28/32
implementation become a new, separately claimed task rather than scope creep
here.

## What does not change

- `IIntakeTriageMatcher`/`NoAcceptedIntakeTriageMatcher` (Triage-to-instruction
  matching) — unrelated concept, out of scope, untouched.
- No automatic VRM reading and no automatic image-led/instruction-led
  matching ship in this task; both stay manual/staff-reasoned, consistent
  with `requirements.md:204`'s automatic-only-when-unambiguous rule.
- `docs/requirements.md`, `docs/capabilities.md`, `CONTEXT.md` already state
  the target behavior correctly; only `docs/open-decisions.md` gets new
  evidence appended to item 1.

## Verification

- `Pegasus.Core` unit tests: reference allocation sequencing (two VRMs
  interleaved, expansion past `-99`, no reuse after unlink), lifecycle rules
  (ambiguous match rejected, reasoned manual link/unlink/relink, ineligible
  post-report Case rejected), version-conflict behavior — mirrors the
  existing `TriageLifecycle`/`TriageContracts` test shape.
- `Pegasus.Infrastructure` integration tests against the EF store: concurrent
  allocation for the same VRM does not collide (same style of test as the
  existing Case-sequence concurrency test, if one exists — otherwise a
  focused two-parallel-request test).
- `Pegasus.Web` Playwright/page test: register an Image intake from retained
  image evidence, link it to an eligible Case, confirm the Case readiness
  recompute and the retained Image Intake Reference history, then unlink and
  confirm reversibility.
- `dotnet build --configuration Release` and the focused + full `dotnet test`
  suite green locally before the PR.
- Decision-1 piece has no code to verify; its output is the
  `open-decisions.md` diff itself, reviewed for accurately representing
  each candidate's licence/cost/contract evidence with no invented claim.
