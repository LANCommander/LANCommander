# picoposh gaps, from the LANCommander C++ SDK

A findings report for picoposh development, produced while porting
`LANCommander.SDK`'s PowerShell script layer to `LANCommander.SDK.Cpp`.

**Status: essentially everything in the first round of this report was fixed in
picoposh, and the two rounds since have closed more.** The verification below is what the C++ SDK now observes.
The historical findings are kept at the end, because they explain why the SDK's
runner is shaped the way it is — and why it could be simplified so sharply.

**Tested against** picoposh `dbdfe25` ("Add PowerShell string escaping with backtick"), built
with MinGW32 GCC 16.1.0 on Windows 11, unelevated. Every claim was reproduced
by running picoposh or by linking against it — nothing is inferred from docs.

---

## Headline

The twelve snippets LANCommander ships to script authors in
`LANCommander.Server/Snippets/Examples/` are the vocabulary users build install
and uninstall scripts from.

| | Before (`aa183fa`) | After (`1d6a328`) |
|---|---|---|
| Shipped snippets passing | **4 / 12** | **12 / 12** |
| Canonical Detect Install example | `False` (wrong, silent) | `True` (matches PowerShell) |

Reproduce with `tools/check-snippets-against-picoposh.sh`.

> Note on the earlier run: five of the eight original failures were reported
> against `HKLM`, which needs elevation. The sweep now rewrites those to the
> per-user hive, because an access-denied error says nothing about whether the
> interpreter supports the construct. Two others were snippets writing to
> registry keys that did not exist, which real PowerShell also rejects. The
> corrected before-figure is still 4/12 — the fixes are real — but the original
> 8-failure breakdown conflated three different causes.

---

## Verified fixed

### Silent wrong answers — all four resolved

| Was | Now |
|---|---|
| `Test-Path "HKLM:\SOFTWARE\Microsoft"` → `False`, exit 0 | `True`. Full provider: `New-Item`, `Remove-Item -Recurse`, `Get-ItemProperty`, `New-ItemProperty`, `Set-ItemProperty`, in both `HKCU:\` and `registry::\HKEY_CURRENT_USER\` forms. Nested key creation with `-Force` works. |
| `$env:PATH` → empty, exit 0 | Real values; `"$($env:TEMP)"` interpolates. |
| `$null -ne $someString` → `False` | Correct for strings, arrays, hashtables and unset, in both operand orders. |
| `Select-Object -ExpandProperty a` → `PicoObject` | Returns the property value. |
| `$PSScriptRoot` → empty | Correct when running a file. (Still empty for `-c`, which is right — there is no script.) |

### Capabilities

| Was | Now |
|---|---|
| No process execution at all | `Start-Process -FilePath -ArgumentList -Wait -PassThru`, with `ExitCode` readable off the returned object. |
| `Copy-Item -Recurse` on a directory failed | Works, including nested trees. |
| `Remove-Item -Recurse` failed on non-empty directories | Works. |
| `New-Item -ItemType Directory -Force` created one level | Creates the whole chain. |
| `-ErrorAction` ignored; every error terminating | `-ErrorAction Ignore`/`SilentlyContinue` and `$ErrorActionPreference` all continue, and `-ErrorAction Stop` still throws. |
| No `Expand-Archive` | Extracts real deflate archives (verified end to end against a Python-written zip). |
| Runaway loops unbounded | `pico_set_step_limit` / `pico_session_set_step_limit`; the run ends with a runtime error naming the limit. |
| `Start-Sleep`, `Get-Random`, `Write-Progress`, `Get-Process` missing | All present. |
| Here-strings, top-level `param()` unsupported | Both work; `param()` binds `-Name value` from `pico_*_args`. |
| Regex had no backslash escapes, non-greedy or non-capturing groups | `\s`, `\d`, `\w`, `\.`, `*?`, `(?:…)` and `{n,m}` all work as of `dbdfe25`. Verified with discriminating cases, not just "does it match" — `"aXbXc" -replace "a.*?X","-"` gives `-bXc` where greedy gives `-c`. The exact UT99 pattern from `Edit-PatchGameSpy` now matches. |
| Strings had no escape mechanism | Backtick escapes work in double-quoted strings: `` `n ``, `` `t ``, `` `$ ``, `` `" ``, `` `` ``. Single-quoted strings stay literal, as in PowerShell. |

### Errors that are now clear rather than confusing

- `[System.IO.Path]::Combine(...)` was `ParseError: expected ')'`. It is now
  `NotSupported: static method and property access on a .NET type
  ([Type]::Member) is not supported`. Deliberately unsupported, clearly said —
  which is the right outcome for a C89 interpreter.
- `New-Object -ComObject` is now `NotSupported: -ComObject is not supported
  (no COM in picoposh)`.

### The embedding API — this is the big one for us

`PicoSession` plus `pico_get_output`, `pico_set_step_limit` and `pico_run_ex`
removed an entire layer from the SDK. Verified semantics:

| Behaviour | Result |
|---|---|
| Host set → script reads → host reads back | Exact round-trip, including `O'Brien "$(Get-ChildItem)" \`x` |
| Variable names with spaces | Fine — they never reach the parser |
| `$Return` after the script calls `exit 7` | **Still readable**, and later runs still work |
| Unset vs `$null` | `get_variable` returns non-zero vs zero-with-empty-string. The distinction we needed |
| State across runs in one session | Persists — so setup can be its own run |
| Error line numbers | The script's own; a fault on line 3 reports `:3:` |
| `pico_get_output` | Returns the installed sink and its userdata |

What that deleted from `PicoPoshScriptRunner`:

- the generated `$Name = '<value>'` preamble;
- `quote_literal`, the single-quote-doubling escaper that was the security
  boundary between server-supplied data and executed script text — single
  quotes were the only safe form back then, since the lexer had no escapes at
  all; backtick escapes landed later, but by then the session API had removed
  the need to generate script text for values in the first place;
- the nonce-delimited stdout epilogue used to recover `$Return`;
- sentinel scanning, and stripping the block back out of the caller's output;
- line-buffering stdout on its way to the streaming callback so the sentinels
  could be withheld;
- error line-number correction, and the `preamble_lines` and `raw_error` fields
  that existed only to expose it;
- the documented caveat that `exit` destroys the return value.

Net: nine helper functions and two `ScriptResult` fields gone, the runner
shorter despite gaining session management, and one whole class of injection
bug that can no longer exist. Values cross the boundary as data.

The one place the SDK still generates script text is converting non-string
variables after injection — `$Manifest = ConvertFrom-Json $Manifest`,
`$Port = [int]$Port`. Because session state persists across runs, that runs as
its *own* script, so the user's script still starts at line 1. Nothing is
prepended to it.

### Cmdlet registration

`pico_register_cmdlet` / `pico_unregister_cmdlets` are in `pico_cmd.h`, with
`pico_registry` now returning host-registered cmdlets alongside the built-ins.
That is the hook LANCommander needs to ship its own cmdlets without forking, and
it works: the SDK now registers 22 of the .NET pack's 35 through it. See
`docs/API_REFERENCE.md` for the list and for which are still missing.

Two notes from using it in anger, neither blocking:

1. **The internal headers have no `extern "C"` guards.** Only `picoposh.h` does,
   so a C++ host that includes `pico_cmd.h`, `pico_pipeline.h` or `pico_value.h`
   to write a cmdlet gets C++ linkage on the declarations and fails to link
   against the C-compiled library. Wrapping the includes in `extern "C"` works
   and is what we do, but the guards would be a one-line fix per header, and
   `pico_register_cmdlet` is specifically an API for host programs — which on
   these targets are quite often C++.
2. **A `PicoCmdletDef` has no user-data slot.** Everything a cmdlet needs beyond
   its parameters has to come from process-global state, because the definition
   is static and the callbacks receive only the `PicoStage`. We work around it
   with a process-global context the host sets — which is what the .NET SDK
   effectively does too, by stashing its request factory in a session variable
   — so this is no longer blocking. A `void *userdata` on the definition,
   passed through to the callbacks, would still be tidier and would let two
   independent components register cmdlets without sharing globals.
   (`PicoStage::user` is per-stage scratch, not per-registration.)

### Build and packaging

- `CMAKE_SOURCE_DIR` → `PROJECT_SOURCE_DIR`: fixed.
- `-Werror`, the CLI target, and the install/export rules are now behind
  `PICOPOSH_WERROR`, `PICOPOSH_BUILD_CLI` and `PICOPOSH_INSTALL`, all defaulting
  to the top-level check. CTest is behind the same check.

The SDK consequently switched from compiling picoposh's sources itself to plain
`add_subdirectory` + `picoposh::picoposh`. That was overdue anyway: with five
families of per-platform backend (directory, HTTP, registry, process, ZIP) that
`build/sources.list` deliberately excludes, mirroring the selection by hand had
become exactly the drift it was meant to avoid — and it broke on the first
upstream change.

---

## Still open

Small, and none of it blocking.

### 1. `-Werror` breaks picoposh's own build on GCC 16

Building picoposh top-level with the default `PICOPOSH_WERROR=ON` fails:

```
third_party/libzip/lib/zip.h:272:22: error: comma at end of enumerator list [-Werror=pedantic]
src/os/pico_os_http_wininet.c:124:17: error: cast between incompatible function types
    from 'FARPROC' ... [-Werror=cast-function-type]
```

Two separate causes: a trailing comma in a vendored third-party header, and
`GetProcAddress` results cast to specific function pointer types — idiomatic
Win32, but `-Wcast-function-type` objects. `-DPICOPOSH_WERROR=OFF` builds clean.

This does not affect consumers (vendored ⇒ the option defaults off), but it
will break picoposh's CI as runners move to newer GCC. Suggestions: keep libzip's
headers out of the `-pedantic` set the way the OS backends already are, and
route the `GetProcAddress` casts through an intermediate `void (*)(void)`.

### 2. `&` and bare native command invocation

```powershell
& "cmd.exe" /c "exit 0"   # ParseError: expected a statement
cmd.exe /c "exit 0"       # RuntimeError: the term 'cmd.exe' is not recognized
```

`Start-Process` covers the underlying need, so this is not blocking. But `&` in
particular is common enough in copied-in scripts that a `NotSupported` message
naming the call operator would beat `ParseError: expected a statement` — the
same treatment `[Type]::Member` just got.

### 3. A pipeline cannot be assigned to a variable

```powershell
$a = 'hello'
$r = $a | Write-Output     # ParseError: expected a statement, at the '|'
$r = Write-Output 'hello'  # fine — a single command is accepted
$a | Write-Output          # fine — a bare pipeline statement is accepted
```

The README lists "`$var` assignment (from an expression *or* a pipeline)" as
supported, but only the single-command form parses; adding a `|` fails at that
token. Verified at `1d6a328` both via `-c` and from a script file.

This is the most-missed one so far. `$files = Get-ChildItem | Where-Object {…}`
is everyday PowerShell, and the workaround — run the pipeline as a bare
statement and capture stdout, or assign inside `ForEach-Object { $r = $_ }`,
which does reach the outer scope — is not obvious from the error.

It also makes pipeline-accepting cmdlets awkward to test: our own suite has to
assert on stdout rather than on `$Return`.

### 4. `-Param a,b` array arguments are not parsed

```powershell
Write-Output -InputObject 'a','b'    # binds 'a', then emits ',' and 'b' separately
Write-Output -InputObject @('a','b') # correct
```

A bare comma list in argument position does not become an array — the comma
arrives as its own argument token, so everything after it shifts onto the
following positional parameters. That is quiet and confusing: a cmdlet with
`-Path` then `-Hostname` positionally will bind `,` to `-Hostname`.

It matters because it is the natural PowerShell spelling and appears in
LANCommander's own scripting documentation
(`Edit-PatchGameSpy … -BinariesToPatch "*.dll","*.exe","*.so"`). Our
`Edit-PatchGameSpy` now detects a stray `,` argument and points at the `@(…)`
form rather than failing with a baffling message about hostname length.

Array *variables* and `@(…)` literals both bind correctly, so this is purely
the bare-comma spelling.

### 5. Array literals cannot start a pipeline

`@(1,2,3) | Out-Null` is a parse error; `$a | Out-Null` is fine. String and
hashtable literals *can* start one, so this is specific to `@(...)`. Minor, and
easy to work around, but the diagnostic does not hint at the cause.

---

## Historical findings (fixed — kept for context)

The original report, against `aa183fa`, is in this file's git history. Its
structure was:

- **Tier 1, silent wrong answers**: registry paths resolving as filesystem
  paths, `$env:` expanding to empty, `-eq`/`-ne` against `$null` comparing
  numerically, `Select-Object -ExpandProperty` returning the object,
  `$PSScriptRoot`/`$MyInvocation` empty.
- **Tier 2, blocked categories**: no process execution, no recursive
  filesystem operations, `-ErrorAction` unimplemented with all errors
  terminating, no archive extraction.
- **Tier 3, embedding API**: no way to pre-set or read back a variable, forcing
  source rewriting; no sink getter; no execution budget; `pico_run_capture` and
  `pico_set_output` silently mutually exclusive.
- **Tier 4**: static method access, here-strings, top-level `param()`,
  `Start-Sleep`, `Get-Random`, `Write-Progress`, `Get-Process`, `-ComObject`.
- **Build**: `CMAKE_SOURCE_DIR`, ungated `-Werror`/CLI/CTest/install.

One item from Tier 3 is worth restating because it is still true and still
undocumented: **`pico_run_capture` and `pico_set_output` remain mutually
exclusive.** `pico_con_out` checks the capture buffer first, so a sink
installed via `pico_set_output` is silently bypassed during a
`pico_run_capture` run. The SDK drives the sink directly and never calls
`pico_run_capture`, so this does not affect us — but the header still does not
say so, and the failure mode is silence.

---

## Reproducing

```bash
cd LANCommander.SDK.Cpp
cmake -S vendor/picoposh -B vendor/picoposh/build/cmake -DPICOPOSH_WERROR=OFF
cmake --build vendor/picoposh/build/cmake

# the shipped-snippet sweep — exits non-zero if any snippet fails
./tools/check-snippets-against-picoposh.sh

# the SDK's own suite, which exercises the session API end to end
cmake -S . -B build -DLANCOMMANDER_BUILD_TESTS=ON
cmake --build build
ctest --test-dir build --output-on-failure
```
