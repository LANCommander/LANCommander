using LANCommander.UI.Providers;
using Microsoft.JSInterop;

namespace LANCommander.UI.Services;

public class UploadTracker : IAsyncDisposable
{
    private readonly ScriptProvider _scriptProvider;
    private IJSObjectReference? _managerInterop;
    private DotNetObjectReference<UploadTracker>? _dotNetRef;
    private bool _initialized;

    public Dictionary<string, BackgroundUploadInfo> ActiveUploads { get; } = new();

    public event Action? OnStateChanged;
    public event Func<BackgroundUploadInfo, Task>? OnUploadCompleted;

    public UploadTracker(ScriptProvider scriptProvider)
    {
        _scriptProvider = scriptProvider;
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        _dotNetRef = DotNetObjectReference.Create(this);
        _managerInterop = await _scriptProvider.ImportModuleAsync("UploadManager");
        await _managerInterop.InvokeVoidAsync("Initialize", _dotNetRef);
        _initialized = true;
    }

    public async Task<string> StartUploadAsync(
        string fileInputId,
        Guid storageLocationId,
        string fileName,
        UploadType type,
        string? objectKey = null,
        Func<string, Task>? onCompleted = null)
    {
        await InitializeAsync();

        var uploadId = Guid.NewGuid().ToString();

        var info = new BackgroundUploadInfo
        {
            UploadId = uploadId,
            FileName = fileName,
            Type = type,
            Status = UploadStatus.Uploading,
            OnCompleted = onCompleted,
        };

        ActiveUploads[uploadId] = info;
        OnStateChanged?.Invoke();

        await _managerInterop!.InvokeVoidAsync("StartUpload", uploadId, fileInputId, storageLocationId.ToString(), objectKey ?? "");

        return uploadId;
    }

    public async Task CancelUploadAsync(string uploadId)
    {
        if (_managerInterop != null)
            await _managerInterop.InvokeVoidAsync("CancelUpload", uploadId);

        ActiveUploads.Remove(uploadId);
        OnStateChanged?.Invoke();
    }

    public bool HasActiveUploads => ActiveUploads.Values.Any(u => u.Status == UploadStatus.Uploading);

    [JSInvokable]
    public async Task JSOnUploadProgress(string uploadId, int percent, double rate)
    {
        if (ActiveUploads.TryGetValue(uploadId, out var info))
        {
            info.Percent = percent;

            // Preserve the last known speed when rate is 0 (between chunks)
            if (rate > 0)
                info.Speed = rate;

            OnStateChanged?.Invoke();
        }

        await Task.CompletedTask;
    }

    [JSInvokable]
    public async Task JSOnUploadComplete(string uploadId, string objectKey)
    {
        if (ActiveUploads.TryGetValue(uploadId, out var info))
        {
            info.Status = UploadStatus.Complete;
            info.Percent = 100;
            info.CompletedObjectKey = objectKey;
            OnStateChanged?.Invoke();

            if (info.OnCompleted != null)
            {
                try
                {
                    await info.OnCompleted(objectKey);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Upload completion handler failed: {ex.Message}");
                }
            }

            if (OnUploadCompleted != null)
                await OnUploadCompleted.Invoke(info);

            // Keep completed uploads in the list so the UI can show them.
            // The UploadIndicator handles dismissal.
            OnStateChanged?.Invoke();
        }
    }

    [JSInvokable]
    public async Task JSOnUploadError(string uploadId, string message)
    {
        if (ActiveUploads.TryGetValue(uploadId, out var info))
        {
            info.Status = UploadStatus.Error;
            info.ErrorMessage = message;
            OnStateChanged?.Invoke();
        }

        await Task.CompletedTask;
    }

    public void RemoveUpload(string uploadId)
    {
        ActiveUploads.Remove(uploadId);
        OnStateChanged?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        if (_managerInterop != null)
            await _managerInterop.DisposeAsync();

        _dotNetRef?.Dispose();
    }
}
