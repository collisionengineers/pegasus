[CmdletBinding()]
param(
    [string] $OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\research\doc\2026-07-24\specifications'),
    [switch] $VerifyOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$specifications = @(
    @{ File = 'MS-DOC-12.5-260217.docx'; Uri = 'https://officeprotocoldocs-f5hpbjgea6b8gneq.b02.azurefd.net/files/MS-DOC/%5bMS-DOC%5d-260217.docx'; Sha256 = '2e48b21886ebdd5dcc281c3d9baf1b7841c9f3d6881a153862069bbbc0608d7a' },
    @{ File = 'MS-DOC-12.5.pdf'; Uri = 'https://officeprotocoldocs-f5hpbjgea6b8gneq.b02.azurefd.net/files/MS-DOC/%5bMS-DOC%5d.pdf'; Sha256 = '23fd1a8413ff8fa2902097060ae7dea031fca9a7979ca20633c141a756ddb27c' },
    @{ File = 'MS-CFB-12.0-240423.docx'; Uri = 'https://winprotocoldocs-bhdugrdyduf5h2e4.b02.azurefd.net/MS-CFB/%5bMS-CFB%5d-240423.docx'; Sha256 = '2d650184072a148ba98ad0b68072fd5ad7780e46f3528d7f263f3127b2dadab5' },
    @{ File = 'MS-CFB-12.0.pdf'; Uri = 'https://winprotocoldocs-bhdugrdyduf5h2e4.b02.azurefd.net/MS-CFB/%5bMS-CFB%5d.pdf'; Sha256 = '9d0d61e34495347ee32f3de5b06f2d59953cc60607ea72605d4162d21a34863f' },
    @{ File = 'MS-ODRAW-12.4-250819.docx'; Uri = 'https://officeprotocoldocs-f5hpbjgea6b8gneq.b02.azurefd.net/files/MS-ODRAW/%5bMS-ODRAW%5d-250819.docx'; Sha256 = '9ead8f1f3805cf6d4f5597bed516bf7604e330b803f64d28d9b7a0a9dba9a2fc' },
    @{ File = 'MS-ODRAW-12.4.pdf'; Uri = 'https://officeprotocoldocs-f5hpbjgea6b8gneq.b02.azurefd.net/files/MS-ODRAW/%5bMS-ODRAW%5d.pdf'; Sha256 = 'fbc67309646b0b95e87f66eb7a3d89c6f08153c46379f496b886a8deae42d7e5' },
    @{ File = 'MS-OLEDS-13.0-240423.docx'; Uri = 'https://winprotocoldocs-bhdugrdyduf5h2e4.b02.azurefd.net/MS-OLEDS/%5bMS-OLEDS%5d-240423.docx'; Sha256 = '42e666e9f1b1c437972bbe601d302ec25e45557eb309c7d854e54facfeddb134' },
    @{ File = 'MS-OLEDS-13.0.pdf'; Uri = 'https://winprotocoldocs-bhdugrdyduf5h2e4.b02.azurefd.net/MS-OLEDS/%5bMS-OLEDS%5d.pdf'; Sha256 = '0ca6d5aa542092662d021748e2cc1c6ff45ffeaaecd5f5990c33258d078a9ec8' },
    @{ File = 'MS-OLEPS-9.0-240423.docx'; Uri = 'https://winprotocoldocs-bhdugrdyduf5h2e4.b02.azurefd.net/MS-OLEPS/%5bMS-OLEPS%5d-240423.docx'; Sha256 = '4343243993cd16bda98e5abe5383a82db5f2eea0b34b54dc7d93978a372844ea' },
    @{ File = 'MS-OLEPS-9.0.pdf'; Uri = 'https://winprotocoldocs-bhdugrdyduf5h2e4.b02.azurefd.net/MS-OLEPS/%5bMS-OLEPS%5d.pdf'; Sha256 = 'b6a19bdeb43a498bf7499b9c8a6a06f91b57ce159f6c20bd1bf157ad3a54fa2a' },
    @{ File = 'MS-OSHARED-11.1-251113.docx'; Uri = 'https://officeprotocoldocs-f5hpbjgea6b8gneq.b02.azurefd.net/files/MS-OSHARED/%5bMS-OSHARED%5d-251113.docx'; Sha256 = '3a17ec72868a7ba8c9c987995c8902e832a42d66eecbf149101a4e6c7255f87c' },
    @{ File = 'MS-OSHARED-11.1.pdf'; Uri = 'https://officeprotocoldocs-f5hpbjgea6b8gneq.b02.azurefd.net/files/MS-OSHARED/%5bMS-OSHARED%5d.pdf'; Sha256 = 'fb371245d7a6e217c2c02bf9d0736f7282fafe9242fb8fc96d009294beed2725' },
    @{ File = 'MS-OFFCRYPTO-14.0-260217.docx'; Uri = 'https://officeprotocoldocs-f5hpbjgea6b8gneq.b02.azurefd.net/files/MS-OFFCRYPTO/%5bMS-OFFCRYPTO%5d-260217.docx'; Sha256 = '9b7a67eb5d0408566a61f218792fcd21536dbc970d83695ad94365e535533f33' },
    @{ File = 'MS-OFFCRYPTO-14.0.pdf'; Uri = 'https://officeprotocoldocs-f5hpbjgea6b8gneq.b02.azurefd.net/files/MS-OFFCRYPTO/%5bMS-OFFCRYPTO%5d.pdf'; Sha256 = '65a20fdaef2b24cabd0c783620a46d339a98b33effffa9ca6da7795e9635ddf0' },
    @{ File = 'MS-OVBA-15.0-260519.docx'; Uri = 'https://officeprotocoldocs-f5hpbjgea6b8gneq.b02.azurefd.net/files/MS-OVBA/%5bMS-OVBA%5d-260519.docx'; Sha256 = '31fb68ac3ef209cb32247a3060ff775cc0517c4120137cb39945690448b46c79' },
    @{ File = 'MS-OVBA-15.0.pdf'; Uri = 'https://officeprotocoldocs-f5hpbjgea6b8gneq.b02.azurefd.net/files/MS-OVBA/%5bMS-OVBA%5d.pdf'; Sha256 = '6ca91a091da9b7a550e52ea60808fc78b676a3e96101a80990f75911619768c3' },
    @{ File = 'MS-OFORMS-9.1-250819.docx'; Uri = 'https://officeprotocoldocs-f5hpbjgea6b8gneq.b02.azurefd.net/files/MS-OFORMS/%5bMS-OFORMS%5d-250819.docx'; Sha256 = '7bbbbdc43407524fe2af99c070dfc358cc67404e5224b56d5cdabbc4736c9158' },
    @{ File = 'MS-OFORMS-9.1.pdf'; Uri = 'https://officeprotocoldocs-f5hpbjgea6b8gneq.b02.azurefd.net/files/MS-OFORMS/%5bMS-OFORMS%5d.pdf'; Sha256 = '37e72c8605244ebd28c2f247f9605d858fb44c194f835867d8b9ecedcb744db4' }
)

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
if (-not $VerifyOnly) {
    New-Item -ItemType Directory -Force -Path $resolvedOutput | Out-Null
}

$results = foreach ($specification in $specifications) {
    $target = Join-Path $resolvedOutput $specification.File
    if (-not (Test-Path -LiteralPath $target -PathType Leaf)) {
        if ($VerifyOnly) {
            throw "Missing specification artifact: $target"
        }

        $partial = "$target.download"
        if (Test-Path -LiteralPath $partial) {
            throw "Refusing to reuse partial download: $partial"
        }

        try {
            Invoke-WebRequest -Uri $specification.Uri -OutFile $partial
            $downloadHash = (Get-FileHash -LiteralPath $partial -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($downloadHash -ne $specification.Sha256) {
                throw "Hash mismatch for $($specification.File): $downloadHash"
            }
            Move-Item -LiteralPath $partial -Destination $target
        }
        finally {
            if (Test-Path -LiteralPath $partial) {
                Remove-Item -LiteralPath $partial
            }
        }
    }

    $actualHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $specification.Sha256) {
        throw "Hash mismatch for ${target}: $actualHash"
    }

    [pscustomobject]@{
        file = $specification.File
        sha256 = $actualHash
        bytes = (Get-Item -LiteralPath $target).Length
        status = 'verified'
    }
}

$results | ConvertTo-Json -Depth 3
