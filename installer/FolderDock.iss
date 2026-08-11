; FolderDock — Inno Setup installer script.
; Compiled in CI with:
;   ISCC.exe installer\FolderDock.iss /DVersion=<semver> /DArch=<x64|arm64> /DSourceDir=<publish dir>

#ifndef Version
  #define Version "0.0.0"
#endif
#ifndef Arch
  #define Arch "x64"
#endif
#ifndef SourceDir
  #define SourceDir "..\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish"
#endif

#define MyAppName "FolderDock"
#define MyAppExe "FolderDock.exe"
#define MyAppPublisher "zkelo"
#define MyAppURL "https://github.com/zkelo/FolderDock"

[Setup]
AppId={{7B7C2A4E-6F1D-4A0B-9E64-3C55D1A9F0DC}
AppName={#MyAppName}
AppVersion={#Version}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases

; Путь установки: по умолчанию системная Program Files, страница выбора включена
DefaultDirName={autopf}\{#MyAppName}
DisableDirPage=no

; Меню «Пуск»: страница выбора группы + чекбокс «Не создавать папку»
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=no
AllowNoIcons=yes

; Лицензия (MIT) — страница принятия лицензии
LicenseFile=..\LICENSE

OutputDir=..
OutputBaseFilename=FolderDock-Setup-{#Version}-{#Arch}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\{#MyAppExe}
UninstallDisplayName={#MyAppName}

#if Arch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Comment: "FolderDock — folders on the taskbar"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[Code]
// Тихое удаление ранее установленной версии перед установкой новой.
// Inno Setup регистрирует деинсталлятор под ключом "<AppId>_is1".
const
  UninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{7B7C2A4E-6F1D-4A0B-9E64-3C55D1A9F0DC}_is1';

function GetUninstallString(): String;
begin
  Result := '';
  if not RegQueryStringValue(HKLM, UninstallKey, 'UninstallString', Result) then
    RegQueryStringValue(HKCU, UninstallKey, 'UninstallString', Result);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Uninst: String;
  ResultCode, Waited: Integer;
begin
  Result := '';
  Uninst := GetUninstallString();
  if Uninst = '' then
    Exit; // предыдущей установки нет

  // Закрыть работающее приложение, иначе файлы будут заняты
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#MyAppExe}', '',
    SW_HIDE, ewWaitUntilTerminated, ResultCode);

  Uninst := RemoveQuotes(Uninst);
  if not Exec(Uninst, '/VERYSILENT /NORESTART /SUPPRESSMSGBOXES', '',
    SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    Result := 'Failed to remove the previous version (exit code ' + IntToStr(ResultCode) + ').';
    Exit;
  end;

  // Деинсталлятор порождает копию (_iu14D.tmp) и завершается раньше,
  // чем реально удалены файлы — ждём исчезновения unins*.exe (до 30 с)
  Waited := 0;
  while FileExists(Uninst) and (Waited < 30000) do
  begin
    Sleep(250);
    Waited := Waited + 250;
  end;
end;
