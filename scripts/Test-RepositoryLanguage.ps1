[CmdletBinding()]
param(
    [string]$ApprovalPath,
    [switch]$RequireQdosAlphaActivation
)

$ErrorActionPreference = 'Stop'

& (Join-Path $PSScriptRoot 'Test-RepositoryPolicy.ps1') @PSBoundParameters
