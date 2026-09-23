; LogRogue 설치 스크립트 (Inno Setup)
;
; 쓰는 법
;   1. https://jrsoftware.org 에서 Inno Setup 설치
;   2. 아래 SourceExe 경로가 실제 게시 결과와 맞는지 확인
;   3. 이 파일을 Inno Setup Compiler로 열고 F9 (Compile)
;   4. installer 폴더에 LogRogue-Setup-x.x.x.exe 생성됨

#define AppName "Log Rogue"
#define AppVersion "1.0.0.0"
#define AppPublisher "Famecs"
#define ExeName "Log Rogue.exe"

; 게시 결과물 위치. dotnet publish 결과 경로와 맞춰야 한다.
#define SourceExe "..\LogRogue.App\bin\Release\net10.0-windows\win-x64\publish\Log Rogue.exe"

[Setup]
; AppId는 이 프로그램을 식별하는 고유 값이다.
; 한 번 정하면 절대 바꾸지 말 것. 바꾸면 기존 설치를 업그레이드하지 못하고 따로 설치된다.
AppId={{8F3A21C7-5D4E-4B9A-9C2F-1E7B6A0D4F58}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\{#ExeName}
OutputDir=installer
OutputBaseFilename={#AppName}-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

; Program Files에 설치하므로 관리자 권한이 필요하다.
; 권한 없이 설치하려면 admin 대신 lowest로 바꾼다. 그러면 사용자 폴더에 설치된다.
PrivilegesRequired=admin

; 64비트 전용으로 빌드했으므로 64비트 Windows에서만 설치되게 한다
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; 프로그램이 실행 중이면 설치를 막고 종료를 안내한다.
; 이름은 Program.cs의 InstanceId와 같아야 한다. (Release 빌드 기준)
AppMutex=LogRogue-3B7E2C91.Instance

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Tasks]
Name: "desktopicon"; Description: "바탕 화면에 바로 가기 만들기"; GroupDescription: "추가 작업:"; Flags: unchecked

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#ExeName}"
Name: "{group}\{#AppName} 제거"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#ExeName}"; Tasks: desktopicon

[Registry]
; 자동 실행 등록은 프로그램 안에서 직접 하므로 설치할 때는 만들지 않는다.
; 다만 제거할 때는 남아 있지 않도록 값을 지운다.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "LogRogue"; ValueType: none; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#ExeName}"; Description: "지금 {#AppName} 실행"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; 설정과 이력, 로그는 남긴다. 다시 설치했을 때 그대로 쓸 수 있도록.
; 함께 지우고 싶으면 아래 줄의 주석을 푼다.
; Type: filesandordirs; Name: "{userappdata}\LogRogue"
