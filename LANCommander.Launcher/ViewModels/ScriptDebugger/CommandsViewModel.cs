using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.ViewModels.ScriptDebugger.Items;
using LANCommander.SDK.PowerShell.Debugging.Commands;

namespace LANCommander.Launcher.ViewModels.ScriptDebugger;

/// <summary>
/// The command browser: every cmdlet, function and alias the session can resolve.
/// </summary>
/// <remarks>
/// <para>
/// A default session state resolves a few thousand commands, so the filtered view is rebuilt as a
/// whole list and assigned in one go rather than mutated item by item in an
/// <c>ObservableCollection</c>: the latter raises one collection-changed notification per row and
/// makes every keystroke in the search box visibly slow.
/// </para>
/// <para>
/// Details are loaded only for the selected command. Reading a command's parameters imports its
/// declaring module, which is affordable once and not affordable a few thousand times.
/// </para>
/// </remarks>
public sealed partial class CommandsViewModel : ObservableObject
{
    public const string AllModules = "All modules";
    public const string AllTypes = "All types";

    private static readonly TimeSpan FilterDebounce = TimeSpan.FromMilliseconds(150);

    private readonly CommandCatalogService _catalog;
    private readonly DispatcherTimer _filterTimer;

    private IReadOnlyList<CommandInfoSnapshot> _all = Array.Empty<CommandInfoSnapshot>();

    /// <summary>Guards against a slow detail fetch landing after the user has moved on.</summary>
    private int _detailToken;

    public CommandsViewModel(CommandCatalogService catalog)
    {
        _catalog = catalog;

        // Same shape as the editor's DebouncedParser: restart on every keystroke, do the work once
        // the typing settles.
        _filterTimer = new DispatcherTimer { Interval = FilterDebounce };
        _filterTimer.Tick += (_, _) =>
        {
            _filterTimer.Stop();
            ApplyFilter();
        };
    }

    /// <summary>Raised when the user asks for a command to be written into the editor.</summary>
    public event Action<string>? InsertRequested;

    public ObservableCollection<string> Modules { get; } = new() { AllModules };

    public ObservableCollection<string> CommandTypes { get; } = new() { AllTypes };

    [ObservableProperty]
    private IReadOnlyList<CommandItemViewModel> _visibleItems = Array.Empty<CommandItemViewModel>();

    [ObservableProperty]
    private CommandItemViewModel? _selected;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedModule = AllModules;

    [ObservableProperty]
    private string _selectedCommandType = AllTypes;

    [ObservableProperty]
    private CommandDetail? _detail;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isLoadingDetail;

    [ObservableProperty]
    private string _statusText = "Loading commands...";

    /// <summary>
    /// True while the debugger is stopped and the list on screen does not yet come from it, i.e.
    /// while a refresh would actually tell the user something new.
    /// </summary>
    [ObservableProperty]
    private bool _sessionRefreshAvailable;

    private CommandSource _source = CommandSource.Catalog;
    private bool _sessionAvailable;

    public bool HasSelection => Selected is not null;

    // ---- loading ------------------------------------------------------------------------

    /// <summary>
    /// Load, or reload, the list. Awaited by nobody: the pane fills in when it fills in.
    /// </summary>
    public async Task LoadAsync()
    {
        if (IsLoading)
            return;

        IsLoading = true;
        StatusText = "Loading commands...";

        try
        {
            var (commands, source) = await _catalog.ListAsync();

            _all = commands;
            _source = source;

            UpdateRefreshHint();
            RebuildFilterChoices();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            _all = Array.Empty<CommandInfoSnapshot>();
            VisibleItems = Array.Empty<CommandItemViewModel>();
            StatusText = ex.GetBaseException().Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    /// <summary>
    /// Called when a run ends. A session-sourced list outlives the runspace that produced it and
    /// can name commands that no longer resolve, so it is rebuilt from the catalogue.
    /// </summary>
    public Task ResetToCatalogAsync() =>
        _source == CommandSource.Session ? LoadAsync() : Task.CompletedTask;

    /// <summary>Called by the main view model as the debug session changes state.</summary>
    public void SetSessionAvailable(bool available)
    {
        _sessionAvailable = available;
        UpdateRefreshHint();
    }

    private void UpdateRefreshHint() =>
        SessionRefreshAvailable = _sessionAvailable && _source != CommandSource.Session;

    // ---- filtering ----------------------------------------------------------------------

    partial void OnSearchTextChanged(string value)
    {
        _filterTimer.Stop();
        _filterTimer.Start();
    }

    partial void OnSelectedModuleChanged(string value) => ApplyFilter();

    partial void OnSelectedCommandTypeChanged(string value) => ApplyFilter();

    private void RebuildFilterChoices()
    {
        var module = SelectedModule;
        var type = SelectedCommandType;

        Rebuild(Modules, AllModules, _all.Select(static c => c.ModuleDisplay));
        Rebuild(CommandTypes, AllTypes, _all.Select(static c => c.CommandType));

        // A refresh can drop the module the user had selected -- fall back rather than filter
        // everything away and look broken.
        SelectedModule = Modules.Contains(module) ? module : AllModules;
        SelectedCommandType = CommandTypes.Contains(type) ? type : AllTypes;

        static void Rebuild(ObservableCollection<string> target, string all, IEnumerable<string> values)
        {
            target.Clear();
            target.Add(all);

            foreach (var value in values.Distinct(StringComparer.OrdinalIgnoreCase)
                                        .OrderBy(static v => v, StringComparer.OrdinalIgnoreCase))
            {
                target.Add(value);
            }
        }
    }

    private void ApplyFilter()
    {
        var search = SearchText.Trim();
        var module = SelectedModule;
        var type = SelectedCommandType;

        var filtered = new List<CommandItemViewModel>();

        foreach (var command in _all)
        {
            if (module != AllModules && !string.Equals(command.ModuleDisplay, module, StringComparison.OrdinalIgnoreCase))
                continue;

            if (type != AllTypes && !string.Equals(command.CommandType, type, StringComparison.OrdinalIgnoreCase))
                continue;

            if (search.Length > 0 && command.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            filtered.Add(new CommandItemViewModel(command));
        }

        VisibleItems = filtered;

        var origin = _source == CommandSource.Session ? "session" : "catalog";

        StatusText = filtered.Count == _all.Count
            ? $"{_all.Count:N0} commands ({origin})"
            : $"{filtered.Count:N0} of {_all.Count:N0} commands ({origin})";
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedModule = AllModules;
        SelectedCommandType = AllTypes;
    }

    // ---- details ------------------------------------------------------------------------

    partial void OnSelectedChanged(CommandItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelection));

        Detail = null;

        if (value is not null)
            _ = LoadDetailAsync(value.Name);
    }

    private async Task LoadDetailAsync(string name)
    {
        var token = ++_detailToken;
        IsLoadingDetail = true;

        try
        {
            var detail = await _catalog.DescribeAsync(name);

            // Arrow-keying down the list starts a fetch per row; only the newest one may win.
            if (token == _detailToken)
                Detail = detail;
        }
        catch (Exception)
        {
            if (token == _detailToken)
                Detail = null;
        }
        finally
        {
            if (token == _detailToken)
                IsLoadingDetail = false;
        }
    }

    // ---- editor ------------------------------------------------------------------------

    [RelayCommand]
    private void Insert(CommandItemViewModel? item)
    {
        var name = (item ?? Selected)?.Name;

        if (!string.IsNullOrEmpty(name))
            InsertRequested?.Invoke(name);
    }
}
