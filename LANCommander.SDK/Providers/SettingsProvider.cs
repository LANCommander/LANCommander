using System;
using System.IO;
using System.Threading.Tasks;
using LANCommander.SDK;
using LANCommander.SDK.Abstractions;
using Microsoft.Extensions.Options;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Settings = LANCommander.SDK.Models.Settings;

public class SettingsProvider<TSettings> : ISettingsProvider
    where TSettings : Settings, new()
{
    private readonly string _filePath;
    private readonly IOptionsMonitor<TSettings> _optionsMonitor;

    public TSettings CurrentValue => _optionsMonitor.CurrentValue;

    Settings ISettingsProvider.CurrentValue => _optionsMonitor.CurrentValue; // upcast

    public SettingsProvider(IOptionsMonitor<TSettings> optionsMonitor)
    {
        _filePath = Path.Join(AppPaths.GetConfigDirectory(), Settings.SETTINGS_FILE_NAME);

        if (!File.Exists(_filePath))
        {
            var template = new TSettings();
            Save(template);
        }

        _optionsMonitor = optionsMonitor;
    }

    public async Task UpdateAsync(TSettings settings) => await SaveAsync(settings);

    public async Task UpdateAsync(Action<TSettings> patch)
    {
        patch.Invoke(_optionsMonitor.CurrentValue);
        await SaveAsync(_optionsMonitor.CurrentValue);
    }

    // ISettingsProvider (non-generic) explicit impls
    async Task ISettingsProvider.UpdateAsync(Settings settings)
    {
        // Allow callers who only know about base Settings to update
        if (settings is TSettings typed)
        {
            await UpdateAsync(typed);
        }
        else
        {
            var current = _optionsMonitor.CurrentValue;
            
            await UpdateAsync(current);
        }
    }

    async Task ISettingsProvider.UpdateAsync(Action<Settings> patch)
    {
        // Patch the current derived instance through the base type view
        var current = _optionsMonitor.CurrentValue;
        patch(current);
        await UpdateAsync(current);
    }

    private void Save(TSettings settings)
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .Build();

        File.WriteAllText(_filePath, serializer.Serialize(settings));
    }

    private async Task SaveAsync(TSettings settings)
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .Build();

        await File.WriteAllTextAsync(_filePath, serializer.Serialize(settings));
    }
}
