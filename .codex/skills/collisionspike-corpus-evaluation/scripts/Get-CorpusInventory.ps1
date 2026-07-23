[CmdletBinding()]
param(
    [string]$CorpusPath = (Join-Path (git rev-parse --show-toplevel) 'corpus')
)

$ErrorActionPreference = 'Stop'
$resolved = (Resolve-Path -LiteralPath $CorpusPath).Path
$files = @(Get-ChildItem -LiteralPath $resolved -Recurse -File -Force)

$extensions = $files |
    Group-Object { if ($_.Extension) { $_.Extension.ToLowerInvariant() } else { '[none]' } } |
    Sort-Object Count -Descending |
    Select-Object @{Name = 'extension'; Expression = 'Name'}, @{Name = 'count'; Expression = 'Count'}

[ordered]@{
    generatedAtUtc = [DateTime]::UtcNow.ToString('O')
    root = 'corpus'
    fileCount = $files.Count
    totalBytes = ($files | Measure-Object -Property Length -Sum).Sum
    topLevel = @(Get-ChildItem -LiteralPath $resolved -Directory | ForEach-Object {
        $children = @(Get-ChildItem -LiteralPath $_.FullName -Recurse -File -Force)
        [ordered]@{
            name = $_.Name
            fileCount = $children.Count
            totalBytes = ($children | Measure-Object -Property Length -Sum).Sum
        }
    })
    extensions = @($extensions)
} | ConvertTo-Json -Depth 5
