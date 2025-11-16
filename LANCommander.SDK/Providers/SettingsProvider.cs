using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Factories;
using Microsoft.Extensions.Options;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Settings = LANCommander.SDK.Models.Settings;

public class SettingsProvider<TSettings> : ISettingsProvider
    where TSettings : Settings, new()
{
    public const string FileName = "Settings.yaml";
    
    private readonly string _filePath;
    private readonly IOptionsMonitor<TSettings> _optionsMonitor;
    
    private readonly TimeSpan _debounceDelay = TimeSpan.FromMilliseconds(1000);

    private readonly SemaphoreSlim _ioGate = new(1, 1);
    private readonly object _debounceLock = new();

    private CancellationTokenSource? _saveCts;

    public TSettings CurrentValue => _optionsMonitor.CurrentValue;

    Settings ISettingsProvider.CurrentValue => _optionsMonitor.CurrentValue; // upcast

    public SettingsProvider(IOptionsMonitor<TSettings> optionsMonitor)
    {
        _filePath = Path.Join(AppPaths.GetConfigDirectory(), Settings.SETTINGS_FILE_NAME);

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
            
        }

        await _ioGate.WaitAsync(token).ConfigureAwait(false);

        try
        {
            await SaveAsync(CurrentValue, token).ConfigureAwait(false);
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
}
