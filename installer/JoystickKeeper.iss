; Per-user installer: no admin prompt, and the in-app updater can replace the exe in place.
#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

[Setup]
AppId={{6C2E8F41-9B7A-4D3E-A5C1-2F8D7E4B9A10}
AppName=Joystick Keeper
AppVersion={#AppVersion}
AppPublisher=pikkon38
AppPublisherURL=https://github.com/pikkon38/JoystickKeeper
DefaultDirName={localappdata}\Programs\JoystickKeeper
DefaultGroupName=Joystick Keeper
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\publish
OutputBaseFilename=JoystickKeeper-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\JoystickKeeper.exe
SetupIconFile=..\src\JoystickKeeper\JoystickKeeper.ico
CloseApplications=yes

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"

[Files]
Source: "..\publish\JoystickKeeper.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Joystick Keeper"; Filename: "{app}\JoystickKeeper.exe"
Name: "{group}\Uninstall Joystick Keeper"; Filename: "{uninstallexe}"
Name: "{userdesktop}\Joystick Keeper"; Filename: "{app}\JoystickKeeper.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\JoystickKeeper.exe"; Description: "Open Joystick Keeper"; Flags: nowait postinstall skipifsilent

; Your saved setup and bindings backups in %APPDATA%\JoystickKeeper are left in place on uninstall.
