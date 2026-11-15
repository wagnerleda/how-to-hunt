<#
.SYNOPSIS
  One-command Windows x64 build of How to Hunt, plus a launch smoke test.

.DESCRIPTION
  Version = <VERSION file>.<git commit count>, e.g. 0.1.0.42. It is also the network protocol version:
  builds with different versions refuse to join each other ("update required").
  If a Unity Editor already has this project open, the build runs inside it; otherwise Unity runs in batch mode.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools/build-windows.ps1
  powershell -ExecutionPolicy Bypass -File tools/build-windows.ps1 -Release -Output Builds/Release
#>
param(
    [switch]$Release,
    [switch]$Il2cpp,
    [switch]$SkipSmokeTest,
    [string]$Output = "Builds/Windows"
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $root

$base = (Get-Content (Join-Path $root 'VERSION') -Raw).Trim()
$count = (& git -C $root rev-list --count HEAD).Trim()
$version = "$base.$count"
$dev = -not $Release
Write-Host "How to Hunt build $version ($(if ($dev) {'development'} else {'release'}), $(if ($Il2cpp) {'IL2CPP'} else {'Mono'})) -> $Output"

# Is an Editor open on this exact project? Then build inside it (batch mode can't open a project that is already open).
$editorOpen = $false
$statusJson = & unity status --project-path $root --format json 2>$null
if ($LASTEXITCODE -eq 0) {
    $status = ($statusJson -join "`n") | ConvertFrom-Json
    foreach ($i in $status.data.items) {
        if ((Resolve-Path $i.projectPath).Path -eq $root -and $i.state -eq 'ready') { $editorOpen = $true }
    }
}

if ($editorOpen) {
    Write-Host "Using the open Unity Editor."
    $outLiteral = $Output.Replace('\', '/')
    $code = "return HowToHunt.Editor.BuildScript.BuildWindows(`"$outLiteral`", `"$version`", $($dev.ToString().ToLower()), $($Il2cpp.IsPresent.ToString().ToLower())) ?? `"BUILD OK`";"
    $result = & unity command script eval --project-path $root --timeout 3600 $code 2>&1
    $result | Select-Object -Last 3 | ForEach-Object { Write-Host $_ }
    if (-not (($result -join "`n") -match 'BUILD OK')) { throw "Build failed (see the Unity console)." }
} else {
    Write-Host "Running Unity in batch mode (log: Logs/build.log)."
    $args = @('run', $root, '--no-tail', '--log-file', (Join-Path $root 'Logs/build.log'), '--',
        '-executeMethod', 'HowToHunt.Editor.BuildScript.BuildWindowsCli', '-buildOutput', $Output, '-buildVersion', $version)
    if ($dev) { $args += '-dev' }
    if ($Il2cpp) { $args += '-il2cpp' }
    & unity @args
    if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE). See Logs/build.log." }
}

$exe = Join-Path $root (Join-Path $Output 'HowToHunt.exe')
if (-not (Test-Path $exe)) { throw "Build output missing: $exe" }
Write-Host "Built $exe"

if (-not $SkipSmokeTest) {
    $log = Join-Path $root 'Logs/smoke.log'
    if (Test-Path $log) { Remove-Item $log }
    Write-Host "Smoke test: launching the build headless for a few seconds..."
    $p = Start-Process -FilePath $exe -ArgumentList @('-batchmode', '-nographics', '-logFile', $log, '+smoke_quit', '4') -PassThru
    if (-not $p.WaitForExit(90000)) { $p.Kill(); throw "Smoke test timed out." }
    $text = if (Test-Path $log) { Get-Content $log -Raw } else { '' }
    if ($text -notmatch 'SMOKE OK') { throw "Smoke test failed: the game did not boot cleanly. See $log" }
    ($text -split "`n" | Where-Object { $_ -match 'SMOKE OK|platform:' }) | ForEach-Object { Write-Host "  $_" }
    Write-Host "Smoke test passed."
}
