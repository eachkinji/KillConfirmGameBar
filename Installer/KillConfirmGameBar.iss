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
Name: "chinesesimplified"; MessagesFile: "ChineseSimplified.isl"

[CustomMessages]
english.OpenXboxGameBar=Open Xbox Game Bar
chinesesimplified.OpenXboxGameBar=打开 Xbox Game Bar
english.InstallingOverlay=Installing Kill Confirm Overlay...
chinesesimplified.InstallingOverlay=正在安装击杀确认悬浮窗...
english.InstallScriptLaunchFailed=Could not start the installer script.
chinesesimplified.InstallScriptLaunchFailed=无法启动安装脚本。
english.InstallScriptFailed=Install script failed. See %TEMP%\KillConfirmGameBar_Install.log. Exit code:
chinesesimplified.InstallScriptFailed=安装脚本失败。请查看 %TEMP%\KillConfirmGameBar_Install.log。退出代码：

[InstallDelete]
Type: filesandordirs; Name: "{app}\Payload"

[Files]
Source: "{#TransferRoot}\*"; DestDir: "{app}\Payload"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{cm:OpenXboxGameBar}"; Filename: "explorer.exe"; Parameters: "ms-gamebar:"

[UninstallRun]
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""Get-Process -Name cskillconfirm,TestXboxGameBar,KillConfirmOverlay,KillConfirmGameBar,GameBar,GameBarFTServer,GameBarPresenceWriter -ErrorAction SilentlyContinue | Stop-Process -Force; Start-Sleep -Milliseconds 800; Get-AppxPackage KillConfirmGameBar.Overlay | Remove-AppxPackage -ErrorAction SilentlyContinue; CheckNetIsolation.exe LoopbackExempt -d -n=\""KillConfirmGameBar.Overlay_5jgcw66eyez0m\"" 2>$null"""; Flags: runhidden waituntilterminated; RunOnceId: "RemoveAppxPackage"

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  Params: String;
begin
  if CurStep = ssPostInstall then
  begin
    WizardForm.StatusLabel.Caption := ExpandConstant('{cm:InstallingOverlay}');
    Params := '-NoProfile -ExecutionPolicy Bypass -File "' + ExpandConstant('{app}\Payload\Install-KillConfirm.ps1') + '"';

    if not Exec('powershell.exe', Params, ExpandConstant('{app}\Payload'), SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      MsgBox(ExpandConstant('{cm:InstallScriptLaunchFailed}'), mbError, MB_OK);
      Abort;
    end;

    if ResultCode <> 0 then
    begin
      MsgBox(ExpandConstant('{cm:InstallScriptFailed}') + ' ' + IntToStr(ResultCode), mbError, MB_OK);
      Abort;
    end;
  end;
end;
