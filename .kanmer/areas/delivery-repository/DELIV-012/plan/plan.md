# Plan — DELIV-012, release 12

Authority: the five operator answers recorded in `open-questions/open-questions.md`
(2026-08-19). They enlarge the release beyond what is on `dev` today: four
in-flight PRs are taken over and finished, three orphaned surfaces are made
live, and Sent-evidence polling is approved for the production mailbox.

Baseline at planning time: `origin/main` = `d8de29cb` (release 10, deployed
2026-08-18 14:22–14:26 UTC, migration head `20260814094632_DropBoxFileRequests`);
`origin/dev` = `560f741c`, 42 commits / 12 merge-PRs ahead, three migrations
pending, `infra/**` and `azure.yaml` unchanged. Evidence in
`research/current-estate.md` and `research/codebase-evidence.md`.

## 0. Standing rules for every step

- One branch, one worktree, one PR per unit of work; branch `task/<slug>` cut
  from `origin/dev` into `../pegasus-worktrees/<slug>`, upstream unset after
  branching. The four taken-over INTK branches keep their existing names
  (`intk-00N-…`) because their PRs already point at them.
- **Never** touch `.worktrees/kanmer`, the `kanmer-board` branch, `dev`/`main`
  history, or another worktree's uncommitted files. `.codex/config.toml` in the
  main checkout and `CONTEXT.md` in `.worktrees/intk-008` are not ours.
- Every code PR runs the simplification pass over its own diff before review and
  records findings + dispositions in this plan's "Simplification pass" section
  or the owning ticket's plan.
- Read-only Azure/SQL checks need no approval. Every write is requested
  explicitly for exact targets. `MERGE AUTH GRANTED` is requested immediately
  before the `main` update and for that exact SHA only.
- A step whose premise is a fact about production is checked read-only before it
  runs, not argued.

## 1. Wave A — defects on merged `dev` (start immediately, parallel)

These are independent of the INTK takeover and each becomes its own ticket,
branch and PR into `dev`.

### A1 — Runtime-role grants for `CaseRepairSpecifications` (blocker)

`20260819112640_VersionedRepairSpecifications` creates the table with no GRANT,
while `EfCaseAssessmentStore.SaveAsync` (`src/Pegasus.Infrastructure/Persistence/EfCaseAssessmentStore.cs:117-135`)
SELECTs and INSERTs it on the live Web assessment-save path. Under per-table
least privilege the first production assessment save with estimate lines fails.
Verified read-only: production has 0 `CaseEstimateLines` rows, so the migration's
backfill is a no-op and only the runtime path is at risk.

Fix: a new migration granting `pegasus_web_runtime_role` SELECT, INSERT, UPDATE
on `CaseRepairSpecifications` with DELETE denied, following the convention in
`20260819104953_MailClassificationCorrectionHistory.cs:100-105`; extend the
expected census in `scripts/Invoke-AzureDatabaseBootstrap.ps1` and the census
test so a future missing grant fails CI (today a table with no grants appears in
neither the expected nor the actual set, so nothing catches it).

### A2 — Give `MailOperationalDestinationPolicy` a real caller

`src/Pegasus.Core/Intake/Classification/MailOperationalDestinationPolicy.cs:24`
is referenced only by its unit test. Wire it into the live classification path
so the operational destination it computes is what the Worker/Web actually use,
replacing whatever inline mapping exists there (one list per concept — the
policy becomes the single owner). Behaviour must not change for any category
already mapped; where it would, that is a finding to record, not a silent change.

### A3 — Register `IRepairSpecificationStore` behind a real caller

`RepairSpecifications.cs:210` / `EfRepairSpecificationStore.cs:10` are used only
by `AssessmentPersistenceIntegrationTests`. Register the store in DI and route
the assessment path's repair-specification reads/writes through it instead of
`EfCaseAssessmentStore` reaching into `context.CaseRepairSpecifications`
directly, so the abstraction has a production caller and one owner.

### A4 — Make the report renderer live (absorbs PLAT-007 and DOCS-001)

Three parts, in order, because each is a stop condition for the next:

1. **Container layer.** The image is produced by `dotnet publish /t:PublishContainer`
   (`scripts/Build-ReleaseArtifacts.ps1:60-66`) onto the default
   `mcr.microsoft.com/dotnet/aspnet` base; there is no Dockerfile and no Docker
   on this workstation, and `az acr build` is prohibited by the runbook. So the
   route must be a `ContainerBaseImage` that already carries Chromium and its
   native dependencies, plus the browser binaries resolved at runtime. The first
   task is to **determine and prove** the workable base/route locally (build the
   OCI archive, inspect it with `oras`, run the container and render a document)
   before anything else in A4 proceeds. If no route works without Docker or a
   prohibited command, that is a stop condition and goes back to the operator
   rather than being worked around.
2. **Operator entry point.** A real, navigable action that renders a report
   draft from an accepted assessment — the renderer stops being dark only when a
   person can reach it. Placement follows `docs/design/README.md` and the
   existing page conventions; no new gate, no inline styles.
3. **Evidence.** The rendered output is verified in the deployed container, not
   only locally, during §6 verification.

### A5 — Current-state documentation drift

`docs/current-architecture.md:85` still says the `/Inbox/{id}` page "carries no
handler" and the Web role "holds SELECT alone", which TICK-046 made false.
`docs/operations.md:278` says the Web runs "min 0 max 1 replica"; Bicep and the
live app both say min 1. Both corrected here rather than in the release docs
refresh, so §7 only has to add the release row.

## 2. Wave B — take over and finish the four INTK tickets

Authorised by Q1. Each ticket is force-taken (the claim record is how the other
agent sees the takeover), a scratch note states why, and the work continues on
the existing pushed branch. Their checklists are the definition of done, not the
PR being open.

| Ticket | PR | Branch | State inherited | Work |
|---|---|---|---|---|
| INTK-005 | #416 | `intk-005-grouped-upload` | checklist 7/33, CI red: 7 real failures | Fix the failures (expected-migration list, receipt token `:0` suffix, `/Upload/Group/` redirect, empty-file validation message, two Guid-null Qdos tests); add grants for `IntakeSubmissionGroups` and `IntakeSubmissionGroupMembers`; finish the checklist |
| INTK-006 | #417 | `intk-006-grouped-image-routing` | checklist 26/41, stacked on #416, no CI runs on head | After #416 lands: merge `origin/dev`, resolve the `IntakePersistenceIntegrationTests` migration-list conflict, trigger CI, finish the checklist. Must agree with Q2: readable VRM matching an existing case attaches images as evidence; no match creates an Image-initiated Case |
| INTK-008 | #423 | `intk-008-image-initiated-lifecycle` | checklist 8/29, CI red: `ImageIntakeWebTests.cs:75` asserts superseded wording; one suspected concurrency flake | Fix the assertion, re-run the flake to characterise it, add grants for `ImageIntakeLifecycleEvents`, rewrite the operator-notes edit per Q2 (state **both** branches; keep the existing definitive-match sentence), finish the checklist |
| INTK-007 | #424 | `intk-007-unidentified-intake` | checklist 22/36, forked from `main` (42 behind), no CI runs at all | Rebase/merge onto `dev`, resolve the `docs/capabilities.md` conflict, add grants for `UnidentifiedItems`/`UnidentifiedSequences`/`UnidentifiedHistory`, keep the operator-notes section as written (Q3 confirmed) and update the `CLAUDE.md` `Needs sorting` invariant, run CI, finish the checklist |

Q2's clarification is a **product** correction, not just wording: INTK-006 and
INTK-008 must both express that a readable VRM matching an existing case routes
the images to that case as evidence, and only a non-matching readable VRM
creates an Image-initiated Case. Any code or FRD text that contradicts this is a
defect to fix inside those tickets.

## 3. Wave C — production data write approved by Q4

Set `AllowSentEvidence = 1` for `instructions@collisionengineers.co.uk` in
`ApprovedMailboxes` (production), together with whatever Sent folder identity
the policy requires — `SentFolderIdentity` is currently NULL while
`ApprovedSentPollStates` already holds a Sent folder identity and a Graph
cursor, so the exact required shape is read from the policy code first and the
write is composed to satisfy it. Applied during the release window (§6) so the
result can be observed in the same post-deploy watch: the once-a-minute
`UnauthorizedAccessException` must stop and `ApprovedSentPollStates` must
advance. This is the only pre-approved data write; it is still stated to the
operator with its exact statement before execution.

## 4. Merge order into `dev`

Ordered so that no rebase throws away another's work and each conflict is
resolved once. After every merge, the next branch merges `origin/dev` before its
CI runs; nothing is force-pushed and nothing is rebased across a pushed history
that another PR depends on (#417 depends on #416).

1. **#422 TICK-045** — 12/12, clean against `dev`, single SQL-timeout flake.
   Re-run, merge first; it removes a `docs/capabilities.md` overlap early.
2. **Wave A PRs** — A1 first (grants; smallest and a blocker), then A5 (docs),
   then A2, A3, then A4 (largest, most likely to need a second pass).
3. **#416 INTK-005** — after its failures are fixed; it is #417's base.
4. **#417 INTK-006** — merges `dev` (migration-list conflict only).
5. **#423 INTK-008** — merges `dev`; operator-notes rewritten per Q2.
6. **#424 INTK-007** — last: stalest base, widest doc surface, conflicts with
   #423 in `frd-02`/`frd-12` and with `dev` in `capabilities.md`, and it carries
   the `CLAUDE.md` invariant change.

Rule at each step: merge only on green CI for that branch's own head, with the
independent review recorded. Six new migrations will exist by the end
(`101344`, `112914`, `115323` from the INTK work plus the three already on
`dev`); EF applies every unapplied id in order, so the interleaved ids are
harmless, but `dotnet ef migrations has-pending-model-changes` runs after each
merge because each open PR's model snapshot was scaffolded against an older
model and a textual auto-merge does not prove the model matches.

## 5. Wave D — git hygiene (after every PR is merged, before the release)

Target end state: remote `main`, `dev`, `kanmer-board`; local `main`, `dev`,
`kanmer-board`; worktrees = main checkout + `.worktrees/kanmer`.

1. Verify each branch is contained in `origin/dev` (`git branch --merged origin/dev`
   and `git log origin/dev..<branch>` empty) before deleting anything.
2. Remove worktrees: `../pegasus-worktrees/{deliv-011-release-11, plat-006-shell-upload,
   tick-033, tick-043-mailbox-identity, tick-044-classification-catalogue,
   tick-045-shared-classification-policy, tick-046-classification-history,
   tick-093-versioned-repair-spec}` plus every worktree this ticket creates, and
   `.worktrees/{intk-005,intk-006,intk-007,intk-008}`. `.worktrees/intk-008`
   holds an uncommitted `CONTEXT.md` that is not ours — its content is captured
   into the INTK-008 ticket before the worktree is removed, and nothing is
   discarded silently.
3. Delete the local branches, then the remote ones, then `git fetch --prune` and
   `git worktree prune`.
4. `task/deliv-011-release-11` has no upstream and is fully contained in `dev`;
   it goes with the rest. Its release artefacts are already superseded.
5. Confirm no PR is left open and every closed PR shows MERGED.

## 6. Wave E — release 12

Route per `docs/runbook.md` § Deployment and release, as executed for releases 9
and 10. Run from a clean release worktree at the promoted SHA.

| # | Step | Kind | Stop condition |
|---|---|---|---|
| E1 | `gh pr checks` green on the final `dev` head via PR #410; re-title it for release 12 | read | any lane not SUCCESS |
| E2 | Preflight: `git fetch`; `main` is an ancestor of `dev`; `SHA=$(git rev-parse origin/dev)`; equals PR #410 head | read | mismatch |
| E3 | **Request `MERGE AUTH GRANTED` for that exact SHA**, then `git push --atomic --force-with-lease=refs/heads/dev:$SHA origin $SHA:refs/heads/main $SHA:refs/heads/dev` | **write (git)** | any rejection — never force, never rewrite |
| E4 | Watch the `main` push run including the history guard (`scripts/Test-MainBranchHistory.ps1`) | read | guard fails |
| E5 | Build: `dotnet restore`/`build -c Release`, `Test-AzureDeploymentPlan -Mode Local`, `Build-ReleaseArtifacts.ps1 -Version 0.1.0-alpha.1 -SourceRevision $SHA`, `-Mode Artifact`, record manifest SHA-256 | local | any failure |
| E6 | `azd env refresh -e pegasus-prod`; `-Mode PreUpload` | read | missing outputs |
| E7 | Read-only SQL preflight re-run (duplicate canonical Message-IDs; estimate-line counts; migration head) | read | non-empty duplicate set |
| E8 | `oras cp` the digest-pinned image to `pegasusprodacr252ow37gij`; verify the ACR digest equals the manifest digest | **write (ACR)** | digest mismatch |
| E9 | `-Mode PreMigration`, then `efbundle` applying the six pending migrations from `src/Pegasus.Web` with the Production process environment; readback `__EFMigrationsHistory` head | **write (SQL schema)** | any migration error |
| E10 | `Invoke-AzureDatabaseBootstrap.ps1` (manifest-SHA gated) — asserts the full grant matrix including the new tables | **write (SQL roles)** | census mismatch |
| E11 | Apply the Q4 Sent-evidence approval (§3) | **write (SQL data)** | policy shape unclear → ask |
| E12 | `azd env set` digest/suffix/activation; `-Mode PreProvision`; `azd provision --preview` | read/what-if | anything beyond the new Web revision |
| E13 | `azd provision` | **write (ARM)** | provisioning error |
| E14 | Worker `az functionapp deployment source config-zip` (never `azd deploy --from-package`) | **write (Function App)** | deployment status ≠ success |
| E15 | `Invoke-ProductionSmoke.ps1` with `-ExpectedSourceRevision $SHA -ExpectedWorkerActivation approved-live-worker` | read | non-zero exit |

Each Azure write is requested with its exact target immediately before it runs:
ACR `pegasusprodacr252ow37gij`; Container App `pegasus-prod-web-252ow37gij` via
`azd provision`; Function App `pegasus-prod-worker-252ow37gij`; SQL
`pegasus-prod-sql-252ow37gij/pegasus`; all in `rg-pegasus-prod`, subscription
`e6076573-23a5-46a8-acef-7e22d264e5db`.

Rollback: the previous digest `sha256:4bd50f66…` and revision suffix
`d8de29cb94f3` re-provision the last good Web revision; the release-10 worker
package redeploys by config-zip. The migrations are additive; the
`CaseEstimateLines` backfill is a no-op on production data.

## 7. Verification and closeout

- **Backend**: `/health/live`, `/health/ready`, `/diagnostics/version` reporting
  the promoted SHA; anonymous `/Cases` → https 302; migration head equal to the
  release manifest's; nine-function activation census; Worker package deployment
  active and successful; `ApprovedInboxPollStates.LastCompletedAtUtc` advancing
  after the deploy; the `SentEvidencePollFunction` exception stream stopped
  (Q4's whole point) — checked against App Insights inside the ingestion window,
  because the Log Analytics daily cap trips around 11:50 UTC.
- **UI (browser)**: signed-in checks of the shipped visual work — the centred
  shell and redesigned Upload (PLAT-006), the multi-file upload group flow
  (INTK-005), the Unidentified queue and its U-references (INTK-007), the
  Image-initiated Case pages (INTK-008), the Inbox classification correction
  (TICK-046), and the new report-draft entry point (A4) actually producing a
  document in the deployed container.
- **Docs refresh in the same release**: `docs/operations.md` release-12 row
  (date, SHA, digest, revision, the six migrations), "serves release 12", what
  the release proved, the renderer's changed status; `docs/current-architecture.md`
  release sentence. Merged before the ticket leaves review.
- **Proof**: `proof/proof.md` is the successful deployment — the promotion
  output, the artefact manifest identity, the migration transcript and head, the
  provision and worker deployment results, the smoke output, and the browser and
  endpoint verification above. Written on merged `main`, after the deploy.
- **Board**: every ticket whose proof depended on this deployment
  (PLAT-006, TICK-043, TICK-044, TICK-046, TICK-093, TICK-045, SIMPLI-014,
  PR-009, INTK-005/006/007/008 and the Wave A tickets) moves one gated stage at
  a time to done with its own proof; `DELIV-011` stays archived as superseded.

## 8. Simplification pass

Run per code PR over that PR's own diff before its review, with findings and
dispositions recorded against the owning ticket. Recorded here as they complete.

---

## 9. Plan revisions — 2026-08-19, during execution

The plan above was written before the tickets/PR-comment research finished and
before the lanes reported. Four things changed; recorded here rather than by
rewriting the plan, so the reasoning stays auditable.

### 9.1 A new blocker, and it is already live in production

`scripts/Test-MigrationGrants.ps1` (built in Wave A to stop the missing-GRANT
class recurring) immediately caught a case nobody was looking for:
`20260811122654_CaseCustodyEvaRecovery.cs` creates `EvaHandoffDownloadOperations`
with no grant anywhere in the tree, while `EfHandoffStore` reads it
(`EvaHandoffStore.cs:194`) and inserts into it (`:272`) from
`Pages/Cases/Vehicle.cshtml.cs`.

I verified this against the **production** database read-only rather than
reasoning about it:

| Table | `pegasus_web_runtime_role` | `pegasus_worker_runtime_role` |
|---|---|---|
| `EvaHandoffOperations` | GRANT SELECT, GRANT INSERT, DENY DELETE | DENY DELETE |
| `EvaHandoffRevisions` | GRANT SELECT, GRANT INSERT, DENY DELETE | DENY DELETE |
| `EvaHandoffDownloadOperations` | **no permission rows at all** | **none** |

That migration is already applied (production head is the later
`20260814094632`), so the EVA hand-off download path fails with a SQL permission
error **in the currently deployed release 10** — this is a pre-existing live
defect, not a release-12 risk. It cannot be fixed by editing the applied
migration; a new follow-up migration is added on the Wave A grants branch and
ships with release 12.

### 9.2 TICK-045 is not the cheap first merge the plan assumed

§4 ordered #422 first as "12/12, clean, single flake". The research showed the
PR contains **no production code at all**: it adds one integration test that
seeds a fabricated `MailClassificationResult` (policy key `"shared-mail-policy"`,
version `9` — a literal no policy emits) and never invokes a classifier, plus a
capability-note upgrade that this evidence cannot support, plus a fabricated
mailbox address `claims@collisionengineers.co.uk` which is outside the four
documented identities and trips the repository's "never fabricate domain emails"
rule. Its 12/12 checklist is not earned.

So #422 is no longer first. It now carries the real MAIL-02 caller work: the
lane surfaces `MailOperationalDestinationPolicy` on the retained mailbox viewer
(`/Inbox/{id}`), which is the caller TICK-044's own `open-questions` records the
operator asking for — *"the retained mailbox viewer is meant to show this
information… A policy referenced only by tests is incomplete and must not pass
review as delivered."* The lane's first answer, that no consumer exists because
the categorised queue UI is capability UI-14 and unscheduled, was correct about
UI-14 and wrong about the viewer.

### 9.3 Revised merge order

1. **Wave A grants + CI guard + docs** (`task/deliv-012-grant-and-docs-fixes`) —
   contains both the TICK-093 blocker and the newly found live EVA defect, and
   the guard that stops the class recurring. Earliest, because everything else
   inherits the guard.
2. **PR #425 repair-specification store wiring** — reviewed, independent of the
   above (the migration file is untouched by it).
3. **#422 TICK-045** — once it carries a real caller and honest wording.
4. **Renderer container** and **report-draft entry point** — the two halves of
   the operator's "make the renderer live" decision; container first, since the
   entry point is pointless if no route to a Chromium-capable image exists.
5. **#416 INTK-005**, then **#417 INTK-006** (stacked on it), then **#423
   INTK-008**, then **#424 INTK-007** last — unchanged from §4, and still last
   for INTK-007 because it owns the `Needs sorting` → `Unidentified` vocabulary
   migration that must land after every other branch has stopped adding
   references to the old term.

### 9.4 Vocabulary sequencing across three lanes

`MailOperationalDestination.NeedsSorting` is an enum member that the TICK-045
lane makes reachable from a real page for the first time, exactly as INTK-007
retires that vocabulary. Resolution: TICK-045 does **not** rename it and adds no
new operator-visible "Needs sorting" copy, taking its label from
`OperatorLabels` instead; it reports every location it makes reachable; INTK-007,
merging last, completes the rename against that list together with the three
surviving literal mentions in `docs/operator-notes.md` (lines 42, 199, 388) and
the `CLAUDE.md` product invariant. The operator confirmed the replacement, so
`Mail/Message.cshtml.cs:114`'s mapping of `NeedsSorting` to `Unidentified` is
correct rather than a defect.

### 9.5 Git hygiene brought forward

§5 placed hygiene after every merge. The seven already-merged worktrees were
removed early instead, because the workstation had only 7.0 GB free and the
container work in A4 needs room for a 1.32 GB base image plus an OCI archive.
Each was verified `0` commits ahead of `origin/dev` and clean first; free space
went to 28.1 GB. Their local and remote branches were deleted at the same time,
along with a stray `pr417check` review branch. What remains for §5 is the five
open-PR branches plus this ticket's own working branches.
