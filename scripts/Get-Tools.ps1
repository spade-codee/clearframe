#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$Destination,
    [string]$CacheDirectory
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'tools.lock.json')) {
    $lockFile = Join-Path $PSScriptRoot 'tools.lock.json'
    if (-not $Destination) { $Destination = Join-Path $PSScriptRoot 'tools' }
} else {
    $root = Split-Path $PSScriptRoot -Parent
    $lockFile = Join-Path $root 'config\tools.lock.json'
    if (-not $Destination) { $Destination = Join-Path $root 'dist\ClearFrame\tools' }
}
$Destination = [IO.Path]::GetFullPath($Destination)
if (-not $CacheDirectory) { $CacheDirectory = Join-Path $Destination '.downloads' }
$CacheDirectory = [IO.Path]::GetFullPath($CacheDirectory)
New-Item -ItemType Directory -Force $Destination,$CacheDirectory | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$records = Get-Content -LiteralPath $lockFile -Raw | ConvertFrom-Json
foreach ($record in $records) {
    $uri = [Uri]$record.url
    if ($uri.Scheme -ne 'https' -or $uri.Host -ne 'github.com' -or $record.sha256 -notmatch '^[a-f0-9]{64}$' -or $uri.AbsolutePath -match '/latest/') { throw 'Invalid or unpinned dependency entry.' }
    $assetName = [IO.Path]::GetFileName($uri.AbsolutePath)
    $archive = Join-Path $CacheDirectory $assetName
    if (-not (Test-Path -LiteralPath $archive)) {
        Write-Output "Downloading $($record.project) $($record.version)..."
        $partial = "$archive.part"
        Invoke-WebRequest -Uri $uri -OutFile $partial -MaximumRetryCount 3
        if ((Get-FileHash -LiteralPath $partial -Algorithm SHA256).Hash.ToLowerInvariant() -ne $record.sha256) { throw "Checksum mismatch for $assetName. No binary was installed." }
        Move-Item -LiteralPath $partial -Destination $archive
    }
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $record.sha256) { throw "Checksum mismatch for cached $assetName. Remove that cache file and retry." }
    if ($assetName -eq 'yt-dlp.exe') {
        Copy-Item -LiteralPath $archive -Destination (Join-Path $Destination 'yt-dlp.exe') -Force
    } else {
        $zip = [IO.Compression.ZipFile]::OpenRead($archive)
        try {
            foreach ($entry in $zip.Entries) {
                # Flatten only expected executable/library names; never extract archive paths.
                if ($entry.Name -in @('deno.exe','ffmpeg.exe','ffprobe.exe') -or $entry.Name -like '*.dll') {
                    [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,(Join-Path $Destination $entry.Name),$true)
                } elseif ($entry.FullName -match '(?i)(license|copying)' -and $entry.Length -gt 0) {
                    $legal = Join-Path $Destination 'licenses'
                    New-Item -ItemType Directory -Force $legal | Out-Null
                    $name = $entry.FullName -replace '[^a-zA-Z0-9._-]','_'
                    [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,(Join-Path $legal $name),$true)
                }
            }
        } finally { $zip.Dispose() }
    }
}
foreach ($name in @('yt-dlp.exe','deno.exe','ffmpeg.exe','ffprobe.exe')) {
    if (-not (Test-Path -LiteralPath (Join-Path $Destination $name))) { throw "Missing tool after setup: $name" }
}
Copy-Item -LiteralPath $lockFile -Destination (Join-Path $Destination 'versions.json') -Force
Write-Output "Verified tools installed at $Destination"
