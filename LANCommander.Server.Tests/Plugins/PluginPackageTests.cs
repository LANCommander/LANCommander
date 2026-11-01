using System.IO.Compression;
using System.Text;
using LANCommander.PluginManagement;
using LANCommander.SDK.Plugins;
using LANCommander.Server.Plugins.Installation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NuGet.Frameworks;
using Shouldly;
using ServerSettings = LANCommander.Server.Settings.Settings;

namespace LANCommander.Server.Tests.Plugins;

[Collection("Application")]
public class PluginPackageTests : IDisposable
{
    private const string HostVersion = "2.2.0";

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"lc-plugin-packages-{Guid.NewGuid():N}");
    private readonly ServerSettings _settings = new();
    private readonly PluginStaging _staging;
    private readonly PluginPackageService _service;

    public PluginPackageTests()
    {
        Directory.CreateDirectory(_root);

        _staging = new PluginStaging(Path.Combine(_root, "Plugins"), Path.Combine(_root, "Plugins", PluginStaging.StagingFolderName));

        _service = CreateService(HostVersion);
    }

    private PluginPackageService CreateService(string hostVersion) => new(
        new SettingsProvider<ServerSettings>(new StubOptionsMonitor<ServerSettings>(_settings), Path.Combine(_root, "Settings.yml")),
        new PluginLoaderService(),
        _staging,
        NullLogger<PluginPackageService>.Instance,
        hostVersion,
        NuGetFramework.Parse("net10.0"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    [Fact]
    public void InspectorReadsThePluginAttribute()
    {
        var path = WriteAssembly("Acme.Widgets", """Id = "com.acme.widgets", MinHostVersion = "2.0.0", Hosts = PluginHost.Server""");

        var info = PluginAssemblyInspector.Inspect(path);

        info.ShouldNotBeNull();
        info.AssemblyName.ShouldBe("Acme.Widgets");
        info.Id.ShouldBe("com.acme.widgets");
        info.MinHostVersion.ShouldBe("2.0.0");
        info.MaxHostVersion.ShouldBeNull();
        info.Hosts.ShouldBe(PluginHost.Server);
    }

    [Fact]
    public void InspectorFallsBackToTheEntryPointsFullName()
    {
        var info = PluginAssemblyInspector.Inspect(WriteAssembly("Acme.Widgets", null));

        info.ShouldNotBeNull();
        info.Id.ShouldBe("Acme.Widgets.Entry");
        info.Hosts.ShouldBe(PluginHost.Server | PluginHost.Launcher);
    }

    [Fact]
    public void InspectorIgnoresAssembliesThatAreNotPlugins()
    {
        var library = Path.Combine(_root, "Library.dll");
        File.WriteAllBytes(library, Compile("Library", "namespace Library { public class Thing { } }"));

        var native = Path.Combine(_root, "native.dll");
        File.WriteAllBytes(native, [0x4D, 0x5A, 0x00, 0x01, 0x02]);

        PluginAssemblyInspector.Inspect(library).ShouldBeNull();
        PluginAssemblyInspector.Inspect(native).ShouldBeNull();
    }

    [Fact]
    public async Task UploadStagesThePluginFolderUntilRestart()
    {
        using var package = Package("Acme.Widgets.Plugin", "1.2.0", new()
        {
            ["lib/net10.0/Acme.Widgets.dll"] = PluginAssembly("Acme.Widgets", """Id = "com.acme.widgets", Hosts = PluginHost.Server"""),
            ["lib/net10.0/Acme.Helpers.dll"] = Compile("Acme.Helpers", "namespace Acme.Helpers { public class Helper { } }"),
            ["lib/net10.0/Templates/page.html"] = "<p></p>"u8.ToArray(),
        });

        var result = await _service.InstallAsync(package);

        result.Folder.ShouldBe("Acme.Widgets");
        result.Package.Id.ShouldBe("Acme.Widgets.Plugin");
        result.Package.Version.ShouldBe("1.2.0");

        _service.RestartRequired.ShouldBeTrue();
        _staging.IsInstalled("Acme.Widgets").ShouldBeFalse();

        var plugin = _service.GetInstalled().ShouldHaveSingleItem();
        plugin.State.ShouldBe(PluginInstallState.InstallPending);
        plugin.PendingPackage!.Id.ShouldBe("Acme.Widgets.Plugin");

        _staging.ApplyPending(NullLogger.Instance);

        var installed = _staging.GetInstalledPath("Acme.Widgets");
        File.Exists(Path.Combine(installed, "Acme.Widgets.dll")).ShouldBeTrue();
        File.Exists(Path.Combine(installed, "Acme.Helpers.dll")).ShouldBeTrue();
        File.Exists(Path.Combine(installed, "Templates", "page.html")).ShouldBeTrue();
        PluginPackageRecord.Read(installed)!.Version.ShouldBe("1.2.0");

        _service.RestartRequired.ShouldBeFalse();
        _service.GetInstalled().ShouldHaveSingleItem().State.ShouldBe(PluginInstallState.NotLoaded);
    }

    [Fact]
    public async Task InstallingOverAnInstalledVersionIsAnUpdate()
    {
        await InstallAndApplyAsync("Acme.Widgets.Plugin", "1.0.0", "Acme.Widgets");

        using var update = PluginPackage("Acme.Widgets.Plugin", "1.1.0", "Acme.Widgets");
        await _service.InstallAsync(update);

        var plugin = _service.GetInstalled().ShouldHaveSingleItem();
        plugin.State.ShouldBe(PluginInstallState.UpdatePending);
        plugin.Package!.Version.ShouldBe("1.0.0");
        plugin.PendingPackage!.Version.ShouldBe("1.1.0");

        _staging.ApplyPending(NullLogger.Instance);

        PluginPackageRecord.Read(_staging.GetInstalledPath("Acme.Widgets"))!.Version.ShouldBe("1.1.0");
    }

    [Fact]
    public async Task TheStagingFolderIsNotAPlugin()
    {
        await InstallAndApplyAsync("Acme.Widgets.Plugin", "1.0.0", "Acme.Widgets");

        using var update = PluginPackage("Acme.Widgets.Plugin", "1.1.0", "Acme.Widgets");
        await _service.InstallAsync(update);

        Directory.Exists(Path.Combine(_root, "Plugins", PluginStaging.StagingFolderName)).ShouldBeTrue();
        _staging.GetInstalledFolders().ShouldBe(["Acme.Widgets"]);
        _service.GetInstalled().ShouldHaveSingleItem().Folder.ShouldBe("Acme.Widgets");
    }

    [Fact]
    public async Task AnUpdateKeepsThePluginsData()
    {
        await InstallAndApplyAsync("Acme.Widgets.Plugin", "1.0.0", "Acme.Widgets");
        var settings = WriteData("Acme.Widgets");

        using var update = PluginPackage("Acme.Widgets.Plugin", "1.1.0", "Acme.Widgets");
        await _service.InstallAsync(update);
        _staging.ApplyPending(NullLogger.Instance);

        PluginPackageRecord.Read(_staging.GetInstalledPath("Acme.Widgets"))!.Version.ShouldBe("1.1.0");
        File.ReadAllText(settings).ShouldBe("kept");
    }

    [Fact]
    public async Task AnUpdateThatRenamesTheAssemblyTakesTheDataAlong()
    {
        await InstallAndApplyAsync("Acme.Widgets.Plugin", "1.0.0", "Acme.Widgets");
        WriteData("Acme.Widgets");

        using var update = PluginPackage("Acme.Widgets.Plugin", "2.0.0", "Acme.Widgets.Server");
        await _service.InstallAsync(update);
        _staging.ApplyPending(NullLogger.Instance);

        _staging.IsInstalled("Acme.Widgets").ShouldBeFalse();
        File.ReadAllText(DataFile("Acme.Widgets.Server")).ShouldBe("kept");
    }

    [Fact]
    public async Task UninstallDeletesThePluginsData()
    {
        await InstallAndApplyAsync("Acme.Widgets.Plugin", "1.0.0", "Acme.Widgets");
        var settings = WriteData("Acme.Widgets");

        await _service.UninstallAsync("Acme.Widgets");
        _staging.ApplyPending(NullLogger.Instance);

        File.Exists(settings).ShouldBeFalse();
    }

    private string DataFile(string folder) =>
        Path.Combine(_staging.GetInstalledPath(folder), PluginManifest.DataFolderName, "Nested", "Settings.yaml");

    private string WriteData(string folder)
    {
        var path = DataFile(folder);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "kept");

        return path;
    }

    [Fact]
    public async Task AnUpdateThatRenamesTheAssemblyRemovesTheOldFolder()
    {
        await InstallAndApplyAsync("Acme.Widgets.Plugin", "1.0.0", "Acme.Widgets");

        using var update = PluginPackage("Acme.Widgets.Plugin", "2.0.0", "Acme.Widgets.Server");
        await _service.InstallAsync(update);

        _staging.IsRemovalPending("Acme.Widgets").ShouldBeTrue();

        _staging.ApplyPending(NullLogger.Instance);

        _staging.IsInstalled("Acme.Widgets").ShouldBeFalse();
        _staging.IsInstalled("Acme.Widgets.Server").ShouldBeTrue();
    }

    [Fact]
    public async Task UninstallRemovesThePluginAtRestart()
    {
        await InstallAndApplyAsync("Acme.Widgets.Plugin", "1.0.0", "Acme.Widgets");

        await _service.UninstallAsync("Acme.Widgets");

        _service.GetInstalled().ShouldHaveSingleItem().State.ShouldBe(PluginInstallState.RemovalPending);
        _staging.IsInstalled("Acme.Widgets").ShouldBeTrue();

        _staging.ApplyPending(NullLogger.Instance);

        _staging.IsInstalled("Acme.Widgets").ShouldBeFalse();
        _service.GetInstalled().ShouldBeEmpty();
    }

    [Fact]
    public async Task UninstallingAStagedPluginDropsItImmediately()
    {
        using var package = PluginPackage("Acme.Widgets.Plugin", "1.0.0", "Acme.Widgets");
        await _service.InstallAsync(package);

        await _service.UninstallAsync("Acme.Widgets");

        _service.GetInstalled().ShouldBeEmpty();
        _service.RestartRequired.ShouldBeFalse();
    }

    [Fact]
    public async Task CancellingKeepsTheInstalledVersion()
    {
        await InstallAndApplyAsync("Acme.Widgets.Plugin", "1.0.0", "Acme.Widgets");

        using var update = PluginPackage("Acme.Widgets.Plugin", "1.1.0", "Acme.Widgets");
        await _service.InstallAsync(update);
        await _service.CancelPendingAsync("Acme.Widgets");

        _service.RestartRequired.ShouldBeFalse();
        _service.GetInstalled().ShouldHaveSingleItem().Package!.Version.ShouldBe("1.0.0");
    }

    [Fact]
    public async Task ReplacesAPluginCopiedInByHand()
    {
        var manual = _staging.GetInstalledPath("Acme.Widgets");
        Directory.CreateDirectory(manual);
        File.WriteAllBytes(Path.Combine(manual, "Acme.Widgets.dll"), PluginAssembly("Acme.Widgets", """Id = "com.acme.widgets", Hosts = PluginHost.Server"""));

        using var package = PluginPackage("Acme.Widgets.Plugin", "1.0.0", "Acme.Widgets");
        var result = await _service.InstallAsync(package);

        result.Folder.ShouldBe("Acme.Widgets");
        _service.GetInstalled().ShouldHaveSingleItem().State.ShouldBe(PluginInstallState.UpdatePending);
    }

    [Fact]
    public async Task RejectsAFolderOwnedByAnotherPackage()
    {
        await InstallAndApplyAsync("Acme.Widgets.Plugin", "1.0.0", "Acme.Widgets");

        using var other = PluginPackage("Someone.Else", "1.0.0", "Acme.Widgets", pluginId: "com.someone.else");

        var ex = await Should.ThrowAsync<PluginPackageException>(() => _service.InstallAsync(other));
        ex.Message.ShouldContain("Acme.Widgets.Plugin");
    }

    [Fact]
    public async Task RejectsASecondCopyOfTheSamePlugin()
    {
        await InstallAndApplyAsync("Acme.Widgets.Plugin", "1.0.0", "Acme.Widgets");

        using var copy = PluginPackage("Acme.Widgets.Fork", "1.0.0", "Acme.Widgets.Fork");

        var ex = await Should.ThrowAsync<PluginPackageException>(() => _service.InstallAsync(copy));
        ex.Message.ShouldContain("com.acme.widgets");
    }

    [Fact]
    public async Task RejectsLauncherPlugins()
    {
        using var package = Package("Acme.Launcher", "1.0.0", new()
        {
            ["lib/net10.0/Acme.Launcher.dll"] = PluginAssembly("Acme.Launcher", "Hosts = PluginHost.Launcher"),
        });

        var ex = await Should.ThrowAsync<PluginPackageException>(() => _service.InstallAsync(package));
        ex.Message.ShouldContain("launcher");
        _service.RestartRequired.ShouldBeFalse();
    }

    [Fact]
    public async Task RejectsPluginsForAnotherHostVersion()
    {
        using var package = Package("Acme.Future", "1.0.0", new()
        {
            ["lib/net10.0/Acme.Future.dll"] = PluginAssembly("Acme.Future", """MinHostVersion = "99.0.0" """),
        });

        var ex = await Should.ThrowAsync<PluginPackageException>(() => _service.InstallAsync(package));
        ex.Message.ShouldContain("99.0.0");
    }

    [Fact]
    public async Task RejectsPackagesWithoutAPlugin()
    {
        using var package = Package("Acme.Library", "1.0.0", new()
        {
            ["lib/net10.0/Acme.Library.dll"] = Compile("Acme.Library", "namespace Acme.Library { public class Thing { } }"),
        });

        var ex = await Should.ThrowAsync<PluginPackageException>(() => _service.InstallAsync(package));
        ex.Message.ShouldContain("[LANCommanderPlugin]");
    }

    [Fact]
    public async Task RejectsPackagesWithoutAssembliesForTheServer()
    {
        using var package = Package("Acme.Old", "1.0.0", new()
        {
            ["lib/net48/Acme.Old.dll"] = PluginAssembly("Acme.Old", null),
        });

        await Should.ThrowAsync<PluginPackageException>(() => _service.InstallAsync(package));
    }

    [Fact]
    public async Task RejectsFilesThatAreNotPackages()
    {
        using var stream = new MemoryStream("not a zip"u8.ToArray());

        await Should.ThrowAsync<PluginPackageException>(() => _service.InstallAsync(stream));
    }

    [Fact]
    public async Task PicksTheNearestCompatibleFramework()
    {
        using var package = Package("Acme.Widgets.Plugin", "1.0.0", new()
        {
            ["lib/net8.0/Acme.Widgets.dll"] = PluginAssembly("Acme.Widgets", null),
            ["lib/net8.0/Acme.Net8.txt"] = [],
            ["lib/netstandard2.0/Acme.Widgets.dll"] = PluginAssembly("Acme.Widgets", null),
            ["lib/netstandard2.0/Acme.NetStandard.txt"] = [],
        });

        await _service.InstallAsync(package);

        var staged = _staging.GetStagedPath("Acme.Widgets");
        File.Exists(Path.Combine(staged, "Acme.Net8.txt")).ShouldBeTrue();
        File.Exists(Path.Combine(staged, "Acme.NetStandard.txt")).ShouldBeFalse();
    }

    [Fact]
    public async Task InstallsAndUpdatesFromAPackageSource()
    {
        var feed = Path.Combine(_root, "Feed");
        Directory.CreateDirectory(feed);

        foreach (var version in new[] { "1.0.0", "1.1.0" })
        {
            using var package = PluginPackage("Acme.Widgets.Plugin", version, "Acme.Widgets");
            await File.WriteAllBytesAsync(Path.Combine(feed, $"Acme.Widgets.Plugin.{version}.nupkg"), package.ToArray());
        }

        _settings.Server.Plugins.UseNuGetOrg = false;
        _settings.Server.Plugins.Sources = [new() { Name = "Local", Url = feed }];

        var search = await _service.SearchAsync("Widgets");

        search.Errors.ShouldBeEmpty();
        var listing = search.Packages.ShouldHaveSingleItem();
        listing.Id.ShouldBe("Acme.Widgets.Plugin");
        listing.Version.ShouldBe("1.1.0");
        listing.SourceName.ShouldBe("Local");

        var versions = await _service.GetVersionsAsync(feed, listing.Id);
        versions.ShouldBe(["1.1.0", "1.0.0"]);

        var result = await _service.InstallAsync(feed, listing.Id, "1.0.0");
        result.Package.SourceName.ShouldBe("Local");
        result.Package.SourceUrl.ShouldBe(feed);

        _staging.ApplyPending(NullLogger.Instance);

        var update = (await _service.CheckForUpdatesAsync()).ShouldHaveSingleItem();
        update.Folder.ShouldBe("Acme.Widgets");
        update.Version.ShouldBe("1.1.0");
    }

    [Theory]
    [InlineData("https://api.nuget.org/v3/index.json", "LANCommander.Plugins.Steam", "LANCommander", true)]
    [InlineData("https://api.nuget.org/v3/index.json", "lancommander.plugins.steam", "lancommander", true)]
    [InlineData("https://api.nuget.org/v3/index.json", "LANCommander.Plugins.Steam", "pathartl", false)]
    [InlineData("https://api.nuget.org/v3/index.json", "LANCommander.Plugins.Steam", "someone-else", false)]
    [InlineData("https://api.nuget.org/v3/index.json", "LANCommander.Plugin.Steam", "LANCommander", false)]
    [InlineData("https://api.nuget.org/v3/index.json", "Acme.Plugins.Steam", "LANCommander", false)]
    [InlineData("https://feed.example.com/v3/index.json", "LANCommander.Plugins.Steam", "LANCommander", false)]
    public void OfficialPluginsAreLANCommanderPluginsOwnedByTheOrganizationOnNuGetOrg(string source, string id, string owner, bool official)
    {
        OfficialPlugins.IsOfficial(source, id, [owner]).ShouldBe(official);
    }

    [Fact]
    public void APackageWithoutOwnersIsNotOfficial()
    {
        OfficialPlugins.IsOfficial(PluginPackageService.NuGetOrgUrl, "LANCommander.Plugins.Steam", null).ShouldBeFalse();
    }

    [Fact]
    public async Task OtherFeedsCannotPublishOfficialPlugins()
    {
        var feed = await FeedAsync(("LANCommander.Plugins.Widgets", "1.0.0"));

        var listing = (await _service.SearchAsync("Widgets")).Packages.ShouldHaveSingleItem();
        listing.IsOfficial.ShouldBeFalse();

        var result = await _service.InstallAsync(feed, listing.Id, listing.Version);
        result.Package.IsOfficial.ShouldBeFalse();
        _service.GetInstalled().ShouldHaveSingleItem().IsOfficial.ShouldBeFalse();
    }

    [Fact]
    public async Task UploadsAreNotOfficial()
    {
        using var package = PluginPackage("LANCommander.Plugins.Widgets", "1.0.0", "Acme.Widgets");

        (await _service.InstallAsync(package)).Package.IsOfficial.ShouldBeFalse();
    }

    [Fact]
    public async Task APrereleaseServerOffersPrereleasePlugins()
    {
        await FeedAsync(("Acme.Widgets.Plugin", "2.2.0-alpha"), ("Acme.Widgets.Plugin", "2.2.0-alpha.2"));

        var service = CreateService("2.2.0-alpha.2");

        service.ServerIsPrerelease.ShouldBeTrue();
        service.IncludePrerelease.ShouldBeTrue();
        (await service.SearchAsync("Widgets")).Packages.ShouldHaveSingleItem().Version.ShouldBe("2.2.0-alpha.2");
        (await service.GetVersionsAsync(service.GetEnabledSources()[0].Url, "Acme.Widgets.Plugin")).ShouldBe(["2.2.0-alpha.2", "2.2.0-alpha"]);
    }

    [Fact]
    public async Task AStableServerOffersPrereleasePluginsOnlyWhenAsked()
    {
        await FeedAsync(("Acme.Widgets.Plugin", "2.2.0-alpha"));

        _service.ServerIsPrerelease.ShouldBeFalse();
        (await _service.SearchAsync("Widgets")).Packages.ShouldBeEmpty();

        _settings.Server.Plugins.IncludePrerelease = true;

        (await _service.SearchAsync("Widgets")).Packages.ShouldHaveSingleItem().Version.ShouldBe("2.2.0-alpha");
    }

    [Fact]
    public async Task SearchListsOnlyServerPluginPackages()
    {
        var feed = await FeedAsync(("Acme.Widgets.Plugin", "1.0.0"));

        foreach (var (id, types) in new[]
        {
            ("Acme.Library", Array.Empty<string>()),
            ("Acme.Launcher.Plugin", new[] { PluginPackageTypes.Plugin, PluginPackageTypes.Launcher }),
        })
        {
            using var package = Package(id, "1.0.0", new() { ["lib/net10.0/readme.txt"] = [] }, types);
            await File.WriteAllBytesAsync(Path.Combine(feed, $"{id}.1.0.0.nupkg"), package.ToArray());
        }

        (await _service.SearchAsync("Acme")).Packages.Select(p => p.Id).ShouldBe(["Acme.Widgets.Plugin"]);

        // Naming a package exactly still finds it, for feeds that can't filter; installing checks it
        (await _service.SearchAsync("Acme.Library")).Packages.Select(p => p.Id).ShouldContain("Acme.Library");
    }

    /// <summary>A local feed of plugin packages, made the only source.</summary>
    private async Task<string> FeedAsync(params (string Id, string Version)[] packages)
    {
        var feed = Path.Combine(_root, "Feed");
        Directory.CreateDirectory(feed);

        foreach (var (id, version) in packages)
        {
            using var package = PluginPackage(id, version, "Acme.Widgets");
            await File.WriteAllBytesAsync(Path.Combine(feed, $"{id}.{version}.nupkg"), package.ToArray());
        }

        _settings.Server.Plugins.UseNuGetOrg = false;
        _settings.Server.Plugins.Sources = [new() { Name = "Local", Url = feed }];

        return feed;
    }

    [Fact]
    public async Task OnlyEnabledSourcesAreSearched()
    {
        _settings.Server.Plugins.UseNuGetOrg = false;
        _settings.Server.Plugins.Sources = [new() { Name = "Off", Url = Path.Combine(_root, "Missing"), Enabled = false }];

        _service.GetEnabledSources().ShouldBeEmpty();
        await Should.ThrowAsync<PluginPackageException>(() => _service.InstallAsync(Path.Combine(_root, "Missing"), "Acme", "1.0.0"));
    }

    [Theory]
    [InlineData("..")]
    [InlineData("../Plugins")]
    [InlineData("a\\b")]
    [InlineData("")]
    public void StagingRejectsPathsOutsideThePluginsFolder(string folder)
    {
        Should.Throw<ArgumentException>(() => _staging.GetInstalledPath(folder));
    }

    private async Task InstallAndApplyAsync(string packageId, string version, string assemblyName)
    {
        using var package = PluginPackage(packageId, version, assemblyName);

        await _service.InstallAsync(package);

        _staging.ApplyPending(NullLogger.Instance);
    }

    private MemoryStream PluginPackage(string packageId, string version, string assemblyName, string pluginId = "com.acme.widgets") =>
        Package(packageId, version, new()
        {
            [$"lib/net10.0/{assemblyName}.dll"] = PluginAssembly(assemblyName, $"""Id = "{pluginId}", Hosts = PluginHost.Server"""),
        });

    private string WriteAssembly(string assemblyName, string? attributeArguments)
    {
        var path = Path.Combine(_root, $"{assemblyName}.dll");
        File.WriteAllBytes(path, PluginAssembly(assemblyName, attributeArguments));

        return path;
    }

    /// <summary>An assembly marked as a plugin; the inspector reads metadata only, so the entry point needn't implement IPlugin.</summary>
    internal static byte[] PluginAssembly(string assemblyName, string? attributeArguments)
    {
        var arguments = string.IsNullOrWhiteSpace(attributeArguments) ? "" : $", {attributeArguments}";

        return Compile(assemblyName, $$"""
            using LANCommander.SDK.Plugins;

            [assembly: LANCommanderPlugin(typeof({{assemblyName}}.Entry){{arguments}})]

            namespace {{assemblyName}}
            {
                public class Entry { }
            }
            """);
    }

    private static byte[] Compile(string assemblyName, string source)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Append(typeof(LANCommanderPluginAttribute).Assembly.Location)
            .Distinct()
            .Select(path => MetadataReference.CreateFromFile(path));

        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var output = new MemoryStream();
        var result = compilation.Emit(output);

        result.Success.ShouldBeTrue(string.Join(Environment.NewLine, result.Diagnostics));

        return output.ToArray();
    }

    internal static MemoryStream Package(string id, string version, Dictionary<string, byte[]> files, string[]? packageTypes = null)
    {
        packageTypes ??= [PluginPackageTypes.Plugin, PluginPackageTypes.Server];

        var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var nuspec = archive.CreateEntry($"{id}.nuspec");

            using (var writer = new StreamWriter(nuspec.Open(), Encoding.UTF8))
            {
                writer.Write($"""
                    <?xml version="1.0" encoding="utf-8"?>
                    <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
                      <metadata>
                        <id>{id}</id>
                        <version>{version}</version>
                        <authors>Acme</authors>
                        <description>A test plugin.</description>
                        <packageTypes>
                          {string.Concat(packageTypes.Select(type => $"<packageType name=\"{type}\" />"))}
                        </packageTypes>
                      </metadata>
                    </package>
                    """);
            }

            foreach (var (path, content) in files)
            {
                using var entry = archive.CreateEntry(path).Open();
                entry.Write(content);
            }
        }

        stream.Position = 0;

        return stream;
    }

    private sealed class StubOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
