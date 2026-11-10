<#
.SYNOPSIS
  Builds the pinned ipatool source snapshot after verifying its SHA-256 checksum.
.DESCRIPTION
  The executable is written to artifacts\tools\<rid>\ipatool.exe, where the Perenos
  project picks it up. Version, commit, Go toolchain and checksum live in build\ipatool.json.
#>
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string] $RuntimeIdentifier = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$manifest = Get-Content (Join-Path $PSScriptRoot 'ipatool.json') -Raw | ConvertFrom-Json
$asset = $manifest.archive
$targetDir = Join-Path $root "artifacts\tools\$RuntimeIdentifier"
$target = Join-Path $targetDir 'ipatool.exe'
$stamp = Join-Path $targetDir 'ipatool.sha256'
$stampValue = "$($asset.sha256);$($manifest.go_version);$RuntimeIdentifier"

if ((Test-Path $target) -and (Test-Path $stamp) -and ((Get-Content $stamp -Raw).Trim() -eq $stampValue)) {
    Write-Host "ipatool $($manifest.version) ($RuntimeIdentifier) is already present."
    return
}

$go = Get-Command go -ErrorAction SilentlyContinue
$goExe = if ($go) { $go.Source } else { Join-Path $env:LOCALAPPDATA 'Programs\Go\bin\go.exe' }
if (-not (Test-Path $goExe)) {
    throw "Go $($manifest.go_version) is required. Install it and make go available in PATH."
}

$actualGoVersion = (& $goExe env GOVERSION).Trim()
if ($LASTEXITCODE -ne 0) { throw "Could not determine the Go version." }
if ($actualGoVersion -ne "go$($manifest.go_version)") {
    throw "Go $($manifest.go_version) is required, but $actualGoVersion was found at $goExe."
}

$work = Join-Path ([IO.Path]::GetTempPath()) ("ipatool-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $work | Out-Null
try {
    $archive = Join-Path $work $asset.file
    Write-Host "Downloading $($asset.url)"
    $ProgressPreference = 'SilentlyContinue'
    Invoke-WebRequest -Uri $asset.url -OutFile $archive -UseBasicParsing

    $actual = (Get-FileHash -Algorithm SHA256 $archive).Hash.ToLowerInvariant()
    if ($actual -ne $asset.sha256) {
        throw "Checksum mismatch for $($asset.file): expected $($asset.sha256), got $actual"
    }

    $extract = Join-Path $work 'x'
    New-Item -ItemType Directory -Force -Path $extract | Out-Null
    $tarExe = Join-Path $env:SystemRoot 'System32\tar.exe'
    if (-not (Test-Path $tarExe)) { throw "Windows tar.exe was not found at $tarExe." }
    & $tarExe -xzf $archive -C $extract
    if ($LASTEXITCODE -ne 0) { throw "tar failed with exit code $LASTEXITCODE" }

    $sourceDir = Get-ChildItem $extract -Directory |
        Where-Object { Test-Path (Join-Path $_.FullName 'go.mod') } |
        Select-Object -First 1
    if (-not $sourceDir) { throw "No Go module found in $($asset.file)." }

    $built = Join-Path $work 'ipatool.exe'
    $env:GOOS = 'windows'
    $env:GOARCH = if ($RuntimeIdentifier -eq 'win-arm64') { 'arm64' } else { 'amd64' }
    $env:CGO_ENABLED = '0'
    $env:GOTOOLCHAIN = 'local'
    $env:GOWORK = 'off'
    $ldflags = "-X github.com/majd/ipatool/v2/cmd.version=$($manifest.version)"
    Push-Location $sourceDir.FullName
    try {
        & $goExe build -trimpath -buildvcs=false -ldflags $ldflags -o $built .
        if ($LASTEXITCODE -ne 0) { throw "go build failed with exit code $LASTEXITCODE" }
    }
    finally {
        Pop-Location
    }

    New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
    Copy-Item $built $target -Force
    Set-Content -Path $stamp -Value $stampValue -NoNewline
    Write-Host "ipatool $($manifest.version) ($RuntimeIdentifier) built from $($manifest.commit) and saved to $target"
}
finally {
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}
