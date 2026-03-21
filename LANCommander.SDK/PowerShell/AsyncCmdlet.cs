using System;
using System.Collections.Concurrent;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace LANCommander.SDK.PowerShell;

/// <summary>
/// Base class for PowerShell cmdlets that need to execute async code.
/// Based on <see href="https://github.com/jborean93/PowerShell-OpenAuthenticode">PowerShell-OpenAuthenticode</see> AsyncPSCmdlet.
/// Override BeginProcessingAsync, ProcessRecordAsync, and/or EndProcessingAsync; WriteObject, WriteError, etc.
/// can be called from async code and are marshalled to the pipeline thread.
/// </summary>
public abstract class AsyncCmdlet : PSCmdlet, IDisposable
{
    private enum PipelineType
    {
        Output,
        OutputEnumerate,
        Error,
        Warning,
        Verbose,
        Debug,
        Information,
        Progress,
        ShouldProcess,
    }

    private readonly CancellationTokenSource _cancelSource = new();
    private BlockingCollection<(object?, PipelineType)>? _currentOutPipe;
    private BlockingCollection<object?>? _currentReplyPipe;

    /// <summary>
    /// Gets the cancellation token for the current operation. Canceled when the cmdlet is stopped.
    /// </summary>
    protected CancellationToken CancellationToken => _cancelSource.Token;

    /// <summary>
    /// Override to perform async startup. Default implementation returns a completed task.
    /// </summary>
    protected override void BeginProcessing()
    {
        SessionState.PSVariable.Set("LANCommander.SDK.PSHostUI", Host.UI);
        RunBlockInAsync(BeginProcessingAsync);
    }

    /// <summary>
    /// Override to perform async startup.
    /// </summary>
    protected virtual Task BeginProcessingAsync() => Task.CompletedTask;

    /// <summary>
    /// Processes a single record by running ProcessRecordAsync and consuming pipeline output on the pipeline thread.
    /// </summary>
    protected override void ProcessRecord() => RunBlockInAsync(() => ProcessRecordAsync(CancellationToken));

    /// <summary>
    /// Override to implement async record processing.
    /// </summary>
    protected abstract Task ProcessRecordAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Override to perform async cleanup. Default implementation returns a completed task.
    /// </summary>
    protected override void EndProcessing() => RunBlockInAsync(EndProcessingAsync);

    /// <summary>
    /// Override to perform async cleanup.
    /// </summary>
    protected virtual Task EndProcessingAsync() => Task.CompletedTask;

    /// <summary>
    /// Called when the cmdlet is stopping. Cancels the cancellation token.
    /// </summary>
    protected override void StopProcessing()
    {
        _cancelSource.Cancel();
        base.StopProcessing();
    }

    private void RunBlockInAsync(Func<Task> task)
    {
        using var outPipe = new BlockingCollection<(object?, PipelineType)>();
        using var replyPipe = new BlockingCollection<object?>();
        var blockTask = Task.Run(async () =>
        {
            try
            {
                _currentOutPipe = outPipe;
                _currentReplyPipe = replyPipe;
                await task();
            }
            finally
            {
                _currentOutPipe = null;
                _currentReplyPipe = null;
                outPipe.CompleteAdding();
                replyPipe.CompleteAdding();
            }
        });

        try
        {
            foreach (var (data, pipelineType) in outPipe.GetConsumingEnumerable(_cancelSource.Token))
            {
                switch (pipelineType)
                {
                    case PipelineType.Output:
                        base.WriteObject(data);
                        break;
                    case PipelineType.OutputEnumerate:
                        base.WriteObject(data, true);
                        break;
                    case PipelineType.Error:
                        base.WriteError((ErrorRecord)data!);
                        break;
                    case PipelineType.Warning:
                        base.WriteWarning((string)data!);
                        break;
                    case PipelineType.Verbose:
                        base.WriteVerbose((string)data!);
                        break;
                    case PipelineType.Debug:
                        base.WriteDebug((string)data!);
                        break;
                    case PipelineType.Information:
                        base.WriteInformation((InformationRecord)data!);
                        break;
                    case PipelineType.Progress:
                        base.WriteProgress((ProgressRecord)data!);
                        break;
                    case PipelineType.ShouldProcess:
                        var (target, action) = (ValueTuple<string, string>)data!;
                        var res = base.ShouldProcess(target, action);
                        replyPipe.Add(res, _cancelSource.Token);
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when StopProcessing cancels
        }

        try
        {
            blockTask.GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            base.WriteError(new ErrorRecord(ex, "ProcessRecordError", ErrorCategory.NotSpecified, null));
        }
    }

    /// <summary>
    /// Writes an object to the pipeline. Safe to call from async code; marshalled to the pipeline thread.
    /// </summary>
    public new void WriteObject(object? sendToPipeline) => WriteObject(sendToPipeline, false);

    /// <summary>
    /// Writes an object to the pipeline. Safe to call from async code; marshalled to the pipeline thread.
    /// </summary>
    public new void WriteObject(object? sendToPipeline, bool enumerateCollection)
    {
        ThrowIfStopped();
        _currentOutPipe?.Add((sendToPipeline, enumerateCollection ? PipelineType.OutputEnumerate : PipelineType.Output));
    }

    /// <summary>
    /// Writes an error record. Safe to call from async code; marshalled to the pipeline thread.
    /// </summary>
    public new void WriteError(ErrorRecord errorRecord)
    {
        ThrowIfStopped();
        _currentOutPipe?.Add((errorRecord, PipelineType.Error));
    }

    /// <summary>
    /// Writes a warning. Safe to call from async code; marshalled to the pipeline thread.
    /// </summary>
    public new void WriteWarning(string message)
    {
        ThrowIfStopped();
        _currentOutPipe?.Add((message, PipelineType.Warning));
    }

    /// <summary>
    /// Writes verbose output. Safe to call from async code; marshalled to the pipeline thread.
    /// </summary>
    public new void WriteVerbose(string message)
    {
        ThrowIfStopped();
        _currentOutPipe?.Add((message, PipelineType.Verbose));
    }

    /// <summary>
    /// Writes debug output. Safe to call from async code; marshalled to the pipeline thread.
    /// </summary>
    public new void WriteDebug(string message)
    {
        ThrowIfStopped();
        _currentOutPipe?.Add((message, PipelineType.Debug));
    }

    /// <summary>
    /// Writes an information record. Safe to call from async code; marshalled to the pipeline thread.
    /// </summary>
    public new void WriteInformation(InformationRecord informationRecord)
    {
        ThrowIfStopped();
        _currentOutPipe?.Add((informationRecord, PipelineType.Information));
    }

    /// <summary>
    /// Writes a progress record. Safe to call from async code; marshalled to the pipeline thread.
    /// </summary>
    public new void WriteProgress(ProgressRecord progressRecord)
    {
        ThrowIfStopped();
        _currentOutPipe?.Add((progressRecord, PipelineType.Progress));
    }

    /// <summary>
    /// Confirms an operation with the user. Safe to call from async code; blocks until the pipeline thread returns the result.
    /// </summary>
    public new bool ShouldProcess(string target, string action)
    {
        ThrowIfStopped();
        _currentOutPipe?.Add(((target, action), PipelineType.ShouldProcess));
        return (bool)_currentReplyPipe?.Take(CancellationToken)!;
    }

    private void ThrowIfStopped()
    {
        if (_cancelSource.IsCancellationRequested)
            throw new PipelineStoppedException();
    }

    /// <summary>
    /// Disposes the cancellation source.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
            _cancelSource.Dispose();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
