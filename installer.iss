[Setup]
AppName=Optimizer
AppVersion=1.0.0
DefaultDirName={autopf}\Optimizer
DefaultGroupName=Optimizer
UninstallDisplayIcon={app}\Optimizer.exe
Compression=lzma
SolidCompression=yes
OutputDir=setup_output
OutputBaseFilename=Optimizer-v1-Setup

; --- ADMIN RIGHTS ---
; This makes the INSTALLER run as admin
PrivilegesRequired=admin

; --- ICON ---
; Ensure "app_icon.ico" is in your GitHub repo root folder
SetupIconFile=app_icon.ico 

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "publish\Optimizer.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\Guna.UI2.dll"; DestDir: "{app}"; Flags: ignoreversion
; Include the icon file in the installation folder so shortcuts can use it
Source: "app_icon.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Shortcuts now use the specific icon file installed to the folder
Name: "{group}\Optimizer"; Filename: "{app}\Optimizer.exe"; IconFilename: "{app}\app_icon.ico"
Name: "{autodesktop}\Optimizer"; Filename: "{app}\Optimizer.exe"; Tasks: desktopicon; IconFilename: "{app}\app_icon.ico"

[Registry]
; This tells Windows to ALWAYS run this specific EXE as Administrator
Root: "HKLM"; Subkey: "SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"; \
    ValueType: String; ValueName: "{app}\Optimizer.exe"; ValueData: "~ RUNASADMIN"; \
    Flags: uninsdeletevalue

[Run]
; The "runascurrentuser" flag ensures that if the installer is admin, the app starts as admin
Filename: "{app}\Optimizer.exe"; Description: "{cm:LaunchProgram,Optimizer}"; Flags: nowait postinstall skipifsilent runascurrentuser
