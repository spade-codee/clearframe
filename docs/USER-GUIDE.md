# ClearFrame 0.3 for Windows

A portable desktop downloader for YouTube videos, with source-quality video and sound, from 360p through 8K and uncapped best available.

## New in this version

- **Video cleanup** opens a local editor for blending or blurring a selected fixed logo region, or cropping an edge. The original is kept and edits are exported to a new MP4. See [VIDEO-CLEANUP.md](VIDEO-CLEANUP.md).
- Black and charcoal interface with lemon-lime actions, red stop/error accents, dark format selectors and a library sidebar.
- Batch import of up to 100 individual links, with link validation and duplicate removal.
- MP3 and M4A audio downloads alongside the existing video formats.
- Library search, status filters, audio view, playback, queue reordering, retry-all and completed-history cleanup.
- Saved quality, format, speed limit, subtitle and fallback preferences.
- Optional bandwidth limits of 1, 2, 5 or 10 MiB/s. Limits apply to newly added jobs; one fragment at a time is used when a limit is selected.

## Start

1. Extract the release ZIP. Run `./Get-Tools.ps1` in PowerShell 7 to download the required tools, then keep the resulting **tools** folder beside **ClearFrame.exe**.
2. Double-click **ClearFrame.exe**. No administrator access or installation is required.
3. Paste a YouTube video or Shorts link and select **Check link**.
4. Review the resolution, frame rate and estimated size. Choose a save folder, then **Add to queue**.
5. Add more videos if desired, then select **Start queue**. Select a finished item and **Show file** to find it.

Requires 64-bit Windows 10/11 with .NET Framework 4.7.2 or newer. The application is locally built and unsigned.

## Batch links and audio

Choose your format, quality, speed limit and destination first, then select **Add a batch** in the sidebar. Paste up to 100 individual YouTube links, one per line. An invalid link prevents the batch from being added until corrected. Duplicate links within the batch are collapsed; existing non-failed items with matching format, destination and resolution are skipped. Batch titles and actual availability are checked when each job starts. Playlist URLs are not expanded.

Select **MP3 · audio / best VBR** to convert the best available audio stream into a variable-bitrate MP3. Select **M4A · audio / AAC** to prefer YouTube's original AAC stream; other source audio is converted to AAC when necessary. Video resolution and subtitle controls are disabled for audio downloads. Converting audio does not improve the original recording, and the size shown before download describes the source stream, not a guaranteed final converted-file size.

## Library controls

- Use the sidebar to view all, active, completed, audio, or failed/stopped items. Search matches titles, links and file formats. Filtering does not restrict which queued jobs **Start queue** will run.
- Select a queued item and use **↑ / ↓** to change its position among queued jobs.
- Select a completed item and use **▶** or double-click it to play it in your default media app.
- The **•••** menu contains retry-all, clear completed history, diagnostic details and removal. Clearing history and removing items do not delete media files.
- **Ctrl+L** focuses the URL field; **Ctrl+F** focuses search; **Ctrl+B** opens batch import.
- Preferences are remembered on restart. Existing jobs retain the options captured when added.

## Quality and formats

- Choose **360p, 480p, 720p, 1080p, 1440p, 2160p (4K), or 4320p (8K)**. The selected resolution must be present in the source; the app never upscales.
- **Best source quality** has no resolution cap and selects the highest available resolution compatible with the selected format.
- Lower-resolution fallback is **off by default**. Enable **Allow a lower resolution** to permit it. The actual resolution is shown before adding the download and again in the queue. Streams are rechecked when each job starts, so availability may change.
- **MP4 · modern codecs** accepts H.264, AV1, VP9 and HEVC video with AAC audio. This supports higher qualities where YouTube offers newer codecs; your player needs support for the selected codec, which is shown in the preview.
- **MP4 · compatible H.264** selects H.264 video with AAC audio for broad playback compatibility. Higher resolutions may not be offered in H.264; try modern MP4 or MKV.
- **MKV** allows newer video/audio codecs, preserving the selected source quality without transcoding. Your player must support the codecs in the file.
- **WebM** selects VP8, VP9 or AV1 video with Opus or Vorbis audio.
- **MOV** selects H.264 video and AAC audio in a QuickTime container. Its available resolutions depend on H.264 source availability.
- Resolution is measured using the shorter image dimension, so both 1920×1080 landscape video and 1080×1920 portrait video count as 1080p. Unusual aspect ratios may not match YouTube's displayed resolution label.
- The source frame rate is retained, including 60 fps when offered; no artificial frames or detail are added. Among eligible streams, the app prioritizes resolution and frame rate, then the engine's format preference. Original HDR streams can be retained, but this version does not provide an HDR/SDR selector or tone mapping.
- Separate video and audio streams are merged automatically. Size estimates can be unavailable or approximate.
- Completion requires a readable file containing video and audio, the selected resolution, and a duration consistent with the source. This is a structural check, not a frame-by-frame corruption scan.

## Queue and recovery

- The queue runs one video at a time. Progress refers to the current stream, so it may restart when audio begins. Merging and verification appear as separate stages.
- **Stop queue** stops the current worker and leaves remaining jobs queued. Choose the stopped item, **Retry selected**, then **Start queue**. Partial downloads resume when supported by the stream and engine.
- Queue and history are saved in `%LOCALAPPDATA%\ClearFrame\state.json`. Interrupted jobs are restored as stopped; downloads never start automatically on app launch.
- If history is missing or unreadable, ClearFrame tries `state.json.bak`. This backup may be one save behind. Unreadable files are copied to names containing `.corrupt-` before new history replaces them; saved media files are unaffected.
- If history cannot be saved, a **History not saved** message stays at the start of the status bar until a save succeeds. Hover over the status bar to read the full message. Changes remain in memory and may be lost if you close before saving succeeds.
- Duplicate checks compare the video link, destination, format and requested quality, rather than the resolution eventually downloaded. Requests with different subtitle settings or strict/fallback settings can be added separately. Audio requests ignore the video-resolution preference.
- Partial files stay in `.clearframe` inside your chosen save folder. Unverified merged files are retained with an `.unverified-...` suffix when retried. Failed jobs may require additional disk space because those files are retained.
- Completed files are moved into your chosen folder. Existing files are never intentionally overwritten; a numbered name is used for collisions.
- Removing a queue/history item from the **•••** menu keeps downloaded and partial files. You can delete abandoned partial files manually while the queue is stopped.
- Changing the save folder applies to new jobs; existing jobs retain their original destination.
- Disk-space checks reserve roughly twice the estimated download size plus working space. When size is unknown, a minimum-space check cannot guarantee sufficient capacity.

## Subtitles and unavailable videos

English subtitles are optional separate `.srt` files, including automatic captions when available. Their accuracy depends on the source. If YouTube offers none, the video can still finish. A subtitle network error may cause the job to fail; retry without subtitles if needed.

YouTube may reject requests, rate-limit downloads or change its delivery methods. **Update engine** checks the official yt-dlp release. It does not update FFmpeg or Deno. Download success cannot be guaranteed for every video or network.

This version supports individual public videos and Shorts, including batches of individual links, plus cleanup of local videos. It does not import browser cookies, sign into YouTube, download live streams, expand playlists/channels, select alternate audio languages, or bypass account/region restrictions. Only download content you have permission to save.

## Privacy and maintenance

There is no analytics service or application backend. Download requests go directly to YouTube and its media hosts; engine update requests go to GitHub. Titles, links, destinations and diagnostic logs are stored locally. No Google account password is requested. Logs can contain URLs and local paths, so review them before sharing.

The setup script downloads the engine, merger, verifier and JavaScript runtime into `tools`; the public application ZIP does not bundle those executables. Release information and downloaded-asset hashes are in `tools/versions.json`; attribution and upstream license links are in `THIRD-PARTY.md`; archive license files are retained in `tools/licenses`.

The full C# and XAML source is in `source`. To rebuild with the Windows .NET Framework compiler, run `scripts/Build.ps1` in PowerShell 7. No NuGet packages are required.
