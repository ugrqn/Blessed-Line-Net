#define AppName "BlessedLine"
#define AppVersion "0.4.1"

[Setup]
AppId={{A8CD5EE4-5B28-471E-9C1B-06155D13D696}
AppName={#AppName}
AppVersion={#AppVersion}
DefaultDirName={autopf}\BlessedLine
DefaultGroupName=BlessedLine
SetupIconFile=..\src\BlessedLine.Desktop\Assets\BlessedLine.ico
UninstallDisplayIcon={app}\BlessedLine.exe
OutputDir=..\artifacts\installer
OutputBaseFilename=BlessedLine-0.4.1-win-x64
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
Compression=lzma2
SolidCompression=yes

[Files]
Source: "..\artifacts\win-x64\Desktop\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\BlessedLine"; Filename: "{app}\BlessedLine.exe"
Name: "{autodesktop}\BlessedLine"; Filename: "{app}\BlessedLine.exe"

[Run]
Filename: "{app}\BlessedLine.exe"; Description: "BlessedLine uygulamasını aç"; Flags: nowait postinstall skipifsilent runasoriginaluser
