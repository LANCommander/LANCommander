using LANCommander.Server.UI.Providers;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace LANCommander.Server.UI.Components;

public class BaseComponent : ComponentBase
{
    [Inject]
    protected IJSRuntime? JS { get; set; }

    [Inject]
    protected ScriptProvider ScriptProvider { get; set; }

    [Inject]
    protected NavigationManager NavigationManager { get; set; }
}
