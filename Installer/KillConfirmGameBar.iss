#define MyAppName "Kill Confirm Overlay"
#define MyAppPublisher "KillConfirmGameBar"
#define MyAppExeName "Install-KillConfirm.ps1"

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0.0"
#endif

#ifndef TransferRoot
  #define TransferRoot "..\..\KillConfirmGameBar_Transfer_1.0.0.0"
#endif

[Setup]
AppId={{E0DF6407-CB2E-43D0-8B51-8C8924F50AA1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Kill Confirm Overlay
DefaultGroupName=Kill Confirm Overlay
DisableProgramGroupPage=yes
OutputDir=..\Output
OutputBaseFilename=KillConfirmGameBar_Setup_{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[InstallDelete]
Type: filesandordirs; Name: "{app}\Payload"

[Files]
Source: "{#TransferRoot}\*"; DestDir: "{app}\Payload"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Open Xbox Game Bar"; Filename: "explorer.exe"; Parameters: "ms-gamebar:"

[Run]
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\Payload\Install-KillConfirm.ps1"""; WorkingDir: "{app}\Payload"; StatusMsg: "Installing Kill Confirm Overlay..."; Flags: runhidden waituntilterminated

[UninstallRun]
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""Get-Process cskillconfirm -ErrorAction SilentlyContinue | Stop-Process -Force; Get-AppxPackage KillConfirmGameBar.Overlay | Remove-AppxPackage -ErrorAction SilentlyContinue; CheckNetIsolation LoopbackExempt -d -n=KillConfirmGameBar.Overlay_5jgcw66eyez0m 2>$null"""; Flags: runhidden waituntilterminated; RunOnceId: "RemoveAppxPackage"
