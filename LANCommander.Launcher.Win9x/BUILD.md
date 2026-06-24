# Building the LANCommander Win9x Launcher

This launcher targets Windows 95/98 and is built as a 32-bit native executable
with MinGW + wxWidgets 2.8.x. The same `Makefile` works on Linux (cross-compile)
and on Windows (via MSYS2).

## Vendor dependencies

Before building on any platform, drop these single-file libraries into `vendor/`
(they are intentionally not committed):

| Path | Source | License |
| --- | --- | --- |
| `vendor/cjson/cJSON.{c,h}` | https://github.com/DaveGamble/cJSON | MIT |
| `vendor/miniz/miniz.{c,h}` | https://github.com/richgel999/miniz | MIT |
| `vendor/sqlite/sqlite3.{c,h}` | https://sqlite.org/download.html (the "amalgamation" zip) | Public domain |

## Building on Linux (cross-compile)

```sh
sudo apt install g++-mingw-w64-i686 make
# Build wxWidgets 2.8.x for i686-w64-mingw32 (see "wxWidgets" below)
make WX_CONFIG=/path/to/wx-2.8/bin/wx-config
```

## Building on Windows (MSYS2)

Use **MSYS2** with the 32-bit MinGW toolchain. Visual Studio is not supported
(the Makefile uses GCC-only flags such as `-static-libgcc`, `-static-libstdc++`,
`-mwindows`, and `wx-config`).

### 1. Install MSYS2

Download from https://www.msys2.org and run the installer. After install, open
the **MSYS2 MINGW32** shell (not "MSYS", not "MINGW64" — Win9x needs 32-bit).

### 2. Install the toolchain

```sh
pacman -S mingw-w64-i686-gcc mingw-w64-i686-make
```

### 3. Build wxWidgets 2.8.x

MSYS2 ships wxWidgets 3.x in its repos; this project is pinned to 2.8.x and must
be built from source. Grab `wxMSW-2.8.12.zip` (or any 2.8.x release) from the
wxWidgets archive, extract it, then from the MINGW32 shell:

```sh
cd /c/path/to/wxWidgets-2.8.12
./configure --host=i686-w64-mingw32 \
            --prefix=/c/wx-2.8 \
            --enable-jpeg --enable-png \
            --disable-shared --enable-unicode=no
make && make install
```

`--enable-jpeg --enable-png` is required — cover art will not render without
JPEG/PNG image handlers. `--disable-shared` keeps the launcher from depending on
wxWidgets DLLs at runtime, which is what you want for distribution.

### 4. Build the launcher

```sh
cd /c/path/to/LANCommander.Launcher.Win9x
mingw32-make WX_CONFIG=/c/wx-2.8/bin/wx-config \
             CXX=g++ CC=gcc
```

In the MINGW32 shell `g++` / `gcc` are already the i686 mingw compilers, so
overriding `CXX` / `CC` away from the Linux-style `i686-w64-mingw32-g++` default
is what lets the same Makefile work here.

Output is `lancommander.exe` next to the Makefile.

## Building on Windows (WSL2)

If you already use WSL, install a distro and follow the **Linux** instructions
above inside it. You will still need to build wxWidgets 2.8.x from source —
recent Ubuntu releases removed the `libwxgtk2.8-dev` package.

## Targets

- `make` — build `lancommander.exe`
- `make clean` — remove object files and the binary

## Runtime requirements

The resulting binary statically links `libgcc` and `libstdc++` and only depends
on Win9x-era system DLLs (`wininet`, `wsock32`, `shell32`, plus the wxWidgets
DLLs if you built wx with `--enable-shared`). No .NET runtime is required, which
is why this launcher exists as a separate project from the Blazor/Photino and
Avalonia launchers.
