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

`BatchScriptRunner` uses only Win32 APIs available since Windows 95:
- `CreateProcessA`
- `CreatePipe`
- `GetEnvironmentStringsA` / `FreeEnvironmentStringsA`
- `GetTempPathA` / `GetTempFileNameA`
- `ReadFile`, `WaitForSingleObject`, `GetExitCodeProcess`

No Unicode APIs (W-suffix) are used. All string handling is ANSI.

### Archive Extraction

The `IArchiveExtractor` interface has no built-in implementation. For Win9x:
- **minizip** (part of zlib) is the recommended backend. zlib compiles cleanly with Watcom and MSVC 6.
- The `Crc32` utility class has zero dependencies and works everywhere.

### Considerations

- All string handling uses `std::string` (ANSI). No `std::wstring` or Unicode assumptions.
- No C++11 `thread`, `mutex`, or `chrono` usage in the core library.
- `std::function` (from `<functional>`) is used for callbacks. If your toolchain lacks it, you can replace `DownloadProgressFn` and `ExtractionProgressFn` with plain function pointers.
- The library avoids `long long` in public APIs where possible, but uses it for file sizes (`Archive::compressed_size`, etc.) which may exceed 2GB.

## Modern Windows (7/10/11)

Everything works out of the box with MSVC 2015+ or MinGW-w64. Both HTTP backends are available.

For the script runner, `BatchScriptRunner` works. If you need PowerShell execution, implement `IScriptRunner` to invoke `powershell.exe -File`.

## Linux / macOS

Use the `CurlHttpClient` backend (requires libcurl).

`BatchScriptRunner` is Windows-only and returns an error on other platforms. Implement `IScriptRunner` with a shell-based runner:

```cpp
class ShellScriptRunner : public lancommander::IScriptRunner {
    // Use fork/exec or popen to run /bin/sh scripts
    // Inject variables via the environment block passed to execve
};
```

The `Crc32` utility and `IArchiveExtractor` interface are fully portable.

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

The core library's only dependency is a C++14 standard library and cJSON (vendored C). If your target has a C standard library but limited C++:
- The models are plain structs — they just need `<string>`, `<vector>`, `<map>`
- JSON parsing uses only C APIs (cJSON)
- `Result<T>` uses `std::string` and move semantics

## Backend Selection Summary

| Target | HTTP Backend | Script Runner | Archive Extractor |
|--------|-------------|---------------|-------------------|
| Windows 95/98/ME | WinInetHttpClient | BatchScriptRunner | minizip (bring your own) |
| Windows XP+ | WinInetHttpClient | BatchScriptRunner | minizip or libarchive |
| Windows 10/11 | Either | BatchScriptRunner | minizip or libarchive |
| Linux | CurlHttpClient | ShellScriptRunner (bring your own) | libarchive |
| macOS | CurlHttpClient | ShellScriptRunner (bring your own) | libarchive |

## Compiler Compatibility Matrix

| Compiler | Minimum Version | Notes |
|----------|----------------|-------|
| MSVC | 2015 (v19.0) | Full support |
| GCC | 5.0 | Full support |
| Clang | 3.4 | Full support |
| MinGW-w64 | 5.0 | Full support |
| Open Watcom | 2.0 | May need minor workarounds for `<functional>` |
| Visual C++ 6.0 | SP6 | Template limitations; may need `std::function` replaced with function pointers |
