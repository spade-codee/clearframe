# ClearFrame 0.11.0 for Windows

A portable desktop downloader for YouTube videos, with source-quality video and sound, from 360p through 8K and uncapped best available.

## New in this version

- **Undo / Redo:** reverse clip-field edits, saved changes, removals and ordering from the vertical editor or batch window. Use Ctrl+Z, Ctrl+Y or Ctrl+Shift+Z. Up to 100 steps are kept in the current session; exported files stay untouched. See [CLIPPING.md](CLIPPING.md) for history boundaries and project/recovery behavior.
- **Cleaner dirty tracking:** moving a clip in the batch no longer marks unchanged editor fields as unsaved. The preview can shrink further in smaller windows to keep the controls visible.

## Other recent features

- **Timeline controls:** seek through the source, load still previews automatically, and mark clip start/end visually. Play/pause uses Windows decoding; FFmpeg still previews remain available if Windows cannot play the codec.
- **Local recovery:** clip lists and unfinished editor fields are saved after a short idle period. **Recover…** restores a selected snapshot after checking the original source. Recovery keeps the last completed snapshot and leaves manual project files untouched. See [CLIPPING.md](CLIPPING.md) for timing, storage and limits.
- **Unsaved-field warning:** closing or changing sources now also detects editor changes that have not yet been added to the clip list.
- **Saved clip projects:** use **Save project** and **Open project** in the vertical editor to continue later. Projects keep your saved clip settings and verify the original source; a moved video can be located again. Save is manual, and reopened clips start Ready. See [CLIPPING.md](CLIPPING.md) for backups, relinking and workflow details.
- **Named clip batches:** save up to 50 clip ranges from one local video, with independent crops and output sizes. Review, edit, remove or reorder them, then export pending clips to a folder. Cancellation and failures keep completed outputs; retry skips those completed clips within the current session.
- **Vertical clips:** choose **••• > Create a vertical clip (9:16)…**, open a local video, set start/end times and position the crop. Export a separate 1080×1920 or 720×1280 H.264/AAC MP4. See [CLIPPING.md](CLIPPING.md) for the workflow and resizing limits.
- **Convert a video for Windows:** the **•••** menu now repairs playback compatibility by exporting an existing SDR video as a new H.264/AAC MP4. The full picture and first audio track are retained; source files are kept.
- **Scheduling fix:** a scheduled queue waits while a modal editor or dialog is open, then starts when the application is ready.
- **Setup help and latest release** links are available in the **•••** menu.
- **Playback fix:** compatible H.264/AAC MP4 is now the default. The old modern-MP4 default migrates once; explicit advanced choices made afterward are remembered. Existing queued jobs and files are not converted automatically.
- **Windows installer** handles video-tool setup automatically and adds a Start menu shortcut. No PowerShell commands are needed for installation.
- **Schedule** starts queued downloads at a chosen local time while ClearFrame stays open.
- **Audio language** lists the checked video's labeled audio tracks and saves your choice with that download.
- **Clip range** saves a section of a checked individual link after downloading the full source.
- **Subtitle language** selects one of 15 language families and available regional variants for separate SRT captions.
- **Library sort** organizes the view by date, title, size or duration without changing the download sequence.
- **Choose playlist** previews up to 200 entries so you can select the videos to download.
- **Pause after current** finishes the current attempt before pausing the remaining queue.
- Thumbnail cards show duration, estimated source size and clearly labeled download stages.
- **Video cleanup** opens a local editor for blending or blurring a selected fixed logo region, or cropping an edge. The original is kept and edits are exported to a new MP4. See [VIDEO-CLEANUP.md](VIDEO-CLEANUP.md).
- Black and charcoal interface with lemon-lime actions, red stop/error accents, dark format selectors and a library sidebar.
- Batch import of up to 100 individual links, with link validation and duplicate removal.
- MP3 and M4A audio downloads alongside the existing video formats.
- Library search, status filters, audio view, playback, queue reordering, retry-all and completed-history cleanup.
- Saved quality, format, speed limit, subtitle and fallback preferences.
- Optional bandwidth limits of 1, 2, 5 or 10 MiB/s. Limits apply to newly added jobs; one fragment at a time is used when a limit is selected.

## Start

1. Download and open **ClearFrame-Setup.exe** from the GitHub release. Click **Install** and stay connected while setup downloads and verifies the video tools.
2. Open **ClearFrame** from the Start menu. No administrator account or terminal commands are required. An optional desktop shortcut is offered during setup.
3. Paste a YouTube video or Shorts link and select **Check link**.
4. Review the resolution, frame rate and estimated size. Choose a save folder, then **Add to queue**.
5. Add more videos if desired, then select **Start queue**. Select a finished item and **Show file** to find it.

Requires an Intel/AMD 64-bit PC running Windows 10 version 1809 or later, or Windows 11, with .NET Framework 4.7.2 or newer. The application and setup are unsigned; Windows may show an unknown-publisher warning. See [START-HERE.md](https://github.com/spade-codee/clearframe/blob/main/docs/START-HERE.md) for simple setup help.

The portable ZIP remains available for advanced users: extract it, run `./Get-Tools.ps1` in PowerShell 7, then open `ClearFrame.exe`.

Close ClearFrame before running a newer installer. Setup preserves history and downloaded media, but reinstalls the release's pinned tools. Remove the app through **Windows Settings > Apps**; your history and media are kept.

## Batch links and audio

Choose your format, quality, speed limit and destination first, then select **Add a batch** in the sidebar. Paste up to 100 individual YouTube links, one per line. An invalid link prevents the batch from being added until corrected. Duplicate links within the batch are collapsed; existing non-failed items with matching format, destination and requested settings are skipped. Batch titles and actual availability are checked when each job starts. To choose videos from a playlist, use **Choose playlist** instead.

Select **MP3 · audio / best VBR** to convert the best available audio stream into a variable-bitrate MP3. Select **M4A · audio / AAC** to prefer YouTube's original AAC stream; other source audio is converted to AAC when necessary. Video resolution and subtitle controls are disabled for audio downloads. Converting audio does not improve the original recording, and the size shown before download describes the source stream, not a guaranteed final converted-file size.

## Audio languages

After **Check link**, choose **Audio language…** to see the languages reported by that video's audio formats. Choose a language or leave **Automatic (source preference)**, then add the video to the queue. This selects an existing recording; it does not translate or generate speech. Audio-description variants are labeled separately when the metadata identifies them.

The chosen language must be available in a codec supported by your format and requested quality. For example, a track available only in Opus may work in MKV but not the MP4 profiles. Incompatible or missing choices fail explicitly rather than substituting another language. Languages are matched exactly, including regional variants. Videos without language labels support Automatic only. MP3/M4A use the chosen language when a separate audio stream is offered.

The choice applies to the checked individual link, including clips, and resets when you change the URL. Batch and playlist additions use Automatic. Each queued job retains its language across restart, and duplicate checks distinguish languages. Cards and saved filenames label explicit language choices. Language availability is checked again when the job starts and may have changed since the preview.

## Library controls

- Use the sort selector beside search for **Queue order**, **Newest added**, **Oldest added**, **Title A–Z**, **Largest size**, or **Longest duration**. Size uses final saved bytes when known and source estimates otherwise. Duration uses the clip length. Older history without dates retains relative order within the unknown-date group.
- Sorting does not change the download sequence. **↑ / ↓** is enabled only in **Queue order** view.
- Use the sidebar to view all, active, completed, audio, or failed/stopped items. Search matches titles, links and file formats. Filtering does not restrict which queued jobs **Start queue** will run.
- Select a queued item and use **↑ / ↓** to change its position among queued jobs.
- Select a completed item and use **▶** or double-click it to play it in your default media app.
- The **•••** menu contains retry-all, clear completed history, diagnostic details and removal. Clearing history and removing items do not delete media files.
- **Ctrl+L** focuses the URL field; **Ctrl+F** focuses search; **Ctrl+B** opens batch import.
- Preferences are remembered on restart. Existing jobs retain the options captured when added.
- Cards show source duration and estimated source size when known. Estimates are not guarantees of final file size. Progress is per stream; merging and verification have separate labels. Thumbnails are optional and a missing image does not prevent downloading.

## Playlist selection

1. Set your format, quality, fallback, subtitles, bandwidth limit and destination in the main window.
2. Choose **Choose playlist** from the sidebar, paste a YouTube playlist URL and select **Load playlist**. A video link containing a `list` parameter also works here.
3. Check individual entries or use **Select available**. No videos are selected automatically. **Clear selection** deselects everything; **Cancel check** stops a lookup.
4. Select **Add selected**. Your picks are added in playlist order, with duplicate requests skipped. Use **Start queue** when ready; picks join the existing queue if it is already running.

Only the first 200 entries are offered. Repeated video IDs appear once. Entries reported as private, deleted, live or account-required cannot be selected. Playlist metadata may omit details, and a selectable entry can still fail when the downloader checks its actual availability or requested quality. No playlist media is downloaded during the preview. Full-channel import and account-only playlists are not supported.

## Quality and formats

- Choose **360p, 480p, 720p, 1080p, 1440p, 2160p (4K), or 4320p (8K)**. The selected resolution must be present in the source; the app never upscales.
- **Best source quality** has no resolution cap and selects the highest available resolution compatible with the selected format.
- Lower-resolution fallback is **off by default**. Enable **Allow a lower resolution** to permit it. The actual resolution is shown before adding the download and again in the queue. Streams are rechecked when each job starts, so availability may change.
- **MP4 · advanced codecs** accepts H.264, AV1, VP9 and HEVC video with AAC audio. This supports higher qualities where YouTube offers newer codecs; Windows may require additional decoder support for the selected codec, which is shown in the preview. An `.mp4` extension or 1080p resolution alone does not guarantee player compatibility.
- **MP4 · compatible H.264** is the default and selects H.264 video with AAC audio for broad playback compatibility. Higher resolutions may not be offered in H.264; advanced MP4 or MKV may offer them but need a compatible player.
- **MKV** allows newer video/audio codecs, preserving the selected source quality without transcoding. Your player must support the codecs in the file.
- **WebM** selects VP8, VP9 or AV1 video with Opus or Vorbis audio.
- **MOV** selects H.264 video and AAC audio in a QuickTime container. Its available resolutions depend on H.264 source availability.
- Resolution is measured using the shorter image dimension, so both 1920×1080 landscape video and 1080×1920 portrait video count as 1080p. Unusual aspect ratios may not match YouTube's displayed resolution label.
- The source frame rate is retained, including 60 fps when offered; no artificial frames or detail are added. Among eligible streams, the app prioritizes resolution and frame rate, then the engine's format preference. Original HDR streams can be retained, but this version does not provide an HDR/SDR selector or tone mapping.
- Separate video and audio streams are merged automatically. Size estimates can be unavailable or approximate.
- Completion requires a readable file containing video and audio, the selected resolution, and a duration consistent with the source. This is a structural check, not a frame-by-frame corruption scan.

## Clip ranges

Check an individual video link, choose **Clip range…**, and enter start/end times as seconds (`90.5`), minutes/seconds (`01:30.5`), or hours/minutes/seconds (`01:02:30`). Up to three decimal places are supported. The range must be at least one second, within the source duration, and within seven days. Choose **Use clip**, then **Add to queue**. **Use full video** or changing the URL resets the range.

Clips download the **full source first**, then re-encode the requested range locally. This needs bandwidth for the complete source and extra processing/storage; it is not a partial network download. Source data stays in staging until verification, then is removed when possible. Cancellation retains source download data for retry and removes the current temporary clip when possible. A crash can leave temporary files in `.clearframe`.

Video clips require SDR and even dimensions. MP4, compatible MP4, MKV and MOV clips use H.264 CRF 18 with AAC; WebM uses VP9 CRF 30 with Opus. MP3 clips use variable-bitrate MP3; M4A uses AAC. Clipping is not lossless, can take time at high resolutions, and can change the source codecs and file size.

Filenames include the selected range, and existing files are not overwritten. Captions are shifted to start at zero; cues outside the range are removed and overlapping cues are shortened. Batch and playlist additions remain full-length even when a clip is selected for the checked link. Queued clips retain their own settings across restart.

## Queue and recovery

- **Schedule…** accepts a local date/time in `YYYY-MM-DD HH:mm` format, up to seven days ahead. It starts all items queued at that time. The button shows the scheduled time; click it to change or cancel. Manually starting the queue cancels the schedule.
- Keep ClearFrame open and your PC awake. The app does not wake the PC or run after closing. If the PC wakes late, the schedule runs when the app is ready. An active link check or engine update delays the start until idle. Schedules are deliberately not restored after restarting the app. Daylight-saving times that are skipped or ambiguous are rejected.
- The queue runs one video at a time. Progress refers to the current stream, so it may restart when audio begins. Merging and verification appear as separate stages.
- While the queue runs, check **Pause after current** to finish the active attempt and leave the rest queued. This also pauses after a failed attempt. Uncheck it before completion to continue normally. After pausing, select **Resume queue**; completed items are not repeated. The pause request resets when you start again, and downloads never start automatically after reopening the app.
- **Stop queue** stops the current worker and leaves remaining jobs queued. Choose the stopped item, **Retry selected**, then **Start queue**. Partial downloads resume when supported by the stream and engine.
- Queue and history are saved in `%LOCALAPPDATA%\ClearFrame\state.json`. Interrupted jobs are restored as stopped; downloads never start automatically on app launch.
- If history is missing or unreadable, ClearFrame tries `state.json.bak`. This backup may be one save behind. Unreadable files are copied to names containing `.corrupt-` before new history replaces them; saved media files are unaffected.
- If history cannot be saved, a **History not saved** message stays at the start of the status bar until a save succeeds. Hover over the status bar to read the full message. Changes remain in memory and may be lost if you close before saving succeeds.
- Duplicate checks compare the video link, destination, format, requested quality, clip range and subtitle language. Requests with different subtitle or strict/fallback settings can be added separately. Audio requests ignore the video-resolution preference.
- Partial files stay in `.clearframe` inside your chosen save folder. Unverified merged files are retained with an `.unverified-...` suffix when retried. Failed jobs may require additional disk space because those files are retained.
- Completed files are moved into your chosen folder. Existing files are never intentionally overwritten; a numbered name is used for collisions.
- Removing a queue/history item from the **•••** menu keeps downloaded and partial files. You can delete abandoned partial files manually while the queue is stopped.
- Changing the save folder applies to new jobs; existing jobs retain their original destination.
- Disk-space checks require roughly twice the estimated download size plus working space for full videos, or four times for clips and audio conversions. This is a preflight check, not a reservation. When size is unknown, a minimum-space check cannot guarantee sufficient capacity.

## Subtitles and unavailable videos

Enable **Subtitles (.srt)** and select a language before adding a video, batch or playlist selection. English, Spanish, French, German, Portuguese, Arabic, Hindi, Japanese, Korean, Chinese, Italian, Russian, Turkish, Indonesian and Yoruba are supported, including available regional variants. Queued jobs retain their choice; older jobs default to English.

Captions are separate `.srt` files. Manual and automatic captions are requested when available; ClearFrame does not translate them itself or guarantee accuracy. If the requested language is absent, the video can still finish with an unavailable message. A subtitle network error may fail a download. Caption save or retiming errors after media verification retain the saved video and report a warning in Details. Captions are not embedded.

YouTube may reject requests, rate-limit downloads or change its delivery methods. **Update engine** checks the official yt-dlp release. It does not update FFmpeg or Deno. Download success cannot be guaranteed for every video or network.

This version supports individual public videos and Shorts, batches, selection from the first 200 playlist entries, and cleanup of local videos. It does not import browser cookies, sign into YouTube, download live streams, import full channels, or bypass account/region restrictions. Only download content you have permission to save.

## Privacy and maintenance

There is no analytics service or application backend. Download requests go directly to YouTube and its media hosts; engine update requests go to GitHub. Titles, links, destinations and diagnostic logs are stored locally. No Google account password is requested. Logs can contain URLs and local paths, so review them before sharing.

Thumbnail cards request images from `i.ytimg.com`, including for up to 200 saved library entries when the app opens. Images are cached in memory for the session. Requests have a timeout, a 2 MiB response limit and at most four active requests; failures leave a format badge. The app does not fetch arbitrary thumbnail URLs supplied by metadata.

The Windows installer downloads the engine, merger, verifier and JavaScript runtime directly from pinned upstream releases into `tools`; the setup executable and portable ZIP do not bundle those third-party executables. The portable setup script does the same job manually. Release information and downloaded-asset hashes are in `tools/versions.json`; attribution and upstream license links are in `THIRD-PARTY.md`; the FFmpeg archive license is retained in `tools/licenses`.

The full C# and XAML source is in `source`. To rebuild with the Windows .NET Framework compiler, run `scripts/Build.ps1` in PowerShell 7. No NuGet packages are required.
