[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Repoplugin.Task.psm1') -Force -DisableNameChecking

$assertions = 0
function Assert-True { param([bool]$Condition, [string]$Message) $script:assertions++; if (-not $Condition) { throw $Message } }
function Assert-Throws { param([scriptblock]$Action, [string]$Message) $script:assertions++; try { & $Action } catch { return }; throw $Message }

$wrapper = Join-Path $PSScriptRoot 'Invoke-RepopluginTaskOperation.ps1'
function Invoke-Wrapper {
    param([Parameter(Mandatory)][string[]]$WrapperArguments)
    $output = & pwsh -NoProfile -File $wrapper @WrapperArguments
    if ($LASTEXITCODE -ne 0) { throw "Wrapper failed with exit code $LASTEXITCODE." }
    return ($output -join "`n") | ConvertFrom-Json
}

$root = Join-Path ([IO.Path]::GetTempPath()) ('repoplugin-task-contracts-' + [Guid]::NewGuid().ToString('N'))
$outside = Join-Path ([IO.Path]::GetTempPath()) ('repoplugin-task-contracts-outside-' + [Guid]::NewGuid().ToString('N'))
$rootJunction = Join-Path ([IO.Path]::GetTempPath()) ('repoplugin-task-root-junction-' + [Guid]::NewGuid().ToString('N'))
$rootJunctionTarget = Join-Path ([IO.Path]::GetTempPath()) ('repoplugin-task-root-target-' + [Guid]::NewGuid().ToString('N'))
try {
    [IO.Directory]::CreateDirectory($rootJunctionTarget) | Out-Null
    New-Item -ItemType Junction -Path $rootJunction -Target $rootJunctionTarget | Out-Null
    Assert-Throws { New-RepopluginTask -TaskRoot $rootJunction -TaskId 'junction-root-task' -Request 'must not write' } 'Create wrote through a junction task root.'
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $rootJunctionTarget 'junction-root-task'))) 'Create left task data outside a junction task root.'
    Remove-Item -LiteralPath $rootJunction -Force

    $generated = New-RepopluginTask -TaskRoot $root -Request 'Generate a valid task identifier.'
    Assert-True ($generated.TaskId -match '^[a-z0-9][a-z0-9-]{2,79}$') 'Create generated an invalid task id.'
    $created = New-RepopluginTask -TaskRoot $root -TaskId 'thin-contract-test' -Request 'Keep the task contract small.'
    Assert-True ($created.TaskId -eq 'thin-contract-test') 'Create returned the wrong task id.'
    Assert-True (Test-Path -LiteralPath (Join-Path $created.TaskPath 'task.md')) 'Create did not write task.md.'
    Assert-True ((Get-Content -LiteralPath (Join-Path $created.TaskPath 'state.json') -Raw | ConvertFrom-Json).status -eq 'active') 'Create did not write active state.'
    $areaCount = @(Get-ChildItem -LiteralPath $created.TaskPath -Directory | Where-Object { $_.Name -in @('planning', 'implementation', 'review', 'validation', 'debugging', 'documentation', 'ui-ux') }).Count
    Assert-True ($areaCount -eq 7) 'Create did not make all fixed task areas.'
    Assert-Throws { New-RepopluginTask -TaskRoot $root -TaskId 'thin-contract-test' -Request 'duplicate' } 'Create overwrote an existing task.'
    $attached = Resolve-RepopluginTask -Mode Attach -TaskRoot $root -TaskId 'thin-contract-test'
    Assert-True ($attached.TaskPath -eq $created.TaskPath) 'Attach did not select the explicit task.'
    Assert-Throws { Resolve-RepopluginTask -Mode Attach -TaskRoot $root } 'Attach chose an implicit latest task.'
    $artifact = Write-RepopluginArtifact -TaskRoot $root -TaskId 'thin-contract-test' -Area planning -RelativePath 'research.md' -Owner 'planner' -Content '# Research'
    Assert-True (Test-Path -LiteralPath $artifact.path) 'Artifact was not written.'
    Assert-True ((Get-Content -LiteralPath $artifact.path -Raw) -match 'task_id: thin-contract-test') 'Artifact lacks task identity frontmatter.'
    Assert-Throws { Write-RepopluginArtifact -TaskRoot $root -TaskId 'thin-contract-test' -Area planning -RelativePath '../escape.md' -Owner 'planner' -Content 'bad' } 'Artifact traversal was accepted.'
    Assert-Throws { Write-RepopluginArtifact -TaskRoot $root -TaskId 'thin-contract-test' -Area planning -RelativePath 'C:\\escape.md' -Owner 'planner' -Content 'bad' } 'Absolute artifact path was accepted.'
    $handoff = Write-RepopluginHandoff -TaskRoot $root -TaskId 'thin-contract-test' -Area planning -RelativePath 'handoff.json' -ArtifactReferences @('research.md')
    $resumed = Resolve-RepopluginTask -Mode Resume -TaskRoot $root -HandoffPath $handoff.path
    Assert-True ($resumed.TaskId -eq 'thin-contract-test') 'Resume did not use the handoff task id.'
    Assert-True ((Get-Content -LiteralPath $handoff.path -Raw | ConvertFrom-Json).artifact_references.Count -eq 1) 'Handoff did not preserve artifact references.'
    $statePath = Join-Path $created.TaskPath 'state.json'
    $originalState = Get-Content -LiteralPath $statePath -Raw
    $changedState = $originalState | ConvertFrom-Json
    $changedState.task_id = 'different-task'
    [IO.File]::WriteAllText($statePath, (($changedState | ConvertTo-Json) + "`n"), [Text.UTF8Encoding]::new($false))
    Assert-Throws { Write-RepopluginArtifact -TaskRoot $root -TaskId 'thin-contract-test' -Area planning -RelativePath 'identity-escape.md' -Owner 'planner' -Content 'bad' } 'Artifact write accepted mismatched task state identity.'
    [IO.File]::WriteAllText($statePath, $originalState, [Text.UTF8Encoding]::new($false))
    $invalidState = $originalState | ConvertFrom-Json
    $invalidState.status = 'active'
    $invalidState.completed_at = (Get-Date).ToUniversalTime().ToString('o')
    [IO.File]::WriteAllText($statePath, (($invalidState | ConvertTo-Json) + "`n"), [Text.UTF8Encoding]::new($false))
    Assert-Throws { Resolve-RepopluginTask -Mode Attach -TaskRoot $root -TaskId 'thin-contract-test' } 'Attach accepted active state with completed_at.'
    $invalidState.status = 'completed'
    $invalidState.completed_at = $null
    [IO.File]::WriteAllText($statePath, (($invalidState | ConvertTo-Json) + "`n"), [Text.UTF8Encoding]::new($false))
    Assert-Throws { Resolve-RepopluginTask -Mode Attach -TaskRoot $root -TaskId 'thin-contract-test' } 'Attach accepted completed state without completed_at.'
    $invalidState.status = 'mystery'
    [IO.File]::WriteAllText($statePath, (($invalidState | ConvertTo-Json) + "`n"), [Text.UTF8Encoding]::new($false))
    Assert-Throws { Resolve-RepopluginTask -Mode Attach -TaskRoot $root -TaskId 'thin-contract-test' } 'Attach accepted an unknown task status.'
    [IO.File]::WriteAllText($statePath, $originalState, [Text.UTF8Encoding]::new($false))
    $taskDocumentPath = Join-Path $created.TaskPath 'task.md'
    $originalTaskDocument = Get-Content -LiteralPath $taskDocumentPath -Raw
    $tamperedTaskDocument = $originalTaskDocument -replace '(?m)^task_id: thin-contract-test$', 'task_id: different-task'
    $tamperedTaskDocument += "`n`ntask_id: thin-contract-test`n"
    [IO.File]::WriteAllText($taskDocumentPath, $tamperedTaskDocument, [Text.UTF8Encoding]::new($false))
    Assert-Throws { Resolve-RepopluginTask -Mode Attach -TaskRoot $root -TaskId 'thin-contract-test' } 'Attach accepted task identity from request-body text.'
    [IO.File]::WriteAllText($taskDocumentPath, $originalTaskDocument, [Text.UTF8Encoding]::new($false))
    [IO.Directory]::CreateDirectory($outside) | Out-Null
    $junctionPath = Join-Path $created.TaskPath 'planning\escape'
    New-Item -ItemType Junction -Path $junctionPath -Target $outside | Out-Null
    Assert-Throws { Write-RepopluginArtifact -TaskRoot $root -TaskId 'thin-contract-test' -Area planning -RelativePath 'escape\proof.md' -Owner 'planner' -Content 'bad' } 'Artifact write followed a junction outside the task area.'
    Remove-Item -LiteralPath $junctionPath -Force
    $completed = Complete-RepopluginTask -TaskRoot $root -TaskId 'thin-contract-test'
    Assert-True ($completed.State.status -eq 'completed') 'Complete did not update state.'
    Assert-True (-not [string]::IsNullOrWhiteSpace($completed.State.completed_at)) 'Complete did not set completed_at.'
    $valid = Test-RepopluginTask -TaskRoot $root -TaskId 'thin-contract-test'
    Assert-True $valid.valid 'Validate did not return valid.'

    $wrapperCreated = Invoke-Wrapper @('-Operation', 'Create', '-TaskRoot', $root, '-TaskId', 'wrapper-contract-test', '-Request', 'Exercise the supported wrapper.')
    Assert-True ($wrapperCreated.TaskId -eq 'wrapper-contract-test') 'Wrapper Create returned the wrong task id.'
    $wrapperArtifact = Invoke-Wrapper @('-Operation', 'WriteArtifact', '-TaskRoot', $root, '-TaskId', 'wrapper-contract-test', '-Area', 'planning', '-RelativePath', 'wrapper.md', '-Owner', 'tester', '-Content', '# Wrapper evidence')
    Assert-True (Test-Path -LiteralPath $wrapperArtifact.path) 'Wrapper WriteArtifact did not write its file.'
    $wrapperHandoff = Invoke-Wrapper @('-Operation', 'WriteHandoff', '-TaskRoot', $root, '-TaskId', 'wrapper-contract-test', '-Area', 'planning', '-RelativePath', 'wrapper-handoff.json', '-ArtifactReferences', 'wrapper.md')
    $wrapperResumed = Invoke-Wrapper @('-Operation', 'Resume', '-TaskRoot', $root, '-HandoffPath', $wrapperHandoff.path)
    Assert-True ($wrapperResumed.TaskId -eq 'wrapper-contract-test') 'Wrapper Resume did not use the handoff task id.'
    $wrapperCompleted = Invoke-Wrapper @('-Operation', 'Complete', '-TaskRoot', $root, '-TaskId', 'wrapper-contract-test')
    Assert-True ($wrapperCompleted.State.status -eq 'completed') 'Wrapper Complete did not set completed state.'
    $wrapperValidated = Invoke-Wrapper @('-Operation', 'Validate', '-TaskRoot', $root, '-TaskId', 'wrapper-contract-test')
    Assert-True $wrapperValidated.valid 'Wrapper Validate did not report a valid task.'
    Write-Output "Passed $assertions assertions."
} finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
    if (Test-Path -LiteralPath $outside) { Remove-Item -LiteralPath $outside -Recurse -Force }
    if (Test-Path -LiteralPath $rootJunction) { Remove-Item -LiteralPath $rootJunction -Force }
    if (Test-Path -LiteralPath $rootJunctionTarget) { Remove-Item -LiteralPath $rootJunctionTarget -Recurse -Force }
}
