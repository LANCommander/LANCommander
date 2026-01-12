using LANCommander.UI.Extensions;
using LANCommander.UI.Providers;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using XtermBlazor;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace LANCommander.UI.Components;

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
            await FitAsync();
    }

    /// <summary>
    /// Write a line to the terminal with a specified log level
    /// </summary>
    /// <param name="message">Message to send</param>
    /// <param name="level">LogLevel to write</param>
    public async Task WriteLine(string message, LogLevel level = LogLevel.Information)
    {
        var code = level switch
        {
            LogLevel.Error => 31,
            LogLevel.Warning => 33,
            LogLevel.Debug => 37,
            LogLevel.Trace => 36,
            LogLevel.Information => 37,
            _ => 37,
        };
        
        await base.WriteLine($"\x1b[0;{code}m{message}");
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