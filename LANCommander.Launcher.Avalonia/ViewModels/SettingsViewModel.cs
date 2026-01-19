using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.SDK.Models;
using LANCommander.SDK.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Avalonia.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SettingsViewModel> _logger;

    // Game Settings
    [ObservableProperty]
    private ObservableCollection<InstallDirectoryItem> _installDirectories = new();

    // Media Settings
    [ObservableProperty]
    private string _mediaStoragePath = string.Empty;

    // UI Settings
    [ObservableProperty]
    private CultureItem? _selectedCultureItem;

    [ObservableProperty]
    private ObservableCollection<CultureItem> _availableCultures = new();

    // Debug Settings
    [ObservableProperty]
    private bool _enableScriptDebugging;

    [ObservableProperty]
    private string _loggingPath = string.Empty;

    [ObservableProperty]
    private LogLevel _selectedLogLevel = LogLevel.Warning;

    [ObservableProperty]
    private ObservableCollection<LogLevel> _availableLogLevels = new();

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isSaving;

    public event EventHandler? BackRequested;
    public event EventHandler? SettingsSaved;

    public SettingsViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<SettingsViewModel>>();

        // Initialize available cultures
        var cultures = new[]
        {
            "en-US", "de", "es", "fr", "it", "pt-BR", "nl", "ja", "zh", "ko", "uk"
        };

        foreach (var code in cultures)
        {
            try
            {
                var culture = CultureInfo.GetCultureInfo(code);
                AvailableCultures.Add(new CultureItem(code, culture.NativeName));
            }
            catch
            {
                AvailableCultures.Add(new CultureItem(code, code));
            }
        }

        // Initialize log levels
        foreach (var level in Enum.GetValues<LogLevel>())
        {
            AvailableLogLevels.Add(level);
        }
    }

    public void Load()
    {
        _logger.LogInformation("Loading settings...");

        using var scope = _serviceProvider.CreateScope();
        var settingsProvider = scope.ServiceProvider.GetRequiredService<SettingsProvider<Settings.Settings>>();
        var settings = settingsProvider.CurrentValue;

        // Game settings
        InstallDirectories.Clear();
        if (settings.Games.InstallDirectories?.Length > 0)
        {
            foreach (var dir in settings.Games.InstallDirectories)
            {
                InstallDirectories.Add(new InstallDirectoryItem(dir));
            }
        }
        else
        {
            InstallDirectories.Add(new InstallDirectoryItem(string.Empty));
        }

        // Media settings
        MediaStoragePath = settings.Media.StoragePath ?? string.Empty;

        // UI settings
        var cultureCode = settings.Culture ?? "en-US";
        SelectedCultureItem = AvailableCultures.FirstOrDefault(c => c.Code == cultureCode) 
                              ?? AvailableCultures.First();

        // Debug settings
        EnableScriptDebugging = settings.Debug.EnableScriptDebugging;
        LoggingPath = settings.Debug.LoggingPath ?? "Logs";
        SelectedLogLevel = settings.Debug.LogLevel;

        StatusMessage = null;
        _logger.LogInformation("Settings loaded");
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsSaving) return;

        IsSaving = true;
        StatusMessage = "Saving...";

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var settingsProvider = scope.ServiceProvider.GetRequiredService<SettingsProvider<Settings.Settings>>();

            settingsProvider.Update(s =>
            {
                // Game settings
                s.Games.InstallDirectories = InstallDirectories
                    .Where(d => !string.IsNullOrWhiteSpace(d.Path))
                    .Select(d => d.Path)
                    .ToArray();

                // Media settings
                s.Media.StoragePath = MediaStoragePath;

                // UI settings
                s.Culture = SelectedCultureItem?.Code ?? "en-US";

                // Debug settings
                s.Debug.EnableScriptDebugging = EnableScriptDebugging;
                s.Debug.LoggingPath = LoggingPath;
                s.Debug.LogLevel = SelectedLogLevel;
            });

            StatusMessage = "Settings saved!";
            _logger.LogInformation("Settings saved successfully");
            
            SettingsSaved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            StatusMessage = $"Failed to save: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void AddInstallDirectory()
    {
        InstallDirectories.Add(new InstallDirectoryItem(string.Empty));
    }

    [RelayCommand]
    private void RemoveInstallDirectory(InstallDirectoryItem? item)
    {
        if (item == null || InstallDirectories.Count <= 1) return;
        InstallDirectories.Remove(item);
    }

    [RelayCommand]
    private void GoBack()
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }
}

public partial class InstallDirectoryItem : ObservableObject
{
    [ObservableProperty]
    private string _path;

    public Guid Id { get; } = Guid.NewGuid();

    public InstallDirectoryItem(string path)
    {
        _path = path;
    }
}

public class CultureItem
{
    public string Code { get; }
    public string DisplayName { get; }

    public CultureItem(string code, string displayName)
    {
        Code = code;
        DisplayName = displayName;
    }

    public override string ToString() => DisplayName;
}
