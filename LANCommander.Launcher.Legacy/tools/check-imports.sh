#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# check-imports.sh — Win9x compatibility gate.
#
# Windows 95/98 ship a much smaller kernel32/user32/gdi32 than NT does. An EXE
# that statically imports a symbol those DLLs do not export fails at *load*
# time, before main() runs, with no useful diagnostic. On a target with no
# debugger that is expensive to chase, so this turns it into a build check.
#
# Usage:
#   tools/check-imports.sh <file.exe|file.a> [allowlist]
#   tools/check-imports.sh --dump <file.exe>       # print "dll!symbol" lines
#
# The allowlist is seeded from a binary known to run on the target, so any
# NEW import introduced by a dependency change shows up as a violation and
# gets vetted individually.
#
# Caveat: this only sees the static import table. APIs resolved at runtime
# via LoadLibrary/GetProcAddress (as the launcher does for dwmapi) are
# invisible here and still need testing on real hardware.
# ---------------------------------------------------------------------------
set -uo pipefail

DUMP=0
if [ "${1:-}" = "--dump" ]; then
    DUMP=1
    shift
fi

TARGET="${1:-}"
ALLOWLIST="${2:-$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/win9x-imports-allow.txt}"

if [ -z "$TARGET" ] || [ ! -f "$TARGET" ]; then
    echo "usage: $0 [--dump] <file.exe|file.a> [allowlist]" >&2
    exit 2
fi

extract_imports() {
    case "$1" in
    *.a)
        # Undefined symbols in a static archive, so a dependency can be
        # vetted before it is ever linked. MinGW/i686 decorates a DLL import
        # of stdcall Foo as __imp__Foo@8, and prefixes plain C symbols with
        # a single underscore. Peel those in order: __imp_ , then the leading
        # underscore, then the @N stdcall suffix.
        nm --undefined-only "$1" 2>/dev/null \
            | awk 'NF { print $NF }' \
            | sed -e 's/^__imp_//' -e 's/^_//' -e 's/@.*//' \
            | grep -E '^[A-Za-z_][A-Za-z0-9_]*$' \
            | sort -u \
            | sed 's/^/archive!/'
        ;;
    *)
        # PE import table. objdump prints "DLL Name: foo.dll" followed by
        # rows of: <vma> <ordinal> <hint> <name>
        objdump -p "$1" 2>/dev/null | awk '
            /DLL Name:/ { dll = $NF; sub(/\.[Dd][Ll][Ll]$/, "", dll); next }
            dll != "" && NF == 4 && $4 ~ /^[A-Za-z_][A-Za-z0-9_@]*$/ {
                name = $4; sub(/@.*/, "", name)
                print tolower(dll) "!" name
            }
        ' | sort -u
        ;;
    esac
}

imports=$(extract_imports "$TARGET")

if [ "$DUMP" = "1" ]; then
    echo "$imports"
    exit 0
fi

if [ ! -f "$ALLOWLIST" ]; then
    echo "ERROR: allowlist not found: $ALLOWLIST" >&2
    echo "Seed one with:  $0 --dump <known-good.exe> > $ALLOWLIST" >&2
    exit 2
fi

if [ -z "$imports" ]; then
    echo "ERROR: no imports parsed from $TARGET" >&2
    exit 2
fi

allowed=$(grep -v '^\s*#' "$ALLOWLIST" | grep -v '^\s*$' | tr -d '\r' | sort -u)
violations=$(comm -23 <(echo "$imports") <(echo "$allowed"))

count=$(echo "$imports" | grep -c . || true)
echo "=== Win9x import check: $TARGET ==="
echo "  imported symbols : $count"

if [ -n "$violations" ]; then
    nviol=$(echo "$violations" | grep -c . || true)
    echo "  NOT IN ALLOWLIST : $nviol"
    echo ""
    echo "$violations" | sed 's/^/    /'
    echo ""
    echo "  Each of these is either genuinely absent on Windows 95/98 (fix it)"
    echo "  or a false alarm (add it to $(basename "$ALLOWLIST"))."
    exit 1
fi

echo "  result           : OK"
exit 0
