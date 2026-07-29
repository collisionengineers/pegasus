[CmdletBinding()]
param(
    [string]$ApprovalPath,
    [switch]$RequireQdosAlphaActivation
)

$ErrorActionPreference = 'Stop'

& (Join-Path $PSScriptRoot 'Invoke-Doctor.ps1') @PSBoundParameters
