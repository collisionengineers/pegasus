# Research — PLAT-046: migration containment before a destructive schema change

## Question

What is the smallest current release-control sequence that prevents both the
already-deployed Web revision and the already-deployed Worker from reading a
column while a migration drops it, without changing package-before-migration
order or inventing a new deployment framework?

## Findings

- The incident record correctly describes a Worker timer reading a newly added
  column before the old release procedure applied its migration. Its old
  acceptance criteria also require a Worker-side per-tick schema check.
  Source: ticket body and `plan/plan.md`.
- That release-order defect has since been corrected in the current canonical
  release skill: section 6 requires migration and runtime grants to finish
  before provisioning Web or deploying Worker. This protects a **new**
  package from a new additive schema change; it does not by itself stop an
  **old** Web revision or old Worker package from reading a column that a
  destructive migration removes. Source:
  `.agents/skills/pegasus-release/SKILL.md` sections 6–8.
- The Worker has one all-or-nothing deployment control already: Bicep derives
  `workerActivationApproved` from the exact
  `workerActivation == 'approved-live-worker'` value and renders the same
  fail-closed expression for seven existing
  `AzureWebJobs.<function>.Disabled` settings. The seven setting names are
  independently censused by `scripts/Test-AzureDeploymentPlan.ps1` and
  `scripts/Invoke-ProductionSmoke.ps1`. Source:
  `infra/modules/platform.bicep` lines 54–55 and 667–673;
  `scripts/Test-AzureDeploymentPlan.ps1` lines 32–40 and 241–251.
- Re-provisioning merely to turn that control off before migration is not the
  preferred containment action. It can update the Web/infrastructure surface
  before the database is current, while the current release skill's
  PreProvision invocation hard-codes the approved-live Worker expectation.
  Therefore it is not a narrow migration-containment operation. Source:
  `.agents/skills/pegasus-release/SKILL.md` section 7 and
  `scripts/Test-AzureDeploymentPlan.ps1` PreMigration/PreProvision logic.
- The current Bicep `webActivation` condition controls resource creation; it
  is not an explicit runtime-quiescence primitive. It must not be treated as a
  safe way to contain the old Web revision. Source:
  `infra/modules/platform.bicep` `webActivationApproved` declaration and
  the conditional Container App validation in
  `scripts/Test-AzureDeploymentPlan.ps1`.
- Azure Container Apps documents revision deactivation as shutting down the
  revision's containers. Thus direct deactivation of the exact inventory-read
  active Web revision is a concrete containment primitive, but it causes a
  deliberate Web availability interruption until the new revision is active.
  Sources: [Azure Container Apps lifecycle management](https://learn.microsoft.com/en-us/azure/container-apps/application-lifecycle-management)
  and [Azure CLI revision commands](https://learn.microsoft.com/en-us/cli/azure/containerapp/revision?view=azure-cli-latest).
- Azure Functions ZIP deployment restarts the Function App. Consequently a
  whole-app `az functionapp stop` is not sufficient durable containment for
  this route: it gives no supported guarantee that the package deployment
  preserves the stopped state. Source:
  [Azure Functions ZIP deployment](https://learn.microsoft.com/en-us/azure/azure-functions/deployment-zip-push)
  and [run from package](https://learn.microsoft.com/en-us/azure/azure-functions/run-functions-from-deployment-package).
- ADR-0030 expressly permits, before cutover, a transient failure in the
  currently running revision while a non-additive migration is applied, if the
  release names the affected capability, rolls forward only, and records the
  rollback gap. This is an available deliberate alternative to containment,
  not evidence that the failure is impossible. Source:
  `docs/adr/0030-non-additive-schema-changes-before-cutover.md`.
- The old plan's per-tick Worker schema-readiness proposal is technically
  distinct from release containment. Web's health check uses
  `CanConnectAsync` plus `GetPendingMigrationsAsync().Any()`, but the
  current Worker has no startup/readiness gate and the proposal would add
  runtime code and new test evidence. It should not be silently absorbed into
  a bounded release-procedure decision. Source:
  `src/Pegasus.Web/Health/DatabaseReadinessHealthCheck.cs`,
  `src/Pegasus.Worker/Program.cs`, and the old plan.

## Implications

The preferred candidate is **full operational containment**, after exact
per-target approval and fresh read-only inventory:

1. Directly set each of the seven existing Worker
   `AzureWebJobs.<function>.Disabled=true` settings, then read back the
   exact seven-setting census as disabled. This is a direct Function App
   setting operation, not a pre-migration `azd provision`.
2. Inventory the active Web revision, deactivate that exact revision directly,
   and read it back inactive. The operator must accept the resulting Web
   outage and backlog policy.
3. Run the existing manifest-bound migration, runtime grants/bootstrap, and
   schema-head confirmation while both old runtimes are contained.
4. Provision the new Web while the desired Worker activation remains disabled;
   deploy the new Worker ZIP and re-read the disabled census.
5. Set the normal desired Worker activation to
   `approved-live-worker`, provision, and perform the normal release smoke.

If implemented, the seven setting names should have one canonical script
producer (for example in `scripts/PegasusPlatform.ps1`) shared by the
direct-setting operation and the existing validator/smoke census. Bicep
remains the deployment owner of the settings. This is a small consistency
adjustment, not a new control plane.

Rejected or inferior candidates:

- Do not use `PEGASUS_WEB_ACTIVATION=disabled` as runtime quiescence.
- Do not use a pre-migration `azd provision` merely to disable Worker: its
  potential Web/infrastructure changes violate the narrow containment intent.
- Do not add a new generic containment flag. The existing seven disabled
  settings and normal desired Worker activation express the needed state.
- Do not rely on stopping the Function App alone; ZIP deployment restarts it.
- Do not retune the exception-alert threshold. The recorded alert correctly
  recognized the exception storm; threshold relaxation would hide a genuine
  sustained fault. Any deployment-window suppression remains a separate,
  exact-target cloud-write decision.

The alternative is the ADR-0030 path: explicitly accept the bounded transient
old-runtime failure for the named dropped-column capability and roll forward
only. That is simpler but accepts the predicted fault window and rollback gap.

## Current ticket tension

PLAT-046's original acceptance says a Worker tick on a not-yet-migrated
database should skip rather than throw, and its old plan proposes a new
per-tick schema check. The current task is narrower: decide whether release
containment or ADR-0030's documented transient-error allowance governs a
column-drop release. This research does not weaken the original acceptance,
choose between those objectives, or finalize a plan, ticket body, alert
criteria, or implementation scope.

## Pending operator decision

Choose one explicit release policy before planning:

- **Full containment:** approve the exact Worker Function App settings write,
  the exact Web revision deactivation, the expected Web outage/queue-backlog
  handling, the named migration and bootstrap targets, and forward-only
  recovery if the destructive migration has begun.
- **ADR-0030 allowance:** approve the named affected capability's bounded
  transient old-runtime failure and alert exposure, require the release record
  to name it, and accept roll-forward-only recovery plus the recorded rollback
  gap.

A separate choice is needed only if deployment-window alert suppression is
desired after containment; it is not part of either primary choice and needs
its own exact Azure alert target and write authorization.

## Research limits

No live inventory, Azure/alert operation, release command, build, test, or
verification command was run. No plan, checklist, ticket body, acceptance
criteria, or implementation files were changed.

## Settled operator policy and corrected containment — 8 September 2026

The operator chose temporary shutdown of BOTH old Web and Worker for destructive
migrations, accepting a short outage. Identify destruction during planning.
After actual release, schedule this outside typical usage hours, with a concrete
approved window; do not invent fixed hours. This supersedes the pending decision,
transient-old-runtime-error alternative and obsolete per-tick schema-check proposal
above. It is repository procedure authorization, not permission to stop live apps.

Additional source inspection at dev 9ae9db753e3a3ecce1d9735d5c2fbe6fb5b0ff2c:
- Functions Disabled=true ignores triggers but a master-key REST call may still
  invoke a disabled function. Setting changes restart the host. Therefore the
  full existing WorkerOnly smoke must first prove the exact disabled census,
  then az functionapp stop and read-back Stopped must establish whole-host
  containment. ActivationOnly intentionally skips the exact census and is not
  acceptable containment proof. [Microsoft Functions disabling documentation](https://learn.microsoft.com/en-us/azure/azure-functions/disable-function).
- Deactivating a Container App revision initiates shutdown, not instantaneous
  process absence. Require fresh Single mode/exactly one active revision, deactivate
  only the inventoried target, then bounded read-back of inactive and zero replicas
  before SQL. A timeout, malformed result or unexpected revision blocks migration.
  [Lifecycle](https://learn.microsoft.com/en-us/azure/container-apps/application-lifecycle-management)
  and [replica list](https://learn.microsoft.com/en-us/cli/azure/containerapp/replica?view=azure-cli-latest).
- Retain disabled settings through provisioning and exact Worker ZIP deployment;
  do not assume a prior Stopped host state survives deployment/configuration changes.
  Explicitly verify host state and disabled census after deployment and only enable
  approved new bytes on migrated schema. No old-package restart after destructive
  migration begins; failures remain contained and recovery rolls forward.
- PreProvision already supports desired disabled/observed disabled, followed by
  desired approved-live-worker/observed disabled. No new Bicep switch or runtime
  readiness service is necessary. Do not pre-migration provision or use the Web
  resource-creation activation condition as a shutdown operation.
- Both script consumers already dot-source or can dot-source PegasusPlatform.ps1.
  One canonical producer of existing Worker Disabled setting names removes three
  list copies (validator, smoke, future procedure) while Bicep remains settings owner.
- ADR-0046 will supersede ADR-0030's accepted transient old-runtime fault window,
  retain relevant forward-only recovery, and state current unreleased-development
  and actual post-release scheduling policy. Historical operations observations
  remain untouched. No blanket legacy/compatibility infrastructure is introduced.

No live state or shutdown behavior was experimentally tested. The verification
claim is bounded source/procedure consistency and local contract tests, not a
completed migration or an observed outage-free deployment.
