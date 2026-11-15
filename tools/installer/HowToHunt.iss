; How to Hunt installer (Inno Setup 6). Built by tools/build-installer.ps1:
;   ISCC.exe /DAppVersion=0.1.0.42 /DBuildDir=<release build folder> /DOutputDir=<output folder> HowToHunt.iss
; Per-user install (no admin rights), Start menu + optional desktop shortcut, uninstaller.
; Player settings and saves live in %USERPROFILE%\AppData\LocalLow\How to Hunt and are kept on uninstall.

#ifndef AppVersion
  #define AppVersion "0.0.0.0"
#endif
#ifndef BuildDir
  #define BuildDir "..\..\Builds\Release"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\Builds\Installer"
#endif

[Setup]
; Never change AppId: it is how upgrades find the previous install.
AppId={{E580E18E-2EF2-475B-9E8A-6C45FD25DB23}
AppName=How to Hunt
AppVersion={#AppVersion}
AppVerName=How to Hunt {#AppVersion}
AppPublisher=How to Hunt
VersionInfoVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\How to Hunt
DefaultGroupName=How to Hunt
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=HowToHunt-Setup-{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\HowToHunt.exe
UninstallDisplayName=How to Hunt
CloseApplications=yes
MinVersion=10.0

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"

[InstallDelete]
; Remove the previous version's game data so stale files never mix with the new build.
Type: filesandordirs; Name: "{app}\HowToHunt_Data"
Type: filesandordirs; Name: "{app}\MonoBleedingEdge"

[Files]
Source: "{#BuildDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\How to Hunt"; Filename: "{app}\HowToHunt.exe"
Name: "{autoprograms}\Uninstall How to Hunt"; Filename: "{uninstallexe}"
Name: "{autodesktop}\How to Hunt"; Filename: "{app}\HowToHunt.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\HowToHunt.exe"; Description: "Play How to Hunt"; Flags: nowait postinstall skipifsilent

[Messages]
WelcomeLabel2=This will install How to Hunt {#AppVersion}.%n%nYou need the Steam client running to host or join parties with friends. Solo play works without Steam.
