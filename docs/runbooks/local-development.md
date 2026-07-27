# Local development

CollisionSpike runs offline on Windows without Azure, Graph, Box, DVLA/DVSA,
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

Python and workbook-authoring dependencies are required only when rebuilding
provider reference data. Playwright browser installation is required only for
the browser acceptance lane. Cloud/vendor tools belong to approved live work,
not this baseline.

## Step 2 — author provider reference data

The Step 2 authoring command is an offline, one-time preparation step. It reads
the supplied workbooks under
`docs/reference/workproviders-and-repairers/` as immutable evidence; it never
edits, renames, deletes, or moves those workbooks or CSVs. Every generated
location candidate is emitted with `reviewState: "Unreviewed"` and therefore
cannot become a runtime selector until a later reviewed activation.

Before authoring, close every source workbook in every application. From
PowerShell 7 at the repository root, prove that no Office lock file remains:

```powershell
$locks = @(Get-ChildItem ./docs/reference/workproviders-and-repairers -Filter '~$*' -Force -File -Recurse)
if ($locks.Count -ne 0) {
  $locks | Select-Object -ExpandProperty FullName
  throw "Source workbook lock files remain; authoring is aborted before any source read or output write."
}
```

Only after that check is empty, invoke the owning command directly:

```powershell
pwsh ./scripts/Build-ProviderReferenceData.ps1
```

The command uses the hash-locked workbook dependency from the ignored
`artifacts/reference-data-tools/` cache and writes through the ignored atomic
staging directory `artifacts/reference-data-staging/`. A successful run
promotes the deterministic package and manifest to these committed
destinations:

- `src/CollisionSpike.Infrastructure/ReferenceData/provider-reference-data.v1.json`
- `src/CollisionSpike.Infrastructure/ReferenceData/provider-reference-data.v1.manifest.json`

The command fails nonzero before dependency installation, source reading, or
writing either committed destination if any `~$` workbook lock exists. It also
fails without promotion when a pinned dependency, source read/hash, deterministic
normalization, or output step fails; inspect the error and correct the
precondition before retrying. It makes no cloud or vendor calls.

The command's completion is authoring evidence only. It does not prove that
the package has been operator-reviewed, that candidates are selectable, that
database migration has run, that the observed baseline counts have been
accepted, or that tests, release, or alpha acceptance have passed. The current
working copy contains
`docs/reference/workproviders-and-repairers/~$providers-worked-on.xlsx`; until
that external workbook is closed and the lock disappears, authoring is
expected to fail and neither committed output is claimed as generated.


## First setup

From PowerShell 7 at the repository root:

```powershell
npm ci
dotnet restore ./CollisionSpike.slnx
dotnet dev-certs https --trust
dotnet dev-certs https --check --trust
sqllocaldb start MSSQLLocalDB
dotnet run --project ./src/CollisionSpike.Web --launch-profile https -- --migrate-development
```

Windows displays a current-user trust confirmation for the first command; the
operator must accept it. The second command is a check only and must return zero.

The migration command exits after applying the committed migration stream. A
normal Web or Worker start never applies migrations. The checked-in Development
configuration uses only:

- `Runtime:Profile=DevelopmentOffline`;
- `(localdb)\MSSQLLocalDB`, database `CollisionSpikeV2Development`;
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
Push-Location ./src/CollisionSpike.Worker
func start --port 7071 --no-build
Pop-Location
```

Terminal 3 — Web:

```powershell
dotnet run --project ./src/CollisionSpike.Web --launch-profile https --no-build
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
$env:ConnectionStrings__CollisionSpike = "Server=(localdb)\MSSQLLocalDB;Database=CollisionSpike_$runId;Integrated Security=True;Encrypt=False;MultipleActiveResultSets=True"
$env:Intake__LocalArtifactPath = "../../artifacts/local-development/$runId/intake"
```

Choose unused Azurite and Functions ports and point that run's Worker settings
to them. Never share a database, Azurite location, or custody root between
parallel runs.

## Stop and reset

Stop only the foreground processes started in the corresponding terminals with
Ctrl+C. Local state is ignored and disposable, but deletion is never inferred
from a process name. Before removing it, verify the exact database name begins
with `CollisionSpikeV2Development` or the run-specific `CollisionSpike_` prefix
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
