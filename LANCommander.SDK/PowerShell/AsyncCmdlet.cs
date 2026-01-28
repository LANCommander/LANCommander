using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace LANCommander.SDK.PowerShell;

/// <summary>
/// Base class for PowerShell cmdlets that need to execute async code.
/// Provides a ProcessRecordAsync method that can be overridden for async operations.
/// </summary>
public abstract class AsyncCmdlet : PSCmdlet
{
    private CancellationTokenSource? _cancellationTokenSource;

    /// <summary>
    /// Gets the cancellation token for the current operation.
    /// </summary>
    protected CancellationToken CancellationToken => _cancellationTokenSource?.Token ?? CancellationToken.None;

    /// <summary>
    /// Override this method to implement async record processing.
    /// </summary>
    protected abstract Task ProcessRecordAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Processes a single record synchronously by calling the async ProcessRecordAsync method.
    /// </summary>
    protected override void ProcessRecord()
    {
        try
        {
            _cancellationTokenSource = new CancellationTokenSource();
            ProcessRecordAsync(_cancellationTokenSource.Token).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "ProcessRecordError", ErrorCategory.NotSpecified, null));
        }
        finally
        {
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    /// <summary>
    /// Called when the cmdlet is stopping.
    /// </summary>
    protected override void StopProcessing()
    {
        _cancellationTokenSource?.Cancel();
        base.StopProcessing();
    }
}
