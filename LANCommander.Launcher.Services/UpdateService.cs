using LANCommander.SDK;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Models;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Services;
using LANCommander.SDK.Updating;
using Microsoft.Extensions.Logging;
using Semver;

namespace LANCommander.Launcher.Services
{
    /// <summary>What to tell the user about the last update when the launcher starts.</summary>
    /// <param name="Error">Why the update was undone, or null when it installed.</param>
    public sealed record UpdateNotice(string Version, string? PreviousVersion, string? Error)
    {
        public bool Succeeded => Error == null;
    }

    /// <summary>
    /// Updates the launcher from its server: checks for a newer release, then downloads and stages it for
    /// <see cref="LauncherUpdateHandover"/> to swap in.
    /// </summary>
    public class UpdateService(
        ILogger<UpdateService> logger,
        LauncherClient launcherClient,
        SettingsProvider<Settings.Settings> settingsProvider) : BaseService(logger)
    {
        /// <summary>The running launcher's installation, or null when it can't update itself.</summary>
        public static UpdateInstallation? Installation { get; } =
#if DEBUG
            UpdateInstallation.Detect(isDebugBuild: true);
#else
            UpdateInstallation.Detect();
#endif

        public delegate Task OnUpdateAvailableHandler(CheckForUpdateResponse response);
        public event OnUpdateAvailableHandler? OnUpdateAvailable;

        /// <summary>Raised, on any thread, when an update is found or staged.</summary>
        public event EventHandler? StateChanged;

        private UpdateNotice? _notice;

        public bool IsSupported => Installation != null;

        /// <summary>The newer release the server last offered, if any.</summary>
        public CheckForUpdateResponse? AvailableUpdate { get; private set; }

        /// <summary>The version of an update that is downloaded and staged, ready to install.</summary>
        public string? StagedVersion { get; private set; }

        public bool IsStaged => StagedVersion != null;

        /// <summary>The version the launcher can update to, downloaded or not.</summary>
        public string? AvailableVersion => AvailableUpdate?.Version ?? StagedVersion;

        public bool IsUpdateAvailable => IsSupported && AvailableVersion != null;

        /// <summary>Where the launcher keeps its update journal and staged files.</summary>
        public string JournalDirectory =>
            AppPaths.ResolveStorageLocationPath(settingsProvider.CurrentValue.Updates.StoragePath);

        public async Task<CheckForUpdateResponse?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
        {
            if (Installation == null)
                return null;

            using (var op = Logger.BeginOperation("Checking for updates"))
            {
                var response = await launcherClient.CheckForUpdateAsync(Installation.PackageKind, cancellationToken);

                op.Complete();

                if (response is not { UpdateAvailable: true } || !IsNewer(response.Version))
                    return null;

                // A newer release than the one staged replaces it
                if (StagedVersion != null && StagedVersion != response.Version)
                    StagedVersion = null;

                AvailableUpdate = response;

                Logger?.LogInformation("Launcher update {Version} is available", response.Version);

                StateChanged?.Invoke(this, EventArgs.Empty);

                if (OnUpdateAvailable != null)
                    await OnUpdateAvailable.Invoke(response);

                return response;
            }
        }

        /// <summary>Downloads <see cref="AvailableUpdate"/>, checks it, and extracts it to the staging directory.</summary>
        /// <param name="progress">Called with the bytes received and the total, when the server reports one.</param>
        public async Task DownloadAsync(Action<DownloadProgressChangedEventArgs>? progress = null, CancellationToken cancellationToken = default)
        {
            if (Installation == null)
                throw new NotSupportedException("This launcher can't update itself");

            var update = AvailableUpdate ?? throw new InvalidOperationException("No update is available");

            using var op = Logger.BeginOperation("Downloading launcher update");

            op.Enrich("Version", update.Version);

            Directory.CreateDirectory(JournalDirectory);

            var extension = Installation.PackageKind == UpdatePackageKind.AppImage ? "AppImage" : "zip";
            var packagePath = Path.Combine(JournalDirectory, $"{update.Version}.{extension}");

            try
            {
                await launcherClient.DownloadAsync(packagePath, Installation.PackageKind, progress, cancellationToken);

                if (!await UpdatePackage.VerifyAsync(packagePath, update.Sha256, cancellationToken))
                    throw new InvalidDataException("The downloaded update doesn't match the server's checksum");

                var journal = SelfUpdater.Begin(JournalDirectory, update.Version, Installation, Logger);

                journal.PreviousVersion = GetCurrentVersion().ToString();

                UpdatePackage.Stage(journal, packagePath, Installation.PackageKind);
                journal.Save();

                StagedVersion = update.Version;

                Logger?.LogInformation("Launcher update {Version} is ready to install", update.Version);

                op.Complete();
            }
            finally
            {
                TryDelete(packagePath);

                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Run while starting. Rolls back an update that was cut short and picks up a staged update and
        /// the reason an earlier update was undone. Returns true when the launcher should restart, since
        /// the files it started from may be a mix of two versions.
        /// </summary>
        public bool Recover()
        {
            var restart = SelfUpdater.Recover(JournalDirectory, Logger) == UpdateRecoveryAction.RestartRequired;

            if (UpdateResult.Take(JournalDirectory) is { } result)
                _notice = new UpdateNotice(result.Version, result.PreviousVersion, result.Error);

            // Downloaded earlier, then not installed, or declined at the administrator prompt
            if (UpdateJournal.Load(JournalDirectory) is { State: UpdateState.Staged } journal && IsNewer(journal.Version))
                StagedVersion = journal.Version;

            return restart;
        }

        /// <summary>
        /// Run once the launcher has started far enough to be considered healthy. When this launcher was
        /// just installed by an update, tells the previous one it worked, then deletes the files it
        /// replaced once that one has exited.
        /// </summary>
        public void ConfirmStartup()
        {
            try
            {
                if (SelfUpdater.ConfirmStartup(JournalDirectory, Logger) is { } journal)
                    _notice = new UpdateNotice(journal.Version, journal.PreviousVersion, null);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await SelfUpdater.FinishAsync(JournalDirectory, Logger);
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogError(ex, "Could not finish the launcher update");
                    }
                });
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Could not confirm the launcher update");
            }
        }

        /// <summary>Returns, once, what to tell the user about the last update, if anything.</summary>
        public UpdateNotice? TakeNotice()
        {
            var notice = _notice;

            _notice = null;

            return notice;
        }

        public static SemVersion GetCurrentVersion() => VersionHelper.GetCurrentVersion();

        private static bool IsNewer(string? version) =>
            SemVersion.TryParse(version, SemVersionStyles.Any, out var parsed)
            && parsed.ComparePrecedenceTo(GetCurrentVersion()) > 0;

        private void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                Logger?.LogDebug(ex, "Could not delete {Path}", path);
            }
        }
    }
}
