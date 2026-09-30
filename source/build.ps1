#requires -Version 7.0
$ErrorActionPreference = 'Stop'
& (Join-Path (Split-Path $PSScriptRoot -Parent) 'scripts\Build.ps1')
