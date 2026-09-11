# Testing the legacy launcher on MS-DOS

Produced by `./build-dos.sh`. Like the Win9x pass, this stands in for
automated coverage on the target — the headless tests and the SDK tests run
on the CI host, but nothing that touches VESA, INT 33h or Watt-32 can.

> **Status: runs under DOSBox-X; never run on real hardware.** The launcher
> starts, finds a VBE 2.0 mode, and renders the server-select screen
> correctly — background photograph, panel, text, buttons, chrome and mouse
> pointer. SQLite opens its database through the new DOS VFS, and beacon
> discovery completes and reports no servers rather than hanging.
>
> What that does **not** cover: keyboard entry, clicking, logging in, any
> HTTP request against a real server (Watt-32 has never had a packet driver
> under it here), downloading, extracting, launching a game, or uninstalling.
> Everything from step 4 of the smoke checklist down is unverified.
>
> Unlike the Win9x situation there is no second backend to compare against:
> Allegro dropped DOS after 4.2 and SDL3 has no DOS video driver, so on this
> target `gfx_dos.cpp` *is* the graphics stack.

---

## Build

```sh
./build-dos.sh Release 8      # -> out-dos/
```

On Windows, run it from PowerShell instead:

```powershell
.uild-dos.ps1 -Jobs 8       # -> out-dos```

or `build-dos.cmd` from `cmd.exe` or a double-click. Both are front ends for
the same script: they find MSYS2, check that the packages the build needs are
installed, and hand it a MinGW 32-bit login shell. The DJGPP compiler is a
native Windows build, but Watt-32 configures with a shell script and builds
with GNU make, so MSYS2 is required either way -- the wrapper names what is
missing rather than failing inside CMake.

First run fetches two things into `vendor/` (both gitignored):

| | What | Fetched by |
|---|---|---|
| DJGPP | GCC 12.2 cross toolchain, plus CWSDPMI | `tools/setup-djgpp.sh` |
| Watt-32 | TCP/IP stack, built from the picoposh submodule | `tools/setup-watt32.sh` |

### Why DJGPP and not OpenWatcom

picoposh's own DOS target is 16-bit real mode under OpenWatcom, and this is
deliberately not that. The launcher is 14k lines of C++14 with `std::string`,
`std::map` and `std::vector` throughout; OpenWatcom's C++ does not reach C++14,
and real mode's 64K near-data group does not reach a 32-bit ARGB backbuffer at
800x600 (1.9 MB) let alone the image cache above it.

DJGPP gives 32-bit protected mode, a complete libstdc++ and flat addressing,
at the cost of needing a DPMI host — which is what `CWSDPMI.EXE` is.

---

## What to copy

The whole of `out-dos/`:

```
LANCMDR.EXE     the launcher (~3.5 MB)
CWSDPMI.EXE     DPMI host; loaded automatically if DOS is not providing one
cwsdpmi.doc     CWSDPMI's documentation and licence
assets/         font, icons and login backgrounds, loaded by path at runtime
```

8.3 names throughout, because on a DOS machine without a long filename driver
that is all the shell can see. `LANCOMMANDER.EXE` would simply not be there.

---

## What the target needs

| | Requirement | If it is missing |
|---|---|---|
| CPU | 386 or better | Will not load |
| RAM | ~16 MB free extended memory | Fails to allocate the backbuffer and image cache |
| DPMI | CWSDPMI.EXE next to the launcher, or a host already running (Windows DOS box, EMM386, QEMM, JEMM386) | "no DPMI" from the go32 stub |
| Video | VESA VBE **2.0** with a linear frame buffer, ideally 800x600 | Startup fails with the VBE message; see below |
| Mouse | An INT 33h driver (CuteMouse recommended) | UI runs, but is keyboard-only and draws no pointer |
| Network | A packet driver bound to a NIC, plus `WATTCP.CFG` or DHCP | Everything renders; every request fails with "No TCP/IP" |
| Filesystem | A long filename driver (DOSLFN) — **required in practice** | The UI draws, with no text and no artwork |

### VESA

The launcher asks for 800x600 and takes the closest VBE 2.0 mode with a linear
frame buffer at 32, 24 or 16 bpp, preferring the deepest. Banked VBE 1.x modes
are not supported at all.

Cards that only offer VBE 1.2 can be brought up to 2.0 in software with
[UniVBE](http://www.scitechsoft.com) or, on emulated hardware,
[VBEMP/`univbe`-alikes](https://github.com/...). DOSBox-X provides VBE 2.0 out
of the box.

### Networking

Watt-32 needs a packet driver. Under DOSBox-X, enable the NE2000 emulation and
load the matching driver; on real hardware it is whatever the NIC shipped with.
Then either put a `WATTCP.CFG` next to the executable or let DHCP answer.

**There is no HTTPS.** Watt-32 has no TLS, so an `https://` server URL is
refused with a message saying so rather than silently downgraded — sending a
bearer token in the clear to a server that was asked for securely would be
worse than failing. A DOS client needs the server reachable over plain HTTP.

### 8.3 and long filenames

This is the one thing most likely to make the launcher look broken rather than
report a problem, so it is worth understanding before anything else.

The **assets** are not 8.3 names: `Inter-Regular.ttf`, the `backgrounds/`
directory, and every icon (`arrow-left.png` and friends). Without a long
filename driver DOS cannot open any of them by those names, and the result is
a launcher that lays its screens out perfectly and draws no text and no
artwork — verified under DOSBox-X with `lfn=false`. Since 2026-09-07 a font
that cannot be loaded is a hard startup failure with a message naming the
file, rather than a blank UI.

Two things in the launcher's own *storage* exceed 8.3 as well:

* the media cache, which names files after media GUIDs, and
* `.lancommander/`, the per-install metadata directory shared with the .NET
  launcher.

All of it works under DOSLFN or in a Windows 9x DOS box, and none of it works
on bare DOS 6.22 without one. The log file is deliberately 8.3
(`Data\LOGS\LCddHHMM.LOG`) so that a machine with no LFN still produces the
one artefact you need to diagnose the rest.

Game archives are a further case, and the one that is not ours to fix:
whether a game unpacks depends on the names inside its own zip.

---

## What is different from the Windows builds

These are consequences of the platform, not gaps to be filled in later.

**Downloads block the UI.** DOS is single-tasking. `worker_dos.cpp` runs the
download, the media prefetch and the art fetch inline instead of on a thread,
so the launcher stops repainting for the duration of a transfer. The
alternative — a timer-interrupt scheduler switching stacks under the UI — would
need libc, stdio and Watt-32 to be re-entrant across a hardware interrupt, and
none of them are.

**Launching a game takes the machine.** `process_dos.cpp` drops the video mode,
runs the game through `COMMAND.COM`, and restores the mode when it exits. The
launcher is not running in between, so Stop can never be pressed and the play
session ends the moment the game does. A real-mode game that needs nearly all
of conventional memory may not fit alongside the DPMI host.

**No window chrome behaviour.** The title bar, footer and close button all draw
and work, but there is no window to drag, resize or minimise — the launcher
owns the screen. `chrome_platform_dos.cpp` is the stub `chrome_platform.h`
always said DOS would need.

**No clipboard.** `clipboard_null.cpp` gives copy and paste within the process,
which is the whole of what DOS offers.

**Softer text.** `font_stb.cpp` rasterises the bundled Inter through
stb_truetype, which does not hint. FreeType's hinted output on the SDL build is
crisper at these sizes.

---

## Performance

Measured in DOSBox-X at `cycles=100000`, on the server-select screen at
800x600x32, averaged over 8 frames:

| Phase | Before | After |
|---|---|---|
| input | 0 ms | 0 ms |
| clear | 19 ms | 5 ms |
| **screen draw** | **291 ms** | **17 ms** |
| chrome | 13 ms | 13 ms |
| present | 4 ms | 4 ms |
| **frame** | **403 ms (2.5 FPS)** | **39 ms (~25 FPS)** |

Three things were wrong, in ascending order of how much they mattered.

**Per-pixel integer division.** `blend_over` did four `/255` operations per
pixel and `blit_scaled` two divides per pixel. A divide costs tens of cycles
on a 386, and a full-screen scrim is 480,000 pixels — about 2.9 million
divides a frame. `div255()` is now a shift-based exact form, and
`blit_scaled` hoists the column arithmetic into a table computed once per
call. Worth 18%, which is the part that is easy to over-estimate: it was
real, and it was not the problem.

**Recompositing a static picture every frame.** `auth_background_draw` did a
full-screen aspect-fill scale *and* a full-screen alpha blend on every frame,
to produce an image that only changes when the window resizes or a new
background is picked. It is now composed once into a cached surface and
blitted. That is the 291 ms → 17 ms line, and it helps every backend — DOS is
just where it stopped being survivable.

**A scalar store loop for `clear()`.** Replaced by filling one row and
`memcpy`-replicating it, which becomes a `rep movsd`. 19 ms → 5 ms.

### On real hardware

Untested, and it will be slower. What remains is essentially memory
bandwidth: clear, background blit, panel and text, then the present copy —
roughly 10 MB of traffic per frame at 800x600x32. A 486DX2-66 over VLB moves
perhaps 40-60 MB/s, which puts it back near 200 ms a frame; a Pentium over
PCI should manage 10-15 FPS. Those are estimates from the bandwidth, not
measurements.

If it is too slow on a real machine, the lever is resolution rather than more
micro-optimisation: the launcher asks for 800x600 and `mode_score()` takes
the closest VBE mode, so a machine offering 640x480x16 already pays 2.5x less
per frame. Making the requested size a setting would be the next step.

---

## Testing under DOSBox-X

Faster than a real machine and enough to catch anything structural.

```sh
./build-dos.sh            # once, and after any change
tools/run-dos.cmd         # starts it
```

`run-dos.cmd` finds DOSBox-X (a portable copy under `vendor/dosbox-x/`, or one
on `PATH`), generates a config with the right absolute mount path, and boots
straight into the launcher. It does not build — it runs whatever is in
`out-dos/`. A desktop shortcut pointing at it is the usual way to start this.

`tools/dosbox-x-test.conf` is the same settings written out by hand, with a
comment on each saying what breaks without it. Reach for it when you want to
change one.

What it cannot tell you: how a real card answers the VBE mode query, how a
real mouse driver scales mickeys, and anything about Watt-32 on real hardware.

---

## Smoke checklist

Steps 1-3 pass under DOSBox-X. Everything from 4 down is untried.

1. ✅ `LANCMDR.EXE` starts and reaches a video mode.
2. ✅ The server-select screen renders: background, panel, text, buttons,
   chrome. (With `lfn=false` it renders the layout and nothing else — see
   "8.3 and long filenames".)
3. ✅ The mouse pointer is drawn, and beacon discovery finishes and reports
   "No servers found" rather than hanging.
   ✅ The frame takes ~39 ms under DOSBox-X, so the pointer keeps up. See
   "Performance" for what that was before and why.
4. The pointer tracks correctly and clicks land where it is. (Position is
   integrated from relative mickeys — if it drifts or moves at the wrong
   speed, that is the place to look.)
5. Typing works, including backspace, tab between fields, and Enter.
6. Server discovery finds a real server on the LAN (needs a packet driver).
7. Login succeeds against a plain-HTTP server.
8. The library lists, with art appearing as the prefetcher drains.
9. A small game downloads and extracts.
10. Launching it hands the machine over and comes back to a working UI.
11. Uninstall removes what the manifest lists.

---

## Diagnostics

`Data\LOGS\LCddHHMM.LOG` under the executable is written line by line, so it
survives a hard lock-up. It is the first thing to read after any failure,
including one that leaves the screen unusable.

If the machine needs a power cycle after an exit, the mode restore in
`gfx_dos.cpp` is the suspect — `shutdown_display()` unmaps the frame buffer
and puts the old mode back, and `dos_suspend_display()` does the same around a
game launch.

If the pointer is present but offset, the mouse driver is reporting mickeys at
a different scale; the integration is in `input_dos.cpp`.

---

## If you find something

The DOS-specific surface is five files and nothing else:

| File | Owns |
|---|---|
| `src/gfx/gfx_dos.cpp` | VBE mode set, LFB mapping, the software rasteriser, the pointer |
| `src/ui/input_dos.cpp` | BIOS keyboard, INT 33h mouse |
| `src/ui/font_stb.cpp` | text (portable; nothing in it is DOS-specific) |
| `src/app/worker_dos.cpp`, `fs_dos.cpp`, `process_dos.cpp` | the platform seams |
| `vendor/sqlite3/sqlite3_os_dos.c` | SQLite's VFS |

Everything above those is the same code the Windows builds run.
