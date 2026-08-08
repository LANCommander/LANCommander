#!/bin/bash
# Runs every snippet LANCommander ships to script authors through picoposh and
# reports which ones survive. Run from LANCommander.SDK.Cpp/.
#
# Build picoposh first:
#   cmake -S vendor/picoposh -B vendor/picoposh/build/cmake
#   cmake --build vendor/picoposh/build/cmake
#
# Then:
#   ./tools/check-snippets-against-picoposh.sh
#
# Overridable:
#   PICOPOSH_BIN  path to the picoposh executable
#   SNIPPETS      directory of .ps1 snippets to sweep
#
# Exits non-zero if any snippet fails, so it can gate CI once the gaps in
# docs/PICOPOSH_GAPS.md are closed.

set -u

BIN="${PICOPOSH_BIN:-$PWD/vendor/picoposh/build/cmake/picoposh}"
SRC="${SNIPPETS:-$PWD/../LANCommander.Server/Snippets/Examples}"
WORK="$PWD/.snippet-sweep"

if [ ! -x "$BIN" ] && [ ! -x "$BIN.exe" ]; then
    echo "picoposh not found at $BIN — build it first (see the header of this script)" >&2
    exit 2
fi
[ -x "$BIN" ] || BIN="$BIN.exe"

if [ ! -d "$SRC" ]; then
    echo "snippet directory not found: $SRC" >&2
    exit 2
fi

pass=0
fail=0

# Snippets that write a registry *value* (New-ItemProperty) need the key to
# already exist — real PowerShell errors with "Cannot find path" otherwise, so
# a failure here would say nothing about picoposh. Pre-create the keys the
# substitutions below target, using picoposh itself.
REG_ROOTS='HKCU:\SOFTWARE\Test
HKCU:\SOFTWARE\WOW6432Node\Test\Sub
HKCU:\Software\Classes\VirtualStore\MACHINE\SOFTWARE\Test\Sub'

while IFS= read -r key; do
    "$BIN" -c "New-Item -Path '$key' -Force | Out-Null" >/dev/null 2>&1
done <<< "$REG_ROOTS"

for f in "$SRC"/*.ps1; do
    name=$(basename "$f" .ps1)

    # Fresh scratch tree per snippet, so one failure cannot cascade.
    rm -rf "$WORK"
    mkdir -p "$WORK/work/src"
    echo x > "$WORK/work/src/f.txt"
    echo y > "$WORK/work/rename-me.txt"

    # Substitute the <placeholders> a snippet ships with for real values, so we
    # are testing the construct rather than the placeholder.
    #
    # Bash parameter expansion rather than sed: these replacements are full of
    # backslashes, and sed processes escapes in the replacement text, which
    # silently ate them.
    body=$(cat "$f")
    body=${body#$'\xef\xbb\xbf'}                 # strip the BOM these ship with

    body=${body//'<Source Path>'/src}
    body=${body//'<Destination Path>'/dst}
    body=${body//'<Path>'/'Test\Sub'}
    body=${body//'<DirectoryPath>'/src}
    body=${body//'<FilePath>'/rename-me.txt}
    body=${body//'<NewName>'/renamed.txt}
    body=${body//'<Executable>'/game.exe}
    body=${body//'<RegKeyPath>'/'HKEY_LOCAL_MACHINE\SOFTWARE\Test'}
    body=${body//'<File Path>'/out.txt}

    # Rewrite to the per-user hive *after* the placeholders land, so a path
    # introduced by one of them is rewritten too. Writing under HKLM needs
    # elevation, and an access-denied error says nothing about whether the
    # interpreter supports the construct — real PowerShell fails those writes
    # unelevated as well.
    body=${body//'HKLM:'/'HKCU:'}
    body=${body//HKEY_LOCAL_MACHINE/HKEY_CURRENT_USER}

    {
        echo "\$InstallDirectory = 'work'"
        echo "\$NewPlayerAlias = 'AVeryLongPlayerName'"
        echo "$body"
    } > "$WORK/run.ps1"

    out=$(cd "$WORK" && "$BIN" run.ps1 2>&1)
    code=$?

    printf '%-34s | ' "$name"
    if [ $code -eq 0 ]; then
        printf 'ok\n'
        pass=$((pass + 1))
    else
        printf 'FAIL  %s\n' \
            "$(echo "$out" | head -1 | sed 's/^picoposh: run\.ps1:[0-9]*:[0-9]*: //')"
        fail=$((fail + 1))
    fi
done

rm -rf "$WORK"

# Leave no registry litter behind.
"$BIN" -c "Remove-Item -Path 'HKCU:\SOFTWARE\Test' -Recurse" >/dev/null 2>&1
"$BIN" -c "Remove-Item -Path 'HKCU:\SOFTWARE\WOW6432Node\Test' -Recurse" >/dev/null 2>&1
"$BIN" -c "Remove-Item -Path 'HKCU:\Software\Classes\VirtualStore\MACHINE' -Recurse" >/dev/null 2>&1

echo
echo "$pass passed, $fail failed, of $((pass + fail)) shipped snippets"

[ $fail -eq 0 ]
