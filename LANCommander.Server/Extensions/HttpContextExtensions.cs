using Microsoft.AspNetCore.Authentication;

namespace LANCommander.Server.Extensions
{
    public static class HttpContextExtensions
    {
        public static async Task<AuthenticationScheme[]> GetExternalProvidersAsync(this HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var schemes = context.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>();

            return (from scheme in await schemes.GetAllSchemesAsync()
                    where !string.IsNullOrEmpty(scheme.DisplayName)
                    select scheme).ToArray();
        }

        public static async Task<bool> IsProviderSupportedAsync(this HttpContext context, string provider)
        {
            ArgumentNullException.ThrowIfNull(context);

            return (from scheme in await context.GetExternalProvidersAsync()
                    where string.Equals(scheme.Name, provider, StringComparison.OrdinalIgnoreCase)
                    select scheme).Any();
        }
    }
}
