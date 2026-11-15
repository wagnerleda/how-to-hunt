<#
.SYNOPSIS
  Proves lag-compensated hit registration over a real (loopback) network path with simulated latency.

.DESCRIPTION
  A headless host loads the movement lab (moving target dummies). A headless client with net_fakelag joins and
  auto-fires 30 shots at a dummy exactly where it sees it. The host resolves every shot. Run with lag compensation
  on (expected: all hits) and off (expected: mostly misses on moving targets).

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools/lagcomp-test.ps1 -LatencyMs 150 -Dummy 2
#>
param([int]$LatencyMs = 150, [int]$Dummy = 2, [string]$Build = "Builds/Windows")
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$exe = Join-Path $root (Join-Path $Build 'HowToHunt.exe')
if (-not (Test-Path $exe)) { throw "Build not found: $exe" }
$logs = Join-Path $root 'Logs/lagcomp'
New-Item -ItemType Directory -Force $logs | Out-Null

function Run([int]$lagcomp) {
    Get-ChildItem $logs -Filter *.log | Remove-Item
    $h = Start-Process $exe -ArgumentList @('-batchmode', '-nographics', '-nocfg', '-logFile', "$logs/host.log",
        '+sv_cheats', '1', '+wpn_pistol_damage', '1', '+sv_lagcomp', "$lagcomp", '+host', 'local', '+delay', '24', 'quit') -PassThru
    Start-Sleep -Seconds 4
    $c = Start-Process $exe -ArgumentList @('-batchmode', '-nographics', '-nocfg', '-logFile', "$logs/client.log",
        '+net_fakelag', "$LatencyMs", '+connect', '127.0.0.1', '+delay', '5', 'wpn_autofire', "$Dummy", '30',
        '+delay', '14', 'wpn_stats', '+delay', '15', 'quit') -PassThru
    $c.WaitForExit(60000) | Out-Null
    $h.WaitForExit(60000) | Out-Null
    $m = [regex]::Match((Get-Content "$logs/client.log" -Raw), '\[hit\] (\d+)/(\d+)')
    if (-not $m.Success) { throw "No result (see $logs)." }
    return [int]$m.Groups[1].Value
}

$on = Run 1
$off = Run 0
Write-Host "Latency $LatencyMs ms, dummy $Dummy : lag compensation ON $on/30 hits, OFF $off/30 hits"
if ($on -ge 29 -and $off -lt $on) { Write-Host "LAGCOMP TEST PASSED" } else { Write-Host "LAGCOMP TEST FAILED"; exit 1 }
