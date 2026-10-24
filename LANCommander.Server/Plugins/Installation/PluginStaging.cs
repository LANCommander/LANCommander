using LANCommander.SDK;
using LANCommander.SDK.Plugins;

namespace LANCommander.Server.Plugins.Installation;

/// <summary>
/// Plugin installs and removals waiting for a restart. A loaded plugin stays loaded, with its files
/// locked, until the server stops, so changes to the plugins folder are kept here and made at the
/// next start, before plugins are discovered.
/// </summary>
/// <remarks>
/// <c>Install/&lt;folder&gt;</c> holds a plugin folder ready to move into the plugins folder, and an
/// empty <c>Remove/&lt;folder&gt;</c> marks one to delete. <c>Temp</c> is where packages are unpacked
/// while they are checked. Everything stays on the plugins folder's volume so a move is a rename.
/// </remarks>
public sealed class PluginStaging
{
    public const string StagingFolderName = "PluginStaging";

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

    /// <summary>Staging for the server's own plugins folder.</summary>
    public static PluginStaging CreateDefault() => new(
        AppPaths.GetConfigPath(PluginBootstrap.PluginsFolderName),
        AppPaths.GetConfigPath(StagingFolderName));

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
        ? Directory.GetDirectories(PluginsRoot).Select(Path.GetFileName).OfType<string>()
        : [];

    public IEnumerable<string> GetStagedFolders() => Directory.Exists(InstallRoot)
        ? Directory.GetDirectories(InstallRoot).Select(Path.GetFileName).OfType<string>()
        : [];

    /// <summary>A new directory under <see cref="TempRoot"/> to unpack a package into.</summary>
    internal string CreateTempDirectory()
    {
        var path = Path.Combine(TempRoot, Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(path);

        return path;
    }

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
    internal void StageRemoval(string folder)
    {
        DeleteStagedInstall(folder);

        if (!IsInstalled(folder))
            return;

        Directory.CreateDirectory(RemoveRoot);
        File.WriteAllText(Path.Combine(RemoveRoot, ValidateFolder(folder)), string.Empty);
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
                        Directory.Delete(installed, true);

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
                        Directory.Delete(installed, true);

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
            || folder is "." or ".."
            || folder.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || folder.Contains('/') || folder.Contains('\\'))
            throw new ArgumentException($"'{folder}' is not a plugin folder name.", nameof(folder));

        return folder;
    }
}
