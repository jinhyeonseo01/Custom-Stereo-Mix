#ifndef AppVersion
  #define AppVersion "1.1.0"
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

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "dependencies\MicrosoftEdgeWebview2Setup.exe"; Flags: dontcopy

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[Code]
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
      Result := 'WebView2 Runtime setup could not be started. Install the Runtime and retry.';
      Exit;
    end;
    if not HasWebView2Runtime then
      Result := 'WebView2 Runtime installation did not complete. Check your internet connection, install the Runtime, and retry.';
  end;
end;
