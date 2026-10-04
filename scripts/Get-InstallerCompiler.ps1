#requires -Version 7.0
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$directory = Join-Path $root '.build\inno-6.7.3'
$compiler = Join-Path $directory 'ISCC.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $download = Join-Path $root '.build\innosetup-6.7.3.exe'
    New-Item -ItemType Directory -Force (Split-Path $download) | Out-Null
    Invoke-WebRequest 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe' -OutFile $download
    if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash -ne '9c73c3bae7ed48d44112a0f48e66742c00090bdb5bef71d9d3c056c66e97b732') { throw 'Installer compiler checksum mismatch.' }
    $arguments = @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER','/NOICONS','/TASKS=')
    $arguments += '/DIR="' + $directory + '"'
    $process = Start-Process -FilePath $download -WindowStyle Hidden -Wait -PassThru -ArgumentList $arguments
    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $compiler)) { throw 'Installer compiler setup failed.' }
}
Write-Output $compiler
