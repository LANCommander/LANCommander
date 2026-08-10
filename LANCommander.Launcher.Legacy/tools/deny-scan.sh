#!/usr/bin/env bash
# Scan one or more binaries/archives for symbols known absent on Windows 9x.
# Complements check-imports.sh's allowlist: the denylist always fails, so a
# bad import cannot be waved through by having been in the baseline binary.
#
# Usage: tools/deny-scan.sh <file> [file...]
set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DENY="$HERE/win9x-imports-deny.txt"

deny_syms=$(sed 's/#.*//' "$DENY" | tr -d ' \t\r' | grep -v '^$' | sort -u)

status=0
for f in "$@"; do
    syms=$(bash "$HERE/check-imports.sh" --dump "$f" | cut -d'!' -f2 | sort -u)
    hits=$(comm -12 <(echo "$syms") <(echo "$deny_syms"))

    echo "=== $f"
    echo "    symbols: $(echo "$syms" | grep -c . || true)"
    if [ -n "$hits" ]; then
        status=1
        echo "    NOT ON WIN9X:"
        # Report which object file each offender came from — that is the
        # actual unit of work when porting.
        while read -r s; do
            [ -z "$s" ] && continue
            owner=$(nm -A --undefined-only "$f" 2>/dev/null \
                    | grep -E "[ _]${s}(@|$)" \
                    | sed 's/.*[:/]\([^:/]*\.obj\):.*/\1/' \
                    | sort -u | head -3 | tr '\n' ' ')
            [ -z "$owner" ] && owner="(pe import)"
            printf '      %-32s %s\n' "$s" "$owner"
        done <<< "$hits"
    else
        echo "    NOT ON WIN9X: none"
    fi
done
exit $status
