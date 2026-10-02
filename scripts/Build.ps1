#requires -Version 7.0
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$version = (Get-Content -LiteralPath (Join-Path $root 'VERSION') -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'VERSION must be MAJOR.MINOR.PATCH.' }
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Install .NET Framework 4.7.2+ on Windows x64.' }
$scratch = Join-Path $root '.build'
$output = Join-Path $root 'dist\ClearFrame'
New-Item -ItemType Directory -Force $scratch,$output | Out-Null
$assemblyInfo = Join-Path $scratch 'Version.g.cs'
@"
using System.Reflection;
[assembly: AssemblyTitle("ClearFrame")]
[assembly: AssemblyProduct("ClearFrame")]
[assembly: AssemblyCompany("ClearFrame contributors")]
[assembly: AssemblyCopyright("Copyright 2026 ClearFrame contributors")]
[assembly: AssemblyVersion("$version.0")]
[assembly: AssemblyFileVersion("$version.0")]
[assembly: AssemblyInformationalVersion("$version")]
"@ | Set-Content -LiteralPath $assemblyInfo -Encoding utf8
$manifest = Get-Content -LiteralPath (Join-Path $root 'source\app.manifest') -Raw
$manifest = $manifest -replace '(<assemblyIdentity version=")[^"]+',"`${1}$version.0"
$generatedManifest = Join-Path $scratch 'app.manifest'
$manifest | Set-Content -LiteralPath $generatedManifest -Encoding utf8
$refs = @('System.dll','System.Core.dll','System.Web.Extensions.dll','System.Windows.Forms.dll','System.Drawing.dll','System.Xaml.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll')
$arguments = @('/nologo','/target:winexe','/platform:x64','/optimize+',"/out:$output\ClearFrame.exe","/resource:$root\source\Main.xaml,Main.xaml","/win32manifest:$generatedManifest","/win32icon:$root\source\ClearFrame.ico")
foreach ($ref in $refs) { $arguments += '/reference:' + (Join-Path $framework $ref) }
$arguments += @((Join-Path $root 'source\ClearFrame.cs'),(Join-Path $root 'source\UIFeatures.cs'),(Join-Path $root 'source\VideoCleanup.cs'),(Join-Path $root 'source\StateStore.cs'),$assemblyInfo)
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Copy-Item -LiteralPath (Join-Path $root 'source\ClearFrame.exe.config') -Destination $output -Force
$actual = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $output 'ClearFrame.exe')).FileVersion
if ($actual -ne "$version.0") { throw "Unexpected executable version: $actual" }
Write-Output "Built ClearFrame $version at $output"
