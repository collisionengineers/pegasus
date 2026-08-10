# Durable production Worker activation

Task line: Durable production Worker activation and release gates, branch
`task/durable-worker-activation`.

The 10 August 2026 production incident proved that release 8 reapplied nine
hard-coded `AzureWebJobs.<function>.Disabled=true` settings and stopped every
Worker trigger. This task replaces that configuration drift with one explicit,
fail-closed production activation contract. It changes infrastructure and
release validation only; it does not activate production or exercise any live
caller.

## Activation contract

`workerActivation` is a string input to `infra/main.bicep` and
`infra/modules/platform.bicep`. Its default is `disabled`. The sole enabling
value is `approved-live-worker`, and it enables functions only inside the
existing exact production deployment gate. An omitted value, `disabled`, a
misspelling, or any other value renders all nine settings as `true`.

The normal azd route receives the input from
`PEGASUS_WORKER_ACTIVATION`, mapped in `infra/main.parameters.json` with the
same fail-closed `disabled` default. This parameter-map change was explicitly
approved after inspection showed that a Bicep-only input could be omitted by a
later normal provision and recreate the incident.

The exact function census is:

- `PendingWorkDispatchFunction`;
- `IntakeWorkFunction`;
- `IntakePoisonFunction`;
- `StagedArtifactReconciliationFunction`;
- `InboxPollFunction`;
- `SentEvidencePollFunction`;
- `DueWorkSweepFunction`;
- `ExternalWorkFunction`; and
- `ExternalPoisonFunction`.

No schedule, retry, queue, mailbox, storage, database, external adapter, or
function business logic changes.

## Release validation

`scripts/Test-AzureDeploymentPlan.ps1` will:

- compile the Bicep template;
- inspect the compiled template's exact nine-setting census and conditional
  expression;
- support local validation of both `disabled` and
  `approved-live-worker` inputs;
- assert the azd mapping and fail-closed default;
- add a pre-provision mode that reads the selected azd environment, binds it
  to the exact production subscription, tenant, resource group, and Worker,
  and rejects a disabled desired value when the expected live estate is
  enabled; and
- invoke the shared read-only Worker-setting assertion before any provision.

`scripts/Invoke-ProductionSmoke.ps1` will read the Function App settings after
deployment, require the same exact nine-setting census, and compare every
value with the intended activation state. Missing, extra, malformed, or
mixed-state settings fail the smoke. The script exposes a Worker-only mode so
the same assertion can be used as the pre-provision readback without running
Web smoke.

This task does not run either live mode. Local verification exercises only the
static and compiled-template paths.

### Independent-review remediation

PR #362 review found two release-gate defects. The implementation will be
hardened before merge:

- every setting returned by the secret-safe `AzureWebJobs.` query participates
  in one ordinal, case-sensitive exact census; duplicate, extra, malformed,
  case-variant, missing, and mixed-value results all fail closed;
- behavioral mocked tests execute the Worker-only smoke for the two valid
  states and each named failure class, rather than proving only source text;
- the Azure CLI read receives the literal approved subscription
  `e6076573-23a5-46a8-acef-7e22d264e5db`; and
- the read target is the non-overridable production Worker identity
  `pegasus-prod-worker-252ow37gij`. Pre-provision separately rejects an azd
  environment whose recorded Worker output differs from that identity.

The errors remain secret-safe: they identify the failed contract class, never
the returned setting names or values.

The second independent review accepted those live-readback remediations and
identified two remaining local release-gate defects. Before merge:

- source validation will enumerate every
  `AzureWebJobs.*.Disabled` name independently of its value, require the exact
  ordinal nine-name census, and only then prove that each exact name uses the
  approved fail-closed conditional;
- compiled-template validation will perform the same two independent checks,
  so an extra hard-coded setting cannot disappear from the conditional match;
- a focused regression will append a rogue hard-coded disabled setting to an
  isolated template copy and execute Local validation to prove that ten names
  fail closed; and
- the first-activation, later enabled-estate release, and rollback procedures
  will each bind the exact azd environment and production subscription in
  their own fresh-terminal command sequence.

## Operator procedure

`docs/runbook.md` gains one Worker activation and rollback section. It records:

- the exact `azd env set PEGASUS_WORKER_ACTIVATION` commands;
- the disabled-baseline to approved activation transition;
- the normal enabled-estate preflight rule, where omission/default is a stop
  condition rather than an acceptable redeployment input;
- read-only pre- and post-deployment nine-setting assertions; and
- rollback to the fail-closed value, with the same exact-target approval,
  explicit `-AllowWorkerDisable` transition, deployment, and readback gates.

The runbook will distinguish intended configuration, locally validated
template output, deployed state, and live Worker execution. Enabling settings
does not prove triggers ran, a mailbox message was received, a Case/PO was
allocated, or custody completed.

## Verification

Local, from the task worktree:

- `./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local -WorkerActivation disabled`;
- `./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local -WorkerActivation approved-live-worker`;
- `dotnet restore`;
- `dotnet build --configuration Release`;
- focused PowerShell/release-script tests available in the repository, then
  the canonical non-live test profile required by the runbook; and
- scoped diff, secret-path, and whitespace checks before publication.

An independent reviewer must compare the PR against the incident and this
plan, including the literal nine-function census. Evidence from this task can
reach compiled-template and green local build/test tiers only. No Azure,
production, mailbox, SQL, Blob, Queue, Box, deployment, or live acceptance
claim is authorised.

## Coordination

The parallel manifest-bound cleanup task may add a separate clean-baseline
section to `docs/runbook.md`. Before opening this PR, merge fresh `origin/dev`,
preserve that section wholesale, and resolve only the non-overlapping placement
of this Worker activation section. Remove only this task's `NOW.md` claim.
