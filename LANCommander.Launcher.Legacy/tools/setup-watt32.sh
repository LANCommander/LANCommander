#!/usr/bin/env bash
#
# Builds Watt-32 for DJGPP. This is the launcher's TCP/IP stack on DOS, and
# there is no second choice: DOS ships no networking, so without it the
# launcher can draw its UI and nothing else.
#
#   ./tools/setup-watt32.sh --djgpp DIR [--watt DIR]
#
# Prints the Watt-32 root on stdout (the directory holding inc/ and lib/).
#
# Watt-32 comes in as a submodule of picoposh, which already used it for
# Invoke-WebRequest on its OpenWatcom DOS target. This builds the same
# checkout for DJGPP instead, so both get one Watt-32 rather than two.
#
# The one wrinkle is inc/sys/djgpp.err, the errno table. Watt-32 generates it
# by compiling util/errnos.c *for the target* and running it, which a cross
# build cannot do -- upstream's configur.sh says as much and leaves the file
# out. The workaround is upstream's own: build errnos.c for the host with
# DJGPP's <errno.h> and <sys/version.h> force-included, so it reports DJGPP's
# numbering while running natively.

set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/../.." && pwd)"

djgpp_root=""
watt_root="${repo_root}/liblancommander/vendor/picoposh/third_party/Watt-32"

while [ $# -gt 0 ]; do
    case "$1" in
        --djgpp) djgpp_root="$2"; shift 2 ;;
        --watt)  watt_root="$2";  shift 2 ;;
        -h|--help)
            sed -n '2,20p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
            exit 0 ;;
        *) echo "setup-watt32.sh: unknown argument '$1'" >&2; exit 2 ;;
    esac
done

if [ -z "$djgpp_root" ]; then
    djgpp_root="${DJGPP_ROOT:-}"
fi

if [ -z "$djgpp_root" ]; then
    echo "setup-watt32.sh: --djgpp is required (or set DJGPP_ROOT)." >&2
    exit 2
fi

djgpp_root="$(cd "$djgpp_root" && pwd)"
export PATH="${djgpp_root}/bin:$PATH"
export DJGPP_PREFIX="i586-pc-msdosdjgpp"

# --- Fetch the submodule ------------------------------------------------
if [ ! -f "${watt_root}/src/configur.sh" ]; then
    echo "Fetching the Watt-32 submodule..." >&2
    git -C "${repo_root}/liblancommander/vendor/picoposh" \
        submodule update --init --depth 1 -- third_party/Watt-32
fi

if [ ! -f "${watt_root}/src/configur.sh" ]; then
    echo "setup-watt32.sh: no Watt-32 checkout at ${watt_root}." >&2
    exit 1
fi

watt_root="$(cd "$watt_root" && pwd)"

if [ -f "${watt_root}/lib/libwatt.a" ]; then
    echo "$watt_root"
    exit 0
fi

# Watt-32 ships prebuilt Windows helpers (nasm, mkmake, mkdep, bin2c) that its
# makefiles call by bare name.
case "$(uname -s)" in
    MINGW*|MSYS*|CYGWIN*) export PATH="${watt_root}/util/win32:$PATH" ;;
    *)                    export PATH="${watt_root}/util/linux:$PATH" ;;
esac

# --- The errno table ----------------------------------------------------
sys_include="${djgpp_root}/i586-pc-msdosdjgpp/sys-include"

if [ ! -f "${watt_root}/inc/sys/djgpp.err" ]; then
    echo "Generating the DJGPP errno table..." >&2

    dj_err="${watt_root}/util/win32/dj_err.exe"

    # On anything that is not Windows the prebuilt helper cannot run, so build
    # an equivalent for the host. -D_ERRNO_H_ -D_INC_ERRNO wedge the host's
    # own <errno.h> shut so the force-included DJGPP one is the only
    # definition in play; this is verbatim what util/dj-errno.mak does.
    case "$(uname -s)" in
        MINGW*|MSYS*|CYGWIN*) : ;;
        *)
            dj_err="${watt_root}/util/dj_err_host"
            cc -O1 -o "$dj_err" "${watt_root}/util/errnos.c" \
               -I "${watt_root}/inc" \
               -DWATT32_DJGPP_MINGW -D_ERRNO_H_ -D_INC_ERRNO \
               --include "${sys_include}/errno.h" \
               --include "${sys_include}/sys/version.h"
            ;;
    esac

    "$dj_err" -e > "${watt_root}/inc/sys/djgpp.err"
fi

# --- Configure and build ------------------------------------------------
echo "Configuring Watt-32 for DJGPP..." >&2
( cd "${watt_root}/src" && sh ./configur.sh djgpp )

# configur.sh creates build/djgpp but cannot produce syserr.c for the same
# reason it cannot produce djgpp.err, so it is generated here from the same
# helper, after the directory exists.
if [ ! -f "${watt_root}/src/build/djgpp/syserr.c" ]; then
    dj_err="${watt_root}/util/win32/dj_err.exe"
    [ -x "${watt_root}/util/dj_err_host" ] && dj_err="${watt_root}/util/dj_err_host"
    "$dj_err" -s > "${watt_root}/src/build/djgpp/syserr.c"
fi

echo "Building Watt-32..." >&2
make -C "${watt_root}/src" -f djgpp.mak -j"$(getconf _NPROCESSORS_ONLN 2>/dev/null || echo 4)"

if [ ! -f "${watt_root}/lib/libwatt.a" ]; then
    echo "setup-watt32.sh: the build finished but lib/libwatt.a is missing." >&2
    exit 1
fi

echo "$watt_root"
