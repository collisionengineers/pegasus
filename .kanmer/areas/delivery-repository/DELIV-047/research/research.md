# Research — DELIV-047: Linux release workstation

## Question

Can Pegasus replace its Windows-only authorised release terminal with the provisioned Linux-native WSL environment without weakening exact-SHA, artifact, migration, approval, deployment, smoke, rollback or evidence boundaries?

## Findings

- `scripts/Build-ReleaseArtifacts.ps1` already publishes Web, Worker and OCI artifacts for `linux-x64`; only its migration bundle defaults to `win-x64`. It already emits the bundle name and runtime identifier in the manifest.
- `scripts/Test-AzureDeploymentPlan.ps1` already reads `migrationBundleName`, but retains an old `efbundle.exe` fallback and does not require a Linux runtime. That compatibility would allow a Windows manifest into the new route.
- `Invoke-AzureDatabaseBootstrap.ps1`, `Invoke-ProductionAdministratorBootstrap.ps1` and `Invoke-ProductionSmoke.ps1` use PowerShell 7, Azure CLI, azd and the SqlServer module without a Windows-only API. The migration execution instruction, not the bootstrap implementation, is Windows-specific.
- Microsoft documents `dotnet ef migrations bundle --self-contained --target-runtime linux-x64 --output artifacts/efbundle` as a supported Linux deployment artifact: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying
- Microsoft documents Azure CLI installation on Linux and WSL, and Azure Developer CLI installation on Linux: https://learn.microsoft.com/en-us/cli/azure/install-azure-cli and https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd
- The canonical release skill already creates an isolated exact-SHA worktree and uses platform-neutral PowerShell. Its database-migration reference still names `efbundle.exe`. The `.zcode` copy duplicates an older release procedure rather than forwarding to the canonical skill.
- Local tool census: .NET SDK 10.0.302, PowerShell 7.6.5, Azure CLI 2.88.0, azd 1.28.0 and dotnet-ef 10.0.10 are installed. ORAS is absent even though artifact identity and ACR upload use it. ORAS publishes an official Linux installation route: https://oras.land/docs/installation/
- `az account show` and `azd auth login --check-status` both report no active Azure authentication, and `azd env list --output json` is empty. Local artifact construction needs neither sign-in nor cloud writes; production preflight and release do.
- ADR-0007 chooses an authorised direct terminal but does not actually state Windows. Repository rules and runbook text infer Windows solely from the old bundle default. A thin new ADR can select Linux while leaving ADR-0007 and ADR-0014 intact.
- The group explicitly authorises no cloud write. Repository policy separately requires fresh `MERGE AUTH GRANTED` for `dev` to `main` and exact-target approval for every Azure/database write.

## Implications

Use the existing release route rather than create a Docker-based parallel deployer. Make Linux x64 an enforced build-host invariant, emit only `efbundle`, reject non-Linux manifests, install and doctor-check ORAS, update the canonical release instructions and their forwarding copy, and record the durable platform selection in ADR-0037. Prove artifact equivalence locally from an exact clean SHA. Do not promote or touch Azure until the operator supplies the two explicit authorities after reviewing the exact candidate and manifest.

## Open questions

None for local implementation. Production execution remains an approval boundary, not a design question.
