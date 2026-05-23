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

## Archive Extraction

### `IArchiveExtractor` (abstract)

Interface for extracting archive files. You must provide a concrete implementation (e.g. using minizip, libarchive, or a platform-native API).

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

### `IScriptRunner` (abstract)

Interface for running scripts with variable injection.

```cpp
class IScriptRunner {
public:
    virtual ScriptResult run_file(
        const std::string& script_path,
        const std::string& working_directory,
        const std::map<std::string, std::string>& variables) = 0;

    virtual ScriptResult run_inline(
        const std::string& script_contents,
        const std::string& working_directory,
        const std::map<std::string, std::string>& variables) = 0;
};
```

**ScriptResult fields**: `success`, `exit_code`, `output`, `error`

### `BatchScriptRunner`

Runs `.bat`/`.cmd` scripts via `cmd.exe /C`. Variables are injected as environment variables accessible via `%VAR_NAME%` in the script.

```cpp
lancommander::BatchScriptRunner runner;

std::map<std::string, std::string> vars;
vars["InstallDirectory"] = "C:\\Games\\MyGame";
vars["PlayerAlias"] = "Player1";
vars["ServerAddress"] = "http://192.168.1.100:1337";

auto result = runner.run_file(
    "C:\\Games\\MyGame\\.lancommander\\install.bat",
    "C:\\Games\\MyGame",
    vars);

if (result.success) {
    printf("Script output: %s\n", result.output.c_str());
} else {
    printf("Script failed (exit code %d): %s\n", result.exit_code, result.error.c_str());
}
```

Inline scripts are written to a temporary `.bat` file, executed, and cleaned up:

```cpp
auto result = runner.run_inline(
    "@echo off\necho Installing to %InstallDirectory%\n",
    "C:\\Games\\MyGame",
    vars);
```

**Platform support**: Windows only (95 through modern). On non-Windows platforms, returns a "not supported" error. Use `IScriptRunner` to implement shell-based runners for other platforms.

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
| `Redistributable` | `id`, `name`, `description` |
| `Script` | `type` (enum), `name`, `contents` |
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
