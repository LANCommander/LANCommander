using System.Linq.Expressions;
using LANCommander.SDK.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace LANCommander.UI.Components;

public partial class InfiniteLoader<T> : BaseComponent
{
    [Parameter]
    public Func<T, int, Task<InfiniteResponse<T>>>? Loader { get; set; }

    [Parameter]
    public int PageSize { get; set; } = 10;
    
    [Parameter]
    public Expression<Func<T, string>>? KeySelector { get; set; }
    
    [Parameter]
    public RenderFragment<T>? ChildContent { get; set; }
    
    private readonly List<T> _items = new();
    private T? _next;
    private bool _isLoadingMore;
    private bool _hasMore = true;

    private Func<T, string>? _keySelector;

    private ElementReference _scrollHost;
    private ElementReference _sentinel;

    private IJSObjectReference? _scrollInterop;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        await LoadInitialAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _scrollInterop ??= await ScriptProvider.ImportModuleAsync(this, "InfiniteScroll", _scrollHost, _sentinel);

            await _scrollInterop.InvokeVoidAsync("ObserveSentinel");
            
            // Scroll to bottom to show newest messages
            await Task.Delay(50);
            await ScrollToBottomAsync();
            
            // Check if sentinel is visible (content doesn't fill container) and load more if needed
            // Use a small delay to ensure DOM is fully rendered
            await Task.Delay(100);
            await CheckAndLoadMoreIfNeededAsync();
        }
    }
    
    private async Task ScrollToBottomAsync()
    {
        if (_scrollInterop == null)
            return;
        
        try
        {
            await _scrollInterop.InvokeVoidAsync("ScrollToBottom");
        }
        catch
        {
            // Ignore errors - method might not exist yet
        }
    }
    
    private async Task CheckAndLoadMoreIfNeededAsync()
    {
        if (_scrollInterop == null || _isLoadingMore || !_hasMore || Loader == null)
            return;
        
        try
        {
            // Check if sentinel is visible (content doesn't fill viewport)
            var isSentinelVisible = await _scrollInterop.InvokeAsync<bool>("IsSentinelVisible");
            
            if (isSentinelVisible)
            {
                // Content doesn't fill container, load more messages
                var anchor = await _scrollInterop.InvokeAsync<object?>("CaptureAnchor");
                await LoadMoreAsync(anchor);
            }
        }
        catch
        {
            // Ignore errors - method might not exist yet or sentinel check failed
        }
    }

    protected override void OnParametersSet()
    {
        if (KeySelector is null)
            throw new ArgumentNullException(nameof(KeySelector));
        
        _keySelector = KeySelector.Compile();
    }

    private async Task LoadInitialAsync()
    {
        if (Loader == null)
            throw new ArgumentNullException($"{nameof(Loader)} is null");

        var response = await Loader(_next, PageSize);

        _items.Clear();
        
        if (response.Items is not null)
        {
            // Filter out duplicates using the key selector (in case SignalR already added some)
            if (_keySelector is not null)
            {
                var seenKeys = new HashSet<string>();
                var uniqueItems = response.Items.Where(item =>
                {
                    var key = _keySelector(item);
                    return seenKeys.Add(key);
                }).ToList();
                
                // Reverse the list so oldest messages are at the top and newest at the bottom
                uniqueItems.Reverse();
                _items.AddRange(uniqueItems);
            }
            else
            {
                var itemsList = response.Items.ToList();
                // Reverse the list so oldest messages are at the top and newest at the bottom
                itemsList.Reverse();
                _items.AddRange(itemsList);
            }
        }
        
        _hasMore = response.HasMore;
        
        // Set cursor to the oldest message (first in list after reversal) for loading older messages
        if (_items.Count > 0)
        {
            _next = _items[0];
        }
    }

    private async Task LoadMoreAsync(object? anchor)
    {
        if (_isLoadingMore || !_hasMore || Loader is null || _scrollInterop is null)
            return;
        
        _isLoadingMore = true;

        try
        {
            var page = await Loader.Invoke(_next, PageSize);

            if (page.Items is not null && _keySelector is not null)
            {
                // Filter out duplicates using the key selector
                var existingKeys = new HashSet<string>(_items.Select(_keySelector));
                var newItems = page.Items.Where(item => !existingKeys.Contains(_keySelector(item))).ToList();
                
                if (newItems.Count > 0)
                {
                    // Reverse new items so oldest is first, then insert at the beginning
                    newItems.Reverse();
                    _items.InsertRange(0, newItems);
                    
                    // Update cursor to the oldest message (first in list) for next load
                    _next = _items[0];
                }
            }
            else if (page.Items is not null)
            {
                var itemsList = page.Items.ToList();
                // Reverse new items so oldest is first, then insert at the beginning
                itemsList.Reverse();
                _items.InsertRange(0, itemsList);
                
                // Update cursor to the oldest message (first in list) for next load
                if (_items.Count > 0)
                {
                    _next = _items[0];
                }
            }

            _hasMore = page.HasMore;

            await InvokeAsync(StateHasChanged);
            
            await _scrollInterop.InvokeVoidAsync("RestoreAfterPrepend", anchor);
            
            _isLoadingMore = false;
            
            // After loading and rendering, check if we need to load more to fill the viewport
            await Task.Delay(50);
            await CheckAndLoadMoreIfNeededAsync();
        }
        finally
        {
            _isLoadingMore = false;
        }
    }

    [JSInvokable("OnSentinelVisible")]
    public async Task OnSentinelVisible()
    {
        if (_scrollInterop is null)
            return;

        var anchor = await _scrollInterop.InvokeAsync<object?>("CaptureAnchor");

        await LoadMoreAsync(anchor);
    }

    public async ValueTask DisposeAsync()
    {
        if (_scrollInterop is not null)
            await _scrollInterop.DisposeAsync();
    }
}