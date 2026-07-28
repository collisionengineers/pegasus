# Local testing and service evidence

## Purpose

This area owns the reproducible local test environment and the boundary between local, live-service and operator evidence. Its outcome is an honest answer to whether Pegasus behavior is actually callable and verified, not merely compiled, registered or represented by a mock.

## Feature coverage

Primary matrix ID: `OPS-22`, routed through [caller-backed local and live evidence gates](#caller-backed-local-and-live-evidence-gates). This runbook owns evidence profiles and tool boundaries, not business behavior; allocation remains owned by the [capability inventory](../../product/capabilities.md).

## Authority and current boundary

- **Authority:** [Source-of-truth order](../../agent-guidance/source-of-truth.md), canonical [product requirements](../../product/index.md), retained [questionnaire evidence](../../history/product/project-discovery-questionnaire.md), [remaining first-release requirements](../../product/qdos-alpha-gap.md), [open decisions](../../product/open-decisions.md), [ADR-0002 Azure modular monolith](../../decisions/ADR-0002-dotnet-modular-monolith-on-azure.md), [ADR-0005 multi-format intake assets](../../decisions/ADR-0005-multiformat-intake-assets.md), and the executable test boundary.
- **Policy owner:** Each business capability remains owned by its Core use case. This area owns only test-tool lifecycle, evidence profiles, isolation and evidence classification.
- **Current implementation:** The development-only [manual upload page](../../../src/Pegasus.Web/Pages/Intake/Upload.cshtml.cs) calls `ProcessIntake`; the [Worker composition root](../../../src/Pegasus.Worker/README.md) explicitly has no trigger. Build and test evidence runs through the owning .NET projects.
- **Real callers:** `/Intake/Upload` is the only current source-mapped intake caller; this planning research did not execute it. Authenticated production Web/API/MCP entry points and Worker timer/queue triggers are intended callers and must remain labelled `Planned` until present and exercised.
- **Persistence/adapters:** Current development evidence uses ignored local artifacts and relational persistence. The accepted target adds Azure SQL, transient Blob storage, Storage queues, Box, Graph, Document Intelligence, DVLA/DVSA, EVA export, Key Vault and Azure Monitor adapters.
- **Dependencies:** The delivered feature must expose a real entry point and retain its Core policy owner before its profile can become a release gate.
- **Replaces/consolidates:** Test projects and fixtures own their exact dependency lifecycle and evidence. This area does not add a generic repository workflow script, replace product policy, or create test-only production abstractions.

## Shared failure and observability rules

- A missing required tool, occupied port, failed readiness check, skipped required test, leaked child process or failed scoped cleanup makes the selected profile fail visibly.
- Each run records its profile, command, exit result, input class, run identifier and evidence limitation without storing secret values or document content.
- Tests distinguish transient failure, terminal failure and unknown/manual-review outcomes. A retry is bounded; exhaustion is visible; duplicate delivery cannot duplicate a case, reference or external side effect.
- Logs, TRX, screenshots, traces and evaluation artifacts are scanned before retention for credentials, document text and unnecessary personal data.
- Corpus remains ignored, immutable, local and untrusted. Controlled synthetic fixtures are allowed only for protocol, security and resource-limit behavior, never as operational business evidence.

## Reproducible Windows-native test environment

**Evidence state:** Planned

### Authority and decision gate

- **Requirement/decision:** The repository supports Windows and PowerShell 7; accepted architecture requires Azure Storage queues and transient Blob staging; Blob/Queue/Functions evidence therefore requires Azurite and Functions Core Tools. [Microsoft documents Azurite as the local Blob, Queue and Table emulator](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) and [Functions Core Tools as the local Functions host](https://learn.microsoft.com/azure/azure-functions/functions-develop-local).
- **Confirmed facts:** Azurite can prove Storage SDK and binding behavior but not Azure Files, ADLS, Entra/RBAC, managed identity, durability, replication, quotas, networking or production performance. Docker is not required for npm Azurite or SQL Server Express LocalDB.
- **Decision required before implementation:** None. Activate the Storage/Worker gate in the same slice as the first real Blob/Queue adapter and Worker trigger rather than adding a callerless release gate.

### Owner and dependencies

- **Policy/implementation owner:** Repository test-infrastructure owner; the Worker composition owner remains responsible for the actual trigger and Infrastructure owns Storage translation.
- **Independent evaluator:** Test engineer who did not author the orchestration change.
- **Prerequisites:** Existing Windows repository check; Node/npm; accepted Storage/Worker architecture.
- **Consumers/unlocks:** Local and CI integration evidence for SQL, Blob, Queue, Functions, browser, external adapters, recovery and non-functional profiles.

### Caller, contract and change boundary

- **Real or intended caller:** Developers and Windows CI invoke the repository check with a named profile. Product behavior is exercised through `/Intake/Upload` now and through the actual Functions host only after a trigger exists.
- **Input/output:** A profile plus optional run identifier produces a deterministic pass/fail result and ignored run artifacts. The orchestrator owns every process and disposable resource it creates.
- **Ordered decisions and failure behavior:** Validate tools; allocate a unique run directory, database names and loopback ports; start only selected dependencies; wait for readiness; execute the exact evidence lane; stop owned processes; remove only the validated run scope; retain diagnostics after failure.
- **Persistence/migration:** Disposable SQLite or GUID-named LocalDB databases, Azurite state, backups and downloads live beneath the ignored run directory. Production schemas are exercised through committed migrations; application startup never silently migrates a non-Development database.
- **Concurrency/edit ownership:** Parallel runs use distinct databases, ports, containers/queues and storage directories. A run cannot stop or delete a resource it did not create.
- **Adapters/side effects:** Local profiles use deterministic fakes, LocalDB and Azurite. External network access is disabled unless the explicitly selected live profile names an approved target.
- **Permission/scope guard:** Local profiles contain no live credentials. A live profile requires an allowlisted tenant/subscription/account/resource and must reject a broader or missing scope before constructing the external client.
- **Operator surface and observability:** Concise PowerShell output names the selected profile, failing dependency, evidence path and cleanup status; implementation details never appear in operator-facing application UI.
- **Documentation affected:** This plan and developer test instructions; operator notes remain read-only.
- **Replaces/consolidates:** Any manual reliance on a Visual Studio-bundled or globally floating Azurite installation and any duplicate service-start scripts.

### Tool profiles

| Profile | Required tools | Activation and purpose |
|---|---|---|
| `Baseline` | Windows, PowerShell 7, Git/GitHub CLI, pinned .NET 10 SDK, Azure CLI with Bicep, Azure Developer CLI, Node/npm, Python, Infisical CLI and Box CLI | Required for all developers and Windows CI; builds, tests, validates Bicep and supports approved integration administration |
| `SqlServer` | SQL Server Express LocalDB and `sqlcmd` | Required for relational migrations, constraints, transactions, allocation concurrency, outbox atomicity and local backup/restore; [LocalDB is the Windows-native SQL test engine](https://learn.microsoft.com/sql/database-engine/configure-windows/sql-server-express-localdb?view=sql-server-ver17) |
| `StorageWorker` | Repository-pinned npm Azurite and Azure Functions Core Tools v4 | Required when Blob/Queue/Worker code lands; proves actual SDK, binding, retry, poison and restart paths |
| `Browser` | Microsoft Playwright for .NET, pinned Chromium/Firefox/WebKit binaries, axe-core integration and a trusted .NET development HTTPS certificate | Required with authenticated UI; proves real rendering, multi-session behavior and automated accessibility rules |
| `Graph` | Microsoft Dev Proxy and mocked Kiota request adapters | Required with mailbox intake/outbound Graph behavior; simulates paging, throttling, 401/403, 429, 5xx, timeout and retry. See [Graph error simulation](https://learn.microsoft.com/microsoft-cloud/dev/dev-proxy/how-to/simulate-errors-microsoft-graph-apis) and [Kiota testing](https://learn.microsoft.com/openapi/kiota/testing) |
| `Observability` | OpenTelemetry in-memory test exporter; native OpenTelemetry Collector for end-to-end OTLP evidence | Required when production telemetry lands; proves correlation, attributes, health signals and redaction. See [Azure Monitor OpenTelemetry configuration](https://learn.microsoft.com/azure/azure-monitor/app/opentelemetry-configuration) |
| `Performance` | Pinned k6 CLI | Release/profile lane for eight-user concurrency, burst, average-load, stress and soak evidence. See [k6 test types](https://grafana.com/docs/k6/latest/testing-guides/test-types/) |
| `Security` | .NET dependency vulnerability checks and OWASP ZAP | Release/profile lane for dependency and dynamic HTTP/API evidence; ZAP runs through the conditional Docker profile |
| `Containers` | Docker Desktop in Linux-container mode | Conditional only for ZAP, an optional telemetry stack, an optional SQL compatibility lane, or a specifically approved licensed Document Intelligence container; never required merely to run Azurite |
| `LiveIntegration` | Existing approved developer identity/secret tooling plus the exact service SDK/CLI already owned by the feature | Explicitly approved Azure/vendor gate; never part of the default local check |

Storage Explorer, SSMS and Postman remain optional conveniences. Do not add Service Bus, Event Hubs, Cosmos DB, Redis, PostgreSQL, Azure Files, ADLS, a local SMTP server, Testcontainers or their emulators without a later accepted architecture need.

### Scope

- **Included:** Exact tool pins; capability-aware doctor checks; one run-scoped PowerShell orchestrator; isolated LocalDB and Azurite state; Functions-host readiness; stable test traits; Windows CI installation and cleanup; content-safe diagnostics.
- **Excluded:** Product behavior, cloud deployment, Azure resource creation, vendor calls, corpus upload, Docker as a universal prerequisite, and dormant tools for deferred features.

### Implementation checklist

- [ ] Pin Azurite in an npm manifest and lockfile; install with the repository package workflow rather than a global or Visual Studio-specific path.
- [ ] Document each profile's direct version/prerequisite commands; required dependency fixtures fail with an exact remediation category rather than relying on a generic workstation doctor.
- [ ] Give each LocalDB/Azurite/Functions-host integration fixture ownership-safe run IDs, ports, paths, readiness, diagnostics, and teardown; invoke it through the owning `dotnet test` project.
- [ ] Add stable test traits for `Unit`, `Integration`, `SqlServer`, `Storage`, `FunctionsHost`, `Browser`, `Corpus`, `Performance`, `Security`, `Recovery` and `LiveIntegration`; a required but skipped trait fails.
- [ ] Add Windows CI installation and caching for only the profiles used by that job; activate `StorageWorker` in the same change that provides a real trigger and storage adapter.
- [ ] Retain failed-run diagnostics under ignored artifacts and prove cleanup cannot affect another run or a developer-owned service.

### Validation checklist

- [ ] A missing mandatory tool fails its selected profile with the exact remediation category, while an inactive optional profile does not block baseline work.
- [ ] Azurite starts on isolated ports, accepts Blob and Queue SDK operations, retains no cross-run state and shuts down after success and failure.
- [ ] A port collision or Functions readiness failure retains diagnostics, stops only owned processes and returns a non-zero result.
- [ ] LocalDB migration, transaction and backup/restore checks use new disposable databases and never overwrite the source database.
- [ ] Once the Worker trigger exists, an identifier queued through Azurite reaches the actual Functions host and the same Core use case; direct service invocation does not satisfy this check.
- [ ] Browser, Graph, telemetry, performance, security and live tools are checked only when their named profiles are selected.
- [ ] Run the focused and full owning .NET test projects directly and record exact results and limitations.

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Clean workstation with selected profile installed | The owning test project provisions only run-scoped dependencies and completes the lane | `dotnet test` exit result and ignored run log | Product behavior not included in that lane |
| Azurite Blob/Queue operation | SDK write/read/delete and queue delivery use the run-specific endpoints | Storage integration test | Entra, RBAC, durability, scale or Azure timing |
| Functions-host delivery | Actual trigger consumes the queued identifier and persists the expected idempotent result | Host log plus persistence assertion | Flex Consumption or deployed managed identity |
| Competing local runs | Both complete without shared ports, databases or storage state | Parallel-run test | Exhaustive race freedom in Azure |
| Failed setup or test | Non-zero result, diagnostics retained and owned resources stopped | Deliberate negative fixture | Recovery from every workstation failure |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** None for local tool manifests and ignored run state. Docker installation, live-service access or any Azure/vendor call requires separate approval and exact scope.
- **Rollout/activation:** Land pin and doctor; prove orchestration with a protocol fixture; activate each profile only with its real caller; then add its CI/release gate.
- **Rollback/recovery:** Remove the profile invocation and pinned dependency together if its caller is removed; retained product data is unaffected because all local state is disposable.
- **Irreversible risk:** None for local setup. External data transfer or cloud mutation is outside this task.

### Deferred-capability impact

- **Named capabilities with an activation path:** Broader Outlook coverage, outbound email, WhatsApp, EVA API/replacement, estimating/valuation/accounting, Diminution and Commercial cases, guided capture, Tractable/Ravin, AI/vision, address assistance, and external accounts. Custom domain remains conditional `Later`/`unallocated` pending a direct product decision.
- **Stable seam retained:** Capability profiles attach to the existing Core port and real composition-root caller; run identifiers, source identities, versioned external contracts and ignored evidence directories remain transport-neutral.
- **Future migration/replacement:** Each activated feature still needs its own production adapter, caller, live sandbox, contract fixtures and rollout/rollback evidence; a local emulator never removes that work.
- **Activation boundary:** Applies only to the listed non-`Not planned` capabilities: settled product decision plus representative evidence, licence/cost/security approval and a real caller.
- **Permanent boundaries:** Malware scanning; multi-region/zones/private networking; separate staging/QA/UAT/demo; and S1/slots are `Not planned`. No local profile, port, fixture, emulator, container, service, queue, table, endpoint, dependency, configuration, release gate, or ADR/cost path is created for them.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | Planning review | Required profiles, lifecycle and activation rules are defined | Implementation, caller, CI, cloud behavior and acceptance |

## Caller-backed local and live evidence gates

**Evidence state:** Planned

### Authority and decision gate

- **Requirement/decision:** `0.1.0-alpha.1` must prove authenticated staff workflows, durable source custody, Graph intake, Blob/Queue/Worker behavior, SQL concurrency, Box, automatic vehicle-registration reading from ordinary vehicle images, DVLA/DVSA, EVA export, staff MCP, observability, and recovery through their actual callers. Scan-like PDF OCR and the provider API are `Next`/`unallocated` caller gates and do not block `0.1.0-alpha.1`. The complete list remains owned by [remaining requirements](../../product/qdos-alpha-gap.md), not duplicated here as product authority.
- **Confirmed facts:** The current called slice is the development-only manual upload path. Worker, production Blob/Queue, live external adapters, real-browser/authentication, load, restore-objective and alert-delivery evidence remain planned until delivered and run.
- **Decision required before implementation:** Tests must encode the settled Triage states/findings/reply evidence/linking, used-principal-code replacement, permanent action-history boundary, chase/Held-release, reopen, London activity, immutable case identity, exact Sent-item and no-pre-send-review rules. Only automatic mailbox categorisation/email matching awaits the sole combined research decision. Image association stays conservative when evidence is not definitive; inspection address accepts confirmed physical data or exact `Image Based Assessment` without inferring precedence; `0.1.0-alpha.1` email operations remain explicitly unsupported unless required; reversible EVA wire mapping is an owning integration contract validated against operator acceptance, not product ambiguity.

### Owner and dependencies

- **Policy/implementation owner:** The owning Core feature and its real Web or Worker composition root; this task owns evidence classification and gate composition only.
- **Independent evaluator:** A test engineer or reviewer other than the feature implementer; operator acceptance remains a separate authorised state.
- **Prerequisites:** Reproducible profile from the previous task and the intended product caller being present.
- **Consumers/unlocks:** Release decisions, shared-development validation, production readiness and safe activation of later capabilities.

### Caller, contract and change boundary

- **Real or intended caller:** Manual `/Intake/Upload` today; authenticated Razor Pages/API/MCP routes and actual Functions timer/queue triggers as delivered.
- **Input/output:** Genuine local inputs for business-shape evidence and controlled fixtures for protocol/security boundaries produce persisted state, action history/telemetry and an operator-visible outcome. Every evidence record states what it does and does not prove.
- **Ordered decisions and failure behavior:** Unit policy first; adapter contract second; persistence/transaction third; actual HTTP/Functions caller fourth; genuine cohort/holdout where relevant; approved live-service gate; operator acceptance. Unknown input remains reviewable, terminal failure stops visibly, and transient failure retries with a bound.
- **Persistence/migration:** SQLite proves the supported development path; LocalDB proves SQL Server migrations, transactions, constraints and concurrency; Azure SQL remains a live gate. Restore always targets a new disposable database or explicitly approved Azure target.
- **Concurrency/edit ownership:** Exercise eight concurrent staff, duplicate source/queue/API deliveries, atomic reference allocation, outbox replay, two-browser edit leases, stale versions and external-side-effect idempotency.
- **Adapters/side effects:** Local tests use fakes, emulators or controlled loopback contracts; live tests use exact allowlisted non-production resources and non-corpus inputs.
- **Permission/scope guard:** Authentication/role tests deny before the external client is called. Live Graph, Box, Azure and vendor tests name the authority, grant type and exact resource scope and prove a broader or wrong scope is rejected.
- **Operator surface and observability:** Assert the rendered queue/case outcome, permanent business action history where authorised, content-safe correlation and alertable terminal failures. Technical retry detail remains out of operator language.
- **Documentation affected:** Feature-owned test instructions and this plan; operator notes and corpus remain read-only.
- **Replaces/consolidates:** Mock-only completion claims, direct-service-only integration tests and broad green checks presented as end-to-end proof.

### Required local evidence tiers

1. **Static/build/architecture:** Compile the four approved projects, enforce dependency direction and one policy owner, compile Bicep, check dependencies and prevent tracked corpus or secret material. This proves consistency only.
2. **Core/domain:** Literal positive, contradictory, ambiguous and failure examples for intake, references, matching, lifecycle, roles, completeness and case invariants without duplicating policy at the edge.
3. **Parser/adapter contracts:** EML/PDF/DOCX and later approved DOC/MSG handling, corruption/encryption/resource bounds, cancellation, path/integrity safety, stable contract codes and deterministic external HTTP/SDK failures. Microsoft recommends mocking Azure SDK clients at their port boundary for unit tests. [Azure SDK unit-testing guidance](https://learn.microsoft.com/dotnet/azure/sdk/unit-testing-mocking)
4. **SQLite and LocalDB persistence:** Fresh and incompatible schema behavior, committed migrations, transaction rollback, action-history/state/outbox atomicity, allocation concurrency, constraints, pagination, leases, stale versions and backup/restore.
5. **Web/API/MCP caller:** Actual routes reach Core; authentication, antiforgery, validation, scope, idempotency, exception translation and action-history actor are observable.
6. **Functions/Azurite caller:** Actual timer/queue trigger, Blob staging, identifier-only queue messages, duplicate/retry/poison/restart paths and delete-after-Box-confirmation behavior.
7. **Browser/accessibility:** Authenticated end-to-end workflows, dashboard/queue agreement, two-session editing, keyboard/focus/error handling, semantic labels, text-plus-colour states, 200% zoom and supported-browser coverage. Automated axe evidence does not replace manual keyboard and assistive-technology review.
8. **Genuine corpus:** Immutable reviewed cohort plus untouched holdout through the real caller; field-level accuracy, conflicts, unreadable pages and false case/reference outcomes; detailed evidence remains ignored and local.
9. **Security/observability:** Role matrix, secure cookies, lockout, request forgery, denial-before-client-call, dependency and dynamic scanning, correlation, health, redaction and bounded failure metrics.
10. **Performance/concurrency:** Eight concurrent operators, 2,000 cases per month, 2–20+ files per case, the 10 MB boundary, burst/soak behavior and 48,000–480,000+ annual asset-metadata shapes. No release latency threshold is invented without an explicit decision.
11. **Migration/recovery:** Every supported prior schema, idempotent migration scripts, previous-artifact compatibility, local restore to a new database and reconciliation by stable Outlook/Box identities.
12. **Integrated workflow:** Authenticated source receipt through Core, SQL/outbox, actual Worker trigger, adapter outcome, persisted operator view, telemetry and safe replay. Registration or mock-only paths do not satisfy it.

### Local and live service matrix

| Boundary | Local requirement | Required approved live evidence |
|---|---|---|
| ASP.NET Core / App Service | Kestrel, `WebApplicationFactory`, Playwright and local HTTPS | Linux App Service runtime, F1/B1 limits, health platform, restart and managed identity |
| SQL / Azure SQL | SQLite plus disposable LocalDB databases for migrations, locking, allocation, rollback and restore | Entra identity, Azure SQL configuration/throttling, point-in-time restore and the 15-minute RPO/four-hour RTO exercise |
| Blob / Queue / Functions | Azurite plus the actual Functions host for staging, identifier messages, duplicate/poison/restart behavior | Storage RBAC, managed identity, durability, Flex scale/concurrency and platform diagnostics |
| Key Vault / identity | Mock the port; developer credentials only for separately approved development resources | Deployed managed identity, least-privilege RBAC and firewall behavior; [managed identity itself is unavailable locally](https://learn.microsoft.com/dotnet/azure/sdk/authentication/system-assigned-managed-identity) |
| Application Insights / Log Analytics | In-memory OpenTelemetry exporter and optional local Collector | Ingestion, sampling, KQL, retention, alert rules and recipient delivery |
| Graph / Exchange | Kiota fake plus Dev Proxy for paging, retry, auth and throttling; shared allowlist denies unknown mailbox/folder/action before the client call | Approved test mailbox allowlist, Exchange Application RBAC, immutable IDs, delta behavior and exact Sent-item existence. This does not prove recipient delivery or automatic case matching |
| Box | Fake SDK/HTTP contract for folder/file commands, versions, idempotency and failures | Real folder custody, permissions, versions, file requests and retention |
| Document Intelligence | Candidate-routing and response-contract tests using controlled non-corpus fixtures | OCR accuracy, confidence, API drift, cost, throttling and identity. A [disconnected container](https://learn.microsoft.com/azure/ai-services/document-intelligence/containers/disconnected?view=doc-intel-4.0.0) requires specific licensing and is not the default emulator |
| DVLA/DVSA | Deterministic contract, invalid identifier, retry and unavailable-service outcomes | Entitlement, identity and real response behavior |
| EVA | Exact local JSON/image-bundle contract and reconciliation metadata | Operator drag/drop acceptance and later authorised API sandbox |
| Provider API / staff MCP | Real Kestrel endpoints, auth/scope/idempotency, action history and negative HTTP tests | Public HTTPS, canonical MCP resource metadata, hosted OAuth callback and Internet-facing posture |
| Direct authorised-terminal deployment | Bicep compile/lint and local configuration checks | Approved Azure preflight, package/migration identity, deployment, health smoke and rollback; GitHub Actions/OIDC is `Not planned` |
| Backup / recovery | LocalDB backup/restore into a new disposable database | Azure SQL PITR and the `0.1.0-alpha.1` one-time 15-minute RPO/four-hour RTO proof; recurring quarterly recovery is `Not planned` |

### Scope

- **Included:** Test requirements for intake/provenance, extraction/OCR routing, auth/roles/action history, blocked intake and acceptance, immutable case/reference and linked replacement rules, lifecycle/completeness/editing/due/chasing, dashboard/search, Box/DVLA/EVA/email, provider API/MCP, SQL/outbox/queue, health/telemetry, migration/recovery and the full caller journey.
- **Excluded:** Treating mocks as vendor evidence; uploading corpus; production load; automatic acceptance of unresolved business policy; deployment or live calls without exact approval; positive tests for excluded legacy or speculative capabilities.

### Implementation checklist

- [ ] For each delivered feature, name the authoritative rule, Core policy owner, production entry point, persisted result, adapter/side effect, operator-visible outcome and evidence tier.
- [ ] Add focused unit and contract tests before persistence and real-caller tests; consolidate any duplicated policy before adding another caller.
- [ ] Add an actual HTTP or Functions-host test and a deliberate negative/failure fixture for every new permanent guard.
- [ ] Add genuine local cohort/holdout evidence for extraction and matching without modifying or uploading corpus.
- [ ] Add live-service gates only after exact target, data class, permission, cost and rollback approval; keep their results distinct from local verification.
- [ ] Add CI/release jobs by evidence tier: baseline PR checks; Storage/Functions and Chromium when called; trusted local corpus; scheduled/release browser, performance, security and recovery; explicitly approved live integrations.

### Validation checklist

- [ ] Positive, contradiction/ambiguity, transient and terminal cases produce the ordered Core decision, persisted result, action history/telemetry and operator-visible outcome.
- [ ] Prove principal/reference edits fail immediately after allocation; wrong-principal handling makes the original terminal `Created in error`, links one replacement, never reuses either number and refuses reopening the original.
- [ ] Prove used-principal-code direct edits fail; Administrator cutover creates one linked successor, atomically deactivates predecessor, continues the cutover-year next/exhausted state, starts later years at `001`, records reason/history and survives stale/concurrent/fault-injected transaction tests.
- [ ] Prove first chase at the same London local time after seven calendar days, Held remainder preservation/resumption, reasoned reopen to an otherwise-valid nonterminal state, and London-midnight/Monday dashboard boundaries.
- [ ] Prove manual chaser preparation/view/copy is not sent evidence; explicit staff confirmation persists actor/time/case/channel/outcome/optional note once, makes zero outbound calls, rejects unauthorised/stale/closed/`Held` submissions and stores no message body.
- [ ] Prove the complete separate Triage state/finding/correction/reopen/link contract, no-registration `Needs sorting`, no case/reference, and exact allowlisted reply-chain evidence with no subject/registration/manual-selection fallback.
- [ ] Prove the first successful EVA export generation records one `First sent to Engineer` proxy event but not receipt. When automatic report evidence is absent/ambiguous, exact manual link requires a reason; `sentDateTime` is authoritative, discovery/link times stay separate, unlink/relink recomputes events/counts, later Outlook move/delete preserves confirmed finality, and there is no pre-send review gate.
- [ ] Prove permanent action history includes the settled material actions, denials/failures, accepted external evidence and downloads/exports; prove sign-ins use security log and routine view/search/refresh/poll/retry/lease/heartbeat/adapter mechanics use telemetry only.
- [ ] Duplicate and concurrent requests create one business effect; stale editors and wrong-role/wrong-scope actors are refused before side effects.
- [ ] Corrupt, encrypted, unsupported, oversized and expansion-bound inputs remain visible without case/reference creation or silent truncation.
- [ ] Actual Web and Worker entry points reach the same Core policy; direct DI resolution and test-only callers are insufficient.
- [ ] Genuine cohort and holdout reports state field-level results and false case/reference outcomes without exposing source content.
- [ ] Live results name target, time, configuration class, input class and limitation; no local result is relabelled deployed, live-verified or accepted.
- [ ] Repository consistency and product behavior are reported separately.

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Genuine manual intake | The real Web caller persists provenance and the authorised review outcome without creating unsupported case/reference state | Caller integration plus local cohort | Worker, cloud custody or operator acceptance |
| Duplicate queued work | One persisted business effect and one idempotent external intent; exhaustion is visible in the poison/failure path | Functions host, Azurite and LocalDB | Azure timing or outage behavior |
| Eight parallel operators | Atomic reference/outbox behavior and visible lease/stale-version refusal | Parallel LocalDB and two-browser tests | Production capacity or exhaustive race freedom |
| External adapter failure | 401/403/429/5xx/timeout maps to the authorised terminal/transient/unknown outcome without secret/content leakage | Deterministic contract test | Vendor identity, permissions or current service behavior |
| Restore exercise | A new database is restored, migrated and reconciled by stable source identities | Local recovery log | Azure RPO/RTO until the approved live exercise |
| End-to-end release journey | Authenticated entry point reaches one Core owner, persistence/outbox, actual Worker, adapter and operator-visible result with correlation | Integrated local and separately approved live evidence | Stakeholder acceptance until observed and recorded |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** Local tests require none beyond workstation changes. Every Azure/vendor/mailbox/Box mutation or data transfer requires exact target approval; corpus upload is never implied.
- **Rollout/activation:** Add a feature's focused local tests; prove its actual caller; activate the corresponding profile; run approved shared-development evidence; obtain operator acceptance; only then use it as a production release gate.
- **Rollback/recovery:** Disable the new release gate if its caller is removed, while retaining failure evidence; revert adapter/runtime changes through the feature's own rollback plan without deleting source evidence or shared cloud assets.
- **Irreversible risk:** Sending sensitive source material to an external service or foreclosing a named deferral requires direct user approval and, where architectural, an ADR.

### Deferred-capability impact

| Named capability | Local boundary before activation | Activation and remaining live/migration work | Deliberately absent now |
|---|---|---|---|
| Other Outlook mailboxes and mature categorisation | Graph fake/Dev Proxy, per-mailbox identities, delta replay, idempotency and policy-version/correction tests | Settle categorisation governance; approve named mailboxes and Exchange RBAC | Broader Graph grants, rule engine, rule table or editor |
| Automated outbound email/chasers | Graph-send contract, recipient validation, retry/delivery state and permanent action history | Approve sending behavior and an allowlisted test mailbox; manual cadence and exact sent evidence are already settled | Automatic sender |
| WhatsApp automation | Versioned webhook/client fixtures, provenance, consent, duplicates and receipts | Product/provider selection and sandbox approval | WhatsApp client, webhook or queue |
| EVA API or replacement | Versioned contract, reconciliation, idempotent create/update and shadow comparison | Vendor/operator approval and sandbox; migration from manual bundle | EVA client or replacement engine |
| Estimating, valuation, invoicing, accounting and Audatex | Typed money/currency/source/version policy, permissions, action history and contract fakes | Product/commercial/API approval and vendor sandbox | Finance schema, service or workflow |
| Diminution and Commercial | Explicit unsupported outcome; later lifecycle, fields, shared sequence, persistence and browser evidence | Operator-defined workflow and acceptance criteria | Case type/state implementation |
| Guided capture, Tractable and Ravin | Mobile browser matrix, resumable upload, asset provenance/order, consent and duplicates | Vendor selection, licence/security review and sandbox | Vendor client, upload surface or service |
| AI/vision and automated VRM recognition | Deterministic fake, suggestion-only policy, confidence/provenance/correction and frozen local cohort/holdout | Representative accuracy, model/service, licence/cost/security and data-transfer approval | Model client, endpoint, queue, flag or corpus upload |
| DOC/MSG automatic extraction | Safe local parsing fixtures for nesting, corruption, encryption and resource bounds | Human-reviewed genuine cohort and holdout; external service only if later selected | Automatic production parser until evidence exists |
| Address suggestions/maps | Provider fake, provenance, correction and never-auto-accept behavior | Provider/privacy approval and sandbox | Map client, endpoint or stored guess |
| Malware scanning | `Not planned`: no scanner port, fixture, client, quarantine service, or release claim | No activation path | Scanner implementation |
| External/customer accounts | Deny all external-account access; later invitation, recovery, ownership and cross-tenant isolation tests | Tenancy/identity decision, ADR and approved identity environment | External role, registration or tenant schema |
| Custom domain | Hostname-independent auth, local HTTPS, cookie/redirect/HSTS and callback configuration tests | DNS/TLS/OAuth migration and rollback in approved environment | Domain, certificate or hostname dependency |
| Multi-region, zones, private networking, separate staging/QA/UAT/demo, slots/S1 | `Not planned`: no topology, network, slot, staging, or capacity test path | No activation path | Related runtime, resource, release dependency, or ADR/cost gate |
| Graph webhooks | Signature, replay, expiry and duplicate-notification contract tests | Public callback and Graph subscription in approved shared development | Webhook endpoint or subscription |
| PDF-engine replacement | Same frozen cohort/holdout and contract parity suite | Licence/security/maintenance review and single-path cutover | Parallel permanent PDF engines |

SMS, Teams, a customer portal, redaction, signatures, legal hold, subject-request workflows and predecessor application/data migration remain exclusions, not positive testing requirements, until separately authorised.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | Planning review | Local tiers, live boundaries, caller criteria, activation gates, and permanent `Not planned` boundaries are defined | Implementation, current test pass, deployment, live verification and operator acceptance |
