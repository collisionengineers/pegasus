# Stream B implementation plan

## Governing docs

The user-approved D01-D17 decisions authorize the corresponding FRD corrections in this stream. Existing accounts/access and mail FRDs gain explicit administrator recovery, no periodic reviews, authorized staff sends and truthful Sent evidence; domain FRDs follow their named stream owners. Protected operator notes are not overwritten. Four-project architecture and existing policy owners remain binding.

## Starting state

D = 3284f93fc3ea9fd3bbbea9405ec92dc7818378f2, verified live. Owner tickets A PLAT-075, B CASE-047, C INTK-060. Follow the supplied exact file ownership register. User has authorized autonomous execution and the three-owner exception; no new permission request is needed for this implementation.

# Three-machine execution and handoff

This is an approved exception to one-ticket/one-feature-PR work. Future product
implementation uses three owner tickets and three new branches based on the
same current dev commit. Existing tickets are evidence and residual work owners,
not 210 separate implementation PRs. This planning package has no Kanmer ticket.
All three implementation PRs target dev and remain open and unmerged.

## Startup — Astra coordinates before any coding

Read [DECISIONS](DECISIONS.md), [SHARED-CONTRACTS](SHARED-CONTRACTS.md), your stream
plan and [Git dispositions](registers/git-dispositions.md). Read current
AGENTS/NOW/docs index and native Kanmer status/effective gates. Refresh GitHub
heads and the four old PRs; a changed head requires a delta review and updated
preservation table, not restarting or silently discarding this package.

Planning pin D is `3284f93fc3ea9fd3bbbea9405ec92dc7818378f2`; main pin is
`32f8679d3695e0dcab8f310a1c20f8b129d20190`. The shared source checkout is stale
and dirty. Do not reset, stash, clean, checkout, build or implement there.
Create clean worktrees only when product implementation is authorized.

At that time Astra creates exactly three owner records with descriptions below,
reads their effective profiles/gates, supplies this package as research/plan/
checklist and records actual branch/worktree with native Kanmer. Do not force
take the 47 currently claimed records or silently repoint their resume targets.
Use supported native Kanmer commands; never edit board files/branch manually.
If current Kanmer demands a different branch name, store the actual name once
and update all three machine instructions; ownership and three-PR count remain.

| Machine | Owner ticket title | Proposed branch | Worktree relative to own checkout | Final PR title |
| --- | --- | --- | --- | --- |
| Codex / Astra | Pegasus v1 platform, shared foundation and integration | task/pegasus-v1-platform | ../pegasus-worktrees/v1-platform | Complete v1 platform, custody, mail and integration |
| Claude / Fable 5.1 B | Pegasus v1 Case engineering, Glass's and reports | task/pegasus-v1-casework | ../pegasus-worktrees/v1-casework | Complete v1 Case engineering and report workflow |
| Claude / Fable 5.1 C | Pegasus v1 intake, principals and operator shell | task/pegasus-v1-intake | ../pegasus-worktrees/v1-intake | Complete v1 intake, extraction and operator workspace |

Each ticket includes its stream plan, the same frozen D, all shared decisions,
mapped old ticket IDs, allowed files, tests and the PR-open/unmerged stop. Do not
create a fourth foundation owner/PR, planning ticket or generic umbrella batch.
This package remains outside the repository; canonical product documentation
edits follow AGENTS. No secrets or corpus binaries enter tickets or PRs.

## Commit topology — common foundation, then independent work

All three branches are created at the exact same D. Astra authors F01–F03 on A
as common foundation commits F. B and C do read-only source/corpus preparation
until F is reviewed, compiling, and published. Before either has domain commits,
each fetches A and fast-forwards to **the same F commit objects**, not copies:

```text
                 A domain commits ---- A PR -> dev
                /
dev D ---- F ---+--- B domain commits -- B PR -> dev
                \
                 C domain commits ---- C PR -> dev
```

The fast-forward must be `git merge --ff-only <recorded-F-SHA>` on B/C; verify
`git merge-base --is-ancestor <F> HEAD` and record the shared commit identity.
Do not cherry-pick F, rebase it separately, merge dev, merge the foundation into
dev, or target B/C PRs at A. No dev update is authorized. Foundation appears in
all three dev comparison diffs until any later authorized merge; explicitly
label that shared range in each PR. Git ancestry applies it once.

Once streams diverge, do not fast-forward B/C to A's domain head or merge whole
mega branches into one another. A needed shared correction is authored by Astra
on a temporary local branch/checkout rooted at the latest shared F/G boundary,
reviewed, and merged with `git merge --no-ff <G-SHA>` as the **same G commit**
into each stream. Record G and each stream’s distinct merge commit. It changes only
Foundation-owned files/contracts. The temporary helper has no PR; preserve its
SHA in the owner evidence. Resolve conflicts in the owning stream and retest.
Do not make cross-stream contract changes independently.

Composition exception: when a new concrete type exists only on B or C, A
authors a small branch-local DI/host patch against that recorded head. The
stream applies the exact hash-recorded patch in a serialized registration
window; it does not improvise edits to A-owned files. A remains the sole
registration author/reviewer. That patch travels in the domain PR and compiles
there. Common G is reserved for changes whose dependencies exist in every
stream. The combined checkout combines the three registration additions under
A ownership; small composition conflicts are resolved explicitly. This avoids
stubs, reflective registration and importing unrelated domain commits merely
to register a type. Contract/schema changes still require common G.

## Foundation steps — Codex machine only

**F01, Astra + Sol contract review:** freeze the exact shapes and owners in
SHARED-CONTRACTS and the file manifest. Read B/C foundation requests as input;
the accepted shared contract wins when a request uses a different port name.
Port compatible PR670/671 schema hunks and PR639 watermark into the target
schema design, retaining per-hunk dispositions. Reconcile local AGENTS 0.4.2
semantic changes and the explicit task exception without replacing unrelated
dirty work. Publish exact API/enum/field/test fixture signatures in existing
canonical docs and owner-ticket plan, and author the actual shared C# definitions
in every S02 contract path. The A-before-F/B-or-C-after-F manifest exception is
contract-only; domain commands/stores stay in their stream. No consumer branch
may reference an absent contract or create a private copy. No ambiguous
per-stream schema choices.

**F02, Sol implementation; Terra tests:** own all EF entity declarations,
configuration classes, PegasusDbContext, migrations/model snapshot and grants.
Keep the valid existing migration chain and add the single coherent v1 schema
migration needed for the new model; no historical data conversion, dual columns
or old/new implementation switch. A fresh database applies that chain cleanly.
Default-null new facts represent genuinely unrecorded data, not invented domain
values. Configure A/B/C-provided minimal shapes in the existing aggregates.
The A inventory is `handoffs/A-foundation-requirements.json`; include its
credential, mail-attempt/correlation, cache and administrative lease primitive
requirements alongside both B/C inventories. Use
unique constraints for operation keys, T references, one Current estimate,
credential-active session and artifact versions. Add actual runtime-role grants
and bootstrap census in the same diff. F owns global persistence plumbing; B/C
implement their own store methods after the freeze.

F02's lease-clearance verification uses the explicit A-before-F/B-after-F
exceptions on `CaseEditAuthorityTests.cs` and `CaseWorkflowPersistenceTests.cs`.
A adds only that primitive's policy and persistence tests in those files;
B resumes normal domain ownership after the shared F SHA is recorded.

**F03, Terra; Sol independent check:** publish only registrations whose concrete implementations exist at F,
shared test support and stable shared shell markup/class contract. Do not
reference absent B/C types, use no-op registration hooks or add throwing stubs.
New domain handlers and their registration arrive together through the
serialized branch-local registration window below. Foundation alone is an
incomplete development checkpoint. Run locked restore/build, architecture/migration/grant and contract
tests in isolation, record exact F, and invite B/C fast-forward. Missing domain
implementation must be tracked in its exact step rather than falsely passed.

F is one initial synchronization point, not a demand that A finish its entire
platform before B/C start. Later ports use existing local fakes and genuine
source assets for parallel development. Runtime implementations land in their
owners and the combined checkout proves wiring.

## Waves and model delegation

| Wave | Codex A | Claude B | Claude C | Barrier |
| --- | --- | --- | --- | --- |
| 0 | Astra F01; Sol contract audit; Terra F02/F03 | Fable coordinates; Sonnet B01 read-only PR/source inventory | Fable coordinates; Opus C01 read-only evidence/PR inventory | All branches at D; no B/C domain commits |
| 1 | A01 identity and A04 custody: two Sol workers on disjoint paths | Opus B02 transaction; Sonnet records v3 field/manager matrix | Opus C01 correction then C02 provenance; separate Sonnet directory source inventory | Shared F adopted unchanged |
| 2 | Sol A02 Graph; Terra A06 admin query/UI | Opus B03 valuations then B04 estimates; independent Opus importer slice after estimate contract | Opus C03 profiles in bounded batches; second Opus C04/C07 pre-case rules on disjoint files | B totals and C candidate/location interfaces fixed |
| 3 | Sol A03 sending; second Sol A05 connector | Opus B05 reports/Glass's integration; Sonnet B06 Files when schema ready | Sonnet C06 directory and C08 shared shell; Opus C05 third-party extraction | A custody/send and C shared assets available |
| 4 | Terra A07 CI/performance; Luna A08 docs inventory | Sonnet B07 preparation/B08 assembly; Opus resolves complex findings | Sonnet C08 assembly; Opus corpus/failure checks | Each stream's callers wired; no domain placeholders |
| 5 | Fresh Sol A09; Astra unpublished combined verification | Fresh Fable 5.1 B09 full-stream review | Fresh Fable 5.1 C09 full-stream review | Exact heads and all review dispositions recorded |

Fable 5.1 is each Claude orchestrator, never a worker/subagent until the final
fresh whole-stream review. Opus 5 handles complex policy/concurrency/extraction;
Sonnet 5 handles routine UI/adapters/tests/docs. No Haiku. Astra is the Codex
orchestrator; Sol handles complex work/review, Terra routine work, Luna bounded
mechanical inventory. Every delegated task specifies exact files, inputs,
required caller/tests and stop condition. One author per file. A reviewer must
not be the author. A model need not wait idle merely because a separate step
is blocked; continue its already authorized disjoint work.

## Cross-machine evidence and ownership changes

Each owner ticket stores a compact handoff table: item/contract version,
providing commit, consuming stream, files, focused command/result and remaining
operator gate. Record updates at contract freeze, implementation checkpoint,
review and final head. The three root orchestrators communicate through these
shared owner-ticket documents and Git commit identities; one machine's local
path is never another machine's dependency. Subagent notes are merged into the
owner record, not posted as emails/messages to staff. Native MCP writes for
future ticket work are authorized by that implementation task, not by this
planning turn.

File manifest precedence is exact file, then deepest prefix; a tie is a defect.
Unlisted files are closed to edits until Astra assigns one owner. A shared-file
change goes through Astra's common G commit. Domain-file change goes to its
owner; send an exact patch/request, never edit a neighbour's checkout. B/C
domain interfaces frozen in F may be implemented in their files but changes to
the agreed cross-stream signature require G. A change to shared CSS stays C;
B supplies a fixture/expected behavior and uses Case-only assets for Case logic.

Handoff `newPaths` means new relative to D, not permission to recreate a file
already introduced in F. Check the phase fields first: A may have published its
shared records/interfaces at F, after which the domain owner extends that same
file with the real implementation. A domain worker never forks the definition.

## Existing PRs, tickets and worktrees

The 6 September 2026 snapshot accounts for PRs 639/646/670/671, 44 worktrees and
43 local branches. Refresh this census at implementation startup before any
retirement or preservation decision.
Preserve original commits, branches, ticket evidence and any dirty files.
Port required hunks with source SHA and exact target path; compare final
behavior/tests and reject superseded UI/schema churn with a reason. Do not
blind-merge stale branches. After both code preservation and independent review
are proved, authorized closeout may close the old PRs as superseded by named
replacement PRs. This is not a merge and does not prove their tickets Done.
Exactly three replacement PRs remain open when all streams finish. No draft
PRs are created for subagents, helpers, integration or foundation.

Contained branches are preservation/cleanup candidates only. Existing claims
are reconciled individually under current native gates, not forced, silently
released or deleted. Review/verification of already integrated code remains
real work and is included in the three owners' evidence. A genuine post-v1
feature receives the explicit deferred disposition, not a fake implementation.

## Combined verification and final stop

Astra creates an **unpublished** disposable integration checkout from D and
merges the exact A/B/C heads locally. No PR or remote integration branch. Check
conflicts and migration count, run canonical validation and routed UI/corpus
journeys, and record the three inputs plus combined tree/commit. A combined
failure returns to its file owner, then that owner and the combined checkout
retest affected checks. Do not conceal a failed individual PR behind a passing
combination. Refresh integration whenever any input changes.

Final handoff has exactly 3 PR URLs, all to dev and unmerged; three exact heads;
common F/G ancestry; green applicable CI/standalone checks; combined evidence;
old-PR preservation/closure evidence; honest human provider gates; current docs;
and [the operator checklist](OPERATOR-CHECKLIST.md). No dev/main merge, deploy,
reset, live credentials or provider write occurs as a side effect of completion.



# Stream B — Casework implementation plan

This plan implements the Case workspace and completed report journey from the
pinned `astra_output/source/dev` tree at `3284f93fc3ea9fd3bbbea9405ec92dc7818378f2`.
It is governed by [DECISIONS](../DECISIONS.md), the future
[shared contracts](../SHARED-CONTRACTS.md), and the future
[coordination register](../COORDINATION.md). Those documents settle cross-stream
order and ownership; this file describes only Stream B's implementation.

Stream B owns Case/domain v3, the combined Case save, valuation and estimate
policy, Glass's repair-estimate workflow, report projection/generation and
preparation, and Case-specific assets. Foundation owns global EF entity/model
configuration, migrations, the model snapshot, shared contracts/test support,
and composition-root registration. Stream A owns outbound mail transport and
shared custody/cache infrastructure. Stream C owns extraction and general UI
assets. Stream B may implement its own store/adapter methods after Foundation
freezes their schemas and registrations.

## Execution method and stop conditions

The root Fable 5.1 coordinator maintains the dependency ledger, enforces file
ownership and integrates reviewed commits. It does not write implementation.
Use Opus 5 for B02–B05 because they change concurrency, professional findings,
money or external-session recovery. Use Sonnet 5 for bounded routine slices in
B01 and B06–B08 after their contracts are frozen. Do not run two agents against
the same file. At the end, use a fresh Fable 5.1 context for the complete B09
review of the exact branch head; the root coordinator then applies only
accepted findings and leaves the PR unmerged.

Stop the affected step when a required Foundation contract is absent or has
changed, another stream owns a needed file, an accepted report phrase cannot be
traced to governing evidence, genuine corpus evidence is missing, or the
operator-only Glass's journey is required. Do not invent a substitute, schema,
provider response or report phrase. Do not call Glass's, EVA, Box, Outlook,
Azure or a live mailbox during implementation. Do not revive the standalone
Assessment UI, per-section saves, field padlocks, Confirm requirements, review
checkboxes, periodic account review, automatic chasers, or a damage Type field.

All writes use server-derived `ActionActor`, the current Case edit lease,
expected Case version, operation key and reason where the existing contract
requires them. Administrator visibility never grants Engineer finding
authority. Completed/closed Case facts, accepted estimates, applied valuations
and generated artifacts remain immutable; correction creates a reasoned new
version. Existing application data is disposable, so implement one clean
target schema without a legacy compatibility branch. Historical accepted
records that are deliberately retained keep their recorded policy and totals.

## B01 — Freeze inputs and preserve PR 670

Pin the source comparison to PR 670 tip
`f22751cad3d5a713f39503ef48ff30422d67c97f`. Refresh the remote PR at execution
startup and disposition any delta separately. Preserve this exact tip and
every hunk; B09 compares it against the final B head. B01 creates the one
`_CaseValuation.cshtml` by porting the accepted PR hunk; B03 then extends that
same file, never creates a second implementation.

**Model:** Sonnet 5. **Timing:** first, after Foundation publishes its initial
file-ownership register; no other B implementation starts until this inventory
is reviewed.

**Files to inspect:** `src/Pegasus.Core/Assessment/Valuations.cs`,
`src/Pegasus.Core/Cases/CaseQueries.cs`,
`src/Pegasus.Core/Documents/RequestUploadPolicy.cs`,
`src/Pegasus.Core/Vehicle/VehicleWorkflow.cs`,
`src/Pegasus.Infrastructure/Persistence/EfValuationStore.cs`,
`src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs`,
`src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs`,
`src/Pegasus.Infrastructure/Persistence/EfVehicleWorkflowStore.cs`,
`src/Pegasus.Web/Pages/Cases/Details.cshtml(.cs)`,
`src/Pegasus.Web/Pages/Cases/Shared/_CaseDocuments.cshtml`,
`_CaseHistory.cshtml`, `_CaseVehicle.cshtml`, and the existing
valuation/vehicle/custody tests named by PR 670. Port B-owned hunks only; A/C
apply their own mapped hunks. Port PR 670's new
`_CaseValuation.cshtml` into the combined workspace. Foundation alone owns
PR 670's `AssessmentModelConfiguration.cs`, `CustodyModelConfiguration.cs`,
`20260905173354_CaseValuationGuideMonthAndRequestUploadMetadata*` and
`PegasusDbContextModelSnapshot.cs`. B01 ports the one Case valuation partial;
all remaining additions are implemented in their named later steps or by A/C.

1. Diff PR 670 against the pinned source and record each useful hunk by target
   file. Its exact touched set is the Case details/valuation/vehicle/documents
   surfaces, valuation policy/store, request-upload metadata, tests, snapshots,
   and Foundation-owned migration/model files. Preserve the guide-month field,
   lookup chips, upload-request metadata and their tests only where they agree
   with UI v3 and current contracts.
2. Port preserved source changes into the B branch as ordinary reviewed
   changes. Hand the migration, global model, snapshot and DI portions to
   Foundation through `handoffs/B-foundation-requirements.json`; do not edit
   those files in B.
3. Keep `Pages/Cases/Assessment/Index` as the existing redirect to the Case
   Estimate section. Do not restore PR 670's obsolete independent Valuation or
   Vehicle edit routes, duplicate section commands, old explanatory panels or
   any UI that conflicts with one Edit Case/Save/Discard.
4. Retain original PR/branch evidence until the integrated B tree contains the
   selected hunks and a path-level comparison proves none were lost. Closing
   PR 670 as superseded is a coordination/closeout action, not part of this
   code step.

**Acceptance:** a preservation table maps every PR 670 file to `ported`,
`Foundation handoff`, or `rejected with UI-v3 reason`; the old redirect remains;
no migration/snapshot/DI file is modified by B; focused existing valuation,
vehicle, upload-request and Case-detail tests compile against the new owners.

## B02 — One Case edit transaction and the complete Case surface

The shared administrative lease-clearance primitive is implemented in F02
before B domain work; the manifest explicitly transfers subsequent workflow
file ownership to B. A owns its administration POST caller. B consumes and
regression-tests the primitive; it does not use the holder-token Release
operation as an override. Test wrong target/generation, no active lease, replay,
concurrent renew/clear and rejection of the old token. This explicit small
F-phase exception avoids an unimplemented A caller or duplicate lease owner.

**Model:** Opus 5. **Timing:** after Foundation freezes the Case v3 persistence
shape and a transaction-capable save boundary. Complete before B03–B07 expose
their controls.

**Files:** reuse `Pegasus.Core/Cases/CaseDataContracts.cs`,
`CaseDataOperations.cs`, `EngineerNotes.cs`,
`Pegasus.Core/Assessment/AssessmentContracts.cs`, `AssessmentPolicy.cs`,
`AssessmentOperations.cs`, `Pegasus.Core/Address/InspectionAddressResolution.cs`,
`Pegasus.Infrastructure/Persistence/EfCaseDataStore.cs`,
`EfCaseAssessmentStore.cs`, `InspectionAddressChoicesQueries.cs`,
`InspectionAddressResolutionStore.cs`, `Pages/Cases/Details.cshtml(.cs)`, and
the existing `_CaseSummary`, `_CaseEngineerNotes`, `_CaseInspectionAddress`,
`_CaseVehicle`, `_CaseDamage`, `_CaseSettlement`, `_CaseReport`, `_CaseFiles`,
`_CaseHistory`, `_CaseWorkspaceNav` and `_ReadinessHiddenFields` partials. Add
`Pegasus.Core/Cases/CaseWorkspace.cs`,
`Pegasus.Infrastructure/Persistence/EfCaseWorkspaceStore.cs`,
`tests/Pegasus.Core.Tests/Cases/CaseWorkspaceTests.cs`, and
`tests/Pegasus.IntegrationTests/CaseWorkspacePersistenceTests.cs`. Extend
`CaseDataOperationsTests.cs`, `AssessmentPolicyTests.cs`,
`InspectionAddressResolutionPolicyTests.cs`, `CaseDetailsWebTests.cs`,
`CaseEditModeWebTests.cs`, `CaseDataCompletenessPersistenceTests.cs`,
`InspectionAddressChoicesPersistenceTests.cs`,
`InspectionAddressChoiceBrowserTests.cs`, `CaseVehicleWebTests.cs` and
`CaseEngineerSectionsWebTests.cs`.

Add the Core `SaveCaseWorkspace` command and B's concrete
`EfCaseWorkspaceStore`. The store receives the existing scoped
`PegasusDbContext`, performs the Case-data and assessment writes itself inside
one `BeginTransactionAsync(IsolationLevel.Serializable)` transaction, and
commits once. Foundation freezes the request/result/store contract, configures
the added entities, and registers the concrete store/command. There is no
generic unit of work and no multi-store orchestration. Reuse the edit-lease,
version, operation replay, named finding checks and server actor already present
in the Case stores; extract their internal write helpers only as needed so the
new store remains the single transaction owner.

The command covers the complete eleven-section matrix while retaining each
business owner:

| Section | Exact save/read responsibility |
| --- | --- |
| Overview | Claimant, principal, contacts, repairer, accident, instruction, assigned/sign-off Engineer and workflow facts; read-only versioned principal/source-wide notes; Case-specific principal/source notes and copied claim-source contact snapshot. Claim source is distinct from principal, sender, insurer and third-party engineer. |
| Engineer notes | Use the existing separately attributed append command outside the replace-style workspace payload; it is not a professional finding and does not stale a report. |
| Inspection | [Operator truth](../../source/dev/docs/operator-notes.md#inspection-address) governs the CE method: every CE assessment is desktop. Report location may be blank/undetermined, `Image Based Assessment`, or a selected physical vehicle location; a physical address describes the vehicle's location, never attendance. Preserve IBA as a principal default. Consume C's S05 directory suggestions unchanged: current claimant/repairer/storage, previously accepted principal locations and Administrator-maintained rows. Missing addresses are disabled; staff select explicitly or enter manually. Save the chosen address and provenance, storage business/contact/address, and the same daily/recovery amounts Settlement uses. Retain inspection date, vehicle-present value, condition, contact, telephone, email and inspection notes. |
| Vehicle | Registration, VIN, make/model/body/class/colour/fuel/transmission/engine/year/first registration, lookup evidence/chips, mileage/source, tax/MOT, condition, modifications/extras, fault codes/airbags, roadworthiness/reason, temporary repair and vehicle history/notes. Preserve original odometer value/unit/source; display conversion is exactly `1 mile = 1.609344 kilometres`, never reconverts a rounded display, treats zero as present and sends miles only to miles contracts. |
| Damage | Existing `AssessmentImpact(Zone, Severity, Note)` with the 23 detailed regions, broad Front/Rear/Side retained as broad facts, one headline-parent map, independent entries, tyres/belts/spare/centre belt, unrelated damage/deduction, transfer and incident narrative. No Type field. |
| Valuation | Load recorded cards, working calculator, accepted Engineer value and correction history from B03. Workspace Save retains only non-adopting draft inputs; only B03 Apply changes the professional finding. |
| Estimate | Load named estimates and editor from B04. Workspace Save validates and persists all edited Draft estimate header/rows through B04 policy inside the same database transaction; failure commits none of the workspace. Use as Current, duplicate, discard and import remain explicit commands requiring Save/Discard of pending edits first. |
| Settlement | Repairable, Total loss, Cash in lieu, Contract repair, salvage, excess, betterment, claimant VAT, reserve, duration/delays, storage/recovery, diminution, hire and salvage logistics. Cost-to-value/equity uses Current estimate and accepted Engineer value; it is not the total-loss `PAV - salvage` calculation. |
| Report | Engineer comments, history-check text, signatory choice, agreed fee/description, output switches and date choice feed B05. Generation, preview and Prepare remain explicit commands. |
| Files | Reads custody/provenance, correspondence, upload links and queries; B06's crop/rotation/role/order edits join the workspace transaction, while source files remain immutable. |
| Notes | General attributable Case history/notes only, separate from Engineer, principal, source and report comments. |

Remove client-posted readiness booleans as authority. Inside the mutation,
reload persisted completeness and evaluate the one Core readiness policy. Keep
Instruction complete and Images complete as factual controls; remove Confirm
requirements and all review flags. Discard is client-local restoration before
commit and restores crop/rotation edits as part of the same snapshot.

**Negative cases:** false posted readiness; stale Case version; missing/expired
or foreign edit lease; replay with changed payload; Administrator attempting an
Engineer finding; partial store failure; physical address with blank/IBA
address-treatment mode; any UI claim of CE physical attendance;
zero mileage; repeated mi/km toggles; empty/hostile/stale address suggestion;
broad damage split into detailed regions; accepted/closed record overwrite.
Every case must fail without partial writes.

**Acceptance:** `Details` is the sole production Case workspace with Overview,
Engineer notes, Inspection, Vehicle, Damage, Valuation, Estimate, Settlement,
Report, Files and Notes in scroll and tabs modes. One Save either persists the
entire authorized snapshot or none. Section navigation and lazy GETs never
discard local edits. No periodic review or physical-attendance implication is
present.

## B03 — Recorded valuations and explicit Engineer-value adoption

Reuse the `_CaseValuation.cshtml` ported in B01. Copy each selected preset
ID/version/label/suggested amount, then permit the Engineer to edit the copied
Case amount with validation; this never edits the maintained preset. The
applied snapshot records both suggested and chosen amounts.

**Model:** Opus 5. **Timing:** after B02 and Foundation's valuation/preset schema.

**Files:** reuse `Pegasus.Core/Assessment/Valuations.cs`,
`Pegasus.Infrastructure/Persistence/EfValuationStore.cs`,
`Pegasus.Web/Pages/Cases/Shared/_CaseValuation.cshtml` from B01,
and `tests/Pegasus.Core.Tests/Assessment/ValuationTests.cs`. Add
`Pegasus.Core/Assessment/ValuationCalculations.cs`,
`Pegasus.Web/Pages/Administration/ValuationPresets/Index.cshtml(.cs)`,
`tests/Pegasus.Core.Tests/Assessment/ValuationCalculationTests.cs`,
`tests/Pegasus.IntegrationTests/ValuationPresetPersistenceTests.cs`,
`tests/Pegasus.IntegrationTests/ValuationPresetAdministrationWebTests.cs`, and
`tests/Pegasus.IntegrationTests/CaseValuationWebTests.cs`. Foundation owns the
new valuation/preset entity configuration, migration, snapshot and DI entries.

Extend `Pegasus.Core/Assessment/Valuations.cs` and B-owned
`EfValuationStore.cs`; keep them the sole owner of valuation arithmetic and
Engineer-value adoption. Recorded cards retain stable valuation ID, source,
guide month, mileage, retail, nullable trade, source version/provenance, AI
research/adverts, original value and correction history. Support existing
manual source language including Glass's, Brego and Super CAP, but implement no
live valuation provider. Glass's live integration in B04 is repair estimating
only.

Foundation supplies versioned valuation-preset persistence. Seed Tow bar, PCO
plated, Decals, Camper conversion and Driving tuition from the approved manager
source; Core reads data records rather than hardcoding a list. Selecting a
preset copies ID/version/label/amount; custom additions require a label. Disabled
presets remain readable but cannot be newly selected.

Implement one preview calculation and one explicit Apply command:

```text
V = RoundAwayFromZero(B * 0.20, 0) when commercial VAT applies
A = B + V
T = RoundAwayFromZero(A * p, 0) for prior total loss at p = 0.10 or 0.20
Proposal = RoundAwayFromZero(A - T + fixed additions - condition deduction, 0)
```

`B` is the selected guide retail value and may not be missing. Fixed additions
and condition deduction are non-negative. Claimant VAT registration clears and
disables commercial VAT. Reject a negative proposal. Selection or preview never
changes Engineer's Value. Apply rechecks guide/preset versions, Case version,
lease and Engineer finding authority, then atomically stores the accepted value
and the complete ordered calculation snapshot, actor/time/reason and policy
version. Existing broad recorded adjustments remain history and are not folded
into the new snapshot or subtracted twice. Later manual correction keeps the
applied basis and adds rationale/history.

**Tests:** midpoint and non-midpoint decimal rounding; the £3,100/£620/£744/
£300/£100 = £3,176 example; missing retail; claimant registered; 10%/20% loss;
disabled/stale preset; duplicate source names with distinct IDs/months; negative
addition/deduction/final; unapplied preview; unauthorized actor; replay conflict;
manual value after application. Printed currency is invariant `£#,##0.00`, but
the adopted whole-pound value remains decimal and source inputs are retained.

## B04 — Canonical estimates, arithmetic, imports and Glass's

Concrete handlers: `DetailsModel.OnPostLaunchGlassAsync` and
`OnPostResumeGlassAsync`; `Administration/Glass/IndexModel`
`OnPostSaveAsync`, `OnPostDisableAsync`, `OnPostClearAsync`; and
`Integrations/Glass/CallbackModel.OnGetAsync`/`OnPostAsync` accepting only the
provider completion shape. Add BOTH `Callback.cshtml` with its `@page` route
and `.cshtml.cs`. The callback validates persisted one-use correlation and
provider/Case/session identity; it does not rely on the provider submitting a
staff anti-forgery token. Launch/resume/admin writes use normal staff POST
authorization and anti-forgery. Test these actual routes through production DI.

The normalized provider-account key participates in one-active-session
uniqueness across Pegasus users and credential generations. B owns the session
row and protected cookie/CSRF ciphertext; A owns the credential row and
protection primitive, F owns mapping/constraint. Replacing credentials closes
the old generation before another active session can start. If the Case lease
or version becomes stale while Glass is open, retain validated XML/PDF and
the completion as awaiting import; require the engineer to regain current edit
authority before canonical import. Never discard provider work, overwrite
Current or repeat external creation to resolve an application conflict.

**Model:** Opus 5. **Timing:** estimate policy and importer first; Glass's only
after Foundation freezes its session/entity schema and Stream A exposes the
approved secret/custody boundaries.

**Files:** reuse `Pegasus.Core/Assessment/Estimates.cs`,
`RepairSpecifications.cs`, `EstimateImport.cs`,
`Pegasus.Infrastructure/Persistence/EfRepairSpecificationStore.cs`,
`Pegasus.Infrastructure/Assessment/JsonEstimateParser.cs`,
`AudatexEstimatePdfParser.cs`, `Pages/Cases/Details.cshtml(.cs)`,
`Pages/Cases/Shared/_CaseEstimate.cshtml`, and the existing `EstimateTests.cs`,
`RepairSpecificationPolicyTests.cs`, `AssessmentEstimateImportWebTests.cs`,
`AssessmentPersistenceIntegrationTests.cs`, `JsonEstimateParserTests.cs` and
`AudatexEstimatePdfParserTests.cs`. Add
`Pegasus.Core/Assessment/GlassRepairEstimates.cs`,
`Pegasus.Infrastructure/Glass/GlassRepairEstimateGateway.cs`,
`Pegasus.Infrastructure/Persistence/EfGlassRepairEstimateSessionStore.cs`,
`Pegasus.Web/Pages/Integrations/Glass/Callback.cshtml.cs` (handler only),
`tests/Pegasus.Core.Tests/Assessment/GlassRepairEstimateTests.cs`,
`tests/Pegasus.IntegrationTests/GlassRepairEstimatePersistenceTests.cs`,
`GlassRepairEstimateCallbackWebTests.cs`, and
`GlassCredentialAdministrationWebTests.cs`. Add the domain-specific
`Pages/Administration/Glass/Index.cshtml` and `Index.cshtml.cs` for per-engineer
Glass configuration, consuming A's account/credential administration contracts.
A owns the Accounts/Edit link to this page; B never edits Accounts/Edit.
Foundation owns Glass entity
configuration/migration/snapshot/DI; Stream A owns credential encryption and
the canonical `ICaseArtifactCustody` implementation.

Extend `Pegasus.Core/Assessment/Estimates.cs`, `RepairSpecifications.cs` and
`EstimateImport.cs`; B-owned methods in `EfRepairSpecificationStore.cs`; the
existing JSON and Audatex parsers; and the Case Estimate partial/handlers. Keep
named tabs, blank Untitled creation, name/source/state/duration/rate-card/notes,
duplicate, compare, immutable versions, discard and explicit Use as Current.
At most one estimate is Current. Opening/importing never changes Current.

Rows contain operation, description, part number, quantity, panel hours, paint
hours, materials, fixed unit amount, and retained origin/current values with
source document version/hash and amendment actor/time. Closed operations are
Replace, Repair, R&I, Paint, Blend, Specialist and Other, mapped once to the
existing line vocabulary. Preserve meaningful off-pattern values and flag them;
changing type does not erase fields. Derive New parts required, Repairs required
and Additional operations from rows. Do not add independent text lists.

Snapshot the one rate/version for panel and paint labour. Retain estimate-level
Additional materials and Other costs visibly. Store parts/materials/specialist
discounts, overall discount, VAT flags for Labour/Parts/Materials/Specialist,
repairer VAT status and whether categories are overridden. Unknown repairer VAT
must block acceptance until the operator records explicit status or explicitly
selects the VAT categories. Registered defaults all four; non-registered
defaults Parts and Materials; claimant VAT never controls estimate VAT.

`EstimateTotals.Compute` remains the only calculator. Use decimal inputs and:

```text
P = sum Replace quantity * unit price
L = eligible panel hours * one rate
Q = Paint/Blend paint hours * the same rate
M = row materials + Additional materials
S = Specialist fixed quantity * unit price + Other costs
O = off-pattern unit amounts in specialist treatment
Category = P(1-dP) + L + Q + M(1-dM) + (S+O)(1-dS)
Net = Category(1-dA)
Taxable = selected discounted categories(1-dA)
VAT = Taxable * 0.20
Gross = Net + VAT
```

Specialist hours are displayed but never multiplied by the labour rate. Zero or
missing fixed-cost quantity uses one. Validate discounts in `[0,1]`.
`EstimateTotals` retains the original decimal inputs and unrounded components.
Its presentation projection rounds each discounted category and VAT independently
to two decimals, away from zero: Parts `P(1-dP)(1-dA)`, panel labour
`L(1-dA)`, paint labour `Q(1-dA)`, Materials `M(1-dM)(1-dA)`, Specialist
`(S+O)(1-dS)(1-dA)`, and VAT from the unrounded selected taxable base. It then
defines printed Net as the sum of those five printed components and printed
Gross as printed Net plus printed VAT. It never moves a residual penny into VAT
or any category. Example: raw taxable Parts `100.005`
prints Parts `£100.01`; raw VAT `20.001` prints VAT `£20.00`; Net is `£100.01`
and Gross is `£120.01`. Store calculation-policy version and the raw plus printed
breakdown. Imported totals remain separate reconciliation evidence; never drop
a source value to force agreement.

For Glass's, reuse the same canonical import command. Add a Case Estimate button
for the signed-in configured engineer. Use Core session operations with Case/
user/credential-generation/lease/version/operation key. The Infrastructure
adapter ports only the supplied MVA cookie/CSRF/vehicle/ERE/callback/export and
XML/PDF validation mechanics, with one isolated cookie session and one active
session per configured account. Use a specific one-use expiring HTTPS callback
on the existing Web host; validate allowed origins, correlation, ERE, Case,
user, registration/mileage and application success. Persist identities before
side effects. Creation/start/relay never retry blindly; only safe lookup may
retry once. Completion retains XML/PDF through A's custody port and creates one
new source-labelled Draft. Duplicate launch/callback/export is idempotent.
Unknown side effects remain uncertain and require reconciliation; never start a
replacement silently. No actual provider call is made by an agent. Only the
operator runs live Glass's acceptance.

**Tests:** every operation/category; discounts and overall order; all/no VAT;
unknown VAT gate and explicit override/reset; off-pattern values; specialist
hours; zero quantity; reconciled component rounding without residual mutation;
source-versus-Pegasus discrepancy;
accepted estimate successor; stale version/replay; malformed/ambiguous Audatex;
Glass login expiry, HTTP-200 application failure, hostile redirect, mismatched
identity, malformed/oversize XML/PDF, double click/callback/export, restart at
each persisted stage, two users/credentials isolation and same-account limit.

## B05 — Immutable full report and fee-note generation

Use exact Case handlers `DetailsModel.OnPostGenerateReportAsync`,
`OnPostGenerateFeeNoteAsync`, `OnGetGeneratedArtifactAsync`. Generation reloads
permission, lease/version, persisted readiness and output choices, freezes the
immutable snapshot and operation identity in a short database transaction,
then renders outside that transaction with actual registered
`PlaywrightAssessmentReportRenderer`. Do not hold SQL locks through Chromium
or Box HTTP. A custody receives the frozen artifact occurrence/key/hash; B
records confirmed artifact metadata only after custody succeeds. Pending/failed/
unknown custody stays visible and retry uses the same snapshot/operation.
A concurrent material Case change marks that generation stale; it cannot be
prepared as current merely because rendering finished later.

`OnGetGeneratedArtifactAsync` takes generation/artifact identity and reopens
the authorized confirmed immutable bytes, not current-state regeneration.
The only v1 production templates are `assessment_report.scriban`,
`assessment_fee_note.scriban` and their `report.css`. The other template assets
listed below are read-only design references, not planned embeddings or edits.
A preview of an ungenerated working snapshot must be explicitly labeled as
such and cannot create a final artifact, download-completed event or Sent claim.

Extend `Reports/CaseReportGenerationWebTests.cs` for one routed journey with
genuine case evidence and actual local Playwright/Chromium: save choices,
generate report and fee, assert parseable non-empty PDFs, reopen byte-identical
versions, assert report-only/fee-only renderer calls, change material facts,
refuse stale preparation, regenerate, prepare and invoke A's recording send
transport, then observe Sent only after a matching fake provider observation.
No live Box/Graph call; fakes implement the production custody/send boundaries.

**Model:** Opus 5. **Timing:** after B02–B04 and Foundation's generation/artifact
schema. Stream A's durable-object writer must exist before artifact commit.

**Files:** reuse `Pegasus.Core/Reports/AssessmentReportProjection.cs`,
`AssessmentReportRendering.cs`,
`Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs`,
`Pegasus.Infrastructure/Persistence/EfAssessmentReportProjectionSource.cs`,
`docs/design/assets/report-renderer/templates/assessment_report.scriban`,
`assessment_fee_note.scriban` and `report.css`, plus existing report
Core/integration tests. Other template assets are read-only references. Add
`Pegasus.Core/Reports/CaseReportGeneration.cs`,
`Pegasus.Infrastructure/Persistence/EfCaseReportGenerationStore.cs`,
`tests/Pegasus.Core.Tests/Reports/CaseReportGenerationTests.cs`, and
`tests/Pegasus.IntegrationTests/Reports/CaseReportGenerationPersistenceTests.cs`.
Extend `AssessmentReportProjectionTests.cs`, `AssessmentReportRenderingTests.cs`,
`AssessmentReportRendererTests.cs`, `AssessmentReportDraftWebTests.cs` and
`CaseReportApprovalWebTests.cs`. Foundation owns generation/artifact entity
configuration, migration, snapshot and DI; Stream A implements
`ICaseArtifactCustody`.

Extend `AssessmentReportProjection.cs`, `AssessmentReportRendering.cs`,
`PlaywrightAssessmentReportRenderer.cs`, the accepted Scriban templates and
B-owned `EfAssessmentReportProjectionSource.cs`. The requested artifact kind is
explicit: preview/render only the assessment report or fee note requested; do
not render and discard the other. Bound selected images and apply operation
timeouts. Do not create a new rendering service without measured need.

Replace `CostsOf` and `ReportRepairCosts` recomputation with the complete
canonical `EstimateTotals` breakdown while retaining hours as descriptive
quantities. Printed components and total must reconcile. Make every Glass's
statement source-aware: the `Disclose guide source` flag controls accepted guide
wording, and `StatementOfTruth3` must not name Glass's when another/no guide is
selected. Change only wording with accepted authority; stop on unsupported
legal/payment/outcome language.

Readiness reloads persisted facts and requires authorized Case access, complete
current Case facts, eligible complete signatory tuple, one Current accepted
estimate with rate/policy, accepted Engineer value, exactly one Close-up and one
Overview image, valid selected supporting images/source hashes, and applicable
content fields. Remove retired D18 Engineer-name/qualification/signature gates;
the selected signatory account/version is the owner. Missing optional EVA never
blocks a complete Pegasus report.

Generation freezes Case/version, accepted facts, assigned/signatory account
versions and signature bytes, Current estimate/version/breakdown, accepted
Engineer value/applied valuation, `Disclose guide source`, `Valuation
commentary`, `Include unrelated damage`, report date/override, narrative facts,
agreed fee and fee-description lines, source document versions/hashes, image
role/order/crop/rotation, template/renderer version, generated-at and artifact
hashes. Report date defaults only at generation. It is distinct from
generated/approved/sent timestamps. Fee preview uses the accepted decimal fee
input and the existing two-place, away-from-zero fee VAT/total policy; it does
not infer accounting or invoice behavior.

Persist immutable report PDF and fee note as separately addressable generated
artifacts through A's custody boundary, tied to the same accepted snapshot when
prepared together. Preserve prior generations and issued history. Relevant
accepted fact, method/location, content, Current estimate, Engineer value or
image-preparation changes mark current generation stale. Ordinary Case/Engineer
notes and recipient edits do not change bytes or stale it. Preview/generate
never creates Sent evidence.

**Tests:** each readiness blocker; incomplete/ineligible signatory; retired
field absence; report-only and fee-only renderer calls; stale snapshot after
every relevant change and not after notes/recipient changes; exact image roles,
order/crop/rotation/hash; fee calculations; content switches independently;
report-date override; source-aware wording; HTML escaping; timeout and bounded
image refusal; immutable prior artifact; renderer/template version mismatch;
reloaded permission/version between preview and generation.

## B06 — Case assets, image preparation and Files

**Model:** Sonnet 5. **Timing:** after Foundation's asset/preparation schema and
Stream A's durable Box plus 24-hour Azure-cache contracts.

**Files:** reuse `Pegasus.Core/Custody/CustodyContracts.cs`,
`Pegasus.Infrastructure/Persistence/EfDocumentCustodyStore.cs`,
`Pages/Cases/Custody.cshtml(.cs)`, `Pages/Cases/Shared/_CaseDocuments.cshtml`,
`_CaseFiles.cshtml`, `_CaseCorrespondence.cshtml` and `_CaseReport.cshtml`. Add
`Pegasus.Core/Documents/CaseAssetPreparation.cs`,
`Pegasus.Infrastructure/Persistence/EfCaseAssetPreparationStore.cs`,
`tests/Pegasus.Core.Tests/Documents/CaseAssetPreparationTests.cs`, and
`tests/Pegasus.IntegrationTests/CaseAssetPreparationPersistenceTests.cs`.
Extend `CaseCustodyWebTests.cs`, `DocumentCustodyDurabilityTests.cs`,
`ImageViewingWebTests.cs`, `LocalCaseCustodyAtomicWriteTests.cs` and the Case
browser suite. Foundation owns preparation entity configuration/migration/
snapshot/DI; Stream A's single `ICaseArtifactCustody` supplies both durable
source and generated-artifact byte operations.

Reuse accepted Case-document/image custody queries and write B-owned asset
selection/preparation methods. The Case Files section keeps documents, image
gallery/viewer, provenance, correspondence, upload links and post-report
queries. Do not add a global Images page or treat cache keys as durable IDs.

Preparation assigns exactly one Close-up, one Overview, ordered Supporting, or
Not used. Retain original immutable bytes/hash and source version. Crop uses
fractions of the rotated source; store rotation, crop, role/order, actor/time and
expected asset/Case version. Reset restores the original presentation. Keyboard
move controls and drag ordering call the same command. Crop may open outside
Edit Case, but saving it acquires/uses the common Case edit session, so Discard
restores its prior preparation. Content-type, hash and bounds checks fail closed.

**Tests:** duplicate Close-up/Overview; missing readiness roles; stale asset;
cross-Case asset; invalid/empty/out-of-range crop; rotations; reorder replay;
source immutability; cache miss with durable source; unsupported media; Files
viewer and Report cards showing the same preparation; script-off keyboard
actions. No Box/Azure mutation is performed in local tests; use existing fakes
and genuine repository assets.

## B07 — Report preparation, recipients and optional EVA

Exact Case handlers are `DetailsModel.OnPostPrepareReportDeliveryAsync` and
`OnPostSendPreparedReportAsync`. They use the saved generation/artifact and
preparation version, not posted readiness flags. Reauthorize and validate
freshness/recipient permissions/hashes at both operations; the send handler
invokes A's sole `IStaffReportSend` command. A returns its exact state; B never
creates a local Sent state. Implement B's `IReportSendReadiness` from the shared
F contract in `CaseReportDeliveryPreparation.cs`; A invokes it against persisted
actor/Case/generation/preparation versions and exact hashes at the send boundary.
Transport observation stays with A. Original-thread reply and correction use A's
recorded compose mode/message identity, with previous issued evidence intact.

**Model:** Sonnet 5. **Timing:** after B05. A's mail-send command contract must be
frozen; B never implements transport.

**Files:** edit `Pages/Cases/Shared/_CaseReport.cshtml`, `_CaseCorrespondence.cshtml` and
`_EvaHandoff.cshtml`, plus `Pages/Cases/Eva/Send.cshtml(.cs)`. Add
`Pegasus.Core/Reports/CaseReportDeliveryPreparation.cs`,
`Pegasus.Infrastructure/Persistence/EfCaseReportDeliveryPreparationStore.cs`,
`tests/Pegasus.Core.Tests/Reports/CaseReportDeliveryPreparationTests.cs`, and
`tests/Pegasus.IntegrationTests/CaseReportDeliveryPreparationWebTests.cs`.
Extend `ApprovedMailboxReportSentEvidenceTests.cs`,
`CaseReportApprovalWebTests.cs`, `SentEvidencePollPersistenceTests.cs` and the
existing EVA policy/web tests. Foundation owns preparation entity configuration,
migration, snapshot and DI; Stream A owns `IStaffReportSend` and transport.

Add the Case Report preparation view/command with generated artifact selector,
To, CC, Subject, selected report/fee attachments and a current-snapshot check.
Resolve recipients through authorized structured principal/case contacts; never
parse unstructured notes. Recipient edits affect the delivery intent, not
generated bytes. Recheck Case/report permission, artifact freshness, recipient
authorization and attachment hashes on Prepare and again when invoking A's
staff-send command.

Only a signed-in staff action may invoke A's transport. Unknown send outcome is
preserved as pending/unknown with no automatic duplicate retry; truthful Sent
evidence comes only from A's transport/reconciliation. No scheduled chaser or
automatic send exists. Generation is not delivery.

Keep the existing explicit Send to EVA route as optional. It consumes the same
accepted Case/report data but does not gate report completion. Do not reactivate
automatic EVA, convert unknown EVA delivery into retryable, or describe EVA as
the report system of record.

**Tests:** stale/missing generation; changed recipients; unauthorized address;
attachment hash mismatch; staff-only invocation; duplicate operation key;
unknown transport result; no Sent evidence on prepare/generate; report complete
with EVA absent; explicit EVA route remains separate.

## B08 — UI v3 assembly and regression coverage

Inspection must retain and round-trip date, vehicle-present value, condition,
contact, telephone, email and inspection notes in addition to method/location.
These facts do not introduce a CE physical-attendance method. Use and test the
existing sign-off resolver: eligible persisted Sign-off Engineer, then eligible
assigned Engineer, then configured eligible default. No eligible profile blocks
report readiness. Exercise the resolved tuple in the ribbon and generated
snapshot, and preserve all inspection values on validation return/section switch.

Address suggestions are ONLY C-B03/S05: current claimant/repairer/storage,
principal's previously accepted locations and Administrator-maintained rows;
normalized name/postcode prefix, minimum 2 characters, maximum 20 results, stable source/
version ordering. B consumes the query unchanged and copies the chosen address
and provenance through C's command. It performs no separate ranking, accident
location or image/vision inference and no external address lookup. Manual
entry remains available.

**Model:** Sonnet 5. **Timing:** after B02–B07 handlers are stable. Reuse the
existing Razor layout, partials, tokens, scripts and Test UI machinery; do not
copy the standalone HTML runtime.

**Files:** edit `Pages/Cases/Details.cshtml(.cs)` and every existing Case shared
partial named in B02/B04/B06/B07, the Case-only `case-workspace.css/js`, and
`docs/design/test-ui/pages/case-details--default.html`,
`case-details--unavailable.html`, `case-details--conflict.html`. C owns the
shared `site.css/js`; A owns the catalogue/index and shared test harness.
Do not create a second page or bundled UI framework. Extend B-owned tests and
run the A/C-owned shared tests read-only, using the exact manifest:
`CaseDetailsWebTests.cs`, `CaseEditModeWebTests.cs`,
`CaseEngineerSectionsWebTests.cs`, `TestUiSnapshotTests.cs`,
`TestUiFocusedRenderTests.cs`, `Browser/LayoutIntegrityTests.cs`,
`Browser/AssessmentReadinessSummaryBrowserTests.cs` and
`Browser/InspectionAddressChoiceBrowserTests.cs`.

Implement the persistent ribbon (Case/PO, registration, claimant, principal,
assigned and sign-off Engineer, state), action bar, workflow actions, Current
position/Next action/Figures rail and the eleven named sections. Scroll mode has
sticky jump navigation and lazy section GETs; Tabs renders the same underlying
models. At narrow desktop width the rail collapses without hiding sections.
Opening is read-only; Edit Case exposes one Save/Discard. Use labels and compact
controls, with Report wording as a disclosure. Do not add explanatory empty
panels.

Verify the exact controls from B02–B07: desktop-assessment wording and separate
provider-controlled IBA/physical-address treatment;
disabled missing addresses; vehicle lookup chips and mi/km display; 23-region
damage keyboard diagram; source-card valuation with preview and explicit Apply;
named estimate tabs/grid/provenance/discount/VAT/versions/Glass button; complete
Settlement; Report content/date/wording/image/readiness/previews/history and
delivery preparation; Files viewer/custody/correspondence/upload/query; separate
general Notes and Engineer notes. Empty Engineer sections remain visible as
unrecorded. There is no periodic review, physical-attendance inference, fake
live provider, automatic mail, separate Assessment workspace or obsolete UI.

The manager-v3 delta checklist is binary at exact-head review:

- [ ] One Edit Case, one Save/Discard, one lease/version, and dirty values
  survive section switching, lazy loads and validation returns.
- [ ] Ribbon, action bar, Current position, Next action and Figures remain;
  scroll/tabs expose the same eleven sections and narrow-width rail collapse.
- [ ] Principal/source-wide notes are read-only on Case; This Case notes and
  copied source contacts remain role-specific.
- [ ] Every assessment is described as desktop; IBA is a report-address choice
  and principal default, while physical vehicle addresses never assert CE
  attendance. C's bounded directory suggestions require staff selection.
- [ ] Vehicle retains all compact fields, one DVLA/MOT lookup action, suggestion
  chips, original mileage evidence and non-drifting mi/km display.
- [ ] Damage has the exact 23 regions plus broad and additional chips, only
  zone/severity/note, keyboard use and one parent-region export mapping.
- [ ] Valuation retains distinct source cards/history, guide ID/month/mileage,
  preset/custom additions, commercial VAT, 10%/20% prior loss, condition
  deduction, preview and explicit Apply with the approved rounding order.
- [ ] Estimates retain named tabs, create/duplicate/compare/Current/version
  controls, row operation/part/quantity/panel/paint/material/provenance fields,
  rate snapshot, category/overall discounts, explicit VAT categories, visible
  retained Additional materials/Other, derived work lists and full breakdown.
- [ ] Unknown repairer VAT blocks acceptance until status or categories are
  explicit; claimant VAT does not set estimate VAT.
- [ ] Every editable currency input places a visible `£` prefix immediately
  before the control, outside the editable numeric value. Read-only money uses
  `£#,##0.00`; blank optional money stays blank and an inapplicable typed cell
  shows `—`, never `£0.00`. The £3,100 valuation example displays `£3,176.00`;
  the estimate rounding example displays Parts `£100.01`, VAT `£20.00`, Net
  `£100.01`, Gross `£120.01`.
- [ ] Glass's appears only in Estimate for a configured signed-in engineer and
  returns one source-linked Draft; no Glass valuation or fake live state exists.
- [ ] Settlement retains four outcomes and all original financial/logistics
  fields, with equity distinct from total-loss settlement.
- [ ] Report retains comments/history/signatory/qualifications/fee, three
  content choices, report date override, wording disclosure, readiness,
  report/fee previews, immutable issued history and stale-generation state.
- [ ] Report image preparation enforces one Close-up and one Overview, ordered
  Supporting/Not used, crop/rotation/reset, keyboard/drag controls and immutable
  originals; Files viewer exposes the same preparation.
- [ ] Prepare delivery has generation, To/CC/Subject and attachment selection;
  only Stream A's staff-send transport can create truthful Sent evidence.
- [ ] EVA remains an optional explicit handoff; complete Pegasus report and fee
  preparation does not depend on EVA.
- [ ] Files retains documents/gallery/viewer/custody/correspondence/upload links/
  post-report queries; Engineer notes, general Notes and report comments remain
  separate.

Regenerate only affected `docs/design/test-ui` snapshots with the repository
script and keep catalogue entries deterministic. Add focused browser tests for
one-edit preservation across lazy sections, validation return with all dirty
values, script-off actions, narrow layout, lease loss, report stale state,
unknown VAT, estimate totals and asset ordering. The supplied local v3 HTML was
not freshly browser-verified during planning; implementation proves the routed
Razor pages, not the standalone prototype.

## B09 — Exact-head verification and fresh full review

**Model:** fresh Fable 5.1. **Timing:** after all B commits and Foundation/A
handoffs used by B are present on the B branch. No implementation author performs
this review.

**Files:** modify no product file. Review every file enumerated in B01–B08,
`Pegasus.slnx`, `Directory.Build.props`, package lock files affected by the
build, the exact generated migration/snapshot supplied by Foundation, and the
B-owned changed test files within the existing three test projects. Store review/proof only in the
future owning ticket and PR; B09 creates no repository Markdown file.

Review the full diff against pinned `dev`, all B01 preservation dispositions,
the v3 feature matrix, [DECISIONS](../DECISIONS.md), shared contracts and
ownership register. Trace every new Core contract to the Case UI or report
caller and every store/adapter to Foundation DI. Confirm that PR 670's accepted
hunks survive and rejected UI is absent. Search for duplicate arithmetic,
hardcoded preset/state lists, catch-all suppression, second storage charges,
client-authoritative readiness, legacy branches, Glass valuation calls,
periodic review and attendance inference.

Run, from the B worktree on Windows/PowerShell 7:

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~Assessment|FullyQualifiedName~Reports|FullyQualifiedName~CaseData"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~Case|FullyQualifiedName~Assessment|FullyQualifiedName~Report|FullyQualifiedName~Valuation|FullyQualifiedName~Estimate"
./scripts/Update-TestUiSnapshots.ps1
./scripts/Update-TestUiSnapshots.ps1 -Verify
./scripts/Test-UiCatalogue.ps1
git diff --check origin/dev...HEAD
```

Run genuine corpus-dependent estimate/report tests only when their documented
local evidence is present; otherwise record them as INCONCLUSIVE, never PASS.
All actual external-provider testing is human-owned. Glass's live-user
acceptance is an operator-owned later gate: two supplied
vehicle shapes, Save & Exit to one source-linked Draft, restart/resume,
duplicate callback, two-user isolation and same-account serialization on the
actual HTTPS callback. The PR may be opened with this clearly outstanding, but
must not claim live integration acceptance.

Every review finding receives `fix`, `reject with evidence`, `accepted risk`, or
`defer to named follow-up` disposition. Re-run only checks affected by fixes,
then the required exact-head gate once. Open/update exactly the Stream B PR to
target `dev`, link its owner ticket and the preserved PR 670 evidence, and stop
with the PR unmerged.

## Exact path register

“Reuse” means call/read the existing owner, not permission to edit it. The
[phase-aware file manifest](../registers/file-ownership.csv) controls writes.
These dependencies are explicitly read-only for B:

| Dependency | Writer and required handoff |
| --- | --- |
| Accounts/Edit and Identity secret store | A; link to B's new Administration/Glass page |
| InspectionAddressResolution policy/choices/store | C; B posts the selected source/version through its accepted command |
| CustodyContracts and EfDocumentCustodyStore | A; B calls ICaseArtifactCustody/content ports |
| ApprovedMailboxReportSentEvidence and its EF store | A; B consumes truthful generation-linked evidence |
| OperatorLabels, site.css, site.js, shared evidence widgets | C; B owns case-workspace.css/js and Case partials |
| Catalogue/index, shared browser fixtures, global EF/DI/host files | A during F and serialized later windows; B supplies exact request/fixture |

New B Glass UI files are `src/Pegasus.Web/Pages/Administration/Glass/Index.cshtml`
and `Index.cshtml.cs`; Accounts/Edit entries below are dependency reads only.
The B handoff uses the same ownership and A-authored branch-local registration
protocol. No B worker edits another stream's dependency to unblock itself.

This register resolves every shorthand above. `Reuse` means an existing file;
`new` means the proposed exact path. Foundation paths are read-only to B.

### B01 paths

Inspect the exact PR 670 set below. This is a preservation inventory, not a
grant to edit every listed file. Port only B-owned hunks; send A/C-owned hunks
to their recorded owner. Five additions are classified after the list:
`docs/design/test-ui/catalogue.json`, `docs/design/test-ui/index.html`,
`docs/design/test-ui/pages/case-details--conflict.html`,
`docs/design/test-ui/pages/case-details--default.html`,
`src/Pegasus.Core/Assessment/Valuations.cs`,
`src/Pegasus.Core/Cases/CaseQueries.cs`,
`src/Pegasus.Core/Documents/RequestUploadPolicy.cs`,
`src/Pegasus.Core/Vehicle/VehicleWorkflow.cs`,
`src/Pegasus.Infrastructure/Persistence/AssessmentEntities.cs`,
`src/Pegasus.Infrastructure/Persistence/CustodyEntities.cs`,
`src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs`,
`src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs`,
`src/Pegasus.Infrastructure/Persistence/EfValuationStore.cs`,
`src/Pegasus.Infrastructure/Persistence/EfVehicleWorkflowStore.cs`,
`src/Pegasus.Web/Pages/Cases/Custody.cshtml.cs`,
`src/Pegasus.Web/Pages/Cases/Details.cshtml`,
`src/Pegasus.Web/Pages/Cases/Details.cshtml.cs`,
`src/Pegasus.Web/Pages/Cases/Shared/_CaseDocuments.cshtml`,
`src/Pegasus.Web/Pages/Cases/Shared/_CaseHistory.cshtml`,
`src/Pegasus.Web/Pages/Cases/Shared/_CaseValuation.cshtml`,
`src/Pegasus.Web/Pages/Cases/Shared/_CaseVehicle.cshtml`,
`src/Pegasus.Web/Pages/Cases/Tasks.cshtml.cs`,
`src/Pegasus.Web/Pages/Cases/Valuation.cshtml`,
`src/Pegasus.Web/Pages/Cases/Valuation.cshtml.cs`,
`src/Pegasus.Web/Pages/Cases/Vehicle.cshtml.cs`,
`src/Pegasus.Web/Presentation/OperatorLabels.cs`,
`src/Pegasus.Web/wwwroot/css/site.css`,
`tests/Pegasus.Core.Tests/Assessment/ValuationTests.cs`,
`tests/Pegasus.Core.Tests/Vehicle/VehicleWorkflowTests.cs`,
`tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs`,
`tests/Pegasus.IntegrationTests/CaseCapabilityPagesTestSupport.cs`,
`tests/Pegasus.IntegrationTests/CaseCustodyWebTests.cs`,
`tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs`,
`tests/Pegasus.IntegrationTests/CaseVehicleWebTests.cs`,
`tests/Pegasus.IntegrationTests/DocumentCustodyDurabilityTests.cs`,
`tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`, and
`tests/Pegasus.IntegrationTests/VehicleLookupGapFillTests.cs`.
The PR additions are: port
`src/Pegasus.Web/Pages/Cases/Shared/_CaseValuation.cshtml`; reject the obsolete
standalone `src/Pegasus.Web/Pages/Cases/Valuation.cshtml` and
`src/Pegasus.Web/Pages/Cases/Valuation.cshtml.cs`; Foundation owns the two new
migration files named below. They are not existing B reuse files.
Foundation owns `AssessmentModelConfiguration.cs`,
`CustodyModelConfiguration.cs`,
`Migrations/20260905173354_CaseValuationGuideMonthAndRequestUploadMetadata.cs`,
its `.Designer.cs`, and `Migrations/PegasusDbContextModelSnapshot.cs`, all under
`src/Pegasus.Infrastructure/Persistence/`. B01 adds the one ported _CaseValuation.cshtml; its remaining new schema files belong to F.

### B02 paths

Reuse `src/Pegasus.Core/Cases/CaseDataContracts.cs`, `CaseDataOperations.cs`,
`EngineerNotes.cs`; `src/Pegasus.Core/Assessment/AssessmentContracts.cs`,
`AssessmentPolicy.cs`, `AssessmentOperations.cs`;
`src/Pegasus.Infrastructure/Persistence/EfCaseDataStore.cs`,
`EfCaseAssessmentStore.cs`; `src/Pegasus.Web/Pages/Cases/Details.cshtml`,
`Details.cshtml.cs`, and the exact `_CaseSummary.cshtml`,
`_CaseEngineerNotes.cshtml`, `_CaseInspectionAddress.cshtml`,
`_CaseVehicle.cshtml`, `_CaseDamage.cshtml`, `_CaseSettlement.cshtml`,
`_CaseReport.cshtml`, `_CaseFiles.cshtml`, `_CaseHistory.cshtml`,
`_CaseWorkspaceNav.cshtml`, `_ReadinessHiddenFields.cshtml` files under
`src/Pegasus.Web/Pages/Cases/Shared/`. New:
`src/Pegasus.Core/Cases/CaseWorkspace.cs`,
`src/Pegasus.Infrastructure/Persistence/EfCaseWorkspaceStore.cs`,
`tests/Pegasus.Core.Tests/Cases/CaseWorkspaceTests.cs`,
`tests/Pegasus.IntegrationTests/CaseWorkspacePersistenceTests.cs`. Extend exact
existing tests `CaseDataOperationsTests.cs`, `AssessmentPolicyTests.cs`,
`CaseDetailsWebTests.cs`,
`CaseEditModeWebTests.cs`, `CaseDataCompletenessPersistenceTests.cs`,
`CaseVehicleWebTests.cs`, and
`CaseEngineerSectionsWebTests.cs` in their current test project folders.
Read-only C dependencies: `Core/Address/InspectionAddressResolution.cs`,
`Persistence/InspectionAddressChoicesQueries.cs`,
`Persistence/InspectionAddressResolutionStore.cs` and their policy, persistence
and browser tests. B tests consumption through its Case page; changes to those
dependencies go to C. All shortened paths resolve under the existing project
and exact ownership manifest.

### B03 paths

Reuse `src/Pegasus.Core/Assessment/Valuations.cs`,
`src/Pegasus.Infrastructure/Persistence/EfValuationStore.cs`, and
`tests/Pegasus.Core.Tests/Assessment/ValuationTests.cs`, and the
`src/Pegasus.Web/Pages/Cases/Shared/_CaseValuation.cshtml` ported in B01. New:
`src/Pegasus.Core/Assessment/ValuationCalculations.cs`,
`src/Pegasus.Web/Pages/Administration/ValuationPresets/Index.cshtml`,
`Index.cshtml.cs`, `tests/Pegasus.Core.Tests/Assessment/ValuationCalculationTests.cs`,
`tests/Pegasus.IntegrationTests/ValuationPresetPersistenceTests.cs`,
`ValuationPresetAdministrationWebTests.cs`, and `CaseValuationWebTests.cs`.

### B04 paths

Reuse `src/Pegasus.Core/Assessment/Estimates.cs`, `RepairSpecifications.cs`,
`EstimateImport.cs`; `src/Pegasus.Infrastructure/Persistence/EfRepairSpecificationStore.cs`;
`src/Pegasus.Infrastructure/Assessment/JsonEstimateParser.cs`,
`AudatexEstimatePdfParser.cs`; `src/Pegasus.Web/Pages/Cases/Details.cshtml`,
`Details.cshtml.cs`, `Shared/_CaseEstimate.cshtml`;
and exact existing tests `EstimateTests.cs`, `RepairSpecificationPolicyTests.cs`,
`AssessmentEstimateImportWebTests.cs`, `AssessmentPersistenceIntegrationTests.cs`,
`JsonEstimateParserTests.cs`, `AudatexEstimatePdfParserTests.cs`. New:
`src/Pegasus.Core/Assessment/GlassRepairEstimates.cs`,
`src/Pegasus.Infrastructure/Glass/GlassRepairEstimateGateway.cs`,
`src/Pegasus.Infrastructure/Persistence/EfGlassRepairEstimateSessionStore.cs`,
`src/Pegasus.Web/Pages/Integrations/Glass/Callback.cshtml`,
`src/Pegasus.Web/Pages/Integrations/Glass/Callback.cshtml.cs`,
`src/Pegasus.Web/Pages/Administration/Glass/Index.cshtml`,
`src/Pegasus.Web/Pages/Administration/Glass/Index.cshtml.cs`,
`tests/Pegasus.Core.Tests/Assessment/GlassRepairEstimateTests.cs`,
`tests/Pegasus.IntegrationTests/GlassRepairEstimatePersistenceTests.cs`,
`GlassRepairEstimateCallbackWebTests.cs`, `GlassCredentialAdministrationWebTests.cs`.
A's Accounts pages and protected credential reader/store are read-only
dependencies. C adds the shared navigation entry for B's Glass page.

### B05 paths

Reuse `src/Pegasus.Core/Reports/AssessmentReportProjection.cs`,
`AssessmentReportRendering.cs`;
`src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs`;
`src/Pegasus.Infrastructure/Persistence/EfAssessmentReportProjectionSource.cs`;
the production `docs/design/assets/report-renderer/templates/assessment_report.scriban`,
`assessment_fee_note.scriban`, and `report.css`;
and existing `AssessmentReportProjectionTests.cs`,
`AssessmentReportRenderingTests.cs`, `Reports/AssessmentReportRendererTests.cs`,
`Reports/AssessmentReportDraftWebTests.cs`, `CaseReportApprovalWebTests.cs`. New:
`src/Pegasus.Core/Reports/CaseReportGeneration.cs`,
`src/Pegasus.Infrastructure/Persistence/EfCaseReportGenerationStore.cs`,
`tests/Pegasus.Core.Tests/Reports/CaseReportGenerationTests.cs`,
`tests/Pegasus.IntegrationTests/Reports/CaseReportGenerationPersistenceTests.cs`,
`tests/Pegasus.IntegrationTests/Reports/CaseReportGenerationWebTests.cs`.
The other report template assets are read-only design references with no v1
production caller; do not edit or embed them.

### B06 paths

Reuse `src/Pegasus.Web/Pages/Cases/Custody.cshtml`, `Custody.cshtml.cs`,
`Shared/_CaseDocuments.cshtml`, `_CaseFiles.cshtml`,
`_CaseCorrespondence.cshtml`, `_CaseReport.cshtml`, and existing
`CaseCustodyWebTests.cs`. Read-only A dependencies are `CustodyContracts.cs`,
`EfDocumentCustodyStore.cs`, `DocumentCustodyDurabilityTests.cs`,
`ImageViewingWebTests.cs` and `LocalCaseCustodyAtomicWriteTests.cs`. New:
`src/Pegasus.Core/Documents/CaseAssetPreparation.cs`,
`src/Pegasus.Infrastructure/Persistence/EfCaseAssetPreparationStore.cs`,
`tests/Pegasus.Core.Tests/Documents/CaseAssetPreparationTests.cs`,
`tests/Pegasus.IntegrationTests/CaseAssetPreparationPersistenceTests.cs`.

### B07 paths

Reuse `src/Pegasus.Web/Pages/Cases/Shared/_CaseReport.cshtml`,
`_CaseCorrespondence.cshtml`, `_EvaHandoff.cshtml`,
`src/Pegasus.Web/Pages/Cases/Eva/Send.cshtml`, `Send.cshtml.cs`, and existing
`CaseReportApprovalWebTests.cs`, `EvaSubmissionPolicyTests.cs`,
`EvaSubmissionPersistenceTests.cs`. New:
`src/Pegasus.Core/Reports/CaseReportDeliveryPreparation.cs`,
`src/Pegasus.Infrastructure/Persistence/EfCaseReportDeliveryPreparationStore.cs`,
`tests/Pegasus.Core.Tests/Reports/CaseReportDeliveryPreparationTests.cs`,
`tests/Pegasus.IntegrationTests/CaseReportDeliveryPreparationWebTests.cs`.
Read-only A dependencies are `ApprovedMailboxReportSentEvidence.cs`,
`EfCaseReportSentEvidenceStore.cs`, `ApprovedMailboxReportSentEvidenceTests.cs`
and `SentEvidencePollPersistenceTests.cs`; B tests its real caller against A's
frozen transport and observation contract.

### B08 and B09 paths

B08 edits the exact `Details.cshtml(.cs)`, Case `Shared/` partials named in
B02/B04/B06/B07 and its three `pages/case-details--*.html` snapshots. It owns
`src/Pegasus.Web/wwwroot/css/case-workspace.css` and
`src/Pegasus.Web/wwwroot/js/case-workspace.js` for Case-only behavior. Extend
only B-owned tests from this existing verification set:
`CaseDetailsWebTests.cs`, `CaseEditModeWebTests.cs`,
`CaseEngineerSectionsWebTests.cs`, `TestUiSnapshotTests.cs`,
`TestUiFocusedRenderTests.cs`, `Browser/LayoutIntegrityTests.cs`,
`Browser/AssessmentReadinessSummaryBrowserTests.cs`, and
`Browser/InspectionAddressChoiceBrowserTests.cs`. The ownership manifest assigns
shared harness/layout/address tests to A/C; run them read-only and send requested
changes to their owner. C's `site.css`/`site.js` and A's Test UI catalogue/index
are also read-only dependencies; A assembles the final catalogue. B09 modifies
no file: it reads every path above plus `Pegasus.slnx`, `Directory.Build.props`,
changed `packages.lock.json` files and Foundation's exact migration/snapshot/DI
diff, then records review only in the owning ticket/PR.

## Ticket-by-ticket residual acceptance

This table is additional required scope in the named step, not a separate PR
or licence for adjacent cleanup. Read each linked ticket’s current body/gates.
The current reason overrides stale inherited ticket wording; verify already
integrated clauses and implement only the remaining gap.

| Ticket | Step | Exact residual / acceptance |
| --- | --- | --- |
| AUTO-015 | B03 | Prevent generic automation assessment updates from clearing or overwriting valuation-owned Engineer Value. Enforce the existing field owner in Core, with equivalent Web/MCP tests. |
| AUTO-016 | B04 | Expose one raw-estimate import use case shared by the Case page and MCP; retain source version, idempotency and edit authority. |
| CASE-001 | B02 | Create and intake redirects write an unread notice. Use the existing notice mechanism or stop writing the dead value after the destination is corrected. |
| CASE-009 | B02 | Auto-attached Query correspondence is implemented. Verify current Case navigation and exact report/reply association; remove stale claims of manual query creation. |
| CASE-012 | B08 | Retire superseded workspace design after comparing preserved evidence; the approved v3 is implemented by B02/B08, not the old shell. |
| CASE-020 | B02 | Headers and queues must read the current Case projection, not the original intake draft, or corrected values appear stale. |
| CASE-023 | B02 | Workflow events and notes can compete for a version slot. Prove the concurrent sequence using the real persistence owner; retain both accepted events. |
| CASE-024 | B02 | Case lease renewal exists. The request for a separate Assessment edit mode is superseded by the single Case workbench. Verify lease loss, multi-form saving and forced clearance together. |
| CASE-025 | B08 | The queue shell is merged. Queue projections and Awaiting instruction changed later; verify the final combined page rather than its old snapshot alone. |
| CASE-027 | B02 | Vehicle, address, files and notes sections exist. Verify the current single-scroll callers and exclude later missing image/editor features from this ticket's completion claim. |
| CASE-028 | B02 | Three concerns are bundled: Case timeline, admin Action Logs and navigation counts. Keep the timeline here, query/report UI in PLAT-051 and count-efficiency work with PLAT-063. |
| CASE-029 | B01 | PR 670 remains open. Review source-labelled DVLA/MOT suggestions and valuation ownership against the current Case page; do not describe its work as already merged. |
| CASE-030 | B07 | Bind actual Sent evidence to immutable report generations and implement Return to Engineer without losing issued history; A03 provides staff send and observation. |
| CASE-033 | B02 | Workflow/Closure are POST handler pages, so blank GET pages alone do not prove commands are unreachable. Redirect or reject GET and separately wire the missing Engineer finding caller. |
| CASE-034 | B08 | Apply principal filtering to all Case queues; consume C07 pre-case/Triage projections and explicit unknown-principal state. |
| CASE-035 | B02 | The Case Files Operations link uses an unresolvable page name. Correct the known destination and add that real section state to browser verification. |
| CASE-036 | B02 | A stranded branch is evidence, not current design authority. Compare surviving one-list changes against CASE-038/ENG-034; retire already-replaced ideas and retain only demonstrated gaps. |
| CASE-038 | B02 | The frame is implemented, but its multiple editable forms expose a reproduced last-form-only save defect. Verify all section combinations before release. |
| CASE-039 | B02 | Append-only Engineer notes are present. Verify attribution, case access and note/workflow concurrency rather than converting them to mutable generic Notes. |
| CASE-040 | B05 | Use the configurable signatory tuple for actual UI/server report readiness and immutable generation; A01 owns account identity. |
| CASE-043 | B02 | Use instruction facts first and source-labelled DVLA/DVSA suggestions. These APIs do not supply every proposed VIN/transmission field; specify absence rather than inventing values. |
| CASE-044 | B02 | Provide reverse Add evidence from a real Case, including absorption of existing pre-case material. Reuse association/custody owners and preserve source occurrences without duplicate bytes. |
| CASE-046 | B02 | Hidden-field spelling is not the main defect: callers trust posted readiness booleans. Derive completeness from persisted facts under the mutation boundary before reopen/return/assignment/handoff. |
| DOCS-001 | B05 | Existing preview renderer/use case is reusable. Generate must retain immutable report artifacts and source versions; current draft download is not final report generation. |
| DOCS-012 | B06 | Evidence view is merged. D44 supersedes remaining staff-review checkboxes; preserve the file/source view without restoring the internal custody ledger to ordinary operators. |
| DOCS-014 | B05 | Preview must not be recorded as a completed download. Use truthful view/download-request semantics and avoid permanent per-frame browsing events. |
| DOCS-016 | B07 | Add the missing assertion using existing genuine export evidence; an emitted trace is not a passing mapping assertion. |
| DOCS-018 | B05 | Fee-note preview belongs in Report with the existing fee data and renderer. This ticket does not authorize sending, accounting automation or a second fee store. |
| ENG-019 | B07 | Keep live EVA credential swap as an operator-only optional gate; verify the manual optional handoff without performing credential writes. |
| ENG-020 | B07 | Record unavailable EVA instruction fields honestly and preserve the optional manual handoff; do not send the historical vendor request or invent supported fields. |
| ENG-021 | B07 | Fix/test manual EVA cancellation, lease-loss, database-fault and unknown-outcome residuals; remove obsolete automatic-path activation claims and defer autonomous activation. |
| ENG-024 | B07 | Unknown delivery currently remains retryable despite explicit operator direction. Persist uncertainty and require reconciliation; never blindly repeat a possibly accepted EVA submission. |
| ENG-025 | B02 | Verify the replaced Assessment shell and retire obsolete requirements; the one Case workspace remains authoritative. |
| ENG-027 | B03 | Source-separated Case valuations exist. Glass valuation is distinct from the requested Glass repair-estimate launch; missing adjustments remain TICK-083. |
| ENG-028 | B04 | The multi-estimate editor exists, but totals render as literal text and multi-form saves fail. Raw imports and external AI transport have separate owners. |
| ENG-029 | B02 | Complete Settlement and Report editors against existing Core fields and readiness rules. Wire final Engineer decisions and show field provenance without invented guidance copy. |
| ENG-031 | B06 | Support crop/order and explicit report image selection with source/version retention. Reuse the image store/viewer and keep originals intact. |
| ENG-032 | B04 | Use the supplied Audatex full-report variants, preserving operations and totals. Genuine evidence is authorized by D43; do not invent a synthetic-only testing requirement. |
| ENG-033 | B04 | The owning UI is now the Case Estimate section. Move shared raw import out of the retired Assessment PageModel and reuse it for Glass XML/PDF and MCP. |
| ENG-034 | B02 | Workbench handlers moved into Case, but moving them did not complete report generation or correct save behavior. Verify actual section callers and remove only superseded routes. |
| ENG-036 | B02 | Implement detailed damage zone/severity/note and tyres/restraints without superseded Type; B05 prints the same accepted data. |
| ENG-037 | B05 | Use invariant parsing for the named assessment/report date inputs. Keep user-facing British display separate from transport parsing. |
| ENG-038 | B05 | Remove retired D18 Engineer name/qualifications/signature readiness requirements; the Case Sign-off Engineer tuple is the sole current owner. |
| ENG-039 | B04 | Two Razor locations emit RenderEstimateTotals(totals) as literal text. Correct invocation and inspect repair/total-loss totals in the rendered page. |
| ENG-040 | B08 | Use the current Case section URL. The old route redirects, so this is stale navigation rather than a complete loss of access. |
| INTK-026 | B02 | Retain original mileage/unit/source and exact km-to-mile normalization once; C02 extracts, B02 owns Case value/display and B07 optional export. |
| PLAT-025 | B02 | Workflow Configuration is now an empty page after review flags were removed. Populate only real completeness/chase/rate settings; do not restore obsolete review switches. |
| PLAT-057 | B07 | The no-coverage claim predates later custody/EVA tests. Identify remaining claim, lease-loss and uncertain-outcome branches, then add only missing behavioral tests. |
| PLAT-062 | B02 | Populate supported instruction/image completeness and manual chase-interval settings; no switch may waive principal/custody/immutable-reference invariants. |
| PLAT-072 | B02 | Remove residual staff-confirmation checkboxes/ConfirmedByStaff fields and derive real completeness server-side; F schema and C intake callers agree. |
| TICK-055 | B07 | Keep post-report query/dispute on the existing Case and exact issued version; support explicit Return to Engineer, correction and staff-authored response preparation through A03, preserving prior Sent evidence. |
| TICK-076 | B05 | D02 is resolved: complete engineering and final reports in Pegasus, with optional EVA. Unretained draft or EVA-only alpha is insufficient. |
| TICK-078 | B02 | Complete actual Case assignment/sign-off selection under current role rules; optional EVA consumes that record, no duplicate assignment subsystem. |
| TICK-079 | B04 | Track complete repair-estimate workflow across existing editor, raw import and Glass launch. Core keeps accepted repair specification authority; close constituent work separately. |
| TICK-080 | B03 | Case valuation records are merged. Retain only missing source observations/selection/adjustment behavior; do not repeat the existing valuation data model. |
| TICK-081 | B05 | Renderer integration exists, but current Generate returns an unretained draft. Finish accepted-data validation and immutable artifact generation through the current caller. |
| TICK-082 | B04 | Versioned estimates exist; global labour-rate administration and explicit per-version VAT/rate evidence are real remaining gaps. Reuse estimate ownership and freeze accepted version inputs. |
| TICK-083 | B03 | Preserve dated valuation sources, Engineer selection, explicit adjustments/rationale and revision history. Prevent generic AI updates from bypassing this owner. |
| TICK-084 | B05 | Include versioned agreed fee/fee-note inputs and restricted visibility now; full invoice/payment/accounting automation remains deferred. |
| TICK-085 | B04 | The new requirement exceeds PDF import: per-engineer credentials, Case button, durable launch/callback, validated XML/PDF return, canonical draft estimate and resume/recovery. Reuse spike evidence, not its whole CLI. |
| TICK-092 | B05 | Core already owns case/assessment/estimate/valuation records. Finish missing accepted snapshot fields and callers; avoid a new all-purpose report DTO framework or second policy engine. |
| TICK-094 | B02 | Wire final value/outcome/salvage/roadworthiness through named Engineer authority; B03 owns valuation acceptance and B05 snapshot derivation. |
| TICK-095 | B08 | Residual future workbench is not executable scope now that named Case tickets exist. Link remaining accepted gaps and archive redundant wording. |
| TICK-096 | B05 | The deterministic renderer exists. Audit formula/wording coverage and finish missing outcome fields; do not reintegrate retired workspaces or render both PDFs on every preview. |
| TICK-097 | B05 | Prove the four supported assessment outcome variants, itemized repair specification and fee note from one accepted snapshot. Missing approved wording remains a release evidence gate. |
| TICK-208 | B07 | Retain original Sent generation/chain through query/reopen/correction; A03 observes new explicit staff send without erasing prior evidence. |
| TICK-223 | B07 | Retain real static href targets and enhance with the shared dialog script. Do not replace all dialogs to solve the specific script-off EVA access gap. |
| UIIMP-014 | B08 | Snapshot and browser coverage must include all single-scroll sections, multiple dirty forms, report readiness/totals, lease loss and admin destinations. Existing snapshots missed concrete defects. |

## Casework review dispositions

| Finding | Final disposition |
| --- | --- |
| B01 | Fixed: JSON editable/new/read-only lists, dedicated Glass admin, paired callback and phase-aware authoritative file manifest agree. Cross-owner reuse is read/call only. |
| B02 | Resolved with explicit phase exception: F02 implements/tests the small shared admin-clear primitive before B begins; B owns later workflow changes and regression, A its UI caller. The proposed alternative of leaving F with an absent implementation is rejected. |
| B03 | Fixed: actual Case launch/resume/admin/callback handlers and paired Razor route, normalized account uniqueness, encrypted session ownership, stale-lease awaiting-import recovery and tests are named. |
| B04 | Fixed: routed generate/reopen/prepare/send journey uses actual local renderer and confirmed immutable artifacts; SQL transaction does not span rendering/Box. |
| B05 | Fixed: C S05 directory query is authoritative; B has no broader source ranking/inference. |
| B06 | Fixed: all inspection fields, existing sign-off resolution order and editable copied preset amount are explicit acceptance. |
| B07 | Fixed: only two actual production Scriban templates and CSS are edited; unused assets are read-only references. |
| B08 | Fixed: exact PR670 source SHA and final hunk proof pinned. |
| B09 | Fixed: B01 ports _CaseValuation once, B03 extends it; one owner/path. |



## Stop condition

All assigned implementation, independent review, standalone and combined checks are complete; exactly three replacement PRs target dev, open and unmerged. No merge, deployment, reset or live provider write. External provider/workload evidence remains honestly named operator gates, never fabricated PASS.
