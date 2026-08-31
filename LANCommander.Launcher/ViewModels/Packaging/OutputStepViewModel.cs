using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Services;
using LANCommander.Packaging.LCX;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.ViewModels.Packaging;

/// <summary>
/// Builds the .lcx and optionally publishes it to the connected server.
/// </summary>
public partial class OutputStepViewModel : PackagingStepViewModel
{
    private readonly GameClient _gameClient;
    private readonly AuthenticationService _authenticationService;
    private readonly ILogger<OutputStepViewModel> _logger;

    public OutputStepViewModel(PackagingWizardViewModel wizard, IServiceProvider serviceProvider)
        : base(wizard)
    {
        _gameClient = serviceProvider.GetRequiredService<GameClient>();
        _authenticationService = serviceProvider.GetRequiredService<AuthenticationService>();
        _logger = serviceProvider.GetRequiredService<ILogger<OutputStepViewModel>>();
    }

    public override string Title => "Finish";

    public override string NextLabel => "Finish";

    [ObservableProperty]
    private bool _saveToDisk = true;

    [ObservableProperty]
    private string _outputPath = string.Empty;

    [ObservableProperty]
    private bool _publishToServer;

    [ObservableProperty]
    private bool _patchGameSpy;

    [ObservableProperty]
    private CompressionLevel _compressionLevel = CompressionLevel.Optimal;

    public CompressionLevel[] CompressionLevels { get; } =
    [
        CompressionLevel.NoCompression,
        CompressionLevel.Fastest,
        CompressionLevel.Optimal,
        CompressionLevel.SmallestSize,
    ];

    [ObservableProperty]
    private bool _isBuilding;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isProgressIndeterminate;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isComplete;

    /// <summary>Id of the game the server created, when publishing succeeded.</summary>
    [ObservableProperty]
    private Guid _publishedGameId;

    /// <summary>Publishing needs a server session and permission to create games.</summary>
    public bool CanPublish => _authenticationService.CanManageGames();

    public bool CanBuild => (SaveToDisk || PublishToServer) && !IsBuilding;

    partial void OnSaveToDiskChanged(bool value) => OnPropertyChanged(nameof(CanBuild));

    partial void OnPublishToServerChanged(bool value) => OnPropertyChanged(nameof(CanBuild));

    partial void OnIsBuildingChanged(bool value) => OnPropertyChanged(nameof(CanBuild));

    public override Task OnEnterAsync()
    {
        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            var fileName = $"{Package.Manifest.Title.SanitizeFilename()}.lcx";

            OutputPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), fileName);
        }

        // Not an administrator: publishing is unavailable, so do not leave it checked.
        if (!CanPublish)
            PublishToServer = false;

        CanGoNext = false;

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task BuildAsync()
    {
        if (!CanBuild)
            return;

        IsBuilding = true;
        IsComplete = false;
        ErrorMessage = null;
        Progress = 0;

        // Temp only when the user did not ask for a file of their own.
        var buildingToTemp = !SaveToDisk;

        var targetPath = buildingToTemp
            ? Path.Combine(Path.GetTempPath(), $"lancommander-{Guid.NewGuid():N}.lcx")
            : OutputPath;

        try
        {
            Package.OutputPath = targetPath;
            Package.PatchGameSpy = PatchGameSpy;
            Package.CompressionLevel = CompressionLevel;

            IsProgressIndeterminate = true;

            var progress = new Progress<string>(message =>
                Dispatcher.UIThread.Post(() => Status = message));

            await Task.Run(() => LCXBuilder.BuildAsync(Package, progress));

            if (PublishToServer)
                await PublishAsync(targetPath);

            IsComplete = true;

            Status = BuildCompletionMessage(buildingToTemp);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            Status = "Packaging failed.";

            _logger.LogError(ex, "Could not build the package");
        }
        finally
        {
            IsProgressIndeterminate = false;
            IsBuilding = false;

            // Clean up only a file we created ourselves. The Packager deleted the user's chosen
            // output after a successful upload, which threw away exactly what they asked for.
            if (buildingToTemp)
                TryDelete(targetPath);
        }
    }

    private string BuildCompletionMessage(bool buildingToTemp)
    {
        if (PublishToServer && !buildingToTemp)
            return $"Published to the server and saved to {OutputPath}.";

        if (PublishToServer)
            return "Published to the server.";

        return $"Saved to {OutputPath}.";
    }

    private async Task PublishAsync(string packagePath)
    {
        Status = "Uploading...";

        var totalBytes = new FileInfo(packagePath).Length;

        IsProgressIndeterminate = false;

        var uploadProgress = new Progress<long>(uploaded =>
            Dispatcher.UIThread.Post(() =>
            {
                Progress = totalBytes > 0 ? uploaded * 100d / totalBytes : 0;
                Status = $"Uploading... {Progress:0}%";
            }));

        var result = await _gameClient.ImportAsync(packagePath, uploadProgress: uploadProgress);

        // The import itself is a single request with no streaming progress, so show that it is
        // working rather than pretending to know how far along it is.
        IsProgressIndeterminate = true;
        Status = "Importing on the server...";

        PublishedGameId = result?.RecordId ?? Guid.Empty;
    }

    private void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not delete the temporary package at {Path}", path);
        }
    }
}
