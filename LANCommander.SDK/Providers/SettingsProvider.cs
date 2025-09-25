using System.Threading.Tasks;
using LANCommander.SDK.Factories;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.Providers;

public class SettingsProvider(ApiRequestFactory apiRequestFactory)
{
    private ISettings _settings;

    public ISettings GetSettings()
    {
        return _settings;
    }
    
    public async Task LoadSettingsAsync()
    {
        
    }
}