<#
    .SYNOPSIS
    Copies messages from live mailboxes into corpus/holding/<mailbox>/ as raw
    .eml files, for examination before anything is promoted into the corpus.

    .DESCRIPTION
    Signs in as the operator (delegated Mail.Read.Shared) and reads any mailbox
    that account already has Full Access to. Pegasus's own managed identities
    are not used. Every Graph call is a GET: mail is copied, never moved,
    flagged or marked read.

    Each message is written as
    <yyyy-MM-dd_HHmm>_<12-hex SHA-256 of Internet Message-ID>_<subject>.eml.
    A message whose hash already appears in the mailbox's holding folder is
    skipped, so re-runs and overlapping filters do not duplicate. Each run
    writes index-<utc>.csv beside the files, one row per message.

    Without -Search, -Since/-Until filter on the server, newest first. Graph
    cannot combine $search with $filter, so with -Search the date bounds are
    applied to the returned results instead.

    .EXAMPLE
    pwsh ./scripts/Export-MailboxToCorpusHolding.ps1 -Mailbox desk@collisionengineers.co.uk -Folder inbox -First 5

    .EXAMPLE
    pwsh ./scripts/Export-MailboxToCorpusHolding.ps1 -Mailbox info@collisionengineers.co.uk,engineers@collisionengineers.co.uk -Since 2026-09-01 -Search 'QDOS' -First 200 -WhatIf
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[^@\s/\\]+@[^@\s/\\]+$')]
    [string[]]$Mailbox,
    [string]$Folder,
    [datetime]$Since,
    [datetime]$Until,
    [string]$Search,
    [ValidateRange(1, 100000)]
    [int]$First = 100,
    [string]$CorpusRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-MessageHash([string] $MessageId) {
    $bytes = [System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes($MessageId))
    [System.Convert]::ToHexString($bytes).Substring(0, 12).ToLowerInvariant()
}

function Get-HoldingFileName([datetime] $Received, [string] $MessageId, [string] $Subject) {
    $slug = ($Subject -replace '[^\w\-]+', '-').Trim('-')
    if ($slug.Length -gt 60) { $slug = $slug.Substring(0, 60).TrimEnd('-') }
    if (-not $slug) { $slug = 'no-subject' }
    '{0:yyyy-MM-dd_HHmm}_{1}_{2}.eml' -f $Received.ToUniversalTime(), (Get-MessageHash $MessageId), $slug
}

if (-not $CorpusRoot) {
    # The corpus lives only in the primary worktree; a task worktree would otherwise start an empty one.
    $primary = (& git -C $PSScriptRoot worktree list --porcelain | Select-Object -First 1) -replace '^worktree ', ''
    $CorpusRoot = Join-Path $primary 'corpus'
}
if (-not (Test-Path -LiteralPath $CorpusRoot -PathType Container)) { throw "Corpus root '$CorpusRoot' does not exist." }

Import-Module Microsoft.Graph.Authentication
Connect-MgGraph -Scopes Mail.Read.Shared -NoWelcome

$headers = @{ Prefer = 'IdType="ImmutableId"' }
$wellKnown = 'inbox', 'sentitems', 'deleteditems', 'archive', 'drafts', 'junkemail'
$runStamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')

foreach ($address in $Mailbox) {
    $user = "v1.0/users/$([uri]::EscapeDataString($address))"
    $source = "$user/messages"
    if ($Folder -and $wellKnown -contains $Folder.ToLowerInvariant()) {
        $source = "$user/mailFolders/$($Folder.ToLowerInvariant())/messages"
    }
    elseif ($Folder) {
        $match = Invoke-MgGraphRequest -Method GET -Headers $headers -Uri ("$user/mailFolders?`$filter=" + [uri]::EscapeDataString("displayName eq '$($Folder -replace "'", "''")'"))
        if ($match.value.Count -ne 1) { throw "Folder '$Folder' matched $($match.value.Count) top-level folders in $address." }
        $source = "$user/mailFolders/$([uri]::EscapeDataString($match.value[0].id))/messages"
    }

    $query = @('$select=id,internetMessageId,receivedDateTime,from,subject,hasAttachments', '$top=100')
    if ($Search) {
        $query += '$search=' + [uri]::EscapeDataString('"' + ($Search -replace '"', '') + '"')
    }
    else {
        $bounds = @()
        if ($PSBoundParameters.ContainsKey('Since')) { $bounds += 'receivedDateTime ge ' + $Since.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ') }
        if ($PSBoundParameters.ContainsKey('Until')) { $bounds += 'receivedDateTime lt ' + $Until.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ') }
        if ($bounds) { $query += '$filter=' + [uri]::EscapeDataString($bounds -join ' and ') }
        $query += '$orderby=' + [uri]::EscapeDataString('receivedDateTime desc')
    }

    $target = Join-Path $CorpusRoot "holding/$address"
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    $held = [System.Collections.Generic.HashSet[string]]::new()
    if (Test-Path -LiteralPath $target) {
        foreach ($file in Get-ChildItem -LiteralPath $target -Filter '*.eml' -File) {
            if ($file.Name -match '^\d{4}-\d{2}-\d{2}_\d{4}_([0-9a-f]{12})_') { [void]$held.Add($Matches[1]) }
        }
    }

    $rows = [System.Collections.Generic.List[object]]::new()
    $next = "$source`?" + ($query -join '&')
    while ($next -and $rows.Count -lt $First) {
        # Graph's SDK pipeline already retries 429/503 honouring Retry-After.
        $page = Invoke-MgGraphRequest -Method GET -Headers $headers -Uri $next
        foreach ($message in $page.value) {
            if ($rows.Count -ge $First) { break }
            $received = [datetime]$message.receivedDateTime
            if ($Search -and $PSBoundParameters.ContainsKey('Since') -and $received -lt $Since) { continue }
            if ($Search -and $PSBoundParameters.ContainsKey('Until') -and $received -ge $Until) { continue }

            $messageId = if ($message.internetMessageId) { $message.internetMessageId } else { $message.id }
            $name = Get-HoldingFileName $received $messageId ([string]$message.subject)
            $status = 'skipped'
            if (-not $held.Contains((Get-MessageHash $messageId))) {
                $status = 'would copy'
                $path = Join-Path $target $name
                if ($PSCmdlet.ShouldProcess($path, "Copy message from $address")) {
                    $partial = "$path.partial"
                    try {
                        Invoke-MgGraphRequest -Method GET -Headers $headers -Uri "$user/messages/$([uri]::EscapeDataString($message.id))/`$value" -OutputFilePath $partial
                        Move-Item -LiteralPath $partial -Destination $path
                    }
                    finally {
                        if (Test-Path -LiteralPath $partial) { Remove-Item -LiteralPath $partial }
                    }
                    [void]$held.Add((Get-MessageHash $messageId))
                    $status = 'copied'
                }
            }

            $from = if ($message.from) { $message.from.emailAddress.address } else { '' }
            $rows.Add([pscustomobject]@{
                    Mailbox           = $address
                    Folder            = if ($Folder) { $Folder } else { 'all' }
                    Received          = $received.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
                    From              = $from
                    Subject           = $message.subject
                    HasAttachments    = $message.hasAttachments
                    InternetMessageId = $messageId
                    File              = $name
                    Status            = $status
                })
        }
        $next = $page['@odata.nextLink']
    }

    if ($rows.Count -gt 0) {
        $rows | Export-Csv -LiteralPath (Join-Path $target "index-$runStamp.csv") -NoTypeInformation -Encoding utf8
    }
    $rows
    Write-Host ("{0}: {1} copied, {2} skipped, {3} listed." -f $address, @($rows | Where-Object Status -EQ 'copied').Count, @($rows | Where-Object Status -EQ 'skipped').Count, $rows.Count)
}
