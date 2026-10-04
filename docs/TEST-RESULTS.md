# Verification — 4 October 2026

## Version 0.6.1

- Fourteen playback-default checks passed for fresh and missing preferences, one-time migration of the old default, preservation of other formats and later explicit advanced choices, invalid settings, the persisted migration marker and visible playback guidance.
- The reported 1080p file was inspected locally: AV1 video, AAC audio, 1920×1080, SDR, 260 seconds, valid MP4 container. This supports a decoder-compatibility diagnosis; an MP4 extension alone does not imply H.264.
- A separate H.264/yuv420p + AAC copy was exported locally with strict decode-error handling. Its 1920×1080 dimensions, audio and duration were verified, and the original file's SHA-256 remained unchanged. The repair operation is not an automatic conversion of existing library items.
- Existing offline suites passed. The user confirmed that the repaired copy plays correctly in Windows. Full manual application click-through remains unverified. New defaults affect future additions; existing jobs and media are not silently converted.

## Version 0.6.0

- Ten installer checks cover refusing updates while the application's mutex is held, two unattended installation passes in an isolated workspace directory, application version and Windows uninstall registration, execution of all four installed tools, retained FFmpeg license, installed-app core checks and uninstall that preserves an untracked user file. The test downloads the real pinned upstream tools. It refuses to run if this account already has an installed ClearFrame.
- Fourteen offline scheduling checks passed: local/UTC conversion, invalid/past/out-of-range dates, daylight-saving gaps and ambiguities, not starting early, waiting while busy, late one-time firing, cancellation, visible scheduled state and no automatic schedule restoration in a new session.
- Nineteen audio-language fixture checks passed for stream selection, codec compatibility, absent and regional languages, progressive audio, unlabeled legacy streams, malformed/DRM metadata, persistence, duplicates and link-reset behavior. Separate app processes restored the explicit audio-language choice. No live alternate-language download was verified.
- An additional local reinstall run encountered an upstream Deno download timeout; setup reported failure before replacing application files. The complete install/reinstall/tool-execution/uninstall suite subsequently passed on GitHub's clean Windows runner.
- Existing 68 core, 36 main-interface, 10 editor, 36 clip/caption/library, 24 playlist/card, 12 filesystem, subprocess and separate-process recovery checks passed.
- Main-window renders inspected at 1280×880 and 1064×762. No full manual installer or application click-through was performed. The unattended setup tests do not validate every interactive wizard path or Windows security warning.
- Clip/media processing is unchanged from 0.5.0; its synthetic-media results remain recorded below. Live YouTube clip downloads remain unverified.

## Version 0.5.0

- 36 new offline checks passed for time parsing, clip bounds, subtitle-language selection, caption retiming, duplicate detection, all six library sort modes, queue-order preservation and settings serialization.
- 24 playlist/card checks, 68 core checks, 36 main-interface checks, 10 editor checks, 12 filesystem recovery checks and subprocess checks passed.
- Two separate application processes verified recovery of clip ranges, subtitle language, library sorting and partial download state without a desktop window or live download.
- Nine local clip-media checks passed using synthetic media and the pinned tools: all seven output profiles, a cut between keyframes, output overwrite protection and unchanged source bytes. The MP4 first-frame comparison had mean absolute pixel error 0.59 against the requested source frame (threshold 8).
- All 16 existing video-cleanup media checks passed again.
- Offline WPF renders cover the main window at 1280×880 and 1064×762 and the clip dialog. Screenshots use labeled sample jobs.
- No live YouTube clip download or full manual desktop interaction was verified. Clips download the complete source and then re-encode locally; the media checks verify that local processing, not the complete network workflow. GitHub CI runs the offline checks; optional FFmpeg media checks run locally.

## Version 0.4.0

- 24 new checks passed for playlist link normalization, invalid links, unavailable/duplicate entries, the 200-entry limit, selection controls, cancellation, queue addition, pause/resume/stop behavior and card metadata.
- 68 core checks, 36 main-interface checks, 10 editor checks, 12 filesystem recovery checks and subprocess checks passed.
- Two separate application processes tested restart using isolated state: interrupted work restored as stopped, waiting work remained queued, partial-file bytes survived, and retry retained the same staging ID. No desktop window or network download was used.
- Visually inspected populated and empty main-window renders at 1280×880 and 1064×762, plus the playlist picker with synthetic entries. Public screenshots show clearly labeled sample jobs and illustrated thumbnails.
- Automatic approval review rejected the live playlist-and-thumbnail test launch with “blocked by policy.” It was not retried. Live playlist lookup, thumbnail retrieval and full desktop/network resume remain unverified.
- Video-cleanup behavior is unchanged; prior real-media checks are recorded below.

## Version 0.3.2

- 68 core checks, 36 main-interface checks, 10 editor-control checks and subprocess argument/cancellation/timeout checks passed.
- 12 filesystem checks use temporary fixtures: save/load roundtrips, atomic backup rotation, corrupt/missing primary recovery, preserving both damaged histories, a locked destination, and temporary-file cleanup.
- New offline WPF cases cover requested-quality deduplication, audio and subtitle settings, retry-all exclusion of an active item, restart status normalization, persistent save-error text and delayed progress messages.
- These checks do not simulate a full running desktop restart or a live download resuming. Media-cleanup code was unchanged; its prior synthetic-media results are recorded below.

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
- The original live download test used the engine's short-clip option, not an entire long video. The app downloads complete sources; version 0.5 can trim them locally afterward.
- 8K selection was tested with metadata fixtures; no live 8K download was performed.
- Alternate container files were tested with local media; live downloads were tested in MP4.
- File verification checks stream presence, dimensions and duration, not every decoded frame.
- Availability can change with YouTube, its restrictions, and the user's network. No universal download-success guarantee is made.
