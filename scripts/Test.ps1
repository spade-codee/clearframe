#requires -Version 7.0
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$output = Join-Path $root 'dist\ClearFrame'
$app = Join-Path $output 'ClearFrame.exe'
if (-not (Test-Path -LiteralPath $app)) { throw 'Run scripts/Build.ps1 first.' }
$artifacts = Join-Path $root 'artifacts'
New-Item -ItemType Directory -Force $artifacts | Out-Null
function Invoke-AppCheck([string[]]$AppArgs) {
    $quoted = $AppArgs | ForEach-Object { '"' + $_ + '"' }
    $process = Start-Process -FilePath $app -ArgumentList $quoted -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) {
        $errorFile = Join-Path $output 'test-error.txt'
        if (Test-Path -LiteralPath $errorFile) { Get-Content -LiteralPath $errorFile }
        throw "Application check failed: $($AppArgs[0])"
    }
}
Invoke-AppCheck @('--self-test',(Join-Path $artifacts 'core-tests.txt'))
Invoke-AppCheck @('--render',(Join-Path $artifacts 'interface.png'),'--check-ui')
Invoke-AppCheck @('--render',(Join-Path $artifacts 'interface-small.png'),'1064','762')
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo "/out:$output\ProcessFixture.exe" (Join-Path $root 'tests\ProcessFixture.cs')
if ($LASTEXITCODE -ne 0) { throw 'Process fixture compilation failed.' }
& $compiler /nologo "/reference:$app" "/out:$output\ProcessTests.exe" (Join-Path $root 'tests\ProcessTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Process test compilation failed.' }
& (Join-Path $output 'ProcessTests.exe') (Join-Path $output 'ProcessFixture.exe') (Join-Path $artifacts 'process-tests.txt')
if ($LASTEXITCODE -ne 0) { throw 'Process checks failed.' }
Get-Content (Join-Path $artifacts 'core-tests.txt'),(Join-Path $artifacts 'interface.png.checks.txt'),(Join-Path $artifacts 'process-tests.txt')
