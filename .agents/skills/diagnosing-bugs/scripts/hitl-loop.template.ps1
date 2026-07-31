# Human-in-the-loop reproduction loop.
# Copy this file, edit the steps below, and run it with PowerShell 7.
# The agent runs the script; the user follows prompts in their terminal.
#
# Usage:
#   pwsh -NoProfile -File ./hitl-loop.template.ps1
#
# Two helpers:
#   Step "<instruction>"       -> show instruction, wait for Enter
#   Capture "<question>"       -> show question, return the response
#
# At the end, captured values are printed as KEY=VALUE for the agent to parse.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Step([string] $Instruction) {
    Write-Host "`n>>> $Instruction"
    $null = Read-Host '    [Enter when done]'
}

function Capture([string] $Question) {
    Write-Host "`n>>> $Question"
    return Read-Host '    >'
}

# --- edit below ---------------------------------------------------------

Step 'Open the app at http://localhost:3000 and sign in.'

$errored = Capture "Click the 'Export' button. Did it throw an error? (y/n)"

$errorMessage = Capture "Paste the error message (or 'none'):"

# --- edit above ---------------------------------------------------------

Write-Host "`n--- Captured ---"
Write-Host "ERRORED=$errored"
Write-Host "ERROR_MSG=$errorMessage"
