; installer.iss
#define MyAppName "PDF Pro"
#ifndef MyAppVersion
  #define MyAppVersion "1.5.9"
#endif
#define MyAppPublisher "HPhat Edition"
#define MyAppExeName "PdfViewerApp.exe"
#define MyPublishDir "src\PdfViewerApp\bin\Release\net8.0-windows10.0.26100.0\win-x64\publish"

[Setup]
AppId={{D3B16A74-7D68-4EA4-BD4D-B66AEFA1FA3C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\{#MyAppName}
DisableDirPage=yes
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=releases
OutputBaseFilename=PDFPro_Setup_v{#MyAppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
; Register in Windows Control Panel (Add/Remove Programs)
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} - {#MyAppPublisher}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Registry]
; Context menu "Merge PDF"
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.pdf\shell\PdfPro.Merge"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Ghép PDF bằng PDF HPhat"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.pdf\shell\PdfPro.Merge"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.pdf\shell\PdfPro.Merge"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.pdf\shell\PdfPro.Merge"; ValueType: string; ValueName: "Position"; ValueData: "Top"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.pdf\shell\PdfPro.Merge\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"" --merge --exit-after-merge"; Flags: uninsdeletekey

; Default PDF Association
Root: HKCU; Subkey: "Software\Classes\Applications\{#MyAppExeName}\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Applications\{#MyAppExeName}"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "PDF Pro - {#MyAppPublisher}"; Flags: uninsdeletekey
