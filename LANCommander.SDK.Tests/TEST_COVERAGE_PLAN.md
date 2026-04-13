# LANCommander.SDK — Test Coverage Plan

## Current Status

| Area | Classes | Methods | Tests | Coverage |
|---|---|---|---|---|
| Extensions | 8 public | ~20 public | 88 | High |
| Helpers — `DirectoryHelper` | 1 | 3 | 22 | High |
| Helpers — `ManifestHelper` | 1 | 9 | 40+ | High |
| Helpers — `ScriptHelper` | 1 | 6 | 20+ | High |
| Helpers — `IniHelper` | 1 | 4 | ~20 (via IniHandling/) | Medium |
| Helpers — `DisplayHelper` | 1 | 1 | 0 | None |
| Helpers — `EnvironmentHelper` | 1 | 1 | 0 | None |
| Helpers — `VersionHelper` | 1 | 1 | 0 | None |
| Utilities — `SavePacker` | 1 | 7 | ~20 | Medium |
| Utilities — `RegistryExportUtility` | 1 | 1 | 0 | None |
| Utilities — `RegistryImportUtility` | 1 | 1 | 0 | None |
| Clients (all 18) | 18 | 100+ | 0 | None |
| Models with logic | ~5 | ~10 | ~5 | Low |

---

## Test Infrastructure Requirements

Before writing client tests, two pieces of test infrastructure are needed.

### 1. Fake `HttpMessageHandler`

All HTTP clients use `ApiRequestFactory` → `ApiRequestBuilder` → `HttpClient`. The most effective
isolation strategy is a fake `HttpMessageHandler` that intercepts requests and returns controlled
responses, without touching a real server.

```csharp
// Suggested location: LANCommander.SDK.Tests/Infrastructure/FakeHttpMessageHandler.cs
public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        => _handler = handler;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(_handler(request));
}
```

For JSON responses, a helper that serializes any object to an `application/json`
`StringContent` with status 200 reduces boilerplate across all client tests.

### 2. Package References

Add to `LANCommander.SDK.Tests.csproj`:

```xml
<PackageReference Include="NSubstitute" Version="5.*" />
```

NSubstitute is used to mock `ILogger<T>`, `ISettingsProvider`, `ITokenProvider`,
`IConnectionClient`, and other injected dependencies that client constructors require but
whose behavior is not under test.

### 3. Shared Client Fixtures

Because every client shares the same constructor pattern (`ILogger`, `ApiRequestFactory`, etc.),
a base class or shared factory method should build a ready-to-use client with a fake
`HttpMessageHandler` injected, reducing boilerplate to a single line per test.

---

## Coverage Plan

### Priority 1 — Pure Computation (no mocks needed)

These classes have no I/O dependencies and can be tested directly. Most are already covered;
the gaps are small.

---

#### `VersionHelper.GetCurrentVersion()`
**File:** `Helpers/VersionHelperTests.cs`

| Test | What it checks |
|---|---|
| `GetCurrentVersion_ReturnsNonNullVersion` | Return value is not null |
| `GetCurrentVersion_ReturnsSemVersion` | Parses into a valid `SemVersion` |
| `GetCurrentVersion_MajorAndMinorAreNonNegative` | Sanity-check numeric components |

**Notes:** The method reads from the executing assembly's `AssemblyInformationalVersionAttribute`.
The version available during test runs may be `0.0.0` or similar — tests should assert shape,
not a specific value.

---

#### `IniHelper` (gap fill)
**File:** `IniHandling/IniHelperTests.cs`

The existing `IniHandlingTests_MadMilkman` already tests `FromString` and `ToString`
indirectly via the `ConfigurationTests` data set. The following dedicated tests cover
`IniHelper`'s own behaviour independently of `MadMilkman` specifics.

| Test | What it checks |
|---|---|
| `FromString_WithEmptyString_ReturnsEmptyIniFile` | No sections or keys |
| `FromString_WithNullString_ThrowsOrReturnsEmpty` | Null safety |
| `FromString_DefaultOptions_AllowsDuplicateKeys` | Default `IniOptions` behaviour |
| `ToString_RoundTrip_PreservesAllKeyValues` | Serialise then re-parse matches original |
| `ToString_WithCustomEncoding_WritesExpectedBytes` | Encoding parameter is respected |
| `FromString_WithCustomOptions_KeyDuplicateIgnored_TakesFirstValue` | `IniDuplication.Ignored` |
| `FromString_WithCustomOptions_KeyDuplicateAllowed_RetainsAllValues` | `IniDuplication.Allowed` |

---

#### `ProfileClient.GetAvatarUri()` and `MediaClient` URI/path helpers
**File:** `Clients/MediaClientPureTests.cs`

These methods are pure string computation inside otherwise network-heavy clients, making them
good isolated targets.

| Test | Method | What it checks |
|---|---|---|
| `GetAbsoluteUrl_WithValidMedia_ReturnsAbsoluteUri` | `MediaClient.GetAbsoluteUrl` | Scheme + host from settings |
| `GetLocalPath_WithMedia_ReturnsExpectedFormat` | `MediaClient.GetLocalPath(Media)` | FileId + CRC32 path format |
| `GetLocalPath_WithFileIdAndCrc32_MatchesMediaOverload` | `MediaClient.GetLocalPath(Guid, string)` | Both overloads agree |
| `GetDownloadPath_ReturnsPathUnderExpectedDirectory` | `MediaClient.GetDownloadPath` | Path structure |
| `GetAvatarUri_BuildsCorrectUrl` | `ProfileClient.GetAvatarUri` | Host + username in URI |
| `CalculateChecksumAsync_OnKnownContent_ReturnsExpectedCrc32` | `MediaClient.CalculateChecksumAsync` | File CRC32 is deterministic |

---

### Priority 2 — Helpers with filesystem I/O (already have test infrastructure)

These use the established `IDisposable`+temp-directory pattern from `DirectoryHelperTests`.

---

#### `EnvironmentHelper.IsRunningInContainer()`
**File:** `Helpers/EnvironmentHelperTests.cs`

The method checks three signals in order:
1. Existence of `/.dockerenv`
2. Contents of `/proc/1/cgroup`
3. Environment variables (`KUBERNETES_SERVICE_HOST`, `container`, `PODMAN_VERSION`, etc.)

Because the checks read real filesystem paths, tests must set up controlled temporary files or
environment variables rather than relying on the host system's state.

| Test | Setup | What it checks |
|---|---|---|
| `IsRunningInContainer_WithDockerenvFile_ReturnsTrue` | Create `{tempDir}/.dockerenv`; inject path | Docker detection via file |
| `IsRunningInContainer_WithCgroupContainingDocker_ReturnsTrue` | Write `docker` into a temp cgroup file | cgroup-based detection |
| `IsRunningInContainer_WithCgroupContainingKubernetes_ReturnsTrue` | Write `kubepods` into cgroup | Kubernetes via cgroup |
| `IsRunningInContainer_WithKubernetesEnvVar_ReturnsTrue` | Set `KUBERNETES_SERVICE_HOST` env var | Kubernetes env var |
| `IsRunningInContainer_WithContainerEnvVar_ReturnsTrue` | Set `container=podman` env var | Podman/container env var |
| `IsRunningInContainer_WithNoSignals_ReturnsFalse` | No file, no env var, empty cgroup | Normal environment |

**Notes:** The current implementation reads hardcoded paths (`/.dockerenv`, `/proc/1/cgroup`).
A thin path-injection seam (or a wrapper method the tests can override) will be needed to avoid
making these tests host-dependent.

---

#### `DisplayHelper.GetScreen()` — Linux parsing only
**File:** `Helpers/DisplayHelperTests.cs`

The three Linux code paths (`xrandr`, `xdpyinfo`, `/sys/class/drm`) each parse a specific text
format. The parsing logic can be tested directly if it is extracted into internal parse methods,
or indirectly by supplying mock process output.

| Test | What it checks |
|---|---|
| `ParseXrandrOutput_WithTypicalOutput_ExtractsBoundsAndRefreshRate` | Width × height × Hz from `xrandr` format |
| `ParseXrandrOutput_WithMultipleDisplayLines_PicksConnected` | Multiple monitors, picks "connected" line |
| `ParseXdpyinfoOutput_WithTypicalOutput_ExtractsDimensions` | Width × height from `xdpyinfo` format |
| `ParseDrmOutput_WithTypicalFilesystemContent_ExtractsDimensions` | `/sys/class/drm/*/modes` format |
| `GetScreen_WhenNoDisplayServer_ReturnsDefaultOrNull` | Graceful fallback when xrandr is absent |

**Notes:** The private parse helpers are currently inline within `GetScreen()`. Extracting them
to `internal static` methods would allow direct testing without spawning processes.

---

### Priority 3 — Utilities

---

#### `SavePacker` (gap fill)
**File:** `Utilities/SavePackerTests.cs` *(add to existing file)*

The existing `SavePackerTests` already cover the primary happy paths. The remaining gaps are:

| Test | What it checks |
|---|---|
| `AddPath_WithRegistryType_CallsAddRegistryPath` | `SavePathType.Registry` routes correctly |
| `AddRegistryPath_OnLinux_ProducesNoEntries` | Registry export is no-op on Linux |
| `AddPaths_TwoDifferentSavePathIds_EachInOwnSubdirectory` | Partition by `SavePath.Id` in zip |
| `PackAsync_CalledTwice_ReturnsFreshStreamEachTime` | Idempotency / re-use after pack |
| `PackAsync_EmptyPacker_ProducesValidZipStream` | Empty archive is still valid ZIP |
| `AddManifestAsync_CalledTwice_HasManifestRemainsTrue` | No error on duplicate manifest add |

---

#### `RegistryExportUtility` — Windows-only
**File:** `Utilities/RegistryExportUtilityTests.cs`

These tests only run on Windows. Decorate the class with
`[PlatformSpecific(TestPlatforms.Windows)]` (or use `Skip` on non-Windows).

| Test | What it checks |
|---|---|
| `Export_WithKnownRegistryKey_ProducesRegFileFormat` | Output starts with `Windows Registry Editor` header |
| `Export_WithStringValue_IncludesRegSz` | `REG_SZ` values appear as `"key"="value"` |
| `Export_WithDwordValue_IncludesRegDword` | `REG_DWORD` values formatted with hex |
| `Export_WithExpandStringValue_IncludesRegExpandSz` | `REG_EXPAND_SZ` type tag |
| `Export_WithMultiStringValue_IncludesRegMultiSz` | Null-separated multi-string encoding |
| `Export_WithBinaryValue_IncludesRegBinary` | Hex byte sequence format |
| `Export_WithNonExistentKey_ReturnsEmptyOrThrows` | Error handling for missing keys |
| `Export_WithNestedSubkeys_RecursivelyCapturesAll` | Deep key trees |

---

#### `RegistryImportUtility` — Windows-only
**File:** `Utilities/RegistryImportUtilityTests.cs`

| Test | What it checks |
|---|---|
| `Import_ValidRegFile_WritesValuesToRegistry` | Round-trip with `RegistryExportUtility` |
| `Import_WithDeletedKey_RemovesKey` | `-` prefix in `.reg` syntax |
| `Import_WithMalformedFile_ThrowsOrReturnsError` | Error handling |
| `Import_WithEmptyFile_DoesNotThrow` | Edge case |

---

### Priority 4 — Clients (require mock HTTP infrastructure)

All clients use `ApiRequestFactory` → `HttpClient`. The recommended approach is to create
`ApiRequestFactory` with a custom `HttpClient` backed by `FakeHttpMessageHandler`, then
construct the client under test with that factory.

---

#### `AuthenticationClient`
**File:** `Clients/AuthenticationClientTests.cs`

| Test | HTTP mock | What it checks |
|---|---|---|
| `AuthenticateAsync_WithValidCredentials_ReturnsToken` | POST `/api/Auth/Login` → 200 + `AuthToken` JSON | Token returned and stored |
| `AuthenticateAsync_WithWrongPassword_ThrowsOrReturnsNull` | POST `/api/Auth/Login` → 401 | Failure handling |
| `LogoutAsync_SendsDeleteRequest` | POST/DELETE `/api/Auth/Logout` → 200 | Request sent |
| `RegisterAsync_WithValidData_Succeeds` | POST `/api/Auth/Register` → 200 | No exception |
| `RegisterAsync_WithConflictingUsername_ThrowsOrReturns` | POST `/api/Auth/Register` → 409 | Conflict handling |
| `ValidateTokenAsync_WithValidToken_ReturnsTrue` | GET `/api/Auth/Validate` → 200 | True returned |
| `ValidateTokenAsync_WithExpiredToken_ReturnsFalse` | GET `/api/Auth/Validate` → 401 | False returned |
| `GetAuthenticationProvidersAsync_ReturnsProviderList` | GET `/api/Auth/GetAuthenticationProviders` → provider JSON | List deserialized |
| `GetAuthenticationProviderLoginUrl_WithProvider_BuildsCorrectUrl` | No HTTP needed | URI shape correct |

---

#### `GameClient` — metadata and static methods
**File:** `Clients/GameClientTests.cs`

The `GameClient` is the most complex client (~1800 lines). Focus on the methods that do not
orchestrate archive downloads, as those are better covered by integration tests.

| Test | HTTP mock | What it checks |
|---|---|---|
| `GetAsync_ReturnsDeserializedGameList` | GET `/api/Games` → game list JSON | Correct deserialization |
| `GetAsync_ById_ReturnsGame` | GET `/api/Games/{id}` → single game JSON | Single game returned |
| `GetManifestAsync_ReturnsManifest` | GET `/api/Games/{id}/Manifest` → YAML | Manifest deserialized |
| `GetAddonsAsync_ReturnsAddonList` | GET `/api/Games/{id}/Addons` → addon JSON | Addons listed |
| `GetToolsAsync_ReturnsToolList` | GET `/api/Games/{id}/Tools` → tool JSON | Tools listed |
| `CheckForUpdateAsync_WhenUpdateAvailable_ReturnsTrue` | GET `/api/Games/{id}/CheckForUpdate` → `{"updateAvailable":true}` | True returned |
| `CheckForUpdateAsync_WhenUpToDate_ReturnsFalse` | GET `/api/Games/{id}/CheckForUpdate` → `{"updateAvailable":false}` | False returned |
| `StartedAsync_SendsRequest` | GET `/api/Games/{id}/Started` → 200 | Request sent to correct URL |
| `StoppedAsync_SendsRequest` | GET `/api/Games/{id}/Stopped` → 200 | Request sent to correct URL |
| `GetMetadataDirectoryPath_ReturnsCorrectPath` | No HTTP | Path contains `.lancommander/{id}` |
| `GetPlayerAlias_WhenFileAbsent_ReturnsEmpty` | No HTTP, temp dir | Empty string returned |
| `UpdatePlayerAlias_WritesAliasFile` | No HTTP, temp dir | File written, alias readable |
| `GetCurrentKey_WhenFileAbsent_ReturnsEmpty` | No HTTP, temp dir | Empty string returned |
| `UpdateCurrentKey_WritesKeyFile` | No HTTP, temp dir | File written, key readable |

---

#### `DepotClient`
**File:** `Clients/DepotClientTests.cs`

| Test | HTTP mock | What it checks |
|---|---|---|
| `GetAsync_ReturnsDepotResults` | GET `/api/Depot` → results JSON | Deserialized correctly |
| `GetGameAsync_ReturnsDepotGame` | GET `/api/Depot/Games/{id}` → game JSON | Single game deserialized |
| `GetGameAsync_WithServerError_Throws` | GET `/api/Depot/Games/{id}` → 500 | Exception propagated |

---

#### `LibraryClient`
**File:** `Clients/LibraryClientTests.cs`

| Test | HTTP mock | What it checks |
|---|---|---|
| `GetAsync_ReturnsEntityReferences` | GET `/api/Library` → reference list JSON | List deserialized |
| `AddToLibrary_WithValidGameId_ReturnsTrue` | POST `/api/Library/AddToLibrary/{id}` → `true` | True returned |
| `RemoveFromLibrary_ById_ReturnsTrue` | POST `/api/Library/RemoveFromLibrary/{id}` → `true` | True returned |
| `RemoveFromLibrary_WithAddonIds_SendsAddonList` | POST `/api/Library/RemoveFromLibrary/{id}/addons` | Addon IDs in request body |

---

#### `TagClient`
**File:** `Clients/TagClientTests.cs`

| Test | HTTP mock | What it checks |
|---|---|---|
| `CreateAsync_SendsTagAndReturnsCreated` | POST `/api/Tags` → created tag JSON | Returned tag has correct fields |
| `UpdateAsync_SendsUpdatedTag` | POST `/api/Tags/{id}` → updated tag JSON | Request routed correctly |
| `DeleteAsync_SendsDeleteRequest` | DELETE `/api/Tags/{id}` → 200 | Delete request sent |

---

#### `PlaySessionClient`
**File:** `Clients/PlaySessionClientTests.cs`

| Test | HTTP mock | What it checks |
|---|---|---|
| `GetAsync_ReturnsSessions` | GET `/api/PlaySessions` → session list JSON | List deserialized |
| `GetAsync_ByGameId_ReturnsGameSessions` | GET `/api/PlaySessions/{id}` → session list JSON | Filtered sessions returned |

---

#### `ProfileClient`
**File:** `Clients/ProfileClientTests.cs`

| Test | HTTP mock | What it checks |
|---|---|---|
| `GetAsync_ReturnsUser` | GET `/api/Profile` → user JSON | User deserialized |
| `GetAsync_CalledTwice_UsesCachedResult` | GET `/api/Profile` → 200 (once) | Only one HTTP call made |
| `GetAsync_WithForceLoad_BypassesCache` | GET `/api/Profile` → 200 (twice) | Two HTTP calls made |
| `GetAliasAsync_ReturnsAliasFromUser` | GET `/api/Profile` → user JSON | Alias extracted from user object |
| `ChangeAliasAsync_SendsPutRequest` | PUT `/api/Profile/ChangeAlias` → new alias string | Alias value in response |
| `GetCustomFieldAsync_ReturnsFieldValue` | GET `/api/Profile/CustomField/{name}` → value string | Value returned |
| `UpdateCustomFieldAsync_SendsNewValue` | PUT `/api/Profile/CustomField/{name}` → value | Request body matches value |

---

#### `MediaClient` — network methods
**File:** `Clients/MediaClientTests.cs`

| Test | HTTP mock | What it checks |
|---|---|---|
| `GetAsync_ReturnsMediaObject` | GET `/api/Media/{id}` → media JSON | Deserialized correctly |
| `DownloadAsync_WritesFileToDestination` | GET → binary stream | File exists at destination path |
| `DownloadAsync_WhenDestinationDirectoryAbsent_CreatesIt` | GET → binary stream | Directory auto-created |
| `GetStaleLocalPaths_WhenNoFilesExist_ReturnsEmpty` | No HTTP, temp dir | Empty result |
| `GetStaleLocalPaths_WhenOldVersionsExist_ReturnsThem` | No HTTP, temp dir | Stale files enumerated |
| `CalculateChecksumAsync_KnownContent_ReturnsDeterministicCrc32` | No HTTP, temp file | Same file → same CRC |

---

#### `IssueClient`
**File:** `Clients/IssueClientTests.cs`

| Test | HTTP mock | What it checks |
|---|---|---|
| `Open_WithValidIssue_ReturnsTrueOnSuccess` | POST `/api/Issue/Open` → `true` | True returned |
| `Open_WithServerError_ReturnsFalse` | POST `/api/Issue/Open` → 500 | False returned or exception |

---

#### `LauncherClient`
**File:** `Clients/LauncherClientTests.cs`

| Test | HTTP mock | What it checks |
|---|---|---|
| `CheckForUpdateAsync_WhenUpdateAvailable_ReturnsResponse` | GET `/api/Launcher/CheckForUpdate` → JSON | Response deserialized |
| `DownloadAsync_WritesFileToGivenPath` | GET → binary stream | File written to destination |

---

#### `ConnectionClient` — pure-computation methods
**File:** `Clients/ConnectionClientTests.cs`

Focus on the methods that do not require a running server:

| Test | Setup | What it checks |
|---|---|---|
| `IsConnected_WhenNotYetConnected_ReturnsFalse` | Fresh instance | False by default |
| `IsConfigured_WhenServerAddressSet_ReturnsTrue` | Mock `ISettingsProvider` with address | True when configured |
| `IsConfigured_WhenNoAddress_ReturnsFalse` | Mock `ISettingsProvider` with empty address | False without address |
| `HasServerAddress_WhenAddressSet_ReturnsTrue` | Mock provider | True |
| `IsOfflineMode_WhenOfflineModeEnabled_ReturnsTrue` | Mock provider offline setting | True |
| `PingAsync_WithSuccessfulResponse_ReturnsTrue` | HTTP HEAD → custom X-Pong header | True returned |
| `PingAsync_WithTimeout_ReturnsFalse` | HTTP HEAD → timeout | False returned without throw |

---

#### `SaveClient` — pure-computation methods
**File:** `Clients/SaveClientTests.cs` *(extend existing file)*

The existing `SaveService.cs` tests cover `GetLocalPath`, `GetArchivePath`, and
`GetFileSavePathEntries`. The following are the remaining gaps:

| Test | Setup | What it checks |
|---|---|---|
| `PackAsync_WithManifestAndSavePaths_ProducesZip` | Temp dir + manifest | Zip contains expected entries |
| `GetLocalPath_WithInstallDir_ReturnsInstallPath` | No I/O | `{InstallDir}` expanded correctly |
| `GetLocalPath_WithMyDocuments_ExpandsToActualPath` | No I/O | Special folder expansion |
| `GetArchivePath_StripsInstallDirPrefix` | No I/O | Archive-relative path returned |
| `GetFileSavePathEntries_WithDirectorySavePath_ReturnsAllFiles` | Temp dir with files | All files enumerated |
| `GetFileSavePathEntries_WithRegexSavePath_OnlyMatchingReturned` | Temp dir with mixed files | Non-matching excluded |

---

#### `BeaconClient` — unit-testable subset
**File:** `Clients/BeaconClientTests.cs`

The UDP broadcast and socket operations require a real network or a UDP socket mock, making
full integration tests impractical for unit tests. Focus on the fluent API and configuration.

| Test | Setup | What it checks |
|---|---|---|
| `AddBeaconMessageInterceptor_ReturnsClientInstance` | No network | Fluent return value |
| `AddBeaconMessageInterceptor_InterceptorIsCalledOnMessage` | No network, mock interceptor | Interceptor receives message |
| `CleanupProbe_WhenNotStarted_DoesNotThrow` | No network | Safe on unused instance |
| `StopProbeAsync_WhenNotRunning_DoesNotThrow` | No network | Safe on unused instance |
| `StopBeaconAsync_WhenNotRunning_DoesNotThrow` | No network | Safe on unused instance |

---

### Priority 5 — Models with non-trivial logic

---

#### `InstallProgress`
**File:** `Install/InstallProgressTests.cs` *(already covered, adding edge cases)*

| Test | What it checks |
|---|---|
| `Progress_WhenTotalBytesIsZero_ReturnsNaN` | Division by zero → `float.NaN` *(already exists)* |
| `Progress_WhenBytesExceedTotal_ReturnsGreaterThanOne` | Over-transfer edge case |
| `Progress_WhenTotalIsNegative_BehavesConsistently` | Negative total |

---

#### `ChatThread`
**File:** `Models/ChatThreadTests.cs`

`ChatThread` has observable message collections and async event callbacks.

| Test | What it checks |
|---|---|
| `AddMessage_IncreasesMessageCount` | Message added to collection |
| `AddMessage_FiresMessagesReceivedAsync_IfSubscribed` | Callback invoked |
| `Messages_InitiallyEmpty` | Default state |
| `Typing_InitiallyEmpty` | Default state for typing indicators |

---

### Priority 6 — `ApiRequestBuilder`

`ApiRequestBuilder` is the common HTTP plumbing used by all clients. Testing it in isolation
provides coverage of the serialization, header injection, and progress-reporting logic that all
clients share.

**File:** `Helpers/ApiRequestBuilderTests.cs`

| Test | HTTP mock | What it checks |
|---|---|---|
| `GetAsync_SendsGetRequest_ToConfiguredRoute` | Any 200 | Method is GET, URL matches route |
| `PostAsync_SendsJsonBody` | Capture body, return 200 | Request body deserialized matches input |
| `PutAsync_SendsJsonBody` | Capture body, return 200 | Method is PUT, body correct |
| `DeleteAsync_SendsDeleteRequest` | Any 200 | Method is DELETE |
| `HeadAsync_SendsHeadRequest` | Any 200 | Method is HEAD |
| `UseAuthenticationToken_AddsAuthorizationHeader` | Capture headers | `Authorization: Bearer <token>` present |
| `UseVersioning_AddsVersionHeader` | Capture headers | Custom version header present |
| `AddHeader_AddsCustomHeader` | Capture headers | Header value matches |
| `SetTimeout_OverridesDefault` | Delayed response | Request cancelled after timeout |
| `OnProgress_CalledDuringDownload` | Streaming binary | Progress callback fires |
| `OnComplete_CalledAfterDownload` | Any 200 | Completion callback fires |
| `SendAsync_On4xx_ThrowsOrReturnsError` | 404 response | Handled consistently |
| `SendAsync_On5xx_ThrowsOrPropagates` | 500 response | Error propagated |
| `DownloadAsync_WritesResponseToFile` | Binary stream response | File written at path |
| `UploadAsync_SendsFileAsMultipart` | Capture request | Content-Type is multipart |

---

## Platform-Specific Tests

Tests that only run on a specific platform should use `Skip` to self-document why they
are not running rather than silently passing.

```csharp
[Fact(Skip = "Windows-only: requires P/Invoke to ntdll.dll")]
public void GetParentProcessId_ReturnsParentPid() { ... }
```

### Windows-only
- `RegistryExportUtilityTests` (entire file)
- `RegistryImportUtilityTests` (entire file)
- `DisplayHelper.GetDeviceMode()` tests
- `ProcessHelper.GetParentProcessId()` tests (internal)
- `LobbyClient.GetSteamLobbies()` — also requires Steam runtime

### Linux-only
- `DisplayHelper` xrandr/xdpyinfo/drm parsing tests

### Skipped (require real processes or sockets)
- `ProcessExtensions.WaitForAllExitAsync()` — needs real spawned process, timing-sensitive
- `BeaconClient.StartProbeAsync()` / `StartBeaconAsync()` — needs real UDP sockets

---

## Pre-existing Issues (do not create new tests for these)

The following failures exist in the test suite and should be addressed in the SDK itself
before adding further test coverage that would interact with them.

| Area | Root cause |
|---|---|
| `SaveDownloadTests` / `SaveUploadTests` | `DeflateEnvironmentVariables` throws on Linux when env vars like `LOCALAPPDATA` are null; `Regex.Escape(null)` throws `ArgumentNullException` |
| `SaveClientTests.SimpleInstallDirectorySavePathsShouldWork` | Same root cause |
| `SavePackerTests` (several) | Depends on `GetFileSavePathEntries` which calls `DeflateEnvironmentVariables` |
| `StringExtensions.cs` (ExpandEnvironmentVariables theory entries) | Tests use Windows-specific paths and env vars; results differ on Linux |

The fix is a null-guard in `StringExtensions.DeflateEnvironmentVariables` before calling
`Regex.Escape`:

```csharp
if (string.IsNullOrEmpty(value)) continue;
```

---

## Implementation Order

1. **Fix the `DeflateEnvironmentVariables` null-guard** — unblocks ~30 currently-failing tests
2. **`VersionHelper`, `IniHelper` gap-fill** — pure computation, no setup required
3. **`ApiRequestBuilder` with `FakeHttpMessageHandler`** — builds the HTTP test infrastructure
4. **Small pure-HTTP clients** (`TagClient`, `PlaySessionClient`, `IssueClient`, `DepotClient`) — simple CRUD, same infrastructure
5. **`ProfileClient`, `LibraryClient`, `MediaClient`** — moderate complexity
6. **`AuthenticationClient`** — auth token flow
7. **`ConnectionClient`** — server health and configuration
8. **`GameClient`** metadata methods — largest client, split into multiple test files
9. **`EnvironmentHelper`** with file-system injection
10. **`DisplayHelper`** parsing extraction + tests
11. **`RegistryExportUtility` / `RegistryImportUtility`** — Windows CI job
