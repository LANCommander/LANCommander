# liblancommander

A cross-platform C++14 client library for the LANCommander game server API. Designed for maximum compatibility from Windows 95 through modern desktop and embedded platforms.

The directory is `liblancommander`, matching the `libyaml`/`libzip` convention it sits alongside. The CMake target is `lancommander` — which is what produces `liblancommander.a` — and the include path and namespace are both `lancommander`.

## Features

- **17 API clients** covering authentication, games, library, saves, media, tools, depot, script execution, and more
- **HTTP backend abstraction** — ship with WinINet (Windows) or libcurl (cross-platform), or write your own
- **Zip archive extraction** on libzip, with CRC32 skip-unchanged-files and a zip-slip guard
- **Embedded PowerShell script execution** via [picoposh](https://github.com/LANCommander/picoposh) — the same `.ps1` files the .NET launcher runs, from the same `.lancommander/` layout, with no external shell. See [known limitations](docs/API_REFERENCE.md#picoposh-limitations).
- **LANCommander cmdlet pack** — 22 of the .NET SDK's custom cmdlets, opt-in via `cmdlets::register_all()`. See [the list](docs/API_REFERENCE.md#lancommander-cmdlets).
- **Shared install directories** — reads and writes the same `Manifest.yml` as the .NET launcher, via vendored libyaml
- **Connection lifecycle management** with ping/pong validation and offline mode
- **Zero mandatory external dependencies** beyond a C++14 compiler — cJSON, libyaml and picoposh are vendored, and libzip comes with picoposh

## Quick Start

```cpp
#include <lancommander/lancommander.h>
#include "wininet_http_client.h"  // or "curl_http_client.h"

int main() {
    // 1. Create an HTTP backend
    lancommander::WinInetHttpClient http;
    http.set_base_url("http://192.168.1.100:1337");

    // 2. Authenticate
    lancommander::AuthenticationClient auth(http);
    auto token = auth.login("player", "password");

    if (!token) {
        printf("Login failed: %s\n", token.error.c_str());
        return 1;
    }

    http.set_bearer_token(token.value.access_token);

    // 3. Use any client
    lancommander::GameClient games(http);
    auto all_games = games.get_all();

    if (all_games) {
        for (size_t i = 0; i < all_games.value.size(); ++i)
            printf("  %s\n", all_games.value[i].title.c_str());
    }

    return 0;
}
```

## Building

### Requirements

- CMake 3.14+
- C++14 compiler (and a C compiler for the vendored cJSON and picoposh)
- **Windows**: MSVC, MinGW, or Open Watcom (for Win9x targets)
- **Linux/macOS**: GCC 5+ or Clang 3.4+
- The **picoposh submodule**, which lives at `vendor/picoposh`

The simplest way to get it is `./setup-vendor.ps1` from the repository root,
which handles this alongside the other vendored downloads. By hand:

```bash
git submodule update --init --depth 1 -- liblancommander/vendor/picoposh

# picoposh's own zlib and libzip, which back Expand-Archive
git -C liblancommander/vendor/picoposh submodule update --init --depth 1 \
    -- third_party/zlib third_party/libzip
```

Never use `--recursive`: picoposh also pins Watt-32, a large DOS-only TCP/IP
stack this SDK never compiles. Skipping zlib/libzip is fine too — `Expand-Archive`
then links a stub and reports cleanly, and everything else still builds.

### Build Steps

```bash
mkdir build && cd build
cmake ..
cmake --build .
```

This produces the static libraries `lancommander` and `picoposh` and, on
Windows, the `lancommander_wininet` backend. If libcurl is found,
`lancommander_curl` is also built.

To build and run the SDK's own tests, plus the script-runner demo:

```bash
cmake -S . -B build -DLANCOMMANDER_BUILD_TESTS=ON -DLANCOMMANDER_BUILD_EXAMPLES=ON
cmake --build build
ctest --test-dir build --output-on-failure
```

Both are `OFF` by default so consumers that `add_subdirectory()` this SDK stay
lean and never inherit `enable_testing()`.

### Linking

Link your application against:

1. `lancommander` (core library)
2. One HTTP backend: `lancommander_wininet` **or** `lancommander_curl`

```cmake
# Example CMakeLists.txt for a launcher
add_executable(my_launcher main.cpp)
target_link_libraries(my_launcher lancommander lancommander_wininet)
```

## Documentation

- [API Reference](docs/API_REFERENCE.md) — All clients, models, and methods
- [Architecture Guide](docs/ARCHITECTURE.md) — How the library is structured and how to extend it
- [Platform Notes](docs/PLATFORM_NOTES.md) — Win9x, cross-compilation, and backend selection
- [picoposh Gaps](docs/PICOPOSH_GAPS.md) — where the embedded interpreter differs from real PowerShell, with reproductions

## Project Structure

```
liblancommander/
  include/lancommander/
    lancommander.h          # Umbrella header — includes everything
    types.h                 # Result<T>, DownloadProgressFn
    http/                   # HTTP abstraction (IHttpClient, HttpResponse)
    models/                 # Data structs (Game, Tool, Archive, etc.)
    clients/                # API client classes
    archive/                # IArchiveExtractor, ZipArchiveExtractor, Crc32
    script/                 # IScriptRunner, PicoPoshScriptRunner, ScriptHelper, cmdlets
    util/                   # Path helpers (no <filesystem> on our targets)
    manifest_helper.h       # Manifest.yml, shared with the .NET SDK
  src/
    json/                   # cJSON helpers and writers (internal)
    yaml/                   # YAML <-> JSON conversion, on libyaml (internal)
    clients/                # Client implementations
    archive/                # CRC32 + libzip extractor
    script/                 # picoposh runner, script helper
    script/cmdlets/         # The LANCommander cmdlet pack
    util/                   # Path helpers, base64
  backends/
    wininet/                # WinINet HTTP backend (Windows)
    curl/                   # libcurl HTTP backend (cross-platform)
  tests/                    # Self-contained test program (LANCOMMANDER_BUILD_TESTS)
  examples/                 # script_runner_demo (LANCOMMANDER_BUILD_EXAMPLES)
  tools/                    # check-snippets-against-picoposh.sh
  vendor/
    cjson/                  # Vendored cJSON library (checked in)
    libyaml/                # Vendored libyaml, for Manifest.yml (checked in)
    picoposh/               # Vendored picoposh interpreter (git submodule)
```
