using LANCommander.UI.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using XtermBlazor;

namespace LANCommander.UI.Components;

public partial class Terminal : Xterm
{
    [Inject]
    IJSRuntime JS { get; set; }
    
    protected override async Task OnInitializedAsync()
    {
        await JS.ExecuteVoidAsync("RegisterXtermAddons");
        
        await base.OnInitializedAsync();
    }
}