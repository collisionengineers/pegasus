Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:Areas = @('planning', 'implementation', 'review', 'validation', 'debugging', 'documentation', 'ui-ux')

function Get-RepopluginTaskRoot {
    param([Parameter(Mandatory)][string]$TaskRoot)
    $root = [IO.Path]::GetFullPath($TaskRoot)
    Assert-RepopluginNoReparseAncestor -Path $root
    return $root
}

function Assert-RepopluginNoReparseAncestor {
    param([Parameter(Mandatory)][string]$Path)
    $ancestors = [Collections.Generic.List[string]]::new()
    $current = [IO.DirectoryInfo]::new([IO.Path]::GetFullPath($Path))
    while ($null -ne $current) {
        $ancestors.Add($current.FullName)
        $current = $current.Parent
    }
    for ($index = $ancestors.Count - 1; $index -ge 0; $index--) {
        $ancestor = $ancestors[$index]
        if (Test-Path -LiteralPath $ancestor) {
            $attributes = [IO.File]::GetAttributes($ancestor)
            if (($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Reparse points are not allowed in task-root ancestors: $ancestor" }
        }
    }
}

function Assert-RepopluginNoReparsePoint {
    param([Parameter(Mandatory)][string]$RootPath, [Parameter(Mandatory)][string]$CandidatePath)
    $root = [IO.Path]::GetFullPath($RootPath)
    $candidate = [IO.Path]::GetFullPath($CandidatePath)
    $prefix = $root.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if ($candidate -ne $root -and -not $candidate.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Candidate path escapes its containment root.' }
    $current = $root
    $paths = [Collections.Generic.List[string]]::new()
    $paths.Add($current)
    $relative = [IO.Path]::GetRelativePath($root, $candidate)
    if ($relative -ne '.') {
        foreach ($segment in $relative -split '[\\/]') {
            $current = Join-Path $current $segment
            $paths.Add($current)
        }
    }
    foreach ($path in $paths) {
        if (Test-Path -LiteralPath $path) {
            $attributes = [IO.File]::GetAttributes($path)
            if (($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Reparse points are not allowed in task paths: $path" }
        }
    }
}

function Assert-RepopluginTaskId {
    param([Parameter(Mandatory)][string]$TaskId)
    if ($TaskId -notmatch '^[a-z0-9][a-z0-9-]{2,79}$') {
        throw 'TaskId must be 3-80 lowercase letters, digits, or hyphens and cannot start with a hyphen.'
    }
}

function Get-RepopluginTaskPath {
    param([Parameter(Mandatory)][string]$TaskRoot, [Parameter(Mandatory)][string]$TaskId)
    Assert-RepopluginTaskId $TaskId
    $root = Get-RepopluginTaskRoot $TaskRoot
    $candidate = [IO.Path]::GetFullPath((Join-Path $root $TaskId))
    $prefix = $root.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $candidate.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Task path escapes the task root.' }
    return $candidate
}

function Assert-RepopluginRelativePath {
    param([Parameter(Mandatory)][string]$RelativePath)
    if ([IO.Path]::IsPathRooted($RelativePath) -or [string]::IsNullOrWhiteSpace($RelativePath)) { throw 'A non-empty relative path is required.' }
    if (($RelativePath -split '[\\/]') -contains '..') { throw 'Relative paths cannot contain traversal segments.' }
    if ($RelativePath -notmatch '\.md$|\.json$') { throw 'Task artifacts must be Markdown or JSON.' }
}

function Get-RepopluginContainedPath {
    param([Parameter(Mandatory)][string]$TaskPath, [Parameter(Mandatory)][string]$Area, [Parameter(Mandatory)][string]$RelativePath)
    if ($script:Areas -notcontains $Area) { throw "Unknown task area '$Area'." }
    Assert-RepopluginRelativePath $RelativePath
    $areaPath = [IO.Path]::GetFullPath((Join-Path $TaskPath $Area))
    $candidate = [IO.Path]::GetFullPath((Join-Path $areaPath $RelativePath))
    $prefix = $areaPath.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $candidate.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Artifact path escapes its task area.' }
    return $candidate
}

function Write-RepopluginAtomicText {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$Text)
    $directory = Split-Path -Parent $Path
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    $temporary = Join-Path $directory ('.' + [IO.Path]::GetRandomFileName())
    try {
        [IO.File]::WriteAllText($temporary, $Text, [Text.UTF8Encoding]::new($false))
        Move-Item -LiteralPath $temporary -Destination $Path -Force
    } finally {
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
    }
}

function Read-RepopluginState {
    param([Parameter(Mandatory)][string]$TaskPath)
    $statePath = Join-Path $TaskPath 'state.json'
    if (-not (Test-Path -LiteralPath $statePath -PathType Leaf)) { throw 'Task state.json is missing.' }
    try { return (Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json) }
    catch { throw "Task state.json is invalid JSON: $($_.Exception.Message)" }
}

function Resolve-RepopluginTask {
    param([Parameter(Mandatory)][ValidateSet('Attach', 'Resume')][string]$Mode, [Parameter(Mandatory)][string]$TaskRoot, [string]$TaskId, [string]$HandoffPath)
    if ([string]::IsNullOrWhiteSpace($TaskId) -and [string]::IsNullOrWhiteSpace($HandoffPath)) { throw "$Mode requires an explicit TaskId or handoff path; it never selects a latest task." }
    $handoffFullPath = $null
    if (-not [string]::IsNullOrWhiteSpace($HandoffPath)) {
        $handoffFullPath = [IO.Path]::GetFullPath($HandoffPath)
        $root = Get-RepopluginTaskRoot $TaskRoot
        $rootPrefix = $root.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        if (-not $handoffFullPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Handoff path is outside the task root.' }
        Assert-RepopluginNoReparsePoint -RootPath $root -CandidatePath $handoffFullPath
        if (-not (Test-Path -LiteralPath $handoffFullPath -PathType Leaf)) { throw 'Handoff file does not exist.' }
        try { $handoff = Get-Content -LiteralPath $handoffFullPath -Raw | ConvertFrom-Json } catch { throw 'Handoff file is invalid JSON.' }
        if ([string]::IsNullOrWhiteSpace($handoff.task_id)) { throw 'Handoff does not contain task_id.' }
        if (-not [string]::IsNullOrWhiteSpace($TaskId) -and $TaskId -ne $handoff.task_id) { throw 'TaskId conflicts with handoff task_id.' }
        $TaskId = $handoff.task_id
    }
    $taskPath = Get-RepopluginTaskPath -TaskRoot $TaskRoot -TaskId $TaskId
    if (-not (Test-Path -LiteralPath $taskPath -PathType Container)) { throw "Task '$TaskId' does not exist." }
    Assert-RepopluginNoReparsePoint -RootPath (Get-RepopluginTaskRoot $TaskRoot) -CandidatePath $taskPath
    if ($null -ne $handoffFullPath) {
        $prefix = $taskPath.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        if (-not $handoffFullPath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Handoff path is outside its task folder.' }
        if ($handoff.task_path -ne ".repoplugin/tasks/$TaskId") { throw 'Handoff task_path does not match its task_id.' }
    }
    $statePath = Join-Path $taskPath 'state.json'
    $taskDocumentPath = Join-Path $taskPath 'task.md'
    Assert-RepopluginNoReparsePoint -RootPath $taskPath -CandidatePath $statePath
    Assert-RepopluginNoReparsePoint -RootPath $taskPath -CandidatePath $taskDocumentPath
    $state = Read-RepopluginState $taskPath
    $requiredStateProperties = @('task_id', 'status', 'created_at', 'completed_at')
    if (@(Compare-Object $requiredStateProperties @($state.PSObject.Properties.Name)).Count -ne 0) { throw 'Task state has an invalid shape.' }
    if ($state.task_id -ne $TaskId -or @('active', 'completed') -notcontains $state.status) { throw 'Task state has an invalid identity or status.' }
    if ([string]::IsNullOrWhiteSpace([string]$state.created_at)) { throw 'Task state is missing created_at.' }
    if ($state.status -eq 'active' -and $null -ne $state.completed_at) { throw 'An active task cannot have completed_at.' }
    if ($state.status -eq 'completed' -and [string]::IsNullOrWhiteSpace([string]$state.completed_at)) { throw 'A completed task requires completed_at.' }
    if (-not (Test-Path -LiteralPath $taskDocumentPath -PathType Leaf)) { throw 'Task task.md is missing.' }
    $taskDocument = Get-Content -LiteralPath $taskDocumentPath -Raw
    $frontmatter = [regex]::Match($taskDocument, '\A---\r?\n(?<fields>.*?)\r?\n---(?:\r?\n|$)', [Text.RegularExpressions.RegexOptions]::Singleline)
    if (-not $frontmatter.Success) { throw 'Task document frontmatter is invalid.' }
    $taskIdFields = [regex]::Matches($frontmatter.Groups['fields'].Value, '(?m)^task_id:\s*(?<value>\S+)\s*$')
    if ($taskIdFields.Count -ne 1 -or $taskIdFields[0].Groups['value'].Value -ne $TaskId) { throw 'Task document frontmatter identity does not match the requested task.' }
    return [pscustomobject]@{ TaskId = $TaskId; TaskPath = $taskPath; State = $state }
}

function New-RepopluginTask {
    param([Parameter(Mandatory)][string]$TaskRoot, [string]$TaskId, [Parameter(Mandatory)][string]$Request)
    if ([string]::IsNullOrWhiteSpace($TaskId)) { $TaskId = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ').ToLowerInvariant() + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8) }
    Assert-RepopluginTaskId $TaskId
    $root = Get-RepopluginTaskRoot $TaskRoot
    $taskPath = Get-RepopluginTaskPath -TaskRoot $root -TaskId $TaskId
    if (Test-Path -LiteralPath $taskPath) { throw "Task '$TaskId' already exists; existing task data is preserved." }
    [IO.Directory]::CreateDirectory($taskPath) | Out-Null
    foreach ($area in $script:Areas) { [IO.Directory]::CreateDirectory((Join-Path $taskPath $area)) | Out-Null }
    $timestamp = (Get-Date).ToUniversalTime().ToString('o')
    $taskDocument = "---`ntask_id: $TaskId`npath: .repoplugin/tasks/$TaskId/task.md`nowner: requester`ntimestamp: $timestamp`n---`n`n# Task request`n`n$Request`n"
    Write-RepopluginAtomicText -Path (Join-Path $taskPath 'task.md') -Text $taskDocument
    $state = [ordered]@{ task_id = $TaskId; status = 'active'; created_at = $timestamp; completed_at = $null }
    Write-RepopluginAtomicText -Path (Join-Path $taskPath 'state.json') -Text (($state | ConvertTo-Json) + "`n")
    return Resolve-RepopluginTask -Mode Attach -TaskRoot $root -TaskId $TaskId
}

function Write-RepopluginArtifact {
    param([Parameter(Mandatory)][string]$TaskRoot, [Parameter(Mandatory)][string]$TaskId, [Parameter(Mandatory)][string]$Area, [Parameter(Mandatory)][string]$RelativePath, [Parameter(Mandatory)][string]$Owner, [Parameter(Mandatory)][string]$Content)
    if ($RelativePath -notmatch '\.md$') { throw 'WriteArtifact requires a Markdown relative path.' }
    $task = Resolve-RepopluginTask -Mode Attach -TaskRoot $TaskRoot -TaskId $TaskId
    $path = Get-RepopluginContainedPath -TaskPath $task.TaskPath -Area $Area -RelativePath $RelativePath
    Assert-RepopluginNoReparsePoint -RootPath $task.TaskPath -CandidatePath $path
    $timestamp = (Get-Date).ToUniversalTime().ToString('o')
    $repoPath = ".repoplugin/tasks/$TaskId/$Area/$RelativePath".Replace('\\', '/')
    $document = "---`ntask_id: $TaskId`npath: $repoPath`nowner: $Owner`ntimestamp: $timestamp`n---`n`n$Content"
    Write-RepopluginAtomicText -Path $path -Text $document
    return [pscustomobject]@{ task_id = $TaskId; path = $path; relative_path = "$Area/$RelativePath" }
}

function Write-RepopluginHandoff {
    param([Parameter(Mandatory)][string]$TaskRoot, [Parameter(Mandatory)][string]$TaskId, [Parameter(Mandatory)][string]$Area, [Parameter(Mandatory)][string]$RelativePath, [string[]]$ArtifactReferences = @())
    if ($RelativePath -notmatch '\.json$') { throw 'WriteHandoff requires a JSON relative path.' }
    foreach ($reference in $ArtifactReferences) { Assert-RepopluginRelativePath $reference }
    $task = Resolve-RepopluginTask -Mode Attach -TaskRoot $TaskRoot -TaskId $TaskId
    $path = Get-RepopluginContainedPath -TaskPath $task.TaskPath -Area $Area -RelativePath $RelativePath
    Assert-RepopluginNoReparsePoint -RootPath $task.TaskPath -CandidatePath $path
    $handoff = [ordered]@{ task_id = $TaskId; task_path = ".repoplugin/tasks/$TaskId"; artifact_references = @($ArtifactReferences); created_at = (Get-Date).ToUniversalTime().ToString('o') }
    Write-RepopluginAtomicText -Path $path -Text (($handoff | ConvertTo-Json -Depth 4) + "`n")
    return [pscustomobject]@{ task_id = $TaskId; path = $path; artifact_references = @($ArtifactReferences) }
}

function Complete-RepopluginTask {
    param([Parameter(Mandatory)][string]$TaskRoot, [Parameter(Mandatory)][string]$TaskId)
    $task = Resolve-RepopluginTask -Mode Attach -TaskRoot $TaskRoot -TaskId $TaskId
    if ($task.State.status -eq 'completed') { return $task }
    if ($task.State.status -ne 'active') { throw "Task '$TaskId' has unsupported status '$($task.State.status)'." }
    $task.State.status = 'completed'
    $task.State.completed_at = (Get-Date).ToUniversalTime().ToString('o')
    Write-RepopluginAtomicText -Path (Join-Path $task.TaskPath 'state.json') -Text (($task.State | ConvertTo-Json) + "`n")
    return Resolve-RepopluginTask -Mode Attach -TaskRoot $TaskRoot -TaskId $TaskId
}

function Test-RepopluginTask {
    param([Parameter(Mandatory)][string]$TaskRoot, [Parameter(Mandatory)][string]$TaskId)
    $task = Resolve-RepopluginTask -Mode Attach -TaskRoot $TaskRoot -TaskId $TaskId
    if (-not (Test-Path -LiteralPath (Join-Path $task.TaskPath 'task.md') -PathType Leaf)) { throw 'Task task.md is missing.' }
    foreach ($area in $script:Areas) { if (-not (Test-Path -LiteralPath (Join-Path $task.TaskPath $area) -PathType Container)) { throw "Task area '$area' is missing." } }
    return [pscustomobject]@{ task_id = $TaskId; valid = $true; status = $task.State.status; task_path = $task.TaskPath }
}

Export-ModuleMember -Function New-RepopluginTask, Resolve-RepopluginTask, Write-RepopluginArtifact, Write-RepopluginHandoff, Complete-RepopluginTask, Test-RepopluginTask
