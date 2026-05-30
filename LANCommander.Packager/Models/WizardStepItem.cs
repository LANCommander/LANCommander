using System.ComponentModel;

namespace LANCommander.Packager.Models;

public enum StepState { Pending, Current, Completed }

public class WizardStepItem : INotifyPropertyChanged
{
    private StepState _state = StepState.Pending;

    public int Index { get; init; }
    public string Title { get; init; } = "";
    public bool ShowTopLine => Index > 0;
    public bool ShowBottomLine => Index < 6;

    public StepState State
    {
        get => _state;
        set
        {
            if (_state != value)
            {
                _state = value;
                
                PropertyChanged?.Invoke(this, new(nameof(State)));
                PropertyChanged?.Invoke(this, new(nameof(IsCurrent)));
                PropertyChanged?.Invoke(this, new(nameof(IsCompleted)));
                PropertyChanged?.Invoke(this, new(nameof(IsPending)));
                PropertyChanged?.Invoke(this, new(nameof(TextOpacity)));
            }
        }
    }

    public bool IsCurrent => State == StepState.Current;
    public bool IsCompleted => State == StepState.Completed;
    public bool IsPending => State == StepState.Pending;

    public double TextOpacity => State switch
    {
        StepState.Current => 1.0,
        StepState.Completed => 0.7,
        _ => 0.4
    };

    public event PropertyChangedEventHandler? PropertyChanged;
}
