[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Local', 'Artifact', 'PreMigration')]
    [string] $Mode,

    [string] $ManifestPath,

    [string] $Environment
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$mainBicepPath = Join-Path $repositoryRoot 'infra/main.bicep'
$platformBicepPath = Join-Path $repositoryRoot 'infra/modules/platform.bicep'
$parametersPath = Join-Path $repositoryRoot 'infra/main.parameters.json'
$azureYamlPath = Join-Path $repositoryRoot 'azure.yaml'
$productionPlanPath = Join-Path $repositoryRoot 'azure-production-replacement-plan.md'

function Assert-Text {
    param(
        [Parameter(Mandatory)][string] $Text,
        [Parameter(Mandatory)][string] $Pattern,
        [Parameter(Mandatory)][string] $Failure
    )

    if ($Text -notmatch $Pattern) {
        throw $Failure
    }
}

function Assert-TextAbsent {
    param(
        [Parameter(Mandatory)][string] $Text,
        [Parameter(Mandatory)][string] $Pattern,
        [Parameter(Mandatory)][string] $Failure
    )

    if ($Text -match $Pattern) {
        throw $Failure
    }
}

function Test-ArtifactManifest {
    param([Parameter(Mandatory)][string] $Path)

    $resolvedManifest = Resolve-Path -LiteralPath $Path
    $manifest = Get-Content -LiteralPath $resolvedManifest -Raw | ConvertFrom-Json
    if ($manifest.schemaVersion -ne 1) {
        throw 'The release manifest schemaVersion must be 1.'
    }
    if ($manifest.sourceRevision -notmatch '^[0-9a-f]{40}$') {
        throw 'The release manifest sourceRevision must be an exact Git SHA.'
    }
    if ($manifest.sourceStatus -ne 'clean') {
        throw 'The release manifest must record a clean source status.'
    }
    if (-not $manifest.artifacts -or $manifest.artifacts.Count -ne 3) {
        throw 'The release manifest must contain exactly Web, Worker, and migration artifacts.'
    }

    $manifestDirectory = Split-Path -Parent $resolvedManifest
    $requiredNames = @('web.zip', 'worker.zip', 'efbundle.exe')
    foreach ($name in $requiredNames) {
        $entry = @($manifest.artifacts | Where-Object name -eq $name)
        if ($entry.Count -ne 1) {
            throw "The release manifest must contain exactly one $name entry."
        }
        $artifactPath = Join-Path $manifestDirectory $name
        if (-not (Test-Path -LiteralPath $artifactPath -PathType Leaf)) {
            throw "Release artifact is missing: $artifactPath"
        }
        $file = Get-Item -LiteralPath $artifactPath
        $hash = (Get-FileHash -LiteralPath $artifactPath -Algorithm SHA256).Hash
        if ($file.Length -ne $entry[0].sizeBytes -or $hash -ne $entry[0].sha256) {
            throw "Release artifact identity mismatch: $name"
        }
    }
}

$mainBicep = Get-Content -LiteralPath $mainBicepPath -Raw
$platformBicep = Get-Content -LiteralPath $platformBicepPath -Raw
$parameters = Get-Content -LiteralPath $parametersPath -Raw
$azureYaml = Get-Content -LiteralPath $azureYamlPath -Raw
$productionPlan = Get-Content -LiteralPath $productionPlanPath -Raw
$combined = "$mainBicep`n$platformBicep`n$parameters`n$azureYaml"

Assert-Text $mainBicep "@allowed\(\[\s*'prod'\s*\]\)" 'infra/main.bicep must accept production only.'
Assert-Text $mainBicep "deploymentMode\s*==\s*'approved-live-deployment'" 'Bicep must fail closed unless approved-live-deployment is supplied.'
Assert-TextAbsent $combined "(?im)^\s*SCM_DO_BUILD_DURING_DEPLOYMENT\s*[:=]" 'Remote build is prohibited.'
Assert-TextAbsent $combined "(?i)offline-replay|rg-pegasus-dev|pegasusdev" 'Azure deployment files must not contain a development/offline target.'
Assert-Text $platformBicep 'transportStorageName' 'The transport/deployment storage account is missing.'
Assert-Text $platformBicep 'custodyStorageName' 'The custody/protection storage account is missing.'
Assert-Text $platformBicep "name:\s*'ASPNETCORE_ENVIRONMENT'[\s\S]*?value:\s*'Production'" 'Web must use ASPNETCORE_ENVIRONMENT=Production.'
Assert-Text $platformBicep "name:\s*'Runtime__Profile'[\s\S]*?value:\s*'Production'" 'Worker must use Runtime__Profile=Production.'
Assert-Text $platformBicep "name:\s*'APPLICATIONINSIGHTS_AUTHENTICATION_STRING'" 'Application Insights local authentication must be disabled through managed-identity configuration.'
Assert-Text $platformBicep "AzureWebJobs\.[A-Za-z0-9]+\.Disabled'[\s\S]*?value:\s*'true'" 'Worker triggers must start disabled.'
Assert-Text $platformBicep 'retentionInDays:\s*31' 'Log Analytics retention must be exactly 31 days.'
Assert-Text $mainBicep 'LOG_ANALYTICS_WORKSPACE_NAME' 'The Log Analytics workspace name must be exported for exact post-provision configuration.'
Assert-Text $productionPlan 'az monitor log-analytics workspace update[\s\S]*?workspaceCapping\.dailyQuotaGb=0\.1' 'The runbook must set the Log Analytics cap to exactly 0.1 GB after provisioning.'
Assert-Text $productionPlan 'az monitor app-insights component billing update[\s\S]*?--cap 0\.1' 'The runbook must set the Application Insights cap to exactly 0.1 GB after provisioning.'
Assert-Text $productionPlan '\[decimal\]\$WorkspaceCap -ne 0\.1 -or \[decimal\]\$ApplicationInsightsCap -ne 0\.1' 'The runbook must fail closed unless both telemetry caps verify as exactly 0.1 GB.'
Assert-Text $platformBicep "APPLICATIONINSIGHTS_ENABLEADAPTIVESAMPLING'[\s\S]*?value:\s*'true'" 'Adaptive sampling must be enabled for production telemetry.'
Assert-TextAbsent $platformBicep '4633458b-17de-408a-b874-0445c86b69e6' 'Vault-wide Key Vault Secrets User grants are prohibited; exact secret grants occur only after the secret census.'
Assert-Text $platformBicep 'Microsoft\.Insights/actionGroups' 'The production action group is missing.'
Assert-Text $platformBicep 'Microsoft\.Insights/metricAlerts' 'The production platform metric alert is missing.'
Assert-Text $platformBicep 'Microsoft\.Insights/scheduledQueryRules' 'The production application exception alert is missing.'
Assert-Text $mainBicep "amount:\s*75" 'The monthly production budget must be GBP 75.'
foreach ($threshold in @(50, 80, 100)) {
    Assert-Text $mainBicep "threshold:\s*$threshold" "The production budget is missing threshold $threshold."
}
if ([regex]::Matches($platformBicep, "resource\s+\w+\s+'Microsoft\.Storage/storageAccounts@").Count -ne 2) {
    throw 'The production template must declare exactly two storage accounts.'
}
Assert-TextAbsent $platformBicep 'workerAuthenticationRing' 'Worker access to the Web authentication ring is prohibited.'
Assert-TextAbsent $combined '(?i)\bdocumentintelligence\b|\bcognitiveservices\b|\bfoundry\b|\bmaps\b|\bvision\b|\bstaticwebapp\b' 'Deferred Azure services are prohibited from the alpha deployment.'

$bootstrapScript = Get-Content -LiteralPath (Join-Path $repositoryRoot 'scripts/Invoke-ProductionAdministratorBootstrap.ps1') -Raw
$databaseBootstrapScript = Get-Content -LiteralPath (Join-Path $repositoryRoot 'scripts/Invoke-AzureDatabaseBootstrap.ps1') -Raw
Assert-Text $bootstrapScript 'Get-FileHash[\s\S]*SHA256' 'Administrator bootstrap must verify the immutable Web package SHA-256.'
Assert-Text $bootstrapScript 'ManifestSha256' 'Administrator bootstrap must require the operator-approved manifest SHA-256.'
Assert-Text $bootstrapScript "Test-AzureDeploymentPlan\.ps1'\) -Mode Artifact" 'Administrator bootstrap must run full release-manifest and artifact validation.'
Assert-Text $bootstrapScript 'e6076573-23a5-46a8-acef-7e22d264e5db' 'Administrator bootstrap must enforce the exact approved subscription.'
Assert-Text $bootstrapScript '858cf5b3-aa0a-47a6-9b40-4851fd0afa94' 'Administrator bootstrap must enforce the exact approved tenant.'
Assert-Text $databaseBootstrapScript 'sid <> \$webSid' 'Database bootstrap must reject an existing Web principal with the wrong SID.'
Assert-Text $databaseBootstrapScript 'sid <> \$workerSid' 'Database bootstrap must reject an existing Worker principal with the wrong SID.'
Assert-Text $databaseBootstrapScript 'sys\.database_role_members' 'Database bootstrap must inspect the complete runtime role-membership graph.'
Assert-Text $databaseBootstrapScript 'fn_my_permissions\(NULL, N''DATABASE''\)' 'Database bootstrap must inspect effective database-scoped runtime permissions.'
Assert-Text $databaseBootstrapScript '__UNAPPROVED_SCOPE_' 'Database bootstrap must surface every non-table or column-level runtime-role permission as an exhaustive matrix difference.'
Assert-Text $databaseBootstrapScript 'ManifestSha256' 'Database bootstrap must require the operator-approved manifest SHA-256.'
Assert-Text $databaseBootstrapScript 'manifest\.sourceRevision' 'Database bootstrap must bind its source checkout to the approved release manifest revision.'
Assert-Text $databaseBootstrapScript 'status --porcelain' 'Database bootstrap must require a clean source checkout before reading the migration-defined matrix.'
Assert-Text $databaseBootstrapScript 'sys\.schemas[\s\S]*sys\.objects[\s\S]*owning_principal_id[\s\S]*owner_sid' 'Database bootstrap must reject schema, object, principal, and database ownership authority.'
Assert-Text $databaseBootstrapScript 'HAS_PERMS_BY_NAME' 'Database bootstrap must compare effective per-table runtime DML with the migration-defined matrix.'

& az bicep build --file $mainBicepPath --stdout | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw 'Bicep compilation failed.'
}

if ($Mode -in @('Artifact', 'PreMigration')) {
    if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
        throw '-ManifestPath is required in Artifact mode.'
    }
    Test-ArtifactManifest -Path $ManifestPath
}

if ($Mode -eq 'PreMigration') {
    if ([string]::IsNullOrWhiteSpace($Environment)) {
        throw '-Environment is required in PreMigration mode.'
    }
    $values = & azd env get-values -e $Environment --no-prompt
    if ($LASTEXITCODE -ne 0) { throw "Unable to read azd environment $Environment." }
    $required = @(
        'AZURE_SUBSCRIPTION_ID', 'AZURE_TENANT_ID', 'AZURE_RESOURCE_GROUP', 'AZURE_SQL_SERVER_FQDN',
        'AZURE_SQL_DATABASE_NAME', 'WEB_IDENTITY_CLIENT_ID', 'WORKER_IDENTITY_CLIENT_ID')
    foreach ($key in $required) {
        if ($values -notmatch "(?m)^$key=") { throw "azd environment $Environment is missing $key." }
    }
    if (
        $values -notmatch '(?m)^AZURE_SUBSCRIPTION_ID="?e6076573-23a5-46a8-acef-7e22d264e5db"?$' -or
        $values -notmatch '(?m)^AZURE_TENANT_ID="?858cf5b3-aa0a-47a6-9b40-4851fd0afa94"?$' -or
        $values -notmatch '(?m)^AZURE_RESOURCE_GROUP="?rg-pegasus-prod"?$'
    ) {
        throw 'Pre-migration validation refuses an environment outside the exact approved subscription, tenant, and resource group.'
    }
}

Write-Output "Azure deployment plan validation passed ($Mode)."
