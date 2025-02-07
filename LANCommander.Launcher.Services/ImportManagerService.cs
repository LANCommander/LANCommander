using System.Threading.Tasks;

namespace LANCommander.Launcher.Services;

public class ImportManagerService
{
    private readonly ImportService ImportService;

    public ImportManagerService(ImportService importService)
    {
        ImportService = importService;
    }

    public delegate Task OnImportRequestedHandler();
    public event OnImportRequestedHandler OnImportRequested;

    public async Task RequestImport()
    {
        if (OnImportRequested != null)
            await OnImportRequested.Invoke();
    }
} 