# Machine prerequisites for the hosted test instance

The runbook's [Offline development profile](../../../../docs/runbook.md#offline-development-profile)
owns the pinned versions; `scripts/Invoke-Doctor.ps1 -Profile Offline` checks
them and prints the exact repair command for each gap. This page is the order
to do them in on a fresh WSL distribution, and why each is there.

| Need | Why | Check |
| --- | --- | --- |
| WSL 2 with at least 8 GB memory | SQL Server's container takes 2 GB; Web, the Functions host, Azurite, the in-process ONNX plate reader and Node take the rest | `.wslconfig` on the Windows host: `[wsl2]` `memory=8GB` or more, then `wsl --shutdown` |
| PowerShell 7.6.3 or later | every repository script | `pwsh --version` |
| .NET SDK 10.0.302 | the pinned build; Initialize refuses another | `dotnet --version` from the repository root (global.json pins it) |
| Node 24 and npm 11 | Azurite 3.36.0 comes from `npm ci`; the classifier agent is Node 22+ | `node --version`, `npm --version` |
| Azure Functions Core Tools 4.12.1 | the Worker runs under `func start` | `func --version` |
| Docker Engine, user in the `docker` group | the per-run SQL Server container; Initialize pulls the pinned image once | `docker info`; `id -nG` lists `docker` |
| Azure CLI, signed in as the operator | Key Vault secret reads and the production extract | `az account show` names subscription `e6076573-23a5-46a8-acef-7e22d264e5db` |
| SqlPackage | the production copy | `dotnet tool install --global microsoft.sqlpackage`; `sqlpackage /version` |
| Tailscale on the node that will own the name | `tailscale serve` in front of the run | `tailscale status` |
| Git with the operator's deploy key | the clone and later checkouts | `git ls-remote git@github.com:collisionengineers/pegasus.git HEAD` |

Notes:

- Trusting the development certificate is not needed: the browser sees the
  tailnet certificate, and `tailscale serve` is told the backend is
  `https+insecure`. `Initialize-LocalDevelopment.ps1` still creates the
  certificate so Kestrel can bind HTTPS.
- Docker group membership is root-equivalent on the machine, and the
  container's `sa` password is visible to anyone who can query the container
  runtime. The runbook states both; they are acceptable for a test database.
- The pinned SQL Server image is x64 only.
- Keep the clone outside any Windows-mounted path (`/mnt/c/...`): file
  watching and permissions (mode 600 on the settings file) need a Linux
  filesystem.
- The settings file and every run live under the ignored
  `artifacts/local-development/`; a checkout of a new revision leaves them in
  place.
