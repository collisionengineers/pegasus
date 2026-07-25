[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Create', 'Attach', 'Resume', 'WriteArtifact', 'WriteHandoff', 'Complete', 'Validate')][string]$Operation,
    [Parameter(Mandatory)][string]$TaskRoot,
    [string]$TaskId,
    [string]$HandoffPath,
    [string]$Request,
    [ValidateSet('planning', 'implementation', 'review', 'validation', 'debugging', 'documentation', 'ui-ux')][string]$Area,
    [string]$RelativePath,
    [string]$Owner,
    [string]$Content,
    [string[]]$ArtifactReferences = @()
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Repoplugin.Task.psm1') -Force -DisableNameChecking
switch ($Operation) {
    'Create' { if ([string]::IsNullOrWhiteSpace($Request)) { throw 'Create requires Request.' }; $result = New-RepopluginTask -TaskRoot $TaskRoot -TaskId $TaskId -Request $Request }
    'Attach' { $result = Resolve-RepopluginTask -Mode Attach -TaskRoot $TaskRoot -TaskId $TaskId -HandoffPath $HandoffPath }
    'Resume' { $result = Resolve-RepopluginTask -Mode Resume -TaskRoot $TaskRoot -TaskId $TaskId -HandoffPath $HandoffPath }
    'WriteArtifact' { if ([string]::IsNullOrWhiteSpace($Area) -or [string]::IsNullOrWhiteSpace($RelativePath) -or [string]::IsNullOrWhiteSpace($Owner) -or $null -eq $Content) { throw 'WriteArtifact requires TaskId, Area, RelativePath, Owner, and Content.' }; $result = Write-RepopluginArtifact -TaskRoot $TaskRoot -TaskId $TaskId -Area $Area -RelativePath $RelativePath -Owner $Owner -Content $Content }
    'WriteHandoff' { if ([string]::IsNullOrWhiteSpace($Area) -or [string]::IsNullOrWhiteSpace($RelativePath)) { throw 'WriteHandoff requires TaskId, Area, and RelativePath.' }; $result = Write-RepopluginHandoff -TaskRoot $TaskRoot -TaskId $TaskId -Area $Area -RelativePath $RelativePath -ArtifactReferences $ArtifactReferences }
    'Complete' { $result = Complete-RepopluginTask -TaskRoot $TaskRoot -TaskId $TaskId }
    'Validate' { $result = Test-RepopluginTask -TaskRoot $TaskRoot -TaskId $TaskId }
}
$result | ConvertTo-Json -Depth 6
