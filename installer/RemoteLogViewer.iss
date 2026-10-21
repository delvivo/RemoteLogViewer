; Inno Setup script. Build: iscc /DAppVersion=1.2.0 /DSourceDir=..\publish installer\RemoteLogViewer.iss
; AppId must never change: it is what makes a newer setup upgrade the existing install in place.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\publish"
#endif

[Setup]
AppId={{872D1A88-9BF9-49C5-9330-8F810805CB14}
AppName=Remote Log Viewer
AppVersion={#AppVersion}
AppPublisher=Filippo Del Vivo
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\RemoteLogViewer
DefaultGroupName=Remote Log Viewer
DisableProgramGroupPage=yes
; Per-user install (%LOCALAPPDATA%\Programs), no UAC prompt.
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\src\RemoteLogViewer\app.ico
UninstallDisplayIcon={app}\RemoteLogViewer.exe
OutputDir=..\dist
OutputBaseFilename=RemoteLogViewer-v{#AppVersion}-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes

[Languages]
Name: "it"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Remote Log Viewer"; Filename: "{app}\RemoteLogViewer.exe"
Name: "{autodesktop}\Remote Log Viewer"; Filename: "{app}\RemoteLogViewer.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\RemoteLogViewer.exe"; Description: "{cm:LaunchProgram,Remote Log Viewer}"; Flags: nowait postinstall skipifsilent
