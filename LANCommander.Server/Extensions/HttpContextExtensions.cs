using LANCommander.Server.Services;
using LANCommander.Server.Services.Models;
using Microsoft.AspNetCore.Authentication;

namespace LANCommander.Server.Extensions
{
    public static class HttpContextExtensions
    {
        public static IEnumerable<AuthenticationProvider> GetExternalProviders(this HttpContext context)
        {
            var settings = SettingService.GetSettings();
            
            return settings.Authentication.AuthenticationProviders;
        }

        public static async Task<bool> IsProviderSupportedAsync(this HttpContext context, string provider)
        {
            return true;
        }
    }
}
