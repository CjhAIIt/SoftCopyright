; Inno Setup 脚本：知识产权申请材料工具（桌面版）Windows 安装包。
; 由 scripts/publish-desktop.ps1 -Installer 调用。

#define AppName "知识产权申请材料工具"
#define AppVersion "1.0.0"
#define AppPublisher "SoftCopyright"
#define AppExeName "SoftCopyright.Desktop.exe"

[Setup]
AppId={{8F3C2A11-6D64-4C3E-9E4B-2E1A7B5D9C10}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
OutputDir=..\artifacts\desktop\windows\installer
OutputBaseFilename=SoftCopyright-Desktop-Setup-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; 桌面端是端侧离线应用，安装过程不写注册表、不装服务、不联网。
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#AppExeName}

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务:"; Flags: unchecked

[Files]
Source: "..\artifacts\desktop\windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\卸载 {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "立即启动 {#AppName}"; Flags: nowait postinstall skipifsilent
