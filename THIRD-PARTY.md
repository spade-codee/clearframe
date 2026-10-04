# Third-party tools

The MIT license covers ClearFrame's original application code and documentation. It does not relicense external tools or media.

ClearFrame invokes these tools as separate processes. Their binaries are **not** committed to Git or embedded in the public setup executable or application ZIP. The Windows installer downloads them directly from upstream onto the user's computer and verifies the hashes in `config/tools.lock.json`. Portable users can run `Get-Tools.ps1` for the same purpose.

| Component | Purpose | Upstream license/source |
| --- | --- | --- |
| yt-dlp 2026.08.19 | Video metadata and downloads | [Source and Unlicense](https://github.com/yt-dlp/yt-dlp/tree/2026.08.19); [bundled executable notices](https://github.com/yt-dlp/yt-dlp/blob/2026.08.19/THIRD_PARTY_LICENSES.txt) |
| Deno 2.9.7 | JavaScript runtime used by yt-dlp | [Source and MIT license](https://github.com/denoland/deno/tree/v2.9.7), with additional bundled components |
| FFmpeg / FFprobe N-127014-g18ee27e67b | Merging, conversion and verification | [Pinned GPL build](https://github.com/yt-dlp/FFmpeg-Builds/releases/tag/autobuild-2026-09-30-00-16); [build scripts](https://github.com/yt-dlp/FFmpeg-Builds); [FFmpeg source](https://git.ffmpeg.org/ffmpeg.git); [licensing](https://ffmpeg.org/legal.html) |

The dependency installer retains license files present in downloaded archives. The yt-dlp executable includes its companion JavaScript components; consult its third-party notices for their terms.

Repackaging external binaries creates separate license obligations. In particular, the configured FFmpeg build enables GPL components. The application-only release process avoids redistributing that binary. If you distribute an all-in-one package, review and meet the exact build's license and corresponding-source requirements.

GitHub Actions used in CI are pinned to upstream commit hashes and remain subject to their upstream licenses. No downloaded test media is included in this repository.

The Windows setup executable is built with [Inno Setup 6.7.3](https://github.com/jrsoftware/issrc/releases/tag/is-6_7_3), including its archive support. Inno Setup's compiler, installer engine and bundled components retain their [upstream license terms](https://github.com/jrsoftware/issrc/blob/is-6_7_3/license.txt). The compiler download is pinned and SHA-256 verified by `scripts/Get-InstallerCompiler.ps1`.
