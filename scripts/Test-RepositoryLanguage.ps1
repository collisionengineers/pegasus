[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Test-RepositoryPolicy.ps1')
exit $LASTEXITCODE
