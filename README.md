# ClearFrame

A Windows desktop app for saving YouTube video and audio to your own library. Built with C# and WPF, with a black interface, lemon-lime accents, and explicit quality controls.

## Download for Windows

**[Download ClearFrame for Windows](https://github.com/spade-codee/clearframe/releases/latest/download/ClearFrame-Setup.exe)**

1. Download and open **ClearFrame-Setup.exe**.
2. Click **Install** and stay connected to the internet. Setup takes care of the required video tools.
3. Open **ClearFrame** from the Start menu. Paste a video link, choose **Check link**, **Add to queue**, then **Start queue**.

No commands or administrator account needed. Windows 10/11 on an Intel/AMD 64-bit PC; .NET Framework 4.7.2+ required. The installer is unsigned, so Windows may show an unknown-publisher warning. [Simple setup help](docs/START-HERE.md) · [Release notes and other downloads](https://github.com/spade-codee/clearframe/releases/latest).

**Sharing with a friend? Send this page: [github.com/spade-codee/clearframe](https://github.com/spade-codee/clearframe).** The download link above always points to the latest release.

[![Windows checks](https://github.com/spade-codee/clearframe/actions/workflows/ci.yml/badge.svg)](https://github.com/spade-codee/clearframe/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-D6F64A)](LICENSE)

![ClearFrame interface](docs/screenshot.png)

Interface preview uses sample library items and illustrated thumbnails.

See the [playlist picker preview](docs/playlist.png), also shown with sample entries.

The [clip range dialog](docs/clip-options.png) explains the full-source download and re-encoding requirements.

## Features

- Video: 360p to 8K, plus the best available source resolution. No upscaling or silent quality downgrade.
- Formats: MP4, MKV, WebM and MOV; audio-only MP3 and M4A.
- Compatible H.264/AAC MP4 by default for Windows playback. Advanced codecs remain opt-in and may require additional player support.
- Batches of up to 100 individual YouTube links, with validation and deduplication.
- Playlist picker for choosing videos from the first 200 entries, with unavailable-item indicators and cancellation.
- Pause after the current download, then resume the remaining queue.
- Schedule the queue to start later while ClearFrame stays open, with a visible scheduled time and cancellation control.
- Thumbnail cards with duration, estimated size, speed/ETA details and clearly labeled per-stream progress.
- Clip ranges for individual videos or audio: download the source, then precisely trim and re-encode the selected range.
- Subtitle-language selection, clip caption retiming, and library sorting that preserves queue order.
- Audio-language selection from the checked video's available tracks, with per-download settings and no silent language substitution.
- Searchable library, status filters, queue ordering, retry-all, playback and saved preferences.
- Automatic download-history backup recovery, with damaged-file preservation and visible save errors.
- Optional subtitles, bandwidth limits and automatic video/audio merging.
- Verification of saved media streams, resolution and duration.
- Local-video cleanup: select a fixed logo/text region to blend or blur, or crop away an edge. Compare original and edited frames instantly, then export a separate MP4.

Higher quality and codecs depend on what the source offers. This is an early `0.x` release; live playlist/thumbnail, alternate audio-language and clip-download verification, plus complete UI-driven restart/resume testing, are still outstanding. Automated separate-process restart checks cover queue state, saved options and partial-file preservation. Local synthetic-media tests verify clip exports.

## Portable download (advanced)

1. Download the Windows x64 app ZIP from [Releases](https://github.com/spade-codee/clearframe/releases).
2. Extract the entire ZIP to a writable folder.
3. In PowerShell 7, open that folder and run `./Get-Tools.ps1`. This downloads yt-dlp, Deno and FFmpeg directly from their upstream GitHub releases and verifies the pinned SHA-256 hashes.
4. Run `ClearFrame.exe`.

The portable ZIP requires manual tool setup. For ordinary installation, use the **Windows download** above: it installs the app, downloads the tools automatically and adds a Start menu shortcut. Both options download third-party tools from pinned upstream releases. The installer also registers ClearFrame in Windows Settings > Apps for removal; downloaded media and history are kept.

See the [user guide](docs/USER-GUIDE.md) for quality selection, audio conversion and queue controls. Only save content you have permission to download. ClearFrame is not affiliated with YouTube or Google.

## Build and test

Use PowerShell 7 on Windows with .NET Framework 4.7.2+:

```powershell
git clone https://github.com/spade-codee/clearframe.git
cd clearframe
./scripts/Build.ps1
./scripts/Test.ps1
./scripts/Get-Tools.ps1
./dist/ClearFrame/ClearFrame.exe
```

The build uses the C# compiler included with .NET Framework. No .NET SDK or NuGet packages are needed. Automated tests are offline and do not download YouTube media. Tests write results and UI renders to `artifacts/`.

After tool setup, run `./scripts/Test.ps1 -MediaTools ./dist/ClearFrame/tools` for optional synthetic-video export checks. See [video cleanup](docs/VIDEO-CLEANUP.md) for the editor's workflow and limits. Blending cannot reliably restore detail covered by an embedded logo.

## Versioning and releases

[`VERSION`](VERSION) is the source of truth for the application version. Builds generate assembly metadata and the Windows manifest from it. [CHANGELOG.md](CHANGELOG.md) records changes. Releases are tagged `vMAJOR.MINOR.PATCH`.

Tag pushes run the Windows build and tests, check that the tag matches `VERSION`, and publish a Windows setup executable and portable ZIP with SHA-256 checksums. Installer checks cover installation, reinstallation, tool execution and removal. See [the release guide](docs/RELEASING.md).

## Project layout

```text
source/       C# application, WPF interface, icon and manifest template
scripts/      Build, test, dependency setup and packaging
installer/    Windows setup wizard and automatic tool setup
tests/        Offline subprocess test helpers
config/       Pinned tool releases and SHA-256 hashes
docs/         User guide, screenshot, test limits and release process
.github/      CI, tagged releases and contribution templates
```

## Contributing and licensing

See [CONTRIBUTING.md](CONTRIBUTING.md), [SECURITY.md](SECURITY.md) and [the roadmap](docs/ROADMAP.md).

ClearFrame's application code is [MIT licensed](LICENSE). yt-dlp, FFmpeg, Deno and their dependencies retain their own licenses; see [THIRD-PARTY.md](THIRD-PARTY.md). They are invoked as separate programs and their binaries are excluded from this repository and its application release ZIP.
