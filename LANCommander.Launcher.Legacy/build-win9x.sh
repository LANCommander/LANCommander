#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# build-win9x.sh
#
# Builds the LANCommander Legacy Launcher for Windows 95/98 using
# MinGW-w64 (i686).  Allegro 4 is compiled from the vendored source tree
# and linked statically so the final binary has no Allegro DLL dependency.
#
# Requirements (MSYS2 MinGW32 shell):
#   pacman -S --needed \
#       mingw-w64-i686-gcc \
#       mingw-w64-i686-cmake \
#       mingw-w64-i686-make \
#       make
#
# Usage:
#   ./build-win9x.sh                    # Release, allegro backend
#   ./build-win9x.sh Debug              # debug build
#   ./build-win9x.sh Release 4          # release, 4 parallel jobs
#   ./build-win9x.sh Release 4 sdl3     # SDL3 backend instead of Allegro
#
# Backends:
#   allegro  Known-good on the target, needs a DirectX runtime for
#            DirectDraw. This is what has historically shipped.
#   sdl3     Uses the LANCommander/SDL fork's lancommander/win9x branch,
#            which builds SDL's Windows backend against the ANSI entry
#            points. Needs no DirectX at all (the framebuffer goes through
#            GDI), but has not yet been run on real hardware.
# ---------------------------------------------------------------------------
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
LAUNCHER_DIR="$SCRIPT_DIR"
ALLEGRO_SRC="$LAUNCHER_DIR/vendor/allegro4/allegro5-4.4.3.1"

BUILD_TYPE="${1:-Release}"
JOBS="${2:-$(nproc 2>/dev/null || echo 2)}"
BACKEND="${3:-allegro}"

if [ "$BACKEND" != "allegro" ] && [ "$BACKEND" != "sdl3" ]; then
    echo "ERROR: backend must be 'allegro' or 'sdl3', got '$BACKEND'"
    exit 1
fi

ALLEGRO_BUILD="$LAUNCHER_DIR/build-allegro-win9x"
ALLEGRO_PREFIX="$LAUNCHER_DIR/allegro4-win9x"

# Separate trees and outputs so the two backends can be built and carried to
# the VM side by side for comparison.
if [ "$BACKEND" = "sdl3" ]; then
    LAUNCHER_BUILD="$LAUNCHER_DIR/build-win9x-sdl3"
    OUTPUT_DIR="$LAUNCHER_DIR/out-win9x-sdl3"
else
    LAUNCHER_BUILD="$LAUNCHER_DIR/build-win9x"
    OUTPUT_DIR="$LAUNCHER_DIR/out-win9x"
fi

# ---------------------------------------------------------------------------
# Preflight
# ---------------------------------------------------------------------------
echo "=== LANCommander Legacy Launcher — Win9x build ==="
echo "  Build type : $BUILD_TYPE"
echo "  Backend    : $BACKEND"
echo "  Jobs       : $JOBS"
echo ""

if [ "${MSYSTEM:-}" != "MINGW32" ]; then
    echo "ERROR: This script must be run from the MSYS2 MinGW 32-bit shell."
    echo "  Current MSYSTEM: ${MSYSTEM:-unset}"
    echo ""
    echo "  Option 1: Open 'MSYS2 MinGW 32-bit' from the Start Menu."
    echo "  Option 2: From any MSYS2 shell, run:"
    echo "    MSYSTEM=MINGW32 source /etc/profile && $0 $*"
    exit 1
fi

if ! command -v gcc &>/dev/null; then
    echo "ERROR: No i686 MinGW compiler found."
    echo "  Install via:  pacman -S mingw-w64-i686-gcc"
    exit 1
fi

if [ "$BACKEND" = "allegro" ] && [ ! -d "$ALLEGRO_SRC" ]; then
    echo "ERROR: Allegro 4 source not found at $ALLEGRO_SRC"
    echo "  Run setup-vendor.ps1 first, or extract the Allegro 4.4.3.1 source there."
    exit 1
fi

if [ "$BACKEND" = "sdl3" ] && [ ! -f "$LAUNCHER_DIR/vendor/sdl3/CMakeLists.txt" ]; then
    echo "ERROR: SDL3 submodule missing. Fetch it with:"
    echo "  git submodule update --init --depth 1 -- \\"
    echo "      LANCommander.Launcher.Legacy/vendor/sdl3 \\"
    echo "      LANCommander.Launcher.Legacy/vendor/sdl_ttf"
    echo "  git -C LANCommander.Launcher.Legacy/vendor/sdl_ttf \\"
    echo "      submodule update --init --depth 1 -- external/freetype"
    exit 1
fi

if [ ! -f "$REPO_ROOT/liblancommander/vendor/cjson/cJSON.c" ]; then
    echo "ERROR: cJSON vendor source not found."
    echo "  Run setup-vendor.ps1 first."
    exit 1
fi

# Detect if we are already in a MinGW32 environment or need a cross prefix
CMAKE_GENERATOR="MinGW Makefiles"
if command -v mingw32-make &>/dev/null; then
    MAKE_CMD="mingw32-make"
elif command -v make &>/dev/null; then
    MAKE_CMD="make"
    CMAKE_GENERATOR="Unix Makefiles"
else
    echo "ERROR: Neither mingw32-make nor make found."
    exit 1
fi

# ---------------------------------------------------------------------------
# Step 1: Build Allegro 4 from source (static, no addons)
# ---------------------------------------------------------------------------
if [ "$BACKEND" = "allegro" ]; then
echo "--- Step 1/4: Building Allegro 4 (static) ---"

mkdir -p "$ALLEGRO_BUILD"
cmake -S "$ALLEGRO_SRC" -B "$ALLEGRO_BUILD" \
    -G "$CMAKE_GENERATOR" \
    -DCMAKE_BUILD_TYPE="$BUILD_TYPE" \
    -DCMAKE_INSTALL_PREFIX="$ALLEGRO_PREFIX" \
    -DSHARED=OFF \
    -DWANT_EXAMPLES=OFF \
    -DWANT_TOOLS=OFF \
    -DWANT_TESTS=OFF \
    -DWANT_ALLEGROGL=OFF \
    -DWANT_LOADPNG=OFF \
    -DWANT_LOGG=OFF \
    -DWANT_JPGALLEG=OFF \
    -DWANT_FRAMEWORKS=OFF

$MAKE_CMD -C "$ALLEGRO_BUILD" -j"$JOBS"
$MAKE_CMD -C "$ALLEGRO_BUILD" install

# Find the built static library (name varies by build type)
ALLEGRO_LIB=$(find "$ALLEGRO_PREFIX/lib" -name "liballeg*.a" | head -1)
if [ -z "$ALLEGRO_LIB" ]; then
    echo "ERROR: Allegro static library not found after build."
    exit 1
fi
echo "  Allegro built: $ALLEGRO_LIB"
else
echo "--- Step 1/4: Skipped (SDL3 backend builds SDL from the submodule) ---"
fi

# ---------------------------------------------------------------------------
# Step 2: Build the launcher
# ---------------------------------------------------------------------------
echo ""
echo "--- Step 2/4: Building LANCommander Legacy Launcher ($BACKEND) ---"

mkdir -p "$LAUNCHER_BUILD"

# TARGET_WIN9X=ON is what sets PE subsystem 4.0, links the CRT statically,
# and (for the SDL3 backend) turns on SDL_WIN9X so SDL's Windows backend is
# built against the ANSI entry points.
if [ "$BACKEND" = "sdl3" ]; then
    cmake -S "$LAUNCHER_DIR" -B "$LAUNCHER_BUILD"         -G "$CMAKE_GENERATOR"         -DCMAKE_BUILD_TYPE="$BUILD_TYPE"         -DLAUNCHER_GFX_BACKEND=sdl3         -DTARGET_WIN9X=ON
else
    cmake -S "$LAUNCHER_DIR" -B "$LAUNCHER_BUILD"         -G "$CMAKE_GENERATOR"         -DCMAKE_BUILD_TYPE="$BUILD_TYPE"         -DLAUNCHER_GFX_BACKEND=allegro         -DALLEGRO_STATIC=ON         -DTARGET_WIN9X=ON         -DALLEGRO_ROOT="$ALLEGRO_PREFIX"
fi

$MAKE_CMD -C "$LAUNCHER_BUILD" -j"$JOBS"

# ---------------------------------------------------------------------------
# Step 3: Package output
# ---------------------------------------------------------------------------
echo ""
echo "--- Step 3/4: Packaging ---"

mkdir -p "$OUTPUT_DIR"

# Copy the launcher executable
LAUNCHER_EXE=$(find "$LAUNCHER_BUILD" -name "launcher.exe" | head -1)
if [ -z "$LAUNCHER_EXE" ]; then
    echo "ERROR: launcher.exe not found after build."
    exit 1
fi

cp "$LAUNCHER_EXE" "$OUTPUT_DIR/LANCommander.exe"

# Strip the binary for size
strip "$OUTPUT_DIR/LANCommander.exe" 2>/dev/null || true

# Assets (login backgrounds, bundled font) load by path at runtime — they used
# to be RCDATA inside the EXE, which is Win32-only.
rm -rf "$OUTPUT_DIR/assets"
cp -r "$LAUNCHER_DIR/assets" "$OUTPUT_DIR/assets"
echo "  Bundled: assets/ ($(du -sh "$OUTPUT_DIR/assets" 2>/dev/null | cut -f1))"

# No gdiplus.dll: image decoding is stb now, so there is no redistributable
# to ship. MinGW CRT is statically linked via -static.
#
# Clear out what earlier builds of this script left behind. Deliberately
# narrow — this directory doubles as a hand-staged deployment folder, and
# DirectX-80a.zip / 7z920.exe were put here by hand and are still target
# prerequisites. Only filenames this script itself used to emit are removed,
# and it says so rather than deleting silently.
for stale in gdiplus.dll gdiplus.exe; do
    if [ -f "$OUTPUT_DIR/$stale" ]; then
        rm -f "$OUTPUT_DIR/$stale"
        echo "  Removed obsolete: $stale (GDI+ redistributable, no longer used)"
    fi
done

# Check PE subsystem version
echo ""
echo "  Output    : $OUTPUT_DIR/LANCommander.exe"
echo "  Size      : $(stat --printf='%s' "$OUTPUT_DIR/LANCommander.exe" 2>/dev/null || stat -f '%z' "$OUTPUT_DIR/LANCommander.exe" 2>/dev/null || echo '?') bytes"

# Verify subsystem version if objdump is available
if command -v objdump &>/dev/null; then
    SUBSYS=$(objdump -p "$OUTPUT_DIR/LANCommander.exe" 2>/dev/null | grep -i "MajorOperatingSystemVersion\|MinorOperatingSystemVersion\|MajorSubsystemVersion\|MinorSubsystemVersion" || true)
    if [ -n "$SUBSYS" ]; then
        echo "  PE subsystem info:"
        echo "$SUBSYS" | sed 's/^/    /'
    fi
fi

# ---------------------------------------------------------------------------
# Step 4: Win9x compatibility gate
# ---------------------------------------------------------------------------
# A statically-imported symbol that Win95/98 does not export makes the EXE
# fail at load time, before main() runs, with no useful diagnostic. Catch it
# here rather than on the target.
echo ""
echo "--- Step 4/4: Win9x import check ---"
bash "$LAUNCHER_DIR/tools/deny-scan.sh" "$OUTPUT_DIR/LANCommander.exe"

echo ""
echo "=== Build complete ==="
echo ""
echo "Contents of $OUTPUT_DIR:"
ls -lh "$OUTPUT_DIR"
echo ""
echo "To deploy, copy LANCommander.exe AND assets/ together — the UI font is"
echo "bundled, so without assets/fonts/ no text renders at all."
echo ""
if [ "$BACKEND" = "allegro" ]; then
    echo "The target also needs a DirectX runtime (DirectDraw) and WININET"
    echo "(IE4+ on Win95; built in on Win98)."
else
    echo "The target needs WININET (IE4+ on Win95; built in on Win98)."
    echo "No DirectX required: SDL presents through GDI, not DirectDraw."
    echo ""
    echo "NOTE: this backend has never been run on Win9x. Passing the import"
    echo "      check only means the loader will accept the binary."
fi
echo ""
echo "See WIN9X-TESTING.md for the VM setup and smoke checklist."
