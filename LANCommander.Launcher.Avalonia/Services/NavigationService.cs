using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using LANCommander.Launcher.Avalonia.ViewModels;

namespace LANCommander.Launcher.Avalonia.Services;

public record NavigationEntry(ViewModelBase ViewModel);

public partial class NavigationService : ObservableObject, INavigationService
{
    private const int MaxHistoryDepth = 20;

    private readonly Stack<NavigationEntry> _history = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    private ViewModelBase? _currentView;

    [ObservableProperty]
    private bool _isNavigating;

    public bool CanGoBack => _history.Count > 0;

    public async void NavigateTo(ViewModelBase viewModel, bool clearHistory = false, Func<Task>? initializeAsync = null)
    {
        if (clearHistory)
        {
            _history.Clear();
        }
        else if (CurrentView != null)
        {
            _history.Push(new NavigationEntry(CurrentView));

            // Trim oldest entries if we exceed the cap
            if (_history.Count > MaxHistoryDepth)
            {
                var trimmed = new Stack<NavigationEntry>(MaxHistoryDepth);
                var items = _history.ToArray();
                for (int i = MaxHistoryDepth - 1; i >= 0; i--)
                    trimmed.Push(items[i]);

                _history.Clear();
                foreach (var entry in trimmed)
                    _history.Push(entry);
            }
        }

        // Show the loading overlay and yield so the UI thread can paint it
        // BEFORE the heavy view creation from the ContentControl DataTemplate.
        IsNavigating = true;
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render).GetTask();

        CurrentView = viewModel;

        try
        {
            if (initializeAsync != null)
                await initializeAsync();

            // Yield again so the new view finishes layout/render
            // before we hide the overlay.
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background).GetTask();
        }
        catch
        {
            // Initialization errors are handled by the caller's callback.
        }
        finally
        {
            IsNavigating = false;
        }
    }

    public void GoBack()
    {
        if (_history.Count == 0)
            return;

        IsNavigating = true;

        // Post the actual view swap so the overlay paints first,
        // then yield after layout to hide it.
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                var entry = _history.Pop();
                CurrentView = entry.ViewModel;
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background).GetTask();
            }
            finally
            {
                IsNavigating = false;
            }
        }, DispatcherPriority.Background);
    }

    public void ReplaceCurrent(ViewModelBase viewModel)
    {
        CurrentView = viewModel;
    }
}
