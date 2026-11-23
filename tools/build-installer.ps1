<#
.SYNOPSIS
  One command: release build + Windows installer (Inno Setup) => Builds/Installer/HowToHunt-Setup-<version>.exe

.DESCRIPTION
  1. tools/build-windows.ps1 -Release -Il2cpp (versioned IL2CPP build + headless launch smoke test, ADR 0012)
  2. ISCC compiles tools/installer/HowToHunt.iss
  3. -TestInstall: silent per-user install into a temp folder, launch the installed game and join a local host,
     then uninstall and check the folder is gone.
  Installers are published as GitHub Release assets, never committed.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools/build-installer.ps1 -TestInstall
#>
param([switch]$TestInstall, [switch]$SkipBuild, [switch]$Mono)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$buildDir = Join-Path $root 'Builds/Release'
$outDir = Join-Path $root 'Builds/Installer'

# IL2CPP needs the editor's Windows IL2CPP module and the MSVC C++ tools (docs/RELEASE.md, build machine setup).
function Test-Il2cppToolchain {
    $version = ((Select-String -Path (Join-Path $root 'ProjectSettings/ProjectVersion.txt') -Pattern '^m_EditorVersion:').Line -replace '^m_EditorVersion:\s*', '').Trim()
    $editorDirs = @(Join-Path $env:ProgramFiles "Unity\Hub\Editor\$version\Editor")
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $listed = & unity editors 2>$null | ForEach-Object { "$_" } | Where-Object { $_ -match "^$([regex]::Escape($version))\s" }
    $ErrorActionPreference = $previousPreference
    foreach ($line in $listed) { $exe = ($line -split "`t") | Where-Object { $_ -match 'Unity\.exe$' } | Select-Object -First 1; if ($exe) { $editorDirs += (Split-Path $exe) } }
    $il2cpp = $editorDirs | Where-Object { Get-ChildItem (Join-Path $_ 'Data\PlaybackEngines\windowsstandalonesupport\Variations') -Directory -Filter '*il2cpp*' -ErrorAction SilentlyContinue } | Select-Object -First 1
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    $msvc = (Test-Path $vswhere) -and (& $vswhere -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath)
    return [bool]$il2cpp -and [bool]$msvc
}

if (-not $SkipBuild) {
    $useIl2cpp = -not $Mono
    if ($useIl2cpp -and -not (Test-Il2cppToolchain)) {
        Write-Warning "IL2CPP toolchain missing (Unity Windows IL2CPP module and/or Visual Studio C++ build tools, see docs/RELEASE.md). Building the installer with Mono instead."
        $useIl2cpp = $false
    }
    $buildArgs = @('-Release', '-Output', 'Builds/Release')
    if ($useIl2cpp) { $buildArgs += '-Il2cpp' }
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'build-windows.ps1') @buildArgs
    if ($LASTEXITCODE -ne 0) { throw "Release build failed." }
}
$version = (Get-Content (Join-Path $buildDir 'version.txt') -Raw).Trim()

$iscc = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe'
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 not found. Install it: winget install JRSoftware.InnoSetup" }

New-Item -ItemType Directory -Force $outDir | Out-Null
& $iscc "/DAppVersion=$version" "/DBuildDir=$buildDir" "/DOutputDir=$outDir" /Q (Join-Path $PSScriptRoot 'installer\HowToHunt.iss')
if ($LASTEXITCODE -ne 0) { throw "ISCC failed (exit $LASTEXITCODE)." }
$setup = Join-Path $outDir "HowToHunt-Setup-$version.exe"
if (-not (Test-Path $setup)) { throw "Installer missing: $setup" }
Write-Host ("Installer: {0} ({1:N1} MB)" -f $setup, ((Get-Item $setup).Length / 1MB))

if ($TestInstall) {
    $dir = Join-Path $env:TEMP "HowToHunt-InstallTest"
    if (Test-Path $dir) { Remove-Item -Recurse -Force $dir }
    Write-Host "Installing silently to $dir ..."
    $p = Start-Process $setup -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/CURRENTUSER', "/DIR=`"$dir`"", '/TASKS=') -PassThru -Wait
    if ($p.ExitCode -ne 0) { throw "Install failed (exit $($p.ExitCode))." }
    $installedExe = Join-Path $dir 'HowToHunt.exe'
    if (-not (Test-Path $installedExe)) { throw "Installed exe missing." }
    if ((Get-Content (Join-Path $dir 'version.txt') -Raw).Trim() -ne $version) { throw "Installed version mismatch." }

    # Join test: a host from the build folder, the installed game connects to it.
    $logs = Join-Path $root 'Logs/install-test'
    New-Item -ItemType Directory -Force $logs | Out-Null
    Get-ChildItem $logs -Filter *.log | Remove-Item
    $hostProc = Start-Process (Join-Path $buildDir 'HowToHunt.exe') -ArgumentList @('-batchmode', '-nographics', '-nocfg', '-logFile', "`"$logs/host.log`"", '+host', 'local', '+delay', '20', 'quit') -PassThru
    Start-Sleep -Seconds 4
    $client = Start-Process $installedExe -ArgumentList @('-batchmode', '-nographics', '-nocfg', '-logFile', "`"$logs/installed.log`"", '+name', 'InstalledClient', '+connect', '127.0.0.1', '+delay', '10', 'quit') -PassThru
    $client.WaitForExit(60000) | Out-Null
    $hostProc.WaitForExit(60000) | Out-Null
    $hostLog = Get-Content "$logs/host.log" -Raw
    $clientLog = Get-Content "$logs/installed.log" -Raw
    if ($clientLog -notmatch '\[net\] Connected' -or $hostLog -notmatch 'InstalledClient joined') { throw "The installed game did not join the host (see $logs)." }
    Write-Host "Installed game joined a local host: OK"

    Write-Host "Uninstalling..."
    $uninst = Join-Path $dir 'unins000.exe'
    $u = Start-Process $uninst -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') -PassThru -Wait
    Start-Sleep -Seconds 2
    if (Test-Path $installedExe) { throw "Uninstall left the game behind." }
    Write-Host "INSTALL TEST PASSED (install, launch + join, uninstall)"
}
