# Platform Notes

## Windows 95/98/ME

The library is designed to compile and run on Win9x with appropriate toolchains.

### Compiler Options

- **Open Watcom 2.0** — Targets Win16 and Win32. Use the Win32 target for this SDK. Supports C++14 partially; the SDK avoids features that Watcom lacks.
- **Visual C++ 6.0** — The last MSVC to produce Win9x-native binaries without compatibility shims. May need minor adjustments for template support.
- **MinGW with `-mwindows`** — Modern MinGW can target Win9x if the right runtime is used. Pair with the WinINet backend.

### HTTP Backend

Use `WinInetHttpClient`. WinINet (`wininet.dll`) is available on Win9x when Internet Explorer 3.0+ is installed — which is virtually every Win9x system in practice.

libcurl can also be built for Win9x but requires more setup. WinINet is the path of least resistance.

### Script Execution

`PicoPoshScriptRunner` embeds the picoposh interpreter, which is strict C89.
Between it and the SDK's own path helpers, the only Win32 APIs used are ones
present on Windows 95:

- `GetFileAttributesA`, `CreateDirectoryA`, `RemoveDirectoryA`, `DeleteFileA`
- `SetCurrentDirectoryA` / `GetCurrentDirectoryA`
- `FindFirstFileA` / `FindNextFileA` / `FindClose`
- `GetTempPathA`, `GetCurrentProcessId`, `GetTickCount`
- `CreateProcessA`, `WaitForSingleObject`, `GetExitCodeProcess` (`Start-Process`)
- `RegOpenKeyExA`, `RegQueryValueExA`, `RegSetValueExA`, `RegCreateKeyExA`,
  `RegDeleteKeyA`, `RegEnumKeyExA` from ADVAPI32 (the `HKLM:`/`HKCU:` provider)

`Invoke-WebRequest` uses WinINet, resolved at runtime via `LoadLibrary` /
`GetProcAddress` — no import library is needed, and it degrades gracefully if
`wininet.dll` is absent.

`Expand-Archive` is backed by libzip and zlib, both vendored as picoposh
submodules. On the vintage Watcom targets picoposh substitutes zlib+minizip,
which is what compiles under a strict-ANSI 16-bit compiler. If neither is
present, the cmdlet reports cleanly rather than failing the build.

No Unicode APIs (W-suffix) are used. All string handling is ANSI. picoposh also
avoids `long long` throughout, which its own lint gates on.

### Archive Extraction

`ZipArchiveExtractor` ships and needs nothing extra: it uses the same libzip
picoposh vendors for `Expand-Archive`, so there is one zip implementation in the
process rather than two. On the vintage Watcom targets picoposh substitutes
zlib+minizip, which is what compiles under a strict-ANSI 16-bit compiler.

Build picoposh without its zlib/libzip submodules and both the extractor and
`Expand-Archive` report unavailable rather than failing to link —
`ZipArchiveExtractor::available()` says which.

`IArchiveExtractor` remains abstract if you want a different backend, and the
`Crc32` utility has zero dependencies and works everywhere.

### Considerations

- All string handling uses `std::string` (ANSI). No `std::wstring` or Unicode assumptions.
- No C++11 `thread`, `mutex`, or `chrono` usage in the core library.
- `std::function` (from `<functional>`) is used for callbacks. If your toolchain lacks it, you can replace `DownloadProgressFn` and `ExtractionProgressFn` with plain function pointers.
- The library avoids `long long` in public APIs where possible, but uses it for file sizes (`Archive::compressed_size`, etc.) which may exceed 2GB.

## Modern Windows (7/10/11)

Everything works out of the box with MSVC 2015+ or MinGW-w64. Both HTTP backends are available.

`PicoPoshScriptRunner` runs the same embedded interpreter here as everywhere
else. If you need full Windows PowerShell semantics — real cmdlets,
`Start-Process`, elevation — implement `IScriptRunner` over `pwsh` or
`powershell.exe -File`. See [picoposh limitations](API_REFERENCE.md#picoposh-limitations)
for what the embedded subset cannot do.

## Linux / macOS

Use the `CurlHttpClient` backend (requires libcurl).

`PicoPoshScriptRunner` works unchanged — picoposh selects its POSIX directory
and socket backends automatically. One caveat: `Invoke-WebRequest` is plain HTTP
only on these platforms, because the BSD-socket backend has no TLS. Download
host-side if you need HTTPS.

The `Crc32` utility, the path helpers and the `IArchiveExtractor` interface are
fully portable.

## Cross-Compilation

### Win9x from a Modern Linux Host

```bash
# Using MinGW cross compiler targeting i686
mkdir build && cd build
cmake .. \
    -DCMAKE_SYSTEM_NAME=Windows \
    -DCMAKE_C_COMPILER=i686-w64-mingw32-gcc \
    -DCMAKE_CXX_COMPILER=i686-w64-mingw32-g++
cmake --build .
```

### Embedded / Minimal Systems

The core library needs a C++14 standard library plus three vendored C
dependencies: cJSON, libyaml and picoposh (which brings zlib and libzip for
`Expand-Archive`). All are C, all are built from source in-tree, and none needs
anything from the system. If your target has a C standard library but limited
C++:
- The models are plain structs — they just need `<string>`, `<vector>`, `<map>`
- JSON parsing uses only C APIs (cJSON); YAML likewise (libyaml)
- `Result<T>` uses `std::string` and move semantics
- Dropping picoposh's zlib/libzip submodules removes the two largest
  dependencies, at the cost of archive extraction

## Backend Selection Summary

Neither the script runner nor the archive extractor is a per-platform choice
any more — picoposh is portable and libzip comes with it — so only the HTTP
backend varies.

| Target | HTTP Backend | Script Runner | Archive Extractor |
|--------|-------------|---------------|-------------------|
| Windows 95/98/ME | WinInetHttpClient | PicoPoshScriptRunner | ZipArchiveExtractor (zlib+minizip under Watcom) |
| Windows XP+ | WinInetHttpClient | PicoPoshScriptRunner | ZipArchiveExtractor |
| Windows 10/11 | Either | PicoPoshScriptRunner | ZipArchiveExtractor |
| Linux | CurlHttpClient | PicoPoshScriptRunner | ZipArchiveExtractor |
| macOS | CurlHttpClient | PicoPoshScriptRunner | ZipArchiveExtractor |

## Compiler Compatibility Matrix

| Compiler | Minimum Version | Notes |
|----------|----------------|-------|
| MSVC | 2015 (v19.0) | Full support |
| GCC | 5.0 | Full support |
| Clang | 3.4 | Full support |
| MinGW-w64 | 5.0 | Full support |
| Open Watcom | 2.0 | May need minor workarounds for `<functional>` |
| Visual C++ 6.0 | SP6 | Template limitations; may need `std::function` replaced with function pointers |

The vendored picoposh is strict C89 and builds under Open Watcom 2.0 and
MinGW32 alongside modern toolchains. The SDK compiles it with its own warning
flags rather than upstream's `-Werror` / `/Za`, so a newer compiler growing a
new warning cannot break a consumer's build.
