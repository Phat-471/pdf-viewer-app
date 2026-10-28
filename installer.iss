; installer.iss
#define MyAppName "PDF Pro"
#ifndef MyAppVersion
  #define MyAppVersion "1.6.2"
#endif
#define MyAppPublisher "HPhat Edition"
#define MyAppExeName "PdfViewerApp.exe"
#define MyPublishDir "src\PdfViewerApp\bin\Release\net8.0-windows10.0.26100.0\win-x64\publish"

[Setup]
AppId={{D3B16A74-7D68-4EA4-BD4D-B66AEFA1FA3C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
VersionInfoVersion={#MyAppVersion}.0
VersionInfoProductVersion={#MyAppVersion}.0
VersionInfoTextVersion={#MyAppVersion}
VersionInfoProductTextVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={code:GetDefaultInstallDir}
PrivilegesRequired=lowest
CloseApplications=yes
RestartApplications=no
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
Root: HKCU; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".pdf"; ValueData: ""; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".png"; ValueData: ""; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".jpg"; ValueData: ""; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".jpeg"; ValueData: ""; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".jfif"; ValueData: ""; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".bmp"; ValueData: ""; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".webp"; ValueData: ""; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".tiff"; ValueData: ""; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".tif"; ValueData: ""; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".gif"; ValueData: ""; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".ico"; ValueData: ""; Flags: uninsdeletekey

; App Paths registration for Windows shell execution
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\{#MyAppExeName}"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\{#MyAppExeName}"; ValueType: string; ValueName: "Path"; ValueData: "{app}"; Flags: uninsdeletekey

; Define ProgID for PDF files
Root: HKCU; Subkey: "Software\Classes\PDFPro.PDF"; ValueType: string; ValueName: ""; ValueData: "PDF Document"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\PDFPro.PDF\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\PDFPro.PDF\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey

; Associate PDF extension with our ProgID under OpenWithProgids
Root: HKCU; Subkey: "Software\Classes\.pdf\OpenWithProgids"; ValueType: none; ValueName: "PDFPro.PDF"; Flags: uninsdeletevalue

; Define ProgID for Image files
Root: HKCU; Subkey: "Software\Classes\PDFPro.Image"; ValueType: string; ValueName: ""; ValueData: "Tệp hình ảnh"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\PDFPro.Image\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\PDFPro.Image\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey

; Context menu for images
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\image\shell\PdfPro.Open"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Mở bằng PDF Pro"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\image\shell\PdfPro.Open"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\image\shell\PdfPro.Open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey

; Associate image extensions under OpenWithProgids
Root: HKCU; Subkey: "Software\Classes\.png\OpenWithProgids"; ValueType: none; ValueName: "PDFPro.Image"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.jpg\OpenWithProgids"; ValueType: none; ValueName: "PDFPro.Image"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.jpeg\OpenWithProgids"; ValueType: none; ValueName: "PDFPro.Image"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.jfif\OpenWithProgids"; ValueType: none; ValueName: "PDFPro.Image"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.bmp\OpenWithProgids"; ValueType: none; ValueName: "PDFPro.Image"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.webp\OpenWithProgids"; ValueType: none; ValueName: "PDFPro.Image"; Flags: uninsdeletevalue

; Modern Windows Default App Capabilities Registration
Root: HKCU; Subkey: "Software\PDFPro\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "PDF Pro"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\PDFPro\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "Bộ công cụ xem và xử lý PDF chuyên nghiệp"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\PDFPro\Capabilities\FileAssociations"; ValueType: string; ValueName: ".pdf"; ValueData: "PDFPro.PDF"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\PDFPro\Capabilities\FileAssociations"; ValueType: string; ValueName: ".png"; ValueData: "PDFPro.Image"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\PDFPro\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jpg"; ValueData: "PDFPro.Image"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\PDFPro\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jpeg"; ValueData: "PDFPro.Image"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\PDFPro\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jfif"; ValueData: "PDFPro.Image"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\PDFPro\Capabilities\FileAssociations"; ValueType: string; ValueName: ".bmp"; ValueData: "PDFPro.Image"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\PDFPro\Capabilities\FileAssociations"; ValueType: string; ValueName: ".webp"; ValueData: "PDFPro.Image"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\PDFPro\Capabilities\FileAssociations"; ValueType: string; ValueName: ".tiff"; ValueData: "PDFPro.Image"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\PDFPro\Capabilities\FileAssociations"; ValueType: string; ValueName: ".tif"; ValueData: "PDFPro.Image"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\PDFPro\Capabilities\FileAssociations"; ValueType: string; ValueName: ".gif"; ValueData: "PDFPro.Image"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\PDFPro\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ico"; ValueData: "PDFPro.Image"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\RegisteredApplications"; ValueType: string; ValueName: "PDF Pro"; ValueData: "Software\PDFPro\Capabilities"; Flags: uninsdeletevalue

; Open With list support
Root: HKCU; Subkey: "Software\Classes\.pdf\OpenWithList\{#MyAppExeName}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\.png\OpenWithList\{#MyAppExeName}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\.jpg\OpenWithList\{#MyAppExeName}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\.jpeg\OpenWithList\{#MyAppExeName}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\.jfif\OpenWithList\{#MyAppExeName}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\.bmp\OpenWithList\{#MyAppExeName}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\.webp\OpenWithList\{#MyAppExeName}"; Flags: uninsdeletekey

; Upgrade in place. Never delete the installed app or registry keys during wizard initialization.
[Code]
function GetDefaultInstallDir(Param: String): String;
begin
  Result := ExpandConstant('{localappdata}\PDF Pro');
  if (not DirExists(Result)) and DirExists(ExpandConstant('{localappdata}\PDFPro')) then
    Result := ExpandConstant('{localappdata}\PDFPro');
end;
