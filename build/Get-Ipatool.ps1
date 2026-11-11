<#
.SYNOPSIS
  Builds the pinned ipatool source snapshot after verifying its SHA-256 checksum.
.DESCRIPTION
  Writes artifacts/tools/<rid>/ipatool[.exe]. Requires PowerShell 7, Go and tar
  on Windows or macOS. The source version, commit and checksum are pinned in ipatool.json.
#>
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-arm64', 'osx-x64', 'osx-arm64')]
    [string] $RuntimeIdentifier = $(if ($IsMacOS) { 'osx-arm64' } else { 'win-x64' })
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$manifest = Get-Content (Join-Path $PSScriptRoot 'ipatool.json') -Raw | ConvertFrom-Json
$asset = $manifest.archive
$windows = $RuntimeIdentifier.StartsWith('win-')
$fileName = if ($windows) { 'ipatool.exe' } else { 'ipatool' }
$targetDir = Join-Path $root "artifacts/tools/$RuntimeIdentifier"
$target = Join-Path $targetDir $fileName
$stamp = Join-Path $targetDir 'ipatool.sha256'
$stampValue = "$($asset.sha256);$($manifest.go_version);$RuntimeIdentifier"

if ((Test-Path $target) -and (Test-Path $stamp) -and ((Get-Content $stamp -Raw).Trim() -eq $stampValue)) {
    Write-Host "ipatool $($manifest.version) ($RuntimeIdentifier) is already present."
    return
}

$go = Get-Command go -ErrorAction SilentlyContinue
$goExe = if ($go) { $go.Source } elseif ($env:OS -eq 'Windows_NT') { Join-Path $env:LOCALAPPDATA 'Programs/Go/bin/go.exe' } else { '' }
if (-not $goExe -or -not (Test-Path $goExe)) {
    throw "Go $($manifest.go_version) is required. Install it and make go available in PATH."
}
$actualGoVersion = (& $goExe env GOVERSION).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Could not determine the Go version.' }
if ($actualGoVersion -ne "go$($manifest.go_version)") {
    throw "Go $($manifest.go_version) is required, but $actualGoVersion was found at $goExe."
}
$onWindows = [Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT
$tar = if ($onWindows) { Join-Path $env:SystemRoot 'System32\tar.exe' } else { (Get-Command tar -ErrorAction SilentlyContinue).Source }
if (-not $tar -or -not (Test-Path $tar)) { throw 'tar is required to unpack the pinned source archive.' }

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
    & $tar -xzf $archive --directory $extract
    if ($LASTEXITCODE -ne 0) { throw "tar failed with exit code $LASTEXITCODE" }
    $sourceDir = Get-ChildItem $extract -Directory |
        Where-Object { Test-Path (Join-Path $_.FullName 'go.mod') } |
        Select-Object -First 1
    if (-not $sourceDir) { throw "No Go module found in $($asset.file)." }

    $built = Join-Path $work $fileName
    if (-not $windows) {
        if ($onWindows) { throw 'macOS ipatool must be built on macOS with Xcode Command Line Tools.' }
        $env:CC = if ($RuntimeIdentifier -eq 'osx-arm64') { 'clang -arch arm64' } else { 'clang -arch x86_64' }
    }
    $env:GOOS = if ($windows) { 'windows' } else { 'darwin' }
    $env:GOARCH = if ($RuntimeIdentifier.EndsWith('arm64')) { 'arm64' } else { 'amd64' }
    $env:CGO_ENABLED = if ($windows) { '0' } else { '1' }
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
    if (-not $windows) { chmod +x $target }
    Set-Content -Path $stamp -Value $stampValue -NoNewline
    Write-Host "ipatool $($manifest.version) ($RuntimeIdentifier) built from $($manifest.commit) and saved to $target"
}
finally {
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}
