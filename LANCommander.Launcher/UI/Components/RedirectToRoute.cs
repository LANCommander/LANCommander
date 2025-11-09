using Microsoft.AspNetCore.Components;

namespace LANCommander.Launcher.UI.Components;

public partial class RedirectToRoute : ComponentBase
{
    [Parameter]
    public required string Route { get; set; }
    
    [Inject]
    private NavigationManager NavigationManager { get; set; } = null!;

    protected override void OnInitialized()
    {
        var uri = new Uri(NavigationManager.Uri);
        
        if (uri.LocalPath != Route)
            NavigationManager.NavigateTo(Route);
    }
}