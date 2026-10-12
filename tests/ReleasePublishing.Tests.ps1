$ErrorActionPreference = 'Stop'
$publisher = Join-Path $PSScriptRoot '../tools/publish_release.ps1'
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('stereomix-publisher-test-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$installer = Join-Path $fixture 'CustomStereoMix-1.2.3-Setup-x64.exe'
Set-Content -LiteralPath $installer -Value 'TEST FIXTURE, NOT AN EXECUTABLE' -Encoding ascii
$hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  CustomStereoMix-1.2.3-Setup-x64.exe" | Set-Content -LiteralPath "$installer.sha256" -Encoding ascii

# The function shadows gh for child scripts: these tests never contact GitHub.
$global:stereomixPublisherMock = $null
function gh {
    $arguments = @($args)
    $mock = $global:stereomixPublisherMock
    $mock.Calls.Add(($arguments -join ' '))
    $global:LASTEXITCODE = 0
    if ($arguments[0] -eq 'api' -and $arguments -contains '--method') {
        if ($mock.Mode -eq 'publish-failed') { $global:LASTEXITCODE = 1; return }
        $payloadPath = $arguments[[Array]::IndexOf($arguments, '--input') + 1]
        $payload = Get-Content -LiteralPath $payloadPath -Raw | ConvertFrom-Json
        if ($payload.draft -ne $false -or $payload.make_latest -ne 'legacy') { throw 'Invalid publication payload.' }
        if ($arguments -notcontains 'repos/example/stereomix/releases/123') { throw 'Publication did not use release ID.' }
        return
    }
    if ($arguments[0] -eq 'api') {
        # Reproduce GitHub: the tag endpoint cannot return an unpublished draft.
        if (($arguments -join ' ') -match '/releases/tags/') { $global:LASTEXITCODE = 1; throw 'Published-only lookup used for a draft.' }
        if ($arguments -notcontains '--paginate' -or $arguments -notcontains '--slurp') { throw 'Release pagination is missing.' }
        $mock.Lookups++
        if ($mock.Mode -eq 'lookup-failed') { $global:LASTEXITCODE = 1; return }
        if (($mock.Mode -eq 'new' -and $mock.Lookups -eq 1) -or $mock.Mode -in @('create-failed', 'created-not-found')) { return '[[]]' }
        $release = @{ id = 123; draft = ($mock.Mode -ne 'published'); prerelease = ($mock.Mode -eq 'prerelease'); tag_name = 'v1.2.3' }
        if ($mock.Mode -eq 'invalid-id') { $release.id = '../invalid' }
        $json = $release | ConvertTo-Json -Compress
        if ($mock.Mode -eq 'paginated') { return ('[[{"id":1,"tag_name":"v1.0.0","draft":false}],[' + $json + ']]') }
        if ($mock.Mode -eq 'duplicates') { return ('[[' + $json + ',' + $json + ']]') }
        return ('[[' + $json + ']]')
    }
    if ($arguments[0] -eq 'release' -and $arguments[1] -eq 'create' -and $mock.Mode -eq 'create-failed') { $global:LASTEXITCODE = 1 }
    if ($arguments[0] -eq 'release' -and $arguments[1] -eq 'upload' -and $mock.Mode -eq 'upload-failed') { $global:LASTEXITCODE = 1 }
}

try {
    foreach ($mode in @('new', 'draft', 'paginated', 'published', 'prerelease', 'duplicates', 'lookup-failed', 'create-failed', 'created-not-found', 'invalid-id', 'upload-failed', 'publish-failed')) {
        $mock = @{ Mode = $mode; Lookups = 0; Calls = [Collections.Generic.List[string]]::new() }
        $global:stereomixPublisherMock = $mock
        $failed = $false
        try { & $publisher -Tag v1.2.3 -Repository example/stereomix -SetupDirectory $fixture | Out-Null }
        catch { $failed = $true }
        $successExpected = $mode -in @('new', 'draft', 'paginated')
        if ($successExpected -eq $failed) { throw "Unexpected result: $mode" }
        $creates = @($mock.Calls | Where-Object { $_ -match '^release create' })
        $uploads = @($mock.Calls | Where-Object { $_ -match '^release upload' })
        $publishes = @($mock.Calls | Where-Object { $_ -match '^api --method PATCH' })
        if ($successExpected -and ($uploads.Count -ne 1 -or $publishes.Count -ne 1)) { throw "Incomplete publication: $mode" }
        if ($mode -eq 'new' -and $creates.Count -ne 1) { throw 'New draft was not created exactly once.' }
        if ($mode -in @('draft', 'paginated', 'lookup-failed') -and $creates.Count -ne 0) { throw "Unnecessary draft creation: $mode" }
        if (-not $successExpected -and $mode -ne 'publish-failed' -and $publishes.Count -ne 0) { throw "Unsafe publication: $mode" }
        if ($mode -in @('published', 'prerelease', 'duplicates', 'lookup-failed', 'invalid-id') -and $uploads.Count -ne 0) { throw "Unsafe upload: $mode" }
        Write-Output "PASS release publishing: $mode"
    }
    Set-Content -LiteralPath "$installer.sha256" -Value 'invalid' -Encoding ascii
    $mock.Calls.Clear()
    try { & $publisher -Tag v1.2.3 -Repository example/stereomix -SetupDirectory $fixture | Out-Null; throw 'Invalid checksum accepted.' }
    catch { if ($_.Exception.Message -notmatch 'checksum mismatch') { throw } }
    if ($mock.Calls.Count -ne 0) { throw 'Invalid artifact contacted GitHub.' }
    Write-Output 'PASS release publishing: checksum rejected before GitHub'
    # Expected mocked failures must not become the runner's final exit status.
    $global:LASTEXITCODE = 0
}
finally {
    Remove-Variable -Name stereomixPublisherMock -Scope Global -ErrorAction SilentlyContinue
    # The directory is uniquely created in this test, and contains only fixtures.
    Remove-Item -LiteralPath $installer -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath "$installer.sha256" -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $fixture -Force -ErrorAction SilentlyContinue
}
