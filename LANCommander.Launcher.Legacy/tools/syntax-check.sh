#!/usr/bin/env bash
# Quick per-file syntax check — much faster than a full build when
# converting one file at a time. Run from an MSYS2 MINGW32 shell.
#
# Usage: tools/syntax-check.sh src/ui/theme.cpp [more files...]
cd "$(dirname "${BASH_SOURCE[0]}")/.."

FLAGS="-std=c++14 -fsyntax-only -DALLEGRO_STATICLINK -DWINVER=0x0400 -D_WIN32_WINNT=0x0400"
INCS="-Isrc -Iallegro4-win9x/include -I../liblancommander/include \
      -I../liblancommander/backends/wininet -Ivendor/miniz -Ivendor/sqlite3"

status=0
for f in "$@"; do
    echo "--- $f"
    if ! g++ $FLAGS $INCS "$f" 2>&1 | head -20; then
        status=1
    fi
done
exit $status
