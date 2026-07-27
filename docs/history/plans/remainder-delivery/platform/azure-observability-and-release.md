# Azure, observability and release

> **Archive status — non-authoritative planning evidence.** Revalidate against current product, roadmap, architecture, operations, design, decisions, and code before use.

## Purpose

Host the caller-backed modular monolith in separate UK South development and production environments with least-privilege identities, explicit database migration, correlated operations evidence, tested recovery and reversible releases.

## Feature coverage

Primary matrix IDs: `ACC-11`, `OPS-01`, `OPS-02`, `OPS-03`, `OPS-04`, `OPS-05`, `OPS-06`, `OPS-07`, `OPS-08`, `OPS-09`, `OPS-10`, `OPS-11`, `OPS-13`, `OPS-14`, `OPS-20`, and `OPS-24`. Their routes are [V0 shared-development proof](#provision-and-prove-v0-shared-development), [infrastructure and identity boundaries](#reconcile-infrastructure-and-identity-boundaries), [persistence, observability and recovery](#prove-persistence-observability-and-recovery-in-shared-development), and [immutable release](#release-immutable-artifacts-safely). Allocation remains owned by the [maturity map](../../feature-maturity-map.md); this list is a route, not implementation evidence.

## Authority and current boundary

- **Authority:** [ADR-0002](../../../../architecture/decisions/ADR-0002-dotnet-modular-monolith-on-azure.md), [ADR-0009](../../../../architecture/decisions/ADR-0009-direct-terminal-azure-deployment.md), [remaining requirements](../../../../product/v1-gap.md#7-azure-and-release-readiness), [current inventory](../../../../azure/current-inventory.md), and [replacement/retirement plan](../../../../azure/replacement-and-retirement-plan.md).
- **Policy owner:** Infrastructure definitions and Web/Worker composition own Azure translation; Core remains free of Azure dependencies.
- **Current implementation:** `infra/main.bicep`, `infra/modules/platform.bicep`, `azure.yaml`, and `.github/workflows/ci.yml` are source only. The tracked `azure.yaml` names Web/Worker services and a post-provision database script, but neither it nor Bicep supplies the accepted dedicated migrator/immutable-artifact release path. The Web App still sets `SCM_DO_BUILD_DURING_DEPLOYMENT=true`. These files do not establish a deployed or verified v2 environment.
- **Real callers:** Local Web health endpoints exist. `azd` has a tracked but unexercised service manifest; Azure-hosted Web/Worker and release paths remain planned until the manifest is reconciled and exercised.
- **Persistence/adapters:** One Azure SQL database and migration stream; LRS Storage for queues/transient files; managed identity/RBAC; Key Vault for unavoidable third-party secrets; Application Insights/Log Analytics for content-safe telemetry.
- **Dependencies:** Caller-backed application areas, stable configuration contracts and [exact external boundaries](../README.md#approval-boundaries).
- **Replaces/consolidates:** Local `EnsureCreated` startup and any remote-build or credential-based deployment path are removed when ordered migrations and immutable artifacts take ownership.

## Shared failure and observability rules

Azure adapters expose permanent, transient and unknown failures without turning them into business states. Queue attempts, poison outcomes, Web/Worker correlation, health, heartbeat, queue age, authentication failures, integration failures and unexpected cost are observable without extracted content or secret values. `/health/live` remains process-only; `/health/ready` proves only SQL/schema readiness selected for safe Web traffic. App Service Health Check provides monitoring but no rerouting benefit on F1 or a single B1 instance; Worker readiness instead uses host heartbeat, queue-age and poison telemetry.

Current Microsoft guidance was refreshed read-only on 2026-07-23: [.NET isolated Functions](https://learn.microsoft.com/azure/azure-functions/dotnet-isolated-process-guide), [Flex Consumption](https://learn.microsoft.com/azure/azure-functions/flex-consumption-plan), [App Service Health Check](https://learn.microsoft.com/azure/app-service/monitor-instances-health-check), [managed identity with Azure SQL](https://learn.microsoft.com/azure/app-service/tutorial-connect-msi-sql-database), [Functions telemetry export](https://learn.microsoft.com/azure/azure-functions/functions-monitoring#telemetry-export-options), and [container-scoped Blob data roles](https://learn.microsoft.com/azure/storage/blobs/assign-azure-role-data-access#assign-an-azure-role).

## Provision and prove V0 shared development

**Evidence state:** Planned

`OPS-10` is an authorised-terminal V0 proof of one exact UK South shared-development target. It begins only after the [ADR-0009 package, migration, identity and provenance foundation](#reconcile-infrastructure-and-identity-boundaries) has locally produced separately hashed Web, Worker and immutable migration bundles with pinned tool/runtime provenance, removed remote build, and defined separated deployment, migrator and no-DDL runtime identities. A second gate then requires exact subscription, tenant, resource-group, SKU, quota, policy and spending-cap approval. The authorised terminal previews/provisions the target, applies the named migration bundle, and deploys the same hashed application packages; source Bicep, `azd` registration or a health endpoint is not deployment/caller evidence. Capture exact target, bundle hashes/provenance, Entra-only SQL mode, identity/RBAC separation, schema and smoke result; stop before a write on preflight failure, and recover only by the approved target-specific procedure. This establishes neither production deployment, a Worker business caller, external integration, live verification nor acceptance; it creates no dormant V2 resource, second environment, slot, private network, region or failover topology.

## Reconcile infrastructure and identity boundaries

**Evidence state:** Planned

### Authority and decision gate

- **Requirement/decision:** Use the accepted [.NET 10 modular-monolith Azure architecture](../../../../architecture/decisions/ADR-0002-dotnet-modular-monolith-on-azure.md), implement the [ADR-0009 direct-terminal package/migration sequence](../../../../architecture/decisions/ADR-0009-direct-terminal-azure-deployment.md), and refresh the [current Azure inventory](../../../../azure/current-inventory.md) before design or implementation.
- **Confirmed facts:** Region and initial topology are accepted; SKU availability, API versions, quotas, policies, target-group existence and prices are volatile.
- **Decision required before implementation:** None for local Bicep correction. Every Azure preview or mutation requires a fresh, exact approval.

### Owner and dependencies

- **Policy/implementation owner:** One IaC owner coordinates shared identities, configuration and role assignments.
- **Independent evaluator:** Azure architect/reviewer validates topology, least privilege, cost and rollback after read-only research.
- **Prerequisites:** Stable Web/Worker configuration, the first caller-backed slice, a pinned repository SDK/tool set, the ordered migration stream and named external-adapter contracts. This section must complete locally before `OPS-10` provisioning/deployment.
- **Consumers/unlocks:** Shared-development deployment, SQL proof, integration smoke tests and production release.

### Caller, contract and change boundary

- **Real or intended caller:** An authorised terminal is the intended deployment caller. Bicep and the tracked `azure.yaml` are source-only inputs; `azd` is not a proven caller until the separate package, migration, identity and artifact-provenance work is implemented and exercised.
- **Input/output:** Environment name, approved subscription/region, pinned build inputs and one validated source revision produce deterministic separately scoped resources plus hashed Web, Worker and immutable migration bundles with a machine-readable hash/provenance manifest.
- **Ordered decisions and failure behavior:** Refresh inventory and Microsoft guidance, build/lint locally, check policy/quota with approval, preview exact targets, then provision only after separate approval; fail before writes on ambiguity.
- **Persistence/migration:** Bicep owns infrastructure only. The deployment identity may preview/provision only the approved infrastructure target; a separately identified migrator applies the immutable migration bundle with schema-change permission; Web/Worker runtime identities receive only named application data/execute permissions and no DDL. The release proof must show Azure SQL remains Microsoft Entra-only, SQL-password authentication is unavailable, and none of the three boundaries silently inherits another's privilege.
- **Adapters/side effects:** Managed identity and narrow data-plane RBAC replace keys wherever supported; secret names, never values, enter configuration. In the caller-backed staging slice, Web will receive `Storage Blob Data Contributor` only on `intake-temporary`, delivered with the Blob adapter, composition and actual Web proof; current Bicep grants Web no Blob data-plane role or container configuration. Worker retains account-scoped Blob Owner plus queue/table data roles because identity-based Functions host/deployment storage uses those services as well as application intake; sharing that account is an explicit residual isolation boundary until an accepted separate-host-storage change.
- **Operator surface and observability:** Deployment output names resources and non-secret endpoints; alerts route initially to Alex.
- **Documentation affected:** ADRs or Azure inventory change only when accepted architecture or verified live state changes.
- **Replaces/consolidates:** Remove remote build and FTP/SCM basic credentials when immutable artifact deployment is proven.

### Scope

- **Included:** Separate v2 development/production groups, Web, Worker, SQL, Storage, Key Vault, telemetry, authorised-terminal identities, build-once package/provenance inputs and the explicit migrator boundary.
- **Excluded:** Provisioning in this task, every V2 Document Intelligence resource/role/configuration until the V2 OCR activation slice, production slots, private networking, custom domain, multi-region/zone redundancy, Defender/malware scanning and predecessor deletion. The current conditional Document Intelligence declaration and Worker role are removed from the V0/V1 scaffold rather than shipped disabled.

### Implementation checklist

- [ ] Reconcile the existing `azure.yaml` and Bicep with the accepted four-project runtime, deterministic environment naming, build-once immutable Web/Worker/migration bundles and explicit migrations before treating `azd` as a proven caller; do not create a second manifest.
- [ ] Pin the .NET SDK and every release-affecting CLI/tool or record its exact validated version; generate a machine-readable manifest containing source revision, package paths, SHA-256 hashes and tool/runtime provenance, and verify deployment never rebuilds those bytes.
- [ ] Define least-privilege Web, Worker, migrator and deployment identities without shared keys or secret values. Keep Web Blob data access container-scoped and prove it cannot reach `app-package`, queues or other containers. Retain/document the Worker host-storage account scope separately from its business responsibilities rather than claiming container isolation.
- [ ] Prove Azure SQL is Microsoft Entra-only; prove deployment, migrator and Web/Worker runtime identities are distinct in purpose and effective database permissions, with no DDL for runtime and no standing application-data role for deployment.
- [ ] Remove the current V2 Document Intelligence resource parameter/module path and Worker Cognitive Services role from V0/V1 output; the V2 OCR plan owns a future separately approved activation delta.
- [ ] Make immutable artifact deployment, explicit migrations, health configuration, budgets and alerts first-class outputs.
- [ ] Remove replaced credential and remote-build paths, including `SCM_DO_BUILD_DURING_DEPLOYMENT=true`, in the same slice.

### Validation checklist

- [ ] Build and lint Bicep locally for development and production parameter sets.
- [ ] Compare compiled V0/V1 templates and prove they contain no Document Intelligence account, role assignment, configuration or output.
- [ ] Build the Web, Worker and migration bundles once, verify every manifest hash, then prove the shared-development command consumes those exact paths without a build step.
- [ ] Run configuration and architecture tests that prove Core remains Azure-free and secrets are names/identity references only.
- [ ] Prove compiled SQL configuration enables Entra-only authentication and the database grant procedure produces separate deployment, migrator and no-DDL runtime effective-permission evidence.
- [ ] With approval, run current policy/quota checks and `what-if` against the exact new target group; record the dated source and no-write boundary.
- [ ] `pwsh ./scripts/Invoke-RepoCheck.ps1` with exact result and limitations.

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Local development/production Bicep build | Both parameter sets compile with intended resources, identities and exclusions | Bicep output and lint result | SKU availability, permission or deployment success |
| Exact approved `what-if` | Only intended new v2 resources/role assignments are proposed | Dated preview and target scope | Provisioning or runtime behavior |
| Secret/shared-key inspection | No password, connection string or storage key is required in source; Shared Key is disabled | Scoped static review and approved negative probe | External secret existence or all live RBAC behavior |
| V0/V1 compiled topology | No Document Intelligence account/role/configuration is emitted | Template assertion and exact compiled-resource inventory | Future V2 OCR activation |
| Package/provenance foundation | Web, Worker and immutable migration bundles each match the source-revision/tool-provenance manifest and the deployment route performs no rebuild | Local package/hash verification plus command dry run | Azure permission or deployment success |
| SQL authentication and identity separation | Entra-only authentication is enabled; deployment cannot mutate application data, migrator alone can apply the bundle, and runtime identities cannot perform DDL | Compiled configuration plus approved shared-development positive/negative effective-permission probes | Every future feature permission |
| Web manual upload identity | Web can stage/read one source in `intake-temporary` and is denied Blob data access outside it | actual Web integration smoke plus negative RBAC probe | Worker processing or Box custody |
| Web system identity recreation | role-assignment name changes deterministically with the new principal ID instead of mutating the old binding | compiled template comparison/test | Azure propagation or successful live redeployment |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** Any Azure CLI/MCP read beyond already supplied inventory, `what-if`, provisioning, RBAC change, secret insertion or deployment requires exact subscription/environment/target approval; writes additionally require SKUs and a hard spending cap.
- **Rollout/activation:** Build and hash all three bundles locally, validate the no-dormant V0/V1 topology and identity separation, preview development, provision development, apply the named migration bundle, then deploy the same hashed Web/Worker packages. Production repeats fresh approval and the same explicit sequence; `azd up` is not the release shortcut.
- **Rollback/recovery:** Reapply the prior immutable definitions/artifacts; never delete a resource group or down-migrate as rollback.
- **Irreversible risk:** Role, key and data-bearing resource changes require a fresh dependency and recovery review.

### Deferred-capability impact

- **Custom domain:** `Unclear`; retain host-independent routes and environment configuration only until a direct product, DNS/TLS, and OAuth decision.
- **Permanent boundaries:** GitHub deployment/OIDC, separate staging/QA/UAT/demo, S1/slots, private networking, zone/multi-region resilience, and malware scanning are `Never`. They have no activation path.
- **Deliberately absent:** No dormant resource, scanner port/client, slot, network, second runtime, feature flag, topology test, or ADR/cost gate for a `Never` boundary.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | Planning review | Sequence and criteria exist | Current Azure facts, deployment and acceptance |
| Existing scaffold locally compiled 2026-07-23: `az bicep build --file infra/main.bicep` | Exit 0 | current source IaC, explicitly excluding uncalled Web Blob RBAC/configuration | Existing Bicep syntax/emission is locally valid and no dormant Web Blob privilege was added | Future caller-backed role design, Azure preview, deployment, live RBAC, storage operations or acceptance |

## Prove persistence, observability and recovery in shared development

**Evidence state:** Planned

### Authority and decision gate

- **Requirement/decision:** [Remaining requirements](../../../../product/v1-gap.md#7-azure-and-release-readiness) require SQL concurrency, migration, telemetry and restore proof.
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
- **Persistence/migration:** Prove fresh and upgraded databases, atomic action-history/outbox/state/reference changes, unique constraints and runtime no-DDL roles.
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
| Poison/integration failure | Bounded attempts stop, content-safe alert fires and work remains recoverable | Queue, action-history and alert evidence | Operator acceptance of the business workflow |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** Development deployment, paid OCR/vendor call, alert delivery and restore each require exact target/cost/data approval.
- **Rollout/activation:** Migrate, deploy Web, pass readiness, deploy/enable Worker, then enable one integration at a time.
- **Rollback/recovery:** Pause new claims, redeploy the prior artifact, use expand-compatible schema and restore to a new database when data recovery is required.
- **Irreversible risk:** Never down-migrate or overwrite the source database during restore proof.

### Deferred-capability impact

- **Stable seam retained:** Idempotent handlers, explicit migration, health contracts, environment-scoped identities and immutable artifacts.
- **Permanent boundaries:** Zone/multi-region resilience, private networking, and slots/S1 are `Never`; no topology, replication, failover, network, or slot runbook is planned.
- **Deliberately absent:** No dormant secondary region, slot, network, queueing platform, or test/approval path for those boundaries.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | Planning review | SQL/recovery proof is bounded | Deployment, recovery and live alerts |

## Release immutable artifacts safely

**Evidence state:** Planned

### Authority and decision gate

- **Requirement/decision:** [Questionnaire operations and release decisions](../../../../../PROJECT_DISCOVERY_QUESTIONNAIRE.md#13-monitoring-support-and-operations) and [replacement plan](../../../../azure/replacement-and-retirement-plan.md).
- **Confirmed facts:** F1/B1 have no planned slots; rollback is prior-artifact redeployment, not slot swap or resource deletion.
- **Decision required before implementation:** Fresh production approval, price/quota/policy review, business acceptance and every integration-specific cutover approval.

### Owner and dependencies

- **Policy/implementation owner:** One release owner controls workflow, migration, artifacts and activation order.
- **Independent evaluator:** Reviewer checks target, artifact identity, smoke evidence and rollback readiness.
- **Prerequisites:** Shared-development proof and [acceptance plan](acceptance-and-cutover.md).
- **Consumers/unlocks:** Controlled production availability.

### Caller, contract and change boundary

- **Real or intended caller:** An authorised production terminal executing the ADR-0009 sequence after exact approval.
- **Input/output:** Separately immutable Web, Worker and migration bundles from one validated source revision, plus a manifest of their SHA-256 hashes and pinned tool/runtime provenance, produce a health-checked release with prior application bundles retained.
- **Ordered decisions and failure behavior:** Gate writes, pause Worker claims, migrate expand-only, deploy Web then Worker, smoke, enable integrations singly; on failure pause and redeploy prior artifacts.
- **Persistence/migration:** Release records the migration-bundle hash and migration identity before applying it. Only the migrator has schema-change permission; deployment has infrastructure/package scope, and Web/Worker runtime identities have no DDL. Azure SQL stays Entra-only throughout.
- **Adapters/side effects:** Exactly one production Inbox poller; every external integration starts disabled.
- **Operator surface and observability:** Staff are notified of the planned interruption; health, smoke and alerts identify the release.
- **Documentation affected:** Release/recovery evidence records actual target and outcome; retirement stays separate.
- **Replaces/consolidates:** No manual mutable build or broad subscription credential remains.

### Scope

- **Included:** Direct authorised-terminal route, hashed immutable Web/Worker/migration bundles, pinned build/tool provenance, explicit migration, direct B1 deployment, smoke, integration activation and prior-application-artifact rollback.
- **Excluded:** Automatic predecessor shutdown, production Box enablement beyond approved IDs, slots and destructive rollback.

### Implementation checklist

- [ ] Produce the Web package, Worker package and immutable migration bundle once from the validated revision; record package paths, SHA-256 hashes, source revision, target runtimes and pinned SDK/CLI/tool versions in one release manifest.
- [ ] Verify shared development and production consume the exact manifest paths/hashes without remote build or repackaging; fail closed on any missing/mismatched byte or provenance field.
- [ ] Implement the authorised-terminal preflight and least-privilege identity procedure; GitHub Actions/OIDC deployment remains `Never`.
- [ ] Implement the write gate, claim pause, Entra-only migrator authentication, migration, Web deployment, Worker deployment, health/smoke and one-at-a-time integration activation sequence; prove deployment/migrator/runtime privilege separation.
- [ ] Retain prior artifacts and a tested rollback procedure.

### Validation checklist

- [ ] Prove the same three bundle hashes and provenance manifest pass shared-development migration/deployment/smoke before production approval.
- [ ] Prove SQL-password authentication is unavailable, the deployment identity cannot apply schema/data mutations, the migrator can apply only the approved bundle, and Web/Worker cannot perform DDL.
- [ ] Prove a failed health/smoke gate stops later integration activation.
- [ ] Exercise prior-artifact redeployment in development.
- [ ] Confirm exactly one poller and all unapproved integrations disabled.
- [ ] `pwsh ./scripts/Invoke-RepoCheck.ps1` with exact result and limitations.

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Approved production release | Named artifacts deploy after migration and health/smoke pass | Workflow, deployment and smoke record | Sustained operation or business acceptance beyond tested journey |
| Package or provenance mismatch | Release stops before migration/deployment; no remote rebuild substitutes new bytes | Three-bundle hash/provenance verification | Azure service health |
| SQL identity boundary | Entra-only migrator applies the named bundle; deployment and runtime attempts outside their scopes are denied | Positive/negative effective-permission evidence | Permissions for later integrations |
| Failed readiness/smoke | Activation stops; prior artifact can be redeployed without down-migration | Development rollback exercise | Recovery from arbitrary data corruption |
| Integration activation | Only explicitly approved mailbox/vendor/Box scope is enabled, one boundary at a time | Configuration and negative-scope evidence | Authority to broaden scope later |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** Production terminal/RBAC use, migration, release, mailbox cutover, credentials, MCP/Claude processing and each integration activation require separate exact approval.
- **Rollout/activation:** Release outside office hours in the documented order; never infer production authority from development success.
- **Rollback/recovery:** Pause claims/writes and redeploy prior immutable artifacts; restore to a new database only under the recovery runbook.
- **Irreversible risk:** Predecessor pause/retirement and any data-bearing deletion remain separately authorised operations.

### Deferred-capability impact

- **Stable seam retained:** Immutable artifacts, direct authorised-terminal release, health gates and reversible activation.
- **Permanent boundaries:** S1/slots and separate staging/QA/UAT/demo environments are `Never`; no zero-interruption slot or blue/green route is planned.
- **Separate concern:** Predecessor retirement still requires its own explicit approval; it is not a release-topology activation.
- **Deliberately absent:** No dormant slot, blue/green environment, staging runtime, or automatic predecessor mutation.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | Planning review | Release and rollback sequence exists | Deployment, live verification and acceptance |
