# Architecture Guide

## Design Principles

1. **C++14 minimum** — No C++17 features. This enables compilation with older toolchains including Open Watcom for Win9x targets.
2. **No mandatory external dependencies** — cJSON, libyaml and picoposh are vendored and built in-tree; libzip and zlib come with picoposh. HTTP backends are optional link targets.
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
- JSON parsing and serialization (cJSON + helpers)
- YAML ↔ JSON conversion (libyaml), so `Manifest.yml` is shared with the .NET SDK
- CRC32 utility
- Path helpers (`<filesystem>` is unavailable on our targets)
- `PicoPoshScriptRunner`, `ScriptHelper` and `ScriptExecutionClient`
- The LANCommander cmdlet pack, registered into picoposh on request

Everything above the boundary speaks JSON. YAML is converted at the edge rather
than given its own model parsers, so a manifest read from disk goes through the
same `parse_manifest_json` as an API response.

It links the vendored `picoposh` static library — a git submodule at
`vendor/picoposh`, pulled in with `add_subdirectory` and linked as
`picoposh::picoposh`. Upstream gates its `-Werror`, CLI target, CTest and
install rules behind options that default off when it is vendored, so none of
that leaks into a consumer's build.

`Expand-Archive` needs picoposh's own zlib and libzip submodules. Without them
it links a stub and reports cleanly, so a clone that skipped them still
builds — it just cannot unpack archives. `setup-vendor.ps1` initialises them.

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

### The Script Execution Pipeline

Unlike the HTTP and archive layers, script execution is **not** a per-platform
extension point: picoposh is portable C89 and runs the same subset everywhere,
so `PicoPoshScriptRunner` is the only implementation the SDK ships.

The runner is built on picoposh's **session** API. A session is one interpreter
kept alive across several runs, which is what makes the following possible:

```
pico_session_new()
  ├─ pico_session_set_variable(name, value)      host data, never parsed
  ├─ run  "<lancommander-setup>"                 type conversions only
  ├─ run  "<the user's script>"                  verbatim, starts at line 1
  ├─ pico_session_get_variable("Return")         text form
  └─ run  "<lancommander-return>"                ConvertTo-Json into a variable
pico_session_free()
```

Four properties follow, and they are the reason this code is as short as it is:

1. **Nothing is prepended to the user's script**, so error line numbers are its
   own. The setup statements are a separate run against the same session.
2. **Values are data, not text.** `pico_session_set_variable` takes a string and
   never involves the parser, so a value containing quotes, `$(...)`, backticks
   or newlines needs no escaping, and a variable name may contain spaces. There
   is no injection boundary to get wrong.
3. **`$Return` survives `exit`**, because the session outlives the script.
4. **The JSON read-back goes into a variable**, not to stdout, so it cannot
   pollute `ScriptResult::output`.

The only generated script text is the type conversions — `ConvertFrom-Json` for
object variables, `[int]` for integers, `-eq 'true'` for booleans (deliberately
not `[bool]`, which casts any non-empty string to `$true`), and the expression
itself for `of_raw`. Those need a plain `$Identifier`, so a non-string variable
with an unusual name is injected as a string and noted in `ScriptResult::error`.

> This used to be very different. Before picoposh had a session API, variables
> were injected by generating `$Name = '<value>'` script text and `$Return` was
> recovered by appending a nonce-delimited block to stdout and parsing it back
> out. That cost a single-quote-doubling escaper on the security boundary, a
> line-number correction pass, stdout line-buffering to keep the sentinels out
> of the caller's log, and it lost the return value entirely whenever a script
> called `exit`. All of it is gone. `docs/PICOPOSH_GAPS.md` has the history.

Output uses `pico_set_output` rather than `pico_run_capture`, which are mutually
exclusive: `pico_con_out` checks the capture buffer first, so an installed sink
would be silently bypassed. Driving the sink ourselves gives streaming and
capture at once, and avoids `pico_string_free` for output entirely.

picoposh's sink and current directory are process globals and it is documented
as single-threaded, so the runner refuses re-entrant calls and brackets each run
with a save/restore of both — the sink via `pico_get_output`, the directory
because `Set-Location` mutates the real process CWD.

If you need full Windows PowerShell semantics on a modern desktop, implementing
`IScriptRunner` over `pwsh`/`powershell.exe` is a reasonable thing to do — the
interface is deliberately still abstract.

### Adding a Cmdlet

Cmdlets live in `src/script/cmdlets/`, grouped by what they do, and are
registered with picoposh through `pico_register_cmdlet`. Adding one touches
exactly two files: the group's `.cpp`, and the table in `cmdlets.cpp`.

```cpp
// src/script/cmdlets/cmdlets_mine.cpp
#include "script/cmdlets/cmdlet_defs.h"

namespace { // definitions must be static const — picoposh borrows, not copies

pico_status get_thing_begin(PicoStage* ctx)
{
    std::string name;
    if (!arg_string(ctx, "Name", 0, &name))
        return fail(ctx, PICO_ERR_ARG, "requires a -Name");

    return emit_string(ctx, name);
}

const PicoParamDef params[] = { { "Name", 0, 0 } };   // {name, is_switch, position}

const PicoCmdletDef get_thing_def = {
    "Get-Thing", params, 1, get_thing_begin, NULL, NULL, "Does a thing."
};

}

const PicoCmdletDef* cmdlet_get_thing() { return &get_thing_def; }
```

Three things to know before writing one:

1. **`cmdlet_support.h` wraps picoposh's internal headers in `extern "C"`.**
   Only `picoposh.h` has its own guards, so including `pico_cmd.h` and friends
   from C++ directly would give them C++ linkage and fail to link.
2. **A definition carries no user data.** The callbacks receive only the
   `PicoStage`, and the definition is static, so anything beyond the cmdlet's
   own parameters has to come from process-global state. This is what currently
   blocks the cmdlets needing an HTTP client (see API_REFERENCE.md).
3. **`begin` runs once before input, `process` once per pipeline item.** A
   source cmdlet emits from `begin`; one that transforms piped input implements
   `process`. `Get-SanitizedPath` does both, so it works either way.

Registration is process-global and opt-in — `cmdlets::register_all()` — because
a host embedding this SDK may want the plain picoposh language and nothing else.

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
