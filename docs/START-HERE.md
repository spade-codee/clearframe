# Install ClearFrame on your Windows PC

1. **[Download ClearFrame-Setup.exe](https://github.com/spade-codee/clearframe/releases/latest/download/ClearFrame-Setup.exe).**
2. Open the downloaded file. Follow the setup window and click **Install**.
3. Stay connected to the internet while setup gets the video tools ready.
4. Click **Finish**, then open **ClearFrame** from your Start menu.

You do not need to download source code, open a terminal, or install PowerShell. Setup installs for your Windows account without administrator access. An optional desktop shortcut is offered.

Use Windows 10 (version 1809 or later) or Windows 11 on a 64-bit Intel/AMD PC, with .NET Framework 4.7.2 or later. Allow about 150 MB of downloads and 1.5 GB of free disk space for setup; videos need additional storage. This release does not support macOS, 32-bit Windows or Windows ARM PCs.

## Save your first video

1. Copy the video's YouTube link and paste it into ClearFrame.
2. Choose **Check link**.
3. Select your quality and file format. **MP4 · compatible H.264** is the default for broad player support when the source offers it.
4. Choose **Add to queue**, then **Start queue**.
5. When it says **Complete**, use **Show file** to find your download.

Only download content you have permission to save. Quality depends on the original video; ClearFrame cannot create missing detail.

## If something goes wrong

- **Unknown publisher / Windows warning:** this release is unsigned. Confirm that the file came from `github.com/spade-codee/clearframe`. On a managed or restricted PC, ask its owner or administrator for help; do not turn off antivirus protection.
- **Setup cannot download the tools:** check your connection and select Retry, or run setup again. Setup checks downloads before installing them.
- **Missing .NET Framework:** install Windows updates, then try setup again.
- **Missing video tool after installation:** close ClearFrame and rerun setup to repair it.
- **A video fails:** select it and open **••• > Download details**. Some videos, qualities or network conditions are unsupported.
- **Windows cannot play a saved MP4:** it may use an advanced codec such as AV1. Update ClearFrame, then choose **••• > Convert a video for Windows…**. Open the saved SDR video and export a new MP4; your original is kept. Future downloads default to **MP4 · compatible H.264**. Changing the filename extension does not convert its codec. Older queued jobs keep their original format.

## Update or remove

To update, close ClearFrame and run the newest setup file from the same download link. Your library settings and downloaded videos are kept. Setup reinstalls the pinned tool versions, so an engine updated separately may be replaced by the release's tested version.

To remove ClearFrame, open **Windows Settings > Apps**, find **ClearFrame**, then choose **Uninstall**. Your downloaded videos and local history are kept. History is stored in `%LOCALAPPDATA%\ClearFrame`.

See the [full guide](USER-GUIDE.md) for scheduling, clips, captions, playlists and video cleanup.
