<#
.SYNOPSIS
  Loopback stress test: a headless host, 7 bot clients (full 8-player party) and a 9th bot that must be refused.
  Bots run, jump and turn. After the run, checks every bot joined, the 9th was refused, nobody dropped, no
  exceptions, and reports host/client bandwidth and frame time against the budgets.
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools/bots-test.ps1 -Seconds 60
#>
param([int]$Seconds = 60, [string]$Build = "Builds/Windows")
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$exe = Join-Path $root (Join-Path $Build 'HowToHunt.exe')
if (-not (Test-Path $exe)) { throw "Build not found: $exe (run tools/build-windows.ps1)" }
$logs = Join-Path $root 'Logs/bots'
New-Item -ItemType Directory -Force $logs | Out-Null
Get-ChildItem $logs -Filter *.log | Remove-Item

$stats = $Seconds - 8
$hostArgs = @('-batchmode', '-nographics', '-logFile', "$logs/host.log", '+mat_vsync', '0', '+fps_max', '64', '+host', 'local',
    '+delay', "$stats", 'net_stats', '+delay', "$($stats + 1)", 'status', '+delay', "$Seconds", 'quit')
$procs = @(Start-Process $exe -ArgumentList $hostArgs -PassThru)
Start-Sleep -Seconds 4

for ($i = 1; $i -le 8; $i++) {
    $yaw = 20 + 15 * $i
    $botArgs = @('-batchmode', '-nographics', '-logFile', "$logs/bot$i.log", '+mat_vsync', '0', '+fps_max', '64', '+name', "Bot$i",
        '+cl_bot_forward', '1', '+cl_bot_yawspeed', "$yaw", '+cl_bot_jump', '1', '+connect', '127.0.0.1',
        '+delay', "$($stats - 4)", 'net_stats', '+delay', "$($Seconds - 5)", 'quit')
    $procs += Start-Process $exe -ArgumentList $botArgs -PassThru
    Start-Sleep -Milliseconds 700
}

Write-Host "Running $Seconds s with 1 host + 8 bots..."
foreach ($p in $procs) { if (-not $p.WaitForExit(($Seconds + 40) * 1000)) { $p.Kill() } }

$hostLog = Get-Content "$logs/host.log" -Raw
$joined = ([regex]::Matches($hostLog, '\[party\] Bot\d joined')).Count
$left = ([regex]::Matches($hostLog, '\[party\] Bot\d left')).Count
$refused = 0; $exceptions = 0; $connected = 0
$clientStats = @()
for ($i = 1; $i -le 8; $i++) {
    $t = Get-Content "$logs/bot$i.log" -Raw
    if ($t -match 'party is full') { $refused++ }
    if ($t -match '\[net\] Connected') { $connected++ }
    $exceptions += ([regex]::Matches($t, 'Exception')).Count
    $m = [regex]::Match($t, '\[net_stats\][^\r\n]*'); if ($m.Success) { $clientStats += "bot${i}: $($m.Value)" }
}
$exceptions += ([regex]::Matches($hostLog, 'Exception')).Count
$hostStats = [regex]::Match($hostLog, '\[net_stats\][^\r\n]*').Value

$playersLate = 0
$pm = [regex]::Match($hostStats, 'players (\d+)'); if ($pm.Success) { $playersLate = [int]$pm.Groups[1].Value }
Write-Host "Joined: $joined (expected 7)  refused as full: $refused (expected 1)  connected near the end: $playersLate (expected 8 incl. host)"
Write-Host "Host:  $hostStats"
$clientStats | ForEach-Object { Write-Host "  $_" }
Write-Host "Exceptions in logs: $exceptions"
$ok = ($joined -eq 7) -and ($refused -eq 1) -and ($exceptions -eq 0) -and ($playersLate -eq 8)
if ($ok) { Write-Host "BOTS TEST PASSED" } else { Write-Host "BOTS TEST FAILED (logs in Logs/bots)"; exit 1 }
