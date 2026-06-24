using LANCommander.SDK;
using Microsoft.Extensions.Options;

namespace LANCommander.Launcher.Services.Tests.Helpers;

internal sealed class TestOptionsMonitor<TSettings> : IOptionsMonitor<TSettings>
    where TSettings : class, new()
{
    public TestOptionsMonitor(TSettings? value = null) => CurrentValue = value ?? new TSettings();

    public TSettings CurrentValue { get; }

    public TSettings Get(string? name) => CurrentValue;

    public IDisposable OnChange(Action<TSettings, string?> listener) => NoopDisposable.Instance;

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();
        public void Dispose() { }
    }
}

internal static class TestSettingsProvider
{
    public static SettingsProvider<Settings.Settings> Create(Settings.Settings? settings = null)
        => new(new TestOptionsMonitor<Settings.Settings>(settings));
}
