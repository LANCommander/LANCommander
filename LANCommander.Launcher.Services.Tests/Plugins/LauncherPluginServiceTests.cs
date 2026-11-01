using System.IO.Compression;
using System.Text;
using LANCommander.Launcher.Services.Plugins;
using LANCommander.Launcher.Services.Tests.Helpers;
using LANCommander.PluginManagement;
using LANCommander.SDK.Factories;
using LANCommander.SDK.Models;
using LANCommander.SDK.Plugins;
using LANCommander.SDK.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NuGet.Frameworks;
using Shouldly;
using Xunit;

namespace LANCommander.Launcher.Services.Tests.Plugins;

/// <summary>Launcher plugin installs, over a plugins folder of their own and a faked server.</summary>
public class LauncherPluginServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"lc-launcher-plugin-service-{Guid.NewGuid():N}");
    private readonly Settings.Settings _settings = new();
    private readonly PluginStaging _staging;
    private readonly Mock<LauncherPluginClient> _client = new((ApiRequestFactory)null!);
    private readonly Dictionary<string, byte[]> _serverPackages = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<LauncherPluginPackage> _serverPlugins = [];
    private readonly LauncherPluginService _service;

    public LauncherPluginServiceTests()
    {
        Directory.CreateDirectory(_root);

        _staging = new PluginStaging(Path.Combine(_root, "Plugins"), Path.Combine(_root, "Plugins", PluginStaging.StagingFolderName));

        _client.Setup(c => c.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _serverPlugins.ToList());
        _client.Setup(c => c.DownloadAsync(It.IsAny<LauncherPluginPackage>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((LauncherPluginPackage package, string destination, CancellationToken _) =>
                File.WriteAllBytesAsync(destination, _serverPackages[$"{package.Id}/{package.Version}"]));

        _service = new LauncherPluginService(
            TestSettingsProvider.Create(_settings),
            _client.Object,
            new PluginLoaderService(),
            _staging,
            NullLogger<LauncherPluginService>.Instance,
            "2.2.0",
            NuGetFramework.Parse("net10.0"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    [Fact]
    public void OnlyTheServerIsUsedByDefault()
    {
        var settings = new Settings.PluginSettings();

        settings.UseServer.ShouldBeTrue();
        settings.UseNuGetOrg.ShouldBeFalse();
        LauncherPluginService.GetEnabledFeeds(settings).ShouldBeEmpty();

        settings.UseNuGetOrg = true;
        settings.Sources = [new() { Name = "Off", Url = "https://example.com/v3/index.json", Enabled = false }, new() { Name = "On", Url = _root }];

        LauncherPluginService.GetEnabledFeeds(settings).Select(f => f.Name).ShouldBe([PluginFeed.NuGetOrgName, "On"]);
    }

    [Fact]
    public async Task RequiredPluginsAreInstalledAndOthersAreNot()
    {
        OnServer("Acme.Overlay", "1.0.0", required: true);
        OnServer("Acme.Extras", "1.0.0", required: false);

        var staged = await _service.InstallRequiredAsync();

        staged.ShouldHaveSingleItem().Package.Id.ShouldBe("Acme.Overlay");

        var installed = _service.GetInstalled().ShouldHaveSingleItem();
        installed.State.ShouldBe(PluginInstallState.InstallPending);
        installed.EffectivePackage!.SourceUrl.ShouldBe(LauncherPluginService.ServerSourceUrl);

        // Already waiting for the restart, so nothing more to do
        (await _service.InstallRequiredAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task RequiredPluginsAreInstalledEvenWhenTheServerIsTurnedOff()
    {
        _settings.Plugins.UseServer = false;
        OnServer("Acme.Overlay", "1.0.0", required: true);

        (await _service.InstallRequiredAsync()).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task AnOlderRequiredPluginIsUpdated()
    {
        OnServer("Acme.Overlay", "1.0.0", required: true);
        await _service.InstallRequiredAsync();
        _staging.ApplyPending(NullLogger.Instance);

        OnServer("Acme.Overlay", "1.1.0", required: true);

        (await _service.InstallRequiredAsync()).ShouldHaveSingleItem().Package.Version.ShouldBe("1.1.0");
        _service.GetInstalled().ShouldHaveSingleItem().State.ShouldBe(PluginInstallState.UpdatePending);
    }

    [Fact]
    public async Task ARequiredPluginMarkedForRemovalIsKept()
    {
        OnServer("Acme.Overlay", "1.0.0", required: true);
        await _service.InstallRequiredAsync();
        _staging.ApplyPending(NullLogger.Instance);

        var plugin = _service.GetInstalled().ShouldHaveSingleItem();
        await _service.UninstallAsync(plugin.Folder);

        (await _service.InstallRequiredAsync()).ShouldBeEmpty();
        _service.GetInstalled().ShouldHaveSingleItem().State.ShouldNotBe(PluginInstallState.RemovalPending);
    }

    [Fact]
    public async Task ANewerVersionFromElsewhereIsLeftAlone()
    {
        using (var newer = PluginPackageFor("Acme.Overlay", "2.0.0", PluginHost.Launcher))
            await _service.InstallAsync(newer);

        _staging.ApplyPending(NullLogger.Instance);

        OnServer("Acme.Overlay", "1.0.0", required: true);

        (await _service.InstallRequiredAsync()).ShouldBeEmpty();
        _service.GetInstalled().ShouldHaveSingleItem().Version.ShouldBe("2.0.0");
    }

    [Fact]
    public async Task APluginThatFailsToInstallDoesNotStopTheOthers()
    {
        // The server checks packages are launcher plugins, but the launcher checks again
        _serverPlugins.Add(new LauncherPluginPackage { Id = "Acme.Widgets", Version = "1.0.0", Required = true });
        using (var server = PluginPackageFor("Acme.Widgets", "1.0.0", PluginHost.Server))
            _serverPackages["Acme.Widgets/1.0.0"] = server.ToArray();

        OnServer("Acme.Overlay", "1.0.0", required: true);

        (await _service.InstallRequiredAsync()).ShouldHaveSingleItem().Package.Id.ShouldBe("Acme.Overlay");
    }

    [Fact]
    public async Task ServerPluginsDoNotInstallInTheLauncher()
    {
        using var package = PluginPackageFor("Acme.Widgets", "1.0.0", PluginHost.Server);

        (await Should.ThrowAsync<PluginPackageException>(() => _service.InstallAsync(package)))
            .Message.ShouldContain("does not run in the launcher");
    }

    [Theory]
    [InlineData(null, "1.0.0", true)]
    [InlineData("1.0.0", "1.0.1", true)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("2.0.0", "1.0.0", false)]
    [InlineData("2.2.0-alpha", "2.2.0", true)]
    public void IsOlderComparesNuGetVersions(string? installed, string offered, bool older)
    {
        LauncherPluginService.IsOlder(installed, offered).ShouldBe(older);
    }

    private void OnServer(string id, string version, bool required)
    {
        _serverPlugins.RemoveAll(p => p.Id == id);
        _serverPlugins.Add(new LauncherPluginPackage { Id = id, Version = version, Required = required });

        using var package = PluginPackageFor(id, version, PluginHost.Launcher);
        _serverPackages[$"{id}/{version}"] = package.ToArray();
    }

    private static MemoryStream PluginPackageFor(string id, string version, PluginHost hosts) =>
        Package(id, version, PluginAssembly(id, $"""Id = "com.{id.ToLowerInvariant()}", Hosts = PluginHost.{hosts}"""), PluginPackageTypes.All(hosts));

    /// <summary>An assembly marked as a plugin; installing reads metadata only, so the entry point needn't implement IPlugin.</summary>
    private static byte[] PluginAssembly(string assemblyName, string attributeArguments)
    {
        var source = $$"""
            using LANCommander.SDK.Plugins;

            [assembly: LANCommanderPlugin(typeof({{assemblyName}}.Entry), {{attributeArguments}})]

            namespace {{assemblyName}}
            {
                public class Entry { }
            }
            """;

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

    private static MemoryStream Package(string id, string version, byte[] assembly, IEnumerable<string> packageTypes)
    {
        var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var writer = new StreamWriter(archive.CreateEntry($"{id}.nuspec").Open(), Encoding.UTF8))
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

            using var entry = archive.CreateEntry($"lib/net10.0/{id}.dll").Open();
            entry.Write(assembly);
        }

        stream.Position = 0;

        return stream;
    }
}
