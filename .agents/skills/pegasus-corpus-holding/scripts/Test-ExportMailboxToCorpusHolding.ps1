[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Export-MailboxToCorpusHolding.ps1 skips a message whose hash is already in
# the file names, so the name must be stable for one Message-ID and safe on
# disk for any subject. Its helpers are loaded from the script's syntax tree
# so nothing signs in to Graph.

$script = Join-Path $PSScriptRoot 'Export-MailboxToCorpusHolding.ps1'
$ast = [System.Management.Automation.Language.Parser]::ParseFile($script, [ref]$null, [ref]$null)
foreach ($function in $ast.FindAll({ $args[0] -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $false)) {
    . ([scriptblock]::Create($function.Extent.Text))
}

function Assert([bool] $Condition, [string] $Message) { if (-not $Condition) { throw "FAIL: $Message" } }

$received = [datetime]::new(2026, 10, 8, 9, 5, 0, [DateTimeKind]::Utc)
$id = '<ABC123@mail.example>'
$name = Get-HoldingFileName $received $id 'RE: Instruction / QDOS26080 <urgent>?'

Assert ($name -eq (Get-HoldingFileName $received $id 'RE: Instruction / QDOS26080 <urgent>?')) 'same message gives the same name'
Assert ($name -match '^2026-10-08_0905_([0-9a-f]{12})_RE-Instruction-QDOS26080-urgent\.eml$') "readable, hashed name: $name"
Assert ($Matches[1] -eq (Get-MessageHash $id)) 'name carries the Message-ID hash'
Assert ((Get-MessageHash $id) -ne (Get-MessageHash '<ABC124@mail.example>')) 'different messages hash differently'
Assert ($name.IndexOfAny([System.IO.Path]::GetInvalidFileNameChars()) -lt 0) 'no invalid file name characters'

$long = Get-HoldingFileName $received $id ('x' * 300)
Assert ($long.Length -eq ('2026-10-08_0905_'.Length + 12 + 1 + 60 + '.eml'.Length)) "long subject is cut to 60: $long"

Assert ((Get-HoldingFileName $received $id '') -like '*_no-subject.eml') 'empty subject'
Assert ((Get-HoldingFileName $received $id '???') -like '*_no-subject.eml') 'punctuation-only subject'

Write-Host 'Export-MailboxToCorpusHolding file names: OK'
