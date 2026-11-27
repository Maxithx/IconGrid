#define MyAppName "IconGrid"
#ifndef MyAppVersion
  #define MyAppVersion "0.7.0-beta.6"
#endif
#ifndef MyAppFileVersion
  #define MyAppFileVersion "0.7.0.6"
#endif
#define MyAppPublisher "Maxithx"
#define MyAppURL "https://github.com/Maxithx/IconGrid"
#define MyAppExeName "IconGrid.exe"

[Setup]
AppId={{7A4B9C2E-3D5F-4E6A-9B1C-2D3E4F5A6B7C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
VersionInfoVersion={#MyAppFileVersion}
VersionInfoProductName={#MyAppName}
VersionInfoDescription={#MyAppName} installer
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=..\.local-state\installer\output
OutputBaseFilename=IconGrid-{#MyAppVersion}-Setup
SetupIconFile=..\Assets\dlb-icon.ico
WizardSmallImageFile=wizard-small.png
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
LicenseFile=..\LICENSE
CloseApplications=yes
RestartApplications=no
AppMutex=Local\IconGrid.Launcher.SingleInstance
UsePreviousAppDir=yes
AllowNoIcons=yes
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\.local-state\installer\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""$g = Get-LocalGroup -SID 'S-1-5-32-559' -ErrorAction SilentlyContinue; if ($g) {{ Add-LocalGroupMember -Group $g -Member '{username}' -ErrorAction SilentlyContinue }}"""; Flags: runhidden waituntilterminated; StatusMsg: "Adding the current user to Performance Log Users..."
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
