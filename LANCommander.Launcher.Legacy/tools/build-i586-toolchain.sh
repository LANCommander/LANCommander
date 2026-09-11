#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# build-i586-toolchain.sh — a pre-SSE2 GCC for the Windows 95/98 target.
#
# Why this exists
# ---------------
# MSYS2's mingw-w64 i686 GCC is configured --with-arch=pentium4. Passing
# -march=i586 fixes everything the compiler generates from our sources, but
# the runtime archives it links are prebuilt at that same pentium4 baseline:
#
#   libstdc++.a   507 functions with SSE2 -- std::string::swap, the move
#                 constructors, locale/ctype/timepunct initialisation
#   libmingwex.a   32
#   libpthread.a   27
#   libmingw32.a    4
#
# std::locale and std::ctype initialise during static construction, so the
# process dies with "executed an invalid instruction" before main() on any
# CPU without SSE2 -- which is every machine this target is aimed at. No
# compiler flag reaches those archives; the only fix is a compiler whose
# runtime was built for the right CPU.
#
# What it builds
# --------------
# GCC 16.1.0 into $PREFIX, configured exactly as MSYS2 configures the stock
# i686 compiler (taken from `gcc -v`) except for the prefix,
# --with-arch=i586 --with-tune=i686, and --disable-bootstrap. Then
# mingw-w64's CRT and winpthreads are rebuilt with that compiler so
# libmingwex/libmingw32/libpthread are i586 too.
#
# i586 rather than i686 because the reference target is a K6, which has
# neither SSE nor CMOV. -mtune=i686 still schedules for the machines this
# realistically runs on.
#
# Usage (from an MSYS2 MinGW 32-bit shell):
#   tools/build-i586-toolchain.sh            # full build, roughly an hour
#   tools/build-i586-toolchain.sh 8          # limit parallelism
#
# Needs: pacman -S --needed base-devel git wget
# Leaves the stock /mingw32 toolchain untouched. build-win9x.sh picks this up
# automatically once $PREFIX/bin/gcc.exe exists.
# ---------------------------------------------------------------------------
set -euo pipefail

JOBS="${1:-$(nproc 2>/dev/null || echo 4)}"
PREFIX="${WIN9X_TOOLCHAIN:-/opt/i586-win9x}"
SRC="${WIN9X_TOOLCHAIN_SRC:-/opt/i586-src}"
GCC_VER=16.1.0
# The mingw-w64 commit MSYS2 built the installed CRT from, so the rebuilt CRT
# stays ABI-compatible with everything else in /mingw32.
MINGW_COMMIT=3197fc7d6
# GCC records the system header directory as a literal string and resolves
# $prefix/include by relocation from the compiler binary. During the build
# xgcc.exe lives in the build tree, so that relocation lands nowhere and the
# system headers drop out of the search path entirely -- libgcc's configure
# then fails on "stdio.h: No such file or directory". An absolute path pins
# it. It has to be the Windows form: xgcc.exe is a native binary, and MSYS2
# only rewrites POSIX paths on a native process's command line, not ones
# compiled into it.

if [ "${MSYSTEM:-}" != "MINGW32" ]; then
    echo "ERROR: run this from the MSYS2 MinGW 32-bit shell (MSYSTEM=MINGW32)."
    exit 1
fi

mkdir -p "$PREFIX"
PREFIX_WIN="$(cygpath -m "$PREFIX")"

echo "=== i586 toolchain ==="
echo "  prefix : $PREFIX"
echo "  source : $SRC"
echo "  jobs   : $JOBS"
echo "  headers: $PREFIX_WIN/include"

# --- 1: sources ------------------------------------------------------------
mkdir -p "$SRC"
cd "$SRC"
if [ ! -f "gcc-$GCC_VER.tar.xz" ]; then
    wget -q --show-progress "https://ftp.gnu.org/gnu/gcc/gcc-$GCC_VER/gcc-$GCC_VER.tar.xz"
fi
[ -d "gcc-$GCC_VER" ] || tar xf "gcc-$GCC_VER.tar.xz"
[ -d mingw-w64 ] || git clone --quiet https://github.com/mingw-w64/mingw-w64.git
git -C mingw-w64 checkout --quiet "$MINGW_COMMIT" 2>/dev/null \
    || echo "  WARNING: mingw-w64 $MINGW_COMMIT unavailable, using $(git -C mingw-w64 rev-parse --short HEAD)"

# GMP/MPFR/MPC/ISL come from /mingw32 via --with-gmp= below. An in-tree copy
# silently takes precedence and adds a long stretch to the build.
cd "$SRC/gcc-$GCC_VER"
for d in gmp mpfr mpc isl; do [ -d "$d" ] && rm -rf "$d"; done

# --- 2: seed the prefix ----------------------------------------------------
# GCC resolves headers from $prefix/include and CRT objects from $prefix/lib,
# and cannot build libgcc or libstdc++ without them. The archives copied here
# are the stock SSE2 ones; they are scaffolding, replaced in step 4.
echo "=== 2/4: seeding $PREFIX with mingw-w64 headers and CRT ==="
# Copy /mingw32/include wholesale rather than installing the mingw-w64-headers
# package. Headers contain no code, so nothing about them needs rebuilding for
# i586 -- and the headers-only package is not self-sufficient: its
# pthread_time.h is a stub with no CLOCK_REALTIME, because winpthreads ships
# the real one. GCC drops any -I that duplicates one of its system directories,
# so -I/mingw32/include is discarded and that stub outranks the good header.
# The build then dies in timevar.cc on a clock_gettime that configure has
# already decided is available.
rm -rf "$PREFIX/include"
mkdir -p "$PREFIX/include" "$PREFIX/lib"
cp -r /mingw32/include/. "$PREFIX/include/"

cd /mingw32/lib
for f in *.a *.o; do
    case "$f" in
        libstdc++*|libsupc++*|libgcc*|libgomp*|libatomic*|libssp*|libquadmath*|libitm*|liblto*) continue ;;
    esac
    cp -n "$f" "$PREFIX/lib/" 2>/dev/null || true
done

# An installed GCC finds the CRT in $prefix/lib by relocating from its own
# location. During the build xgcc lives in the build tree, so it cannot; it
# looks in the tooldir instead, and the libgcc link fails with "cannot find
# dllcrt2.o". Mirror both there. The stock toolchain gets away with an almost
# empty tooldir precisely because its compiler is installed.
mkdir -p "$PREFIX/i686-w64-mingw32/lib" "$PREFIX/i686-w64-mingw32/include"
cp -rn "$PREFIX/lib/." "$PREFIX/i686-w64-mingw32/lib/" 2>/dev/null || true
cp -rn "$PREFIX/include/." "$PREFIX/i686-w64-mingw32/include/" 2>/dev/null || true

# --- 3: GCC ----------------------------------------------------------------
echo "=== 3/4: building GCC $GCC_VER (--with-arch=i586) ==="
rm -rf "$SRC/build-gcc" && mkdir -p "$SRC/build-gcc" && cd "$SRC/build-gcc"
# The srcdir must stay RELATIVE. gengtype is a native Windows binary that
# reads source paths out of gtyp-input.list rather than argv, and MSYS2 only
# translates POSIX paths for a native process's command line. An absolute
# srcdir gets it "libcpp/include/line-map.h: No such file or directory" for a
# file that is plainly there. MSYS2 configures the stock compiler the same way.
# --with-sysroot: GCC defaults BUILD_SYSTEM_HEADER_DIR to /mingw/include on
# a mingw host. That does not exist under MSYS2 and fixincludes refuses to
# run without it ("The directory (BUILD_SYSTEM_HEADER_DIR) that should
# contain system headers does not exist"). Pointing the sysroot at our own
# prefix fixes it and is worth having regardless: it guarantees the compiler
# resolves headers and libraries from $PREFIX instead of silently picking up
# the pentium4 CRT still sitting in /mingw32.
"../gcc-$GCC_VER/configure" \
    --prefix="$PREFIX" \
    --with-local-prefix="$PREFIX/local" \
    --with-native-system-header-dir="$PREFIX_WIN/include" \
    --libexecdir="$PREFIX/lib" \
    --disable-bootstrap \
    --enable-checking=release \
    --with-arch=i586 \
    --with-tune=i686 \
    --enable-mingw-wildcard \
    --enable-languages=c,c++,lto \
    --disable-shared --enable-static --enable-libatomic \
    --enable-threads=posix --enable-tls \
    --enable-fully-dynamic-string \
    --enable-libstdcxx-backtrace=yes \
    --enable-libstdcxx-filesystem-ts \
    --enable-libstdcxx-time \
    --disable-libstdcxx-pch \
    --enable-libgomp --disable-libssp \
    --disable-multilib --disable-rpath --disable-win32-registry \
    --disable-nls --disable-werror --disable-symvers \
    --with-libiconv --with-system-zlib \
    --with-gmp=/mingw32 --with-mpfr=/mingw32 --with-mpc=/mingw32 --with-isl=/mingw32 \
    --with-pkgversion='LANCommander i586 Win9x' \
    --with-gnu-as --with-gnu-ld \
    --with-libstdcxx-zoneinfo=yes --disable-libstdcxx-debug \
    --disable-sjlj-exceptions --with-dwarf2 --enable-lto
make -j"$JOBS"
make install

# --- 4: CRT and winpthreads ------------------------------------------------
echo "=== 4/4: rebuilding mingw-w64 CRT and winpthreads at i586 ==="
export PATH="$PREFIX/bin:$PATH"
CC="$PREFIX/bin/gcc"
CXX="$PREFIX/bin/g++"
FLAGS="-march=i586 -mtune=i686 -mfpmath=387 -O2"

cd "$SRC/mingw-w64/mingw-w64-crt"
rm -rf build && mkdir build && cd build
../configure --host=i686-w64-mingw32 --prefix="$PREFIX" \
    CC="$CC" CXX="$CXX" CFLAGS="$FLAGS" CXXFLAGS="$FLAGS" >/dev/null
make -j"$JOBS" >/dev/null
make install >/dev/null

cd "$SRC/mingw-w64/mingw-w64-libraries/winpthreads"
rm -rf build && mkdir build && cd build
../configure --host=i686-w64-mingw32 --prefix="$PREFIX" \
    --disable-dependency-tracking \
    CC="$CC" CXX="$CXX" CFLAGS="$FLAGS" CXXFLAGS="$FLAGS" >/dev/null
make -j"$JOBS" >/dev/null
make install >/dev/null

# `make install` above also regenerated every Win32 import library. Those hold
# import stubs rather than code, so they never needed rebuilding -- and the
# regenerated libmsvcrt.a turns out to be missing secure-CRT entries the stock
# one has (_snwprintf_s, _snprintf_s), which makes libzip's configure checks
# fail and then its compat.h macro collides with the real declaration. The CRT
# startup objects (crt2.o, dllcrt2.o) are already SSE-free in the stock
# toolchain too. So keep only the archives that genuinely contain compiled
# code and put everything else back.
REBUILT="libmingwex.a libmingw32.a libmingwthrd.a libpthread.a libwinpthread.a"
KEEP="$SRC/keep"
rm -rf "$KEEP" && mkdir -p "$KEEP"
for f in $REBUILT; do
    [ -f "$PREFIX/lib/$f" ] && cp -f "$PREFIX/lib/$f" "$KEEP/"
done
cd /mingw32/lib
for f in *.a *.o; do
    case "$f" in
        libstdc++*|libsupc++*|libgcc*|libgomp*|libatomic*|libssp*|libquadmath*|libitm*|liblto*) continue ;;
    esac
    cp -f "$f" "$PREFIX/lib/"
done
cp -f "$KEEP"/*.a "$PREFIX/lib/"

# libmsvcrt.a is not purely an import library: it also carries real code -- the
# *_s secure wrappers and the time/stat helpers -- 155 SSE and 91 CMOV
# instructions of it, and __int_localtime32_s and __int_gmtime32_s do get
# linked into the launcher. Swap the rebuilt objects in by member name so the
# code becomes i586 while the stock import stubs, _snwprintf_s included, stay.
SWAP="$SRC/msvcrt-swap"
rm -rf "$SWAP" && mkdir -p "$SWAP/ext" && cd "$SWAP/ext"
for a in libmsvcrt_extra.a libmsvcrt_common.a libmsvcrt-os.a; do
    f="$SRC/mingw-w64/mingw-w64-crt/build/lib32/$a"
    [ -f "$f" ] && ar x "$f" 2>/dev/null
done
cd "$SWAP"
# ar is a native Windows binary and writes CRLF. Without tr, every member name
# carries a trailing \r, every file test fails, and the swap silently does
# nothing while reporting success.
ar t "$PREFIX/lib/libmsvcrt.a" | tr -d '\r' | sort -u > stock.txt
: > swap.txt
while IFS= read -r m; do
    [ -f "ext/$m" ] && printf '%s\n' "$m" >> swap.txt
done < stock.txt
if [ -s swap.txt ]; then
    cd "$SWAP/ext"
    xargs -a "$SWAP/swap.txt" ar r "$PREFIX/lib/libmsvcrt.a"
    ranlib "$PREFIX/lib/libmsvcrt.a"
    echo "  libmsvcrt.a: swapped $(wc -l < "$SWAP/swap.txt") members to i586"
fi

# An installed GCC would find these in $prefix/lib by relocation, but names are
# resolved through the tooldir here, so it has to be a copy rather than a hope.
cp -rf "$PREFIX/lib/." "$PREFIX/i686-w64-mingw32/lib/"
cp -rf "$PREFIX/include/." "$PREFIX/i686-w64-mingw32/include/"

# --- verify ----------------------------------------------------------------
echo ""
echo "=== runtime archives ==="
for l in libstdc++.a libgcc.a libmingwex.a libmingw32.a libpthread.a libmsvcrt.a; do
    p=$("$CC" -print-file-name="$l")
    if [ -f "$p" ]; then
        sse=$(objdump -d "$p" 2>/dev/null | grep -cE '%(x|y|z)mm[0-9]' || true)
        cmov=$(objdump -d "$p" 2>/dev/null | grep -cE '[[:space:]]cmov' || true)
        printf '  %-16s sse=%-6s cmov=%-6s\n' "$l" "$sse" "$cmov"
    fi
done
echo ""
echo "Both columns must read 0. Then rebuild with ./build-win9x.sh, which"
echo "picks up $PREFIX automatically, and tools/isa-scan.sh should report"
echo "zero for the launcher too."
