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
#   ./build-win9x.sh            # default Release build
#   ./build-win9x.sh Debug      # debug build
#   ./build-win9x.sh Release 4  # release, 4 parallel jobs
# ---------------------------------------------------------------------------
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
LAUNCHER_DIR="$SCRIPT_DIR"
ALLEGRO_SRC="$LAUNCHER_DIR/vendor/allegro4/allegro5-4.4.3.1"

BUILD_TYPE="${1:-Release}"
JOBS="${2:-$(nproc 2>/dev/null || echo 2)}"

ALLEGRO_BUILD="$LAUNCHER_DIR/build-allegro-win9x"
ALLEGRO_PREFIX="$LAUNCHER_DIR/allegro4-win9x"
LAUNCHER_BUILD="$LAUNCHER_DIR/build-win9x"
OUTPUT_DIR="$LAUNCHER_DIR/out-win9x"

# ---------------------------------------------------------------------------
# Preflight
# ---------------------------------------------------------------------------
echo "=== LANCommander Legacy Launcher — Win9x build ==="
echo "  Build type : $BUILD_TYPE"
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

if [ ! -d "$ALLEGRO_SRC" ]; then
    echo "ERROR: Allegro 4 source not found at $ALLEGRO_SRC"
    echo "  Run setup-vendor.ps1 first, or extract the Allegro 4.4.3.1 source there."
    exit 1
fi

if [ ! -f "$REPO_ROOT/LANCommander.SDK.Cpp/vendor/cjson/cJSON.c" ]; then
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
echo "--- Step 1/3: Building Allegro 4 (static) ---"

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

# ---------------------------------------------------------------------------
# Step 2: Build the launcher
# ---------------------------------------------------------------------------
echo ""
echo "--- Step 2/3: Building LANCommander Legacy Launcher ---"

mkdir -p "$LAUNCHER_BUILD"
cmake -S "$LAUNCHER_DIR" -B "$LAUNCHER_BUILD" \
    -G "$CMAKE_GENERATOR" \
    -DCMAKE_BUILD_TYPE="$BUILD_TYPE" \
    -DALLEGRO_STATIC=ON \
    -DTARGET_WIN9X=ON \
    -DALLEGRO_ROOT="$ALLEGRO_PREFIX"

$MAKE_CMD -C "$LAUNCHER_BUILD" -j"$JOBS"

# ---------------------------------------------------------------------------
# Step 3: Package output
# ---------------------------------------------------------------------------
echo ""
echo "--- Step 3/3: Packaging ---"

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

# Bundle GDI+ redistributable (ships with XP+, needed on Win9x)
MINGW_PREFIX="${MINGW_PREFIX:-/mingw32}"
if [ -f "$MINGW_PREFIX/bin/gdiplus.dll" ]; then
    cp "$MINGW_PREFIX/bin/gdiplus.dll" "$OUTPUT_DIR/"
    echo "  Bundled: gdiplus.dll"
else
    echo "  WARNING: gdiplus.dll not found — must be provided on target"
fi
# MinGW CRT (libgcc, libstdc++, libwinpthread) is statically linked via -static

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

echo ""
echo "=== Build complete ==="
echo ""
echo "Contents of $OUTPUT_DIR:"
ls -lh "$OUTPUT_DIR"
echo ""
echo "NOTE: The target Win9x machine also needs:"
echo "  - DirectX runtime (DirectDraw, DirectInput, DirectSound)."
