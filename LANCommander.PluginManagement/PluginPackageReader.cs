using LANCommander.SDK.Plugins;
using NuGet.Frameworks;
using NuGet.Packaging;

namespace LANCommander.PluginManagement;

/// <summary>Why a package can't be installed as a plugin; the message is shown to the administrator.</summary>
public sealed class PluginPackageException(string message) : Exception(message);

/// <summary>A plugin package unpacked into a work folder and checked, ready to stage.</summary>
public sealed record UnpackedPlugin(string Directory, PluginAssemblyInfo Plugin, PluginPackageRecord Package)
{
    /// <summary>The plugin folder: named after the main assembly, which is how the loader finds it.</summary>
    public string Folder => Plugin.AssemblyName;
}

/// <summary>
/// Unpacks plugin packages. A plugin package carries its whole drop-in folder (the plugin assembly and
/// the private dependencies it ships) under <c>lib/&lt;framework&gt;/</c>. Package dependencies are not
/// installed: what the plugin needs beyond the host's own assemblies is in the package. Its package
/// types (<see cref="PluginPackageTypes"/>) only make it findable; what the assembly declares decides
/// whether it installs.
/// </summary>
public static class PluginPackageReader
{
    /// <summary>The framework the host runs on, which picks the package's lib folder.</summary>
    public static NuGetFramework HostFramework { get; } = AppContext.TargetFrameworkName is { } name
        ? NuGetFramework.Parse(name)
        : NuGetFramework.Parse($"net{Environment.Version.Major}.{Environment.Version.Minor}");

    /// <summary>
    /// Unpacks <paramref name="package"/> into a new folder under <paramref name="workRoot"/> and checks
    /// it runs on <paramref name="host"/>, and on <paramref name="hostVersion"/> unless that is null.
    /// </summary>
    public static UnpackedPlugin Unpack(
        Stream package,
        string workRoot,
        PluginHost host,
        string? hostVersion,
        NuGetFramework hostFramework,
        string? sourceName = null,
        string? sourceUrl = null,
        bool official = false)
    {
        PackageArchiveReader reader;
        NuGet.Packaging.Core.PackageIdentity identity;

        try
        {
            reader = new PackageArchiveReader(package, leaveStreamOpen: true);
        }
        catch (InvalidDataException ex)
        {
            throw new PluginPackageException($"This is not a NuGet package: {ex.Message}");
        }

        using (reader)
        {
            try
            {
                identity = reader.GetIdentity();
            }
            catch (Exception ex) when (ex is NuGet.Packaging.Core.PackagingException or InvalidDataException or System.Xml.XmlException)
            {
                throw new PluginPackageException($"This is not a NuGet package: {ex.Message}");
            }

            var nuspec = reader.NuspecReader;

            var group = NuGetFrameworkUtility.GetNearest(reader.GetLibItems(), hostFramework, g => g.TargetFramework);

            if (group == null || !group.Items.Any())
                throw new PluginPackageException($"{identity.Id} has no assemblies for {hostFramework.GetShortFolderName()} under lib/.");

            var directory = Path.Combine(workRoot, Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(directory);

            try
            {
                Extract(reader, group, directory);

                var plugin = FindPlugin(directory, identity.Id);

                if ((plugin.Hosts & host) == 0)
                    throw new PluginPackageException(host == PluginHost.Server
                        ? $"{identity.Id} is a launcher plugin and does not run on the server."
                        : $"{identity.Id} is a server plugin and does not run in the launcher.");

                if (hostVersion != null && !PluginLoaderService.IsVersionCompatible(hostVersion, plugin.MinHostVersion, plugin.MaxHostVersion))
                    throw new PluginPackageException(
                        $"{identity.Id} {identity.Version} needs LANCommander {plugin.MinHostVersion ?? "*"} to {plugin.MaxHostVersion ?? "*"}; this {(host == PluginHost.Server ? "server" : "launcher")} is {hostVersion}.");

                var record = new PluginPackageRecord
                {
                    Id = identity.Id,
                    Version = identity.Version.ToNormalizedString(),
                    Title = NullIfEmpty(nuspec.GetTitle()),
                    Authors = NullIfEmpty(nuspec.GetAuthors()),
                    Description = NullIfEmpty(nuspec.GetDescription()),
                    ProjectUrl = NullIfEmpty(nuspec.GetProjectUrl()),
                    IconUrl = NullIfEmpty(nuspec.GetIconUrl()),
                    SourceName = sourceName,
                    SourceUrl = sourceUrl,
                    IsOfficial = official,
                    InstalledOn = DateTime.UtcNow,
                };

                record.Write(directory);

                return new UnpackedPlugin(directory, plugin, record);
            }
            catch
            {
                Directory.Delete(directory, true);
                throw;
            }
        }
    }

    private static void Extract(PackageArchiveReader reader, FrameworkSpecificGroup group, string directory)
    {
        var root = Path.GetFullPath(directory) + Path.DirectorySeparatorChar;

        foreach (var item in group.Items)
        {
            // lib/<framework>/<path within the plugin folder>
            var segments = item.Split('/');

            if (segments.Length < 3 || item.EndsWith('/'))
                continue;

            var target = Path.GetFullPath(Path.Combine(directory, Path.Combine(segments[2..])));

            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new PluginPackageException($"The package entry '{item}' points outside the plugin folder.");

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            using var source = reader.GetStream(item);
            using var destination = File.Create(target);

            source.CopyTo(destination);
        }
    }

    private static PluginAssemblyInfo FindPlugin(string directory, string packageId)
    {
        var plugins = Directory.GetFiles(directory, "*.dll")
            .Select(PluginAssemblyInspector.Inspect)
            .OfType<PluginAssemblyInfo>()
            .ToList();

        return plugins.Count switch
        {
            1 => plugins[0],
            0 => throw new PluginPackageException($"{packageId} has no assembly marked with [LANCommanderPlugin]."),
            _ => throw new PluginPackageException(
                $"{packageId} has more than one plugin assembly ({string.Join(", ", plugins.Select(p => p.AssemblyName))}); a package holds one plugin."),
        };
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
