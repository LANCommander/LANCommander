using LANCommander.SDK;
using LANCommander.SDK.Plugins;
using Microsoft.Extensions.Logging;

namespace LANCommander.PluginManagement;

/// <summary>
/// Plugin installs and removals waiting for a restart. A loaded plugin stays loaded, with its files
/// locked, until the host stops, so changes to the plugins folder are kept here and made at the
/// next start, before plugins are discovered.
/// </summary>
/// <remarks>
/// <c>Install/&lt;folder&gt;</c> holds a plugin folder ready to move into the plugins folder, and an
/// empty <c>Remove/&lt;folder&gt;</c> marks one to delete. <c>Temp</c> is where packages are unpacked
/// while they are checked. It all sits in <c>Plugins/.staging</c>, on the plugins folder's volume, so
/// a move is a rename.
/// Installing over a plugin keeps its <see cref="PluginManifest.DataFolderName"/> folder; removing
/// one deletes it with the rest.
/// </remarks>
public sealed class PluginStaging
{
    /// <summary>Inside the plugins folder; the leading dot keeps it from being taken for a plugin.</summary>
    public const string StagingFolderName = ".staging";

    public string PluginsRoot { get; }
    public string StagingRoot { get; }

    private string InstallRoot => Path.Combine(StagingRoot, "Install");
    private string RemoveRoot => Path.Combine(StagingRoot, "Remove");
    internal string TempRoot => Path.Combine(StagingRoot, "Temp");

    public PluginStaging(string pluginsRoot, string stagingRoot)
    {
        PluginsRoot = pluginsRoot;
        StagingRoot = stagingRoot;
    }

    /// <summary>Staging for the plugins folder of the host this runs in: the server's or the launcher's.</summary>
    public static PluginStaging CreateDefault() => new(
        AppPaths.GetConfigPath(PluginBootstrap.PluginsFolderName),
        AppPaths.GetConfigPath(PluginBootstrap.PluginsFolderName, StagingFolderName));

    public string GetInstalledPath(string folder) => Path.Combine(PluginsRoot, ValidateFolder(folder));

    public string GetStagedPath(string folder) => Path.Combine(InstallRoot, ValidateFolder(folder));

    public bool IsInstalled(string folder) => Directory.Exists(GetInstalledPath(folder));

    public bool IsInstallPending(string folder) => Directory.Exists(GetStagedPath(folder));

    public bool IsRemovalPending(string folder) => File.Exists(Path.Combine(RemoveRoot, ValidateFolder(folder)));

    /// <summary>Whether anything waits for a restart.</summary>
    public bool HasPendingChanges =>
        (Directory.Exists(InstallRoot) && Directory.EnumerateDirectories(InstallRoot).Any())
        || (Directory.Exists(RemoveRoot) && Directory.EnumerateFiles(RemoveRoot).Any());

    public IEnumerable<string> GetInstalledFolders() => Directory.Exists(PluginsRoot)
        ? Directory.GetDirectories(PluginsRoot).Select(Path.GetFileName).OfType<string>().Where(PluginBootstrap.IsPluginFolder)
        : [];

    public IEnumerable<string> GetStagedFolders() => Directory.Exists(InstallRoot)
        ? Directory.GetDirectories(InstallRoot).Select(Path.GetFileName).OfType<string>()
        : [];

    /// <summary>
    /// Stages the plugin unpacked at <paramref name="source"/> to be installed as <paramref name="folder"/>,
    /// replacing whatever was staged or marked for removal there.
    /// </summary>
    internal void StageInstall(string source, string folder)
    {
        var target = GetStagedPath(folder);

        if (Directory.Exists(target))
            Directory.Delete(target, true);

        Directory.CreateDirectory(InstallRoot);
        MoveDirectory(source, target);

        DeleteRemovalMarker(folder);
    }

    /// <summary>Marks an installed plugin for removal and drops any install staged for it.</summary>
    /// <param name="dataTo">
    /// The folder of a staged install to hand the plugin's data to, when the plugin is being replaced
    /// by itself under a new folder name. Otherwise its data is deleted with it.
    /// </param>
    internal void StageRemoval(string folder, string? dataTo = null)
    {
        DeleteStagedInstall(folder);

        if (!IsInstalled(folder))
            return;

        Directory.CreateDirectory(RemoveRoot);
        File.WriteAllText(Path.Combine(RemoveRoot, ValidateFolder(folder)), dataTo is null ? string.Empty : ValidateFolder(dataTo));
    }

    /// <summary>Forgets whatever was staged for <paramref name="folder"/>.</summary>
    internal void CancelPending(string folder)
    {
        DeleteStagedInstall(folder);
        DeleteRemovalMarker(folder);
    }

    /// <summary>
    /// Makes the staged changes. Runs at startup, before plugins are discovered. A change that fails is
    /// logged and left staged so the next start tries again.
    /// </summary>
    public void ApplyPending(ILogger logger)
    {
        if (Directory.Exists(RemoveRoot))
        {
            foreach (var marker in Directory.GetFiles(RemoveRoot))
            {
                var folder = Path.GetFileName(marker);

                try
                {
                    var installed = GetInstalledPath(folder);

                    if (Directory.Exists(installed))
                    {
                        var dataTo = File.ReadAllText(marker).Trim();

                        if (dataTo.Length > 0 && IsInstallPending(dataTo))
                            KeepData(installed, GetStagedPath(dataTo));

                        Directory.Delete(installed, true);
                    }

                    File.Delete(marker);

                    logger.LogInformation("Removed plugin {Folder}", folder);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Could not remove plugin {Folder}", folder);
                }
            }
        }

        if (Directory.Exists(InstallRoot))
        {
            foreach (var staged in Directory.GetDirectories(InstallRoot))
            {
                var folder = Path.GetFileName(staged);

                try
                {
                    var installed = GetInstalledPath(folder);

                    if (Directory.Exists(installed))
                    {
                        // An upgrade keeps the plugin's data
                        KeepData(installed, staged);

                        Directory.Delete(installed, true);
                    }

                    Directory.CreateDirectory(PluginsRoot);
                    MoveDirectory(staged, installed);

                    logger.LogInformation("Installed plugin {Folder}", folder);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Could not install plugin {Folder}", folder);
                }
            }
        }

        try
        {
            if (Directory.Exists(TempRoot))
                Directory.Delete(TempRoot, true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not clear {TempRoot}", TempRoot);
        }
    }

    /// <summary>
    /// Moves the data folder of the plugin at <paramref name="installed"/> into the staged install at
    /// <paramref name="staged"/>, in place of any the package shipped. Done before the installed folder
    /// is deleted, so if anything after fails the data is still staged for the next start.
    /// </summary>
    private static void KeepData(string installed, string staged)
    {
        var data = Path.Combine(installed, PluginManifest.DataFolderName);

        if (!Directory.Exists(data))
            return;

        var stagedData = Path.Combine(staged, PluginManifest.DataFolderName);

        if (Directory.Exists(stagedData))
            Directory.Delete(stagedData, true);

        MoveDirectory(data, stagedData);
    }

    private void DeleteStagedInstall(string folder)
    {
        var staged = GetStagedPath(folder);

        if (Directory.Exists(staged))
            Directory.Delete(staged, true);
    }

    private void DeleteRemovalMarker(string folder)
    {
        var marker = Path.Combine(RemoveRoot, ValidateFolder(folder));

        if (File.Exists(marker))
            File.Delete(marker);
    }

    /// <summary>A rename where possible; a copy where the staging and plugins folders are on different volumes.</summary>
    private static void MoveDirectory(string source, string target)
    {
        try
        {
            Directory.Move(source, target);
        }
        catch (IOException) when (!Directory.Exists(target))
        {
            CopyDirectory(source, target);
            Directory.Delete(source, true);
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);

        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));

        foreach (var directory in Directory.GetDirectories(source))
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
    }

    /// <summary>A plugin folder is a single path segment; anything else would reach outside the plugins folder.</summary>
    private static string ValidateFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder)
            || !PluginBootstrap.IsPluginFolder(folder)
            || folder.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || folder.Contains('/') || folder.Contains('\\'))
            throw new ArgumentException($"'{folder}' is not a plugin folder name.", nameof(folder));

        return folder;
    }
}
