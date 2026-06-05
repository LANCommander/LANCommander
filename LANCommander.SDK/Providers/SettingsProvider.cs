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

    public SettingsProvider(IOptionsMonitor<TSettings> optionsMonitor)
    {
        _filePath = AppPaths.GetConfigPath(Settings.SETTINGS_FILE_NAME);

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

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // Cancel any pending debounce timer and flush immediately
        lock (_debounceLock)
        {
            _saveCts?.Cancel();
            _saveCts?.Dispose();
            _saveCts = null;
        }

        if (_hasPendingSave)
        {
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
    }
}
