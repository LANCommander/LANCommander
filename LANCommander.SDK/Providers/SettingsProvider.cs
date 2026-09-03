using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Factories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Settings = LANCommander.SDK.Models.Settings;

public class SettingsProvider<TSettings> : ISettingsProvider, IHostedService
    where TSettings : Settings, new()
{
    public const string FileName = "Settings.yaml";

    private readonly string _filePath;
    private readonly IOptionsMonitor<TSettings> _optionsMonitor;

    private readonly TimeSpan _debounceDelay = TimeSpan.FromMilliseconds(1000);

    private readonly SemaphoreSlim _ioGate = new(1, 1);
    private readonly object _debounceLock = new();

    private CancellationTokenSource? _saveCts;
    private volatile bool _hasPendingSave;

    public TSettings CurrentValue => _optionsMonitor.CurrentValue;

    Settings ISettingsProvider.CurrentValue => _optionsMonitor.CurrentValue; // upcast

    /// <param name="optionsMonitor">Backing options, which own the in-memory settings instance.</param>
    /// <param name="filePath">
    /// Where to persist. Defaults to the application config directory; supply a path to isolate an
    /// instance, which tests need because the default resolves to one process-wide location.
    /// </param>
    public SettingsProvider(IOptionsMonitor<TSettings> optionsMonitor, string? filePath = null)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? AppPaths.GetConfigPath(Settings.SETTINGS_FILE_NAME)
            : filePath;

        _optionsMonitor = optionsMonitor;
    }

    public void Update(Action<TSettings> mutator)
    {
        mutator.Invoke(_optionsMonitor.CurrentValue);

        ScheduleSave();
    }

    void ISettingsProvider.Update(Action<Settings> mutator)
    {
        mutator.Invoke(_optionsMonitor.CurrentValue);

        ScheduleSave();
    }

    private void ScheduleSave()
    {
        _hasPendingSave = true;
        CancellationTokenSource? ctsToStart;

        lock (_debounceLock)
        {
            _saveCts?.Cancel();
            _saveCts?.Dispose();
            _saveCts = new CancellationTokenSource();
            ctsToStart = _saveCts;
        }

        _ = DebouncedSaveAsync(ctsToStart!.Token);
    }

    private async Task DebouncedSaveAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(_debounceDelay, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await _ioGate.WaitAsync(token).ConfigureAwait(false);

        try
        {
            await SaveAsync(CurrentValue, token).ConfigureAwait(false);
            _hasPendingSave = false;
        }
        finally
        {
            _ioGate.Release();
        }
    }

    private async Task SaveAsync(TSettings settings, CancellationToken ct)
    {
        var serializer = YamlSerializerFactory.Create();
        var serialization = serializer.Serialize(settings);

        await File.WriteAllTextAsync(_filePath, serialization, ct);
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Writes pending changes to disk immediately instead of waiting out the debounce.
    /// </summary>
    /// <remarks>
    /// Most settings tolerate the debounce fine — losing a second of a half-typed form is nothing.
    /// Some do not: a rotating credential that is written here and then lost to a crash before the
    /// debounce elapses cannot be recovered. Callers holding that kind of value should flush.
    /// </remarks>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        // Cancel any pending debounce timer so it can't write again behind this flush.
        lock (_debounceLock)
        {
            _saveCts?.Cancel();
            _saveCts?.Dispose();
            _saveCts = null;
        }

        if (!_hasPendingSave)
            return;

        await _ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await SaveAsync(CurrentValue, cancellationToken).ConfigureAwait(false);
            _hasPendingSave = false;
        }
        finally
        {
            _ioGate.Release();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => FlushAsync(cancellationToken);
}
