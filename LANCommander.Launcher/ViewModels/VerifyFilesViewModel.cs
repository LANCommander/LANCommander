using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ByteSizeLib;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Services;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.ViewModels;

/// <summary>
/// Checks an installed game's files against its archive, listing problems as they are found, and
/// repairs only the files that failed once the user asks. Nothing on disk changes until Repair.
/// </summary>
public partial class VerifyFilesViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<VerifyFilesViewModel> _logger;
    private readonly INavigationService _navigationService;
    private readonly string _installDirectory;
    private CancellationTokenSource? _cts;

    public Guid GameId { get; }
    public string Title { get; }

    /// <summary>Raised when a check or repair ends (completed, stopped or failed).</summary>
    public event EventHandler? Finished;

    public ObservableCollection<VerifyProblemViewModel> Problems { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy), nameof(CanRepair), nameof(CanCheckAgain), nameof(ProblemsNote), nameof(RepairNote))]
    private bool _isChecking;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy), nameof(CanRepair), nameof(CanCheckAgain))]
    private bool _isRepairing;

    /// <summary>The last check ran to the end, so the problem list is complete.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsClean), nameof(ProblemsNote), nameof(RepairNote))]
    private bool _isCheckComplete;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRepair), nameof(CanCheckAgain), nameof(RepairNote), nameof(ShowRepair))]
    private bool _isRepaired;

    [ObservableProperty]
    private string _statusTitle = "Preparing…";

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountText), nameof(MatchedCount))]
    private int _checkedFiles;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountText))]
    private int _totalFiles;

    [ObservableProperty]
    private string _currentFile = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PercentText))]
    private double _progress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MatchedCount))]
    private int _mismatchCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MatchedCount))]
    private int _missingCount;

    [ObservableProperty]
    private string _redownloadSizeText = "0 B";

    public bool IsBusy => IsChecking || IsRepairing;
    public bool HasProblems => Problems.Count > 0;
    public bool IsClean => IsCheckComplete && !HasProblems;
    /// <summary>A stopped or failed check can still repair what it found; the note says the list may be partial.</summary>
    public bool CanRepair => HasProblems && !IsBusy && !IsRepaired;
    public bool ShowRepair => HasProblems && !IsRepaired;
    public bool CanCheckAgain => !IsBusy;
    public int MatchedCount => Math.Max(0, CheckedFiles - MismatchCount - MissingCount);
    public string CountText => $"{CheckedFiles:N0} / {TotalFiles:N0}";
    public string PercentText => $"{Progress:P0}";
    public string RepairLabel => Problems.Count == 1 ? "Repair 1 file" : $"Repair {Problems.Count:N0} files";
    public string ProblemsNote => IsChecking ? "Still checking — more may appear"
        : !IsCheckComplete && HasProblems ? "Check didn't finish — list may be incomplete"
        : string.Empty;

    public string RepairNote
    {
        get
        {
            if (IsRepaired)
                return "The failed files were downloaded again. Check again to confirm the install matches.";

            if (!IsChecking && !IsCheckComplete && HasProblems)
                return Problems.Count == 1
                    ? "The check didn't finish, so Repair fixes only the 1 file found so far. Check again to find the rest."
                    : $"The check didn't finish, so Repair fixes only the {Problems.Count:N0} files found so far. Check again to find the rest.";

            return "Repair re-downloads only the files that failed. Save files are skipped, so local saves are left alone.";
        }
    }

    public VerifyFilesViewModel(IServiceProvider serviceProvider, Guid gameId, string title, string installDirectory)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<VerifyFilesViewModel>>();
        _navigationService = serviceProvider.GetRequiredService<INavigationService>();
        GameId = gameId;
        Title = title;
        _installDirectory = installDirectory;

        Problems.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasProblems));
            OnPropertyChanged(nameof(IsClean));
            OnPropertyChanged(nameof(CanRepair));
            OnPropertyChanged(nameof(RepairLabel));
            OnPropertyChanged(nameof(ProblemsNote));
            OnPropertyChanged(nameof(RepairNote));
            OnPropertyChanged(nameof(ShowRepair));
        };
    }

    /// <summary>Runs a check. Must be called on the UI thread: progress is marshalled back to it.</summary>
    public async Task StartAsync()
    {
        if (IsBusy)
            return;

        Problems.Clear();
        CheckedFiles = TotalFiles = MismatchCount = MissingCount = 0;
        Progress = 0;
        CurrentFile = string.Empty;
        RedownloadSizeText = "0 B";
        ErrorMessage = null;
        IsCheckComplete = false;
        IsRepaired = false;
        IsChecking = true;
        StatusTitle = "Checking files against the installed archive";

        _cts = new CancellationTokenSource();
        long redownloadBytes = 0;

        var progress = new Progress<ArchiveValidationProgress>(p =>
        {
            CheckedFiles = p.CheckedFiles;
            TotalFiles = p.TotalFiles;
            CurrentFile = p.CurrentFile?.Replace('/', '\\') ?? string.Empty;
            Progress = p.TotalFiles > 0 ? (double)p.CheckedFiles / p.TotalFiles : 1;

            if (p.Conflict != null)
            {
                Problems.Add(new VerifyProblemViewModel(p.Conflict));

                if (p.Conflict.Type == ArchiveValidationConflictType.Missing)
                    MissingCount++;
                else
                    MismatchCount++;

                redownloadBytes += p.Conflict.Length;
                RedownloadSizeText = ByteSize.FromBytes(redownloadBytes).ToString("0.#");
            }
        });

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var gameClient = scope.ServiceProvider.GetRequiredService<GameClient>();

#if DEBUG
            if (IsFixtureRequested)
                await RunFixtureCheckAsync(progress, _cts.Token);
            else
#endif
            await gameClient.ValidateFilesAsync(_installDirectory, GameId, progress, _cts.Token);

            IsCheckComplete = true;
            CurrentFile = string.Empty;
            StatusTitle = Problems.Count == 0 ? "All files match the archive" : "Check complete";
            _logger.LogInformation("File verification for {GameId} ({Title}) found {Count} problem(s)", GameId, Title, Problems.Count);
        }
        catch (OperationCanceledException)
        {
            StatusTitle = "Check stopped";
            _logger.LogInformation("File verification for {GameId} ({Title}) stopped by the user", GameId, Title);
        }
        catch (Exception ex)
        {
            StatusTitle = "Verification failed";
            ErrorMessage = ex.Message;
            _logger.LogError(ex, "Failed to verify files for game {GameId} ({Title})", GameId, Title);
        }
        finally
        {
            IsChecking = false;
            _cts.Dispose();
            _cts = null;
            Finished?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    private void Stop() => _cts?.Cancel();

    [RelayCommand]
    private Task CheckAgainAsync() => StartAsync();

    /// <summary>Re-downloads only the files that failed. Save files were never checked, so they are untouched.</summary>
    [RelayCommand]
    private async Task RepairAsync()
    {
        if (!CanRepair)
            return;

        IsRepairing = true;
        ErrorMessage = null;
        CurrentFile = string.Empty;
        StatusTitle = Problems.Count == 1 ? "Repairing 1 file" : $"Repairing {Problems.Count:N0} files";

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var gameClient = scope.ServiceProvider.GetRequiredService<GameClient>();

#if DEBUG
            if (IsFixtureRequested)
                await RunFixtureRepairAsync();
            else
#endif
            await gameClient.DownloadFilesAsync(
                _installDirectory,
                Problems.Select(p => (p.Conflict.GameId ?? GameId, p.Conflict.FullName)).ToList());

            foreach (var problem in Problems)
                problem.IsRepaired = true;

            RedownloadSizeText = "0 B";
            IsRepaired = true;
            StatusTitle = Problems.Count == 1 ? "Repaired 1 file" : $"Repaired {Problems.Count:N0} files";
            _logger.LogInformation("Repaired {Count} file(s) for game {GameId} ({Title})", Problems.Count, GameId, Title);
        }
        catch (Exception ex)
        {
            StatusTitle = "Repair failed";
            ErrorMessage = ex.Message;
            _logger.LogError(ex, "Failed to repair files for game {GameId} ({Title})", GameId, Title);
        }
        finally
        {
            IsRepairing = false;
            Finished?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    private void Back()
    {
        _cts?.Cancel();
        _navigationService.GoBack();
    }
}

/// <summary>One file that failed verification.</summary>
public partial class VerifyProblemViewModel : ObservableObject
{
    public ArchiveValidationConflict Conflict { get; }

    public VerifyProblemViewModel(ArchiveValidationConflict conflict) => Conflict = conflict;

    /// <summary>Set once Repair has downloaded the file again; the row keeps its place but reads as fixed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMissing), nameof(ShowMismatch), nameof(Detail))]
    private bool _isRepaired;

    public bool IsMissing => Conflict.Type == ArchiveValidationConflictType.Missing;
    public bool ShowMissing => IsMissing && !IsRepaired;
    public bool ShowMismatch => !IsMissing && !IsRepaired;
    public string Path => Conflict.FullName.Replace('/', '\\');
    public string TagText => IsMissing ? "MISSING" : "MISMATCH";
    public string SizeText => Conflict.Length > 0 ? ByteSize.FromBytes(Conflict.Length).ToString("0.#") : string.Empty;

    /// <summary>"crc expected → actual" for a mismatch, "not on disk" when missing, "downloaded again" once repaired.</summary>
    public string Detail => IsRepaired
        ? "downloaded again"
        : IsMissing
        ? "not on disk"
        : $"crc {Conflict.Crc32:x8} → {Conflict.LocalCrc32 ?? 0:x8}";
}
