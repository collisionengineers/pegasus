[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$validator = Join-Path $PSScriptRoot 'Test-MarkdownPlacement.ps1'
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) "pegasus-markdown-placement-$([guid]::NewGuid().ToString('N'))"
$savedGitEnvironment = @{}
$gitEnvironmentNames = @(Get-ChildItem Env:GIT_* | Select-Object -ExpandProperty Name)
foreach ($name in $gitEnvironmentNames) {
    $savedGitEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    Remove-Item -LiteralPath "Env:$name"
}

function Assert-TemporaryRepositoryRoot {
    $resolvedRoot = (Resolve-Path -LiteralPath $testRoot).Path
    $expectedRoot = [System.IO.Path]::GetFullPath($testRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
    if (-not [string]::Equals($resolvedRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar), $expectedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Markdown fixture root resolved outside its temporary directory: '$resolvedRoot'."
    }

    $gitRoot = (& git -C $testRoot rev-parse --show-toplevel).Trim()
    if ($LASTEXITCODE -ne 0 -or -not [string]::Equals(
        [System.IO.Path]::GetFullPath($gitRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar),
        $expectedRoot,
        [StringComparison]::OrdinalIgnoreCase)) {
        throw "Markdown fixture Git root is not its temporary directory: '$gitRoot'."
    }
}

function Invoke-Git {
    param([Parameter(Mandatory)][string[]] $Arguments)

    if ($Arguments[0] -ne 'init') {
        Assert-TemporaryRepositoryRoot
    }
    & git -C $testRoot @Arguments | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Test setup Git command failed: git $($Arguments -join ' ')"
    }
}

function Add-TestCommit {
    param([Parameter(Mandatory)][string] $Message)

    Invoke-Git -Arguments @('add', '--all')
    Invoke-Git -Arguments @('commit', '-m', $Message)
    return (& git -C $testRoot rev-parse HEAD).Trim()
}

function Assert-Passes {
    param(
        [Parameter(Mandatory)][string] $Case,
        [Parameter(Mandatory)][string] $Base,
        [Parameter(Mandatory)][string] $Head
    )

    try {
        & $validator -Base $Base -Head $Head -RepositoryRoot $testRoot | Out-Null
    }
    catch {
        throw "$Case should pass, but failed: $($_.Exception.Message)"
    }
}

function Assert-Fails {
    param(
        [Parameter(Mandatory)][string] $Case,
        [Parameter(Mandatory)][string] $Base,
        [Parameter(Mandatory)][string] $Head,
        [Parameter(Mandatory)][string[]] $ExpectedText
    )

    try {
        & $validator -Base $Base -Head $Head -RepositoryRoot $testRoot | Out-Null
        throw "$Case should fail, but passed."
    }
    catch {
        if ($_.Exception.Message -eq "$Case should fail, but passed.") {
            throw
        }
        foreach ($text in $ExpectedText) {
            if ($_.Exception.Message -notlike "*$text*") {
                throw "$Case did not report '$text'. Actual: $($_.Exception.Message)"
            }
        }
    }
}

New-Item -ItemType Directory -Path $testRoot | Out-Null
try {
    Invoke-Git -Arguments @('init', '--initial-branch=main')
    Assert-TemporaryRepositoryRoot
    Invoke-Git -Arguments @('config', 'user.email', 'markdown-placement@example.invalid')
    Invoke-Git -Arguments @('config', 'user.name', 'Markdown Placement Test')

    Set-Content -LiteralPath (Join-Path $testRoot 'README.md') -Value '# Existing root document'
    $initial = Add-TestCommit -Message 'initial grandfathered content'

    $allowed = @(
        'docs/prd/new.md',
        'docs/frd/new.md',
        'docs/adr/new.md',
        'docs/design/new.md',
        '.design-sync/new.md',
        '.grok/skills/example/SKILL.md',
        '.stitch/DESIGN.md',
        'design/planning-and-old-designs/new.md',
        'workspaces/document-extraction/docs/new.md'
    )
    foreach ($path in $allowed) {
        $fullPath = Join-Path $testRoot $path
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $fullPath) | Out-Null
        Set-Content -LiteralPath $fullPath -Value "# $path"
    }
    $allowedHead = Add-TestCommit -Message 'add allowed markdown'
    Assert-Passes -Case 'Canonical and registered workspace additions' -Base $initial -Head $allowedHead

    $forbidden = @('new-root.md', '.agents/new.md', 'reference/new.md', 'docs/temp-plans/new.md')
    foreach ($path in $forbidden) {
        $fullPath = Join-Path $testRoot $path
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $fullPath) | Out-Null
        Set-Content -LiteralPath $fullPath -Value "# $path"
    }
    $forbiddenHead = Add-TestCommit -Message 'add forbidden markdown'
    Assert-Fails -Case 'Forbidden additions aggregate' -Base $allowedHead -Head $forbiddenHead -ExpectedText $forbidden

    Set-Content -LiteralPath (Join-Path $testRoot 'README.md') -Value '# Modified grandfathered root document'
    Remove-Item -LiteralPath (Join-Path $testRoot 'new-root.md')
    $changedHead = Add-TestCommit -Message 'modify and delete grandfathered markdown'
    Assert-Passes -Case 'Modifications and deletions are grandfathered' -Base $forbiddenHead -Head $changedHead

    Invoke-Git -Arguments @('mv', 'README.md', 'renamed-root.md')
    $renameHead = Add-TestCommit -Message 'rename into forbidden destination'
    Assert-Fails -Case 'Rename destination' -Base $changedHead -Head $renameHead -ExpectedText @('renamed-root.md')

    Copy-Item -LiteralPath (Join-Path $testRoot 'docs/prd/new.md') -Destination (Join-Path $testRoot 'reference/copied.md')
    $copyHead = Add-TestCommit -Message 'copy into forbidden destination'
    Assert-Fails -Case 'Copy destination' -Base $renameHead -Head $copyHead -ExpectedText @('reference/copied.md')

    Assert-Fails -Case 'All-zero base' -Base ('0' * 40) -Head $copyHead -ExpectedText @('all zeroes')
    Assert-Fails -Case 'Unavailable base' -Base 'not-a-commit' -Head $copyHead -ExpectedText @('not an available commit')

    Write-Output 'Markdown placement regression tests passed.'
    $global:LASTEXITCODE = 0
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Assert-TemporaryRepositoryRoot
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
    foreach ($name in $gitEnvironmentNames) {
        [Environment]::SetEnvironmentVariable($name, $savedGitEnvironment[$name], 'Process')
    }
}
