# Windows developer workstation

CollisionSpike v2 is developed from PowerShell 7 on Windows. Verify tools
directly with their standard version commands; the repository does not install
global tools or require a cloud login for offline development.

## Offline baseline

| Tool | Supported version/presence | Purpose |
| --- | --- | --- |
| PowerShell | 7.6.3 | development shell |
| Git | current supported client | source control and path ownership checks |
| .NET SDK | 10.0.302 from `global.json` | build, migration command, Web and tests |
| Python | 3.11+ | standard-library provider-domain package authoring only |
| Node/npm | Node 24 / npm 11 | restore pinned Azurite |
| Azurite | 3.36.0 from `package-lock.json` | local Blob/Queue/Table services |
| Azure Functions Core Tools | 4.12.1 | actual local isolated Worker host |
| SQL Server Express LocalDB | installed LocalDB runtime | full local relational database |
| Development HTTPS certificate | trusted .NET development certificate | Web and local OAuth/MCP |

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

Run `npm ci` and `dotnet restore ./CollisionSpike.slnx` once package feeds are
available. Normal local start and smoke must not need a cloud or vendor network.
See [local development](local-development.md) for process and state ownership.

Python is an authoring-only tool; the provider-domain command requires 3.11+
and standard-library modules only. It creates no virtual environment and
installs no package. Playwright browsers are used only by the browser acceptance
lane. Neither is an application runtime. See [local development](local-development.md)
for the immutable v1 command and cumulative future-version procedure.

## Optional approved live-work profile

These tools are not offline prerequisites. Check them only when the exact live
operation has already been approved:

| Tool/module | Supported version |
| --- | --- |
| Azure CLI | 2.88 |
| Azure Developer CLI | 1.28.0 |
| Bicep CLI | 0.45.15 |
| GitHub CLI | 2.88 |
| Infisical CLI | 0.43.104 |
| Box CLI | 4.9.2 |
| SqlServer PowerShell module | 22.4.5.1 |
| ExchangeOnlineManagement PowerShell module | 3.10.0 |

Install PowerShell modules only at CurrentUser scope and only for the selected
live work:

```powershell
Install-Module SqlServer -Scope CurrentUser -RequiredVersion 22.4.5.1 -Force -AllowClobber -Repository PSGallery
Install-Module ExchangeOnlineManagement -Scope CurrentUser -RequiredVersion 3.10.0 -Force -AllowClobber -Repository PSGallery
```

Tool installation and authentication do not authorize an external read or
write. `az login`, `azd auth login`, Exchange connection, Box login, credential
changes, deployment, and Azure operations retain separate exact-target
approval. Application staff accounts are CollisionSpike Identity accounts, not
assumed Entra users. Third-party credentials never enter tracked settings,
terminal output, prompts, telemetry, or business history.
