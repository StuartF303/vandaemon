<#
.SYNOPSIS
    Build, verify and flash VANDIMMER-4CH+2A firmware with delivery guarantees.

.DESCRIPTION
    Wraps `pio run` in the checks that ad-hoc command lines were missing.

    The problem this exists to solve: on this machine os.spawnve() is broken
    (Windows 11 build 26200 -- see tools/scons_spawn_fix.py), and SCons routes
    every build action through it. Builds therefore died partway, leaving
    bootloader.bin and partitions.bin but NO firmware.bin, while spawning a
    console window per compile step until the machine fell over. Nothing in that
    sequence announced itself as a failure, so a stale image could be flashed
    and believed.

    Every gate below exists because that actually happened.

      PREFLIGHT   interpreter, IDF_PATH, spawnve self-test, workaround wired in,
                  truncated SCons database
      BUILD       firmware.bin deleted first, so absence afterwards is proof of
                  failure regardless of what the exit code claimed
      VERIFY      artifacts present, freshly written, plausibly sized, hashed
      FLASH       only reached if VERIFY passed
      CONFIRM     board's boot banner must report the build id just produced

.PARAMETER Port
    Serial port (e.g. COM4). Auto-detected when omitted.

.PARAMETER Environment
    PlatformIO environment. Default: 4ch2a

.PARAMETER Clean
    Force a clean build.

.PARAMETER NoFlash
    Build and verify only; do not touch the board.

.PARAMETER NoConfirm
    Flash without the post-flash serial confirmation. Not recommended.

.PARAMETER FixEnvironment
    Permanently remove the stale user-level IDF_PATH (points at a drive that no
    longer exists and makes idf_tools.py abort). Asks first.

.EXAMPLE
    .\tools\Build-Flash.ps1
.EXAMPLE
    .\tools\Build-Flash.ps1 -Clean -Port COM4
.EXAMPLE
    .\tools\Build-Flash.ps1 -NoFlash
#>

[CmdletBinding()]
param(
    [string] $Port,
    [string] $Environment = '4ch2a',
    [switch] $Clean,
    [switch] $NoFlash,
    [switch] $NoConfirm,
    [switch] $FixEnvironment,
    [double] $ConfirmTimeout = 20
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------- presentation
$script:StepNo = 0
function Step  ([string] $m) { $script:StepNo++; Write-Host ''; Write-Host ("== [{0}] {1}" -f $script:StepNo, $m) -ForegroundColor Cyan }
function Ok    ([string] $m) { Write-Host ("   OK    " + $m) -ForegroundColor Green }
function Note  ([string] $m) { Write-Host ("   note  " + $m) -ForegroundColor DarkGray }
function Warn  ([string] $m) { Write-Host ("   WARN  " + $m) -ForegroundColor Yellow }
function Fail  ([string] $m, [string[]] $why = @()) {
    Write-Host ''
    Write-Host ("   FAIL  " + $m) -ForegroundColor Red
    foreach ($line in $why) { Write-Host ("         " + $line) -ForegroundColor Red }
    Write-Host ''
    exit 1
}

# ------------------------------------------------------------------- locations
$ProjectDir = Split-Path -Parent $PSScriptRoot
$BuildDir   = Join-Path $ProjectDir ".pio\build\$Environment"
$Python     = Join-Path $env:USERPROFILE '.platformio-venv\Scripts\python.exe'

Write-Host ''
Write-Host 'VANDIMMER-4CH+2A  build / flash' -ForegroundColor White
Write-Host ("project  " + $ProjectDir) -ForegroundColor DarkGray
Write-Host ("env      " + $Environment) -ForegroundColor DarkGray

# ================================================================== PREFLIGHT
Step 'Preflight'

if (-not (Test-Path $Python)) {
    Fail "PlatformIO interpreter not found at $Python" @(
        'Rebuild the venv, then re-run:',
        '  py -m venv $env:USERPROFILE\.platformio-venv',
        '  & $env:USERPROFILE\.platformio-venv\Scripts\python.exe -m pip install platformio "click<8.2"',
        'The click pin is not optional -- esptool 5.0 breaks on click 8.2.'
    )
}
$pyVer = (& $Python -c "import sys;print('%d.%d.%d' % sys.version_info[:3])" 2>&1)
Ok "interpreter $pyVer  ($Python)"

# Stale IDF_PATH makes idf_tools.py abort with an unrelated-looking error.
$userIdf = [Environment]::GetEnvironmentVariable('IDF_PATH', 'User')
if ($userIdf) {
    if ($FixEnvironment) {
        Write-Host ("   IDF_PATH is set at user level: " + $userIdf) -ForegroundColor Yellow
        $ans = Read-Host '   Remove it permanently? [y/N]'
        if ($ans -match '^[Yy]') {
            [Environment]::SetEnvironmentVariable('IDF_PATH', $null, 'User')
            Ok 'removed user-level IDF_PATH (new shells will be clean)'
        } else { Note 'left unchanged' }
    } else {
        Warn "user-level IDF_PATH=$userIdf  (stale; re-run with -FixEnvironment to remove)"
    }
}
$env:IDF_PATH = $null
Ok 'IDF_PATH cleared for this build'

# Is the OS-level spawnve defect still present, and is the workaround wired in?
$spawnProbe = @'
import os, sys
c = os.environ["COMSPEC"]
try:
    os.spawnve(os.P_WAIT, c, [c, "/C", "echo x"], {"SystemRoot": os.environ["SystemRoot"]})
    print("OK")
except Exception as e:
    print("EXC:%s" % e)
'@
$probeOut = (& $Python -c $spawnProbe 2>&1 | Select-Object -Last 1)
$spawnBroken = ($LASTEXITCODE -ne 0) -or ($probeOut -notmatch 'OK')

$iniPath = Join-Path $ProjectDir 'platformio.ini'
$iniText = Get-Content $iniPath -Raw
$patchWired = $iniText -match 'pre:tools/scons_spawn_fix\.py'

if ($spawnBroken) {
    Warn ("os.spawnve is BROKEN on this machine (exit {0})" -f $LASTEXITCODE)
    if (-not $patchWired) {
        Fail 'The spawnve workaround is not registered, and builds cannot succeed without it.' @(
            'Add to the [env] section of platformio.ini:',
            '    extra_scripts =',
            '        pre:tools/scons_spawn_fix.py',
            'Without it SCons dies mid-build, produces no firmware.bin, and opens',
            'a console window per compile step until the machine is unusable.'
        )
    }
    Ok 'workaround registered in platformio.ini -- safe to build'
} else {
    Ok 'os.spawnve works on this machine'
    if ($patchWired) { Note 'workaround still wired in (harmless; safe to retire once this stays green)' }
}

# A truncated SCons database forces a full rebuild -- the storm-prone case.
$dblite = Get-ChildItem (Join-Path $BuildDir '.sconsign*.dblite') -ErrorAction SilentlyContinue
foreach ($d in $dblite) {
    if ($d.Length -eq 0) {
        Warn "$($d.Name) is 0 bytes (truncated by an earlier crash) -- forcing a clean build"
        $Clean = $true
    }
}

$conhostBefore = @(Get-Process conhost -ErrorAction SilentlyContinue).Count
Note "conhost processes before build: $conhostBefore"

# ====================================================================== BUILD
if ($Clean) {
    Step 'Clean'
    & $Python -m platformio run -e $Environment -t fullclean 2>&1 | ForEach-Object { Write-Host "   $_" }
    Ok 'build tree cleaned'
}

Step 'Build'

# Pin the build identity for BOTH the build and the upload.
#
# `pio run -t upload` re-runs the pre-scripts, so an unpinned build_id.py would
# mint a fresh timestamp at flash time: a different global -D, every object
# recompiled, and the binary actually flashed no longer the one this script
# verified and hashed. Pinning makes the upload a genuine no-op rebuild.
$sha8 = (& git -C $ProjectDir rev-parse --short=8 HEAD 2>$null)
if ($LASTEXITCODE -ne 0 -or -not $sha8) { $sha8 = 'nogit' }
$dirtyFlag = ''
if (& git -C $ProjectDir status --porcelain --untracked-files=no 2>$null) { $dirtyFlag = '-dirty' }
$env:VANDIMMER_BUILD_ID = '{0}{1} {2}' -f $sha8, $dirtyFlag, (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
Note "build identity pinned: $env:VANDIMMER_BUILD_ID"

# Delete the image first. If it is missing afterwards the build failed, whatever
# the exit code says -- this is the check that catches a silent partial build.
$FirmwareBin = Join-Path $BuildDir 'firmware.bin'
if (Test-Path $FirmwareBin) { Remove-Item $FirmwareBin -Force; Note 'removed previous firmware.bin' }
$buildStart = Get-Date

& $Python -m platformio run -e $Environment 2>&1 | ForEach-Object { Write-Host "   $_" }
$buildExit = $LASTEXITCODE

$conhostAfter = @(Get-Process conhost -ErrorAction SilentlyContinue).Count
$storm = $conhostAfter - $conhostBefore
if ($storm -gt 20) {
    Warn "console-window storm detected (+$storm conhost processes)"
    Warn 'the spawnve workaround is not taking effect -- check tools/scons_spawn_fix.py loaded'
}

if ($buildExit -ne 0) { Fail "pio run exited $buildExit" }
Ok 'pio run reported success'

# ===================================================================== VERIFY
Step 'Verify build output'

if (-not (Test-Path $FirmwareBin)) {
    Fail 'firmware.bin was not produced, despite pio reporting success.' @(
        'This is the silent-partial-build failure. Nothing is safe to flash.',
        'Re-run with -Clean; if it recurs, the spawnve workaround is not loading.'
    )
}
$fw = Get-Item $FirmwareBin
if ($fw.LastWriteTime -lt $buildStart) { Fail 'firmware.bin predates this build -- it is stale.' }
if ($fw.Length -lt 100KB)  { Fail ("firmware.bin is implausibly small ({0} bytes)" -f $fw.Length) }
if ($fw.Length -gt 3MB)    { Warn ("firmware.bin is large ({0:N0} bytes) -- check partition sizes" -f $fw.Length) }
Ok ("firmware.bin      {0:N0} bytes   {1:HH:mm:ss}" -f $fw.Length, $fw.LastWriteTime)

foreach ($n in 'bootloader.bin', 'partitions.bin') {
    $p = Join-Path $BuildDir $n
    if (-not (Test-Path $p)) { Fail "$n missing from the build output" }
    Ok ("{0,-18}{1,10:N0} bytes" -f $n, (Get-Item $p).Length)
}

$sha = (Get-FileHash $FirmwareBin -Algorithm SHA256).Hash
Ok ("sha256 " + $sha.Substring(0, 32) + '...')

$BuildIdFile = Join-Path $BuildDir 'build_id.txt'
if (-not (Test-Path $BuildIdFile)) {
    Fail 'build_id.txt missing -- tools/build_id.py did not run.' @(
        'Post-flash confirmation depends on it. Check extra_scripts in platformio.ini.'
    )
}
$buildId = (Get-Content $BuildIdFile -Raw).Trim()
Ok "build id  $buildId"

if ($NoFlash) {
    Write-Host ''
    Write-Host 'Build verified. -NoFlash set, stopping here.' -ForegroundColor Green
    exit 0
}

# ====================================================================== FLASH
Step 'Locate board'

if (-not $Port) {
    $listPorts = 'import serial.tools.list_ports as lp;print(";".join(p.device for p in lp.comports()))'
    $found = (& $Python -c $listPorts 2>&1 | Select-Object -Last 1).Trim()
    $ports = @($found -split ';' | Where-Object { $_ })
    if ($ports.Count -eq 0) {
        Fail 'no serial ports found.' @(
            'This board has NO USB power path -- apply 12 V to J1/J2 before flashing.',
            'It will not enumerate from the USB-C connector alone.'
        )
    }
    if ($ports.Count -gt 1) {
        Warn ("multiple ports: {0} -- using {1}; pass -Port to choose" -f ($ports -join ', '), $ports[0])
    }
    $Port = $ports[0]
}
Ok "port $Port"

Step 'Flash'
& $Python -m platformio run -e $Environment -t upload --upload-port $Port 2>&1 | ForEach-Object { Write-Host "   $_" }
if ($LASTEXITCODE -ne 0) { Fail "upload exited $LASTEXITCODE" }
Ok 'esptool reported success'

# The upload re-runs the build. Prove it flashed the artifact we verified, and
# not a silently regenerated one.
$shaAfter = (Get-FileHash $FirmwareBin -Algorithm SHA256).Hash
$idAfter  = (Get-Content $BuildIdFile -Raw).Trim()
if ($shaAfter -ne $sha -or $idAfter -ne $buildId) {
    Fail 'the upload rebuilt the firmware -- what was flashed is not what was verified.' @(
        "verified: $buildId  sha $($sha.Substring(0,16))...",
        "flashed : $idAfter  sha $($shaAfter.Substring(0,16))...",
        'The build identity pin is not reaching tools/build_id.py.'
    )
}
Ok 'flashed image is byte-identical to the verified one'

# ==================================================================== CONFIRM
if ($NoConfirm) {
    Write-Host ''
    Warn 'post-flash confirmation skipped -- "flashed" is unproven'
    exit 0
}

Step 'Confirm the board is running this build'
Start-Sleep -Milliseconds 600
& $Python (Join-Path $PSScriptRoot 'verify_boot.py') `
    --port $Port --expect-file $BuildIdFile --timeout $ConfirmTimeout 2>&1 |
    ForEach-Object { Write-Host "   $_" }

if ($LASTEXITCODE -ne 0) {
    Fail 'the board is not confirmed to be running this build.' @(
        "expected build id: $buildId",
        'Re-run with -ConfirmTimeout 30, or press reset while it listens.'
    )
}

Write-Host ''
Write-Host 'DELIVERED' -ForegroundColor Green
Write-Host ("  build id  " + $buildId) -ForegroundColor Green
Write-Host ("  sha256    " + $sha) -ForegroundColor Green
Write-Host ("  port      " + $Port) -ForegroundColor Green
Write-Host ''
exit 0
