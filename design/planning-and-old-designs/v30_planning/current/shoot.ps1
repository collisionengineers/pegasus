param(
    [string]$Chrome = "$env:LOCALAPPDATA\ms-playwright\chromium_headless_shell-1234\chrome-headless-shell-win64\chrome-headless-shell.exe",
    [string[]]$Only = @()
)

# v30 screenshot set: every surface x state x layer (and each switch on) at
# 1580x1000, 1440x900 and 760x1000. Not run in full on 25 September 2026 by
# the operator's instruction; run it when the set is wanted:
#   pwsh design/planning-and-old-designs/v30_planning/current/shoot.ps1
#   pwsh .../shoot.ps1 -Only signin,inbox

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSCommandPath
$shots = Join-Path $root 'v30-shots'
New-Item -ItemType Directory -Path $shots -Force | Out-Null

$surfaces = @(
    @{ key = 'signin'; file = 'pegasus_signin_v30.html'; states = @('default', 'error', 'signed-out', 'forced-password', 'access-denied'); layers = @('baseline', 'proposal'); opts = @('brand:compact,title:short,reveal:on') },
    @{ key = 'inbox'; file = 'pegasus_inbox_v30.html'; states = @('default', 'list', 'empty', 'stale', 'dismissed', 'deleted', 'search'); layers = @('baseline', 'proposal'); opts = @() },
    @{ key = 'work-centre'; file = 'pegasus_work_centre_v30.html'; states = @('default', 'mine', 'filtered', 'empty', 'unavailable', 'assign'); layers = @('baseline', 'proposal'); opts = @('metrics:toned') },
    @{ key = 'accounts'; file = 'pegasus_admin_accounts_v30.html'; states = @('list', 'settings-self', 'settings-other', 'settings-disabled', 'error', 'create', 'temporary-password', 'delete'); layers = @('baseline', 'proposal'); opts = @() }
)
foreach ($variant in @('a', 'b', 'c', 'd', 'e')) {
    $surfaces += @{ key = $variant; file = "pegasus_upload_${variant}_v30.html"; states = @('select', 'chosen', 'storing', 'processing', 'decision', 'registered', 'mixed', 'single', 'failed', 'discarded', 'no-match', 'multiple', 'attached'); layers = @('proposal'); opts = @() }
}

$sizes = @(@(1580, 1000), @(1440, 900), @(760, 1000))
$count = 0
foreach ($surface in $surfaces) {
    if ($Only.Count -gt 0 -and $Only -notcontains $surface.key) { continue }
    $file = "file:///" + (Join-Path $root $surface.file).Replace('\', '/')
    foreach ($state in $surface.states) {
        foreach ($layer in $surface.layers) {
            $name = if ($surface.layers.Count -gt 1) { "$state-$layer" } else { $state }
            $queries = @(@{ q = "state=$state&layer=$layer&embed=1"; name = $name })
            if ($layer -eq 'proposal') {
                foreach ($opt in $surface.opts) {
                    $queries += @{ q = "state=$state&layer=proposal&opt=$opt&embed=1"; name = "$state-proposal-" + ($opt -replace '[:,]', '-') }
                }
            }
            foreach ($query in $queries) {
                foreach ($size in $sizes) {
                    $out = Join-Path $shots ("{0}-{1}-{2}.png" -f $surface.key, $query.name, $size[0])
                    $window = "--window-size={0},{1}" -f $size[0], $size[1]
                    & $Chrome --headless=new --allow-file-access-from-files --disable-gpu --hide-scrollbars $window --virtual-time-budget=2500 "--screenshot=$out" "$file?$($query.q)" 2>$null | Out-Null
                    $count++
                }
            }
        }
    }
}
Write-Host "Captured $count screenshots into $shots"
