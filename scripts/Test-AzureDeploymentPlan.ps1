[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Local', 'Artifact', 'PreDeploy', 'PreMigration', 'PreProvision')]
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
. (Join-Path $PSScriptRoot 'PegasusPlatform.ps1')
$mainBicepPath = Join-Path $repositoryRoot 'infra/main.bicep'
$platformBicepPath = Join-Path $repositoryRoot 'infra/modules/platform.bicep'
$parametersPath = Join-Path $repositoryRoot 'infra/main.parameters.json'
$azureYamlPath = Join-Path $repositoryRoot 'azure.yaml'
$productionSmokePath = Join-Path $repositoryRoot 'scripts/Invoke-ProductionSmoke.ps1'
$expectedWorkerSettings = @(Get-PegasusWorkerDisabledSettingNames)

# Local mode owns the deployment template's invariants. An assertion belongs
# here only when losing the thing it names would be a security, cost or
# fail-closed regression, or when the release depends on it (compile, identity).
# Configuration echoes (SKUs, ports, output names) are not asserted: the
# template is their owner and a test that restates it proves nothing.

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

$mainBicep = Get-Content -LiteralPath $mainBicepPath -Raw
$platformBicep = Get-Content -LiteralPath $platformBicepPath -Raw
$parameters = Get-Content -LiteralPath $parametersPath -Raw
$azureYaml = Get-Content -LiteralPath $azureYamlPath -Raw
$combined = "$mainBicep`n$platformBicep`n$parameters`n$azureYaml"

# Fail-closed activation: nothing is created or enabled unless the exact
# approval value is supplied.
Assert-Text $mainBicep "@allowed\(\[\s*'prod'\s*\]\)" 'infra/main.bicep must accept production only.'
Assert-Text $mainBicep "deploymentMode\s*==\s*'approved-live-deployment'" 'Bicep must fail closed unless approved-live-deployment is supplied.'
Assert-Text $mainBicep "param\s+webActivation\s+string\s*=\s*'disabled'" 'Base provisioning must leave Web activation disabled by default.'
Assert-Text $mainBicep "param\s+workerActivation\s+string\s*=\s*'disabled'" 'Base provisioning must leave Worker activation disabled by default.'
Assert-Text $parameters '"workerActivation"\s*:\s*\{\s*"value"\s*:\s*"\$\{PEGASUS_WORKER_ACTIVATION=disabled\}"\s*\}' 'The azd parameter map must default PEGASUS_WORKER_ACTIVATION to disabled.'
Assert-Text $platformBicep "webActivationApproved\s*=\s*webActivation\s*==\s*'approved'" 'Only the exact approved value may create the production Web App.'
Assert-Text $platformBicep "workerActivationApproved\s*=\s*workerActivation\s*==\s*'approved-live-worker'" 'Only the exact approved-live-worker value may enable the production Worker.'
$webAppMatches = [regex]::Matches($platformBicep, "(?ms)^resource webApp 'Microsoft\.Web/sites@[^']+' = if \(webActivationApproved\) \{.*?^\}")
if ($webAppMatches.Count -ne 1) {
    throw 'The Web App must be declared exactly once and conditional on approved activation.'
}
$webAppResource = $webAppMatches[0].Value
$webPlanMatches = [regex]::Matches($platformBicep, "(?ms)^resource webPlan 'Microsoft\.Web/serverfarms@[^']+' = \{.*?^\}")
if ($webPlanMatches.Count -ne 1) {
    throw 'The production template must declare exactly one Web App Service plan.'
}
$webPlanResource = $webPlanMatches[0].Value

# Transport and credential security.
Assert-Text $webAppResource "httpsOnly:\s*true" 'The Web App must be HTTPS only.'
Assert-Text $webAppResource "ftpsState:\s*'Disabled'" 'The Web App must disable FTPS.'
Assert-Text $webAppResource "minTlsVersion:\s*'1\.2'" 'The Web App must require TLS 1.2.'
Assert-Text $webAppResource "Graph__ChangeNotificationClientState',\s*value:\s*'@Microsoft\.KeyVault\(SecretUri=\$\{graphChangeNotificationClientStateSecretUri\}\)'" 'The Web callback must receive clientState only through its Key Vault reference.'
Assert-Text $parameters 'GRAPH_CHANGE_NOTIFICATION_CLIENT_STATE_SECRET_URI' 'The Graph notification clientState must be supplied as a versioned secret URI.'
Assert-Text $platformBicep "resource\s+webAppScmBasicAuth[\s\S]*?name:\s*'scm'[\s\S]*?allow:\s*false" 'Kudu basic publishing credentials must stay disabled.'
Assert-Text $platformBicep "resource\s+webAppFtpBasicAuth[\s\S]*?name:\s*'ftp'[\s\S]*?allow:\s*false" 'FTP basic publishing credentials must stay disabled.'
Assert-Text $platformBicep "name:\s*'APPLICATIONINSIGHTS_AUTHENTICATION_STRING'" 'Application Insights local authentication must be disabled through managed-identity configuration.'
Assert-TextAbsent $platformBicep '4633458b-17de-408a-b874-0445c86b69e6' 'Vault-wide Key Vault Secrets User grants are prohibited; exact secret grants occur only after the secret census.'
Assert-TextAbsent $platformBicep 'workerAuthenticationRing' 'Worker access to the Web authentication ring is prohibited.'
Assert-Text $platformBicep "queueDataMessageSenderRole\s*=\s*subscriptionResourceId\('Microsoft.Authorization/roleDefinitions',\s*'c6a89b2d-59bc-44d0-9896-0f6e12d7b80a'\)" 'The Web must use the built-in Storage Queue Data Message Sender role.'
Assert-Text $platformBicep "resource\s+webIntakeQueueSender[\s\S]*?scope:\s*intakeQueue[\s\S]*?roleDefinitionId:\s*queueDataMessageSenderRole" 'The Web identity must receive sender-only access scoped to intake-work.'

# ADR-0040 permits one keyless Document Intelligence account, not the other
# Cognitive Services kinds or an additional account.
$ocrAccounts = [regex]::Matches(
    $platformBicep,
    "(?ms)^resource documentIntelligence 'Microsoft\.CognitiveServices/accounts@2026-05-01' = \{.*?^\}"
)
if ($ocrAccounts.Count -ne 1) {
    throw 'The production template must declare exactly the approved Document Intelligence account.'
}
$ocrAccount = $ocrAccounts[0].Value
Assert-Text $ocrAccount "kind:\s*'FormRecognizer'" 'Only the FormRecognizer Document Intelligence account kind is approved.'
Assert-Text $ocrAccount 'disableLocalAuth:\s*true' 'Document Intelligence local authentication must remain disabled.'
Assert-TextAbsent ($combined.Replace($ocrAccount, '')) '(?i)\bcognitiveservices\b' 'Additional Cognitive Services resources are prohibited.'

# Scope: no container hosting, no host builds, no deferred services, no
# development targets in the production template (ADR-0049).
Assert-TextAbsent $webAppResource "(?i)docker|containerapp|azurecr" 'The Web App must not reference a container image or registry.'
Assert-TextAbsent $combined "(?i)Microsoft\.App/|Microsoft\.ContainerRegistry/|acrPull|containerRegistry" 'ADR-0049: Container Apps and the container registry have left the template.'
Assert-Text $azureYaml "host:\s*appservice" 'azure.yaml must select App Service for Web.'
Assert-TextAbsent $azureYaml "(?s)web:.*?remoteBuild:\s*true.*?worker:" 'The Web service must not request a remote build.'
Assert-Text $webAppResource "name:\s*'SCM_DO_BUILD_DURING_DEPLOYMENT',\s*value:\s*'false'" 'The Web App must never build on the host.'
# The Web App's SCM_DO_BUILD_DURING_DEPLOYMENT=false is asserted above; any
# other spelling of a host build, in any deployment file, is prohibited.
Assert-TextAbsent ($combined.Replace("{ name: 'SCM_DO_BUILD_DURING_DEPLOYMENT', value: 'false' }", '')) "SCM_DO_BUILD_DURING_DEPLOYMENT|ENABLE_ORYX_BUILD" 'Remote build is prohibited.'
Assert-TextAbsent $combined "(?i)offline-replay|rg-pegasus-dev|pegasusdev" 'Azure deployment files must not contain a development/offline target.'
Assert-TextAbsent $combined '(?i)\bfoundry\b|\bmaps\b|\bvision\b|\bstaticwebapp\b' 'Deferred Azure services are prohibited from the alpha deployment.'

# Cost.
Assert-Text $mainBicep "amount:\s*75" 'The monthly production budget must be GBP 75.'
foreach ($threshold in @(50, 80, 100)) {
    Assert-Text $mainBicep "threshold:\s*$threshold" "The production budget is missing threshold $threshold."
}

# Worker census in source: every disabled setting is the exact fail-closed
# conditional, and the set is exactly the shared producer's.
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

# The compiled template is what Azure deploys; the census and fail-closed
# default are re-checked there so a bicep expression cannot hide a change.
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

if ($Mode -in @('Artifact', 'PreDeploy', 'PreMigration')) {
    if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
        throw '-ManifestPath is required in Artifact mode.'
    }
    Test-PegasusArtifactManifest -Path $ManifestPath
}

if ($Mode -in @('PreDeploy', 'PreMigration')) {
    if ($ManifestSha256 -notmatch '^[0-9a-fA-F]{64}$') {
        throw '-ManifestSha256 must be the operator-approved 64-character SHA-256 in PreDeploy and PreMigration modes.'
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
        'WORKER_APP_NAME')
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

    # ADR-0049 quota pre-flight. The Web plan is fixed compute, so the Web
    # region must hold App Service VM quota for the plan SKU before provision.
    # On 2026-09-13 the subscription's per-SKU quota in the platform region
    # read 0 for every SKU while other UK regions carried only an aggregate
    # row, so the SKU row is checked first and the aggregate ('*') row only
    # when no SKU row exists. Read-only.
    $webRegion = if ($environmentValues.ContainsKey('PEGASUS_WEB_LOCATION') -and
        -not [string]::IsNullOrWhiteSpace([string]$environmentValues['PEGASUS_WEB_LOCATION'])) {
        [string]$environmentValues['PEGASUS_WEB_LOCATION']
    }
    elseif ($environmentValues.ContainsKey('AZURE_LOCATION') -and
        -not [string]::IsNullOrWhiteSpace([string]$environmentValues['AZURE_LOCATION'])) {
        [string]$environmentValues['AZURE_LOCATION']
    }
    else {
        'uksouth'
    }
    if ($webRegion -cnotmatch '^[a-z0-9]+$') {
        throw "The Web region '$webRegion' is not a lowercase Azure region name."
    }
    $webPlanSku = [regex]::Match($webPlanResource, "sku:\s*\{\s*name:\s*'([^']+)'").Groups[1].Value
    if ([string]::IsNullOrWhiteSpace($webPlanSku)) { throw 'Unable to read the Web plan SKU from the template.' }
    $quotaJson = (& az rest --method get `
        --url "https://management.azure.com/subscriptions/$($environmentValues['AZURE_SUBSCRIPTION_ID'])/providers/Microsoft.Web/locations/$webRegion/providers/Microsoft.Quota/quotas?api-version=2023-02-01" `
        --output json) -join "`n"
    if ($LASTEXITCODE -ne 0) { throw "Unable to read the App Service quota for $webRegion." }
    try { $quotaItems = @(($quotaJson | ConvertFrom-Json).value) }
    catch { throw "The App Service quota read-back for $webRegion was not valid JSON." }
    $skuQuota = @($quotaItems | Where-Object { [string]$_.name -ceq $webPlanSku })
    $aggregateQuota = @($quotaItems | Where-Object { [string]$_.name -ceq '*' })
    $effectiveQuota = if ($skuQuota.Count -eq 1) { $skuQuota[0] } elseif ($aggregateQuota.Count -eq 1) { $aggregateQuota[0] } else { $null }
    if ($null -eq $effectiveQuota) {
        throw "The App Service quota list for $webRegion has neither a $webPlanSku row nor an aggregate row."
    }
    $effectiveLimit = [int]$effectiveQuota.properties.limit.value
    if ($effectiveLimit -lt 1) {
        $summary = ($quotaItems | Where-Object { [int]$_.properties.limit.value -gt 0 } |
            ForEach-Object { "$($_.name)=$($_.properties.limit.value)" }) -join ', '
        if ([string]::IsNullOrWhiteSpace($summary)) { $summary = 'none' }
        throw "App Service $webPlanSku quota in $webRegion is $effectiveLimit ($($effectiveQuota.name) row). Request a quota increase for $webRegion or set PEGASUS_WEB_LOCATION to a region with quota. Rows with quota in ${webRegion}: $summary."
    }
    Write-Output "App Service quota pre-flight passed: $webPlanSku in $webRegion has limit $effectiveLimit ($($effectiveQuota.name) row)."
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
