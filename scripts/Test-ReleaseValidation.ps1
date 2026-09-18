#!/usr/bin/env pwsh
<#
.SYNOPSIS
Behaviour tests for the release validation scripts, against a fake Azure CLI.

.DESCRIPTION
Runs Invoke-ProductionSmoke.ps1 -WorkerOnly and Test-AzureDeploymentPlan.ps1
-Mode PreProvision in a child pwsh with a fake `az` and `azd` first on PATH.
The fakes record every argument list and answer from fixture files, so the
tests prove what the scripts refuse before any live read, what they accept,
and which exact target they bind to. No Azure call is made.

.EXAMPLE
pwsh ./scripts/Test-ReleaseValidation.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'PegasusPlatform.ps1')

$productionSmoke = Join-Path $PSScriptRoot 'Invoke-ProductionSmoke.ps1'
$deploymentPlan = Join-Path $PSScriptRoot 'Test-AzureDeploymentPlan.ps1'
$expectedSettingNames = @(Get-PegasusWorkerDisabledSettingNames)
$approvedSubscription = 'e6076573-23a5-46a8-acef-7e22d264e5db'
$root = Join-Path ([IO.Path]::GetTempPath()) "pegasus-release-validation-$([guid]::NewGuid().ToString('N'))"

function Assert-True {
    param(
        [Parameter(Mandatory)][bool] $Condition,
        [Parameter(Mandatory)][string] $Case,
        [Parameter(Mandatory)][string] $Failure,
        [string] $Diagnostic = ''
    )

    if (-not $Condition) {
        throw "$Case`: $Failure`n$Diagnostic"
    }
}

function ConvertTo-SingleLine {
    param([AllowEmptyString()][string] $Value)

    # pwsh wraps error text at the console width and prefixes continuation
    # lines with '|'; collapse both so a sentence can be matched whole.
    return [regex]::Replace([regex]::Replace($Value, '\s*\|\s*', ' '), '\s+', ' ')
}

function New-ExactSettings {
    param([Parameter(Mandatory)][string] $Value)

    $settings = [System.Collections.Generic.List[hashtable]]::new()
    foreach ($name in $expectedSettingNames) {
        $settings.Add(@{ name = $name; value = $Value })
    }
    # The comma keeps the list whole instead of unrolling it into an array.
    return , $settings
}

function New-QuotaDocument {
    param([Parameter(Mandatory)][object[]] $Rows)

    return @{
        value = @($Rows | ForEach-Object {
            @{ name = $_[0]; properties = @{ limit = @{ value = $_[1] }; name = @{ value = $_[0] }; unit = 'Instances' } }
        })
    } | ConvertTo-Json -Depth 6 -Compress
}

function New-CompiledWorkerTemplate {
    $resources = @($expectedSettingNames | ForEach-Object {
        @{ name = $_; value = "[if(variables('workerActivationApproved'), 'false', 'true')]" }
    })
    return @{
        resources = $resources
        variables = @{ workerActivationApproved = "[equals(parameters('workerActivation'), 'approved-live-worker')]" }
        parameters = @{ workerActivation = @{ type = 'string'; defaultValue = 'disabled' } }
    } | ConvertTo-Json -Depth 6 -Compress
}

function New-ValidPreProvisionEnvironment {
    return [ordered]@{
        AZURE_SUBSCRIPTION_ID = $approvedSubscription
        AZURE_TENANT_ID = '858cf5b3-aa0a-47a6-9b40-4851fd0afa94'
        AZURE_RESOURCE_GROUP = 'rg-pegasus-prod'
        WORKER_APP_NAME = 'pegasus-prod-worker-252ow37gij'
        PEGASUS_WORKER_ACTIVATION = 'disabled'
        BOX_HOLDING_FOLDER_ID = 'test-holding-folder'
        AZURE_KEY_VAULT_NAME = 'pegasusprodkv252ow37g'
        AUTOMATION_MCP_SIGNING_CERTIFICATE_SECRET_URIS =
            'https://pegasusprodkv252ow37g.vault.azure.net/secrets/signing-current/version-one,' +
            'https://pegasusprodkv252ow37g.vault.azure.net/secrets/signing-retained/version-two'
        AUTOMATION_MCP_ENCRYPTION_CERTIFICATE_SECRET_URIS =
            'https://pegasusprodkv252ow37g.vault.azure.net/secrets/encryption/version-three'
    }
}

function Write-FakeCli {
    <#
        The fake `az` appends its arguments to PEGASUS_TEST_AZ_ARGUMENTS_PATH,
        answers `bicep build` with the compiled-template fixture, a Quota read
        with the quota fixture, a PendingWorkRecoverySchedule query with the
        once-per-minute cron, and every other call with the settings fixture.
        The fake `azd` prints the environment fixture.
    #>
    param([Parameter(Mandatory)][string] $Directory)

    if ($IsWindows) {
        Set-Content -LiteralPath (Join-Path $Directory 'az.cmd') -Value @(
            '@echo off',
            '>> "%PEGASUS_TEST_AZ_ARGUMENTS_PATH%" echo %*',
            'echo %* | findstr /c:"bicep build" >nul && (type "%PEGASUS_TEST_AZ_COMPILED_TEMPLATE_PATH%" & exit /b 0)',
            'echo %* | findstr /c:"Microsoft.Quota" >nul && (type "%PEGASUS_TEST_AZ_QUOTA_PATH%" & exit /b 0)',
            'echo %* | findstr /c:"PendingWorkRecoverySchedule" >nul && (echo 0 * * * * * & exit /b 0)',
            'type "%PEGASUS_TEST_AZ_SETTINGS_PATH%"',
            'exit /b 0'
        ) -Encoding ascii
        Set-Content -LiteralPath (Join-Path $Directory 'azd.cmd') -Value @(
            '@echo off',
            'type "%PEGASUS_TEST_AZD_VALUES_PATH%"'
        ) -Encoding ascii
        return
    }

    $az = Join-Path $Directory 'az'
    Set-Content -LiteralPath $az -Value @(
        '#!/bin/sh',
        'printf ''%s\n'' "$*" >> "$PEGASUS_TEST_AZ_ARGUMENTS_PATH"',
        'case "$*" in',
        '  *"bicep build"*) cat "$PEGASUS_TEST_AZ_COMPILED_TEMPLATE_PATH";;',
        '  *Microsoft.Quota*) cat "$PEGASUS_TEST_AZ_QUOTA_PATH";;',
        '  *PendingWorkRecoverySchedule*) printf ''%s\n'' ''0 * * * * *'';;',
        '  *) cat "$PEGASUS_TEST_AZ_SETTINGS_PATH";;',
        'esac'
    ) -Encoding ascii
    [IO.File]::SetUnixFileMode($az, [IO.UnixFileMode]::UserRead -bor [IO.UnixFileMode]::UserWrite -bor [IO.UnixFileMode]::UserExecute)
    $azd = Join-Path $Directory 'azd'
    Set-Content -LiteralPath $azd -Value @('#!/bin/sh', 'cat "$PEGASUS_TEST_AZD_VALUES_PATH"') -Encoding ascii
    [IO.File]::SetUnixFileMode($azd, [IO.UnixFileMode]::UserRead -bor [IO.UnixFileMode]::UserWrite -bor [IO.UnixFileMode]::UserExecute)
}

$script:caseIndex = 0

function Invoke-Isolated {
    <#
        Runs a script in a child pwsh with the fake CLI first on PATH. Returns
        the exit code, the combined output on one line, and the recorded
        Azure CLI arguments.
    #>
    param(
        [Parameter(Mandatory)][string] $Script,
        [Parameter(Mandatory)][string[]] $Arguments,
        [Parameter(Mandatory)][object[]] $Settings,
        [System.Collections.IDictionary] $AzdEnvironment,
        [string] $QuotaDocument
    )

    $script:caseIndex++
    $caseRoot = Join-Path $root ("case-{0:D3}" -f $script:caseIndex)
    New-Item -ItemType Directory -Path $caseRoot | Out-Null
    Write-FakeCli -Directory $caseRoot

    $argumentsPath = Join-Path $caseRoot 'azure-arguments.txt'
    $settingsPath = Join-Path $caseRoot 'worker-settings.json'
    Set-Content -LiteralPath $settingsPath -Value (ConvertTo-Json @($Settings) -Compress -Depth 3) -Encoding ascii
    $environmentPath = Join-Path $caseRoot 'environment.txt'
    $lines = @()
    if ($null -ne $AzdEnvironment) {
        $lines = @($AzdEnvironment.Keys | ForEach-Object { "$_=$($AzdEnvironment[$_])" })
    }
    Set-Content -LiteralPath $environmentPath -Value $lines -Encoding ascii
    $templatePath = Join-Path $caseRoot 'compiled-template.json'
    Set-Content -LiteralPath $templatePath -Value (New-CompiledWorkerTemplate) -Encoding ascii
    $quotaPath = Join-Path $caseRoot 'app-service-quota.json'
    if ([string]::IsNullOrEmpty($QuotaDocument)) {
        $QuotaDocument = New-QuotaDocument -Rows @(@('B1', 1), @('*', 1))
    }
    Set-Content -LiteralPath $quotaPath -Value $QuotaDocument -Encoding ascii

    $savedPath = $env:PATH
    $env:PATH = $caseRoot + [IO.Path]::PathSeparator + $savedPath
    $env:PEGASUS_TEST_AZ_ARGUMENTS_PATH = $argumentsPath
    $env:PEGASUS_TEST_AZ_SETTINGS_PATH = $settingsPath
    $env:PEGASUS_TEST_AZD_VALUES_PATH = $environmentPath
    $env:PEGASUS_TEST_AZ_COMPILED_TEMPLATE_PATH = $templatePath
    $env:PEGASUS_TEST_AZ_QUOTA_PATH = $quotaPath
    try {
        $output = @(& pwsh -NoLogo -NoProfile -File $Script @Arguments 2>&1 | ForEach-Object { "$_" })
        $exitCode = $LASTEXITCODE
    }
    finally {
        $env:PATH = $savedPath
        foreach ($name in 'PEGASUS_TEST_AZ_ARGUMENTS_PATH', 'PEGASUS_TEST_AZ_SETTINGS_PATH', 'PEGASUS_TEST_AZD_VALUES_PATH',
            'PEGASUS_TEST_AZ_COMPILED_TEMPLATE_PATH', 'PEGASUS_TEST_AZ_QUOTA_PATH') {
            Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue
        }
    }

    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = ConvertTo-SingleLine ($output -join "`n")
        AzureArguments = if (Test-Path -LiteralPath $argumentsPath) { Get-Content -Raw -LiteralPath $argumentsPath } else { '' }
    }
}

function Invoke-WorkerSmoke {
    param(
        [Parameter(Mandatory)][object[]] $Settings,
        [Parameter(Mandatory)][string] $ExpectedActivation,
        [string] $SubscriptionId = $approvedSubscription
    )

    return Invoke-Isolated -Script $productionSmoke -Settings $Settings -Arguments @(
        '-WorkerOnly',
        '-SubscriptionId', $SubscriptionId,
        '-ResourceGroupName', 'rg-pegasus-prod',
        '-ExpectedWorkerActivation', $ExpectedActivation)
}

function Invoke-PreProvision {
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Environment,
        [string] $QuotaDocument
    )

    return Invoke-Isolated -Script $deploymentPlan -Settings (New-ExactSettings 'true') `
        -AzdEnvironment $Environment -QuotaDocument $QuotaDocument -Arguments @(
            '-Mode', 'PreProvision',
            '-Environment', 'test',
            '-WorkerActivation', 'disabled',
            '-ExpectedLiveWorkerActivation', 'disabled')
}

function Assert-CensusRejected {
    param(
        [Parameter(Mandatory)][string] $Case,
        [Parameter(Mandatory)][object[]] $Settings,
        [Parameter(Mandatory)][string] $ProtectedSettingName
    )

    $result = Invoke-WorkerSmoke -Settings $Settings -ExpectedActivation 'disabled'
    Assert-True ($result.ExitCode -ne 0) $Case 'should fail.' $result.Output
    Assert-True ($result.Output.Contains('census differs from the exact seven-function release contract')) $Case 'did not report the census.' $result.Output
    # The rejection names no live setting: the diagnostic must not echo what
    # the production Worker carries.
    Assert-True (-not $result.Output.Contains($ProtectedSettingName)) $Case 'echoed a live setting name.' $result.Output
}

New-Item -ItemType Directory -Path $root | Out-Null
try {
    # --- Invoke-ProductionSmoke.ps1 -WorkerOnly -----------------------------

    $result = Invoke-WorkerSmoke -Settings (New-ExactSettings 'true') -ExpectedActivation 'disabled'
    $case = 'Exact disabled census'
    Assert-True ($result.ExitCode -eq 0) $case 'should pass.' $result.Output
    Assert-True ($result.Output.Contains('Production Worker activation smoke passed (disabled).')) $case 'did not report success.' $result.Output
    foreach ($binding in "--subscription $approvedSubscription", '--name pegasus-prod-worker-252ow37gij', '--resource-group rg-pegasus-prod') {
        Assert-True ($result.AzureArguments.Contains($binding)) $case "did not bind $binding." $result.AzureArguments
    }

    $result = Invoke-WorkerSmoke -Settings (New-ExactSettings 'false') -ExpectedActivation 'approved-live-worker'
    $case = 'Exact approved census'
    Assert-True ($result.ExitCode -eq 0) $case 'should pass.' $result.Output
    Assert-True ($result.Output.Contains('Production Worker activation smoke passed (approved-live-worker).')) $case 'did not report success.' $result.Output

    $result = Invoke-WorkerSmoke -Settings (New-ExactSettings 'true') -ExpectedActivation 'disabled' -SubscriptionId '00000000-0000-0000-0000-000000000000'
    $case = 'Unapproved subscription'
    Assert-True ($result.ExitCode -ne 0) $case 'should fail.' $result.Output
    Assert-True ($result.Output.Contains('does not belong to the set')) $case 'was not refused by parameter validation.' $result.Output
    Assert-True ([string]::IsNullOrEmpty($result.AzureArguments)) $case 'reached Azure before refusing.' $result.AzureArguments

    $settings = New-ExactSettings 'true'
    $settings.Add(@{ name = 'AzureWebJobs.UnexpectedFunction.Disabled'; value = 'true' })
    Assert-CensusRejected -Case 'Extra disabled setting' -Settings $settings -ProtectedSettingName 'AzureWebJobs.UnexpectedFunction.Disabled'

    $settings = New-ExactSettings 'true'
    $settings.Add(@{ name = 'AzureWebJobs.Extra-Function.Disabled'; value = 'true' })
    Assert-CensusRejected -Case 'Malformed disabled setting' -Settings $settings -ProtectedSettingName 'AzureWebJobs.Extra-Function.Disabled'

    $settings = New-ExactSettings 'true'
    $settings.RemoveAll({ param($s) $s.name -eq 'AzureWebJobs.InboxRecoveryFunction.Disabled' }) | Out-Null
    $settings.Add(@{ name = 'AzureWebJobs.inboxpollfunction.Disabled'; value = 'true' })
    Assert-CensusRejected -Case 'Case-variant disabled setting' -Settings $settings -ProtectedSettingName 'AzureWebJobs.inboxpollfunction.Disabled'

    $settings = New-ExactSettings 'true'
    $settings.RemoveAll({ param($s) $s.name -eq 'AzureWebJobs.InboxRecoveryFunction.Disabled' }) | Out-Null
    Assert-CensusRejected -Case 'Missing disabled setting' -Settings $settings -ProtectedSettingName 'AzureWebJobs.InboxRecoveryFunction.Disabled'

    $settings = New-ExactSettings 'true'
    $settings.Add(@{ name = 'AzureWebJobs.InboxRecoveryFunction.Disabled'; value = 'true' })
    Assert-CensusRejected -Case 'Duplicate disabled setting' -Settings $settings -ProtectedSettingName 'AzureWebJobs.InboxRecoveryFunction.Disabled'

    $settings = New-ExactSettings 'true'
    ($settings | Where-Object { $_.name -eq 'AzureWebJobs.InboxRecoveryFunction.Disabled' }).value = 'false'
    $result = Invoke-WorkerSmoke -Settings $settings -ExpectedActivation 'disabled'
    $case = 'Mixed disabled values'
    Assert-True ($result.ExitCode -ne 0) $case 'should fail.' $result.Output
    Assert-True ($result.Output.Contains("do not match the intended 'disabled' activation value")) $case 'did not report the value mismatch.' $result.Output
    Assert-True (-not $result.Output.Contains('InboxRecoveryFunction') -and -not $result.Output.Contains('false')) $case 'echoed a live setting.' $result.Output

    # --- Test-AzureDeploymentPlan.ps1 -Mode PreProvision ---------------------

    foreach ($key in 'BOX_HOLDING_FOLDER_ID', 'AUTOMATION_MCP_SIGNING_CERTIFICATE_SECRET_URIS', 'AUTOMATION_MCP_ENCRYPTION_CERTIFICATE_SECRET_URIS') {
        foreach ($value in @($null, '', '   ')) {
            $environment = New-ValidPreProvisionEnvironment
            if ($null -eq $value) { $environment.Remove($key) } else { $environment[$key] = $value }
            $result = Invoke-PreProvision -Environment $environment
            $case = "Missing or empty $key ('$value')"
            Assert-True ($result.ExitCode -ne 0) $case 'should fail.' $result.Output
            Assert-True ($result.Output.Contains("missing $key")) $case 'did not name the missing key.' $result.Output
            Assert-True (-not $result.AzureArguments.Contains('functionapp config appsettings list')) $case 'reached the Worker smoke.' $result.AzureArguments
        }
    }

    foreach ($value in @(
        'http://pegasusprodkv252ow37g.vault.azure.net/secrets/signing/version',
        'https://operator@pegasusprodkv252ow37g.vault.azure.net/secrets/signing/version',
        'https://pegasusprodkv252ow37g.vault.azure.net:444/secrets/signing/version',
        'https://pegasusprodkv252ow37g.vault.azure.net/secrets/signing/version?query=value',
        'https://pegasusprodkv252ow37g.vault.azure.net/secrets/signing/version#fragment',
        'https://pegasusprodkv252ow37g.vault.azure.net/secrets/signing')) {
        $environment = New-ValidPreProvisionEnvironment
        $environment['AUTOMATION_MCP_SIGNING_CERTIFICATE_SECRET_URIS'] = $value
        $result = Invoke-PreProvision -Environment $environment
        $case = "Malformed certificate URI $value"
        Assert-True ($result.ExitCode -ne 0) $case 'should fail.' $result.Output
        Assert-True ($result.Output.Contains('AUTOMATION_MCP_SIGNING_CERTIFICATE_SECRET_URIS must contain')) $case 'did not name the rule.' $result.Output
        Assert-True (-not $result.AzureArguments.Contains('functionapp config appsettings list')) $case 'reached the Worker smoke.' $result.AzureArguments
    }

    $environment = New-ValidPreProvisionEnvironment
    $environment['AUTOMATION_MCP_ENCRYPTION_CERTIFICATE_SECRET_URIS'] = 'https://another-vault.vault.azure.net/secrets/encryption/version'
    $result = Invoke-PreProvision -Environment $environment
    $case = 'Cross-vault certificates'
    Assert-True ($result.ExitCode -ne 0) $case 'should fail.' $result.Output
    Assert-True ($result.Output.Contains('same Azure Key Vault')) $case 'did not name the rule.' $result.Output
    Assert-True (-not $result.AzureArguments.Contains('functionapp config appsettings list')) $case 'reached the Worker smoke.' $result.AzureArguments

    $result = Invoke-PreProvision -Environment (New-ValidPreProvisionEnvironment)
    $case = 'Valid pre-provision environment'
    Assert-True ($result.ExitCode -eq 0) $case 'should pass.' $result.Output
    Assert-True ($result.Output.Contains('App Service quota pre-flight passed: B1 in uksouth has limit 1')) $case 'did not pass the quota pre-flight.' $result.Output
    Assert-True ($result.Output.Contains('Azure deployment plan validation passed (PreProvision')) $case 'did not report success.' $result.Output
    Assert-True ($result.AzureArguments.Contains('Microsoft.Web/locations/uksouth/providers/Microsoft.Quota/quotas')) $case 'did not read the platform-region quota.' $result.AzureArguments
    Assert-True ($result.AzureArguments.Contains('functionapp config appsettings list')) $case 'did not reach the Worker smoke.' $result.AzureArguments

    $environment = New-ValidPreProvisionEnvironment
    $environment['PEGASUS_WEB_LOCATION'] = 'ukwest'
    $result = Invoke-PreProvision -Environment $environment -QuotaDocument (New-QuotaDocument -Rows @(, @('*', 30)))
    $case = 'Chosen Web region quota'
    Assert-True ($result.ExitCode -eq 0) $case 'should pass.' $result.Output
    Assert-True ($result.Output.Contains('App Service quota pre-flight passed: B1 in ukwest has limit 30 (* row)')) $case 'did not use the aggregate row.' $result.Output
    Assert-True ($result.AzureArguments.Contains('Microsoft.Web/locations/ukwest/providers/Microsoft.Quota/quotas')) $case 'did not read the chosen region.' $result.AzureArguments

    # ADR-0049: the 2026-09-13 read of the platform region showed every SKU at
    # 0; a B1 row at 0 must stop provision even when v4 rows have quota, and the
    # aggregate row must not rescue a present SKU row.
    $result = Invoke-PreProvision -Environment (New-ValidPreProvisionEnvironment) -QuotaDocument (New-QuotaDocument -Rows @(@('B1', 0), @('P0v4', 30), @('*', 0)))
    $case = 'Zero App Service quota'
    Assert-True ($result.ExitCode -ne 0) $case 'should fail.' $result.Output
    Assert-True ($result.Output.Contains('App Service B1 quota in uksouth is 0 (B1 row)')) $case 'did not name the zero row.' $result.Output
    Assert-True ($result.Output.Contains('P0v4=30')) $case 'did not list the rows with quota.' $result.Output
    Assert-True (-not $result.AzureArguments.Contains('functionapp config appsettings list')) $case 'reached the Worker smoke.' $result.AzureArguments

    Write-Output "Release validation behaviour tests passed ($script:caseIndex isolated runs)."
}
finally {
    if (Test-Path -LiteralPath $root) {
        $resolvedRoot = [IO.Path]::GetFullPath($root)
        $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/')
        if ((Split-Path $resolvedRoot -Parent) -ne $temporaryRoot -or
            (Split-Path $resolvedRoot -Leaf) -notmatch '^pegasus-release-validation-[0-9a-f]{32}$') {
            throw "Refusing to remove unexpected directory '$resolvedRoot'."
        }
        Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
    }
}
