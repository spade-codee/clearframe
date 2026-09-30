#requires -Version 7.0
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$version = (Get-Content -LiteralPath (Join-Path $root 'VERSION') -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid VERSION.' }
$app = Join-Path $root 'dist\ClearFrame\ClearFrame.exe'
if (-not (Test-Path -LiteralPath $app)) { throw 'Build before packaging.' }
if ([Diagnostics.FileVersionInfo]::GetVersionInfo($app).FileVersion -ne "$version.0") { throw 'Build version does not match VERSION.' }
$stage = Join-Path $root ('.build\package-' + [Guid]::NewGuid().ToString('N') + '\ClearFrame')
$artifacts = Join-Path $root 'artifacts'
New-Item -ItemType Directory -Force $stage,$artifacts | Out-Null
# An explicit allowlist prevents accidental bundling of downloaded tools or test programs.
Copy-Item -LiteralPath $app,(Join-Path $root 'dist\ClearFrame\ClearFrame.exe.config'),(Join-Path $root 'LICENSE'),(Join-Path $root 'THIRD-PARTY.md'),(Join-Path $root 'VERSION') -Destination $stage
Copy-Item -LiteralPath (Join-Path $root 'scripts\Get-Tools.ps1') -Destination $stage
Copy-Item -LiteralPath (Join-Path $root 'config\tools.lock.json') -Destination $stage
Copy-Item -LiteralPath (Join-Path $root 'docs\USER-GUIDE.md') -Destination (Join-Path $stage 'USER-GUIDE.md')
@"
ClearFrame $version — Windows x64

1. Extract this entire folder.
2. Open PowerShell 7 in this folder and run: ./Get-Tools.ps1
3. Run ClearFrame.exe.

Tool setup downloads external programs from pinned upstream releases and verifies SHA-256 hashes.
The application is unsigned. Requires Windows 10/11 x64 and .NET Framework 4.7.2+.
See USER-GUIDE.md, LICENSE and THIRD-PARTY.md.
Source: https://github.com/spade-codee/clearframe
"@ | Set-Content -LiteralPath (Join-Path $stage 'START-HERE.txt') -Encoding utf8
$zip = Join-Path $artifacts "ClearFrame-$version-windows-x64.zip"
Compress-Archive -LiteralPath $stage -DestinationPath $zip -Force
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $([IO.Path]::GetFileName($zip))" | Set-Content -LiteralPath "$zip.sha256" -Encoding ascii
Write-Output "Created $zip"
