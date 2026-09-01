using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace LANCommander.Launcher.Services.Tests.Helpers;

/// <summary>
/// Builds real (not mocked) SDK clients and launcher services for tests that exercise
/// installation-lifecycle behavior without touching the network. GameClient/ToolClient expose
/// only non-virtual methods (see ImportServiceTests), so rather than mocking them this
/// constructs real instances with null! dependencies and only exercises call paths that are
/// file-system-only and safe when no on-disk manifest exists at the given directory (e.g.
/// GameClient.UninstallAsync/MoveAsync on a fresh temp directory return/no-op immediately
/// instead of touching any of the null fields).
/// </summary>
internal static class ServiceTestFactory
{
    /// <summary>
    /// A GameClient whose dependencies are all null. Safe to call UninstallAsync, MoveAsync,
    /// GetInstallDirectory, UninstallAddonsAsync/InstallAddonsAsync/RestoreFilesAsync, and
    /// UpdateGameInstallationAsync against directories that have no on-disk manifest / are not a
    /// Mod/Expansion/StandaloneMod with a BaseGameId — those paths return early without
    /// dereferencing any of the null fields. Do NOT call network-bound methods (GetAsync,
    /// GenerateInstallPlanAsync, ExecuteInstallPlanItemAsync, RunAsync, ResolveArchiveAsync, ...)
    /// against this instance.
    /// </summary>
    public static GameClient CreateGameClient() =>
        new(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);

    /// <summary>
    /// A ToolClient whose dependencies are all null. Safe to call UninstallAsync against a
    /// directory with no on-disk manifest. Do NOT call network-bound methods against it.
    /// </summary>
    public static ToolClient CreateToolClient() =>
        new(null!, null!, null!, null!);

    public static GameInstallationService CreateGameInstallationService(Data.DatabaseContext context) =>
        new(context, NullLogger<GameInstallationService>.Instance);

    public static ToolService CreateToolService(Data.DatabaseContext context) =>
        new(NullLogger<ToolService>.Instance, context);

    /// <summary>
    /// A GameService wired for tests that only exercise Uninstall/Run's own logic (never touches
    /// PlaySessionService/ProfileClient/AuthenticationService/IConnectionClient, which are only
    /// used by the process-launching parts of Run()). Pass a real (possibly empty)
    /// IServiceProvider since UninstallAsync resolves InstallService/LibraryService from it via
    /// null-conditional calls.
    /// </summary>
    public static GameService CreateGameService(
        Data.DatabaseContext context,
        ToolService toolService,
        GameInstallationService gameInstallationService,
        GameClient? gameClient = null,
        ToolClient? toolClient = null,
        IServiceProvider? serviceProvider = null) =>
        new(
            context,
            NullLogger<GameService>.Instance,
            authenticationService: null!,
            playSessionService: null!,
            profileClient: null!,
            gameClient: gameClient ?? CreateGameClient(),
            toolService: toolService,
            toolClient: toolClient ?? CreateToolClient(),
            gameInstallationService: gameInstallationService,
            connectionClient: null!,
            serviceProvider: serviceProvider ?? new ServiceCollection().BuildServiceProvider());

    /// <summary>
    /// An InstallService wired for tests that only exercise queue bookkeeping (Add/Remove/
    /// ClearCompleted/CancelInstallAsync) and Move — never touches ImportService,
    /// RedistributableClient, or MediaClient. GameClient/ToolClient must be real instances (not
    /// null!) because the constructor subscribes to their progress events.
    /// </summary>
    public static InstallService CreateInstallService(
        GameService gameService,
        ToolService toolService,
        GameInstallationService gameInstallationService,
        GameClient? gameClient = null,
        ToolClient? toolClient = null) =>
        new(
            NullLogger<InstallService>.Instance,
            gameService,
            toolService,
            importService: null!,
            gameInstallationService,
            gameClient ?? CreateGameClient(),
            redistributableClient: null!,
            toolClient ?? CreateToolClient(),
            mediaClient: null!);
}
