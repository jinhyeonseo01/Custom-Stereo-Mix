#ifndef AppVersion
  #define AppVersion "1.2.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\webview2"
#endif
#ifndef SetupOutputDir
  #define SetupOutputDir "..\artifacts\setup"
#endif
#define AppName "Custom Stereo Mix"
#define AppExe "CustomStereoMix.App.exe"

[Setup]
AppId={{D66D9052-BB87-4CE1-95F8-D01F9A9CB6BA}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Custom Stereo Mix
DefaultDirName={localappdata}\Programs\CustomStereoMix
DefaultGroupName={#AppName}
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#SetupOutputDir}
OutputBaseFilename=CustomStereoMix-{#AppVersion}-Setup-x64
SetupIconFile=..\src\CustomStereoMix.App\Assets\AppIcon.ico
UninstallDisplayIcon={app}\{#AppExe}
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
DisableProgramGroupPage=yes
AllowNoIcons=yes
CloseApplications=no
AppMutex=Local\CustomStereoMix.WebView2
ShowLanguageDialog=yes

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[CustomMessages]
korean.AppLanguage=ko-KR
english.AppLanguage=en-US
japanese.AppLanguage=ja-JP
chinesesimplified.AppLanguage=zh-CN
korean.RuntimeStartError=WebView2 Runtime 설치를 시작하지 못했습니다. 런타임을 설치한 뒤 다시 시도하세요.
english.RuntimeStartError=WebView2 Runtime setup could not be started. Install the Runtime and retry.
japanese.RuntimeStartError=WebView2 Runtimeのインストールを開始できませんでした。ランタイムをインストールして再試行してください。
chinesesimplified.RuntimeStartError=无法启动 WebView2 Runtime 安装程序。请安装运行时后重试。
korean.RuntimeInstallError=WebView2 Runtime 설치가 완료되지 않았습니다. 인터넷 연결을 확인하고 런타임을 설치한 뒤 다시 시도하세요.
english.RuntimeInstallError=WebView2 Runtime installation did not complete. Check your internet connection, install the Runtime, and retry.
japanese.RuntimeInstallError=WebView2 Runtimeのインストールが完了しませんでした。インターネット接続を確認し、ランタイムをインストールして再試行してください。
chinesesimplified.RuntimeInstallError=WebView2 Runtime 安装未完成。请检查网络连接，安装运行时后重试。
korean.LanguageSaveError=앱의 초기 언어 설정을 저장하지 못했습니다.
english.LanguageSaveError=Could not save the app's initial language setting.
japanese.LanguageSaveError=アプリの初期言語設定を保存できませんでした。
chinesesimplified.LanguageSaveError=无法保存应用的初始语言设置。

[UninstallDelete]
Type: files; Name: "{app}\initial-language.json"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "dependencies\MicrosoftEdgeWebview2Setup.exe"; Flags: dontcopy

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    if not SaveStringToFile(ExpandConstant('{app}\initial-language.json'),
      '{"language":"' + ExpandConstant('{cm:AppLanguage}') + '"}', False) then
      RaiseException(ExpandConstant('{cm:LanguageSaveError}'));
end;

function HasRuntimeVersion(RootKey: Integer; Key: String): Boolean;
var Version: String;
begin
  Result := RegQueryStringValue(RootKey, Key, 'pv', Version) and
    (Version <> '') and (Version <> '0.0.0.0');
end;

function HasWebView2Runtime: Boolean;
var Key: String;
begin
  Key := 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';
  Result := HasRuntimeVersion(HKCU, Key) or HasRuntimeVersion(HKLM32, Key) or HasRuntimeVersion(HKLM64, Key);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var ExitCode: Integer;
begin
  Result := '';
  if not HasWebView2Runtime then begin
    ExtractTemporaryFile('MicrosoftEdgeWebview2Setup.exe');
    if not Exec(ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe'), '/silent /install', '',
      SW_HIDE, ewWaitUntilTerminated, ExitCode) then begin
      Result := ExpandConstant('{cm:RuntimeStartError}');
      Exit;
    end;
    if not HasWebView2Runtime then
      Result := ExpandConstant('{cm:RuntimeInstallError}');
  end;
end;
