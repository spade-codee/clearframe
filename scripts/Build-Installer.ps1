#requires -Version 7.0
[CmdletBinding()]
param([string]$Compiler)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$version = (Get-Content -LiteralPath (Join-Path $root 'VERSION') -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid VERSION.' }
if (-not $Compiler) { $Compiler = Join-Path $root '.build\inno-6.7.3\ISCC.exe' }
if (-not (Test-Path -LiteralPath $Compiler)) { throw 'Run scripts/Get-InstallerCompiler.ps1 first.' }
$app = Join-Path $root 'dist\ClearFrame\ClearFrame.exe'
if ([Diagnostics.FileVersionInfo]::GetVersionInfo($app).FileVersion -ne "$version.0") { throw 'Build version does not match VERSION.' }
$records = @(Get-Content -LiteralPath (Join-Path $root 'config\tools.lock.json') -Raw | ConvertFrom-Json)
$expected = @('yt-dlp/yt-dlp','denoland/deno','yt-dlp/FFmpeg-Builds')
if ($records.Count -ne 3) { throw 'Expected exactly three pinned tools.' }
$lines = @()
for ($i=0; $i -lt 3; $i++) {
    $entry = $records[$i]
    $uri = [Uri]$entry.url
    if ($entry.project -ne $expected[$i] -or $uri.Scheme -ne 'https' -or $uri.Host -ne 'github.com' -or $uri.AbsolutePath -notlike "/$($expected[$i])/releases/download/*" -or $uri.AbsolutePath -match '/latest/' -or $entry.url -match "['`r`n]" -or $entry.sha256 -notmatch '^[a-f0-9]{64}$') { throw 'Invalid dependency pin.' }
    $lines += "#define ToolUrl$i `"$($entry.url)`""
    $lines += "#define ToolHash$i `"$($entry.sha256)`""
}
New-Item -ItemType Directory -Force (Join-Path $root '.build') | Out-Null
$lines | Set-Content -LiteralPath (Join-Path $root '.build\installer-tools.iss') -Encoding utf8
& $Compiler "/DAppVersion=$version" (Join-Path $root 'installer\ClearFrame.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
$setup = Join-Path $root "artifacts\ClearFrame-$version-Setup.exe"
$hash = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $([IO.Path]::GetFileName($setup))" | Set-Content -LiteralPath "$setup.sha256" -Encoding ascii
# Stable asset name keeps the link shared with friends useful after future releases.
Copy-Item -LiteralPath $setup -Destination (Join-Path $root 'artifacts\ClearFrame-Setup.exe') -Force
"$hash  ClearFrame-Setup.exe" | Set-Content -LiteralPath (Join-Path $root 'artifacts\ClearFrame-Setup.exe.sha256') -Encoding ascii
Write-Output "Created $setup"
