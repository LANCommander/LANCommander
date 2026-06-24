using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LANCommander.Packager.Models;
using LANCommander.Packager.Views;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Clients;
using LANCommander.SDK.Factories;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Packager;

public partial class MainWindow : Window
{
    private readonly PackageContext _context;
    private readonly IServiceProvider _services;
    private readonly UserControl[] _steps;
    private readonly string[] _stepTitles;
    private readonly string[] _stepHelps;
    private readonly ObservableCollection<WizardStepItem> _stepItems;
    private int _currentStep;
    private bool _monitoringComplete;
    private bool _isAuthenticated;

    private readonly MonitoringView _monitoringView;
    private readonly InstallDirectoryView _installDirView;
    private readonly FileSelectionView _fileSelectionView;
    private readonly RegistrySelectionView _registrySelectionView;
    private readonly MetadataView _metadataView;
    private readonly ActionView _actionView;
    private readonly OutputView _outputView;

    private readonly AuthenticationClient _authClient;
    private readonly IConnectionClient _connectionClient;
    private readonly ISettingsProvider _settingsProvider;
    private readonly ITokenProvider _tokenProvider;
    private readonly MetadataClient _metadataClient;
    private readonly ApiRequestFactory _apiRequestFactory;

    public MainWindow() : this(new PackageContext(), null!) { }

    public MainWindow(PackageContext context, IServiceProvider services)
    {
        _context = context;
        _services = services;
        InitializeComponent();

        _authClient = _services.GetRequiredService<AuthenticationClient>();
        _connectionClient = _services.GetRequiredService<IConnectionClient>();
        _settingsProvider = _services.GetRequiredService<ISettingsProvider>();
        _tokenProvider = _services.GetRequiredService<ITokenProvider>();
        _metadataClient = _services.GetRequiredService<MetadataClient>();
        _apiRequestFactory = _services.GetRequiredService<ApiRequestFactory>();

        _monitoringView = new MonitoringView(context);
        _installDirView = new InstallDirectoryView(context);
        _fileSelectionView = new FileSelectionView(context);
        _registrySelectionView = new RegistrySelectionView(context);
        _metadataView = new MetadataView(context, _metadataClient);
        _actionView = new ActionView(context);
        _outputView = new OutputView(context, _apiRequestFactory, _settingsProvider);

        _monitoringView.MonitoringCompleted += () =>
        {
            _monitoringComplete = true;
            Dispatcher.UIThread.Post(() => NextButton.IsEnabled = true);
        };

        _steps = [_monitoringView, _installDirView, _fileSelectionView,
                  _registrySelectionView, _metadataView, _actionView, _outputView];

        _stepTitles = ["Monitor Installer", "Install Directory", "Select Files",
                       "Registry Entries", "Game Metadata", "Game Executable", "Generate Package"];

        _stepHelps =
        [
            "The installer will be monitored for file and registry changes.",
            "Confirm the directory where the game was installed. This will be the root of the game archive.",
            "Select which files to include in the package. Use the checkboxes to toggle selection.",
            "Select which registry entries should be recreated by the install script.",
            "Enter basic information about the game.",
            "Select the primary game executable and configure the launch action.",
            "Choose the output path and generate the .LCX package file."
        ];

        _stepItems = new ObservableCollection<WizardStepItem>
        {
            new() { Index = 0, Title = "Monitor Installer" },
            new() { Index = 1, Title = "Install Directory" },
            new() { Index = 2, Title = "Select Files" },
            new() { Index = 3, Title = "Registry Entries" },
            new() { Index = 4, Title = "Game Metadata" },
            new() { Index = 5, Title = "Game Executable" },
            new() { Index = 6, Title = "Generate Package" },
        };

        StepList.ItemsSource = _stepItems;

        BackButton.Click += OnBackClick;
        NextButton.Click += OnNextClick;
        CancelButton.Click += (_, _) => Close();
        ConnectButton.Click += OnConnectClick;

        GoToStep(0);
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        await CheckExistingAuthAsync();
        await StartFirstStep();
    }

    private async Task CheckExistingAuthAsync()
    {
        try
        {
            var token = _tokenProvider.GetToken();

            if (token == null || string.IsNullOrEmpty(token.AccessToken))
                return;

            var serverAddress = _settingsProvider.CurrentValue.Authentication.ServerAddress;

            if (serverAddress != null)
                await _connectionClient.UpdateServerAddressAsync(serverAddress);

            var valid = await _authClient.ValidateTokenAsync();

            if (valid)
                SetAuthenticated(true);
        }
        catch
        {
            // Token expired or server unreachable - stay disconnected
        }
    }

    private void SetAuthenticated(bool authenticated)
    {
        _isAuthenticated = authenticated;

        Dispatcher.UIThread.Post(() =>
        {
            if (authenticated)
            {
                var address = _settingsProvider.CurrentValue.Authentication.ServerAddress;
                AuthStatusLabel.Text = $"Connected to {address}";
                AuthStatusLabel.Opacity = 0.8;
                StatusDot.Fill = new SolidColorBrush(Color.Parse("#49AA19"));
                ConnectButton.Content = "Server Settings";
            }
            else
            {
                AuthStatusLabel.Text = "Not connected";
                AuthStatusLabel.Opacity = 0.5;
                StatusDot.Fill = new SolidColorBrush(Color.Parse("#555555"));
                ConnectButton.Content = "Connect to Server";
            }

            _metadataView.SetAuthenticated(authenticated);
            _outputView.SetAuthenticated(authenticated);
        });
    }

    private async void OnConnectClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new ConnectDialog(_authClient, _connectionClient, _settingsProvider);
        var result = await dialog.ShowDialog<bool?>(this);

        if (result == true && dialog.IsAuthenticated)
            SetAuthenticated(true);
        else if (!dialog.IsAuthenticated)
            SetAuthenticated(false);
    }

    private async Task StartFirstStep()
    {
        if (string.IsNullOrWhiteSpace(_context.InstallerPath))
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Installer",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new("Executable Files") { Patterns = ["*.exe", "*.msi"] },
                    FilePickerFileTypes.All
                ]
            });

            if (files.Count > 0)
                _context.InstallerPath = files[0].Path.LocalPath;
            else
            {
                Close();
                return;
            }
        }

        _monitoringView.StartMonitoring();
    }

    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        if (_currentStep > 0)
        {
            ApplyCurrentStep();
            GoToStep(_currentStep - 1);
        }
    }

    private void OnNextClick(object? sender, RoutedEventArgs e)
    {
        if (_currentStep == _steps.Length - 1)
        {
            Close();
            return;
        }

        ApplyCurrentStep();
        GoToStep(_currentStep + 1);
    }

    private void ApplyCurrentStep()
    {
        switch (_currentStep)
        {
            case 1: _installDirView.ApplySelection(); break;
            case 2: _fileSelectionView.ApplySelection(); break;
            case 3: _registrySelectionView.ApplySelection(); break;
            case 4: _metadataView.ApplyMetadata(); break;
            case 5: _actionView.ApplyAction(); break;
        }
    }

    private void GoToStep(int step)
    {
        _currentStep = step;

        ContentArea.Content = _steps[step];
        StepTitle.Text = _stepTitles[step];
        StepHelp.Text = _stepHelps[step];

        for (int i = 0; i < _stepItems.Count; i++)
        {
            _stepItems[i].State = i < step ? StepState.Completed
                                : i == step ? StepState.Current
                                : StepState.Pending;
        }

        BackButton.IsVisible = step > 0;
        NextButton.IsVisible = step != _steps.Length - 1;
        NextButton.IsEnabled = step != 0 || _monitoringComplete;

        EnterStep(step);
    }

    private void EnterStep(int step)
    {
        switch (step)
        {
            case 1:
                var monitor = _monitoringView.GetMonitorService();
                if (monitor != null)
                    _installDirView.PopulateFromMonitor(monitor);
                else
                    _installDirView.PopulateFromDetectedPath(_monitoringView.GetDetectedInstallDirectory());
                break;
            case 2:
                _installDirView.ApplySelection();
                _fileSelectionView.PopulateFiles();
                break;
            case 3:
                _fileSelectionView.ApplySelection();
                _registrySelectionView.PopulateEntries();
                break;
            case 4:
                _registrySelectionView.ApplySelection();
                _metadataView.SetDefaultTitle(
                    Path.GetFileNameWithoutExtension(_context.InstallerPath));
                break;
            case 5:
                _metadataView.ApplyMetadata();
                _actionView.PopulateExecutables();
                break;
            case 6:
                _actionView.ApplyAction();
                _outputView.SetDefaultOutputPath();
                break;
        }
    }
}
