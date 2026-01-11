using AntDesign;
using LANCommander.UI.Providers;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace LANCommander.UI.Components;

public class BaseComponent : ComponentBase
{
    [Inject]
    protected IJSRuntime? JS { get; set; }
    
    [Inject]
    protected ScriptProvider ScriptProvider { get; set; }
    
    [Inject]
    protected NavigationManager NavigationManager { get; set; }
    
    [Inject]
    protected IMessageService MessageService { get; set; }
}