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
Invoke-AppCheck @('--render-vertical',(Join-Path $artifacts 'vertical-empty.png'))
Invoke-AppCheck @('--render-converter',(Join-Path $artifacts 'converter-empty.png'))
Invoke-AppCheck @('--check-vertical',(Join-Path $artifacts 'vertical-checks.txt'))
Get-Content (Join-Path $artifacts 'vertical-checks.txt')
Invoke-AppCheck @('--check-batch-clips',(Join-Path $artifacts 'batch-clip-checks.txt'))
Get-Content (Join-Path $artifacts 'batch-clip-checks.txt')
Invoke-AppCheck @('--check-clip-projects',(Join-Path $artifacts ('project-fixtures-'+[Guid]::NewGuid().ToString('N'))),(Join-Path $artifacts 'clip-project-checks.txt'))
Get-Content (Join-Path $artifacts 'clip-project-checks.txt')
$projectRestart = Join-Path $artifacts ('project-restart-'+[Guid]::NewGuid().ToString('N'))
Invoke-AppCheck @('--check-project-restart',$projectRestart,'write')
Invoke-AppCheck @('--check-project-restart',$projectRestart,'read')
Get-Content (Join-Path $projectRestart 'project-restart-checks.txt')
$recoveryFixture = Join-Path $artifacts ('recovery-'+[Guid]::NewGuid().ToString('N'))
Invoke-AppCheck @('--check-recovery',$recoveryFixture,(Join-Path $artifacts 'recovery-checks.txt'))
Get-Content (Join-Path $artifacts 'recovery-checks.txt')
$recoveryRestart = Join-Path $artifacts ('recovery-restart-'+[Guid]::NewGuid().ToString('N'))
Invoke-AppCheck @('--check-recovery-restart',$recoveryRestart,'write')
Invoke-AppCheck @('--check-recovery-restart',$recoveryRestart,'read')
Get-Content (Join-Path $recoveryRestart 'recovery-restart.txt')
Invoke-AppCheck @('--render-clip-batch',(Join-Path $artifacts 'clip-batch.png'))
Invoke-AppCheck @('--check-editor',(Join-Path $artifacts 'editor-controls.txt'))
Invoke-AppCheck @('--check-v04',(Join-Path $artifacts 'feature-checks.txt'))
Invoke-AppCheck @('--check-v05',(Join-Path $artifacts 'feature-v05-checks.txt'))
Invoke-AppCheck @('--check-schedule',(Join-Path $artifacts 'schedule-checks.txt'))
Invoke-AppCheck @('--check-audio-languages',(Join-Path $artifacts 'audio-language-checks.txt'))
Invoke-AppCheck @('--check-playback-defaults',(Join-Path $artifacts 'playback-default-checks.txt'))
Get-Content (Join-Path $artifacts 'playback-default-checks.txt')
Get-Content (Join-Path $artifacts 'audio-language-checks.txt')
Get-Content (Join-Path $artifacts 'schedule-checks.txt')
Get-Content (Join-Path $artifacts 'feature-v05-checks.txt')
$restartFixture = Join-Path $artifacts ('restart-' + [Guid]::NewGuid().ToString('N'))
Invoke-AppCheck @('--check-restart',$restartFixture,'write')
Invoke-AppCheck @('--check-restart',$restartFixture,'read')
Get-Content (Join-Path $restartFixture 'restart-checks.txt'),(Join-Path $artifacts 'feature-checks.txt')
Invoke-AppCheck @('--render-library',(Join-Path $artifacts 'library.png'))
Invoke-AppCheck @('--render-library',(Join-Path $artifacts 'library-small.png'),'1064','762')
Invoke-AppCheck @('--render-playlist',(Join-Path $artifacts 'playlist.png'))
Invoke-AppCheck @('--render-clip',(Join-Path $artifacts 'clip-options.png'))
Invoke-AppCheck @('--render-audio-languages',(Join-Path $artifacts 'audio-languages.png'))
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo "/out:$output\ProcessFixture.exe" (Join-Path $root 'tests\ProcessFixture.cs')
if ($LASTEXITCODE -ne 0) { throw 'Process fixture compilation failed.' }
& $compiler /nologo "/reference:$app" "/out:$output\ProcessTests.exe" (Join-Path $root 'tests\ProcessTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Process test compilation failed.' }
& (Join-Path $output 'ProcessTests.exe') (Join-Path $output 'ProcessFixture.exe') (Join-Path $artifacts 'process-tests.txt')
if ($LASTEXITCODE -ne 0) { throw 'Process checks failed.' }
& $compiler /nologo "/reference:$app" "/out:$output\StateTests.exe" (Join-Path $root 'tests\StateTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'State test compilation failed.' }
& (Join-Path $output 'StateTests.exe') (Join-Path $artifacts 'state-fixtures') (Join-Path $artifacts 'state-tests.txt')
if ($LASTEXITCODE -ne 0) { Get-Content (Join-Path $artifacts 'state-tests.txt'); throw 'State checks failed.' }
Get-Content (Join-Path $artifacts 'state-tests.txt')
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
    Invoke-AppCheck @('--check-autosave-ui',([IO.Path]::GetFullPath($MediaTools)),(Join-Path $artifacts ('autosave-ui-'+[Guid]::NewGuid().ToString('N'))),(Join-Path $artifacts 'media-fixtures/synthetic source.mp4'),(Join-Path $artifacts 'autosave-ui-checks.txt'))
    Get-Content (Join-Path $artifacts 'autosave-ui-checks.txt')
    Invoke-AppCheck @('--render-vertical',(Join-Path $artifacts 'vertical-clips.png'),(Join-Path $artifacts 'media-fixtures/preview.png'))
    Invoke-AppCheck @('--render-converter',(Join-Path $artifacts 'converter.png'),(Join-Path $artifacts 'media-fixtures/preview.png'))
    $clipArguments = $arguments | ForEach-Object { $_.Replace('CleanupMediaTests','ClipMediaTests') }
    & $compiler @clipArguments
    if ($LASTEXITCODE -ne 0) { throw 'Clip media test compilation failed.' }
    & (Join-Path $output 'ClipMediaTests.exe') ([IO.Path]::GetFullPath($MediaTools)) (Join-Path $artifacts 'clip-fixtures') (Join-Path $artifacts 'clip-media-tests.txt')
    if ($LASTEXITCODE -ne 0) { Get-Content (Join-Path $artifacts 'clip-media-tests.txt'); throw 'Clip media checks failed.' }
    Get-Content (Join-Path $artifacts 'clip-media-tests.txt')
}
