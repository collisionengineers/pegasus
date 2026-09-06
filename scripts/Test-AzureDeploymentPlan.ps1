[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Local', 'Artifact', 'PreUpload', 'PreMigration', 'PreProvision')]
    [string] $Mode,

    [string] $ManifestPath,

    [string] $Environment,

    [string] $ManifestSha256,

    [string] $WorkerActivation = 'disabled',

    [ValidateSet('disabled', 'approved-live-worker')]
    [string] $ExpectedLiveWorkerActivation,

    [switch] $AllowWorkerDisable
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$mainBicepPath = Join-Path $repositoryRoot 'infra/main.bicep'
$platformBicepPath = Join-Path $repositoryRoot 'infra/modules/platform.bicep'
$parametersPath = Join-Path $repositoryRoot 'infra/main.parameters.json'
$azureYamlPath = Join-Path $repositoryRoot 'azure.yaml'
$productionSmokePath = Join-Path $repositoryRoot 'scripts/Invoke-ProductionSmoke.ps1'
$releaseArtifactPath = Join-Path $repositoryRoot 'scripts/Build-ReleaseArtifacts.ps1'
$expectedWorkerSettings = @(
    'AzureWebJobs.PendingWorkRecoveryFunction.Disabled',
    'AzureWebJobs.UnifiedWorkFunction.Disabled',
    'AzureWebJobs.UnifiedWorkPoisonFunction.Disabled',
    'AzureWebJobs.StagedArtifactReconciliationFunction.Disabled',
    'AzureWebJobs.InboxRecoveryFunction.Disabled',
    'AzureWebJobs.SentEvidencePollFunction.Disabled',
    'AzureWebJobs.DueWorkSweepFunction.Disabled'
)
# The executed production runbook (azure-production-replacement-plan.md) and
# the one-off predecessor archive/retirement scripts were retired after the
# 2026-08-02 release; their content assertions retired with them (git history).

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

function Assert-ExactOrdinalCensus {
    param(
        [Parameter(Mandatory)][string[]] $Expected,
        [Parameter(Mandatory)][string[]] $Actual,
        [Parameter(Mandatory)][string] $Failure
    )

    $expectedNames = [Collections.Generic.HashSet[string]]::new(
        [StringComparer]::Ordinal
    )
    foreach ($name in $Expected) {
        [void]$expectedNames.Add($name)
    }
    $actualNames = [Collections.Generic.HashSet[string]]::new(
        [StringComparer]::Ordinal
    )
    $isExact = $Actual.Count -eq $Expected.Count
    foreach ($name in $Actual) {
        if (-not $expectedNames.Contains($name) -or -not $actualNames.Add($name)) {
            $isExact = $false
        }
    }
    foreach ($name in $Expected) {
        if (-not $actualNames.Contains($name)) {
            $isExact = $false
        }
    }

    if (-not $isExact) {
        throw $Failure
    }
}

function Test-ArtifactManifest {
    param([Parameter(Mandatory)][string] $Path)

    $resolvedManifest = Resolve-Path -LiteralPath $Path
    $manifest = Get-Content -LiteralPath $resolvedManifest -Raw | ConvertFrom-Json
    if ($manifest.schemaVersion -ne 3) {
        throw 'The release manifest schemaVersion must be 3.'
    }
    if ($manifest.sourceRevision -notmatch '^[0-9a-f]{40}$') {
        throw 'The release manifest sourceRevision must be an exact Git SHA.'
    }
    if ($manifest.sourceStatus -ne 'clean') {
        throw 'The release manifest must record a clean source status.'
    }
    if (-not $manifest.artifacts -or $manifest.artifacts.Count -ne 4) {
        throw 'The release manifest must contain exactly the bootstrap Web ZIP, Web OCI archive, Worker ZIP, and migration bundle.'
    }

    $manifestDirectory = Split-Path -Parent $resolvedManifest
    if ($manifest.migrationRuntimeIdentifier -ne 'linux-x64' -or
        $manifest.migrationBundleName -ne 'efbundle') {
        throw 'The release manifest must carry the Linux x64 efbundle.'
    }
    $migrationBundleName = 'efbundle'
    $requiredNames = @('web.zip', 'web-image.tar.gz', 'worker.zip', $migrationBundleName)
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

    $migrationBundlePath = Join-Path $manifestDirectory $migrationBundleName
    $migrationBundleMode = [IO.File]::GetUnixFileMode($migrationBundlePath)
    if (($migrationBundleMode -band [IO.UnixFileMode]::UserExecute) -eq 0) {
        throw 'The Linux x64 migration bundle must be executable by its owner.'
    }

    if (
        $manifest.webImage.repository -ne 'pegasus/web' -or
        $manifest.webImage.tag -ne $manifest.sourceRevision -or
        $manifest.webImage.digest -notmatch '^sha256:[0-9a-f]{64}$' -or
        $manifest.webImage.platform -ne 'linux/amd64' -or
        $manifest.webImage.archive -ne 'web-image.tar.gz'
    ) {
        throw 'The release manifest Web OCI identity is incomplete or invalid.'
    }
    $imageArchive = Join-Path $manifestDirectory 'web-image.tar.gz'
    $descriptor = & oras manifest fetch --oci-layout "${imageArchive}:$($manifest.webImage.tag)" --descriptor | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or $descriptor.digest -ne $manifest.webImage.digest) {
        throw 'The Web OCI archive descriptor differs from the release manifest.'
    }
    $imageManifest = & oras manifest fetch --oci-layout "${imageArchive}:$($manifest.webImage.tag)" | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or $imageManifest.config.digest -notmatch '^sha256:[0-9a-f]{64}$') {
        throw 'The Web OCI config descriptor is invalid.'
    }
    $imageConfig = & oras blob fetch --oci-layout --output - "${imageArchive}@$($imageManifest.config.digest)" | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or "$($imageConfig.os)/$($imageConfig.architecture)" -ne $manifest.webImage.platform) {
        throw 'The inspected Web OCI platform differs from the release manifest.'
    }
}

$mainBicep = Get-Content -LiteralPath $mainBicepPath -Raw
$platformBicep = Get-Content -LiteralPath $platformBicepPath -Raw
$parameters = Get-Content -LiteralPath $parametersPath -Raw
$azureYaml = Get-Content -LiteralPath $azureYamlPath -Raw
$productionSmoke = Get-Content -LiteralPath $productionSmokePath -Raw
$releaseArtifactScript = Get-Content -LiteralPath $releaseArtifactPath -Raw
$combined = "$mainBicep`n$platformBicep`n$parameters`n$azureYaml"

Assert-Text $releaseArtifactScript "Get-PegasusPlatform[\s\S]*?IsLinux" 'Release artifact construction must require the Linux platform helper.'
Assert-Text $releaseArtifactScript "OSArchitecture[\s\S]*?Architecture\]::X64" 'Release artifact construction must require x64.'
Assert-Text $releaseArtifactScript "migrationRuntimeIdentifier\s*=\s*'linux-x64'" 'Release artifact construction must fix the migration runtime to linux-x64.'
Assert-Text $releaseArtifactScript "migrationBundleName\s*=\s*'efbundle'" 'Release artifact construction must fix the migration bundle name to efbundle.'
Assert-Text $releaseArtifactScript 'schemaVersion\s*=\s*3' 'Release artifact construction must emit manifest schema 3.'
Assert-TextAbsent $releaseArtifactScript 'win-x64|efbundle\.exe' 'The active release artifact route must not retain a Windows bundle path.'

Assert-Text $mainBicep "@allowed\(\[\s*'prod'\s*\]\)" 'infra/main.bicep must accept production only.'
Assert-Text $mainBicep "deploymentMode\s*==\s*'approved-live-deployment'" 'Bicep must fail closed unless approved-live-deployment is supplied.'
Assert-Text $mainBicep "param\s+webActivation\s+string\s*=\s*'disabled'" 'Base provisioning must leave Web activation disabled by default.'
Assert-Text $mainBicep "param\s+workerActivation\s+string\s*=\s*'disabled'" 'Base provisioning must leave Worker activation disabled by default.'
Assert-Text $mainBicep 'workerActivation:\s*workerActivation' 'The main template must pass the Worker activation input to the platform module.'
Assert-Text $parameters '"workerActivation"\s*:\s*\{\s*"value"\s*:\s*"\$\{PEGASUS_WORKER_ACTIVATION=disabled\}"\s*\}' 'The azd parameter map must default PEGASUS_WORKER_ACTIVATION to disabled.'
Assert-Text $parameters 'GRAPH_CHANGE_NOTIFICATION_CLIENT_STATE_SECRET_URI' 'The Graph notification clientState must be supplied as a versioned secret URI.'
Assert-Text $platformBicep "webImageReference\s*=\s*'\$\{containerRegistryName\}\.azurecr\.io/pegasus/web@\$\{webImageDigest\}'" 'The template must own the exact ACR and repository image prefix.'
Assert-Text $platformBicep "webActivation\s*==\s*'approved'[\s\S]*?startsWith\(webImageDigest,\s*'sha256:'\)[\s\S]*?length\(webImageDigest\)\s*==\s*71[\s\S]*?length\(webRevisionSuffix\)\s*==\s*12" 'Approved Web activation must require a sha256 digest and exact revision suffix.'
Assert-Text $platformBicep "workerActivationApproved\s*=\s*workerActivation\s*==\s*'approved-live-worker'" 'Only the exact approved-live-worker value may enable the production Worker.'
Assert-Text $platformBicep "scaleAndConcurrency:\s*\{[\s\S]*?instanceMemoryMB:\s*2048[\s\S]*?alwaysReady:\s*\[[\s\S]*?name:\s*'function:UnifiedWorkFunction'[\s\S]*?instanceCount:\s*1" 'The Worker must retain one 2 GiB always-ready unified queue consumer.'
Assert-Text $platformBicep "resource\s+webContainerApp[\s\S]*?if\s*\(webActivationApproved\)" 'The Web Container App must be conditional on approved activation.'
Assert-Text $platformBicep "image:\s*webImageReference" 'The Container App must use the exact supplied digest reference.'
Assert-Text $platformBicep "activeRevisionsMode:\s*'Single'" 'The Container App must use one active revision.'
Assert-Text $platformBicep "targetPort:\s*8080" 'The Container App ingress must target port 8080.'
Assert-Text $platformBicep "minReplicas:\s*1[\s\S]*?maxReplicas:\s*1" 'The Web Container App must retain exactly one always-warm replica.'
Assert-Text $platformBicep "Graph__ChangeNotificationClientState'[\s\S]*?secretRef:\s*'graph-change-notification-client-state'" 'The Web callback must receive clientState only through its Key Vault-backed secret.'
Assert-Text $platformBicep "Graph__ChangeNotificationUrl'[\s\S]*?/hooks/microsoft-graph/mail" 'The Worker must maintain the exact Web Graph callback URL.'
Assert-Text $platformBicep "ApprovedInboxPollSchedule'[\s\S]*?value:\s*'0 \*/5 \* \* \* \*'" 'Approved Inbox polling must be five-minute recovery, not the ordinary intake path.'
# Raised from 0.5 vCPU / 1 GiB on the operator's decision (2026-08-19,
# DELIV-012) when the report renderer began running in process in this
# container per ADR-0028: headless Chromium shares the app's CPU and memory,
# Container Apps hard-OOM-kills rather than throttling, and the app runs a
# single always-warm replica. The exact pair stays asserted so a later change
# cannot drift the sizing silently.
Assert-Text $platformBicep "cpu:\s*json\('1\.0'\)[\s\S]*?memory:\s*'2Gi'" 'The Web Container App must use 1.0 vCPU and 2 GiB.'
Assert-Text $platformBicep "sku:\s*\{\s*name:\s*'Basic'\s*\}[\s\S]*?adminUserEnabled:\s*false" 'The production ACR must be Basic with admin credentials disabled.'
Assert-Text $platformBicep "roleDefinitionId:\s*acrPullRole" 'The Web identity must receive AcrPull at the production ACR.'
Assert-Text $platformBicep "queueDataMessageSenderRole\s*=\s*subscriptionResourceId\('Microsoft.Authorization/roleDefinitions',\s*'c6a89b2d-59bc-44d0-9896-0f6e12d7b80a'\)" 'The Web must use the built-in Storage Queue Data Message Sender role.'
Assert-Text $platformBicep "resource\s+webIntakeQueueSender[\s\S]*?scope:\s*intakeQueue[\s\S]*?roleDefinitionId:\s*queueDataMessageSenderRole" 'The Web identity must receive sender-only access scoped to intake-work.'
if ([regex]::Matches($platformBicep, 'roleDefinitionId:\s*monitoringMetricsPublisherRole').Count -ne 2) {
    throw 'Both Web and Worker identities must receive Monitoring Metrics Publisher at Application Insights.'
}
Assert-Text $platformBicep "server:\s*containerRegistry\.properties\.loginServer[\s\S]*?identity:\s*webIdentity\.id" 'The Container App must pull from ACR through the Web user-assigned identity.'
Assert-Text $mainBicep 'WEB_CONTAINER_APP_FQDN' 'The Container App FQDN must be exported.'
Assert-Text $mainBicep 'CONTAINER_REGISTRY_LOGIN_SERVER' 'The ACR login server must be exported.'
Assert-Text $azureYaml "host:\s*containerapp" 'azure.yaml must select Container Apps for Web.'
Assert-TextAbsent $combined "(?im)^\s*SCM_DO_BUILD_DURING_DEPLOYMENT\s*[:=]" 'Remote build is prohibited.'
Assert-TextAbsent $combined "(?i)offline-replay|rg-pegasus-dev|pegasusdev" 'Azure deployment files must not contain a development/offline target.'
Assert-Text $platformBicep 'transportStorageName' 'The transport/deployment storage account is missing.'
Assert-Text $platformBicep 'custodyStorageName' 'The custody/protection storage account is missing.'
Assert-Text $platformBicep "name:\s*'ASPNETCORE_ENVIRONMENT'[\s\S]*?value:\s*'Production'" 'Web must use ASPNETCORE_ENVIRONMENT=Production.'
Assert-Text $platformBicep "name:\s*'Runtime__Profile'[\s\S]*?value:\s*'Production'" 'Worker must use Runtime__Profile=Production.'
Assert-Text $platformBicep "name:\s*'APPLICATIONINSIGHTS_AUTHENTICATION_STRING'" 'Application Insights local authentication must be disabled through managed-identity configuration.'
$sourceWorkerNameMatches = [regex]::Matches(
    $platformBicep,
    "name:\s*'(AzureWebJobs\.[^']+\.Disabled)'"
)
$sourceWorkerNames = @($sourceWorkerNameMatches | ForEach-Object { $_.Groups[1].Value })
Assert-ExactOrdinalCensus `
    -Expected $expectedWorkerSettings `
    -Actual $sourceWorkerNames `
    -Failure 'The Worker template must contain the exact seven-function disabled-setting name census.'
$sourceWorkerConditionalMatches = [regex]::Matches(
    $platformBicep,
    "name:\s*'(AzureWebJobs\.[^']+\.Disabled)'\s*,\s*value:\s*workerActivationApproved\s*\?\s*'false'\s*:\s*'true'"
)
$sourceConditionalWorkerNames = @(
    $sourceWorkerConditionalMatches | ForEach-Object { $_.Groups[1].Value }
)
Assert-ExactOrdinalCensus `
    -Expected $expectedWorkerSettings `
    -Actual $sourceConditionalWorkerNames `
    -Failure 'Every exact Worker disabled setting must use the approved fail-closed conditional.'

function Get-AzdEnvironmentMap {
    param([Parameter(Mandatory)][string] $Name)

    $lines = @(& azd env get-values -e $Name --no-prompt)
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to read azd environment $Name."
    }

    $values = @{}
    foreach ($line in $lines) {
        if ($line -notmatch '^([^=]+)=(.*)$') {
            continue
        }

        $key = $Matches[1]
        $value = $Matches[2]
        if ($value.Length -ge 2 -and $value[0] -eq '"' -and $value[-1] -eq '"') {
            $value = $value.Substring(1, $value.Length - 2).Replace('\"', '"')
        }
        $values[$key] = $value
    }

    return $values
}
Assert-Text $platformBicep 'retentionInDays:\s*31' 'Log Analytics retention must be exactly 31 days.'
Assert-Text $mainBicep 'LOG_ANALYTICS_WORKSPACE_NAME' 'The Log Analytics workspace name must be exported for exact post-provision configuration.'
Assert-Text $platformBicep "APPLICATIONINSIGHTS_ENABLEADAPTIVESAMPLING'[\s\S]*?value:\s*'true'" 'Adaptive sampling must be enabled for production telemetry.'
Assert-TextAbsent $platformBicep "resource\s+webPlan\b|name:\s*'P0v4'|kind:\s*'app,linux'" 'The superseded App Service Web route is prohibited.'
Assert-TextAbsent $platformBicep '4633458b-17de-408a-b874-0445c86b69e6' 'Vault-wide Key Vault Secrets User grants are prohibited; exact secret grants occur only after the secret census.'
Assert-Text $platformBicep 'Microsoft\.Insights/actionGroups' 'The production action group is missing.'
Assert-Text $platformBicep 'Microsoft\.Insights/metricAlerts' 'The production platform metric alert is missing.'
Assert-Text $platformBicep "metricNamespace:\s*'Microsoft\.App/containerapps'[\s\S]*?metricName:\s*'Requests'[\s\S]*?name:\s*'StatusCodeCategory'[\s\S]*?values:\s*\['5xx'\]" 'The Web 5xx alert must use the Container Apps Requests metric and status category.'
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
Assert-Text $bootstrapScript 'manifest\.schemaVersion\s+-ne\s+3' 'Administrator bootstrap must accept only the current schema-3 release manifest.'
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
$grantMigrationFiles = Get-ChildItem `
    -LiteralPath (Join-Path $repositoryRoot 'src/Pegasus.Infrastructure/Persistence/Migrations') `
    -Filter '*.cs' |
    Where-Object {
        $_.Name -notlike '*.Designer.cs' -and
        $_.Name -ne '20260729199000_RuntimeRoleReconciliation.cs' -and
        $_.Name -gt '20260729199000_RuntimeRoleReconciliation.cs' -and
        (Get-Content -Raw -LiteralPath $_.FullName) -cmatch '\bGRANT\s'
    }
foreach ($grantMigration in $grantMigrationFiles) {
    Assert-Text `
        $databaseBootstrapScript `
        ([regex]::Escape([IO.Path]::GetFileNameWithoutExtension($grantMigration.Name))) `
        "Database bootstrap must account for grant-carrying migration $($grantMigration.Name)."
}

$smokeWorkerMatches = [regex]::Matches(
    $productionSmoke,
    "'(AzureWebJobs\.[^']+\.Disabled)'"
)
$smokeWorkerSettings = @($smokeWorkerMatches | ForEach-Object { $_.Groups[1].Value })
Assert-ExactOrdinalCensus `
    -Expected $expectedWorkerSettings `
    -Actual $smokeWorkerSettings `
    -Failure 'Production smoke must inspect the exact seven-function disabled-setting census.'
Assert-Text $productionSmoke 'az\s+functionapp\s+config\s+appsettings\s+list' 'Production smoke must read the live Worker app settings.'
Assert-Text $productionSmoke "ExpectedWorkerActivation\s*-eq\s*'approved-live-worker'[\s\S]*?'false'[\s\S]*?'true'" 'Production smoke must map approved-live-worker to enabled settings and disabled to disabled settings.'
Assert-Text $productionSmoke 'HashSet\[string\][\s\S]*StringComparer\]::Ordinal' 'Production smoke must compare every Worker setting name with ordinal semantics.'
Assert-Text $productionSmoke '--subscription\s+\$SubscriptionId' 'Production smoke must pass the approved subscription explicitly to Azure CLI.'
Assert-Text $productionSmoke "workerAppName\s*=\s*'pegasus-prod-worker-252ow37gij'" 'Production smoke must bind readback to the exact reviewed Worker identity.'
Assert-Text $productionSmoke 'WorkerOnly' 'Production smoke must expose its read-only Worker assertion for pre-provision validation.'
Assert-Text $productionSmoke 'ActivationOnly' 'Production smoke must expose activation-only validation for pre-provision releases that rename functions.'
Assert-Text $productionSmoke 'if\s*\(\s*-not\s+\$ActivationOnly\s+-and\s+-not\s+\$censusIsExact\s*\)' 'Production smoke must keep the exact Worker census as its default.'
Assert-Text $productionSmoke "ends_with\(name, '\.Disabled'\)" 'Production smoke activation validation must inspect only function disabled settings.'
Assert-Text $productionSmoke 'if\s*\(\s*-not\s+\$ActivationOnly\s*\)[\s\S]*?PendingWorkRecoverySchedule[\s\S]*?''0 \* \* \* \* \*''[\s\S]*?if\s*\(\s*\$WorkerOnly\s*\)' 'Every post-deployment smoke path must require the live recovery timer to run once per minute.'

$compiledTemplateJson = (& az bicep build --file $mainBicepPath --stdout) -join "`n"
if ($LASTEXITCODE -ne 0) {
    throw 'Bicep compilation failed.'
}
$compiledWorkerNameMatches = [regex]::Matches(
    $compiledTemplateJson,
    '"name"\s*:\s*"(AzureWebJobs\.[^"]+\.Disabled)"'
)
$compiledWorkerNames = @($compiledWorkerNameMatches | ForEach-Object { $_.Groups[1].Value })
Assert-ExactOrdinalCensus `
    -Expected $expectedWorkerSettings `
    -Actual $compiledWorkerNames `
    -Failure 'The compiled template must contain the exact seven-function disabled-setting name census.'
$compiledWorkerConditionalMatches = [regex]::Matches(
    $compiledTemplateJson,
    '"name"\s*:\s*"(AzureWebJobs\.[^"]+\.Disabled)"\s*,\s*"value"\s*:\s*"\[if\(variables\(''workerActivationApproved''\), ''false'', ''true''\)\]"'
)
$compiledConditionalWorkerNames = @(
    $compiledWorkerConditionalMatches | ForEach-Object { $_.Groups[1].Value }
)
Assert-ExactOrdinalCensus `
    -Expected $expectedWorkerSettings `
    -Actual $compiledConditionalWorkerNames `
    -Failure 'The compiled template must contain the exact seven-function fail-closed Worker setting expressions.'
Assert-Text $compiledTemplateJson '"workerActivationApproved"\s*:\s*"\[equals\(parameters\(''workerActivation''\), ''approved-live-worker''\)\]"' 'The compiled template must enable the Worker only for the exact approved-live-worker input.'
Assert-Text $compiledTemplateJson '"workerActivation"\s*:\s*\{\s*"type"\s*:\s*"string"\s*,\s*"defaultValue"\s*:\s*"disabled"' 'The compiled template must retain the fail-closed Worker activation default.'

$expectedRenderedValue = if ($WorkerActivation -eq 'approved-live-worker') {
    'false'
}
else {
    'true'
}

if ($Mode -in @('Artifact', 'PreUpload', 'PreMigration')) {
    if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
        throw '-ManifestPath is required in Artifact mode.'
    }
    Test-ArtifactManifest -Path $ManifestPath
}

if ($Mode -in @('PreUpload', 'PreMigration')) {
    if ($ManifestSha256 -notmatch '^[0-9a-fA-F]{64}$') {
        throw '-ManifestSha256 must be the operator-approved 64-character SHA-256 in PreUpload and PreMigration modes.'
    }
    $actualManifestSha256 = (Get-FileHash -LiteralPath (Resolve-Path -LiteralPath $ManifestPath) -Algorithm SHA256).Hash
    if (-not $actualManifestSha256.Equals($ManifestSha256, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'release-manifest.json does not match the operator-approved SHA-256.'
    }
}

if ($Mode -eq 'PreMigration') {
    if ([string]::IsNullOrWhiteSpace($Environment)) {
        throw '-Environment is required in PreMigration mode.'
    }
    $values = (& azd env get-values -e $Environment --no-prompt) -join "`n"
    if ($LASTEXITCODE -ne 0) { throw "Unable to read azd environment $Environment." }
    $required = @(
        'AZURE_SUBSCRIPTION_ID', 'AZURE_TENANT_ID', 'AZURE_RESOURCE_GROUP', 'AZURE_SQL_SERVER_FQDN',
        'AZURE_SQL_DATABASE_NAME', 'WEB_IDENTITY_CLIENT_ID', 'WORKER_IDENTITY_CLIENT_ID',
        'CONTAINER_REGISTRY_NAME', 'CONTAINER_REGISTRY_LOGIN_SERVER')
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

if ($Mode -eq 'PreProvision') {
    if ([string]::IsNullOrWhiteSpace($Environment)) {
        throw '-Environment is required in PreProvision mode.'
    }
    if ([string]::IsNullOrWhiteSpace($ExpectedLiveWorkerActivation)) {
        throw '-ExpectedLiveWorkerActivation is required in PreProvision mode.'
    }
    if ($WorkerActivation -notin @('disabled', 'approved-live-worker')) {
        throw 'Pre-provision validation accepts only disabled or the exact approved-live-worker desired value.'
    }

    $environmentValues = Get-AzdEnvironmentMap -Name $Environment
    $required = @(
        'AZURE_SUBSCRIPTION_ID',
        'AZURE_TENANT_ID',
        'AZURE_RESOURCE_GROUP',
        'WORKER_APP_NAME',
        'PEGASUS_WORKER_ACTIVATION',
        'BOX_HOLDING_FOLDER_ID',
        'AUTOMATION_MCP_SIGNING_CERTIFICATE_SECRET_URIS',
        'AUTOMATION_MCP_ENCRYPTION_CERTIFICATE_SECRET_URIS'
    )
    foreach ($key in $required) {
        if (-not $environmentValues.ContainsKey($key) -or
            [string]::IsNullOrWhiteSpace([string]$environmentValues[$key])) {
            throw "azd environment $Environment is missing $key."
        }
    }
    $certificateVaultHost = $null
    foreach ($key in @('AUTOMATION_MCP_SIGNING_CERTIFICATE_SECRET_URIS',
        'AUTOMATION_MCP_ENCRYPTION_CERTIFICATE_SECRET_URIS')) {
        foreach ($value in ([string]$environmentValues[$key]).Split(',')) {
            $certificateUri = $null
            if (-not [Uri]::TryCreate($value.Trim(), [UriKind]::Absolute, [ref]$certificateUri) -or
                $certificateUri.Scheme -ne 'https' -or
                -not $certificateUri.Host.EndsWith('.vault.azure.net', [StringComparison]::OrdinalIgnoreCase) -or
                -not $certificateUri.IsDefaultPort -or
                $certificateUri.UserInfo.Length -ne 0 -or
                $certificateUri.Query.Length -ne 0 -or
                $certificateUri.Fragment.Length -ne 0 -or
                $certificateUri.AbsolutePath -cnotmatch '^/secrets/[^/]+/[^/]+/?$') {
                throw "$key must contain comma-separated versioned Azure Key Vault HTTPS secret URIs."
            }
            if ($null -ne $certificateVaultHost -and
                -not $certificateVaultHost.Equals($certificateUri.Host, [StringComparison]::OrdinalIgnoreCase)) {
                throw 'Automation MCP signing and encryption certificates must belong to the same Azure Key Vault.'
            }
            $certificateVaultHost = $certificateUri.Host
        }
    }
    if ($environmentValues.ContainsKey('AZURE_KEY_VAULT_NAME') -and
        -not [string]::IsNullOrWhiteSpace([string]$environmentValues['AZURE_KEY_VAULT_NAME']) -and
        -not $certificateVaultHost.Equals(
            "$($environmentValues['AZURE_KEY_VAULT_NAME']).vault.azure.net", [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Automation MCP certificates must belong to the deployment environment Azure Key Vault.'
    }
    if (
        $environmentValues['AZURE_SUBSCRIPTION_ID'] -ne 'e6076573-23a5-46a8-acef-7e22d264e5db' -or
        $environmentValues['AZURE_TENANT_ID'] -ne '858cf5b3-aa0a-47a6-9b40-4851fd0afa94' -or
        $environmentValues['AZURE_RESOURCE_GROUP'] -ne 'rg-pegasus-prod' -or
        $environmentValues['WORKER_APP_NAME'] -ne 'pegasus-prod-worker-252ow37gij'
    ) {
        throw 'Pre-provision validation refuses an environment outside the exact approved production Worker target.'
    }
    if ($environmentValues['PEGASUS_WORKER_ACTIVATION'] -cne $WorkerActivation) {
        throw 'The desired Worker activation differs from the explicit PEGASUS_WORKER_ACTIVATION azd environment value.'
    }
    if ($AllowWorkerDisable -and
        ($ExpectedLiveWorkerActivation -ne 'approved-live-worker' -or
            $WorkerActivation -ne 'disabled')) {
        throw '-AllowWorkerDisable is valid only for an explicit enabled-to-disabled rollback.'
    }
    if ($ExpectedLiveWorkerActivation -eq 'approved-live-worker' -and
        $WorkerActivation -ne 'approved-live-worker' -and
        -not $AllowWorkerDisable) {
        throw 'An enabled production Worker may not be redeployed with an omitted or disabled desired activation.'
    }

    & $productionSmokePath `
        -WorkerOnly `
        -ActivationOnly `
        -SubscriptionId 'e6076573-23a5-46a8-acef-7e22d264e5db' `
        -ResourceGroupName 'rg-pegasus-prod' `
        -ExpectedWorkerActivation $ExpectedLiveWorkerActivation
}

Write-Output "Azure deployment plan validation passed ($Mode; Worker Disabled settings render '$expectedRenderedValue')."
