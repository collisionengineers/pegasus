param(
    [string]$Chrome = "$env:LOCALAPPDATA\ms-playwright\chromium_headless_shell-1234\chrome-headless-shell-win64\chrome-headless-shell.exe"
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSCommandPath
$shots = Join-Path $root 'v29-shots'
New-Item -ItemType Directory -Path $shots -Force | Out-Null

foreach ($variant in 'a', 'b', 'c', 'd', 'e') {
    foreach ($state in 'select', 'chosen', 'storing', 'processing', 'decision', 'registered', 'mixed', 'single', 'failed', 'discarded') {
        foreach ($size in @(@(1580, 1000), @(1440, 900), @(760, 1000))) {
            $width, $height = $size
            $name = "$variant-$state-$width.png"
            $file = Join-Path $shots $name
            $url = "file:///" + (Join-Path $root "pegasus_upload_${variant}_v29.html").Replace('\', '/') + "?state=$state"
            & $Chrome --headless=new --allow-file-access-from-files --disable-gpu `
                --window-size="$width,$height" --screenshot="$file" $url 2>$null | Out-Null
            if (-not (Test-Path -LiteralPath $file)) {
                throw "Screenshot missing: $file"
            }
            Write-Output $name
        }
    }
}
