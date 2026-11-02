; Perenos installer (Inno Setup 6). Built by build\build.ps1:
;   ISCC /DAppVersion=1.2.0 /DSourceDir=..\artifacts\publish\win-x64 /DArch=x64 AppBridge.iss
; Installs per user by default (no administrator rights needed); the first page offers
; installing for all users instead.

#ifndef AppVersion
  #define AppVersion "1.2.0"
#endif
#ifndef Arch
  #define Arch "x64"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\publish\win-" + Arch
#endif

#define AppName "Перенос"
#define AppExe "Perenos.exe"
#define AppUrl "https://github.com/kr0nsch/perenos-ios"

[Setup]
AppId={{26B4ECF7-3BA6-4014-B073-DADCE560F63C}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Perenos contributors
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
AppCopyright=Copyright (c) 2026 Perenos contributors
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}
VersionInfoDescription={#AppName} Setup
; Keep the legacy installation identity and directory so AppBridge upgrades in place.
DefaultDirName={autopf}\AppBridge
UsePreviousAppDir=yes
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
#if Arch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif
MinVersion=10.0.19041
OutputBaseFilename=Perenos-{#AppVersion}-setup-{#Arch}
SetupIconFile=..\src\AppBridge\Assets\AppBridge.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
WizardImageFile=wizard-large-164.bmp,wizard-large-246.bmp,wizard-large-328.bmp
WizardSmallImageFile=wizard-small-55.bmp,wizard-small-83.bmp,wizard-small-110.bmp,wizard-small-138.bmp
LicenseFile=..\LICENSE
Compression=lzma2/ultra64
SolidCompression=yes
CloseApplications=yes
CloseApplicationsFilter=*.exe,*.dll
RestartApplications=no
ShowLanguageDialog=auto
#ifdef Sign
; Passed by build\build.ps1 when a code-signing certificate is configured.
SignTool=signtool
SignedUninstaller=yes
#endif

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"

[CustomMessages]
en.RemoveData=Also remove Perenos's settings, logs and saved App Store sign-in?%n%nDownloaded .ipa files are not touched.
ru.RemoveData=Удалить также настройки «Переноса», журналы и сохранённый вход в App Store?%n%nСкачанные файлы .ipa не затрагиваются.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "portable.txt"

[InstallDelete]
; Restart Manager checks these entries too, including the running legacy executable.
; Only obsolete program files in this installation are removed; user data stays intact.
Type: files; Name: "{app}\AppBridge.exe"
Type: files; Name: "{app}\AppBridge.dll"
Type: files; Name: "{app}\AppBridge.deps.json"
Type: files; Name: "{app}\AppBridge.runtimeconfig.json"
Type: files; Name: "{app}\AppBridge.pri"

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\tools"

[Code]
procedure RemoveLegacyShortcut(const LinkPath: String);
var
  Shell, Link: Variant;
  Target: String;
begin
  if not FileExists(LinkPath) then
    Exit;
  try
    Shell := CreateOleObject('WScript.Shell');
    Link := Shell.CreateShortcut(LinkPath);
    Target := Link.TargetPath;
    { Do not remove a similarly named shortcut pointing to another installation. }
    if CompareText(ExpandFileName(Target), ExpandConstant('{app}\AppBridge.exe')) = 0 then
      DeleteFile(LinkPath);
  except
    Log('Could not inspect a legacy shortcut: ' + LinkPath);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    RemoveLegacyShortcut(ExpandConstant('{autoprograms}\AppBridge.lnk'));
    RemoveLegacyShortcut(ExpandConstant('{autodesktop}\AppBridge.lnk'));
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if (CurUninstallStep = usPostUninstall) and not UninstallSilent then
  begin
    DataDir := ExpandConstant('{localappdata}\AppBridge');
    if DirExists(DataDir) and (MsgBox(CustomMessage('RemoveData'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES) then
      DelTree(DataDir, True, True, True);
  end;
end;
