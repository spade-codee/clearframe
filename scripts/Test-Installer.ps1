#requires -Version 7.0
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$version = (Get-Content -LiteralPath (Join-Path $root 'VERSION') -Raw).Trim()
$setup = Join-Path $root "artifacts\ClearFrame-$version-Setup.exe"
$key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{E03EA218-6503-46D0-AE9B-2B7172DC92B4}_is1'
if (Test-Path -LiteralPath $key) { throw 'An installed ClearFrame exists. Run this test on a clean account to avoid changing it.' }
$testRoot = Join-Path $root ('.build\installer-test-' + [Guid]::NewGuid().ToString('N'))
$target = Join-Path $testRoot 'ClearFrame'
New-Item -ItemType Directory -Force $testRoot | Out-Null
$results = [Collections.Generic.List[string]]::new()
$created = $false
$mutex = [Threading.Mutex]::new($true, 'Local\ClearFrame.Desktop', [ref]$created)
try {
    if (-not $created) { throw 'ClearFrame is running; close it before installer testing.' }
    $mutexArgs = @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOICONS','/TASKS=')
    $mutexArgs += '/DIR="' + $target + '"'
    $mutexArgs += '/LOG="' + (Join-Path $root 'artifacts\installer-mutex.log') + '"'
    $process = Start-Process -FilePath $setup -ArgumentList $mutexArgs -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(20000)) { $process.Kill(); throw 'Installer did not exit when the app mutex was held.' }
    if ($process.ExitCode -eq 0 -or (Test-Path -LiteralPath $key)) { throw 'Installer did not block an open application.' }
    $results.Add('PASS: installer refuses to update while the app mutex is held')
} finally { if ($created) { $mutex.ReleaseMutex() }; $mutex.Dispose() }
try {
    foreach ($pass in 1,2) {
        $arguments = @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOICONS','/TASKS=')
        $arguments += '/DIR="' + $target + '"'
        $arguments += '/LOG="' + (Join-Path $root "artifacts\installer-pass-$pass.log") + '"'
        $process = Start-Process -FilePath $setup -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
        if ($process.ExitCode -ne 0) { throw "Installer pass $pass failed: $($process.ExitCode). See installer log." }
        if ([Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $target 'ClearFrame.exe')).FileVersion -ne "$version.0") { throw 'Installed application has incorrect version.' }
        if (-not (Test-Path -LiteralPath $key)) { throw 'Missing Windows uninstall registration.' }
        if ($pass -eq 1) { 'keep this file' | Set-Content -LiteralPath (Join-Path $target 'user-file.txt') }
        if ((Get-Content -LiteralPath (Join-Path $target 'user-file.txt') -Raw).Trim() -ne 'keep this file') { throw 'Reinstall changed a user file.' }
        $results.Add("PASS: installation pass $pass, version, uninstall registration and user file preservation")
    }
    foreach ($tool in 'yt-dlp','deno','ffmpeg','ffprobe') {
        $argument = if ($tool -in 'ffmpeg','ffprobe') { '-version' } else { '--version' }
        & (Join-Path $target "tools\$tool.exe") $argument | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Installed $tool cannot run." }
        $results.Add("PASS: installed $tool executes")
    }
    if (-not (Test-Path -LiteralPath (Join-Path $target 'tools\licenses\LICENSE.txt'))) { throw 'FFmpeg license missing.' }
    $results.Add('PASS: FFmpeg archive license retained')
    $report = Join-Path $testRoot 'core-check.txt'
    $process = Start-Process -FilePath (Join-Path $target 'ClearFrame.exe') -ArgumentList @('--self-test',('"' + $report + '"')) -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $report)) { throw 'Installed app offline checks failed.' }
    $results.Add('PASS: installed application core checks')
} finally {
    $uninstall = Join-Path $target 'unins000.exe'
    if (Test-Path -LiteralPath $uninstall) {
        $process = Start-Process -FilePath $uninstall -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -WindowStyle Hidden -PassThru -Wait
        if ($process.ExitCode -ne 0) { throw 'Test installation could not be uninstalled.' }
    }
}
if ((Test-Path -LiteralPath (Join-Path $target 'ClearFrame.exe')) -or (Test-Path -LiteralPath $key)) { throw 'Uninstall left application or registration behind.' }
if (-not (Test-Path -LiteralPath (Join-Path $target 'user-file.txt'))) { throw 'Uninstall deleted an untracked user file.' }
$results.Add('PASS: uninstall removes application and registration; keeps untracked user file')
$results | Set-Content -LiteralPath (Join-Path $root 'artifacts\installer-tests.txt')
$results
