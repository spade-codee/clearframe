# Vertical clips for Shorts and TikTok

Turn a local video into a separate 9:16 MP4. Download your source once, then reuse it for several clips.

![Vertical editor with a synthetic demonstration frame](vertical-clips.png)

1. Finish or stop the queue, then choose **••• > Create a vertical clip (9:16)…**.
2. Choose **Open video** and select your local video.
3. Enter **Clip start** and **Clip end** in seconds, `mm:ss` or `hh:mm:ss`. The range must be at least one second and stay within the video. Changing the start also sets the preview frame time; select **Load frame** to refresh it.
4. Drag on the original frame to position the lemon-colored crop. The rectangle stays at a fixed 9:16 ratio. Use **X** and **Y** for precise, even-pixel positioning; width and height are calculated automatically.
5. Choose **1080 × 1920** or **720 × 1280**, then **Preview edit**. **Show original / Show edit** compares that frame. Inspect other frame times to check that your subject stays inside the crop.
6. Choose **Export new MP4** and give the clip a new name. ClearFrame checks its dimensions, duration and H.264/AAC format before saving the finished output. **Show export** opens its folder.
7. Change the range or crop and export under another name to create another clip from the same source.

The source and existing files are never overwritten. Cancel stops the export and attempts to remove its temporary output.

## Export several named clips together

![Batch review with demonstration clip names](clip-batch.png)

1. Set the range, crop and size for your first clip. Enter a unique **Clip name**, then choose **Save clip**.
2. Choose **New**, adjust the settings and name, then **Save clip** again. A batch can hold up to 50 clips from the open video. Each saved clip keeps its own range, crop position and output size.
3. Choose **Clips (N)…** to review the list. Use **Move up / Move down** to set export order, **Remove** to remove a draft without deleting its exported video, or **Edit selected** to load its settings into the editor. After editing, choose **Save changes**; uncommitted editor changes are not included in the batch.
4. Choose **Export pending clips…** and select an output folder. Filenames use your clip names with `.mp4` appended. Names must be unique regardless of letter case and valid for Windows, with at most 80 characters. Use a short folder path: final output paths are limited to 200 characters to leave room for temporary filenames.
5. ClearFrame validates all pending ranges and target filenames before starting. If any target already exists, rename that clip or choose another folder. It exports sequentially and verifies each clip before publishing its final file.
6. **Cancel** stops the batch and keeps completed clips. If cancelled or stopped by an error, review the list and export again: completed clips are skipped, while ready, cancelled and failed clips are retried. You can select a different folder for those remaining clips. Existing files are never overwritten, including files created by another program after the initial check.

**Clip lists exist only in the current editor session.** Closing the editor or opening another source asks before discarding unexported saved clips. Lists are not restored after a restart or crash; exported videos remain on disk. Saving changes to a completed clip makes that revision pending again: rename it or use another folder if its previous output still exists.

## What this version supports

- Local SDR video with even dimensions and square pixels, including common 90-degree rotation metadata. HDR and non-square-pixel sources are rejected with an explanation.
- The largest even-pixel 9:16 crop that fits inside the source, positioned manually. It stays in the same place throughout the clip; it does not track faces or moving subjects.
- H.264 High profile, 8-bit YUV 4:2:0 and AAC LC in MP4. The first audio track is used; silent videos are supported. Extra audio tracks, subtitles, chapters and source metadata are not copied.
- The crop is resized to the chosen output size. A landscape 1080p video contains fewer pixels in its portrait crop, so a 1080×1920 export can involve enlargement. This does not create missing detail. Exports re-encode and can change quality and file size.
- Single exports or batches of up to 50 named clips, with a still-frame preview. Automatic highlights, face tracking, saved project files, styled captions, a playback timeline and direct platform uploads are not included.

For full-video codec repair, choose **••• > Convert a video for Windows…**. For fixed logo blending, blur or freeform cropping, see [Video cleanup](VIDEO-CLEANUP.md).
