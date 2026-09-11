#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# isa-scan.sh — Win9x CPU instruction-set gate.
#
# The launcher's Win9x floor is a Pentium/K6/Pentium II class machine. MSYS2's
# mingw-w64 i686 GCC is configured --with-arch=pentium4, so left alone it
# emits SSE2 into ordinary code — a store of eight bytes in a static
# initialiser becomes `movq %xmm0,mem`. On the target that is an illegal
# opcode and the process dies with
#
#     LANCOMMANDER executed an invalid instruction in module LANCOMMANDER.EXE
#
# before main() runs. deny-scan.sh cannot see this: it is codegen, not an
# import. CMakeLists.txt sets -march=i586 for TARGET_WIN9X builds; this is the
# check that it actually reached every object, including the prebuilt libgcc
# and libstdc++ that get linked in statically.
#
# Usage: tools/isa-scan.sh <file.exe> [file.exe...]
#
# Exit 1 if any SSE/MMX instruction is present. CMOV is reported separately as
# a warning: Pentium Pro and Pentium II have it, plain Pentium and K6 do not.
#
# Only .text is disassembled. Data that merely sits in the image cannot be
# executed as instructions, and decoding it produces convincing false hits.
# ---------------------------------------------------------------------------
set -uo pipefail

if [ "$#" -eq 0 ]; then
    echo "usage: $0 <file.exe> [file.exe...]" >&2
    exit 2
fi

status=0
for f in "$@"; do
    echo "=== $f"

    if [ ! -f "$f" ]; then
        echo "    ERROR: no such file"
        status=1
        continue
    fi

    # objdump -d emits "004010f0 <symbol>:" headers; carry the most recent one
    # so a hit can be attributed to the function that has to be fixed.
    # A stripped binary has no symbols to attribute hits to, so everything
    # lands under the section symbol .text -- including the exception-table
    # data filtered below, which then cannot be told apart from real code.
    # Scan the unstripped binary in the build tree instead; it is the same
    # code, and build-win9x.sh already does.
    if [ "$(nm "$f" 2>/dev/null | wc -l)" = "0" ]; then
        echo "    NOTE: stripped binary -- no symbols, so exception-table data"
        echo "          cannot be told apart from code. Counts below overstate."
        echo "          Scan the unstripped build-tree binary for a real answer."
    fi

    # -j .text only. A bare `objdump -d` also disassembles
    # .gcc_except_table, and the exception tables' bytes decode as
    # plausible-looking SSE and MMX -- 16 phantom hits in a binary whose
    # actual code is clean. Those bytes are never executed as
    # instructions.
    report=$(objdump -d -j .text "$f" 2>/dev/null | awk '
        /^[0-9a-f]+ <.*>:$/ {
            sym = $2
            gsub(/[<>:]/, "", sym)
            next
        }
        # LSDA exception tables are emitted inside .text on MinGW, and their
        # bytes decode as plausible SSE and MMX -- 16 phantom hits in a binary
        # whose code is clean. They are data: the disassembly is littered with
        # (bad), and nothing ever jumps there.
        sym ~ /^\.gcc_except_table/ { next }
        /%(x|y|z)mm[0-9]/  { sse[sym]++; sse_total++;  next }
        /%mm[0-7]([^0-9]|$)/ { mmx[sym]++; mmx_total++; next }
        /\tcmov/           { cmov_total++; next }
        END {
            printf "TOTALS %d %d %d\n", sse_total, mmx_total, cmov_total
            for (s in sse)  printf "SSE %d %s\n", sse[s], s
            for (s in mmx)  printf "MMX %d %s\n", mmx[s], s
        }')

    totals=$(echo "$report" | awk '/^TOTALS/ { print $2, $3, $4 }')
    sse_total=$(echo "$totals" | cut -d' ' -f1)
    mmx_total=$(echo "$totals" | cut -d' ' -f2)
    cmov_total=$(echo "$totals" | cut -d' ' -f3)

    echo "    SSE/AVX : ${sse_total:-0}"
    echo "    MMX     : ${mmx_total:-0}"
    echo "    CMOV    : ${cmov_total:-0}   (Pentium Pro/II and later only)"

    if [ "${sse_total:-0}" != "0" ] || [ "${mmx_total:-0}" != "0" ]; then
        status=1
        echo "    ABOVE THE FLOOR — top offenders:"
        echo "$report" \
            | grep -E '^(SSE|MMX) ' \
            | sort -k2 -rn \
            | head -15 \
            | while read -r kind count sym; do
                  printf '      %-4s %6s  %s\n' "$kind" "$count" "$sym"
              done
    fi
done

exit $status
