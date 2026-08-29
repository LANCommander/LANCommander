using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Services;
using LANCommander.Launcher.Services.Packaging;
using LANCommander.Packaging.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.ViewModels.Packaging;

/// <summary>
/// Drives the packaging wizard: monitor an installer, choose what it produced, describe it, and
/// turn it into an .lcx.
/// </summary>
/// <remarks>
/// A single navigation target rather than one per step. The navigation service is a history
/// stack for app-level pages, so pushing seven steps onto it would let the titlebar back button
/// walk the user out of a live capture.
/// </remarks>
public partial class PackagingWizardViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PackagingWizardViewModel> _logger;

    public PackagingWizardViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<PackagingWizardViewModel>>();

        Session = serviceProvider.GetRequiredService<IPackagingSessionService>();

        Steps =
        [
            new MonitorStepViewModel(this, serviceProvider),
            new InstallDirectoryStepViewModel(this),
            new FileSelectionStepViewModel(this),
            new RegistrySelectionStepViewModel(this),
            new MetadataStepViewModel(this, serviceProvider),
            new ActionStepViewModel(this),
            new OutputStepViewModel(this, serviceProvider),
        ];

        CurrentStep = Steps[0];
    }

    public IPackagingSessionService Session { get; }

    public PackageDefinition Package { get; private set; } = new();

    public ObservableCollection<PackagingStepViewModel> Steps { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepNumber))]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    [NotifyPropertyChangedFor(nameof(NextLabel))]
    [NotifyPropertyChangedFor(nameof(IsLastStep))]
    private PackagingStepViewModel _currentStep;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// Steps that apply to the package being built. A step with nothing to show — an empty
    /// registry capture, say — is skipped rather than presented blank.
    /// </summary>
    public IReadOnlyList<PackagingStepViewModel> ApplicableSteps =>
        [.. Steps.Where(s => s.IsApplicable)];

    public int StepNumber => ApplicableSteps.ToList().IndexOf(CurrentStep) + 1;

    public int StepCount => ApplicableSteps.Count;

    public bool IsLastStep => FindAdjacentStep(forward: true) == null;

    public bool CanGoBack => CurrentStep.CanGoBack && FindAdjacentStep(forward: false) != null;

    /// <summary>
    /// The next or previous applicable step, or null at either end.
    /// </summary>
    private PackagingStepViewModel? FindAdjacentStep(bool forward)
    {
        var index = Steps.IndexOf(CurrentStep);

        if (index < 0)
            return null;

        for (var i = forward ? index + 1 : index - 1;
             i >= 0 && i < Steps.Count;
             i += forward ? 1 : -1)
        {
            if (Steps[i].IsApplicable)
                return Steps[i];
        }

        return null;
    }

    public string NextLabel => CurrentStep.NextLabel;

    /// <summary>
    /// Prepares a brand new package. Called each time the wizard is opened.
    /// </summary>
    public async Task ResetAsync()
    {
        await Session.StopAsync();

        Session.Reset();

        Package = new PackageDefinition();
        ErrorMessage = null;

        foreach (var step in Steps)
            step.CanGoNext = true;

        CurrentStep = Steps[0];

        await CurrentStep.OnEnterAsync();
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        if (!CurrentStep.CanGoNext || IsBusy)
            return;

        IsBusy = true;

        try
        {
            // Persist this step's edits first: whether a later step applies often depends on
            // what this one just wrote into the package.
            await CurrentStep.OnLeaveAsync();

            var next = FindAdjacentStep(forward: true);

            if (next == null)
                return;

            CurrentStep = next;

            await CurrentStep.OnEnterAsync();

            RefreshNavigation();

            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error advancing the packaging wizard");

            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task BackAsync()
    {
        if (!CanGoBack || IsBusy)
            return;

        IsBusy = true;

        try
        {
            var previous = FindAdjacentStep(forward: false);

            if (previous == null)
                return;

            CurrentStep = previous;

            await CurrentStep.OnEnterAsync();

            RefreshNavigation();

            ErrorMessage = null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Re-evaluates the derived navigation state. Which steps apply can change as the package
    /// is filled in, so these cannot be computed once.
    /// </summary>
    private void RefreshNavigation()
    {
        OnPropertyChanged(nameof(ApplicableSteps));
        OnPropertyChanged(nameof(StepNumber));
        OnPropertyChanged(nameof(StepCount));
        OnPropertyChanged(nameof(IsLastStep));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(NextLabel));
    }

    /// <summary>
    /// Stops any capture in progress. Called when the wizard is navigated away from, so a
    /// forgotten session cannot keep injecting in the background.
    /// </summary>
    public async Task ShutdownAsync()
    {
        try
        {
            await Session.StopAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping the packaging session");
        }
    }
}
