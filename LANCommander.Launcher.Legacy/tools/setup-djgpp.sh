#!/usr/bin/env bash
#
# Fetches the DJGPP cross toolchain and the CWSDPMI DPMI host for the MS-DOS
# build. Both are prebuilt downloads rather than a from-source build: DJGPP's
# own bootstrap wants a DOS machine, and cross-building binutils and GCC takes
# the better part of an hour for a result identical to the published one.
#
#   ./tools/setup-djgpp.sh [--prefix DIR]
#
# Prints the resulting DJGPP root on stdout, so it can be captured:
#   DJGPP_ROOT=$(./tools/setup-djgpp.sh)
#
# Everything it writes lands under vendor/, which .gitignore covers.

set -euo pipefail

# GCC 12.2.0. Pinned rather than tracked: a toolchain change is a deliberate
# decision on a target whose whole point is that it does not move.
DJGPP_RELEASE="v3.4"
DJGPP_BASE="https://github.com/andrewwutw/build-djgpp/releases/download/${DJGPP_RELEASE}"

# CWSDPMI r7 -- the DPMI host every go32-v2 binary needs when DOS is not
# already running one. Ships next to the launcher; see build-dos.sh.
CWSDPMI_URL="http://www.delorie.com/pub/djgpp/current/v2misc/csdpmi7b.zip"

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
prefix="${script_dir}/../vendor/djgpp"

while [ $# -gt 0 ]; do
    case "$1" in
        --prefix) prefix="$2"; shift 2 ;;
        -h|--help)
            sed -n '2,14p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
            exit 0 ;;
        *) echo "setup-djgpp.sh: unknown argument '$1'" >&2; exit 2 ;;
    esac
done

mkdir -p "$prefix"
prefix="$(cd "$prefix" && pwd)"

case "$(uname -s)" in
    Linux)            asset="djgpp-linux64-gcc1220.tar.bz2" ;;
    Darwin)           asset="djgpp-osx-gcc1220.tar.bz2" ;;
    MINGW*|MSYS*|CYGWIN*) asset="djgpp-mingw-gcc1220-standalone.zip" ;;
    *)
        echo "setup-djgpp.sh: no prebuilt DJGPP for $(uname -s)." >&2
        exit 1 ;;
esac

root="${prefix}/djgpp"

if [ ! -x "${root}/bin/i586-pc-msdosdjgpp-gcc" ] &&
   [ ! -f "${root}/bin/i586-pc-msdosdjgpp-gcc.exe" ]; then
    echo "Fetching ${asset}..." >&2
    curl -fsSL -o "${prefix}/${asset}" "${DJGPP_BASE}/${asset}"

    echo "Unpacking..." >&2
    case "$asset" in
        *.zip)     unzip -q -o "${prefix}/${asset}" -d "$prefix" ;;
        *.tar.bz2) tar -xjf "${prefix}/${asset}" -C "$prefix" ;;
    esac

    rm -f "${prefix}/${asset}"
fi

if [ ! -e "${root}/bin/i586-pc-msdosdjgpp-gcc" ] &&
   [ ! -e "${root}/bin/i586-pc-msdosdjgpp-gcc.exe" ]; then
    echo "setup-djgpp.sh: unpacked, but no compiler at ${root}/bin." >&2
    exit 1
fi

# --- CWSDPMI ------------------------------------------------------------
#
# Fetched separately because the toolchain does not carry it: it is a runtime
# component of the produced program, not of the compiler.
cwsdpmi_dir="${prefix}/cwsdpmi"

if [ ! -f "${cwsdpmi_dir}/bin/CWSDPMI.EXE" ]; then
    echo "Fetching CWSDPMI..." >&2
    mkdir -p "$cwsdpmi_dir"
    if curl -fsSL -o "${cwsdpmi_dir}/csdpmi.zip" "$CWSDPMI_URL"; then
        unzip -q -o "${cwsdpmi_dir}/csdpmi.zip" -d "$cwsdpmi_dir"
        rm -f "${cwsdpmi_dir}/csdpmi.zip"
    else
        # Not fatal: a machine already running a DPMI host (a Windows DOS box,
        # or DOS with an EMM386/QEMM providing DPMI) does not need it. The
        # packaging step reports the omission.
        echo "setup-djgpp.sh: could not fetch CWSDPMI; continuing without it." >&2
    fi
fi

echo "$root"
