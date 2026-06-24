using System;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Services;
using Microsoft.Extensions.Logging;
using Notify.NET.Abstractions;

namespace LANCommander.Launcher.Services;

/// <summary>
/// Drives the OS taskbar/Dock progress indicator for the active download,
/// backed by Notify.NET's cross-platform <see cref="ITaskbarProgressService"/>.
/// </summary>
public class TaskbarProgressService
{
    private readonly ITaskbarProgressService _taskbar;
    private readonly ILogger<TaskbarProgressService> _logger;

    public TaskbarProgressService(ITaskbarProgressService taskbar, ILogger<TaskbarProgressService> logger)
    {
        _taskbar = taskbar;
        _logger = logger;
    }

    public void Initialize(IntPtr hwnd)
    {
        _logger.LogInformation("TaskbarProgress.Initialize: IsSupported={IsSupported}, hwnd={Hwnd}", _taskbar.IsSupported, hwnd);

        if (!_taskbar.IsSupported)
            return;

        try
        {
            _taskbar.SetWindow(hwnd);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to bind taskbar progress to window handle");
        }
    }

    /// <summary>
    /// Reflects the current install item's progress and status on the taskbar:
    /// indeterminate phases pulse, active transfers show a value, failures turn red.
    /// </summary>
    public void Report(InstallProgress progress)
    {
        if (!_taskbar.IsSupported)
            return;

        try
        {
            _logger.LogDebug("TaskbarProgress.Report: Status={Status}, Indeterminate={Indeterminate}, Progress={Progress}", progress.Status, progress.Indeterminate, progress.Progress);

            switch (progress.Status)
            {
                case InstallStatus.Failed:
                    _taskbar.SetState(TaskbarProgressState.Error);
                    break;
                case InstallStatus.Canceled:
                case InstallStatus.Complete:
                    _taskbar.SetState(TaskbarProgressState.None);
                    break;
                default:
                    // Progress is BytesTransferred/TotalBytes and is NaN before the total is
                    // known (division by zero). Treat that—and any non-finite value—as an
                    // indeterminate pulse rather than feeding NaN to the native indicator.
                    if (progress.Indeterminate || float.IsNaN(progress.Progress) || float.IsInfinity(progress.Progress))
                        _taskbar.SetState(TaskbarProgressState.Indeterminate);
                    else
                        _taskbar.SetProgress(progress.Progress);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update taskbar progress");
        }
    }

    public void SetError()
    {
        if (!_taskbar.IsSupported)
            return;

        try
        {
            _taskbar.SetState(TaskbarProgressState.Error);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set taskbar error state");
        }
    }

    public void ClearProgress()
    {
        if (!_taskbar.IsSupported)
            return;

        try
        {
            _taskbar.SetState(TaskbarProgressState.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear taskbar progress");
        }
    }
}
