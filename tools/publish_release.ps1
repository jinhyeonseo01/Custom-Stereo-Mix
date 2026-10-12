param(
    [Parameter(Mandatory)][string]$Tag,
    [Parameter(Mandatory)][string]$Repository,
    [string]$SetupDirectory = (Join-Path $PSScriptRoot '../artifacts/setup')
)
$ErrorActionPreference = 'Stop'
if ($Tag -cnotmatch '^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z') { throw 'Use a vX.Y.Z release tag.' }
$releaseVersion = [Version]::Parse($Tag.Substring(1))
if ($releaseVersion.Major -gt 65535 -or $releaseVersion.Minor -gt 65535 -or $releaseVersion.Build -gt 65535) { throw 'Version component exceeds 65535.' }
if ($Repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') { throw 'Use owner/repository.' }
$installerName = "CustomStereoMix-$releaseVersion-Setup-x64.exe"
$installerPath = Join-Path $SetupDirectory $installerName
$checksumPath = "$installerPath.sha256"
if (-not (Test-Path -LiteralPath $installerPath) -or -not (Test-Path -LiteralPath $checksumPath)) { throw 'Installer and checksum are required.' }
$installerHash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ((Get-Content -LiteralPath $checksumPath -Raw).Trim() -cne "$installerHash  $installerName") { throw 'Installer checksum mismatch.' }

$releaseJson = gh api "repos/$Repository/releases/tags/$Tag"
if ($LASTEXITCODE -ne 0) {
    gh release create $Tag --repo $Repository --draft --verify-tag --generate-notes --title $Tag
    if ($LASTEXITCODE -ne 0) { throw 'Draft release creation failed.' }
    $releaseJson = gh api "repos/$Repository/releases/tags/$Tag"
    if ($LASTEXITCODE -ne 0) { throw 'Draft release lookup failed.' }
}
$release = $releaseJson | ConvertFrom-Json
if (-not $release.draft -or $release.prerelease -or $release.tag_name -cne $Tag) {
    throw 'The tag already has a published or non-matching release. Use a new version tag.'
}
# Upload completely before publication so the updater cannot see a partial release.
gh release upload $Tag $installerPath $checksumPath --repo $Repository --clobber
if ($LASTEXITCODE -ne 0) { throw 'Release asset upload failed; the draft was kept for retry.' }
$publishPayloadFile = Join-Path ([IO.Path]::GetTempPath()) ("stereomix-release-" + [Guid]::NewGuid().ToString('N') + '.json')
try {
    @{ draft = $false; make_latest = 'legacy' } | ConvertTo-Json | Set-Content -LiteralPath $publishPayloadFile -Encoding utf8
    gh api --method PATCH "repos/$Repository/releases/$($release.id)" --input $publishPayloadFile --silent
    if ($LASTEXITCODE -ne 0) { throw 'Release publication failed; rerun to finish the draft.' }
}
finally { Remove-Item -LiteralPath $publishPayloadFile -Force -ErrorAction SilentlyContinue }
Write-Output "Published https://github.com/$Repository/releases/tag/$Tag"
