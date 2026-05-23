# LANCommander C++ SDK

A cross-platform C++14 client library for the LANCommander game server API. Designed for maximum compatibility from Windows 95 through modern desktop and embedded platforms.

## Features

- **16 API clients** covering authentication, games, library, saves, media, tools, depot, and more
- **HTTP backend abstraction** — ship with WinINet (Windows) or libcurl (cross-platform), or write your own
- **Archive extraction interface** with CRC32 skip-unchanged-files support
- **Batch script execution** with environment variable injection
- **Connection lifecycle management** with ping/pong validation and offline mode
- **Zero mandatory external dependencies** beyond a C++14 compiler (cJSON is vendored)

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
- C++14 compiler
- **Windows**: MSVC, MinGW, or Open Watcom (for Win9x targets)
- **Linux/macOS**: GCC 5+ or Clang 3.4+

### Build Steps

```bash
mkdir build && cd build
cmake ..
cmake --build .
```

This produces the static library `lancommander` and, on Windows, the `lancommander_wininet` backend. If libcurl is found, `lancommander_curl` is also built.

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

## Project Structure

```
LANCommander.SDK.Cpp/
  include/lancommander/
    lancommander.h          # Umbrella header — includes everything
    types.h                 # Result<T>, DownloadProgressFn
    http/                   # HTTP abstraction (IHttpClient, HttpResponse)
    models/                 # Data structs (Game, Tool, Archive, etc.)
    clients/                # API client classes
    archive/                # IArchiveExtractor, Crc32
    script/                 # IScriptRunner, BatchScriptRunner
  src/
    json/                   # cJSON helpers (internal)
    clients/                # Client implementations
    archive/                # CRC32 implementation
    script/                 # BatchScriptRunner implementation
  backends/
    wininet/                # WinINet HTTP backend (Windows)
    curl/                   # libcurl HTTP backend (cross-platform)
  vendor/
    cjson/                  # Vendored cJSON library
```
