#define MyAppName "NetMind"
#define MyAppVersion "1.7.6"
#define MyAppPublisher "NetMind"
#define MyAppExeName "NetMind.WebApi.exe"

#ifnexist "publish\agent\src\agent_kernel.py"
  #error "Missing publish\agent\src\agent_kernel.py. Add the agent folder before compiling installer."
#endif

#ifnexist "AI文档\SQL\Init.sql"
  #error "Missing AI文档\SQL\Init.sql."
#endif

[Setup]
AppId={{A6F9C6AC-1D5E-47E7-85EE-0B7FA2F124A9}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\NetMind
DefaultGroupName=NetMind
DisableProgramGroupPage=yes
OutputDir=artifacts\installer
OutputBaseFilename=NetMind-Setup-{#MyAppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Files]
Source: "publish\netmind\*"; DestDir: "{app}\netmind"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "publish\NetMind.Frontend\dist\*"; DestDir: "{app}\NetMind.Frontend\dist"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "publish\agent\*"; DestDir: "{app}\agent"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "AI文档\SQL\*.sql"; DestDir: "{app}\SQL"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\NetMind"; Filename: "{app}\netmind\{#MyAppExeName}"; WorkingDir: "{app}\netmind"
Name: "{autodesktop}\NetMind"; Filename: "{app}\netmind\{#MyAppExeName}"; WorkingDir: "{app}\netmind"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create desktop shortcut"; GroupDescription: "Additional icons:"

[Run]
Filename: "{app}\netmind\{#MyAppExeName}"; Description: "Launch NetMind"; WorkingDir: "{app}\netmind"; Flags: nowait postinstall skipifsilent
