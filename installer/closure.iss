; Inno Setup 6 스크립트
; 사용: ISCC.exe /DAppVersion=0.1.0 /DSourceDir=<Unity 빌드 폴더> installer\closure.iss
#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\Unity\Builds\Windows"
#endif

[Setup]
; AppId는 업데이트가 같은 설치본을 덮어쓰도록 고정한다. 바꾸지 말 것.
AppId={{B4C7E1D2-6A53-4B0E-9C21-3D5F8A7E2C41}
AppName=폐합 CLOSURE
AppVersion={#AppVersion}
AppPublisher=Closure Prototype
; 사용자 폴더에 설치하므로 관리자 권한 없이 설치·자동 업데이트가 가능하다.
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\Closure
DefaultGroupName=폐합 CLOSURE
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\Closure.exe
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
OutputDir=..\dist
OutputBaseFilename=Closure-Setup-{#AppVersion}

[Languages]
#if FileExists(CompilerPath + "Languages\Korean.isl")
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
#endif
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "바탕 화면에 바로 가기 만들기"; GroupDescription: "추가 작업:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "*_BackUpThisFolder_ButDontShipItWithYourGame\*,*_BurstDebugInformation_DoNotShip\*,*.pdb"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\폐합 CLOSURE"; Filename: "{app}\Closure.exe"
Name: "{autodesktop}\폐합 CLOSURE"; Filename: "{app}\Closure.exe"; Tasks: desktopicon

[Run]
; 직접 설치: 마지막 화면에서 실행 여부를 묻는다.
Filename: "{app}\Closure.exe"; Description: "폐합 CLOSURE 실행"; Flags: nowait postinstall skipifsilent
; 자동 업데이트(/SILENT): 설치가 끝나면 게임을 바로 다시 실행한다.
Filename: "{app}\Closure.exe"; Flags: nowait skipifnotsilent
