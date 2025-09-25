namespace LANCommander.Launcher.Services;

public class ImportManagerService
{
    public delegate Task OnImportRequestedHandler();
    public event OnImportRequestedHandler OnImportRequested;

    public async Task RequestImport()
    {
        if (OnImportRequested != null)
            await OnImportRequested.Invoke();
    }
} 