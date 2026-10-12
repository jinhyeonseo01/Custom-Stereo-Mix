param(
    [Parameter(Mandatory)][string]$Tag,
    [Parameter(Mandatory)][string]$Repository,
    [string]$SetupDirectory = (Join-Path $PSScriptRoot '../artifacts/setup'),
    [long]$ReleaseId = 0
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

function Invoke-ReleaseJsonRequest([string]$Method, [string]$Endpoint, [hashtable]$Body) {
    $payloadFile = Join-Path ([IO.Path]::GetTempPath()) ("stereomix-release-" + [Guid]::NewGuid().ToString('N') + '.json')
    try {
        $Body | ConvertTo-Json | Set-Content -LiteralPath $payloadFile -Encoding utf8
        $response = gh api --method $Method $Endpoint --input $payloadFile
        if ($LASTEXITCODE -ne 0) { throw "Release $Method request failed. Existing drafts and assets were kept for retry." }
        return ($response -join "`n") | ConvertFrom-Json
    }
    finally { Remove-Item -LiteralPath $payloadFile -Force -ErrorAction SilentlyContinue }
}

if ($ReleaseId -gt 0) {
    $releaseJson = gh api "repos/$Repository/releases/$ReleaseId"
    if ($LASTEXITCODE -ne 0) { throw 'The specified release ID could not be read.' }
    $release = ($releaseJson -join "`n") | ConvertFrom-Json
}
else {
    # Only check this tag's published release. Draft recovery is explicit by ID,
    # avoiding full-list scans and token-dependent draft discovery.
    $lookup = gh api --include --silent "repos/$Repository/releases/tags/$Tag" 2>&1
    if ($LASTEXITCODE -eq 0) { throw 'The tag already has a published release. Use a new version tag.' }
    if (-not ($lookup | Where-Object { "$_" -match '^HTTP/[\d.]+ 404(?:\s|$)' })) { throw 'Release lookup failed; no draft was created.' }
    $release = $null
}
if ($null -eq $release) {
    gh api "repos/$Repository/git/ref/tags/$Tag" --silent
    if ($LASTEXITCODE -ne 0) { throw 'Release tag does not exist in the remote repository.' }
    # Keep the authoritative POST response: GITHUB_TOKEN can create a draft even
    # when a subsequent list/tag lookup cannot see that unpublished release.
    $release = Invoke-ReleaseJsonRequest POST "repos/$Repository/releases" @{
        tag_name = $Tag; name = $Tag; draft = $true; prerelease = $false; generate_release_notes = $true
    }
}
if (-not $release.draft -or $release.prerelease -or $release.tag_name -cne $Tag) {
    throw 'The tag already has a published or non-matching release. Use a new version tag.'
}
if (($release.id -isnot [long] -and $release.id -isnot [int]) -or $release.id -le 0) { throw 'Release ID is invalid.' }
Write-Output "Using draft release ID $($release.id) for $Tag"
# Upload completely before publication so the updater cannot see a partial release.
foreach ($assetPath in @($installerPath, $checksumPath)) {
    $assetName = [IO.Path]::GetFileName($assetPath)
    foreach ($existing in @($release.assets | Where-Object name -CEQ $assetName)) {
        if (($existing.id -isnot [long] -and $existing.id -isnot [int]) -or $existing.id -le 0) { throw 'Existing asset ID is invalid.' }
        gh api --method DELETE "repos/$Repository/releases/assets/$($existing.id)" --silent
        if ($LASTEXITCODE -ne 0) { throw 'Replacing a draft asset failed; the draft was kept.' }
    }
    $encodedName = [Uri]::EscapeDataString($assetName)
    # Address the upload service by ID too; gh release upload would resolve the
    # draft by tag again and reintroduce the same GITHUB_TOKEN visibility issue.
    $uploadedJson = gh api --method POST "https://uploads.github.com/repos/$Repository/releases/$($release.id)/assets?name=$encodedName" `
        --header 'Content-Type: application/octet-stream' --input $assetPath
    if ($LASTEXITCODE -ne 0) { throw "Uploading $assetName failed; draft ID $($release.id) was kept for retry." }
    $uploaded = ($uploadedJson -join "`n") | ConvertFrom-Json
    if ($uploaded.name -cne $assetName -or $uploaded.state -cne 'uploaded' -or $uploaded.size -ne (Get-Item -LiteralPath $assetPath).Length) {
        throw 'Uploaded asset metadata is incomplete; the release remains a draft.'
    }
    $assetHash = if ($assetPath -eq $installerPath) { $installerHash } else { (Get-FileHash -LiteralPath $assetPath -Algorithm SHA256).Hash.ToLowerInvariant() }
    if ($uploaded.digest -and $uploaded.digest -cne "sha256:$assetHash") { throw 'Uploaded asset digest mismatch; the release remains a draft.' }
}
$published = Invoke-ReleaseJsonRequest PATCH "repos/$Repository/releases/$($release.id)" @{ draft = $false; make_latest = 'legacy' }
if ($published.draft -or $published.tag_name -cne $Tag -or $published.id -ne $release.id) { throw 'Release publication response was invalid.' }
Write-Output "Published https://github.com/$Repository/releases/tag/$Tag"
