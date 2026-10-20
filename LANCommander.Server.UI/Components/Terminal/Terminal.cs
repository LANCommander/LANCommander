using LANCommander.Server.UI.Extensions;
using LANCommander.Server.UI.Providers;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using XtermBlazor;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace LANCommander.Server.UI.Components;

public partial class Terminal : Xterm
{
    [Inject]
    ScriptProvider ScriptProvider { get; set; }

    IJSObjectReference? _interop;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _interop ??= await ScriptProvider.ImportModuleAsync<Terminal>();

            if (Addons == null)
                Addons = new HashSet<string>();

            Addons.Add("readline");
            Addons.Add("addon-fit");
        }
        
        await base.OnAfterRenderAsync(firstRender);
        
        if (firstRender)
        {
            await ApplyLineHeightAsync();
            await FitAsync();
        }
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