param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'artifacts\webview2'), [switch]$RegenerateIcons)
$ErrorActionPreference = "Stop"
if ($RegenerateIcons) { & "$PSScriptRoot\tools\build_icons.ps1" }

dotnet publish "$PSScriptRoot\src\CustomStereoMix.App\CustomStereoMix.App.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    --output $OutputDirectory

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "Build finished:"
Write-Host (Join-Path $OutputDirectory "CustomStereoMix.App.exe")
