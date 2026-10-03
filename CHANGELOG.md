# Changelog

Changes follow semantic versioning. During `0.x`, minor releases may change behavior or storage formats; patch releases focus on compatible fixes.

## [Unreleased]

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

[Unreleased]: https://github.com/spade-codee/clearframe/compare/v0.4.0...HEAD
[0.4.0]: https://github.com/spade-codee/clearframe/releases/tag/v0.4.0
[0.3.2]: https://github.com/spade-codee/clearframe/releases/tag/v0.3.2
[0.3.1]: https://github.com/spade-codee/clearframe/releases/tag/v0.3.1
[0.3.0]: https://github.com/spade-codee/clearframe/releases/tag/v0.3.0
[0.2.0]: https://github.com/spade-codee/clearframe/releases/tag/v0.2.0
