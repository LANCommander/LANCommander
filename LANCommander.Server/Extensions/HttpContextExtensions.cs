using LANCommander.Server.Services;
using LANCommander.Server.Services.Models;
using LANCommander.Server.Settings.Models;
using Microsoft.AspNetCore.Authentication;

namespace LANCommander.Server.Extensions
{
    public static class HttpContextExtensions
    {
        public static IEnumerable<AuthenticationProvider> GetExternalProviders(this HttpContext context)
        {
            var settingsProvider = context.RequestServices.GetRequiredService<SettingsProvider<Settings.Settings>>();
            
            return settingsProvider.CurrentValue.Server.Authentication.AuthenticationProviders;
        }

        public static async Task<bool> IsProviderSupportedAsync(this HttpContext context, string provider)
        {
            return true;
        }
    }
}
