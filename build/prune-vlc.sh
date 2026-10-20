#!/usr/bin/env bash
#
# Applies the VLC plugin allowlist on Linux and macOS.
#
# On Windows the same filtering happens at build time, because
# VideoLAN.LibVLC.Windows exposes include-list properties that build/VlcPlugins.props
# populates directly. The Linux and macOS bundles are assembled by CI instead - Linux
# unpacks Debian's vlc-plugin-base, macOS gets a payload from VideoLAN.LibVLC.Mac -
# so they need this script to apply the same list after the fact.
#
# VlcPlugins.props is the single source of truth for what we keep; the stems are
# parsed straight out of it so the list is never maintained in two places.
#
# Usage:
#   prune-vlc.sh copy   <src-plugins-dir> <dest-plugins-dir>
#       Copy only allowlisted plugins from a system/extracted VLC into the bundle.
#
#   prune-vlc.sh prune  <libvlc-dir>
#       Delete every non-allowlisted plugin from an already-populated bundle,
#       then drop the lua/ and hrtfs/ payloads that go with the plugins we removed.
#
#   prune-vlc.sh verify <dir>
#       Fail if any GPL-licensed component survived. LANCommander is MIT and ships
#       these binaries alongside it, so a GPL plugin reaching a release would create
#       copyleft obligations we do not want. Intended as a CI gate.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROPS="$SCRIPT_DIR/VlcPlugins.props"

# Anything matching these is GPL (or GPL-linked) and must never ship.
# libx264/libx265 are encoders we do not use; crystalhd is a GPL hardware decoder.
GPL_PATTERNS=(x264 x265 crystalhd dvdcss)

if [[ ! -f "$PROPS" ]]; then
  echo "error: cannot find $PROPS" >&2
  exit 1
fi

# Extract the plugin stems ("codec/libavcodec_plugin") from the props file.
read_stems() {
  sed -n 's/.*_LcVlcPlugin Include="\([^"]*\)".*/\1/p' "$PROPS"
}

cmd_copy() {
  local src="$1" dest="$2" kept=0
  [[ -d "$src" ]] || { echo "error: source plugin dir not found: $src" >&2; exit 1; }

  while read -r stem; do
    local subdir base
    subdir="$(dirname "$stem")"
    base="$(basename "$stem")"

    # Trailing glob, matching the Windows behaviour: an entry that matches nothing
    # is skipped rather than fatal. The architectures and platforms genuinely differ
    # (no _mmx/_sse2 chroma on arm64, no WASAPI off Windows), so a single shared list
    # can only work if absent entries are tolerated.
    shopt -s nullglob
    local matches=("$src/$subdir/$base"*)
    shopt -u nullglob

    if [[ ${#matches[@]} -gt 0 ]]; then
      mkdir -p "$dest/$subdir"
      cp -L "${matches[@]}" "$dest/$subdir/"
      kept=$((kept + ${#matches[@]}))
    fi
  done < <(read_stems)

  echo "Bundled $kept allowlisted plugin(s) from $src"
}

cmd_prune() {
  local root="$1"
  local plugins="$root/plugins"
  [[ -d "$plugins" ]] || { echo "error: no plugins dir under $root" >&2; exit 1; }

  local before after keep
  before=$(find "$plugins" -type f | wc -l)

  # Build the keep-set as absolute paths, then delete anything not in it.
  keep="$(mktemp)"
  while read -r stem; do
    shopt -s nullglob
    for f in "$plugins/$stem"*; do echo "$f"; done >> "$keep"
    shopt -u nullglob
  done < <(read_stems)

  while IFS= read -r f; do
    grep -qxF "$f" "$keep" || rm -f "$f"
  done < <(find "$plugins" -type f)
  rm -f "$keep"

  # These only exist to serve plugins we just removed: lua/ backs the Lua interface
  # and playlist modules, hrtfs/ backs the spatial-audio filter.
  rm -rf "$root/lua" "$root/hrtfs"

  # Import libraries are link-time only and do nothing at runtime.
  find "$root" -maxdepth 1 -name "*.lib" -delete

  find "$plugins" -type d -empty -delete
  after=$(find "$plugins" -type f | wc -l)
  echo "Pruned plugins in $root: $before -> $after file(s)"
}

cmd_verify() {
  local dir="$1" found=0

  # 1. Whole plugins that are GPL by identity.
  for pattern in "${GPL_PATTERNS[@]}"; do
    while IFS= read -r hit; do
      echo "error: GPL component in published output: $hit" >&2
      found=1
    done < <(find "$dir" -iname "*${pattern}*" 2>/dev/null)
  done

  # 2. The FFmpeg build linked into libavcodec_plugin. This is the check that
  #    actually matters and the one a filename scan cannot make: VideoLAN's stock
  #    plugin carries no x264/x265 at all, yet is still GPLv2+ because their contribs
  #    configure FFmpeg with --enable-gpl --enable-postproc (libpostproc is GPL-only,
  #    and enabling it forces the whole libav* stack to GPL). FFmpeg embeds its own
  #    license in the binary, so read that rather than inferring it.
  #    Note "license: GPL" does not match "license: LGPL" - the discrimination is
  #    exactly what we want here.
  while IFS= read -r plugin; do
    local licenses
    licenses="$(grep -a -o -E "libav(codec|format|util) license: [A-Za-z0-9. ]+" "$plugin" 2>/dev/null | sort -u || true)"

    if [[ -z "$licenses" ]]; then
      echo "warning: could not read an FFmpeg license string from $plugin" >&2
      continue
    fi

    if grep -qE "license: GPL" <<< "$licenses"; then
      if [[ "${LC_ALLOW_GPL_FFMPEG:-0}" == "1" ]]; then
        # Escape hatch for the window between pruning landing and the LGPL runtime
        # being built. Loud on purpose: it is not a supported release configuration.
        echo "warning: GPL-licensed FFmpeg in $plugin, allowed via LC_ALLOW_GPL_FFMPEG=1" >&2
        sed 's/^/         /' <<< "$licenses" >&2
      else
        echo "error: GPL-licensed FFmpeg linked into $plugin" >&2
        sed 's/^/         /' <<< "$licenses" >&2
        found=1
      fi
    fi
  done < <(find "$dir" -name "libavcodec_plugin.*" 2>/dev/null)

  if [[ $found -eq 1 ]]; then
    echo "error: refusing to ship GPL components alongside MIT-licensed LANCommander." >&2
    echo "       See build/README.md for how the LGPL runtime is produced." >&2
    exit 1
  fi
  echo "License scan clean: no GPL components in $dir"
}

case "${1:-}" in
  copy)   [[ $# -eq 3 ]] || { echo "usage: $0 copy <src> <dest>" >&2; exit 1; }; cmd_copy "$2" "$3" ;;
  prune)  [[ $# -eq 2 ]] || { echo "usage: $0 prune <libvlc-dir>" >&2; exit 1; }; cmd_prune "$2" ;;
  verify) [[ $# -eq 2 ]] || { echo "usage: $0 verify <dir>" >&2; exit 1; }; cmd_verify "$2" ;;
  *)      sed -n '3,26p' "${BASH_SOURCE[0]}" | sed 's/^# \?//' >&2; exit 1 ;;
esac
