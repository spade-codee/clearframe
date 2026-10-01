#requires -Version 7.0
[CmdletBinding()]
param([string]$MediaTools)
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
Invoke-AppCheck @('--render-editor',(Join-Path $artifacts 'editor-empty.png'))
Invoke-AppCheck @('--check-editor',(Join-Path $artifacts 'editor-controls.txt'))
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo "/out:$output\ProcessFixture.exe" (Join-Path $root 'tests\ProcessFixture.cs')
if ($LASTEXITCODE -ne 0) { throw 'Process fixture compilation failed.' }
& $compiler /nologo "/reference:$app" "/out:$output\ProcessTests.exe" (Join-Path $root 'tests\ProcessTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Process test compilation failed.' }
& (Join-Path $output 'ProcessTests.exe') (Join-Path $output 'ProcessFixture.exe') (Join-Path $artifacts 'process-tests.txt')
if ($LASTEXITCODE -ne 0) { throw 'Process checks failed.' }
Get-Content (Join-Path $artifacts 'core-tests.txt'),(Join-Path $artifacts 'interface.png.checks.txt'),(Join-Path $artifacts 'editor-controls.txt'),(Join-Path $artifacts 'process-tests.txt')
if ($MediaTools) {
    $references = @('System.Web.Extensions.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll')
    $arguments = @('/nologo',"/reference:$app","/out:$output\CleanupMediaTests.exe")
    foreach ($ref in $references) { $arguments += '/reference:' + (Join-Path (Split-Path $compiler -Parent) $ref) }
    $arguments += Join-Path $root 'tests\CleanupMediaTests.cs'
    & $compiler @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Media test compilation failed.' }
    & (Join-Path $output 'CleanupMediaTests.exe') ([IO.Path]::GetFullPath($MediaTools)) (Join-Path $artifacts 'media-fixtures') (Join-Path $artifacts 'cleanup-media-tests.txt')
    if ($LASTEXITCODE -ne 0) { Get-Content (Join-Path $artifacts 'cleanup-media-tests.txt'); throw 'Media checks failed.' }
    Get-Content (Join-Path $artifacts 'cleanup-media-tests.txt')
}
