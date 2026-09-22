using Microsoft.Extensions.Options;
using Settings = LANCommander.SDK.Models.Settings;

namespace LANCommander.SDK.Tests.Providers;

/// <summary>
/// Covers the immediate-flush path. Most settings tolerate the one-second debounce, but a rotating
/// credential does not: it is written once, spent immediately, and cannot be re-derived, so a crash
/// inside the debounce window would destroy it.
/// </summary>
public class SettingsProviderFlushTests : IDisposable
{
    // An explicit path per instance. The default resolves to one process-wide location, which
    // parallel test classes would fight over now that flushing writes synchronously.
    private readonly string _settingsPath = Path.Combine(
        Path.GetTempPath(), $"lc-settings-flush-{Guid.NewGuid():N}.yml");

    public void Dispose()
    {
        if (File.Exists(_settingsPath))
            File.Delete(_settingsPath);
    }

    private sealed class StubOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = value;

        public T Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private SettingsProvider<Settings> Create(Settings settings) =>
        new(new StubOptionsMonitor<Settings>(settings), _settingsPath);

    [Fact]
    public async Task FlushAsync_WritesImmediatelyWithoutWaitingOutTheDebounce()
    {
        var settings = new Settings();
        var provider = Create(settings);
        var marker = $"flush-{Guid.NewGuid():N}";

        provider.Update(s => s.Culture = marker);

        // No delay: the debounce is one second, so an unflushed write would not be on disk yet.
        await provider.FlushAsync();

        Assert.Contains(marker, await File.ReadAllTextAsync(_settingsPath));
    }

    [Fact]
    public async Task FlushAsync_WithNothingPending_DoesNotThrow()
    {
        var provider = Create(new Settings());

        await provider.FlushAsync();
        await provider.FlushAsync();
    }

    [Fact]
    public async Task FlushAsync_PersistsTheMostRecentValueWhenCalledRepeatedly()
    {
        // Mirrors a rotating credential: each save must land, not just the last one after a quiet
        // period. A flush that dropped intermediate writes would strand the client on a spent token.
        var settings = new Settings();
        var provider = Create(settings);

        for (var i = 0; i < 3; i++)
        {
            var marker = $"rotation-{i}";

            provider.Update(s => s.Culture = marker);

            await provider.FlushAsync();

            Assert.Contains(marker, await File.ReadAllTextAsync(_settingsPath));
        }
    }

    [Fact]
    public async Task StopAsync_StillFlushesPendingChanges()
    {
        // StopAsync now delegates to FlushAsync; graceful shutdown must keep working.
        var settings = new Settings();
        var provider = Create(settings);
        var marker = $"shutdown-{Guid.NewGuid():N}";

        provider.Update(s => s.Culture = marker);

        await provider.StopAsync(CancellationToken.None);

        Assert.Contains(marker, await File.ReadAllTextAsync(_settingsPath));
    }
}
