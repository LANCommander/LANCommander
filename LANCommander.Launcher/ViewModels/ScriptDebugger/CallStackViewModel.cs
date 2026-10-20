using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LANCommander.Launcher.ViewModels.ScriptDebugger.Items;
using LANCommander.SDK.PowerShell.Debugging;

namespace LANCommander.Launcher.ViewModels.ScriptDebugger;

public sealed partial class CallStackViewModel : ObservableObject
{
    public ObservableCollection<CallStackFrameViewModel> Frames { get; } = new();

    [ObservableProperty]
    private CallStackFrameViewModel? _selectedFrame;

    /// <summary>Raised when the user picks a frame, so the editor and Variables panel can follow.</summary>
    public event Action<CallStackFrameViewModel?>? FrameSelected;

    partial void OnSelectedFrameChanged(CallStackFrameViewModel? value) => FrameSelected?.Invoke(value);

    public void Show(IReadOnlyList<CallStackFrameInfo> frames)
    {
        Frames.Clear();

        foreach (var frame in frames)
            Frames.Add(new CallStackFrameViewModel(frame));

        SelectedFrame = Frames.FirstOrDefault();
    }

    public void Clear()
    {
        Frames.Clear();
        SelectedFrame = null;
    }
}
