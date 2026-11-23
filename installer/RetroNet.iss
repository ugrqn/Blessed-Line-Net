#define AppName "RetroNet Control"
#define AppVersion "0.1.0"

[Setup]
AppId={{A8CD5EE4-5B28-471E-9C1B-06155D13D696}
AppName={#AppName}
AppVersion={#AppVersion}
DefaultDirName={autopf}\RetroNet Control
DefaultGroupName=RetroNet Control
OutputDir=..\artifacts\installer
OutputBaseFilename=RetroNet-Control-0.1.0-win-x64
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
Compression=lzma2
SolidCompression=yes

[Files]
Source: "..\artifacts\win-x64\Desktop\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\RetroNet Control"; Filename: "{app}\RetroNet.Desktop.exe"
Name: "{autodesktop}\RetroNet Control"; Filename: "{app}\RetroNet.Desktop.exe"

[Run]
Filename: "{app}\RetroNet.Desktop.exe"; Description: "RetroNet Control'ü aç"; Flags: nowait postinstall skipifsilent
