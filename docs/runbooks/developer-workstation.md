# Windows developer workstation

CollisionSpike v2 is developed from PowerShell 7 on Windows. Run `pwsh ./scripts/Invoke-Doctor.ps1` for the current check.

Verified on 2026-07-23:

| Tool | Verified version/presence | Purpose |
|---|---|---|
| PowerShell | 7.6.3 | repository and cloud automation |
| Git | 2.53 | source control |
| .NET SDK | 10.0.302 plus 10.0.204 | application build/test |
| Azure CLI | 2.88 | read-only inventory and explicit cloud operations |
| Bicep CLI | 0.45.15 | infrastructure compile |
| Azure Developer CLI | 1.28.0 | approved direct-terminal Bicep/package deployment after ADR-0009 gaps are implemented |
| Azure Functions Core Tools | 4.12.1 | local isolated Worker host |
| GitHub CLI | 2.88 | repository/CI operations |
| Node/npm | 24.14 / 11.9 | pinned Azure MCP launcher and tooling |
| Python | 3.14.3 | skill and evaluation utilities |
| Infisical CLI | 0.43.104 | local/runtime secret workflow |
| Box CLI | 4.9.2 | explicitly approved Box diagnostics/operations |
| SqlServer PowerShell module | 22.4.5.1 | Entra-token SQL post-provision grant |

Azure Developer CLI is installed under `%LOCALAPPDATA%\Programs\Azure Dev CLI` and on the user PATH. An already-running shell may need to restart before `azd` resolves by name.

## Install or repair commands

```powershell
winget install --id Microsoft.Azd --exact --accept-package-agreements --accept-source-agreements
Install-Module SqlServer -Scope CurrentUser -Force -AllowClobber -Repository PSGallery
```

## Authentication boundaries

- `az login` authenticates Azure CLI and managed-identity-aware local development through the signed-in operator.
- `azd auth login` is separate and should be run only when provisioning/deployment work is approved.
- GitHub Actions/OIDC deployment is `Never`. An authorised terminal identity is
  required only for explicitly approved Azure work; do not create long-lived
  Azure client secrets as a substitute.
- Application users are not Entra users by assumption. Their usernames/passwords are owned by ASP.NET Core Identity in the application database.
- Infisical or Azure Key Vault holds third-party credentials. Never place values in local settings examples, azd parameters, committed appsettings, or agent prompts.

Repository-local Azure and Microsoft Learn MCP declarations were removed before
Azure Workflow onboarding. Current Microsoft/Azure facts use the active workflow
tools when available; tool availability never authorizes a cloud read or write.
