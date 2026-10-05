; Builds a small online installer. Tools are downloaded directly from upstream,
; verified before installation, and tracked by the uninstaller.
#include "..\.build\installer-tools.iss"

[Setup]
AppId={{E03EA218-6503-46D0-AE9B-2B7172DC92B4}
AppName=ClearFrame
AppVersion={#AppVersion}
AppPublisher=ClearFrame contributors
AppPublisherURL=https://github.com/spade-codee/clearframe
AppSupportURL=https://github.com/spade-codee/clearframe/issues
AppUpdatesURL=https://github.com/spade-codee/clearframe/releases/latest
AppMutex=Local\ClearFrame.Desktop
DefaultDirName={localappdata}\Programs\ClearFrame
DefaultGroupName=ClearFrame
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.17763
WizardStyle=modern
SetupIconFile=..\source\ClearFrame.ico
UninstallDisplayIcon={app}\ClearFrame.exe
LicenseFile=..\LICENSE
InfoBeforeFile=setup-info.txt
OutputDir=..\artifacts
OutputBaseFilename=ClearFrame-{#AppVersion}-Setup
Compression=lzma2
SolidCompression=yes
ArchiveExtraction=full
ExtraDiskSpaceRequired=1073741824
CloseApplications=no
RestartApplications=no
Uninstallable=yes
VersionInfoVersion={#AppVersion}.0

[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "..\dist\ClearFrame\ClearFrame.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\ClearFrame\ClearFrame.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD-PARTY.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\docs\USER-GUIDE.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\docs\VIDEO-CLEANUP.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\docs\video-cleanup.png"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\docs\CLIPPING.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\docs\vertical-clips.png"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\config\tools.lock.json"; DestDir: "{app}\tools"; DestName: "versions.json"; Flags: ignoreversion
Source: "{tmp}\yt-dlp.exe"; DestDir: "{app}\tools"; Flags: external ignoreversion; ExternalSize: 40000000
Source: "{tmp}\deno\deno.exe"; DestDir: "{app}\tools"; Flags: external ignoreversion; ExternalSize: 180000000
Source: "{tmp}\ffmpeg\ffmpeg.exe"; DestDir: "{app}\tools"; Flags: external ignoreversion; ExternalSize: 1000000
Source: "{tmp}\ffmpeg\ffprobe.exe"; DestDir: "{app}\tools"; Flags: external ignoreversion; ExternalSize: 1000000
Source: "{tmp}\ffmpeg\*.dll"; DestDir: "{app}\tools"; Flags: external ignoreversion; ExternalSize: 180000000
Source: "{tmp}\ffmpeg\LICENSE*"; DestDir: "{app}\tools\licenses"; Flags: external ignoreversion; ExternalSize: 50000

[Icons]
Name: "{autoprograms}\ClearFrame"; Filename: "{app}\ClearFrame.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\ClearFrame"; Filename: "{app}\ClearFrame.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\ClearFrame.exe"; Description: "Open ClearFrame"; Flags: nowait postinstall skipifsilent

[Code]
var
  DownloadPage: TDownloadWizardPage;
  ExtractionPage: TExtractionWizardPage;
  ToolsReady: Boolean;

function InitializeSetup: Boolean;
begin
  Result := IsDotNetInstalled(net472, 0);
  if not Result then
    SuppressibleMsgBox('ClearFrame needs .NET Framework 4.7.2 or newer. Install Windows updates, then run this setup again.', mbError, MB_OK, IDOK);
end;

procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage('Getting ClearFrame ready', 'Downloading and checking the required video tools. Please stay connected to the internet.', nil);
  DownloadPage.ShowBaseNameInsteadOfUrl := True;
  ExtractionPage := CreateExtractionPage('Preparing video tools', 'This may take a moment.', nil);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if ToolsReady then exit;
  DownloadPage.Clear;
  DownloadPage.Add('{#ToolUrl0}', 'yt-dlp.exe', '{#ToolHash0}');
  DownloadPage.Add('{#ToolUrl1}', 'deno.zip', '{#ToolHash1}');
  DownloadPage.Add('{#ToolUrl2}', 'ffmpeg.zip', '{#ToolHash2}');
  DownloadPage.Show;
  try
    try
      DownloadPage.Download;
    except
      Result := 'The video tools could not be downloaded or verified. Check your internet connection and try again. ' + GetExceptionMessage;
    end;
  finally
    DownloadPage.Hide;
  end;
  if Result <> '' then exit;
  ExtractionPage.Clear;
  ExtractionPage.Add(ExpandConstant('{tmp}\deno.zip'), ExpandConstant('{tmp}\deno'), False);
  ExtractionPage.Add(ExpandConstant('{tmp}\ffmpeg.zip'), ExpandConstant('{tmp}\ffmpeg'), False);
  ExtractionPage.Show;
  try
    try
      ExtractionPage.Extract;
      if not FileExists(ExpandConstant('{tmp}\deno\deno.exe')) or
         not FileExists(ExpandConstant('{tmp}\ffmpeg\ffmpeg.exe')) or
         not FileExists(ExpandConstant('{tmp}\ffmpeg\ffprobe.exe')) then
        RaiseException('An expected video tool is missing.');
      ToolsReady := True;
    except
      Result := 'The video tools could not be prepared. Please try again. ' + GetExceptionMessage;
    end;
  finally
    ExtractionPage.Hide;
  end;
end;
