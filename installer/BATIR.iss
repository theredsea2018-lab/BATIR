#define AppName "BATIR"
#define AppVersion "1.0.0"
#define AppPublisher "BATIR"
#define AppExeName "BATIR.exe"

[Setup]
AppId={{7F4C4D20-8E5E-4A6D-9E4D-BATIR2026}}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\BATIR
DefaultGroupName=BATIR
OutputDir=..\installer-output
OutputBaseFilename=BATIR-Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
PrivilegesRequired=admin
UninstallDisplayName=BATIR | باتیر

[Files]
Source: "..\src\BATIR\bin\Any CPU\Release\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{autodesktop}\BATIR"; Filename: "{app}\{#AppExeName}"
Name: "{group}\BATIR"; Filename: "{app}\{#AppExeName}"

[Run]
Filename: "{app}\{#AppExeName}"; Description: "اجرای BATIR"; Flags: nowait postinstall skipifsilent
