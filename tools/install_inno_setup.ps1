param([string]$DestinationDirectory = (Join-Path $env:TEMP 'CustomStereoMix-InnoSetup'))
$ErrorActionPreference = 'Stop'
$compilerDirectory = [IO.Path]::GetFullPath($DestinationDirectory)
New-Item -ItemType Directory -Path $compilerDirectory -Force | Out-Null
$compilerInstaller = Join-Path $compilerDirectory 'innosetup-7.1.0-x64.exe'
Invoke-WebRequest -Uri 'https://github.com/jrsoftware/issrc/releases/download/is-7_1_0/innosetup-7.1.0-x64.exe' -OutFile $compilerInstaller -TimeoutSec 120
$compilerHash = (Get-FileHash -LiteralPath $compilerInstaller -Algorithm SHA256).Hash
if ($compilerHash -ne '0362A383ED217D4C4239B5933866DD96D3EB2102737DA92F80F6057A4B40DF2F') {
    throw 'Inno Setup download hash does not match the pinned release.'
}
$compilerSignature = Get-AuthenticodeSignature -LiteralPath $compilerInstaller
if ($compilerSignature.Status -ne 'Valid' -or $compilerSignature.SignerCertificate.Subject -notmatch 'Pyrsys') {
    throw 'Inno Setup publisher signature validation failed.'
}
$compilerProcess = Start-Process -FilePath $compilerInstaller -ArgumentList @(
    '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/CURRENTUSER', '/NOICONS', "/DIR=`"$compilerDirectory`""
) -WindowStyle Hidden -Wait -PassThru
if ($compilerProcess.ExitCode -ne 0) { throw "Inno Setup installation failed: $($compilerProcess.ExitCode)" }
$compilerPath = Join-Path $compilerDirectory 'ISCC.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) { throw 'Inno Setup compiler was not installed.' }
Write-Output $compilerPath
