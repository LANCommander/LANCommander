#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# build-dos.sh
#
# Builds the LANCommander Legacy Launcher for MS-DOS.
#
# The toolchain is DJGPP: 32-bit protected mode, GCC 12 with a full
# libstdc++, so the launcher's C++14 compiles as it stands. The result is a
# go32-v2 image that needs a DPMI host, which is why CWSDPMI.EXE is packaged
# alongside it.
#
# There is no third-party graphics library involved. Allegro dropped DOS
# after 4.2 and SDL3 has no DOS video driver, so src/gfx/gfx_dos.cpp talks to
# VESA VBE 2.0 directly and is both the software rasteriser and the display
# driver. Networking is Watt-32, which is the only TCP/IP stack DOS has.
#
# Everything it needs is fetched on first run:
#   tools/setup-djgpp.sh   the cross toolchain and CWSDPMI
#   tools/setup-watt32.sh  the TCP/IP stack (a submodule of picoposh)
#
# Usage:
#   ./build-dos.sh                # Release
#   ./build-dos.sh Debug          # debug build
#   ./build-dos.sh Release 8      # release, 8 parallel jobs
#
# Output: out-dos/ , which is the whole distribution -- copy it to the DOS
# machine and run LANCMDR.EXE.
# ---------------------------------------------------------------------------
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

BUILD_TYPE="${1:-Release}"
JOBS="${2:-$(nproc 2>/dev/null || getconf _NPROCESSORS_ONLN 2>/dev/null || echo 2)}"

BUILD_DIR="$SCRIPT_DIR/build-dos"
OUTPUT_DIR="$SCRIPT_DIR/out-dos"

# ---------------------------------------------------------------------------
# Preflight
# ---------------------------------------------------------------------------
for tool in cmake curl git; do
    command -v "$tool" >/dev/null 2>&1 || {
        echo "ERROR: $tool is required but was not found on PATH."
        exit 1
    }
done

if [ ! -f "$REPO_ROOT/liblancommander/vendor/picoposh/CMakeLists.txt" ]; then
    echo "Fetching the picoposh submodule..."
    git -C "$REPO_ROOT" submodule update --init --depth 1 -- \
        liblancommander/vendor/picoposh
    git -C "$REPO_ROOT/liblancommander/vendor/picoposh" \
        submodule update --init --depth 1 -- third_party/zlib third_party/libzip
fi

# ---------------------------------------------------------------------------
# Toolchain and TCP/IP stack
# ---------------------------------------------------------------------------
echo "==> DJGPP"
DJGPP_ROOT="$("$SCRIPT_DIR/tools/setup-djgpp.sh")"
export DJGPP_ROOT
echo "    $DJGPP_ROOT"

echo "==> Watt-32"
WATT32_ROOT="$("$SCRIPT_DIR/tools/setup-watt32.sh" --djgpp "$DJGPP_ROOT")"
echo "    $WATT32_ROOT"

# ---------------------------------------------------------------------------
# Build
# ---------------------------------------------------------------------------
# A generator has to be named explicitly. CMake's default on a Windows
# host is a Visual Studio one, which cannot drive a DJGPP cross compiler
# at all -- and fails with an MSBuild stack trace rather than anything
# that names the real problem.
if command -v ninja >/dev/null 2>&1; then
    GENERATOR="Ninja"
else
    GENERATOR="Unix Makefiles"
fi

echo "==> Configuring ($GENERATOR)"
cmake -S "$SCRIPT_DIR" -B "$BUILD_DIR" -G "$GENERATOR" \
    -DCMAKE_TOOLCHAIN_FILE="$SCRIPT_DIR/cmake/toolchain-djgpp.cmake" \
    -DDJGPP_ROOT="$DJGPP_ROOT" \
    -DWATT32_ROOT="$WATT32_ROOT" \
    -DLAUNCHER_GFX_BACKEND=dos \
    -DCMAKE_BUILD_TYPE="$BUILD_TYPE"

echo "==> Building"
cmake --build "$BUILD_DIR" -j"$JOBS"

# ---------------------------------------------------------------------------
# Package
# ---------------------------------------------------------------------------
echo "==> Packaging"
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

# 8.3, because that is all a DOS filesystem without a long filename driver
# can show. LANCOMMANDER.EXE would be invisible on a plain DOS 6.22 machine.
cp "$BUILD_DIR/launcher.exe" "$OUTPUT_DIR/LANCMDR.EXE"
"$DJGPP_ROOT/bin/i586-pc-msdosdjgpp-strip" "$OUTPUT_DIR/LANCMDR.EXE"

# Assets are loaded by path at runtime, next to the executable.
cp -r "$SCRIPT_DIR/assets" "$OUTPUT_DIR/assets"

# The DPMI host. A go32-v2 image loads it from its own directory when DOS is
# not already providing DPMI -- which plain DOS is not.
CWSDPMI="$SCRIPT_DIR/vendor/djgpp/cwsdpmi/bin/CWSDPMI.EXE"
if [ -f "$CWSDPMI" ]; then
    cp "$CWSDPMI" "$OUTPUT_DIR/CWSDPMI.EXE"
    # CWSDPMI's own documentation, which carries its licence, travels with
    # the binary it documents. Only cwsdpmi.doc: the rest of the package
    # documents tools that are not shipped.
    CWSDOC="$SCRIPT_DIR/vendor/djgpp/cwsdpmi/bin/cwsdpmi.doc"
    [ -f "$CWSDOC" ] && cp "$CWSDOC" "$OUTPUT_DIR/"
else
    echo "    WARNING: no CWSDPMI.EXE. The launcher will only start on a"
    echo "             machine that already provides DPMI (a Windows DOS box,"
    echo "             or DOS with EMM386/QEMM/JEMM386 loaded)."
fi

echo ""
ls -lh "$OUTPUT_DIR/"
echo ""
echo "Done. Copy $OUTPUT_DIR to the DOS machine and run LANCMDR.EXE."
echo "See DOS-TESTING.md for what the target needs (packet driver, mouse,"
echo "VESA 2.0, and a long filename driver for the media cache)."
