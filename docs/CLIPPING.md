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

## What this version supports

- Local SDR video with even dimensions and square pixels, including common 90-degree rotation metadata. HDR and non-square-pixel sources are rejected with an explanation.
- The largest even-pixel 9:16 crop that fits inside the source, positioned manually. It stays in the same place throughout the clip; it does not track faces or moving subjects.
- H.264 High profile, 8-bit YUV 4:2:0 and AAC LC in MP4. The first audio track is used; silent videos are supported. Extra audio tracks, subtitles, chapters and source metadata are not copied.
- The crop is resized to the chosen output size. A landscape 1080p video contains fewer pixels in its portrait crop, so a 1080×1920 export can involve enlargement. This does not create missing detail. Exports re-encode and can change quality and file size.
- One clip per export, with a still-frame preview. Automatic highlights, face tracking, batch ranges, styled captions, a playback timeline and direct platform uploads are not included.

For full-video codec repair, choose **••• > Convert a video for Windows…**. For fixed logo blending, blur or freeform cropping, see [Video cleanup](VIDEO-CLEANUP.md).
