using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Services;
using LANCommander.Launcher.Services.Plugins;
using LANCommander.Launcher.Settings;
using LANCommander.PluginManagement;
using LANCommander.SDK.Models;
using LANCommander.SDK.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.ViewModels;

/// <summary>
/// Installs and removes launcher plugins, from the server, the player's NuGet registries or a package
/// file, and edits which registries are used. Changes are made when the launcher next starts.
/// </summary>
public partial class PluginsViewModel : ViewModelBase
{
    private readonly LauncherPluginService _plugins;
    private readonly SettingsProvider<Settings.Settings> _settingsProvider;
    private readonly INavigationService _navigationService;
    private readonly ILogger<PluginsViewModel> _logger;

    private IReadOnlyList<LauncherPluginPackage> _serverPlugins = [];
    private IReadOnlyList<PluginUpdate> _feedUpdates = [];

    public ObservableCollection<InstalledPluginItem> Installed { get; } = new();
    public ObservableCollection<PluginListingItem> Results { get; } = new();
    public ObservableCollection<string> SearchErrors { get; } = new();
    public ObservableCollection<PluginRegistryItem> Registries { get; } = new();

    [ObservableProperty]
    private string _searchTerm = string.Empty;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _restartRequired;

    [ObservableProperty]
    private bool _includePrerelease;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _hasNoInstalled;

    [ObservableProperty]
    private bool _hasNoResults;

    public PluginsViewModel(IServiceProvider serviceProvider)
    {
        _plugins = serviceProvider.GetRequiredService<LauncherPluginService>();
        _settingsProvider = serviceProvider.GetRequiredService<SettingsProvider<Settings.Settings>>();
        _navigationService = serviceProvider.GetRequiredService<INavigationService>();
        _logger = serviceProvider.GetRequiredService<ILogger<PluginsViewModel>>();
    }

    public async Task LoadAsync()
    {
        StatusMessage = null;

        LoadRegistries();

        await LoadServerPluginsAsync();

        RefreshInstalled();

        await SearchAsync();

        _ = CheckForUpdatesAsync();
    }

    private async Task LoadServerPluginsAsync()
    {
        try
        {
            _serverPlugins = await _plugins.GetServerPluginsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not list the server's launcher plugins");
            _serverPlugins = [];
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            _feedUpdates = await _plugins.CheckForUpdatesAsync();

            RefreshInstalled();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check for plugin updates");
        }
    }

    #region Installed

    private void RefreshInstalled()
    {
        Installed.Clear();

        foreach (var plugin in _plugins.GetInstalled())
        {
            var packageId = plugin.EffectivePackage?.Id;
            var server = packageId == null ? null : FindServerPlugin(packageId);

            Installed.Add(new InstalledPluginItem(plugin, server?.Required == true, FindUpdate(plugin, server)));
        }

        HasNoInstalled = Installed.Count == 0;
        RestartRequired = _plugins.RestartRequired;

        foreach (var result in Results)
            result.InstalledVersion = InstalledVersionOf(result.Id);
    }

    private LauncherPluginPackage? FindServerPlugin(string packageId) =>
        _serverPlugins.FirstOrDefault(p => p.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase));

    /// <summary>A newer version: from the server, for a plugin installed from it or one it offers, or else from the feed it came from.</summary>
    private PluginUpdateOffer? FindUpdate(InstalledPlugin plugin, LauncherPluginPackage? server)
    {
        if (plugin.State is not (PluginInstallState.Loaded or PluginInstallState.NotLoaded or PluginInstallState.Disabled))
            return null;

        var installed = plugin.EffectivePackage?.Version;

        if (server != null && (_plugins.UseServer || server.Required) && LauncherPluginService.IsOlder(installed, server.Version))
            return new PluginUpdateOffer(server.Version, server, null);

        var update = _feedUpdates.FirstOrDefault(u => u.Folder.Equals(plugin.Folder, StringComparison.OrdinalIgnoreCase));

        return update == null ? null : new PluginUpdateOffer(update.Version, null, update);
    }

    private string? InstalledVersionOf(string packageId) => _plugins.FindByPackage(packageId)?.EffectivePackage?.Version;

    [RelayCommand]
    private async Task UpdateAsync(InstalledPluginItem? item)
    {
        if (item?.Update is not { } update)
            return;

        await RunAsync($"{item.Name} {update.Version} installs when the launcher restarts", () => update.Server != null
            ? _plugins.InstallFromServerAsync(update.Server)
            : _plugins.InstallAsync(update.Feed!.SourceUrl, update.Feed.PackageId, update.Feed.Version));
    }

    [RelayCommand]
    private async Task UninstallAsync(InstalledPluginItem? item)
    {
        if (item == null || item.IsRequired)
            return;

        await RunAsync($"{item.Name} is removed when the launcher restarts", () => _plugins.UninstallAsync(item.Folder));
    }

    [RelayCommand]
    private async Task UndoAsync(InstalledPluginItem? item)
    {
        if (item == null)
            return;

        await RunAsync(null, () => _plugins.CancelPendingAsync(item.Folder));
    }

    /// <summary>Installs a package file the player picked.</summary>
    public async Task InstallFromFileAsync(string path)
    {
        await RunAsync(null, async () =>
        {
            await using var stream = File.OpenRead(path);

            var result = await _plugins.InstallAsync(stream);

            StatusMessage = $"{result.Package.Title ?? result.Package.Id} {result.Package.Version} installs when the launcher restarts";
        });
    }

    #endregion

    #region Browse

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (IsSearching)
            return;

        IsSearching = true;
        SearchErrors.Clear();

        try
        {
            var term = SearchTerm.Trim();
            var results = new List<PluginListingItem>();

            if (_plugins.UseServer)
            {
                results.AddRange(_serverPlugins
                    .Where(p => term.Length == 0 || new[] { p.Id, p.Title, p.Description }
                        .Any(text => text?.Contains(term, StringComparison.OrdinalIgnoreCase) == true))
                    .Select(p => PluginListingItem.FromServer(p, InstalledVersionOf(p.Id))));
            }

            if (_plugins.Feeds.Feeds.Count > 0)
            {
                var found = await _plugins.SearchAsync(term);

                foreach (var error in found.Errors)
                    SearchErrors.Add(error);

                // A package the server offers is installed from the server
                results.AddRange(found.Packages
                    .Where(p => !results.Any(r => r.Id.Equals(p.Id, StringComparison.OrdinalIgnoreCase)))
                    .Select(p => PluginListingItem.FromFeed(p, InstalledVersionOf(p.Id))));
            }

            Results.Clear();

            foreach (var result in results)
                Results.Add(result);

            HasNoResults = Results.Count == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not search for plugins");
            SearchErrors.Add(ex.Message);
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    private async Task InstallAsync(PluginListingItem? item)
    {
        if (item == null)
            return;

        await RunAsync($"{item.DisplayName} {item.Version} installs when the launcher restarts", () => item.Server != null
            ? _plugins.InstallFromServerAsync(item.Server)
            : _plugins.InstallAsync(item.Feed!.SourceUrl, item.Feed.Id, item.Feed.Version));
    }

    #endregion

    #region Registries

    private void LoadRegistries()
    {
        var settings = _settingsProvider.CurrentValue;

        Registries.Clear();

        Registries.Add(new PluginRegistryItem(PluginRegistryKind.Server)
        {
            Name = "LANCommander Server",
            Url = settings.Authentication.ServerAddress?.ToString() ?? string.Empty,
            Enabled = settings.Plugins.UseServer,
        });

        Registries.Add(new PluginRegistryItem(PluginRegistryKind.NuGetOrg)
        {
            Name = PluginFeed.NuGetOrgName,
            Url = PluginFeed.NuGetOrgUrl,
            Enabled = settings.Plugins.UseNuGetOrg,
        });

        // Edited as copies, so nothing changes until Save
        foreach (var source in settings.Plugins.Sources)
        {
            Registries.Add(new PluginRegistryItem(PluginRegistryKind.Custom)
            {
                Name = source.Name,
                Url = source.Url,
                Enabled = source.Enabled,
                Username = source.Username,
                Password = source.Password,
            });
        }

        IncludePrerelease = settings.Plugins.IncludePrerelease;
    }

    [RelayCommand]
    private void AddRegistry()
    {
        Registries.Add(new PluginRegistryItem(PluginRegistryKind.Custom)
        {
            Name = $"Registry {Registries.Count(r => r.Kind == PluginRegistryKind.Custom) + 1}",
        });
    }

    [RelayCommand]
    private void RemoveRegistry(PluginRegistryItem? item)
    {
        if (item is { IsBuiltIn: false })
            Registries.Remove(item);
    }

    [RelayCommand]
    private async Task TestRegistryAsync(PluginRegistryItem? item)
    {
        if (item == null || item.Kind == PluginRegistryKind.Server || string.IsNullOrWhiteSpace(item.Url))
            return;

        item.TestResult = "Connecting...";

        try
        {
            await _plugins.TestSourceAsync(item.ToSource());

            item.TestResult = "Connected";
        }
        catch (Exception ex)
        {
            item.TestResult = ex.Message;
        }
    }

    [RelayCommand]
    private async Task SaveRegistriesAsync()
    {
        var custom = Registries.Where(r => r.Kind == PluginRegistryKind.Custom).ToList();

        foreach (var registry in custom)
        {
            registry.Name = registry.Name.Trim();
            registry.Url = registry.Url.Trim();
        }

        if (custom.Any(r => string.IsNullOrWhiteSpace(r.Url)))
        {
            StatusMessage = "Every registry needs a URL or folder.";
            return;
        }

        if (Registries.GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
        {
            StatusMessage = "Each registry needs a name of its own.";
            return;
        }

        try
        {
            _settingsProvider.Update(s =>
            {
                s.Plugins.UseServer = Registries.First(r => r.Kind == PluginRegistryKind.Server).Enabled;
                s.Plugins.UseNuGetOrg = Registries.First(r => r.Kind == PluginRegistryKind.NuGetOrg).Enabled;
                s.Plugins.IncludePrerelease = IncludePrerelease;
                s.Plugins.Sources = custom.Select(r => r.ToSource()).ToList();
            });

            StatusMessage = "Registries saved";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not save the plugin registries");
            StatusMessage = $"Could not save the registries: {ex.Message}";
            return;
        }

        // Registries changed, so earlier results may no longer apply
        RefreshInstalled();
        await SearchAsync();
    }

    #endregion

    /// <summary>Runs a change, then shows <paramref name="success"/>, or the error, and the plugins as they now are.</summary>
    private async Task RunAsync(string? success, Func<Task> change)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        StatusMessage = null;

        try
        {
            await change();

            if (success != null)
                StatusMessage = success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plugin change failed");

            await Views.AlertOverlay.ShowAsync("Plugin Change Failed", ex is PluginPackageException ? ex.Message : $"The change could not be made: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            RefreshInstalled();
        }
    }

    [RelayCommand]
    private void GoBack() => _navigationService.GoBack();
}

/// <summary>A newer version of an installed plugin: on the server, or on the feed it came from.</summary>
public sealed record PluginUpdateOffer(string Version, LauncherPluginPackage? Server, PluginUpdate? Feed);

public sealed class InstalledPluginItem
{
    private readonly InstalledPlugin _plugin;

    public InstalledPluginItem(InstalledPlugin plugin, bool isRequired, PluginUpdateOffer? update)
    {
        _plugin = plugin;
        IsRequired = isRequired;
        Update = update;
    }

    public string Folder => _plugin.Folder;
    public string Name => _plugin.Name;
    public string PackageId => _plugin.EffectivePackage?.Id ?? _plugin.Folder;
    public bool IsOfficial => _plugin.IsOfficial;

    /// <summary>The server requires it, so it can't be uninstalled.</summary>
    public bool IsRequired { get; }

    public PluginUpdateOffer? Update { get; }
    public bool HasUpdate => Update != null;

    public string Version => _plugin.State == PluginInstallState.UpdatePending
        ? $"{_plugin.Package?.Version} → {_plugin.PendingPackage?.Version}"
        : _plugin.Version ?? string.Empty;

    public string Source => _plugin.EffectivePackage switch
    {
        null => "Manual",
        { SourceName: null } => "File",
        var package => package.SourceName,
    };

    public string Status => _plugin.State switch
    {
        PluginInstallState.Loaded => "Running",
        PluginInstallState.NotLoaded => "Not loaded",
        PluginInstallState.Disabled => "Turned off",
        PluginInstallState.InstallPending => "Installs on restart",
        PluginInstallState.UpdatePending => "Updates on restart",
        PluginInstallState.RemovalPending => "Removed on restart",
        _ => string.Empty,
    };

    public bool IsRunning => _plugin.State == PluginInstallState.Loaded;
    public bool IsPending => _plugin.State is PluginInstallState.InstallPending or PluginInstallState.UpdatePending or PluginInstallState.RemovalPending;
    public bool IsFaulted => _plugin.State is PluginInstallState.NotLoaded or PluginInstallState.Disabled;

    public bool CanUndo => IsPending;
    public bool CanUninstall => !IsRequired && _plugin.State is not (PluginInstallState.RemovalPending or PluginInstallState.InstallPending);
}

public sealed partial class PluginListingItem : ObservableObject
{
    private PluginListingItem(string id, string version, string displayName, string? description, string? authors, string sourceName, bool isOfficial, bool isRequired, string? installedVersion)
    {
        Id = id;
        Version = version;
        DisplayName = displayName;
        Description = description;
        Authors = authors;
        SourceName = sourceName;
        IsOfficial = isOfficial;
        IsRequired = isRequired;
        _installedVersion = installedVersion;
    }

    public static PluginListingItem FromServer(LauncherPluginPackage package, string? installedVersion) =>
        new(package.Id, package.Version, string.IsNullOrWhiteSpace(package.Title) ? package.Id : package.Title, package.Description, package.Authors,
            LauncherPluginService.ServerSourceName, package.IsOfficial, package.Required, installedVersion)
        {
            Server = package,
        };

    public static PluginListingItem FromFeed(PluginPackageListing listing, string? installedVersion) =>
        new(listing.Id, listing.Version, listing.DisplayName, listing.Description, listing.Authors, listing.SourceName, listing.IsOfficial, false, installedVersion)
        {
            Feed = listing,
        };

    public string Id { get; }
    public string Version { get; }
    public string DisplayName { get; }
    public string? Description { get; }
    public string? Authors { get; }
    public string SourceName { get; }
    public bool IsOfficial { get; }
    public bool IsRequired { get; }

    /// <summary>Set for a package the server offers.</summary>
    public LauncherPluginPackage? Server { get; private init; }

    /// <summary>Set for a package found on a NuGet feed.</summary>
    public PluginPackageListing? Feed { get; private init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstall), nameof(CanUpdate), nameof(IsInstalled))]
    private string? _installedVersion;

    public bool CanInstall => InstalledVersion == null;
    public bool CanUpdate => InstalledVersion != null && LauncherPluginService.IsOlder(InstalledVersion, Version);
    public bool IsInstalled => InstalledVersion != null && !CanUpdate;

    public string Byline => string.IsNullOrWhiteSpace(Authors) ? $"{Id} · {SourceName}" : $"{Id} · by {Authors} · {SourceName}";
}

public enum PluginRegistryKind
{
    /// <summary>The server the launcher is connected to.</summary>
    Server,
    NuGetOrg,
    /// <summary>A NuGet feed the player added.</summary>
    Custom,
}

public sealed partial class PluginRegistryItem(PluginRegistryKind kind) : ObservableObject
{
    public PluginRegistryKind Kind { get; } = kind;

    /// <summary>The server and nuget.org can be turned off, but not renamed or removed.</summary>
    public bool IsBuiltIn => Kind != PluginRegistryKind.Custom;

    public bool IsEditable => !IsBuiltIn;

    /// <summary>The server is reached as the player is signed in to it; NuGet feeds can be tested.</summary>
    public bool CanTest => Kind != PluginRegistryKind.Server;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _url = string.Empty;

    [ObservableProperty]
    private bool _enabled = true;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _testResult;

    public PluginPackageSource ToSource() => new()
    {
        Name = Name,
        Url = Url,
        Enabled = Enabled,
        Username = Username,
        Password = Password,
    };
}
