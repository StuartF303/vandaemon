#!/usr/bin/env pwsh
<#
.SYNOPSIS
  SC-005 / FR-008 / FR-009 guard: assert the Kotlin shell stays thin (no application
  logic; rough line-count budget) and remains an ordinary app (no HOME/DEFAULT category).

.DESCRIPTION
  Fails (exit 1) if:
   - main Kotlin LOC under app/src/main/kotlin exceeds the budget, or
   - AndroidManifest.xml declares android.intent.category.HOME or DEFAULT.
  Constitution §XII.4 caps the Kotlin layer at "a few hundred lines"; budget set to 500.
#>
[CmdletBinding()]
param(
    [int]$Budget = 500
)

$ErrorActionPreference = 'Stop'
$appRoot  = Split-Path -Parent $PSScriptRoot
$mainKt   = Join-Path $appRoot 'src/main/kotlin'
$manifest = Join-Path $appRoot 'src/main/AndroidManifest.xml'

$loc = (Get-ChildItem $mainKt -Recurse -Filter *.kt | Get-Content | Measure-Object -Line).Lines
Write-Host "Main Kotlin LOC: $loc (budget $Budget)"

$manifestText = Get-Content $manifest -Raw
# Match an actual category DECLARATION (the android:name="..." attribute form), not a
# prose mention in a comment — otherwise the comment explaining we omit HOME/DEFAULT
# would itself trip this guard.
$declaresHome = $manifestText -match 'android:name\s*=\s*"android\.intent\.category\.(HOME|DEFAULT)"'

$ok = $true
if ($loc -gt $Budget) {
    Write-Host "FAIL: Kotlin LOC $loc exceeds budget $Budget (Constitution §XII.4)" -ForegroundColor Red
    $ok = $false
}
if ($declaresHome) {
    Write-Host "FAIL: manifest declares a HOME/DEFAULT category (FR-009, must stay an ordinary app)" -ForegroundColor Red
    $ok = $false
}

if ($ok) {
    Write-Host "PASS: thin shell within budget; no HOME/DEFAULT category." -ForegroundColor Green
    exit 0
} else {
    exit 1
}
