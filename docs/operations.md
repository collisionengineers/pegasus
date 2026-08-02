# Operations

This document is the sole procedure owner for developing, testing, running, deploying, diagnosing, and recovering Pegasus. Product policy remains owned by [requirements](requirements.md), capability allocation by [capabilities](capabilities.md), unresolved rules by [open decisions](open-decisions.md), and system boundaries by [architecture](architecture.md).

## Evidence and authority

Use these evidence states literally and independently:

`Planned` → `Implemented` → `Called` → `Locally verified` → `Deployed` → `Live verified` → `Accepted`

Compilation, registration, mocks, local execution, deployment, live-service observation, and operator acceptance are different conclusions. Describe code as **Implemented** only when source exists and is connected as claimed; reserve **Called** for a genuine input traversing a real Web or Worker entry point. Direct dependency-injection resolution, registration, host startup, an emulator, source workspace, or benchmark harness is not caller proof.

The authenticated `/Intake` `ReceiveIntake` POST handler through `ProcessIntake` is the manual HTTP intake entry point (the former Development-only `/Intake/Upload` page is retired). The Worker has implemented timer and queue-triggered callers for intake dispatch, inbox polling, due work, sent evidence, staged-artifact reconciliation, and external work. Those source-level callers are not deployment, live traffic, or acceptance evidence; starting a Functions host alone remains host evidence only.

Every external read, mutation, billed call, data transfer, credential change, deployment, recovery exercise, or resource retirement requires explicit approval after showing the exact target, scope, operation, data class, cost exposure, and rollback path. Installed tools, repository configuration, credentials, and authentication never grant authority by themselves.

## Supported platform

Repository development supports Windows with PowerShell 7 and Linux with
PowerShell 7. The application targets .NET 10 for ASP.NET Core and Azure
Functions isolated worker.

Pegasus is developed on **one** platform per workstation. Where this
documentation shows a Windows form and a Linux form, run the one matching your
workstation. Nothing here requires or supports mixing the two in a single run,
checkout, or evidence record.

Release operations remain Windows-only. The migration bundle is built for
`win-x64` and applied from the authorised release terminal, which is a fixed
release-route decision recorded in ADR-0007, not a development-platform
requirement. Web and Worker packages are `linux-x64` and build identically on
either platform.

Hosted continuous integration runs `windows-latest` only. Linux development is
supported by these procedures and is not proved by any automated gate: a Linux
result is developer evidence, not repository-check evidence, until a Linux job
exists and passes.

### Platform capability differences

These are technical facts about what each platform can do for this repository,
not a preference. Choose the platform that suits the work in front of you.

What Linux gives this project that Windows does not:

| Capability | Why it matters here |
| --- | --- |
| Runtime parity with production | Web and Worker deploy to Linux, so a Linux workstation runs the same runtime as the deployed application. |
| A container runtime without Docker Desktop | The local database, and the AI Centre pgvector profile that has never been exercised, both need containers. |
| `poppler-utils` (`pdftoppm`) | Already required by `workspaces/report-renderer/scripts/visual-regression.ps1`, and packaged on Linux. |
| `fonts-liberation` and `fonts-dejavu-core` | The exact fonts the renderer's container image installs, so local PDF glyph metrics match the deployed container. |
| `perf` and `lldb` beside `dotnet-trace`, `dotnet-counters`, `dotnet-dump` and `dotnet-gcdump` | Deeper diagnosis for the `Performance` evidence profile. |
| No long-path constraint | The repository's longest tracked relative path (about 122 characters) needs no configuration. |

What Windows gives this project that Linux does not:

| Capability | Why it matters here |
| --- | --- |
| SQL Server Express LocalDB | Zero-configuration local database with integrated security and no container. |
| Microsoft Edge Stable with Windows Narrator | The named accessibility evidence tooling. This is a release gate, and it is Windows-bound. |
| `dotnet dev-certs https --trust` | Trust works directly. On Linux it populates per-user NSS and OpenSSL stores and needs `libnss3-tools` plus `SSL_CERT_DIR`. |
| The `win-x64` migration bundle and authorised release terminal | Fixed by ADR-0007; see above. |
| The Entra interactive authentication broker, and the `SqlServer` and `ExchangeOnlineManagement` modules | Used by the approved live-work profile. |
| `scripts/email-eval-desktop` and `CollisionRenderer.Gui` | These target `net10.0-windows` with Windows Forms and WinUI 3 respectively. Neither framework has a Linux implementation, so both are Windows-only by construction. |

A 2026-07-27 currency check found:

- .NET 10 in active LTS support through 2028-11-14;
- Azure Functions 4.x supporting .NET 10 isolated;
- Worker 2.52.0 and Worker SDK 2.0.7 above Microsoft’s stated minimums.

These vendor facts can drift. Refresh them before changing the SDK, target framework, Functions host, or release platform.

### Checkout path

The repository's longest tracked relative path is about 122 characters, and
build output nests further beneath project directories.

#### On Windows

Before cloning, either:

1. enable Windows long-path support and configure Git for long paths; or
2. choose a reasonably short checkout root, such as `C:\src\pegasus` — roots up to about 130 characters leave headroom for the tracked tree, though generated build paths benefit from shorter roots.

A very long root can exceed the traditional 260-character Windows limit before a repository command can run.

Read-only checks:

```powershell
(Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem').LongPathsEnabled
git config --show-origin --get core.longpaths
```

For a longer checkout root, the first command must return `1` and the Git setting must return `true`. If not, use the approved workstation-administration process before cloning.

#### On Linux

No configuration is required. The path limit is 4096 characters, so the tracked paths impose no constraint on the checkout root.

## Offline development profile

Pegasus supports a reproducible `Offline` profile on Windows or Linux with
PowerShell 7.6.3 or later, .NET SDK 10.0.302, Python 3.11+, Node 24/npm 11, the
repository-pinned Azurite 3.36.0, Functions Core Tools 4.12.1, the platform's
supported SQL Server, a Development HTTPS certificate, and the package-pinned
Playwright Chromium browser. It requires no Azure, Graph, Box, DVLA/DVSA, EVA,
Infisical, cloud login, or vendor authentication. Package and browser
restoration may use package feeds; an initialized run's Start and Smoke paths do
not.

**Platform delta.** *Windows:* the database is SQL Server Express LocalDB, and
the profile needs no container runtime. *Linux:* the database is a per-run SQL
Server container, so a reachable Docker daemon and the pinned image are
prerequisites; `Invoke-Doctor.ps1` checks both and never pulls. See
[local database](#local-database).

Pegasus has one supported database-provider contract: SQL Server. The local
development and integration-acceptance provider for persistence, migrations,
concurrency, and recovery evidence is SQL Server Express LocalDB on Windows and
a SQL Server container on Linux; Azure SQL is the deployed provider. All of them
use the committed SQL Server migration stream, and supported configuration
exposes no provider choice on either platform.

### Local database

The lifecycle owns one database instance per run and creates, starts, stops and
removes it for you. `Reset` discards the databases by removing the instance, so
neither platform needs a SQL client for that.

#### On Windows

The instance is a LocalDB instance named after the run. Nothing further is
required once LocalDB is installed.

#### On Linux

The instance is one container per run, published on loopback only, created from
an image pinned by digest. The credential is generated per run, written to
`<run-root>/state/mssql.env` readable only by its owner, and reaches the
application through the started process environment. It is never written to the
run manifest and never appears on a command line.

`Invoke-Doctor.ps1` requires the pinned image to be present locally and never
pulls it; `Initialize-LocalDevelopment.ps1` acquires it once. Each running
instance costs roughly 2 GiB of memory and 10 to 25 seconds of first start, so
expect to keep at most two runs started at once on a typical workstation.

The credential is visible to anyone who can query the container runtime, and
membership of the `docker` group is equivalent to root on the workstation. Both
are acceptable for a disposable development database and are stated here so the
exposure is not a surprise.

Use the owned commands rather than manually composing service terminals:

```powershell
pwsh ./scripts/Invoke-Doctor.ps1 -Profile Offline
pwsh ./scripts/Initialize-LocalDevelopment.ps1
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Start
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Status
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Smoke
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Stop
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Reset
```

Doctor checks only its selected profile. It never installs software, trusts a
certificate, signs in, calls a cloud/vendor endpoint, or creates resources; a
failed check prints its exact repair command. Initialization restores the
committed tool/package locks, installs the Playwright Chromium binary selected
by the pinned package, checks the Offline profile, starts LocalDB, and creates
only ignored local state.

`Cloud` is a separate static prerequisite profile for an already-approved live
operation. `pwsh ./scripts/Invoke-Doctor.ps1 -Profile Cloud` checks the pinned
CLI/module versions only; passing it neither signs in nor authorizes a read,
write, deployment, or SQL bootstrap.

Python creates no virtual environment and installs no package. Playwright
binaries are an Offline browser-acceptance prerequisite, not an application
runtime.

Run the deterministic browser dependency and accessibility gate after
initialization:

```powershell
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --no-restore --filter 'Category=Browser'
```

This lane launches the package-pinned headless Chromium with a fixed viewport,
light colour scheme, and reduced motion. It drives the running local Web host
through the DevelopmentOffline authenticated staff profile and the rendered
route responses; it does not treat copied markup or a synthetic browser document
as route evidence. It runs axe against the returned pages and fails on a missing
browser, host or route failure, or reported automated axe violation; only axe
rule identifiers enter assertion output.

The local profile exercises no external adapter, credential, approval, or
evidence gate. Browser coverage of authenticated and denied states is reproducible
local caller evidence only; it cannot grant an external approval or activate a
provider, custody, address, EVA, deployment, or operator-acceptance claim.
Microsoft Edge Stable, Windows Narrator, manual keyboard/focus/200% zoom review,
production identity/session behavior, external services, deployment, and
operator acceptance remain separately required fail-closed evidence gates. Until
those gates have their exact approval and evidence, their release claims remain
unavailable.

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

<a id="approved-box-integration-test-target"></a>

### Approved Box custody root

The Box production custody boundary is decided (2026-08-02): Box folder
`405543781910` ("pegasus") is the production custody root, and all case folders
are created only under this root. The source guard, Bicep app setting, and
tests carry that root; the currently deployed production configuration still
points at the former folder `392761581105` until the next approved deployment
applies the decided root. Folder `392761581105` remains the only eligible
controlled integration-test boundary — testing remains confined to an approved
disposable test subtree — and neither folder grants standing write authority.
Before any non-production invocation, obtain explicit approval naming the
exact target folder/object and create or controlled-update operation. The
activated production caller remains confined to case-scoped objects under the
configured root. No caller may delete, move, copy, or share Box content,
operate outside that folder, or expose credentials in source, configuration,
command lines, prompts, output, telemetry, or business history. Every invocation
must verify ancestry and the target/action allowlist and retain stable source and
target identities plus outcome. A failed attempt remains visible for authorised
staff retry; there is no automatic business retry. Box CLI authentication and
root membership do not expand approval.

Production server authentication uses the retained `box-config-json` JWT
configuration and `box-client-secret` Key Vault secrets. The Box SDK obtains and
refreshes short-lived authorization headers at runtime; a static access token is
not an accepted setting or deployment input. Secret values remain resolved only
inside the Worker through Key Vault references.

The intended application staff accounts are Pegasus Identity accounts. The DevelopmentOffline profile authenticates its deterministic local Administrator fixture and enforces its Administrator role. Application staff identity initialization remains a separately controlled application operation; Entra users must not be assumed. Third-party credentials must never enter tracked settings, command-line arguments, prompts that may be retained, terminal output, telemetry, or business history.

### Azure SQL runtime-role bootstrap

`scripts/Invoke-AzureDatabaseBootstrap.ps1` implements the explicit
post-provision, post-migration user/role operation. It creates only the fixed
external-user aliases from the Web/Worker managed-identity client-ID SIDs,
rejects broad roles or direct DDL, and compares the live object permission set
with the exhaustive migration-defined grant and `DELETE`-denial matrix. It is
not an automatic `azure.yaml` hook. It ran against production on 2026-08-02 as
part of the executed release and verified the exhaustive matrix; any further
execution is a separately approved exact-target cloud write.

Migration `20260729176000_AzureSqlRuntimeLeastPrivilege` creates and owns the
fixed custom roles `pegasus_web_runtime_role` and
`pegasus_worker_runtime_role`. Role-reconciliation migration
`20260729199000_RuntimeRoleReconciliation` first removes every direct
object-level DML permission for those roles across the complete application
table census, then grants the exhaustive caller-derived matrix. It explicitly
denies `DELETE` on every table except the four Web workflows that require it
(`AspNetUserRoles`, `CaseDataFields`, `OrganizationRoles`, and
`TriageResponseEvidenceLinks`); Worker has no `DELETE` grant. Neither role
receives DDL, schema-wide access, `db_datareader`, `db_datawriter`, or
`db_owner`. Web owns staff identity and administration, case editing,
document-custody, request-upload, and operator intake persistence. Worker owns
mailbox polling, queued intake, due-work and sent-evidence processing, and
vehicle-observation persistence. Runtime migration tests compare the complete
schema census, grants, and delete denials rather than sampling named tables.
The bootstrap owns only the fixed external-user aliases
`pegasus_web_runtime` and `pegasus_worker_runtime`, created from the
corresponding managed-identity client-ID SID.

Before execution, the production runbook must identify the exact server,
database, principal, approval evidence, least-privilege matrix, rollback, and
caller-backed verification. Migration tests and the script implementation are
local evidence only; they neither create an Azure principal nor authorise a
cloud write.

## Locked restore, build, and test

Run focused owning projects while iterating. Before delivery, run the canonical solution commands exactly (`--locked-mode` enforces the committed package locks, matching CI):

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"
```

These commands are identical on both platforms; `pwsh` runs them either way.

**Platform delta.** The `SqlServer` test lane needs a reachable SQL Server. On
Windows that is LocalDB and needs no configuration. On Linux, point the tests at
a SQL Server container before running them:

```powershell
$env:PEGASUS_TEST_SQL_DATASOURCE = '127.0.0.1,<port>'
$env:PEGASUS_TEST_SQL_USER = 'sa'
$env:PEGASUS_TEST_SQL_PASSWORD = '<password>'
```

Leaving `PEGASUS_TEST_SQL_DATASOURCE` unset keeps the LocalDB default, so the
Windows command is unchanged. Without it on Linux, exclude the lane with
`--filter "Category!=Corpus&Category!=SqlServer"` and record that the lane did
not run.

These commands prove repository compilation and the selected non-corpus tests only. Genuine corpus, browser, LocalDB/Azurite/Functions, cloud, recovery, and operator evidence are separate caller-specific gates.

### Imported source workspaces

Source workspaces validate independently and are not part of the application solution:

```powershell
Push-Location ./workspaces/document-extraction; dotnet test --solution ./CollisionDocNet.slnx --configuration Release; Pop-Location
Push-Location ./workspaces/report-renderer; dotnet run --project ./src/CollisionRenderer.Cli -- install-browser; dotnet test ./CollisionRenderer.sln --configuration Release; Pop-Location
Push-Location ./workspaces/ai-centre/services/collision-brain; dotnet restore ./CollisionBrain.slnx --locked-mode; dotnet build ./CollisionBrain.slnx --configuration Release --no-restore; dotnet test ./CollisionBrain.slnx --configuration Release --no-build; Pop-Location
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

Run these commands from PowerShell 7 at the repository root:

```powershell
pwsh ./scripts/Invoke-Doctor.ps1 -Profile Offline
pwsh ./scripts/Initialize-LocalDevelopment.ps1
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Start
```

Initialization resolves the exact checkout `HEAD` and requires the tracked and
untracked working tree to remain clean before restore, immediately before and
after the Debug build, and before publishing its marker. The build disables
incremental compilation so the dependency graph is rebuilt from those clean
inputs. The marker records the relative paths, byte lengths, and SHA-256 hashes
of the Web and Worker runtime assemblies. `Start` refuses a changed revision,
package lock, missing artifact, or runtime-byte mismatch before it creates or
restarts a run.


`Start` prints a generated 32-character run ID. It creates
`artifacts/local-development/<run-id>/` with its ownership manifest, logs,
Azurite store, intake/mailbox/case-file roots, dynamic loopback ports, and a
`PegasusDevelopment_<run-id>` LocalDB instance. It starts Azurite first, runs
the explicit Development migration path, waits for Web readiness, and then
starts and checks the actual Functions host. Normal Web and Worker startup
never applies migrations.

The one-shot `--initialize-development` command is invoked before the Web
process starts. It is gated to Development plus `DevelopmentOffline`, applies
the migration stream, and idempotently creates the fixed passwordless local
Administrator and roles. It neither creates a production bootstrap principal
nor configures an OAuth or MCP client.

The run-specific Web readiness URL and Functions status URL are printed by
`Start`. All development settings are process-scoped; no tracked configuration
file, `corpus/`, Azure resource, or another run is changed.

### Status and smoke

```powershell
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Status
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Smoke -RunId <run-id>
```

When exactly one owned run exists, `Smoke`, `Stop`, and `Reset` can omit
`-RunId`; with zero or multiple runs they refuse ambiguity. Status enumerates
all owned manifests and probes a running run's owned process start times, Web
readiness, and Functions-host `Running` state rather than treating a PID as
readiness. Smoke additionally checks the non-sensitive version/source-SHA
diagnostic.
Smoke also proves that the manifest HTTPS origin is listening and that the
version diagnostic matches the manifest source SHA. It does not prove an OAuth,
MCP, deployment, or external-system caller.
A successful `Start` persists current-attempt readiness evidence only after
Azurite, Web health, and the Functions host have all passed. `Smoke` takes the
lifecycle mutex, invalidates any earlier smoke result before probing, and then
atomically persists either `Passed` evidence or a failed result for that same
start attempt. The passed record binds the version diagnostic source SHA,
initialized identity, HTTPS origin, Administrator route, and service
readiness to the run manifest.


These checks prove the local process graph and the exercised health/diagnostic
paths only. They do not prove a business caller, durable cloud behavior,
managed identity, RBAC, external delivery, deployment, or acceptance.

### Isolated runs and failure controls

Parallel starts use distinct generated run IDs, ports, LocalDB databases,
Azurite accounts/stores, and artifact roots. To exercise orchestration failure
recovery without touching another run, use one run-scoped control:

```powershell
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Start -FailureMode AfterWeb
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Start -FailureMode StoragePressure -StoragePressureMegabytes 32
```

The first control fails after the named owned dependency has reached readiness.
The second allocates only the named bounded file beneath that failed run before
failing; it is safe cleanup/recovery evidence, not a claim to model an
application volume quota. Failed-run manifests and logs remain for diagnosis,
and their child processes are stopped.

### Stop and reset

```powershell
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Stop -RunId <run-id>
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Reset -RunId <run-id>
```

Stop retains the manifest and diagnostics. Reset first verifies that the
manifest run ID, directory, database name, and every owned path agree; it then
stops only matching child processes, drops only that LocalDB database, and
removes only that run directory. A malformed or ambiguous manifest refuses
action. Never manually repurpose these commands to remove another run,
`corpus/`, tracked reference files, or an Azure resource.
## Configuration and secrets

Configuration ownership is:

| Boundary | Owner |
| --- | --- |
| Web composition and named SQL Server connection | `src/Pegasus.Web/Program.cs` and environment configuration |
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

The current operational baseline is the offline profile above. The following
caller-scoped profile model distinguishes implemented local gates from planned
activation; installing a tool never establishes a caller.

| Profile | Current gate and planned boundary |
| --- | --- |
| `Baseline` | Windows or Linux, PowerShell 7, Git/GitHub CLI, pinned .NET 10 SDK, Azure CLI with Bicep, Azure Developer CLI, Node/npm, Python, Infisical CLI, and Box CLI for build, test, Bicep validation, and approved administration. Cloud/vendor tools remain optional in the current offline baseline. |
| `SqlServer` | The platform's supported SQL Server (LocalDB on Windows, a container on Linux) and `sqlcmd` for migrations, constraints, transactions, allocation concurrency, outbox atomicity, and local backup/restore. |
| `StorageWorker` | Repository-pinned npm Azurite and Functions Core Tools v4 for real Blob/Queue SDK, trigger, retry, poison, and restart paths. Activate only with the first real storage adapter and Worker trigger. |
| `Browser` | The `Browser` trait pins Microsoft Playwright for .NET, Chromium, and Deque axe-core. It drives the rendered DevelopmentOffline Operations, intake, Triage, administration, password-change, and case-document routes through a loopback Kestrel host, including semantic, responsive, forced-colour, and reduced-motion checks. It remains local caller evidence: Edge Stable, Narrator, manual accessibility review, external approvals, deployment, and operator/management acceptance remain separate fail-closed gates. |
| `Graph` | Microsoft Dev Proxy and mocked Kiota request adapters for paging, throttling, 401/403, 429, 5xx, timeout, authentication, and retry. |
| `Observability` | OpenTelemetry in-memory exporter and an optional native Collector for correlation, attributes, health signals, OTLP, and redaction. |
| `Performance` | `Invoke-QdosAlphaAcceptance.ps1 -Profile CiPressure` compiles the two bounded pressure sources through the existing integration-test host, exercises eight concurrent DevelopmentOffline Web callers, and retains content-safe TRX and hashed run evidence. It installs no load-test framework and makes no alpha-capacity claim. |
| `Security` | .NET dependency vulnerability checks and OWASP ZAP; ZAP uses the conditional container profile. |
| `Containers` | A container runtime (Docker Desktop in Linux-container mode on Windows, the native engine on Linux), conditionally for ZAP, optional telemetry, optional SQL compatibility, or a specifically approved licensed Document Intelligence container. Docker is never required merely for Azurite. On Linux the local database is a container, so a container runtime is a base prerequisite there rather than a conditional one. |
| `LiveIntegration` | The existing approved developer identity/secret tooling and exact SDK/CLI owned by the feature. Never part of the default local check. |

Storage Explorer, SSMS, and Postman are optional conveniences.

Do not add Service Bus, Event Hubs, Cosmos DB, Redis, PostgreSQL, Azure Files, ADLS, local SMTP infrastructure, Testcontainers, or related emulators without a later accepted architectural need.

`scripts/Invoke-QdosAlphaAcceptance.ps1` is the current narrow Checkpoint 12 pressure orchestrator. `CiPressure` temporarily stages `tests/Pegasus.PerformanceTests/CapacitySoakTests.cs` and `FailureInjectionTests.cs` into the existing `Pegasus.IntegrationTests` compilation, runs only `Category=QdosPressure`, removes that owned staging directory unconditionally, and writes content-safe evidence beneath `artifacts/qdos-alpha-acceptance/<run-id>/`. Supply the exact 40-character checked-out source revision:

```powershell
./scripts/Invoke-QdosAlphaAcceptance.ps1 `
  -Profile CiPressure `
  -SourceRevision $env:GITHUB_SHA
```

The runner requires Git metadata and a clean working tree, resolves the supplied revision to the exact checked-out `HEAD`, and rejects a mismatch before creating the run evidence directory or compiling tests. `OfflineCandidate` also requires the caller manifest and any inherited `PEGASUS_QDOS_ACCEPTANCE_SOURCE_REVISION` value to identify that exact revision; its Web-host gate compares the environment revision with the source SHA exposed by the compiled `/diagnostics/version` endpoint.

This lane proves bounded in-process Web-caller concurrency, latency, antiforgery denial, cancellation recovery, and idempotent replay against controlled fixtures. It does **not** prove the approved 30-minute workload, 2,000-case/source distribution, Worker/Azurite queue recovery, LocalDB restore, full case/EVA/report journeys, deployment, or acceptance.

`-Profile OfflineCandidate` is deliberately fail closed. It requires the operator-approved immutable 2,000-case dataset and hash, the complete QDOS-owned caller-evidence manifest, and the exact run-owned `artifacts/local-development/<run-id>/run-manifest.json`. That local manifest must identify the same clean source revision and acceptance run ID, remain `Running`, record completed fixed local identity initialization, and contain `Passed` readiness and smoke observations from the current start attempt in timestamp order. The runner also re-hashes the exact Web and Worker runtime paths recorded at initialization, so missing or altered local binaries fail before any acceptance tests execute. The script never promotes this offline evidence to deployed, live-verified, release-accepted, QDOS operator-accepted, or Collision Engineers management-accepted evidence.

Traits currently in use are `SqlServer`, `Browser`, `Corpus`, `QdosPressure`,
and `QdosAlphaAcceptance`. Additional stable planned traits (unused until their
lanes exist) are `Unit`, `Integration`, `Storage`, `FunctionsHost`,
`Performance`, `Security`, `Recovery`, and `LiveIntegration`.

A required but skipped selected trait fails. Optional inactive profiles do not block baseline work.

## Required evidence tiers

For each delivered capability, identify the authoritative rule, Core policy owner, real production entry point, persisted result, adapter or side effect, operator-visible result, and applicable tier.

1. **Static/build/architecture** — compile the four approved projects, enforce dependency direction and one policy owner, compile Bicep, inspect dependencies, and prevent tracked corpus or secret material. This proves consistency only.
2. **Core/domain** — positive, contradictory, ambiguous, and failure cases for intake, references, matching, lifecycle, roles, completeness, and case invariants.
3. **Parser/adapter contracts** — EML/PDF/DOCX and later approved DOC/MSG handling; corruption, encryption, expansion/resource limits, cancellation, path/integrity safety, stable contract codes, and deterministic external failures.
4. **LocalDB persistence** — fresh and incompatible schemas, committed SQL Server migrations, rollback, state/action-history/outbox atomicity, reference allocation, constraints, pagination, leases, stale versions, concurrency, and backup/restore.
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
| ASP.NET Core / Container Apps | Kestrel, `WebApplicationFactory`, Playwright, local HTTPS, local OCI-layout inspection | Linux/AMD64 Container Apps Consumption runtime, digest-pinned ACR pull, scale-to-zero cold/warm behavior, probes, revision restart, managed identity |
| SQL Server / Azure SQL | Disposable LocalDB for migrations, locking, allocation, rollback, backup, and restore | Entra identity, Azure SQL configuration/throttling, point-in-time restore, 15-minute RPO and four-hour RTO |
| Blob / Queue / Functions | Azurite and actual Functions host for staging, identifiers, duplicate/poison/restart behavior | Storage RBAC, managed identity, durability, Flex scale/concurrency, platform diagnostics |
| Key Vault / identity | Mock the owned port; developer credentials only for approved development resources | Deployed managed identity, least-privilege RBAC, firewall behavior |
| Application Insights / Log Analytics | In-memory OpenTelemetry and optional local Collector | Ingestion, sampling, KQL, retention, alert rules, recipient delivery |
| Graph / Exchange | Kiota fake and Dev Proxy; allowlist rejects unknown mailbox/folder/action before client call | Approved mailbox allowlist, Exchange Application RBAC, immutable IDs, delta behavior, exact Sent-item existence |
| Box | Fake SDK/HTTP contract for folder/file commands, custody, versions, idempotency, and failures; the approved Box integration-test target may also create/update controlled non-corpus artifacts for local or explicitly approved non-production deployment evidence | Real custody, permissions, versions, recovery, production target, and caller evidence |
| Document Intelligence | Candidate-routing and response-contract tests with controlled non-corpus fixtures | OCR accuracy, confidence, API drift, cost, throttling, identity; licensed disconnected containers are not the default emulator |
| DVLA/DVSA | Deterministic contracts, invalid identifiers, retries, unavailable-service outcomes | Entitlement, identity, real response behavior |
| EVA | Exact local JSON/image-bundle contract and reconciliation metadata | Operator drag/drop acceptance and any later authorised API sandbox |
| Provider API / Automation MCP | Not implemented: no endpoint, client, credential, or caller | Settled actor/client/authentication contract, real caller evidence, and separately approved activation |
| Direct authorised-terminal deployment | Bicep compile/lint and local configuration checks | Approved preflight, package/migration identity, deployment, health smoke, rollback |
| Backup/recovery | LocalDB backup/restore into a new disposable database | Azure SQL PITR and the one-time alpha RPO/RTO exercise |

Managed identity itself is unavailable locally. LocalDB does not prove Azure SQL Entra, throttling, backup, restore, RPO, or RTO. Azurite does not prove Azure Files, ADLS, Entra/RBAC, managed identity, durability, replication, quotas, networking, scale, or production timing.

Graph Sent-item evidence does not prove recipient delivery or automatic case matching.

### Automation MCP remains a deferred ingress

No Automation MCP endpoint, OAuth client, metadata route, staff impersonation
path, credential, or application caller is implemented. Migration
`20260729150000_DocumentCustodyAndRequests` created the dormant OpenIddict
tables that a later client contract would use; schema presence is not an
implemented ingress. ADR-0013 leaves the Automation Actor identity and
authentication/client contract open. The current application therefore fails
closed by exposing no such ingress.

Activation requires an accepted contract naming the durable actor identity,
authentication and client custody, approved tools and scopes, action-history
attribution, revocation behaviour, actual HTTP caller evidence, and the exact
deployment/security approval. A staff browser identity is not a substitute for
that actor.

## Live-operation approval matrix

| Action | Exact scope required | Required approval and evidence |
| --- | --- | --- |
| Use an Azure service | Subscription, resource group, resource, operation | Explicit mutation/cost approval, fresh inventory, least-privilege identity |
| Read or change an Outlook mailbox | Tenant, application, mailbox, folder, action | Exchange Application RBAC approval and negative scope test before the Graph call |
| Use Box or another vendor sandbox | Enterprise/account, folder/project, operation | Credential/data approval and controlled non-corpus input |
| Use the approved Box integration-test target | Folder `392761581105`; local or explicitly approved non-production deployment; create and update controlled non-corpus artifacts only | Approved disposable test subtree; no delete, move, copy, share, broader folder access, or credential exposure; production case custody belongs only to the activated production caller under the decided root `405543781910` |
| Send a document to OCR, vision, AI, or another processor | Service, region, model, input class | Data, licence, cost, and security approval; corpus remains prohibited unless separately authorised |
| Deploy, restore, fail over, or retire | Exact environment (isolated local development or production only, per ADR-0014) and recoverable target | Explicit operation approval for the exact target, fresh inventory, rollback path, retained source data |

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
- A 2026-07-23 multi-format evaluation used controlled protocol fixtures and pinned genuine samples through the historical Development-only `POST /Intake/Qdos`. The current route is the authenticated `/Intake` `ReceiveIntake` POST handler through `ProcessIntake`. The historical result records sampled QDOS-policy behavior and failure boundaries, not current-caller execution, complete workflow, field-level accuracy, Worker/Graph/Box/Azure behavior, or production acceptance.
- A 2026-07-23 embedded-PDF benchmark used 74 unique PDFs and 567 reported pages from an immutable local QDOS cohort through a disposable benchmark harness. It records comparative embedded-text decoding and marker coverage only; it does not prove literal field accuracy, OCR, future layouts, production runtime behavior, or operator acceptance.

### Planned EML evaluator

Local working-copy EML evaluation belongs to the separately owned desktop evaluator ([ADR-0016](adr/0016-standalone-desktop-email-evaluator.md)); its allocation is owned by the [capability inventory](capabilities.md) evaluator boundary. This remains an evaluator boundary, not proof that the current real caller was exercised.

EML contract evidence must cover parsing, provenance, corruption, nesting, cancellation, resource limits, deterministic failures, and content safety. Product-behavior claims require the current Web or later Worker caller; a standalone evaluator or historical endpoint is insufficient.

DOC and MSG automatic extraction remain deferred until safe local parsing fixtures and a human-reviewed genuine cohort and untouched holdout exist. An external processor requires separate selection and data-transfer approval.

## Release dependency order

Release allocation does not waive technical prerequisites. [Delivery dependencies](requirements.md#delivery-dependencies) owns current precedence. The predecessor delivery roadmap (git history) preserved the prerequisite, parallel-branch, and rejoin route; revalidate any of its claims against current canonical owners before use.

Operationally, do not run later caller or release gates before the revalidated spine has supplied relational intake state, trusted staff identity/action history, principal/configuration data, durable custody and the allocator, definitive acceptance, then case files/editing/lifecycle/UI, the real Worker and Triage, vehicle/EVA, and finally Azure migration/recovery and operator acceptance. An Automation MCP caller remains a separately deferred ingress. A local check, generated package, Bicep file, or deployment cannot advance a missing predecessor gate.

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

Core contains local `ActivitySource` instrumentation. The deployed Worker registers and exports Application Insights telemetry (its live executions are observable in the production Application Insights resource), and the production budget/alert wiring is recorded under [production environment](#production-environment). The current Web host registers no in-process telemetry exporter, so correlated Web/Worker telemetry (OPS-07) remains open work; there is no live incident record or current recovery/deletion incident evidence, and historical predecessor incidents do not establish current Pegasus behavior.

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

Refresh the live Azure inventory under separate authorization immediately before any cloud decision; the [production environment](#production-environment) section records the deployed end state, and dated names are not current identity proof.

## Deployment and release

The accepted direct-terminal Azure design is indexed by [architecture](architecture.md) and the [decision register](adr/README.md). The target files are `infra/`, `azure.yaml`, and `.azure/deployment-plan.md`.

`azd up` is not the release procedure. GitHub Actions/OIDC deployment is `Not planned`.

### Production environment

Executed 2026-08-02 (full runbook and evidence hashes: git history,
`azure-production-replacement-plan.md`):

- **Environments:** isolated local development and production only; no Azure
  dev/test/integration/staging resources (ADR-0014).
- **Production target:** subscription `e6076573-23a5-46a8-acef-7e22d264e5db`,
  tenant `858cf5b3-aa0a-47a6-9b40-4851fd0afa94`, resource group
  `rg-pegasus-prod`, region `uksouth`.
- **Compute/data:** Linux/AMD64 Razor Pages Web on Container Apps Consumption
  (single revision, 0.5 vCPU / 1 GiB, min 0 max 1 replica — cold start
  accepted), FC1 .NET 10 isolated Worker, Basic ACR, S0 Azure SQL, two Standard
  LRS storage accounts, distinct Web/Worker managed identities, a Pegasus Key
  Vault, Log Analytics, and Application Insights.
- **Deployed evidence (2026-08-02):** Web source revision `94997dd0…` on an
  immutable image digest; health endpoints returned 200 after predecessor
  retirement; Graph Inbox/Sent processing live-verified through the production
  Worker (83 successful executions, zero exceptions in the final readback).
- **Integrations:** Graph via the Worker managed identity scoped by Exchange
  Application RBAC to `instructions@collisionengineers.co.uk`; Box production
  custody deployed with root folder `392761581105` — the decided root is
  `405543781910` ("pegasus") and applies at the next approved deployment;
  official DVLA VES v1.2 and DVSA MOT History v1; EVA remains the accepted
  manual JSON/image handoff.
- **Secrets:** the adopted predecessor vaults `cespkboxkvv76a47` and
  `cespkenrichkvgi62sd` remain (intentionally retained inside
  `rg-collisionspike-dev`); secret-level access only for the identities and
  exact secrets that call them. The three obsolete vaults are soft-deleted with
  platform purge scheduled 2026-08-09.
- **Predecessor retirement:** executed through the exact verified manifest;
  eight resource batches completed, 30 delete-classified role assignments
  removed, 7 retained; the archive manifest hash is recorded in the runbook
  (git history).
- **Monitoring/cost:** 31-day retention, adaptive sampling, 0.1 GB/day
  Application Insights cap, £75 monthly budget notifying
  `digital@collisionengineers.co.uk` at actual 50/80/100% and forecast 100%.
  Alerts never stop resources.
- **Recovery gate:** the first release launched under an explicit exception —
  no second production release until the isolated RPO/RTO recovery exercise in
  [production recovery](#production-recovery) passes.

### Release artifacts and bootstrap

The release scripts are `Build-ReleaseArtifacts.ps1` (immutable packages from
a clean tree at an exact HEAD), `Test-AzureDeploymentPlan.ps1` (local, artifact,
pre-upload, and pre-migration validation), `Invoke-AzureDatabaseBootstrap.ps1`
and `Invoke-ProductionAdministratorBootstrap.ps1` (manifest-SHA-gated), and
`Invoke-ProductionSmoke.ps1` (health and exact version/SHA assertions). The
executed 2026-08-02 sequence and its evidence gates are recorded in the retired
runbook (git history, `azure-production-replacement-plan.md`). The one-off
predecessor archive/retirement scripts completed their purpose in that run and
are also recoverable from git history.

### Azure activation remains fail-closed

`infra/main.bicep` remains fail-closed unless the exact
`deploymentMode=approved-live-deployment` value is supplied. The production-only
route replaced the former development/offline topology; Bicep compilation and
local plan validation do not authorize Azure.

The concrete activation gate is a separately recorded approval for the exact
subscription, resource group, principal, cost scope, data boundary, and
migration/deployment sequence, followed by a fresh authorised-terminal check of
availability, quota, pricing, role-assignment authority, target names, SQL Entra
administrator, and external credential readiness.

Apply migrations explicitly before application packages. Application startup must never silently migrate a non-Development database. Deployment does not itself prove live behavior or acceptance.

## Recovery

Current source provides no application backup/restore executable or
receipt/artifact deletion route, and no Pegasus recovery, failover, RPO, or
RTO exercise has completed; the first production release launched under an
explicit exception, and a second release is blocked until the isolated
exercise below passes. The production Box custody adapter is deployed behind
the existing Core port and rooted at the approved custody root (see
[production environment](#production-environment)); it is not
recovery-tested or operator-accepted. Test cleanup and migration tests are
narrower evidence. The procedures below are release gates, not claims that
recovery or deletion is accepted.

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

The allocated [OPS-09](capabilities.md) capability and its [product-quality objectives](requirements.md#quality-capacity-security-and-evidence) are proved after `0.1.0-alpha.1` acceptance and gate any second production release (NOW.md path, decided 2026-08-02). The isolated exercise must prove:

- a 15-minute recovery point objective; and
- a four-hour restoration path.

Repeat the proof after material persistence or release changes where required. Recurring quarterly recovery is `Not planned`.

A recovery, restore, failover, or retirement exercise requires exact target approval, fresh inventory, a recoverable target, retained source data, and a rollback path.

Predecessor retirement executed on 2026-08-02 through the exact verified manifest. `rg-collisionspike-dev` intentionally remains only as the container for the two adopted Key Vaults; any further action on it requires separately approved exact targets.

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

Work tracking uses no GitHub issues, labels, milestones, or project boards.
[`NOW.md`](../NOW.md) is the only work tracker, the
[capability inventory](capabilities.md) is the roadmap, and
[open decisions](open-decisions.md) holds unresolved questions
(see the [repository instructions](../AGENTS.md)). Allocation, activation,
implementation, caller proof, deployment, live verification, and
operator/management acceptance remain distinct states.

## Maintenance

Reconcile this procedure whenever requirements, accepted decisions, production callers, external contracts, supported platforms, evidence boundaries, or deployment architecture change.

Add a tool, service, profile, or release gate only with its real caller or named release invariant. Remove replaced test infrastructure in the same change. Record dated command results and limitations in the owning change or task, not as an evergreen status ledger.
