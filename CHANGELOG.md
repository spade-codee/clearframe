# Changelog

Changes follow semantic versioning. During `0.x`, minor releases may change behavior or storage formats; patch releases focus on compatible fixes.

## [Unreleased]

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

[Unreleased]: https://github.com/spade-codee/clearframe/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/spade-codee/clearframe/releases/tag/v0.3.0
[0.2.0]: https://github.com/spade-codee/clearframe/releases/tag/v0.2.0
