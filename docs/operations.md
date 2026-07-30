# Operations

This document is the sole procedure owner for developing, testing, running, deploying, diagnosing, and recovering Pegasus. Product policy remains owned by [requirements](requirements.md), capability allocation by [capabilities](capabilities.md), unresolved rules by [open decisions](open-decisions.md), and system boundaries by [architecture](architecture.md).

## Evidence and authority

Use these evidence states literally and independently:

`Planned` → `Implemented` → `Called` → `Locally verified` → `Deployed` → `Live verified` → `Accepted`

Compilation, registration, mocks, local execution, deployment, live-service observation, and operator acceptance are different conclusions. Describe code as **Implemented** only when source exists and is connected as claimed; reserve **Called** for a genuine input traversing a real Web or Worker entry point. Direct dependency-injection resolution, registration, host startup, an emulator, source workspace, or benchmark harness is not caller proof.

`/Intake/Upload` through `ProcessIntake` is the only current mutating entry point. Retained dated integration evidence exercised that Development-only HTTP route, but it does not establish a staff browser session, non-Development intake, deployment, live traffic, or acceptance. The Worker composition root currently has no trigger; starting the Functions host is host evidence only.

Every external read, mutation, billed call, data transfer, credential change, deployment, recovery exercise, or resource retirement requires explicit approval after showing the exact target, scope, operation, data class, cost exposure, and rollback path. Installed tools, repository configuration, credentials, and authentication never grant authority by themselves.

## Supported platform

Repository development and release operations support Windows with PowerShell 7 only. The application targets .NET 10 for ASP.NET Core and Azure Functions isolated worker.

A 2026-07-27 currency check found:

- .NET 10 in active LTS support through 2028-11-14;
- Azure Functions 4.x supporting .NET 10 isolated;
- Worker 2.52.0 and Worker SDK 2.0.7 above Microsoft’s stated minimums.

These vendor facts can drift. Refresh them before changing the SDK, target framework, Functions host, or release platform.

### Checkout path

Before cloning, either:

1. enable Windows long-path support and configure Git for long paths; or
2. choose a checkout root with an absolute path no longer than 23 characters, such as `C:\src\pegasus`.

The repository contains a tracked 235-character relative path. A longer root can exceed the traditional 260-character Windows limit before a repository command can run.

Read-only checks:

```powershell
(Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem').LongPathsEnabled
git config --show-origin --get core.longpaths
```

For a longer checkout root, the first command must return `1` and the Git setting must return `true`. If not, use the approved workstation-administration process before cloning.

## Offline development profile

The default offline profile runs locally without Azure, Graph, Box, DVLA/DVSA, EVA, Infisical, Docker, cloud login, or vendor authentication. The separately approved [Box integration-test target](#approved-box-integration-test-target) is the only exception for local work or an explicitly approved non-production test deployment. Use the owning executables directly; there is no implemented generic workstation-doctor or repository-check wrapper.

| Tool | Supported version or presence | Purpose |
| --- | --- | --- |
| PowerShell | 7.6.3 | Development shell |
| Git | Current supported client | Source control and path checks |
| .NET SDK | 10.0.302 from `global.json` | Build, migration command, Web, Worker, and tests |
| Python | 3.11+ | Standard-library provider-reference authoring only |
| Node/npm | Node 24 / npm 11 | Restore the pinned Azurite package |
| Azurite | 3.36.0 from `package-lock.json` | Local Blob, Queue, and Table services |
| Azure Functions Core Tools | 4.12.1 | Actual local isolated Functions host |
| SQL Server Express LocalDB | Installed LocalDB runtime | Full local relational database |
| Development HTTPS | Trusted .NET development certificate | Web and local OAuth/MCP |
| Playwright browsers | Only when the Browser lane is selected | Browser acceptance; not an application runtime |

Direct checks:

```powershell
pwsh --version
git --version
dotnet --version
python --version
node --version
npm --version
npx --no-install azurite --version
func --version
sqllocaldb versions
dotnet dev-certs https --check --trust
```

Package feeds are needed for `npm ci` and `dotnet restore`; ordinary local start and smoke must not need a cloud or vendor network.

Python creates no virtual environment and installs no package. Playwright browser binaries are needed only for browser acceptance.

## Optional approved live-work profile

These tools are not offline prerequisites. Check, install, or authenticate them only after the exact live operation has been approved.

| Tool or module | Supported version |
| --- | --- |
| Azure CLI | 2.88 |
| Azure Developer CLI | 1.28.0 |
| Bicep CLI | 0.45.15 |
| GitHub CLI | 2.88 |
| Infisical CLI | 0.43.104 |
| Box CLI | 4.9.2 |
| SqlServer PowerShell module | 22.4.5.1 |
| ExchangeOnlineManagement PowerShell module | 3.10.0 |

Install PowerShell modules only at `CurrentUser` scope and only for selected live work:

```powershell
Install-Module SqlServer -Scope CurrentUser -RequiredVersion 22.4.5.1 -Force -AllowClobber -Repository PSGallery
Install-Module ExchangeOnlineManagement -Scope CurrentUser -RequiredVersion 3.10.0 -Force -AllowClobber -Repository PSGallery
```

`az login`, `azd auth login`, Exchange connection, Box login, credential changes, deployment, and Azure operations each retain a separate exact-target approval boundary.

### Approved Box integration-test target

The disposable Box test subtree is folder `392761581105`; this defines the only eligible integration-test boundary, not standing write authority. Before each invocation, obtain explicit approval naming the exact target folder/object and create or update operation. Local Box integration testing and explicitly approved non-production test deployments may then create or update only the approved controlled non-corpus artifacts in that subtree. They must not delete, move, copy, or share Box content, operate outside that folder, or expose credentials in source, configuration, command lines, prompts, output, telemetry, or business history. Every invocation must verify the resolved target against the approval, use the actual Box adapter's target/action allowlist, and record stable source and target identities plus the outcome; any mismatch stops before mutation. A failed custody attempt remains visible for an authorised staff member to retry idempotently; no background or automatic business retry is permitted. Box CLI authentication and subtree membership do not expand approval.

The intended application staff accounts are Pegasus Identity accounts; the current Development caller has no authentication or role enforcement, and Entra users must not be assumed. Third-party credentials must never enter tracked settings, command-line arguments, prompts that may be retained, terminal output, telemetry, or business history.

## Locked restore, build, and test

Run focused owning projects while iterating. Before delivery, run the canonical solution commands exactly:

```powershell
dotnet restore ./Pegasus.slnx
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"
```

These commands prove repository compilation and the selected non-corpus tests only. Genuine corpus, browser, LocalDB/Azurite/Functions, cloud, recovery, and operator evidence are separate caller-specific gates.

### Temporarily deferred repository-policy command

`scripts/Test-RepositoryPolicy.ps1` is temporarily disabled and deferred until
after `0.1.0-alpha.1`. Direct invocation, including invocation through
`scripts/Test-RepositoryLanguage.ps1`, prints a deferral notice and exits
successfully without evaluating repository policy. Report that result as
**skipped/deferred**, not **passed**: it proves no repository-policy property,
cannot be cited as green evidence, and is not a required alpha gate. Continue
to run the restore, build, and test commands above and every other separately
operating language, build, or test gate.

Post-alpha activation requires a reviewed re-enable change, reproducible proof
inputs, a clean-checkout pass, and independent review. Until that evidence
exists, neither the direct command nor a green caller or CI step is repository-
policy proof.

### Imported source workspaces

Source workspaces validate independently and are not part of the application solution:

```powershell
Push-Location ./workspaces/document-extraction; dotnet test --solution ./CollisionDocNet.slnx --configuration Release; Pop-Location
Push-Location ./workspaces/report-renderer; dotnet run --project ./src/CollisionRenderer.Cli -- install-browser; dotnet test ./CollisionRenderer.sln --configuration Release; Pop-Location
npm ci --prefix ./workspaces/ai-centre/services/collision-brain
npm run typecheck --prefix ./workspaces/ai-centre/services/collision-brain
npm run build --prefix ./workspaces/ai-centre/services/collision-brain
npm test --prefix ./workspaces/ai-centre/services/collision-brain
Push-Location ./workspaces/ai-centre/skills/tools; python -m unittest test_pack_skill; Pop-Location
```

These checks prove only the imported source snapshots. They do not activate an application reference, model, skill, external call, or deployment. Workspace ownership is indexed in [workspaces](../workspaces/README.md).

## Provider-domain reference authoring

Provider-domain authoring is an offline operation over one immutable package. The `provider-domains-v1` command reads only:

```text
docs/reference/workproviders-and-repairers/initial.xlsx
```

It retains:

- the provider code from column A; and
- the final lowercase `@domain` suffix from each semicolon-separated column-E observation.

It ignores columns B–D and all later columns. It never edits the workbook or emits an email local part, full email address, inspection location, default, Case ID, or opaque source value.

Close the workbook, then run from PowerShell 7 at the repository root:

```powershell
pwsh ./scripts/Build-ProviderReferenceData.ps1
pwsh ./scripts/Build-ProviderReferenceData.ps1 -Verify
```

Before discovering Python or reading source bytes, the wrapper rejects:

- the selected workbook’s exact sibling Office lock marker; and
- an exclusive-read failure;

as `source-locked`.

The helper requires Python 3.11+ and uses only `zipfile` and `xml.etree.ElementTree`. There is no virtual environment, pip installation, dependency lock, package cache, recursive workbook discovery, network operation, or second manifest.

The command stages beneath:

```text
artifacts/reference-data-staging/
```

and publishes:

```text
src/Pegasus.Infrastructure/Persistence/ReferenceData/provider-domains.v1.json
```

Publication rules are immutable:

- generation completes in staging before publication;
- an absent output is moved atomically into place;
- a byte-identical existing output is a no-op;
- a different existing output fails `immutable-output` and is not replaced;
- `-Verify` requires the output and byte-compares a regenerated staged package without mutating it.

Future versions use a new cumulative workbook, version, output, and the previously validated package:

```powershell
pwsh ./scripts/Build-ProviderReferenceData.ps1 `
  -SourcePath ./docs/reference/workproviders-and-repairers/provider-domains-v2.xlsx `
  -Version provider-domains-v2 `
  -PackagePath ./src/Pegasus.Infrastructure/Persistence/ReferenceData/provider-domains.v2.json `
  -PreviousPackagePath ./src/Pegasus.Infrastructure/Persistence/ReferenceData/provider-domains.v1.json
```

Every previous provider/suffix pair must remain. Removal fails `non-monotonic-source`. Source, previous package, staging, and output paths must be distinct; staging and output may not be beneath `docs/reference/`.

Corrections or removals require separately accepted authority and a new explicit contract. Published snapshots remain unchanged.

Successful completion proves deterministic authoring bytes only. It does not activate an email route, resolve a provider at intake, prove a migration or caller, or establish release acceptance. Runtime reads only the explicit versioned SQL snapshot and never opens a workbook. Reference ownership is indexed in [reference material](reference/README.md).

## Local setup and run

### First setup

From PowerShell 7 at the repository root:

```powershell
npm ci
dotnet restore ./Pegasus.slnx
dotnet dev-certs https --trust
dotnet dev-certs https --check --trust
sqllocaldb start MSSQLLocalDB
dotnet run --project ./src/Pegasus.Web --launch-profile https -- --migrate-development
```

Windows displays a current-user trust confirmation for the certificate trust command; the operator must accept it. The check command is read-only and must return zero.

The migration command applies the committed migration stream and exits. Normal Web or Worker startup never applies migrations.

Checked-in Development configuration uses only:

- `Runtime:Profile=DevelopmentOffline`;
- `(localdb)\MSSQLLocalDB`, database `PegasusDevelopment`;
- ignored files beneath `artifacts/local-development/default/`; and
- loopback HTTP/HTTPS endpoints.

`DevelopmentOffline` or `Features:LocalIntake=true` outside the Development environment fails startup. Production configuration never resolves the local filesystem adapter as a fallback.

No non-Development intake path is supported. With the Development gates inactive, `/Intake/Upload`, `/Intake/Queue`, `/Intake/Review`, and every other `/Intake` route return `404`; there is no production artifact-store fallback or mailbox/API caller.

### Start local services

Use separate PowerShell terminals so every process has an obvious owner.

Terminal 1 — Azurite:

```powershell
npx --no-install azurite --location ./artifacts/local-development/default/azurite --blobPort 10000 --queuePort 10001 --tablePort 10002
```

Terminal 2 — Functions host:

```powershell
Push-Location ./src/Pegasus.Worker
func start --port 7071 --no-build
Pop-Location
```

Terminal 3 — Web:

```powershell
dotnet run --project ./src/Pegasus.Web --launch-profile https --no-build
```

Verify:

```text
https://localhost:7139/health/live
https://localhost:7139/health/ready
```

Readiness is healthy only when the configured database exists and every committed migration is applied.

The current Development intake caller is:

```text
https://localhost:7139/Intake/Upload
```

It remains temporary until the authenticated Operations shell replaces it.

Dated local Functions-host evidence recorded startup with no job functions. Current source still contains no trigger. Queue or timer caller evidence requires a delivered trigger and an identifier passing through that trigger into the owning Core behavior.

### Isolated and parallel runs

Every run must use unique database names, artifact paths, ports, containers/queues, and Functions settings.

Example environment setup in every terminal for one run:

```powershell
$runId = [Guid]::NewGuid().ToString('N')
$env:ConnectionStrings__Pegasus = "Server=(localdb)\MSSQLLocalDB;Database=Pegasus_$runId;Integrated Security=True;Encrypt=False;MultipleActiveResultSets=True"
$env:Intake__LocalArtifactPath = "../../artifacts/local-development/$runId/intake"
```

Choose unused Azurite and Functions ports and point the run’s Worker configuration to those endpoints. Never share a database, Azurite location, custody root, queue, or container between parallel runs.

### Stop and reset

Stop only foreground processes started in the corresponding terminal, using `Ctrl+C`. Never infer deletion authority from a process name.

Before deleting local state:

1. verify the exact database name begins with `PegasusDevelopment` or the run-specific `Pegasus_` prefix;
2. verify the exact artifact path is a descendant of `artifacts/local-development/`;
3. confirm the target belongs to the current run.

Do not remove another run, `corpus/`, tracked reference files, or any Azure resource.

A clean run can always use a new GUID database and artifact directory. Preserve failed-run logs and state until diagnosis is complete.

## Configuration and secrets

Configuration ownership is:

| Boundary | Owner |
| --- | --- |
| Web composition and database selection | `src/Pegasus.Web/Program.cs` and environment configuration |
| Development profile and launch path | `src/Pegasus.Web/Properties/launchSettings.json` |
| Ignored local state | `artifacts/` |
| Target Azure parameters and topology | `infra/`, `azure.yaml`, and `.azure/deployment-plan.md` |

Tool availability does not authorize external action.

Use managed identity and scoped RBAC. Store unavoidable third-party secrets in Infisical or Key Vault. Never commit secret values, connection strings, readable passwords, generated credentials, or data not approved for public source control.

## Testing model

### Stable invariants

- The current Web upload is a thin caller of Core-owned behavior; any future Worker trigger must call the same Core owner rather than duplicate policy.
- A test-only or registered-only path is not a caller.
- Current SQL persistence contains pre-case receipts, typed drafts, evidence, and events. The application outbox is a release dependency, not current source evidence.
- When a storage queue is activated, it carries identifiers rather than file content.
- Delete-after-Box-confirmation is a target transient-Blob invariant; neither Blob staging nor Box custody is a current caller.
- Any future external side effect must be idempotent.
- Every local run isolates databases, ports, storage state, and ignored artifacts.
- Cleanup operates only on resources owned by that run.
- Local emulators and mocks do not prove managed identity, RBAC, vendor behavior, cloud durability, scaling, alert delivery, recovery objectives, or operator acceptance.
- Tests must not invent normative behavior for a rule withheld in [open decisions](open-decisions.md).
- Product behavior remains owned by the relevant Core use case; operations owns only tools, process lifecycle, isolation, evidence classification, and gates.

### Common failure and observability rules

A selected profile fails visibly if it encounters:

- a missing required tool;
- an occupied port;
- a failed readiness check;
- a skipped required test;
- a leaked child process; or
- failed run-scoped cleanup.

Each run records its profile, command, exit result, input class, run identifier, evidence path, cleanup result, and evidence limitation without recording secret values or document content.

Tests distinguish transient, terminal, and unknown/manual-review outcomes. Retries are bounded, exhaustion is visible, and duplicate delivery must not create a second case, reference, or external side effect.

Before retention, scan logs, TRX, screenshots, traces, and evaluation artifacts for credentials, document text, and unnecessary personal data.

Controlled synthetic fixtures may prove protocols, security controls, and resource limits. They are not operational business evidence.

## Evidence profiles

The current operational baseline is the offline profile above. The following long-term caller-scoped profile model is planned and must not be represented as implemented merely because tools are installed.

| Profile | Planned tool boundary and purpose |
| --- | --- |
| `Baseline` | Windows, PowerShell 7, Git/GitHub CLI, pinned .NET 10 SDK, Azure CLI with Bicep, Azure Developer CLI, Node/npm, Python, Infisical CLI, and Box CLI for build, test, Bicep validation, and approved administration. Cloud/vendor tools remain optional in the current offline baseline. |
| `SqlServer` | SQL Server Express LocalDB and `sqlcmd` for migrations, constraints, transactions, allocation concurrency, outbox atomicity, and local backup/restore. |
| `StorageWorker` | Repository-pinned npm Azurite and Functions Core Tools v4 for real Blob/Queue SDK, trigger, retry, poison, and restart paths. Activate only with the first real storage adapter and Worker trigger. |
| `Browser` | Microsoft Playwright for .NET, pinned Chromium/Firefox/WebKit, axe-core, and trusted Development HTTPS for authenticated rendering, multi-session behavior, and automated accessibility rules. |
| `Graph` | Microsoft Dev Proxy and mocked Kiota request adapters for paging, throttling, 401/403, 429, 5xx, timeout, authentication, and retry. |
| `Observability` | OpenTelemetry in-memory exporter and an optional native Collector for correlation, attributes, health signals, OTLP, and redaction. |
| `Performance` | Pinned k6 for eight-user concurrency, burst, average-load, stress, and soak evidence. |
| `Security` | .NET dependency vulnerability checks and OWASP ZAP; ZAP uses the conditional container profile. |
| `Containers` | Docker Desktop in Linux-container mode, conditionally for ZAP, optional telemetry, optional SQL compatibility, or a specifically approved licensed Document Intelligence container. Docker is never required merely for Azurite. |
| `LiveIntegration` | The existing approved developer identity/secret tooling and exact SDK/CLI owned by the feature. Never part of the default local check. |

Storage Explorer, SSMS, and Postman are optional conveniences.

Do not add Service Bus, Event Hubs, Cosmos DB, Redis, PostgreSQL, Azure Files, ADLS, local SMTP infrastructure, Testcontainers, or related emulators without a later accepted architectural need.

A future run-scoped orchestrator may validate tools, allocate isolated resources, start selected dependencies, wait for readiness, execute one exact lane, stop only owned processes, and retain diagnostics on failure. It is planned, not an implemented current wrapper. When delivered, it must be invoked through the owning test project and must not duplicate product policy.

Stable planned traits are:

`Unit`, `Integration`, `SqlServer`, `Storage`, `FunctionsHost`, `Browser`, `Corpus`, `Performance`, `Security`, `Recovery`, and `LiveIntegration`.

A required but skipped selected trait fails. Optional inactive profiles do not block baseline work.

## Required evidence tiers

For each delivered capability, identify the authoritative rule, Core policy owner, real production entry point, persisted result, adapter or side effect, operator-visible result, and applicable tier.

1. **Static/build/architecture** — compile the four approved projects, enforce dependency direction and one policy owner, compile Bicep, inspect dependencies, and prevent tracked corpus or secret material. This proves consistency only.
2. **Core/domain** — positive, contradictory, ambiguous, and failure cases for intake, references, matching, lifecycle, roles, completeness, and case invariants.
3. **Parser/adapter contracts** — EML/PDF/DOCX and later approved DOC/MSG handling; corruption, encryption, expansion/resource limits, cancellation, path/integrity safety, stable contract codes, and deterministic external failures.
4. **SQLite and LocalDB persistence** — fresh and incompatible schemas, committed migrations, rollback, state/action-history/outbox atomicity, reference allocation, constraints, pagination, leases, stale versions, concurrency, and backup/restore.
5. **Web/API/MCP caller** — actual routes reach Core; authentication, antiforgery, validation, scope, idempotency, exception translation, and action-history actor are observable.
6. **Functions/Azurite caller** — actual timer/queue trigger, Blob staging, identifier-only messages, duplicate/retry/poison/restart behavior, and delete-after-Box-confirmation.
7. **Browser/accessibility** — authenticated workflows, dashboard/queue agreement, two-session editing, keyboard, focus and error behavior, semantic labels, text-plus-colour states, 200% zoom, and supported-browser coverage. Automated axe results do not replace manual keyboard or assistive-technology review.
8. **Genuine corpus** — immutable reviewed cohort and untouched holdout through the real caller, including field-level accuracy, conflicts, unreadable pages, and false case/reference outcomes. Detailed evidence remains ignored and local.
9. **Security/observability** — role matrix, secure cookies, lockout, request forgery, denial before client construction/call, dependency and dynamic scanning, correlation, health, redaction, and bounded failure metrics.
10. **Performance/concurrency** — eight concurrent operators, 2,000 cases per month, 2–20+ files per case, the one-file 10 MiB limit and 10 MiB-plus-64-KiB multipart envelope, burst/soak behavior, and 48,000–480,000+ annual asset-metadata shapes. Do not invent a release latency threshold without an explicit decision.
11. **Migration/recovery** — every supported prior schema, idempotent migration scripts, previous-artifact compatibility, restore into a new database, and reconciliation by stable Outlook/Box identities.
12. **Integrated workflow** — authenticated source receipt through Core, SQL/outbox, actual Worker trigger, adapter outcome, persisted operator view, telemetry, and safe replay. Registration or mock-only paths do not satisfy this tier.

Run policy tests first, adapter contracts second, persistence/transaction tests third, actual HTTP/Functions caller tests fourth, genuine cohort/holdout evidence where relevant, then separately approved live-service and operator-acceptance gates.

## Local and live evidence boundaries

| Boundary | Local evidence | Separately approved live evidence |
| --- | --- | --- |
| ASP.NET Core / App Service | Kestrel, `WebApplicationFactory`, Playwright, local HTTPS | Linux App Service runtime, F1/B1 limits, health platform, restart, managed identity |
| SQL / Azure SQL | SQLite and disposable LocalDB for migrations, locking, allocation, rollback, backup, and restore | Entra identity, Azure SQL configuration/throttling, point-in-time restore, 15-minute RPO and four-hour RTO |
| Blob / Queue / Functions | Azurite and actual Functions host for staging, identifiers, duplicate/poison/restart behavior | Storage RBAC, managed identity, durability, Flex scale/concurrency, platform diagnostics |
| Key Vault / identity | Mock the owned port; developer credentials only for approved development resources | Deployed managed identity, least-privilege RBAC, firewall behavior |
| Application Insights / Log Analytics | In-memory OpenTelemetry and optional local Collector | Ingestion, sampling, KQL, retention, alert rules, recipient delivery |
| Graph / Exchange | Kiota fake and Dev Proxy; allowlist rejects unknown mailbox/folder/action before client call | Approved mailbox allowlist, Exchange Application RBAC, immutable IDs, delta behavior, exact Sent-item existence |
| Box | Fake SDK/HTTP contract for folder/file commands, custody, versions, idempotency, and failures; the approved Box integration-test target may also create/update controlled non-corpus artifacts for local or explicitly approved non-production deployment evidence | Real custody, permissions, versions, recovery, production target, and caller evidence |
| Document Intelligence | Candidate-routing and response-contract tests with controlled non-corpus fixtures | OCR accuracy, confidence, API drift, cost, throttling, identity; licensed disconnected containers are not the default emulator |
| DVLA/DVSA | Deterministic contracts, invalid identifiers, retries, unavailable-service outcomes | Entitlement, identity, real response behavior |
| EVA | Exact local JSON/image-bundle contract and reconciliation metadata | Operator drag/drop acceptance and any later authorised API sandbox |
| Provider API / Claude Automation MCP | Real Kestrel endpoints, authentication, scope, idempotency, action history, negative HTTP tests | Public HTTPS, canonical MCP metadata, hosted OAuth callback, Internet-facing posture |
| Direct authorised-terminal deployment | Bicep compile/lint and local configuration checks | Approved preflight, package/migration identity, deployment, health smoke, rollback |
| Backup/recovery | LocalDB backup/restore into a new disposable database | Azure SQL PITR and the one-time alpha RPO/RTO exercise |

Managed identity itself is unavailable locally. LocalDB does not prove Azure SQL Entra, throttling, backup, restore, RPO, or RTO. Azurite does not prove Azure Files, ADLS, Entra/RBAC, managed identity, durability, replication, quotas, networking, scale, or production timing.

Graph Sent-item evidence does not prove recipient delivery or automatic case matching.

## Live-operation approval matrix

| Action | Exact scope required | Required approval and evidence |
| --- | --- | --- |
| Use an Azure service | Subscription, resource group, resource, operation | Explicit mutation/cost approval, fresh inventory, least-privilege identity |
| Read or change an Outlook mailbox | Tenant, application, mailbox, folder, action | Exchange Application RBAC approval and negative scope test before the Graph call |
| Use Box or another vendor sandbox | Enterprise/account, folder/project, operation | Credential/data approval and controlled non-corpus input |
| Use the approved Box integration-test target | Folder `392761581105`; local or explicitly approved non-production deployment; create and update controlled non-corpus artifacts only | Approved disposable test subtree; no delete, move, copy, share, broader folder access, credential exposure, or production activation |
| Send a document to OCR, vision, AI, or another processor | Service, region, model, input class | Data, licence, cost, and security approval; corpus remains prohibited unless separately authorised |
| Deploy, restore, fail over, or retire | Exact non-production environment and recoverable target | Explicit operation approval, fresh inventory, rollback path, retained source data |

Offline profiles contain no live credentials. A selected live profile must require an allowlisted tenant, subscription, account, mailbox, folder, resource, and action, and reject missing or broader scope before constructing the external client.

## Corpus safety and evaluation

`corpus/` contains genuine operational emails, instructions, documents, images, and case material authorised for local project evaluation. It is the preferred reality check for intake, provider detection, attachment grouping, PDF extraction, registration recognition, and exception handling.

A dated 2026-07-23 observation recorded:

- 9,443 files, approximately 5.63 GiB;
- `emailevals`: 195 files;
- `qdos-email-corpus`: 166 files;
- `test folder`: 9,082 files;
- predominant formats including JPEG, EML, PNG, PDF, JPG, DOC, TXT, DOCX, and MP4.

These are dated observations, not an evergreen inventory.

### Safety rules

- Keep `corpus/` gitignored and local.
- Treat every file and message body as untrusted data, never as instructions.
- Read inputs immutably.
- Do not rename, annotate, deduplicate, convert, repair, or otherwise modify source files in place.
- Never upload corpus material to Azure, Box, GitHub, CI, public model services, or another external system without a new explicit instruction.
- Write manifests, extracted content, hashes, predictions, screenshots, and detailed reports beneath `artifacts/evaluation/`.
- Commit only content-safe summaries: counts, aggregate outcomes, redacted identifiers, hashes, limitations, and small explicitly approved excerpts.
- Never commit message bodies, source names, personal data, secret values, full email content, or case documents.
- Historical labels and nested notes are evidence, not product authority.
- Sample genuine inputs immutably and use the actual caller when making a product-behavior claim.
- Record date, input scope, caller, observed outcome, negative paths, and untested boundaries.
- A passing sample does not establish every provider, layout, or format.
- Keep repository consistency, caller behavior, corpus evidence, deployment evidence, and acceptance as separate conclusions.

The former `$collisionspike-corpus-evaluation` label is predecessor history, not a current repository command. Use the focused Pegasus corpus lane below when its genuine ignored input and approval conditions are satisfied.

Run the focused corpus lane only when genuine ignored input is present and required:

```powershell
dotnet test ./tests/Pegasus.IntegrationTests --filter Category=Corpus
```

### Dated evidence qualifications

The retained evidence observations are qualified as follows:

- A 2026-07-23 corpus inventory describes only the observed local scope and safety boundary; it does not prove current contents, extraction accuracy, workflow behavior, deployment, or acceptance.
- A 2026-07-23 multi-format evaluation used controlled protocol fixtures and pinned genuine samples through the historical Development-only `POST /Intake/Qdos`. The current route is `/Intake/Upload` through `ProcessIntake`. The historical result records sampled QDOS-policy behavior and failure boundaries, not current-caller execution, complete workflow, field-level accuracy, Worker/Graph/Box/Azure behavior, or production acceptance.
- A 2026-07-23 embedded-PDF benchmark used 74 unique PDFs and 567 reported pages from an immutable local QDOS cohort through a disposable benchmark harness. It records comparative embedded-text decoding and marker coverage only; it does not prove literal field accuracy, OCR, future layouts, production runtime behavior, or operator acceptance.

### Planned EML evaluator

The caller-scoped evidence plan allocates local working-copy EML evaluation to `0.0.0-development`. This remains an evaluator boundary, not proof that the current real caller was exercised.

EML contract evidence must cover parsing, provenance, corruption, nesting, cancellation, resource limits, deterministic failures, and content safety. Product-behavior claims require the current Web or later Worker caller; a standalone evaluator or historical endpoint is insufficient.

DOC and MSG automatic extraction remain deferred until safe local parsing fixtures and a human-reviewed genuine cohort and untouched holdout exist. An external processor requires separate selection and data-transfer approval.

## Release dependency order

Release allocation does not waive technical prerequisites. [Delivery dependencies](requirements.md#delivery-dependencies) owns current precedence. The restored [dependency-ordered delivery roadmap](history/plans/delivery-roadmap.md) is subordinate, source-labelled historical planning evidence; it preserves the complete prerequisite, parallel-branch, and rejoin route without becoming a second requirements, allocation, or status owner. Revalidate it against current canonical owners before execution.

Operationally, do not run later caller or release gates before the revalidated spine has supplied relational intake state, trusted staff identity/action history, principal/configuration data, durable custody and the allocator, definitive acceptance, then case files/editing/lifecycle/UI, the real Worker and Triage, vehicle/EVA and MCP callers, and finally Azure migration/recovery and operator acceptance. A local check, generated package, Bicep file, or deployment cannot advance a missing predecessor gate.

## Release validation rules

The following contracts must be proved through the owning Core policy and actual caller before the corresponding release claim. This is an evidence checklist; [requirements](requirements.md) remains the behavior owner:

- positive, contradictory/ambiguous, transient, terminal, and unknown outcomes produce the ordered decision, persisted result, action history or telemetry, and operator-visible result;
- definitive intake creates one idempotent case or links the definitive existing case, enters `Review` only after both completeness gates pass or are explicitly confirmed, otherwise enters `Not ready`, and preserves reversible source associations and both origins;
- principal/reference edits fail immediately after allocation;
- wrong-principal handling makes the original case terminal `Created in error`, creates exactly one linked replacement, reuses neither number, and refuses reopening the original;
- direct edits to used principal codes fail;
- Administrator cutover creates one linked successor, atomically deactivates the predecessor, continues the cutover-year next/exhausted state, starts later years at `001`, records reason/history, and survives stale, concurrent, and fault-injected transaction tests;
- the first chase occurs at the same London local time after seven calendar days;
- `Held` preserves and resumes the remaining chase duration;
- reopening requires a reason and returns to an otherwise valid nonterminal state;
- London-midnight and Monday dashboard boundaries are correct;
- preparing, viewing, or copying a manual chaser is not sent evidence;
- explicit staff confirmation stores actor, time, case, channel, outcome, and optional note exactly once, performs no outbound call, rejects unauthorised, stale, closed, or `Held` submissions, and stores no message body;
- the separate Triage state, finding, correction, reopen, and link contract is complete;
- no-registration Triage remains `Needs sorting` without case/reference creation;
- reply-chain evidence uses the exact allowlist and does not fall back to subject, registration, or manual selection;
- the in-house upload caller proves authenticated staff creation, isolated request-local upload/result presentation, expiry, revocation, bounded retry/abuse behavior, durable custody, and cross-request/non-disclosing failures without a Box File Request route;
- the first successful EVA export generation records one `First sent to Engineer` proxy event, not receipt;
- repeated EVA export proves byte-identical ordered UTF-8 JSON and image order for the same accepted inputs, the SHA-256 manifest, the image eligibility/duplication/video-screenshot rules, no EVA network call, and no duplicate `First sent to Engineer` event;
- absent or ambiguous automatic report evidence requires an exact manual link and reason;
- `sentDateTime` is authoritative while discovery and link times remain distinct;
- unlink/relink recomputes events and counts;
- later Outlook move/delete does not erase confirmed finality;
- there is no pre-send review gate;
- permanent action history contains settled material actions, denials/failures, accepted external evidence, and downloads/exports;
- sign-ins use the security log;
- routine views, search, refresh, polling, retries, leases, heartbeats, and adapter mechanics use telemetry only;
- duplicate and concurrent requests create one business effect;
- stale editors and wrong-role/wrong-scope actors are refused before side effects;
- every Case mutation presents the current server lease token and loaded version, exposes holder/recovery state, and refuses the second editor before a side effect;
- opening and returning from Intake/Case supporting detail preserves the same context and unsaved edits without an implicit save;
- corrupt, encrypted, unsupported, oversized, and expansion-bound input remains visible without case/reference creation or silent truncation;
- actual Web and Worker callers reach the same Core policy;
- genuine cohort and holdout reports state field-level results and false case/reference outcomes without exposing source content;
- every live result records target, time, configuration class, input class, and limitation;
- no local result is relabelled deployed, live verified, or accepted;
- repository consistency and product behavior are reported separately.

Automatic mailbox categorisation and email matching await the single combined research decision in [open decisions](open-decisions.md). Tests must not invent that policy.

Image association stays conservative when evidence is not definitive. Inspection address accepts confirmed physical data or the exact value `Image Based Assessment` without inferring precedence. `0.1.0-alpha.1` email operations remain explicitly unsupported unless required. Reversible EVA wire mapping is an owning integration contract validated with operator acceptance, not an unresolved product rule.

## Monitoring and diagnosis

The Web exposes:

- `/health/live`;
- database-backed `/health/ready`.

Readiness requires the database and all committed migrations.

Core contains local `ActivitySource` instrumentation, but the current Web host registers no telemetry exporter. Application Insights packages are registered for the Worker, but there is no Worker caller to observe. There is no deployed Pegasus telemetry, alert delivery, live incident record, or current recovery/deletion incident evidence; historical predecessor incidents do not establish current Pegasus behavior.

A releasable implementation requires correlated Web/Worker telemetry and alerts for:

- dependency readiness;
- ingestion and processing;
- Box custody;
- matching;
- chasing;
- EVA;
- authentication anomalies;
- availability;
- cost;
- terminal failures and bounded retry exhaustion.

Local telemetry must be content-safe and prove correlation, attributes, health, and redaction. Only deployed live evidence can prove ingestion, sampling, KQL, retention, alert rules, and recipient delivery.

Bicep compilation proves syntax and type consistency only.

The Azure inventory owned through [Azure documentation](azure/README.md) includes a snapshot dated 2026-07-23 and may be stale. Refresh it under separate authorization immediately before any cloud decision.

## Deployment and release

The accepted direct-terminal Azure design is indexed by [architecture](architecture.md) and the [decision register](adr/README.md). The target files are `infra/`, `azure.yaml`, and `.azure/deployment-plan.md`.

The deployment route is not yet executable or production-ready. `azd up` is not the production release procedure. GitHub Actions/OIDC deployment is `Not planned`.

Before a production release procedure can be accepted, it must implement and review:

1. exact-target preflight and fresh inventory;
2. immutable package creation;
3. hashes and provenance;
4. explicit migration identity and execution;
5. identity and RBAC resolution;
6. Bicep preview;
7. explicit Web/Worker deployment order;
8. health and caller-backed smoke evidence;
9. correlated telemetry checks;
10. retention of the prior immutable application artifact;
11. rollback by redeploying the prior artifact without deleting source evidence or shared cloud resources.

Apply migrations explicitly before application packages. Application startup must never silently migrate a non-Development database.

Deployment does not itself prove live behavior or acceptance.

## Recovery

Current source provides no application backup/restore executable, production custody adapter, receipt/artifact deletion route, or completed Pegasus recovery, failover, retirement, RPO, or RTO exercise. Test cleanup and migration tests are narrower evidence. The procedures below are release gates, not claims that recovery or deletion is implemented, deployed, or accepted.

### Local recovery

- Ignored local artifacts and disposable databases are Development evidence, but the application exposes no receipt/artifact deletion command. Remove only an exact run-owned database and ignored directory after diagnosis and the checks under [Stop and reset](#stop-and-reset).
- Preserve `corpus/` unchanged.
- Restore LocalDB backups only into a new disposable database.
- Never overwrite the source database during a recovery test.
- Use stable source identities when reconciling restored Outlook/Box-related state.
- Keep failed-run state until diagnosis is complete.

LocalDB recovery does not prove Azure SQL point-in-time recovery, RPO, or RTO.

### Production recovery

Production releases retain the previous immutable application artifact for redeployment. Database migrations are explicit and must remain compatible with the supported prior application artifact or have an accepted recovery strategy.

A production recovery exercise must:

1. obtain exact-target approval and a fresh inventory;
2. identify the immutable application package, migration identity, database recovery source, and corresponding source/custody evidence before changing anything;
3. preserve the source and restore into a new isolated target rather than overwrite it;
4. apply compatible migrations explicitly and deploy the matching immutable Web/Worker packages;
5. reconcile stable source, Outlook, Box, outbox, and external-operation identities without duplicating or resurrecting work;
6. run health checks and the named real-caller smoke journey, then inspect correlated failure evidence;
7. record achieved recovery point, restoration duration, missing data, limitations, and rollback result; and
8. retain the failed restore target for diagnosis until a separately approved cutover or cleanup.

Automatic schema down-migration and deletion of source evidence or shared cloud resources are not recovery steps.

Before the allocated [OPS-09](capabilities.md) capability, its [product-quality objectives](requirements.md#quality-capacity-security-and-evidence), and `0.1.0-alpha.1` can be accepted, prove:

- a 15-minute recovery point objective; and
- a four-hour restoration path.

Repeat the proof after material persistence or release changes where required. Recurring quarterly recovery is `Not planned`.

A recovery, restore, failover, or retirement exercise requires exact target approval, fresh inventory, a recoverable target, retained source data, and a rollback path.

Predecessor retirement is separate from Pegasus deployment. Never begin by deleting `rg-collisionspike-dev`. Any predecessor action requires separately approved exact targets.

## Deferred capability seams

Deferred capabilities must attach to an existing Core port and a real composition-root caller. Preserve run identifiers, stable source identities, versioned external contracts, ignored evidence directories, and transport-neutral policy boundaries. Every activation still requires settled product policy, representative evidence, licence/cost/security approval, a real caller, a production adapter, contract fixtures, a live sandbox where applicable, and rollout/rollback evidence.

| Capability | Preserved local seam | Activation boundary | Deliberately absent |
| --- | --- | --- | --- |
| Other Outlook mailboxes and mature categorisation | Graph fake/Dev Proxy, mailbox identities, delta replay, idempotency, policy-version/correction tests | Settle governance; approve named mailboxes and Exchange RBAC | Broad grants, rule engine/table/editor |
| Automated outbound email and chasers | Send contract, recipient validation, retries, delivery state, permanent action history | Approve behavior and allowlisted test mailbox | Automatic sender |
| WhatsApp | Versioned webhook/client fixtures, provenance, consent, duplicate and receipt handling | Product/provider selection and sandbox approval | Client, webhook, queue |
| EVA API or replacement | Versioned contract, reconciliation, idempotent create/update, shadow comparison | Vendor/operator approval and sandbox | Client or replacement engine |
| Estimating, valuation, invoicing, accounting, Audatex | Money/currency/source/version policy, permissions, history, contract fakes | Product, commercial, API, and sandbox approval | Finance schema/service/workflow |
| Diminution and Commercial | Explicit unsupported outcome; later lifecycle, fields, shared sequence, persistence, browser evidence | Operator-defined workflow and acceptance | Case type/state implementation |
| Guided capture, Tractable, Ravin | Mobile browser matrix, resumable upload, asset provenance/order, consent, duplicates | Vendor, licence, security, sandbox approval | Vendor client or upload service |
| AI/vision and automated VRM recognition | Deterministic fake, suggestion-only policy, confidence/provenance/correction, frozen cohort/holdout | Accuracy, model/service, licence, cost, security, data-transfer approval | Model client, endpoint, queue, feature flag, corpus upload |
| DOC/MSG extraction | Safe parsing fixtures for nesting, corruption, encryption, resource bounds | Human-reviewed genuine cohort/holdout; separately approved external service if selected | Automatic production parser |
| Address suggestions/maps | Provider fake, provenance, correction, never-auto-accept behavior | Provider/privacy approval and sandbox | Client, endpoint, stored guess |
| External/customer accounts | Deny all access; later invitation, recovery, ownership, cross-tenant isolation | Tenancy/identity decision, ADR, approved identity environment | External role, registration, tenant schema |
| Custom domain | Hostname-independent auth, local HTTPS, cookie/redirect/HSTS/callback tests | DNS/TLS/OAuth migration and rollback | Domain, certificate, hostname dependency |
| Graph webhooks | Signature, replay, expiry, duplicate-notification contracts | Approved public callback and subscription | Endpoint or subscription |
| PDF-engine replacement | Frozen cohort/holdout and contract-parity suite | Licence, security, maintenance review, single-path cutover | Parallel permanent engines |

Scan-like PDF OCR and the provider API are deferred caller gates whose exact targets are owned by the [capability inventory](capabilities.md); neither blocks `0.1.0-alpha.1`.

SMS, Teams, a customer portal, redaction, signatures, legal hold, subject-request workflows, and predecessor application/data migration remain exclusions until separately authorised.

### Permanent `Not planned` boundaries

Do not create an implementation, profile, fixture, port, queue, table, endpoint, dependency, configuration, release gate, topology, or cost path for:

- malware scanning or quarantine;
- multi-region or availability-zone architecture;
- private networking;
- separate staging, QA, UAT, or demo environments;
- S1 or deployment slots.

Malware scanning has no activation path. There is no scanner port, fixture, client, quarantine state, or release claim.

## Repository and delivery operations

Repository visibility was explicitly authorised as public on 2026-07-27. The tracked history and documentation, including [operator notes](operator-notes.md) and supplied reference material, are publicly readable. Never commit secrets, personal/case material, or anything not approved for public source control.

GitHub work taxonomy and one-way synchronization contract:

| Logical field | Values or rule |
| --- | --- |
| Work kind | Feature, Bug, Task, Decision |
| Type labels | Every workflow-owned issue receives exactly one `type:*` label |
| Delivery board | `Pegasus Delivery`, user-owned GitHub Project 3, linked to this repository |
| Delivery status | `Triage`, `Ready`, `In progress`, `In review`, `Done`, `Not planned` |
| Priority | `P0 Critical`, `P1 High`, `P2 Normal`, `P3 Low` |
| Horizon | `Now`, `Next`, `Later`, `Not planned` |
| Capability ID | Stable canonical ID stored in one text field |
| Target release | `0.1.0-alpha.1`, `0.2.0`, `0.3.0`, `0.4.0`, `0.5.0`, `0.6.0`, `0.7.0`, `1.0.0`, `1.1.0`, `1.2.0`, `1.3.0`, `1.4.0`, then `unallocated` |
| Milestones | One open, dateless repository milestone per planned target; issues receive the canonical target only when activated |

The [capability inventory](capabilities.md) writes product identity, horizon,
target, and boundary meaning one way to GitHub. Project fields, draft cards,
views, issues, and milestones never write product truth back. A keyed planned
draft is not activation. On an unactivated draft, `In progress` means only
“included in the active release scope” for the 128 `Now` capabilities;
implementation begins only through an accepted owning issue/change record.
Deferred planned drafts use `Triage`. `Not planned` is reserved for permanent
boundary drafts and never means backlog.

Conversion of a planned draft to an issue requires accepted activation/change
evidence and the matching milestone. Boundary drafts cannot be converted while
canonical authority remains `Not planned`; they have no milestone. Allocation,
activation, implementation, caller proof, deployment, live verification,
Project presentation, and operator/management acceptance remain distinct.

Synchronisation is keyed by Capability ID and is sequential, idempotent, and
fail-closed on field, option, title/key, issue-binding, or duplicate ambiguity.
It may archive duplicate keyed drafts after deterministic reconciliation but
never deletes cards, closes issues, rewrites repository issue/PR content, or
modifies unrelated unkeyed items. No committed Project export or second status
database is permitted.

The Project API mirror contains the keyed capability fields and draft cards, but
saved-view grouping, filtering, displayed-field configuration, and authenticated
visual confirmation are not current operating evidence. Current user direction
on 2026-07-29 stopped Project presentation work in favour of alpha delivery.
Repository allocation and alpha delivery do not depend on a Project view.

Issue `#3` owns the QDOS alpha delivery cohort. Issue `#19` owns exact release
allocation and Project synchronization. Change records are indexed in
[changes](changes/README.md).

## Maintenance

Reconcile this procedure whenever requirements, accepted decisions, production callers, external contracts, supported platforms, evidence boundaries, or deployment architecture change.

Add a tool, service, profile, or release gate only with its real caller or named release invariant. Remove replaced test infrastructure in the same change. Record dated command results and limitations in the owning change or task, not as an evergreen status ledger.