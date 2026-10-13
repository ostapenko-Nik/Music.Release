[Setup]
AppName=VK Music Controller
AppVersion=1.1.2
DefaultDirName={pf}\VKMusicController
DefaultGroupName=VK Music Controller
OutputBaseFilename=VKMusicControllerInstaller
Compression=lzma
SolidCompression=yes
ArchitecturesAllowed=x64

[Files]
Source: "..\src\VKMusicController\bin\Releases\v1.1.2\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\VK Music Controller"; Filename: "{app}\VKMusicController.exe"
Name: "{commondesktop}\VK Music Controller"; Filename: "{app}\VKMusicController.exe"; Tasks: desktopicon

[Tasks]
Name: desktopicon; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"; Flags: unchecked

[Run]
Filename: "{app}\VKMusicController.exe"; Description: "Launch VK Music Controller"; Flags: nowait postinstall skipifsilent
