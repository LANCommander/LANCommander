# API Reference

All types live in the `lancommander` namespace. Include `<lancommander/lancommander.h>` for everything, or include individual headers as needed.

---

## Core Types

### `Result<T>`

Every client method returns `Result<T>`. Check success with `operator bool()` or the `.success` field.

```cpp
auto result = games.get_all();

if (result) {
    // result.value contains the data
    for (auto& g : result.value)
        printf("%s\n", g.title.c_str());
} else {
    // result.error contains a human-readable message
    printf("Error: %s\n", result.error.c_str());
}
```

Static factories:
- `Result<T>::ok(T value)` — create a success result
- `Result<T>::fail(std::string error)` — create a failure result

### `DownloadProgressFn`

```cpp
using DownloadProgressFn = std::function<bool(uint64_t received, uint64_t total)>;
```

Callback for download progress. Return `false` to abort the download.

```cpp
games.download(game_id, "game.zip", [](uint64_t recv, uint64_t total) -> bool {
    printf("\r%llu / %llu bytes", recv, total);
    return true;  // return false to cancel
});
```

---

## HTTP Layer

### `IHttpClient` (abstract)

All clients take a reference to an `IHttpClient`. You must provide a concrete backend.

```cpp
class IHttpClient {
public:
    virtual void set_base_url(const std::string& url) = 0;
    virtual void set_bearer_token(const std::string& token) = 0;

    virtual HttpResponse get(const std::string& path) = 0;
    virtual HttpResponse post(const std::string& path,
                              const std::string& body,
                              const std::string& content_type = "application/json") = 0;
    virtual HttpResponse put(const std::string& path,
                             const std::string& body,
                             const std::string& content_type = "application/json") = 0;
    virtual HttpResponse del(const std::string& path) = 0;

    virtual bool download(const std::string& path,
                          const std::string& dest_path,
                          DownloadProgressFn progress = nullptr) = 0;

    virtual HttpResponse post_multipart_file(const std::string& path,
                                             const std::string& field_name,
                                             const std::string& file_path) = 0;
};
```

### `HttpResponse`

```cpp
struct HttpResponse {
    int status_code;
    std::string body;
    std::map<std::string, std::string> headers;

    bool ok() const;  // true if status_code is 2xx
};
```

### Built-in Backends

| Class | Header | Platform | Dependency |
|-------|--------|----------|------------|
| `WinInetHttpClient` | `wininet_http_client.h` | Windows (95+) | wininet.lib |
| `CurlHttpClient` | `curl_http_client.h` | Any | libcurl |

---

## Clients

All clients are constructed with an `IHttpClient&` reference (except `BeaconClient` which uses UDP directly). Call `set_base_url()` and `set_bearer_token()` on the HTTP client before using authenticated endpoints.

One more lives with the script layer rather than here: [`ScriptExecutionClient`](#scriptexecutionclient) takes an `IScriptRunner&` instead of an `IHttpClient&`, because it runs lifecycle scripts rather than calling the API.

### AuthenticationClient

```cpp
AuthenticationClient auth(http);
```

| Method | Returns | Description |
|--------|---------|-------------|
| `login(username, password)` | `Result<AuthToken>` | Authenticate and receive tokens |
| `validate()` | `Result<bool>` | Check if the current token is valid |
| `refresh(current_token)` | `Result<AuthToken>` | Refresh an expired token |
| `logout()` | `void` | Invalidate the current session |
| `get_providers()` | `Result<vector<AuthenticationProvider>>` | List available auth providers |

**AuthToken fields**: `access_token`, `refresh_token`, `expiration`

### ConnectionClient

Manages the connection lifecycle to a LANCommander server. Unlike other clients, this one maintains internal state.

```cpp
ConnectionClient conn(http);

conn.set_server_address("http://192.168.1.100:1337");
conn.set_access_token(token.value.access_token);

if (conn.ping()) {
    conn.connect();
}
```

| Method | Returns | Description |
|--------|---------|-------------|
| `is_connected()` | `bool` | Whether the client is in connected state |
| `is_configured()` | `bool` | Whether address and token are both set |
| `is_offline_mode()` | `bool` | Whether offline mode is enabled |
| `has_server_address()` | `bool` | Whether a server address is set |
| `get_server_address()` | `std::string` | The current server address |
| `get_access_token()` | `std::string` | The current bearer token |
| `update_server_address(address)` | `Result<bool>` | Validate via ping, then set the address |
| `set_server_address(address)` | `void` | Set address without validation |
| `set_access_token(token)` | `void` | Set the bearer token |
| `connect()` | `Result<bool>` | Mark as connected (requires configured state) |
| `disconnect()` | `Result<bool>` | Mark as disconnected |
| `enable_offline_mode()` | `void` | Disconnect and enable offline mode |
| `disable_offline_mode()` | `void` | Disable offline mode |
| `ping(address?)` | `Result<bool>` | Ping the server with X-Ping/X-Pong validation |

**Event callbacks** (C function pointers with `void* user_data`):

```cpp
conn.set_on_connect(my_connect_handler, my_data);
conn.set_on_disconnect(my_disconnect_handler, my_data);
conn.set_on_server_address_changed(my_addr_handler, my_data);
conn.set_on_offline_mode_enabled(my_offline_handler, my_data);
```

### GameClient

```cpp
GameClient games(http);
```

| Method | Returns | Description |
|--------|---------|-------------|
| `get_all()` | `Result<vector<Game>>` | Fetch all games |
| `get(game_id)` | `Result<Game>` | Fetch a single game |
| `get_manifest(game_id)` | `Result<GameManifest>` | Fetch the install manifest |
| `get_manifest_json(game_id)` | `Result<std::string>` | The raw manifest body, preserving fields `GameManifest` does not model. What `$GameManifest` wants |
| `get_actions(game_id)` | `Result<vector<Action>>` | Get game actions (launch configs) |
| `get_addons(game_id)` | `Result<vector<Game>>` | Get expansions/mods for a game |
| `get_redistributables(game_id)` | `Result<vector<Redistributable>>` | Get required redistributables |
| `check_for_update(game_id, version)` | `Result<bool>` | Check if an update is available |
| `download(game_id, dest, progress?)` | `Result<bool>` | Download the game archive |
| `notify_started(game_id)` | `void` | Notify the server a game was launched |
| `notify_stopped(game_id)` | `void` | Notify the server a game was closed |

Also exposes a free function:
```cpp
bool parse_manifest_json(const std::string& json, GameManifest* out, std::string* error);
```

### LibraryClient

```cpp
LibraryClient library(http);
```

| Method | Returns | Description |
|--------|---------|-------------|
| `get()` | `Result<vector<EntityReference>>` | Get the user's library |
| `add(game_id)` | `Result<bool>` | Add a game to the library |
| `remove(game_id)` | `Result<bool>` | Remove a game from the library |

### DepotClient

```cpp
DepotClient depot(http);
```

| Method | Returns | Description |
|--------|---------|-------------|
| `get()` | `Result<DepotResults>` | Fetch the full depot (games, genres, tags, etc.) |
| `get_game(game_id)` | `Result<DepotGame>` | Fetch a single depot game with full metadata |

`DepotResults` contains: `games`, `collections`, `companies`, `engines`, `genres`, `platforms`, `tags`, `popular`, `backlog`.

### MediaClient

```cpp
MediaClient media(http);
```

| Method | Returns | Description |
|--------|---------|-------------|
| `download_thumbnail(media_id, dest)` | `Result<bool>` | Download a thumbnail |
| `download(media_id, dest, progress?)` | `Result<bool>` | Download full media |
| `get_for_game(game_id)` | `Result<vector<MediaRef>>` | List media for a game |

### SaveClient

```cpp
SaveClient saves(http);
```

| Method | Returns | Description |
|--------|---------|-------------|
| `get(game_id)` | `Result<vector<GameSave>>` | List all saves for a game |
| `get_latest(game_id)` | `Result<GameSave>` | Get the most recent save |
| `download_latest(game_id, dest, progress?)` | `Result<bool>` | Download the latest save |
| `upload(game_id, zip_path)` | `Result<bool>` | Upload a save archive |

### KeyClient

Requires an `IMachineInfo` implementation for machine identification during key allocation.

```cpp
class MyMachineInfo : public lancommander::IMachineInfo {
public:
    std::string get_computer_name() override { return "MY-PC"; }
    std::string get_ip_address() override { return "192.168.1.50"; }
    std::string get_mac_address() override { return "AA:BB:CC:DD:EE:FF"; }
};

MyMachineInfo machine;
KeyClient keys(http, machine);
```

| Method | Returns | Description |
|--------|---------|-------------|
| `get_allocated(game_id)` | `Result<std::string>` | Get the key allocated to this machine |
| `allocate(game_id)` | `Result<std::string>` | Allocate a new key |

### ProfileClient

```cpp
ProfileClient profile(http);
```

| Method | Returns | Description |
|--------|---------|-------------|
| `get()` | `Result<User>` | Get the current user profile |
| `get_alias()` | `Result<std::string>` | Get the user's display alias |
| `change_alias(alias)` | `Result<bool>` | Change the display alias |
| `get_custom_field(name)` | `Result<std::string>` | Read a per-user custom field. A field that was never set returns empty rather than failing |
| `update_custom_field(name, value)` | `Result<std::string>` | Set a per-user custom field |
| `download_avatar(dest)` | `Result<bool>` | Download the user's avatar |

### ScriptClient

Fetches script contents from the server (does not execute them — see [Script Execution](#script-execution)).

```cpp
ScriptClient scripts(http);
```

| Method | Returns | Description |
|--------|---------|-------------|
| `get_game_scripts(game_id)` | `Result<vector<Script>>` | Get scripts for a game |
| `get_redistributable_scripts(redist_id)` | `Result<vector<Script>>` | Get scripts for a redistributable |

### ToolClient

```cpp
ToolClient tools(http);
```

| Method | Returns | Description |
|--------|---------|-------------|
| `get(tool_id)` | `Result<Tool>` | Fetch tool details (with archives and scripts) |
| `get_scripts(tool_id)` | `Result<vector<Script>>` | Fetch tool scripts |
| `download(tool_id, dest, progress?)` | `Result<bool>` | Download the tool archive |

### LauncherClient

```cpp
LauncherClient launcher(http);
```

| Method | Returns | Description |
|--------|---------|-------------|
| `check_for_update()` | `Result<CheckForUpdateResponse>` | Check for launcher updates |
| `download(dest, progress?)` | `Result<bool>` | Download the launcher update |

**CheckForUpdateResponse fields**: `update_available`, `version`, `download_url`

### RedistributableClient

```cpp
RedistributableClient redist(http);
```

| Method | Returns | Description |
|--------|---------|-------------|
| `download(redist_id, dest, progress?)` | `Result<bool>` | Download a redistributable |

### PlaySessionClient

```cpp
PlaySessionClient sessions(http);
```

| Method | Returns | Description |
|--------|---------|-------------|
| `get()` | `Result<vector<EntityReference>>` | List all play sessions |
| `get_for_game(game_id)` | `Result<vector<PlaySession>>` | Get play sessions for a game |

**PlaySession fields**: `id`, `start`, `end`, `game_id`, `user_id`

### IssueClient

```cpp
IssueClient issues(http);
```

| Method | Returns | Description |
|--------|---------|-------------|
| `open(description, game_id)` | `Result<bool>` | Report an issue for a game |

### BeaconClient

Discovers LANCommander servers on the local network via UDP broadcast. Does not require an HTTP client.

```cpp
BeaconClient beacon;
auto servers = beacon.discover(3000);  // 3 second timeout

if (servers) {
    for (auto& s : servers.value)
        printf("Found: %s at %s\n", s.name.c_str(), s.address.c_str());
}
```

| Method | Returns | Description |
|--------|---------|-------------|
| `discover(timeout_ms, port?)` | `Result<vector<DiscoveredServer>>` | Find servers via UDP broadcast |

Default port: `35891`

---

## Path Utilities

`<filesystem>` is unavailable here — the SDK is C++14 and targets MinGW32, Open
Watcom and VC6 alongside modern toolchains — so these fill the gap. Windows uses
only ANSI APIs present on Windows 95; everything else uses POSIX.

```cpp
#include <lancommander/util/path.h>
```

| Function | Returns | Notes |
|---|---|---|
| `separator()` | `char` | `'\'` on Windows, `'/'` elsewhere |
| `combine(a, b)` | `std::string` | Joins with exactly one separator. An absolute `b` replaces `a`. Separators already in an argument are left alone, so `"C:/Games"` stays as written |
| `parent(p)` / `base_name(p)` | `std::string` | Either side of the final separator |
| `exists(p)` / `is_directory(p)` | `bool` | |
| `create_directories(p)` | `Result<bool>` | Recursive; succeeds when it already exists |
| `list_directory(p)` | `Result<vector<DirectoryEntry>>` | Immediate children, excluding `.` and `..`. Not recursive |
| `remove_file(p)` | `Result<bool>` | Succeeds when the file was already absent |
| `temp_directory()` | `std::string` | No trailing separator |
| `create_temp_file(prefix, suffix)` | `Result<std::string>` | Creates an empty file and returns its path. The caller owns deletion |
| `get_current_directory()` / `set_current_directory(p)` | `std::string` / `bool` | |

`DirectoryEntry` is `{ std::string name; bool is_directory; }` — `name` is the
leaf, not a full path.

---

## Archive Extraction

### `ZipArchiveExtractor`

The shipped implementation, built on the same libzip picoposh vendors — one zip implementation in the process rather than two.

```cpp
#include <lancommander/archive/zip_archive_extractor.h>

if (!lancommander::ZipArchiveExtractor::available()) {
    // picoposh was built without its zlib/libzip submodules; run
    // setup-vendor.ps1. Both methods report cleanly in this state.
}

lancommander::ZipArchiveExtractor extractor;
auto result = extractor.extract(archive_path, install_directory);
```

Every archive LANCommander serves is a plain zip — the server builds them with `System.IO.Compression.ZipArchive` throughout. The .NET SDK extracts via SharpCompress, which also reads rar/7z/tar, but nothing in LANCommander produces those.

**Entries that escape the destination are refused, not sanitised.** An absolute path, a drive-qualified path, or any `..` component fails the whole extraction, because an archive is untrusted input.

`skip_existing_matching_crc` (default true) compares each entry's CRC against the file already on disk and skips the ones that match, which is what makes reinstalling over an existing directory cheap. Skipped files do not appear in `extracted_files`.

Returning `false` from the progress callback stops the extraction and reports `canceled`, distinct from `success == false`.

### `IArchiveExtractor` (abstract)

The interface, should you want a different backend.

```cpp
class IArchiveExtractor {
public:
    virtual ExtractionResult extract(
        const std::string& archive_path,
        const std::string& dest_directory,
        bool skip_existing_matching_crc = true,
        ExtractionProgressFn progress = ExtractionProgressFn()) = 0;

    virtual std::vector<ArchiveEntry> list(const std::string& archive_path) = 0;
};
```

**ExtractionProgressFn**:
```cpp
using ExtractionProgressFn = std::function<bool(
    int entries_done, int entries_total,
    long long bytes_done, long long bytes_total)>;
// Return false to cancel extraction.
```

**ExtractionResult fields**: `success`, `canceled`, `directory`, `error`, `extracted_files`

**ArchiveEntry fields**: `path`, `is_directory`, `crc32`, `compressed_size`, `uncompressed_size`

### `Crc32`

Self-contained CRC32 utility with no external dependencies.

```cpp
// Incremental usage
lancommander::Crc32 crc;
crc.update(data, length);
crc.update(more_data, more_length);
unsigned long checksum = crc.value();

// One-shot file checksum
unsigned long file_checksum = lancommander::Crc32::file_crc32("path/to/file");
```

---

## Script Execution

Scripts are PowerShell (`.ps1`), executed by the embedded [picoposh](https://github.com/LANCommander/picoposh) interpreter — a C89 PowerShell-subset implementation vendored at `vendor/picoposh`. There is no external shell and no process spawn, so the same code path works on Windows 95, modern Windows, Linux and macOS.

Read [picoposh limitations](#picoposh-limitations) before writing a script the C++ SDK will run. The subset is real, and a few differences from the .NET SDK will silently change a script's behaviour.

### `ScriptVariable` and `ScriptVariableList`

Variables become PowerShell *variables*, not environment variables — picoposh has no `$env:` provider at all, and the .NET SDK injects typed objects that an environment block could not carry.

```cpp
enum class ScriptValueKind { String, Int, Bool, Json, Raw };

ScriptVariable::of_string("InstallDirectory", "C:\\Games\\Quake3");
ScriptVariable::of_int("Port", 27960);
ScriptVariable::of_bool("FirstRun", true);
ScriptVariable::of_json("GameManifest", manifest_json);   // -> ConvertFrom-Json
ScriptVariable::of_raw("Computed", "$a + $b");            // verbatim expression
```

`ScriptVariableList` is a `std::vector`, not a map, and the order matters: later entries overwrite earlier ones. That is how manifest custom fields shadow the well-known variables, and how RunWrapper overrides `$WorkingDirectory` — both matching the .NET SDK.

### `IScriptRunner` (abstract)

```cpp
class IScriptRunner {
public:
    virtual ScriptResult run_file(
        const std::string& script_path,
        const std::string& working_directory,
        const ScriptVariableList& variables) = 0;

    virtual ScriptResult run_inline(
        const std::string& script_contents,
        const std::string& script_name,       // appears in error messages
        const std::string& working_directory,
        const ScriptVariableList& variables) = 0;

    virtual void set_output_callback(ScriptOutputFn fn) = 0;
};
```

**`ScriptResult` fields**

| Field | Meaning |
|---|---|
| `success` | Ran to completion with exit code 0 |
| `exit_code` | The script's `exit <n>`, or a `PICO_EXIT_*` code (1 runtime, 2 parse, 3 unsupported, 4 I/O) |
| `output` | stdout, with the return-value block stripped out |
| `error` | stderr, with line numbers corrected to refer to the script rather than the injected preamble |
| `raw_error` | stderr exactly as the interpreter emitted it |
| `has_return_value` | `$Return` was set to something other than `$null` |
| `return_value` | Text form of `$Return`. An object renders as the opaque `PicoObject` — prefer `return_json` for anything non-scalar |
| `return_json` | `ConvertTo-Json` of `$Return`. **Authoritative** |
| `preamble_lines` | Lines injected ahead of the script's first line |

### `PicoPoshScriptRunner`

```cpp
lancommander::PicoPoshScriptRunner runner;

lancommander::ScriptVariableList vars;
vars.push_back(lancommander::ScriptVariable::of_string("InstallDirectory", "C:\\Games\\Quake3"));
vars.push_back(lancommander::ScriptVariable::of_string("PlayerAlias", "Player1"));
vars.push_back(lancommander::ScriptVariable::of_json("GameManifest", manifest_json));

auto result = runner.run_file(
    "C:\\Games\\Quake3\\.lancommander\\<game-id>\\Install.ps1",
    "C:\\Games\\Quake3",
    vars);

if (result.success)
    printf("%s\n", result.output.c_str());
else
    printf("failed (exit %d): %s\n", result.exit_code, result.error.c_str());
```

`set_output_callback` taps stdout and stderr live as the script runs, receiving exactly the bytes the script wrote. `set_debug(true)` echoes the injected variable names first, mirroring the .NET SDK's `EnableScriptDebugging`. `set_output_limit(n)` caps how much stdout is retained in `output` (default 4 MiB); output past the cap still reaches the callback. `set_step_limit(n)` bounds the script at `n` statements so a runaway loop cannot hang the calling thread — unlimited by default, and the .NET SDK's 10-second `DetectInstall` timeout is the closest equivalent.

**Process-global state.** picoposh keeps its output sink and current directory in process globals and is single-threaded. Only one script may run at a time — a re-entrant call fails immediately rather than corrupting interpreter state. The runner saves and restores both the sink (via `pico_get_output`) and the process working directory around every run, because `Set-Location` inside a script mutates the real CWD and picoposh never puts it back.

### How a script is executed

Your script is handed to picoposh verbatim — nothing is prepended or appended.
Variables cross the boundary through picoposh's session API as data, so they
never pass through the parser: a value containing quotes, `$(...)`, backticks
or newlines is just a value, and a variable name may contain spaces.

The one thing the runner generates is a conversion for the non-string kinds:

```powershell
$GameManifest = ConvertFrom-Json $GameManifest   # of_json
$Port = [int]$Port                               # of_int
$Enabled = ($Enabled -eq 'true')                 # of_bool — not [bool], which
                                                 # casts any non-empty string
                                                 # to $true
$Computed = 2 + 3                                # of_raw
```

That runs as its *own* script in the session before yours. Session state
persists between runs, so your script still starts at line 1 and the line
numbers in `error` are its own.

`$Return` is read back out of the session after the run, which is why it
survives a script ending in `exit` — the session outlives the script. It is
read twice: once as text (`return_value`) and once via `ConvertTo-Json` into a
variable rather than to stdout (`return_json`), so nothing leaks into `output`.
A `return_json` of `"null"` is how "no value" is distinguished from a value.

The working directory is the process CWD, which `Get-Location` reports. The
runner sets it before the run and restores it after, because `Set-Location`
inside a script mutates the real CWD and picoposh does not put it back.

### `ScriptHelper` — `lancommander::script`

The on-disk layout, shared with the .NET SDK:

```
<install directory>/.lancommander/<entity id>/Install.ps1
```

```cpp
namespace lancommander { namespace script {

std::string script_file_name(ScriptType type);          // "Install.ps1", … or "" 
ScriptType  script_type_from_file_name(const std::string& file_name);
std::string metadata_directory_path(const std::string& install_directory,
                                    const std::string& entity_id);
std::string script_file_path(const std::string& install_directory,
                             const std::string& entity_id, ScriptType type);
std::string manifest_file_path(const std::string& install_directory,
                               const std::string& entity_id);
std::string script_contents(const Script& script);

Result<bool>        save_script(const std::string& install_directory,
                                const std::string& entity_id, const Script& script);
Result<bool>        save_scripts(const std::string& install_directory,
                                 const std::string& entity_id,
                                 const std::vector<Script>& scripts);
Result<std::string> save_temp_script(const std::string& contents);

RuntimePlatform current_runtime_platform();
bool supports_current_runtime(int platforms);
bool supports_current_runtime(const std::vector<Script>& scripts, ScriptType type);

bool            result_to_int(const ScriptResult& result, int* out);
bool            result_to_bool(const ScriptResult& result);
Result<Package> result_to_package(const ScriptResult& result);

}}
```

Nine of the sixteen `ScriptType` values are file-backed: `Install.ps1`, `Uninstall.ps1`, `ChangeName.ps1`, `ChangeKey.ps1`, `DetectInstall.ps1`, `BeforeStart.ps1`, `AfterStop.ps1`, `Package.ps1`, `RunWrapper.ps1`. `script_file_name` returns `""` for the other seven, which only ever exist inline.

`supports_current_runtime` mirrors `EnvironmentHelper.SupportsCurrentRuntime`, including its permissive treatment of `None`: an unspecified platform set means "runs everywhere", for backwards compatibility with scripts predating the field.

`result_to_bool` deliberately does **not** fall back to "exit code was 0". "The script ran cleanly" and "the redistributable is installed" are different facts, and conflating them would make every well-formed `DetectInstall` report "installed".

`Manifest.yml` lives in the same directory, and is the same file the .NET SDK's `ManifestHelper` writes — see [`manifest_helper.h`](../include/lancommander/manifest_helper.h) to read or write it. An install directory is fully shared between the two launchers.

### `ScriptExecutionClient`

Ports the .NET `ScriptClient.{Games,Redistributables,Tools}` lifecycle methods. It is separate from `ScriptClient` (which fetches scripts over HTTP and needs no runner) so the runner's single-instance constraint stays visible in the type that requires it.

```cpp
lancommander::PicoPoshScriptRunner runner;
lancommander::ScriptExecutionClient scripts(runner);

lancommander::ScriptContext ctx;
ctx.install_directory   = "C:\\Games\\Quake3";
ctx.game_id             = game.id;
ctx.server_address      = "http://192.168.1.100:1337";
ctx.game_manifest_json  = manifest_json;   // prefer GameClient::get_manifest_json
ctx.custom_fields       = manifest.custom_fields;
ctx.scripts             = manifest.scripts;   // metadata, for runtime gating

auto run = scripts.game_run_install(ctx);
if (!run)
    printf("install script failed: %s\n", run.error.c_str());
else if (!run.value.ran)
    printf("no install script for this game/platform\n");
else
    printf("install script returned %d\n", run.value.value);
```

`ScriptRun` distinguishes the three outcomes a bare `int` cannot: `ran == false` (no script file, or gated out by platform), `skipped_runtime == true` (the file existed but this platform is excluded), and a genuine result in `value` / `bool_value` / `result`.

A missing or gated script is `ok`, not a failure. A script that runs and exits non-zero is also `ok` — the exit code is the result, matching .NET. Only a runner that could not start, or a script that could not be parsed, produces `fail`.

**Variables and working directories, per lifecycle.** Every method injects `$ScriptType`, `$WorkingDirectory`, `$InstallDirectory`, `$DefaultInstallDirectory` and `$ServerAddress` first, then its own, then the game's custom fields last.

| Method | Additional variables | Working directory |
|---|---|---|
| `game_run_install` / `game_run_uninstall` | `$GameManifest` | install directory |
| `game_run_before_start` / `game_run_after_stop` | `$GameManifest`, `$PlayerAlias` | install directory |
| `game_run_name_change` | `$GameManifest`, `$OldPlayerAlias`, `$NewPlayerAlias` | install directory |
| `game_run_key_change` | `$GameManifest`, `$AllocatedKey` | install directory |
| `redistributable_run_detect_install` | `$GameManifest`, `$RedistributableManifest` | `.lancommander/<redist id>` |
| `redistributable_run_install` / `_uninstall` / `_before_start` / `_after_stop` / `_name_change` | as above, plus `$PlayerAlias` or `$Old`/`$NewPlayerAlias` | `.lancommander/<redist id>/Files` |
| `redistributable_run_run_wrapper` | as above, plus `$ExecutablePath`, `$Arguments`, and an overriding `$WorkingDirectory` | `.lancommander/<redist id>` |
| `tool_run_detect_install` | `$GameManifest`, `$ToolManifest` | `.lancommander/<tool id>` |
| `tool_run_install` / `_uninstall` / `_before_start` / `_after_stop` | `$ToolManifest` only — no `$GameManifest`, no custom fields | `.lancommander/<tool id>/Files` |
| `run_package` | `$Entity`, `$LatestArchivePath` | none (inline) |

`$ScriptType` is injected as a string, so `$ScriptType -eq 'Install'` works but `$ScriptType.ToString()` does not.

Not ported, deliberately: `IScriptInterceptor` and `IScriptDebugger` (the C++ SDK has no plugin host), the 10-second `DetectInstall` timeout, and RunWrapper's kill-on-cancel. picoposh is synchronous and in-process, so there is no thread to cancel; bound a script with `PicoPoshScriptRunner::set_step_limit` instead. `Start-Process` does exist now, so a wrapper script can spawn a child — but the runner does not track or kill it.

---

## picoposh limitations

picoposh implements a deliberate subset of PowerShell. Anything outside it
produces a clear "not supported" error rather than misbehaving.

[**PICOPOSH_GAPS.md**](PICOPOSH_GAPS.md) tracks the subset against what the
.NET SDK's runspace provides, with reproduction commands. The summary below is
what a script author needs.

`tools/check-snippets-against-picoposh.sh` runs every snippet LANCommander
ships to script authors through the interpreter; all twelve currently pass.

### Differences from the .NET SDK

| Difference | Consequence |
|---|---|
| **No "last pipeline value" return** | `PowerShellScript.ExecuteAsync` reads `$Return`, then falls back to the last pipeline result. Here, top-level pipeline output is indistinguishable from `Write-Host` output, so only `$Return` is recoverable. A script ending in a bare `$true` returns nothing. The documented LANCommander pattern already assigns `$Return`, so most scripts are unaffected. |
| **No admin elevation** | `#Requires -RunAsAdministrator` is parsed as an ordinary comment and is inert. `Script::requires_admin` is preserved on the model and still written to disk for .NET interop, but the runner does nothing with it — the host must elevate itself or refuse. Registry writes under `HKLM` fail unelevated, exactly as they would in PowerShell. |
| **`$ScriptType` is a string** | Comparisons work; method calls on it do not. |
| **No .NET type access** | `[System.IO.Path]::Combine(...)`, `[Guid]::NewGuid()` and friends report `NotSupported`. Use the cmdlet equivalents (`Join-Path`, and so on). |
| **No COM** | `New-Object -ComObject` reports `NotSupported`. |
| **No `&` call operator or bare native invocation** | `& "setup.exe"` is a parse error and `setup.exe /S` is not recognised. Use `Start-Process`, which is supported. |
| **Only part of the LANCommander cmdlet pack** | 22 of the .NET SDK's 35 are registered; the rest need subsystems this SDK does not have. See [LANCommander cmdlets](#lancommander-cmdlets) — and note registration is opt-in. |

Everything else a lifecycle script typically reaches for is present: the
registry provider (`HKLM:`/`HKCU:`/`registry::`), `$env:`, `Start-Process` with
`-Wait`/`-PassThru`, recursive `Copy-Item`/`Remove-Item`, `New-Item -Force`
creating parent directories, `-ErrorAction`, `Expand-Archive`, `Start-Sleep`,
`Get-Process`, here-strings, and top-level `param()`.

### Execution model

| Property | Behaviour |
|---|---|
| **Nothing is prepended to your script** | Variables cross the picoposh session API as data, so error line numbers are your script's own and a value containing quotes, `$(...)` or newlines is just a value. |
| **`$Return` survives `exit`** | It is read back from the session after the run, so `$Return = 1; exit 0` reports both. |
| **Runaway protection is opt-in** | `PicoPoshScriptRunner::set_step_limit(n)` bounds a script at `n` statements. Unlimited by default. |
| **Single-threaded** | picoposh keeps its output sink and current directory in process globals, so one script runs at a time; a re-entrant call fails immediately. The runner saves and restores both around each run. |
| **`Invoke-WebRequest` is plain HTTP on Linux/macOS** | The BSD-socket backend has no TLS. WinINet on Windows does. |

## LANCommander cmdlets

The SDK ships a cmdlet pack for the embedded interpreter — the C++ counterpart
of the cmdlets the .NET SDK registers into its runspace, built on picoposh's
`pico_register_cmdlet` hook.

**Registration is opt-in.** Call it once at startup, before running anything:

```cpp
#include <lancommander/script/cmdlets.h>

lancommander::cmdlets::register_all();

// Only needed if a script calls one of the three server-backed cmdlets.
lancommander::cmdlets::Context context;
context.http = &my_http_client;      // must outlive every script run
lancommander::cmdlets::set_context(context);
```

A `PicoCmdletDef` is static and its callbacks receive only the pipeline stage,
so there is nowhere to hang per-registration state — the context is
process-global, like the registry itself. The .NET SDK reaches the same place
by a different route: it stashes the `ApiRequestFactory` in a session variable
and the cmdlet fetches it back out. Cmdlets needing something the context does
not have fail with a message saying so.

Registration is process-global and shared by every interpreter and every
`PicoPoshScriptRunner`, matching picoposh's own contract. A host that wants the
plain picoposh language and nothing else simply does not call it.

### Available

| Cmdlet | Parameters | Notes |
|---|---|---|
| `Get-Runtime` | — | Emits `Windows`, `Linux`, `macOS` or `None`. A string, so `-eq 'Windows'` works. |
| `Get-SanitizedPath` | `-Path` (0, pipeline) | Applies the colon rule (`Half-Life: Opposing Force` → `Half-Life - Opposing Force`), strips characters invalid in a filename, drops a trailing `.`. |
| `Get-PrimaryDisplay` | — | `Primary`, `Width`, `Height`, `RefreshRate`, `BitsPerPixel`. Windows only; the dimensions are zero elsewhere, as in .NET when no display source can be read. |
| `Convert-AspectRatio` | `-Width` (0), `-Height` (1), `-AspectRatio` (2) | Pillar- or letterboxes a resolution. Emits `Width` and `Height`. |
| `Get-HorizontalFov` | `-Width`, `-Height`, `-BaseFov` (90) | Scales a 4:3 FOV to the display. Dimensions default to the primary display. |
| `Get-VerticalFov` | `-Width`, `-Height`, `-BaseFov` (75) | As above. |
| `ConvertTo-StringBytes` | `-Input` (0), `-Utf16`, `-BigEndian`, `-MaxLength`, `-MinLength` | Emits an array of byte values. |
| `ConvertTo-SerializedBase64` | `-Input` (0, pipeline) | Serializes to YAML and base64-encodes it. Same wire format as .NET, so either launcher can read the other's output. |
| `ConvertFrom-SerializedBase64` | `-Input` (0, pipeline) | The inverse. Invalid base64 or non-YAML payloads are reported, not swallowed. |
| `Edit-PatchBinary` | `-Offset` (0), `-Data` (1), `-FilePath` (2) | Writes bytes in place; the rest of the file is untouched. Emits nothing. |
| `Edit-PatchGameSpy` | `-Path` (0), `-Hostname` (1), `-PublicKey` (2), `-BinariesToPatch` (3), `-TextFilesToPatch` (4) | Repoints GameSpy master server references at a replacement service. Emits nothing. See below. |
| `Write-ReplaceContentInFile` | `-Pattern` (0), `-Substitution` (1), `-FilePath` (2) | Regex replace in place, `$1`–`$9` substitutions. Emits the new contents. |
| `Update-IniValue` | `-Section` (0), `-Key` (1), `-Value` (2), `-FilePath` (3), plus the switches below | Emits nothing. |
| `Get-GameManifest` | `-Path` (0), `-Id` | Reads `Manifest.yml`. Emits nothing when absent, matching `ManifestHelper.Read`. |
| `Get-GameOptions` | `-Path` (0), `-Id` | Resolves the game's `OptionSchema` and `Options` into a nested object. |
| `Get-RedistributableOptions` | `-Path` (0), `-Id`, `-Name` | The same for a named redistributable in the manifest. Name matching is case-insensitive. |
| `Write-GameManifest` | `-Path` (0), `-Manifest` (1) | Writes `Manifest.yml`. Emits the path written. |
| `New-Package` | `-Path` (0), `-Version` (1), `-Changelog` (2) | Emits the package **and** assigns `$Return`, as in .NET. |
| `Get-UserCustomField` | `-Name` (0) | Needs `context().http`. A field that was never set returns empty rather than failing. |
| `Update-UserCustomField` | `-Name` (0), `-Value` (1) | Needs `context().http`. |
| `Out-PlayerAvatar` | — | Emits the signed-in user's avatar as a byte array. Needs `context().http`. |
| `Expand-LatestArchive` | `-DestinationPath` (0), `-Destination`, `-OutputPath`, `-GameId`, `-RedistributableId`, `-ToolId` | Needs `context().http` unless `$LatestArchivePath` is set. Emits the destination directory. |

`Update-IniValue` also accepts `-WrapValueInQuotes` (tri-state: omit to follow
the existing value, `1` to force quotes, `0` to strip them), `-UpdateOrAdd`,
`-NoAdd`, `-OnlyRemove`, `-Clear`, `-AlwaysAppend` and `-InsertIndex`. It edits
line by line, so comments, blank lines, spacing and untouched sections survive
byte for byte.

Two deliberate fidelity notes:

- `Convert-AspectRatio` reproduces the .NET version's integer division in its
  branch condition. It looks like a bug, but changing it would silently give
  different resolutions than the .NET launcher for the same inputs.
- `ConvertTo-StringBytes` reproduces the .NET padding quirk where
  `MinLength >= MaxLength` pads to *MaxLength*. Both are covered by tests
  written against the examples in the scripting documentation.
- `ConvertFrom-SerializedBase64` **types plain scalars**, so `Value: 42` comes
  back as a number and `$d.Value + 1` gives `43`. YamlDotNet's
  `Deserialize<object>` returns strings, where the same expression gives
  `"421"`. The typed behaviour is kept because it already applies to
  `Get-GameManifest` — changing it would make the SDK inconsistent with itself.
  Structure and strings round-trip between the two launchers either way.

### Option schemas

`Get-GameOptions` and `Get-RedistributableOptions` resolve two things into one
object: the entity's `OptionSchema` (YAML describing its configurable options,
nested into groups, with defaults) and its `Options` map (dot-notation
overrides, e.g. `Proton.PROTONPATH: GE-Proton9-20`).

```powershell
$options = Get-RedistributableOptions -Path $InstallDirectory -Id $GameId -Name 'umu-launcher'
$options.Proton.PROTONPATH      # "GE-Proton9-20" if overridden, else the schema default
```

Resolution order is schema defaults first, then the `Options` map on top. An
override with no matching schema entry is still surfaced. Dot-notation keys
become real nesting, so `Proton.PROTONPATH` is reached as `$options.Proton.PROTONPATH`.

Options declared `Type: list` are stored as a JSON string and hydrated back
into an array. A scalar list coerces each item to its `ItemType` (`string`,
`int`, `bool`); a composite list — one with `Fields` — becomes an array of
objects, with each field coerced to its own `Type` and falling back to its own
`Default` when a row omits it. A stored value that is not valid JSON is
surfaced as the raw string rather than throwing inside the script.

A list is a leaf: its `Fields` describe the shape of its items, not options
sitting alongside it, so they are never flattened into the result.

Unlike the .NET versions, these do not deserialise into typed
`OptionSchema`/`OptionDefinition` models — the schema stays a JSON tree,
because these two cmdlets read only `Type`, `Default`, `Options`, `ItemType`
and `Fields` from it. `CommandTemplate`, `Choices`, `MinItems`/`MaxItems` and
the display metadata are for the server's UI and are ignored here.

### Edit-PatchGameSpy

Scans a directory for game files referencing GameSpy's master servers and
repoints them, at OpenSpy by default.

```powershell
Edit-PatchGameSpy -Path $InstallDirectory
Edit-PatchGameSpy -Path "$InstallDirectory\System" -BinariesToPatch @('*.dll','*.exe','*.so')
```

Binaries get a byte-level replacement of the `gamespy.com` hostname and the
GameSpy public key. Both replacements must be the same length as what they
replace — 11 characters for the hostname, 256 for the key — so the file's
layout and size never change. A shorter or longer value is refused.

> The .NET help text and the scripting documentation both say the hostname must
> be "exactly 12 characters". It is 11: `gamespy.com`. The .NET code compares
> against `GAMESPY_HOSTNAME.Length`, so 11 is what it actually enforces.

Text configs get three Unreal Engine edits: the UT99 `MasterServerAddress`
line, `bFallbackFactories` inside the `[UBrowserAll]` block, and the Unreal 2
run of `MasterServerList=` entries under `[IpDrv.MasterServerLink]`. Files with
nothing to change are left byte-identical.

**Array parameters need `@(...)`.** picoposh does not parse PowerShell's
`-Param a,b` syntax — the comma arrives as its own argument and would land on
the next positional parameter. Write `-BinariesToPatch @('*.dll','*.exe')`.
Passing the comma form is detected and reported rather than silently
misbinding. Note the example in `Scripting/Cmdlets.md` uses the comma form.

**Globs are non-recursive unless you ask.** `*.dll` covers the given directory
only; `**/*.dll` walks the tree. That matches the `FileSystemGlobbing` semantics
the .NET version relies on, so the defaults (`*.dll`, `*.exe`) only reach the
top level — point `-Path` at the game's `System` directory, or pass `**` patterns.

**Two deliberate divergences from .NET**, both toward working correctly:

- The .NET version searches binaries for the *OpenSpy* public key and replaces
  it with `-PublicKey`, which defaults to that same key — so on an unpatched
  binary, holding the *GameSpy* key, it does nothing. This version searches for
  both keys, which patches an unpatched binary (the documented intent) and still
  re-points an already-patched one. With the default key the second pass writes
  identical bytes.
- The .NET byte search bounds its comparison loop by the buffer length rather
  than the pattern length, so it reads past the end of the pattern as soon as it
  finds a match. This one does not.

### Not available

These need subsystems the C++ SDK does not have. They are absent rather than
stubbed, so calling one gets picoposh's ordinary "not recognized as a cmdlet"
error rather than a silent no-op.

| Cmdlet(s) | Blocked on |
|---|---|
| `Connect-SteamCmd`, `Disconnect-SteamCmd`, `Get-SteamCmdConnectionStatus`, `Get-SteamCmdPath`, `Get-SteamCmdProfile`, `Get-SteamCmdProfiles`, `Set-SteamCmdProfile`, `Remove-SteamCmdProfile`, `Install-SteamContent`, `Remove-SteamContent` | SteamCMD orchestration and profile storage. Now feasible — picoposh has `Start-Process` — but it needs decisions about where SteamCMD and its profiles live. |
| `Get-SteamAppInfo`, `Search-SteamGames`, `Get-SteamWebAssetUri` | The Steam Web API. The host context now carries an `IHttpClient`, so this is mostly a matter of porting the request and response shapes. |

Everything still missing is Steam, and it is **out of scope by choice** rather
than blocked: picoposh has `Start-Process` and the host context carries an
`IHttpClient`, so it is implementable whenever it is wanted. What it needs is
decisions about where SteamCMD lives and where its profiles are stored.

### Regex support

`Write-ReplaceContentInFile` uses picoposh's own regex engine, so its behaviour
matches `-match` and `-replace` in the same script.

Supported: literals, `.`, `*`, `+`, `?`, `[...]` classes with ranges and
negation, `^`/`$` anchors, alternation, capture groups, non-capturing groups
`(?:…)`, non-greedy quantifiers `*?`, counted quantifiers `{n,m}`, and
backslash escapes (`\s`, `\d`, `\w`, `\.` and friends).

Two gaps remain against .NET's `Regex`:

- **No backreferences.** `(ab)\1` does not match `abab`.
- **Always case-insensitive.** There is no case-sensitive variant, and
  `-cmatch` does not exist.

Verified against picoposh `dbdfe25`. Earlier versions had no backslash escapes,
non-greedy or non-capturing groups at all — if you are reading older notes
saying so, they are out of date.

---

## Models

All models are plain structs in the `lancommander` namespace. String fields that represent GUIDs or dates are stored as `std::string` for maximum compatibility (no dependency on UUID or date libraries).

### Key Models

| Struct | Key Fields |
|--------|------------|
| `Game` | `id`, `title`, `sort_title`, `description`, `type`, `base_game_id`, `in_library`, `media`, `genres`, `developers`, `publishers` |
| `GameManifest` | `id`, `title`, `version`, `actions`, `save_paths`, `redistributables` |
| `Tool` | `id`, `name`, `description`, `archives`, `scripts` |
| `Archive` | `id`, `version`, `changelog`, `compressed_size`, `uncompressed_size` |
| `DepotGame` | `id`, `title`, `collections`, `developers`, `publishers`, `genres`, `platforms`, `tags`, `multiplayer_modes`, `cover` |
| `DepotResults` | `games`, `collections`, `companies`, `engines`, `genres`, `platforms`, `tags`, `popular`, `backlog` |

### Entity/Metadata Models

| Struct | Fields |
|--------|--------|
| `Collection` | `id`, `name` |
| `Company` | `id`, `name` |
| `Engine` | `id`, `name` |
| `Genre` | `id`, `name` |
| `Platform` | `id`, `name` |
| `Tag` | `id`, `name` |
| `EntityReference` | `id`, `name` |

### Game-Related Models

| Struct | Fields |
|--------|--------|
| `Action` | `name`, `path`, `arguments`, `working_directory`, `is_primary`, `sort_order`, `variables` |
| `MediaRef` | `id`, `type`, `crc32`, `file_id` |
| `Media` | `id`, `type` (enum), `file_id`, `crc32`, `source_url` |
| `MultiplayerMode` | `type`, `network_protocol`, `min_players`, `max_players`, `spectators` |
| `GameCustomField` | `name`, `value` |
| `GameExternalId` | `provider`, `external_id` |
| `Redistributable` | `id`, `name`, `description`, `scripts` |
| `Script` | `type` (enum), `name`, `description`, `contents`, `requires_admin`, `platforms` (RuntimePlatform bitmask) |
| `GameSave` | `id`, `game_id`, `created_on`, `updated_on` |
| `PlaySession` | `id`, `start`, `end`, `game_id`, `user_id` |

### Other Models

| Struct | Fields |
|--------|--------|
| `AuthToken` | `access_token`, `refresh_token`, `expiration` |
| `AuthenticationProvider` | `name`, `type` |
| `User` | `id`, `user_name`, `alias` |
| `Key` | `value`, `game_id` |
| `Lobby` | `id`, `game_id`, `external_game_id`, `external_username` |
| `Page` | `id`, `title`, `slug`, `route`, `contents`, `sort_order` |
| `Package` | `path`, `version`, `changelog` |
| `Issue` | `description`, `game_id` |
| `CheckForUpdateResponse` | `update_available`, `version`, `download_url` |
| `ErrorResponse` | `error`, `message`, `details` (vector of `ErrorInfo`) |
| `DiscoveredServer` | `address`, `name`, `version` |
| `ServerDetail` | `id`, `name`, `path`, `host`, `port`, `autostart`, `scripts`, etc. |

### Enums

| Enum | Values |
|------|--------|
| `GameType` | `MainGame`, `Expansion`, `StandaloneExpansion`, `Mod`, `StandaloneMod` |
| `MediaType` | `Icon`, `Cover`, `Background`, `Avatar`, `Logo`, `Manual`, `Thumbnail`, `PageImage`, `Grid`, `Screenshot`, `Video` |
| `ScriptType` | `Install`, `Uninstall`, `NameChange`, `KeyChange`, `SaveUpload`, `SaveDownload`, `DetectInstall`, `BeforeStart`, `AfterStop`, `GameStarted`, `GameStopped`, `UserRegistration`, `UserLogin`, `ApplicationStart`, `Package`, `RunWrapper`, `Unknown` |
| `MultiplayerType` | `Local`, `LAN`, `Online` |
| `NetworkProtocol` | `TCPIP`, `IPX`, `Modem`, `Serial`, `Lobby` |
| `ProcessTerminationMethod` | `Close`, `Kill`, `SIGHUP`, `SIGINT`, `SIGKILL`, `SIGTERM` |
| `ServerAutostartMethod` | `OnApplicationStart`, `OnPlayerActivity` |
| `ServerConsoleType` | `LogFile`, `RCON` |
