using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using LANCommander.Packaging.Models;

namespace LANCommander.Launcher.ViewModels.Packaging;

/// <summary>
/// One step of the packaging wizard.
/// </summary>
/// <remarks>
/// Steps share a single <see cref="PackageDefinition"/> rather than passing values between
/// themselves, so going back and forward does not lose edits.
/// </remarks>
public abstract partial class PackagingStepViewModel : ViewModelBase
{
    protected PackagingStepViewModel(PackagingWizardViewModel wizard)
    {
        Wizard = wizard;
    }

    protected PackagingWizardViewModel Wizard { get; }

    protected PackageDefinition Package => Wizard.Package;

    /// <summary>Shown in the step rail.</summary>
    public abstract string Title { get; }

    [ObservableProperty]
    private bool _canGoNext = true;

    /// <summary>Whether Back is meaningful here. Monitoring cannot be re-entered.</summary>
    public virtual bool CanGoBack => true;

    /// <summary>
    /// Whether this step has anything to offer for the current package. Steps that return false
    /// are skipped in both directions rather than shown empty.
    /// </summary>
    public virtual bool IsApplicable => true;

    /// <summary>Label for the forward button, so the last step can say "Finish".</summary>
    public virtual string NextLabel => "Next";

    /// <summary>Called when the step becomes visible.</summary>
    public virtual Task OnEnterAsync() => Task.CompletedTask;

    /// <summary>
    /// Called before leaving forward. Persists the step's edits into the shared package.
    /// </summary>
    public virtual Task OnLeaveAsync() => Task.CompletedTask;
}
