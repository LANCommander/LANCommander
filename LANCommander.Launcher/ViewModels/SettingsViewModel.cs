using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Services;
using LANCommander.Launcher.Services.Interposer;
using LANCommander.Launcher.Settings;
using LANCommander.SDK.Models;
using LANCommander.SDK.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly INavigationService _navigationService;

    // Game Settings
    [ObservableProperty]
    private ObservableCollection<InstallDirectoryItem> _installDirectories = new();

    [ObservableProperty]
    private int _maxInstallAttempts = 10;

    // Media Settings
    [ObservableProperty]
    private string _mediaStoragePath = string.Empty;

    // UI Settings
    [ObservableProperty]
    private CultureItem? _selectedCultureItem;

    [ObservableProperty]
    private ObservableCollection<CultureItem> _availableCultures = new();

    // Notification Settings
    [ObservableProperty]
    private bool _notifyOnInstallComplete = true;

    [ObservableProperty]
    private bool _notifyOnInstallFailed = true;

    [ObservableProperty]
    private bool _notifyOnChatMessage = true;

    [ObservableProperty]
    private bool _notifyOnElevationRequired = true;

    [ObservableProperty]
    private NotificationSoundTheme _selectedSoundTheme = NotificationSoundTheme.SystemDefault;

    [ObservableProperty]
    private ObservableCollection<NotificationSoundTheme> _availableSoundThemes = new();

    // Compatibility Settings: the network relay and the Interposer, both Windows only
    public bool IsCompatibilityAvailable => OperatingSystem.IsWindows();

    [ObservableProperty]
    private bool _joinNetworkRelay = true;

    [ObservableProperty]
    private bool _addInterposerToGames = true;

    /// <summary>Interposer settings for every game, under each game's own.</summary>
    [ObservableProperty]
    private InterposerSettingsViewModel? _interposer;

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

    public event EventHandler? SettingsSaved;

    public SettingsViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<SettingsViewModel>>();
        _navigationService = serviceProvider.GetRequiredService<INavigationService>();

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

        // Initialize sound themes
        foreach (var theme in Enum.GetValues<NotificationSoundTheme>())
            AvailableSoundThemes.Add(theme);

        // Initialize log levels
        foreach (var level in Enum.GetValues<LogLevel>())
            AvailableLogLevels.Add(level);
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
            foreach (var dir in settings.Games.InstallDirectories)
                InstallDirectories.Add(new InstallDirectoryItem(dir));
        else
            InstallDirectories.Add(new InstallDirectoryItem(string.Empty));

        MaxInstallAttempts = settings.Games.MaxInstallAttempts;

        // Media settings
        MediaStoragePath = settings.Media.StoragePath ?? string.Empty;

        // UI settings
        var cultureCode = settings.Culture ?? "en-US";
        
        SelectedCultureItem = AvailableCultures.FirstOrDefault(c => c.Code == cultureCode) 
                              ?? AvailableCultures.First();

        // Notification settings
        NotifyOnInstallComplete = settings.Notifications.NotifyOnInstallComplete;
        NotifyOnInstallFailed = settings.Notifications.NotifyOnInstallFailed;
        NotifyOnChatMessage = settings.Notifications.NotifyOnChatMessage;
        NotifyOnElevationRequired = settings.Notifications.NotifyOnElevationRequired;
        SelectedSoundTheme = settings.Notifications.SoundTheme;

        // Compatibility settings. The relay has its own switch above the Interposer's options, so its
        // option is not offered twice; everything else starts from the Interposer's own defaults.
        JoinNetworkRelay = settings.NetworkRelay.Enabled;
        AddInterposerToGames = settings.Interposer.Enabled;
        Interposer = InterposerSettingsViewModel.Build(
            settings.Interposer.Options ?? new(),
            key => new InheritedOptionValue(InterposerOptions.GetDefault(key), null),
            "NetworkRelay.Enabled");

        if (IsCompatibilityAvailable)
            _ = Interposer.LoadDevicesAsync(scope.ServiceProvider.GetRequiredService<Services.Platform.IDeviceCatalog>());

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
        if (IsSaving) 
            return;

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

                s.Games.MaxInstallAttempts = Math.Max(1, MaxInstallAttempts);

                // Media settings
                s.Media.StoragePath = MediaStoragePath;

                // UI settings
                s.Culture = SelectedCultureItem?.Code ?? "en-US";

                // Notification settings
                s.Notifications.NotifyOnInstallComplete = NotifyOnInstallComplete;
                s.Notifications.NotifyOnInstallFailed = NotifyOnInstallFailed;
                s.Notifications.NotifyOnChatMessage = NotifyOnChatMessage;
                s.Notifications.NotifyOnElevationRequired = NotifyOnElevationRequired;
                s.Notifications.SoundTheme = SelectedSoundTheme;

                // Compatibility settings
                s.NetworkRelay.Enabled = JoinNetworkRelay;
                s.Interposer.Enabled = AddInterposerToGames;

                if (Interposer != null)
                    s.Interposer.Options = Interposer.CollectValues();

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
        if (item == null || InstallDirectories.Count <= 1)
            return;
        
        InstallDirectories.Remove(item);
    }

    [RelayCommand]
    private void ShowPlugins()
    {
        var plugins = new PluginsViewModel(_serviceProvider);

        _navigationService.NavigateTo(plugins, initializeAsync: plugins.LoadAsync);
    }

    [RelayCommand]
    private void GoBack()
    {
        _navigationService.GoBack();
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
