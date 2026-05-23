# Architecture Guide

## Design Principles

1. **C++14 minimum** — No C++17 features. This enables compilation with older toolchains including Open Watcom for Win9x targets.
2. **No mandatory external dependencies** — cJSON is vendored. HTTP backends are optional link targets.
3. **Backend abstraction** — HTTP, archive extraction, and script execution are all behind abstract interfaces. Consumers provide platform-specific implementations.
4. **Value types** — Models are plain structs. No inheritance hierarchies, no virtual methods, no allocator magic. They copy and move naturally.
5. **Synchronous API** — All methods are blocking. Async behavior is the caller's responsibility (threads, event loops, etc.). This avoids platform-specific async primitives.

## Library Layers

```
┌─────────────────────────────────────────────┐
│              Your Application                │
├─────────────────────────────────────────────┤
│          Clients (GameClient, etc.)          │
│          ConnectionClient (stateful)         │
│          Script Execution                    │
│          Archive Extraction                  │
├─────────────────────────────────────────────┤
│          JSON Parsing (internal)             │
├─────────────────────────────────────────────┤
│          IHttpClient (abstract)              │
├──────────────────┬──────────────────────────┤
│  WinInetHttpClient │   CurlHttpClient       │
│   (Windows 95+)    │   (any platform)       │
└──────────────────┴──────────────────────────┘
```

### Core Library (`lancommander`)

The static library contains:
- All client implementations
- JSON parsing (cJSON + helpers)
- CRC32 utility
- BatchScriptRunner

It does **not** contain any HTTP backend. You must link one separately.

### HTTP Backends

Each backend is a separate static library:

- **`lancommander_wininet`** — Uses the WinINet API (`wininet.dll`). Available on every Windows version since 95 with Internet Explorer installed. No external downloads needed.
- **`lancommander_curl`** — Uses libcurl. Works everywhere curl works. Only built if CMake finds curl on the system.

### JSON Layer

JSON parsing uses [cJSON](https://github.com/DaveGamble/cJSON), a single-file C library vendored in `vendor/cjson/`. The parsing helpers in `src/json/` are internal to the library and not part of the public API.

The helpers support dual-case field lookup (camelCase and PascalCase) because the LANCommander server may serialize fields in either convention depending on the endpoint.

## Extending the Library

### Adding a New HTTP Backend

1. Create a class that inherits from `IHttpClient`
2. Implement all virtual methods
3. Optionally add it to CMakeLists.txt as a separate static library target

```cpp
#include <lancommander/http/http_client.h>

class MyHttpClient : public lancommander::IHttpClient {
public:
    void set_base_url(const std::string& url) override { /* ... */ }
    void set_bearer_token(const std::string& token) override { /* ... */ }
    HttpResponse get(const std::string& path) override { /* ... */ }
    HttpResponse post(const std::string& path, const std::string& body,
                      const std::string& content_type) override { /* ... */ }
    HttpResponse put(const std::string& path, const std::string& body,
                     const std::string& content_type) override { /* ... */ }
    HttpResponse del(const std::string& path) override { /* ... */ }
    bool download(const std::string& path, const std::string& dest_path,
                  DownloadProgressFn progress) override { /* ... */ }
    HttpResponse post_multipart_file(const std::string& path,
                                     const std::string& field_name,
                                     const std::string& file_path) override { /* ... */ }
};
```

### Adding a New Archive Extractor

Implement `IArchiveExtractor`. A minizip-based example:

```cpp
#include <lancommander/archive/archive_extractor.h>
#include <lancommander/archive/crc32_util.h>

class MinizipExtractor : public lancommander::IArchiveExtractor {
public:
    lancommander::ExtractionResult extract(
        const std::string& archive_path,
        const std::string& dest_directory,
        bool skip_existing_matching_crc,
        lancommander::ExtractionProgressFn progress) override
    {
        // 1. Open the zip with minizip's unzOpen
        // 2. Iterate entries with unzGoToNextFile
        // 3. For each entry:
        //    a. If skip_existing_matching_crc, compute CRC32 of the local file
        //       using Crc32::file_crc32() and compare to the entry's CRC
        //    b. If CRCs match, skip extraction
        //    c. Otherwise, extract to dest_directory
        // 4. Call progress callback periodically
        // 5. Return ExtractionResult
    }

    std::vector<lancommander::ArchiveEntry> list(
        const std::string& archive_path) override
    {
        // Open and enumerate without extracting
    }
};
```

### Adding a New Script Runner

Implement `IScriptRunner` for your platform:

```cpp
#include <lancommander/script/script_runner.h>

class ShellScriptRunner : public lancommander::IScriptRunner {
public:
    lancommander::ScriptResult run_file(
        const std::string& script_path,
        const std::string& working_directory,
        const std::map<std::string, std::string>& variables) override
    {
        // 1. Fork/exec /bin/sh with the script
        // 2. Set environment variables from the map
        // 3. Capture stdout/stderr
        // 4. Return ScriptResult with exit code
    }

    lancommander::ScriptResult run_inline(
        const std::string& script_contents,
        const std::string& working_directory,
        const std::map<std::string, std::string>& variables) override
    {
        // Write to temp file, run_file(), delete temp file
    }
};
```

### Adding a New API Client

Follow the existing pattern:

1. **Model header** in `include/lancommander/models/` — plain struct
2. **Client header** in `include/lancommander/clients/` — class with `IHttpClient&` member
3. **Client implementation** in `src/clients/` — uses `json::JsonDoc` and `json::parse_*` helpers
4. **JSON parser** in `src/json/json_helpers.h/.cpp` — add `parse_your_model()` function
5. **CMakeLists.txt** — add the `.cpp` to the source list
6. **Umbrella header** — add includes to `lancommander.h`

Template:

```cpp
// include/lancommander/clients/foo_client.h
class FooClient {
public:
    explicit FooClient(IHttpClient& http);
    Result<Foo> get(const std::string& id);
private:
    IHttpClient& m_http;
};

// src/clients/foo_client.cpp
#include "lancommander/clients/foo_client.h"
#include "../json/json_helpers.h"
#include <sstream>

namespace lancommander {

FooClient::FooClient(IHttpClient& http) : m_http(http) {}

Result<Foo> FooClient::get(const std::string& id)
{
    HttpResponse resp = m_http.get("/api/Foos/" + id);
    if (!resp.ok()) {
        std::ostringstream e;
        e << "GetFoo failed (HTTP " << resp.status_code << ")";
        return Result<Foo>::fail(e.str());
    }

    json::JsonDoc doc(resp.body);
    if (!doc) return Result<Foo>::fail("Invalid JSON response");

    Foo f = json::parse_foo(doc.root);
    return Result<Foo>::ok(std::move(f));
}

} // namespace lancommander
```

## Error Handling

The library does not use exceptions. All fallible operations return `Result<T>`. This is intentional for:
- Compatibility with compilers/runtimes that don't support exceptions (e.g. some Win9x toolchains)
- Predictable control flow
- Lightweight builds with `-fno-exceptions`

Internal errors (JSON parse failures, HTTP errors) are captured in `Result::error` as human-readable strings that include the HTTP status code where applicable.

## Thread Safety

The library is **not** thread-safe by default. Each `IHttpClient` instance and each client should be used from a single thread. If you need concurrent access, either:
- Create separate `IHttpClient` + client instances per thread
- Add your own synchronization around client calls
