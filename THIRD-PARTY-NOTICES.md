# Third-party notices

LANCommander is licensed under the MIT license (see `LICENSE`). It bundles and drives
third-party native components that carry their own terms, reproduced below.

Nothing here places copyleft obligations on LANCommander's own source. Every component
listed is either linked dynamically or invoked as a separate process, and none is
GPL-licensed.

---

## libvlc / libvlccore — LGPL v2.1 or later

Copyright © the VideoLAN project and its contributors.
<https://www.videolan.org/vlc/libvlc.html>

Bundled with the launcher, which uses it to decode game trailers
(`LANCommander.Launcher/Helpers/VideoFrameRenderer.cs`). Loaded dynamically at runtime;
LANCommander links against no VLC code at build time.

Only the plugins required for playback are shipped. The list is
`build/VlcPlugins.props`; GPL-licensed VLC plugins — notably `libx26410b` (x264) and
`libcrystalhd` — are deliberately excluded.

## FFmpeg (libavcodec, libavformat, libavutil, libswscale) — LGPL v2.1 or later

Copyright © the FFmpeg project and its contributors.
<https://ffmpeg.org>

Bundled with the launcher inside VLC's `libavcodec_plugin`, which is what actually
decodes H.264/AAC video.

This is **not** VideoLAN's prebuilt plugin. Theirs is configured with `--enable-gpl
--enable-postproc` and reports `libavcodec license: GPL version 2 or later`, which would
be incompatible with the notice above. LANCommander builds it from source against LGPL
contribs instead; see `build/build-vlc-lgpl.sh` and `build/README.md`.

### Satisfying LGPL section 6 (relinking)

FFmpeg and libvlc are shipped as separate shared libraries and loaded dynamically, so
they can be replaced with modified versions without rebuilding LANCommander. In
addition:

- **Complete source** for both is available from the upstream projects above, at the
  versions pinned in `Directory.Packages.props`.
- **The scripts used to control compilation** are in this repository:
  `build/build-vlc-lgpl.sh`, `build/VlcPlugins.props`, and
  `.github/workflows/LANCommander.VLC.yml`. These record the exact configure flags used.
- Requests for a copy of the corresponding source may be directed to the project's issue
  tracker.

---

## Components invoked, but not distributed

The server downloads these at runtime into its own tools directory. They are executed as
separate processes and are never redistributed as part of a LANCommander release, so
their terms do not attach to it. They are listed for transparency.

### FFmpeg (command-line) — LGPL v3 or later

Obtained from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds), `lgpl`
variant. Note this build is configured with `--enable-version3`, making it LGPL **v3**
rather than v2.1 — a different version than the launcher's.

Used by `MediaToolService` and `MediaService` to convert animated covers to H.264 video.
Because this build has no libx264 (GPL), encoding uses **OpenH264** (below);
`MediaToolService.GetH264EncoderAsync` probes the binary and selects accordingly, so an
operator's own ffmpeg is still used if it has something better.

On macOS this is installed via Homebrew, which is a GPL build. It is installed into the
operator's Homebrew prefix rather than redistributed; `InstallFfmpegViaBrew` logs a
warning to make that explicit.

### OpenH264 — BSD 2-Clause

Copyright © Cisco Systems, Inc. <https://github.com/cisco/openh264>

The H.264 encoder used by the LGPL ffmpeg builds above.

### yt-dlp — The Unlicense (public domain)

<https://github.com/yt-dlp/yt-dlp>

Used by `YouTubeMediaGrabber` to download trailer video.

---

## License texts

- LGPL v2.1 — <https://www.gnu.org/licenses/old-licenses/lgpl-2.1.html>
- LGPL v3 — <https://www.gnu.org/licenses/lgpl-3.0.html>
- BSD 2-Clause — <https://opensource.org/licenses/BSD-2-Clause>
