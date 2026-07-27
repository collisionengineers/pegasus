# Local development

Pegasus runs offline on Windows without Azure, Graph, Box, DVLA/DVSA,
EVA, Infisical, Docker, or a cloud login. Use the standard owning executables
directly; the repository does not wrap them in a workstation doctor or generic
repository-check script.

## Required local tools

| Tool | Supported version | Direct check |
| --- | --- | --- |
| PowerShell | 7.6.3 | `pwsh --version` |
| .NET SDK | 10.0.302 from `global.json` | `dotnet --version` |
| Node/npm | Node 24 / npm 11 | `node --version`; `npm --version` |
| Azurite | 3.36.0 from `package-lock.json` | `npx --no-install azurite --version` |
| Functions Core Tools | 4.12.1 | `func --version` |
| SQL Server Express LocalDB | installed LocalDB runtime | `sqllocaldb versions` |
| Development HTTPS | trusted .NET development certificate | `dotnet dev-certs https --check --trust` |

Python 3.11+ is required only when authoring provider-domain reference data; the
script uses the standard library and installs no package. Playwright browser
installation is required only for the browser acceptance lane. Cloud/vendor
tools belong to approved live work, not this baseline.

## Step 2 — author provider-domain reference data

The Step 2 command is an offline authoring operation over one immutable
cumulative source workbook. For `0.1.0-alpha.1` it reads only
`docs/reference/workproviders-and-repairers/initial.xlsx` and retains only the
provider code from column A and the final lowercase `@domain` suffix from each
semicolon-separated column-E observation. It ignores columns B-D and later
columns. It never edits the workbook or emits an email local part, full email
address, inspection location, default, Case ID, or opaque source value.

Close the selected workbook, then run from PowerShell 7 at the repository root:

```powershell
pwsh ./scripts/Build-ProviderReferenceData.ps1
pwsh ./scripts/Build-ProviderReferenceData.ps1 -Verify
```

Before discovering Python or reading source bytes, the wrapper rejects the
selected workbook's exact sibling Office lock marker and an exclusive-read
failure as `source-locked`. The helper requires Python 3.11+ and uses only
`zipfile` and `xml.etree.ElementTree`; there is no authoring virtual
environment, pip install, dependency lock, package cache, recursive workbook
discovery, network operation, or second manifest.

The `0.1.0-alpha.1` command stages beneath ignored
`artifacts/reference-data-staging/` and publishes this immutable package:

```text
src/Pegasus.Infrastructure/Persistence/ReferenceData/provider-domains.v1.json
```

Generation completes in staging before publication. An absent output is moved
atomically into place; an existing byte-identical output is a no-op; an
existing different output fails `immutable-output` and is not replaced.
`-Verify` requires the output and byte-compares a regenerated staged package
without mutating it.

Later growth uses a new immutable cumulative workbook, a new version and output,
and the previous validated package:

```powershell
pwsh ./scripts/Build-ProviderReferenceData.ps1 `
  -SourcePath ./docs/reference/workproviders-and-repairers/provider-domains-v2.xlsx `
  -Version provider-domains-v2 `
  -PackagePath ./src/Pegasus.Infrastructure/Persistence/ReferenceData/provider-domains.v2.json `
  -PreviousPackagePath ./src/Pegasus.Infrastructure/Persistence/ReferenceData/provider-domains.v1.json
```

Every previous provider/suffix pair must remain. A removal fails
`non-monotonic-source`; source, previous package, staging, and output paths must
be distinct; staging/output may not be under `docs/reference/`. Corrections or
removals require separately accepted authority and a new explicit contract;
published snapshots remain unchanged.

Command completion proves deterministic authoring bytes only. It does not
activate an email route, resolve a provider at intake, prove a migration or
caller, or establish release/alpha acceptance. Application runtime reads only
the explicit versioned SQL snapshot and never opens a workbook.


## First setup

From PowerShell 7 at the repository root:

```powershell
npm ci
dotnet restore ./Pegasus.slnx
dotnet dev-certs https --trust
dotnet dev-certs https --check --trust
sqllocaldb start MSSQLLocalDB
dotnet run --project ./src/Pegasus.Web --launch-profile https -- --migrate-development
```

Windows displays a current-user trust confirmation for the first command; the
operator must accept it. The second command is a check only and must return zero.

The migration command exits after applying the committed migration stream. A
normal Web or Worker start never applies migrations. The checked-in Development
configuration uses only:

- `Runtime:Profile=DevelopmentOffline`;
- `(localdb)\MSSQLLocalDB`, database `PegasusV2Development`;
- ignored files below `artifacts/local-development/default/`; and
- loopback HTTP/HTTPS endpoints.

`DevelopmentOffline` or `Features:LocalIntake=true` outside the Development
environment fails startup. Production configuration never resolves the local
filesystem adapter as a fallback.

## Start the local services

Use separate PowerShell terminals so each process has an obvious owner.

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

Verify `https://localhost:7139/health/live` and
`https://localhost:7139/health/ready`. Readiness is healthy only when the
configured database exists and every committed migration is applied. The Web
intake caller is `https://localhost:7139/Intake/Upload` until the authenticated
Operations shell replaces it.

At the current checkpoint the actual Functions host starts successfully but
reports that no job functions exist. That is host evidence only; queue/timer
caller evidence is not claimed until the Worker delivery slice adds and
exercises the triggers.

## Isolated or parallel runs

Use a unique database name, artifact path, ports, and Functions settings for
each run. For example, set these in every terminal before starting its process:

```powershell
$runId = [Guid]::NewGuid().ToString('N')
$env:ConnectionStrings__Pegasus = "Server=(localdb)\MSSQLLocalDB;Database=Pegasus_$runId;Integrated Security=True;Encrypt=False;MultipleActiveResultSets=True"
$env:Intake__LocalArtifactPath = "../../artifacts/local-development/$runId/intake"
```

Choose unused Azurite and Functions ports and point that run's Worker settings
to them. Never share a database, Azurite location, or custody root between
parallel runs.

## Stop and reset

Stop only the foreground processes started in the corresponding terminals with
Ctrl+C. Local state is ignored and disposable, but deletion is never inferred
from a process name. Before removing it, verify the exact database name begins
with `PegasusV2Development` or the run-specific `Pegasus_` prefix
and verify the exact artifact path is a descendant of
`artifacts/local-development/`. Do not remove another run, `corpus/`, tracked
reference files, or any Azure resource.

A clean run can always use a new GUID database and artifact directory. Keep
failed-run logs/state until the failure has been diagnosed.

## Evidence boundaries

- LocalDB proves SQL Server migrations, constraints, and transactions, not Azure
  SQL Entra, throttling, backup, restore, RPO, or RTO.
- Azurite and the Functions host prove local SDK/trigger composition only after
  a real queued identifier reaches the trigger; host startup alone is not that
  proof.
- Local mailbox, custody, vehicle replay, evaluator, OAuth/MCP, and telemetry
  evidence are owned by their feature slices. None proves live vendor scope.
- No local run may construct or call a live Graph, Box, DVLA/DVSA, EVA, Azure,
  or other vendor client.
