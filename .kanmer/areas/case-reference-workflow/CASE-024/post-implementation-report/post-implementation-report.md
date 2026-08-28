# Post-implementation report

## What shipped

**1. Editing holds the lease for as long as the editor is open.** A new Core
seam (`IHeartbeatCaseEditLease` → `ILeaseCaseForEdit.HeartbeatAsync` →
`EfCaseWorkflowStore.HeartbeatAsync`) extends the holder's own live lease. A
hidden form rendered by `Pages/Shared/_EditHeartbeat.cshtml` posts it every
`CaseEditAuthority.HeartbeatInterval` (60 s). The operator is never timed out
mid-edit.

**2. Assessment enters the same edit mode.** `Pages/Cases/Assessment/` gains
Edit / Finish editing over the same one case lease, and its three save paths
present the token the operator holds instead of each claiming its own.

**3. A save still ends editing at zero seconds**, unchanged, with a regression
test that also proves a heartbeat cannot resurrect a lease a save just cleared.

## What the plan said, and where the work departed from it

Three departures, all recorded with reasons:

- **The lease duration did not change.** The plan first proposed a second,
  shorter heartbeat window (150 s) so an abandoned case freed sooner. Rejected
  before implementation: MCP automation cannot heartbeat and a slow model turn
  can exceed 150 s, the no-JS operator is in the same position, and hidden-tab
  timer throttling makes a sub-60 s interval worthless anyway. Keeping five
  minutes means nothing regresses and ~10 existing expiry tests pass untouched.
  Abandonment recovery is unchanged rather than improved.
- **Automatic mail association stops yielding to the lease.** Not in the
  original plan; added once the code showed the guard sat on the wrong side of
  the boundary FRD-01 draws. `AssociateFromMatchAsync` writes receipt rows only
  — never `caseWorkflow.Version`, and its history records `ExpectedCaseVersion`
  as null to say so. Without this, mail arriving during any editing session
  would silently need manual linking, because the yield is one-shot and never
  retried. The image-intake path at `EfIntakeMutationStore:510` really does
  mutate the case and keeps its check.
- **Shared edit-mode state moved to `CaseMutationPageModel`.** The plan said
  "reuse its TempData plumbing"; doing so honestly meant moving
  `RestoreLeaseState`, the claim/release handlers, the lease properties and
  `RequireOperationKey` onto the base class rather than copying them.

## Evidence

`dotnet restore --locked-mode`, `dotnet build --configuration Release` and
`dotnet test --filter "Category!=Corpus"` all clean on the branch head —
**exit 0**, 1045 Core + 100 Architecture + 1023 Integration, zero failures.
Full numbers and the caveat about an earlier tool-killed run are in
`scratch/notes.md`.

New tests state the two requirements directly: ten beats past the point an
unattended lease would have lapsed keep the case held and add no
`CaseEditLeaseOperations` rows; stopping frees it for another holder; a save
clears the lease and the next beat is refused; the heartbeat leaves
`EditLeaseOperationKey` holding the claim key; the web handler answers 204/409,
touches no TempData, and is refused 400 without its antiforgery token;
assessment saves make zero new claims and are refused outside edit mode; and
automatic association writes while a staff lease is live with the case version
and lease provably untouched.

## Simplification pass

Run before the PR; findings and dispositions are in `plan` under
"Simplification pass — 2026-08-28". It caught one **real defect**, not a style
point: the assessment had copied the workspace's claim handler and the copies
had drifted, so a refused claim minted a fresh operation key on one page and
replayed the same one on the other — a claim is idempotent by that key, so
minting risks claiming twice. Fixed by giving both pages one implementation. It
also closed a hole where a tab rendered before edit mode could save under a
lease a different tab entered afterwards. Two findings were dispositioned
without change, each with a stated reason.

## Stop conditions for the reviewer

Two things need operator sign-off and are **not** settled by this PR:

1. **The operator copy deletions.** Every edit-mode sentence that named a time
   now omits it, because a heartbeat-held lease makes "editing becomes available
   at 14:32" a promise that will be broken. Every change deletes a clause and
   writes no new sentence, but the approved-copy list is closed and this is
   operator-facing wording.
2. **The UI-15 exception.** `docs/design/README.md:896-910` records the
   assessment workbench as an exception whose "staff save paths … remain
   forbidden until the full UI-15 re-entry approval". Four such paths already
   existed there; putting an edit-mode control on the surface makes them
   operator-visible and widens that recorded exception.

## Not in this change

`KANMER-005` — lease exclusivity between staff and Automation Actors. A real
open defect that longer-held leases make more visible; this change neither
fixes nor worsens the claim path it concerns.
