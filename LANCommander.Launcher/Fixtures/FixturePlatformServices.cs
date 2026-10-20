#if DEBUG
using System;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.Providers;
using Notify.NET.Abstractions;

namespace LANCommander.Launcher.Fixtures;

/// <summary>Swallows notifications so a fixture never pops a toast on the desktop.</summary>
internal sealed class FixtureNotificationService : INotificationService
{
    public bool IsSupported => false;

    public Task<long> ShowAsync(NotificationRequest request, CancellationToken cancellationToken = default) => Task.FromResult(0L);

    public Task HideAsync(long id, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void Dispose() { }
}

/// <summary>Keeps fixtures from driving the real taskbar progress indicator.</summary>
internal sealed class FixtureTaskbarProgressService : ITaskbarProgressService
{
    public bool IsSupported => false;

    public void SetState(TaskbarProgressState state) { }

    public void SetProgress(ulong completed, ulong total) { }

    public void SetProgress(double progress) { }

    public void SetWindow(IntPtr hwnd) { }

    public void Dispose() { }
}

/// <summary>Stands in for the server configuration refresher so nothing is fetched.</summary>
internal sealed class FixtureConfigurationRefresher : IServerConfigurationRefresher
{
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
#endif
