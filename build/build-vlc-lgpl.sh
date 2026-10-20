#!/usr/bin/env bash
#
# Builds an LGPL libvlc runtime for the launcher.
#
# Why this exists
# ---------------
# VideoLAN's prebuilt packages cannot be used as-is. Their contribs configure FFmpeg
# with "--enable-gpl --enable-postproc" (libpostproc is GPL-only, and enabling it
# forces the whole libav* stack to GPL), so the libavcodec_plugin they ship reports:
#
#     libavcodec license: GPL version 2 or later
#
# Pruning the plugin set removes the separately-GPL plugins (x264, crystalhd) but
# cannot fix this one, because the GPL code is linked into the decoder we actually
# need. LANCommander is MIT and ships this binary alongside itself, so the decoder has
# to be rebuilt from source against an LGPL FFmpeg.
#
# libavcodec_plugin is a VLC module, not a standalone FFmpeg binary - it can only be
# compiled inside VLC's build tree. So this builds VLC's contribs and then VLC itself,
# rather than building FFmpeg separately and swapping a file.
#
# Expected to run inside one of VideoLAN's own toolchain images, which is how VLC
# builds upstream; see .github/workflows/LANCommander.VLC.yml.
#
# Usage:
#   build-vlc-lgpl.sh <host-triplet> <output-dir>
#
#   e.g. build-vlc-lgpl.sh x86_64-w64-mingw32 out/win-x64
#        build-vlc-lgpl.sh aarch64-w64-mingw32 out/win-arm64
#
# Environment:
#   VLC_VERSION   VLC tag to build (default: pinned below - keep in step with the
#                 VideoLAN.LibVLC.* versions in Directory.Packages.props)

set -euo pipefail

# Keep in step with Directory.Packages.props. Building a different version than the
# NuGet packages pin would produce a runtime whose plugins do not match the allowlist.
VLC_VERSION="${VLC_VERSION:-3.0.21}"

HOST="${1:?usage: $0 <host-triplet> <output-dir>}"
OUTDIR="${2:?usage: $0 <host-triplet> <output-dir>}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORKDIR="${WORKDIR:-$(pwd)/.vlc-build}"

# Decoders/demuxers/parsers to keep. This mirrors build/VlcPlugins.props: the plugin
# allowlist decides which VLC modules ship, this decides what FFmpeg can do inside the
# one module that matters. H.264/AAC is what the server produces; VP8/VP9/Opus/Vorbis
# cover WebM sources.
FFMPEG_MINIMAL_CONF="
--disable-everything
--disable-programs
--disable-doc
--disable-postproc
--enable-decoder=h264,aac,mp3,vp8,vp9,opus,vorbis,flac,pcm_s16le
--enable-parser=h264,aac,vp8,vp9,opus,vorbis
--enable-demuxer=mov,matroska,ogg,h264
--enable-protocol=file
"

echo "=== Building LGPL libvlc $VLC_VERSION for $HOST ==="

mkdir -p "$WORKDIR"
cd "$WORKDIR"

if [[ ! -d "vlc-$VLC_VERSION" ]]; then
  echo "--- Fetching VLC $VLC_VERSION ---"
  curl -fsSL "https://download.videolan.org/vlc/$VLC_VERSION/vlc-$VLC_VERSION.tar.xz" -o "vlc.tar.xz"
  tar -xf vlc.tar.xz
fi

cd "vlc-$VLC_VERSION"

# ---------------------------------------------------------------------------
# Contribs
#
# "--disable-gpl" is the flag that does the real work: it drops the GPL contrib
# packages (x264, x265, ...) and, critically, builds FFmpeg without --enable-gpl.
# "--disable-gnuv3" keeps us on LGPLv2.1 rather than pulling in v3-only contribs,
# which keeps the obligations identical to libvlc's own.
# ---------------------------------------------------------------------------
echo "--- Building contribs (LGPL) ---"
mkdir -p "contrib/$HOST"
pushd "contrib/$HOST" > /dev/null

# Narrow FFmpeg to the codecs we actually decode. Appending to FFMPEGCONF rather than
# replacing it preserves the cross-compilation flags the contrib system computes.
mkdir -p ../src
cat >> ../src/ffmpeg/rules.mak <<EOF

# Appended by LANCommander (build/build-vlc-lgpl.sh): restrict FFmpeg to the codecs
# the launcher actually decodes. Cuts libavcodec_plugin from ~16.5 MB to a few MB.
FFMPEGCONF += $(echo $FFMPEG_MINIMAL_CONF | tr '\n' ' ')
EOF

../bootstrap --host="$HOST" --disable-gpl --disable-gnuv3
make -j"$(nproc)"
popd > /dev/null

# ---------------------------------------------------------------------------
# VLC itself. We only want libvlc, libvlccore and the plugins - no interfaces, no
# player UI - so most of VLC is disabled here.
# ---------------------------------------------------------------------------
echo "--- Building libvlc ---"
./bootstrap

mkdir -p "build-$HOST"
pushd "build-$HOST" > /dev/null

../configure \
  --host="$HOST" \
  --disable-lua \
  --disable-vlc \
  --disable-qt \
  --disable-skins2 \
  --disable-nls \
  --disable-x264 \
  --disable-x265 \
  --disable-crystalhd \
  --disable-postproc \
  --disable-dvdcss \
  --disable-dvdread \
  --disable-dvdnav \
  --disable-bluray \
  --disable-srt \
  --disable-vnc \
  --disable-freerdp \
  --disable-schroedinger \
  --enable-avcodec \
  --enable-mkv \
  --enable-ogg

make -j"$(nproc)"
popd > /dev/null

# ---------------------------------------------------------------------------
# Collect. Reuses the same allowlist as every other platform so the produced runtime
# and the CI-pruned ones cannot drift apart.
# ---------------------------------------------------------------------------
echo "--- Collecting runtime into $OUTDIR ---"
mkdir -p "$OUTDIR/plugins"

BUILT="$WORKDIR/vlc-$VLC_VERSION/build-$HOST"

# Core libraries, dereferencing symlinks so the content survives archiving.
find "$BUILT" -name "libvlc.dll" -o -name "libvlccore.dll" \
              -o -name "libvlc.so.*" -o -name "libvlccore.so.*" \
              -o -name "libvlc.*.dylib" -o -name "libvlccore.*.dylib" \
  | while read -r lib; do cp -L "$lib" "$OUTDIR/"; done

bash "$SCRIPT_DIR/prune-vlc.sh" copy "$BUILT/modules/.libs" "$OUTDIR/plugins" \
  || bash "$SCRIPT_DIR/prune-vlc.sh" copy "$BUILT/modules" "$OUTDIR/plugins"

echo "--- Verifying license ---"
bash "$SCRIPT_DIR/prune-vlc.sh" verify "$OUTDIR"

echo "=== Done: $OUTDIR ($(du -sh "$OUTDIR" | cut -f1)) ==="
