; Perenos installer (Inno Setup 6). Built by build\build.ps1:
;   ISCC /DAppVersion=1.7.1 /DSourceDir=..\artifacts\publish\win-x64 /DArch=x64 Perenos.iss
; Installs per user by default (no administrator rights needed); the first page offers
; installing for all users instead.

#ifndef AppVersion
  #define AppVersion "1.7.1"
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
; Keep the installation identity for upgrades. Only the legacy default folder is renamed.
DefaultDirName={autopf}\Perenos
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
SetupIconFile=..\src\Perenos\Assets\Perenos.ico
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
SignTool=signtool
SignedUninstaller=yes
#endif

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"

[CustomMessages]
en.RemoveData=Also remove Perenos's settings, logs and saved App Store sign-in, including data from previous versions?%n%nDownloaded .ipa files are not touched.
ru.RemoveData=Удалить также настройки «Переноса», журналы и сохранённый вход в App Store, включая данные прежних версий?%n%nСкачанные файлы .ipa не затрагиваются.
en.MigrationFailed=Perenos could not move the previous installation folder. Close the previous app and install again, or choose another folder.
ru.MigrationFailed=Не удалось перенести папку прежней установки. Закройте прежнее приложение и повторите установку или выберите другую папку.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "portable.txt"

[InstallDelete]
Type: files; Name: "{app}\Perenos.exe"
Type: files; Name: "{app}\Perenos.dll"
Type: files; Name: "{app}\Perenos.deps.json"
Type: files; Name: "{app}\Perenos.runtimeconfig.json"
Type: files; Name: "{app}\Perenos.pri"
Type: files; Name: "{app}\Perenos.Core.dll"

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\tools"

[Code]
#include "Migration.inc"

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
    if CompareText(ExpandFileName(Target), ExpandConstant('{app}\AppBridge.exe')) = 0 then
      DeleteFile(LinkPath);
  except
    Log('Could not inspect a legacy shortcut: ' + LinkPath);
  end;
end;

procedure MigratePreviousInstallation;
var
  PreviousDir, NewDir: String;
begin
  PreviousDir := ExpandConstant('{autopf}\AppBridge');
  NewDir := ExpandConstant('{autopf}\Perenos');
  if CompareText(ExpandConstant('{app}'), NewDir) <> 0 then
    Exit;
  MovePreviousInstallationDirectory(PreviousDir, NewDir);
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if (CurPageID = wpSelectDir) and
     (CompareText(WizardForm.DirEdit.Text, ExpandConstant('{autopf}\AppBridge')) = 0) then
    WizardForm.DirEdit.Text := ExpandConstant('{autopf}\Perenos');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    MigratePreviousInstallation;
  if CurStep = ssPostInstall then
  begin
    RemoveLegacyShortcut(ExpandConstant('{autoprograms}\AppBridge.lnk'));
    RemoveLegacyShortcut(ExpandConstant('{autodesktop}\AppBridge.lnk'));
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir, LegacyDataDir: String;
begin
  if (CurUninstallStep = usPostUninstall) and not UninstallSilent then
  begin
    DataDir := ExpandConstant('{localappdata}\Perenos');
    LegacyDataDir := ExpandConstant('{localappdata}\AppBridge');
    if (DirExists(DataDir) or DirExists(LegacyDataDir)) and
       (MsgBox(CustomMessage('RemoveData'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES) then
    begin
      if DirExists(DataDir) then
        DelTree(DataDir, True, True, True);
      if DirExists(LegacyDataDir) then
        DelTree(LegacyDataDir, True, True, True);
    end;
  end;
end;
