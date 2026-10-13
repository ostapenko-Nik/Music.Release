#define AppVersion "1.1.2"

[Setup]
AppId={{EDB408E9-4985-41F6-B671-89AD67A983B8}
AppName=PlayerForGame
AppVersion={#AppVersion}
VersionInfoVersion=1.1.2.0
VersionInfoTextVersion=1.1.2
VersionInfoDescription=PlayerForGame Setup
AppPublisher=ostapenko-Nik
AppPublisherURL=https://github.com/ostapenko-Nik/Music.Release
AppSupportURL=https://github.com/ostapenko-Nik/Music.Release/issues
AppUpdatesURL=https://github.com/ostapenko-Nik/Music.Release/releases/latest
DefaultDirName={localappdata}\Programs\PlayerForGame
DefaultGroupName=PlayerForGame
PrivilegesRequired=lowest
MinVersion=10.0.19041
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
DisableDirPage=no
OutputDir=..\src\VKMusicController\bin\Releases\v{#AppVersion}\installer
OutputBaseFilename=PlayerForGame-v{#AppVersion}-Setup-x64
SetupIconFile=..\src\VKMusicController\Assets\app.ico
UninstallDisplayIcon={app}\VKMusicController.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\src\VKMusicController\bin\Releases\v{#AppVersion}\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "INSTALL.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\PlayerForGame"; Filename: "{app}\VKMusicController.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\PlayerForGame"; Filename: "{app}\VKMusicController.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Tasks]
Name: desktopicon; Description: "Создать ярлык на рабочем столе / Create a desktop shortcut"; Flags: unchecked

[Run]
Filename: "{app}\VKMusicController.exe"; Description: "Запустить PlayerForGame / Launch PlayerForGame"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent
