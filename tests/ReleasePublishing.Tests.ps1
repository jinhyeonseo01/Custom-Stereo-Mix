$ErrorActionPreference = 'Stop'
$publisher = Join-Path $PSScriptRoot '../tools/publish_release.ps1'
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('stereomix-publisher-test-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$installer = Join-Path $fixture 'CustomStereoMix-1.2.3-Setup-x64.exe'
Set-Content -LiteralPath $installer -Value 'TEST FIXTURE, NOT AN EXECUTABLE' -Encoding ascii
$hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  CustomStereoMix-1.2.3-Setup-x64.exe" | Set-Content -LiteralPath "$installer.sha256" -Encoding ascii

# Simulate both account-token access and Actions tokens which cannot list drafts.
# Only this function is called by the publisher; tests never contact GitHub.
$global:stereomixPublisherMock = $null
function gh {
    $arguments = @($args)
    $mock = $global:stereomixPublisherMock
    $mock.Calls.Add(($arguments -join ' '))
    $global:LASTEXITCODE = 0
    if ($arguments[0] -ne 'api') { throw 'Tag-resolving release CLI command was used.' }
    $methodIndex = [Array]::IndexOf($arguments, '--method')
    $method = if ($methodIndex -ge 0) { $arguments[$methodIndex + 1] } else { 'GET' }
    $endpoint = if ($methodIndex -ge 0) { $arguments[$methodIndex + 2] } elseif ($arguments -contains '--slurp') { $arguments[-1] } else { $arguments[1] }
    $release = @{ id = 123; draft = ($mock.Mode -ne 'published'); prerelease = ($mock.Mode -eq 'prerelease'); tag_name = 'v1.2.3'; assets = @() }
    if ($mock.Mode -eq 'invalid-id') { $release.id = '../invalid' }
    if ($mock.Mode -in @('replace', 'replace-failed')) { $release.assets = @(@{ id = 456; name = 'CustomStereoMix-1.2.3-Setup-x64.exe' }) }
    if ($method -eq 'GET' -and $endpoint -match '/git/ref/tags/') {
        if ($mock.Mode -eq 'missing-tag') { $global:LASTEXITCODE = 1 }
        return
    }
    if ($method -eq 'GET' -and $endpoint -eq 'repos/example/stereomix/releases/123') { return ($release | ConvertTo-Json -Depth 5 -Compress) }
    if ($method -eq 'GET') {
        if ($endpoint -match '/releases/tags/') { throw 'Published-only lookup used for a draft.' }
        if ($arguments -notcontains '--paginate' -or $arguments -notcontains '--slurp') { throw 'Release pagination is missing.' }
        $mock.Lookups++
        if ($mock.Mode -eq 'lookup-failed') { $global:LASTEXITCODE = 1; return }
        if ($mock.Mode -in @('new', 'new-invisible', 'create-failed', 'missing-tag', 'created-invalid')) {
            if ($mock.Lookups -gt 1) { throw 'Created draft was looked up again instead of using the POST response.' }
            return '[[]]'
        }
        $json = $release | ConvertTo-Json -Depth 5 -Compress
        if ($mock.Mode -eq 'paginated') { return ('[[{"id":1,"tag_name":"v1.0.0","draft":false}],[' + $json + ']]') }
        if ($mock.Mode -eq 'duplicates') { return ('[[' + $json + ',' + $json + ']]') }
        return ('[[' + $json + ']]')
    }
    if ($method -eq 'DELETE') {
        if ($endpoint -ne 'repos/example/stereomix/releases/assets/456') { throw 'Wrong asset deletion ID.' }
        if ($mock.Mode -eq 'replace-failed') { $global:LASTEXITCODE = 1 }
        return
    }
    $inputPath = $arguments[[Array]::IndexOf($arguments, '--input') + 1]
    if ($method -eq 'POST' -and $endpoint.StartsWith('https://uploads.github.com/')) {
        if ($endpoint -notmatch '/releases/123/assets\?name=') { throw 'Upload did not use the created release ID.' }
        if ($arguments -notcontains 'Content-Type: application/octet-stream') { throw 'Binary content type missing.' }
        if ($mock.Mode -eq 'upload-failed') { $global:LASTEXITCODE = 1; return }
        $size = (Get-Item -LiteralPath $inputPath).Length
        $digest = 'sha256:' + (Get-FileHash -LiteralPath $inputPath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($mock.Mode -eq 'asset-corrupt') { $size = 0 }
        if ($mock.Mode -eq 'hash-corrupt') { $digest = 'sha256:invalid' }
        return (@{ name = [IO.Path]::GetFileName($inputPath); state = 'uploaded'; size = $size; digest = $digest } | ConvertTo-Json -Compress)
    }
    $payload = Get-Content -LiteralPath $inputPath -Raw | ConvertFrom-Json
    if ($method -eq 'POST') {
        if ($endpoint -ne 'repos/example/stereomix/releases' -or $payload.tag_name -cne 'v1.2.3' -or -not $payload.draft -or -not $payload.generate_release_notes) { throw 'Invalid draft creation payload.' }
        if ($mock.Mode -eq 'create-failed') { $global:LASTEXITCODE = 1; return }
        if ($mock.Mode -eq 'created-invalid') { $release.tag_name = 'v99.0.0' }
        return ($release | ConvertTo-Json -Depth 5 -Compress)
    }
    if ($method -ne 'PATCH' -or $endpoint -ne 'repos/example/stereomix/releases/123' -or $payload.draft -ne $false -or $payload.make_latest -ne 'legacy') { throw 'Invalid publication request.' }
    if ($mock.Mode -eq 'publish-failed') { $global:LASTEXITCODE = 1; return }
    $release.draft = $false
    return ($release | ConvertTo-Json -Depth 5 -Compress)
}

try {
    foreach ($mode in @('new', 'new-invisible', 'draft', 'paginated', 'explicit-id', 'replace', 'published', 'prerelease', 'duplicates', 'lookup-failed', 'create-failed', 'created-invalid', 'invalid-id', 'missing-tag', 'upload-failed', 'asset-corrupt', 'hash-corrupt', 'replace-failed', 'publish-failed')) {
        $mock = @{ Mode = $mode; Lookups = 0; Calls = [Collections.Generic.List[string]]::new() }
        $global:stereomixPublisherMock = $mock
        $parameters = @{ Tag = 'v1.2.3'; Repository = 'example/stereomix'; SetupDirectory = $fixture }
        if ($mode -eq 'explicit-id') { $parameters.ReleaseId = 123 }
        $failed = $false
        try { & $publisher @parameters | Out-Null }
        catch { $failed = $true }
        $successExpected = $mode -in @('new', 'new-invisible', 'draft', 'paginated', 'explicit-id', 'replace')
        if ($successExpected -eq $failed) { throw "Unexpected result: $mode" }
        $creates = @($mock.Calls | Where-Object { $_ -match '^api --method POST repos/example/stereomix/releases ' })
        $uploads = @($mock.Calls | Where-Object { $_ -match '^api --method POST https://uploads.github.com/' })
        $publishes = @($mock.Calls | Where-Object { $_ -match '^api --method PATCH' })
        if ($successExpected -and ($uploads.Count -ne 2 -or $publishes.Count -ne 1)) { throw "Incomplete publication: $mode" }
        if ($mode -in @('new', 'new-invisible') -and ($creates.Count -ne 1 -or $mock.Lookups -ne 1)) { throw 'Creation response was not reused.' }
        if ($mode -in @('draft', 'paginated', 'lookup-failed', 'explicit-id') -and $creates.Count -ne 0) { throw "Unnecessary creation: $mode" }
        if ($mode -eq 'explicit-id' -and $mock.Lookups -ne 0) { throw 'Explicit ID recovery used a list lookup.' }
        if (-not $successExpected -and $mode -ne 'publish-failed' -and $publishes.Count -ne 0) { throw "Unsafe publication: $mode" }
        if ($mode -in @('published', 'prerelease', 'duplicates', 'lookup-failed', 'invalid-id', 'missing-tag', 'created-invalid') -and $uploads.Count -ne 0) { throw "Unsafe upload: $mode" }
        Write-Output "PASS release publishing: $mode"
    }
    Set-Content -LiteralPath "$installer.sha256" -Value 'invalid' -Encoding ascii
    $mock.Calls.Clear()
    try { & $publisher -Tag v1.2.3 -Repository example/stereomix -SetupDirectory $fixture | Out-Null; throw 'Invalid checksum accepted.' }
    catch { if ($_.Exception.Message -notmatch 'checksum mismatch') { throw } }
    if ($mock.Calls.Count -ne 0) { throw 'Invalid artifact contacted GitHub.' }
    Write-Output 'PASS release publishing: checksum rejected before GitHub'
    $global:LASTEXITCODE = 0
}
finally {
    Remove-Variable -Name stereomixPublisherMock -Scope Global -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $installer -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath "$installer.sha256" -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $fixture -Force -ErrorAction SilentlyContinue
}
