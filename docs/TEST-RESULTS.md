# Verification — 2 October 2026

## Version 0.3.1

- 68 core checks, 10 main-interface checks, 10 editor-control checks and subprocess argument/cancellation/timeout checks passed.
- Editor checks construct real WPF controls without opening a desktop window: comparison toggle, stale-preview invalidation after numeric/drag/method/time changes, busy-state actions and non-square-pixel selection mapping.
- 16 synthetic-media checks passed with the pinned FFmpeg build. Added all three preview filters, silent video export, attached cover art exclusion, first-video selection in a multistream file, and correct display proportions for non-square-pixel previews.
- Full manual interaction remains unverified. GitHub CI runs the core, offline WPF and subprocess checks; media checks were run locally with the pinned tools.

## Version 0.3.0

- 55 core checks and 10 offline WPF control checks passed, plus subprocess argument/cancellation/timeout checks.
- Real FFmpeg exports on synthetic video passed for blend, blur and crop. Dimensions, audio and duration were verified.
- Existing-output overwrite protection, unchanged source bytes, small edge blur regions and 90-degree rotation handling passed.
- The editor and main interface were rendered without opening a desktop window and visually inspected. Full manual interaction is still unverified.
- Dependency setup succeeded using pinned release archives and SHA-256 verification, including the versioned FFmpeg archive.
- GitHub's Windows build/tests and tagged publication passed for the first public v0.2.0 release; later run results are recorded in GitHub Actions.

## Version 0.2 redesign and feature checks

- 43 core checks passed, covering the prior video-quality rules plus MP3/M4A source selection, audio-file verification, batch limits, URL deduplication, invalid-batch rejection and persisted audio-job fields.
- 10 offline WPF control checks passed: library views, case-insensitive search, format search, queue movement, audio/video control switching, speed-limit selection and preference serialization. These construct the actual controls without showing a desktop window or making network requests.
- Real two-second MP3 and M4A downloads succeeded with the bundled engine and FFmpeg. FFprobe and the application's verifier confirmed MP3/AAC audio and matching duration. YouTube returned a recoverable initial-data warning for the MP3 request; the engine retried successfully.
- Process argument, cancellation and timeout checks passed again against the updated executable.
- Inspected interface renders at 1280×880 and 1064×762 content sizes, then fixed sidebar clipping at the smaller size.
- Full interactive click-through, network batch completion and restart/resume remain unverified. The offline checks do not replace those tests.

The earlier video-engine results below remain relevant; video quality/format support is retained.

## Passed

- Native Windows x64 executable compiled with the .NET Framework C# compiler.
- 32 core checks: accepted/rejected YouTube URL forms; 1080p, 1440p, 4K and 8K selection; uncapped source quality; explicit fallback; MP4/MKV/WebM/MOV codec pairing; portrait dimensions; audio requirements; source-duration validation; process-argument escaping.
- Process tests with real subprocesses: spaces, quotes, trailing backslashes, shell metacharacters and Unicode survive argument roundtrips; cancellation and timeouts terminate the worker.
- Live metadata extraction from the official Blender Foundation Big Buck Bunny YouTube upload exposed streams through 4K.
- Downloaded a two-second 3840×2160 source clip through the bundled engine and FFmpeg; FFprobe confirmed 4K video, audio and expected duration. The source was identified by YouTube as Creative Commons Attribution. This test clip is not included in the deliverable.
- Applied the application's real format-selection code to that live metadata: modern MP4 chose AV1 + AAC; MKV and WebM chose AV1 + Opus at 4K / 60 fps.
- Generated local two-second 1080p test media and verified readable MP4, MKV, WebM and MOV files with video, audio and matching duration using the application's verifier.
- Rendered and visually inspected the WPF interface.
- Downloaded dependency archives matched upstream SHA-256 digests; the yt-dlp executable also matched the project's published SHA-256 list.

## Limits

- Automatic approval review rejected the hidden desktop integration-test launch with “blocked by policy.” A full interactive click-through, real queue restart and UI-driven resume were not verified.
- The live download test used the engine's short-clip option, not an entire long video. The app downloads complete videos.
- 8K selection was tested with metadata fixtures; no live 8K download was performed.
- Alternate container files were tested with local media; live downloads were tested in MP4.
- File verification checks stream presence, dimensions and duration, not every decoded frame.
- Availability can change with YouTube, its restrictions, and the user's network. No universal download-success guarantee is made.
