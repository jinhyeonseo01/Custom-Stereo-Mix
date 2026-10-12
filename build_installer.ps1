param(
    [string]$IsccPath,
    [string]$Version,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'artifacts\setup')
)
$ErrorActionPreference = 'Stop'
if (-not $Version) {
    [xml]$buildProperties = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Directory.Build.props') -Raw
    $Version = [string]$buildProperties.Project.PropertyGroup.Version
}
if ($Version -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') { throw 'Use a numeric release version such as 1.1.0.' }
if (-not $IsccPath) {
    $compiler = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($compiler) { $IsccPath = $compiler.Source }
    else {
        $locations = @(
            (Join-Path ${env:ProgramFiles} 'Inno Setup 7\ISCC.exe'),
            (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
            (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe')
        )
        $IsccPath = $locations | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    }
}
if (-not $IsccPath -or -not (Test-Path -LiteralPath $IsccPath)) {
    throw 'Install Inno Setup 7 or pass -IsccPath with the full path to ISCC.exe.'
}
$compilerFolder = Split-Path -Parent (Resolve-Path -LiteralPath $IsccPath).Path
foreach ($translation in @('Korean.isl', 'Japanese.isl', 'ChineseSimplified.isl')) {
    if (-not (Test-Path -LiteralPath (Join-Path $compilerFolder "Languages\$translation"))) {
        throw "The compiler is missing $translation. Install Inno Setup 7 with its language files."
    }
}
$dependencyFolder = Join-Path $PSScriptRoot 'installer\dependencies'
New-Item -ItemType Directory -Path $dependencyFolder -Force | Out-Null
$bootstrapper = Join-Path $dependencyFolder 'MicrosoftEdgeWebview2Setup.exe'
if (-not (Test-Path -LiteralPath $bootstrapper)) {
    Invoke-WebRequest -Uri 'https://go.microsoft.com/fwlink/p/?LinkId=2124703' -OutFile $bootstrapper
}
$signature = Get-AuthenticodeSignature -LiteralPath $bootstrapper
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Microsoft') {
    throw 'WebView2 Runtime bootstrapper signature validation failed.'
}
$publishFolder = Join-Path $PSScriptRoot 'artifacts\webview2'
& (Join-Path $PSScriptRoot 'tools\build_icons.ps1')
dotnet publish (Join-Path $PSScriptRoot 'src\CustomStereoMix.App\CustomStereoMix.App.csproj') -c Release -r win-x64 --self-contained true -p:Version=$Version --output $publishFolder
if ($LASTEXITCODE -ne 0) { throw "Application publish failed: $LASTEXITCODE" }
& $IsccPath "/DAppVersion=$Version" "/DPublishDir=$publishFolder" "/DSetupOutputDir=$OutputDirectory" (Join-Path $PSScriptRoot 'installer\CustomStereoMix.iss')
if ($LASTEXITCODE -ne 0) { throw "Installer compilation failed: $LASTEXITCODE" }
Write-Output (Join-Path $OutputDirectory "CustomStereoMix-$Version-Setup-x64.exe")
