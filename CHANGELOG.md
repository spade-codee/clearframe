# Changelog

Changes follow semantic versioning. During `0.x`, minor releases may change behavior or storage formats; patch releases focus on compatible fixes.

## [Unreleased]

## [0.9.0] - 2026-10-08

### Added

- Save and open `.cfclips.json` projects containing the source reference and up to 50 named clip recipes, including order, ranges, crops and output sizes. Empty projects are supported after opening a source.
- Source fingerprint verification and relinking for renamed or moved unchanged videos. Failed or cancelled project opens preserve the current clip list.
- Atomic project saves with a previous-version backup, strict schema/settings validation, size limits and protection for corrupt projects or backups. Save is manual; a dirty marker and discard prompt identify unsaved list changes.
- Reopened clips start Ready without restoring completion state or automatically starting exports. Existing output files remain protected.

### Verified

- Twenty-six project checks, thirty-seven editor-control checks, separate-process project reopening, and a real media export using settings reopened against a renamed source.

## [0.8.0] - 2026-10-08

### Added

- Batches of up to 50 named vertical clips from one local source. Save each range, crop position and output size; edit, remove and reorder clips before exporting.
- Sequential batch export with per-clip verification, progress, cancellation and retry of unfinished clips. Completed videos are retained and skipped on retry.
- Filename validation, all-pending preflight collision checks, protection against files created during export, and a source-metadata check before processing. Originals and existing outputs are never overwritten.
- A warning before closing or replacing the source when saved clips remain unexported. Clip lists are session-only; project persistence is not included.

### Verified

- Twenty batch validation checks, thirty editor-control checks and eight additional synthetic batch-media scenarios, including cancellation, retry, failure cleanup and filename collisions.

## [0.7.0] - 2026-10-05

### Added

- Local vertical clip workflow for Shorts/TikTok: start/end times, a draggable fixed 9:16 crop, original/edited still previews and 1080×1920 or 720×1280 H.264/AAC MP4 export. Each clip saves separately after codec, dimensions and duration verification. Sources are kept; existing output files are protected. SDR, square-pixel sources only; the crop stays fixed throughout the clip and enlargement cannot add detail.
- Built-in full-video Windows conversion for existing SDR media, including AV1/Opus sources. Creates a separate H.264/yuv420p and AAC LC MP4 with strict decode-error handling and output verification.
- Setup help and latest-release links in the More menu, plus a clipping guide included in both installer and portable packages.

### Fixed

- Scheduled queues now defer while a modal editor or dialog is open and fire once the app is ready.

## [0.6.1] - 2026-10-04

### Fixed

- Default to compatible H.264/AAC MP4 for new downloads. The previous modern MP4 default could save AV1 video that Windows' player cannot decode without additional support, even at 1080p.
- Migrate the old default format once for existing preferences; retain other saved formats and explicit advanced MP4 choices made after this update. Existing queued jobs and saved media keep their original settings.
- Label modern MP4 as advanced codecs and explain the Windows playback requirement in the format hint.
- Fourteen playback-default checks passed alongside the existing offline suites. The reported file was confirmed to contain AV1 video and AAC audio in a valid 1920×1080 MP4. The user confirmed that the repaired H.264 copy plays correctly in Windows.

## [0.6.0] - 2026-10-04

### Added

- Windows setup wizard with automatic, SHA-256-verified tool downloads, a Start menu shortcut, optional desktop shortcut and Windows uninstall registration. No terminal commands or administrator account needed.
- Stable `ClearFrame-Setup.exe` release asset and prominent GitHub download link for sharing with nontechnical users.
- Scheduled queue starts within seven days using the local clock. Visible scheduled time, change/cancel controls, busy-state deferral and one-time execution. Closing the app or starting manually cancels the schedule; schedules never restart automatically with the app.
- Simple installation, first-download, repair, upgrade and removal instructions.
- Audio-language selection from the checked video's metadata, including exact regional and audio-description variants. Per-job language persistence, filename/card labels and language-aware deduplication. Missing or incompatible choices never silently use another language; batches/playlists retain Automatic selection.

### Fixed and verified

- Missing-tool messages now direct installed users to rerun setup for repair.
- Installer refuses to update an open ClearFrame instance and checks Windows architecture, version and .NET Framework requirements.
- Ten installer checks cover blocking updates while the app is open, installation, reinstallation, tool execution, installed-app core checks, retained licensing and removal without deleting an untracked user file.
- Fourteen offline scheduling checks cover local/UTC conversion, invalid and daylight-saving times, deferred and one-time starts, cancellation and fresh-session behavior. Existing offline checks pass.
- Nineteen audio-language checks cover stream pairing, codec incompatibility, missing/regional/unlabeled languages, progressive audio, unsafe metadata, persistence and deduplication. Separate app processes restore the selected audio language.
- No live YouTube clip or alternate-language download, or complete manual desktop click-through, was verified. Setup requires internet; the application and installer remain unsigned.

## [0.5.0] - 2026-10-03

### Added

- Clip ranges for checked individual links, with seconds, mm:ss and hh:mm:ss input. Full source media is downloaded before precise local re-encoding; batches and playlist picks remain full-length.
- Clip exports in all seven existing profiles, with duration/resolution/audio verification and unique filenames. Video clips require SDR and even dimensions; clipping is not lossless.
- Subtitle selection across 15 languages, including regional variants. Preferences and per-job choices survive restart. Clip SRT captions are clamped and shifted onto the new timeline.
- Library sorting by added date, title, size or output duration. Sorting only changes the view; download order remains unchanged. Queue-order view enables manual reordering again.
- Per-job added timestamps and final saved sizes, with compatibility for older history files.

### Fixed and verified

- Caption save failures retain verified media and report a warning instead of marking an already saved video as failed.
- Duplicate checks distinguish clip ranges and subtitle languages.
- 36 new option/UI checks passed, plus existing offline, filesystem and subprocess suites. Separate app processes retained clip settings, subtitle language, sort preferences and partial-file identity.
- Nine clip-media checks passed across seven profiles, including a decoded-frame comparison at a non-keyframe start, source preservation and overwrite protection. All 16 video-cleanup media checks also passed.
- Live YouTube clip download and manual desktop interaction remain unverified. Precise trimming was tested with synthetic local media; it currently downloads the full source first.

## [0.4.0] - 2026-10-03

### Added

- Playlist picker: preview the first 200 entries, choose individual videos, select available entries together, and cancel a lookup. Reported private, deleted, account-required and live entries cannot be selected. Actual availability and quality are checked again per download.
- Pause after current: finish the current download attempt, leave remaining items queued, then resume on demand. Immediate Stop remains available.
- Download cards with thumbnails, duration, estimated source size, speed/ETA details and explicit per-stream progress labels. Missing thumbnails fall back to format/quality badges.
- Bounded thumbnail requests to YouTube's image host, with in-memory caching and no arbitrary metadata URL fetching.

### Verified and limitations

- 24 playlist/queue/card checks, 68 core checks, 46 existing offline interface checks, 12 filesystem recovery checks and subprocess checks passed locally.
- Separate application processes preserved interrupted-job status, queued jobs, partial-file bytes and retry staging identity. These checks do not open desktop windows or resume a live network download.
- Main interface and playlist picker were rendered and visually inspected, including the main window at its minimum supported size.
- Automatic approval review blocked the live playlist-and-thumbnail test. Live lookup, thumbnail delivery and full manual desktop restart/resume remain unverified for this release.
- Playlists beyond the first 200 entries, channel import and account-required playlists are not supported.

## [0.3.2] - 2026-10-02

### Fixed

- Recover missing or unreadable download history from `state.json.bak`. Preserve unreadable files separately before replacing them, and retain the usable backup during recovery.
- Keep history-save failures visible through later queue status updates; show the full message on hover and clear it after a successful save.
- Ignore delayed progress and merge messages after cancellation, verification or completion.
- Exclude the active item from retry-all while its current attempt is finishing.
- Use the requested quality when identifying duplicates, including Best available and lower-resolution fallback. Single links and batch imports now use the same matching rules; subtitle and strict-quality requests remain distinct.

### Verified

- 68 core checks, 46 offline WPF checks, 12 real-filesystem history checks and subprocess checks passed locally.
- Recovery checks include corrupt/missing history, both files damaged, backup preservation, failed atomic replacement and temporary-file cleanup.
- No live network download or full desktop restart test was performed for this patch.

## [0.3.1] - 2026-10-02

### Improved

- Instant **Show original / Show edit** comparison of the same preview frame.
- Changing the rectangle or method discards stale edited previews and returns to the original. Changing the frame time clears both previews.

### Fixed

- Consistent video stream selection for metadata, previews and exports; embedded cover art is excluded.
- Correct preview proportions and selection mapping for non-square-pixel video.
- Invalid or non-finite frame times and source durations are rejected before processing.
- Export-location failures show a message in the editor.

### Verified

- 68 core checks, 20 offline WPF control checks, subprocess checks and 16 synthetic-media checks passed locally.
- Added preview generation tests for all three methods, plus silent export, cover art, multiple video streams and non-square-pixel preview tests.

## [0.3.0] - 2026-10-01

### Added

- Local-video cleanup editor for fixed logos/text: blend a selected region, blur it, or crop to the selected picture area.
- Drag-to-select and numeric rectangle controls, frame-time selection, and before-export frame previews.
- New-file MP4 export, cancellation, audio retention and output verification. Existing files are never overwritten.
- Rotation-aware editing for common 90-degree orientations; explicit rejection of HDR input to avoid unintentional color changes.
- Synthetic-media regression tests for all three filters, small blur regions, rotated files and source preservation.

### Limits

- Blending estimates pixels and can leave artifacts; it does not recover hidden image detail.
- Selection is fixed for the entire video; moving-logo tracking, variable regions and HDR editing are not implemented.
- Video is re-encoded to H.264. Only the first audio track is kept; AAC can be copied, other audio is converted.

## [0.2.0] - 2026-09-30

### Added

- Black/charcoal interface with lemon-lime and red accents.
- Batch import of up to 100 individual links.
- MP3 and M4A audio downloads.
- Search, library filters, playback, queue ordering and retry-all.
- Saved preferences and per-job bandwidth limits.
- Public source repository, MIT license, versioned builds, Windows CI and tagged releases.
- Pinned, checksum-verified tool setup without vendored third-party binaries.

### Retained from the initial local prototype

- 360p through 8K and uncapped source selection.
- MP4, MKV, WebM and MOV output; video/audio merging and media verification.
- Queue persistence, retries, optional English subtitles and engine updates.

### Known limitations

- Playlist expansion, scheduled downloads, clips and account-required downloads are not implemented.
- Live network tests covered short clips; full interactive restart/resume testing is not complete.

[Unreleased]: https://github.com/spade-codee/clearframe/compare/v0.9.0...HEAD
[0.9.0]: https://github.com/spade-codee/clearframe/releases/tag/v0.9.0
[0.8.0]: https://github.com/spade-codee/clearframe/releases/tag/v0.8.0
[0.7.0]: https://github.com/spade-codee/clearframe/releases/tag/v0.7.0
[0.6.1]: https://github.com/spade-codee/clearframe/releases/tag/v0.6.1
[0.6.0]: https://github.com/spade-codee/clearframe/releases/tag/v0.6.0
[0.5.0]: https://github.com/spade-codee/clearframe/releases/tag/v0.5.0
[0.4.0]: https://github.com/spade-codee/clearframe/releases/tag/v0.4.0
[0.3.2]: https://github.com/spade-codee/clearframe/releases/tag/v0.3.2
[0.3.1]: https://github.com/spade-codee/clearframe/releases/tag/v0.3.1
[0.3.0]: https://github.com/spade-codee/clearframe/releases/tag/v0.3.0
[0.2.0]: https://github.com/spade-codee/clearframe/releases/tag/v0.2.0
