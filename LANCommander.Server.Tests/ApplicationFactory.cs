using System.Data.Common;
using System.Text;
using LANCommander.SDK.Providers;
using LANCommander.Server.Data;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Tests.Mocks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ServerSettings = LANCommander.Server.Settings.Settings;

namespace LANCommander.Server.Tests;

public class ApplicationFactory<TProgram>
    : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            #region DatabaseContext
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == 
                     typeof(IDbContextOptionsConfiguration<DatabaseContext>));

            services.Remove(dbContextDescriptor);

            var dbConnectionDescriptor = services.SingleOrDefault(
                d => d.ServiceType ==
                     typeof(DbConnection));

            services.Remove(dbConnectionDescriptor);
            
            services.AddDbContextFactory<DatabaseContext>(optionsBuilder =>
            {
                optionsBuilder.UseInMemoryDatabase("Test");
            });
            #endregion
            
            #region IVersionProvider

            var versionProviderDescriptor = services.SingleOrDefault(
                d => typeof(IVersionProvider).IsAssignableFrom(d.ServiceType));
            
            services.Remove(versionProviderDescriptor);

            services.AddSingleton<IVersionProvider, VersionProviderMock>();
            #endregion
            
            #region GitHubService
            var gitHubServiceDescriptor = services.SingleOrDefault(
                d => typeof(IGitHubService).IsAssignableFrom(d.ServiceType));
            
            services.Remove(gitHubServiceDescriptor);

            services.AddSingleton(GitHubServiceMockFactory.Create());
            #endregion

            #region JWT signing key alignment
            // The server snapshots the JWT signing secret from configuration when AddIdentity runs,
            // but ValidateSettings regenerates the secret at startup whenever it is missing (which is
            // always the case in tests, where no persisted settings file provides one). Worse, the
            // secret can be regenerated more than once during a long test run (e.g. via repeated
            // settings saves triggered by storage-location setup), so pinning the validation key to a
            // single snapshot (even one taken after the first regeneration) can still go stale later
            // and start failing every bearer-token validation with IDX10517 (signature/kid mismatch).
            // Use a resolver that reads the *live* secret from the settings provider on every
            // validation instead of a one-time snapshot, so it can never drift out of sync with
            // whatever secret AuthenticationService most recently signed tokens with.
            services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>, AlignJwtSigningKey>();
            #endregion
        });
    }

    private sealed class AlignJwtSigningKey(SettingsProvider<ServerSettings> settingsProvider)
        : IPostConfigureOptions<JwtBearerOptions>
    {
        public void PostConfigure(string? name, JwtBearerOptions options)
        {
            options.TokenValidationParameters.IssuerSigningKeyResolver = (_, _, _, _) =>
            [
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(settingsProvider.CurrentValue.Server.Authentication.TokenSecret))
            ];
        }
    }
}