using System.Security.Cryptography;
using LANCommander.PluginManagement;
using LANCommander.SDK.Plugins;
using LANCommander.Server.Endpoints;
using LANCommander.Server.Plugins.Installation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using NuGet.Frameworks;
using Shouldly;
using ServerSettings = LANCommander.Server.Settings.Settings;
using static LANCommander.Server.Tests.Plugins.PluginPackageTests;

namespace LANCommander.Server.Tests.Plugins;

[Collection("Application")]
public class LauncherPluginCacheTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"lc-launcher-plugins-{Guid.NewGuid():N}");
    private readonly ServerSettings _settings = new();
    private readonly LauncherPluginCache _cache;

    public LauncherPluginCacheTests()
    {
        Directory.CreateDirectory(_root);

        var packages = new PluginPackageService(
            new SettingsProvider<ServerSettings>(new StubOptionsMonitor<ServerSettings>(_settings), Path.Combine(_root, "Settings.yml")),
            new PluginLoaderService(),
            new PluginStaging(Path.Combine(_root, "Plugins"), Path.Combine(_root, "Plugins", PluginStaging.StagingFolderName)),
            NullLogger<PluginPackageService>.Instance,
            "2.2.0",
            NuGetFramework.Parse("net10.0"));

        _cache = new LauncherPluginCache(Path.Combine(_root, "LauncherPlugins"), packages, NullLogger<LauncherPluginCache>.Instance, NuGetFramework.Parse("net10.0"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    [Fact]
    public async Task AnUploadedLauncherPluginIsKeptAsItIs()
    {
        using var package = LauncherPackage("Acme.Overlay", "1.0.0");
        var bytes = package.ToArray();

        var plugin = await _cache.AddAsync(package);

        plugin.Id.ShouldBe("Acme.Overlay");
        plugin.PluginId.ShouldBe("com.acme.overlay");
        plugin.Required.ShouldBeFalse();
        plugin.Size.ShouldBe(bytes.Length);
        plugin.Sha512.ShouldBe(Convert.ToBase64String(SHA512.HashData(bytes)));
        plugin.Package.SourceName.ShouldBeNull();

        _cache.GetAll().ShouldHaveSingleItem().Version.ShouldBe("1.0.0");

        await using var stored = _cache.OpenRead("acme.overlay", "1.0.0").ShouldNotBeNull();
        using var copy = new MemoryStream();
        await stored.CopyToAsync(copy);
        copy.ToArray().ShouldBe(bytes);

        // Nothing is installed on the server
        Directory.Exists(Path.Combine(_root, "Plugins")).ShouldBeFalse();
    }

    [Fact]
    public async Task ServerPluginsAreNotOfferedToLaunchers()
    {
        using var package = PluginPackageFor("Acme.Widgets", "1.0.0", PluginHost.Server);

        (await Should.ThrowAsync<PluginPackageException>(() => _cache.AddAsync(package)))
            .Message.ShouldContain("does not run in the launcher");

        _cache.GetAll().ShouldBeEmpty();
    }

    [Fact]
    public async Task ANewVersionReplacesTheOldAndStaysRequired()
    {
        using (var first = LauncherPackage("Acme.Overlay", "1.0.0"))
            await _cache.AddAsync(first);

        await _cache.SetRequiredAsync("Acme.Overlay", true);

        using (var second = LauncherPackage("Acme.Overlay", "1.1.0"))
            await _cache.AddAsync(second);

        var plugin = _cache.GetAll().ShouldHaveSingleItem();
        plugin.Version.ShouldBe("1.1.0");
        plugin.Required.ShouldBeTrue();

        _cache.OpenRead("Acme.Overlay", "1.0.0").ShouldBeNull();
        Directory.GetFiles(Path.Combine(_root, "LauncherPlugins", "acme.overlay"), "*.nupkg").ShouldHaveSingleItem();
    }

    [Fact]
    public async Task RemovingStopsOfferingIt()
    {
        using (var package = LauncherPackage("Acme.Overlay", "1.0.0"))
            await _cache.AddAsync(package);

        await _cache.RemoveAsync("Acme.Overlay");

        _cache.GetAll().ShouldBeEmpty();
        _cache.OpenRead("Acme.Overlay", "1.0.0").ShouldBeNull();
    }

    [Theory]
    [InlineData("..")]
    [InlineData("../Plugins")]
    [InlineData("a\\b")]
    public async Task IdsThatAreNotPackageIdsReachNothing(string id)
    {
        _cache.Get(id).ShouldBeNull();
        _cache.OpenRead(id, "1.0.0").ShouldBeNull();

        await Should.ThrowAsync<PluginPackageException>(() => _cache.SetRequiredAsync(id, true));
    }

    [Fact]
    public async Task SourcesAreSearchedForLauncherPluginsOnly()
    {
        var feed = Path.Combine(_root, "Feed");
        Directory.CreateDirectory(feed);

        using (var launcher = LauncherPackage("Acme.Overlay", "1.0.0"))
            await File.WriteAllBytesAsync(Path.Combine(feed, "Acme.Overlay.1.0.0.nupkg"), launcher.ToArray());

        using (var server = PluginPackageFor("Acme.Widgets", "1.0.0", PluginHost.Server))
            await File.WriteAllBytesAsync(Path.Combine(feed, "Acme.Widgets.1.0.0.nupkg"), server.ToArray());

        _settings.Server.Plugins.UseNuGetOrg = false;
        _settings.Server.Plugins.Sources = [new() { Name = "Local", Url = feed }];

        var listing = (await _cache.SearchAsync("Acme")).Packages.ShouldHaveSingleItem();
        listing.Id.ShouldBe("Acme.Overlay");

        var plugin = await _cache.AddAsync(feed, listing.Id, listing.Version);
        plugin.Package.SourceName.ShouldBe("Local");

        (await _cache.CheckForUpdatesAsync()).ShouldBeEmpty();

        using (var update = LauncherPackage("Acme.Overlay", "1.2.0"))
            await File.WriteAllBytesAsync(Path.Combine(feed, "Acme.Overlay.1.2.0.nupkg"), update.ToArray());

        (await _cache.CheckForUpdatesAsync()).ShouldHaveSingleItem().Version.ShouldBe("1.2.0");
    }

    [Fact]
    public async Task TheRegistryListsAndServesCachedPackages()
    {
        using (var package = LauncherPackage("Acme.Overlay", "1.0.0"))
            await _cache.AddAsync(package);

        await _cache.SetRequiredAsync("Acme.Overlay", true);

        var listed = LauncherPluginEndpoints.List(_cache)
            .ShouldBeOfType<Ok<IEnumerable<SDK.Models.LauncherPluginPackage>>>().Value.ShouldNotBeNull().ShouldHaveSingleItem();

        listed.Id.ShouldBe("Acme.Overlay");
        listed.Required.ShouldBeTrue();
        listed.DownloadPath.ShouldBe("/api/Launcher/Plugins/acme.overlay/1.0.0/acme.overlay.1.0.0.nupkg");

        var download = LauncherPluginEndpoints.Download("acme.overlay", "1.0.0", "acme.overlay.1.0.0.nupkg", _cache)
            .ShouldBeOfType<FileStreamHttpResult>();

        await download.FileStream.DisposeAsync();

        LauncherPluginEndpoints.Download("acme.overlay", "0.9.0", "acme.overlay.0.9.0.nupkg", _cache).ShouldBeOfType<NotFound>();
        LauncherPluginEndpoints.Download("acme.overlay", "1.0.0", "package.json", _cache).ShouldBeOfType<NotFound>();
        LauncherPluginEndpoints.Versions("Acme.Overlay", _cache).ShouldNotBeOfType<NotFound>();
        LauncherPluginEndpoints.Versions("Acme.Missing", _cache).ShouldBeOfType<NotFound>();
    }

    private static MemoryStream LauncherPackage(string id, string version) => PluginPackageFor(id, version, PluginHost.Launcher);

    private static MemoryStream PluginPackageFor(string id, string version, PluginHost hosts) =>
        Package(id, version, new()
        {
            [$"lib/net10.0/{id}.dll"] = PluginAssembly(id, $"""Id = "com.{id.ToLowerInvariant()}", Hosts = PluginHost.{hosts}"""),
        }, PluginPackageTypes.All(hosts).ToArray());

    private sealed class StubOptionsMonitor<T>(T value) : Microsoft.Extensions.Options.IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
