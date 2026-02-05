[Setup]
AppName=Optimizer
AppVersion=1.0.0
DefaultDirName={pf}\Optimizer
OutputDir=Output
OutputBaseFilename=Setup
SetupIconFile=Optimizer\icon.ico
Compression=lzma
SolidCompression=yes

[Files]
Source: "Optimizer\bin\Release\Optimizer.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "Optimizer\bin\Release\Guna.UI2.dll"; DestDir: "{app}"; Flags: ignoreversion
