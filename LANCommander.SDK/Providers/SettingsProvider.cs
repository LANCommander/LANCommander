using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Settings = LANCommander.SDK.Models.Settings;

namespace LANCommander.SDK.Providers;

public class SettingsProvider<TSettings> where TSettings : Settings
{
    private readonly string _filePath;
    private readonly IOptionsMonitor<TSettings> _optionsMonitor;

    public TSettings CurrentValue => _optionsMonitor.CurrentValue;

    public SettingsProvider(IOptionsMonitor<TSettings> optionsMonitor)
    {
        _filePath = Path.Join(AppPaths.GetConfigDirectory(), Settings.SETTINGS_FILE_NAME);

        // Scaffold a new settings file if it doesn't already exist
        if (!File.Exists(_filePath))
        {
            var template = Activator.CreateInstance<TSettings>();

            Save(template);
        }
            
        _optionsMonitor = optionsMonitor;
    }

    public async Task UpdateAsync(TSettings settings)
    {
        await SaveAsync(settings);
    }

    public async Task UpdateAsync(Action<TSettings> patch)
    {
        patch.Invoke(_optionsMonitor.CurrentValue);
        
        await SaveAsync(_optionsMonitor.CurrentValue);
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