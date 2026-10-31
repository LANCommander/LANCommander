using LANCommander.Server.UI.Extensions;
using LANCommander.Server.UI.Providers;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using XtermBlazor;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace LANCommander.Server.UI.Components;

public partial class Terminal : Xterm, IAsyncDisposable
{
    [Inject]
    ScriptProvider ScriptProvider { get; set; }

    /// <summary>
    /// Loads the readline addon for <see cref="ReadLineAsync"/>. Turn it off for a terminal whose
    /// keystrokes go elsewhere as they are typed, e.g. to a shell, which readline would hold back.
    /// </summary>
    [Parameter]
    public bool Readline { get; set; } = true;

    /// <summary>Refits the terminal to its element whenever the element changes size.</summary>
    [Parameter]
    public bool FitToElement { get; set; }

    IJSObjectReference? _interop;
    IJSObjectReference? _resizeObserver;
    DotNetObjectReference<Terminal>? _self;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _interop ??= await ScriptProvider.ImportModuleAsync<Terminal>();

            if (Addons == null)
                Addons = new HashSet<string>();

            if (Readline)
                Addons.Add("readline");

            Addons.Add("addon-fit");
        }
        
        await base.OnAfterRenderAsync(firstRender);
        
        if (firstRender)
        {
            await ApplyLineHeightAsync();
            await FitAsync();

            if (FitToElement)
                await ObserveResizeAsync();
        }
    }

    async Task ObserveResizeAsync()
    {
        if (_interop == null)
            return;

        try
        {
            _self = DotNetObjectReference.Create(this);
            _resizeObserver = await _interop.InvokeAsync<IJSObjectReference>("ObserveResize", ElementReference, _self);
        }
        catch (JSException)
        {
            // A stale bundle without ObserveResize keeps the size the terminal first fitted to
        }
    }

    /// <summary>Called from the module as the terminal's element changes size.</summary>
    [JSInvokable]
    public async Task ElementResized()
    {
        try
        {
            await FitAsync();
        }
        catch (JSException)
        {
            // The terminal is going
        }
    }

    public new async ValueTask DisposeAsync()
    {
        try
        {
            if (_resizeObserver != null)
            {
                await _resizeObserver.InvokeVoidAsync("Dispose");
                await _resizeObserver.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
            // The circuit is gone, and the page with it
        }

        _self?.Dispose();

        await base.DisposeAsync();
    }

    /// <summary>
    /// The consoles' 19px lines on 12px text. xterm takes the line height as a multiple, which
    /// XtermBlazor types as a whole number, so it is set through the module instead.
    /// </summary>
    async Task ApplyLineHeightAsync()
    {
        if (_interop == null)
            return;

        try
        {
            await _interop.InvokeVoidAsync("SetOption", Id, "lineHeight", ConsolePalette.LineHeight);
        }
        catch (JSException)
        {
            // A stale bundle without SetOption keeps xterm's own line height
        }
    }

    /// <summary>Writes a line coloured for its log level; see <see cref="ConsolePalette"/>.</summary>
    public Task WriteLine(string message, LogLevel level = LogLevel.Information) =>
        WriteLine(message, ConsolePalette.KindOf(level));

    /// <summary>Writes a line coloured for what it is, e.g. a warning, or the runner's own messages.</summary>
    public async Task WriteLine(string message, ConsoleLineKind kind)
    {
        foreach (var line in ConsolePalette.Format(message, kind))
            await base.WriteLine(line);
    }

    /// <summary>
    /// Rerenders the terminal and changes the size to fit within the element's rect. Utilizes the `addon-fit` addon.
    /// </summary>
    public async Task FitAsync()
        => await Addon("addon-fit").InvokeVoidAsync("fit");
    
    /// <summary>
    /// Wait for user input in the terminal
    /// </summary>
    /// <param name="prefix"></param>
    public async Task<string> ReadLineAsync(string prefix = "> ")
        => await Addon("readline").InvokeAsync<string>("read", prefix);
}