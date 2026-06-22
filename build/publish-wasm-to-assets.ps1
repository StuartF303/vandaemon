#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Documented two-step build glue (FR-014): publish the VanDaemon Blazor WASM UI and stage
  it into the launcher shell's APK assets. Gradle does NOT call this — run it first, then
  `cd app; ./gradlew assembleDebug`.

.DESCRIPTION
  Step 1: dotnet publish src/Frontend/VanDaemon.Web
  Step 2: copy the published wwwroot -> app/src/main/assets/www/
  (Step 3 — `gradle assemble` — is run separately, keeping Gradle decoupled from .NET.)

  The staged assets/www/ folder is generated output and is git-ignored.
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = 'Stop'

$repoRoot   = Split-Path -Parent $PSScriptRoot
$webProj    = Join-Path $repoRoot 'src/Frontend/VanDaemon.Web/VanDaemon.Web.csproj'
$publishDir = Join-Path $repoRoot 'artifacts/wasm-publish'
$assetsDir  = Join-Path $repoRoot 'app/src/main/assets/www'

if (-not (Test-Path $webProj)) { throw "VanDaemon.Web project not found at $webProj" }

Write-Host "[1/2] dotnet publish ($Configuration) -> $publishDir"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish $webProj -c $Configuration -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }

$wwwroot = Join-Path $publishDir 'wwwroot'
if (-not (Test-Path $wwwroot)) { throw "Published wwwroot not found at $wwwroot" }

Write-Host "[2/2] Staging wwwroot -> $assetsDir (excluding precompressed .gz/.br)"
# Blazor publish emits both uncompressed files and precompressed .gz/.br copies. Android's
# asset packager strips the .gz suffix (auto-gunzip), so foo + foo.gz collide as duplicate
# assets. We serve uncompressed originals via WwwAssetPathHandler (no Content-Encoding), so
# the precompressed variants are dead weight — skip them. (Brotli/AOT pipeline is out of
# scope this feature — FR-015.)
if (Test-Path $assetsDir) { Remove-Item $assetsDir -Recurse -Force }
New-Item -ItemType Directory -Force $assetsDir | Out-Null
$src = (Resolve-Path $wwwroot).Path.TrimEnd('\', '/')
Get-ChildItem -Path $src -Recurse -File |
    Where-Object { $_.Extension -notin '.gz', '.br' } |
    ForEach-Object {
        $rel  = $_.FullName.Substring($src.Length).TrimStart('\', '/')
        $dest = Join-Path $assetsDir $rel
        New-Item -ItemType Directory -Force (Split-Path $dest -Parent) | Out-Null
        Copy-Item $_.FullName $dest -Force
    }

$indexPresent = Test-Path (Join-Path $assetsDir 'index.html')
$frameworkPresent = Test-Path (Join-Path $assetsDir '_framework')
Write-Host "Done. index.html: $indexPresent  _framework/: $frameworkPresent"
if (-not ($indexPresent -and $frameworkPresent)) {
    throw "Staging incomplete: expected index.html and _framework/ under $assetsDir"
}
Write-Host "Next: cd app; ./gradlew assembleDebug"
