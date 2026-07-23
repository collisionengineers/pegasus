# Azure, observability and release

## Purpose

Host the caller-backed modular monolith in separate UK South development and production environments with least-privilege identities, explicit database migration, correlated operations evidence, tested recovery and reversible releases.

## Authority and current boundary

- **Authority:** [ADR-0002](../../../architecture/decisions/ADR-0002-dotnet-modular-monolith-on-azure.md), [remaining requirements](../../remaining-requirements.md#7-azure-and-release-readiness), [current inventory](../../../azure/current-inventory.md), and [replacement/retirement plan](../../../azure/replacement-and-retirement-plan.md).
- **Policy owner:** Infrastructure definitions and Web/Worker composition own Azure translation; Core remains free of Azure dependencies.
- **Current implementation:** `infra/main.bicep`, `infra/modules/platform.bicep`, and `.github/workflows/ci.yml` are source only. There is no `azure.yaml`, no dedicated migrator identity, and the current Function setting `SCM_DO_BUILD_DURING_DEPLOYMENT=true` conflicts with the accepted immutable-artifact path. They do not establish a deployed or verified v2 environment.
- **Real callers:** Local Web health endpoints exist. `azd`, Azure-hosted Web/Worker and release paths remain planned until their configuration exists and is exercised.
- **Persistence/adapters:** One Azure SQL database and migration stream; LRS Storage for queues/transient files; managed identity/RBAC; Key Vault for unavoidable third-party secrets; Application Insights/Log Analytics for content-safe telemetry.
- **Dependencies:** Caller-backed application areas, stable configuration contracts and [exact external boundaries](../README.md#approval-boundaries).
- **Replaces/consolidates:** Local `EnsureCreated` startup and any remote-build or credential-based deployment path are removed when ordered migrations and immutable artifacts take ownership.

## Shared failure and observability rules

Azure adapters expose permanent, transient and unknown failures without turning them into business states. Queue attempts, poison outcomes, Web/Worker correlation, health, heartbeat, queue age, authentication failures, integration failures and unexpected cost are observable without extracted content or secret values. `/health/live` remains process-only; `/health/ready` proves only SQL/schema readiness selected for safe Web traffic. App Service Health Check provides monitoring but no rerouting benefit on F1 or a single B1 instance; Worker readiness instead uses host heartbeat, queue-age and poison telemetry.

Current Microsoft guidance was refreshed read-only on 2026-07-23: [.NET isolated Functions](https://learn.microsoft.com/azure/azure-functions/dotnet-isolated-process-guide), [Flex Consumption](https://learn.microsoft.com/azure/azure-functions/flex-consumption-plan), [App Service Health Check](https://learn.microsoft.com/azure/app-service/monitor-instances-health-check), [managed identity with Azure SQL](https://learn.microsoft.com/azure/app-service/tutorial-connect-msi-sql-database), and [Functions telemetry export](https://learn.microsoft.com/azure/azure-functions/functions-monitoring#telemetry-export-options).

## Reconcile infrastructure and identity boundaries

**Evidence state:** Planned

### Authority and decision gate

- **Requirement/decision:** Use the accepted .NET 10 Web/Worker topology and [approved Azure architecture](../../../../.codex/skills/collisionspike-azure-app/references/approved-architecture.md).
- **Confirmed facts:** Region and initial topology are accepted; SKU availability, API versions, quotas, policies, target-group existence and prices are volatile.
- **Decision required before implementation:** None for local Bicep correction. Every Azure preview or mutation requires a fresh, exact approval.

### Owner and dependencies

- **Policy/implementation owner:** One IaC owner coordinates shared identities, configuration and role assignments.
- **Independent evaluator:** Azure architect/reviewer validates topology, least privilege, cost and rollback after read-only research.
- **Prerequisites:** Stable Web/Worker configuration and external-adapter contracts.
- **Consumers/unlocks:** Shared-development deployment, SQL proof, integration smoke tests and production release.

### Caller, contract and change boundary

- **Real or intended caller:** Bicep and GitHub OIDC are planned deployment callers. `azd` is not a caller until an accepted `azure.yaml` exists; no source file proves a deployment.
- **Input/output:** Environment name and approved subscription/region produce deterministic, separately scoped resources and identities.
- **Ordered decisions and failure behavior:** Refresh inventory and Microsoft guidance, build/lint locally, check policy/quota with approval, preview exact targets, then provision only after separate approval; fail before writes on ambiguity.
- **Persistence/migration:** Bicep owns infrastructure only; a post-provision migrator identity applies application migrations and runtime identities have no DDL.
- **Adapters/side effects:** Managed identity and narrow data-plane RBAC replace keys wherever supported; secret names, never values, enter configuration.
- **Operator surface and observability:** Deployment output names resources and non-secret endpoints; alerts route initially to Alex.
- **Documentation affected:** ADRs or Azure inventory change only when accepted architecture or verified live state changes.
- **Replaces/consolidates:** Remove remote build and FTP/SCM basic credentials when immutable artifact deployment is proven.

### Scope

- **Included:** Separate v2 development/production groups, Web, Worker, SQL, Storage, Key Vault, telemetry, conditional scanned-PDF OCR resource, OIDC and identities.
- **Excluded:** Provisioning in this task, production slots, private networking, custom domain, multi-region/zone redundancy, Defender/malware scanning and predecessor deletion.

### Implementation checklist

- [ ] Reconcile Bicep with the accepted four-project runtime and deterministic environment naming; add the bounded `azure.yaml` before treating `azd` as a caller.
- [ ] Define least-privilege Web, Worker, migrator and deployment identities without shared keys or secret values.
- [ ] Make immutable artifact deployment, explicit migrations, health configuration, budgets and alerts first-class outputs.
- [ ] Remove replaced credential and remote-build paths in the same slice.

### Validation checklist

- [ ] Build and lint Bicep locally for development and production parameter sets.
- [ ] Run configuration and architecture tests that prove Core remains Azure-free and secrets are names/identity references only.
- [ ] With approval, run current policy/quota checks and `what-if` against the exact new target group; record the dated source and no-write boundary.
- [ ] `pwsh ./scripts/Invoke-RepoCheck.ps1` with exact result and limitations.

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Local development/production Bicep build | Both parameter sets compile with intended resources, identities and exclusions | Bicep output and lint result | SKU availability, permission or deployment success |
| Exact approved `what-if` | Only intended new v2 resources/role assignments are proposed | Dated preview and target scope | Provisioning or runtime behavior |
| Secret/shared-key inspection | No password, connection string or storage key is required in source | Scoped static review | External secret existence or live RBAC |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** Any Azure CLI/MCP read beyond already supplied inventory, `what-if`, provisioning, RBAC/OIDC change, secret insertion or deployment requires exact subscription/environment/target approval; writes additionally require SKUs and a hard spending cap.
- **Rollout/activation:** Validate locally, preview development, provision development, then separately repeat fresh review for production.
- **Rollback/recovery:** Reapply the prior immutable definitions/artifacts; never delete a resource group or down-migrate as rollback.
- **Irreversible risk:** Role, key and data-bearing resource changes require a fresh dependency and recovery review.

### Deferred-capability impact

- **Named capabilities:** Custom domain, slots, private networking, multi-region/zone resilience, later scale and malware scanning.
- **Stable seam retained:** Host-independent routes, environment configuration, managed identities and modular adapters.
- **Future migration/replacement:** Later tiers/networking require Bicep, DNS/certificate, identity and release changes without changing Core policy.
- **Activation boundary:** Measured quota/reliability need, security policy, cost review, direct approval and an ADR where architecture changes.
- **Deliberately absent:** No dormant resource, scanner, slot, network, second runtime or feature flag.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | Planning review | Sequence and criteria exist | Current Azure facts, deployment and acceptance |

## Prove persistence, observability and recovery in shared development

**Evidence state:** Planned

### Authority and decision gate

- **Requirement/decision:** [Remaining requirements](../../remaining-requirements.md#7-azure-and-release-readiness) require SQL concurrency, migration, telemetry and restore proof.
- **Confirmed facts:** Local SQLite and source-only IaC do not prove Azure SQL behavior or recovery targets.
- **Decision required before implementation:** Exact Azure account, region, SKUs, corpus/vendor scope and hard spending cap before billed/shared-development work.

### Owner and dependencies

- **Policy/implementation owner:** Persistence owner controls `CollisionSpikeDbContext`, migrations and migrator procedure; platform owner controls deployment and alerts.
- **Independent evaluator:** A test engineer authors concurrency/replay/recovery cases and a separate reviewer gives the final verdict.
- **Prerequisites:** Approved development environment and caller-backed intake/lifecycle data model.
- **Consumers/unlocks:** Integration verification, operator acceptance and production readiness.

### Caller, contract and change boundary

- **Real or intended caller:** Explicit migrator, deployed Web/Worker, health probes and alert rules in shared development.
- **Input/output:** Ordered migrations and concurrent/replayed work yield one committed business result, correlated evidence and recoverable data.
- **Ordered decisions and failure behavior:** Migrate before Web, then Worker; reject schema mismatch; bound retries; poison exhausted work; alert without leaking content.
- **Persistence/migration:** Prove fresh and upgraded databases, atomic audit/outbox/state/reference changes, unique constraints and runtime no-DDL roles.
- **Adapters/side effects:** Storage queues carry identifiers only; transient Blob cleanup occurs only after confirmed approved custody.
- **Operator surface and observability:** Health, heartbeat, queue age, poison, integration failure, restore and cost signals are actionable.
- **Documentation affected:** Record tested recovery procedure and dated evidence without rewriting accepted requirements.
- **Replaces/consolidates:** Production startup never calls `EnsureCreated` or silently migrates.

### Scope

- **Included:** Azure SQL concurrency/rollback/exhaustion, migration roles, queue replay/poison, health, alerts, PITR to a new database and four-hour/15-minute recovery objectives.
- **Excluded:** Corpus upload, production restore, external live production folders and broad load/resilience claims.

### Implementation checklist

- [ ] Deliver one ordered migration path and separate no-DDL runtime/migrator roles.
- [ ] Correlate Web, Worker, outbox, queue, external attempts and operator-visible failures.
- [ ] Configure and exercise health, business/integration alerts, budgets and poison handling.
- [ ] Restore to a new database and document reconnect/validation steps within the accepted objectives.

### Validation checklist

- [ ] Run concurrent reference allocation, duplicate delivery, rollback and sequence-exhaustion tests on SQL Server/Azure SQL.
- [ ] Prove runtime identities cannot perform DDL and migrator access is separately scoped.
- [ ] Trigger safe synthetic protocol failures for poison/alert evidence without using synthetic operational instructions.
- [ ] Exercise PITR to a new database and record recovery time and data point.
- [ ] `pwsh ./scripts/Invoke-RepoCheck.ps1` with exact result and limitations.

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Concurrent allocation/replay | One reference/business action and duplicate-safe outcome | Azure SQL integration evidence | Full production load |
| Runtime schema mismatch | Readiness fails and traffic does not reach an incompatible runtime | Health/deployment evidence | Third-party health |
| Point-in-time restore | New database is restored, validated and reconnectable within the target | Timed restore record | Regional disaster recovery |
| Poison/integration failure | Bounded attempts stop, content-safe alert fires and work remains recoverable | Queue, audit and alert evidence | Operator acceptance of the business workflow |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** Development deployment, paid OCR/vendor call, alert delivery and restore each require exact target/cost/data approval.
- **Rollout/activation:** Migrate, deploy Web, pass readiness, deploy/enable Worker, then enable one integration at a time.
- **Rollback/recovery:** Pause new claims, redeploy the prior artifact, use expand-compatible schema and restore to a new database when data recovery is required.
- **Irreversible risk:** Never down-migrate or overwrite the source database during restore proof.

### Deferred-capability impact

- **Named capabilities:** Higher scale, zone/multi-region resilience, private networking and slots.
- **Stable seam retained:** Idempotent handlers, explicit migration, health contracts, environment-scoped identities and immutable artifacts.
- **Future migration/replacement:** Later resilience needs new topology, replication/failover and operational runbooks.
- **Activation boundary:** Measured workload/recovery gap, accepted cost and architecture decision.
- **Deliberately absent:** No dormant secondary region, slot, network or queueing platform.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | Planning review | SQL/recovery proof is bounded | Deployment, recovery and live alerts |

## Release immutable artifacts safely

**Evidence state:** Planned

### Authority and decision gate

- **Requirement/decision:** [Questionnaire operations and release decisions](../../../../PROJECT_DISCOVERY_QUESTIONNAIRE.md#13-monitoring-support-and-operations) and [replacement plan](../../../azure/replacement-and-retirement-plan.md).
- **Confirmed facts:** F1/B1 have no planned slots; rollback is prior-artifact redeployment, not slot swap or resource deletion.
- **Decision required before implementation:** Fresh production approval, price/quota/policy review, business acceptance and every integration-specific cutover approval.

### Owner and dependencies

- **Policy/implementation owner:** One release owner controls workflow, migration, artifacts and activation order.
- **Independent evaluator:** Reviewer checks target, artifact identity, smoke evidence and rollback readiness.
- **Prerequisites:** Shared-development proof and [acceptance plan](acceptance-and-cutover.md).
- **Consumers/unlocks:** Controlled production availability.

### Caller, contract and change boundary

- **Real or intended caller:** Protected GitHub production environment using scoped OIDC.
- **Input/output:** One immutable tested artifact and explicit migration produce a health-checked release with prior artifact retained.
- **Ordered decisions and failure behavior:** Gate writes, pause Worker claims, migrate expand-only, deploy Web then Worker, smoke, enable integrations singly; on failure pause and redeploy prior artifacts.
- **Persistence/migration:** Release records artifact and migration identity; runtime has no migration authority.
- **Adapters/side effects:** Exactly one production Inbox poller; every external integration starts disabled.
- **Operator surface and observability:** Staff are notified of the planned interruption; health, smoke and alerts identify the release.
- **Documentation affected:** Release/recovery evidence records actual target and outcome; retirement stays separate.
- **Replaces/consolidates:** No manual mutable build or broad subscription credential remains.

### Scope

- **Included:** Protected OIDC workflow, immutable artifacts, explicit migration, direct B1 deployment, smoke, integration activation and prior-artifact rollback.
- **Excluded:** Automatic predecessor shutdown, production Box enablement beyond approved IDs, slots and destructive rollback.

### Implementation checklist

- [ ] Produce one immutable Web/Worker artifact set from the validated revision.
- [ ] Protect environment approvals and scope OIDC independently for development and production.
- [ ] Implement the write gate, claim pause, migration, health/smoke and one-at-a-time integration activation sequence.
- [ ] Retain prior artifacts and a tested rollback procedure.

### Validation checklist

- [ ] Prove the same artifacts pass shared-development smoke before production approval.
- [ ] Prove a failed health/smoke gate stops later integration activation.
- [ ] Exercise prior-artifact redeployment in development.
- [ ] Confirm exactly one poller and all unapproved integrations disabled.
- [ ] `pwsh ./scripts/Invoke-RepoCheck.ps1` with exact result and limitations.

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Approved production release | Named artifacts deploy after migration and health/smoke pass | Workflow, deployment and smoke record | Sustained operation or business acceptance beyond tested journey |
| Failed readiness/smoke | Activation stops; prior artifact can be redeployed without down-migration | Development rollback exercise | Recovery from arbitrary data corruption |
| Integration activation | Only explicitly approved mailbox/vendor/Box scope is enabled, one boundary at a time | Configuration and negative-scope evidence | Authority to broaden scope later |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** Production OIDC/RBAC, migration, release, mailbox cutover, credentials, MCP/Claude processing and each integration activation require separate exact approval.
- **Rollout/activation:** Release outside office hours in the documented order; never infer production authority from development success.
- **Rollback/recovery:** Pause claims/writes and redeploy prior immutable artifacts; restore to a new database only under the recovery runbook.
- **Irreversible risk:** Predecessor pause/retirement and any data-bearing deletion remain separately authorised operations.

### Deferred-capability impact

- **Named capabilities:** Slots, zero-interruption release, advanced rollout and predecessor retirement.
- **Stable seam retained:** Immutable artifacts, protected environments, health gates and reversible activation.
- **Future migration/replacement:** A later tier/slot rollout changes deployment topology and test evidence, not Core behavior.
- **Activation boundary:** Measured interruption problem, accepted tier cost and new release decision.
- **Deliberately absent:** No dormant slot, blue/green environment or automatic predecessor mutation.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | Planning review | Release and rollback sequence exists | Deployment, live verification and acceptance |
