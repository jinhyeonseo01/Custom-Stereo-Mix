$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location (Join-Path $projectRoot 'ui')
try {
    $dependencyLock = Join-Path (Get-Location) 'node_modules/.package-lock.json'
    $sourceLock = Join-Path (Get-Location) 'package-lock.json'
    if (-not (Test-Path -LiteralPath $dependencyLock) -or
        (Get-Item -LiteralPath $sourceLock).LastWriteTimeUtc -gt (Get-Item -LiteralPath $dependencyLock).LastWriteTimeUtc -or
        -not (Test-Path -LiteralPath 'node_modules/sharp/package.json') -or
        -not (Test-Path -LiteralPath 'node_modules/lucide-static/icons/audio-lines.svg')) {
        npm ci --no-audit --no-fund
        if ($LASTEXITCODE -ne 0) { throw 'Frontend dependency restore failed.' }
    }
    node (Join-Path $PSScriptRoot 'build-icons.mjs')
    if ($LASTEXITCODE -ne 0) { throw 'Icon generation failed.' }
}
finally { Pop-Location }
