# Changelog

Changes follow semantic versioning. During `0.x`, minor releases may change behavior or storage formats; patch releases focus on compatible fixes.

## [Unreleased]

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

[Unreleased]: https://github.com/spade-codee/clearframe/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/spade-codee/clearframe/releases/tag/v0.2.0
