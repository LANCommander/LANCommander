using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace LANCommander.UI.Components.InfiniteLoader;

public partial class InfiniteLoader<T> : ComponentBase
{
    [Parameter]
    public Func<T, int, Task<InfiniteLoadResponse<T>>>? Loader { get; set; }

    [Parameter]
    public int PageSize { get; set; } = 10;
    
    [Parameter]
    public Expression<Func<T, string>>? KeySelector { get; set; }
    
    [Parameter]
    public RenderFragment<T>? ChildContent { get; set; }
    
    [Inject]
    private IJSRuntime JS { get; set; }
    
    private readonly List<T> _items = new();
    private T? _next;
    private bool _isLoadingMore;
    private bool _hasMore = true;

    private Func<T, string>? _keySelector;

    private ElementReference _scrollHost;
    private ElementReference _sentinel;

    private InfiniteScrollInterop? _scrollInterop;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        await LoadInitialAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        _scrollInterop = new InfiniteScrollInterop(JS);

        await _scrollInterop.InitializeAsync(_scrollHost, _sentinel);
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

        if (_next is null)
            return;

        var response = await Loader(_next, PageSize);

        _items.Clear();
        
        if (response.Items is not null)
            _items.AddRange(response.Items);
        
        _hasMore = response.HasMore;
        _next = response.Next;
    }

    private async Task LoadMoreAsync(object? anchor)
    {
        if (_isLoadingMore || !_hasMore || Loader is null || _next is null)
            return;
        
        _isLoadingMore = true;

        try
        {
            var page = await Loader.Invoke(_next, PageSize);

            if (page.Items is not null)
                _items.InsertRange(0, page.Items);

            _hasMore = page.HasMore;
            _next = page.Next;

            await InvokeAsync(StateHasChanged);
            await _scrollInterop!.RestoreAfterPrependAsync(_scrollHost, anchor);
        }
        finally
        {
            _isLoadingMore = false;
        }
    }

    [JSInvokable]
    public async Task OnSentinelVisible()
    {
        if (_scrollInterop is null)
            return;

        var anchor = await _scrollInterop.CaptureAnchorAsync(_scrollHost);

        await LoadMoreAsync(anchor);
    }

    public async ValueTask DisposeAsync()
    {
        if (_scrollInterop is not null)
            await _scrollInterop.DisposeAsync();
    }
}