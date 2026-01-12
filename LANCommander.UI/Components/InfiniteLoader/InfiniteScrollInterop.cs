using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace LANCommander.UI.Components;

public sealed class InfiniteScrollInterop(IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? _module;
    private IJSObjectReference? _observerHandle;

    public async Task InitializeAsync(ElementReference scrollHost, ElementReference sentinel)
    {
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/infiniteScroll.js");
        _observerHandle = await _module.InvokeAsync<IJSObjectReference>("observeSentinel", scrollHost, sentinel);
    }

    public async Task<object?> CaptureAnchorAsync(ElementReference scrollHost)
    {
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/infiniteScroll.js"); 
        return await _module.InvokeAsync<IJSObjectReference>("CaptureAnchor", scrollHost);
    }

    public async Task RestoreAfterPrependAsync(ElementReference scrollHost, object? anchor)
    {
        if (anchor is null)
            return;
        
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/infiniteScroll.js");
        
        await _module.InvokeVoidAsync("RestoreAfterPrepend", scrollHost, anchor);
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_observerHandle is not null)
            await _observerHandle.InvokeVoidAsync("dispose");
        
        if (_module is not null)
            await _module.DisposeAsync();
    }
}