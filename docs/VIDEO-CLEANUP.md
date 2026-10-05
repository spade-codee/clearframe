# Video cleanup

ClearFrame never adds watermarks to downloads. This editor handles a logo or text already embedded in a local video, for footage you are entitled to edit.

![Editor with a synthetic demonstration frame](video-cleanup.png)

1. Finish or stop the download queue and choose **Video cleanup** in the sidebar.
2. Open a local SDR video. Choose a frame time in seconds and select **Load frame**.
3. Choose the method and drag a rectangle on the original frame. Numeric X, Y, width and height values are available for precise adjustment. Coordinates and sizes use even pixel values for H.264 compatibility.
4. Select **Preview edit** to inspect that frame. Use **Show original / Show edit** to compare the same frame instantly. Changing the rectangle or method returns to the original and discards the old edit preview; select **Preview edit** again to see the new result. Changing the frame time clears both previews until you load or preview the new time.
5. Select **Export new MP4** and choose a new filename. The source and existing output files are not overwritten. Exports are checked for expected dimensions, duration and audio presence.

## Methods

- **Blend fixed logo area** uses FFmpeg's delogo interpolation. Select the whole logo, with surrounding pixels available on each side. A border is required; use crop or blur for a logo touching the frame edge. Hidden detail cannot be recovered reliably, and texture/motion can leave visible artifacts.
- **Blur selected area** obscures the selected rectangle, including regions at a frame edge. It hides detail rather than reconstructing it.
- **Crop to selected area** keeps the selected rectangle and discards everything outside it. This can remove edge logos at the cost of part of the picture.
- **Windows-compatible MP4** converts the full SDR video to H.264 with AAC LC audio in a separate file. Region controls are disabled. Open it directly through **••• > Convert a video for Windows…**; choose the source and export a new MP4. Video decoding errors stop export, and the result is checked for codecs, pixel format, dimensions, duration and audio. This mode always re-encodes the first audio track when present.
- **Vertical clip (9:16)** adds start/end times, a fixed portrait crop and 720×1280 or 1080×1920 output. See [CLIPPING.md](CLIPPING.md).

## Export and limits

- The same fixed rectangle is applied to the entire video. It does not follow moving logos or changing camera layouts.
- Video is re-encoded to H.264, CRF 18, 8-bit YUV 4:2:0 in MP4. This is not lossless and can change file size substantially.
- The first audio track is retained; AAC is copied when possible and other codecs are converted to AAC. Additional audio tracks, embedded subtitles and source metadata are not copied.
- HDR inputs are rejected instead of silently producing an incorrect SDR result. Even pixel dimensions and common 90-degree rotation steps are supported.
- Embedded cover art is ignored. If a file has multiple video streams, the first ordinary video stream is used throughout. Previews account for non-square pixels when drawing and positioning the selection.
- A selected-frame preview does not prove the edit looks good throughout a moving scene. Check representative frame times and inspect the complete export.
- Cancellation removes the editor's temporary output when possible and leaves the input unchanged. A crash can leave a `.partial-...mp4` file beside the chosen destination; it can be removed after the app is closed.
- Local synthetic-media checks cover all three filters and their previews, small edge regions, rotated video, silent video, embedded cover art, multiple video streams, non-square-pixel previews, file protection and audio/duration checks. Offline control checks cover preview comparison and invalidation. Manual full-editor interaction and every possible codec/container combination remain unverified.
