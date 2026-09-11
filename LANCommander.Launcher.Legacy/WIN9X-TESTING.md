# Testing the legacy launcher on Windows 95/98

Produced by `./build-win9x.sh`. This is the manual pass that currently stands
in for automated coverage — there are no launcher tests yet.

> **Status: first hardware run 2026-09-08, on an AMD K6. Both backends died
> before `main()`, for two unrelated reasons. Both are fixed in the tree; the
> fixes have not themselves been confirmed on hardware yet.**
>
> ```
> Allegro:  LANCOMMANDER executed an invalid instruction in module
>           LANCOMMANDER.EXE at 016f:007b01ba
> SDL3:     The LANCOMMANDER.EXE file is linked to missing export
>           KERNEL32.DLL:AddVectoredExceptionHandler
> ```
>
> The SDL3 failure was three XP/2000-only imports from SDL — the loader names
> only the first it hits. `AddVectoredExceptionHandler` and
> `RemoveVectoredExceptionHandler` come from `SDL_systhread.c` naming threads
> for a debugger; `VerifyVersionInfoW` and `VerSetConditionMask` from
> `SDL_windows.c`'s version check. The fork resolves all of them through
> `GetProcAddress` now. They were not in `win9x-imports-deny.txt`, which is why
> the gate passed them.
>
> The Allegro failure was SSE2 — see **The CPU floor** below. It was never
> specific to Allegro; the SDL3 build had the same problem waiting behind its
> load failure.
>
> Everything past initialisation is still unverified. The rest of this document
> is what to do once a binary starts.
>
> The Allegro backend cannot be exercised anywhere but the target: it faults
> inside `DirectInputCreateEx` on Windows 11, and has since well before the
> SDL port (the prebuilt binary from 2026-05-23 fails identically).
>
> The SDL3 backend is in better shape. Because Windows 11 implements both the
> A and W entry points, the ANSI build it produces has been run there and
> renders correctly — so window creation, the message pump, display
> enumeration and font loading are known to work through the ANSI paths.

---

## Build

```sh
# From an MSYS2 MinGW 32-bit shell
tools/build-i586-toolchain.sh       # once, roughly an hour -- see below
./build-win9x.sh Release 8          # Allegro backend  -> out-win9x/
./build-win9x.sh Release 8 sdl3     # SDL3 backend     -> out-win9x-sdl3/
```

On Windows, run it from PowerShell instead -- no MSYS2 shell to open first:

```powershell
.uild-win9x.ps1 -Jobs 8                    # Allegro  -> out-win9x.uild-win9x.ps1 -Jobs 8 -Backend sdl3      # SDL3     -> out-win9x-sdl3```

or `build-win9x.cmd` from `cmd.exe` or a double-click. It finds MSYS2, checks
the MinGW 32-bit packages are installed, reports which toolchain the build
will use -- stock or i586 -- and then runs `build-win9x.sh` in a MinGW 32-bit
login shell. The gates below still run, and the wrapper returns their exit
code.

They land in separate directories so both can go to the VM and be compared.

### What is not in the Win9x build

`Expand-Archive` inside a script reports "not supported". Its backend, libzip,
imports `SetFilePointerEx` and `GetFileSizeEx`, which arrived in XP -- and an
import Win9x cannot resolve is refused by the *loader*, so the launcher would
not start at all rather than failing only when a script unpacked something.
The Win9x configure therefore links picoposh's zip stub
(`PICOPOSH_ENABLE_ZIP=OFF`). Nothing else is affected: the launcher's own game
extraction is miniz, which it drives through `src/app/zip_io.cpp` for the same
reason.

The build ends with two gates, and both have to pass:

| Gate | Catches |
|---|---|
| `tools/deny-scan.sh` | An import Win95/98 does not export. Fails at *load*, before `main()`. |
| `tools/isa-scan.sh` | An instruction above the CPU floor. Faults the moment it is reached — in practice inside a static initialiser, so also before `main()`. |

`build-win9x.sh` prints which toolchain it used in its banner. If that line
says "stock MSYS2", the binary will not run on a pre-SSE2 CPU no matter what
else is green.

---

## The CPU floor

This is the thing that bit hardest, so it is worth stating plainly: **the
stock MSYS2 toolchain cannot build a working binary for this target.**

MSYS2's mingw-w64 i686 GCC is configured `--with-arch=pentium4`. SSE2 is its
baseline and it emits `xmm` instructions for ordinary code — the 2026-09-08
crash was a `movq %xmm0,mem` storing eight bytes in a static initialiser in
`icons.cpp`. On a K6 or Pentium II that is an illegal opcode. Before the fix
the shipped binaries carried 45,845 (Allegro) and 97,546 (SDL3) such
instructions.

`STBI_NO_SIMD` / `STBIR_NO_SIMD` only ever disabled stb's hand-written
intrinsics. They said nothing about what the compiler generated, which is why
the problem survived so long unnoticed.

Two settings fix the code we compile:

- `-march=i586 -mtune=i686 -mfpmath=387`, applied at **directory** scope in
  `CMakeLists.txt` so SDL3, SDL_ttf, FreeType, SQLite, miniz and
  liblancommander inherit it. A `target_compile_options()` on `launcher`
  would not. Allegro is a separate CMake project and gets it from
  `build-win9x.sh`.
- `SDL_ASSEMBLY=OFF`, which turns off SDL's whole `SDL_SSE*`/`SDL_AVX*`/
  `SDL_MMX` family. Those kernels are runtime-dispatched behind
  `SDL_HasSSE2()` so they would not actually execute, but they are ~10k
  instructions of dead weight and they drown out real offenders in the scan.

That gets 45,845 down to 2,945. The rest is **not reachable by any compiler
flag**: it is inside the prebuilt runtime archives.

| Archive | Offending functions |
|---|---|
| `libstdc++.a` | 507 — `std::string::swap`, the move constructors, `locale`/`ctype`/`timepunct` initialisation |
| `libmingwex.a` | 32 |
| `libpthread.a` | 27 |
| `libmingw32.a` | 4 |

`std::locale` and `std::ctype` initialise during static construction, so a
binary linked against these dies before `main()` on any CPU without SSE2. A
three-line `std::string` program shows exactly the same residue, which is how
you can tell it is the toolchain and not this tree.

`tools/build-i586-toolchain.sh` closes it: GCC 16.1.0 built with
`--with-arch=i586 --with-tune=i686` into `/opt/i586-win9x`, then mingw-w64's
CRT and winpthreads rebuilt with it. It uses MSYS2's own configure line for
that GCC version, changing only the prefix, the arch and `--disable-bootstrap`,
and it leaves `/mingw32` alone. `build-win9x.sh` finds it automatically; set
`WIN9X_TOOLCHAIN` to put it elsewhere.

i586 and not i686 because the reference machine is a K6, which has neither
SSE nor CMOV -- those same archives carry ~1,340 CMOV instructions, which
would fault just as hard on a K6 or a plain Pentium. On a Pentium II or III,
CMOV is fine and `-march=i686` would be a valid, slightly better floor.

With that toolchain both backends scan clean:

| | before | after |
|---|---|---|
| SSE/AVX | 45,845 (Allegro) / 97,546 (SDL3) | **0** |
| MMX | 1 / 192 | **0** |
| CMOV | 4,626 / 1,360 | **1** |

The one remaining CMOV is in libstdc++'s `__x86_rdrand`, the RDRAND path
behind `std::random_device`. It is selected by a CPUID check that a K6 fails,
so it is never entered. Nothing else above the floor survives.

### Two traps worth knowing about

`libmsvcrt.a` is not purely an import library. It carries real code -- the
`*_s` secure wrappers and the time and stat helpers -- and
`__int_localtime32_s` and `__int_gmtime32_s` really do end up in the launcher.
A wholesale rebuild of it is wrong too: the regenerated archive lacks the
`_snwprintf_s` import entries the stock one has, which makes libzip's
`check_symbol_exists` fail, at which point its `compat.h` defines
`_snwprintf_s` as a macro that collides with the real declaration and nothing
in libzip compiles. The script therefore swaps rebuilt objects into the stock
archive by member name: stubs kept, code replaced.

`tools/isa-scan.sh` disassembles `.text`, and on MinGW the LSDA exception
tables live inside `.text`. Their bytes decode as convincing SSE and MMX --
16 phantom hits in a binary whose code is clean, with the disassembly littered
with `(bad)` where it gives up. The scan skips anything attributed to
`.gcc_except_table` for that reason.

### Which backend

| | `allegro` | `sdl3` |
|---|---|---|
| Status | Historically shipped; **also never verified on 9x** | Newer; never run on 9x |
| DirectX | **Required** (DirectDraw) | **Not required** — presents via GDI |
| EXE size | ~4.5 MB | ~7.4 MB |
| Runs on Win11 | No (faults in DirectInput) | Yes |

Neither has been confirmed on real hardware, so testing both is worthwhile —
they fail in different places, and that is diagnostic in itself.

The SDL3 backend needs no DirectX because SDL's framebuffer path is
`CreateDIBSection` + `BitBlt`, and every hardware renderer is compiled out.
That removes the largest install-time prerequisite, so if it works it is the
better package to ship.

Its Windows backend is built against the ANSI entry points (`SDL_WIN9X`, set
automatically by `TARGET_WIN9X`). On 9x the W-suffixed functions are exported
but fail with `ERROR_CALL_NOT_IMPLEMENTED`, so a normal UNICODE build links
cleanly and then cannot create a window. That comes from the
`LANCommander/SDL` fork, branch `lancommander/win9x`.

---

## What to copy

Two things, and **both are required**:

```
LANCommander.exe    4.5/7.4 MB  Allegro / SDL3; stripped, PE subsystem 4.0
assets/             ~2.1 MB   backgrounds/*.jpg, fonts/Inter-Regular.ttf, fonts/OFL.txt
```

The UI font is bundled now rather than pulled from the host font collection —
Win9x has neither Inter nor Segoe UI, and the old code silently fell back to
whatever was installed. **If `assets/fonts/` does not make it across, no text
renders at all.** That is by far the most likely "I copied it wrong" symptom,
so check it first if the window comes up blank.

`Data/` is created on first run and holds settings, the local game DB, cached
media and logs.

### Not needed any more

`gdiplus.dll` / `gdiplus.exe` — image decoding moved from GDI+ to stb, so
there is no redistributable to ship. `build-win9x.sh` deletes these from
`out-win9x/` if an older build left them there.

---

## What the target needs

Taken from the binary's actual import table, not guessed:

**Allegro backend:**

| DLL | Provided by |
|---|---|
| `ddraw.dll` | DirectX runtime — Allegro's windowed mode uses DirectDraw |
| `dsound.dll` | DirectX runtime (linked by Allegro; the launcher has no audio) |
| `wininet.dll` | IE4+ on Win95; built in on Win98 |
| `ws2_32.dll` | Built in on Win98; on Win95 needs the Winsock 2 update. Pulled in by the beacon client's UDP server discovery |
| `winmm.dll`, `gdi32`, `user32`, `kernel32`, `ole32`, `shell32`, `msvcrt` | base OS |

So: **a DirectX runtime and, on Win95, Internet Explorer.** `DirectX-80a.zip`
(8.0a, the last version supporting Win9x) is staged in `out-win9x/` along with
`7z920.exe` to unpack it on the target.

**SDL3 backend** — same minus DirectX, plus a few more system DLLs:

| DLL | Note |
|---|---|
| `wininet.dll` | IE4+ on Win95; built in on Win98 |
| `ws2_32.dll` | Built in on Win98; on Win95 needs the Winsock 2 update. Pulled in by the beacon client's UDP server discovery |
| `advapi32`, `imm32`, `setupapi`, `shell32`, `version` | present on Win98; `setupapi` is missing on early Win95 |
| `gdi32`, `user32`, `kernel32`, `ole32`, `winmm`, `msvcrt` | base OS |

No `ddraw.dll`, no `dsound.dll`. On Win95, `setupapi` and `ws2_32` are the
two to watch; both are fine on Win98.

There is no MinGW runtime DLL to ship — `-static -static-libgcc
-static-libstdc++` links the CRT in.

---

## Choosing a VM

**86Box — recommended.** Emulates period-correct hardware (S3 Trio64/Virge,
Sound Blaster 16, a real BIOS), so it exercises the same DirectDraw paths and
CPU feature set as an actual machine of the era. That matters here: the whole
point of this target is old hardware, and stb is built with `STBI_NO_SIMD` /
`STBIR_NO_SIMD` precisely because SSE2 would be an illegal opcode on a
Pentium II. PCem is the older sibling; 86Box is actively maintained.

**VirtualBox — faster to iterate.** Boots Win98 SE fine, but DirectDraw
support is thin and it will not catch CPU-feature or timing problems. Good
enough for "does it start and render", not for "is it correct".

**Hyper-V — not viable.** No legacy BIOS or VGA emulation; Win9x will not
install.

Getting files in: mount a folder as a CD-ROM ISO under 86Box. VirtualBox
shared folders need Guest Additions, which are unreliable on 9x.

---

## Smoke checklist

Window and chrome:

- [ ] Frameless window appears at 800×600 with the custom title bar
- [ ] Title bar drags the window
- [ ] All 8 resize edges/corners work, and show the right cursor
- [ ] Resize clamps at 640×480
- [ ] Minimise and restore
- [ ] Close button, and Alt+F4

Rendering:

- [ ] **Text renders** (see the `assets/fonts/` note above)
- [ ] A login background image appears — this proves stb's JPEG decoder runs
      on that CPU, which is the main thing the SIMD flags guard
- [ ] The dark overlay over the background looks right, not banded or inverted
- [ ] Title bar tint and the game-detail gradient render smoothly

Input:

- [ ] Type into all three login fields
- [ ] Shift and Caps Lock produce the right characters
- [ ] Backspace, Tab between fields, Enter submits
- [ ] Mouse wheel scrolls the library grid

Application:

- [ ] Offline mode (settings screen) lets you reach the library without a
      server — useful if you have no LANCommander instance handy
- [ ] With a server: login, library, game detail, a download
- [ ] `Data/Logs/*.log` is written

Performance, worth eyeballing on a period-appropriate CPU:

- [ ] Roughly 30 fps and not pegging the CPU when idle
- [ ] Scrolling the library does not stall on image decode

---

## Diagnostics

`Data/Logs/<timestamp>.log` is the only instrument — there is no debugger on
target. The logger flushes per line, so the file survives a hard crash and the
last entry tells you how far initialisation got.

Useful landmarks in a healthy startup log:

```
[INFO] Launcher started              <- log_init, before the display exists
[INFO] Init complete, starting on ServerSelect screen (alias=, surface=800x600)
[INFO] Beacon sweep found 1 server(s)  <- UDP discovery; needs ws2_32
```

If you get the first line and nothing else, the failure is in display
creation, the chrome, or the font/asset load.

If the process dies before *any* log line, it never reached `main()` — that is
a loader problem: a missing DLL or an unsupported import. Re-run
`tools/deny-scan.sh` against the exact binary you copied.

---

## If you find something

The Win9x path has no automated coverage, so a reproduction plus the log is
the whole bug report. Worth noting which of the two backends and which VM,
since DirectDraw behaviour differs sharply between 86Box and VirtualBox.
